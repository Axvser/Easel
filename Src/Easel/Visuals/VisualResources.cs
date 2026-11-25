namespace Easel.Visuals;

/// <summary>Holds the named parts of one created <see cref="VisualControlTemplate"/> instance.</summary>
/// <remarks>
/// A scope belongs to one template instance, not to the template description. A template applied to many elements
/// therefore gets one scope per element, and a part name never resolves to another element's part.
/// </remarks>
public sealed class VisualNameScope
{
    // 多数模板实例没有 x:Name；字典按需创建，省掉每个实例一次空字典分配。
    private Dictionary<string, object>? _names;

    /// <summary>Registers <paramref name="value"/> under <paramref name="name"/>.</summary>
    /// <param name="name">The part name declared with <c>x:Name</c>.</param>
    /// <param name="value">The element or object the name refers to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    public void Register(string name, object value)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        if (value is null) throw new ArgumentNullException(nameof(value));
        (_names ??= new Dictionary<string, object>(StringComparer.Ordinal))[name] = value;
    }

    /// <summary>Returns the object registered under <paramref name="name"/>, or <see langword="null"/>.</summary>
    /// <param name="name">The part name to look up.</param>
    public object? Find(string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        return _names is not null && _names.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>Returns the object registered under <paramref name="name"/> when it is a <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The type the caller expects.</typeparam>
    /// <param name="name">The part name to look up.</param>
    public T? Find<T>(string name) where T : class
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        return _names is not null && _names.TryGetValue(name, out var value) ? value as T : null;
    }
}

/// <summary>Resolves named resources for the visual backend through the element's ancestor chain and its surface.</summary>
/// <remarks>
/// <para>
/// The nearest dictionary wins: the element's own <see cref="VisualElement.Resources"/>, then each ancestor's, then
/// <see cref="VisualHost.Resources"/>. A key missing from every dictionary throws instead of returning a default,
/// because a silently default value turns a typo into a blank control far from the mistake.
/// </para>
/// <para>
/// Generated code applies resource-valued attributes before the element is parented, so a static reference is
/// resolved lazily: <see cref="ApplyStatic{T}"/> retries as the ancestor chain grows, and <see cref="BindDynamic{T}"/>
/// keeps listening to every reachable dictionary so add, replace and remove all reach the target. Application code
/// normally uses <see cref="FindResource"/> or <see cref="Get{T}"/> instead.
/// </para>
/// </remarks>
public static class VisualResourceLookup
{
    /// <summary>Returns the resource stored under <paramref name="key"/>, or <see langword="null"/> when it is absent.</summary>
    /// <param name="element">The element whose resource chain is searched.</param>
    /// <param name="key">The resource key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="element"/> or <paramref name="key"/> is <see langword="null"/>.</exception>
    public static object? FindResource(VisualElement element, object key)
        => TryFindResource(element, key, out var value) ? value : null;

    internal static bool TryFindResource(VisualElement element, object key, out object? value)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (key is null) throw new ArgumentNullException(nameof(key));

        for (var current = element; current is not null; current = current.ParentElement)
        {
            if (current.Resources.TryGetValue(key, out value))
            {
                return true;
            }
        }

        if (element.Surface is { } surface && surface.Resources.TryGetValue(key, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>Returns the resource stored under <paramref name="key"/>, converted to <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The type the caller expects.</typeparam>
    /// <param name="element">The element whose resource chain is searched.</param>
    /// <param name="key">The resource key.</param>
    /// <exception cref="InvalidOperationException">The key is absent, or its value cannot become a <typeparamref name="T"/>.</exception>
    public static T Get<T>(VisualElement element, object key)
    {
        if (!TryFindResource(element, key, out var value))
        {
            throw new InvalidOperationException($"No resource named '{key}' was found in the visual element tree.");
        }

        return ConvertResource<T>(value, key);
    }

    /// <summary>Resolves a resource stored in a standalone <see cref="ResourceDictionary"/>.</summary>
    /// <typeparam name="T">The type the caller expects.</typeparam>
    /// <param name="dictionary">The dictionary to search, including its merged dictionaries.</param>
    /// <param name="key">The resource key.</param>
    /// <returns>The converted resource.</returns>
    /// <exception cref="InvalidOperationException">The key is absent, or its value cannot become a <typeparamref name="T"/>.</exception>
    /// <remarks>XAML resource dictionaries resolve their own <c>{StaticResource}</c> values before they join a
    /// visual element, so generated dictionary code uses this entry point instead of an element-chain lookup.</remarks>
    public static T GetFromDictionary<T>(ResourceDictionary dictionary, object key)
    {
        if (dictionary is null) throw new ArgumentNullException(nameof(dictionary));
        if (key is null) throw new ArgumentNullException(nameof(key));

        if (!dictionary.TryGetValue(key, out var value))
        {
            throw new InvalidOperationException($"No resource named '{key}' was found in the dictionary.");
        }

        return ConvertResource<T>(value, key);
    }

    /// <summary>
    /// Applies a static resource once it is reachable, retrying while the element's ancestor chain grows.
    /// </summary>
    /// <typeparam name="T">The type the target property expects.</typeparam>
    /// <param name="element">The element the value is applied to; also the anchor of the resource chain.</param>
    /// <param name="key">The resource key.</param>
    /// <param name="apply">Receives the converted value.</param>
    /// <param name="targetToken">Identifies the target property; a later resource reference for the same token
    /// supersedes this one, so a static value applied after a dynamic reference wins.</param>
    /// <remarks>
    /// A static reference is not a live link: the subscription ends as soon as the key resolves. A key that never
    /// resolves leaves no value behind, matching a StaticResource that was not yet declared when the element was built.
    /// </remarks>
    public static void ApplyStatic<T>(VisualElement element, object key, Action<T> apply, object? targetToken = null)
    {
        if (apply is null) throw new ArgumentNullException(nameof(apply));
        new ResourceBinding(
            element,
            key,
            value => apply(ConvertResource<T>(value, key)),
            dynamic: false,
            targetToken).Start();
    }

    /// <summary>Applies a resource now and again whenever a reachable dictionary adds, replaces or removes the key.</summary>
    /// <typeparam name="T">The type the target property expects.</typeparam>
    /// <param name="element">The element the value is applied to; also the anchor of the resource chain.</param>
    /// <param name="key">The resource key.</param>
    /// <param name="apply">Receives the converted value on every update.</param>
    /// <param name="onMissing">
    /// Invoked after a previously resolved key disappears from every dictionary; clear the target's value here. A key
    /// that was never present does not invoke it, so a later declaration still applies normally.
    /// </param>
    /// <param name="targetToken">Identifies the target property; a later resource reference for the same token
    /// supersedes this one, matching "last reference wins" for static/dynamic mixes.</param>
    /// <returns>A subscription released when the element is disposed, or by the caller earlier.</returns>
    public static IDisposable BindDynamic<T>(
        VisualElement element,
        object key,
        Action<T> apply,
        Action? onMissing = null,
        object? targetToken = null)
    {
        if (apply is null) throw new ArgumentNullException(nameof(apply));
        return new ResourceBinding(
            element,
            key,
            value => apply(ConvertResource<T>(value, key)),
            dynamic: true,
            targetToken,
            onMissing).Start();
    }

    private static T ConvertResource<T>(object? value, object key)
    {
        if (value is T typed)
        {
            return typed;
        }

        var converted = XamlRuntime.ConvertValue(value, typeof(T));
        return converted is T result
            ? result
            : throw new InvalidOperationException($"Resource '{key}' cannot be converted to '{typeof(T)}'.");
    }
}

// 静态引用只在解析成功后收尾；动态引用一直订阅。两者都在元素父链变化时重新挂到新的祖先字典上。
internal sealed class ResourceBinding : IDisposable
{
    private readonly VisualElement _element;
    private readonly object _key;
    private readonly Action<object?> _apply;
    private readonly bool _dynamic;
    private readonly Action? _onMissing;
    private readonly object? _targetToken;
    private readonly List<VisualElement> _chain = [];
    private readonly List<ResourceDictionary> _dictionaries = [];
    private VisualElement? _surfaceRoot;
    private ResourceDictionary? _rootResources;
    private ResourceDictionary? _surfaceResources;
    private ResourceDictionary? _source;
    private bool _hadValue;
    private bool _disposed;

    internal ResourceBinding(
        VisualElement element,
        object key,
        Action<object?> apply,
        bool dynamic,
        object? targetToken = null,
        Action? onMissing = null)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _dynamic = dynamic;
        _targetToken = targetToken;
        _onMissing = onMissing;
    }

    internal ResourceBinding Start()
    {
        _element.TrackResourceBinding(this);
        if (_targetToken is not null)
        {
            // 同一目标属性上的后一个资源引用接管前一个：静态/动态混用时以最后写入的为准。
            _element.RegisterResourceBinding(_targetToken, this);
        }

        Refresh();
        return this;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_targetToken is not null)
        {
            _element.UntrackResourceBinding(_targetToken, this);
        }

        Unsubscribe();
    }

    private void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        UpdateChain();
        AttachRootEvents();
        RefreshValue();
    }

    private void RefreshValue()
    {
        var surface = _element.Surface;
        var found = Resolve(surface, out var value, out var source);
        SyncDictionarySubscriptions(surface, source);
        if (found)
        {
            ApplyValue(value);
            return;
        }

        // 曾经解析过、现在被移除：动态引用回到默认值；从未解析过则保持等待，稍后再声明的资源仍会生效。
        if (_dynamic && _hadValue)
        {
            _hadValue = false;
            _onMissing?.Invoke();
        }
    }

    // 父链通常只在根上追加祖先（模板部件挂到列表/弹层）。按公共前缀增量地退订/订阅，
    // 避免每次父级变化都把整条链退订再重挂；链被截断时也只摘掉消失的尾段。
    private void UpdateChain()
    {
        var index = 0;
        var current = _element;
        while (current is not null && index < _chain.Count && ReferenceEquals(_chain[index], current))
        {
            index++;
            current = current.ParentElement;
        }

        if (current is null && index == _chain.Count)
        {
            return;
        }

        for (var tail = _chain.Count - 1; tail >= index; tail--)
        {
            _chain[tail].ParentChanged -= OnChainChanged;
        }

        if (index < _chain.Count)
        {
            _chain.RemoveRange(index, _chain.Count - index);
        }

        while (current is not null)
        {
            _chain.Add(current);
            current.ParentChanged += OnChainChanged;
            current = current.ParentElement;
        }
    }

    private void AttachRootEvents()
    {
        var root = _chain.Count > 0 ? _chain[_chain.Count - 1] : null;
        if (ReferenceEquals(root, _surfaceRoot))
        {
            return;
        }

        if (_surfaceRoot is not null)
        {
            _surfaceRoot.SurfaceChanged -= OnChainChanged;
        }

        _surfaceRoot = root;
        if (root is not null)
        {
            root.SurfaceChanged += OnChainChanged;
        }
    }

    // 一次向上遍历同时取值和来源字典；表面资源是链之后的最后一站。
    private bool Resolve(VisualHost? surface, out object? value, out ResourceDictionary? source)
    {
        for (var current = _element; current is not null; current = current.ParentElement)
        {
            if (current.Resources.TryFindSource(_key, out value, out source))
            {
                return true;
            }
        }

        if (surface is not null && surface.Resources.TryGetValue(_key, out value))
        {
            source = surface.Resources;
            return true;
        }

        value = null;
        source = null;
        return false;
    }

    // 只为可能提供/影响这个 key 的字典挂订阅，并摘掉链变化后已不可达的旧订阅。
    // 订阅集合只在链根、表面或来源换人时才变；同一次构造里的重复父级事件直接跳过。
    private void SyncDictionarySubscriptions(VisualHost? surface, ResourceDictionary? source)
    {
        var root = _chain.Count > 0 ? _chain[_chain.Count - 1] : null;
        var rootResources = root?.Resources;
        var surfaceResources = surface?.Resources;
        if (ReferenceEquals(rootResources, _rootResources)
            && ReferenceEquals(surfaceResources, _surfaceResources)
            && ReferenceEquals(source, _source))
        {
            return;
        }

        var elementResources = _element.Resources;
        AddDictionary(elementResources);
        if (rootResources is not null)
        {
            AddDictionary(rootResources);
        }

        if (source is not null)
        {
            AddDictionary(source);
        }

        if (surfaceResources is not null)
        {
            AddDictionary(surfaceResources);
        }

        for (var index = _dictionaries.Count - 1; index >= 0; index--)
        {
            var dictionary = _dictionaries[index];
            if (!ReferenceEquals(dictionary, elementResources)
                && !ReferenceEquals(dictionary, rootResources)
                && !ReferenceEquals(dictionary, source)
                && !ReferenceEquals(dictionary, surfaceResources))
            {
                RemoveDictionary(dictionary);
            }
        }

        _rootResources = rootResources;
        _surfaceResources = surfaceResources;
        _source = source;
    }

    private void ApplyValue(object? value)
    {
        _hadValue = true;
        _apply(value);
        if (!_dynamic)
        {
            // 静态资源一次性生效；之后字典再变化也不再改写属性。
            Dispose();
        }
    }

    private void AddDictionary(ResourceDictionary dictionary)
    {
        if (_dictionaries.Contains(dictionary))
        {
            return;
        }

        _dictionaries.Add(dictionary);
        dictionary.Changed += OnDictionaryChanged;
    }

    private void RemoveDictionary(ResourceDictionary dictionary)
    {
        if (_dictionaries.Remove(dictionary))
        {
            dictionary.Changed -= OnDictionaryChanged;
        }
    }

    private void Unsubscribe()
    {
        foreach (var current in _chain)
        {
            current.ParentChanged -= OnChainChanged;
        }

        _chain.Clear();

        if (_surfaceRoot is not null)
        {
            _surfaceRoot.SurfaceChanged -= OnChainChanged;
            _surfaceRoot = null;
        }

        foreach (var dictionary in _dictionaries)
        {
            dictionary.Changed -= OnDictionaryChanged;
        }

        _dictionaries.Clear();
        _rootResources = null;
        _surfaceResources = null;
        _source = null;
    }

    private void OnChainChanged(object? sender, EventArgs e) => Refresh();

    private void OnDictionaryChanged(object? changedKey)
    {
        if (_disposed)
        {
            return;
        }

        if (changedKey is null)
        {
            // 合并字典列表或整个字典变了：命中的来源可能换人，重扫整条链。
            Refresh();
            return;
        }

        if (Equals(changedKey, _key))
        {
            // 已知字典里的具体键变化不会改变父链，只重算值（来源可能因此换到别的字典）。
            RefreshValue();
        }
    }
}
