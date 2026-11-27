using System.Drawing;
using System.Windows.Forms;
using XamlThickness = Easel.Controls.Thickness;

namespace Easel.Visuals.Controls;

/// <summary>A panel that realizes only the items intersecting its viewport and recycles them as it scrolls.</summary>
/// <remarks>
/// <para>
/// Items share one <see cref="ItemExtent"/> along <see cref="Orientation"/>, so the visible index range is plain
/// division. Moving the viewport by one row recycles the leaving rows and rebinds them to the entering rows; the
/// rows that stay visible are not rebuilt. Override <see cref="RealizeItem"/> and <see cref="RecycleItem"/> to
/// customize how a row is created and released.
/// </para>
/// <para>
/// Inside a <see cref="ScrollViewer"/> the panel reads the viewer's pixel offset and reports its full extent as
/// its desired size. Setting <see cref="ScrollViewer.CanContentScroll"/> makes the viewer drive this panel's own
/// <see cref="IScrollInfo"/> implementation instead, and the panel then positions rows relative to the viewport.
/// </para>
/// </remarks>
public class VirtualizingPanel : ItemsControl, IScrollInfo
{
    /// <summary>The fixed size of one item along <see cref="Orientation"/>, in pixels.</summary>
    public static readonly DependencyProperty ItemExtentProperty =
        DependencyProperty.Register(
            nameof(ItemExtent), typeof(double), typeof(VirtualizingPanel),
            new PropertyMetadata(24d) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>Whether items stack vertically (default) or horizontally.</summary>
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation), typeof(Orientation), typeof(VirtualizingPanel),
            new PropertyMetadata(Orientation.Vertical) { AffectsMeasure = true, AffectsRender = false });

    private readonly List<VisualElement> _pool = new();
    private ScrollViewer? _scrollOwner;
    private int _firstRealizedIndex = -1;
    private double _ownVerticalOffset;
    private double _ownHorizontalOffset;
    private float _lastArrangedOffset;
    private bool _usingOwnScroll;

    /// <summary>Creates an empty virtualizing panel.</summary>
    public VirtualizingPanel()
    {
        ParentChanged += OnPanelParentChanged;
        HookScrollOwner();
    }

    /// <summary>The fixed size of one item along <see cref="Orientation"/>, in pixels.</summary>
    public virtual double ItemExtent
    {
        get => (double)GetValue(ItemExtentProperty)!;
        set => SetValue(ItemExtentProperty, value);
    }

    /// <summary>Whether items stack vertically (default) or horizontally.</summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty)!;
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>The source index of the first realized item, or -1 before the first arrange.</summary>
    public int FirstRealizedIndex => _firstRealizedIndex;

    /// <summary>The number of realized item visuals.</summary>
    public int RealizedCount => Items.Count;

    /// <summary>Whether horizontal offset changes are honoured by <see cref="IScrollInfo"/>.</summary>
    public bool CanHorizontallyScroll { get; set; }

    /// <summary>Whether vertical offset changes are honoured by <see cref="IScrollInfo"/>.</summary>
    public bool CanVerticallyScroll { get; set; } = true;

    /// <summary>The viewer that owns this panel's <see cref="IScrollInfo"/> state, or <see langword="null"/>.</summary>
    public ScrollViewer? ScrollOwner { get; set; }

    /// <summary>The full scrollable width, in pixels.</summary>
    public double ExtentWidth => Orientation == Orientation.Horizontal ? TotalExtent : Bounds.Width;

    /// <summary>The full scrollable height, in pixels.</summary>
    public double ExtentHeight => Orientation == Orientation.Vertical ? TotalExtent : Bounds.Height;

    /// <summary>The visible width, in pixels.</summary>
    public double ViewportWidth => ResolveScrollViewport().Width;

    /// <summary>The visible height, in pixels.</summary>
    public double ViewportHeight => ResolveScrollViewport().Height;

    /// <summary>The current horizontal offset, in pixels.</summary>
    public double HorizontalOffset => _ownHorizontalOffset;

    /// <summary>The current vertical offset, in pixels.</summary>
    public double VerticalOffset => _ownVerticalOffset;

    /// <summary>Moves one item up.</summary>
    public void LineUp() => AdjustVertical(-Math.Max(1d, ItemExtent));

    /// <summary>Moves one item down.</summary>
    public void LineDown() => AdjustVertical(Math.Max(1d, ItemExtent));

    /// <summary>Moves left by one item.</summary>
    public void LineLeft() => AdjustHorizontal(-Math.Max(1d, ItemExtent));

    /// <summary>Moves right by one item.</summary>
    public void LineRight() => AdjustHorizontal(Math.Max(1d, ItemExtent));

    /// <summary>Moves up by one viewport.</summary>
    public void PageUp() => AdjustVertical(-Math.Max(1d, ViewportHeight));

    /// <summary>Moves down by one viewport.</summary>
    public void PageDown() => AdjustVertical(Math.Max(1d, ViewportHeight));

    /// <summary>Moves left by one viewport.</summary>
    public void PageLeft() => AdjustHorizontal(-Math.Max(1d, ViewportWidth));

    /// <summary>Moves right by one viewport.</summary>
    public void PageRight() => AdjustHorizontal(Math.Max(1d, ViewportWidth));

    /// <summary>Moves up by one wheel notch.</summary>
    public void MouseWheelUp() => AdjustVertical(-Math.Max(1d, ItemExtent) * 3d);

    /// <summary>Moves down by one wheel notch.</summary>
    public void MouseWheelDown() => AdjustVertical(Math.Max(1d, ItemExtent) * 3d);

    /// <summary>Moves left by one wheel notch.</summary>
    public void MouseWheelLeft() => AdjustHorizontal(-Math.Max(1d, ItemExtent) * 3d);

    /// <summary>Moves right by one wheel notch.</summary>
    public void MouseWheelRight() => AdjustHorizontal(Math.Max(1d, ItemExtent) * 3d);

    /// <summary>Sets the horizontal <see cref="IScrollInfo"/> offset.</summary>
    /// <param name="offset">The requested offset in pixels.</param>
    public void SetHorizontalOffset(double offset)
    {
        var next = CanHorizontallyScroll ? XamlMath.Clamp(offset, 0d, MaxHorizontalOffset) : 0d;
        if (Math.Abs(next - _ownHorizontalOffset) < 0.01d)
        {
            return;
        }

        _ownHorizontalOffset = next;
        InvalidateArrange();
        ScrollOwner?.InvalidateScrollInfo();
    }

    /// <summary>Sets the vertical <see cref="IScrollInfo"/> offset.</summary>
    /// <param name="offset">The requested offset in pixels.</param>
    public void SetVerticalOffset(double offset)
    {
        var next = CanVerticallyScroll ? XamlMath.Clamp(offset, 0d, MaxVerticalOffset) : 0d;
        if (Math.Abs(next - _ownVerticalOffset) < 0.01d)
        {
            return;
        }

        _ownVerticalOffset = next;
        InvalidateArrange();
        ScrollOwner?.InvalidateScrollInfo();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsTemplated)
        {
            MeasureTemplate(availableSize);
        }

        var padding = Padding;
        var extent = Math.Max(1d, ItemExtent) * SourceItems.Count;
        return Orientation == Orientation.Vertical
            ? new Size(
                availableSize.Width >= int.MaxValue / 2 ? 0 : Math.Max(0, availableSize.Width),
                (int)Math.Ceiling(extent + padding.Vertical))
            : new Size(
                (int)Math.Ceiling(extent + padding.Horizontal),
                availableSize.Height >= int.MaxValue / 2 ? 0 : Math.Max(0, availableSize.Height));
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

        var extent = (float)Math.Max(1d, ItemExtent);
        var (offset, viewport) = ResolveViewport(finalSize);
        var origin = host is not null
            ? (Orientation == Orientation.Vertical
                ? ResolveHostLead(host, vertical: true) + host.Padding.Top
                : ResolveHostLead(host, vertical: false) + host.Padding.Left)
            : (Orientation == Orientation.Vertical ? Padding.Top : Padding.Left);
        var (first, last) = ComputeRange(extent, viewport, offset, origin);
        UpdateRealizedWindow(first, last, host);
        ArrangeRealized(host, finalSize, extent, offset);
        _lastArrangedOffset = offset;
    }

    /// <inheritdoc />
    protected override void RealizeItems()
    {
        ClearRealizedRowsToPool();
        _firstRealizedIndex = -1;
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
    protected virtual void RecycleItem(int index, VisualElement element)
    {
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
        // 先把当前窗口按真实索引退回池：若只把 FirstRealizedIndex 置 -1，下一次 Arrange 已经不知道旧窗口
        // 从哪开始，RecycleItem 收到的索引会从 -1 起算，和元素实际显示的行对不上。
        if (_firstRealizedIndex >= 0)
        {
            ClearRealizedRowsToPool();
        }

        _firstRealizedIndex = -1;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (ReferenceEquals(e.Property, ItemExtentProperty)
            || ReferenceEquals(e.Property, OrientationProperty))
        {
            InvalidateRealizedWindow();
            InvalidateMeasure();
            return;
        }

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

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UnhookScrollOwner();
            ParentChanged -= OnPanelParentChanged;
            DisposePool();
        }

        base.Dispose(disposing);
    }

    private double TotalExtent => Math.Max(1d, ItemExtent) * SourceItems.Count;

    private double MaxVerticalOffset => Math.Max(0d, ExtentHeight - ViewportHeight);

    private double MaxHorizontalOffset => Math.Max(0d, ExtentWidth - ViewportWidth);

    private void AdjustVertical(double delta)
    {
        if (Orientation == Orientation.Vertical)
        {
            SetVerticalOffset(_ownVerticalOffset + delta);
        }
    }

    private void AdjustHorizontal(double delta)
    {
        if (Orientation == Orientation.Horizontal)
        {
            SetHorizontalOffset(_ownHorizontalOffset + delta);
        }
    }

    private (double Width, double Height) ResolveScrollViewport()
    {
        var presenter = (_scrollOwner ?? FindScrollOwner())?.ContentPresenter;
        return presenter is null
            ? (Bounds.Width, Bounds.Height)
            : (presenter.ViewportWidth > 0f ? presenter.ViewportWidth : Bounds.Width,
               presenter.ViewportHeight > 0f ? presenter.ViewportHeight : Bounds.Height);
    }

    private void UpdateRealizedWindow(int first, int last, Panel? host)
    {
        if (first < 0)
        {
            first = 0;
        }

        if (last < first)
        {
            last = first;
        }

        if (_firstRealizedIndex < 0)
        {
            ClearRealizedRowsToPool();
            _firstRealizedIndex = first;
        }

        while (_firstRealizedIndex < first && Items.Count > 0)
        {
            var leaving = RemoveRealizedItemAt(0);
            Recycle(_firstRealizedIndex, leaving);
            _firstRealizedIndex++;
        }

        while (Items.Count > 0 && _firstRealizedIndex + Items.Count > last)
        {
            var leaving = RemoveRealizedItemAt(Items.Count - 1);
            Recycle(_firstRealizedIndex + Items.Count, leaving);
        }

        if (Items.Count == 0)
        {
            _firstRealizedIndex = first;
        }

        while (_firstRealizedIndex > first)
        {
            _firstRealizedIndex--;
            InsertRealizedItem(0, RealizeItem(_firstRealizedIndex), host);
        }

        while (_firstRealizedIndex + Items.Count < last)
        {
            var index = _firstRealizedIndex + Items.Count;
            InsertRealizedItem(Items.Count, RealizeItem(index), host);
        }
    }

    private void Recycle(int index, VisualElement element)
    {
        RecycleItem(index, element);
        if (element.ParentElement is { } parent)
        {
            parent.RemoveChild(element);
        }

        _pool.Add(element);
    }

    private void ClearRealizedRowsToPool()
    {
        while (Items.Count > 0)
        {
            var element = RemoveRealizedItemAt(Items.Count - 1);
            Recycle(_firstRealizedIndex + Items.Count, element);
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

    private void ArrangeRealized(Panel? host, Size finalSize, float extent, float offset)
    {
        var hostPadding = host?.Padding ?? XamlThickness.Empty;
        var vertical = Orientation == Orientation.Vertical;
        var width = host is not null
            ? Math.Max(0f, host.Bounds.Width - hostPadding.Horizontal)
            : Math.Max(0f, finalSize.Width - Padding.Horizontal);
        var height = host is not null
            ? Math.Max(0f, host.Bounds.Height - hostPadding.Vertical)
            : Math.Max(0f, finalSize.Height - Padding.Vertical);
        if (vertical && width <= 0f)
        {
            width = Math.Max(0f, finalSize.Width - Padding.Horizontal - hostPadding.Horizontal);
        }

        for (var index = 0; index < Items.Count; index++)
        {
            var sourceIndex = _firstRealizedIndex + index;
            var element = Items[index];
            var lead = (host is not null ? (vertical ? hostPadding.Top : hostPadding.Left) : (vertical ? Padding.Top : Padding.Left))
                       + (sourceIndex * extent);
            if (_usingOwnScroll)
            {
                lead -= offset;
            }

            if (vertical)
            {
                element.Measure(new Size((int)Math.Ceiling(width), (int)Math.Ceiling(extent)));
                element.Arrange(new RectangleF(
                    host is not null ? hostPadding.Left : Padding.Left,
                    lead,
                    width,
                    extent));
            }
            else
            {
                element.Measure(new Size((int)Math.Ceiling(extent), (int)Math.Ceiling(height)));
                element.Arrange(new RectangleF(
                    lead,
                    host is not null ? hostPadding.Top : Padding.Top,
                    extent,
                    height));
            }
        }
    }

    private (int First, int Last) ComputeRange(float extent, float viewport, float offset, float origin)
    {
        var count = SourceItems.Count;
        if (count == 0)
        {
            return (0, 0);
        }

        var visible = Math.Max(extent, viewport);
        var start = Math.Max(0f, offset - origin);
        var first = (int)Math.Floor(start / extent);
        var last = (int)Math.Ceiling((start + visible) / extent);
        first = XamlMath.Clamp(first, 0, count);
        last = XamlMath.Clamp(last, first, count);
        return (first, last);
    }

    // 优先用内容自己的 IScrollInfo（CanContentScroll）；否则按滚动祖先的像素偏移虚拟化。
    private (float Offset, float Viewport) ResolveViewport(Size finalSize)
    {
        var vertical = Orientation == Orientation.Vertical;
        var scroll = FindScrollOwner();
        if (scroll is not null)
        {
            if (scroll.CanContentScroll && scroll.ContentPresenter is { } presenter)
            {
                _usingOwnScroll = true;
                var viewport = vertical ? presenter.ViewportHeight : presenter.ViewportWidth;
                var fallback = vertical ? scroll.Bounds.Height : scroll.Bounds.Width;
                var effectiveViewport = viewport > 0f ? viewport : fallback;

                // 源变短 / ItemExtent 变大后，逻辑偏移可能已经落到新范围之外。ScrollViewer 在逻辑滚动模式下
                // 只把偏移转发给内容，不会替它夹取；这里不夹取就会算出 first == last == count，窗口全空。
                var maxOffset = Math.Max(0d, TotalExtent - effectiveViewport);
                if (vertical)
                {
                    if (_ownVerticalOffset > maxOffset + 0.01d)
                    {
                        _ownVerticalOffset = maxOffset;
                    }
                }
                else if (_ownHorizontalOffset > maxOffset + 0.01d)
                {
                    _ownHorizontalOffset = maxOffset;
                }

                return ((float)(vertical ? _ownVerticalOffset : _ownHorizontalOffset), effectiveViewport);
            }

            _usingOwnScroll = false;
            var pixelViewport = vertical ? scroll.ViewportHeight : scroll.ViewportWidth;
            var pixelFallback = vertical ? scroll.Bounds.Height : scroll.Bounds.Width;
            var pixelOffset = vertical ? scroll.VerticalOffset : scroll.HorizontalOffset;
            return (pixelOffset, pixelViewport > 0f
                ? pixelViewport
                : pixelFallback > 0f
                    ? pixelFallback
                    : (vertical ? finalSize.Height : finalSize.Width));
        }

        _usingOwnScroll = false;
        return (0f, vertical ? finalSize.Height : finalSize.Width);
    }

    private Panel? ResolveRealizationHost()
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

        // 命名的 PART_ItemsPresenter 即使没有 IsItemsHost 也接受。
        var named = GetTemplateChild<Panel>("PART_ItemsPresenter");
        if (named is not null)
        {
            if (named is not ItemsPresenter && !ItemsControl.GetIsItemsHost(named))
            {
                ItemsControl.SetIsItemsHost(named, true);
            }

            return named;
        }

        throw new InvalidOperationException(
            "A virtualizing panel template needs an ItemsPresenter or a panel marked with ItemsControl.IsItemsHost.");
    }

    // 模板根里的宿主已由模板排好；ItemsPanel 宿主是控件的直接子项，需要在这里测量并摆放。
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

    private float ResolveHostLead(VisualElement host, bool vertical)
    {
        var lead = 0f;
        for (var current = host; current is not null && !ReferenceEquals(current, this); current = current.ParentElement)
        {
            lead += vertical ? current.Bounds.Y : current.Bounds.X;
        }

        return lead;
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

    // 偏移是像素滚动祖先在动的，内容尺寸没变时 Arrange 会被跳过；这里显式让虚拟化窗口跟着偏移重算。
    private void OnScrollOwnerChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_usingOwnScroll)
        {
            return;
        }

        var offset = Orientation == Orientation.Vertical ? e.VerticalOffset : e.HorizontalOffset;
        if (Math.Abs(offset - _lastArrangedOffset) >= 0.01f)
        {
            InvalidateArrange();
        }
    }
}
