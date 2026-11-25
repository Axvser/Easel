using System.ComponentModel;
using System.Drawing;

namespace Easel.Visuals.Spatial;

/// <summary>Exposes a <see cref="VisualElement"/>'s absolute bounds to a spatial index.</summary>
/// <remarks>
/// <see cref="Bounds"/> is read from <see cref="VisualElement.AbsoluteBounds"/> on demand. The layout pass does
/// not notify visual bounds changes, so a host calls <see cref="Refresh"/> after arranging the subtree to publish
/// a change to the index.
/// </remarks>
public sealed class VisualSpatialBoundsProvider : ISpatialBoundsProvider
{
    private RectangleF _lastBounds;
    private bool _hasLastBounds;

    /// <summary>Wraps <paramref name="visual"/> for spatial indexing.</summary>
    /// <param name="visual">The visual whose absolute bounds are indexed.</param>
    public VisualSpatialBoundsProvider(VisualElement visual)
    {
        Visual = visual ?? throw new ArgumentNullException(nameof(visual));
    }

    /// <summary>The indexed visual.</summary>
    public VisualElement Visual { get; }

    /// <summary>The visual's current absolute bounds.</summary>
    public RectangleF Bounds => Visual.AbsoluteBounds;

    /// <summary>Raised when <see cref="Refresh"/> observes new bounds.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Publishes the current bounds when they differ from the last call.</summary>
    public void Refresh()
    {
        var bounds = Bounds;
        if (_hasLastBounds && SpatialGeometry.Equals(bounds, _lastBounds))
        {
            return;
        }

        _lastBounds = bounds;
        _hasLastBounds = true;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Bounds)));
    }
}
