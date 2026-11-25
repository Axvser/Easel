using System.Drawing;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>The built-in data-list family shell templates and row container styles.</summary>
[STATestClass]
public sealed class ItemsDefaultsTests
{
    [TestMethod]
    public void ItemsControl_DefaultShellHostsItemsInAPresenterInsideAScrollViewer()
    {
        using var surface = new VisualHost { Size = new Size(200, 120), DpiScaleOverride = 1f };
        using var control = new VC.ItemsControl { ItemsSource = new[] { "a", "b" } };
        surface.Root = control;
        Layout(surface, 200, 120);

        Assert.IsNotNull(control.TemplateRoot, "the built-in shell applies when the control is hosted");
        Assert.IsNotNull(
            control.GetTemplateChild<VC.ScrollViewer>("PART_ScrollViewer"),
            "the shell provides the scroll chrome");

        var host = control.GetTemplateChild<VC.ItemsPresenter>("PART_ItemsPresenter");
        Assert.IsNotNull(host, "the shell declares one items presenter");
        Assert.IsTrue(host!.Children.Contains(control.Items[0]), "realized items live in the shell presenter");
        Assert.IsInstanceOfType<VC.TextBlock>(control.Items[0], "a plain ItemsControl keeps its data projection");
    }

    [TestMethod]
    public void ListBox_WrapsRowsInContainersThatPaintSelectionAndHover()
    {
        using var surface = new VisualHost { Size = new Size(220, 160), DpiScaleOverride = 1f };
        using var list = new VC.ListBox
        {
            Width = 200,
            Height = 140,
            ItemsSource = new[] { "alpha", "beta", "gamma" },
        };
        surface.Root = list;
        Layout(surface, 220, 160);

        Assert.IsInstanceOfType<VC.ListBoxItem>(list.Items[0], "each row is a public list container");
        var row = (VC.ListBoxItem)list.Items[1];
        var selectionBand = row.GetTemplateChild<VC.Border>("SelectionBand");
        var hoverBand = row.GetTemplateChild<VC.Border>("HoverBand");
        Assert.IsNotNull(selectionBand, "the default container template exposes its selection band");
        Assert.IsNotNull(hoverBand, "the default container template exposes its hover band");
        Assert.AreEqual(0d, selectionBand!.Opacity, 0.001, "nothing is selected yet");

        list.SelectedIndex = 1;

        Assert.IsTrue(row.IsSelected, "the list pushes selection into the container");
        Assert.AreEqual(1d, selectionBand.Opacity, 0.001, "the container paints the selection band");

        surface.ProcessPointerMove(Center(row.AbsoluteBounds));

        Assert.IsTrue(row.IsPointerOver, "the list pushes hover into the container");
        Assert.AreEqual(1d, hoverBand!.Opacity, 0.001, "the container paints the hover band");

        surface.ProcessPointerMove(new PointF(2f, 2f));
        Assert.IsFalse(row.IsPointerOver, "leaving the row clears the hover state");
    }

    [TestMethod]
    public void VirtualList_DefaultShellPreservesVirtualizationAndContainerSelection()
    {
        var rows = Enumerable.Range(0, 100).Select(static index => (object)$"Row {index}").ToList();
        using var surface = new VisualHost { Size = new Size(220, 120), DpiScaleOverride = 1f };
        using var viewer = new VC.ScrollViewer { Width = 200, Height = 100 };
        using var list = new VC.VirtualList { ItemsSource = rows, ItemHeight = 20 };
        viewer.Content = list;
        surface.Root = viewer;
        Layout(surface, 220, 120);

        Assert.IsNotNull(list.TemplateRoot, "the built-in shell applies when the list is hosted");
        Assert.IsInstanceOfType<VC.ItemsPresenter>(list.TemplateRoot, "the virtual list shell is its realization host");
        Assert.AreEqual(2000f, viewer.ExtentHeight, 0.1f, "the full fixed-row extent still reaches the viewer");
        Assert.AreEqual(0, list.FirstRealizedIndex);
        Assert.AreEqual(5, list.RealizedCount, "only the viewport rows are realized");
        Assert.IsInstanceOfType<VC.ListBoxItem>(list.Items[0], "each virtual row is a list container");

        list.SelectedIndex = 2;

        var selected = (VC.ListBoxItem)list.Items[2];
        Assert.IsTrue(selected.IsSelected, "the list pushes selection into the realized container");
        Assert.AreEqual(
            1d,
            selected.GetTemplateChild<VC.Border>("SelectionBand")!.Opacity,
            0.001,
            "the container paints the selection band");

        viewer.VerticalOffset = 200f;
        Layout(surface, 220, 120);

        Assert.AreEqual(10, list.FirstRealizedIndex, "a 200px offset starts at row 10");
        Assert.AreEqual(5, list.RealizedCount);
        Assert.AreEqual(
            "Row 10",
            ((VC.TextBlock)((VC.ListBoxItem)list.Items[0]).Content!).Text,
            "a recycled container rebinds its content visual");
    }

    [TestMethod]
    public void TreeView_DefaultShellAndRowTemplateKeepExpansionSelectionAndHover()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var tree = new VC.TreeView { Width = 300, Height = 200 };
        var group = new VC.TreeViewItem { Header = "Group", IsExpanded = true };
        var child = new VC.TreeViewItem { Header = "Child" };
        group.Items.Add(child);
        var second = new VC.TreeViewItem { Header = "Second" };
        tree.Items.Add(group);
        tree.Items.Add(second);
        surface.Root = tree;
        Layout(surface, 320, 240);

        Assert.IsNotNull(tree.TemplateRoot, "the built-in tree shell applies when the tree is hosted");
        var rootHost = tree.GetTemplateChild<VC.ItemsPresenter>("PART_ItemsPresenter");
        Assert.IsNotNull(rootHost, "the tree shell declares the root row host");
        Assert.IsTrue(rootHost!.Children.Contains(group), "root rows live in the shell presenter");
        Assert.IsTrue(rootHost.Children.Contains(second));

        Assert.AreEqual("Group", group.GetTemplateChild<VC.TextBlock>("PART_Header")!.Text);
        Assert.IsTrue(group.HasItems, "a row with children reports that state to its template");
        Assert.IsFalse(second.HasItems);

        var childHost = group.GetTemplateChild<VC.ItemsPresenter>("PART_ItemsPresenter");
        Assert.IsNotNull(childHost, "the row template hosts its children in a presenter");
        Assert.IsTrue(childHost!.Children.Contains(child), "an expanded row hosts its children");

        tree.SelectedItem = group;

        var selectionBand = group.GetTemplateChild<VC.Border>("SelectionBand");
        Assert.IsNotNull(selectionBand);
        Assert.AreEqual(1d, selectionBand!.Opacity, 0.001, "the row template paints selection");

        surface.ProcessPointerMove(Center(second.AbsoluteBounds));

        var hoverBand = second.GetTemplateChild<VC.Border>("HoverBand");
        Assert.IsNotNull(hoverBand);
        Assert.IsTrue(second.IsPointerOver, "the tree pushes hover into the row");
        Assert.AreEqual(1d, hoverBand!.Opacity, 0.001, "the row template paints hover");

        var expander = group.GetTemplateChild<VisualElement>("PART_Expander");
        Assert.IsNotNull(expander, "the default row template exposes a disclosure part");
        Click(surface, Center(expander!.AbsoluteBounds));

        Assert.IsFalse(group.IsExpanded, "clicking the disclosure part collapses the row");
        Assert.IsFalse(childHost.Children.Contains(child), "collapsing removes the child row");
        Assert.HasCount(2, tree.Rows);
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }

    private static void Click(VisualHost surface, PointF point)
    {
        surface.ProcessPointerDown(point, MouseButtons.Left);
        surface.ProcessPointerUp(point, MouseButtons.Left);
    }

    private static PointF Center(RectangleF bounds)
        => new(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));
}
