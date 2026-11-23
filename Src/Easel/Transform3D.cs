using System.Drawing;

namespace Easel;

/// <summary>A 4x4 matrix in row-vector order: a point maps as <c>v' = v · M</c>.</summary>
/// <remarks>
/// <para>
/// The layout follows <c>System.Numerics.Matrix4x4</c>: <c>M11</c> is the first row and column, and the
/// translation sits in <c>M41</c>, <c>M42</c> and <c>M43</c>. The runtime defines the type itself so the
/// <c>netframework4.6.1</c> target does not need an extra package.
/// </para>
/// <para>
/// Composition is <c>v · (A · B) = (v · A) · B</c>: <see cref="Multiply"/> applies <c>a</c> first.
/// </para>
/// </remarks>
public struct Matrix4x4
{
    /// <summary>The first row, first column element.</summary>
    public double M11;
    /// <summary>The first row, second column element.</summary>
    public double M12;
    /// <summary>The first row, third column element.</summary>
    public double M13;
    /// <summary>The first row, fourth column element.</summary>
    public double M14;
    /// <summary>The second row, first column element.</summary>
    public double M21;
    /// <summary>The second row, second column element.</summary>
    public double M22;
    /// <summary>The second row, third column element.</summary>
    public double M23;
    /// <summary>The second row, fourth column element.</summary>
    public double M24;
    /// <summary>The third row, first column element.</summary>
    public double M31;
    /// <summary>The third row, second column element.</summary>
    public double M32;
    /// <summary>The third row, third column element.</summary>
    public double M33;
    /// <summary>The third row, fourth column element.</summary>
    public double M34;
    /// <summary>The fourth row, first column element; the horizontal translation.</summary>
    public double M41;
    /// <summary>The fourth row, second column element; the vertical translation.</summary>
    public double M42;
    /// <summary>The fourth row, third column element; the depth translation.</summary>
    public double M43;
    /// <summary>The fourth row, fourth column element.</summary>
    public double M44;

    /// <summary>The identity matrix.</summary>
    public static Matrix4x4 Identity => new()
    {
        M11 = 1d,
        M22 = 1d,
        M33 = 1d,
        M44 = 1d,
    };

    /// <summary>Whether every element matches the identity within a small tolerance.</summary>
    public readonly bool IsIdentity
        => Near(M11, 1d) && Near(M12, 0d) && Near(M13, 0d) && Near(M14, 0d)
           && Near(M21, 0d) && Near(M22, 1d) && Near(M23, 0d) && Near(M24, 0d)
           && Near(M31, 0d) && Near(M32, 0d) && Near(M33, 1d) && Near(M34, 0d)
           && Near(M41, 0d) && Near(M42, 0d) && Near(M43, 0d) && Near(M44, 1d);

    /// <summary>Creates a translation matrix.</summary>
    /// <param name="x">The horizontal translation.</param>
    /// <param name="y">The vertical translation.</param>
    /// <param name="z">The depth translation.</param>
    public static Matrix4x4 CreateTranslation(double x, double y, double z)
    {
        var matrix = Identity;
        matrix.M41 = x;
        matrix.M42 = y;
        matrix.M43 = z;
        return matrix;
    }

    /// <summary>Creates a scale matrix about the origin.</summary>
    /// <param name="x">The horizontal scale factor.</param>
    /// <param name="y">The vertical scale factor.</param>
    /// <param name="z">The depth scale factor.</param>
    public static Matrix4x4 CreateScale(double x, double y, double z)
    {
        var matrix = Identity;
        matrix.M11 = x;
        matrix.M22 = y;
        matrix.M33 = z;
        return matrix;
    }

    /// <summary>Creates a right-handed rotation about the X axis.</summary>
    /// <param name="degrees">The rotation angle in degrees.</param>
    public static Matrix4x4 CreateRotationX(double degrees)
    {
        var (sin, cos) = SinCos(degrees);
        var matrix = Identity;
        matrix.M22 = cos;
        matrix.M23 = sin;
        matrix.M32 = -sin;
        matrix.M33 = cos;
        return matrix;
    }

    /// <summary>Creates a right-handed rotation about the Y axis.</summary>
    /// <param name="degrees">The rotation angle in degrees.</param>
    public static Matrix4x4 CreateRotationY(double degrees)
    {
        var (sin, cos) = SinCos(degrees);
        var matrix = Identity;
        matrix.M11 = cos;
        matrix.M13 = -sin;
        matrix.M31 = sin;
        matrix.M33 = cos;
        return matrix;
    }

    /// <summary>Creates a right-handed rotation about the Z axis.</summary>
    /// <param name="degrees">The rotation angle in degrees.</param>
    public static Matrix4x4 CreateRotationZ(double degrees)
    {
        var (sin, cos) = SinCos(degrees);
        var matrix = Identity;
        matrix.M11 = cos;
        matrix.M12 = sin;
        matrix.M21 = -sin;
        matrix.M22 = cos;
        return matrix;
    }

    /// <summary>Multiplies two matrices; the result applies <paramref name="a"/> first.</summary>
    /// <param name="a">The first matrix.</param>
    /// <param name="b">The second matrix.</param>
    public static Matrix4x4 Multiply(in Matrix4x4 a, in Matrix4x4 b)
        => new()
        {
            M11 = (a.M11 * b.M11) + (a.M12 * b.M21) + (a.M13 * b.M31) + (a.M14 * b.M41),
            M12 = (a.M11 * b.M12) + (a.M12 * b.M22) + (a.M13 * b.M32) + (a.M14 * b.M42),
            M13 = (a.M11 * b.M13) + (a.M12 * b.M23) + (a.M13 * b.M33) + (a.M14 * b.M43),
            M14 = (a.M11 * b.M14) + (a.M12 * b.M24) + (a.M13 * b.M34) + (a.M14 * b.M44),

            M21 = (a.M21 * b.M11) + (a.M22 * b.M21) + (a.M23 * b.M31) + (a.M24 * b.M41),
            M22 = (a.M21 * b.M12) + (a.M22 * b.M22) + (a.M23 * b.M32) + (a.M24 * b.M42),
            M23 = (a.M21 * b.M13) + (a.M22 * b.M23) + (a.M23 * b.M33) + (a.M24 * b.M43),
            M24 = (a.M21 * b.M14) + (a.M22 * b.M24) + (a.M23 * b.M34) + (a.M24 * b.M44),

            M31 = (a.M31 * b.M11) + (a.M32 * b.M21) + (a.M33 * b.M31) + (a.M34 * b.M41),
            M32 = (a.M31 * b.M12) + (a.M32 * b.M22) + (a.M33 * b.M32) + (a.M34 * b.M42),
            M33 = (a.M31 * b.M13) + (a.M32 * b.M23) + (a.M33 * b.M33) + (a.M34 * b.M43),
            M34 = (a.M31 * b.M14) + (a.M32 * b.M24) + (a.M33 * b.M34) + (a.M34 * b.M44),

            M41 = (a.M41 * b.M11) + (a.M42 * b.M21) + (a.M43 * b.M31) + (a.M44 * b.M41),
            M42 = (a.M41 * b.M12) + (a.M42 * b.M22) + (a.M43 * b.M32) + (a.M44 * b.M42),
            M43 = (a.M41 * b.M13) + (a.M42 * b.M23) + (a.M43 * b.M33) + (a.M44 * b.M43),
            M44 = (a.M41 * b.M14) + (a.M42 * b.M24) + (a.M43 * b.M34) + (a.M44 * b.M44),
        };

    /// <summary>Multiplies two matrices; the result applies <paramref name="left"/> first.</summary>
    /// <param name="left">The first matrix.</param>
    /// <param name="right">The second matrix.</param>
    public static Matrix4x4 operator *(Matrix4x4 left, Matrix4x4 right) => Multiply(left, right);

    /// <summary>Transforms one point and returns its homogeneous coordinates.</summary>
    /// <param name="x">The point's x coordinate.</param>
    /// <param name="y">The point's y coordinate.</param>
    /// <param name="z">The point's z coordinate.</param>
    /// <param name="w">The homogeneous w coordinate; one for a position.</param>
    /// <param name="resultX">The transformed x.</param>
    /// <param name="resultY">The transformed y.</param>
    /// <param name="resultZ">The transformed z.</param>
    /// <param name="resultW">The transformed w.</param>
    public readonly void Transform(
        double x,
        double y,
        double z,
        double w,
        out double resultX,
        out double resultY,
        out double resultZ,
        out double resultW)
    {
        resultX = (x * M11) + (y * M21) + (z * M31) + (w * M41);
        resultY = (x * M12) + (y * M22) + (z * M32) + (w * M42);
        resultZ = (x * M13) + (y * M23) + (z * M33) + (w * M43);
        resultW = (x * M14) + (y * M24) + (z * M34) + (w * M44);
    }

    private static bool Near(double value, double expected) => Math.Abs(value - expected) < 1e-9d;

    private static (double Sin, double Cos) SinCos(double degrees)
    {
        var radians = degrees * Math.PI / 180d;
        return (Math.Sin(radians), Math.Cos(radians));
    }
}

/// <summary>A three-dimensional transform applied to a two-dimensional visual element.</summary>
/// <remarks>
/// <para>
/// The visual's local plane is z = 0. A transform that moves part of it toward the camera makes that part
/// larger and the opposite side smaller when the transform carries a perspective depth.
/// </para>
/// <para>
/// <see cref="PerspectiveDepth"/> is zero for transforms without a camera distance; those stay orthographic.
/// </para>
/// </remarks>
public abstract class Transform3D
{
    /// <summary>The identity transform.</summary>
    public static Transform3D Identity { get; } = new MatrixTransform3D();

    /// <summary>The composed matrix of this transform.</summary>
    public abstract Matrix4x4 Value { get; }

    /// <summary>The camera distance used for a perspective divide; zero means an orthographic projection.</summary>
    public virtual double PerspectiveDepth => 0d;

    /// <summary>Whether this transform leaves every point in place.</summary>
    public bool IsIdentity => Value.IsIdentity;
}

/// <summary>A transform given directly by its sixteen matrix elements.</summary>
public sealed class MatrixTransform3D : Transform3D
{
    /// <summary>The first row, first column element; one by default.</summary>
    public double M11 { get; set; } = 1d;

    /// <summary>The first row, second column element.</summary>
    public double M12 { get; set; }

    /// <summary>The first row, third column element.</summary>
    public double M13 { get; set; }

    /// <summary>The first row, fourth column element.</summary>
    public double M14 { get; set; }

    /// <summary>The second row, first column element.</summary>
    public double M21 { get; set; }

    /// <summary>The second row, second column element; one by default.</summary>
    public double M22 { get; set; } = 1d;

    /// <summary>The second row, third column element.</summary>
    public double M23 { get; set; }

    /// <summary>The second row, fourth column element.</summary>
    public double M24 { get; set; }

    /// <summary>The third row, first column element.</summary>
    public double M31 { get; set; }

    /// <summary>The third row, second column element.</summary>
    public double M32 { get; set; }

    /// <summary>The third row, third column element; one by default.</summary>
    public double M33 { get; set; } = 1d;

    /// <summary>The third row, fourth column element.</summary>
    public double M34 { get; set; }

    /// <summary>The fourth row, first column element; the horizontal translation.</summary>
    public double M41 { get; set; }

    /// <summary>The fourth row, second column element; the vertical translation.</summary>
    public double M42 { get; set; }

    /// <summary>The fourth row, third column element; the depth translation.</summary>
    public double M43 { get; set; }

    /// <summary>The fourth row, fourth column element; one by default.</summary>
    public double M44 { get; set; } = 1d;

    /// <summary>The camera distance used for the perspective divide; zero stays orthographic.</summary>
    public double Depth { get; set; }

    /// <inheritdoc />
    public override Matrix4x4 Value => new()
    {
        M11 = M11,
        M12 = M12,
        M13 = M13,
        M14 = M14,
        M21 = M21,
        M22 = M22,
        M23 = M23,
        M24 = M24,
        M31 = M31,
        M32 = M32,
        M33 = M33,
        M34 = M34,
        M41 = M41,
        M42 = M42,
        M43 = M43,
        M44 = M44,
    };

    /// <inheritdoc />
    public override double PerspectiveDepth => Depth;
}

/// <summary>A translation in three dimensions.</summary>
public sealed class Translate3DTransform : Transform3D
{
    /// <summary>The horizontal distance.</summary>
    public double X { get; set; }

    /// <summary>The vertical distance.</summary>
    public double Y { get; set; }

    /// <summary>The depth distance.</summary>
    public double Z { get; set; }

    /// <inheritdoc />
    public override Matrix4x4 Value => Matrix4x4.CreateTranslation(X, Y, Z);
}

/// <summary>A scale in three dimensions about <see cref="CenterX"/>, <see cref="CenterY"/>, <see cref="CenterZ"/>.</summary>
public sealed class Scale3DTransform : Transform3D
{
    /// <summary>The horizontal scale factor; one by default.</summary>
    public double ScaleX { get; set; } = 1d;

    /// <summary>The vertical scale factor; one by default.</summary>
    public double ScaleY { get; set; } = 1d;

    /// <summary>The depth scale factor; one by default.</summary>
    public double ScaleZ { get; set; } = 1d;

    /// <summary>The horizontal centre of the scale.</summary>
    public double CenterX { get; set; }

    /// <summary>The vertical centre of the scale.</summary>
    public double CenterY { get; set; }

    /// <summary>The depth centre of the scale.</summary>
    public double CenterZ { get; set; }

    /// <inheritdoc />
    public override Matrix4x4 Value
    {
        get
        {
            if (CenterX == 0d && CenterY == 0d && CenterZ == 0d)
            {
                return Matrix4x4.CreateScale(ScaleX, ScaleY, ScaleZ);
            }

            return Matrix4x4.Multiply(
                Matrix4x4.Multiply(
                    Matrix4x4.CreateTranslation(-CenterX, -CenterY, -CenterZ),
                    Matrix4x4.CreateScale(ScaleX, ScaleY, ScaleZ)),
                Matrix4x4.CreateTranslation(CenterX, CenterY, CenterZ));
        }
    }
}

/// <summary>
/// A rotation about the X, Y and Z axes, applied in that order, around a local centre.
/// </summary>
/// <remarks>
/// The element rotates around its centre by default only when the caller sets the centres; the defaults are
/// the local origin, matching Windows Presentation Foundation's transform types. Set
/// <see cref="CenterX"/> and <see cref="CenterY"/> to half the element's width and height for a centre spin.
/// </remarks>
public sealed class Rotate3DTransform : Transform3D
{
    /// <summary>The rotation about the X axis in degrees.</summary>
    public double AngleX { get; set; }

    /// <summary>The rotation about the Y axis in degrees.</summary>
    public double AngleY { get; set; }

    /// <summary>The rotation about the Z axis in degrees.</summary>
    public double AngleZ { get; set; }

    /// <summary>The horizontal centre of the rotation, in the visual's local coordinates.</summary>
    public double CenterX { get; set; }

    /// <summary>The vertical centre of the rotation, in the visual's local coordinates.</summary>
    public double CenterY { get; set; }

    /// <summary>The depth of the rotation centre, in the visual's local coordinates.</summary>
    public double CenterZ { get; set; }

    /// <summary>The camera distance used for perspective; zero or less disables the perspective divide.</summary>
    public double Depth { get; set; } = 500d;

    /// <inheritdoc />
    public override Matrix4x4 Value
    {
        get
        {
            var rotation = Matrix4x4.Multiply(
                Matrix4x4.Multiply(Matrix4x4.CreateRotationX(AngleX), Matrix4x4.CreateRotationY(AngleY)),
                Matrix4x4.CreateRotationZ(AngleZ));

            if (CenterX == 0d && CenterY == 0d && CenterZ == 0d)
            {
                return rotation;
            }

            return Matrix4x4.Multiply(
                Matrix4x4.Multiply(
                    Matrix4x4.CreateTranslation(-CenterX, -CenterY, -CenterZ),
                    rotation),
                Matrix4x4.CreateTranslation(CenterX, CenterY, CenterZ));
        }
    }

    /// <inheritdoc />
    public override double PerspectiveDepth => Depth;
}

/// <summary>A sequence of 3D transforms applied in declaration order.</summary>
public sealed class Transform3DGroup : Transform3D
{
    /// <summary>The child transforms, applied first to last.</summary>
    public IList<Transform3D> Children { get; } = new List<Transform3D>();

    /// <inheritdoc />
    public override Matrix4x4 Value
    {
        get
        {
            var result = Matrix4x4.Identity;
            foreach (var child in Children)
            {
                if (child is not null)
                {
                    result = Matrix4x4.Multiply(result, child.Value);
                }
            }

            return result;
        }
    }

    /// <summary>The first child's positive camera distance, or zero when no child has one.</summary>
    public override double PerspectiveDepth
    {
        get
        {
            foreach (var child in Children)
            {
                if (child is not null && child.PerspectiveDepth > 0d)
                {
                    return child.PerspectiveDepth;
                }
            }

            return 0d;
        }
    }
}

/// <summary>
/// The perspective projection of a visual's local plane into two dimensions.
/// </summary>
/// <remarks>
/// The four projected corners define a homography, so every straight line in the source stays straight.
/// <see cref="Map(PointF)"/> and <see cref="Unmap(PointF)"/> are inverses for non-degenerate quads.
/// </remarks>
public sealed class Projection3D
{
    private readonly double _left;
    private readonly double _top;
    private readonly double _width;
    private readonly double _height;
    private readonly double _a;
    private readonly double _b;
    private readonly double _c;
    private readonly double _d;
    private readonly double _e;
    private readonly double _f;
    private readonly double _g;
    private readonly double _h;

    /// <summary>Projects <paramref name="bounds"/> through <paramref name="transform"/>.</summary>
    /// <param name="bounds">The visual's local rectangle.</param>
    /// <param name="transform">The transform to apply, or <see langword="null"/> for the identity.</param>
    public Projection3D(RectangleF bounds, Transform3D? transform)
    {
        _left = bounds.Left;
        _top = bounds.Top;
        _width = bounds.Width;
        _height = bounds.Height;

        var matrix = transform?.Value ?? Matrix4x4.Identity;
        var depth = transform?.PerspectiveDepth ?? 0d;
        var corners = new[]
        {
            Project(matrix, depth, bounds.Left, bounds.Top),
            Project(matrix, depth, bounds.Right, bounds.Top),
            Project(matrix, depth, bounds.Right, bounds.Bottom),
            Project(matrix, depth, bounds.Left, bounds.Bottom),
        };
        Corners = corners;
        IsDegenerate = !IsUsable(corners) || Area(corners) < 0.5d;

        if (!IsDegenerate)
        {
            ( _a, _b, _c, _d, _e, _f, _g, _h) = Homography(corners);
        }
    }

    /// <summary>The projected corners in top-left, top-right, bottom-right, bottom-left order.</summary>
    public PointF[] Corners { get; }

    /// <summary>Whether the quad collapsed to a line or a point, or has a non-finite corner.</summary>
    public bool IsDegenerate { get; }

    /// <summary>Maps a local point to the projected plane.</summary>
    /// <param name="local">The point in the visual's local coordinates.</param>
    public PointF Map(PointF local)
    {
        if (IsDegenerate || _width == 0d || _height == 0d)
        {
            return local;
        }

        return MapNormalized((local.X - _left) / _width, (local.Y - _top) / _height);
    }

    /// <summary>Maps a point expressed as fractions of the local rectangle, 0..1 on each axis.</summary>
    /// <param name="u">The horizontal fraction.</param>
    /// <param name="v">The vertical fraction.</param>
    public PointF MapNormalized(double u, double v)
    {
        if (IsDegenerate)
        {
            return new PointF(
                (float)(_left + (u * _width)),
                (float)(_top + (v * _height)));
        }

        var denominator = (_g * u) + (_h * v) + 1d;
        if (Math.Abs(denominator) < 1e-8d)
        {
            denominator = denominator < 0d ? -1e-8d : 1e-8d;
        }

        return new PointF(
            (float)(((_a * u) + (_b * v) + _c) / denominator),
            (float)(((_d * u) + (_e * v) + _f) / denominator));
    }

    /// <summary>Maps a projected point back into the visual's local coordinates.</summary>
    /// <param name="projected">The point on the projected plane.</param>
    public PointF Unmap(PointF projected)
    {
        if (IsDegenerate || _width == 0d || _height == 0d)
        {
            return projected;
        }

        if (!TryUnmapNormalized(projected, out var u, out var v))
        {
            return projected;
        }

        return new PointF(
            (float)(_left + (u * _width)),
            (float)(_top + (v * _height)));
    }

    /// <summary>Maps a projected point back to fractions of the local rectangle, 0..1 on each axis.</summary>
    /// <param name="projected">The point on the projected plane.</param>
    /// <param name="u">The horizontal fraction.</param>
    /// <param name="v">The vertical fraction.</param>
    /// <returns><see langword="false"/> when the projection is degenerate or the inverse is not finite.</returns>
    internal bool TryUnmapNormalized(PointF projected, out double u, out double v)
    {
        u = 0d;
        v = 0d;
        if (IsDegenerate || _width == 0d || _height == 0d)
        {
            return false;
        }

        var determinant = (_a * ((_e * 1d) - (_f * _h)))
                          - (_b * ((_d * 1d) - (_f * _g)))
                          + (_c * ((_d * _h) - (_e * _g)));
        if (Math.Abs(determinant) < 1e-12d)
        {
            return false;
        }

        var x = projected.X;
        var y = projected.Y;
        var inverse = 1d / determinant;
        var ia = ((_e * 1d) - (_f * _h)) * inverse;
        var ib = ((_c * _h) - _b) * inverse;
        var ic = ((_b * _f) - (_c * _e)) * inverse;
        var id = ((_f * _g) - _d) * inverse;
        var ie = ((_a * 1d) - (_c * _g)) * inverse;
        var @if = ((_c * _d) - (_a * _f)) * inverse;
        var ig = ((_d * _h) - (_e * _g)) * inverse;
        var ih = ((_b * _g) - (_a * _h)) * inverse;
        var ii = ((_a * _e) - (_b * _d)) * inverse;

        var denominator = (ig * x) + (ih * y) + ii;
        if (Math.Abs(denominator) < 1e-8d)
        {
            denominator = denominator < 0d ? -1e-8d : 1e-8d;
        }

        u = ((ia * x) + (ib * y) + ic) / denominator;
        v = ((id * x) + (ie * y) + @if) / denominator;
        return XamlMath.IsFinite(u) && XamlMath.IsFinite(v);
    }

    private static PointF Project(in Matrix4x4 matrix, double depth, double x, double y)
    {
        matrix.Transform(x, y, 0d, 1d, out var tx, out var ty, out var tz, out _);
        if (depth > 0d)
        {
            var w = 1d - (tz / depth);
            if (Math.Abs(w) < 1e-4d)
            {
                w = w < 0d ? -1e-4d : 1e-4d;
            }

            tx /= w;
            ty /= w;
        }

        return new PointF((float)tx, (float)ty);
    }

    private static bool IsUsable(IReadOnlyList<PointF> corners)
    {
        foreach (var corner in corners)
        {
            if (!XamlMath.IsFinite(corner.X) || !XamlMath.IsFinite(corner.Y))
            {
                return false;
            }
        }

        return true;
    }

    private static double Area(IReadOnlyList<PointF> corners)
    {
        // Shoelace area; the sign only says whether the quad is flipped.
        var sum = 0d;
        for (var index = 0; index < corners.Count; index++)
        {
            var current = corners[index];
            var next = corners[(index + 1) % corners.Count];
            sum += ((double)current.X * next.Y) - ((double)next.X * current.Y);
        }

        return Math.Abs(sum) * 0.5d;
    }

    // Square-to-quadrilateral homography (Heckbert): p0=(0,0), p1=(1,0), p2=(1,1), p3=(0,1).
    private static (double A, double B, double C, double D, double E, double F, double G, double H) Homography(
        IReadOnlyList<PointF> corners)
    {
        var p0 = corners[0];
        var p1 = corners[1];
        var p2 = corners[2];
        var p3 = corners[3];

        var dx1 = (double)p1.X - p2.X;
        var dx2 = (double)p3.X - p2.X;
        var dx3 = (double)p0.X - p1.X + p2.X - p3.X;
        var dy1 = (double)p1.Y - p2.Y;
        var dy2 = (double)p3.Y - p2.Y;
        var dy3 = (double)p0.Y - p1.Y + p2.Y - p3.Y;
        var denominator = (dx1 * dy2) - (dx2 * dy1);

        if (Math.Abs(dx3) < 1e-12d && Math.Abs(dy3) < 1e-12d)
        {
            return (
                p1.X - p0.X,
                p2.X - p1.X,
                p0.X,
                p1.Y - p0.Y,
                p2.Y - p1.Y,
                p0.Y,
                0d,
                0d);
        }

        if (Math.Abs(denominator) < 1e-12d)
        {
            return (
                p1.X - p0.X,
                p3.X - p0.X,
                p0.X,
                p1.Y - p0.Y,
                p3.Y - p0.Y,
                p0.Y,
                0d,
                0d);
        }

        var g = ((dx3 * dy2) - (dx2 * dy3)) / denominator;
        var h = ((dx1 * dy3) - (dx3 * dy1)) / denominator;
        return (
            p1.X - p0.X + (g * p1.X),
            p3.X - p0.X + (h * p3.X),
            p0.X,
            p1.Y - p0.Y + (g * p1.Y),
            p3.Y - p0.Y + (h * p3.Y),
            p0.Y,
            g,
            h);
    }
}
