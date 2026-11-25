using System.Collections.ObjectModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using XamlColumnDefinition = Easel.Controls.ColumnDefinition;
using XamlGridLength = Easel.Controls.GridLength;
using XamlRowDefinition = Easel.Controls.RowDefinition;
using XamlVisibility = Easel.Controls.Visibility;

namespace Easel.Visuals.Controls;

/// <summary>Arranges children in rows and columns whose tracks are pixel, auto or star sized.</summary>
/// <remarks>
/// <para>
/// Tracks are declared through <see cref="RowDefinitions"/> and <see cref="ColumnDefinitions"/>; a grid with no
/// definitions behaves as one star row and one star column. Each child picks its cell through the attached
/// <see cref="GetRow"/>, <see cref="GetColumn"/>, <see cref="GetRowSpan"/> and <see cref="GetColumnSpan"/> values.
/// </para>
/// <para>
/// Auto tracks take the largest desired size of the children that touch them; a spanning child contributes only to
/// the first auto track in its span. Star tracks then divide the room left by the pixel and auto tracks by weight.
/// </para>
/// </remarks>
public class Grid : Panel
{
    /// <summary>The attached row index.</summary>
    public static readonly DependencyProperty RowProperty =
        DependencyProperty.RegisterAttached(
            "Row", typeof(int), typeof(Grid),
            new PropertyMetadata(0) { AffectsRender = false, AffectsMeasure = true });

    /// <summary>The attached column index.</summary>
    public static readonly DependencyProperty ColumnProperty =
        DependencyProperty.RegisterAttached(
            "Column", typeof(int), typeof(Grid),
            new PropertyMetadata(0) { AffectsRender = false, AffectsMeasure = true });

    /// <summary>The attached row span; values below one become one.</summary>
    public static readonly DependencyProperty RowSpanProperty =
        DependencyProperty.RegisterAttached(
            "RowSpan", typeof(int), typeof(Grid),
            new PropertyMetadata(1) { AffectsRender = false, AffectsMeasure = true });

    /// <summary>The attached column span; values below one become one.</summary>
    public static readonly DependencyProperty ColumnSpanProperty =
        DependencyProperty.RegisterAttached(
            "ColumnSpan", typeof(int), typeof(Grid),
            new PropertyMetadata(1) { AffectsRender = false, AffectsMeasure = true });

    /// <summary>Creates an empty grid.</summary>
    public Grid()
    {
        RowDefinitions = new TrackCollection<XamlRowDefinition>(this);
        ColumnDefinitions = new TrackCollection<XamlColumnDefinition>(this);
    }

    /// <summary>The row definitions; a grid with none behaves as one star row.</summary>
    public IList<XamlRowDefinition> RowDefinitions { get; }

    /// <summary>The column definitions; a grid with none behaves as one star column.</summary>
    public IList<XamlColumnDefinition> ColumnDefinitions { get; }

    /// <summary>Reads the zero-based row of <paramref name="element"/>.</summary>
    /// <param name="element">The element whose attached value is read.</param>
    /// <returns>The row index; zero when the element never set one.</returns>
    public static int GetRow(VisualElement element)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        return (int)element.GetValue(RowProperty)!;
    }

    /// <summary>Writes the zero-based row of <paramref name="element"/>.</summary>
    /// <param name="element">The element receiving the attached value.</param>
    /// <param name="value">The row index; negative values are treated as zero.</param>
    public static void SetRow(VisualElement element, int value)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        element.SetValue(RowProperty, Math.Max(0, value));
    }

    /// <summary>Reads the zero-based column of <paramref name="element"/>.</summary>
    /// <param name="element">The element whose attached value is read.</param>
    /// <returns>The column index; zero when the element never set one.</returns>
    public static int GetColumn(VisualElement element)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        return (int)element.GetValue(ColumnProperty)!;
    }

    /// <summary>Writes the zero-based column of <paramref name="element"/>.</summary>
    /// <param name="element">The element receiving the attached value.</param>
    /// <param name="value">The column index; negative values are treated as zero.</param>
    public static void SetColumn(VisualElement element, int value)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        element.SetValue(ColumnProperty, Math.Max(0, value));
    }

    /// <summary>Reads how many rows <paramref name="element"/> spans.</summary>
    /// <param name="element">The element whose attached value is read.</param>
    /// <returns>The span; at least one.</returns>
    public static int GetRowSpan(VisualElement element)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        return Math.Max(1, (int)element.GetValue(RowSpanProperty)!);
    }

    /// <summary>Writes how many rows <paramref name="element"/> spans.</summary>
    /// <param name="element">The element receiving the attached value.</param>
    /// <param name="value">The span; values below one are treated as one.</param>
    public static void SetRowSpan(VisualElement element, int value)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        element.SetValue(RowSpanProperty, Math.Max(1, value));
    }

    /// <summary>Reads how many columns <paramref name="element"/> spans.</summary>
    /// <param name="element">The element whose attached value is read.</param>
    /// <returns>The span; at least one.</returns>
    public static int GetColumnSpan(VisualElement element)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        return Math.Max(1, (int)element.GetValue(ColumnSpanProperty)!);
    }

    /// <summary>Writes how many columns <paramref name="element"/> spans.</summary>
    /// <param name="element">The element receiving the attached value.</param>
    /// <param name="value">The span; values below one are treated as one.</param>
    public static void SetColumnSpan(VisualElement element, int value)
    {
        if (element is null) throw new ArgumentNullException(nameof(element));
        element.SetValue(ColumnSpanProperty, Math.Max(1, value));
    }

    // 轨道解析的临时数组挂在实例上复用：每趟布局每张网格都要重算，局部 new 会让整页布局反复分配。
    // 数组只在轨道数变化时重建，其余时候原地覆盖（星号缓存每次清零）。
    private XamlGridLength[] _columnLengths = [];
    private XamlGridLength[] _rowLengths = [];
    private double[] _widths = [];
    private double[] _heights = [];
    private double[] _starWidths = [];
    private double[] _starHeights = [];
    private Size _tracksContent;
    private bool _tracksResolved;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        var content = new Size(
            Math.Max(0, availableSize.Width - (int)Math.Ceiling(padding.Horizontal)),
            Math.Max(0, availableSize.Height - (int)Math.Ceiling(padding.Vertical)));
        var (widths, heights) = ResolveTracks(content);
        return new Size(
            (int)Math.Ceiling(Sum(widths) + padding.Horizontal),
            (int)Math.Ceiling(Sum(heights) + padding.Vertical));
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        var padding = Padding;
        var content = new Size(
            Math.Max(0, finalSize.Width - (int)Math.Ceiling(padding.Horizontal)),
            Math.Max(0, finalSize.Height - (int)Math.Ceiling(padding.Vertical)));

        // 布局管线在同一次布局里先 Measure 后 Arrange；尺寸没变时轨道结果仍然有效，
        // 再走一遍 ResolveTracks 会把每个子项重新量一遍（那正是上一轮刚做过的事）。
        double[] widths;
        double[] heights;
        if (_tracksResolved && IsMeasureValid && _tracksContent == content)
        {
            widths = _widths;
            heights = _heights;
        }
        else
        {
            (widths, heights) = ResolveTracks(content);
        }

        var columnOffsets = Offsets(widths, padding.Left);
        var rowOffsets = Offsets(heights, padding.Top);

        var children = Children;
        for (var childIndex = 0; childIndex < children.Count; childIndex++)
        {
            if (children[childIndex] is not VisualElement child
                || child.Visibility == XamlVisibility.Collapsed)
            {
                continue;
            }

            var placement = PlacementOf(child);
            var column = XamlMath.Clamp(placement.Column, 0, widths.Length - 1);
            var row = XamlMath.Clamp(placement.Row, 0, heights.Length - 1);
            var columnSpan = XamlMath.Clamp(placement.ColumnSpan, 1, widths.Length - column);
            var rowSpan = XamlMath.Clamp(placement.RowSpan, 1, heights.Length - row);

            var slot = new RectangleF(
                columnOffsets[column],
                rowOffsets[row],
                (float)SpanSize(widths, column, columnSpan),
                (float)SpanSize(heights, row, rowSpan));
            child.Arrange(slot);
        }
    }

    private void EnsureTrackStorage(int columns, int rows)
    {
        if (_columnLengths.Length != columns)
        {
            _columnLengths = new XamlGridLength[columns];
            _widths = new double[columns];
            _starWidths = new double[columns];
        }

        if (_rowLengths.Length != rows)
        {
            _rowLengths = new XamlGridLength[rows];
            _heights = new double[rows];
            _starHeights = new double[rows];
        }
    }

    // 测一次子项得到自然尺寸，再按定义分到像素/自动/星号三种轨道。
    private (double[] Widths, double[] Heights) ResolveTracks(Size content)
    {
        // 没有定义的网格等价于一整行星号轨道；不再为这个默认轨道 new 定义对象。
        var columnCount = ColumnDefinitions.Count;
        var rowCount = RowDefinitions.Count;
        var defaultColumns = columnCount == 0;
        var defaultRows = rowCount == 0;
        if (defaultColumns)
        {
            columnCount = 1;
        }

        if (defaultRows)
        {
            rowCount = 1;
        }

        EnsureTrackStorage(columnCount, rowCount);

        // 定义只在解析开始时读一次，之后全部用数组，避免每趟布局把集合复制成数组。
        for (var index = 0; index < columnCount; index++)
        {
            var width = defaultColumns ? XamlGridLength.Star : ColumnDefinitions[index].Width;
            _columnLengths[index] = width;
            _widths[index] = width.IsAbsolute ? Math.Ceiling(Math.Max(0d, width.Value)) : 0d;
        }

        for (var index = 0; index < rowCount; index++)
        {
            var height = defaultRows ? XamlGridLength.Star : RowDefinitions[index].Height;
            _rowLengths[index] = height;
            _heights[index] = height.IsAbsolute ? Math.Ceiling(Math.Max(0d, height.Value)) : 0d;
        }

        Array.Clear(_starWidths, 0, columnCount);
        Array.Clear(_starHeights, 0, rowCount);

        var children = Children;
        for (var childIndex = 0; childIndex < children.Count; childIndex++)
        {
            if (children[childIndex] is not VisualElement child
                || child.Visibility == XamlVisibility.Collapsed)
            {
                continue;
            }

            var placement = PlacementOf(child);
            var column = XamlMath.Clamp(placement.Column, 0, columnCount - 1);
            var row = XamlMath.Clamp(placement.Row, 0, rowCount - 1);
            var columnSpan = XamlMath.Clamp(placement.ColumnSpan, 1, columnCount - column);
            var rowSpan = XamlMath.Clamp(placement.RowSpan, 1, rowCount - row);

            // 自动轨道沿该轴不设限地量：Auto 的语义是「由内容决定」，若仍按整张网格的可用尺寸量，
            // 里面一个默认单星号行的 Grid 就会把整段高度要走（Auto 行拿到整段，星号行归零）。
            // 星号轨道则相反 —— 它需要剩余空间才能分配，先按整个可用尺寸量，第二遍再按格子收紧。
            var measureWidth = MeasureAxis(_columnLengths, column, columnSpan, content.Width);
            var measureHeight = MeasureAxis(_rowLengths, row, rowSpan, content.Height);
            var measured = child.Measure(new Size(measureWidth, measureHeight));
            var autoColumn = FirstAutoTrack(_columnLengths, column, columnSpan);
            if (autoColumn >= 0)
            {
                _widths[autoColumn] = Math.Max(_widths[autoColumn], measured.Width);
            }

            var autoRow = FirstAutoTrack(_rowLengths, row, rowSpan);
            if (autoRow >= 0)
            {
                _heights[autoRow] = Math.Max(_heights[autoRow], measured.Height);
            }

            for (var index = column; index < column + columnSpan; index++)
            {
                if (_columnLengths[index].IsStar)
                {
                    _starWidths[index] = Math.Max(_starWidths[index], measured.Width);
                }
            }

            for (var index = row; index < row + rowSpan; index++)
            {
                if (_rowLengths[index].IsStar)
                {
                    _starHeights[index] = Math.Max(_starHeights[index], measured.Height);
                }
            }
        }

        DistributeStars(_columnLengths, _widths, content.Width, _starWidths);
        DistributeStars(_rowLengths, _heights, content.Height, _starHeights);

        // 轨道尺寸定下来之后，再按每个子项实际分到的格子量一次。第一遍用的是整张网格的可用尺寸，
        // 这对按自己尺寸绘制的子项够用，但 ScrollViewer 的期望尺寸取决于父级给它的可用宽度：
        // 按整格宽量过之后，它会把内容算成整格宽，安排进窄轨道时右对齐的内容被推出裁剪区，
        // 槽位行为量到的锚点也跟着偏到裁剪区外。
        for (var childIndex = 0; childIndex < children.Count; childIndex++)
        {
            if (children[childIndex] is not VisualElement child
                || child.Visibility == XamlVisibility.Collapsed)
            {
                continue;
            }

            var placement = PlacementOf(child);
            var column = XamlMath.Clamp(placement.Column, 0, columnCount - 1);
            var row = XamlMath.Clamp(placement.Row, 0, rowCount - 1);
            var columnSpan = XamlMath.Clamp(placement.ColumnSpan, 1, columnCount - column);
            var rowSpan = XamlMath.Clamp(placement.RowSpan, 1, rowCount - row);

            var slotWidth = (int)Math.Ceiling(Math.Max(0d, SpanSize(_widths, column, columnSpan)));
            var slotHeight = (int)Math.Ceiling(Math.Max(0d, SpanSize(_heights, row, rowSpan)));
            var measureWidth = MeasureAxis(_columnLengths, column, columnSpan, content.Width);
            var measureHeight = MeasureAxis(_rowLengths, row, rowSpan, content.Height);

            // 只有跨星号轨道的子项需要第二遍：它们的格子由分配结果决定。像素/自动轨道的测量空间
            // 已经等于最终格子（像素轨道给像素和，自动轨道给不设限），再量一次只是重复整棵子树。
            var needsReflow = HasStar(_columnLengths, column, columnSpan)
                              || HasStar(_rowLengths, row, rowSpan);
            if (needsReflow && (slotWidth != measureWidth || slotHeight != measureHeight))
            {
                child.Measure(new Size(slotWidth, slotHeight));
            }
        }

        _tracksContent = content;
        _tracksResolved = true;
        return (_widths, _heights);
    }

    // 星号轨道平分扣除像素与自动轨道后的剩余；最后一格吃掉取整误差，保证总宽等于可用宽。
    private static void DistributeStars(XamlGridLength[] lengths, double[] sizes, double available, double[] contentSizes)
    {
        // A star track measured with zero/infinite room has no share to divide; size it to its content.
        // int.MaxValue 是这套后端的「不设限」约定（见 WrapPanel）；容器在向下传递时会减掉 Padding，
        // 所以按半量级判定，而不是要求恰好等于 int.MaxValue。
        if (available <= 0d || double.IsInfinity(available) || available >= int.MaxValue / 2d)
        {
            for (var index = 0; index < lengths.Length; index++)
            {
                if (lengths[index].IsStar)
                {
                    sizes[index] = Math.Max(0d, contentSizes[index]);
                }
            }

            return;
        }

        var totalWeight = 0d;
        var lastStar = -1;
        var fixedSize = 0d;
        for (var index = 0; index < lengths.Length; index++)
        {
            if (lengths[index].IsStar)
            {
                totalWeight += Math.Max(0d, lengths[index].Value);
                lastStar = index;
            }
            else
            {
                fixedSize += sizes[index];
            }
        }

        if (totalWeight <= 0d || lastStar < 0 || available <= 0d)
        {
            return;
        }

        var remaining = Math.Max(0d, available - fixedSize);
        var assigned = 0d;
        for (var index = 0; index < lengths.Length; index++)
        {
            if (!lengths[index].IsStar)
            {
                continue;
            }

            var share = index == lastStar
                ? remaining - assigned
                : Math.Floor(remaining * (Math.Max(0d, lengths[index].Value) / totalWeight));
            sizes[index] = Math.Max(0d, share);
            assigned += share;
        }
    }

    private static int FirstAutoTrack(XamlGridLength[] lengths, int start, int span)
    {
        for (var index = start; index < start + span; index++)
        {
            if (lengths[index].IsAuto)
            {
                return index;
            }
        }

        return -1;
    }

    // 子项该按多大的空间测量：跨到星号轨道的按整张网格量（星号要用可用尺寸分配），
    // 只有自动轨道或像素轨道的按该轴不设限量（自动轨道的尺寸由内容给出）。
    private static int MeasureAxis(XamlGridLength[] lengths, int start, int span, double contentAxis)
    {
        var hasAuto = false;
        var size = 0d;
        for (var index = start; index < start + span; index++)
        {
            var length = lengths[index];
            if (length.IsStar)
            {
                return ToAxis(contentAxis);
            }

            if (length.IsAuto)
            {
                hasAuto = true;
            }
            else
            {
                size += Math.Max(0d, length.Value);
            }
        }

        return hasAuto ? int.MaxValue : ToAxis(size);
    }

    private static bool HasStar(XamlGridLength[] lengths, int start, int span)
    {
        for (var index = start; index < start + span; index++)
        {
            if (lengths[index].IsStar)
            {
                return true;
            }
        }

        return false;
    }

    private static int ToAxis(double value)
        => value <= 0d || double.IsNaN(value) || double.IsInfinity(value)
            ? 0
            : (int)Math.Min(int.MaxValue, Math.Ceiling(value));

    private static double SpanSize(double[] sizes, int start, int span)
    {
        var total = 0d;
        for (var index = start; index < start + span; index++)
        {
            total += sizes[index];
        }

        return total;
    }

    private static double Sum(double[] sizes)
    {
        var total = 0d;
        foreach (var size in sizes)
        {
            total += size;
        }

        return total;
    }

    private static float[] Offsets(double[] sizes, float origin)
    {
        var offsets = new float[sizes.Length];
        var position = origin;
        for (var index = 0; index < sizes.Length; index++)
        {
            offsets[index] = position;
            position += (float)sizes[index];
        }

        return offsets;
    }

    private static (int Row, int Column, int RowSpan, int ColumnSpan) PlacementOf(VisualElement element)
        => (GetRow(element), GetColumn(element), GetRowSpan(element), GetColumnSpan(element));

    // 定义集合被增删时让整个网格重新测量；修改已有定义的 Width/Height 不会触发通知。
    private sealed class TrackCollection<T>(Grid owner) : Collection<T>
    {
        protected override void InsertItem(int index, T item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));
            base.InsertItem(index, item);
            owner.InvalidateMeasure();
        }

        protected override void SetItem(int index, T item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));
            base.SetItem(index, item);
            owner.InvalidateMeasure();
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            owner.InvalidateMeasure();
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            owner.InvalidateMeasure();
        }
    }
}
