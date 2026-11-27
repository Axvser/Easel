using System.Drawing;
using Pen = System.Drawing.Pen;
using LineCap = System.Drawing.Drawing2D.LineCap;
using Easel;
using Easel.Visuals;
using XamlBrush = Easel.Brush;

namespace IndustrialWorkbench.Controls;

/// <summary>A horizontal bar meter with coloured bands, a fill and a scale beneath it.</summary>
public sealed class LinearGauge : VisualElement
{
    /// <summary>The current value.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(LinearGauge),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>The lowest value.</summary>
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(LinearGauge),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>The highest value.</summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(LinearGauge),
        new PropertyMetadata(100d) { AffectsRender = true });

    /// <summary>The value where the amber band starts.</summary>
    public static readonly DependencyProperty WarnThresholdProperty = DependencyProperty.Register(
        nameof(WarnThreshold), typeof(double), typeof(LinearGauge),
        new PropertyMetadata(70d) { AffectsRender = true });

    /// <summary>The value where the red band starts.</summary>
    public static readonly DependencyProperty DangerThresholdProperty = DependencyProperty.Register(
        nameof(DangerThreshold), typeof(double), typeof(LinearGauge),
        new PropertyMetadata(90d) { AffectsRender = true });

    /// <summary>The unit label.</summary>
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(LinearGauge),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>The label above the bar.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(LinearGauge),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>The normal colour.</summary>
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(XamlBrush), typeof(LinearGauge),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The amber colour.</summary>
    public static readonly DependencyProperty WarnBrushProperty = DependencyProperty.Register(
        nameof(WarnBrush), typeof(XamlBrush), typeof(LinearGauge),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The red colour.</summary>
    public static readonly DependencyProperty DangerBrushProperty = DependencyProperty.Register(
        nameof(DangerBrush), typeof(XamlBrush), typeof(LinearGauge),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>Creates a non-interactive meter.</summary>
    public LinearGauge()
    {
        IsHitTestVisible = false;
        MinHeight = 40f;
        Accent = new SolidColorBrush(Color.FromArgb(0xFF, 0x25, 0xD0, 0xEC));
        WarnBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xB0, 0x20));
        DangerBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x3B, 0x4E));
    }

    /// <summary>The current value.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty)!;
        set => SetValue(ValueProperty, Math.Clamp(value, Minimum, Maximum));
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

    /// <summary>The unit label.</summary>
    public string Unit
    {
        get => (string)GetValue(UnitProperty)!;
        set => SetValue(UnitProperty, value ?? string.Empty);
    }

    /// <summary>The label above the bar.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value ?? string.Empty);
    }

    /// <summary>The normal colour.</summary>
    public XamlBrush? Accent
    {
        get => (XamlBrush?)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>The amber colour.</summary>
    public XamlBrush? WarnBrush
    {
        get => (XamlBrush?)GetValue(WarnBrushProperty);
        set => SetValue(WarnBrushProperty, value);
    }

    /// <summary>The red colour.</summary>
    public XamlBrush? DangerBrush
    {
        get => (XamlBrush?)GetValue(DangerBrushProperty);
        set => SetValue(DangerBrushProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width is > 0 and < 100000 ? availableSize.Width : 220;
        var height = availableSize.Height is > 0 and < 100000 ? availableSize.Height : 54;
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 16f || height <= 12f)
        {
            return;
        }

        var accent = HmiPaint.ColorOf(Accent, Color.FromArgb(0xFF, 0x25, 0xD0, 0xEC));
        var warn = HmiPaint.ColorOf(WarnBrush, Color.FromArgb(0xFF, 0xFF, 0xB0, 0x20));
        var danger = HmiPaint.ColorOf(DangerBrush, Color.FromArgb(0xFF, 0xFF, 0x3B, 0x4E));
        var muted = Color.FromArgb(0xFF, 0x74, 0x88, 0x9C);
        var text = Color.FromArgb(0xFF, 0xD6, 0xE4, 0xF0);

        var headerHeight = Math.Min(16f, height * 0.30f);
        var scaleHeight = Math.Min(12f, height * 0.22f);
        var barHeight = Math.Max(6f, height - headerHeight - scaleHeight - 6f);
        var bar = new RectangleF(2f, headerHeight + 2f, Math.Max(4f, width - 4f), barHeight);
        var range = Maximum - Minimum;
        var fraction = range <= 0d ? 0d : Math.Clamp((Value - Minimum) / range, 0d, 1d);

        // Track and bands.
        HmiPaint.FillRounded(graphics, Color.FromArgb(0xFF, 0x08, 0x0E, 0x14), bar, 2.5f);
        var warnFraction = range <= 0d ? 1d : Math.Clamp((WarnThreshold - Minimum) / range, 0d, 1d);
        var dangerFraction = range <= 0d ? 1d : Math.Clamp((DangerThreshold - Minimum) / range, 0d, 1d);
        DrawBand(graphics, bar, warnFraction, dangerFraction, HmiPaint.Fade(warn, 0.16));
        DrawBand(graphics, bar, dangerFraction, 1d, HmiPaint.Fade(danger, 0.18));

        var fillColor = fraction >= dangerFraction ? danger : fraction >= warnFraction ? warn : accent;
        var fillWidth = bar.Width * (float)fraction;
        if (fillWidth > 0f)
        {
            var fill = new RectangleF(bar.Left, bar.Top, fillWidth, bar.Height);
            using (var glow = new SolidBrush(HmiPaint.Fade(fillColor, 0.22)))
            {
                graphics.FillRectangle(glow, new RectangleF(fill.Left, fill.Top - 1.5f, fill.Width, fill.Height + 3f));
            }
            HmiPaint.FillRounded(graphics, fillColor, fill, 2f);
        }

        HmiPaint.DrawRounded(graphics, HmiPaint.Fade(muted, 0.6), bar, 2.5f, 1f);

        // Ticks.
        var scaleY = bar.Bottom + 2f;
        for (var index = 0; index <= 10; index++)
        {
            var major = index % 5 == 0;
            var x = bar.Left + (bar.Width * index / 10f);
            using var pen = new Pen(major ? muted : HmiPaint.Fade(muted, 0.5), major ? 1.3f : 1f);
            graphics.DrawLine(pen, x, scaleY, x, scaleY + (major ? scaleHeight * 0.55f : scaleHeight * 0.3f));
            if (major)
            {
                var label = (Minimum + (range * index / 10d)).ToString("0");
                HmiPaint.Text(graphics, label, HmiPaint.Mono(Math.Max(5f, scaleHeight * 0.7f)), muted,
                    new RectangleF(x - 14f, scaleY + (scaleHeight * 0.35f), 28f, scaleHeight * 0.65f),
                    StringAlignment.Center, StringAlignment.Center);
            }
        }

        // Value marker above the fill head.
        var markerX = bar.Left + fillWidth;
        using (var marker = new SolidBrush(fillColor))
        {
            graphics.FillPolygon(marker,
            [
                new PointF(markerX, bar.Top - 4f),
                new PointF(markerX - 4f, bar.Top - 9f),
                new PointF(markerX + 4f, bar.Top - 9f),
            ]);
        }

        if (!string.IsNullOrEmpty(Title))
        {
            HmiPaint.Text(graphics, Title, HmiPaint.Ui(Math.Max(7f, headerHeight * 0.7f)), muted,
                new RectangleF(2f, 0f, width * 0.6f, headerHeight), StringAlignment.Near, StringAlignment.Center);
        }

        var valueText = Value.ToString("0.0") + (string.IsNullOrEmpty(Unit) ? string.Empty : " " + Unit);
        HmiPaint.Text(graphics, valueText, HmiPaint.Mono(Math.Max(7f, headerHeight * 0.72f), FontStyle.Bold), text,
            new RectangleF(width * 0.4f, 0f, (width * 0.6f) - 2f, headerHeight), StringAlignment.Far, StringAlignment.Center);
    }

    private static void DrawBand(Graphics graphics, RectangleF bar, double from, double to, Color color)
    {
        if (to <= from || color.A == 0)
        {
            return;
        }

        var rect = new RectangleF(
            bar.Left + (bar.Width * (float)from),
            bar.Top,
            bar.Width * (float)(to - from),
            bar.Height);
        using var brush = new SolidBrush(color);
        graphics.FillRectangle(brush, rect);
    }
}
