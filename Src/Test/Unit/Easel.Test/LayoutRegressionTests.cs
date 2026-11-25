using System.Drawing;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>Layout-cost regressions for the templated range controls and the ComboBox popup.</summary>
[STATestClass]
public sealed class LayoutRegressionTests
{
    [TestMethod]
    public void SliderValueChangesDoNotRelayoutTheSurface()
    {
        using var surface = new VisualHost { Size = new Size(320, 80), DpiScaleOverride = 1f };
        var slider = new VC.Slider { Width = 240, Height = 30, Minimum = 0, Maximum = 100, Value = 10 };
        surface.Root = slider;
        Layout(surface, 320, 80);

        var chrome = slider.GetTemplateChild<VC.SliderChrome>("PART_Chrome");
        Assert.IsNotNull(chrome);
        var startFraction = chrome!.Fraction;

        var layouts = 0;
        surface.LayoutUpdated += (_, _) => layouts++;

        slider.Value = 80;

        Assert.AreEqual(0, layouts, "a value change updates the chrome without a surface layout pass");
        Assert.IsGreaterThan(startFraction, chrome.Fraction, "the chrome follows the value");
    }

    [TestMethod]
    public void ComboBoxPopupWidthFollowsTheTarget()
    {
        using var surface = new VisualHost { Size = new Size(800, 500), DpiScaleOverride = 1f };
        var combo = new VC.ComboBox { Width = 160, SelectedIndex = 0 };
        combo.Items.Add("Dark+");
        combo.Items.Add("Light+");
        combo.Items.Add("High contrast");
        surface.Root = combo;
        Layout(surface, 800, 500);

        Assert.IsNotNull(combo.TemplatePopup, "the default template supplies the popup");
        combo.IsDropDownOpen = true;
        Layout(surface, 800, 500);

        Assert.IsLessThan(170, combo.TemplatePopup!.Bounds.Width, "the popup does not stretch to the surface width");
        Assert.IsGreaterThan(120, combo.TemplatePopup.Bounds.Width, "the popup keeps the target width");
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
