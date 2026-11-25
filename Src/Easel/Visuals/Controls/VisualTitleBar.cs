using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Easel.Visuals;
using XamlVisibility = Easel.Controls.Visibility;

namespace Easel.Visuals.Controls;

/// <summary>A modern title bar with an icon slot, a title and the minimize, maximize and close buttons.</summary>
/// <remarks>
/// <para>
/// Without a <see cref="TemplatedControl.Template"/> the bar paints itself from the active theme: the window
/// background, a one-pixel bottom border, the optional <see cref="Icon"/>, the <see cref="Title"/> text and three
/// caption buttons. Hovering and pressing the buttons redraws them; the close button turns red under the pointer.
/// </para>
/// <para>
/// A template replaces that self-drawn chrome. The control resolves <c>PART_Icon</c>, <c>PART_Title</c>,
/// <c>PART_Minimize</c>, <c>PART_Maximize</c>, <c>PART_Close</c> and the optional <c>PART_DragArea</c>. A caption
/// part should be a <see cref="Button"/>; its click changes the <see cref="Target"/> window state. Missing parts
/// are tolerated, so a template may leave every button out and act as a drag bar. Content between the icon/title and
/// the buttons is allowed and is treated as interactive when it focuses or carries a cursor.
/// </para>
/// <para>
/// Attach the surrounding window with <see cref="WindowChrome"/>. When the bar joins a <see cref="VisualHost"/> that
/// is inside a <see cref="Form"/> it resolves <see cref="Target"/> and attaches automatically; set
/// <see cref="AutoAttachChrome"/> to <see langword="false"/> to opt out, or assign <see cref="Target"/> directly.
/// </para>
/// </remarks>
public class VisualTitleBar : TemplatedControl
{
    /// <summary>The default bar height in logical pixels.</summary>
    public const double DefaultHeight = 32d;

    /// <summary>The width of one self-drawn caption button in logical pixels.</summary>
    public const float CaptionButtonWidth = 46f;

    private const float IconSize = 16f;
    private const float IconMargin = 12f;
    private const float TitleGap = 8f;

    /// <summary>The text shown in the title area; when empty, the target form's text is used.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(VisualTitleBar),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    /// <summary>An optional logo drawn in the icon slot.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(VisualElement), typeof(VisualTitleBar),
        new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>Whether the minimize button is shown; defaults to <see langword="true"/>.</summary>
    public static readonly DependencyProperty ShowMinimizeProperty = DependencyProperty.Register(
        nameof(ShowMinimize), typeof(bool), typeof(VisualTitleBar),
        new PropertyMetadata(true) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>Whether the maximize button is shown; defaults to <see langword="true"/>.</summary>
    public static readonly DependencyProperty ShowMaximizeProperty = DependencyProperty.Register(
        nameof(ShowMaximize), typeof(bool), typeof(VisualTitleBar),
        new PropertyMetadata(true) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>Whether the close button is shown; defaults to <see langword="true"/>.</summary>
    public static readonly DependencyProperty ShowCloseProperty = DependencyProperty.Register(
        nameof(ShowClose), typeof(bool), typeof(VisualTitleBar),
        new PropertyMetadata(true) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The window the caption buttons drive; resolved from the hosting surface when unset.</summary>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target), typeof(Form), typeof(VisualTitleBar),
        new PropertyMetadata(null));

    /// <summary>Whether the target window is currently maximized; read-only to callers.</summary>
    /// <remarks>Template triggers can observe this dependency property to swap a custom restore glyph.</remarks>
    public static readonly DependencyProperty IsMaximizedProperty = DependencyProperty.Register(
        nameof(IsMaximized), typeof(bool), typeof(VisualTitleBar),
        new PropertyMetadata(false) { AffectsRender = true });

    private readonly CaptionButton _minimize;
    private readonly CaptionButton _maximize;
    private readonly CaptionButton _close;

    private VisualElement? _partMinimize;
    private VisualElement? _partMaximize;
    private VisualElement? _partClose;
    private TextBlock? _partTitleText;
    private ContentControl? _partTitleContent;
    private VisualElement? _partIcon;
    private VisualElement? _dragArea;

    private Form? _autoTarget;
    private Form? _hookedTarget;
    private bool _autoAttachChrome = true;
    private bool _suppressAttach;
    private bool _disposed;

    private Font? _titleFont;
    private string? _titleFontFamily;
    private float _titleFontSize;

    private float _titleLeft = IconMargin;
    private float _titleRight = float.MaxValue;

    /// <summary>Creates a title bar with the default height and the three self-drawn caption buttons.</summary>
    public VisualTitleBar()
    {
        Height = DefaultHeight;
        IsHitTestVisible = true;

        _minimize = new CaptionButton(this, CaptionKind.Minimize) { Visibility = XamlVisibility.Visible };
        _maximize = new CaptionButton(this, CaptionKind.Maximize) { Visibility = XamlVisibility.Visible };
        _close = new CaptionButton(this, CaptionKind.Close) { Visibility = XamlVisibility.Visible };
        Add(_minimize);
        Add(_maximize);
        Add(_close);

        SurfaceRootChanged += OnSurfaceRootChanged;
        UpdateDefaultChrome();
    }

    /// <summary>The text shown in the title area; when empty, the target form's text is used.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value ?? string.Empty);
    }

    /// <summary>An optional logo drawn in the icon slot.</summary>
    public VisualElement? Icon
    {
        get => (VisualElement?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Whether the minimize button is shown.</summary>
    public bool ShowMinimize
    {
        get => (bool)GetValue(ShowMinimizeProperty)!;
        set => SetValue(ShowMinimizeProperty, value);
    }

    /// <summary>Whether the maximize button is shown.</summary>
    public bool ShowMaximize
    {
        get => (bool)GetValue(ShowMaximizeProperty)!;
        set => SetValue(ShowMaximizeProperty, value);
    }

    /// <summary>Whether the close button is shown.</summary>
    public bool ShowClose
    {
        get => (bool)GetValue(ShowCloseProperty)!;
        set => SetValue(ShowCloseProperty, value);
    }

    /// <summary>The window the caption buttons drive, or the form resolved from the hosting surface.</summary>
    public Form? Target
    {
        get => (Form?)GetValue(TargetProperty) ?? _autoTarget;
        set => SetValue(TargetProperty, value);
    }

    /// <summary>Whether the target window is currently maximized.</summary>
    public bool IsMaximized => (bool)GetValue(IsMaximizedProperty)!;

    /// <summary>Whether the bar attaches <see cref="WindowChrome"/> when it resolves a hosting form.</summary>
    /// <remarks>Defaults to <see langword="true"/>. Set it to <see langword="false"/> when the host owns the window
    /// chrome and only wants the title-bar visuals.</remarks>
    public bool AutoAttachChrome
    {
        get => _autoAttachChrome;
        set
        {
            if (_autoAttachChrome == value)
            {
                return;
            }

            _autoAttachChrome = value;
            if (!value)
            {
                if (_hookedTarget is { } form)
                {
                    form.Resize -= OnTargetResize;
                    form.TextChanged -= OnTargetTextChanged;
                    form.FormClosed -= OnTargetClosed;
                    _hookedTarget = null;
                    WindowChrome.DetachBar(form, this);
                    RefreshMaximizedState();
                    InvalidateVisual();
                }
            }
            else
            {
                RefreshTarget();
            }
        }
    }

    /// <summary>The effective title: <see cref="Title"/>, or the target form's caption when that is empty.</summary>
    public string EffectiveTitle
    {
        get
        {
            var title = Title;
            return string.IsNullOrEmpty(title) ? Target?.Text ?? string.Empty : title;
        }
    }

    // 供窗口钩子与测试写入只读的最大化状态。
    internal void SetMaximizedState(bool value) => SetValue(IsMaximizedProperty, value);

    // 外部 Attach 时把目标窗口接管过来，但不再触发一次 Attach。
    internal void AdoptChromeTarget(Form form)
    {
        if (IsSet(TargetProperty) || ReferenceEquals(_autoTarget, form))
        {
            return;
        }

        _autoTarget = form;
        _suppressAttach = true;
        try
        {
            RefreshTarget();
        }
        finally
        {
            _suppressAttach = false;
        }
    }

    // 空标题区才可拖动；模板给了 PART_DragArea 就只认它。
    internal bool ContainsDragArea(PointF surfacePoint)
    {
        if (_dragArea is { } part)
        {
            return part.AbsoluteBounds.Contains(surfacePoint);
        }

        return AbsoluteBounds.Contains(surfacePoint);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateTargetFromSurface();

        if (IsTemplated)
        {
            return MeasureTemplate(availableSize);
        }

        var height = ResolveHeight();
        if (Icon is { } icon && icon.Visibility != XamlVisibility.Collapsed)
        {
            var box = new Size((int)Math.Ceiling(IconSize), (int)Math.Ceiling(height));
            icon.Measure(box);
            if (icon.DesiredSize.Width <= 0 || icon.DesiredSize.Height <= 0)
            {
                icon.Measure(box);
            }
        }

        foreach (var button in VisibleDefaultButtons())
        {
            button.Measure(new Size((int)Math.Ceiling(CaptionButtonWidth), (int)Math.Ceiling(height)));
        }

        return new Size(availableSize.Width > 0 ? availableSize.Width : 0, (int)Math.Ceiling(height));
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        UpdateTargetFromSurface();

        if (IsTemplated)
        {
            ArrangeTemplate(finalSize);
            ArrangeIcon(finalSize, templated: true);
            return;
        }

        var height = finalSize.Height > 0 ? (float)finalSize.Height : ResolveHeight();
        var right = (float)finalSize.Width;
        foreach (var button in DefaultButtonsRightToLeft())
        {
            if (button.Visibility == XamlVisibility.Collapsed)
            {
                continue;
            }

            right -= CaptionButtonWidth;
            button.Arrange(new RectangleF(right, 0f, CaptionButtonWidth, height));
        }

        _titleRight = right;

        var left = IconMargin;
        if (Icon is { } icon)
        {
            var size = Math.Min(IconSize, Math.Max(0f, height - 8f));
            var x = IconMargin;
            icon.Arrange(new RectangleF(x, (height - size) / 2f, size, size));
            left = x + size + TitleGap;
        }

        _titleLeft = left;
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        if (IsTemplated)
        {
            return;
        }

        var theme = Theme.Current;
        var width = Math.Max(1, (int)Math.Ceiling(Bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(Bounds.Height));

        // 自绘底色只在作者没有指定 Background 时铺；指定了就由基础绘制负责。
        if (Background is null)
        {
            using var background = new SolidBrush(theme.WindowBackground);
            graphics.FillRectangle(background, 0, 0, width, height);
        }

        if (BorderThickness.IsEmpty)
        {
            using var border = new Pen(theme.ControlBorder);
            graphics.DrawLine(border, 0, height - 1, width - 1, height - 1);
        }

        var title = EffectiveTitle;
        if (string.IsNullOrEmpty(title) || _titleRight <= _titleLeft)
        {
            return;
        }

        var bounds = new RectangleF(
            _titleLeft,
            0f,
            Math.Max(0f, _titleRight - TitleGap - _titleLeft),
            height);
        VisualText.Draw(
            this,
            graphics,
            title,
            GetTitleFont(),
            theme.Foreground,
            bounds,
            wrap: false,
            StringAlignment.Near,
            StringAlignment.Center);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        base.OnPointerPressed(args);
        if (args.Handled || args.Button != MouseButtons.Left || args.ClickCount < 2)
        {
            return;
        }

        // 没有窗口钩子时（例如只在宿主里放一条标题栏）仍支持双击标题区切换最大化。
        if (Target is { } form && ContainsDragArea(args.Position))
        {
            form.WindowState = form.WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
            args.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (ReferenceEquals(e.Property, TitleProperty))
        {
            UpdatePartTitle();
            InvalidateVisual();
        }
        else if (ReferenceEquals(e.Property, IconProperty))
        {
            UpdateIconChild(e.OldValue as VisualElement, e.NewValue as VisualElement);
            InvalidateMeasure();
        }
        else if (ReferenceEquals(e.Property, ShowMinimizeProperty)
                 || ReferenceEquals(e.Property, ShowMaximizeProperty)
                 || ReferenceEquals(e.Property, ShowCloseProperty))
        {
            UpdateDefaultChrome();
            UpdatePartVisibility();
            InvalidateMeasure();
        }
        else if (ReferenceEquals(e.Property, TargetProperty))
        {
            RefreshTarget();
        }
        else if (ReferenceEquals(e.Property, IsMaximizedProperty))
        {
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanging()
    {
        UnhookTemplateParts();
        base.OnTemplateChanging();
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        base.OnTemplateChanged();

        if (IsTemplated)
        {
            _partMinimize = GetTemplateChild<VisualElement>("PART_Minimize");
            _partMaximize = GetTemplateChild<VisualElement>("PART_Maximize");
            _partClose = GetTemplateChild<VisualElement>("PART_Close");
            _partTitleText = GetTemplateChild<TextBlock>("PART_Title");
            _partTitleContent = GetTemplateChild<ContentControl>("PART_Title");
            _partIcon = GetTemplateChild<VisualElement>("PART_Icon");
            _dragArea = GetTemplateChild<VisualElement>("PART_DragArea");

            HookTemplatePart(_partMinimize, OnMinimizeClick);
            HookTemplatePart(_partMaximize, OnMaximizeClick);
            HookTemplatePart(_partClose, OnCloseClick);
        }

        UpdateDefaultChrome();
        UpdatePartVisibility();
        UpdatePartTitle();
        if (IsTemplated && Icon is { } icon && Children.Contains(icon))
        {
            // 图标要画在模板根之上；模板套用时把它挪到子级末尾。
            RemoveChild(icon);
            Add(icon);
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            SurfaceRootChanged -= OnSurfaceRootChanged;
            UnhookTemplateParts();

            if (_hookedTarget is { } form)
            {
                form.Resize -= OnTargetResize;
                form.TextChanged -= OnTargetTextChanged;
                form.FormClosed -= OnTargetClosed;
                WindowChrome.DetachBar(form, this);
                _hookedTarget = null;
            }

            _autoTarget = null;
            _titleFont?.Dispose();
            _titleFont = null;
        }

        base.Dispose(disposing);
    }

    private float ResolveHeight()
    {
        var height = Height;
        return double.IsNaN(height) || height <= 0d ? (float)DefaultHeight : (float)height;
    }

    private Font GetTitleFont()
    {
        var family = Theme.Current.FontFamily;
        if (string.IsNullOrWhiteSpace(family))
        {
            family = "Segoe UI";
        }

        const float size = 11f;
        if (_titleFont is null || !string.Equals(_titleFontFamily, family, StringComparison.Ordinal)
            || Math.Abs(_titleFontSize - size) > 0.01f)
        {
            _titleFont?.Dispose();
            _titleFont = new Font(family, size, FontStyle.Bold, GraphicsUnit.Point);
            _titleFontFamily = family;
            _titleFontSize = size;
        }

        return _titleFont;
    }

    private void OnSurfaceRootChanged(object? sender, EventArgs e) => UpdateTargetFromSurface();

    private void UpdateTargetFromSurface()
    {
        if (_disposed || IsSet(TargetProperty))
        {
            return;
        }

        var form = Surface?.FindForm();
        if (ReferenceEquals(form, _autoTarget))
        {
            return;
        }

        _autoTarget = form;
        RefreshTarget();
    }

    private void RefreshTarget()
    {
        var effective = (Form?)GetValue(TargetProperty) ?? _autoTarget;
        if (effective is { IsDisposed: true })
        {
            effective = null;
        }

        if (ReferenceEquals(effective, _hookedTarget))
        {
            RefreshMaximizedState();
            return;
        }

        var old = _hookedTarget;
        _hookedTarget = effective;

        if (old is not null)
        {
            old.Resize -= OnTargetResize;
            old.TextChanged -= OnTargetTextChanged;
            old.FormClosed -= OnTargetClosed;
            WindowChrome.DetachBar(old, this);
        }

        if (effective is not null)
        {
            effective.Resize += OnTargetResize;
            effective.TextChanged += OnTargetTextChanged;
            effective.FormClosed += OnTargetClosed;
            if (_autoAttachChrome && !_suppressAttach)
            {
                WindowChrome.Attach(effective, this);
            }
        }

        RefreshMaximizedState();
        InvalidateVisual();
    }

    private void RefreshMaximizedState()
        => SetValue(IsMaximizedProperty, _hookedTarget?.WindowState == FormWindowState.Maximized);

    private void OnTargetResize(object? sender, EventArgs e)
    {
        RefreshMaximizedState();
        InvalidateVisual();
    }

    private void OnTargetTextChanged(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(Title))
        {
            UpdatePartTitle();
            InvalidateVisual();
        }
    }

    private void OnTargetClosed(object? sender, FormClosedEventArgs e)
    {
        if (sender is not Form closed || !ReferenceEquals(closed, _hookedTarget))
        {
            return;
        }

        closed.Resize -= OnTargetResize;
        closed.TextChanged -= OnTargetTextChanged;
        closed.FormClosed -= OnTargetClosed;
        _hookedTarget = null;
        if (ReferenceEquals(_autoTarget, closed))
        {
            _autoTarget = null;
        }

        if (ReferenceEquals(GetValue(TargetProperty), closed))
        {
            ClearValue(TargetProperty);
        }

        RefreshMaximizedState();
        InvalidateVisual();
    }

    private void OnMinimizeClick(object? sender, EventArgs e)
    {
        if (Target is { } form)
        {
            form.WindowState = FormWindowState.Minimized;
        }
    }

    private void OnMaximizeClick(object? sender, EventArgs e)
    {
        if (Target is not { } form)
        {
            return;
        }

        form.WindowState = form.WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
    }

    private void OnCloseClick(object? sender, EventArgs e) => Target?.Close();

    private void UpdateDefaultChrome()
    {
        var allow = !IsTemplated;
        _minimize.Visibility = allow && ShowMinimize ? XamlVisibility.Visible : XamlVisibility.Collapsed;
        _maximize.Visibility = allow && ShowMaximize ? XamlVisibility.Visible : XamlVisibility.Collapsed;
        _close.Visibility = allow && ShowClose ? XamlVisibility.Visible : XamlVisibility.Collapsed;
    }

    private void UpdatePartVisibility()
    {
        SetPartVisibility(_partMinimize, ShowMinimize);
        SetPartVisibility(_partMaximize, ShowMaximize);
        SetPartVisibility(_partClose, ShowClose);
    }

    private static void SetPartVisibility(VisualElement? part, bool visible)
    {
        if (part is not null)
        {
            part.Visibility = visible ? XamlVisibility.Visible : XamlVisibility.Collapsed;
        }
    }

    private void UpdatePartTitle()
    {
        var title = EffectiveTitle;
        if (_partTitleText is not null && !string.Equals(_partTitleText.Text, title, StringComparison.Ordinal))
        {
            _partTitleText.Text = title;
        }

        if (_partTitleContent is not null
            && !Equals(_partTitleContent.Content, title))
        {
            _partTitleContent.Content = title;
        }
    }

    private void UpdateIconChild(VisualElement? oldIcon, VisualElement? newIcon)
    {
        if (oldIcon is not null && ReferenceEquals(oldIcon.ParentElement, this))
        {
            RemoveChild(oldIcon);
        }

        if (newIcon is not null && !ReferenceEquals(newIcon.ParentElement, this))
        {
            Add(newIcon);
        }
    }

    private void ArrangeIcon(Size finalSize, bool templated)
    {
        if (Icon is not { } icon || icon.Visibility == XamlVisibility.Collapsed)
        {
            return;
        }

        var height = finalSize.Height > 0 ? finalSize.Height : (float)ResolveHeight();
        if (templated && _partIcon is { } part)
        {
            var origin = PointFromSurface(part.PointToSurface(PointF.Empty));
            icon.Arrange(new RectangleF(origin.X, origin.Y, part.Bounds.Width, part.Bounds.Height));
            return;
        }

        var size = Math.Min(IconSize, Math.Max(0f, height - 8f));
        icon.Arrange(new RectangleF(IconMargin, (height - size) / 2f, size, size));
    }

    private IEnumerable<CaptionButton> VisibleDefaultButtons()
    {
        if (ShowMinimize && _minimize.Visibility != XamlVisibility.Collapsed)
        {
            yield return _minimize;
        }

        if (ShowMaximize && _maximize.Visibility != XamlVisibility.Collapsed)
        {
            yield return _maximize;
        }

        if (ShowClose && _close.Visibility != XamlVisibility.Collapsed)
        {
            yield return _close;
        }
    }

    private IEnumerable<CaptionButton> DefaultButtonsRightToLeft()
    {
        if (ShowClose && _close.Visibility != XamlVisibility.Collapsed)
        {
            yield return _close;
        }

        if (ShowMaximize && _maximize.Visibility != XamlVisibility.Collapsed)
        {
            yield return _maximize;
        }

        if (ShowMinimize && _minimize.Visibility != XamlVisibility.Collapsed)
        {
            yield return _minimize;
        }
    }

    private void HookTemplatePart(VisualElement? part, EventHandler handler)
    {
        if (part is Button button)
        {
            button.Click += handler;
        }
    }

    private void UnhookTemplateParts()
    {
        if (_partMinimize is Button minimize)
        {
            minimize.Click -= OnMinimizeClick;
        }

        if (_partMaximize is Button maximize)
        {
            maximize.Click -= OnMaximizeClick;
        }

        if (_partClose is Button close)
        {
            close.Click -= OnCloseClick;
        }

        _partMinimize = null;
        _partMaximize = null;
        _partClose = null;
        _partTitleText = null;
        _partTitleContent = null;
        _partIcon = null;
        _dragArea = null;
    }

    private enum CaptionKind
    {
        Minimize,
        Maximize,
        Close,
    }

    // 自绘标题栏的单个窗口按钮：固定宽，整个高度都是命中区。
    private sealed class CaptionButton : Button
    {
        private static readonly Color CloseHover = Color.FromArgb(0xE8, 0x11, 0x23);
        private static readonly Color ClosePressed = Color.FromArgb(0xC5, 0x0F, 0x1F);

        private readonly VisualTitleBar _owner;
        private readonly CaptionKind _kind;

        internal CaptionButton(VisualTitleBar owner, CaptionKind kind)
        {
            _owner = owner;
            _kind = kind;
            Focusable = false;
            IsTabStop = false;
            Cursor = Cursors.Default;
            Padding = default;
            MinHeight = 0;
            CornerRadius = 0;
        }

        protected override Size MeasureOverride(Size availableSize)
            => new((int)Math.Ceiling(CaptionButtonWidth), (int)Math.Ceiling(_owner.ResolveHeight()));

        protected override void OnClick()
        {
            base.OnClick();
            switch (_kind)
            {
                case CaptionKind.Minimize:
                    _owner.OnMinimizeClick(this, EventArgs.Empty);
                    break;
                case CaptionKind.Maximize:
                    _owner.OnMaximizeClick(this, EventArgs.Empty);
                    break;
                default:
                    _owner.OnCloseClick(this, EventArgs.Empty);
                    break;
            }
        }

        protected override void OnRender(Graphics graphics)
        {
            var theme = Theme.Current;
            var width = Math.Max(1, (int)Math.Ceiling(Bounds.Width));
            var height = Math.Max(1, (int)Math.Ceiling(Bounds.Height));

            if (IsPointerOver || IsPressed)
            {
                var background = _kind == CaptionKind.Close
                    ? (IsPressed ? ClosePressed : CloseHover)
                    : (IsPressed ? theme.ControlPressed : theme.ControlHover);
                using var brush = new SolidBrush(background);
                graphics.FillRectangle(brush, 0, 0, width, height);
            }

            var closeActive = _kind == CaptionKind.Close && (IsPointerOver || IsPressed);
            var color = closeActive ? Color.White : theme.Foreground;
            DrawGlyph(graphics, color, width, height);
        }

        private void DrawGlyph(Graphics graphics, Color color, int width, int height)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var cx = width / 2f;
            var cy = height / 2f;
            using var pen = new Pen(color, 1f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };

            switch (_kind)
            {
                case CaptionKind.Minimize:
                    graphics.DrawLine(pen, cx - 5f, cy, cx + 5f, cy);
                    break;
                case CaptionKind.Maximize when _owner.IsMaximized:
                    // 还原图标：两个错位方框。
                    graphics.DrawRectangle(pen, cx - 5.5f, cy - 2f, 7f, 7f);
                    graphics.DrawRectangle(pen, cx - 2f, cy - 5.5f, 7f, 7f);
                    break;
                case CaptionKind.Maximize:
                    graphics.DrawRectangle(pen, cx - 4.5f, cy - 4.5f, 9f, 9f);
                    break;
                default:
                    graphics.DrawLine(pen, cx - 5f, cy - 5f, cx + 5f, cy + 5f);
                    graphics.DrawLine(pen, cx + 5f, cy - 5f, cx - 5f, cy + 5f);
                    break;
            }
        }
    }
}
