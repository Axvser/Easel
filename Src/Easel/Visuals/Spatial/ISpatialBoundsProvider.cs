using System.ComponentModel;
using System.Drawing;

namespace Easel.Visuals.Spatial;

/// <summary>Supplies the spatial bounds of an item stored in a <see cref="SpatialGridHashMap{T}"/>.</summary>
/// <remarks>
/// Implementations raise <see cref="INotifyPropertyChanged.PropertyChanged"/> with the property name
/// <c>Bounds</c> when the rectangle moves or changes size, so the index can re-file the item.
/// </remarks>
public interface ISpatialBoundsProvider : INotifyPropertyChanged
{
    /// <summary>The item's rectangle in the indexed coordinate space.</summary>
    RectangleF Bounds { get; }
}
