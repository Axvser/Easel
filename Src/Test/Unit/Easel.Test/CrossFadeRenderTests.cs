using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>The composed cross-fade paints inside the element's rounded clip, at a fractional DPI too.</summary>
[STATestClass]
public sealed class CrossFadeRenderTests
{
    [TestMethod]
    public void CrossFade_InsideARoundedBorder_KeepsTheClipAtFractionalDpi()
    {
        var linear = new LinearGradientBrush();
        linear.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 210, 255), 0d));
        linear.GradientStops.Add(new GradientStop(Color.FromArgb(255, 123, 47, 247), 1d));

        var radial = new RadialGradientBrush { Center = new PointF(0.5f, 0.5f), Radius = 1.1f };
        radial.GradientStops.Add(new GradientStop(Color.FromArgb(255, 123, 47, 247), 0d));
        radial.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 210, 255), 1d));

        var border = new VC.Border
        {
            Width = 180,
            Height = 110,
            CornerRadius = 12,
            Background = new CrossFadeBrush(linear, radial, 0.5d),
        };

        using var host = new Easel.Visuals.VisualHost { Size = new Size(360, 225), DpiScaleOverride = 1.5f };
        border.Margin = new Easel.Controls.Thickness(30f, 30f, 0f, 0f);
        host.Root = border;

        using var bitmap = new Bitmap(360, 225);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.FromArgb(255, 20, 20, 20));
            host.RenderPhysical(graphics, new Rectangle(0, 0, 360, 225));
        }

        // 圆角之外仍是背景：图层按目标设备缩放栅格化，裁剪也按同一坐标系。
        Assert.AreEqual(20, bitmap.GetPixel(46, 46).R, 3, "top-left corner stays outside the rounded fill");
        Assert.AreEqual(20, bitmap.GetPixel(314, 46).R, 3, "top-right corner stays outside the rounded fill");

        // 中心是两端的混合，不是任何单独一支。
        var centre = bitmap.GetPixel(180, 112);
        Assert.IsTrue(centre.B > 40 && centre.R > 40, "the centre blends both brushes");
    }
}
