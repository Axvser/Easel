using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Easel.Test;

/// <summary>
/// 3D transform interpolation: same-kind transforms move their own components, mixed kinds blend the composed
/// matrix.
/// </summary>
[STATestClass]
public sealed class Transform3DInterpolationTests
{
    [TestMethod]
    public void Rotations_LerpTheirAnglesAndKeepThePivot()
    {
        var start = new Rotate3DTransform { AngleY = 0d, CenterX = 30d, CenterY = 20d, Depth = 600d };
        var end = new Rotate3DTransform { AngleY = 90d, CenterX = 30d, CenterY = 20d, Depth = 800d };

        var halfway = (Rotate3DTransform)Transform3DInterpolation.Interpolate(start, end, 0.5d);

        Assert.AreEqual(45d, halfway.AngleY, 1e-9d);
        Assert.AreEqual(30d, halfway.CenterX, 1e-9d);
        Assert.AreEqual(700d, halfway.Depth, 1e-9d);

        // 走的是角度而不是矩阵：矩阵插值在两个旋转之间会缩水，角度插值仍是一条正交旋转。
        var expected = Matrix4x4.CreateRotationY(45d);
        Assert.AreEqual(expected.M11, halfway.Value.M11, 1e-12d);
        Assert.AreEqual(expected.M13, halfway.Value.M13, 1e-12d);
    }

    [TestMethod]
    public void SameKindAtTheEndpoints_ReproducesTheEndpoint()
    {
        var end = new Rotate3DTransform { AngleX = 12d, AngleZ = -40d, Depth = 250d };

        var atEnd = (Rotate3DTransform)Transform3DInterpolation.Interpolate(
            new Rotate3DTransform { AngleX = 0d },
            end,
            1d);

        Assert.AreEqual(end.AngleX, atEnd.AngleX, 1e-9d);
        Assert.AreEqual(end.AngleZ, atEnd.AngleZ, 1e-9d);
        Assert.AreEqual(end.Depth, atEnd.Depth, 1e-9d);
        Assert.AreEqual(end.Value.M12, atEnd.Value.M12, 1e-12d);
    }

    [TestMethod]
    public void DifferentKinds_BlendTheComposedMatrix()
    {
        var rotate = new Rotate3DTransform { AngleX = 10d };
        var translate = new Translate3DTransform { X = 20d, Y = 10d };

        var halfway = Transform3DInterpolation.Interpolate(rotate, translate, 0.5d);

        var matrix = halfway as MatrixTransform3D;
        Assert.IsNotNull(matrix, "mixed kinds come back as a matrix transform");
        Assert.AreEqual(10d, matrix!.Value.M41, 1e-9d);
        Assert.AreEqual(5d, matrix.Value.M42, 1e-9d);
    }

    [TestMethod]
    public void Groups_InterpolateChildByChild()
    {
        var start = new Transform3DGroup();
        start.Children.Add(new Translate3DTransform { X = 0d });
        start.Children.Add(new Rotate3DTransform { AngleZ = 0d });

        var end = new Transform3DGroup();
        end.Children.Add(new Translate3DTransform { X = 40d });
        end.Children.Add(new Rotate3DTransform { AngleZ = 90d });

        var quarter = (Transform3DGroup)Transform3DInterpolation.Interpolate(start, end, 0.25d);

        Assert.AreEqual(2, quarter.Children.Count);
        Assert.AreEqual(10d, ((Translate3DTransform)quarter.Children[0]).X, 1e-9d);
        Assert.AreEqual(22.5d, ((Rotate3DTransform)quarter.Children[1]).AngleZ, 1e-9d);
    }

    [TestMethod]
    public void Null_CountsAsIdentity()
    {
        var translate = new Translate3DTransform { X = 20d };

        // null 是恒等（MatrixTransform3D），与具体类型不同类，因此走矩阵回退。
        var halfway = Transform3DInterpolation.Interpolate(null, translate, 0.5d);

        Assert.IsInstanceOfType<MatrixTransform3D>(halfway);
        Assert.AreEqual(10d, halfway.Value.M41, 1e-9d);
    }
}
