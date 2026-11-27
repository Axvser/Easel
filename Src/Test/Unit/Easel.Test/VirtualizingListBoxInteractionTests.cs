using System.Drawing;
using System.Windows.Forms;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Interaction coverage for <see cref="VC.VirtualizingListBox"/>: pointer selection, keyboard navigation, hover and
/// selection state must map through the recycled window to the source index, not to a host position.
/// </summary>
[STATestClass]
public sealed class VirtualizingListBoxInteractionTests
{
    private const int ItemHeight = 20;
    private const int ViewportWidth = 200;
    private const int ViewportHeight = 100;

    [TestMethod]
    public void PointerSelectionMapsThroughARecycledWindow()
    {
        var source = Strings(1_000);
        using var surface = new VisualHost { Size = new Size(240, 140), DpiScaleOverride = 1f };
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var list = new VC.VirtualizingListBox { ItemHeight = ItemHeight, ItemsSource = source };
        viewer.Content = list;
        surface.Root = viewer;
        Layout(surface, 240, 140);

        viewer.VerticalOffset = 40 * ItemHeight;
        Layout(surface, 240, 140);
        Assert.AreEqual(40, list.FirstRealizedIndex);

        var target = list.Items[3];
        var point = Center(target.AbsoluteBounds);
        surface.ProcessPointerDown(point, MouseButtons.Left);
        surface.ProcessPointerUp(point, MouseButtons.Left);

        Assert.AreEqual(43, list.SelectedIndex, "a click maps the host position back to the source index");
        Assert.AreSame(source[43], list.SelectedItem);
        Assert.IsTrue(((VC.ListBoxItem)target).IsSelected);

        viewer.VerticalOffset = 900 * ItemHeight;
        Layout(surface, 240, 140);
        Assert.AreEqual(43, list.SelectedIndex, "selection is index-based and survives the window moving away");
        Assert.AreSame(source[43], list.SelectedItem);

        viewer.VerticalOffset = 40 * ItemHeight;
        Layout(surface, 240, 140);
        Assert.AreEqual(43, list.SelectedIndex);
        Assert.IsTrue(((VC.ListBoxItem)list.Items[3]).IsSelected,
            "the recycled row at the selected index paints the selected state again");
    }

    [TestMethod]
    public void KeyboardNavigationMovesTheSelectedIndex()
    {
        var source = Strings(40);
        using var surface = new VisualHost { Size = new Size(240, 140), DpiScaleOverride = 1f };
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var list = new VC.VirtualizingListBox { ItemHeight = ItemHeight, ItemsSource = source };
        viewer.Content = list;
        surface.Root = viewer;
        Layout(surface, 240, 140);

        Assert.IsTrue(surface.FocusElement(list), "the list takes focus for keyboard navigation");
        Assert.AreEqual(-1, list.SelectedIndex);

        surface.ProcessKeyDown(Keys.Down);
        Assert.AreEqual(0, list.SelectedIndex, "Down with no selection selects the first row");
        surface.ProcessKeyDown(Keys.Down);
        Assert.AreEqual(1, list.SelectedIndex);
        surface.ProcessKeyDown(Keys.Up);
        Assert.AreEqual(0, list.SelectedIndex);
        surface.ProcessKeyDown(Keys.Up);
        Assert.AreEqual(0, list.SelectedIndex, "Up clamps at the first row");
        surface.ProcessKeyDown(Keys.End);
        Assert.AreEqual(39, list.SelectedIndex, "End selects the last source index");
        surface.ProcessKeyDown(Keys.Home);
        Assert.AreEqual(0, list.SelectedIndex, "Home selects the first source index");
        Assert.AreSame(source[0], list.SelectedItem, "SelectedItem follows the index");
    }

    [TestMethod]
    public void HoverMapsToTheVisibleIndexAfterScrolling()
    {
        var source = Strings(400);
        using var surface = new VisualHost { Size = new Size(240, 140), DpiScaleOverride = 1f };
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var list = new VC.VirtualizingListBox { ItemHeight = ItemHeight, ItemsSource = source };
        viewer.Content = list;
        surface.Root = viewer;
        Layout(surface, 240, 140);

        surface.ProcessPointerMove(Center(list.Items[2].AbsoluteBounds));
        Assert.IsTrue(((VC.ListBoxItem)list.Items[2]).IsPointerOver, "the hovered row paints its hover state");

        viewer.VerticalOffset = 200 * ItemHeight;
        Layout(surface, 240, 140);
        surface.ProcessPointerMove(Center(list.Items[1].AbsoluteBounds));

        var hovered = list.Items.OfType<VC.ListBoxItem>().Where(static row => row.IsPointerOver).ToArray();
        Assert.HasCount(1, hovered, "only the row under the pointer is hovered after the window moved");
        Assert.AreSame(list.Items[1], hovered[0], "hover follows the recycled host position, not the old index");
        Assert.AreEqual(201, list.FirstRealizedIndex + 1);
    }

    [TestMethod]
    public void ItemExtentAliasMirrorsItemHeight()
    {
        using var list = new VC.VirtualizingListBox();

        list.ItemHeight = 31d;
        Assert.AreEqual(31d, list.ItemExtent, "ItemExtent reads ItemHeight");

        list.ItemExtent = 42d;
        Assert.AreEqual(42d, list.ItemHeight, "writing ItemExtent writes ItemHeight");
    }

    [TestMethod]
    public void ReplacingTheSourceClampsSelectionAndRebindsTheWindow()
    {
        var source = Strings(50);
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var list = new VC.VirtualizingListBox { ItemHeight = ItemHeight, ItemsSource = source };
        viewer.Content = list;
        Layout(viewer);

        list.SelectedIndex = 2;
        var selectedVisual = list.Items[2];
        Assert.IsTrue(((VC.ListBoxItem)selectedVisual).IsSelected);

        list.SelectedIndex = 49;
        list.ItemsSource = Strings(3);
        Layout(viewer);

        Assert.AreEqual(-1, list.SelectedIndex, "a shrunken source clamps a stale selection");
        Assert.IsNull(list.SelectedItem);
        Assert.AreEqual(3, list.RealizedCount);
        Assert.IsTrue(list.Items.All(static item => item.DataContext is string),
            "the replacement source is bound to the reused visuals");
    }

    [TestMethod]
    public void PointerSelectionWorksAfterAWindowJump()
    {
        var source = Strings(500);
        using var surface = new VisualHost { Size = new Size(240, 140), DpiScaleOverride = 1f };
        using var viewer = new VC.ScrollViewer { Width = ViewportWidth, Height = ViewportHeight };
        using var list = new VC.VirtualizingListBox { ItemHeight = ItemHeight, ItemsSource = source };
        viewer.Content = list;
        surface.Root = viewer;
        Layout(surface, 240, 140);

        viewer.VerticalOffset = 300 * ItemHeight;
        Layout(surface, 240, 140);

        var target = list.Items[^1];
        surface.ProcessPointerDown(Center(target.AbsoluteBounds), MouseButtons.Left);
        surface.ProcessPointerUp(Center(target.AbsoluteBounds), MouseButtons.Left);

        Assert.AreEqual(list.FirstRealizedIndex + list.Items.Count - 1, list.SelectedIndex,
            "a click after a jump maps to the source index of the clicked row");
        Assert.AreSame(source[list.SelectedIndex], list.SelectedItem);
    }

    private static List<object?> Strings(int count)
    {
        var rows = new List<object?>(count);
        for (var index = 0; index < count; index++)
        {
            rows.Add($"Row {index}");
        }

        return rows;
    }

    private static PointF Center(RectangleF bounds)
        => new(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));

    private static void Layout(VisualElement root)
    {
        root.Measure(new Size(ViewportWidth, ViewportHeight));
        root.Arrange(new RectangleF(0f, 0f, ViewportWidth, ViewportHeight));
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
