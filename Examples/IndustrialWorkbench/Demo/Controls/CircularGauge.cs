using System.Drawing;
using Pen = System.Drawing.Pen;
using LineCap = System.Drawing.Drawing2D.LineCap;
using System.Drawing.Drawing2D;
using Easel;
using Easel.Visuals;
using XamlBrush = Easel.Brush;

namespace IndustrialWorkbench.Controls;

/// <summary>A circular HMI gauge: 270° scale arc, coloured bands, a needle and a live centre value.</summary>
/// <remarks>
/// <see cref="Value"/> is clamped into <see cref="Minimum"/>..<see cref="Maximum"/>; the arc and needle
/// colour follow the warning/danger thresholds. Everything is drawn in logical DIPs inside the arranged bounds.
/// </remarks>
public sealed class CircularGauge : VisualElement
{
    /// <summary>The lowest value.</summary>
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(CircularGauge),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>The highest value.</summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(CircularGauge),
        new PropertyMetadata(100d) { AffectsRender = true });

    /// <summary>The current value.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(CircularGauge),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>The value where the amber band starts.</summary>
    public static readonly DependencyProperty WarnThresholdProperty = DependencyProperty.Register(
        nameof(WarnThreshold), typeof(double), typeof(CircularGauge),
        new PropertyMetadata(70d) { AffectsRender = true });

    /// <summary>The value where the red band starts.</summary>
    public static readonly DependencyProperty DangerThresholdProperty = DependencyProperty.Register(
        nameof(DangerThreshold), typeof(double), typeof(CircularGauge),
        new PropertyMetadata(90d) { AffectsRender = true });

    /// <summary>The unit label under the value.</summary>
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(CircularGauge),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>The label drawn above the dial.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(CircularGauge),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>The normal arc and needle colour.</summary>
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(XamlBrush), typeof(CircularGauge),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The amber band colour.</summary>
    public static readonly DependencyProperty WarnBrushProperty = DependencyProperty.Register(
        nameof(WarnBrush), typeof(XamlBrush), typeof(CircularGauge),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The red band colour.</summary>
    public static readonly DependencyProperty DangerBrushProperty = DependencyProperty.Register(
        nameof(DangerBrush), typeof(XamlBrush), typeof(CircularGauge),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The unfilled scale colour.</summary>
    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(XamlBrush), typeof(CircularGauge),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>Whether minor/major ticks and scale numbers are drawn.</summary>
    public static readonly DependencyProperty ShowTicksProperty = DependencyProperty.Register(
        nameof(ShowTicks), typeof(bool), typeof(CircularGauge),
        new PropertyMetadata(true) { AffectsRender = true });

    /// <summary>Creates a non-interactive gauge.</summary>
    public CircularGauge()
    {
        IsHitTestVisible = false;
        MinWidth = 100f;
        MinHeight = 100f;
        Accent = new SolidColorBrush(Color.FromArgb(0xFF, 0x25, 0xD0, 0xEC));
        WarnBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xB0, 0x20));
        DangerBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x3B, 0x4E));
        TrackBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x1A, 0x28, 0x34));
    }

    /// <summary>The lowest value.</summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty)!;
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>The highest value.</summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty)!;
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>The current value.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty)!;
        set => SetValue(ValueProperty, Math.Clamp(value, Minimum, Maximum));
    }

    /// <summary>The value where the amber band starts.</summary>
    public double WarnThreshold
    {
        get => (double)GetValue(WarnThresholdProperty)!;
        set => SetValue(WarnThresholdProperty, value);
    }

    /// <summary>The value where the red band starts.</summary>
    public double DangerThreshold
    {
        get => (double)GetValue(DangerThresholdProperty)!;
        set => SetValue(DangerThresholdProperty, value);
    }

    /// <summary>The unit label under the value.</summary>
    public string Unit
    {
        get => (string)GetValue(UnitProperty)!;
        set => SetValue(UnitProperty, value ?? string.Empty);
    }

    /// <summary>The label drawn above the dial.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value ?? string.Empty);
    }

    /// <summary>The normal arc and needle colour.</summary>
    public XamlBrush? Accent
    {
        get => (XamlBrush?)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>The amber band colour.</summary>
    public XamlBrush? WarnBrush
    {
        get => (XamlBrush?)GetValue(WarnBrushProperty);
        set => SetValue(WarnBrushProperty, value);
    }

    /// <summary>The red band colour.</summary>
    public XamlBrush? DangerBrush
    {
        get => (XamlBrush?)GetValue(DangerBrushProperty);
        set => SetValue(DangerBrushProperty, value);
    }

    /// <summary>The unfilled scale colour.</summary>
    public XamlBrush? TrackBrush
    {
        get => (XamlBrush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    /// <summary>Whether ticks and scale numbers are drawn.</summary>
    public bool ShowTicks
    {
        get => (bool)GetValue(ShowTicksProperty)!;
        set => SetValue(ShowTicksProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width is > 0 and < 100000 ? availableSize.Width : 180;
        var height = availableSize.Height is > 0 and < 100000 ? availableSize.Height : 180;
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 8f || height <= 8f)
        {
            return;
        }

        var size = Math.Min(width, height);
        var cx = width / 2f;
        var cy = height / 2f;
        var radius = (size / 2f) - (size * 0.10f);
        var arcThickness = Math.Max(4f, size * 0.055f);
        var arcRect = new RectangleF(cx - radius, cy - radius, radius * 2f, radius * 2f);

        var accent = HmiPaint.ColorOf(Accent, Color.FromArgb(0xFF, 0x25, 0xD0, 0xEC));
        var warn = HmiPaint.ColorOf(WarnBrush, Color.FromArgb(0xFF, 0xFF, 0xB0, 0x20));
        var danger = HmiPaint.ColorOf(DangerBrush, Color.FromArgb(0xFF, 0xFF, 0x3B, 0x4E));
        var track = HmiPaint.ColorOf(TrackBrush, Color.FromArgb(0xFF, 0x1A, 0x28, 0x34));
        var text = Color.FromArgb(0xFF, 0xD6, 0xE4, 0xF0);
        var muted = Color.FromArgb(0xFF, 0x74, 0x88, 0x9C);

        var range = Maximum - Minimum;
        var fraction = range <= 0d ? 0d : Math.Clamp((Value - Minimum) / range, 0d, 1d);
        const float start = 135f;
        const float sweep = 270f;

        // Track, then the coloured bands and the live arc.
        using (var trackPen = new Pen(track, arcThickness) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            graphics.DrawArc(trackPen, arcRect, start, sweep);
        }

        var warnFraction = range <= 0d ? 1d : Math.Clamp((WarnThreshold - Minimum) / range, 0d, 1d);
        var dangerFraction = range <= 0d ? 1d : Math.Clamp((DangerThreshold - Minimum) / range, 0d, 1d);
        DrawBand(graphics, arcRect, start, sweep, 0d, warnFraction, HmiPaint.Fade(accent, 0.22), arcThickness);
        DrawBand(graphics, arcRect, start, sweep, warnFraction, dangerFraction, HmiPaint.Fade(warn, 0.55), arcThickness);
        DrawBand(graphics, arcRect, start, sweep, dangerFraction, 1d, HmiPaint.Fade(danger, 0.55), arcThickness);

        var valueColor = fraction >= dangerFraction ? danger : fraction >= warnFraction ? warn : accent;
        HmiPaint.GlowArc(graphics, arcRect, start, sweep * (float)fraction, valueColor, arcThickness);

        if (ShowTicks)
        {
            for (var index = 0; index <= 20; index++)
            {
                var major = index % 4 == 0;
                var angle = start + ((sweep / 20f) * index);
                var outer = HmiPaint.PointOn(cx, cy, radius - (arcThickness * 0.85f), angle);
                var inner = HmiPaint.PointOn(cx, cy, radius - (arcThickness * 0.85f) - (major ? size * 0.045f : size * 0.025f), angle);
                using var pen = new Pen(major ? muted : HmiPaint.Fade(muted, 0.5), major ? 1.6f : 1f);
                graphics.DrawLine(pen, outer, inner);

                if (major && index % 8 == 0)
                {
                    var label = Math.Round(Minimum + (range * index / 20d)).ToString("0");
                    var point = HmiPaint.PointOn(cx, cy, radius - (arcThickness * 0.85f) - (size * 0.12f), angle);
                    HmiPaint.Text(
                        graphics, label, HmiPaint.Mono(Math.Max(7f, size * 0.06f)), muted,
                        new RectangleF(point.X - (size * 0.1f), point.Y - (size * 0.06f), size * 0.2f, size * 0.12f),
                        StringAlignment.Center, StringAlignment.Center);
                }
            }
        }

        // Needle.
        var needleAngle = start + (sweep * (float)fraction);
        var tip = HmiPaint.PointOn(cx, cy, radius * 0.72f, needleAngle);
        var tail = HmiPaint.PointOn(cx, cy, radius * 0.16f, needleAngle + 180f);
        using (var haloPen = new Pen(HmiPaint.Fade(valueColor, 0.25), Math.Max(5f, size * 0.05f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            graphics.DrawLine(haloPen, tail, tip);
        }

        using (var needlePen = new Pen(valueColor, Math.Max(2f, size * 0.02f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            graphics.DrawLine(needlePen, tail, tip);
        }

        var hubRadius = size * 0.055f;
        using (var hubBrush = new SolidBrush(Color.FromArgb(0xFF, 0x0B, 0x11, 0x18)))
        {
            graphics.FillEllipse(hubBrush, cx - hubRadius, cy - hubRadius, hubRadius * 2f, hubRadius * 2f);
        }
        using (var hubPen = new Pen(HmiPaint.Fade(valueColor, 0.9), 2f))
        {
            graphics.DrawEllipse(hubPen, cx - hubRadius, cy - hubRadius, hubRadius * 2f, hubRadius * 2f);
        }

        // Labels: title above, value and unit in the lower half.
        if (!string.IsNullOrEmpty(Title))
        {
            HmiPaint.Text(
                graphics, Title, HmiPaint.Ui(Math.Max(7f, size * 0.062f)), muted,
                new RectangleF(0f, size * 0.01f, width, size * 0.085f),
                StringAlignment.Center, StringAlignment.Center);
        }

        HmiPaint.Text(
            graphics, Value.ToString("0.0"), HmiPaint.Mono(Math.Max(12f, size * 0.19f), FontStyle.Bold), text,
            new RectangleF(cx - (size * 0.35f), cy + (size * 0.20f), size * 0.7f, size * 0.22f),
            StringAlignment.Center, StringAlignment.Center);

        if (!string.IsNullOrEmpty(Unit))
        {
            HmiPaint.Text(
                graphics, Unit, HmiPaint.Ui(Math.Max(7f, size * 0.07f)), muted,
                new RectangleF(cx - (size * 0.35f), cy + (size * 0.40f), size * 0.7f, size * 0.12f),
                StringAlignment.Center, StringAlignment.Center);
        }
    }

    private static void DrawBand(
        Graphics graphics,
        RectangleF rect,
        float start,
        float sweep,
        double from,
        double to,
        Color color,
        float thickness)
    {
        if (to <= from || color.A == 0)
        {
            return;
        }

        using var pen = new Pen(color, thickness) { StartCap = LineCap.Flat, EndCap = LineCap.Flat };
        graphics.DrawArc(pen, rect, start + (sweep * (float)from), sweep * (float)(to - from));
    }
}
