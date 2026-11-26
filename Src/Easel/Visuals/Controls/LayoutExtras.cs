using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Easel;
using XamlBrush = Easel.Brush;
using XamlThickness = Easel.Controls.Thickness;
using XamlVisibility = Easel.Controls.Visibility;
using XamlStretch = Easel.Controls.Stretch;

namespace Easel.Visuals.Controls;

/// <summary>A grid of equally sized cells.</summary>
public class UniformGrid : Panel
{
    /// <summary>The fixed row count; 0 derives it from the children and <see cref="Columns"/>.</summary>
    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows), typeof(int), typeof(UniformGrid),
        new PropertyMetadata(0) { AffectsMeasure = true, AffectsArrange = true, AffectsRender = false });

    /// <summary>The fixed column count; 0 derives it from the children and <see cref="Rows"/>.</summary>
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(int), typeof(UniformGrid),
        new PropertyMetadata(0) { AffectsMeasure = true, AffectsArrange = true, AffectsRender = false });

    /// <summary>Creates an empty uniform grid.</summary>
    public UniformGrid()
    {
        IsHitTestVisible = true;
    }

    /// <summary>The fixed row count; 0 derives it.</summary>
    public int Rows
    {
        get => (int)GetValue(RowsProperty)!;
        set => SetValue(RowsProperty, value);
    }

    /// <summary>The fixed column count; 0 derives it.</summary>
    public int Columns
    {
        get => (int)GetValue(ColumnsProperty)!;
        set => SetValue(ColumnsProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var (rows, columns) = ResolveShape();
        var cellWidth = 0;
        var cellHeight = 0;
        foreach (var child in Children)
        {
            if (child is not VisualElement element || element.Visibility == XamlVisibility.Collapsed)
            {
                continue;
            }

            var measured = element.Measure(new Size(
                columns > 0 ? Math.Max(0, availableSize.Width / columns) : availableSize.Width,
                rows > 0 ? Math.Max(0, availableSize.Height / rows) : availableSize.Height));
            cellWidth = Math.Max(cellWidth, measured.Width);
            cellHeight = Math.Max(cellHeight, measured.Height);
        }

        return new Size((cellWidth * columns) + (int)Math.Ceiling(Padding.Horizontal), (cellHeight * rows) + (int)Math.Ceiling(Padding.Vertical));
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        var (rows, columns) = ResolveShape();
        if (rows == 0 || columns == 0)
        {
            return;
        }

        var cellWidth = Math.Max(0f, (finalSize.Width - Padding.Horizontal) / columns);
        var cellHeight = Math.Max(0f, (finalSize.Height - Padding.Vertical) / rows);
        var index = 0;
        foreach (var child in Children)
        {
            if (child is not VisualElement element || element.Visibility == XamlVisibility.Collapsed)
            {
                continue;
            }

            var row = index / columns;
            var column = index % columns;
            element.Arrange(new RectangleF(
                (int)Math.Ceiling(Padding.Left) + (column * cellWidth),
                (int)Math.Ceiling(Padding.Top) + (row * cellHeight),
                cellWidth,
                cellHeight));
            index++;
        }
    }

    private (int Rows, int Columns) ResolveShape()
    {
        var count = 0;
        foreach (var child in Children)
        {
            if (child is VisualElement { Visibility: not XamlVisibility.Collapsed })
            {
                count++;
            }
        }

        if (count == 0)
        {
            return (Math.Max(0, Rows), Math.Max(0, Columns));
        }

        if (Rows > 0 && Columns > 0)
        {
            return (Rows, Columns);
        }

        if (Columns > 0)
        {
            return ((count + Columns - 1) / Columns, Columns);
        }

        if (Rows > 0)
        {
            return (Rows, (count + Rows - 1) / Rows);
        }

        var autoColumns = (int)Math.Ceiling(Math.Sqrt(count));
        return ((count + autoColumns - 1) / autoColumns, autoColumns);
    }
}

/// <summary>Scales one child uniformly into the available rectangle.</summary>
/// <remarks>
/// The scale is a <see cref="Visual.Transform"/> on the child. Hit testing follows it in
/// <see cref="Visual.HitTestVisual"/>, and <see cref="VisualElement.PointFromSurface"/> inverts it with the rest of
/// the ancestor chain; the viewbox is still intended for static cards.
/// </remarks>
public class Viewbox : VisualElement
{
    /// <summary>The scaled child.</summary>
    public static readonly DependencyProperty ChildProperty = DependencyProperty.Register(
        nameof(Child), typeof(VisualElement), typeof(Viewbox),
        new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>How the child is fitted into the box.</summary>
    public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
        nameof(Stretch), typeof(XamlStretch), typeof(Viewbox),
        new PropertyMetadata(XamlStretch.Uniform) { AffectsMeasure = true, AffectsArrange = true, AffectsRender = false });

    /// <summary>Creates a non-hit-testable viewbox.</summary>
    public Viewbox()
    {
        IsHitTestVisible = false;
    }

    /// <summary>The scaled child.</summary>
    public VisualElement? Child
    {
        get => (VisualElement?)GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    /// <summary>How the child is fitted into the box.</summary>
    public XamlStretch Stretch
    {
        get => (XamlStretch)GetValue(StretchProperty)!;
        set => SetValue(StretchProperty, value);
    }

    // XAML can put the child in either place: the explicit Child property or the first visual child. Both forms
    // are treated as the scaled content, so `<Viewbox><Grid .../></Viewbox>` works like the other platforms.
    private VisualElement? EffectiveChild
        => Child ?? (Children.Count > 0 ? Children[0] as VisualElement : null);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
        => EffectiveChild?.Measure(availableSize) ?? Size.Empty;

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        if (EffectiveChild is not { } child)
        {
            return;
        }

        var desired = child.DesiredSize;
        if (desired.Width <= 0 || desired.Height <= 0)
        {
            child.Arrange(new RectangleF(0f, 0f, finalSize.Width, finalSize.Height));
            child.Transform = new Matrix();
            return;
        }

        child.Arrange(new RectangleF(0f, 0f, desired.Width, desired.Height));
        var scaleX = finalSize.Width / (float)desired.Width;
        var scaleY = finalSize.Height / (float)desired.Height;
        var scale = Stretch switch
        {
            XamlStretch.Fill => 0f,
            XamlStretch.UniformToFill => Math.Max(scaleX, scaleY),
            XamlStretch.None => 1f,
            _ => Math.Min(scaleX, scaleY),
        };

        if (scale <= 0f)
        {
            child.Transform = new Matrix(scaleX, 0f, 0f, scaleY, 0f, 0f);
            return;
        }

        var offsetX = (finalSize.Width - (desired.Width * scale)) / 2f;
        var offsetY = (finalSize.Height - (desired.Height * scale)) / 2f;
        child.Transform = new Matrix(scale, 0f, 0f, scale, offsetX, offsetY);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!ReferenceEquals(e.Property, ChildProperty))
        {
            return;
        }

        if (e.OldValue is VisualElement old && Children.Contains(old))
        {
            RemoveChild(old);
        }

        if (e.NewValue is VisualElement next)
        {
            if (next.Parent is ContainerVisual parent)
            {
                parent.Remove(next);
            }

            Add(next);
        }

        InvalidateMeasure();
    }
}

/// <summary>A single-child container with padding and nothing else.</summary>
public class Decorator : VisualElement
{
    /// <summary>The decorated child.</summary>
    public static readonly DependencyProperty ChildProperty = DependencyProperty.Register(
        nameof(Child), typeof(VisualElement), typeof(Decorator),
        new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>Creates a transparent decorator.</summary>
    public Decorator()
    {
        IsHitTestVisible = false;
    }

    /// <summary>The decorated child.</summary>
    public VisualElement? Child
    {
        get => (VisualElement?)GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        var inner = new Size(
            Math.Max(0, availableSize.Width - (int)Math.Ceiling(padding.Horizontal)),
            Math.Max(0, availableSize.Height - (int)Math.Ceiling(padding.Vertical)));
        var desired = Child?.Measure(inner) ?? Size.Empty;
        return new Size(
            desired.Width + (int)Math.Ceiling(padding.Horizontal),
            desired.Height + (int)Math.Ceiling(padding.Vertical));
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
        => Child?.Arrange(new RectangleF(
            (int)Math.Ceiling(Padding.Left),
            (int)Math.Ceiling(Padding.Top),
            Math.Max(0f, finalSize.Width - Padding.Horizontal),
            Math.Max(0f, finalSize.Height - Padding.Vertical)));

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!ReferenceEquals(e.Property, ChildProperty))
        {
            return;
        }

        if (e.OldValue is VisualElement old && Children.Contains(old))
        {
            RemoveChild(old);
        }

        if (e.NewValue is VisualElement next)
        {
            if (next.Parent is ContainerVisual parent)
            {
                parent.Remove(next);
            }

            Add(next);
        }

        InvalidateMeasure();
    }
}

/// <summary>A header line above one content element.</summary>
/// <remarks>
/// The default template parts are <c>PART_Header</c> (the header label) and <c>PART_ContentHost</c> (where
/// <see cref="Content"/> is parented while a template is applied).
/// </remarks>
public class HeaderedContentControl : TemplatedControl
{
    /// <summary>The header text.</summary>
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(HeaderedContentControl),
        new PropertyMetadata(string.Empty) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The content below the header.</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(VisualElement), typeof(HeaderedContentControl),
        new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The header text colour; unset uses the theme foreground.</summary>
    public static readonly DependencyProperty HeaderForegroundProperty = DependencyProperty.Register(
        nameof(HeaderForeground), typeof(XamlBrush), typeof(HeaderedContentControl),
        new PropertyMetadata(null) { AffectsRender = true });

    private const float HeaderHeight = 24f;

    private VisualElement? _contentHost;
    private VisualElement? _hostedContent;
    private TextBlock? _headerPart;
    private bool _disposing;

    /// <summary>Creates an empty header and content pair.</summary>
    public HeaderedContentControl()
    {
        IsHitTestVisible = true;
        SetChromeTheme(
            static _ => Color.Transparent,
            static _ => Color.Transparent,
            static _ => 0f);
        SetThemeMetric(HeaderForegroundProperty, static palette => new SolidColorBrush(palette.Foreground));
    }

    /// <summary>The header text.</summary>
    public string Header
    {
        get => (string)GetValue(HeaderProperty)!;
        set => SetValue(HeaderProperty, value ?? string.Empty);
    }

    /// <summary>The header text colour; unset uses the theme foreground.</summary>
    public XamlBrush? HeaderForeground
    {
        get => (XamlBrush?)GetValue(HeaderForegroundProperty);
        set => SetValue(HeaderForegroundProperty, value);
    }

    /// <summary>The content below the header.</summary>
    public VisualElement? Content
    {
        get => (VisualElement?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _disposing = disposing;
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override bool HitTestContent(PointF point) => IsHitTestVisible;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsTemplated)
        {
            return base.MeasureOverride(availableSize);
        }

        var desired = Content?.Measure(new Size(availableSize.Width, Math.Max(0, availableSize.Height - (int)HeaderHeight))) ?? Size.Empty;
        return new Size(desired.Width, desired.Height + (int)HeaderHeight);
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        if (IsTemplated)
        {
            base.ArrangeOverride(finalSize);
            return;
        }

        Content?.Arrange(new RectangleF(0f, HeaderHeight, finalSize.Width, Math.Max(0f, finalSize.Height - HeaderHeight)));
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
        => VisualText.Draw(this,
            graphics,
            Header,
            ResolveFont(DefaultFont),
            Theme.Current.MutedForeground,
            new Rectangle(0, 0, Math.Max(0, (int)Bounds.Width), (int)HeaderHeight),
            wrap: false,
            StringAlignment.Near,
            StringAlignment.Center);

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, ContentProperty))
        {
            SyncContent();
        }
        else if (ReferenceEquals(e.Property, HeaderProperty))
        {
            SyncHeader();
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanging()
    {
        if (!_disposing)
        {
            DetachHostedContent();
        }

        _contentHost = null;
        _headerPart = null;
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        _contentHost = GetTemplateChild<VisualElement>("PART_ContentHost");
        _headerPart = GetTemplateChild<TextBlock>("PART_Header");
        SyncHeader();
        SyncContent();
    }

    private void SyncHeader()
    {
        if (_headerPart is { } header)
        {
            header.Text = Header;
        }
    }

    private void SyncContent()
    {
        var content = Content;
        if (!ReferenceEquals(_hostedContent, content))
        {
            DetachHostedContent();
            _hostedContent = content;
        }

        if (content is not null)
        {
            if (_contentHost is { } host)
            {
                if (!ReferenceEquals(content.ParentElement, host))
                {
                    content.ParentElement?.RemoveChild(content);
                    host.Add(content);
                }
            }
            else if (!ReferenceEquals(content.ParentElement, this))
            {
                content.ParentElement?.RemoveChild(content);
                base.Add(content);
            }
        }

        InvalidateMeasure();
    }

    private void DetachHostedContent()
    {
        if (_hostedContent is not { } content)
        {
            return;
        }

        if (ReferenceEquals(content.ParentElement, this))
        {
            RemoveChild(content);
        }
        else if (_contentHost is { } host && ReferenceEquals(content.ParentElement, host))
        {
            host.RemoveChild(content);
        }

        _hostedContent = null;
    }
}

/// <summary>A thin sash that resizes one target element by dragging.</summary>
/// <remarks>
/// This is a layout sash, not WPF's grid-coordinate splitter: it changes <see cref="VisualElement.Width"/> (vertical
/// bar) or <see cref="VisualElement.Height"/> (horizontal bar) of <see cref="Target"/>, so put the panes in a
/// container that respects an explicit size.
/// </remarks>
public class GridSplitter : VisualElement
{
    /// <summary>The element whose width or height the sash changes.</summary>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target), typeof(VisualElement), typeof(GridSplitter),
        new PropertyMetadata(null) { AffectsRender = false });

    /// <summary>Whether the bar is vertical (resizes width) or horizontal (resizes height).</summary>
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(GridSplitter),
        new PropertyMetadata(Orientation.Vertical) { AffectsMeasure = true, AffectsRender = true });

    private bool _dragging;
    private bool _invertDelta;
    private PointF _start;
    private double _startSize;

    /// <summary>Creates a focusable sash.</summary>
    public GridSplitter()
    {
        IsHitTestVisible = true;
        Focusable = false;
        Cursor = Cursors.SizeWE;
    }

    /// <summary>The element whose width or height the sash changes.</summary>
    public VisualElement? Target
    {
        get => (VisualElement?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>Whether the bar is vertical (resizes width) or horizontal (resizes height).</summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty)!;
        set => SetValue(OrientationProperty, value);
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        if (Background is not null)
        {
            base.OnRender(graphics);
            return;
        }

        using var brush = new SolidBrush(Theme.Current.SurfaceAlt);
        graphics.FillRectangle(brush, 0, 0, Bounds.Width, Bounds.Height);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
        => Orientation == Orientation.Vertical
            ? new Size(6, Math.Max(0, availableSize.Height))
            : new Size(Math.Max(0, availableSize.Width), 6);

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        base.OnPointerPressed(args);
        if (args.Button != MouseButtons.Left || Target is null)
        {
            return;
        }

        _dragging = true;
        _start = args.Position;
        // Use the arranged size, not Width/Height: an auto-sized pane reports double.NaN there and a drag
        // could never start from its current size.
        _startSize = Orientation == Orientation.Vertical ? Target.Bounds.Width : Target.Bounds.Height;

        // A sash between two panes grows the pane the pointer moves toward. When the target sits after the
        // splitter (a right/bottom pane), the pointer delta has to be inverted.
        var targetBounds = Target.AbsoluteBounds;
        var splitterBounds = AbsoluteBounds;
        _invertDelta = Orientation == Orientation.Vertical
            ? targetBounds.Left >= splitterBounds.Left
            : targetBounds.Top >= splitterBounds.Top;
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(VisualPointerEventArgs args)
    {
        base.OnPointerMoved(args);
        if (!_dragging || Target is null)
        {
            return;
        }

        var delta = Orientation == Orientation.Vertical
            ? args.Position.X - _start.X
            : args.Position.Y - _start.Y;
        if (_invertDelta)
        {
            delta = -delta;
        }

        var next = Math.Max(Orientation == Orientation.Vertical ? 80d : 40d, _startSize + delta);
        var current = Orientation == Orientation.Vertical ? Target.Bounds.Width : Target.Bounds.Height;
        if (Math.Abs(next - current) < 0.5d)
        {
            return;
        }

        if (Orientation == Orientation.Vertical)
        {
            Target.Width = next;
        }
        else
        {
            Target.Height = next;
        }

        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(VisualPointerEventArgs args)
    {
        base.OnPointerReleased(args);
        _dragging = false;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, OrientationProperty))
        {
            Cursor = Orientation == Orientation.Vertical ? Cursors.SizeWE : Cursors.SizeNS;
            InvalidateMeasure();
        }
    }
}
