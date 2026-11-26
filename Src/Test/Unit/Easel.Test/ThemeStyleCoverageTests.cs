using System.Drawing;
using System.Drawing.Imaging;
using Easel.Visuals;
using Easel.Visuals.Controls;
using VisualRichTextBox = Easel.Visuals.Controls.RichTextBox;
using VisualScrollBar = Easel.Visuals.Controls.ScrollBar;
using VisualTextBox = Easel.Visuals.Controls.TextBox;
using VisualTreeView = Easel.Visuals.Controls.TreeView;

namespace Easel.Test;

/// <summary>Control style defaults follow the active fallback palette.</summary>
[STATestClass]
public sealed class ThemeStyleCoverageTests
{
    private static ThemePalette CustomPalette()
    {
        var palette = Theme.CreateDark();
        palette.Foreground = Color.FromArgb(0xFF, 0xFE, 0x01, 0x02);
        palette.MutedForeground = Color.FromArgb(0xFF, 0x13, 0x24, 0x35);
        palette.ScrollBarThumb = Color.FromArgb(0xFF, 0x11, 0x22, 0x33);
        palette.ScrollBarThumbHover = Color.FromArgb(0xFF, 0x44, 0x55, 0x66);
        palette.ShapeStroke = Color.FromArgb(0xFF, 0x77, 0x88, 0x99);
        palette.ShapeStrokeThickness = 3f;
        palette.ShadowColor = Color.FromArgb(0xFF, 0xAA, 0xBB, 0xCC);
        palette.ShadowOffset = new PointF(9f, 11f);
        palette.ShadowBlur = 7f;
        palette.ShadowOpacity = 0.8d;
        palette.FontFamily = "Consolas";
        palette.FontSize = 13f;
        palette.FontWeight = FontWeight.SemiBold;
        palette.TextVisualForeground = palette.Foreground;
        palette.TextVisualFontFamily = "Consolas";
        palette.TextVisualFontSize = 13f;
        palette.TextVisualFontWeight = FontWeight.SemiBold;
        return palette;
    }


    [TestMethod]
    public void BuiltInResourceDictionary_RoundTripsStyleRoles()
    {
        var resources = Theme.CreateResourceDictionary(ThemeMode.Dark);
        var palette = ThemePalette.FromResourceDictionary(resources);

        Assert.AreEqual("Segoe UI", palette.FontFamily);
        Assert.AreEqual(9.5f, palette.FontSize, 0.01f);
        Assert.AreEqual(FontWeight.Normal, palette.FontWeight);
        Assert.AreEqual(1.6f, palette.FocusThickness, 0.01f);
        Assert.AreEqual(1.4f, palette.FocusThicknessMedium, 0.01f);
        Assert.AreEqual(1f, palette.FocusThicknessThin, 0.01f);
        Assert.AreEqual(3f, palette.ScrollBarThumbRadius, 0.01f);
        Assert.AreEqual(new Easel.Controls.Thickness(1f), palette.ControlBorderThickness);
        Assert.AreEqual(1.5f, palette.ShapeStrokeThickness, 0.01f);
        Assert.AreEqual(4f, palette.ShadowOffset.X, 0.01f);
        Assert.AreEqual(6f, palette.ShadowOffset.Y, 0.01f);
        Assert.AreEqual(6f, palette.ShadowBlur, 0.01f);
        Assert.AreEqual(0.45d, palette.ShadowOpacity, 0.01d);
        Assert.AreEqual(Color.FromArgb(0x66, 0xA0, 0xA0, 0xA0).ToArgb(), palette.ScrollBarThumb.ToArgb());
        Assert.AreEqual(Color.FromArgb(0xCC, 0xE7, 0xE7, 0xE7).ToArgb(), palette.ScrollBarThumbHover.ToArgb());
        Assert.AreEqual(Color.White.ToArgb(), palette.TextVisualForeground.ToArgb());
        Assert.AreEqual("Segoe UI", palette.TextVisualFontFamily);
        Assert.AreEqual(9f, palette.TextVisualFontSize, 0.01f);
    }

    [TestMethod]
    public void ControlDefaults_FollowTheActivePalette()
    {
        var original = Theme.Current;
        try
        {
            var palette = CustomPalette();
            Theme.Current = palette;

            Assert.AreEqual("Consolas", VisualElement.DefaultFont.FontFamily.Name);
            Assert.AreEqual(13f, VisualElement.DefaultFont.Size, 0.01f);
            Assert.AreEqual(FontStyle.Bold, VisualElement.DefaultFont.Style & FontStyle.Bold);

            using var textBlock = new TextBlock();
            using var textBox = new VisualTextBox();
            using var tree = new VisualTreeView();
            using var rich = new VisualRichTextBox();
            foreach (var font in new[] { textBlock.Font, textBox.Font, tree.Font, rich.Font })
            {
                Assert.AreEqual("Consolas", font.FontFamily.Name);
                Assert.AreEqual(13f, font.Size, 0.01f);
            }

            var visual = new TextVisual();
            Assert.AreEqual(palette.Foreground.ToArgb(), visual.Foreground!.ApproximateColor.ToArgb());
            Assert.AreEqual("Consolas", visual.Font.FontFamily.Name);

            var curves = new CurveBatchVisual();
            Assert.AreEqual(palette.ShapeStroke.ToArgb(), curves.Stroke.Brush!.ApproximateColor.ToArgb());
            Assert.AreEqual(3f, curves.Stroke.Thickness, 0.01f);

            var shadow = new DropShadowEffect();
            Assert.AreEqual(palette.ShadowColor.ToArgb(), shadow.Color.ToArgb());
            Assert.AreEqual(9f, shadow.Offset.X, 0.01f);
            Assert.AreEqual(11f, shadow.Offset.Y, 0.01f);
            Assert.AreEqual(7f, shadow.Blur, 0.01f);
            Assert.AreEqual(0.8d, shadow.Opacity, 0.01d);
        }
        finally
        {
            Theme.Current = original;
        }
    }

    [TestMethod]
    public void DefaultItemsControl_PaintsNoContainerBackgroundOrBorder()
    {
        // The container must be content-driven: paint over a distinct backdrop and every pixel the list itself
        // would own (corners, edges, centre with no items) must still be the backdrop.
        using var host = new VisualHost { Size = new Size(120, 80), DpiScaleOverride = 1f };
        host.Root = new ItemsControl { Width = 120d, Height = 80d };

        using var bitmap = new Bitmap(120, 80, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Magenta);
            host.RenderTo(graphics, new Size(120, 80));
        }

        var probes = new[]
        {
            new Point(2, 2),
            new Point(118, 2),
            new Point(2, 78),
            new Point(118, 78),
            new Point(60, 2),
            new Point(2, 40),
        };
        foreach (var point in probes)
        {
            Assert.AreEqual(
                Color.Magenta.ToArgb(),
                bitmap.GetPixel(point.X, point.Y).ToArgb(),
                $"a default ItemsControl painted its own chrome at {point}");
        }
    }

    [TestMethod]
    public void ScrollBarThumb_FollowsTheActivePalette()
    {
        var original = Theme.Current;
        try
        {
            var palette = CustomPalette();
            Theme.Current = palette;

            using var host = new VisualHost { DpiScaleOverride = 1f, Size = new Size(12, 100) };
            host.Root = new VisualScrollBar
            {
                Width = 12d,
                Height = 100d,
                Minimum = 0d,
                Maximum = 100d,
                ViewportSize = 20d,
            };

            using var bitmap = new Bitmap(12, 100, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                host.RenderTo(graphics, new Size(12, 100));
            }

            Assert.AreEqual(
                palette.ScrollBarThumb.ToArgb(),
                bitmap.GetPixel(6, 14).ToArgb(),
                "the default thumb must use the palette role, not a literal");
        }
        finally
        {
            Theme.Current = original;
        }
    }
}
