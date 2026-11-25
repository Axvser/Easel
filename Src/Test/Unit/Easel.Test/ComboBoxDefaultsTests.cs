using System.Drawing;
using System.Windows.Forms;
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
