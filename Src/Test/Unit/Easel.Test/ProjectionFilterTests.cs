using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Easel;
using Easel.Visuals;

namespace Easel.Test;

/// <summary>The footprint-driven resampling used by the projected 3D path.</summary>
[STATestClass]
public sealed class ProjectionFilterTests
{
    /// <summary>A one-pixel line minified four times averages to one flat gray instead of vanishing or saturating.</summary>
    [TestMethod]
    public void MinifiedOnePixelLine_ReadsAsFlatGray()
    {
        using var source = OnePixelLine(64, 64, 32);
        var projection = new Projection3D(
            new RectangleF(0f, 0f, 64f, 64f),
            new Scale3DTransform { ScaleX = 0.25d, ScaleY = 0.25d });
        using var target = Warp(source, projection, new RectangleF(0f, 0f, 64f, 64f));

        // 目标第 8 列覆盖源 x=32..36，白色 1px 竖线占四分之一；面积采样应给出 255/4 左右，而不是 0 或 255。
        var pixel = target.GetPixel(8, 8);
        Assert.IsGreaterThan(200, pixel.A, $"alpha {pixel}");
        Assert.IsGreaterThan(15, pixel.R, $"red {pixel}");
        Assert.IsLessThan(140, pixel.R, $"red {pixel}");
    }

    /// <summary>A magnified one-pixel line interpolates through intermediate values instead of nearest-neighbour blocks.</summary>
    [TestMethod]
    public void MagnifiedOnePixelLine_StaysSmooth()
    {
        using var source = OnePixelLine(16, 16, 8);
        var projection = new Projection3D(
            new RectangleF(0f, 0f, 16f, 16f),
            new Scale3DTransform { ScaleX = 4d, ScaleY = 4d });
        using var target = Warp(source, projection, new RectangleF(0f, 0f, 16f, 16f));

        var sawIntermediate = false;
        var previous = target.GetPixel(24, 32).R;
        for (var x = 25; x < 40; x++)
        {
            var current = target.GetPixel(x, 32).R;
            Assert.IsLessThan(220, Math.Abs(current - previous), $"step at x={x}");
            sawIntermediate |= current is > 20 and < 235;
            previous = current;
        }

        Assert.IsTrue(sawIntermediate, "no interpolated value across the magnified line");
    }

    /// <summary>Under perspective the compressed (far) side has the larger source footprint; the enlarged near side seeks magnification.</summary>
    [TestMethod]
    public void PerspectiveQuad_SeparatesNearAndFarFootprints()
    {
        // 先整体放大再绕 Y 倾斜，近侧才会在两个方向都真正 > 1:1；纯 45° 旋转的 cos 收缩
        // 会把整张图压成缩小，近侧只是“缩得少一点”。
        var transform = new Transform3DGroup();
        transform.Children.Add(new Scale3DTransform { ScaleX = 1.5d, ScaleY = 1.5d });
        transform.Children.Add(new Rotate3DTransform
        {
            AngleY = 30d,
            CenterX = 48d,
            CenterY = 48d,
            Depth = 60d,
        });
        var projection = new Projection3D(new RectangleF(0f, 0f, 64f, 64f), transform);

        // 先由目标空间的局部间距判断哪一侧是放大的近侧。
        var leftSpacing = Distance(projection.MapNormalized(0.05d, 0.5d), projection.MapNormalized(0.15d, 0.5d));
        var rightSpacing = Distance(projection.MapNormalized(0.85d, 0.5d), projection.MapNormalized(0.95d, 0.5d));
        var nearU = leftSpacing >= rightSpacing ? 0.1d : 0.9d;
        var farU = leftSpacing >= rightSpacing ? 0.9d : 0.1d;

        Assert.IsTrue(
            VisualProjectionFilter.TryGetFootprint(
                projection, 64d, 64d,
                projection.MapNormalized(nearU, 0.5d).X,
                projection.MapNormalized(nearU, 0.5d).Y,
                out var nearFootprintX,
                out _));
        Assert.IsTrue(
            VisualProjectionFilter.TryGetFootprint(
                projection, 64d, 64d,
                projection.MapNormalized(farU, 0.5d).X,
                projection.MapNormalized(farU, 0.5d).Y,
                out var farFootprintX,
                out _));

        // 近侧在屏幕上被放大，每目标像素覆盖的源像素更少（< 0.9 → bicubic）；
        // 远侧被压缩，覆盖的源像素更多（> 1.1 → 面积采样）。
        Assert.IsLessThan(0.9d, nearFootprintX, $"near footprint {nearFootprintX}");
        Assert.IsGreaterThan(1.1d, farFootprintX, $"far footprint {farFootprintX}");
    }

    private static double Distance(PointF a, PointF b)
        => Math.Sqrt((((double)a.X - b.X) * (a.X - b.X)) + (((double)a.Y - b.Y) * (a.Y - b.Y)));

    private static Bitmap OnePixelLine(int width, int height, int x)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);
        graphics.FillRectangle(Brushes.White, x, 0, 1, height);
        return bitmap;
    }

    // 与 Visual.DrawProjected 相同的脚手架：锁定位图、逐目标像素调用滤波器、写回字节。
    // 直接调 helper，测试不受 DrawProjected 是否已接线影响，也便于合并前先验证滤波器本身。
    private static Bitmap Warp(Bitmap source, Projection3D projection, RectangleF sourceRect)
    {
        var quad = projection.Corners;
        var left = (int)Math.Floor(quad.Min(point => point.X));
        var top = (int)Math.Floor(quad.Min(point => point.Y));
        var width = Math.Max(1, (int)Math.Ceiling(quad.Max(point => point.X)) - left);
        var height = Math.Max(1, (int)Math.Ceiling(quad.Max(point => point.Y)) - top);

        var target = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        var sourceData = source.LockBits(
            new Rectangle(0, 0, source.Width, source.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppPArgb);
        try
        {
            var stride = Math.Abs(sourceData.Stride);
            var sourceBytes = new byte[stride * source.Height];
            Marshal.Copy(sourceData.Scan0, sourceBytes, 0, sourceBytes.Length);
            var bottomUp = sourceData.Stride < 0;
            var destinationBytes = new byte[width * height * 4];
            for (var row = 0; row < height; row++)
            {
                for (var column = 0; column < width; column++)
                {
                    if (!VisualProjectionFilter.Sample(
                            sourceBytes, stride, source.Width, source.Height, bottomUp,
                            projection,
                            sourceRect.Left, sourceRect.Top, sourceRect.Width, sourceRect.Height,
                            left + column + 0.5d, top + row + 0.5d,
                            out var blue, out var green, out var red, out var alpha))
                    {
                        continue;
                    }

                    var offset = ((row * width) + column) * 4;
                    destinationBytes[offset] = ToByte(blue);
                    destinationBytes[offset + 1] = ToByte(green);
                    destinationBytes[offset + 2] = ToByte(red);
                    destinationBytes[offset + 3] = ToByte(alpha);
                }
            }

            var targetData = target.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppPArgb);
            try
            {
                Marshal.Copy(destinationBytes, 0, targetData.Scan0, destinationBytes.Length);
            }
            finally
            {
                target.UnlockBits(targetData);
            }
        }
        finally
        {
            source.UnlockBits(sourceData);
        }

        return target;
    }

    private static byte ToByte(double value)
        => value <= 0d ? (byte)0 : value >= 255d ? (byte)255 : (byte)Math.Round(value);
}
