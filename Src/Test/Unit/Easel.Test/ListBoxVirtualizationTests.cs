using System.Drawing;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// A <see cref="VC.ListBox"/> keeps its realized rows index-for-index but defers each row's control template until the
/// row reaches the viewport; rows that leave it are released again.
/// </summary>
[STATestClass]
public sealed class ListBoxVirtualizationTests
{
    private const int RowCount = 200;

    [TestMethod]
    public void LargeSource_RealizesOnlyTheRowsTouchingTheViewport()
    {
        var source = Enumerable.Range(0, RowCount).Select(static index => (object)$"Row {index}").ToList();
        using var surface = new VisualHost { Size = new Size(260, 420), DpiScaleOverride = 1f };
        using var list = new VC.ListBox { Width = 240, Height = 380, ItemsSource = source };
        surface.Root = list;
        Layout(surface, 260, 420);

        Assert.AreEqual(RowCount, list.Items.Count, "Items stays index-for-index with the source");
        var realized = RealizedCount(list);
        Assert.IsGreaterThan(0, realized, "the viewport rows are realized");
        Assert.IsLessThan(RowCount, realized, "rows outside the viewport keep their templates deferred");

        Assert.IsNotNull(Row(list, 0).TemplateRoot, "the first visible row has its template");
        Assert.IsNull(Row(list, RowCount - 1).TemplateRoot, "a far row has no template yet");
        Assert.IsTrue(Row(list, RowCount - 1).IsContentDeferred, "a far row also defers its content visual");
        Assert.AreEqual(
            "Row 0",
            ((VC.TextBlock)Row(list, 0).Content!).Text,
            "a realized row shows its source item");
        Assert.AreEqual(
            $"Row {RowCount - 1}",
            ((VC.TextBlock)Row(list, RowCount - 1).Content!).Text,
            "reading a deferred row still materializes its content");
        Assert.IsFalse(Row(list, RowCount - 1).IsContentDeferred, "reading Content realizes it");
    }

    [TestMethod]
    public void Scrolling_ReleasesRowsThatLeaveAndRealizesRowsThatEnter()
    {
        var source = Enumerable.Range(0, RowCount).Select(static index => (object)$"Row {index}").ToList();
        using var surface = new VisualHost { Size = new Size(260, 420), DpiScaleOverride = 1f };
        using var list = new VC.ListBox { Width = 240, Height = 380, ItemsSource = source };
        surface.Root = list;
        Layout(surface, 260, 420);

        var scroll = list.GetTemplateChild<VC.ScrollViewer>("PART_ScrollViewer");
        Assert.IsNotNull(scroll, "the built-in shell provides the viewport");

        var targetOffset = scroll!.VerticalOffset + 1600f;
        scroll.VerticalOffset = targetOffset;
        Layout(surface, 260, 420);

        Assert.IsNull(Row(list, 0).TemplateRoot, "the row that left the viewport released its template");
        Assert.IsNotNull(Row(list, 70).TemplateRoot, "a row inside the new viewport was realized");
        Assert.IsLessThan(RowCount, RealizedCount(list), "the list still does not template every row");
    }

    [TestMethod]
    public void SelectionAndHover_ReachRowsRealizedAfterTheFirstLayout()
    {
        var source = Enumerable.Range(0, RowCount).Select(static index => (object)$"Row {index}").ToList();
        using var surface = new VisualHost { Size = new Size(260, 420), DpiScaleOverride = 1f };
        using var list = new VC.ListBox { Width = 240, Height = 380, ItemsSource = source };
        surface.Root = list;
        Layout(surface, 260, 420);

        var scroll = list.GetTemplateChild<VC.ScrollViewer>("PART_ScrollViewer");
        Assert.IsNotNull(scroll);

        // 先选中尚未实现的远行，再滚到它：模板是之后才建的，触发器必须读到已同步的状态。
        list.SelectedIndex = 100;
        scroll!.VerticalOffset = 100 * 24f;
        Layout(surface, 260, 420);

        var row = Row(list, 100);
        Assert.IsNotNull(row.TemplateRoot, "the selected row was realized by scrolling to it");
        Assert.IsTrue(row.IsSelected, "selection reached the deferred container before its template existed");
        Assert.AreEqual(
            1d,
            row.GetTemplateChild<VC.Border>("SelectionBand")!.Opacity,
            0.001,
            "the newly created template paints the selection band");

        surface.ProcessPointerMove(Center(row.AbsoluteBounds));
        Assert.IsTrue(row.IsPointerOver, "hover reaches the row realized after the first layout");
        Assert.AreEqual(
            1d,
            row.GetTemplateChild<VC.Border>("HoverBand")!.Opacity,
            0.001,
            "the newly created template paints the hover band");
    }

    [TestMethod]
    public void Scrolling_KeepsTheSameContainerInstances()
    {
        var source = Enumerable.Range(0, RowCount).Select(static index => (object)$"Row {index}").ToList();
        using var surface = new VisualHost { Size = new Size(260, 420), DpiScaleOverride = 1f };
        using var list = new VC.ListBox { Width = 240, Height = 380, ItemsSource = source };
        surface.Root = list;
        Layout(surface, 260, 420);

        var containers = list.Items.ToArray();
        list.GetTemplateChild<VC.ScrollViewer>("PART_ScrollViewer")!.VerticalOffset = 2000f;
        Layout(surface, 260, 420);

        Assert.HasCount(RowCount, list.Items, "the item count is unchanged");
        for (var index = 0; index < containers.Length; index++)
        {
            Assert.AreSame(containers[index], list.Items[index], $"row {index} must not be recreated by scrolling");
        }
    }

    private static int RealizedCount(VC.ListBox list)
        => list.Items.OfType<VC.ListBoxItem>().Count(static row => row.TemplateRoot is not null);

    private static VC.ListBoxItem Row(VC.ListBox list, int index) => (VC.ListBoxItem)list.Items[index];

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }

    private static PointF Center(RectangleF bounds)
        => new(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));
}
