using System.Drawing;
using Pen = System.Drawing.Pen;
using LineCap = System.Drawing.Drawing2D.LineCap;
using System.Drawing.Drawing2D;
using Easel;
using Easel.Visuals;
using XamlBrush = Easel.Brush;

namespace IndustrialWorkbench.Controls;

/// <summary>A horizontal pipe with moving dashes; flow direction, rate and a live label.</summary>
public sealed class PipeFlow : VisualElement
{
    /// <summary>Whether the dashes move.</summary>
    public static readonly DependencyProperty IsFlowingProperty = DependencyProperty.Register(
        nameof(IsFlowing), typeof(bool), typeof(PipeFlow),
        new PropertyMetadata(true) { AffectsRender = true });

    /// <summary>The flow rate; scales the dash speed and the label.</summary>
    public static readonly DependencyProperty FlowRateProperty = DependencyProperty.Register(
        nameof(FlowRate), typeof(double), typeof(PipeFlow),
        new PropertyMetadata(40d) { AffectsRender = true });

    /// <summary>0 flows left to right; 1 flows right to left.</summary>
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(
        nameof(Direction), typeof(int), typeof(PipeFlow),
        new PropertyMetadata(0) { AffectsRender = true });

    /// <summary>A monotonically increasing phase that scrolls the dashes.</summary>
    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(double), typeof(PipeFlow),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>The pipe shell colour.</summary>
    public static readonly DependencyProperty PipeBrushProperty = DependencyProperty.Register(
        nameof(PipeBrush), typeof(XamlBrush), typeof(PipeFlow),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The flowing medium colour.</summary>
    public static readonly DependencyProperty FlowBrushProperty = DependencyProperty.Register(
        nameof(FlowBrush), typeof(XamlBrush), typeof(PipeFlow),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The label drawn above the pipe.</summary>
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(PipeFlow),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>Creates a non-interactive pipe.</summary>
    public PipeFlow()
    {
        IsHitTestVisible = false;
        MinWidth = 120f;
        MinHeight = 30f;
        PipeBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x1A, 0x26, 0x32));
        FlowBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x25, 0xD0, 0xEC));
    }

    /// <summary>Whether the dashes move.</summary>
    public bool IsFlowing
    {
        get => (bool)GetValue(IsFlowingProperty)!;
        set => SetValue(IsFlowingProperty, value);
    }

    /// <summary>The flow rate.</summary>
    public double FlowRate
    {
        get => (double)GetValue(FlowRateProperty)!;
        set => SetValue(FlowRateProperty, Math.Max(0d, value));
    }

    /// <summary>0 flows left to right; 1 flows right to left.</summary>
    public int Direction
    {
        get => (int)GetValue(DirectionProperty)!;
        set => SetValue(DirectionProperty, value == 1 ? 1 : 0);
    }

    /// <summary>Drives the dash scroll.</summary>
    public double Phase
    {
        get => (double)GetValue(PhaseProperty)!;
        set => SetValue(PhaseProperty, value);
    }

    /// <summary>The pipe shell colour.</summary>
    public XamlBrush? PipeBrush
    {
        get => (XamlBrush?)GetValue(PipeBrushProperty);
        set => SetValue(PipeBrushProperty, value);
    }

    /// <summary>The flowing medium colour.</summary>
    public XamlBrush? FlowBrush
    {
        get => (XamlBrush?)GetValue(FlowBrushProperty);
        set => SetValue(FlowBrushProperty, value);
    }

    /// <summary>The label drawn above the pipe.</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty)!;
        set => SetValue(LabelProperty, value ?? string.Empty);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width is > 0 and < 100000 ? availableSize.Width : 220;
        var height = availableSize.Height is > 0 and < 100000 ? availableSize.Height : 46;
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 16f || height <= 10f)
        {
            return;
        }

        var flowing = IsFlowing && FlowRate > 0.01d;
        var flowColor = HmiPaint.ColorOf(FlowBrush, Color.FromArgb(0xFF, 0x25, 0xD0, 0xEC));
        var pipeColor = HmiPaint.ColorOf(PipeBrush, Color.FromArgb(0xFF, 0x1A, 0x26, 0x32));
        var muted = Color.FromArgb(0xFF, 0x74, 0x88, 0x9C);

        var hasLabel = !string.IsNullOrEmpty(Label);
        var labelHeight = hasLabel ? Math.Min(16f, height * 0.3f) : 0f;
        var bandHeight = Math.Min(height - labelHeight - 4f, Math.Max(12f, height * 0.52f));
        var bandTop = labelHeight + ((height - labelHeight - bandHeight) / 2f);
        var band = new RectangleF(2f, bandTop, Math.Max(4f, width - 16f), Math.Max(6f, bandHeight));
        var radius = band.Height * 0.5f;

        // Shell.
        HmiPaint.FillRounded(graphics, pipeColor, band, radius);
        HmiPaint.DrawRounded(graphics, HmiPaint.Fade(Color.FromArgb(0xFF, 0x33, 0x50, 0x6A), 0.9), band, radius, 1.4f);

        var channel = new RectangleF(band.Left + 4f, band.Top + (band.Height * 0.22f), Math.Max(2f, band.Width - 8f), Math.Max(2f, band.Height * 0.56f));
        var channelRadius = channel.Height * 0.5f;
        HmiPaint.FillRounded(graphics, Color.FromArgb(0xFF, 0x05, 0x09, 0x0D), channel, channelRadius);

        // Moving dashes, clipped to the channel.
        var state = graphics.Save();
        using (var clip = HmiPaint.RoundedRect(channel, channelRadius))
        {
            graphics.SetClip(clip, CombineMode.Intersect);
            var period = Math.Max(18f, channel.Height * 1.8f);
            var dash = period * 0.52f;
            var speed = 0.6f + ((float)Math.Min(120d, FlowRate) * 0.02f);
            var offset = (float)((Phase * speed) % period);
            var direction = Direction == 1 ? -1f : 1f;
            for (var x = channel.Left - period; x < channel.Right + period; x += period)
            {
                var start = x + (offset * direction);
                var rect = new RectangleF(start, channel.Top, dash, channel.Height);
                var color = flowing ? HmiPaint.Fade(flowColor, 0.85) : HmiPaint.Fade(flowColor, 0.18);
                HmiPaint.FillRounded(graphics, color, rect, channelRadius);
            }

            if (flowing)
            {
                using var core = new Pen(HmiPaint.Fade(HmiPaint.Mix(flowColor, Color.White, 0.4), 0.45), Math.Max(1f, channel.Height * 0.14f));
                var coreY = channel.Top + (channel.Height / 2f);
                graphics.DrawLine(core, channel.Left, coreY, channel.Right, coreY);
            }
        }

        graphics.Restore(state);

        // Direction arrow at the outlet.
        var arrowX = band.Right + 2f;
        var arrowSize = Math.Max(5f, band.Height * 0.42f);
        var arrowY = band.Top + (band.Height / 2f);
        using (var arrow = new SolidBrush(flowing ? flowColor : HmiPaint.Fade(muted, 0.6)))
        {
            var flip = Direction == 1;
            var tip = flip ? band.Left - 2f : arrowX + arrowSize;
            var baseX = flip ? band.Left + arrowSize - 2f : arrowX;
            graphics.FillPolygon(arrow,
            [
                new PointF(tip, arrowY),
                new PointF(baseX, arrowY - (arrowSize * 0.6f)),
                new PointF(baseX, arrowY + (arrowSize * 0.6f)),
            ]);
        }

        if (hasLabel)
        {
            HmiPaint.Text(
                graphics, Label, HmiPaint.Ui(Math.Max(7f, labelHeight * 0.72f)), muted,
                new RectangleF(2f, 0f, width - 4f, labelHeight),
                StringAlignment.Near, StringAlignment.Center);
        }
    }
}
