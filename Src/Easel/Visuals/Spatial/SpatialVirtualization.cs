using System.Drawing;

namespace Easel.Visuals.Spatial;

/// <summary>Computes the visible subset of a spatial index, optionally expanding through an adjacency graph.</summary>
/// <typeparam name="T">The indexed item type.</typeparam>
/// <remarks>
/// <para>
/// The result is a scratch list owned by this instance: read it before the next <see cref="CollectVisible"/> call.
/// Reusing the buffers is what keeps a per-frame viewport query allocation-free apart from the bounds the caller
/// supplies.
/// </para>
/// <para>
/// Depth expansion mirrors how a linked canvas keeps a whole connection visible when only one of its endpoints
/// enters the viewport: depth zero returns the direct hits, each further level follows <c>adjacency</c> from the
/// items discovered so far.
/// </para>
/// </remarks>
public sealed class SpatialVirtualization<T>
    where T : class, ISpatialBoundsProvider
{
    private readonly ISpatialMap<T> _map;
    private readonly Func<T, IEnumerable<T>>? _adjacency;
    private readonly List<T> _visible = new();
    private readonly List<T> _direct = new();
    private readonly HashSet<T> _seen = new();
    private List<T> _frontier = new();
    private List<T> _next = new();

    /// <summary>Creates a visible-set query over <paramref name="map"/>.</summary>
    /// <param name="map">The spatial index to query.</param>
    /// <param name="adjacency">Returns the items connected to one item, or <see langword="null"/> for no expansion.</param>
    public SpatialVirtualization(ISpatialMap<T> map, Func<T, IEnumerable<T>>? adjacency = null)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _adjacency = adjacency;
    }

    /// <summary>The result of the most recent <see cref="CollectVisible"/> call.</summary>
    public IReadOnlyList<T> Visible => _visible;

    /// <summary>Collects the items visible in <paramref name="viewport"/> plus <paramref name="expansionDepth"/> hops.</summary>
    /// <param name="viewport">The visible rectangle.</param>
    /// <param name="expansionDepth">The number of adjacency hops past the direct hits; zero means direct only.</param>
    /// <returns>The same list as <see cref="Visible"/>.</returns>
    public IReadOnlyList<T> CollectVisible(RectangleF viewport, int expansionDepth = 0)
    {
        _visible.Clear();
        if (SpatialGeometry.IsEmpty(viewport))
        {
            return _visible;
        }

        _direct.Clear();
        _map.CollectInto(viewport, _direct);
        if (_adjacency is null || expansionDepth <= 0 || _direct.Count == 0)
        {
            _visible.AddRange(_direct);
            return _visible;
        }

        _seen.Clear();
        _frontier.Clear();
        foreach (var item in _direct)
        {
            if (_seen.Add(item))
            {
                _visible.Add(item);
                _frontier.Add(item);
            }
        }

        for (var depth = 0; depth < expansionDepth && _frontier.Count > 0; depth++)
        {
            _next.Clear();
            foreach (var item in _frontier)
            {
                foreach (var neighbor in _adjacency(item))
                {
                    if (neighbor is null || !_seen.Add(neighbor))
                    {
                        continue;
                    }

                    _visible.Add(neighbor);
                    _next.Add(neighbor);
                }
            }

            (_frontier, _next) = (_next, _frontier);
        }

        return _visible;
    }
}
