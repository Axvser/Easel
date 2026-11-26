using System.Drawing;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>Theme switching mutates shared brushes instead of rebuilding the resource chain.</summary>
[STATestClass]
public sealed class ThemeSwitchRegressionTests
{
    [TestMethod]
    public void ThemeSwitchUpdatesTheButtonChromeBrush()
    {
        var previous = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;
            using var surface = new VisualHost { Size = new Size(240, 80), DpiScaleOverride = 1f };
            var button = new VC.Button { Width = 140, Height = 40, Text = "go" };
            surface.Root = button;
            Layout(surface, 240, 80);

            var chrome = button.GetTemplateChild<VC.Border>("Chrome");
            Assert.IsNotNull(chrome);
            var before = chrome.Background!.ApproximateColor;

            Theme.Mode = ThemeMode.Light;

            Assert.AreNotEqual(before.ToArgb(), chrome.Background!.ApproximateColor.ToArgb(),
                "the chrome brush follows the switched palette");
        }
        finally
        {
            Theme.Mode = previous;
        }
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
