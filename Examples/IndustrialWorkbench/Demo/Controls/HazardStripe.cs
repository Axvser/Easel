using System.Drawing;
using Pen = System.Drawing.Pen;
using LineCap = System.Drawing.Drawing2D.LineCap;
using Easel;
using Easel.Visuals;
using XamlBrush = Easel.Brush;

namespace IndustrialWorkbench.Controls;

/// <summary>A diagonal hazard-stripe bar; the stripes scroll when <see cref="Phase"/> advances.</summary>
public sealed class HazardStripe : VisualElement
{
    /// <summary>A monotonically increasing phase that scrolls the stripes.</summary>
    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(double), typeof(HazardStripe),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>The stripe colour.</summary>
    public static readonly DependencyProperty StripeBrushProperty = DependencyProperty.Register(
        nameof(StripeBrush), typeof(XamlBrush), typeof(HazardStripe),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The background colour.</summary>
    public static readonly DependencyProperty BaseBrushProperty = DependencyProperty.Register(
        nameof(BaseBrush), typeof(XamlBrush), typeof(HazardStripe),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The stripe width in DIPs.</summary>
    public static readonly DependencyProperty StripeWidthProperty = DependencyProperty.Register(
        nameof(StripeWidth), typeof(double), typeof(HazardStripe),
        new PropertyMetadata(10d) { AffectsRender = true });

    /// <summary>The diagonal angle in degrees.</summary>
    public static readonly DependencyProperty AngleProperty = DependencyProperty.Register(
        nameof(Angle), typeof(double), typeof(HazardStripe),
        new PropertyMetadata(45d) { AffectsRender = true });

    /// <summary>Creates a non-interactive decoration.</summary>
    public HazardStripe()
    {
        IsHitTestVisible = false;
        MinHeight = 8f;
        StripeBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xB0, 0x20));
        BaseBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x0B, 0x11, 0x18));
    }

    /// <summary>Drives the scroll.</summary>
    public double Phase
    {
        get => (double)GetValue(PhaseProperty)!;
        set => SetValue(PhaseProperty, value);
    }

    /// <summary>The stripe colour.</summary>
    public XamlBrush? StripeBrush
    {
        get => (XamlBrush?)GetValue(StripeBrushProperty);
        set => SetValue(StripeBrushProperty, value);
    }

    /// <summary>The background colour.</summary>
    public XamlBrush? BaseBrush
    {
        get => (XamlBrush?)GetValue(BaseBrushProperty);
        set => SetValue(BaseBrushProperty, value);
    }

    /// <summary>The stripe width in DIPs.</summary>
    public double StripeWidth
    {
        get => (double)GetValue(StripeWidthProperty)!;
        set => SetValue(StripeWidthProperty, Math.Max(2d, value));
    }

    /// <summary>The diagonal angle in degrees.</summary>
    public double Angle
    {
        get => (double)GetValue(AngleProperty)!;
        set => SetValue(AngleProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width is > 0 and < 100000 ? availableSize.Width : 200;
        var height = availableSize.Height is > 0 and < 100000 ? availableSize.Height : 12;
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 2f || height <= 2f)
        {
            return;
        }

        var stripe = HmiPaint.ColorOf(StripeBrush, Color.FromArgb(0xFF, 0xFF, 0xB0, 0x20));
        var background = HmiPaint.ColorOf(BaseBrush, Color.FromArgb(0xFF, 0x0B, 0x11, 0x18));
        var bandWidth = Math.Max(2f, (float)StripeWidth);
        var period = bandWidth * 2f;
        var offset = (float)((Phase * 14d) % period);

        using (var bg = new SolidBrush(background))
        {
            graphics.FillRectangle(bg, 0f, 0f, width, height);
        }

        var state = graphics.Save();
        graphics.SetClip(new RectangleF(0f, 0f, width, height));
        try
        {
            using var pen = new Pen(HmiPaint.Fade(stripe, 0.85), bandWidth);
            var span = width + height + period;
            for (var x = -height - period + offset; x < span; x += period)
            {
                graphics.DrawLine(pen, x, height, x + height, 0f);
            }
        }
        finally
        {
            graphics.Restore(state);
        }

        using (var top = new Pen(HmiPaint.Fade(Color.White, 0.06), 1f))
        {
            graphics.DrawLine(top, 0f, 0.5f, width, 0.5f);
        }
    }
}
