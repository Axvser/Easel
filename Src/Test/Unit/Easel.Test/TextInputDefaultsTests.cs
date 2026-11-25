using System.Drawing;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>Built-in default templates for the text input controls.</summary>
[STATestClass]
public sealed class TextInputDefaultsTests
{
    [TestMethod]
    public void TextBoxDefaultTemplate_ShowsTheFocusRingOnFocus()
    {
        using var surface = new VisualHost { Size = new Size(240, 80), DpiScaleOverride = 1f };
        var box = new VC.TextBox { Width = 200, Height = 36 };
        surface.Root = box;
        Layout(surface, 240, 80);

        var ring = box.GetTemplateChild<VC.Border>("FocusRing");
        Assert.IsNotNull(ring, "the default template carries the focus ring");
        Assert.AreEqual(0d, ring.Opacity, 0.001d, "an unfocused field hides the ring");

        surface.FocusElement(box);

        Assert.AreEqual(1d, ring.Opacity, 0.001d, "focus drives the ring through the template trigger");
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
