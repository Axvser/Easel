using System.Drawing;
using Pen = System.Drawing.Pen;
using LineCap = System.Drawing.Drawing2D.LineCap;
using System.Drawing.Drawing2D;
using Easel;
using Easel.Visuals;
using XamlBrush = Easel.Brush;

namespace IndustrialWorkbench.Controls;

/// <summary>An industrial indicator lamp with a domed highlight, an outer halo, an optional blink and a caption.</summary>
public sealed class LedLamp : VisualElement
{
    /// <summary>Whether the lamp is lit.</summary>
    public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
        nameof(IsOn), typeof(bool), typeof(LedLamp),
        new PropertyMetadata(true) { AffectsRender = true });

    /// <summary>Whether a lit lamp pulses.</summary>
    public static readonly DependencyProperty BlinkProperty = DependencyProperty.Register(
        nameof(Blink), typeof(bool), typeof(LedLamp),
        new PropertyMetadata(false) { AffectsRender = true });

    /// <summary>The lamp colour.</summary>
    public static readonly DependencyProperty LampColorProperty = DependencyProperty.Register(
        nameof(LampColor), typeof(XamlBrush), typeof(LedLamp),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The caption under the lamp; empty draws the dome only.</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(LedLamp),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>A monotonically increasing phase that drives the blink and the halo shimmer.</summary>
    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(double), typeof(LedLamp),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>Creates a non-interactive lamp.</summary>
    public LedLamp()
    {
        IsHitTestVisible = false;
        LampColor = new SolidColorBrush(Color.FromArgb(0xFF, 0x32, 0xE0, 0xA2));
    }

    /// <summary>Whether the lamp is lit.</summary>
    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty)!;
        set => SetValue(IsOnProperty, value);
    }

    /// <summary>Whether a lit lamp pulses.</summary>
    public bool Blink
    {
        get => (bool)GetValue(BlinkProperty)!;
        set => SetValue(BlinkProperty, value);
    }

    /// <summary>The lamp colour.</summary>
    public XamlBrush? LampColor
    {
        get => (XamlBrush?)GetValue(LampColorProperty);
        set => SetValue(LampColorProperty, value);
    }

    /// <summary>The caption under the lamp.</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty)!;
        set => SetValue(LabelProperty, value ?? string.Empty);
    }

    /// <summary>Drives the blink and halo shimmer.</summary>
    public double Phase
    {
        get => (double)GetValue(PhaseProperty)!;
        set => SetValue(PhaseProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width is > 0 and < 100000 ? availableSize.Width : 48;
        var height = availableSize.Height is > 0 and < 100000 ? availableSize.Height : 56;
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 4f || height <= 4f)
        {
            return;
        }

        var hasLabel = !string.IsNullOrEmpty(Label);
        var labelHeight = hasLabel ? Math.Min(height * 0.26f, 18f) : 0f;
        var diameter = Math.Min(width, height - labelHeight);
        var radius = diameter / 2f;
        var cx = width / 2f;
        var cy = (height - labelHeight) / 2f;

        var color = HmiPaint.ColorOf(LampColor, Color.FromArgb(0xFF, 0x32, 0xE0, 0xA2));
        var lit = IsOn;
        if (Blink && lit)
        {
            lit = (Math.Sin(Phase) * 0.5) + 0.5 > 0.35;
        }

        // Bezel.
        var baseColor = Color.FromArgb(0xFF, 0x0D, 0x14, 0x1C);
        using (var bezel = new SolidBrush(baseColor))
        {
            graphics.FillEllipse(bezel, cx - radius, cy - radius, diameter, diameter);
        }
        using (var bezelRing = new Pen(Color.FromArgb(0xFF, 0x2A, 0x3A, 0x48), Math.Max(1f, radius * 0.09f)))
        {
            graphics.DrawEllipse(bezelRing, cx - radius, cy - radius, diameter, diameter);
        }

        var dome = diameter * 0.74f;
        var domeRadius = dome / 2f;

        // Halo when lit.
        if (lit)
        {
            for (var layer = 3; layer >= 1; layer--)
            {
                using var halo = new SolidBrush(HmiPaint.Fade(color, 0.05 * layer));
                var haloDiameter = dome + (layer * diameter * 0.12f);
                graphics.FillEllipse(halo, cx - (haloDiameter / 2f), cy - (haloDiameter / 2f), haloDiameter, haloDiameter);
            }
        }

        // Dome with a radial highlight.
        using (var path = new GraphicsPath())
        {
            var domeRect = new RectangleF(cx - domeRadius, cy - domeRadius, dome, dome);
            path.AddEllipse(domeRect);
            using var gradient = new PathGradientBrush(path)
            {
                CenterPoint = new PointF(cx - (domeRadius * 0.3f), cy - (domeRadius * 0.35f)),
                CenterColor = lit ? HmiPaint.Mix(color, Color.White, 0.55) : HmiPaint.Fade(color, 0.28),
                SurroundColors = [lit ? color : HmiPaint.Fade(color, 0.15)],
            };
            graphics.FillPath(gradient, path);
        }

        using (var rim = new Pen(HmiPaint.Fade(Color.White, lit ? 0.35 : 0.10), Math.Max(1f, dome * 0.03f)))
        {
            graphics.DrawEllipse(rim, cx - domeRadius, cy - domeRadius, dome, dome);
        }

        // Specular dot.
        var spec = dome * 0.16f;
        using (var specBrush = new SolidBrush(HmiPaint.Fade(Color.White, lit ? 0.7 : 0.16)))
        {
            graphics.FillEllipse(specBrush, cx - (domeRadius * 0.42f), cy - (domeRadius * 0.46f), spec, spec);
        }

        if (hasLabel)
        {
            HmiPaint.Text(
                graphics, Label, HmiPaint.Ui(Math.Max(6f, labelHeight * 0.66f)),
                Color.FromArgb(0xFF, 0x9F, 0xB2, 0xC4),
                new RectangleF(0f, height - labelHeight, width, labelHeight),
                StringAlignment.Center, StringAlignment.Center);
        }
    }
}
