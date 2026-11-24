namespace Easel;

/// <summary>
/// Interpolates two <see cref="Transform3D"/> values.
/// </summary>
/// <remarks>
/// <para>
/// Transforms of the same kind interpolate their own values - angles, centres, scale factors, offsets, camera
/// depth - so a rotation keeps its pivot and a perspective keeps its camera distance. A rotation between two
/// angles therefore sweeps the arc instead of collapsing the matrix.
/// </para>
/// <para>
/// Transforms of different kinds have no shared parameter, so their composed <see cref="Matrix4x4"/> values are
/// lerped element by element and returned as a <see cref="MatrixTransform3D"/>. That representation is exact at
/// both endpoints, but a blend between two rotations can shrink the matrix mid-way; pair transforms of the same
/// kind when the sweep matters.
/// </para>
/// <para>
/// The eased time is not clamped: every component extrapolates linearly, which is what an easing curve with
/// overshoot expects.
/// </para>
/// </remarks>
public static class Transform3DInterpolation
{
    /// <summary>Interpolates <paramref name="from"/> toward <paramref name="to"/> at <paramref name="t"/>.</summary>
    /// <param name="from">The transform at <c>t = 0</c>; <see langword="null"/> counts as the identity.</param>
    /// <param name="to">The transform at <c>t = 1</c>; <see langword="null"/> counts as the identity.</param>
    /// <param name="t">The position along the transition; values outside 0..1 extrapolate.</param>
    public static Transform3D Interpolate(Transform3D? from, Transform3D? to, double t)
    {
        from ??= Transform3D.Identity;
        to ??= Transform3D.Identity;

        return (from, to) switch
        {
            (Rotate3DTransform start, Rotate3DTransform end) => new Rotate3DTransform
            {
                AngleX = Lerp(start.AngleX, end.AngleX, t),
                AngleY = Lerp(start.AngleY, end.AngleY, t),
                AngleZ = Lerp(start.AngleZ, end.AngleZ, t),
                CenterX = Lerp(start.CenterX, end.CenterX, t),
                CenterY = Lerp(start.CenterY, end.CenterY, t),
                CenterZ = Lerp(start.CenterZ, end.CenterZ, t),
                Depth = Lerp(start.Depth, end.Depth, t),
            },
            (Scale3DTransform start, Scale3DTransform end) => new Scale3DTransform
            {
                ScaleX = Lerp(start.ScaleX, end.ScaleX, t),
                ScaleY = Lerp(start.ScaleY, end.ScaleY, t),
                ScaleZ = Lerp(start.ScaleZ, end.ScaleZ, t),
                CenterX = Lerp(start.CenterX, end.CenterX, t),
                CenterY = Lerp(start.CenterY, end.CenterY, t),
                CenterZ = Lerp(start.CenterZ, end.CenterZ, t),
            },
            (Translate3DTransform start, Translate3DTransform end) => new Translate3DTransform
            {
                X = Lerp(start.X, end.X, t),
                Y = Lerp(start.Y, end.Y, t),
                Z = Lerp(start.Z, end.Z, t),
            },
            (MatrixTransform3D start, MatrixTransform3D end) => FromMatrix(
                LerpMatrix(start.Value, end.Value, t),
                Lerp(start.Depth, end.Depth, t)),
            (Transform3DGroup start, Transform3DGroup end) when start.Children.Count == end.Children.Count
                => InterpolateGroup(start, end, t),
            _ => FromMatrix(
                LerpMatrix(from.Value, to.Value, t),
                Lerp(from.PerspectiveDepth, to.PerspectiveDepth, t)),
        };
    }

    private static Transform3DGroup InterpolateGroup(Transform3DGroup start, Transform3DGroup end, double t)
    {
        var result = new Transform3DGroup();
        for (var index = 0; index < start.Children.Count; index++)
        {
            result.Children.Add(Interpolate(start.Children[index], end.Children[index], t));
        }

        return result;
    }

    private static MatrixTransform3D FromMatrix(in Matrix4x4 matrix, double depth) => new()
    {
        M11 = matrix.M11,
        M12 = matrix.M12,
        M13 = matrix.M13,
        M14 = matrix.M14,
        M21 = matrix.M21,
        M22 = matrix.M22,
        M23 = matrix.M23,
        M24 = matrix.M24,
        M31 = matrix.M31,
        M32 = matrix.M32,
        M33 = matrix.M33,
        M34 = matrix.M34,
        M41 = matrix.M41,
        M42 = matrix.M42,
        M43 = matrix.M43,
        M44 = matrix.M44,
        Depth = depth > 0d ? depth : 0d,
    };

    private static Matrix4x4 LerpMatrix(in Matrix4x4 start, in Matrix4x4 end, double t) => new()
    {
        M11 = Lerp(start.M11, end.M11, t),
        M12 = Lerp(start.M12, end.M12, t),
        M13 = Lerp(start.M13, end.M13, t),
        M14 = Lerp(start.M14, end.M14, t),
        M21 = Lerp(start.M21, end.M21, t),
        M22 = Lerp(start.M22, end.M22, t),
        M23 = Lerp(start.M23, end.M23, t),
        M24 = Lerp(start.M24, end.M24, t),
        M31 = Lerp(start.M31, end.M31, t),
        M32 = Lerp(start.M32, end.M32, t),
        M33 = Lerp(start.M33, end.M33, t),
        M34 = Lerp(start.M34, end.M34, t),
        M41 = Lerp(start.M41, end.M41, t),
        M42 = Lerp(start.M42, end.M42, t),
        M43 = Lerp(start.M43, end.M43, t),
        M44 = Lerp(start.M44, end.M44, t),
    };

    private static double Lerp(double start, double end, double t) => start + ((end - start) * t);
}
