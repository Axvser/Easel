using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Contract coverage for <see cref="VC.VirtualizingStackPanel"/>: the exact index window at pixel and content
/// scrolling, extent with spacing/orientation, source transitions, empty-source first-index semantics, random jumps
/// and a coarse 100k-row performance budget.
/// </summary>
[STATestClass]
public sealed class VirtualizingStackPanelContractTests
{
    private const int ViewportWidth = 200;
    private const int ViewportHeight = 100;

    /// <summary>The test context used to print the measured performance numbers.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ItemExtentAndSpacingDriveTheVerticalExtentAndRowBounds()
    {
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var panel = new VC.VirtualizingStackPanel { ItemExtent = 10d, Spacing = 5d, ItemsSource = Rows(4) };
        viewer.Content = panel;

        Layout(viewer);

        Assert.AreEqual(55f, viewer.ExtentHeight, 0.1f, "4 rows of 10px plus three 5px gaps");
        Assert.AreEqual(4, panel.RealizedCount, "a short source realizes every row");
        for (var index = 0; index < 4; index++)
        {
            Assert.AreEqual(index * 15f, panel.Items[index].Bounds.Y, 0.1f, $"row {index} starts after its gaps");
            Assert.AreEqual(10f, panel.Items[index].Bounds.Height, 0.1f);
        }
    }

    [TestMethod]
    public void HorizontalItemExtentAndSpacingDriveTheColumnExtent()
    {
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var panel = new VC.VirtualizingStackPanel
        {
            Orientation = Orientation.Horizontal,
            ItemExtent = 10d,
            Spacing = 5d,
            ItemsSource = Rows(4),
        };
        viewer.Content = panel;

        Layout(viewer);

        Assert.AreEqual(55f, viewer.ExtentWidth, 0.1f, "4 columns of 10px plus three 5px gaps");
        for (var index = 0; index < 4; index++)
        {
            Assert.AreEqual(index * 15f, panel.Items[index].Bounds.X, 0.1f, $"column {index} starts after its gaps");
            Assert.AreEqual(10f, panel.Items[index].Bounds.Width, 0.1f);
        }
    }

    [TestMethod]
    public void PixelScrollKeepsDocumentCoordinatesAndReusesTheWindow()
    {
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var panel = new CountingPanel { ItemExtent = 20d, ItemsSource = Rows(100) };
        viewer.Content = panel;

        Layout(viewer);
        Assert.AreEqual(0, panel.FirstRealizedIndex);
        Assert.AreEqual(5, panel.RealizedCount);
        var created = panel.Created;

        viewer.VerticalOffset = 100f;
        Layout(viewer);
        Assert.AreEqual(5, panel.FirstRealizedIndex);
        Assert.AreEqual(5, panel.RealizedCount);
        Assert.AreEqual(100f, panel.Items[0].Bounds.Y, 0.1f,
            "pixel scrolling keeps records in document coordinates; the presenter moves the content");
        Assert.AreEqual(created, panel.Created, "scrolling recycles instead of calling the factory");

        viewer.VerticalOffset = 110f;
        Layout(viewer);
        Assert.AreEqual(5, panel.FirstRealizedIndex, "a sub-row offset keeps the same first index");
        Assert.AreEqual(6, panel.RealizedCount,
            "the partial last row (index 10) enters the window, so the overscan window grows by one");
        Assert.AreEqual(created + 1, panel.Created, "only the one newly visible row is created");
        Assert.AreEqual(100f, panel.Items[0].Bounds.Y, 0.1f);
    }

    [TestMethod]
    public void ContentScrollPositionsTheWindowRelativeToTheViewport()
    {
        using var viewer = new VC.ScrollViewer
        {
            Width = ViewportWidth,
            Height = ViewportHeight,
            CanContentScroll = true,
            VerticalScrollBarVisibility = VC.ScrollBarVisibility.Auto,
        };
        using var panel = new VC.VirtualizingStackPanel { ItemExtent = 20d, ItemsSource = Rows(100) };
        viewer.Content = panel;

        Layout(viewer);
        Assert.AreEqual(2000f, viewer.ExtentHeight, 0.1f);
        Assert.AreEqual(100f, viewer.ViewportHeight, 0.1f);

        viewer.VerticalOffset = 140f;
        Layout(viewer);

        Assert.AreEqual(7, panel.FirstRealizedIndex, "content scrolling feeds the panel's own offset");
        Assert.AreEqual(140d, panel.VerticalOffset, 0.1d);
        Assert.AreEqual(0f, panel.Items[0].Bounds.Y, 0.1f, "the first realized row starts at the viewport origin");
    }

    [TestMethod]
    public void HorizontalContentScrollReachesLaterColumns()
    {
        using var viewer = new VC.ScrollViewer
        {
            Width = ViewportWidth,
            Height = ViewportHeight,
            CanContentScroll = true,
            HorizontalScrollBarVisibility = VC.ScrollBarVisibility.Auto,
        };
        using var panel = new VC.VirtualizingStackPanel
        {
            Orientation = Orientation.Horizontal,
            ItemExtent = 20d,
            ItemsSource = Rows(100),
        };
        viewer.Content = panel;

        Layout(viewer);
        Assert.AreEqual(2000f, viewer.ExtentWidth, 0.1f);

        viewer.HorizontalOffset = 60f;
        Layout(viewer);

        Assert.AreEqual(3, panel.FirstRealizedIndex, "horizontal content scrolling reaches column three");
        Assert.AreEqual(60d, panel.HorizontalOffset, 0.1d);
        Assert.AreEqual(0f, panel.Items[0].Bounds.X, 0.1f);
    }

    [TestMethod]
    public void SourceTransitionsFromZeroThroughLargeToSmallAndBack()
    {
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var panel = new CountingPanel { ItemExtent = 20d, ItemsSource = Rows(0) };
        viewer.Content = panel;

        Layout(viewer);
        Assert.AreEqual(0, panel.RealizedCount, "an empty source realizes nothing");
        Assert.AreEqual(0, panel.FirstRealizedIndex);
        Assert.AreEqual(0f, viewer.ExtentHeight, 0.1f);

        panel.ItemsSource = Rows(100_000);
        Layout(viewer);
        var largeWindow = panel.RealizedCount;
        var largeCreated = panel.Created;
        Assert.IsGreaterThan(0, largeWindow);
        Assert.IsLessThan(50, largeWindow, "a large source realizes only the viewport");
        Assert.AreEqual(0, panel.FirstRealizedIndex);

        panel.ItemsSource = Rows(3);
        Layout(viewer);
        Assert.AreEqual(3, panel.RealizedCount, "a tiny source realizes every row");
        Assert.AreEqual(0, panel.FirstRealizedIndex);
        Assert.AreEqual(60f, viewer.ExtentHeight, 0.1f);

        panel.ItemsSource = Rows(0);
        Layout(viewer);
        Assert.AreEqual(0, panel.RealizedCount, "shrinking to zero clears the window");
        Assert.AreEqual(0f, viewer.ExtentHeight, 0.1f);

        panel.ItemsSource = Rows(5);
        Layout(viewer);
        Assert.AreEqual(5, panel.RealizedCount);
        Assert.AreEqual(largeCreated, panel.Created, "the round trip reused the pool instead of growing it");
    }

    [TestMethod]
    public void EmptySourceFirstRealizedIndexContract()
    {
        using var panel = new VC.VirtualizingStackPanel { ItemExtent = 20d };
        Assert.AreEqual(-1, panel.FirstRealizedIndex, "no arrange has happened yet");

        panel.ItemsSource = Array.Empty<object?>();
        Assert.AreEqual(-1, panel.FirstRealizedIndex, "a rebuild does not arrange");

        panel.Measure(new Size(ViewportWidth, ViewportHeight));
        panel.Arrange(new RectangleF(0f, 0f, ViewportWidth, ViewportHeight));

        Assert.AreEqual(0, panel.FirstRealizedIndex, "an arranged empty window reports zero");
        Assert.AreEqual(0, panel.RealizedCount);
    }

    [TestMethod]
    public void RandomJumpsMatchTheReferenceIndexWindow()
    {
        const int Count = 100_000;
        const float Step = 20f;
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var panel = new VC.VirtualizingStackPanel { ItemExtent = Step, ItemsSource = Rows(Count) };
        viewer.Content = panel;
        Layout(viewer);

        var random = new Random(20261127);
        var jumps = new List<float> { -500f, 0f, Step * 7f, Step * 4321f + 3f, float.MaxValue };
        for (var step = 0; step < 40; step++)
        {
            jumps.Add(random.Next(Count) * Step);
        }

        foreach (var requested in jumps)
        {
            viewer.VerticalOffset = requested;
            Layout(viewer);

            var offset = (float)viewer.VerticalOffset;
            var first = Math.Clamp((int)Math.Floor(offset / Step), 0, Count);
            var last = Math.Clamp((int)Math.Ceiling((offset + viewer.ViewportHeight) / Step), first, Count);

            Assert.AreEqual(first, panel.FirstRealizedIndex,
                $"offset {offset} starts at the reference index");
            Assert.AreEqual(last - first, panel.RealizedCount,
                $"offset {offset} realizes exactly the reference window");
            if (last > first)
            {
                Assert.AreEqual(first * Step, panel.Items[0].Bounds.Y, 0.1f,
                    $"offset {offset} arranges the first record at its document position");
            }
        }
    }

    [TestMethod]
    public void ExtentAndOrientationChangesRebuildThroughThePool()
    {
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var panel = new CountingPanel { ItemExtent = 20d, ItemsSource = Rows(1_000) };
        viewer.Content = panel;
        Layout(viewer);
        var created = panel.Created;
        var window = panel.RealizedCount;
        Assert.IsGreaterThan(0, window);

        panel.Spacing = 5d;
        Layout(viewer);
        Assert.AreEqual(100f / 25f, panel.RealizedCount, 0.01f, "spacing widens the step and shrinks the window");
        Assert.AreEqual(created, panel.Created, "a spacing change is served from the pool");

        panel.Orientation = Orientation.Horizontal;
        Layout(viewer);
        Assert.IsGreaterThan(0, panel.RealizedCount);
        Assert.AreEqual(Math.Max(created, panel.RealizedCount), panel.Created,
            "the wider horizontal window is filled from the pool, then only its overflow is created");
        Assert.IsTrue(panel.Items.All(static item => item.Bounds.Width == 20f),
            "after the orientation change each record becomes a column of the item extent");
    }

    [TestMethod]
    public void HundredThousandRowPerformanceStaysWithinBudgets()
    {
        var build = Stopwatch.StartNew();
        var rows = Rows(100_000);
        build.Stop();

        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var panel = new VC.VirtualizingStackPanel { ItemExtent = 20d, ItemsSource = rows };
        viewer.Content = panel;

        var firstLayout = Stopwatch.StartNew();
        Layout(viewer);
        firstLayout.Stop();
        Assert.IsGreaterThan(0, panel.RealizedCount);
        Assert.IsLessThan(50, panel.RealizedCount);
        var window = panel.RealizedCount;

        var jump = Stopwatch.StartNew();
        viewer.VerticalOffset = 50_000 * 20f;
        Layout(viewer);
        jump.Stop();
        Assert.AreEqual(50_000, panel.FirstRealizedIndex);
        Assert.AreEqual(window, panel.RealizedCount);

        var randomJumps = Stopwatch.StartNew();
        var random = new Random(20261127);
        for (var step = 0; step < 50; step++)
        {
            viewer.VerticalOffset = random.Next(100_000) * 20f;
            Layout(viewer);
            Assert.AreEqual(window, panel.RealizedCount, "a random jump keeps the window bounded");
        }

        randomJumps.Stop();

        TestContext.WriteLine(
            $"100k rows: build {build.ElapsedMilliseconds} ms · first layout {firstLayout.ElapsedMilliseconds} ms · " +
            $"one jump {jump.ElapsedMilliseconds} ms · 50 random jumps {randomJumps.ElapsedMilliseconds} ms · " +
            $"realized {panel.RealizedCount}");

        Assert.IsLessThan(3_000L, build.ElapsedMilliseconds, "building one hundred thousand rows stays under 3s");
        Assert.IsLessThan(2_000L, firstLayout.ElapsedMilliseconds, "the first window stays under 2s");
        Assert.IsLessThan(1_000L, jump.ElapsedMilliseconds, "a single jump stays under 1s");
        Assert.IsLessThan(5_000L, randomJumps.ElapsedMilliseconds, "50 random jumps stay under 5s");
    }

    [TestMethod]
    public void FixedItemExtentDoesNotSupportPerRowVariableHeights()
    {
        // 限制：VirtualizingStackPanel 的步长和行高只来自 ItemExtent，不支持按行测量出来的可变高度。
        // 某一行自己写更高的 Height 也不会生效，后面的行同样不会为它让位。
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var panel = new VC.VirtualizingStackPanel { ItemExtent = 20d, ItemsSource = Rows(10) };
        viewer.Content = panel;
        Layout(viewer);

        panel.Items[0].Height = 100d;
        panel.InvalidateMeasure();
        Layout(viewer);

        for (var index = 0; index < panel.RealizedCount; index++)
        {
            Assert.AreEqual(index * 20f, panel.Items[index].Bounds.Y, 0.1f,
                "row positions stay on the fixed ItemExtent stride regardless of a taller row");
        }

        Assert.AreEqual(20f, panel.Items[0].Bounds.Height, 0.1f,
            "a per-row height is clamped to ItemExtent; only ItemExtent can change the row size");
    }

    private sealed class CountingPanel : VC.VirtualizingStackPanel
    {
        public int Created { get; private set; }

        protected override VisualElement CreateItemVisual(object? item)
        {
            Created++;
            return base.CreateItemVisual(item);
        }
    }

    private static List<object?> Rows(int count)
    {
        var rows = new List<object?>(count);
        for (var index = 0; index < count; index++)
        {
            rows.Add(index);
        }

        return rows;
    }

    private static void Layout(VisualElement root)
    {
        root.Measure(new Size(ViewportWidth, ViewportHeight));
        root.Arrange(new RectangleF(0f, 0f, ViewportWidth, ViewportHeight));
    }
}
