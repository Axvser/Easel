using System.Drawing;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;
using VCS = Easel.Visuals.Controls.Shapes;

namespace Easel.Test;

/// <summary>The built-in menu family shell templates and row container style.</summary>
[STATestClass]
public sealed class MenuDefaultsTests
{
    [TestMethod]
    public void ContextMenu_DefaultShellRealizesRowsAndPaintsTheirStates()
    {
        using var surface = new VisualHost { Size = new Size(360, 320), DpiScaleOverride = 1f };
        using var root = new VC.StackPanel();
        using var target = new VC.Border { Width = 80f, Height = 30f, Background = Color.Lime };
        root.Add(target);
        surface.Root = root;

        var invoked = 0;
        var delete = new VC.VisualMenuItem
        {
            Header = "Delete",
            InputGestureText = "Del",
            Command = () => invoked++,
        };
        var parent = new VC.VisualMenuItem { Header = "More" };
        parent.Items.Add(new VC.VisualMenuItem { Header = "Nested" });
        var check = new VC.VisualMenuItem { Header = "Show grid", IsChecked = true };
        var disabled = new VC.VisualMenuItem { Header = "Off", IsEnabled = false };
        var separator = new VC.VisualMenuItem { IsSeparator = true };

        using var menu = new VC.VisualContextMenu();
        menu.Items.Add(delete);
        menu.Items.Add(parent);
        menu.Items.Add(check);
        menu.Items.Add(disabled);
        menu.Items.Add(separator);

        menu.OpenAt(target, new PointF(target.AbsoluteBounds.X + 2f, target.AbsoluteBounds.Y + 2f));
        Layout(surface, 360, 320);

        Assert.IsNotNull(menu.TemplateRoot, "the built-in context menu shell applies when the menu opens");
        Assert.IsNotNull(
            menu.GetTemplateChild<VC.ItemsPresenter>("PART_ItemsPresenter"),
            "the built-in shell declares the row host");
        Assert.HasCount(5, menu.Containers, "one container is realized per row");

        var first = menu.Containers[0];
        Assert.AreEqual("Delete", first.GetTemplateChild<VC.TextBlock>("RowHeader")!.Text);
        Assert.AreEqual("Del", first.GetTemplateChild<VC.TextBlock>("GestureText")!.Text);
        Assert.IsFalse(first.HasSubmenu);
        Assert.AreEqual(0d, first.GetTemplateChild<VC.Border>("SelectionBand")!.Opacity, 0.001);

        var submenuRow = menu.Containers[1];
        Assert.IsTrue(submenuRow.HasSubmenu, "a row with children reports its submenu state to the template");
        Assert.AreEqual(1d, submenuRow.GetTemplateChild<VCS.Path>("SubmenuArrow")!.Opacity, 0.001);

        Assert.IsTrue(menu.Containers[2].IsChecked, "the row mirrors the item's checked state");
        Assert.AreEqual(1d, menu.Containers[2].GetTemplateChild<VCS.Path>("CheckMark")!.Opacity, 0.001);

        var disabledRow = menu.Containers[3];
        Assert.IsFalse(disabledRow.IsEnabled, "the row mirrors the item's disabled state");
        Assert.AreEqual(0.55d, disabledRow.GetTemplateChild<VC.Grid>("Root")!.Opacity, 0.001);

        var separatorRow = menu.Containers[4];
        Assert.IsTrue(separatorRow.IsSeparator);
        Assert.AreEqual(
            Easel.Controls.Visibility.Visible,
            separatorRow.GetTemplateChild<VCS.Rectangle>("SeparatorLine")!.Visibility);
        Assert.AreEqual(9d, separatorRow.GetTemplateChild<VC.Grid>("RowContent")!.Height, 0.001);

        var parentPoint = Center(submenuRow.AbsoluteBounds);
        surface.ProcessPointerDown(parentPoint, MouseButtons.Left);
        surface.ProcessPointerUp(parentPoint, MouseButtons.Left);

        Assert.IsNotNull(menu.Submenu, "a row with children opens the nested menu");
        Assert.IsNotNull(menu.Submenu!.TemplateRoot, "the nested menu uses the built-in shell");
        Assert.HasCount(1, menu.Submenu.Containers, "the nested menu realized its child row");

        surface.ProcessPointerMove(Center(first.AbsoluteBounds));

        Assert.IsNull(menu.Submenu, "moving to a leaf row closes the nested menu");

        Assert.IsTrue(first.IsHighlighted, "hovering a row moves the menu highlight");
        Assert.IsTrue(menu.IsPointerOver, "the shell exposes hover to its template");
        Assert.AreEqual(1d, first.GetTemplateChild<VC.Border>("SelectionBand")!.Opacity, 0.001);
        Assert.AreEqual(1d, menu.GetTemplateChild<VC.Border>("HoverRing")!.Opacity, 0.001);

        menu.Focus();
        surface.ProcessKeyDown(Keys.Down);

        Assert.IsTrue(submenuRow.IsHighlighted, "the keyboard selection uses the same row state");
        Assert.AreEqual(1d, submenuRow.GetTemplateChild<VC.Border>("SelectionBand")!.Opacity, 0.001);
        Assert.IsFalse(first.IsHighlighted, "moving the selection clears the previous row");

        surface.ProcessPointerDown(Center(first.AbsoluteBounds), MouseButtons.Left);
        Assert.AreEqual(1d, first.GetTemplateChild<VC.Border>("PressedBand")!.Opacity, 0.001);

        surface.ProcessPointerUp(Center(first.AbsoluteBounds), MouseButtons.Left);

        Assert.AreEqual(1, invoked, "clicking a templated row invokes its command");
        Assert.IsFalse(menu.IsOpen, "invoking a row closes the menu");
    }

    [TestMethod]
    public void VisualMenu_DefaultShellRealizesHeadersAndOpensItsDropDown()
    {
        using var surface = new VisualHost { Size = new Size(360, 220), DpiScaleOverride = 1f };
        using var bar = new VC.VisualMenu();
        var file = new VC.VisualMenuItem { Header = "File" };
        file.Items.Add(new VC.VisualMenuItem { Header = "Open", Command = () => { } });
        var edit = new VC.VisualMenuItem { Header = "Edit" };
        edit.Items.Add(new VC.VisualMenuItem { Header = "Copy" });
        bar.Items.Add(file);
        bar.Items.Add(edit);

        surface.Root = bar;
        Layout(surface, 360, 220);

        Assert.IsNotNull(bar.TemplateRoot, "the built-in menu bar shell applies when the bar is hosted");
        var host = bar.GetTemplateChild<VC.StackPanel>("PART_ItemsPresenter");
        Assert.IsNotNull(host, "the bar shell declares a horizontal header host");
        Assert.AreEqual(Orientation.Horizontal, host!.Orientation);
        Assert.HasCount(2, bar.Containers, "one container is realized per top-level item");

        var first = bar.Containers[0];
        Assert.IsTrue(first.IsTopLevel);
        Assert.AreEqual("File", first.GetTemplateChild<VC.TextBlock>("TopLevelHeader")!.Text);
        Assert.AreEqual(
            Easel.Controls.Visibility.Visible,
            first.GetTemplateChild<VC.TextBlock>("TopLevelHeader")!.Visibility);
        Assert.AreEqual(
            Easel.Controls.Visibility.Collapsed,
            first.GetTemplateChild<VC.Grid>("RowContent")!.Visibility,
            "a top-level header hides the drop-down row layout");

        var point = Center(first.AbsoluteBounds);
        surface.ProcessPointerDown(point, MouseButtons.Left);
        surface.ProcessPointerUp(point, MouseButtons.Left);

        Assert.IsNotNull(bar.OpenedMenu, "pressing a templated header opens its drop-down");
        Assert.IsTrue(first.IsHighlighted, "the open header exposes its state to the row template");
        Assert.AreEqual(1d, first.GetTemplateChild<VC.Border>("SelectionBand")!.Opacity, 0.001);
        Assert.IsNotNull(bar.OpenedMenu!.TemplateRoot, "the drop-down uses the built-in context menu shell");
        Assert.HasCount(1, bar.OpenedMenu.Containers);
    }

    [TestMethod]
    public void MenuFamily_ExplicitEmptyStyleRestoresTheSelfDrawnFallback()
    {
        using var surface = new VisualHost { Size = new Size(360, 260), DpiScaleOverride = 1f };
        using var root = new VC.StackPanel();
        using var target = new VC.Border { Width = 80f, Height = 30f, Background = Color.Lime };
        root.Add(target);
        surface.Root = root;

        // An explicit surface style wins over the built-in default, so the self-drawn path handles appearance again.
        surface.Styles.Add(new VisualStyle { TargetType = typeof(VC.VisualContextMenu) });
        surface.Styles.Add(new VisualStyle { TargetType = typeof(VC.VisualMenu) });

        var invoked = 0;
        using var menu = new VC.VisualContextMenu();
        menu.Items.Add(new VC.VisualMenuItem { Header = "Delete", Command = () => invoked++ });
        menu.OpenAt(target, new PointF(target.AbsoluteBounds.X + 2f, target.AbsoluteBounds.Y + 2f));
        Layout(surface, 360, 260);

        Assert.IsNull(menu.TemplateRoot, "the self-drawn menu returns when the default template is opted out");
        Assert.AreEqual(0, menu.Containers.Count);

        var row = new PointF(menu.AbsoluteBounds.X + 30f, menu.AbsoluteBounds.Y + 18f);
        surface.ProcessPointerDown(row, MouseButtons.Left);
        surface.ProcessPointerUp(row, MouseButtons.Left);

        Assert.AreEqual(1, invoked, "the self-drawn row still invokes its command");
        Assert.IsFalse(menu.IsOpen);

        using var bar = new VC.VisualMenu();
        var file = new VC.VisualMenuItem { Header = "File" };
        file.Items.Add(new VC.VisualMenuItem { Header = "Open" });
        bar.Items.Add(file);
        surface.Root = bar;
        Layout(surface, 360, 260);

        Assert.IsNull(bar.TemplateRoot, "the self-drawn bar returns when the default template is opted out");
        Assert.AreEqual(0, bar.Containers.Count);

        surface.ProcessPointerDown(
            new PointF(bar.AbsoluteBounds.X + 10f, bar.AbsoluteBounds.Y + 10f),
            MouseButtons.Left);
        surface.ProcessPointerUp(
            new PointF(bar.AbsoluteBounds.X + 10f, bar.AbsoluteBounds.Y + 10f),
            MouseButtons.Left);

        Assert.IsNotNull(bar.OpenedMenu, "the self-drawn header still opens its drop-down");
        Assert.IsNull(bar.OpenedMenu!.TemplateRoot, "the drop-down inherits the opted-out style too");
    }

    [TestMethod]
    public void MenuFamily_DefaultPartsFollowTheThemePalette()
    {
        var originalMode = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;

            using var surface = new VisualHost { Size = new Size(360, 320), DpiScaleOverride = 1f };
            using var root = new VC.StackPanel();
            using var target = new VC.Border { Width = 80f, Height = 30f, Background = Color.Lime };
            root.Add(target);
            surface.Root = root;

            var parent = new VC.VisualMenuItem { Header = "More" };
            parent.Items.Add(new VC.VisualMenuItem { Header = "Nested" });
            using var menu = new VC.VisualContextMenu();
            menu.Items.Add(new VC.VisualMenuItem { Header = "Delete" });
            menu.Items.Add(new VC.VisualMenuItem { Header = "Show grid", IsChecked = true });
            menu.Items.Add(parent);
            menu.Items.Add(new VC.VisualMenuItem { IsSeparator = true });
            menu.OpenAt(target, new PointF(target.AbsoluteBounds.X + 2f, target.AbsoluteBounds.Y + 2f));
            Layout(surface, 360, 320);

            var palette = Theme.Current;
            var chrome = menu.GetTemplateChild<VC.Border>("Chrome")!;
            Assert.AreEqual(palette.SurfaceAlt.ToArgb(), chrome.Background!.ApproximateColor.ToArgb());
            Assert.AreEqual(palette.ControlBorder.ToArgb(), chrome.BorderBrush!.ApproximateColor.ToArgb());

            var first = menu.Containers[0];
            Assert.AreEqual(
                palette.Selection.ToArgb(),
                first.GetTemplateChild<VC.Border>("SelectionBand")!.Background!.ApproximateColor.ToArgb());
            Assert.AreEqual(
                palette.ControlPressed.ToArgb(),
                first.GetTemplateChild<VC.Border>("PressedBand")!.Background!.ApproximateColor.ToArgb());
            Assert.AreEqual(
                palette.Accent.ToArgb(),
                menu.Containers[1].GetTemplateChild<VCS.Path>("CheckMark")!.Stroke!.ApproximateColor.ToArgb());
            Assert.AreEqual(
                palette.MutedForeground.ToArgb(),
                menu.Containers[2].GetTemplateChild<VCS.Path>("SubmenuArrow")!.Fill!.ApproximateColor.ToArgb());
            Assert.AreEqual(
                palette.ControlBorder.ToArgb(),
                menu.Containers[3].GetTemplateChild<VCS.Rectangle>("SeparatorLine")!.Fill!.ApproximateColor.ToArgb());

            surface.ProcessPointerMove(Center(first.AbsoluteBounds));
            var ring = menu.GetTemplateChild<VC.Border>("HoverRing")!;
            Assert.AreEqual(palette.FocusRing.ToArgb(), ring.BorderBrush!.ApproximateColor.ToArgb());
        }
        finally
        {
            Theme.Mode = originalMode;
        }
    }

    [TestMethod]
    public void MenuFamily_ItemsAddedAfterTheShellAppliesAreRealizedOnTheNextMeasure()
    {
        using var surface = new VisualHost { Size = new Size(360, 260), DpiScaleOverride = 1f };
        using var bar = new VC.VisualMenu();
        surface.Root = bar;
        Layout(surface, 360, 260);

        Assert.AreEqual(0, bar.Containers.Count, "the bar had no items when its shell was applied");

        bar.Items.Add(new VC.VisualMenuItem { Header = "File" });
        bar.Refresh();
        Layout(surface, 360, 260);

        Assert.HasCount(1, bar.Containers, "Refresh realizes rows added after the template");
        Assert.AreEqual("File", bar.Containers[0].GetTemplateChild<VC.TextBlock>("TopLevelHeader")!.Text);

        using var root = new VC.StackPanel();
        using var target = new VC.Border { Width = 80f, Height = 30f, Background = Color.Lime };
        root.Add(target);
        surface.Root = root;

        using var menu = new VC.VisualContextMenu();
        menu.OpenAt(target, new PointF(target.AbsoluteBounds.X + 2f, target.AbsoluteBounds.Y + 2f));
        Layout(surface, 360, 260);

        Assert.AreEqual(0, menu.Containers.Count, "the open menu had no rows when its shell was applied");

        menu.Items.Add(new VC.VisualMenuItem { Header = "Delete" });
        menu.InvalidateMeasure();
        Layout(surface, 360, 260);

        Assert.HasCount(1, menu.Containers, "the next measure realizes rows added to an open menu");
        Assert.AreEqual("Delete", menu.Containers[0].GetTemplateChild<VC.TextBlock>("RowHeader")!.Text);
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }

    private static PointF Center(RectangleF bounds)
        => new(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));
}
