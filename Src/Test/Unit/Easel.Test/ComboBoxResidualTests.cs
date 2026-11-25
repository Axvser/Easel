using System.Drawing;
using System.Windows.Forms;
using Easel.Visuals;
using Easel.Visuals.Controls.Shapes;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// The ComboBox default template's editable press path, explicit toggle region, foreground-aware chevron,
/// muted placeholder and the ComboBoxItem row style.
/// </summary>
[STATestClass]
public sealed class ComboBoxResidualTests
{
    [TestMethod]
    public void DefaultTemplate_ArrowRegionTogglesTheDropDownOncePerClick()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var combo = new VC.ComboBox { Width = 180, SelectedIndex = 0 };
        combo.Items.Add("One");
        combo.Items.Add("Two");
        surface.Root = combo;
        Layout(surface, 320, 240);

        var arrow = combo.GetTemplateChild<VC.Border>("PART_Arrow");
        Assert.IsNotNull(arrow, "the default template declares a PART_Arrow region");
        Assert.IsTrue(arrow!.IsHitTestVisible, "the toggle region stays hit-testable");

        var arrowCenter = Center(arrow.AbsoluteBounds);
        surface.ProcessPointerDown(arrowCenter, MouseButtons.Left);
        surface.ProcessPointerUp(arrowCenter, MouseButtons.Left);
        Assert.IsTrue(combo.IsDropDownOpen, "clicking the arrow opens the drop-down");

        surface.ProcessPointerDown(arrowCenter, MouseButtons.Left);
        surface.ProcessPointerUp(arrowCenter, MouseButtons.Left);
        Assert.IsFalse(combo.IsDropDownOpen, "a second arrow click closes it without flipping twice");
    }

    [TestMethod]
    public void Editable_EditorPressOpensTheDropDownAndSelectionStillCommits()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var combo = new VC.ComboBox { Width = 180, IsEditable = true, SelectedIndex = 1 };
        combo.Items.Add("One");
        combo.Items.Add("Two");
        combo.Items.Add("Three");
        surface.Root = combo;
        Layout(surface, 320, 240);

        var editor = combo.EditableTextBox;
        Assert.IsNotNull(editor);
        var editorCenter = Center(editor!.AbsoluteBounds);
        surface.ProcessPointerDown(editorCenter, MouseButtons.Left);
        surface.ProcessPointerUp(editorCenter, MouseButtons.Left);
        Assert.IsTrue(combo.IsDropDownOpen, "pressing the editable text field opens the drop-down");

        var row = Center(combo.DropDownList.Items[2].AbsoluteBounds);
        surface.ProcessPointerDown(row, MouseButtons.Left);
        surface.ProcessPointerUp(row, MouseButtons.Left);

        Assert.IsFalse(combo.IsDropDownOpen, "choosing a row closes the drop-down");
        Assert.AreEqual(2, combo.SelectedIndex);
        Assert.AreEqual("Three", editor.Text, "the committed row refreshes the editable text");
    }

    [TestMethod]
    public void DefaultTemplate_ChevronUsesControlForegroundWhenSetAndThemeOtherwise()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var themed = new VC.ComboBox { Width = 180, SelectedIndex = 0 };
        themed.Items.Add("One");
        surface.Root = themed;
        Layout(surface, 320, 240);

        var themedChevron = themed.GetTemplateChild<VC.Grid>("ChevronDown");
        Assert.IsNotNull(themedChevron);
        var themedAccent = AccentPolygon(themedChevron!);
        Assert.IsNull(themedAccent.Fill, "an unset Foreground leaves the control brush absent so the theme shows");

        using var colored = new VC.ComboBox { Width = 180, SelectedIndex = 0, Foreground = Color.Red };
        colored.Items.Add("One");
        surface.Root = colored;
        Layout(surface, 320, 240);

        var coloredChevron = colored.GetTemplateChild<VC.Grid>("ChevronDown");
        Assert.IsNotNull(coloredChevron);
        var coloredAccent = AccentPolygon(coloredChevron!);
        Assert.IsNotNull(coloredAccent.Fill, "a control Foreground fills the overlay chevron");
        Assert.AreEqual(Color.Red.ToArgb(), coloredAccent.Fill!.ApproximateColor.ToArgb(),
            "the overlay follows the control Foreground");
    }

    [TestMethod]
    public void DefaultTemplate_PlaceholderIsMutedUntilAnItemIsSelected()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var combo = new VC.ComboBox { Width = 180, Placeholder = "Choose" };
        combo.Items.Add("One");
        surface.Root = combo;
        Layout(surface, 320, 240);

        var display = combo.GetTemplateChild<VC.TextBlock>("PART_Display");
        Assert.IsNotNull(display);
        Assert.AreEqual("Choose", display!.Text);
        Assert.AreEqual(0.65d, display.Opacity, 0.001d, "the placeholder renders dimmed");
        Assert.IsFalse(combo.HasSelection);

        combo.SelectedIndex = 0;
        Assert.IsTrue(combo.HasSelection);
        Assert.AreEqual("One", display.Text);
        Assert.AreEqual(1d, display.Opacity, 0.001d, "a real selection restores full contrast");
    }

    [TestMethod]
    public void ComboBoxItem_DefaultStyleSuppliesTheRowGeometry()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var combo = new VC.ComboBox { Width = 180, SelectedIndex = 0 };
        var item = new VC.ComboBoxItem { Text = "Alpha" };
        combo.Items.Add(item);
        surface.Root = combo;
        Layout(surface, 320, 240);

        Assert.AreEqual(8f, item.Padding.Left, "the implicit ComboBoxItem style sets the row padding");
        Assert.AreEqual(4f, item.Padding.Top);
        Assert.AreEqual(0, combo.SelectedIndex);
        var row = (VC.ListBoxItem)combo.DropDownList.Items[0];
        Assert.AreSame(item, row.Content, "the styled inline item stays the row content");
    }

    private static Polygon AccentPolygon(VC.Grid chevron)
        => chevron.Children.OfType<Polygon>().Last();

    private static PointF Center(RectangleF bounds)
        => new(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));

    private static void Layout(VisualHost surface, int width, int height)
    {
        surface.Size = new Size(width, height);
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
