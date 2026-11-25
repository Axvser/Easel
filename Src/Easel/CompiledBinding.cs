using System.Collections;

namespace Easel;

/// <summary>
/// Reads a compiled binding path from a source object and reports the value, the nodes that need change
/// tracking, and a writer for two-way bindings.
/// </summary>
/// <param name="source">The object the binding path starts from.</param>
/// <param name="nodes">Receives every object that owns a path segment; the binding subscribes to their property changes.</param>
/// <param name="value">The resolved leaf value.</param>
/// <param name="valueType">The declared type of the leaf, used for conversion and write-back.</param>
/// <param name="write">Writes a converted value back to the leaf, or <see langword="null"/> when the path cannot be written.</param>
/// <returns><see langword="true"/> when the path was resolved.</returns>
/// <remarks>
/// The XAML generator emits one implementation per compiled binding. The reader uses direct property access, so it
/// never consults <see cref="System.ComponentModel.TypeDescriptor"/>.
/// </remarks>
public delegate bool CompiledBindingReader(
    object? source,
    List<object?> nodes,
    out object? value,
    out Type valueType,
    out Action<object?>? write);

/// <summary>
/// A <see cref="Binding"/> whose path the XAML generator compiles into direct property access instead of resolving
/// it at run time through <see cref="System.ComponentModel.TypeDescriptor"/>.
/// </summary>
/// <remarks>
/// <para>
/// The generator emits this binding for data-context paths when <c>x:CompileBindings="True"</c> and an
/// <c>x:DataType</c> are in scope. Everything else on <see cref="Binding"/> — mode, converters, formatting,
/// fallback values and validation — behaves the same way.
/// </para>
/// <para>
/// Bindings with an explicit <see cref="Binding.Source"/>, <see cref="Binding.ElementName"/>,
/// <see cref="Binding.RelativeSource"/> or <see cref="TemplateBinding"/> stay runtime bindings; the compiler
/// does not use <see cref="Binding.Path"/> in a compiled reader.
/// </para>
/// </remarks>
public sealed class CompiledBinding : Binding
{
    /// <summary>Creates a compiled binding over <paramref name="path"/>.</summary>
    /// <param name="path">The source path; it is kept for diagnostics and is not parsed by the binding engine.</param>
    /// <param name="read">The generated reader.</param>
    /// <exception cref="ArgumentNullException"><paramref name="read"/> is <see langword="null"/>.</exception>
    public CompiledBinding(string path, CompiledBindingReader read)
        : base(path)
    {
        Read = read ?? throw new ArgumentNullException(nameof(read));
    }

    /// <summary>The generated reader that walks the path.</summary>
    public CompiledBindingReader Read { get; }

    /// <summary>Returns the declared type of <paramref name="value"/> without evaluating a conversion.</summary>
    /// <typeparam name="T">The static type of the expression; inferred by the caller.</typeparam>
    /// <param name="value">The value whose declared type is needed.</param>
    /// <returns><c>typeof(T)</c>, including <see cref="Nullable{T}"/> for nullable value types.</returns>
    public static Type TypeOf<T>(T value) => typeof(T);

    /// <summary>Reports whether a path intermediate is non-null; returns <see langword="true"/> for value types.</summary>
    /// <typeparam name="T">The static type of <paramref name="value"/>.</typeparam>
    /// <param name="value">The intermediate to test.</param>
    /// <returns><see langword="false"/> only for a null reference or an empty <see cref="Nullable{T}"/>.</returns>
    public static bool HasValue<T>(T value) => value is not null;

    /// <summary>Converts <paramref name="value"/> to the declared property type.</summary>
    /// <typeparam name="T">The property type; inferred from <paramref name="current"/>.</typeparam>
    /// <param name="current">The property's current value; used only to infer <typeparamref name="T"/>.</param>
    /// <param name="value">The incoming target value.</param>
    /// <returns>The value as <typeparamref name="T"/>, converting plain scalars and enums when needed.</returns>
    public static T Coerce<T>(T current, object? value)
    {
        _ = current;
        return value is T typed ? typed : (T)XamlRuntime.ConvertValue(value, typeof(T))!;
    }

    /// <summary>Creates a writer that assigns through a typed property accessor.</summary>
    /// <typeparam name="TOwner">The object that owns the leaf property.</typeparam>
    /// <param name="owner">The resolved leaf owner.</param>
    /// <param name="write">The generated assignment.</param>
    /// <returns>A writer, or <see langword="null"/> when <paramref name="owner"/> is null.</returns>
    public static Action<object?>? CreateWriter<TOwner>(TOwner owner, Action<TOwner, object?> write)
        where TOwner : class
        => owner is null ? null : value => write(owner, value);

    /// <summary>Reads one element from an array or list indexer.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array to read.</param>
    /// <param name="index">The element index.</param>
    /// <param name="value">Receives the element.</param>
    /// <returns><see langword="true"/> when the index is in range.</returns>
    public static bool TryGetIndex<T>(T[] array, int index, out T value)
    {
        if (array is not null && (uint)index < (uint)array.Length)
        {
            value = array[index];
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>Reads one element from a generic list indexer.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="list">The list to read.</param>
    /// <param name="index">The element index.</param>
    /// <param name="value">Receives the element.</param>
    /// <returns><see langword="true"/> when the index is in range.</returns>
    public static bool TryGetIndex<T>(IList<T> list, int index, out T value)
    {
        if (list is not null && (uint)index < (uint)list.Count)
        {
            value = list[index];
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>Reads one entry from a generic dictionary indexer.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary to read.</param>
    /// <param name="key">The entry key.</param>
    /// <param name="value">Receives the entry value.</param>
    /// <returns><see langword="true"/> when the key exists.</returns>
    public static bool TryGetIndex<TKey, TValue>(
        IDictionary<TKey, TValue> dictionary,
        TKey key,
        out TValue value)
        where TKey : notnull
    {
        if (dictionary is not null && key is not null && dictionary.TryGetValue(key, out value!))
        {
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>Reads an element when the container's static type is only <see cref="object"/> or a custom collection.</summary>
    /// <param name="container">The collection instance.</param>
    /// <param name="key">An integer index or a dictionary key.</param>
    /// <param name="value">Receives the element.</param>
    /// <returns><see langword="true"/> when the container is a supported collection and the key resolves.</returns>
    /// <remarks>
    /// The non-generic collection paths live here rather than in public overloads: a public <see cref="IList"/>
    /// overload would make <see cref="IList{T}"/> call sites ambiguous.
    /// </remarks>
    public static bool TryGetIndex(object? container, object key, out object? value)
    {
        if (key is int index && container is IList list)
        {
            if (list is not null && (uint)index < (uint)list.Count)
            {
                value = list[index];
                return true;
            }

            value = null;
            return false;
        }

        if (container is IDictionary dictionary && key is not null && dictionary.Contains(key))
        {
            value = dictionary[key];
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>Creates a writer for an array element.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array to write.</param>
    /// <param name="index">The element index.</param>
    /// <returns>A writer, or <see langword="null"/> when <paramref name="array"/> is null.</returns>
    public static Action<object?>? CreateIndexWriter<T>(T[] array, int index)
        => array is null
            ? null
            : value =>
            {
                if ((uint)index < (uint)array.Length)
                {
                    array[index] = Coerce(array[index], value);
                }
            };

    /// <summary>Creates a writer for a generic list element.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="list">The list to write.</param>
    /// <param name="index">The element index.</param>
    /// <returns>A writer, or <see langword="null"/> when the list is null or read-only.</returns>
    public static Action<object?>? CreateIndexWriter<T>(IList<T> list, int index)
        => list is null || list.IsReadOnly
            ? null
            : value =>
            {
                if ((uint)index < (uint)list.Count)
                {
                    list[index] = Coerce(list[index], value);
                }
            };

    /// <summary>Creates a writer for a generic dictionary entry.</summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary to write.</param>
    /// <param name="key">The entry key.</param>
    /// <returns>A writer, or <see langword="null"/> when the dictionary is null, read-only or missing the key.</returns>
    public static Action<object?>? CreateIndexWriter<TKey, TValue>(
        IDictionary<TKey, TValue> dictionary,
        TKey key)
        where TKey : notnull
        => dictionary is null || dictionary.IsReadOnly || !dictionary.ContainsKey(key)
            ? null
            : value => dictionary[key] = Coerce(dictionary[key], value);

    /// <summary>Creates a writer when the container's static type is only <see cref="object"/> or a custom collection.</summary>
    /// <param name="container">The collection instance.</param>
    /// <param name="key">An integer index or a dictionary key.</param>
    /// <returns>A writer, or <see langword="null"/> when no supported writable collection matches.</returns>
    /// <remarks>
    /// The non-generic collection paths live here rather than in public overloads: a public <see cref="IList"/>
    /// overload would make <see cref="IList{T}"/> call sites ambiguous.
    /// </remarks>
    public static Action<object?>? CreateIndexWriter(object? container, object key)
    {
        if (key is int index && container is IList list && !list.IsReadOnly)
        {
            return value =>
            {
                if ((uint)index < (uint)list.Count)
                {
                    list[index] = value;
                }
            };
        }

        if (container is IDictionary dictionary
            && key is not null
            && !dictionary.IsReadOnly
            && dictionary.Contains(key))
        {
            return value => dictionary[key] = value;
        }

        return null;
    }
}
