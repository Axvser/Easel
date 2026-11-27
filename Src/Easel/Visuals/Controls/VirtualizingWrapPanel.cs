using System.Drawing;

namespace Easel.Visuals.Controls;

/// <summary>A fixed-cell virtualizer that flows items left to right and wraps them into rows.</summary>
/// <remarks>
/// <para>
/// Every item occupies <see cref="ItemWidth"/> by <see cref="ItemHeight"/> DIP, separated by
/// <see cref="HorizontalSpacing"/> and <see cref="VerticalSpacing"/>. The column count is derived from the available
/// width, so a wider viewport packs more columns while <see cref="MeasureLayout"/> always reports every row.
/// </para>
/// <para>
/// Rows are indices divided by the column count, so the realized window is a contiguous row band expanded by
/// <see cref="OverscanRows"/> on either side. That arithmetic is O(visible) at any source size and beats a spatial
/// hash that would index every item on each layout. A row occupies a full pitch slot (item height plus the vertical
/// gap), which is the unit the visible band and the tests use.
/// </para>
/// </remarks>
public class VirtualizingWrapPanel : VirtualizingPanel
{
    /// <summary>The slot width of one item, in DIP; values below one are treated as one.</summary>
    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(
            nameof(ItemWidth), typeof(double), typeof(VirtualizingWrapPanel),
            new PropertyMetadata(100d) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The slot height of one item, in DIP; values below one are treated as one.</summary>
    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.Register(
            nameof(ItemHeight), typeof(double), typeof(VirtualizingWrapPanel),
            new PropertyMetadata(100d) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The horizontal gap between adjacent items, in DIP; negative values are treated as zero.</summary>
    public static readonly DependencyProperty HorizontalSpacingProperty =
        DependencyProperty.Register(
            nameof(HorizontalSpacing), typeof(double), typeof(VirtualizingWrapPanel),
            new PropertyMetadata(0d) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The vertical gap between adjacent rows, in DIP; negative values are treated as zero.</summary>
    public static readonly DependencyProperty VerticalSpacingProperty =
        DependencyProperty.Register(
            nameof(VerticalSpacing), typeof(double), typeof(VirtualizingWrapPanel),
            new PropertyMetadata(0d) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The number of rows realized above and below the visible band.</summary>
    public static readonly DependencyProperty OverscanRowsProperty =
        DependencyProperty.Register(
            nameof(OverscanRows), typeof(int), typeof(VirtualizingWrapPanel),
            new PropertyMetadata(1) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>Creates an empty wrap panel.</summary>
    public VirtualizingWrapPanel()
    {
    }

    /// <summary>The slot width of one item, in DIP; values below one are treated as one.</summary>
    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty)!;
        set => SetValue(ItemWidthProperty, value);
    }

    /// <summary>The slot height of one item, in DIP; values below one are treated as one.</summary>
    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty)!;
        set => SetValue(ItemHeightProperty, value);
    }

    /// <summary>The horizontal gap between adjacent items, in DIP; negative values are treated as zero.</summary>
    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty)!;
        set => SetValue(HorizontalSpacingProperty, value);
    }

    /// <summary>The vertical gap between adjacent rows, in DIP; negative values are treated as zero.</summary>
    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty)!;
        set => SetValue(VerticalSpacingProperty, value);
    }

    /// <summary>The number of rows realized above and below the visible band.</summary>
    public int OverscanRows
    {
        get => (int)GetValue(OverscanRowsProperty)!;
        set => SetValue(OverscanRowsProperty, value);
    }

    /// <summary>The column count used by the most recent layout pass.</summary>
    public int Columns { get; private set; }

    /// <inheritdoc />
    protected override double VerticalLineStep => ItemPitchHeight;

    /// <inheritdoc />
    protected override Size MeasureLayout(Size availableSize)
    {
        var padding = Padding;
        var count = SourceItems.Count;
        var rawWidth = availableSize.Width - padding.Horizontal;
        var unbounded = rawWidth >= UnboundedWidth;
        var contentWidth = unbounded ? 0d : Math.Max(0d, rawWidth);
        var columns = ResolveColumns(contentWidth, count, unbounded);
        var rows = RowCount(count, columns);
        var width = Math.Max(contentWidth, RowWidth(columns, count));

        return new Size(
            SafeCeil(width + padding.Horizontal),
            SafeCeil(RowExtent(rows) + padding.Vertical));
    }

    /// <inheritdoc />
    protected override VirtualizationLayoutContext BuildLayout(VirtualizationLayoutRequest request)
    {
        var padding = request.Padding;
        var host = request.Host;
        var count = SourceItems.Count;
        var contentWidth = host is not null
            ? Math.Max(0f, host.Bounds.Width - host.Padding.Horizontal)
            : Math.Max(0f, request.Viewport.Width - padding.Horizontal);
        if (contentWidth <= 0f && request.FinalSize.Width > 0)
        {
            contentWidth = Math.Max(0f, request.FinalSize.Width - padding.Horizontal);
        }

        var columns = ResolveColumns(contentWidth, count, unbounded: false);
        Columns = columns;
        var rows = RowCount(count, columns);
        var itemWidth = ItemSlotWidth;
        var itemHeight = ItemSlotHeight;
        var gapX = HorizontalGap;
        var pitchY = itemHeight + VerticalGap;
        var originX = host is not null
            ? ResolveHostLead(host, vertical: false) + host.Padding.Left
            : padding.Left;
        var originY = host is not null
            ? ResolveHostLead(host, vertical: true) + host.Padding.Top
            : padding.Top;

        var firstRow = 0;
        var lastRowExclusive = 0;
        if (count > 0)
        {
            // 行的可见性按 pitch 整槽判断：行下方的间距算在上一行里，所以边界恰好落在槽线时不会多算一行。
            var contentTop = Math.Max(0d, request.OffsetY - originY);
            var firstVisible = XamlMath.Clamp((int)Math.Floor(contentTop / pitchY), 0, rows);
            var lastVisibleExclusive = XamlMath.Clamp(
                (int)Math.Ceiling((contentTop + request.Viewport.Height) / pitchY), firstVisible, rows);
            if (lastVisibleExclusive <= firstVisible)
            {
                lastVisibleExclusive = Math.Min(rows, firstVisible + 1);
            }

            var overscan = Math.Max(0, OverscanRows);
            firstRow = Math.Max(0, firstVisible - overscan);
            lastRowExclusive = Math.Min(rows, lastVisibleExclusive + overscan);
        }

        var startIndex = (long)firstRow * columns;
        var endIndex = Math.Min(count, (long)lastRowExclusive * columns);
        var items = new List<VirtualizationItemLayout>(Math.Max(0, (int)(endIndex - startIndex)));
        for (var index = (int)startIndex; index < endIndex; index++)
        {
            var row = index / columns;
            var column = index - (row * columns);
            var x = originX + (column * (itemWidth + gapX));
            var y = originY + (row * pitchY);
            items.Add(new VirtualizationItemLayout(index, new RectangleF(
                (float)x, (float)y, (float)itemWidth, (float)itemHeight)));
        }

        var extentWidth = Math.Max(contentWidth, RowWidth(columns, count));
        var extent = new Size(
            SafeCeil(extentWidth + padding.Horizontal),
            SafeCeil(RowExtent(rows) + padding.Vertical));
        var viewport = new RectangleF(request.OffsetX, request.OffsetY, request.Viewport.Width, request.Viewport.Height);
        return new VirtualizationLayoutContext(
            items, extent, viewport, request.OffsetX, request.OffsetY, count);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, ItemWidthProperty)
            || ReferenceEquals(e.Property, ItemHeightProperty)
            || ReferenceEquals(e.Property, HorizontalSpacingProperty)
            || ReferenceEquals(e.Property, VerticalSpacingProperty)
            || ReferenceEquals(e.Property, OverscanRowsProperty))
        {
            InvalidateRealizedWindow();
            InvalidateMeasure();
        }
    }

    private const float UnboundedWidth = int.MaxValue / 2f;

    private double ItemSlotWidth => Math.Max(1d, ItemWidth);

    private double ItemSlotHeight => Math.Max(1d, ItemHeight);

    private double HorizontalGap => Math.Max(0d, HorizontalSpacing);

    private double VerticalGap => Math.Max(0d, VerticalSpacing);

    private double ItemPitchHeight => ItemSlotHeight + VerticalGap;

    private int ResolveColumns(double contentWidth, int count, bool unbounded)
    {
        if (count <= 0)
        {
            return 1;
        }

        if (unbounded)
        {
            // 未约束宽度时所有项排成一行，和 WrapPanel 一致；正常情况下竖向滚动只会量到视口宽。
            return count;
        }

        var pitch = ItemSlotWidth + HorizontalGap;
        var columns = (int)Math.Floor((Math.Max(0d, contentWidth) + HorizontalGap) / pitch);
        return Math.Max(1, columns);
    }

    private static int RowCount(int count, int columns)
        => count <= 0 ? 0 : (int)((count + (long)columns - 1) / columns);

    private double RowWidth(int columns, int count)
    {
        if (count <= 0 || columns <= 0)
        {
            return 0d;
        }

        return (columns * ItemSlotWidth) + ((columns - 1) * HorizontalGap);
    }

    private double RowExtent(int rows)
    {
        if (rows <= 0)
        {
            return 0d;
        }

        return (rows * ItemSlotHeight) + ((rows - 1) * VerticalGap);
    }

    private static int SafeCeil(double value)
        => value <= 0d ? 0 : value >= int.MaxValue ? int.MaxValue : (int)Math.Ceiling(value);
}
