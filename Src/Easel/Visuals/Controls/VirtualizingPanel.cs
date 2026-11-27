using System.Drawing;
using System.Windows.Forms;
using XamlThickness = Easel.Controls.Thickness;

namespace Easel.Visuals.Controls;

/// <summary>One item's layout inside a <see cref="VirtualizationLayoutContext"/>.</summary>
/// <remarks><see cref="Bounds"/> is in document coordinates; <see cref="Order"/> is the host child order.</remarks>
public readonly struct VirtualizationItemLayout
{
    /// <summary>Creates an item layout record.</summary>
    /// <param name="index">The zero-based source index.</param>
    /// <param name="bounds">The item rectangle in document coordinates.</param>
    /// <param name="order">The host child order; defaults to <paramref name="index"/>.</param>
    public VirtualizationItemLayout(int index, RectangleF bounds, int order = -1)
    {
        Index = index;
        Bounds = bounds;
        Order = order >= 0 ? order : index;
    }

    /// <summary>The zero-based source index.</summary>
    public int Index { get; }

    /// <summary>The item rectangle in document coordinates.</summary>
    public RectangleF Bounds { get; }

    /// <summary>The host child order, usually the source index.</summary>
    public int Order { get; }
}

/// <summary>The per-arrange layout snapshot a <see cref="VirtualizingPanel"/> hands back to its engine.</summary>
/// <remarks>
/// The engine only consumes the realized window: it never asks whether the layout is a stack, wrap or grid.
/// A subclass builds the visible/overscan records, the full extent and the current offsets; the base class
/// performs the visible/keep/recycle/reuse/create diff.
/// </remarks>
public sealed class VirtualizationLayoutContext
{
    /// <summary>Creates a layout snapshot.</summary>
    /// <param name="items">The realized window in host order; duplicates are not allowed.</param>
    /// <param name="extent">The full document extent.</param>
    /// <param name="viewport">The visible document rectangle.</param>
    /// <param name="offsetX">The horizontal scroll offset.</param>
    /// <param name="offsetY">The vertical scroll offset.</param>
    /// <param name="itemCount">The source item count.</param>
    public VirtualizationLayoutContext(
        IReadOnlyList<VirtualizationItemLayout> items,
        Size extent,
        RectangleF viewport,
        float offsetX,
        float offsetY,
        int itemCount)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items));
        Extent = extent;
        Viewport = viewport;
        OffsetX = offsetX;
        OffsetY = offsetY;
        ItemCount = itemCount;
    }

    /// <summary>The realized window in host order.</summary>
    public IReadOnlyList<VirtualizationItemLayout> Items { get; }

    /// <summary>The full document extent.</summary>
    public Size Extent { get; }

    /// <summary>The visible document rectangle.</summary>
    public RectangleF Viewport { get; }

    /// <summary>The horizontal scroll offset.</summary>
    public float OffsetX { get; }

    /// <summary>The vertical scroll offset.</summary>
    public float OffsetY { get; }

    /// <summary>The source item count.</summary>
    public int ItemCount { get; }
}

/// <summary>The inputs a <see cref="VirtualizingPanel"/> gives its layout implementation.</summary>
public readonly struct VirtualizationLayoutRequest
{
    /// <summary>Creates a layout request.</summary>
    public VirtualizationLayoutRequest(
        Size finalSize,
        Size viewport,
        float offsetX,
        float offsetY,
        bool usingOwnScroll,
        Panel? host,
        XamlThickness padding)
    {
        FinalSize = finalSize;
        Viewport = viewport;
        OffsetX = offsetX;
        OffsetY = offsetY;
        UsingOwnScroll = usingOwnScroll;
        Host = host;
        Padding = padding;
    }

    /// <summary>The arranged panel size.</summary>
    public Size FinalSize { get; }

    /// <summary>The scroll viewport size.</summary>
    public Size Viewport { get; }

    /// <summary>The effective horizontal offset.</summary>
    public float OffsetX { get; }

    /// <summary>The effective vertical offset.</summary>
    public float OffsetY { get; }

    /// <summary>Whether the panel positions items itself for <c>ScrollViewer.CanContentScroll</c>.</summary>
    public bool UsingOwnScroll { get; }

    /// <summary>The items host, or <see langword="null"/> when items sit directly on the panel.</summary>
    public Panel? Host { get; }

    /// <summary>The panel padding.</summary>
    public XamlThickness Padding { get; }
}

/// <summary>A cell hash used by two-dimensional virtualizing layouts to answer viewport queries.</summary>
/// <remarks>
/// An item spanning several cells is registered in each of them. The query returns each index once. A fixed
/// one-dimensional layout should compute its index range arithmetically instead, so a million-row list does not
/// allocate a million hash entries.
/// </remarks>
public sealed class VirtualizationSpatialIndex
{
    private readonly Dictionary<(int Column, int Row), List<int>> _cells = [];
    private readonly HashSet<int> _seen = [];
    private readonly float _cellWidth;
    private readonly float _cellHeight;

    /// <summary>Creates a grid hash with the given cell size.</summary>
    /// <param name="cellWidth">The cell width in document units; values below one are treated as one.</param>
    /// <param name="cellHeight">The cell height in document units; values below one are treated as one.</param>
    public VirtualizationSpatialIndex(float cellWidth, float cellHeight)
    {
        _cellWidth = Math.Max(1f, cellWidth);
        _cellHeight = Math.Max(1f, cellHeight);
    }

    /// <summary>Registers an item rectangle in every cell it overlaps.</summary>
    public void Add(int index, RectangleF bounds)
    {
        var left = (int)Math.Floor(bounds.Left / _cellWidth);
        var top = (int)Math.Floor(bounds.Top / _cellHeight);
        var right = (int)Math.Floor((bounds.Right - 0.001f) / _cellWidth);
        var bottom = (int)Math.Floor((bounds.Bottom - 0.001f) / _cellHeight);
        for (var column = left; column <= right; column++)
        {
            for (var row = top; row <= bottom; row++)
            {
                var key = (column, row);
                if (!_cells.TryGetValue(key, out var list))
                {
                    list = [];
                    _cells[key] = list;
                }

                list.Add(index);
            }
        }
    }

    /// <summary>Collects the distinct item indices whose bounds touch <paramref name="viewport"/>.</summary>
    /// <param name="viewport">The query rectangle in document coordinates.</param>
    /// <param name="results">The destination list; callers should clear it when they need a fresh answer.</param>
    public void Query(RectangleF viewport, List<int> results)
    {
        _seen.Clear();
        var left = (int)Math.Floor(viewport.Left / _cellWidth);
        var top = (int)Math.Floor(viewport.Top / _cellHeight);
        var right = (int)Math.Floor((viewport.Right - 0.001f) / _cellWidth);
        var bottom = (int)Math.Floor((viewport.Bottom - 0.001f) / _cellHeight);
        for (var column = left; column <= right; column++)
        {
            for (var row = top; row <= bottom; row++)
            {
                if (!_cells.TryGetValue((column, row), out var bucket))
                {
                    continue;
                }

                foreach (var index in bucket)
                {
                    if (_seen.Add(index))
                    {
                        results.Add(index);
                    }
                }
            }
        }
    }

    /// <summary>Drops every registration.</summary>
    public void Clear()
    {
        _cells.Clear();
        _seen.Clear();
    }
}

/// <summary>The virtualization engine: it realizes the visible window and recycles the rest.</summary>
/// <remarks>
/// <para>
/// Layout is delegated to a subclass through <see cref="BuildLayout"/> and <see cref="MeasureLayout"/>. The
/// subclass returns a <see cref="VirtualizationLayoutContext"/> containing the realized (visible + overscan)
/// records, the full extent and the current offsets; this class performs the keep/recycle/reuse/create diff.
/// A stack, wrap grid, uniform grid or arbitrary spatial layout therefore share one realization and pooling path.
/// </para>
/// <para>
/// Inside a <see cref="ScrollViewer"/> the base class reads pixel offsets by default. Setting
/// <c>CanContentScroll</c> makes the viewer drive this panel's own <see cref="IScrollInfo"/> implementation;
/// the layout request then sets <see cref="VirtualizationLayoutRequest.UsingOwnScroll"/> and the engine positions
/// records relative to the viewport.
/// </para>
/// </remarks>
public abstract class VirtualizingPanel : ItemsControl, IScrollInfo
{
    private readonly List<VisualElement> _pool = [];
    private readonly Dictionary<int, VisualElement> _realized = [];
    private readonly Dictionary<int, int> _orders = [];
    private ScrollViewer? _scrollOwner;
    private Size _scrollExtent;
    private int _firstRealizedIndex = -1;
    private double _ownVerticalOffset;
    private double _ownHorizontalOffset;
    private float _lastPixelOffsetX;
    private float _lastPixelOffsetY;
    private bool _usingOwnScroll;

    /// <summary>Creates an empty virtualizing panel.</summary>
    protected VirtualizingPanel()
    {
        ParentChanged += OnPanelParentChanged;
        HookScrollOwner();
    }

    /// <summary>The source index of the first realized item, or -1 before the first arrange.</summary>
    public int FirstRealizedIndex => _firstRealizedIndex;

    /// <summary>The number of realized item visuals.</summary>
    public int RealizedCount => _realized.Count;

    /// <summary>Whether horizontal offset changes are honoured by <see cref="IScrollInfo"/>.</summary>
    public bool CanHorizontallyScroll { get; set; }

    /// <summary>Whether vertical offset changes are honoured by <see cref="IScrollInfo"/>.</summary>
    public bool CanVerticallyScroll { get; set; } = true;

    /// <summary>The viewer that owns this panel's <see cref="IScrollInfo"/> state, or <see langword="null"/>.</summary>
    public ScrollViewer? ScrollOwner { get; set; }

    /// <summary>The full scrollable width, in pixels.</summary>
    public double ExtentWidth => Math.Max(_scrollExtent.Width, Bounds.Width);

    /// <summary>The full scrollable height, in pixels.</summary>
    public double ExtentHeight => Math.Max(_scrollExtent.Height, Bounds.Height);

    /// <summary>The visible width, in pixels.</summary>
    public double ViewportWidth => ResolveScrollViewport().Width;

    /// <summary>The visible height, in pixels.</summary>
    public double ViewportHeight => ResolveScrollViewport().Height;

    /// <summary>The current horizontal offset, in pixels.</summary>
    public double HorizontalOffset => _ownHorizontalOffset;

    /// <summary>The current vertical offset, in pixels.</summary>
    public double VerticalOffset => _ownVerticalOffset;

    /// <summary>The vertical step for line input, in pixels.</summary>
    protected virtual double VerticalLineStep => 24d;

    /// <summary>The horizontal step for line input, in pixels.</summary>
    protected virtual double HorizontalLineStep => 24d;

    /// <summary>Measures the full document extent for the given available size.</summary>
    /// <param name="availableSize">The available panel size.</param>
    protected abstract Size MeasureLayout(Size availableSize);

    /// <summary>Builds the realized window and layout records for one arrange pass.</summary>
    /// <param name="request">The viewport, offsets and host.</param>
    protected abstract VirtualizationLayoutContext BuildLayout(VirtualizationLayoutRequest request);

    /// <summary>Measures and arranges one realized item at its layout record.</summary>
    /// <param name="element">The item visual.</param>
    /// <param name="item">The layout record.</param>
    /// <param name="context">The layout snapshot.</param>
    /// <param name="usingOwnScroll">Whether the record must be moved by the context offsets.</param>
    protected virtual void ArrangeItem(
        VisualElement element,
        in VirtualizationItemLayout item,
        VirtualizationLayoutContext context,
        bool usingOwnScroll)
    {
        var bounds = item.Bounds;
        if (usingOwnScroll)
        {
            bounds.Offset(-context.OffsetX, -context.OffsetY);
        }

        element.Measure(new Size((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height)));
        element.Arrange(bounds);
    }

    /// <summary>Creates or reuses the visual for <paramref name="index"/>.</summary>
    /// <param name="index">The source index to realize.</param>
    /// <returns>A visual bound to the item at <paramref name="index"/>.</returns>
    protected virtual VisualElement RealizeItem(int index)
    {
        if (_pool.Count > 0)
        {
            var element = _pool[_pool.Count - 1];
            _pool.RemoveAt(_pool.Count - 1);
            BindRecycledItem(element, index);
            return element;
        }

        return CreateItemVisual(GetItemAt(index));
    }

    /// <summary>Called after an item visual left the visible range and before it enters the pool.</summary>
    /// <param name="index">The source index the visual showed.</param>
    /// <param name="element">The visual that left the range.</param>
    /// <remarks>
    /// The default clears the data context: a pooled visual must not keep the item it last showed alive. The engine
    /// writes the new item when it binds the visual again through <see cref="BindRecycledItem"/>.
    /// </remarks>
    protected virtual void RecycleItem(int index, VisualElement element)
    {
        element.ClearValue(VisualElement.DataContextProperty);
    }

    /// <summary>Binds a pooled visual to a new source item.</summary>
    /// <param name="element">The pooled visual.</param>
    /// <param name="index">The source index to bind.</param>
    protected virtual void BindRecycledItem(VisualElement element, int index)
    {
        var item = GetItemAt(index);
        if (item is not null)
        {
            element.DataContext = item;
        }
        else
        {
            element.ClearValue(VisualElement.DataContextProperty);
        }
    }

    /// <summary>Resets the realized window so the next arrange realizes from scratch.</summary>
    protected void InvalidateRealizedWindow()
    {
        ClearRealizedRowsToPool();
        _firstRealizedIndex = -1;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsTemplated)
        {
            MeasureTemplate(availableSize);
        }

        _scrollExtent = MeasureLayout(availableSize);
        return _scrollExtent;
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        if (IsTemplated)
        {
            ArrangeTemplate(finalSize);
        }

        var host = ResolveRealizationHost();
        if (host is not null)
        {
            ArrangeDetachedHost(host, finalSize, Padding);
        }

        var (viewport, offsetX, offsetY, usingOwnScroll) = ResolveViewport(finalSize);
        var context = BuildLayout(new VirtualizationLayoutRequest(
            finalSize,
            viewport,
            offsetX,
            offsetY,
            usingOwnScroll,
            host,
            Padding));
        _scrollExtent = context.Extent;
        _usingOwnScroll = usingOwnScroll;
        UpdateRealizedWindow(context, host, usingOwnScroll);
        _lastPixelOffsetX = offsetX;
        _lastPixelOffsetY = offsetY;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The base rebuild detaches and clears <see cref="Items"/> before calling back here, but the engine's
    /// index-to-visual map still points at the old visuals. Those entries must be recycled into the pool (and the map
    /// reset): otherwise the next arrange treats them as still realized, so a replaced source keeps showing the old
    /// items, and a replaced factory never gets to dispose them.
    /// </remarks>
    protected override void RealizeItems()
    {
        ClearRealizedRowsToPool();

        foreach (var pair in _realized.ToArray())
        {
            RecycleItem(pair.Key, pair.Value);
            _pool.Add(pair.Value);
        }

        _realized.Clear();
        _orders.Clear();
        _firstRealizedIndex = -1;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // 工厂/模板换了，池里那批元素可能已经是另一种类型，不能再复用。
        if (ReferenceEquals(e.Property, ItemFactoryProperty)
            || ReferenceEquals(e.Property, ItemTemplateProperty)
            || ReferenceEquals(e.Property, ItemTemplateSelectorProperty)
            || ReferenceEquals(e.Property, ItemsPanelProperty)
            || ReferenceEquals(e.Property, DisplayMemberPathProperty))
        {
            DisposePool();
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanging()
    {
        base.OnTemplateChanging();
        InvalidateRealizedWindow();
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        base.OnTemplateChanged();
        if (IsTemplated)
        {
            ResolveRealizationHost();
        }

        InvalidateMeasure();
    }

    /// <summary>Sets the horizontal <see cref="IScrollInfo"/> offset.</summary>
    public void SetHorizontalOffset(double offset)
    {
        var next = CanHorizontallyScroll
            ? XamlMath.Clamp(offset, 0d, MaxHorizontalOffset)
            : 0d;
        if (Math.Abs(next - _ownHorizontalOffset) < 0.01d)
        {
            return;
        }

        _ownHorizontalOffset = next;
        InvalidateArrange();
        ScrollOwner?.InvalidateScrollInfo();
    }

    /// <summary>Sets the vertical <see cref="IScrollInfo"/> offset.</summary>
    public void SetVerticalOffset(double offset)
    {
        var next = CanVerticallyScroll
            ? XamlMath.Clamp(offset, 0d, MaxVerticalOffset)
            : 0d;
        if (Math.Abs(next - _ownVerticalOffset) < 0.01d)
        {
            return;
        }

        _ownVerticalOffset = next;
        InvalidateArrange();
        ScrollOwner?.InvalidateScrollInfo();
    }

    /// <summary>Moves one item up.</summary>
    public void LineUp() => SetVerticalOffset(_ownVerticalOffset - VerticalLineStep);

    /// <summary>Moves one item down.</summary>
    public void LineDown() => SetVerticalOffset(_ownVerticalOffset + VerticalLineStep);

    /// <summary>Moves left by one item.</summary>
    public void LineLeft() => SetHorizontalOffset(_ownHorizontalOffset - HorizontalLineStep);

    /// <summary>Moves right by one item.</summary>
    public void LineRight() => SetHorizontalOffset(_ownHorizontalOffset + HorizontalLineStep);

    /// <summary>Moves up by one viewport.</summary>
    public void PageUp() => SetVerticalOffset(_ownVerticalOffset - Math.Max(1d, ViewportHeight));

    /// <summary>Moves down by one viewport.</summary>
    public void PageDown() => SetVerticalOffset(_ownVerticalOffset + Math.Max(1d, ViewportHeight));

    /// <summary>Moves left by one viewport.</summary>
    public void PageLeft() => SetHorizontalOffset(_ownHorizontalOffset - Math.Max(1d, ViewportWidth));

    /// <summary>Moves right by one viewport.</summary>
    public void PageRight() => SetHorizontalOffset(_ownHorizontalOffset + Math.Max(1d, ViewportWidth));

    /// <summary>Moves up by one wheel notch.</summary>
    public void MouseWheelUp() => SetVerticalOffset(_ownVerticalOffset - (VerticalLineStep * 3d));

    /// <summary>Moves down by one wheel notch.</summary>
    public void MouseWheelDown() => SetVerticalOffset(_ownVerticalOffset + (VerticalLineStep * 3d));

    /// <summary>Moves left by one wheel notch.</summary>
    public void MouseWheelLeft() => SetHorizontalOffset(_ownHorizontalOffset - (HorizontalLineStep * 3d));

    /// <summary>Moves right by one wheel notch.</summary>
    public void MouseWheelRight() => SetHorizontalOffset(_ownHorizontalOffset + (HorizontalLineStep * 3d));

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UnhookScrollOwner();
            ParentChanged -= OnPanelParentChanged;
            DisposePool();
            _realized.Clear();
            _orders.Clear();
        }

        base.Dispose(disposing);
    }

    /// <summary>Resolves the item host used by the base realization engine.</summary>
    protected Panel? ResolveRealizationHost()
    {
        var host = ResolveItemsHost();
        if (host is not null)
        {
            return host;
        }

        if (!IsTemplated)
        {
            return null;
        }

        var named = GetTemplateChild<Panel>("PART_ItemsPresenter");
        if (named is not null)
        {
            if (named is not ItemsPresenter && !GetIsItemsHost(named))
            {
                SetIsItemsHost(named, true);
            }

            return named;
        }

        throw new InvalidOperationException(
            "A virtualizing panel template needs an ItemsPresenter or a panel marked with ItemsControl.IsItemsHost.");
    }

    /// <summary>Returns the accumulated lead of a host inside this panel.</summary>
    protected float ResolveHostLead(VisualElement host, bool vertical)
    {
        var lead = 0f;
        for (var current = host; current is not null && !ReferenceEquals(current, this); current = current.ParentElement)
        {
            lead += vertical ? current.Bounds.Y : current.Bounds.X;
        }

        return lead;
    }

    private double MaxVerticalOffset => Math.Max(0d, ExtentHeight - ViewportHeight);

    private double MaxHorizontalOffset => Math.Max(0d, ExtentWidth - ViewportWidth);

    private (double Width, double Height) ResolveScrollViewport()
    {
        var presenter = (_scrollOwner ?? FindScrollOwner())?.ContentPresenter;
        return presenter is null
            ? (Bounds.Width, Bounds.Height)
            : (presenter.ViewportWidth > 0f ? presenter.ViewportWidth : Bounds.Width,
               presenter.ViewportHeight > 0f ? presenter.ViewportHeight : Bounds.Height);
    }

    private void UpdateRealizedWindow(VirtualizationLayoutContext context, Panel? host, bool usingOwnScroll)
    {
        var requested = context.Items;
        var first = int.MaxValue;
        foreach (var item in requested)
        {
            first = Math.Min(first, item.Index);
        }

        _firstRealizedIndex = requested.Count > 0 ? first : 0;
        var wanted = new HashSet<int>();
        foreach (var item in requested)
        {
            wanted.Add(item.Index);
        }

        foreach (var pair in _realized.ToArray())
        {
            if (wanted.Contains(pair.Key))
            {
                continue;
            }

            var position = IndexOfVisual(pair.Value);
            if (position >= 0)
            {
                RemoveRealizedItemAt(position);
            }

            RecycleItem(pair.Key, pair.Value);
            if (pair.Value.ParentElement is { } parent)
            {
                parent.RemoveChild(pair.Value);
            }

            _pool.Add(pair.Value);
            _realized.Remove(pair.Key);
            _orders.Remove(pair.Key);
        }

        for (var order = 0; order < requested.Count; order++)
        {
            var item = requested[order];
            if (!_realized.TryGetValue(item.Index, out var visual))
            {
                visual = RealizeItem(item.Index);
                _realized[item.Index] = visual;
                _orders[item.Index] = item.Order;
                InsertRealizedItem(InsertionIndex(item.Order), visual, host);
            }

            ArrangeItem(visual, item, context, usingOwnScroll);
        }

        if (!IsRealizedOrder(context))
        {
            ReorderRealized(context, host);
        }
    }

    private int IndexOfVisual(VisualElement visual)
    {
        for (var index = 0; index < Items.Count; index++)
        {
            if (ReferenceEquals(Items[index], visual))
            {
                return index;
            }
        }

        return -1;
    }

    private int InsertionIndex(int order)
    {
        // Items are kept in host order; new records enter before the first realized item with a larger order.
        for (var position = 0; position < Items.Count; position++)
        {
            var index = IndexOfVisual(Items[position]);
            if (index >= 0 && _orders.TryGetValue(index, out var existingOrder) && existingOrder > order)
            {
                return position;
            }
        }

        return Items.Count;
    }

    private int OrderOf(VisualElement visual)
    {
        foreach (var pair in _realized)
        {
            if (ReferenceEquals(pair.Value, visual))
            {
                return pair.Key;
            }
        }

        return -1;
    }

    private bool IsRealizedOrder(VirtualizationLayoutContext context)
    {
        if (Items.Count != context.Items.Count)
        {
            return false;
        }

        for (var position = 0; position < Items.Count; position++)
        {
            var item = context.Items[position];
            if (!_realized.TryGetValue(item.Index, out var visual)
                || !ReferenceEquals(Items[position], visual)
                || !_orders.TryGetValue(item.Index, out var order)
                || order != item.Order)
            {
                return false;
            }
        }

        return true;
    }

    private void ReorderRealized(VirtualizationLayoutContext context, Panel? host)
    {
        // Kept visuals are detached and reinserted in the new order; layout order changes are rare (column count).
        var visuals = context.Items
            .Select(item => _realized.TryGetValue(item.Index, out var visual) ? visual : null)
            .Where(visual => visual is not null)
            .ToArray();
        for (var position = Items.Count - 1; position >= 0; position--)
        {
            RemoveRealizedItemAt(position);
        }

        for (var position = 0; position < visuals.Length; position++)
        {
            InsertRealizedItem(position, visuals[position]!, host);
        }

        for (var position = 0; position < context.Items.Count; position++)
        {
            _orders[context.Items[position].Index] = context.Items[position].Order;
        }
    }

    private void ClearRealizedRowsToPool()
    {
        while (Items.Count > 0)
        {
            var index = OrderOf(Items[0]);
            var visual = RemoveRealizedItemAt(0);
            if (index >= 0)
            {
                RecycleItem(index, visual);
                _realized.Remove(index);
                _orders.Remove(index);
            }

            _pool.Add(visual);
        }
    }

    private void DisposePool()
    {
        foreach (var element in _pool)
        {
            element.Dispose();
        }

        _pool.Clear();
    }

    private void ArrangeDetachedHost(Panel host, Size finalSize, XamlThickness padding)
    {
        if (IsInsideTemplateRoot(host))
        {
            return;
        }

        var rect = new RectangleF(
            (int)Math.Ceiling(padding.Left),
            (int)Math.Ceiling(padding.Top),
            Math.Max(0f, finalSize.Width - padding.Horizontal),
            Math.Max(0f, finalSize.Height - padding.Vertical));
        host.Measure(new Size((int)Math.Ceiling(rect.Width), (int)Math.Ceiling(rect.Height)));
        host.Arrange(rect);
    }

    private bool IsInsideTemplateRoot(VisualElement element)
    {
        if (TemplateRoot is null)
        {
            return false;
        }

        for (var current = element; current is not null; current = current.ParentElement)
        {
            if (ReferenceEquals(current, TemplateRoot))
            {
                return true;
            }

            if (ReferenceEquals(current, this))
            {
                break;
            }
        }

        return false;
    }

    private void OnPanelParentChanged(object? sender, EventArgs e) => HookScrollOwner();

    private void HookScrollOwner()
    {
        var owner = FindScrollOwner();
        if (ReferenceEquals(owner, _scrollOwner))
        {
            return;
        }

        if (_scrollOwner is not null)
        {
            _scrollOwner.ScrollChanged -= OnScrollOwnerChanged;
        }

        _scrollOwner = owner;
        if (owner is not null)
        {
            owner.ScrollChanged += OnScrollOwnerChanged;
            ScrollOwner ??= owner;
        }
    }

    private void UnhookScrollOwner()
    {
        if (_scrollOwner is not null)
        {
            _scrollOwner.ScrollChanged -= OnScrollOwnerChanged;
            _scrollOwner = null;
        }
    }

    private ScrollViewer? FindScrollOwner()
    {
        for (var current = ParentElement; current is not null; current = current.ParentElement)
        {
            if (current is ScrollViewer scroll)
            {
                return scroll;
            }
        }

        return null;
    }

    private (Size Viewport, float OffsetX, float OffsetY, bool UsingOwnScroll) ResolveViewport(Size finalSize)
    {
        var scroll = FindScrollOwner();
        if (scroll is not null && scroll.CanContentScroll && scroll.ContentPresenter is { } presenter)
        {
            var viewportWidth = presenter.ViewportWidth > 0f ? presenter.ViewportWidth : scroll.Bounds.Width;
            var viewportHeight = presenter.ViewportHeight > 0f ? presenter.ViewportHeight : scroll.Bounds.Height;
            var viewport = new Size((int)Math.Ceiling(viewportWidth), (int)Math.Ceiling(viewportHeight));
            var maxX = Math.Max(0d, _scrollExtent.Width - viewport.Width);
            var maxY = Math.Max(0d, _scrollExtent.Height - viewport.Height);
            _ownHorizontalOffset = XamlMath.Clamp(_ownHorizontalOffset, 0d, maxX);
            _ownVerticalOffset = XamlMath.Clamp(_ownVerticalOffset, 0d, maxY);
            return (viewport, (float)_ownHorizontalOffset, (float)_ownVerticalOffset, true);
        }

        if (scroll is not null)
        {
            var viewportWidth = scroll.ViewportWidth > 0f ? scroll.ViewportWidth : scroll.Bounds.Width;
            var viewportHeight = scroll.ViewportHeight > 0f ? scroll.ViewportHeight : scroll.Bounds.Height;
            var viewport = new Size((int)Math.Ceiling(viewportWidth), (int)Math.Ceiling(viewportHeight));
            if (viewport.Width <= 0f)
            {
                viewport = new Size(finalSize.Width, viewport.Height);
            }

            if (viewport.Height <= 0f)
            {
                viewport = new Size(viewport.Width, finalSize.Height);
            }

            return (viewport, scroll.HorizontalOffset, scroll.VerticalOffset, false);
        }

        return (finalSize, 0f, 0f, false);
    }

    // 像素滚动祖先在移动内容；内容尺寸不变时 Arrange 会被跳过，这里让虚拟化窗口跟着偏移重算。
    private void OnScrollOwnerChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_usingOwnScroll)
        {
            return;
        }

        if (Math.Abs(e.HorizontalOffset - _lastPixelOffsetX) >= 0.01f
            || Math.Abs(e.VerticalOffset - _lastPixelOffsetY) >= 0.01f)
        {
            InvalidateArrange();
        }
    }
}
