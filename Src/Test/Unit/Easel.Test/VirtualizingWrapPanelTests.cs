using System.Diagnostics;
using System.Drawing;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Coverage for <see cref="VC.VirtualizingWrapPanel"/>: the realized row band is checked against a brute-force slot
/// scan across widths, counts and offsets, and integration through a <see cref="VC.ScrollViewer"/> proves recycling,
/// both scroll modes and a bounded window at 100k items.
/// </summary>
[STATestClass]
public sealed class VirtualizingWrapPanelTests
{
    private const double ItemWidth = 40d;
    private const double ItemHeight = 20d;
    private const double GapX = 4d;
    private const double GapY = 6d;

    /// <summary>The test context used to print the measured performance numbers.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void WindowMatchesBruteForceAcrossWidthsCountsAndOffsets()
    {
        var sources = new Dictionary<int, List<object?>>();
        foreach (var count in new[] { 0, 1, 3, 10, 97, 1000 })
        {
            sources[count] = Items(count);
        }

        foreach (var width in new[] { 89, 100, 200, 233, 400 })
        {
            foreach (var pair in sources)
            {
                foreach (var overscan in new[] { 0, 1, 3 })
                {
                    using var panel = new ProbeWrapPanel
                    {
                        ItemWidth = ItemWidth,
                        ItemHeight = ItemHeight,
                        HorizontalSpacing = GapX,
                        VerticalSpacing = GapY,
                        OverscanRows = overscan,
                        ItemsSource = pair.Value,
                    };

                    var viewport = new Size(width, 137);
                    // 空源的列数是实现约定的 1，不是按宽度能塞下几列。
                    var columns = pair.Key <= 0 ? 1 : FitColumns(viewport.Width);
                    var rows = RowCount(pair.Key, columns);
                    var extentHeight = RowExtent(rows);
                    var maxOffset = Math.Max(0d, extentHeight - viewport.Height);
                    var candidates = new[]
                    {
                        0f,
                        0.5f,
                        1f,
                        3f,
                        77f,
                        (float)Math.Max(0d, maxOffset - 10.5d),
                        (float)maxOffset,
                    };

                    void Check(float offset)
                    {
                        var context = panel.Compute(viewport, viewport, 0f, offset);

                        Assert.AreEqual(columns, panel.Columns,
                            $"count {pair.Key} at {width}px must fit {columns} columns");
                        Assert.AreEqual(
                            Math.Max(viewport.Width, RowWidth(columns, pair.Key)), context.Extent.Width, 1f,
                            $"count {pair.Key} extent width");
                        Assert.AreEqual(extentHeight, context.Extent.Height, 1f,
                            $"count {pair.Key} extent height");

                        var expected = BruteWindow(pair.Key, columns, overscan, viewport, offset);
                        AssertWindow(expected, context.Items);
                    }

                    foreach (var candidate in candidates)
                    {
                        // 直接调用不会像 ScrollViewer 那样夹取偏移；参考实现只覆盖 [0, maxOffset]。
                        Check((float)Math.Min(candidate, maxOffset));
                    }

                    var random = new Random((width * 31) + pair.Key + overscan);
                    for (var draw = 0; draw < 8; draw++)
                    {
                        Check((float)(random.NextDouble() * maxOffset));
                    }
                }
            }
        }
    }

    [TestMethod]
    public void OverscanOnlyAddsRowsAroundTheVisibleBand()
    {
        using var panel = new ProbeWrapPanel
        {
            ItemWidth = ItemWidth,
            ItemHeight = ItemHeight,
            HorizontalSpacing = GapX,
            VerticalSpacing = GapY,
            OverscanRows = 0,
            ItemsSource = Items(500),
        };
        var viewport = new Size(200, 100);
        var columns = FitColumns(viewport.Width);
        var visible = panel.Compute(viewport, viewport, 0f, 0f).Items.Count;

        panel.OverscanRows = 2;
        var expanded = panel.Compute(viewport, viewport, 0f, 0f).Items.Count;

        Assert.AreEqual(columns * 2, expanded - visible, "two extra rows of overscan add two full rows");
        Assert.IsGreaterThan(visible, expanded);
    }

    [TestMethod]
    public void EmptyAndSubRowSourcesReportAnEmptyWindow()
    {
        using var empty = new ProbeWrapPanel
        {
            ItemWidth = ItemWidth,
            ItemHeight = ItemHeight,
            ItemsSource = Items(0),
        };
        var viewport = new Size(200, 100);
        var context = empty.Compute(viewport, viewport, 0f, 0f);

        Assert.HasCount(0, context.Items, "an empty source realizes nothing");
        Assert.AreEqual(0, context.Extent.Height);
        Assert.AreEqual(viewport.Width, context.Extent.Width);

        using var single = new ProbeWrapPanel
        {
            ItemWidth = ItemWidth,
            ItemHeight = ItemHeight,
            ItemsSource = Items(1),
        };
        var one = single.Compute(viewport, viewport, 0f, 0f);

        Assert.HasCount(1, one.Items);
        Assert.AreEqual(0f, one.Items[0].Bounds.X, 0.01f);
        Assert.AreEqual(0f, one.Items[0].Bounds.Y, 0.01f);
        Assert.AreEqual(ItemHeight, one.Extent.Height, 0.01f);
    }

    [TestMethod]
    public void ExactlyOneScreenRealizesEveryItemAndTheNextWraps()
    {
        using var panel = new ProbeWrapPanel
        {
            ItemWidth = 40d,
            ItemHeight = 20d,
            OverscanRows = 0,
            ItemsSource = Items(25),
        };
        var viewport = new Size(200, 100);

        var exact = panel.Compute(viewport, viewport, 0f, 0f);
        Assert.AreEqual(5, panel.Columns, "five 40px columns fit 200px");
        Assert.HasCount(25, exact.Items, "an exact screen realizes every item");
        Assert.AreEqual(100f, exact.Extent.Height, 0.01f, "five 20px rows make one screen");

        panel.ItemsSource = Items(26);
        var extra = panel.Compute(viewport, viewport, 0f, 0f);
        Assert.HasCount(25, extra.Items, "the wrapping sixth item is off the first screen");
        Assert.AreEqual(120f, extra.Extent.Height, 0.01f, "count 26 grows to six rows");

        var end = panel.Compute(viewport, viewport, 0f, 20f);
        Assert.AreEqual(25, end.Items[^1].Index, "scrolling one row reaches the wrapping item");
        Assert.IsTrue(end.Items.Any(item => item.Index / 5 == 5), "the last row is a cross-column wrap");
    }

    [TestMethod]
    public void ChangingTheAvailableWidthRebuildsColumnsAndExtent()
    {
        using var panel = new ProbeWrapPanel
        {
            ItemWidth = ItemWidth,
            ItemHeight = ItemHeight,
            HorizontalSpacing = GapX,
            VerticalSpacing = GapY,
            ItemsSource = Items(1000),
        };

        var wide = new Size(400, 120);
        var wideContext = panel.Compute(wide, wide, 0f, 0f);
        var wideColumns = FitColumns(wide.Width);
        Assert.AreEqual(wideColumns, panel.Columns, "the wide layout fixes the column count");

        var narrow = new Size(100, 120);
        var narrowContext = panel.Compute(narrow, narrow, 0f, 0f);
        var narrowColumns = FitColumns(narrow.Width);
        Assert.AreEqual(narrowColumns, panel.Columns, "the narrow layout rebinds the column count");

        Assert.IsGreaterThan(narrowColumns, wideColumns);
        Assert.IsGreaterThan(wideContext.Extent.Height, narrowContext.Extent.Height,
            "fewer columns mean more rows and a taller extent");

        // 再量一次宽视口：列数、窗口与 extent 都要回到宽布局。
        var again = panel.Compute(wide, wide, 0f, 0f);
        Assert.AreEqual(wideColumns, panel.Columns);
        AssertWindow(BruteWindow(1000, wideColumns, panel.OverscanRows, wide, 0f), again.Items);
    }

    [TestMethod]
    public void ScrollingRecyclesInsteadOfCreating()
    {
        const int count = 2000;
        using var viewer = new VC.ScrollViewer { Width = 200, Height = 100 };
        using var panel = new ProbeWrapPanel
        {
            ItemWidth = ItemWidth,
            ItemHeight = ItemHeight,
            HorizontalSpacing = GapX,
            VerticalSpacing = GapY,
            OverscanRows = 1,
            ItemsSource = Items(count),
        };
        viewer.Content = panel;

        Layout(viewer);

        var columns = panel.Columns;
        Assert.IsGreaterThan(0, columns);
        Assert.IsGreaterThan(0, panel.RealizedCount);
        var initialCreated = panel.Created;
        Assert.AreEqual(initialCreated, panel.RealizedCount, "the first window creates one visual per realized item");
        Assert.AreEqual(initialCreated, panel.Items.Count);
        Assert.AreEqual(panel.Items.Count, DistinctCount(panel.Items), "no visual is realized twice");

        var window = panel.Items.ToArray();
        var pitch = ItemHeight + GapY;
        var rows = RowCount(count, columns);
        var viewportHeight = (int)Math.Ceiling(viewer.ViewportHeight);

        // 预热到窗口最大的一屏：顶部只有下方 overscan，离开顶部后才两侧都算，窗口会多一行。
        // 先把这一行实现出来，再断言连续滚动不再创建。
        viewer.VerticalOffset = (float)(pitch * 3);
        Layout(viewer);
        var created = panel.Created;

        for (var step = 1; step <= 30; step++)
        {
            var offset = (float)(step * pitch * 3);
            viewer.VerticalOffset = offset;
            Layout(viewer);

            Assert.AreEqual(columns, panel.Columns, "the width is unchanged so the column count is too");
            Assert.AreEqual(0, panel.Created - created, "scrolling never creates another visual");
            Assert.IsLessThan(count, panel.RealizedCount, "the window stays bounded");
            Assert.AreEqual(panel.Items.Count, DistinctCount(panel.Items), "pooled visuals never appear twice");

            var expected = BruteRows(rows, panel.OverscanRows, offset, viewportHeight);
            var values = DataContexts(panel);
            Assert.AreEqual(columns, panel.Columns);
            for (var position = 0; position < values.Count; position++)
            {
                var row = values[position]!.Value / columns;
                Assert.IsGreaterThanOrEqualTo(expected.First, row, "no realized row above the band");
                Assert.IsLessThan(expected.LastExclusive, row, "no realized row below the band");
            }
        }

        Assert.IsGreaterThan(0, panel.Recycled, "rows that left the band returned to the pool");

        // 离开窗口的视觉必须已经摘离宿主，而不是留在 Children 里。
        var current = panel.Items.ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var visual in window)
        {
            if (!current.Contains(visual))
            {
                Assert.IsNull(visual.ParentElement, "a recycled visual is detached from the host");
                Assert.IsFalse(panel.Children.Contains(visual), "a recycled visual is no longer a child");
            }
        }

        Assert.IsTrue(DataContexts(panel).SequenceEqual(DataContexts(panel).OrderBy(value => value)),
            "the realized order is the row-major index order");

        // 滚到底再回到顶部：窗口仍不增长，且末项被实现。
        viewer.VerticalOffset = float.MaxValue;
        Layout(viewer);
        Assert.AreEqual(count - 1, DataContexts(panel)[^1]!.Value, "the last item is realized at the end");
        Assert.AreEqual(created, panel.Created);

        viewer.VerticalOffset = 0f;
        Layout(viewer);
        Assert.AreEqual(0, DataContexts(panel)[0]!.Value, "the window returns to the first item");
        Assert.AreEqual(created, panel.Created, "a full round trip creates nothing new");
    }

    [TestMethod]
    public void PixelAndContentScrollRealizeTheSameIndices()
    {
        var source = Items(3000);

        using var pixelViewer = new VC.ScrollViewer { Width = 200, Height = 100 };
        using var pixel = new VC.VirtualizingWrapPanel
        {
            ItemWidth = ItemWidth,
            ItemHeight = ItemHeight,
            VerticalSpacing = GapY,
            ItemsSource = source,
        };
        pixelViewer.Content = pixel;
        Layout(pixelViewer);

        using var contentViewer = new VC.ScrollViewer
        {
            Width = 200,
            Height = 100,
            CanContentScroll = true,
        };
        using var content = new VC.VirtualizingWrapPanel
        {
            ItemWidth = ItemWidth,
            ItemHeight = ItemHeight,
            VerticalSpacing = GapY,
            ItemsSource = source,
        };
        contentViewer.Content = content;
        Layout(contentViewer);

        foreach (var offset in new[] { 0f, 250f, 1000f, 5000f })
        {
            pixelViewer.VerticalOffset = offset;
            Layout(pixelViewer);
            contentViewer.VerticalOffset = offset;
            Layout(contentViewer);

            Assert.AreEqual(pixel.Columns, content.Columns, "both modes fit the same columns");
            CollectionAssert.AreEqual(
                DataContexts(pixel), DataContexts(content),
                "both scroll modes realize the same index window");
        }

        Assert.AreEqual(pixelViewer.VerticalOffset, contentViewer.VerticalOffset, 0.1d,
            "content scrolling routes the same clamped offset");
    }

    [TestMethod]
    public void ContentScrollReportsTheFullExtentAndPositionsRelativeToTheViewport()
    {
        const int count = 5000;
        using var viewer = new VC.ScrollViewer
        {
            Width = 200,
            Height = 100,
            CanContentScroll = true,
        };
        using var panel = new VC.VirtualizingWrapPanel
        {
            ItemWidth = ItemWidth,
            ItemHeight = ItemHeight,
            VerticalSpacing = GapY,
            ItemsSource = Items(count),
        };
        viewer.Content = panel;
        Layout(viewer);

        var topColumns = panel.Columns;
        Assert.AreEqual(RowExtent(RowCount(count, topColumns)), viewer.ExtentHeight, 0.5f,
            "the panel reports every row");
        Assert.AreEqual(0f, panel.Items[0].Bounds.Y, 0.5f, "the first window starts at the document top");

        viewer.VerticalOffset = 2000f;
        Layout(viewer);

        // 竖向滚动条出现后视口变窄，列数可能变化；以这一轮的实际列数为准。
        var columns = panel.Columns;
        var rows = RowCount(count, columns);
        var expected = BruteRows(rows, panel.OverscanRows, 2000f, (int)Math.Ceiling(panel.ViewportHeight));
        Assert.AreEqual(expected.First, panel.FirstRealizedIndex / columns,
            "the first realized row is the overscanned band start");
        Assert.AreEqual(expected.LastExclusive - 1,
            (panel.FirstRealizedIndex + panel.RealizedCount - 1) / columns,
            "the last realized row is the band end");

        // 内容滚动把记录整体上移 offset，overscan 行可以落在视口上方（负 y）。
        var first = panel.Items[0];
        var firstRow = ((int)first.DataContext! / columns);
        var expectedY = (firstRow * (ItemHeight + GapY)) - 2000d;
        Assert.AreEqual((float)expectedY, first.Bounds.Y, 0.5f,
            "records are positioned relative to the viewport in content-scroll mode");
    }

    [TestMethod]
    public void HundredThousandItemsStayWithinABudget()
    {
        const int count = 100_000;
        var source = Items(count);
        using var viewer = new VC.ScrollViewer { Width = 400, Height = 224 };
        using var panel = new VC.VirtualizingWrapPanel
        {
            ItemWidth = ItemWidth,
            ItemHeight = ItemHeight,
            HorizontalSpacing = GapX,
            VerticalSpacing = GapY,
            ItemsSource = source,
        };
        viewer.Content = panel;

        var firstLayout = Stopwatch.StartNew();
        Layout(viewer);
        firstLayout.Stop();

        Assert.IsGreaterThan(0, panel.RealizedCount);
        Assert.IsLessThan(500, panel.RealizedCount, "a 224px viewport realizes a bounded row band");
        Assert.IsLessThan(100_000, panel.RealizedCount);

        var jump = Stopwatch.StartNew();
        viewer.VerticalOffset = 60_000f;
        Layout(viewer);
        jump.Stop();

        Assert.AreEqual(60_000f / (ItemHeight + GapY), panel.FirstRealizedIndex / panel.Columns, 2d,
            "the band follows the offset divided by the row pitch");

        var random = new Random(20261127);
        var randomJumps = Stopwatch.StartNew();
        for (var step = 0; step < 25; step++)
        {
            viewer.VerticalOffset = random.Next(1_000_000);
            Layout(viewer);
            Assert.IsLessThan(500, panel.RealizedCount, "a random jump never enlarges the window");
        }

        randomJumps.Stop();

        TestContext.WriteLine(
            $"wrap 100k: first layout {firstLayout.ElapsedMilliseconds} ms · jump {jump.ElapsedMilliseconds} ms · " +
            $"25 jumps {randomJumps.ElapsedMilliseconds} ms · realized {panel.RealizedCount} · columns {panel.Columns}");

        Assert.IsLessThan(5_000L, firstLayout.ElapsedMilliseconds, "the first 100k window stays under 5s");
        Assert.IsLessThan(2_000L, jump.ElapsedMilliseconds, "a single jump stays under 2s");
        Assert.IsLessThan(10_000L, randomJumps.ElapsedMilliseconds, "25 random jumps stay under 10s");
    }

    // ---- 参考实现：与控件独立，用整槽矩形暴力扫描可见行，不套用 floor/ceil 那一套。 ----

    private static SortedDictionary<int, RectangleF> BruteWindow(
        int count, int columns, int overscan, Size viewport, float offsetY)
        => BruteWindow(count, columns, overscan, viewport, offsetY, originX: 0f, originY: 0f);

    private static SortedDictionary<int, RectangleF> BruteWindow(
        int count, int columns, int overscan, Size viewport, float offsetY, float originX, float originY)
    {
        var result = new SortedDictionary<int, RectangleF>();
        var rows = RowCount(count, columns);
        var pitchX = ItemWidth + GapX;
        var pitchY = ItemHeight + GapY;
        var (first, lastExclusive) = BruteRows(rows, overscan, offsetY, viewport.Height, originY);

        for (var row = first; row < lastExclusive; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var index = (row * columns) + column;
                if (index >= count)
                {
                    break;
                }

                result[index] = new RectangleF(
                    (float)(originX + (column * pitchX)),
                    (float)(originY + (row * pitchY)),
                    (float)ItemWidth,
                    (float)ItemHeight);
            }
        }

        return result;
    }

    private static (int First, int LastExclusive) BruteRows(
        int rows, int overscan, float offsetY, int viewportHeight)
        => BruteRows(rows, overscan, offsetY, viewportHeight, originY: 0f);

    private static (int First, int LastExclusive) BruteRows(
        int rows, int overscan, float offsetY, int viewportHeight, float originY)
    {
        if (rows <= 0)
        {
            return (0, 0);
        }

        var pitch = ItemHeight + GapY;
        var visibleFirst = -1;
        var visibleLast = -1;
        for (var row = 0; row < rows; row++)
        {
            var top = originY + (row * pitch);
            if (top < offsetY + viewportHeight && top + pitch > offsetY)
            {
                if (visibleFirst < 0)
                {
                    visibleFirst = row;
                }

                visibleLast = row;
            }
        }

        if (visibleFirst < 0)
        {
            // 视口落在末尾之外：夹到最后一行的位置，避免空窗口。
            visibleFirst = visibleLast = XamlMath.Clamp(
                (int)Math.Floor((offsetY - originY) / pitch), 0, rows - 1);
        }

        return (Math.Max(0, visibleFirst - overscan), Math.Min(rows, visibleLast + 1 + overscan));
    }

    private static void AssertWindow(
        SortedDictionary<int, RectangleF> expected, IReadOnlyList<VC.VirtualizationItemLayout> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count, "the realized window size matches the brute force");
        CollectionAssert.AreEqual(
            expected.Keys.ToList(), actual.Select(item => item.Index).ToList(),
            "the realized indices match the brute force");

        foreach (var item in actual)
        {
            Assert.AreEqual(item.Index, item.Order, "order follows the row-major index");
            var bounds = expected[item.Index];
            Assert.AreEqual(bounds.X, item.Bounds.X, 0.01f, $"index {item.Index} x");
            Assert.AreEqual(bounds.Y, item.Bounds.Y, 0.01f, $"index {item.Index} y");
            Assert.AreEqual(bounds.Width, item.Bounds.Width, 0.01f, $"index {item.Index} width");
            Assert.AreEqual(bounds.Height, item.Bounds.Height, 0.01f, $"index {item.Index} height");
        }
    }

    private static int FitColumns(int width)
    {
        var columns = 0;
        while (((columns + 1) * ItemWidth) + (columns * GapX) <= width + 1e-6)
        {
            columns++;
        }

        return Math.Max(1, columns);
    }

    private static int RowCount(int count, int columns)
        => count <= 0 ? 0 : (count + columns - 1) / columns;

    private static double RowWidth(int columns, int count)
        => count <= 0 || columns <= 0 ? 0d : (columns * ItemWidth) + ((columns - 1) * GapX);

    private static double RowExtent(int rows)
        => rows <= 0 ? 0d : (rows * ItemHeight) + ((rows - 1) * GapY);

    private static int DistinctCount(IReadOnlyList<VisualElement> items)
        => items.ToHashSet(ReferenceEqualityComparer.Instance).Count;

    private static List<int?> DataContexts(VC.VirtualizingWrapPanel panel)
        => panel.Items.Select(visual => (int?)visual.DataContext).ToList();

    private static List<object?> Items(int count)
    {
        var items = new List<object?>(count);
        for (var index = 0; index < count; index++)
        {
            items.Add(index);
        }

        return items;
    }

    private static void Layout(VisualElement root)
    {
        var width = root.Width > 0d && !double.IsNaN(root.Width) ? (int)root.Width : 400;
        var height = root.Height > 0d && !double.IsNaN(root.Height) ? (int)root.Height : 224;
        Layout(root, width, height);
    }

    private static void Layout(VisualElement root, int width, int height)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new RectangleF(0f, 0f, width, height));
    }

    private sealed class ProbeWrapPanel : VC.VirtualizingWrapPanel
    {
        public int Created { get; private set; }

        public int Recycled { get; private set; }

        public VC.VirtualizationLayoutContext Compute(
            Size finalSize, Size viewport, float offsetX, float offsetY)
            => BuildLayout(new VC.VirtualizationLayoutRequest(
                finalSize, viewport, offsetX, offsetY, usingOwnScroll: false, host: null, Padding));

        protected override VisualElement CreateItemVisual(object? item)
        {
            Created++;
            return base.CreateItemVisual(item);
        }

        protected override void RecycleItem(int index, VisualElement element)
        {
            Recycled++;
            base.RecycleItem(index, element);
        }
    }
}
