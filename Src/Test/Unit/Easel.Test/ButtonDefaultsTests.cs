using System.Drawing;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;
using VShape = Easel.Visuals.Controls.Shapes;

namespace Easel.Test;

/// <summary>
/// The built-in Button family templates: exact-type implicit styles for RepeatButton and the toggle controls,
/// the checked/indeterminate triggers, and the opt-out path.
/// </summary>
[STATestClass]
public sealed class ButtonDefaultsTests
{
    [TestMethod]
    public void ButtonFamily_GetsBuiltInTemplatesOnAttach()
    {
        using var surface = CreateSurface(220, 220);
        using var root = new VC.StackPanel();
        var button = new VC.Button { Text = "Button" };
        var repeat = new VC.RepeatButton { Text = "Repeat" };
        var toggle = new VC.ToggleButton();
        var check = new VC.CheckBox();
        var radio = new VC.RadioButton();
        root.Add(button);
        root.Add(repeat);
        root.Add(toggle);
        root.Add(check);
        root.Add(radio);

        surface.Root = root;
        Layout(surface, 220, 220);

        Assert.IsNotNull(button.TemplateRoot, "Button keeps its built-in template");
        Assert.IsNotNull(repeat.TemplateRoot, "RepeatButton has its own exact-type style");
        Assert.IsNotNull(toggle.TemplateRoot, "ToggleButton draws its switch in the template");
        Assert.IsNotNull(check.TemplateRoot, "CheckBox draws its box in the template");
        Assert.IsNotNull(radio.TemplateRoot, "RadioButton draws its ring in the template");
    }

    [TestMethod]
    public void RepeatButton_SharesTheButtonChromeTemplate()
    {
        using var surface = CreateSurface(200, 80);
        var repeat = new VC.RepeatButton { Text = "Repeat" };
        surface.Root = repeat;

        Assert.IsNotNull(repeat.TemplateRoot);
        Assert.IsNotNull(repeat.GetTemplateChild<VC.Border>("Chrome"), "the chrome part comes from the shared Button template");
        Assert.IsNotNull(repeat.GetTemplateChild<VC.Border>("FocusRing"));
    }

    [TestMethod]
    public void RepeatButton_HoverRevealsTheOverlayLayer()
    {
        using var surface = CreateSurface(200, 80);
        var repeat = new VC.RepeatButton { Text = "Repeat" };
        surface.Root = repeat;
        Layout(surface, 200, 80);

        var hover = repeat.GetTemplateChild<VC.Border>("Hover");
        Assert.IsNotNull(hover);
        Assert.AreEqual(0d, hover.Opacity, 0.001, "the hover layer starts hidden");

        surface.ProcessPointerMove(CenterOf(repeat));
        Assert.AreEqual(1d, hover.Opacity, 0.001, "the template trigger raises the hover layer");
    }

    [TestMethod]
    public void CheckBox_CheckedAndIndeterminateStatesDriveTheTemplateMarks()
    {
        using var surface = CreateSurface(80, 80);
        var box = new VC.CheckBox();
        surface.Root = box;

        var checkedBox = box.GetTemplateChild<VShape.Rectangle>("CheckedBox");
        var mark = box.GetTemplateChild<VShape.Polyline>("Mark");
        var dash = box.GetTemplateChild<VShape.Polyline>("Dash");
        Assert.IsNotNull(checkedBox);
        Assert.IsNotNull(mark);
        Assert.IsNotNull(dash);

        Assert.AreEqual(0d, checkedBox.Opacity, 0.001, "an unchecked box shows the plain surface");
        Assert.AreEqual(0d, mark.Opacity, 0.001);
        Assert.AreEqual(0d, dash.Opacity, 0.001);

        box.IsChecked = true;
        Assert.AreEqual(1d, checkedBox.Opacity, 0.001, "checked draws the accent fill");
        Assert.AreEqual(1d, mark.Opacity, 0.001, "checked draws the tick");
        Assert.AreEqual(0d, dash.Opacity, 0.001);

        box.IsChecked = null;
        Assert.AreEqual(0d, checkedBox.Opacity, 0.001, "indeterminate keeps the plain fill");
        Assert.AreEqual(0d, mark.Opacity, 0.001);
        Assert.AreEqual(1d, dash.Opacity, 0.001, "indeterminate draws the dash");
    }

    [TestMethod]
    public void ToggleButton_CheckedStateMovesTheKnobToTheAccentTrack()
    {
        using var surface = CreateSurface(80, 60);
        var toggle = new VC.ToggleButton();
        surface.Root = toggle;

        var track = toggle.GetTemplateChild<VC.Border>("Checked");
        var knobOff = toggle.GetTemplateChild<VShape.Ellipse>("KnobOff");
        var knobOn = toggle.GetTemplateChild<VShape.Ellipse>("KnobOn");
        Assert.IsNotNull(track);
        Assert.IsNotNull(knobOff);
        Assert.IsNotNull(knobOn);

        Assert.AreEqual(0d, track.Opacity, 0.001);
        Assert.AreEqual(1d, knobOff.Opacity, 0.001);
        Assert.AreEqual(0d, knobOn.Opacity, 0.001);

        toggle.IsChecked = true;
        Assert.AreEqual(1d, track.Opacity, 0.001, "checked fills the track with the accent");
        Assert.AreEqual(0d, knobOff.Opacity, 0.001);
        Assert.AreEqual(1d, knobOn.Opacity, 0.001, "checked moves the knob to the right");

        toggle.IsChecked = false;
        Assert.AreEqual(0d, track.Opacity, 0.001);
        Assert.AreEqual(1d, knobOff.Opacity, 0.001);
        Assert.AreEqual(0d, knobOn.Opacity, 0.001);
    }

    [TestMethod]
    public void RadioButton_CheckedStateShowsTheAccentRingAndDot()
    {
        using var surface = CreateSurface(80, 80);
        var radio = new VC.RadioButton();
        surface.Root = radio;

        var ring = radio.GetTemplateChild<VShape.Ellipse>("CheckedRing");
        var dot = radio.GetTemplateChild<VShape.Ellipse>("Dot");
        Assert.IsNotNull(ring);
        Assert.IsNotNull(dot);

        Assert.AreEqual(0d, ring.Opacity, 0.001);
        Assert.AreEqual(0d, dot.Opacity, 0.001);

        radio.IsChecked = true;
        Assert.AreEqual(1d, ring.Opacity, 0.001, "checked draws the accent ring");
        Assert.AreEqual(1d, dot.Opacity, 0.001, "checked draws the accent dot");
    }

    [TestMethod]
    public void ToggleTemplates_LeaveTheControlAsTheHitTarget()
    {
        using var surface = CreateSurface(120, 120);
        using var root = new VC.StackPanel { Spacing = 6 };
        var check = new VC.CheckBox();
        var radio = new VC.RadioButton();
        var toggle = new VC.ToggleButton();
        root.Add(check);
        root.Add(radio);
        root.Add(toggle);
        surface.Root = root;
        Layout(surface, 120, 120);

        Assert.AreSame(check, surface.FindElementAt(CenterOf(check)), "template children must not steal the hit");
        Assert.AreSame(radio, surface.FindElementAt(CenterOf(radio)));
        Assert.AreSame(toggle, surface.FindElementAt(CenterOf(toggle)));
    }

    [TestMethod]
    public void CheckBox_DefaultTemplatePaintsTheAccentFill()
    {
        using var surface = CreateSurface(28, 28);
        surface.BackColor = Color.Black;
        var box = new VC.CheckBox { Width = 28, Height = 28, IsChecked = true };
        surface.Root = box;

        using var bitmap = new Bitmap(28, 28);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            surface.RenderTo(graphics, new Size(28, 28));
        }

        var pixel = bitmap.GetPixel(7, 7);
        Assert.IsTrue(pixel.B > 150 && pixel.R < 80, "the checked template paints the accent fill, not the self-drawn glyph");
    }

    [TestMethod]
    public void EmptyExplicitStyle_OptsOutOfTheDefaultTemplate()
    {
        using var surface = CreateSurface(200, 80);
        var repeat = new VC.RepeatButton { Text = "Repeat", Style = new VisualStyle { TargetType = typeof(VC.RepeatButton) } };
        surface.Root = repeat;

        Assert.IsNull(repeat.TemplateRoot, "an explicit empty style keeps the self-drawn path");
    }

    private static VisualHost CreateSurface(int width, int height)
        => new() { Size = new Size(width, height) };

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }

    private static PointF CenterOf(VisualElement element)
    {
        var bounds = element.AbsoluteBounds;
        return new PointF(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));
    }
}
