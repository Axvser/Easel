using System.Drawing;
using Pen = System.Drawing.Pen;
using LineCap = System.Drawing.Drawing2D.LineCap;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using XamlBrush = Easel.Brush;

namespace IndustrialWorkbench.Controls;

/// <summary>A draggable rotary knob: 270° tick ring, live arc, pointer and value readout.</summary>
/// <remarks>Dragging, the mouse wheel and the arrow keys change <see cref="Value"/>; a two-way binding publishes it.</remarks>
public sealed class Knob : VisualElement
{
    private bool _dragging;

    /// <summary>The current value.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(Knob),
        new PropertyMetadata(50d) { AffectsRender = true });

    /// <summary>The lowest value.</summary>
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(Knob),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>The highest value.</summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(Knob),
        new PropertyMetadata(100d) { AffectsRender = true });

    /// <summary>The unit label.</summary>
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(Knob),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>The label above the knob.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(Knob),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>The active arc and pointer colour.</summary>
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(XamlBrush), typeof(Knob),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The tick colour.</summary>
    public static readonly DependencyProperty TickBrushProperty = DependencyProperty.Register(
        nameof(TickBrush), typeof(XamlBrush), typeof(Knob),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>Creates a focusable, pointer-driven knob.</summary>
    public Knob()
    {
        Focusable = true;
        IsHitTestVisible = true;
        Cursor = Cursors.Hand;
        MinWidth = 80f;
        MinHeight = 90f;
        Accent = new SolidColorBrush(Color.FromArgb(0xFF, 0x25, 0xD0, 0xEC));
        TickBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x74, 0x88, 0x9C));
    }

    /// <summary>Raised after <see cref="Value"/> changes.</summary>
    public event EventHandler? ValueChanged;

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

    /// <summary>The unit label.</summary>
    public string Unit
    {
        get => (string)GetValue(UnitProperty)!;
        set => SetValue(UnitProperty, value ?? string.Empty);
    }

    /// <summary>The label above the knob.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value ?? string.Empty);
    }

    /// <summary>The active arc and pointer colour.</summary>
    public XamlBrush? Accent
    {
        get => (XamlBrush?)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>The tick colour.</summary>
    public XamlBrush? TickBrush
    {
        get => (XamlBrush?)GetValue(TickBrushProperty);
        set => SetValue(TickBrushProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width is > 0 and < 100000 ? availableSize.Width : 120;
        var height = availableSize.Height is > 0 and < 100000 ? availableSize.Height : 130;
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        base.OnPointerPressed(args);
        if (!IsEnabled || args.Button != MouseButtons.Left)
        {
            return;
        }

        Focus();
        _dragging = true;
        SetFromPoint(args.GetPosition(this));
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(VisualPointerEventArgs args)
    {
        base.OnPointerMoved(args);
        if (_dragging)
        {
            SetFromPoint(args.GetPosition(this));
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(VisualPointerEventArgs args)
    {
        base.OnPointerReleased(args);
        _dragging = false;
    }

    /// <inheritdoc />
    protected override void OnPointerWheel(VisualPointerEventArgs args)
    {
        base.OnPointerWheel(args);
        if (!IsEnabled)
        {
            return;
        }

        var step = (Maximum - Minimum) / 50d;
        Value += args.Delta > 0 ? step : -step;
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(VisualKeyEventArgs args)
    {
        base.OnKeyDown(args);
        var step = (Maximum - Minimum) / 50d;
        switch (args.KeyCode)
        {
            case Keys.Left:
            case Keys.Down:
                Value -= step;
                args.Handled = true;
                break;
            case Keys.Right:
            case Keys.Up:
                Value += step;
                args.Handled = true;
                break;
            case Keys.Home:
                Value = Minimum;
                args.Handled = true;
                break;
            case Keys.End:
                Value = Maximum;
                args.Handled = true;
                break;
        }
    }

    /// <summary>Maps an angle in the 135°..405° sweep to a value.</summary>
    public void SetFromPoint(PointF point)
    {
        var radius = Math.Min(Bounds.Width, Bounds.Height) / 2f;
        var cx = Bounds.Width / 2f;
        var cy = Bounds.Height / 2f;
        var dx = point.X - cx;
        var dy = point.Y - cy;
        if ((dx * dx) + (dy * dy) < (radius * 0.12f) * (radius * 0.12f))
        {
            return;
        }

        var degrees = Math.Atan2(dy, dx) * (180d / Math.PI);
        if (degrees < 0d)
        {
            degrees += 360d;
        }

        if (degrees < 135d)
        {
            degrees += 360d;
        }

        degrees = Math.Clamp(degrees, 135d, 405d);
        var fraction = (degrees - 135d) / 270d;
        Value = Minimum + (fraction * (Maximum - Minimum));
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, ValueProperty))
        {
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
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
        var tick = HmiPaint.ColorOf(TickBrush, Color.FromArgb(0xFF, 0x74, 0x88, 0x9C));
        var text = Color.FromArgb(0xFF, 0xD6, 0xE4, 0xF0);
        var muted = Color.FromArgb(0xFF, 0x74, 0x88, 0x9C);

        var hasTitle = !string.IsNullOrEmpty(Title);
        var titleHeight = hasTitle ? Math.Min(16f, height * 0.14f) : 0f;
        var size = Math.Min(width, height - titleHeight);
        var cx = width / 2f;
        var cy = titleHeight + (size / 2f);
        var outer = (size / 2f) - 2f;
        var ring = outer * 0.86f;

        var range = Maximum - Minimum;
        var fraction = range <= 0d ? 0d : Math.Clamp((Value - Minimum) / range, 0d, 1d);

        // Body.
        using (var body = new SolidBrush(Color.FromArgb(0xFF, 0x0B, 0x12, 0x19)))
        {
            graphics.FillEllipse(body, cx - ring, cy - ring, ring * 2f, ring * 2f);
        }
        using (var rim = new Pen(Color.FromArgb(0xFF, 0x25, 0x38, 0x48), Math.Max(1f, ring * 0.08f)))
        {
            graphics.DrawEllipse(rim, cx - ring, cy - ring, ring * 2f, ring * 2f);
        }

        // Ticks.
        for (var index = 0; index <= 20; index++)
        {
            var major = index % 5 == 0;
            var angle = 135f + ((270f / 20f) * index);
            var outerPoint = HmiPaint.PointOn(cx, cy, outer, angle);
            var innerPoint = HmiPaint.PointOn(cx, cy, outer - (major ? size * 0.07f : size * 0.04f), angle);
            using var pen = new Pen(major ? tick : HmiPaint.Fade(tick, 0.5), major ? 1.6f : 1f);
            graphics.DrawLine(pen, outerPoint, innerPoint);
        }

        // Active arc.
        var arcRect = new RectangleF(cx - ring, cy - ring, ring * 2f, ring * 2f);
        HmiPaint.GlowArc(graphics, arcRect, 135f, 270f * (float)fraction, accent, Math.Max(2.5f, size * 0.035f));

        // Pointer.
        var angle2 = 135f + (270f * (float)fraction);
        var tip = HmiPaint.PointOn(cx, cy, ring * 0.72f, angle2);
        var tail = HmiPaint.PointOn(cx, cy, ring * 0.12f, angle2 + 180f);
        using (var pointer = new Pen(accent, Math.Max(2f, size * 0.022f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            graphics.DrawLine(pointer, tail, tip);
        }

        var hub = size * 0.11f;
        using (var hubBrush = new SolidBrush(Color.FromArgb(0xFF, 0x16, 0x22, 0x2C)))
        {
            graphics.FillEllipse(hubBrush, cx - hub, cy - hub, hub * 2f, hub * 2f);
        }
        using (var hubPen = new Pen(HmiPaint.Fade(accent, 0.8), 1.4f))
        {
            graphics.DrawEllipse(hubPen, cx - hub, cy - hub, hub * 2f, hub * 2f);
        }

        HmiPaint.Text(graphics, Value.ToString("0"), HmiPaint.Mono(Math.Max(10f, size * 0.20f), FontStyle.Bold), text,
            new RectangleF(cx - (size * 0.4f), cy + (size * 0.20f), size * 0.8f, size * 0.18f),
            StringAlignment.Center, StringAlignment.Center);
        if (!string.IsNullOrEmpty(Unit))
        {
            HmiPaint.Text(graphics, Unit, HmiPaint.Ui(Math.Max(6f, size * 0.08f)), muted,
                new RectangleF(cx - (size * 0.4f), cy + (size * 0.36f), size * 0.8f, size * 0.12f),
                StringAlignment.Center, StringAlignment.Center);
        }

        if (hasTitle)
        {
            HmiPaint.Text(graphics, Title, HmiPaint.Ui(Math.Max(7f, titleHeight * 0.7f)), muted,
                new RectangleF(0f, 0f, width, titleHeight), StringAlignment.Center, StringAlignment.Center);
        }
    }
}
