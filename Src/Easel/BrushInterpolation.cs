using System.Drawing;

namespace Easel;

/// <summary>
/// Interpolates two brushes by structure instead of by representative colour.
/// </summary>
/// <remarks>
/// <para>
/// Brushes of the same kind interpolate their own values, so a gradient keeps its shape while it moves:
/// <see cref="SolidColorBrush"/> lerps its colour, the gradient brushes lerp their stop list and their geometry,
/// and every kind lerps <see cref="Brush.Opacity"/>.
/// </para>
/// <para>
/// A solid brush against a gradient is lifted to the gradient's kind and both are sampled at the union of their
/// stop offsets. Sampling a piecewise-linear gradient at an extra offset does not change its shape, so both
/// endpoints stay exact while the middle is a real blend instead of a flattening to one colour.
/// </para>
/// <para>
/// Pairs with no structural relationship - a linear gradient against a radial one, any <see cref="ImageBrush"/>
/// - fall back to <see cref="CrossFadeBrush"/>, which paints both brushes into separate layers and blends them.
/// </para>
/// <para>
/// The eased time is not clamped: colours saturate per channel, stop offsets clamp to 0..1 and geometry keeps
/// extrapolating, which is what an easing curve with overshoot needs.
/// </para>
/// </remarks>
public static class BrushInterpolation
{
    /// <summary>Interpolates <paramref name="from"/> toward <paramref name="to"/> at <paramref name="t"/>.</summary>
    /// <param name="from">The starting brush; <see langword="null"/> counts as transparent.</param>
    /// <param name="to">The ending brush; <see langword="null"/> counts as transparent.</param>
    /// <param name="t">The position along the transition; values outside 0..1 extrapolate.</param>
    /// <returns>
    /// The brush at <paramref name="t"/>, or <see langword="null"/> when both sides are <see langword="null"/>.
    /// A pair with no structural relationship returns a <see cref="CrossFadeBrush"/>.
    /// </returns>
    public static Brush? Interpolate(Brush? from, Brush? to, double t)
    {
        if (from is null && to is null)
        {
            return null;
        }

        if (from is null)
        {
            from = new SolidColorBrush(Color.Transparent);
        }

        if (to is null)
        {
            to = new SolidColorBrush(Color.Transparent);
        }

        return (from, to) switch
        {
            (SolidColorBrush start, SolidColorBrush end) => new SolidColorBrush(LerpColor(start.Color, end.Color, t))
            {
                Opacity = Lerp(start.Opacity, end.Opacity, t),
            },
            (LinearGradientBrush start, LinearGradientBrush end) => InterpolateLinear(start, end, t),
            (RadialGradientBrush start, RadialGradientBrush end) => InterpolateRadial(start, end, t),
            (SolidColorBrush start, LinearGradientBrush end) => InterpolateLinear(LiftToLinear(start, end), end, t),
            (LinearGradientBrush start, SolidColorBrush end) => InterpolateLinear(start, LiftToLinear(end, start), t),
            (SolidColorBrush start, RadialGradientBrush end) => InterpolateRadial(LiftToRadial(start, end), end, t),
            (RadialGradientBrush start, SolidColorBrush end) => InterpolateRadial(start, LiftToRadial(end, start), t),
            _ => new CrossFadeBrush(from, to, t),
        };
    }

    /// <summary>Lerps two colours channel by channel, alpha included, saturating each channel at 0..255.</summary>
    /// <param name="from">The colour at <c>t = 0</c>.</param>
    /// <param name="to">The colour at <c>t = 1</c>.</param>
    /// <param name="t">The position along the transition.</param>
    public static Color LerpColor(Color from, Color to, double t)
        => Color.FromArgb(
            LerpChannel(from.A, to.A, t),
            LerpChannel(from.R, to.R, t),
            LerpChannel(from.G, to.G, t),
            LerpChannel(from.B, to.B, t));

    private static LinearGradientBrush InterpolateLinear(LinearGradientBrush start, LinearGradientBrush end, double t)
    {
        var result = new LinearGradientBrush
        {
            StartPoint = LerpPoint(start.StartPoint, end.StartPoint, t),
            EndPoint = LerpPoint(start.EndPoint, end.EndPoint, t),
            Opacity = Lerp(start.Opacity, end.Opacity, t),
        };

        foreach (var stop in InterpolateStops(start.GradientStops, end.GradientStops, t))
        {
            result.GradientStops.Add(stop);
        }

        return result;
    }

    private static RadialGradientBrush InterpolateRadial(RadialGradientBrush start, RadialGradientBrush end, double t)
    {
        var result = new RadialGradientBrush
        {
            Center = LerpPoint(start.Center, end.Center, t),
            Radius = (float)Lerp(start.Radius, end.Radius, t),
            Opacity = Lerp(start.Opacity, end.Opacity, t),
        };

        foreach (var stop in InterpolateStops(start.GradientStops, end.GradientStops, t))
        {
            result.GradientStops.Add(stop);
        }

        return result;
    }

    // 纯色抬成另一端渐变的形状，只带一个 stop：采样时它处处返回同一个颜色，端点因此仍与纯色一致。
    private static LinearGradientBrush LiftToLinear(SolidColorBrush solid, LinearGradientBrush shape)
    {
        var lifted = new LinearGradientBrush
        {
            StartPoint = shape.StartPoint,
            EndPoint = shape.EndPoint,
            Opacity = solid.Opacity,
        };
        lifted.GradientStops.Add(new GradientStop(solid.Color, 0d));
        return lifted;
    }

    private static RadialGradientBrush LiftToRadial(SolidColorBrush solid, RadialGradientBrush shape)
    {
        var lifted = new RadialGradientBrush
        {
            Center = shape.Center,
            Radius = shape.Radius,
            Opacity = solid.Opacity,
        };
        lifted.GradientStops.Add(new GradientStop(solid.Color, 0d));
        return lifted;
    }

    // 两端 stop 偏移取并集后逐点采样：线性渐变在任意额外偏移处采样不改变形状，所以并集不引入新的色带。
    private static List<GradientStop> InterpolateStops(
        IList<GradientStop> start,
        IList<GradientStop> end,
        double t)
    {
        var first = SortedStops(start);
        var second = SortedStops(end);
        var offsets = UnionOffsets(first, second);
        var result = new List<GradientStop>(offsets.Count);
        foreach (var offset in offsets)
        {
            result.Add(new GradientStop(
                LerpColor(SampleStops(first, offset), SampleStops(second, offset), t),
                offset));
        }

        return result;
    }

    private static List<GradientStop> SortedStops(IList<GradientStop> stops)
    {
        var sorted = new List<GradientStop>(stops.Count);
        foreach (var stop in stops)
        {
            if (stop is not null)
            {
                sorted.Add(stop);
            }
        }

        sorted.Sort(static (left, right) => left.Offset.CompareTo(right.Offset));
        return sorted;
    }

    private static List<double> UnionOffsets(List<GradientStop> start, List<GradientStop> end)
    {
        var offsets = new List<double> { 0d, 1d };
        foreach (var stop in start)
        {
            AddOffset(offsets, stop.Offset);
        }

        foreach (var stop in end)
        {
            AddOffset(offsets, stop.Offset);
        }

        offsets.Sort();
        return offsets;
    }

    private static void AddOffset(List<double> offsets, double offset)
    {
        var value = XamlMath.Clamp(offset, 0d, 1d);
        foreach (var existing in offsets)
        {
            if (Math.Abs(existing - value) < 1e-6d)
            {
                return;
            }
        }

        offsets.Add(value);
    }

    // GDI+ 的 ColorBlend 在相邻 stop 之间按通道线性插值；这里照同一规则求值，两端的颜色因此与直接绘制一致。
    private static Color SampleStops(List<GradientStop> stops, double offset)
    {
        if (stops.Count == 0)
        {
            return Color.Transparent;
        }

        var firstOffset = XamlMath.Clamp(stops[0].Offset, 0d, 1d);
        if (offset <= firstOffset)
        {
            return stops[0].Color;
        }

        var lastOffset = XamlMath.Clamp(stops[stops.Count - 1].Offset, 0d, 1d);
        if (offset >= lastOffset)
        {
            return stops[stops.Count - 1].Color;
        }

        for (var index = 0; index < stops.Count - 1; index++)
        {
            var left = XamlMath.Clamp(stops[index].Offset, 0d, 1d);
            var right = XamlMath.Clamp(stops[index + 1].Offset, 0d, 1d);
            if (offset < left || offset > right)
            {
                continue;
            }

            var span = right - left;
            if (span <= 0d)
            {
                return stops[index + 1].Color;
            }

            return LerpColor(stops[index].Color, stops[index + 1].Color, (offset - left) / span);
        }

        return stops[stops.Count - 1].Color;
    }

    private static int LerpChannel(byte start, byte end, double t)
        => Math.Min(255, Math.Max(0, (int)Math.Round(start + ((end - start) * t))));

    private static double Lerp(double start, double end, double t)
        => start + ((end - start) * t);

    private static PointF LerpPoint(PointF start, PointF end, double t)
        => new(
            (float)Lerp(start.X, end.X, t),
            (float)Lerp(start.Y, end.Y, t));
}
