using System.Drawing;
using System.Windows.Forms;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// A <see cref="VC.ListBox"/> inside its built-in shell keeps one container per source item but only attaches the
/// rows intersecting the viewport; spacers preserve the scroll extent and selection maps back to the source index.
/// </summary>
[STATestClass]
public sealed class ListBoxWindowVirtualizationTests
{
    private const int RowCount = 500;

    [TestMethod]
    public void ItemsStayLogicalWhileOnlyTheViewportIsAttached()
    {
        using var surface = new VisualHost { Size = new Size(280, 420), DpiScaleOverride = 1f };
        using var list = new VC.ListBox { Width = 240, Height = 380, ItemsSource = Source() };
        surface.Root = list;
        Layout(surface, 280, 420);

        Assert.AreEqual(RowCount, list.Items.Count, "Items stays index-for-index with the source");
        var attached = Attached(list);
        Assert.IsGreaterThan(0, attached.Count, "the viewport rows are attached");
        Assert.IsLessThan(RowCount, attached.Count, "offscreen rows stay out of the presenter");
        Assert.AreEqual(0, attached[0], "the window starts at the first row");

        var far = (VC.ListBoxItem)list.Items[RowCount - 1];
        Assert.IsNull(far.ParentElement, "a far row is not attached");
        Assert.IsNull(far.TemplateRoot, "a far row has no template yet");
        Assert.AreEqual(
            $"Row {RowCount - 1}",
            ((VC.TextBlock)far.Content!).Text,
            "the content visual still exists for an offscreen row");
    }

    [TestMethod]
    public void ScrollExtentCoversTheWholeSourceNotOnlyTheWindow()
    {
        using var surface = new VisualHost { Size = new Size(280, 420), DpiScaleOverride = 1f };
        using var list = new VC.ListBox { Width = 240, Height = 380, ItemsSource = Source() };
        surface.Root = list;
        Layout(surface, 280, 420);

        var scroll = list.GetTemplateChild<VC.ScrollViewer>("PART_ScrollViewer");
        Assert.IsNotNull(scroll, "the built-in shell provides the viewport");
        var rowHeight = list.Items[0].Bounds.Height;
        Assert.IsGreaterThan(0f, rowHeight, "the first row was measured");
        Assert.AreEqual(
            rowHeight * RowCount,
            scroll!.ExtentHeight,
            1.0,
            "the spacers report every source row to the scroll viewer");

        scroll.VerticalOffset = 2400f;
        Layout(surface, 280, 420);

        Assert.AreEqual(
            rowHeight * RowCount,
            scroll.ExtentHeight,
            1.0,
            "the extent does not shrink when the window moves");
        var attached = Attached(list);
        Assert.IsLessThan(RowCount, attached.Count, "the new window is still virtual");
        Assert.IsGreaterThan(50, attached[0], "the attached window followed the scroll offset");
        Assert.IsNull(((VC.ListBoxItem)list.Items[0]).ParentElement, "the first row left the window");
    }

    [TestMethod]
    public void KeyboardEndScrollsTheWindowToTheLastSourceRow()
    {
        using var surface = new VisualHost { Size = new Size(280, 420), DpiScaleOverride = 1f };
        using var list = new VC.ListBox { Width = 240, Height = 380, ItemsSource = Source() };
        surface.Root = list;
        Layout(surface, 280, 420);

        surface.FocusElement(list);
        surface.ProcessKeyDown(Keys.End);
        Layout(surface, 280, 420);

        Assert.AreEqual(RowCount - 1, list.SelectedIndex, "End selects the last source item");
        var last = (VC.ListBoxItem)list.Items[RowCount - 1];
        Assert.IsNotNull(last.ParentElement, "the window scrolled to the selected row");
        Assert.IsTrue(last.IsSelected, "the attached row paints selection");
        Assert.IsNotNull(last.TemplateRoot, "the selected row has its template");
        Assert.IsLessThan(RowCount, Realized(list), "reaching the end does not realize the whole source");

        surface.ProcessKeyDown(Keys.Home);
        Layout(surface, 280, 420);

        Assert.AreEqual(0, list.SelectedIndex, "Home returns to the first item");
        Assert.IsNotNull(list.Items[0].ParentElement, "the window returned to the top");
    }

    [TestMethod]
    public void PointerSelectionAfterScrollingMapsToTheSourceIndex()
    {
        using var surface = new VisualHost { Size = new Size(280, 420), DpiScaleOverride = 1f };
        using var list = new VC.ListBox { Width = 240, Height = 380, ItemsSource = Source() };
        surface.Root = list;
        Layout(surface, 280, 420);

        var scroll = list.GetTemplateChild<VC.ScrollViewer>("PART_ScrollViewer");
        Assert.IsNotNull(scroll);
        scroll!.VerticalOffset = 1600f;
        Layout(surface, 280, 420);

        var index = Attached(list)[3];
        var row = (VC.ListBoxItem)list.Items[index];
        var center = Center(row.AbsoluteBounds);
        surface.ProcessPointerDown(center, MouseButtons.Left);
        surface.ProcessPointerUp(center, MouseButtons.Left);

        Assert.AreEqual(index, list.SelectedIndex, "the click maps through the attached window");
        Assert.AreEqual($"Row {index}", list.SelectedItem, "the selected item is the source string");
        Assert.IsTrue(row.IsSelected, "the clicked container carries the selection");
    }

    [TestMethod]
    public void UntemplatedListBoxStillAttachesAndRealizesEveryRow()
    {
        using var surface = new VisualHost { Size = new Size(280, 420), DpiScaleOverride = 1f };
        using var list = new VC.ListBox
        {
            Width = 240,
            Height = 380,
            ItemsSource = Source(),
            Style = new VisualStyle { TargetType = typeof(VC.ListBox) },
        };
        surface.Root = list;
        Layout(surface, 280, 420);

        Assert.IsNull(list.TemplateRoot, "the explicit style removes the default shell template");
        Assert.AreSame(list, list.Items[0].ParentElement, "the self-arranged path attaches rows to the list");
        Assert.IsNotNull(((VC.ListBoxItem)list.Items[0]).TemplateRoot, "row templates are realized without a shell");
        Assert.AreEqual(0f, list.Items[0].Bounds.Y, 0.1f, "rows stack from the top");
        Assert.AreEqual(
            list.Items[0].Bounds.Height,
            list.Items[1].Bounds.Y,
            0.1f,
            "rows stack vertically");
    }

    private static List<object> Source()
        => Enumerable.Range(0, RowCount).Select(static index => (object)$"Row {index}").ToList();

    private static List<int> Attached(VC.ListBox list)
    {
        var attached = new List<int>();
        for (var index = 0; index < list.Items.Count; index++)
        {
            if (list.Items[index].ParentElement is not null)
            {
                attached.Add(index);
            }
        }

        return attached;
    }

    private static int Realized(VC.ListBox list)
        => list.Items.OfType<VC.ListBoxItem>().Count(static row => row.TemplateRoot is not null);

    private static PointF Center(RectangleF bounds)
        => new(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
