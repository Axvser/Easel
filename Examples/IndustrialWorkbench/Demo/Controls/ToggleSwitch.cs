using System.Drawing;
using Pen = System.Drawing.Pen;
using LineCap = System.Drawing.Drawing2D.LineCap;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using XamlBrush = Easel.Brush;

namespace IndustrialWorkbench.Controls;

/// <summary>An industrial selector lever: click toggles the state and swings the handle.</summary>
public sealed class ToggleSwitch : VisualElement
{
    private bool _pressed;

    /// <summary>Whether the switch is on.</summary>
    public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
        nameof(IsOn), typeof(bool), typeof(ToggleSwitch),
        new PropertyMetadata(false) { AffectsRender = true });

    /// <summary>The label above the switch.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ToggleSwitch),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>The on-state colour.</summary>
    public static readonly DependencyProperty OnBrushProperty = DependencyProperty.Register(
        nameof(OnBrush), typeof(XamlBrush), typeof(ToggleSwitch),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The off-state colour.</summary>
    public static readonly DependencyProperty OffBrushProperty = DependencyProperty.Register(
        nameof(OffBrush), typeof(XamlBrush), typeof(ToggleSwitch),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>Creates an interactive switch.</summary>
    public ToggleSwitch()
    {
        Focusable = true;
        IsHitTestVisible = true;
        Cursor = Cursors.Hand;
        MinWidth = 80f;
        MinHeight = 80f;
        OnBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x32, 0xE0, 0xA2));
        OffBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x3B, 0x4E));
    }

    /// <summary>Raised after the switch is toggled by a click or the keyboard.</summary>
    public event EventHandler? Toggled;

    /// <summary>Whether the switch is on.</summary>
    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty)!;
        set => SetValue(IsOnProperty, value);
    }

    /// <summary>The label above the switch.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value ?? string.Empty);
    }

    /// <summary>The on-state colour.</summary>
    public XamlBrush? OnBrush
    {
        get => (XamlBrush?)GetValue(OnBrushProperty);
        set => SetValue(OnBrushProperty, value);
    }

    /// <summary>The off-state colour.</summary>
    public XamlBrush? OffBrush
    {
        get => (XamlBrush?)GetValue(OffBrushProperty);
        set => SetValue(OffBrushProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width is > 0 and < 100000 ? availableSize.Width : 90;
        var height = availableSize.Height is > 0 and < 100000 ? availableSize.Height : 100;
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        base.OnPointerPressed(args);
        if (args.Button == MouseButtons.Left && IsEnabled)
        {
            Focus();
            _pressed = true;
            args.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(VisualPointerEventArgs args)
    {
        base.OnPointerReleased(args);
        if (_pressed && args.Button == MouseButtons.Left)
        {
            _pressed = false;
            IsOn = !IsOn;
            Toggled?.Invoke(this, EventArgs.Empty);
            args.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(VisualKeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (args.KeyCode is Keys.Space or Keys.Enter)
        {
            IsOn = !IsOn;
            Toggled?.Invoke(this, EventArgs.Empty);
            args.Handled = true;
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

        var on = IsOn;
        var color = HmiPaint.ColorOf(on ? OnBrush : OffBrush,
            on ? Color.FromArgb(0xFF, 0x32, 0xE0, 0xA2) : Color.FromArgb(0xFF, 0xFF, 0x3B, 0x4E));
        var muted = Color.FromArgb(0xFF, 0x74, 0x88, 0x9C);
        var text = Color.FromArgb(0xFF, 0xD6, 0xE4, 0xF0);

        var hasTitle = !string.IsNullOrEmpty(Title);
        var titleHeight = hasTitle ? Math.Min(16f, height * 0.16f) : 0f;
        var bodyTop = titleHeight;
        var bodyHeight = height - titleHeight;
        var cx = width / 2f;

        // Base plate.
        var plateWidth = Math.Min(width * 0.72f, bodyHeight * 0.72f);
        var plateHeight = Math.Max(8f, bodyHeight * 0.16f);
        var plate = new RectangleF(cx - (plateWidth / 2f), bodyTop + bodyHeight - plateHeight - 2f, plateWidth, plateHeight);
        HmiPaint.FillRounded(graphics, Color.FromArgb(0xFF, 0x12, 0x1C, 0x26), plate, plateHeight / 2f);
        HmiPaint.DrawRounded(graphics, HmiPaint.Fade(muted, 0.5), plate, plateHeight / 2f, 1f);

        var pivotY = plate.Top - 2f;
        var pivot = new PointF(cx, pivotY);
        var armLength = bodyHeight * 0.58f;
        var angle = on ? -55d : -125d;
        var radians = angle * (Math.PI / 180d);
        var end = new PointF(
            pivot.X + ((float)Math.Cos(radians) * armLength),
            pivot.Y + ((float)Math.Sin(radians) * armLength));

        // Lever.
        using (var arm = new Pen(Color.FromArgb(0xFF, 0x8F, 0xA6, 0xBA), Math.Max(3f, armLength * 0.10f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            graphics.DrawLine(arm, pivot, end);
        }

        if (on)
        {
            using var glow = new Pen(HmiPaint.Fade(color, 0.22), Math.Max(7f, armLength * 0.22f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawLine(glow, pivot, end);
        }

        // Knob.
        var knob = Math.Max(5f, armLength * 0.16f);
        using (var knobBrush = new SolidBrush(color))
        {
            graphics.FillEllipse(knobBrush, end.X - knob, end.Y - knob, knob * 2f, knob * 2f);
        }
        using (var knobRim = new Pen(HmiPaint.Fade(Color.White, 0.35), 1f))
        {
            graphics.DrawEllipse(knobRim, end.X - knob, end.Y - knob, knob * 2f, knob * 2f);
        }

        // Pivot base.
        var pivotRadius = Math.Max(3f, plateHeight * 0.4f);
        using (var pivotBrush = new SolidBrush(Color.FromArgb(0xFF, 0x2A, 0x3C, 0x4C)))
        {
            graphics.FillEllipse(pivotBrush, pivot.X - pivotRadius, pivot.Y - pivotRadius, pivotRadius * 2f, pivotRadius * 2f);
        }

        // State labels beside the lever.
        HmiPaint.Text(graphics, on ? "ON" : "OFF", HmiPaint.Ui(Math.Max(6f, bodyHeight * 0.11f), FontStyle.Bold), color,
            new RectangleF(cx - (width * 0.4f), plate.Bottom - (bodyHeight * 0.12f), width * 0.8f, bodyHeight * 0.12f),
            StringAlignment.Center, StringAlignment.Center);

        if (hasTitle)
        {
            HmiPaint.Text(graphics, Title, HmiPaint.Ui(Math.Max(7f, titleHeight * 0.7f)), text,
                new RectangleF(0f, 0f, width, titleHeight), StringAlignment.Center, StringAlignment.Center);
        }
    }
}
