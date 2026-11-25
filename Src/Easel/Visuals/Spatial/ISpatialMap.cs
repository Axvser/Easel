using System.Drawing;

namespace Easel.Visuals.Spatial;

/// <summary>Inserts, removes and queries items by their spatial bounds.</summary>
/// <typeparam name="T">The indexed item type.</typeparam>
public interface ISpatialMap<T> where T : class, ISpatialBoundsProvider
{
    /// <summary>Adds <paramref name="item"/>; an item already present is ignored.</summary>
    /// <param name="item">The item to index.</param>
    void Insert(T item);

    /// <summary>Removes <paramref name="item"/> when it is indexed.</summary>
    /// <param name="item">The item to remove.</param>
    void Remove(T item);

    /// <summary>Enumerates the items whose bounds intersect <paramref name="viewport"/>.</summary>
    /// <param name="viewport">The query rectangle.</param>
    IEnumerable<T> Query(RectangleF viewport);

    /// <summary>Copies the items that intersect <paramref name="viewport"/> into <paramref name="buffer"/>.</summary>
    /// <param name="viewport">The query rectangle.</param>
    /// <param name="buffer">The destination list; the caller clears it first.</param>
    void CollectInto(RectangleF viewport, List<T> buffer);

    /// <summary>Removes every item.</summary>
    void Clear();

    /// <summary>The smallest rectangle covering every indexed item, or <see cref="RectangleF.Empty"/> when empty.</summary>
    RectangleF Bounds { get; }
}
