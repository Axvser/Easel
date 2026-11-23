using System.Drawing;
using System.Drawing.Imaging;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>Composition through an opacity mask and the mask compositor itself.</summary>
[STATestClass]
public sealed class OpacityMaskTests
{
    [TestMethod]
    public void GradientMask_FadesTheTrailingEdge()
    {
        using var surface = new VisualHost { Size = new Size(120, 40) };
        using var border = new VC.Border
        {
            Width = 100f,
            Height = 20f,
            Background = Color.White,
            BorderThickness = default,
            HorizontalAlignment = Easel.Controls.HorizontalAlignment.Left,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Top,
            OpacityMask = HorizontalFade(),
        };
        surface.Root = border;

        using var bitmap = Render(surface, 120, 40);
        var left = bitmap.GetPixel(2, 10);
        var right = bitmap.GetPixel(97, 10);

        Assert.IsGreaterThan(200, left.A, "the opaque end keeps the subtree");
        Assert.IsLessThan(40, right.A, "the transparent end removes it");
    }

    [TestMethod]
    public void OpaqueMask_LeavesTheCompositionUnchanged()
    {
        using var without = RenderBorder(null);
        using var withMask = RenderBorder(new SolidColorBrush(Color.White));

        Assert.AreEqual(
            without.GetPixel(50, 10).ToArgb(),
            withMask.GetPixel(50, 10).ToArgb(),
            "a fully opaque mask is a no-op");
    }

    [TestMethod]
    public void Compositor_MultipliesEveryPremultipliedChannel()
    {
        using var source = new Bitmap(2, 1, PixelFormat.Format32bppPArgb);
        source.SetPixel(0, 0, Color.FromArgb(255, 255, 255, 255));
        source.SetPixel(1, 0, Color.FromArgb(255, 255, 255, 255));

        using var mask = new Bitmap(2, 1, PixelFormat.Format32bppPArgb);
        mask.SetPixel(0, 0, Color.FromArgb(255, 255, 255, 255));
        mask.SetPixel(1, 0, Color.FromArgb(0, 255, 255, 255));

        OpacityMaskCompositor.Apply(source, mask);

        Assert.AreEqual(255, source.GetPixel(0, 0).A);
        Assert.AreEqual(0, source.GetPixel(1, 0).A);
    }

    /// <summary>Luminance mode uses the mask's premultiplied brightness, not its alpha.</summary>
    [TestMethod]
    public void LuminanceMask_ReadsColourInsteadOfAlpha()
    {
        using var source = WhiteRow();
        using var mask = new Bitmap(4, 1, PixelFormat.Format32bppPArgb);
        mask.SetPixel(0, 0, Color.White);
        mask.SetPixel(1, 0, Color.FromArgb(255, 192, 192, 192));
        mask.SetPixel(2, 0, Color.FromArgb(255, 64, 64, 64));
        mask.SetPixel(3, 0, Color.Black);

        OpacityMaskCompositor.Apply(source, mask, OpacityMaskMode.Luminance);

        Assert.AreEqual(255, source.GetPixel(0, 0).A);
        Assert.AreEqual(192, source.GetPixel(1, 0).A);
        Assert.AreEqual(64, source.GetPixel(2, 0).A);
        Assert.AreEqual(0, source.GetPixel(3, 0).A);
    }

    /// <summary>The same opaque white-to-black mask is a no-op in the default alpha mode.</summary>
    [TestMethod]
    public void SameMask_IsOpaqueInAlphaMode()
    {
        using var source = WhiteRow();
        using var mask = new Bitmap(4, 1, PixelFormat.Format32bppPArgb);
        mask.SetPixel(0, 0, Color.White);
        mask.SetPixel(1, 0, Color.FromArgb(255, 192, 192, 192));
        mask.SetPixel(2, 0, Color.FromArgb(255, 64, 64, 64));
        mask.SetPixel(3, 0, Color.Black);

        OpacityMaskCompositor.Apply(source, mask, OpacityMaskMode.Alpha);

        for (var x = 0; x < 4; x++)
        {
            Assert.AreEqual(255, source.GetPixel(x, 0).A, $"alpha at {x}");
        }
    }

    /// <summary>An opaque black-to-white gradient fades the composited subtree through luminance.</summary>
    [TestMethod]
    public void LuminanceMode_FadesAnOpaqueColourGradient()
    {
        using var surface = new VisualHost { Size = new Size(120, 40) };
        using var border = new VC.Border
        {
            Width = 100f,
            Height = 20f,
            Background = Color.White,
            BorderThickness = default,
            HorizontalAlignment = Easel.Controls.HorizontalAlignment.Left,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Top,
            OpacityMask = OpaqueBlackToWhite(),
            OpacityMaskMode = OpacityMaskMode.Luminance,
        };
        surface.Root = border;

        using var bitmap = Render(surface, 120, 40);

        Assert.IsLessThan(40, bitmap.GetPixel(2, 10).A, "the black end hides the subtree");
        Assert.IsGreaterThan(200, bitmap.GetPixel(97, 10).A, "the white end keeps the subtree");
    }

    /// <summary>The compositor falls back to alpha when asked for the default mode.</summary>
    [TestMethod]
    public void Compositor_DefaultModeIsAlpha()
    {
        using var source = new Bitmap(2, 1, PixelFormat.Format32bppPArgb);
        source.SetPixel(0, 0, Color.White);
        source.SetPixel(1, 0, Color.White);
        using var mask = new Bitmap(2, 1, PixelFormat.Format32bppPArgb);
        mask.SetPixel(0, 0, Color.Black);
        mask.SetPixel(1, 0, Color.FromArgb(0, 255, 255, 255));

        OpacityMaskCompositor.Apply(source, mask);

        Assert.AreEqual(255, source.GetPixel(0, 0).A, "opaque black still covers in alpha mode");
        Assert.AreEqual(0, source.GetPixel(1, 0).A);
    }

    private static Bitmap WhiteRow()
    {
        var bitmap = new Bitmap(4, 1, PixelFormat.Format32bppPArgb);
        for (var x = 0; x < 4; x++)
        {
            bitmap.SetPixel(x, 0, Color.White);
        }

        return bitmap;
    }

    private static LinearGradientBrush OpaqueBlackToWhite()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new PointF(0f, 0f),
            EndPoint = new PointF(1f, 0f),
        };
        brush.GradientStops.Add(new GradientStop(Color.Black, 0d));
        brush.GradientStops.Add(new GradientStop(Color.White, 1d));
        return brush;
    }

    private static Bitmap RenderBorder(Brush? mask)
    {
        using var surface = new VisualHost { Size = new Size(120, 40) };
        using var border = new VC.Border
        {
            Width = 100f,
            Height = 20f,
            Background = Color.White,
            BorderThickness = default,
            HorizontalAlignment = Easel.Controls.HorizontalAlignment.Left,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Top,
            OpacityMask = mask,
        };
        surface.Root = border;
        return Render(surface, 120, 40);
    }

    private static LinearGradientBrush HorizontalFade()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new PointF(0f, 0f),
            EndPoint = new PointF(1f, 0f),
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 255, 255), 0d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1d));
        return brush;
    }

    private static Bitmap Render(VisualHost surface, int width, int height)
    {
        var bitmap = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            surface.RenderTo(graphics, new Size(width, height));
        }

        return bitmap;
    }
}
