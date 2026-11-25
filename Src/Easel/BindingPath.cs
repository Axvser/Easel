using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;

namespace Easel;

/// <summary>
/// Resolves one dot-separated binding path, including simple indexers, and exposes the leaf so a two-way binding can
/// write back through the same shape it read.
/// </summary>
internal sealed class BindingPath
{
    // 每次更新都重解析路径，TypeDescriptor 会重复新建描述符集合；按 (类型, 段名) 缓存。
    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyDescriptor?> s_descriptors = new();

    private readonly string[] _segments;
    private readonly CompiledBindingReader? _compiled;
    private BindingLeaf? _leaf;
    private object? _leafInstance;
    private PropertyDescriptor? _leafDescriptor;

    public BindingPath(string? path, CompiledBindingReader? compiled = null)
    {
        Raw = path ?? string.Empty;
        _compiled = compiled;
        _segments = string.IsNullOrEmpty(Raw) || Raw == "."
            ? []
            : Raw.Split('.');
    }

    public string Raw { get; }

    public bool IsEmpty => _segments.Length == 0;

    /// <summary>The first path segment without its indexer, or <see langword="null"/> for an empty path.</summary>
    public string? FirstSegment
    {
        get
        {
            if (_segments.Length == 0)
            {
                return null;
            }

            var segment = _segments[0];
            var open = segment.IndexOf('[');
            return open >= 0 ? segment.Substring(0, open) : segment;
        }
    }

    public bool TryResolve(
        object? root,
        out object? value,
        out List<object?> nodes,
        out BindingLeaf? leaf)
    {
        value = null;
        nodes = [];
        leaf = null;

        if (_compiled is not null)
        {
            if (root is null
                || !_compiled(root, nodes, out value, out var compiledType, out var write))
            {
                return false;
            }

            var compiledValue = value;
            leaf = new BindingLeaf(compiledType, () => compiledValue, write);
            return true;
        }

        if (IsEmpty)
        {
            value = root;
            if (root is not null)
            {
                nodes.Add(root);
            }

            return root is not null;
        }

        var current = root;
        if (current is null)
        {
            return false;
        }

        nodes.Add(current);

        for (var index = 0; index < _segments.Length; index++)
        {
            if (!TryParseSegment(_segments[index], out var name, out var indexText, out var hasIndex))
            {
                return false;
            }

            if (name.Length > 0)
            {
                var descriptor = FindDescriptor(current, name);
                if (descriptor is null)
                {
                    return false;
                }

                if (!hasIndex && index == _segments.Length - 1)
                {
                    value = descriptor.GetValue(current);
                    // 同一目标对象上的重复解析复用同一个叶子；源对象换了才新建。
                    if (_leaf is not null
                        && ReferenceEquals(_leafInstance, current)
                        && ReferenceEquals(_leafDescriptor, descriptor))
                    {
                        leaf = _leaf;
                    }
                    else
                    {
                        leaf = new BindingLeaf(descriptor, current);
                        _leaf = leaf;
                        _leafInstance = current;
                        _leafDescriptor = descriptor;
                    }

                    return true;
                }

                current = descriptor.GetValue(current);
                if (current is null)
                {
                    return false;
                }
            }

            if (hasIndex)
            {
                nodes.Add(current);

                if (!TryGetIndexed(current, indexText, out var indexed, out var canWrite, out var setter))
                {
                    return false;
                }

                if (index == _segments.Length - 1)
                {
                    value = indexed;
                    leaf = new BindingLeaf(
                        indexed?.GetType() ?? typeof(object),
                        canWrite ? () => indexed : null,
                        canWrite ? setter : null);
                    return true;
                }

                current = indexed;
                if (current is null)
                {
                    return false;
                }
            }

            nodes.Add(current);
        }

        return false;
    }

    // 实现 ICustomTypeDescriptor 的对象可能按实例给出不同属性集，这类不缓存；其余按类型缓存。
    private static PropertyDescriptor? FindDescriptor(object target, string name)
    {
        var type = target.GetType();
        if (typeof(ICustomTypeDescriptor).IsAssignableFrom(type))
        {
            return TypeDescriptor.GetProperties(target).Find(name, ignoreCase: false);
        }

        var key = (type, name);
        if (s_descriptors.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var descriptor = TypeDescriptor.GetProperties(type).Find(name, ignoreCase: false);
        s_descriptors[key] = descriptor;
        return descriptor;
    }

    private static bool TryParseSegment(string segment, out string name, out string indexText, out bool hasIndex)
    {
        name = segment;
        indexText = string.Empty;
        hasIndex = false;

        var open = segment.IndexOf('[');
        if (open < 0)
        {
            return segment.Length > 0;
        }

        if (!segment.EndsWith("]", StringComparison.Ordinal))
        {
            return false;
        }

        name = segment.Substring(0, open);
        indexText = segment.Substring(open + 1, segment.Length - open - 2).Trim();
        hasIndex = true;
        return name.Length > 0 || indexText.Length > 0;
    }

    private static bool TryGetIndexed(
        object? container,
        string indexText,
        out object? value,
        out bool canWrite,
        out Action<object?>? setter)
    {
        value = null;
        canWrite = false;
        setter = null;
        if (container is null)
        {
            return false;
        }

        var keyText = indexText.Trim('"', '\'');

        if (container is IList list)
        {
            if (int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                && index >= 0
                && index < list.Count)
            {
                value = list[index];
                canWrite = !list.IsReadOnly && !list.IsFixedSize;
                setter = newValue => list[index] = newValue;
                return true;
            }

            return false;
        }

        if (container is IDictionary dictionary)
        {
            object key = int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)
                ? numeric
                : keyText;

            if (dictionary.Contains(key))
            {
                value = dictionary[key];
                canWrite = !dictionary.IsReadOnly && !dictionary.IsFixedSize;
                setter = newValue => dictionary[key] = newValue;
                return true;
            }
        }

        return false;
    }
}

/// <summary>The writable leaf of a resolved <see cref="BindingPath"/>.</summary>
internal sealed class BindingLeaf
{
    private readonly Func<object?>? _getter;
    private readonly Action<object?>? _setter;
    private readonly PropertyDescriptor? _descriptor;
    private readonly object? _instance;

    public BindingLeaf(Type valueType, Func<object?>? getter, Action<object?>? setter)
    {
        ValueType = valueType;
        _getter = getter;
        _setter = setter;
    }

    // 普通属性叶子直接持有描述符与目标，读写不经过闭包。
    public BindingLeaf(PropertyDescriptor descriptor, object instance)
    {
        ValueType = descriptor.PropertyType;
        _descriptor = descriptor;
        _instance = instance;
    }

    public Type ValueType { get; }

    public bool CanWrite => _descriptor is not null ? !_descriptor.IsReadOnly : _setter is not null;

    public object? GetValue() => _descriptor is not null
        ? _descriptor.GetValue(_instance)
        : _getter?.Invoke();

    public void SetValue(object? value)
    {
        if (_descriptor is not null)
        {
            if (!_descriptor.IsReadOnly)
            {
                _descriptor.SetValue(_instance, value);
            }

            return;
        }

        _setter?.Invoke(value);
    }
}
