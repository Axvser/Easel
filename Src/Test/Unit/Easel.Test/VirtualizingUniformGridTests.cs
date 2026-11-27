using System.Diagnostics;
using System.Drawing;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Coverage for <see cref="VC.VirtualizingUniformGrid"/>: the documented row/column resolution, the realized
/// row-by-column window against a brute-force scan, recycling through a <see cref="VC.ScrollViewer"/>, horizontal
/// windowing, both scroll modes and a bounded window at 100k cells.
/// </summary>
[STATestClass]
public sealed class VirtualizingUniformGridTests
{
    private const double CellWidth = 50d;
    private const double CellHeight = 30d;

    /// <summary>The test context used to print the measured performance numbers.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void GridResolutionFollowsTheDocumentedRule()
    {
        // 只给列：行数由数量补足。
        AssertGrid(rows: 0, columns: 7, count: 100, expectedRows: 15, expectedColumns: 7);
        // 只给行：列数由数量补足，行数恰好是指定值。
        AssertGrid(rows: 4, columns: 0, count: 100, expectedRows: 4, expectedColumns: 25);
        // 都不给：近似正方。
        AssertGrid(rows: 0, columns: 0, count: 100, expectedRows: 10, expectedColumns: 10);
        AssertGrid(rows: 0, columns: 0, count: 101, expectedRows: 10, expectedColumns: 11);
        // 都给：列数固定，行数取“需要”与“指定”的较大者。
        AssertGrid(rows: 20, columns: 5, count: 10, expectedRows: 20, expectedColumns: 5);
        AssertGrid(rows: 2, columns: 5, count: 100, expectedRows: 20, expectedColumns: 5);
        // 空源：没有行，列数是固定列或 1。
        AssertGrid(rows: 0, columns: 0, count: 0, expectedRows: 0, expectedColumns: 1);
        AssertGrid(rows: 9, columns: 6, count: 0, expectedRows: 0, expectedColumns: 6);
    }

    [TestMethod]
    public void WindowMatchesBruteForceAcrossCountsModesAndOffsets()
    {
        var modes = new (int Rows, int Columns)[]
        {
            (0, 7),
            (4, 0),
            (0, 0),
            (20, 5),
        };
        var viewports = new[] { new Size(160, 90), new Size(400, 240), new Size(55, 33) };
        var counts = new[] { 0, 1, 5, 17, 100, 1000 };

        foreach (var mode in modes)
        {
            foreach (var count in counts)
            {
                using var panel = new ProbeGrid
                {
                    CellWidth = CellWidth,
                    CellHeight = CellHeight,
                    Rows = mode.Rows,
                    Columns = mode.Columns,
                    OverscanRows = 1,
                    OverscanColumns = 1,
                    ItemsSource = Items(count),
                };

                var (expectedRows, expectedColumns) = ExpectedGrid(count, mode.Rows, mode.Columns);
                foreach (var viewport in viewports)
                {
                    var maxX = Math.Max(0d, (expectedColumns * CellWidth) - viewport.Width);
                    var maxY = Math.Max(0d, (expectedRows * CellHeight) - viewport.Height);
                    var candidates = new[]
                    {
                        new PointF(0f, 0f),
                        new PointF(0.5f, 0.5f),
                        new PointF((float)Math.Min(75d, maxX), (float)Math.Min(75d, maxY)),
                        new PointF((float)maxX, (float)maxY),
                    };

                    void Check(PointF offset)
                    {
                        var context = panel.Compute(viewport, viewport, offset.X, offset.Y);

                        Assert.AreEqual(expectedRows, panel.ComputedRows,
                            $"count {count} rows for mode ({mode.Rows},{mode.Columns})");
                        Assert.AreEqual(expectedColumns, panel.ComputedColumns,
                            $"count {count} columns for mode ({mode.Rows},{mode.Columns})");

                        var expectedWidth = Math.Max(viewport.Width, count > 0 ? expectedColumns * CellWidth : 0d);
                        Assert.AreEqual(expectedWidth, context.Extent.Width, 1f, "extent width");
                        Assert.AreEqual(expectedRows * CellHeight, context.Extent.Height, 1f, "extent height");

                        var expected = BruteWindow(
                            count, expectedRows, expectedColumns, viewport, offset, overscanRows: 1, overscanColumns: 1);
                        AssertWindow(expected, context.Items);
                    }

                    foreach (var candidate in candidates)
                    {
                        // 直接调用不会夹取偏移；参考实现只覆盖可滚动范围。
                        Check(new PointF(
                            (float)Math.Min(candidate.X, maxX), (float)Math.Min(candidate.Y, maxY)));
                    }

                    var random = new Random((viewport.Width * 31) + count + mode.Columns);
                    for (var draw = 0; draw < 8; draw++)
                    {
                        Check(new PointF(
                            (float)(random.NextDouble() * maxX), (float)(random.NextDouble() * maxY)));
                    }
                }
            }
        }
    }

    [TestMethod]
    public void OverscanExpandsBothAxesButStaysBounded()
    {
        using var panel = new ProbeGrid
        {
            CellWidth = 40d,
            CellHeight = 20d,
            Columns = 8,
            OverscanRows = 0,
            OverscanColumns = 0,
            ItemsSource = Items(500),
        };
        var viewport = new Size(100, 100);
        var tight = panel.Compute(viewport, viewport, 300f, 60f).Items.Count;

        panel.OverscanRows = 1;
        panel.OverscanColumns = 2;
        var expanded = panel.Compute(viewport, viewport, 300f, 60f).Items.Count;

        Assert.IsGreaterThan(tight, expanded, "adding overscan grows the realized window");
    }

    [TestMethod]
    public void RuntimeGridChangeRebuildsRowsColumnsAndExtent()
    {
        using var panel = new ProbeGrid
        {
            CellWidth = CellWidth,
            CellHeight = CellHeight,
            Columns = 5,
            ItemsSource = Items(100),
        };
        var viewport = new Size(200, 100);

        var fiveWide = panel.Compute(viewport, viewport, 0f, 0f);
        Assert.AreEqual(5, panel.ComputedColumns);
        Assert.AreEqual(20, panel.ComputedRows);
        Assert.AreEqual(600f, fiveWide.Extent.Height, 0.5f, "twenty 30px rows");

        // 改成四行、列数自动：25 列 × 50px，宽于视口。
        panel.Columns = 0;
        panel.Rows = 4;
        var fourRows = panel.Compute(viewport, viewport, 0f, 0f);
        Assert.AreEqual(4, panel.ComputedRows);
        Assert.AreEqual(25, panel.ComputedColumns);
        Assert.AreEqual(1250f, fourRows.Extent.Width, 0.5f, "the fixed cells widen the extent");
        Assert.AreEqual(120f, fourRows.Extent.Height, 0.5f, "four 30px rows");

        var mid = panel.Compute(viewport, viewport, 400f, 0f);
        var expected = BruteWindow(100, 4, 25, viewport, new PointF(400f, 0f), 1, 1);
        AssertWindow(expected, mid.Items);
    }

    [TestMethod]
    public void AutoCellWidthFillsTheAvailableWidth()
    {
        using var panel = new ProbeGrid
        {
            CellWidth = 0d,
            CellHeight = 20d,
            Columns = 5,
            ItemsSource = Items(50),
        };
        var viewport = new Size(400, 100);
        var context = panel.Compute(viewport, viewport, 0f, 0f);

        Assert.AreEqual(400f, context.Extent.Width, 1f, "auto cells span the available width");
        Assert.AreEqual(80f, context.Items[0].Bounds.Width, 0.01f, "five auto cells share 400px");

        using var heightFollows = new ProbeGrid
        {
            CellWidth = 0d,
            CellHeight = 0d,
            Columns = 5,
            ItemsSource = Items(50),
        };
        var square = heightFollows.Compute(viewport, viewport, 0f, 0f);
        Assert.AreEqual(square.Items[0].Bounds.Width, square.Items[0].Bounds.Height, 0.01f,
            "an auto height follows the cell width");
    }

    [TestMethod]
    public void ScrollingRecyclesAndWindowsBothAxes()
    {
        const int count = 5000;
        using var viewer = new VC.ScrollViewer { Width = 200, Height = 100 };
        using var panel = new ProbeGrid
        {
            CellWidth = 30d,
            CellHeight = 20d,
            Columns = 6,
            OverscanRows = 0,
            OverscanColumns = 0,
            ItemsSource = Items(count),
        };
        viewer.Content = panel;
        Layout(viewer);

        Assert.AreEqual(6, panel.ComputedColumns);
        Assert.AreEqual((count + 5) / 6, panel.ComputedRows);
        Assert.IsGreaterThan(0, panel.RealizedCount);
        var created = panel.Created;
        Assert.AreEqual(created, panel.Items.Count);
        Assert.AreEqual(panel.Items.Count, DistinctCount(panel.Items));

        var initial = panel.Items.ToArray();
        var viewportHeight = (int)Math.Ceiling(viewer.ViewportHeight);
        var rows = panel.ComputedRows;

        for (var step = 1; step <= 30; step++)
        {
            var offset = (float)(step * 20d * 3d);
            viewer.VerticalOffset = offset;
            Layout(viewer);

            Assert.AreEqual(0, panel.Created - created, "scrolling never creates another visual");
            Assert.AreEqual(panel.Items.Count, DistinctCount(panel.Items), "pooled visuals never appear twice");

            var range = BruteAxis(rows, 20d, 1, offset, viewportHeight);
            foreach (var value in DataContexts(panel))
            {
                var row = value!.Value / 6;
                Assert.IsGreaterThanOrEqualTo(range.First, row, "no realized row above the band");
                Assert.IsLessThan(range.LastExclusive, row, "no realized row below the band");
            }
        }

        Assert.IsGreaterThan(0, panel.Recycled, "rows that left the band returned to the pool");

        var current = panel.Items.ToHashSet(ReferenceEqualityComparer.Instance);
        foreach (var visual in initial)
        {
            if (!current.Contains(visual))
            {
                Assert.IsNull(visual.ParentElement, "a recycled visual is detached from the host");
                Assert.IsFalse(panel.Children.Contains(visual));
            }
        }

        Assert.IsTrue(DataContexts(panel).SequenceEqual(DataContexts(panel).OrderBy(value => value)),
            "the realized order is the row-major index order");
        Assert.AreEqual(created, panel.Created);
    }

    [TestMethod]
    public void HorizontalOffsetWindowsTheColumnBand()
    {
        const int count = 4000;
        using var viewer = new VC.ScrollViewer { Width = 100, Height = 120 };
        using var panel = new VC.VirtualizingUniformGrid
        {
            CellWidth = 50d,
            CellHeight = 40d,
            Columns = 20,
            OverscanColumns = 0,
            ItemsSource = Items(count),
        };
        viewer.Content = panel;
        Layout(viewer);

        var gridWidth = 20 * 50d;
        Assert.AreEqual(gridWidth, viewer.ExtentWidth, 0.5f, "a wide grid reports its full width");
        Assert.IsLessThan(20 * 5, panel.RealizedCount, "a 100px viewport realizes only a column band");

        viewer.HorizontalOffset = 0f;
        Layout(viewer);
        var firstLeft = panel.Items.Min(visual => visual.Bounds.X);

        viewer.HorizontalOffset = 500f;
        Layout(viewer);
        var shifted = panel.Items.Min(visual => visual.Bounds.X);

        Assert.IsGreaterThan(firstLeft, shifted, "a horizontal offset moves the window to the right");
        Assert.AreEqual(0f, panel.Items.Min(visual => visual.Bounds.X) % 50f, 0.01f,
            "boundaries stay on cell lines");
        Assert.AreEqual(20, panel.ComputedColumns);
    }

    [TestMethod]
    public void PixelAndContentScrollRealizeTheSameIndices()
    {
        var source = Items(4000);

        using var pixelViewer = new VC.ScrollViewer { Width = 200, Height = 100 };
        using var pixel = new VC.VirtualizingUniformGrid
        {
            CellWidth = 30d,
            CellHeight = 20d,
            Columns = 6,
            ItemsSource = source,
        };
        pixelViewer.Content = pixel;
        Layout(pixelViewer);

        using var contentViewer = new VC.ScrollViewer { Width = 200, Height = 100, CanContentScroll = true };
        using var content = new VC.VirtualizingUniformGrid
        {
            CellWidth = 30d,
            CellHeight = 20d,
            Columns = 6,
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

            CollectionAssert.AreEqual(DataContexts(pixel), DataContexts(content),
                "both scroll modes realize the same index window");
        }

        Assert.AreEqual(pixelViewer.VerticalOffset, contentViewer.VerticalOffset, 0.1d);
    }

    [TestMethod]
    public void ContentScrollReportsTheFullExtentAndPositionsRelativeToTheViewport()
    {
        const int count = 5000;
        using var viewer = new VC.ScrollViewer { Width = 200, Height = 100, CanContentScroll = true };
        using var panel = new VC.VirtualizingUniformGrid
        {
            CellWidth = 30d,
            CellHeight = 20d,
            Columns = 6,
            ItemsSource = Items(count),
        };
        viewer.Content = panel;
        Layout(viewer);

        var rows = panel.ComputedRows;
        Assert.AreEqual(rows * 20f, viewer.ExtentHeight, 0.5f, "the panel reports every row");
        Assert.AreEqual(0f, panel.Items[0].Bounds.Y, 0.5f);

        viewer.VerticalOffset = 400f;
        Layout(viewer);

        var range = BruteAxis(rows, 20d, panel.OverscanRows, 400f,
            (int)Math.Ceiling(panel.ViewportHeight));
        var firstRow = panel.Items.Min(visual => (int)visual.DataContext! / 6);
        Assert.IsGreaterThanOrEqualTo(range.First, firstRow, "the top of the window is the band start");

        var first = panel.Items[0];
        var expectedY = (((int)first.DataContext! / 6) * 20d) - 400d;
        Assert.AreEqual((float)expectedY, first.Bounds.Y, 0.5f,
            "records are positioned relative to the viewport in content-scroll mode");
    }

    [TestMethod]
    public void HundredThousandCellsStayWithinABudget()
    {
        const int count = 100_000;
        var source = Items(count);
        using var viewer = new VC.ScrollViewer { Width = 400, Height = 224 };
        using var panel = new VC.VirtualizingUniformGrid
        {
            CellWidth = 16d,
            CellHeight = 12d,
            Columns = 20,
            ItemsSource = source,
        };
        viewer.Content = panel;

        var firstLayout = Stopwatch.StartNew();
        Layout(viewer);
        firstLayout.Stop();

        Assert.AreEqual(20, panel.ComputedColumns);
        Assert.AreEqual(5000, panel.ComputedRows);
        Assert.IsGreaterThan(0, panel.RealizedCount);
        Assert.IsLessThan(1000, panel.RealizedCount, "a 224px viewport realizes a bounded cell band");

        var jump = Stopwatch.StartNew();
        viewer.VerticalOffset = 30_000f;
        Layout(viewer);
        jump.Stop();
        Assert.AreNotEqual(0, panel.FirstRealizedIndex);

        var random = new Random(20261127);
        var randomJumps = Stopwatch.StartNew();
        for (var step = 0; step < 25; step++)
        {
            viewer.VerticalOffset = random.Next(1_000_000);
            Layout(viewer);
            Assert.IsLessThan(1000, panel.RealizedCount, "a random jump never enlarges the window");
        }

        randomJumps.Stop();

        TestContext.WriteLine(
            $"grid 100k: first layout {firstLayout.ElapsedMilliseconds} ms · jump {jump.ElapsedMilliseconds} ms · " +
            $"25 jumps {randomJumps.ElapsedMilliseconds} ms · realized {panel.RealizedCount}");

        Assert.IsLessThan(5_000L, firstLayout.ElapsedMilliseconds, "the first 100k window stays under 5s");
        Assert.IsLessThan(2_000L, jump.ElapsedMilliseconds, "a single jump stays under 2s");
        Assert.IsLessThan(10_000L, randomJumps.ElapsedMilliseconds, "25 random jumps stay under 10s");
    }

    // ---- 参考实现 ----

    private static SortedDictionary<int, RectangleF> BruteWindow(
        int count,
        int rows,
        int columns,
        Size viewport,
        PointF offset,
        int overscanRows,
        int overscanColumns)
    {
        var result = new SortedDictionary<int, RectangleF>();
        if (count <= 0 || rows <= 0 || columns <= 0)
        {
            return result;
        }

        var rowRange = BruteAxis(rows, CellHeight, overscanRows, offset.Y, viewport.Height);
        var columnRange = BruteAxis(columns, CellWidth, overscanColumns, offset.X, viewport.Width);

        for (var row = rowRange.First; row < rowRange.LastExclusive; row++)
        {
            for (var column = columnRange.First; column < columnRange.LastExclusive; column++)
            {
                var index = (row * columns) + column;
                if (index >= count)
                {
                    continue;
                }

                result[index] = new RectangleF(
                    (float)(column * CellWidth),
                    (float)(row * CellHeight),
                    (float)CellWidth,
                    (float)CellHeight);
            }
        }

        return result;
    }

    private static (int First, int LastExclusive) BruteAxis(
        int cellCount, double cellSize, int overscan, float offset, int viewport)
    {
        if (cellCount <= 0)
        {
            return (0, 0);
        }

        var first = -1;
        var last = -1;
        for (var cell = 0; cell < cellCount; cell++)
        {
            var top = cell * cellSize;
            if (top < offset + viewport && top + cellSize > offset)
            {
                if (first < 0)
                {
                    first = cell;
                }

                last = cell;
            }
        }

        if (first < 0)
        {
            first = last = XamlMath.Clamp((int)Math.Floor(offset / cellSize), 0, cellCount - 1);
        }

        return (Math.Max(0, first - overscan), Math.Min(cellCount, last + 1 + overscan));
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

    private static (int Rows, int Columns) ExpectedGrid(int count, int rows, int columns)
    {
        if (count <= 0)
        {
            return (0, Math.Max(1, Math.Max(0, columns)));
        }

        int resolvedColumns;
        if (columns > 0)
        {
            resolvedColumns = columns;
        }
        else if (rows > 0)
        {
            resolvedColumns = (count + rows - 1) / rows;
        }
        else
        {
            resolvedColumns = (int)Math.Ceiling(Math.Sqrt(count));
        }

        resolvedColumns = Math.Max(1, resolvedColumns);
        var resolvedRows = (count + resolvedColumns - 1) / resolvedColumns;
        return (Math.Max(1, Math.Max(Math.Max(0, rows), resolvedRows)), resolvedColumns);
    }

    private static void AssertGrid(int rows, int columns, int count, int expectedRows, int expectedColumns)
    {
        using var panel = new ProbeGrid
        {
            CellWidth = CellWidth,
            CellHeight = CellHeight,
            Rows = rows,
            Columns = columns,
            ItemsSource = Items(count),
        };

        var viewport = new Size(200, 100);
        panel.Compute(viewport, viewport, 0f, 0f);

        Assert.AreEqual(expectedRows, panel.ComputedRows, $"count {count} mode ({rows},{columns}) rows");
        Assert.AreEqual(expectedColumns, panel.ComputedColumns, $"count {count} mode ({rows},{columns}) columns");
    }

    private static int DistinctCount(IReadOnlyList<VisualElement> items)
        => items.ToHashSet(ReferenceEqualityComparer.Instance).Count;

    private static List<int?> DataContexts(VC.VirtualizingUniformGrid panel)
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

    private sealed class ProbeGrid : VC.VirtualizingUniformGrid
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
