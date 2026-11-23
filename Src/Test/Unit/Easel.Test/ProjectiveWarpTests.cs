using System.Drawing;
using System.Drawing.Imaging;
using Easel;
using Easel.Visuals;

namespace Easel.Test;

/// <summary>The perspective-correct homography warp used by <see cref="Visual.Transform3D"/>.</summary>
[STATestClass]
public sealed class ProjectiveWarpTests
{
    /// <summary>Every interior sample of a solid source is painted at its projected point.</summary>
    [TestMethod]
    public void SolidSource_FillsTheProjectedQuad()
    {
        using var source = Solid(40, 40, Color.Red);
        var projection = StrongProjection(40f, 40f);
        using var target = new Bitmap(240, 200, PixelFormat.Format32bppPArgb);

        Draw(target, source, projection, new RectangleF(0f, 0f, 40f, 40f));

        foreach (var (u, v) in InteriorSamples())
        {
            var pixel = Sample(target, projection, u, v);
            Assert.IsGreaterThan(200, pixel.A, $"alpha at {u},{v}");
            Assert.IsGreaterThan(200, pixel.R, $"red at {u},{v}");
            Assert.IsLessThan(50, pixel.G, $"green at {u},{v}");
            Assert.IsLessThan(50, pixel.B, $"blue at {u},{v}");
        }
    }

    /// <summary>The source is sampled at the point the inverse homography maps back to, not at an affine guess.</summary>
    [TestMethod]
    public void Quadrants_KeepTheSourceMappingUnderPerspective()
    {
        using var source = Quadrants(64, 64);
        var projection = StrongProjection(64f, 64f);
        using var target = new Bitmap(300, 240, PixelFormat.Format32bppPArgb);

        Draw(target, source, projection, new RectangleF(0f, 0f, 64f, 64f));

        AssertColor(Color.Red, Sample(target, projection, 0.25d, 0.25d));
        AssertColor(Color.Lime, Sample(target, projection, 0.75d, 0.25d));
        AssertColor(Color.Blue, Sample(target, projection, 0.25d, 0.75d));
        AssertColor(Color.Yellow, Sample(target, projection, 0.75d, 0.75d));
    }

    /// <summary>A vertical source boundary stays on the projected u = 0.5 line.</summary>
    [TestMethod]
    public void HalfSplit_FollowsTheProjectedCentreLine()
    {
        using var source = new Bitmap(64, 64, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(source))
        {
            graphics.FillRectangle(Brushes.Red, 0, 0, 32, 64);
            graphics.FillRectangle(Brushes.Blue, 32, 0, 32, 64);
        }

        var projection = StrongProjection(64f, 64f);
        using var target = new Bitmap(300, 240, PixelFormat.Format32bppPArgb);
        Draw(target, source, projection, new RectangleF(0f, 0f, 64f, 64f));

        foreach (var v in new[] { 0.2d, 0.5d, 0.8d })
        {
            Assert.IsGreaterThan(180, Sample(target, projection, 0.35d, v).R, $"left of the centre line at v={v}");
            Assert.IsGreaterThan(180, Sample(target, projection, 0.65d, v).B, $"right of the centre line at v={v}");
        }
    }

    /// <summary>A 90-degree in-plane rotation keeps every destination pixel on its inverse-mapped source pixel.</summary>
    [TestMethod]
    public void RotateZ90_IsExact()
    {
        using var source = Quadrants(64, 64);
        var projection = new Projection3D(
            new RectangleF(0f, 0f, 64f, 64f),
            new Rotate3DTransform
            {
                AngleZ = 90d,
                CenterX = 32d,
                CenterY = 32d,
                Depth = 0d,
            });
        using var target = new Bitmap(180, 180, PixelFormat.Format32bppPArgb);

        Draw(target, source, projection, new RectangleF(0f, 0f, 64f, 64f));

        // 90° in place: the destination square maps each quadrant one step around the centre.
        AssertDestinationColor(target, projection, 20f, 20f);
        AssertDestinationColor(target, projection, 44f, 20f);
        AssertDestinationColor(target, projection, 44f, 44f);
        AssertDestinationColor(target, projection, 20f, 44f);
    }

    /// <summary>A degenerate projection falls back to drawing the source unwarped.</summary>
    [TestMethod]
    public void DegenerateProjection_FallsBackToTheSourceRect()
    {
        using var source = Solid(20, 10, Color.Red);
        var projection = new Projection3D(
            new RectangleF(0f, 0f, 0f, 10f),
            new Rotate3DTransform { AngleY = 45d, Depth = 100d });
        using var target = new Bitmap(40, 30, PixelFormat.Format32bppPArgb);

        Draw(target, source, projection, new RectangleF(4f, 6f, 20f, 10f));

        var pixel = target.GetPixel(12, 10);
        Assert.IsGreaterThan(200, pixel.A);
        Assert.IsGreaterThan(200, pixel.R);
    }

    /// <summary>A projected area over the exact-warp budget uses the affine fallback instead of allocating it.</summary>
    [TestMethod]
    public void OversizedProjection_UsesTheFallback()
    {
        using var source = Solid(2, 2, Color.Red);
        var projection = new Projection3D(
            new RectangleF(0f, 0f, 2f, 2f),
            new MatrixTransform3D
            {
                M11 = 2050d,
                M22 = 2050d,
            });
        using var target = new Bitmap(4200, 4200, PixelFormat.Format32bppPArgb);

        Draw(target, source, projection, new RectangleF(0f, 0f, 2f, 2f));

        var pixel = target.GetPixel(2100, 2100);
        Assert.IsGreaterThan(200, pixel.A);
        Assert.IsGreaterThan(200, pixel.R);
    }

    private static Projection3D StrongProjection(float width, float height)
        => new(
            new RectangleF(0f, 0f, width, height),
            new Rotate3DTransform
            {
                AngleX = 22d,
                AngleY = 48d,
                CenterX = width / 2f,
                CenterY = height / 2f,
                Depth = 95d,
            });

    private static IEnumerable<(double U, double V)> InteriorSamples()
    {
        foreach (var u in new[] { 0.2d, 0.4d, 0.6d, 0.8d })
        {
            foreach (var v in new[] { 0.2d, 0.4d, 0.6d, 0.8d })
            {
                yield return (u, v);
            }
        }
    }

    private static void Draw(Bitmap target, Bitmap source, Projection3D projection, RectangleF sourceRect)
    {
        using var graphics = Graphics.FromImage(target);
        graphics.Clear(Color.Transparent);
        Visual.DrawProjected(graphics, source, projection, sourceRect, 1d);
    }

    private static Color Sample(Bitmap target, Projection3D projection, double u, double v)
    {
        var point = projection.MapNormalized(u, v);
        var x = XamlMath.Clamp((int)Math.Round(point.X), 0, target.Width - 1);
        var y = XamlMath.Clamp((int)Math.Round(point.Y), 0, target.Height - 1);
        return target.GetPixel(x, y);
    }

    // Destination (x,y) maps back through the projection; the source quadrant under that point is the expected colour.
    private static void AssertDestinationColor(Bitmap target, Projection3D projection, float x, float y)
    {
        var source = projection.Unmap(new PointF(x, y));
        var expected = source.X < 32f
            ? (source.Y < 32f ? Color.Red : Color.Blue)
            : (source.Y < 32f ? Color.Lime : Color.Yellow);
        var px = XamlMath.Clamp((int)Math.Round(x), 0, target.Width - 1);
        var py = XamlMath.Clamp((int)Math.Round(y), 0, target.Height - 1);
        AssertColor(expected, target.GetPixel(px, py));
    }

    private static void AssertColor(Color expected, Color actual)
    {
        var tolerance = 28;
        Assert.IsLessThan(tolerance, Math.Abs(expected.R - actual.R), $"red {actual} vs {expected}");
        Assert.IsLessThan(tolerance, Math.Abs(expected.G - actual.G), $"green {actual} vs {expected}");
        Assert.IsLessThan(tolerance, Math.Abs(expected.B - actual.B), $"blue {actual} vs {expected}");
        Assert.IsGreaterThan(200, actual.A, $"alpha {actual}");
    }

    private static Bitmap Solid(int width, int height, Color color)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }

    private static Bitmap Quadrants(int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        var halfWidth = width / 2;
        var halfHeight = height / 2;
        graphics.FillRectangle(Brushes.Red, 0, 0, halfWidth, halfHeight);
        graphics.FillRectangle(Brushes.Lime, halfWidth, 0, width - halfWidth, halfHeight);
        graphics.FillRectangle(Brushes.Blue, 0, halfHeight, halfWidth, height - halfHeight);
        graphics.FillRectangle(Brushes.Yellow, halfWidth, halfHeight, width - halfWidth, height - halfHeight);
        return bitmap;
    }
}
