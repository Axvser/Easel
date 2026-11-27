using System.Drawing;

namespace Easel.Visuals.Controls;

/// <summary>A fixed-cell virtualizer that lays items out in a uniform, row-major grid.</summary>
/// <remarks>
/// <para>
/// <see cref="Columns"/> and <see cref="Rows"/> are resolved once per layout from the source count:
/// a positive <see cref="Columns"/> fixes the column count; otherwise a positive <see cref="Rows"/> fixes the row
/// count and the columns follow; when both are auto the grid is roughly square. When both are positive the row count
/// is the larger of <see cref="Rows"/> and the rows the items actually need, so an explicit row count can add empty
/// trailing rows but never hides items.
/// </para>
/// <para>
/// Each cell is <see cref="CellWidth"/> by <see cref="CellHeight"/> DIP; a non-positive value derives the width from
/// the available width divided by the column count (and the height from the width, keeping cells square). The realized
/// window is the visible row-by-column rectangle expanded by <see cref="OverscanRows"/> and
/// <see cref="OverscanColumns"/>; index maps to a cell as <c>row = index / columns</c>, so the lookup stays
/// O(visible) at any source size instead of indexing every item in a spatial hash.
/// </para>
/// </remarks>
public class VirtualizingUniformGrid : VirtualizingPanel
{
    /// <summary>The fixed row count, or a non-positive value to derive it from <see cref="Columns"/> and the item count.</summary>
    public static readonly DependencyProperty RowsProperty =
        DependencyProperty.Register(
            nameof(Rows), typeof(int), typeof(VirtualizingUniformGrid),
            new PropertyMetadata(0) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The fixed column count, or a non-positive value to derive it from <see cref="Rows"/> and the item count.</summary>
    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(
            nameof(Columns), typeof(int), typeof(VirtualizingUniformGrid),
            new PropertyMetadata(0) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The cell width in DIP; a non-positive value fills the available width.</summary>
    public static readonly DependencyProperty CellWidthProperty =
        DependencyProperty.Register(
            nameof(CellWidth), typeof(double), typeof(VirtualizingUniformGrid),
            new PropertyMetadata(64d) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The cell height in DIP; a non-positive value follows the cell width.</summary>
    public static readonly DependencyProperty CellHeightProperty =
        DependencyProperty.Register(
            nameof(CellHeight), typeof(double), typeof(VirtualizingUniformGrid),
            new PropertyMetadata(64d) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The number of rows realized above and below the visible row band.</summary>
    public static readonly DependencyProperty OverscanRowsProperty =
        DependencyProperty.Register(
            nameof(OverscanRows), typeof(int), typeof(VirtualizingUniformGrid),
            new PropertyMetadata(1) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The number of columns realized left and right of the visible column band.</summary>
    public static readonly DependencyProperty OverscanColumnsProperty =
        DependencyProperty.Register(
            nameof(OverscanColumns), typeof(int), typeof(VirtualizingUniformGrid),
            new PropertyMetadata(1) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>Creates an empty uniform grid.</summary>
    public VirtualizingUniformGrid()
    {
    }

    /// <summary>The fixed row count, or a non-positive value to derive it from the other axis and the item count.</summary>
    public int Rows
    {
        get => (int)GetValue(RowsProperty)!;
        set => SetValue(RowsProperty, value);
    }

    /// <summary>The fixed column count, or a non-positive value to derive it from the other axis and the item count.</summary>
    public int Columns
    {
        get => (int)GetValue(ColumnsProperty)!;
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>The cell width in DIP; a non-positive value fills the available width.</summary>
    public double CellWidth
    {
        get => (double)GetValue(CellWidthProperty)!;
        set => SetValue(CellWidthProperty, value);
    }

    /// <summary>The cell height in DIP; a non-positive value follows the cell width.</summary>
    public double CellHeight
    {
        get => (double)GetValue(CellHeightProperty)!;
        set => SetValue(CellHeightProperty, value);
    }

    /// <summary>The number of rows realized above and below the visible row band.</summary>
    public int OverscanRows
    {
        get => (int)GetValue(OverscanRowsProperty)!;
        set => SetValue(OverscanRowsProperty, value);
    }

    /// <summary>The number of columns realized left and right of the visible column band.</summary>
    public int OverscanColumns
    {
        get => (int)GetValue(OverscanColumnsProperty)!;
        set => SetValue(OverscanColumnsProperty, value);
    }

    /// <summary>The row count used by the most recent layout pass.</summary>
    public int ComputedRows { get; private set; }

    /// <summary>The column count used by the most recent layout pass.</summary>
    public int ComputedColumns { get; private set; }

    /// <inheritdoc />
    protected override double VerticalLineStep
        => CellHeight > 0d ? CellHeight : CellWidth > 0d ? CellWidth : 24d;

    /// <inheritdoc />
    protected override double HorizontalLineStep
        => CellWidth > 0d ? CellWidth : 24d;

    /// <inheritdoc />
    protected override Size MeasureLayout(Size availableSize)
    {
        var padding = Padding;
        var count = SourceItems.Count;
        var (rows, columns) = ResolveGrid(count);
        var contentWidth = Math.Max(0d, availableSize.Width - padding.Horizontal);
        var cellWidth = ResolveCellWidth(contentWidth, columns);
        var cellHeight = ResolveCellHeight(cellWidth);
        var gridColumns = count > 0 ? columns : 0;
        var width = Math.Max(contentWidth, gridColumns * cellWidth);

        return new Size(
            SafeCeil(width + padding.Horizontal),
            SafeCeil((rows * cellHeight) + padding.Vertical));
    }

    /// <inheritdoc />
    protected override VirtualizationLayoutContext BuildLayout(VirtualizationLayoutRequest request)
    {
        var padding = request.Padding;
        var host = request.Host;
        var count = SourceItems.Count;
        var (rows, columns) = ResolveGrid(count);
        ComputedRows = rows;
        ComputedColumns = columns;

        var contentWidth = host is not null
            ? Math.Max(0f, host.Bounds.Width - host.Padding.Horizontal)
            : Math.Max(0f, request.Viewport.Width - padding.Horizontal);
        if (contentWidth <= 0f && request.FinalSize.Width > 0)
        {
            contentWidth = Math.Max(0f, request.FinalSize.Width - padding.Horizontal);
        }

        var cellWidth = ResolveCellWidth(contentWidth, columns);
        var cellHeight = ResolveCellHeight(cellWidth);
        var originX = host is not null
            ? ResolveHostLead(host, vertical: false) + host.Padding.Left
            : padding.Left;
        var originY = host is not null
            ? ResolveHostLead(host, vertical: true) + host.Padding.Top
            : padding.Top;

        var firstRow = 0;
        var lastRowExclusive = 0;
        var firstColumn = 0;
        var lastColumnExclusive = 0;
        if (count > 0)
        {
            var contentTop = Math.Max(0d, request.OffsetY - originY);
            var firstVisibleRow = XamlMath.Clamp((int)Math.Floor(contentTop / cellHeight), 0, rows);
            var lastVisibleRowExclusive = XamlMath.Clamp(
                (int)Math.Ceiling((contentTop + request.Viewport.Height) / cellHeight), firstVisibleRow, rows);
            if (lastVisibleRowExclusive <= firstVisibleRow)
            {
                lastVisibleRowExclusive = Math.Min(rows, firstVisibleRow + 1);
            }

            var overscanRows = Math.Max(0, OverscanRows);
            firstRow = Math.Max(0, firstVisibleRow - overscanRows);
            lastRowExclusive = Math.Min(rows, lastVisibleRowExclusive + overscanRows);

            var contentLeft = Math.Max(0d, request.OffsetX - originX);
            var firstVisibleColumn = XamlMath.Clamp((int)Math.Floor(contentLeft / cellWidth), 0, columns);
            var lastVisibleColumnExclusive = XamlMath.Clamp(
                (int)Math.Ceiling((contentLeft + request.Viewport.Width) / cellWidth), firstVisibleColumn, columns);
            if (lastVisibleColumnExclusive <= firstVisibleColumn)
            {
                lastVisibleColumnExclusive = Math.Min(columns, firstVisibleColumn + 1);
            }

            var overscanColumns = Math.Max(0, OverscanColumns);
            firstColumn = Math.Max(0, firstVisibleColumn - overscanColumns);
            lastColumnExclusive = Math.Min(columns, lastVisibleColumnExclusive + overscanColumns);
        }

        var items = new List<VirtualizationItemLayout>();
        for (var row = firstRow; row < lastRowExclusive; row++)
        {
            for (var column = firstColumn; column < lastColumnExclusive; column++)
            {
                var index = ((long)row * columns) + column;
                if (index < 0 || index >= count)
                {
                    continue;
                }

                var x = originX + (column * cellWidth);
                var y = originY + (row * cellHeight);
                items.Add(new VirtualizationItemLayout((int)index, new RectangleF(
                    (float)x, (float)y, (float)cellWidth, (float)cellHeight)));
            }
        }

        var extentWidth = Math.Max(contentWidth, count > 0 ? columns * cellWidth : 0d);
        var extent = new Size(
            SafeCeil(extentWidth + padding.Horizontal),
            SafeCeil((rows * cellHeight) + padding.Vertical));
        var viewport = new RectangleF(request.OffsetX, request.OffsetY, request.Viewport.Width, request.Viewport.Height);
        return new VirtualizationLayoutContext(
            items, extent, viewport, request.OffsetX, request.OffsetY, count);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, RowsProperty)
            || ReferenceEquals(e.Property, ColumnsProperty)
            || ReferenceEquals(e.Property, CellWidthProperty)
            || ReferenceEquals(e.Property, CellHeightProperty)
            || ReferenceEquals(e.Property, OverscanRowsProperty)
            || ReferenceEquals(e.Property, OverscanColumnsProperty))
        {
            InvalidateRealizedWindow();
            InvalidateMeasure();
        }
    }

    private (int Rows, int Columns) ResolveGrid(int count)
    {
        var fixedRows = Math.Max(0, Rows);
        var fixedColumns = Math.Max(0, Columns);
        if (count <= 0)
        {
            return (0, Math.Max(1, fixedColumns));
        }

        int columns;
        if (fixedColumns > 0)
        {
            columns = fixedColumns;
        }
        else if (fixedRows > 0)
        {
            columns = (int)((count + (long)fixedRows - 1) / fixedRows);
        }
        else
        {
            columns = (int)Math.Ceiling(Math.Sqrt(count));
        }

        columns = Math.Max(1, columns);
        var neededRows = (int)((count + (long)columns - 1) / columns);
        var rows = Math.Max(1, Math.Max(fixedRows, neededRows));
        return (rows, columns);
    }

    private double ResolveCellWidth(double contentWidth, int columns)
    {
        if (CellWidth > 0d)
        {
            return CellWidth;
        }

        return contentWidth > 0d && columns > 0 ? Math.Max(1d, contentWidth / columns) : 1d;
    }

    private double ResolveCellHeight(double cellWidth)
        => CellHeight > 0d ? CellHeight : Math.Max(1d, cellWidth);

    private static int SafeCeil(double value)
        => value <= 0d ? 0 : value >= int.MaxValue ? int.MaxValue : (int)Math.Ceiling(value);
}
