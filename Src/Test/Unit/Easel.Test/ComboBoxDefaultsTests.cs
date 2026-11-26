using System.Drawing;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;
using XamlVisibility = Easel.Controls.Visibility;

namespace Easel.Test;

/// <summary>
/// The built-in ComboBox default template: part contract, display text, editable lifecycle and the self-drawn
/// fallback when an explicit empty style suppresses the default.
/// </summary>
[STATestClass]
public sealed class ComboBoxDefaultsTests
{
    [TestMethod]
    public void DefaultStyle_AppliesTemplateAndPushesDisplayText()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var combo = new VC.ComboBox { Width = 180, SelectedIndex = 0, Placeholder = "Choose" };
        combo.Items.Add("One");
        combo.Items.Add("Two");
        surface.Root = combo;
        Layout(surface, 320, 240);

        Assert.IsNotNull(combo.TemplateRoot, "the implicit default style supplies a template without an explicit one");
        Assert.IsNotNull(combo.TemplatePopup, "the default template declares PART_Popup");
        Assert.IsNotNull(combo.EditableTextBox, "the default template declares PART_EditableTextBox");

        var display = combo.GetTemplateChild<VC.TextBlock>("PART_Display");
        Assert.IsNotNull(display, "the default template declares PART_Display");
        Assert.AreEqual("One", display!.Text, "the selected item text reaches PART_Display");

        combo.SelectedIndex = 1;
        Assert.AreEqual("Two", display.Text, "a selection change updates PART_Display");

        combo.SelectedIndex = -1;
        Assert.AreEqual("Choose", display.Text, "an empty selection falls back to the placeholder");
    }

    [TestMethod]
    public void DefaultStyle_DisplayFollowsDisplayMemberPath()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var combo = new VC.ComboBox { Width = 180, SelectedIndex = 0, DisplayMemberPath = nameof(Option.Name) };
        combo.Items.Add(new Option { Name = "Alpha", Code = "A" });
        combo.Items.Add(new Option { Name = "Beta", Code = "B" });
        surface.Root = combo;
        Layout(surface, 320, 240);

        var display = combo.GetTemplateChild<VC.TextBlock>("PART_Display")!;
        Assert.AreEqual("Alpha", display.Text, "DisplayMemberPath selects the display property");

        combo.DisplayMemberPath = nameof(Option.Code);
        Assert.AreEqual("A", display.Text, "changing DisplayMemberPath refreshes the display part");
    }

    [TestMethod]
    public void DefaultStyle_DropDownOpensAndCommitsThroughTemplateParts()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var combo = new VC.ComboBox { Width = 180, SelectedIndex = 0 };
        combo.Items.Add("One");
        combo.Items.Add("Two");
        combo.Items.Add("Three");
        surface.Root = combo;
        Layout(surface, 320, 240);

        var host = combo.GetTemplateChild<VC.ItemsPresenter>("PART_ItemsPresenter");
        Assert.IsNotNull(host, "the default template declares PART_ItemsPresenter");
        Assert.AreSame(host, combo.DropDownList.ParentElement, "the realized list moved into the template host");

        var comboCenter = Center(combo.AbsoluteBounds);
        surface.ProcessPointerDown(comboCenter, MouseButtons.Left);
        surface.ProcessPointerUp(comboCenter, MouseButtons.Left);

        Assert.IsTrue(combo.IsDropDownOpen, "a pointer press opens the templated drop-down");
        Assert.IsTrue(combo.TemplatePopup!.IsOpen, "the template popup is the drop-down");

        var row = Center(combo.DropDownList.Items[2].AbsoluteBounds);
        surface.ProcessPointerDown(row, MouseButtons.Left);
        surface.ProcessPointerUp(row, MouseButtons.Left);

        Assert.IsFalse(combo.IsDropDownOpen, "choosing a row closes the templated drop-down");
        Assert.AreEqual(2, combo.SelectedIndex);
        Assert.AreEqual("Three", combo.SelectedItem);
    }

    [TestMethod]
    public void DefaultStyle_EditableTriggerRevealsAndSyncsTheEditor()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var combo = new VC.ComboBox { Width = 180, IsEditable = true, SelectedIndex = 1 };
        combo.Items.Add("One");
        combo.Items.Add("Two");
        surface.Root = combo;
        Layout(surface, 320, 240);

        var editor = combo.EditableTextBox;
        Assert.IsNotNull(editor, "the editable part stays resolvable in the default template");
        Assert.AreEqual(XamlVisibility.Visible, editor!.Visibility, "IsEditable reveals the editor part");
        Assert.AreEqual("Two", editor.Text, "the selected item drives the editable text");

        var display = combo.GetTemplateChild<VC.TextBlock>("PART_Display");
        Assert.IsNotNull(display);
        Assert.AreEqual(XamlVisibility.Collapsed, display!.Visibility, "the display part yields to the editor");
    }

    [TestMethod]
    public void ExplicitEmptyStyle_KeepsTheSelfDrawnPath()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        // An explicit empty style suppresses the implicit default; the control must still draw and select itself.
        using var combo = new VC.ComboBox
        {
            Width = 180,
            SelectedIndex = 0,
            Style = new VisualStyle { TargetType = typeof(VC.ComboBox) },
        };
        combo.Items.Add("One");
        combo.Items.Add("Two");
        surface.Root = combo;
        Layout(surface, 320, 240);

        Assert.IsNull(combo.TemplateRoot, "the default template is not applied");
        Assert.IsNull(combo.TemplatePopup);
        Assert.IsNull(combo.EditableTextBox);

        var comboCenter = Center(combo.AbsoluteBounds);
        surface.ProcessPointerDown(comboCenter, MouseButtons.Left);
        surface.ProcessPointerUp(comboCenter, MouseButtons.Left);
        Assert.IsTrue(combo.IsDropDownOpen, "the no-template popup path still opens");

        var row = Center(combo.DropDownList.Items[1].AbsoluteBounds);
        surface.ProcessPointerDown(row, MouseButtons.Left);
        surface.ProcessPointerUp(row, MouseButtons.Left);
        Assert.AreEqual(1, combo.SelectedIndex, "the no-template popup still commits a row");
        Assert.IsFalse(combo.IsDropDownOpen);
    }

    [TestMethod]
    public void DefaultStyle_InlineComboBoxItemShowsItsText()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var combo = new VC.ComboBox { Width = 180, SelectedIndex = 0 };
        combo.Items.Add(new VC.ComboBoxItem { Text = "Alpha" });
        combo.Items.Add(new VC.ComboBoxItem { Text = "Beta" });
        surface.Root = combo;
        Layout(surface, 320, 240);

        var display = combo.GetTemplateChild<VC.TextBlock>("PART_Display");
        Assert.IsNotNull(display);
        Assert.AreEqual("Alpha", display!.Text, "an inline ComboBoxItem shows its Text, not its type name");

        combo.SelectedIndex = 1;
        Assert.AreEqual("Beta", display.Text, "a later inline item shows its own text");
    }

    [TestMethod]
    public void DefaultStyle_EditablePartKeepsOnlyTheContentHost()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var combo = new VC.ComboBox
        {
            Width = 180,
            IsEditable = true,
            Placeholder = "Choose",
        };
        combo.Items.Add("One");
        combo.Items.Add("Two");
        surface.Root = combo;
        Layout(surface, 320, 240);

        var editor = combo.EditableTextBox;
        Assert.IsNotNull(editor);
        Assert.IsInstanceOfType<VC.TextBoxContentHost>(
            editor!.TemplateRoot,
            "the default editor template keeps only the content host, not the TextBox chrome");
        Assert.AreEqual(string.Empty, editor.Text, "no selection leaves the editor empty");
        Assert.AreEqual("Choose", editor.Placeholder, "the placeholder stays on the TextBox");
        Assert.IsFalse(combo.HasSelection);

        combo.SelectedIndex = 0;
        Assert.AreEqual("One", editor.Text, "a selection drives the editor text");
        Assert.AreEqual("Choose", editor.Placeholder, "the placeholder is not consumed by the selection");
    }

    [TestMethod]
    public void DefaultStyle_EmptyComboDoesNotOpen()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var combo = new VC.ComboBox { Width = 180, Placeholder = "Nothing here" };
        surface.Root = combo;
        Layout(surface, 320, 240);

        combo.IsDropDownOpen = true;
        Assert.IsFalse(combo.IsDropDownOpen, "an empty combo ignores a programmatic open");
        Assert.IsFalse(combo.TemplatePopup!.IsOpen);

        var center = Center(combo.AbsoluteBounds);
        surface.ProcessPointerDown(center, MouseButtons.Left);
        surface.ProcessPointerUp(center, MouseButtons.Left);
        Assert.IsFalse(combo.IsDropDownOpen, "a pointer press on an empty combo does not open");
    }

    [TestMethod]
    public void DefaultStyle_DisablingClosesAndLocksTheDropDown()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var combo = new VC.ComboBox { Width = 180, SelectedIndex = 0 };
        combo.Items.Add("One");
        combo.Items.Add("Two");
        surface.Root = combo;
        Layout(surface, 320, 240);

        combo.IsDropDownOpen = true;
        Layout(surface, 320, 240);
        Assert.IsTrue(combo.TemplatePopup!.IsOpen);

        combo.IsEnabled = false;
        Assert.IsFalse(combo.IsDropDownOpen, "disabling closes the drop-down");
        Assert.IsFalse(combo.TemplatePopup!.IsOpen, "the overlay popup closes with the control");

        combo.IsDropDownOpen = true;
        Assert.IsFalse(combo.IsDropDownOpen, "a disabled combo stays closed");

        combo.IsEnabled = true;
        combo.IsDropDownOpen = true;
        Assert.IsTrue(combo.IsDropDownOpen, "re-enabling restores opening");
        Assert.IsTrue(combo.TemplatePopup!.IsOpen);
    }

    [TestMethod]
    public void DefaultStyle_DropDownMatchesTheComboWidth()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var combo = new VC.ComboBox { Width = 180, SelectedIndex = 0 };
        combo.Items.Add("One");
        combo.Items.Add("Two");
        surface.Root = combo;
        Layout(surface, 320, 240);

        combo.IsDropDownOpen = true;
        Layout(surface, 320, 240);

        var popup = combo.TemplatePopup!;
        Assert.AreEqual(combo.Bounds.Width, popup.Bounds.Width, 0.5f,
            "the popup surface matches the control width");
        Assert.AreEqual(combo.AbsoluteBounds.Left, popup.AbsoluteBounds.Left, 0.5f,
            "the popup starts at the control's left edge");
        Assert.AreEqual(
            popup.AbsoluteBounds.Left + popup.Padding.Left,
            combo.DropDownList.AbsoluteBounds.Left,
            0.5f,
            "the list starts inside the popup padding");
    }

    [TestMethod]
    public void DefaultStyle_DropDownSurfaceFollowsThemeMode()
    {
        var originalMode = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;
            var dark = Theme.CreateDark();
            using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
            using var combo = new VC.ComboBox { Width = 180, SelectedIndex = 0 };
            combo.Items.Add("One");
            combo.Items.Add("Two");
            surface.Root = combo;
            Layout(surface, 320, 240);
            combo.IsDropDownOpen = true;
            Layout(surface, 320, 240);

            var popup = combo.TemplatePopup!;
            Assert.AreEqual(dark.Surface.ToArgb(), popup.Background!.ApproximateColor.ToArgb(),
                "the popup surface reads the dark palette");

            Theme.Mode = ThemeMode.Light;
            var light = Theme.CreateLight();
            Assert.AreEqual(light.Surface.ToArgb(), popup.Background!.ApproximateColor.ToArgb(),
                "an open popup follows a theme switch");
            Assert.AreEqual(light.ControlBorder.ToArgb(), popup.BorderBrush!.ApproximateColor.ToArgb(),
                "an open popup follows the switched border colour");
        }
        finally
        {
            Theme.Mode = originalMode;
        }
    }

    private sealed class Option
    {
        public string Name { get; init; } = string.Empty;

        public string Code { get; init; } = string.Empty;

        public override string ToString() => "Option";
    }

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
