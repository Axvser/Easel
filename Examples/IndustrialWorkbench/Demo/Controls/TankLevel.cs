using System.Drawing;
using Pen = System.Drawing.Pen;
using LineCap = System.Drawing.Drawing2D.LineCap;
using GdiLinearGradientBrush = System.Drawing.Drawing2D.LinearGradientBrush;
using System.Drawing.Drawing2D;
using Easel;
using Easel.Visuals;
using XamlBrush = Easel.Brush;

namespace IndustrialWorkbench.Controls;

/// <summary>A vertical tank level: rounded vessel, rippling liquid surface, scale ticks and a percentage readout.</summary>
public sealed class TankLevel : VisualElement
{
    /// <summary>The fill percentage (0..100).</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(TankLevel),
        new PropertyMetadata(50d) { AffectsRender = true });

    /// <summary>The label above the tank.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(TankLevel),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>The unit shown with the percentage.</summary>
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(TankLevel),
        new PropertyMetadata("%") { AffectsRender = true });

    /// <summary>The low-level alarm limit.</summary>
    public static readonly DependencyProperty LowLimitProperty = DependencyProperty.Register(
        nameof(LowLimit), typeof(double), typeof(TankLevel),
        new PropertyMetadata(15d) { AffectsRender = true });

    /// <summary>The high-level alarm limit.</summary>
    public static readonly DependencyProperty HighLimitProperty = DependencyProperty.Register(
        nameof(HighLimit), typeof(double), typeof(TankLevel),
        new PropertyMetadata(85d) { AffectsRender = true });

    /// <summary>The liquid colour.</summary>
    public static readonly DependencyProperty LiquidBrushProperty = DependencyProperty.Register(
        nameof(LiquidBrush), typeof(XamlBrush), typeof(TankLevel),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The amber colour used outside the limits.</summary>
    public static readonly DependencyProperty WarnBrushProperty = DependencyProperty.Register(
        nameof(WarnBrush), typeof(XamlBrush), typeof(TankLevel),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>A monotonically increasing phase that drives the surface ripple.</summary>
    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(double), typeof(TankLevel),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>Creates a non-interactive tank.</summary>
    public TankLevel()
    {
        IsHitTestVisible = false;
        MinWidth = 90f;
        MinHeight = 140f;
        LiquidBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x25, 0xD0, 0xEC));
        WarnBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xB0, 0x20));
    }

    /// <summary>The fill percentage (0..100).</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty)!;
        set => SetValue(ValueProperty, Math.Clamp(value, 0d, 100d));
    }

    /// <summary>The label above the tank.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value ?? string.Empty);
    }

    /// <summary>The unit shown with the percentage.</summary>
    public string Unit
    {
        get => (string)GetValue(UnitProperty)!;
        set => SetValue(UnitProperty, value ?? string.Empty);
    }

    /// <summary>The low-level alarm limit.</summary>
    public double LowLimit
    {
        get => (double)GetValue(LowLimitProperty)!;
        set => SetValue(LowLimitProperty, value);
    }

    /// <summary>The high-level alarm limit.</summary>
    public double HighLimit
    {
        get => (double)GetValue(HighLimitProperty)!;
        set => SetValue(HighLimitProperty, value);
    }

    /// <summary>The liquid colour.</summary>
    public XamlBrush? LiquidBrush
    {
        get => (XamlBrush?)GetValue(LiquidBrushProperty);
        set => SetValue(LiquidBrushProperty, value);
    }

    /// <summary>The amber colour used outside the limits.</summary>
    public XamlBrush? WarnBrush
    {
        get => (XamlBrush?)GetValue(WarnBrushProperty);
        set => SetValue(WarnBrushProperty, value);
    }

    /// <summary>Drives the surface ripple.</summary>
    public double Phase
    {
        get => (double)GetValue(PhaseProperty)!;
        set => SetValue(PhaseProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width is > 0 and < 100000 ? availableSize.Width : 110;
        var height = availableSize.Height is > 0 and < 100000 ? availableSize.Height : 180;
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 12f || height <= 20f)
        {
            return;
        }

        var titleHeight = string.IsNullOrEmpty(Title) ? 0f : Math.Min(18f, height * 0.11f);
        var scaleWidth = Math.Min(30f, width * 0.26f);
        var tank = new RectangleF(6f, titleHeight + 4f, Math.Max(10f, width - scaleWidth - 10f), Math.Max(20f, height - titleHeight - 10f));
        var radius = Math.Min(tank.Width, tank.Height) * 0.12f;

        var liquid = HmiPaint.ColorOf(LiquidBrush, Color.FromArgb(0xFF, 0x25, 0xD0, 0xEC));
        var warn = HmiPaint.ColorOf(WarnBrush, Color.FromArgb(0xFF, 0xFF, 0xB0, 0x20));
        var outOfRange = Value < LowLimit || Value > HighLimit;
        if (outOfRange)
        {
            liquid = warn;
        }

        var text = Color.FromArgb(0xFF, 0xD6, 0xE4, 0xF0);
        var muted = Color.FromArgb(0xFF, 0x74, 0x88, 0x9C);

        // Vessel shell.
        using (var shell = HmiPaint.RoundedRect(tank, radius))
        {
            using var fill = new SolidBrush(Color.FromArgb(0xFF, 0x07, 0x0C, 0x12));
            graphics.FillPath(fill, shell);
        }

        var fraction = (float)Math.Clamp(Value / 100d, 0d, 1d);
        var levelTop = tank.Bottom - (tank.Height * fraction);

        using (var clip = HmiPaint.RoundedRect(tank, radius))
        {
            var state = graphics.Save();
            graphics.SetClip(clip, CombineMode.Intersect);
            try
            {
                // Liquid body.
                var bodyRect = new RectangleF(tank.Left, levelTop, tank.Width, tank.Bottom - levelTop);
                using (var body = new GdiLinearGradientBrush(
                           new PointF(tank.Left, levelTop),
                           new PointF(tank.Left, tank.Bottom),
                           HmiPaint.Fade(liquid, 0.85),
                           HmiPaint.Fade(HmiPaint.Mix(liquid, Color.Black, 0.55), 0.9)))
                {
                    graphics.FillRectangle(body, bodyRect);
                }

                // Rippled surface.
                using var wave = new GraphicsPath();
                var amplitude = Math.Max(1.5f, tank.Width * 0.018f);
                var step = Math.Max(4f, tank.Width / 24f);
                wave.AddLine(tank.Left, tank.Bottom, tank.Left, levelTop);
                for (var x = tank.Left; x <= tank.Right; x += step)
                {
                    var waveY = levelTop + ((float)Math.Sin((x * 0.09f) + Phase) * amplitude);
                    wave.AddLine(x, waveY, Math.Min(x + step, tank.Right), waveY);
                }

                wave.AddLine(tank.Right, levelTop, tank.Right, tank.Bottom);
                wave.CloseFigure();
                using (var surface = new SolidBrush(HmiPaint.Fade(HmiPaint.Mix(liquid, Color.White, 0.25), 0.9)))
                {
                    graphics.FillPath(surface, wave);
                }

                // Surface highlight line.
                var centerX = tank.Left + (tank.Width / 2f);
                var lightY = levelTop + ((float)Math.Sin((centerX * 0.09f) + Phase) * amplitude);
                using (var line = new Pen(HmiPaint.Fade(Color.White, 0.55), 1.2f))
                {
                    graphics.DrawLine(line, tank.Left + 3f, lightY, tank.Right - 3f, lightY);
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        HmiPaint.DrawRounded(graphics, HmiPaint.Fade(muted, 0.8), tank, radius, 1.6f);

        // Scale on the right.
        var scaleX = tank.Right + 6f;
        for (var index = 0; index <= 10; index++)
        {
            var major = index % 5 == 0;
            var y = tank.Bottom - (tank.Height * index / 10f);
            using var pen = new Pen(major ? muted : HmiPaint.Fade(muted, 0.5), major ? 1.4f : 1f);
            graphics.DrawLine(pen, scaleX, y, scaleX + (major ? 8f : 4f), y);
            if (major)
            {
                HmiPaint.Text(
                    graphics, (index * 10).ToString(), HmiPaint.Mono(Math.Max(6f, height * 0.045f)), muted,
                    new RectangleF(scaleX + 9f, y - 7f, scaleWidth - 9f, 14f),
                    StringAlignment.Near, StringAlignment.Center);
            }
        }

        // Level band across the tank at the current height.
        using (var band = new Pen(HmiPaint.Fade(liquid, 0.9), 1.2f))
        {
            graphics.DrawLine(band, tank.Left + 2f, levelTop, tank.Right - 2f, levelTop);
        }

        // Percentage readout floating in the vessel.
        HmiPaint.Text(
            graphics, Value.ToString("0"), HmiPaint.Mono(Math.Max(12f, Math.Min(width, height) * 0.17f), FontStyle.Bold), text,
            new RectangleF(tank.Left, tank.Top + (tank.Height * 0.36f), tank.Width, tank.Height * 0.2f),
            StringAlignment.Center, StringAlignment.Center);
        if (!string.IsNullOrEmpty(Unit))
        {
            HmiPaint.Text(
                graphics, Unit, HmiPaint.Ui(Math.Max(7f, height * 0.055f)), muted,
                new RectangleF(tank.Left, tank.Top + (tank.Height * 0.55f), tank.Width, height * 0.09f),
                StringAlignment.Center, StringAlignment.Center);
        }

        if (outOfRange)
        {
            HmiPaint.Text(
                graphics, Value > HighLimit ? "HIGH" : "LOW", HmiPaint.Ui(Math.Max(7f, height * 0.06f), FontStyle.Bold), warn,
                new RectangleF(tank.Left, tank.Bottom - (height * 0.12f), tank.Width, height * 0.1f),
                StringAlignment.Center, StringAlignment.Center);
        }

        if (!string.IsNullOrEmpty(Title))
        {
            HmiPaint.Text(
                graphics, Title, HmiPaint.Ui(Math.Max(7f, titleHeight * 0.72f)), muted,
                new RectangleF(0f, 0f, width, titleHeight),
                StringAlignment.Center, StringAlignment.Center);
        }
    }
}
