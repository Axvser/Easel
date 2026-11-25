using System.Drawing;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Measure/arrange reuse: a pane resize must not re-measure subtrees whose result cannot change.
/// These lock in the shell-splitter fix (scrollable tree measured with an unbounded probe, nested
/// auto-row grids arranged at a different height, DockPanel measuring with the same constraint it
/// arranges with) without depending on the Gallery.
/// </summary>
[STATestClass]
public sealed class LayoutMeasureReuseTests
{
    private sealed class CountingPanel : VC.Panel
    {
        private readonly Size _contentSize;

        public CountingPanel(Size contentSize) => _contentSize = contentSize;

        public int Measures { get; private set; }

        public Size LastAvailable { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            Measures++;
            LastAvailable = availableSize;
            return _contentSize;
        }
    }

    [TestMethod]
    public void Grid_ArrangingAnUnboundedMeasuredStarChild_DoesNotRemeasureIt()
    {
        using var grid = new VC.Grid();
        var child = new CountingPanel(new Size(80, 20));
        grid.Add(child);

        grid.Measure(new Size(int.MaxValue, int.MaxValue));
        var measures = child.Measures;
        Assert.IsTrue(measures > 0, "the star child is measured once");

        grid.Arrange(new RectangleF(0f, 0f, 200f, 100f));

        Assert.AreEqual(measures, child.Measures, "arrange redistributes the star track instead of re-measuring");
        Assert.AreEqual(200f, child.Bounds.Width, 0.01f, "the star column still fills the arranged width");
    }

    [TestMethod]
    public void Grid_ArrangingNarrowerThanAFiniteMeasure_RemeasuresTheStarChild()
    {
        using var grid = new VC.Grid();
        var child = new CountingPanel(new Size(80, 20));
        grid.Add(child);

        grid.Measure(new Size(400, 100));
        var measures = child.Measures;

        grid.Arrange(new RectangleF(0f, 0f, 200f, 100f));

        Assert.AreEqual(measures + 1, child.Measures, "a width-constrained child is re-measured against the smaller slot");
        Assert.AreEqual(200f, child.LastAvailable.Width, 0.01f, "the re-measure uses the arranged slot width");
        Assert.AreEqual(200f, child.Bounds.Width, 0.01f);
    }

    [TestMethod]
    public void Grid_ArrangingTallerThanAZeroHeightMeasure_DoesNotRemeasureAutoRows()
    {
        using var grid = new VC.Grid();
        grid.RowDefinitions.Add(new Easel.Controls.RowDefinition { Height = Easel.Controls.GridLength.Auto });
        grid.RowDefinitions.Add(new Easel.Controls.RowDefinition { Height = Easel.Controls.GridLength.Auto });
        var child = new CountingPanel(new Size(80, 20));
        grid.Add(child);

        // ItemsPresenter measures stacked rows with height 0; the row template has no star rows,
        // so the auto row heights cannot depend on the arranged height.
        grid.Measure(new Size(200, 0));
        var measures = child.Measures;

        grid.Arrange(new RectangleF(0f, 0f, 200f, 100f));

        Assert.AreEqual(measures, child.Measures, "auto rows reuse their measured heights");
        Assert.AreEqual(20f, child.Bounds.Height, 0.01f, "the auto row keeps its content height");
    }

    [TestMethod]
    public void DockPanel_ArrangesWithTheMeasuredDesiredSize_WithoutMeasuringAgain()
    {
        using var dock = new VC.DockPanel { LastChildFill = true };
        var left = new CountingPanel(new Size(80, 20)) { Width = 50f };
        VC.DockPanel.SetDock(left, Easel.Controls.DockPlacement.Left);
        var fill = new CountingPanel(new Size(80, 20));
        dock.Add(left);
        dock.Add(fill);

        dock.Measure(new Size(400, 300));
        Assert.AreEqual(1, left.Measures);
        Assert.AreEqual(1, fill.Measures);
        Assert.AreEqual(350f, fill.LastAvailable.Width, 0.01f, "the fill child is measured in the remaining room");

        dock.Arrange(new RectangleF(0f, 0f, 400f, 300f));

        Assert.AreEqual(1, left.Measures, "arrange reuses the measured size");
        Assert.AreEqual(1, fill.Measures, "arrange reuses the measured size");
        Assert.AreEqual(0f, left.Bounds.X, 0.01f);
        Assert.AreEqual(50f, left.Bounds.Width, 0.01f);
        Assert.AreEqual(50f, fill.Bounds.X, 0.01f);
        Assert.AreEqual(350f, fill.Bounds.Width, 0.01f);
    }

    [TestMethod]
    public void TreeView_UnboundedProbeMeasuresToContentWidth_AndArrangeFillsTheViewport()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var tree = new VC.TreeView { Width = 300, Height = 200 };
        var group = new VC.TreeViewItem { Header = "Group", IsExpanded = true };
        group.Items.Add(new VC.TreeViewItem { Header = "Child" });
        tree.Items.Add(group);
        tree.Items.Add(new VC.TreeViewItem { Header = "Second" });
        surface.Root = tree;
        Layout(surface, 320, 240);

        var desired = tree.Measure(new Size(VisualElement.UnboundedMeasureProbe, int.MaxValue));
        Assert.IsTrue(desired.Width > 0f, "the tree still reports its content");
        Assert.IsTrue(
            desired.Width < VisualElement.UnboundedMeasureProbe,
            $"the probe width is treated as unbounded so the tree reports content width (was {desired.Width})");

        tree.Arrange(new RectangleF(0f, 0f, 200f, 200f));
        Assert.AreEqual(200f, group.Bounds.Width, 0.01f, "arrange stretches the row to the viewport");

        tree.Arrange(new RectangleF(0f, 0f, 260f, 200f));
        Assert.AreEqual(260f, group.Bounds.Width, 0.01f, "a pane resize only redistributes, it does not re-measure rows");
        Assert.AreEqual(desired.Width, tree.DesiredSize.Width, 0.01f, "the measured content width did not change");
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        surface.Size = new Size(width, height);
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
