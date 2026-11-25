using System.ComponentModel;
using System.Drawing;

namespace Easel.Visuals.Spatial;

/// <summary>A uniform-grid spatial index over items that publish their own bounds.</summary>
/// <typeparam name="T">The indexed item type; reference equality identifies an item.</typeparam>
/// <remarks>
/// <para>
/// An item is filed into every cell its bounds touch. Queries walk only the cells the viewport touches, so the
/// cost follows the viewport rather than the number of indexed items. Items whose bounds are still empty (a view
/// that has not been measured yet) are tracked but not filed; <see cref="Query"/> files them once their bounds
/// become real, so a viewport move never leaves them permanently invisible.
/// </para>
/// <para>
/// The index is not thread-safe and is expected to be driven by the same UI thread that moves the items.
/// </para>
/// </remarks>
public sealed class SpatialGridHashMap<T> : ISpatialMap<T>
    where T : class, ISpatialBoundsProvider
{
    private readonly Dictionary<CellKey, HashSet<T>> _grid = new();
    private readonly Dictionary<T, RectangleF> _trackedItems = new();
    private readonly double _cellSize = 1d;
    private readonly HashSet<T> _queryScratch = new();
    // 已登记但边界仍为空（视图还没测量）的条目：它们不在任何格子里，查询时会再补一次。
    private readonly HashSet<T> _unindexed = new();
    private RectangleF _bounds;
    private bool _boundsDirty;
    private bool _reindexing;
    private bool _rerunPending;

    /// <summary>Creates an index whose cells are <paramref name="cellSize"/> units square.</summary>
    /// <param name="cellSize">The cell edge; values below one are raised to one.</param>
    public SpatialGridHashMap(double cellSize)
    {
        _cellSize = Math.Max(1d, cellSize);
    }

    /// <summary>The smallest rectangle covering every indexed item.</summary>
    public RectangleF Bounds
    {
        get
        {
            EnsureBounds();
            return _bounds;
        }
    }

    /// <inheritdoc />
    public void Insert(T item)
    {
        if (item is null || _trackedItems.ContainsKey(item))
        {
            return;
        }

        var bounds = item.Bounds;
        item.PropertyChanged += OnItemPropertyChanged;
        _trackedItems[item] = bounds;
        if (SpatialGeometry.IsEmpty(bounds))
        {
            _unindexed.Add(item);
        }
        else
        {
            IndexItem(item, bounds);
        }

        InvalidateBounds();
    }

    /// <inheritdoc />
    public void Remove(T item)
    {
        if (item is null || !_trackedItems.TryGetValue(item, out var bounds))
        {
            return;
        }

        item.PropertyChanged -= OnItemPropertyChanged;
        if (!SpatialGeometry.IsEmpty(bounds))
        {
            DeindexItem(item, bounds);
        }

        _unindexed.Remove(item);
        _trackedItems.Remove(item);
        InvalidateBounds();
    }

    /// <inheritdoc />
    public IEnumerable<T> Query(RectangleF viewport)
    {
        if (SpatialGeometry.IsEmpty(viewport))
        {
            yield break;
        }

        EnsureIndexed();
        _queryScratch.Clear();
        foreach (var cell in GetCells(viewport))
        {
            if (!_grid.TryGetValue(cell, out var set))
            {
                continue;
            }

            foreach (var item in set)
            {
                if (!_queryScratch.Add(item))
                {
                    continue;
                }

                var bounds = item.Bounds;
                var visible = SpatialGeometry.IsEmpty(bounds)
                    ? SpatialGeometry.Contains(viewport, bounds.X, bounds.Y)
                    : SpatialGeometry.Intersects(viewport, bounds);
                if (visible)
                {
                    yield return item;
                }
            }
        }
    }

    /// <inheritdoc />
    public void CollectInto(RectangleF viewport, List<T> buffer)
    {
        if (SpatialGeometry.IsEmpty(viewport))
        {
            return;
        }

        EnsureIndexed();
        _queryScratch.Clear();
        foreach (var cell in GetCells(viewport))
        {
            if (!_grid.TryGetValue(cell, out var set))
            {
                continue;
            }

            foreach (var item in set)
            {
                if (!_queryScratch.Add(item))
                {
                    continue;
                }

                var bounds = item.Bounds;
                var visible = SpatialGeometry.IsEmpty(bounds)
                    ? SpatialGeometry.Contains(viewport, bounds.X, bounds.Y)
                    : SpatialGeometry.Intersects(viewport, bounds);
                if (visible)
                {
                    buffer.Add(item);
                }
            }
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        foreach (var item in _trackedItems.Keys)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }

        _trackedItems.Clear();
        _grid.Clear();
        _queryScratch.Clear();
        _unindexed.Clear();
        _bounds = RectangleF.Empty;
        _boundsDirty = false;
    }

    private void EnsureBounds()
    {
        if (!_boundsDirty)
        {
            return;
        }

        _boundsDirty = false;
        if (_trackedItems.Count == 0)
        {
            _bounds = RectangleF.Empty;
            return;
        }

        var union = RectangleF.Empty;
        foreach (var bounds in _trackedItems.Values)
        {
            union = SpatialGeometry.Union(union, bounds);
        }

        _bounds = union;
    }

    private void InvalidateBounds()
    {
        _boundsDirty = true;
        EnsureBounds();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not T item
            || e.PropertyName != nameof(ISpatialBoundsProvider.Bounds)
            || !_trackedItems.TryGetValue(item, out var oldBounds))
        {
            return;
        }

        var newBounds = item.Bounds;
        if (SpatialGeometry.Equals(oldBounds, newBounds))
        {
            return;
        }

        // 缩放会连续触发一批条目的 Bounds 变更；重入时先只记录新值，等外层那一趟结束后统一重建。
        if (_reindexing)
        {
            _trackedItems[item] = newBounds;
            _rerunPending = true;
            return;
        }

        _reindexing = true;
        try
        {
            if (SpatialGeometry.IsEmpty(oldBounds))
            {
                if (!SpatialGeometry.IsEmpty(newBounds))
                {
                    IndexItem(item, newBounds);
                }
            }
            else if (SpatialGeometry.IsEmpty(newBounds))
            {
                DeindexItem(item, oldBounds);
            }
            else
            {
                DeindexItem(item, oldBounds);
                IndexItem(item, newBounds);
            }

            _trackedItems[item] = newBounds;
            if (SpatialGeometry.IsEmpty(newBounds))
            {
                _unindexed.Add(item);
            }
            else
            {
                _unindexed.Remove(item);
            }

            InvalidateBounds();
        }
        finally
        {
            _reindexing = false;
        }

        if (_rerunPending)
        {
            _rerunPending = false;
            ResyncGrid();
        }
    }

    private void ResyncGrid()
    {
        _reindexing = true;
        try
        {
            _grid.Clear();
            _unindexed.Clear();
            foreach (var pair in _trackedItems)
            {
                if (SpatialGeometry.IsEmpty(pair.Value))
                {
                    _unindexed.Add(pair.Key);
                }
                else
                {
                    IndexItem(pair.Key, pair.Value);
                }
            }

            _queryScratch.Clear();
        }
        finally
        {
            _reindexing = false;
        }
    }

    // 查询是唯一必须给出真话的地方：先补上被延后的重建，再给边界曾为空的条目一次机会。
    private void EnsureIndexed()
    {
        for (var round = 0; round < 4 && _rerunPending; round++)
        {
            _rerunPending = false;
            ResyncGrid();
        }

        if (_unindexed.Count == 0)
        {
            return;
        }

        var settled = false;
        _reindexing = true;
        try
        {
            foreach (var item in _unindexed.ToArray())
            {
                var bounds = item.Bounds;
                if (SpatialGeometry.IsEmpty(bounds))
                {
                    continue;
                }

                _unindexed.Remove(item);
                _trackedItems[item] = bounds;
                IndexItem(item, bounds);
                settled = true;
            }
        }
        finally
        {
            _reindexing = false;
        }

        if (settled)
        {
            InvalidateBounds();
        }
    }

    private void IndexItem(T item, RectangleF bounds)
    {
        foreach (var cell in GetCells(bounds))
        {
            if (!_grid.TryGetValue(cell, out var set))
            {
                set = new HashSet<T>();
                _grid[cell] = set;
            }

            set.Add(item);
        }
    }

    private void DeindexItem(T item, RectangleF bounds)
    {
        foreach (var cell in GetCells(bounds))
        {
            if (!_grid.TryGetValue(cell, out var set))
            {
                continue;
            }

            set.Remove(item);
            if (set.Count == 0)
            {
                _grid.Remove(cell);
            }
        }
    }

    private CellEnumerable GetCells(RectangleF bounds) => new(bounds, _cellSize);

    private readonly struct CellEnumerable
    {
        private readonly RectangleF _bounds;
        private readonly double _cellSize;

        internal CellEnumerable(RectangleF bounds, double cellSize)
        {
            _bounds = bounds;
            _cellSize = cellSize;
        }

        public CellEnumerator GetEnumerator() => new(_bounds, _cellSize);
    }

    private struct CellEnumerator
    {
        private readonly int _minX;
        private readonly int _maxX;
        private readonly int _minY;
        private readonly int _maxY;
        private int _x;
        private int _y;
        private bool _started;

        internal CellEnumerator(RectangleF bounds, double cellSize)
        {
            var left = (int)Math.Floor(bounds.X / cellSize);
            var top = (int)Math.Floor(bounds.Y / cellSize);
            if (SpatialGeometry.IsEmpty(bounds))
            {
                // 零尺寸条目按它所在的那一格处理。
                _minX = left;
                _minY = top;
                _maxX = left + 1;
                _maxY = top + 1;
            }
            else
            {
                _minX = left;
                _minY = top;
                _maxX = (int)Math.Ceiling((bounds.X + bounds.Width) / cellSize);
                _maxY = (int)Math.Ceiling((bounds.Y + bounds.Height) / cellSize);
            }

            _x = _minX;
            _y = _minY;
            _started = false;
        }

        public CellKey Current => new(_x, _y);

        public bool MoveNext()
        {
            if (!_started)
            {
                _started = true;
                return _x < _maxX && _y < _maxY;
            }

            _x++;
            if (_x >= _maxX)
            {
                _x = _minX;
                _y++;
            }

            return _y < _maxY;
        }
    }
}
