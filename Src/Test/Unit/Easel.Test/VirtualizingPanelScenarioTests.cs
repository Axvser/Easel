using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Demo.ViewModels;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Scenario coverage for the generic <see cref="VC.VirtualizingPanel"/>: window sizing at 100k/1M rows, recycling,
/// source replacement, content scrolling, both orientations, pointer input and a coarse performance budget.
/// </summary>
[STATestClass]
public sealed class VirtualizingPanelScenarioTests
{
    private const float Extent = 28f;
    private const int ViewportWidth = 400;
    private const int ViewportHeight = 224;

    /// <summary>The test context used to print the measured performance numbers.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void LargeSourcesKeepTheRealizedWindowBounded()
    {
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var panel = new VC.VirtualizingPanel { ItemExtent = Extent, ItemsSource = Rows(100_000) };
        viewer.Content = panel;

        Layout(viewer);

        var window = panel.RealizedCount;
        Assert.IsGreaterThan(0, window, "the first window is realized");
        Assert.IsLessThan(20, window, "a 224px viewport realizes about eight 28px rows");
        Assert.AreEqual(0, panel.FirstRealizedIndex);

        panel.ItemsSource = Rows(1_000_000);
        viewer.VerticalOffset = 0f;
        Layout(viewer);

        Assert.AreEqual(0, panel.FirstRealizedIndex);
        Assert.AreEqual(window, panel.RealizedCount, "the window width is a viewport fact, not a source-size fact");

        viewer.VerticalOffset = 500_000 * Extent;
        Layout(viewer);

        Assert.AreEqual(500_000, panel.FirstRealizedIndex, "the first index is the offset divided by the extent");
        Assert.AreEqual(window, panel.RealizedCount);
        Assert.AreEqual((object)500_000, panel.Items[0].DataContext);

        viewer.VerticalOffset = float.MaxValue;
        Layout(viewer);

        Assert.AreEqual(1_000_000, panel.FirstRealizedIndex + panel.RealizedCount,
            "scrolling past the end still reaches the last source index");
        Assert.AreEqual((object)999_999, panel.Items[^1].DataContext, "the last row is realized at the bottom");
        Assert.IsLessThan(20, panel.RealizedCount, "reaching the end does not realize the whole source");
    }

    [TestMethod]
    public void ScrollingRecyclesInsteadOfCreating()
    {
        var source = Rows(10_000);
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var panel = new ProbePanel { ItemExtent = Extent, ItemsSource = source };
        viewer.Content = panel;

        Layout(viewer);

        var initial = panel.Items.ToArray();
        var created = panel.Created;
        Assert.IsGreaterThan(0, created);
        Assert.AreEqual(created, initial.Length, "the first window creates exactly one visual per realized row");

        for (var step = 1; step <= 40; step++)
        {
            viewer.VerticalOffset = step * Extent * 7f;
            Layout(viewer);

            Assert.AreEqual(created, panel.Created, "scrolling never creates another row visual");
            Assert.IsLessThan(20, panel.RealizedCount);
            Assert.IsTrue(panel.Items.All(initial.Contains), "every visible row is one of the original instances");
        }

        Assert.IsGreaterThan(0, panel.Recycled, "rows that left the window were recycled");
        Assert.IsGreaterThan(0, panel.Rebound, "pooled rows were rebound to entering indices");
        Assert.IsGreaterThan(0, panel.RealizedPasses);

        viewer.VerticalOffset = float.MaxValue;
        Layout(viewer);

        Assert.AreEqual(10_000, panel.FirstRealizedIndex + panel.RealizedCount, "the end row is realized");
        Assert.AreEqual(created, panel.Created, "reaching the end reuses the pool");
        Assert.AreEqual(initial.Length, panel.RealizedCount, "the window keeps its size at the end");

        viewer.VerticalOffset = 0f;
        Layout(viewer);

        Assert.AreEqual(0, panel.FirstRealizedIndex, "the window returns to the top");
        Assert.AreEqual(created, panel.Created, "a full round trip creates nothing new");
        Assert.IsTrue(panel.Items.All(initial.Contains));
    }

    [TestMethod]
    public void RecycleItemReceivesTheIndexTheVisualShowed()
    {
        var source = Rows(1_000);
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var panel = new ProbePanel { ItemExtent = 20d, ItemsSource = source };
        viewer.Content = panel;

        Layout(viewer);

        for (var step = 1; step <= 20; step++)
        {
            viewer.VerticalOffset = step * 20f * 3f;
            Layout(viewer);
        }

        Assert.IsGreaterThan(0, panel.RecycledItems.Count);
        foreach (var (index, item) in panel.RecycledItems)
        {
            Assert.IsGreaterThanOrEqualTo(0, index, "a recycled visual must report the source index it showed");
            Assert.IsLessThan(source.Count, index);
            Assert.AreEqual(source[index], item, "the reported index must match the recycled element's item");
        }

        // 行高改变会让窗口整体失效；此时回收的行同样要报告真实索引。
        viewer.VerticalOffset = 200f;
        Layout(viewer);
        panel.ItemExtent = 40d;
        Layout(viewer);

        Assert.IsGreaterThan(0, panel.RecycledItems.Count);
        foreach (var (index, item) in panel.RecycledItems)
        {
            Assert.IsGreaterThanOrEqualTo(0, index, "an extent change must not recycle with a -1 index");
            Assert.IsLessThan(source.Count, index);
            Assert.AreEqual(source[index], item);
        }

        Assert.AreEqual(5, panel.FirstRealizedIndex, "200px / 40px starts at row five");
        Assert.AreEqual(6, panel.RealizedCount, "a 224px viewport / 40px rows realizes six rows");
    }

    [TestMethod]
    public void SourceReplacementThroughEmptyAndTinySourcesStaysConsistent()
    {
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var panel = new VC.VirtualizingPanel { ItemExtent = Extent, ItemsSource = Rows(5_000) };
        viewer.Content = panel;
        Layout(viewer);

        viewer.VerticalOffset = 4_000 * Extent;
        Layout(viewer);

        panel.ItemsSource = Rows(10);
        Layout(viewer);

        Assert.IsGreaterThan(0, panel.RealizedCount, "a 10-row source still realizes its window");
        Assert.AreEqual(10, panel.FirstRealizedIndex + panel.RealizedCount, "the shrunk source ends at its last row");
        Assert.IsLessThan(10, panel.FirstRealizedIndex);
        Assert.AreEqual(280f, viewer.ExtentHeight, 0.1f, "the extent follows the new row count");

        panel.ItemsSource = Rows(0);
        Layout(viewer);

        Assert.AreEqual(0, panel.RealizedCount, "an empty source realizes nothing");
        Assert.AreEqual(0, panel.FirstRealizedIndex);
        Assert.AreEqual(0f, viewer.ExtentHeight, 0.1f);

        panel.ItemsSource = Rows(3);
        Layout(viewer);

        Assert.AreEqual(3, panel.RealizedCount, "a source shorter than the viewport realizes every row");
        Assert.AreEqual(0, panel.FirstRealizedIndex);
        Assert.AreEqual(0f, panel.Items[0].Bounds.Y, 0.1f);
        Assert.AreEqual(28f, panel.Items[1].Bounds.Y, 0.1f, "short sources stack at the fixed extent");
    }

    [TestMethod]
    public void ContentScrollWindowFollowsItsOwnOffsets()
    {
        using var viewer = new VC.ScrollViewer
        {
            Width = ViewportWidth,
            Height = ViewportHeight,
            CanContentScroll = true,
            VerticalScrollBarVisibility = VC.ScrollBarVisibility.Auto,
        };
        using var panel = new VC.VirtualizingPanel { ItemExtent = Extent, ItemsSource = Rows(100_000) };
        viewer.Content = panel;

        Layout(viewer);

        Assert.AreEqual(2_800_000d, viewer.ExtentHeight, 0.5d, "the panel reports the full item extent");
        Assert.AreEqual(0d, viewer.VerticalOffset, 0.1d);

        viewer.VerticalOffset = 70_000 * Extent;
        Layout(viewer);

        Assert.AreEqual(70_000, panel.FirstRealizedIndex, "content scrolling feeds the panel's own offset");
        Assert.AreEqual(70_000 * Extent, panel.VerticalOffset, 0.1d);
        Assert.AreEqual(0f, panel.Items[0].Bounds.Y, 0.1f, "rows are positioned relative to the viewport");

        viewer.VerticalOffset = float.MaxValue;
        Layout(viewer);

        Assert.AreEqual(100_000, panel.FirstRealizedIndex + panel.RealizedCount);
        Assert.AreEqual(2_800_000d - ViewportHeight, panel.VerticalOffset, 0.5d,
            "the panel clamps its own offset to the scrollable range");
    }

    [TestMethod]
    public void ContentScrollRecoversWhenTheSourceShrinks()
    {
        using var viewer = new VC.ScrollViewer
        {
            Width = ViewportWidth,
            Height = ViewportHeight,
            CanContentScroll = true,
            VerticalScrollBarVisibility = VC.ScrollBarVisibility.Auto,
        };
        using var panel = new VC.VirtualizingPanel { ItemExtent = Extent, ItemsSource = Rows(5_000) };
        viewer.Content = panel;
        Layout(viewer);

        viewer.VerticalOffset = float.MaxValue;
        Layout(viewer);
        Assert.AreEqual(5_000, panel.FirstRealizedIndex + panel.RealizedCount, "the window sits at the old end");

        panel.ItemsSource = Rows(10);
        Layout(viewer);

        Assert.IsLessThan(10, panel.FirstRealizedIndex,
            "a shrunk source must pull the stale content offset back into range");
        Assert.IsGreaterThan(0, panel.RealizedCount, "the shrunken source still shows its window");
        Assert.AreEqual(10, panel.FirstRealizedIndex + panel.RealizedCount);
    }

    [TestMethod]
    public void HorizontalOrientationWindowsAColumnRange()
    {
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var panel = new VC.VirtualizingPanel
        {
            Orientation = Orientation.Horizontal,
            ItemExtent = Extent,
            ItemsSource = Rows(100_000),
        };
        viewer.Content = panel;
        Layout(viewer);

        var window = panel.RealizedCount;
        Assert.IsGreaterThan(0, window);
        Assert.IsLessThan(20, window, "a 400px viewport realizes about fifteen 28px columns");

        viewer.HorizontalOffset = 50_000 * Extent;
        Layout(viewer);

        Assert.AreEqual(50_000, panel.FirstRealizedIndex);
        Assert.AreEqual(window, panel.RealizedCount);

        viewer.HorizontalOffset = float.MaxValue;
        Layout(viewer);

        Assert.AreEqual(100_000, panel.FirstRealizedIndex + panel.RealizedCount);
        Assert.AreEqual((object)99_999, panel.Items[^1].DataContext);
    }

    [TestMethod]
    public void SmallViewportAndOverscrollStayInsideTheSource()
    {
        using var viewer = new VC.ScrollViewer { Width = 120, Height = 10 };
        using var panel = new VC.VirtualizingPanel { ItemExtent = 40d, ItemsSource = Rows(100) };
        viewer.Content = panel;
        Layout(viewer, 120, 10);

        Assert.AreEqual(1, panel.RealizedCount, "a viewport shorter than one item realizes one row");
        Assert.AreEqual(0, panel.FirstRealizedIndex);

        viewer.VerticalOffset = -500f;
        Layout(viewer, 120, 10);
        Assert.AreEqual(0, panel.FirstRealizedIndex, "a negative offset clamps to the top");

        viewer.VerticalOffset = 10_000f;
        Layout(viewer, 120, 10);
        Assert.AreEqual(100, panel.FirstRealizedIndex + panel.RealizedCount, "overscroll clamps to the last row");
        Assert.AreEqual(1, panel.RealizedCount);
        Assert.AreEqual((object)99, panel.Items[0].DataContext);
    }

    [TestMethod]
    public void WheelAndPageKeysMoveTheVirtualWindow()
    {
        using var surface = new VisualHost { Size = new Size(440, 284), DpiScaleOverride = 1f };
        using var viewer = new VC.ScrollViewer
        {
            Width = ViewportWidth,
            Height = ViewportHeight,
            VerticalScrollBarVisibility = VC.ScrollBarVisibility.Auto,
        };
        using var panel = new VC.VirtualizingPanel { ItemExtent = Extent, ItemsSource = Rows(50_000) };
        viewer.Content = panel;
        surface.Root = viewer;
        Layout(surface, 440, 284);

        Assert.AreEqual(0, panel.FirstRealizedIndex);
        var before = viewer.VerticalOffset;

        surface.ProcessPointerWheel(viewer.PointToSurface(new PointF(100f, 100f)), -120);

        Assert.IsGreaterThan(before, viewer.VerticalOffset, "one wheel notch moves the pixel offset");
        Layout(surface, 440, 284);
        Assert.AreEqual((int)(viewer.VerticalOffset / Extent), panel.FirstRealizedIndex,
            "the realized window follows the wheel offset");

        Assert.IsTrue(surface.FocusElement(viewer), "the viewer takes focus for paging");
        var pageStart = viewer.VerticalOffset;
        surface.ProcessKeyDown(Keys.PageDown);
        Layout(surface, 440, 284);
        Assert.IsGreaterThan(pageStart, viewer.VerticalOffset, "PageDown moves by a viewport");
        Assert.AreEqual((int)(viewer.VerticalOffset / Extent), panel.FirstRealizedIndex);

        surface.ProcessKeyDown(Keys.End);
        Layout(surface, 440, 284);
        Assert.AreEqual(50_000, panel.FirstRealizedIndex + panel.RealizedCount, "End reaches the last row");

        surface.ProcessKeyDown(Keys.Home);
        Layout(surface, 440, 284);
        Assert.AreEqual(0, panel.FirstRealizedIndex, "Home returns to the first row");
    }

    [TestMethod]
    public void CustomSubclassSelectionMapsThroughTheRecycledWindow()
    {
        var source = Enumerable.Range(0, 1_000).Select(static index => new VirtualLogRow(index)).ToList();
        using var surface = new VisualHost { Size = new Size(440, 284), DpiScaleOverride = 1f };
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var panel = new Demo.Views.VirtualLogPanel { ItemExtent = Extent, ItemsSource = source };
        viewer.Content = panel;
        surface.Root = viewer;
        Layout(surface, 440, 284);

        var target = panel.Items[2];
        var point = new PointF(
            target.AbsoluteBounds.X + (target.AbsoluteBounds.Width / 2f),
            target.AbsoluteBounds.Y + (target.AbsoluteBounds.Height / 2f));
        surface.ProcessPointerDown(point, MouseButtons.Left);
        surface.ProcessPointerUp(point, MouseButtons.Left);

        Assert.AreEqual(2, panel.SelectedIndex, "the click maps through the realized window");
        Assert.IsTrue(source[2].IsSelected, "selection lives on the row model");

        var created = panel.CreatedCount;
        viewer.VerticalOffset = 600 * Extent;
        Layout(surface, 440, 284);
        Assert.AreSame(source[2], panel.SelectedRow, "selection is index-based and survives scrolling");
        Assert.IsTrue(source[2].IsSelected, "the row model keeps its selected state while offscreen");

        viewer.VerticalOffset = 0f;
        Layout(surface, 440, 284);
        Assert.AreEqual(2, panel.SelectedIndex);
        Assert.AreSame(source[2], panel.Items[2].DataContext, "scrolling back rebinds the selected row");
        Assert.IsTrue(source[2].IsSelected);
        Assert.AreEqual(created, panel.CreatedCount, "recycling the selection does not create new row visuals");
    }

    [TestMethod]
    public void OneMillionRowPerformanceStaysWithinBudgets()
    {
        var build = Stopwatch.StartNew();
        var rows = VirtualizationLogViewModel.Build(1_000_000);
        build.Stop();

        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var panel = new VC.VirtualizingPanel { ItemExtent = Extent, ItemsSource = rows };
        viewer.Content = panel;

        var firstLayout = Stopwatch.StartNew();
        Layout(viewer);
        firstLayout.Stop();

        Assert.IsGreaterThan(0, panel.RealizedCount);
        Assert.IsLessThan(20, panel.RealizedCount, "one million rows still realize only a viewport");

        var jump = Stopwatch.StartNew();
        viewer.VerticalOffset = 500_000 * Extent;
        Layout(viewer);
        jump.Stop();

        Assert.AreEqual(500_000, panel.FirstRealizedIndex);
        Assert.IsLessThan(20, panel.RealizedCount);

        var random = new Random(20261127);
        var randomJumps = Stopwatch.StartNew();
        for (var step = 0; step < 25; step++)
        {
            viewer.VerticalOffset = random.Next(1_000_000) * Extent;
            Layout(viewer);
            Assert.IsLessThan(20, panel.RealizedCount, "a random jump never enlarges the window");
        }

        randomJumps.Stop();

        TestContext.WriteLine(
            $"1M rows: build {build.ElapsedMilliseconds} ms · first layout {firstLayout.ElapsedMilliseconds} ms · " +
            $"one jump {jump.ElapsedMilliseconds} ms · 25 random jumps {randomJumps.ElapsedMilliseconds} ms · " +
            $"realized {panel.RealizedCount}");

        Assert.IsLessThan(15_000L, build.ElapsedMilliseconds, "building one million lightweight rows stays under 15s");
        Assert.IsLessThan(5_000L, firstLayout.ElapsedMilliseconds, "the first window of one million rows stays under 5s");
        Assert.IsLessThan(2_000L, jump.ElapsedMilliseconds, "a single jump stays under 2s");
        Assert.IsLessThan(10_000L, randomJumps.ElapsedMilliseconds, "25 random jumps stay under 10s");
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
        => Layout(root, ViewportWidth, ViewportHeight);

    private static void Layout(VisualElement root, int width, int height)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new RectangleF(0f, 0f, width, height));
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }

    private sealed class ProbePanel : VC.VirtualizingPanel
    {
        public int Created { get; private set; }

        public int Recycled { get; private set; }

        public int Rebound { get; private set; }

        public int RealizedPasses { get; private set; }

        public List<(int Index, object? Item)> RecycledItems { get; } = [];

        protected override VisualElement CreateItemVisual(object? item)
        {
            Created++;
            return base.CreateItemVisual(item);
        }

        protected override VisualElement RealizeItem(int index)
        {
            RealizedPasses++;
            return base.RealizeItem(index);
        }

        protected override void RecycleItem(int index, VisualElement element)
        {
            Recycled++;
            RecycledItems.Add((index, element.DataContext));
            base.RecycleItem(index, element);
        }

        protected override void BindRecycledItem(VisualElement element, int index)
        {
            Rebound++;
            base.BindRecycledItem(element, index);
        }
    }
}
