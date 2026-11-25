using System.Drawing;
using System.Windows.Forms;
using Easel;
using XamlBrush = Easel.Brush;
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
    private VisualElement? _trackPart;
    private VisualElement? _fillPart;
    private ProgressBarChrome? _chrome;
    private RectangleF _trackRect;
    private RectangleF? _valueDamage;
    private bool _trackRectValid;
    private bool _partsResolved;

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
        => RangeParts.FractionOf(Value, Minimum, Maximum);

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
        ArrangeTemplateParts(finalSize, refreshTrack: true);
    }

    private void ArrangeTemplateParts(SizeF finalSize, bool refreshTrack)
    {
        if (!IsTemplated)
        {
            return;
        }

        ResolveParts();
        if (_chrome is { } chrome)
        {
            chrome.Fraction = Fraction;
        }

        var track = refreshTrack || !_trackRectValid ? GetTrackRect() : _trackRect;
        if (track.Width <= 0f)
        {
            track = new RectangleF(track.X, track.Y, Math.Max(0f, finalSize.Width), track.Height);
        }

        _trackRect = track;
        _trackRectValid = true;

        if (_fillPart is { } fill)
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

    private RectangleF GetTrackRect()
    {
        if (IsTemplated)
        {
            ResolveParts();
            if (_trackPart is { } trackPart)
            {
                var arranged = RangeParts.ToControlBounds(this, trackPart);
                if (arranged.Width > 0f)
                {
                    return arranged;
                }
            }
        }

        return new RectangleF(0f, 0f, Bounds.Width, Bounds.Height);
    }

    private void ResolveParts()
    {
        if (_partsResolved)
        {
            return;
        }

        _trackPart = GetTemplateChild<VisualElement>("PART_Track");
        _fillPart = GetTemplateChild<VisualElement>("PART_Fill");
        _chrome = GetTemplateChild<ProgressBarChrome>("PART_Chrome");
        _partsResolved = true;
    }

    /// <inheritdoc />
    protected override void OnTemplateChanging()
    {
        base.OnTemplateChanging();
        _trackPart = null;
        _fillPart = null;
        _chrome = null;
        _trackRectValid = false;
        _partsResolved = false;
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        base.OnTemplateChanged();
        ResolveParts();
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
    protected override RectangleF GetInvalidationBounds()
        => _valueDamage ?? base.GetInvalidationBounds();

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        _valueDamage = ComputeValueDamage(e);
        base.OnPropertyChanged(e);
        _valueDamage = null;

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
            // 值变化只移动模板部件，不让整棵模板重排；拖动时每帧一次 Arrange 会放大到整页布局。
            if (Bounds.Width > 0f && Bounds.Height > 0f)
            {
                ArrangeTemplateParts(Bounds.Size, refreshTrack: false);
            }
            else
            {
                InvalidateArrange();
            }
        }
    }

    // 默认铬件只在填充末端变化；值变化只脏这段，不必重画整条轨道和同带相邻节点。
    private RectangleF? ComputeValueDamage(DependencyPropertyChangedEventArgs e)
    {
        if (IsTemplated && _chrome is null)
        {
            return null;
        }

        if (!ReferenceEquals(e.Property, ValueProperty)
            && !ReferenceEquals(e.Property, MinimumProperty)
            && !ReferenceEquals(e.Property, MaximumProperty))
        {
            return null;
        }

        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0f || height <= 0f)
        {
            return null;
        }

        var oldMinimum = Minimum;
        var oldMaximum = Maximum;
        var oldValue = Value;
        var newValue = Value;
        if (ReferenceEquals(e.Property, MinimumProperty))
        {
            oldMinimum = (double)e.OldValue!;
        }
        else if (ReferenceEquals(e.Property, MaximumProperty))
        {
            oldMaximum = (double)e.OldValue!;
        }
        else
        {
            oldValue = (double)e.OldValue!;
            newValue = (double)e.NewValue!;
        }

        var oldFraction = RangeParts.FractionOf(oldValue, oldMinimum, oldMaximum);
        var newFraction = RangeParts.FractionOf(newValue, Minimum, Maximum);
        var oldFill = width * (float)oldFraction;
        var newFill = width * (float)newFraction;
        var reach = Math.Max(2f, CornerRadius + 1f);
        var left = Math.Max(0f, Math.Min(oldFill, newFill) - reach);
        var right = Math.Min(width, Math.Max(oldFill, newFill) + reach);
        return new RectangleF(left, 0f, Math.Max(1f, right - left), height);
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
    private VisualElement? _trackPart;
    private VisualElement? _fillPart;
    private VisualElement? _thumbPart;
    private VisualElement? _focusRingPart;
    private SliderChrome? _chrome;
    private RectangleF _trackRect;
    private RectangleF? _valueDamage;
    private bool _trackRectValid;
    private bool _partsResolved;
    private bool _dragging;

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

    internal const float ThumbRadius = 7f;
    internal const float TrackHeight = 4f;

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
        => RangeParts.FractionOf(Value, Minimum, Maximum);

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
        if (_valueDamage is { } valueDamage)
        {
            return valueDamage;
        }

        // 模板部件可以画满整块；默认铬件和自绘一样只占中间轨道带，窄的失效区能避免整列重画。
        if (IsTemplated && _chrome is null)
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
        ArrangeTemplateParts(finalSize, refreshTrack: true);
    }

    private void ArrangeTemplateParts(SizeF finalSize, bool refreshTrack)
    {
        if (!IsTemplated)
        {
            return;
        }

        ResolveParts();
        if (_chrome is { } chrome)
        {
            chrome.Fraction = Fraction;
        }

        var track = refreshTrack || !_trackRectValid ? GetTrackRect() : _trackRect;
        _trackRect = track;
        _trackRectValid = true;

        var thumbX = track.Left + (track.Width * (float)Fraction);
        if (_fillPart is { } fill)
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
        if (_thumbPart is { } thumb)
        {
            RangeParts.ArrangeCentered(this, thumb, center);
        }

        if (_focusRingPart is { } focusRing)
        {
            RangeParts.ArrangeCentered(this, focusRing, center);
        }
    }

    private void ResolveParts()
    {
        if (_partsResolved)
        {
            return;
        }

        _trackPart = GetTemplateChild<VisualElement>("PART_Track");
        _fillPart = GetTemplateChild<VisualElement>("PART_Fill");
        _thumbPart = GetTemplateChild<VisualElement>("PART_Thumb");
        _focusRingPart = GetTemplateChild<VisualElement>("PART_FocusRing");
        _chrome = GetTemplateChild<SliderChrome>("PART_Chrome");
        _partsResolved = true;
    }

    /// <inheritdoc />
    protected override void OnTemplateChanging()
    {
        base.OnTemplateChanging();
        _trackPart = null;
        _fillPart = null;
        _thumbPart = null;
        _focusRingPart = null;
        _chrome = null;
        _trackRectValid = false;
        _partsResolved = false;
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        base.OnTemplateChanged();
        ResolveParts();
        // 换模板可能把整块外观换成另一个形状：整块失效一次，旧的满幅像素才不会剩下。
        if (Bounds.Width > 0f && Bounds.Height > 0f)
        {
            InvalidateVisual(new RectangleF(0f, 0f, Bounds.Width, Bounds.Height));
        }

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
        _valueDamage = ComputeValueDamage(e);
        base.OnPropertyChanged(e);
        _valueDamage = null;

        if (ReferenceEquals(e.Property, ValueProperty))
        {
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        if (IsTemplated
            && (ReferenceEquals(e.Property, ValueProperty)
                || ReferenceEquals(e.Property, MinimumProperty)
                || ReferenceEquals(e.Property, MaximumProperty)))
        {
            // 值变化只移动模板部件，不让整棵模板重排；拖动时每帧一次 Arrange 会放大到整页布局。
            if (Bounds.Width > 0f && Bounds.Height > 0f)
            {
                ArrangeTemplateParts(Bounds.Size, refreshTrack: false);
            }
            else
            {
                InvalidateArrange();
            }
        }
    }

    // 默认铬件的轨道不变，只有填充末端、拇指和焦点环会移动：脏区取旧/新位置的并集。
    private RectangleF? ComputeValueDamage(DependencyPropertyChangedEventArgs e)
    {
        if (IsTemplated && _chrome is null)
        {
            return null;
        }

        if (!ReferenceEquals(e.Property, ValueProperty)
            && !ReferenceEquals(e.Property, MinimumProperty)
            && !ReferenceEquals(e.Property, MaximumProperty))
        {
            return null;
        }

        var width = Bounds.Width;
        var height = Bounds.Height;
        var trackWidth = width - (ThumbRadius * 2f);
        if (width <= 0f || height <= 0f || trackWidth <= 0f)
        {
            return null;
        }

        var oldMinimum = Minimum;
        var oldMaximum = Maximum;
        var oldValue = Value;
        var newValue = Value;
        if (ReferenceEquals(e.Property, MinimumProperty))
        {
            oldMinimum = (double)e.OldValue!;
        }
        else if (ReferenceEquals(e.Property, MaximumProperty))
        {
            oldMaximum = (double)e.OldValue!;
        }
        else
        {
            oldValue = (double)e.OldValue!;
            newValue = (double)e.NewValue!;
        }

        var oldFraction = RangeParts.FractionOf(oldValue, oldMinimum, oldMaximum);
        var newFraction = RangeParts.FractionOf(newValue, Minimum, Maximum);
        var oldThumb = ThumbRadius + (trackWidth * (float)oldFraction);
        var newThumb = ThumbRadius + (trackWidth * (float)newFraction);
        var reach = ThumbRadius + 5f;
        var left = Math.Max(0f, Math.Min(oldThumb, newThumb) - reach);
        var right = Math.Min(width, Math.Max(oldThumb, newThumb) + reach);
        var center = height / 2f;
        var top = Math.Max(0f, center - reach);
        var bottom = Math.Min(height, center + reach);
        return new RectangleF(left, top, Math.Max(1f, right - left), Math.Max(1f, bottom - top));
    }

    // 模板轨道的已排矩形是拖动的行程；没有 PART_Track 时退回自绘轨道的内缩矩形。
    private RectangleF GetTrackRect()
    {
        if (IsTemplated && _trackPart is { } trackPart)
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

// Slider 的默认外观：轨道、填充、拇指、焦点环一次 OnRender 画完。
// 拆成四个模板零件时，值变化要多走四遍子树绘制与 GDI+ 状态栈；单一铬件把同一几何压成一次绘制。
internal sealed class SliderChrome : VisualElement
{
    internal static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(XamlBrush), typeof(SliderChrome),
        new PropertyMetadata(null) { AffectsRender = true });

    internal static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush), typeof(XamlBrush), typeof(SliderChrome),
        new PropertyMetadata(null) { AffectsRender = true });

    internal static readonly DependencyProperty ThumbBrushProperty = DependencyProperty.Register(
        nameof(ThumbBrush), typeof(XamlBrush), typeof(SliderChrome),
        new PropertyMetadata(null) { AffectsRender = true });

    internal static readonly DependencyProperty FocusRingBrushProperty = DependencyProperty.Register(
        nameof(FocusRingBrush), typeof(XamlBrush), typeof(SliderChrome),
        new PropertyMetadata(null) { AffectsRender = true });

    internal static readonly DependencyProperty FractionProperty = DependencyProperty.Register(
        nameof(Fraction), typeof(double), typeof(SliderChrome),
        // 值变化由 Slider 自己按旧/新位置报脏区；铬件再报一次整块会把脏区扩回整条轨道。
        new PropertyMetadata(0d) { AffectsRender = false });

    internal static readonly DependencyProperty ShowFocusRingProperty = DependencyProperty.Register(
        nameof(ShowFocusRing), typeof(bool), typeof(SliderChrome),
        new PropertyMetadata(false) { AffectsRender = true });

    internal static readonly DependencyProperty FocusRingThicknessProperty = DependencyProperty.Register(
        nameof(FocusRingThickness), typeof(double), typeof(SliderChrome),
        new PropertyMetadata(1.4d) { AffectsRender = true });

    internal SliderChrome()
    {
        IsHitTestVisible = false;
    }

    internal XamlBrush? TrackBrush
    {
        get => (XamlBrush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    internal XamlBrush? FillBrush
    {
        get => (XamlBrush?)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    internal XamlBrush? ThumbBrush
    {
        get => (XamlBrush?)GetValue(ThumbBrushProperty);
        set => SetValue(ThumbBrushProperty, value);
    }

    internal XamlBrush? FocusRingBrush
    {
        get => (XamlBrush?)GetValue(FocusRingBrushProperty);
        set => SetValue(FocusRingBrushProperty, value);
    }

    internal double Fraction
    {
        get => (double)GetValue(FractionProperty)!;
        set => SetValue(FractionProperty, XamlMath.Clamp(value, 0d, 1d));
    }

    internal bool ShowFocusRing
    {
        get => (bool)GetValue(ShowFocusRingProperty)!;
        set => SetValue(ShowFocusRingProperty, value);
    }

    internal double FocusRingThickness
    {
        get => (double)GetValue(FocusRingThicknessProperty)!;
        set => SetValue(FocusRingThicknessProperty, value);
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        var center = height / 2f;
        var track = new RectangleF(
            Slider.ThumbRadius,
            center - (Slider.TrackHeight / 2f),
            Math.Max(0f, width - (Slider.ThumbRadius * 2f)),
            Slider.TrackHeight);
        var radius = Slider.TrackHeight / 2f;
        if (TrackBrush is { } trackBrush)
        {
            using var path = XamlDrawing.RoundedRectangle(track, radius);
            trackBrush.FillPath(graphics, path);
        }

        var thumbX = track.Left + (track.Width * (float)Fraction);
        var fillWidth = Math.Max(0f, thumbX - track.Left);
        if (fillWidth > 0f && FillBrush is { } fillBrush)
        {
            using var path = XamlDrawing.RoundedRectangle(
                new RectangleF(track.Left, track.Top, fillWidth, track.Height),
                radius);
            fillBrush.FillPath(graphics, path);
        }

        if (ThumbBrush is { } thumbBrush)
        {
            FillEllipse(
                graphics,
                thumbBrush,
                thumbX - Slider.ThumbRadius,
                center - Slider.ThumbRadius,
                Slider.ThumbRadius * 2f,
                Slider.ThumbRadius * 2f);
        }

        if (ShowFocusRing && FocusRingBrush is { } ringBrush)
        {
            var ringRadius = Slider.ThumbRadius + 2f;
            using var pen = new Pen(ringBrush, (float)FocusRingThickness);
            graphics.DrawEllipse(
                pen,
                thumbX - ringRadius,
                center - ringRadius,
                ringRadius * 2f,
                ringRadius * 2f);
        }
    }

    private static void FillEllipse(Graphics graphics, XamlBrush brush, float x, float y, float width, float height)
    {
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        var native = brush.AcquireNativeBrush(new RectangleF(x, y, width, height), out var owns);
        if (native is null)
        {
            return;
        }

        try
        {
            graphics.FillEllipse(native, x, y, width, height);
        }
        finally
        {
            if (owns)
            {
                native.Dispose();
            }
        }
    }
}

// ProgressBar 的默认外观：整块轨道与按比例覆盖的填充，同样一次 OnRender。
internal sealed class ProgressBarChrome : VisualElement
{
    internal static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(XamlBrush), typeof(ProgressBarChrome),
        new PropertyMetadata(null) { AffectsRender = true });

    internal static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush), typeof(XamlBrush), typeof(ProgressBarChrome),
        new PropertyMetadata(null) { AffectsRender = true });

    internal static readonly DependencyProperty FractionProperty = DependencyProperty.Register(
        nameof(Fraction), typeof(double), typeof(ProgressBarChrome),
        // 填充末端由 ProgressBar 报脏；铬件不重复扩大脏区。
        new PropertyMetadata(0d) { AffectsRender = false });

    internal ProgressBarChrome()
    {
        IsHitTestVisible = false;
    }

    internal XamlBrush? TrackBrush
    {
        get => (XamlBrush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    internal XamlBrush? FillBrush
    {
        get => (XamlBrush?)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    internal double Fraction
    {
        get => (double)GetValue(FractionProperty)!;
        set => SetValue(FractionProperty, XamlMath.Clamp(value, 0d, 1d));
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        var radius = Math.Max(0f, CornerRadius);
        var bounds = new RectangleF(0f, 0f, width, height);
        if (TrackBrush is { } trackBrush)
        {
            using var path = XamlDrawing.RoundedRectangle(bounds, radius);
            trackBrush.FillPath(graphics, path);
        }

        var fillWidth = Math.Max(0f, width * (float)Fraction);
        if (fillWidth > 0f && FillBrush is { } fillBrush)
        {
            using var path = XamlDrawing.RoundedRectangle(new RectangleF(0f, 0f, fillWidth, height), radius);
            fillBrush.FillPath(graphics, path);
        }
    }
}

// 模板部件可能嵌在面板里：换算到控件坐标系，再把控件坐标系的槽减回部件父级坐标。
internal static class RangeParts
{
    internal static double FractionOf(double value, double minimum, double maximum)
        => maximum <= minimum ? 0d : XamlMath.Clamp((value - minimum) / (maximum - minimum), 0d, 1d);

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
