using System.Drawing;
using System.Windows.Forms;
using Easel;
using XamlDrawing = Easel.Controls.XamlDrawing;
using XamlThickness = Easel.Controls.Thickness;

namespace Easel.Visuals.Controls;

/// <summary>A horizontal progress track filled to <see cref="Value"/>.</summary>
/// <remarks>
/// A control template may replace the chrome with a <c>PART_Track</c> and a stretching <c>PART_Fill</c>.
/// The control still owns <see cref="Minimum"/>, <see cref="Maximum"/>, <see cref="Value"/> and the covered
/// fraction; it arranges <c>PART_Fill</c> across the track's arranged bounds. Without a template it draws the
/// track itself.
/// </remarks>
public class ProgressBar : TemplatedControl
{
    /// <summary>The lowest value.</summary>
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(ProgressBar),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>The highest value.</summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(ProgressBar),
        new PropertyMetadata(100d) { AffectsRender = true });

    /// <summary>The current value, coerced into the range.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(ProgressBar),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>Creates a non-interactive bar.</summary>
    public ProgressBar()
    {
        IsHitTestVisible = false;
        MinHeight = 6f;
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

    /// <summary>The current value.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty)!;
        set => SetValue(ValueProperty, XamlMath.Clamp(value, Minimum, Maximum));
    }

    /// <summary>The covered fraction in the range 0..1.</summary>
    protected double Fraction
        => Maximum <= Minimum ? 0d : XamlMath.Clamp((Value - Minimum) / (Maximum - Minimum), 0d, 1d);

    /// <inheritdoc />
    protected override bool HitTestContent(PointF point) => IsHitTestVisible;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var templated = IsTemplated ? MeasureTemplate(availableSize) : Size.Empty;
        return new Size(
            Math.Max(availableSize.Width > 0 ? availableSize.Width : 120, templated.Width),
            Math.Max(Math.Max(6, Math.Max(0, availableSize.Height)), templated.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(finalSize);
        if (!IsTemplated)
        {
            return;
        }

        var track = GetTemplateChild<VisualElement>("PART_Track") is { } trackPart
            ? RangeParts.ToControlBounds(this, trackPart)
            : new RectangleF(0f, 0f, finalSize.Width, finalSize.Height);
        if (track.Width <= 0f)
        {
            track = new RectangleF(track.X, track.Y, Math.Max(0f, finalSize.Width), track.Height);
        }

        if (GetTemplateChild<VisualElement>("PART_Fill") is { } fill)
        {
            RangeParts.Arrange(
                this,
                fill,
                new RectangleF(
                    track.X,
                    track.Y,
                    Math.Max(0f, track.Width * (float)Fraction),
                    track.Height));
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        base.OnTemplateChanged();
        InvalidateArrange();
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var theme = Theme.Current;
        var bounds = new RectangleF(0f, 0f, Math.Max(0f, Bounds.Width), Math.Max(0f, Bounds.Height));
        var radius = Math.Max(1f, bounds.Height / 2f);
        using (var track = new SolidBrush(theme.SurfaceAlt))
        using (var path = XamlDrawing.RoundedRectangle(bounds, radius))
        {
            graphics.FillPath(track, path);
        }

        var width = (float)(bounds.Width * Fraction);
        if (width <= 0f)
        {
            return;
        }

        using var fill = new SolidBrush(theme.Accent);
        using var fillPath = XamlDrawing.RoundedRectangle(
            new RectangleF(0f, 0f, width, bounds.Height),
            radius);
        graphics.FillPath(fill, fillPath);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, ValueProperty))
        {
            var coerced = XamlMath.Clamp(Value, Minimum, Maximum);
            if (Math.Abs(coerced - Value) > double.Epsilon)
            {
                SetValue(ValueProperty, coerced);
            }
        }

        if (IsTemplated
            && (ReferenceEquals(e.Property, ValueProperty)
                || ReferenceEquals(e.Property, MinimumProperty)
                || ReferenceEquals(e.Property, MaximumProperty)))
        {
            InvalidateArrange();
        }
    }
}

/// <summary>A horizontal slider with a draggable thumb.</summary>
/// <remarks>
/// A control template may replace the chrome with <c>PART_Track</c>, a stretching <c>PART_Fill</c>, a
/// <c>PART_Thumb</c> and an optional <c>PART_FocusRing</c>. The track's arranged bounds define the travel;
/// the control arranges the fill to the covered fraction and centers the thumb at the value while keeping the
/// pointer and keyboard math. Without a template it draws the same geometry itself.
/// </remarks>
public class Slider : TemplatedControl
{
    /// <summary>The lowest value.</summary>
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(Slider),
        new PropertyMetadata(0d) { AffectsRender = true });

    /// <summary>The highest value.</summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(Slider),
        new PropertyMetadata(100d) { AffectsRender = true });

    /// <summary>The current value, coerced into the range.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(Slider),
        new PropertyMetadata(0d) { AffectsRender = true });

    private bool _dragging;
    private const float ThumbRadius = 7f;
    private const float TrackHeight = 4f;

    /// <summary>Creates a focusable slider.</summary>
    public Slider()
    {
        Focusable = true;
        IsHitTestVisible = true;
        Cursor = Cursors.Hand;
        MinHeight = 24f;
    }

    /// <summary>Raised after <see cref="Value"/> changes.</summary>
    public event EventHandler? ValueChanged;

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

    /// <summary>The current value.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty)!;
        set => SetValue(ValueProperty, XamlMath.Clamp(value, Minimum, Maximum));
    }

    private double Fraction
        => Maximum <= Minimum ? 0d : XamlMath.Clamp((Value - Minimum) / (Maximum - Minimum), 0d, 1d);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var templated = IsTemplated ? MeasureTemplate(availableSize) : Size.Empty;
        var width = availableSize.Width > 0 ? availableSize.Width : 160;
        var height = Math.Max(Theme.Current.ControlHeight, (int)(ThumbRadius * 2f + 6f));
        return new Size(
            Math.Max(width, templated.Width),
            Math.Max(height, templated.Height));
    }

    /// <inheritdoc />
    protected override RectangleF GetInvalidationBounds()
    {
        // 模板部件可以画满整块；自绘时只有中间一条轨道，窄的失效区能避免整列重画。
        if (IsTemplated)
        {
            return new RectangleF(0f, 0f, Bounds.Width, Bounds.Height);
        }

        var top = Math.Max(0f, (Bounds.Height / 2f) - ThumbRadius - 4f);
        var height = Math.Min(Bounds.Height - top, (ThumbRadius * 2f) + 8f);
        return new RectangleF(0f, top, Bounds.Width, Math.Max(0f, height));
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(finalSize);
        if (!IsTemplated)
        {
            return;
        }

        var track = GetTrackRect();
        var thumbX = track.Left + (track.Width * (float)Fraction);
        if (GetTemplateChild<VisualElement>("PART_Fill") is { } fill)
        {
            RangeParts.Arrange(
                this,
                fill,
                new RectangleF(
                    track.Left,
                    track.Top,
                    Math.Max(0f, thumbX - track.Left),
                    track.Height));
        }

        var center = new PointF(thumbX, track.Top + (track.Height / 2f));
        if (GetTemplateChild<VisualElement>("PART_Thumb") is { } thumb)
        {
            RangeParts.ArrangeCentered(this, thumb, center);
        }

        if (GetTemplateChild<VisualElement>("PART_FocusRing") is { } focusRing)
        {
            RangeParts.ArrangeCentered(this, focusRing, center);
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        base.OnTemplateChanged();
        InvalidateArrange();
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var theme = Theme.Current;
        var center = Bounds.Height / 2f;
        var track = new RectangleF(ThumbRadius, center - (TrackHeight / 2f), Math.Max(0f, Bounds.Width - (ThumbRadius * 2f)), TrackHeight);
        using (var background = new SolidBrush(theme.SurfaceAlt))
        using (var path = XamlDrawing.RoundedRectangle(track, TrackHeight / 2f))
        {
            graphics.FillPath(background, path);
        }

        var thumbX = track.Left + (float)(track.Width * Fraction);
        var active = new RectangleF(track.Left, track.Top, Math.Max(0f, thumbX - track.Left), track.Height);
        if (active.Width > 0f)
        {
            using var fill = new SolidBrush(theme.Accent);
            using var path = XamlDrawing.RoundedRectangle(active, TrackHeight / 2f);
            graphics.FillPath(fill, path);
        }

        using (var thumb = new SolidBrush(IsEnabled ? theme.Accent : theme.DisabledForeground))
        {
            graphics.FillEllipse(thumb, thumbX - ThumbRadius, center - ThumbRadius, ThumbRadius * 2f, ThumbRadius * 2f);
        }

        if (IsFocusVisible)
        {
            using var pen = new Pen(theme.FocusRing, theme.FocusThicknessMedium);
            graphics.DrawEllipse(pen, thumbX - ThumbRadius - 2f, center - ThumbRadius - 2f, (ThumbRadius * 2f) + 4f, (ThumbRadius * 2f) + 4f);
        }
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
        SetFromX(args.GetPosition(this).X);
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(VisualPointerEventArgs args)
    {
        base.OnPointerMoved(args);
        if (_dragging)
        {
            SetFromX(args.GetPosition(this).X);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(VisualPointerEventArgs args)
    {
        base.OnPointerReleased(args);
        _dragging = false;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(VisualKeyEventArgs args)
    {
        base.OnKeyDown(args);
        var step = (Maximum - Minimum) / 100d;
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
            case Keys.PageDown:
                Value -= step * 10d;
                args.Handled = true;
                break;
            case Keys.PageUp:
                Value += step * 10d;
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

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, ValueProperty))
        {
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        if (IsTemplated
            && (ReferenceEquals(e.Property, ValueProperty)
                || ReferenceEquals(e.Property, MinimumProperty)
                || ReferenceEquals(e.Property, MaximumProperty)))
        {
            InvalidateArrange();
        }
    }

    // 模板轨道的已排矩形是拖动的行程；没有 PART_Track 时退回自绘轨道的内缩矩形。
    private RectangleF GetTrackRect()
    {
        if (IsTemplated && GetTemplateChild<VisualElement>("PART_Track") is { } trackPart)
        {
            var arranged = RangeParts.ToControlBounds(this, trackPart);
            if (arranged.Width > 0f)
            {
                return arranged;
            }
        }

        return new RectangleF(
            ThumbRadius,
            Math.Max(0f, (Bounds.Height - TrackHeight) / 2f),
            Math.Max(0f, Bounds.Width - (ThumbRadius * 2f)),
            TrackHeight);
    }

    private void SetFromX(float x)
    {
        var track = GetTrackRect();
        var fraction = track.Width <= 0f
            ? 0f
            : XamlMath.Clamp((x - track.Left) / track.Width, 0f, 1f);
        Value = Minimum + ((Maximum - Minimum) * fraction);
    }
}

// 模板部件可能嵌在面板里：换算到控件坐标系，再把控件坐标系的槽减回部件父级坐标。
internal static class RangeParts
{
    internal static RectangleF ToControlBounds(VisualElement owner, VisualElement part)
    {
        var origin = ParentOrigin(owner, part);
        return new RectangleF(
            part.Bounds.X + origin.X,
            part.Bounds.Y + origin.Y,
            part.Bounds.Width,
            part.Bounds.Height);
    }

    internal static void Arrange(VisualElement owner, VisualElement part, RectangleF rect)
    {
        var origin = ParentOrigin(owner, part);
        part.Arrange(new RectangleF(
            rect.X - origin.X,
            rect.Y - origin.Y,
            rect.Width,
            rect.Height));
    }

    internal static void ArrangeCentered(VisualElement owner, VisualElement part, PointF center)
    {
        var size = part.DesiredSize;
        Arrange(
            owner,
            part,
            new RectangleF(
                center.X - (size.Width / 2f),
                center.Y - (size.Height / 2f),
                size.Width,
                size.Height));
    }

    private static PointF ParentOrigin(VisualElement owner, VisualElement part)
    {
        var origin = PointF.Empty;
        for (var current = part.ParentElement; current is not null && !ReferenceEquals(current, owner); current = current.ParentElement)
        {
            origin.X += current.Bounds.X;
            origin.Y += current.Bounds.Y;
        }

        return origin;
    }
}

/// <summary>A button that repeats its click while the pointer stays down.</summary>
public class RepeatButton : Button
{
    private readonly System.Windows.Forms.Timer _timer = new();
    private const int Delay = 400;
    private const int Interval = 80;

    /// <summary>Creates a repeating button.</summary>
    public RepeatButton()
    {
        _timer.Tick += (_, _) =>
        {
            _timer.Interval = Interval;
            OnClick();
        };
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        base.OnPointerPressed(args);
        if (IsEnabled && args.Button == MouseButtons.Left)
        {
            _timer.Interval = Delay;
            _timer.Start();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(VisualPointerEventArgs args)
    {
        base.OnPointerReleased(args);
        _timer.Stop();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(VisualPointerEventArgs args)
    {
        base.OnPointerExited(args);
        if (Control.MouseButtons != MouseButtons.Left)
        {
            _timer.Stop();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }
}
