using System.Drawing;
using Easel.Test.Fixtures;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>3D transform maths, projection, rendering and hit testing.</summary>
[STATestClass]
public sealed class Transform3DTests
{
    [TestMethod]
    public void RotationY90_MapsPositiveXToNegativeDepth()
    {
        var matrix = Matrix4x4.CreateRotationY(90d);
        matrix.Transform(1d, 0d, 0d, 1d, out var x, out var y, out var z, out var w);

        Assert.AreEqual(0d, x, 1e-9d);
        Assert.AreEqual(0d, y, 1e-9d);
        Assert.AreEqual(-1d, z, 1e-9d);
        Assert.AreEqual(1d, w, 1e-9d);
    }

    [TestMethod]
    public void TransformGroup_AppliesChildrenInDeclarationOrder()
    {
        var group = new Transform3DGroup();
        group.Children.Add(new Translate3DTransform { X = 10d });
        group.Children.Add(new Scale3DTransform { ScaleX = 2d, ScaleY = 2d, ScaleZ = 1d });

        group.Value.Transform(1d, 0d, 0d, 1d, out var x, out var y, out _, out _);

        Assert.AreEqual(22d, x, 1e-9d, "translate first, then scale");
        Assert.AreEqual(0d, y, 1e-9d);
    }

    [TestMethod]
    public void Rotate3D_UsesTheConfiguredCentre()
    {
        var rotation = new Rotate3DTransform
        {
            AngleZ = 180d,
            CenterX = 50d,
            CenterY = 20d,
        };

        rotation.Value.Transform(50d, 30d, 0d, 1d, out var x, out var y, out _, out _);

        Assert.AreEqual(50d, x, 1e-9d);
        Assert.AreEqual(10d, y, 1e-9d, "the point flips around the centre");
    }

    [TestMethod]
    public void Projection_RoundTripsLocalPoints()
    {
        var projection = new Projection3D(
            new RectangleF(0f, 0f, 120f, 80f),
            new Rotate3DTransform
            {
                AngleX = 25d,
                AngleY = 40d,
                CenterX = 60d,
                CenterY = 40d,
                Depth = 400d,
            });

        Assert.IsFalse(projection.IsDegenerate);

        PointF[] samples =
        [
            new(0f, 0f),
            new(120f, 80f),
            new(60f, 40f),
            new(13f, 57f),
        ];

        foreach (var sample in samples)
        {
            var back = projection.Unmap(projection.Map(sample));
            Assert.AreEqual(sample.X, back.X, 0.02f);
            Assert.AreEqual(sample.Y, back.Y, 0.02f);
        }
    }

    [TestMethod]
    public void Projection_WithNoAreaIsDegenerate()
    {
        var projection = new Projection3D(
            new RectangleF(0f, 0f, 0f, 80f),
            new Rotate3DTransform { AngleY = 45d, Depth = 400d });

        Assert.IsTrue(projection.IsDegenerate);
    }

    [TestMethod]
    public void RotatedBorder_RendersANearAndFarEdge()
    {
        using var surface = new VisualHost { Size = new Size(200, 160) };
        using var border = new VC.Border
        {
            Width = 120f,
            Height = 80f,
            Background = Color.Red,
            BorderThickness = default,
            HorizontalAlignment = Easel.Controls.HorizontalAlignment.Left,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Top,
            Transform3D = new Rotate3DTransform
            {
                AngleY = 60d,
                CenterX = 60d,
                CenterY = 40d,
                Depth = 400d,
            },
        };
        surface.Root = border;

        using var bitmap = Render(surface, 200, 160);
        var leftHeight = CountRed(bitmap, 40);
        var rightHeight = CountRed(bitmap, 75);

        Assert.IsGreaterThan(0, leftHeight, "the projected quad paints");
        Assert.IsGreaterThan(0, rightHeight, "both halves paint");
        Assert.IsGreaterThan(rightHeight, leftHeight, "the edge turned toward the camera is taller");
    }

    [TestMethod]
    public void RotatedBorder_HitsOnlyInsideTheProjectedQuad()
    {
        using var surface = new VisualHost { Size = new Size(200, 160) };
        using var border = new VC.Border
        {
            Width = 120f,
            Height = 80f,
            Background = Color.Red,
            BorderThickness = default,
            HorizontalAlignment = Easel.Controls.HorizontalAlignment.Left,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Top,
            Transform3D = new Rotate3DTransform
            {
                AngleY = 60d,
                CenterX = 60d,
                CenterY = 40d,
                Depth = 400d,
            },
        };
        surface.Root = border;
        _ = Render(surface, 200, 160);

        Assert.IsTrue(border.HitTest(new PointF(60f, 40f)), "the centre is inside");
        Assert.IsFalse(border.HitTest(new PointF(98f, 2f)), "the cut-away top-right corner is outside");

        var projected = border.AbsoluteBounds;
        Assert.IsGreaterThan(40f, projected.Width);
        Assert.IsLessThan(50f, projected.Width);
    }

    [TestMethod]
    public void Xaml_DeclaresA3DTransformAndAGradientBrush()
    {
        using var view = new Transform3DView();

        Assert.IsTrue(view.Transform3D is Rotate3DTransform);
        var rotation = (Rotate3DTransform)view.Transform3D!;
        Assert.AreEqual(35d, rotation.AngleY, 1e-9d);
        Assert.AreEqual(400d, rotation.Depth, 1e-9d);

        Assert.IsTrue(view.Background is LinearGradientBrush);
        var gradient = (LinearGradientBrush)view.Background!;
        Assert.AreEqual(2, gradient.GradientStops.Count);
        Assert.AreEqual(Color.Red.ToArgb(), gradient.GradientStops[0].Color.ToArgb());
        Assert.AreEqual(Color.Blue.ToArgb(), gradient.GradientStops[1].Color.ToArgb());
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

    private static int CountRed(Bitmap bitmap, int x)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.A > 200 && pixel.R > 200 && pixel.G < 80 && pixel.B < 80)
            {
                count++;
            }
        }

        return count;
    }
}
