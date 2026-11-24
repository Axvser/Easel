using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Easel.Test;

/// <summary>
/// Structural brush interpolation: same-kind brushes keep their shape, a solid lifts to a gradient, and a pair
/// with no shared structure is composed instead of flattened.
/// </summary>
[STATestClass]
public sealed class BrushInterpolationTests
{
    [TestMethod]
    public void SolidBrushes_LerpTheirColourAndOpacity()
    {
        var start = new SolidColorBrush(Color.FromArgb(200, 255, 0, 0)) { Opacity = 0.4d };
        var end = new SolidColorBrush(Color.FromArgb(40, 0, 0, 255)) { Opacity = 0.8d };

        var halfway = (SolidColorBrush)BrushInterpolation.Interpolate(start, end, 0.5d)!;

        Assert.AreEqual(Color.FromArgb(120, 128, 0, 128), halfway.Color);
        Assert.AreEqual(0.6d, halfway.Opacity, 1e-9d);
    }

    [TestMethod]
    public void SolidBrushes_SaturateWhenTheEasedTimeLeavesTheUnitInterval()
    {
        var start = new SolidColorBrush(Color.FromArgb(255, 240, 0, 0));
        var end = new SolidColorBrush(Color.FromArgb(255, 0, 0, 240));

        var beyond = (SolidColorBrush)BrushInterpolation.Interpolate(start, end, 1.5d)!;

        // 过冲不再继续外推：通道夹在 0..255，这正是缓动曲线需要的行为。
        Assert.AreEqual(Color.FromArgb(255, 0, 0, 255), beyond.Color);
    }

    [TestMethod]
    public void Gradients_KeepTheirStopsAndGeometry()
    {
        var start = new LinearGradientBrush { StartPoint = new PointF(0f, 0f), EndPoint = new PointF(0f, 1f) };
        start.GradientStops.Add(new GradientStop(Color.Black, 0d));
        start.GradientStops.Add(new GradientStop(Color.White, 1d));

        var end = new LinearGradientBrush { StartPoint = new PointF(1f, 0f), EndPoint = new PointF(0f, 1f) };
        end.GradientStops.Add(new GradientStop(Color.Red, 0d));
        end.GradientStops.Add(new GradientStop(Color.Blue, 1d));

        var halfway = (LinearGradientBrush)BrushInterpolation.Interpolate(start, end, 0.5d)!;

        Assert.AreEqual(new PointF(0.5f, 0f), halfway.StartPoint, "the gradient direction interpolates");
        Assert.AreEqual(2, halfway.GradientStops.Count, "shared offsets stay one stop each");
        Assert.AreEqual(Color.FromArgb(255, 128, 0, 0), halfway.GradientStops[0].Color);
        Assert.AreEqual(Color.FromArgb(255, 128, 128, 255), halfway.GradientStops[1].Color);
    }

    [TestMethod]
    public void SolidAgainstGradient_IsLiftedInsteadOfFlattened()
    {
        var solid = new SolidColorBrush(Color.Lime);
        var gradient = new LinearGradientBrush();
        gradient.GradientStops.Add(new GradientStop(Color.Red, 0d));
        gradient.GradientStops.Add(new GradientStop(Color.Blue, 1d));

        var start = (LinearGradientBrush)BrushInterpolation.Interpolate(solid, gradient, 0d)!;
        var halfway = (LinearGradientBrush)BrushInterpolation.Interpolate(solid, gradient, 0.5d)!;

        Assert.AreEqual(2, start.GradientStops.Count);
        Assert.AreEqual(Color.Lime.ToArgb(), start.GradientStops[0].Color.ToArgb(), "t = 0 still looks solid");
        Assert.AreEqual(Color.Lime.ToArgb(), start.GradientStops[1].Color.ToArgb());

        // 中点两端都还在：不是"绿→纯色"的拍平。
        Assert.IsTrue(
            halfway.GradientStops[0].Color.ToArgb() != halfway.GradientStops[1].Color.ToArgb(),
            "the halfway brush is still a gradient");
    }

    [TestMethod]
    public void DifferentKinds_FallBackToAComposedCrossFade()
    {
        var linear = new LinearGradientBrush();
        linear.GradientStops.Add(new GradientStop(Color.Red, 0d));
        linear.GradientStops.Add(new GradientStop(Color.Blue, 1d));

        var radial = new RadialGradientBrush();
        radial.GradientStops.Add(new GradientStop(Color.White, 0d));
        radial.GradientStops.Add(new GradientStop(Color.Black, 1d));

        var halfway = BrushInterpolation.Interpolate(linear, radial, 0.25d);

        var fade = halfway as CrossFadeBrush;
        Assert.IsNotNull(fade, "no structural relationship means composition");
        Assert.AreEqual(0.25d, fade!.Progress, 1e-9d);
        Assert.AreSame(linear, fade.From);
        Assert.AreSame(radial, fade.To);
    }

    [TestMethod]
    public void CrossFade_BlendsTheTwoLayersInsteadOfDilutingThem()
    {
        using var bitmap = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Black);
            var fade = new CrossFadeBrush(new SolidColorBrush(Color.Red), new SolidColorBrush(Color.Blue), 0.5d);
            fade.Paint(graphics, new RectangleF(0f, 0f, 8f, 8f));
        }

        var pixel = bitmap.GetPixel(4, 4);

        // 真正的交叉淡化：0.5 * 红 + 0.5 * 蓝。叠两次 over 会得到 (64,0,128) —— 背景渗进来。
        Assert.AreEqual(128, pixel.R, 2);
        Assert.AreEqual(0, pixel.G, 2);
        Assert.AreEqual(128, pixel.B, 2);
    }

    [TestMethod]
    public void CrossFade_BlendsWhereTheSecondBrushDoesNotPaint()
    {
        // 径向渐变只覆盖椭圆内部：椭圆外那半个淡化必须由起点层自己完成，
        // over 叠放会在交界处留下一整块未淡化的起点色（曾经的实际缺陷）。
        var from = new SolidColorBrush(Color.Red);
        var to = new RadialGradientBrush { Center = new PointF(0.5f, 0.5f), Radius = 0.1f };
        to.GradientStops.Add(new GradientStop(Color.Blue, 0d));
        to.GradientStops.Add(new GradientStop(Color.Blue, 1d));

        using var bitmap = new Bitmap(100, 60, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Black);
            new CrossFadeBrush(from, to, 0.5d).Paint(graphics, new RectangleF(0f, 0f, 100f, 60f));
        }

        var outside = bitmap.GetPixel(2, 2);
        Assert.AreEqual(128, outside.R, 3, "outside the radial coverage the start layer fades");
        Assert.AreEqual(0, outside.B, 3);

        var centre = bitmap.GetPixel(50, 30);
        Assert.AreEqual(128, centre.B, 6, "inside the coverage both sides contribute");
    }

    [TestMethod]
    public void CrossFade_WithOneSideMissing_FadesTheOtherInOrOut()
    {
        using var bitmap = new Bitmap(4, 4, PixelFormat.Format32bppArgb);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Black);
            new CrossFadeBrush(null, new SolidColorBrush(Color.White), 0.25d)
                .Paint(graphics, new RectangleF(0f, 0f, 4f, 4f));
        }

        Assert.AreEqual(64, bitmap.GetPixel(2, 2).R, 2, "a lone end brush fades in");

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Black);
            new CrossFadeBrush(new SolidColorBrush(Color.White), null, 0.25d)
                .Paint(graphics, new RectangleF(0f, 0f, 4f, 4f));
        }

        Assert.AreEqual(191, bitmap.GetPixel(2, 2).R, 2, "a lone start brush fades out");
    }

    [TestMethod]
    public void LayerPool_ReusesALayerAndHandsItBackTransparent()
    {
        var layer = BrushLayerPool.Rent(48, 32);
        using (var graphics = Graphics.FromImage(layer))
        {
            graphics.Clear(Color.Red);
        }

        BrushLayerPool.Return(layer);
        var again = BrushLayerPool.Rent(48, 32);

        Assert.AreSame(layer, again, "the same size comes back from the pool");
        Assert.AreEqual(0, again.GetPixel(1, 1).A, "a rented layer starts empty");
    }

    [TestMethod]
    public void Null_CountsAsTransparent()
    {
        var solid = new SolidColorBrush(Color.Red);

        Assert.IsNull(BrushInterpolation.Interpolate(null, null, 0.5d));

        var fading = (SolidColorBrush)BrushInterpolation.Interpolate(null, solid, 0.5d)!;
        Assert.AreEqual(128, fading.Color.A, 1);
    }
}
