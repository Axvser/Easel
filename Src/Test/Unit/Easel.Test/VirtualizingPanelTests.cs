using System.Drawing;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>The generic virtualizing panel realizes a bounded window and reuses rows as the viewport moves.</summary>
[STATestClass]
public sealed class VirtualizingPanelTests
{
    [TestMethod]
    public void RealizesOnlyTheVisibleWindowAndRecyclesOnScroll()
    {
        var rows = Enumerable.Range(0, 1000).Select(static index => (object)$"Row {index}").ToList();
        using var viewer = new VC.ScrollViewer { Width = 200, Height = 100 };
        using var panel = new CountingPanel { ItemExtent = 20d, ItemsSource = rows };
        viewer.Content = panel;

        Layout(viewer, 200, 100);

        Assert.AreEqual(0, panel.FirstRealizedIndex);
        Assert.AreEqual(5, panel.RealizedCount, "a 100px viewport realizes five 20px rows");
        Assert.AreEqual(5, panel.Created, "each initial row was created once");
        Assert.IsLessThan(rows.Count, panel.RealizedCount);

        viewer.VerticalOffset = 100f;
        Layout(viewer, 200, 100);

        Assert.AreEqual(5, panel.FirstRealizedIndex, "a 100px offset starts at row five");
        Assert.AreEqual(5, panel.RealizedCount, "the window stays bounded");
        Assert.AreEqual(5, panel.Created, "the leaving rows were recycled, not recreated");
        Assert.IsGreaterThan(0, panel.Recycled, "rows left the window and returned to the pool");
        Assert.AreEqual(100f, panel.Items[0].Bounds.Y, 0.1f, "row five sits at its document position");
    }

    [TestMethod]
    public void ALargeSourceKeepsTheRealizedCountBounded()
    {
        var rows = Enumerable.Range(0, 5000).Select(static index => (object)index).ToList();
        using var viewer = new VC.ScrollViewer { Width = 200, Height = 100 };
        using var panel = new VC.VirtualizingPanel { ItemExtent = 16d, ItemsSource = rows };
        viewer.Content = panel;

        Layout(viewer, 200, 100);

        Assert.IsGreaterThan(0, panel.RealizedCount);
        Assert.IsLessThan(20, panel.RealizedCount, "5000 rows still realize only the viewport");
    }

    [TestMethod]
    public void ContentScrollUsesThePanelOwnScrollInfo()
    {
        var rows = Enumerable.Range(0, 1000).Select(static index => (object)index).ToList();
        using var viewer = new VC.ScrollViewer { Width = 200, Height = 100, CanContentScroll = true };
        using var panel = new VC.VirtualizingPanel { ItemExtent = 20d, ItemsSource = rows };
        viewer.Content = panel;

        Layout(viewer, 200, 100);

        Assert.AreEqual(20000f, viewer.ExtentHeight, 0.1f, "the panel reports the full item extent");
        Assert.AreEqual(100f, viewer.ViewportHeight, 0.1f);

        viewer.VerticalOffset = 100f;
        Assert.IsTrue(viewer.ContentPresenter?.PresentedContent is VC.VirtualizingPanel, "the presenter hosts the panel");
        Assert.IsTrue(viewer.ContentPresenter?.PresentedContent is VC.IScrollInfo, "the panel is a scroll info");
        Assert.AreEqual(100d, panel.VerticalOffset, 0.1d, "the viewer routed the offset to the panel scroll info");
        Layout(viewer, 200, 100);

        Assert.AreEqual(5, panel.FirstRealizedIndex);
        Assert.AreEqual(5, panel.RealizedCount);
        Assert.AreEqual(0f, panel.Items[0].Bounds.Y, 0.1f, "content scrolling positions rows relative to the viewport");
    }

    [TestMethod]
    public void HorizontalOrientationRealizesColumns()
    {
        var items = Enumerable.Range(0, 100).Select(static index => (object)index).ToList();
        using var viewer = new VC.ScrollViewer { Width = 100, Height = 100 };
        using var panel = new VC.VirtualizingPanel
        {
            Orientation = System.Windows.Forms.Orientation.Horizontal,
            ItemExtent = 20d,
            ItemsSource = items,
        };
        viewer.Content = panel;

        Layout(viewer, 100, 100);

        Assert.AreEqual(5, panel.RealizedCount, "a 100px viewport realizes five 20px columns");

        viewer.HorizontalOffset = 40f;
        Layout(viewer, 100, 100);

        Assert.AreEqual(2, panel.FirstRealizedIndex, "a 40px offset starts at column two");
        Assert.AreEqual(40f, panel.Items[0].Bounds.X, 0.1f, "column two sits at its document position");
    }

    private sealed class CountingPanel : VC.VirtualizingPanel
    {
        public int Created { get; private set; }

        public int Recycled { get; private set; }

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

    private static void Layout(VisualElement root, int width, int height)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new RectangleF(0f, 0f, width, height));
    }
}
