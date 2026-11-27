using System.Drawing;
using Pen = System.Drawing.Pen;
using LineCap = System.Drawing.Drawing2D.LineCap;
using System.Drawing.Drawing2D;
using Easel;
using Easel.Visuals;
using XamlBrush = Easel.Brush;

namespace IndustrialWorkbench.Controls;

/// <summary>A motor / fan with rotating blades, a housing, an rpm readout and a running halo.</summary>
public sealed class MotorFan : VisualElement
{
    /// <summary>The current blade rotation in degrees.</summary>
    public static readonly DependencyProperty AngleProperty = DependencyProperty.Register(
        nameof(Angle), typeof(double), typeof(MotorFan),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>The displayed rotation speed in rpm.</summary>
    public static readonly DependencyProperty SpeedProperty = DependencyProperty.Register(
        nameof(Speed), typeof(double), typeof(MotorFan),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>Whether the fan is driven.</summary>
    public static readonly DependencyProperty IsRunningProperty = DependencyProperty.Register(
        nameof(IsRunning), typeof(bool), typeof(MotorFan),
        new PropertyMetadata(true) { AffectsRender = true });

    /// <summary>The accent colour.</summary>
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(XamlBrush), typeof(MotorFan),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The label shown under the fan.</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(MotorFan),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>Creates a non-interactive fan.</summary>
    public MotorFan()
    {
        IsHitTestVisible = false;
        MinWidth = 90f;
        MinHeight = 100f;
        Accent = new SolidColorBrush(Color.FromArgb(0xFF, 0x25, 0xD0, 0xEC));
    }

    /// <summary>The current blade rotation in degrees.</summary>
    public double Angle
    {
        get => (double)GetValue(AngleProperty)!;
        set => SetValue(AngleProperty, value);
    }

    /// <summary>The displayed rotation speed in rpm.</summary>
    public double Speed
    {
        get => (double)GetValue(SpeedProperty)!;
        set => SetValue(SpeedProperty, value);
    }

    /// <summary>Whether the fan is driven.</summary>
    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty)!;
        set => SetValue(IsRunningProperty, value);
    }

    /// <summary>The accent colour.</summary>
    public XamlBrush? Accent
    {
        get => (XamlBrush?)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>The label shown under the fan.</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty)!;
        set => SetValue(LabelProperty, value ?? string.Empty);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width is > 0 and < 100000 ? availableSize.Width : 110;
        var height = availableSize.Height is > 0 and < 100000 ? availableSize.Height : 120;
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 12f || height <= 12f)
        {
            return;
        }

        var accent = HmiPaint.ColorOf(Accent, Color.FromArgb(0xFF, 0x25, 0xD0, 0xEC));
        var running = IsRunning;
        var muted = Color.FromArgb(0xFF, 0x74, 0x88, 0x9C);
        var hasLabel = !string.IsNullOrEmpty(Label);
        var labelHeight = hasLabel ? Math.Min(16f, height * 0.14f) : 0f;
        var size = Math.Min(width, height - labelHeight);
        var cx = width / 2f;
        var cy = (height - labelHeight) / 2f;
        var outer = (size / 2f) - 3f;
        var bladeColor = running ? accent : HmiPaint.Fade(muted, 0.5);

        // Housing.
        using (var housing = new SolidBrush(Color.FromArgb(0xFF, 0x0B, 0x12, 0x19)))
        {
            graphics.FillEllipse(housing, cx - outer, cy - outer, outer * 2f, outer * 2f);
        }

        if (running)
        {
            for (var layer = 3; layer >= 1; layer--)
            {
                using var halo = new Pen(HmiPaint.Fade(accent, 0.06 * layer), 2f + (layer * 2f));
                graphics.DrawEllipse(halo, cx - outer, cy - outer, outer * 2f, outer * 2f);
            }
        }

        using (var rim = new Pen(Color.FromArgb(0xFF, 0x2A, 0x3C, 0x4C), Math.Max(1.2f, size * 0.03f)))
        {
            graphics.DrawEllipse(rim, cx - outer, cy - outer, outer * 2f, outer * 2f);
        }

        // Blades.
        var state = graphics.Save();
        graphics.TranslateTransform(cx, cy);
        graphics.RotateTransform((float)Angle);
        using (var blade = new GraphicsPath())
        {
            blade.AddEllipse(outer * 0.12f, -outer * 0.10f, outer * 0.70f, outer * 0.20f);
            using var bladeBrush = new SolidBrush(HmiPaint.Fade(bladeColor, running ? 0.85 : 0.45));
            using var bladePen = new Pen(HmiPaint.Fade(bladeColor, running ? 0.45 : 0.2), 1f);
            for (var index = 0; index < 6; index++)
            {
                graphics.RotateTransform(60f);
                graphics.FillPath(bladeBrush, blade);
                graphics.DrawPath(bladePen, blade);
            }
        }

        graphics.Restore(state);

        // Hub.
        var hub = size * 0.13f;
        using (var hubBrush = new SolidBrush(Color.FromArgb(0xFF, 0x16, 0x22, 0x2C)))
        {
            graphics.FillEllipse(hubBrush, cx - hub, cy - hub, hub * 2f, hub * 2f);
        }
        using (var hubPen = new Pen(HmiPaint.Fade(bladeColor, 0.9), 1.4f))
        {
            graphics.DrawEllipse(hubPen, cx - hub, cy - hub, hub * 2f, hub * 2f);
        }

        // Speed readout.
        HmiPaint.Text(graphics, Speed.ToString("0"), HmiPaint.Mono(Math.Max(8f, size * 0.14f), FontStyle.Bold),
            running ? Color.FromArgb(0xFF, 0xD6, 0xE4, 0xF0) : muted,
            new RectangleF(cx - (size * 0.4f), cy + (size * 0.60f), size * 0.8f, size * 0.14f),
            StringAlignment.Center, StringAlignment.Center);
        HmiPaint.Text(graphics, "RPM", HmiPaint.Ui(Math.Max(5f, size * 0.07f)), muted,
            new RectangleF(cx - (size * 0.4f), cy + (size * 0.74f), size * 0.8f, size * 0.10f),
            StringAlignment.Center, StringAlignment.Center);

        if (hasLabel)
        {
            HmiPaint.Text(graphics, Label, HmiPaint.Ui(Math.Max(7f, labelHeight * 0.7f)), muted,
                new RectangleF(0f, height - labelHeight, width, labelHeight), StringAlignment.Center, StringAlignment.Center);
        }
    }
}
