using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Easel.Controls;

namespace Easel.Animation.Sampling;

// 引擎的值插值表。端点由动画启动时读出，这里只按缓动进度算出这一帧的值；
// Brush 与 Transform3D 复用 Easel 已有的结构化插值，其余是逐分量线性插值。
internal static class ValueInterpolator
{
    internal static bool CanInterpolate(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            type = underlying;
        }

        if (type == typeof(double) || type == typeof(float) || type == typeof(int)
            || type == typeof(long) || type == typeof(short) || type == typeof(byte)
            || type == typeof(decimal))
        {
            return true;
        }

        if (type == typeof(Color) || type == typeof(Point) || type == typeof(PointF)
            || type == typeof(Size) || type == typeof(SizeF)
            || type == typeof(Rectangle) || type == typeof(RectangleF)
            || type == typeof(Padding) || type == typeof(Thickness)
            || type == typeof(Matrix) || type == typeof(Matrix4x4))
        {
            return true;
        }

        return typeof(Brush).IsAssignableFrom(type) || typeof(Transform3D).IsAssignableFrom(type);
    }

    internal static object? Interpolate(Type type, object? from, object? to, double t)
    {
        // 端帧精确等于端点：既不依赖缓动曲线在 0/1 的精度，也让起点为 NaN（如 Width 的默认值）时仍落到目标值。
        // 非可空值类型的端点若为 null，就按另一端处理，避免把 null 写进值类型属性。
        var nullableValue = !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
        if (t == 1d)
        {
            return to is null && !nullableValue ? from : to;
        }

        if (t == 0d)
        {
            return from is null && !nullableValue ? to : from;
        }

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            return from is null && to is null ? null : Interpolate(underlying, from, to, t);
        }

        if (type == typeof(double))
        {
            var start = ToDouble(from);
            return Lerp(start, to is null ? start : ToDouble(to), t);
        }

        if (type == typeof(float))
        {
            var start = ToDouble(from);
            return (float)Lerp(start, to is null ? start : ToDouble(to), t);
        }

        if (type == typeof(int))
        {
            var start = ToDouble(from);
            return (int)Math.Round(Lerp(start, to is null ? start : ToDouble(to), t));
        }

        if (type == typeof(long))
        {
            var start = ToDouble(from);
            return (long)Math.Round(Lerp(start, to is null ? start : ToDouble(to), t));
        }

        if (type == typeof(short))
        {
            var start = ToDouble(from);
            return (short)Math.Round(Lerp(start, to is null ? start : ToDouble(to), t));
        }

        if (type == typeof(byte))
        {
            var start = ToDouble(from);
            return (byte)Math.Round(Lerp(start, to is null ? start : ToDouble(to), t));
        }

        if (type == typeof(decimal))
        {
            var start = ToDouble(from);
            return (decimal)Lerp(start, to is null ? start : ToDouble(to), t);
        }

        if (type == typeof(Color))
        {
            var start = from is Color startColor ? startColor : Color.Transparent;
            var end = to is Color endColor ? endColor : start;
            return BrushInterpolation.LerpColor(start, end, t);
        }

        if (type == typeof(PointF))
        {
            var start = from is PointF startPoint ? startPoint : default;
            var end = to is PointF endPoint ? endPoint : start;
            return new PointF((float)Lerp(start.X, end.X, t), (float)Lerp(start.Y, end.Y, t));
        }

        if (type == typeof(Point))
        {
            var start = from is Point startPoint ? startPoint : default;
            var end = to is Point endPoint ? endPoint : start;
            return new Point(
                (int)Math.Round(Lerp(start.X, end.X, t)),
                (int)Math.Round(Lerp(start.Y, end.Y, t)));
        }

        if (type == typeof(SizeF))
        {
            var start = from is SizeF startSize ? startSize : default;
            var end = to is SizeF endSize ? endSize : start;
            return new SizeF((float)Lerp(start.Width, end.Width, t), (float)Lerp(start.Height, end.Height, t));
        }

        if (type == typeof(Size))
        {
            var start = from is Size startSize ? startSize : default;
            var end = to is Size endSize ? endSize : start;
            return new Size(
                (int)Math.Round(Lerp(start.Width, end.Width, t)),
                (int)Math.Round(Lerp(start.Height, end.Height, t)));
        }

        if (type == typeof(RectangleF))
        {
            var start = from is RectangleF startRect ? startRect : default;
            var end = to is RectangleF endRect ? endRect : start;
            return new RectangleF(
                (float)Lerp(start.X, end.X, t),
                (float)Lerp(start.Y, end.Y, t),
                (float)Lerp(start.Width, end.Width, t),
                (float)Lerp(start.Height, end.Height, t));
        }

        if (type == typeof(Rectangle))
        {
            var start = from is Rectangle startRect ? startRect : default;
            var end = to is Rectangle endRect ? endRect : start;
            return new Rectangle(
                (int)Math.Round(Lerp(start.X, end.X, t)),
                (int)Math.Round(Lerp(start.Y, end.Y, t)),
                (int)Math.Round(Lerp(start.Width, end.Width, t)),
                (int)Math.Round(Lerp(start.Height, end.Height, t)));
        }

        if (type == typeof(Padding))
        {
            var start = from is Padding startPadding ? startPadding : default;
            var end = to is Padding endPadding ? endPadding : start;
            return new Padding(
                (int)Math.Round(Lerp(start.Left, end.Left, t)),
                (int)Math.Round(Lerp(start.Top, end.Top, t)),
                (int)Math.Round(Lerp(start.Right, end.Right, t)),
                (int)Math.Round(Lerp(start.Bottom, end.Bottom, t)));
        }

        if (type == typeof(Thickness))
        {
            var start = from is Thickness startThickness ? startThickness : default;
            var end = to is Thickness endThickness ? endThickness : start;
            return new Thickness(
                (float)Lerp(start.Left, end.Left, t),
                (float)Lerp(start.Top, end.Top, t),
                (float)Lerp(start.Right, end.Right, t),
                (float)Lerp(start.Bottom, end.Bottom, t));
        }

        if (type == typeof(Matrix))
        {
            var start = from is Matrix startMatrix ? startMatrix : new Matrix();
            var end = to is Matrix endMatrix ? endMatrix : start;
            var a = start.Elements;
            var b = end.Elements;
            return new Matrix(
                (float)Lerp(a[0], b[0], t),
                (float)Lerp(a[1], b[1], t),
                (float)Lerp(a[2], b[2], t),
                (float)Lerp(a[3], b[3], t),
                (float)Lerp(a[4], b[4], t),
                (float)Lerp(a[5], b[5], t));
        }

        if (type == typeof(Matrix4x4))
        {
            var a = from is Matrix4x4 startMatrix ? startMatrix : Matrix4x4.Identity;
            var b = to is Matrix4x4 endMatrix ? endMatrix : a;
            return new Matrix4x4
            {
                M11 = Lerp(a.M11, b.M11, t),
                M12 = Lerp(a.M12, b.M12, t),
                M13 = Lerp(a.M13, b.M13, t),
                M14 = Lerp(a.M14, b.M14, t),
                M21 = Lerp(a.M21, b.M21, t),
                M22 = Lerp(a.M22, b.M22, t),
                M23 = Lerp(a.M23, b.M23, t),
                M24 = Lerp(a.M24, b.M24, t),
                M31 = Lerp(a.M31, b.M31, t),
                M32 = Lerp(a.M32, b.M32, t),
                M33 = Lerp(a.M33, b.M33, t),
                M34 = Lerp(a.M34, b.M34, t),
                M41 = Lerp(a.M41, b.M41, t),
                M42 = Lerp(a.M42, b.M42, t),
                M43 = Lerp(a.M43, b.M43, t),
                M44 = Lerp(a.M44, b.M44, t),
            };
        }

        if (typeof(Brush).IsAssignableFrom(type))
        {
            return BrushInterpolation.Interpolate(from as Brush, to as Brush, t);
        }

        if (typeof(Transform3D).IsAssignableFrom(type))
        {
            return Transform3DInterpolation.Interpolate(from as Transform3D, to as Transform3D, t);
        }

        throw new NotSupportedException($"'{type.Name}' has no interpolator in the animation engine.");
    }

    private static double ToDouble(object? value) => value switch
    {
        double number => number,
        float number => number,
        int number => number,
        long number => number,
        short number => number,
        byte number => number,
        decimal number => (double)number,
        _ => 0d,
    };

    private static double Lerp(double start, double end, double t) => start + ((end - start) * t);
}
