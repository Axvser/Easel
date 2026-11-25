using System.Collections;
using System.Collections.ObjectModel;

namespace Easel;

/// <summary>
/// The resource store behind <see cref="Easel.Visuals.VisualElement.Resources"/> and <c>VisualHost.Resources</c>. It is a normal dictionary plus a change signal so
/// implicit styles can be re-evaluated when the resource with their type key is added, replaced or removed.
/// </summary>
/// <remarks>
/// A lookup checks this dictionary first and then each entry of <see cref="MergedDictionaries"/>, last added
/// first. A change inside a merged dictionary is forwarded through <see cref="Changed"/> as the merged key, so
/// implicit styles and dynamic resources that resolved through it refresh in place.
/// </remarks>
public class ResourceDictionary : IDictionary<object, object?>
{
    private readonly Dictionary<object, object?> _items = [];

    /// <summary>Creates an empty dictionary.</summary>
    public ResourceDictionary()
    {
        MergedDictionaries = new MergedDictionaryCollection(this);
    }

    /// <summary>Raised after a key is added, replaced or removed; <see langword="null"/> means the dictionary was cleared.</summary>
    public event Action<object?>? Changed;

    // 共享字典上会有大量动态资源订阅；内部观察者走列表，避免多播事件逐个增删时重建委托数组。
    private List<Action<object?>>? _changedObservers;

    internal void AddChangedObserver(Action<object?> observer)
        => (_changedObservers ??= new List<Action<object?>>()).Add(observer);

    internal void RemoveChangedObserver(Action<object?> observer)
        => _changedObservers?.Remove(observer);

    private void RaiseChanged(object? key)
    {
        Changed?.Invoke(key);
        var observers = _changedObservers;
        if (observers is null)
        {
            return;
        }

        // 观察者可能在回调里增删订阅；倒序并复查边界。
        for (var index = observers.Count - 1; index >= 0; index--)
        {
            if (index < observers.Count)
            {
                observers[index](key);
            }
        }
    }

    /// <summary>The dictionaries consulted after this one when a key is not found locally.</summary>
    /// <remarks>Merged entries are searched last added first, matching WPF's fallback order.</remarks>
    public IList<ResourceDictionary> MergedDictionaries { get; }

    /// <inheritdoc />
    public object? this[object key]
    {
        get => _items[key];
        set
        {
            _items[key] = value;
            RaiseChanged(key);
        }
    }

    /// <inheritdoc />
    public ICollection<object> Keys => _items.Keys;

    /// <inheritdoc />
    public ICollection<object?> Values => _items.Values;

    /// <inheritdoc />
    public int Count => _items.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public void Add(object key, object? value)
    {
        _items.Add(key, value);
        RaiseChanged(key);
    }

    /// <inheritdoc />
    public bool ContainsKey(object key) => TryGetValue(key, out _);

    /// <inheritdoc />
    public bool Remove(object key)
    {
        var removed = _items.Remove(key);
        if (removed)
        {
            RaiseChanged(key);
        }

        return removed;
    }

    /// <inheritdoc />
    /// <remarks>Falls back to <see cref="MergedDictionaries"/> when the key is not stored locally.</remarks>
    public bool TryGetValue(object key, out object? value) => TryFindSource(key, out value, out _);

    // visited 集合按线程复用：只有走进合并字典才需要它，本地命中仍走短路径。
    [ThreadStatic] private static HashSet<ResourceDictionary>? t_visited;
    [ThreadStatic] private static int t_visitedDepth;

    internal bool TryFindSource(object key, out object? value, out ResourceDictionary? source)
    {
        if (_items.TryGetValue(key, out value))
        {
            source = this;
            return true;
        }

        if (MergedDictionaries.Count == 0)
        {
            value = null;
            source = null;
            return false;
        }

        var visited = t_visited ??= new HashSet<ResourceDictionary>();
        var top = t_visitedDepth == 0;
        if (top)
        {
            visited.Clear();
        }

        t_visitedDepth++;
        try
        {
            return TryFindSourceCore(key, visited, out value, out source);
        }
        finally
        {
            t_visitedDepth--;
            if (top)
            {
                visited.Clear();
            }
        }
    }

    private bool TryFindSourceCore(
        object key,
        HashSet<ResourceDictionary> visited,
        out object? value,
        out ResourceDictionary? source)
    {
        if (_items.TryGetValue(key, out value))
        {
            source = this;
            return true;
        }

        if (!visited.Add(this))
        {
            // 合并链可能成环；已访问过的字典不再展开。
            value = null;
            source = null;
            return false;
        }

        for (var index = MergedDictionaries.Count - 1; index >= 0; index--)
        {
            var merged = MergedDictionaries[index];
            if (merged is not null && merged.TryFindSourceCore(key, visited, out value, out source))
            {
                return true;
            }
        }

        value = null;
        source = null;
        return false;
    }

    /// <inheritdoc />
    public void Clear()
    {
        _items.Clear();
        RaiseChanged(null);
    }

    void ICollection<KeyValuePair<object, object?>>.Add(KeyValuePair<object, object?> item)
        => Add(item.Key, item.Value);

    bool ICollection<KeyValuePair<object, object?>>.Contains(KeyValuePair<object, object?> item)
        => ((ICollection<KeyValuePair<object, object?>>)_items).Contains(item);

    void ICollection<KeyValuePair<object, object?>>.CopyTo(KeyValuePair<object, object?>[] array, int arrayIndex)
        => ((ICollection<KeyValuePair<object, object?>>)_items).CopyTo(array, arrayIndex);

    bool ICollection<KeyValuePair<object, object?>>.Remove(KeyValuePair<object, object?> item)
    {
        var removed = ((ICollection<KeyValuePair<object, object?>>)_items).Remove(item);
        if (removed)
        {
            RaiseChanged(item.Key);
        }

        return removed;
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<object, object?>> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void OnMergedChanged(object? changedKey) => RaiseChanged(changedKey);

    /// <summary>A request to raise <see cref="Changed"/> with <see langword="null"/>, used by the merged list.</summary>
    private void NotifyMergedListChanged() => RaiseChanged(null);

    /// <summary>The mutable merged list; it forwards each dictionary's own change signal through its owner.</summary>
    private sealed class MergedDictionaryCollection : Collection<ResourceDictionary>
    {
        private readonly ResourceDictionary _owner;

        public MergedDictionaryCollection(ResourceDictionary owner)
        {
            _owner = owner;
        }

        protected override void InsertItem(int index, ResourceDictionary item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));
            base.InsertItem(index, item);
            item.Changed += _owner.OnMergedChanged;
            _owner.NotifyMergedListChanged();
        }

        protected override void SetItem(int index, ResourceDictionary item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));
            this[index].Changed -= _owner.OnMergedChanged;
            base.SetItem(index, item);
            item.Changed += _owner.OnMergedChanged;
            _owner.NotifyMergedListChanged();
        }

        protected override void RemoveItem(int index)
        {
            this[index].Changed -= _owner.OnMergedChanged;
            base.RemoveItem(index);
            _owner.NotifyMergedListChanged();
        }

        protected override void ClearItems()
        {
            foreach (var item in this)
            {
                item.Changed -= _owner.OnMergedChanged;
            }

            base.ClearItems();
            _owner.NotifyMergedListChanged();
        }
    }
}
