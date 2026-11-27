using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using XamlBrush = Easel.Brush;

namespace IndustrialWorkbench.Controls;

/// <summary>A severity-coloured alarm banner; clicking it raises <see cref="Acknowledged"/>.</summary>
public sealed class AlarmBanner : VisualElement
{
    /// <summary>The alarm text.</summary>
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(AlarmBanner),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>0 = info, 1 = warning, 2 = critical.</summary>
    public static readonly DependencyProperty SeverityProperty = DependencyProperty.Register(
        nameof(Severity), typeof(int), typeof(AlarmBanner),
        new PropertyMetadata(1) { AffectsRender = true });

    /// <summary>Whether an alarm is currently shown.</summary>
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(AlarmBanner),
        new PropertyMetadata(true) { AffectsRender = true });

    /// <summary>Whether the current alarm has been acknowledged; acknowledged alarms stop pulsing.</summary>
    public static readonly DependencyProperty IsAcknowledgedProperty = DependencyProperty.Register(
        nameof(IsAcknowledged), typeof(bool), typeof(AlarmBanner),
        new PropertyMetadata(false) { AffectsRender = true });

    /// <summary>The number of pending alarms shown in the corner.</summary>
    public static readonly DependencyProperty CountProperty = DependencyProperty.Register(
        nameof(Count), typeof(int), typeof(AlarmBanner),
        new PropertyMetadata(1) { AffectsRender = true });

    /// <summary>Drives the critical pulse.</summary>
    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(double), typeof(AlarmBanner),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>The heading prefix; defaults to "ALARM".</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(AlarmBanner),
        new PropertyMetadata("ALARM") { AffectsRender = true });

    /// <summary>Creates an interactive banner.</summary>
    public AlarmBanner()
    {
        IsHitTestVisible = true;
        Cursor = Cursors.Hand;
        MinHeight = 34f;
    }

    /// <summary>Raised when the banner is clicked while active.</summary>
    public event EventHandler? Acknowledged;

    /// <summary>The alarm text.</summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty)!;
        set => SetValue(MessageProperty, value ?? string.Empty);
    }

    /// <summary>0 = info, 1 = warning, 2 = critical.</summary>
    public int Severity
    {
        get => (int)GetValue(SeverityProperty)!;
        set => SetValue(SeverityProperty, Math.Clamp(value, 0, 2));
    }

    /// <summary>Whether an alarm is currently shown.</summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty)!;
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Whether the current alarm has been acknowledged.</summary>
    public bool IsAcknowledged
    {
        get => (bool)GetValue(IsAcknowledgedProperty)!;
        set => SetValue(IsAcknowledgedProperty, value);
    }

    /// <summary>The number of pending alarms.</summary>
    public int Count
    {
        get => (int)GetValue(CountProperty)!;
        set => SetValue(CountProperty, Math.Max(0, value));
    }

    /// <summary>Drives the critical pulse.</summary>
    public double Phase
    {
        get => (double)GetValue(PhaseProperty)!;
        set => SetValue(PhaseProperty, value);
    }

    /// <summary>The heading prefix.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value ?? string.Empty);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width is > 0 and < 100000 ? availableSize.Width : 320;
        var height = availableSize.Height is > 0 and < 100000 ? availableSize.Height : 38;
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(VisualPointerEventArgs args)
    {
        base.OnPointerReleased(args);
        if (args.Button == MouseButtons.Left && IsActive && IsEnabled)
        {
            Acknowledged?.Invoke(this, EventArgs.Empty);
            args.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 12f || height <= 8f)
        {
            return;
        }

        var active = IsActive;
        var severity = Severity;
        var color = !active
            ? Color.FromArgb(0xFF, 0x32, 0xE0, 0xA2)
            : severity >= 2 ? Color.FromArgb(0xFF, 0xFF, 0x3B, 0x4E)
            : severity == 1 ? Color.FromArgb(0xFF, 0xFF, 0xB0, 0x20)
            : Color.FromArgb(0xFF, 0x25, 0xD0, 0xEC);
        var text = Color.FromArgb(0xFF, 0xE4, 0xEE, 0xF6);
        var muted = Color.FromArgb(0xFF, 0x74, 0x88, 0x9C);

        var rect = new RectangleF(1.5f, 1.5f, width - 3f, height - 3f);
        var radius = Math.Min(6f, height * 0.18f);
        HmiPaint.FillRounded(graphics, Color.FromArgb(0xFF, 0x0C, 0x13, 0x1A), rect, radius);
        HmiPaint.FillRounded(graphics, HmiPaint.Fade(color, 0.10), rect, radius);

        // Accent bar on the left.
        using (var bar = HmiPaint.RoundedRect(new RectangleF(rect.Left, rect.Top + 3f, 4f, rect.Height - 6f), 2f))
        {
            using var brush = new SolidBrush(color);
            graphics.FillPath(brush, bar);
        }

        var pulse = active && severity >= 2 && !IsAcknowledged
            ? (float)((Math.Sin(Phase * 1.6d) * 0.5d) + 0.5d)
            : 0f;
        var borderColor = pulse > 0f
            ? HmiPaint.Fade(color, 0.35 + (pulse * 0.65))
            : HmiPaint.Fade(color, active ? 0.55 : 0.25);
        HmiPaint.DrawRounded(graphics, borderColor, rect, radius, active && severity >= 2 ? 1.6f : 1f);

        // Icon.
        var iconSize = Math.Min(height * 0.5f, 20f);
        var iconCx = rect.Left + 14f + (iconSize / 2f);
        var iconCy = rect.Top + (rect.Height / 2f);
        using (var iconBrush = new SolidBrush(HmiPaint.Fade(color, 0.85)))
        {
            graphics.FillEllipse(iconBrush, iconCx - (iconSize / 2f), iconCy - (iconSize / 2f), iconSize, iconSize);
        }

        var glyph = active ? "!" : "✓";
        HmiPaint.Text(graphics, glyph, HmiPaint.Ui(iconSize * 0.7f, FontStyle.Bold),
            Color.FromArgb(0xFF, 0x07, 0x0C, 0x12),
            new RectangleF(iconCx - iconSize, iconCy - iconSize, iconSize * 2f, iconSize * 2f),
            StringAlignment.Center, StringAlignment.Center);

        // Heading and message.
        var bodyLeft = iconCx + (iconSize / 2f) + 8f;
        var bodyRight = rect.Right - 74f;
        if (bodyRight > bodyLeft + 20f)
        {
            HmiPaint.Text(graphics, Title, HmiPaint.Ui(Math.Max(6f, height * 0.20f), FontStyle.Bold), color,
                new RectangleF(bodyLeft, rect.Top + (rect.Height * 0.16f), bodyRight - bodyLeft, rect.Height * 0.30f),
                StringAlignment.Near, StringAlignment.Center);
            HmiPaint.Text(graphics, active ? Message : "所有报警已确认 · 系统监视中",
                HmiPaint.Ui(Math.Max(7f, height * 0.26f)), active ? text : muted,
                new RectangleF(bodyLeft, rect.Top + (rect.Height * 0.46f), bodyRight - bodyLeft, rect.Height * 0.42f),
                StringAlignment.Near, StringAlignment.Center);
        }

        // ACK chip and count.
        var chip = new RectangleF(rect.Right - 64f, rect.Top + ((rect.Height - 20f) / 2f), 56f, 20f);
        var chipColor = active && !IsAcknowledged ? HmiPaint.Fade(color, 0.85) : HmiPaint.Fade(muted, 0.4);
        HmiPaint.DrawRounded(graphics, chipColor, chip, 10f, 1.2f);
        HmiPaint.Text(graphics, active ? "ACK" : "CLEAR", HmiPaint.Ui(Math.Max(6f, height * 0.18f), FontStyle.Bold),
            active ? color : muted, chip, StringAlignment.Center, StringAlignment.Center);

        if (active && Count > 0)
        {
            using var badge = new SolidBrush(HmiPaint.Fade(color, 0.9));
            graphics.FillEllipse(badge, rect.Right - 12f, rect.Top + 4f, 10f, 10f);
        }
    }
}
