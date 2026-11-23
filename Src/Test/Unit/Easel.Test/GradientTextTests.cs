using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>Text painting keeps a gradient brush instead of collapsing it to its first stop.</summary>
[TestClass]
public sealed class GradientTextTests
{
    private static LinearGradientBrush RedToBlue()
    {
        var brush = new LinearGradientBrush { StartPoint = new PointF(0f, 0f), EndPoint = new PointF(1f, 0f) };
        brush.GradientStops.Add(new GradientStop(Color.Red, 0d));
        brush.GradientStops.Add(new GradientStop(Color.Blue, 1d));
        return brush;
    }

    private static (int Red, int Blue) CountHues(Bitmap bitmap)
    {
        var red = 0;
        var blue = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A < 100)
                {
                    continue;
                }

                if (pixel.R > pixel.B + 36)
                {
                    red++;
                }
                else if (pixel.B > pixel.R + 36)
                {
                    blue++;
                }
            }
        }

        return (red, blue);
    }

    /// <summary>A TextBlock paints its glyphs with the gradient supplied to the Foreground dependency property.</summary>
    [TestMethod]
    public void TextBlock_ForegroundGradient_PaintsBothEnds()
    {
        using var host = new VisualHost();
        var text = new TextBlock
        {
            Text = "GRADIENT",
            Width = 240f,
            Height = 64f,
            Font = new Font("Segoe UI", 28f, FontStyle.Bold),
            VerticalAlignment = Easel.Controls.VerticalAlignment.Center,
        };
        text.SetValue(TextBlock.ForegroundProperty, RedToBlue());
        host.Root = text;

        using var bitmap = new Bitmap(240, 64, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            host.RenderTo(graphics, new Size(240, 64));
        }

        var (red, blue) = CountHues(bitmap);
        Assert.IsTrue(red > 0, "no red-dominant glyph pixels");
        Assert.IsTrue(blue > 0, "no blue-dominant glyph pixels");
    }

    /// <summary>A hosted TextVisual keeps the gradient instead of reducing it to ApproximateColor.</summary>
    [TestMethod]
    public void TextVisual_ForegroundGradient_PaintsBothEnds()
    {
        using var host = new VisualHost();
        var layer = new ContainerVisual();
        layer.Add(new TextVisual
        {
            Bounds = new RectangleF(0f, 0f, 240f, 64f),
            Text = "GRADIENT",
            Font = new Font("Segoe UI", 28f, FontStyle.Bold),
            Foreground = RedToBlue(),
            Wrap = false,
        });
        host.Root = new VisualPresenter { Width = 240f, Height = 64f, Content = layer };

        using var bitmap = new Bitmap(240, 64, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            host.RenderTo(graphics, new Size(240, 64));
        }

        var (red, blue) = CountHues(bitmap);
        Assert.IsTrue(red > 0, "no red-dominant glyph pixels");
        Assert.IsTrue(blue > 0, "no blue-dominant glyph pixels");
    }
}
