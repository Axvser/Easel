using System.Drawing;
using Pen = System.Drawing.Pen;
using LineCap = System.Drawing.Drawing2D.LineCap;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using XamlBrush = Easel.Brush;

namespace IndustrialWorkbench.Controls;

/// <summary>A gate valve: pipe stubs, a bow-tie body, a stem and a hand wheel that turns when toggled.</summary>
public sealed class Valve : VisualElement
{
    /// <summary>Whether the valve is open.</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(Valve),
        new PropertyMetadata(true) { AffectsRender = true });

    /// <summary>The label shown under the valve.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(Valve),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>The open-state colour.</summary>
    public static readonly DependencyProperty OpenBrushProperty = DependencyProperty.Register(
        nameof(OpenBrush), typeof(XamlBrush), typeof(Valve),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The closed-state colour.</summary>
    public static readonly DependencyProperty ClosedBrushProperty = DependencyProperty.Register(
        nameof(ClosedBrush), typeof(XamlBrush), typeof(Valve),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>Creates an interactive valve.</summary>
    public Valve()
    {
        IsHitTestVisible = true;
        Cursor = Cursors.Hand;
        MinWidth = 90f;
        MinHeight = 90f;
        OpenBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x32, 0xE0, 0xA2));
        ClosedBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x3B, 0x4E));
    }

    /// <summary>Raised after the valve is toggled by a click.</summary>
    public event EventHandler? Toggled;

    /// <summary>Whether the valve is open.</summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty)!;
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>The label shown under the valve.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value ?? string.Empty);
    }

    /// <summary>The open-state colour.</summary>
    public XamlBrush? OpenBrush
    {
        get => (XamlBrush?)GetValue(OpenBrushProperty);
        set => SetValue(OpenBrushProperty, value);
    }

    /// <summary>The closed-state colour.</summary>
    public XamlBrush? ClosedBrush
    {
        get => (XamlBrush?)GetValue(ClosedBrushProperty);
        set => SetValue(ClosedBrushProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width is > 0 and < 100000 ? availableSize.Width : 110;
        var height = availableSize.Height is > 0 and < 100000 ? availableSize.Height : 110;
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(VisualPointerEventArgs args)
    {
        base.OnPointerReleased(args);
        if (args.Button == MouseButtons.Left && IsEnabled)
        {
            IsOpen = !IsOpen;
            Toggled?.Invoke(this, EventArgs.Empty);
            args.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 16f || height <= 16f)
        {
            return;
        }

        var open = IsOpen;
        var stateColor = HmiPaint.ColorOf(open ? OpenBrush : ClosedBrush,
            open ? Color.FromArgb(0xFF, 0x32, 0xE0, 0xA2) : Color.FromArgb(0xFF, 0xFF, 0x3B, 0x4E));
        var steel = Color.FromArgb(0xFF, 0x2A, 0x3C, 0x4C);
        var muted = Color.FromArgb(0xFF, 0x74, 0x88, 0x9C);

        var hasLabel = !string.IsNullOrEmpty(Title);
        var labelHeight = hasLabel ? Math.Min(16f, height * 0.16f) : 0f;
        var bodyHeight = height - labelHeight;
        var cx = width / 2f;
        var cy = bodyHeight / 2f;
        var bodyRadius = Math.Min(width, bodyHeight) * 0.24f;
        var pipeHeight = Math.Max(6f, bodyRadius * 0.42f);

        // Pipe stubs.
        var pipe = new RectangleF(2f, cy - (pipeHeight / 2f), width - 4f, pipeHeight);
        HmiPaint.FillRounded(graphics, Color.FromArgb(0xFF, 0x12, 0x1C, 0x26), pipe, pipeHeight / 2f);
        HmiPaint.DrawRounded(graphics, HmiPaint.Fade(steel, 0.8), pipe, pipeHeight / 2f, 1.2f);

        // Body: two triangles pointing at the centre.
        using (var body = new SolidBrush(Color.FromArgb(0xFF, 0x1B, 0x28, 0x34)))
        {
            graphics.FillPolygon(body,
            [
                new PointF(cx - bodyRadius, cy - bodyRadius),
                new PointF(cx - bodyRadius, cy + bodyRadius),
                new PointF(cx, cy),
            ]);
            graphics.FillPolygon(body,
            [
                new PointF(cx + bodyRadius, cy - bodyRadius),
                new PointF(cx + bodyRadius, cy + bodyRadius),
                new PointF(cx, cy),
            ]);
        }

        using (var bodyPen = new Pen(HmiPaint.Fade(stateColor, 0.85), 1.6f))
        {
            graphics.DrawPolygon(bodyPen,
            [
                new PointF(cx - bodyRadius, cy - bodyRadius),
                new PointF(cx - bodyRadius, cy + bodyRadius),
                new PointF(cx, cy),
            ]);
            graphics.DrawPolygon(bodyPen,
            [
                new PointF(cx + bodyRadius, cy - bodyRadius),
                new PointF(cx + bodyRadius, cy + bodyRadius),
                new PointF(cx, cy),
            ]);
        }

        // Stem and hand wheel.
        var stemTop = cy - bodyRadius - (bodyRadius * 0.85f);
        using (var stem = new Pen(steel, Math.Max(2f, bodyRadius * 0.18f)))
        {
            graphics.DrawLine(stem, cx, cy - bodyRadius * 0.2f, cx, stemTop);
        }

        var wheelRadius = bodyRadius * 0.55f;
        using (var wheel = new Pen(stateColor, Math.Max(2f, wheelRadius * 0.28f)))
        {
            graphics.DrawEllipse(wheel, cx - wheelRadius, stemTop - wheelRadius, wheelRadius * 2f, wheelRadius * 2f);
        }

        // The wheel bar turns 90° when the valve closes.
        var barAngle = open ? 0f : 90f;
        var radians = barAngle * (float)(Math.PI / 180d);
        var dx = (float)Math.Cos(radians) * wheelRadius;
        var dy = (float)Math.Sin(radians) * wheelRadius;
        using (var bar = new Pen(HmiPaint.Fade(stateColor, 0.9), Math.Max(2f, wheelRadius * 0.22f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            graphics.DrawLine(bar, cx - dx, stemTop - dy, cx + dx, stemTop + dy);
        }

        // State text.
        var stateText = open ? "OPEN" : "CLOSED";
        HmiPaint.Text(graphics, stateText, HmiPaint.Ui(Math.Max(6f, bodyRadius * 0.34f), FontStyle.Bold), stateColor,
            new RectangleF(cx - (width * 0.3f), cy + (bodyRadius * 0.45f), width * 0.6f, bodyRadius * 0.5f),
            StringAlignment.Center, StringAlignment.Center);

        if (hasLabel)
        {
            HmiPaint.Text(graphics, Title, HmiPaint.Ui(Math.Max(7f, labelHeight * 0.7f)), muted,
                new RectangleF(0f, height - labelHeight, width, labelHeight), StringAlignment.Center, StringAlignment.Center);
        }
    }
}
