using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using XamlDrawing = Easel.Controls.XamlDrawing;
using XamlBrush = Easel.Brush;
using XamlThickness = Easel.Controls.Thickness;

namespace Easel.Visuals.Controls;

/// <summary>A self-drawn push button with hover, press and keyboard states.</summary>
public class Button : ButtonBase
{
    /// <summary>The text drawn inside the button.</summary>
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(Button),
            new PropertyMetadata(string.Empty) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The font used to draw the text.</summary>
    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font), typeof(Font), typeof(Button),
            new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The text colour as a brush value; an empty colour uses the active theme.</summary>
    /// <remarks>
    /// Stored values are canonicalised to a colour-comparing brush, so a <see cref="VisualTrigger"/> written as
    /// <c>Value="#FF203040"</c> matches a brush produced by a style, binding or local assignment.
    /// </remarks>
    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground), typeof(XamlBrush), typeof(Button),
            new PropertyMetadata(null, null, static (_, value) => ColorBrushCoercion.Coerce(value))
            {
                AffectsRender = true,
            });

    private bool _keyDown;

    /// <summary>Creates a focusable button with theme metrics.</summary>
    public Button()
    {
        Focusable = true;
        IsHitTestVisible = true;
        Cursor = Cursors.Hand;
        Padding = new XamlThickness(14f, 6f);
        MinHeight = Theme.Current.ControlHeight;
        CornerRadius = Theme.Current.CornerRadius;
    }

    /// <summary>The text drawn inside the button.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty)!;
        set => SetValue(TextProperty, value ?? string.Empty);
    }

    /// <summary>The font used to draw the text, composed with the inherited typography properties.</summary>
    public Font Font
    {
        get => ResolveFont((Font?)GetValue(FontProperty) ?? DefaultFont);
        set
        {
            if (value is not null)
            {
                SetValue(FontProperty, value);
            }
        }
    }

    /// <summary>The text colour; an empty colour uses the active theme.</summary>
    /// <remarks>
    /// The dependency property stores a brush, so a style, trigger or binding may supply a
    /// <see cref="Easel.Brush"/>, a hex colour string or a <see cref="Color"/>. The CLR wrapper
    /// reads back the representative colour. A TwoWay binding writes the brush back to its source, so a source
    /// property must be brush-typed (or supply a converter); a <see cref="Color"/> source reports a binding failure
    /// on the write-back path instead of converting implicitly.
    /// </remarks>
    public Color Foreground
    {
        get => GetValue(ForegroundProperty) is XamlBrush brush ? brush.ApproximateColor : Color.Empty;
        set
        {
            if (value.IsEmpty)
            {
                ClearValue(ForegroundProperty);
            }
            else
            {
                SetValue(ForegroundProperty, new SolidColorBrush(value));
            }
        }
    }

    /// <summary>Raised when the button is clicked by the pointer or the keyboard.</summary>
    public event EventHandler? Click;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsTemplated)
        {
            return MeasureTemplate(availableSize);
        }

        var sample = string.IsNullOrEmpty(Text) ? "M" : Text;
        var measured = VisualText.Measure(sample, Font, 0f, wrap: false, RenderDpi);
        var width = (int)Math.Ceiling(measured.Width + Padding.Horizontal + 2);
        var height = Math.Max(
            Theme.Current.ControlHeight,
            (int)Math.Ceiling(measured.Height + Padding.Vertical + 2));
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void RenderContent(Graphics graphics)
    {
        if (!IsTemplated)
        {
            OnRender(graphics);
        }
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        if (IsTemplated)
        {
            return;
        }

        var theme = Theme.Current;
        var background = Background?.ApproximateColor
            ?? (!IsEnabled
                ? theme.DisabledBackground
                : IsPressed
                    ? theme.ControlPressed
                    : IsPointerOver
                        ? theme.ControlHover
                        : theme.ControlBackground);
        var border = BorderBrush?.ApproximateColor ?? theme.ControlBorder;
        var bounds = new Rectangle(0, 0, Math.Max(1, (int)Math.Ceiling(Bounds.Width)), Math.Max(1, (int)Math.Ceiling(Bounds.Height)));
        var thickness = BorderThickness.IsEmpty ? theme.ControlBorderThickness : BorderThickness;
        XamlDrawing.PaintChrome(
            graphics,
            bounds,
            background,
            border,
            thickness,
            CornerRadius > 0 ? CornerRadius : theme.CornerRadius);

        if (IsFocusVisible)
        {
            using var focusPath = XamlDrawing.RoundedRectangle(
                new RectangleF(1.5f, 1.5f, Math.Max(0f, bounds.Width - 3f), Math.Max(0f, bounds.Height - 3f)),
                Math.Max(0f, (CornerRadius > 0 ? CornerRadius : theme.CornerRadius) - 1f));
            using var focusPen = new Pen(theme.FocusRing, theme.FocusThickness);
            graphics.DrawPath(focusPen, focusPath);
        }

        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        var content = new Rectangle(
            (int)Math.Ceiling(Padding.Left) + 1,
            (int)Math.Ceiling(Padding.Top) + 1,
            Math.Max(0, bounds.Width - (int)Math.Ceiling(Padding.Horizontal) - 2),
            Math.Max(0, bounds.Height - (int)Math.Ceiling(Padding.Vertical) - 2));
        var foreground = Foreground.IsEmpty
            ? (IsEnabled ? theme.Foreground : theme.DisabledForeground)
            : Foreground;
        if (GetValue(ForegroundProperty) is XamlBrush brush)
        {
            VisualText.Draw(this, graphics, Text, Font, brush, content,
                wrap: false, StringAlignment.Center, StringAlignment.Center);
        }
        else
        {
            VisualText.Draw(this, graphics, Text, Font, foreground, content,
                wrap: false, StringAlignment.Center, StringAlignment.Center);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(VisualPointerEventArgs args)
    {
        SetIsPointerOver(true);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(VisualPointerEventArgs args)
    {
        SetIsPointerOver(false);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        if (!IsEnabled || args.Button != MouseButtons.Left)
        {
            return;
        }

        SetIsPressed(true);
        InvalidateVisual();
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(VisualPointerEventArgs args)
    {
        if (!IsPressed)
        {
            return;
        }

        SetIsPressed(false);
        InvalidateVisual();
        args.Handled = true;
        if (IsEnabled && AbsoluteBounds.Contains(args.Position))
        {
            OnClick();
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(VisualKeyEventArgs args)
    {
        if (!IsEnabled || _keyDown)
        {
            return;
        }

        if (args.KeyCode is Keys.Space or Keys.Enter)
        {
            _keyDown = true;
            if (args.KeyCode == Keys.Enter)
            {
                OnClick();
            }

            args.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyUp(VisualKeyEventArgs args)
    {
        if (args.KeyCode is Keys.Space or Keys.Enter)
        {
            var activate = args.KeyCode == Keys.Space && _keyDown && IsEnabled;
            _keyDown = false;
            args.Handled = true;
            if (activate)
            {
                OnClick();
            }
        }
    }

    /// <inheritdoc />
    protected override void OnGotFocus() => InvalidateVisual();

    /// <inheritdoc />
    protected override void OnLostFocus() => InvalidateVisual();

    /// <summary>Raises <see cref="Click"/> after executing <see cref="ButtonBase.Command"/>.</summary>
    protected virtual void OnClick()
    {
        if (!IsEnabled)
        {
            return;
        }

        ExecuteCommand();
        Click?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// A self-drawn text field with caret, selection, clipboard, undo and multi-line editing.
/// </summary>
/// <remarks>
/// <para>
/// The field paints its own text, selection and caret, and receives characters through
/// <see cref="VisualHost.ProcessTextInput"/>. Navigation, editing and <c>Ctrl+Z</c>/<c>Ctrl+Y</c> are handled in
/// <see cref="OnKeyDown"/>; the edit history is the self-drawn path's own snapshot stack.
/// </para>
/// <para>
/// When <see cref="UseNativeEditor"/> is <see langword="true"/> the focused field also hosts a private
/// <see cref="NativeControlHost"/> over its content rectangle. The hosted
/// <see cref="System.Windows.Forms.TextBox"/> is the native island that supplies IME composition and an accessibility
/// peer, and the self-drawn text and caret are hidden while it is active. The island is a real HWND, so it always
/// paints above the visual tree and cannot be transformed, blended or clipped: multi-line overflow is clipped by the
/// visual path, and only the content rectangle is covered — the border and padding stay visual. The visual path is the
/// default; turn the island on only as the fallback for IME composition or the native accessibility peer.
/// </para>
/// <para>
/// A control template replaces the self-drawn border and background. The template must contain a
/// <see cref="TextBoxContentHost"/> named <c>PART_ContentHost</c> (or <c>PART_EditorHost</c>): that part paints the
/// text surface, and the native island is positioned over its rectangle while the field has focus.
/// <see cref="TemplateDiagnostic"/> reports a template that has no such part. The island is still a real HWND owned
/// by the surface, so it cannot become a visual child of the part or be transformed with it.
/// </para>
/// </remarks>
public class TextBox : TemplatedControl
{
    /// <summary>The current text.</summary>
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(TextBox),
            new PropertyMetadata(string.Empty) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The text shown while the field is empty.</summary>
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(
            nameof(Placeholder), typeof(string), typeof(TextBox),
            new PropertyMetadata(string.Empty) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The font used to draw the text.</summary>
    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font), typeof(Font), typeof(TextBox),
            new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The text colour as a brush value; an empty colour uses the active theme.</summary>
    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground), typeof(XamlBrush), typeof(TextBox),
            new PropertyMetadata(null, null, static (_, value) => ColorBrushCoercion.Coerce(value))
            {
                AffectsRender = true,
            });

    /// <summary>Whether the field can be edited.</summary>
    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly), typeof(bool), typeof(TextBox),
            new PropertyMetadata(false) { AffectsRender = true });

    /// <summary>Whether the focused field falls back to the native editor island for IME and accessibility.</summary>
    /// <remarks>
    /// <para>
    /// The visual path is the default: no child HWND, no airspace island, and every appearance property — including
    /// <see cref="SelectionBrush"/>, <see cref="SelectionForeground"/>, <see cref="CaretBrush"/>,
    /// <see cref="CaretThickness"/> and <see cref="CaretBlinkInterval"/> — paints as authored.
    /// </para>
    /// <para>
    /// Set this to <see langword="true"/> to use the native editor island as the fallback when IME composition or the
    /// native accessibility peer is required. Typing, selection, clipboard, undo, templates and bindings stay on the
    /// visual path either way.
    /// </para>
    /// </remarks>
    public static readonly DependencyProperty UseNativeEditorProperty =
        DependencyProperty.Register(
            nameof(UseNativeEditor), typeof(bool), typeof(TextBox),
            new PropertyMetadata(false));

    /// <summary>Whether the field currently owns focus, including while its native editor island holds the keyboard focus.</summary>
    /// <remarks>A control template trigger reads this property; the CLR <see cref="VisualElement.IsFocused"/> stays the surface-level flag.</remarks>
    public static readonly DependencyProperty IsFocusedProperty =
        DependencyProperty.Register(
            nameof(IsFocused), typeof(bool), typeof(TextBox),
            new PropertyMetadata(false) { AffectsRender = false });

    /// <summary>Whether the text is drawn as bullets.</summary>
    public static readonly DependencyProperty IsPasswordProperty =
        DependencyProperty.Register(
            nameof(IsPassword), typeof(bool), typeof(TextBox),
            new PropertyMetadata(false) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>Whether Enter inserts a line break and the field measures and draws multiple lines.</summary>
    public static readonly DependencyProperty AcceptsReturnProperty =
        DependencyProperty.Register(
            nameof(AcceptsReturn), typeof(bool), typeof(TextBox),
            new PropertyMetadata(false) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>Whether overlong lines wrap at the content width instead of staying on one line.</summary>
    public static readonly DependencyProperty WrapProperty =
        DependencyProperty.Register(
            nameof(Wrap), typeof(bool), typeof(TextBox),
            new PropertyMetadata(false) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The selection highlight brush; an unset value uses the active theme's selection colour.</summary>
    /// <remarks>Only the visual editing path paints it; the native editor island draws the operating system selection.</remarks>
    public static readonly DependencyProperty SelectionBrushProperty =
        DependencyProperty.Register(
            nameof(SelectionBrush), typeof(XamlBrush), typeof(TextBox),
            new PropertyMetadata(null, null, static (_, value) => ColorBrushCoercion.Coerce(value))
            {
                AffectsRender = true,
            });

    /// <summary>The colour of text inside the selection; an unset value keeps the field's foreground.</summary>
    public static readonly DependencyProperty SelectionForegroundProperty =
        DependencyProperty.Register(
            nameof(SelectionForeground), typeof(XamlBrush), typeof(TextBox),
            new PropertyMetadata(null, null, static (_, value) => ColorBrushCoercion.Coerce(value))
            {
                AffectsRender = true,
            });

    /// <summary>The caret brush; an unset value uses the field's foreground.</summary>
    /// <remarks>The caret is a single stroke, so a gradient brush paints its representative colour.</remarks>
    public static readonly DependencyProperty CaretBrushProperty =
        DependencyProperty.Register(
            nameof(CaretBrush), typeof(XamlBrush), typeof(TextBox),
            new PropertyMetadata(null, null, static (_, value) => ColorBrushCoercion.Coerce(value))
            {
                AffectsRender = true,
            });

    /// <summary>The caret stroke width in device-independent pixels.</summary>
    public static readonly DependencyProperty CaretThicknessProperty =
        DependencyProperty.Register(
            nameof(CaretThickness), typeof(double), typeof(TextBox),
            new PropertyMetadata(1d, null, static (_, value) => Math.Max(0.5d, (double)value))
            {
                AffectsRender = true,
            });

    /// <summary>The caret blink period in milliseconds; <c>0</c> keeps the caret steady.</summary>
    public static readonly DependencyProperty CaretBlinkIntervalProperty =
        DependencyProperty.Register(
            nameof(CaretBlinkInterval), typeof(int), typeof(TextBox),
            new PropertyMetadata(530));

    /// <summary>Whether typing replaces the character after the caret instead of inserting.</summary>
    /// <remarks>The Insert key toggles it; the visual path draws a block caret in this mode. The native editor island
    /// keeps the operating system's insert behaviour.</remarks>
    public static readonly DependencyProperty IsOverwriteModeProperty =
        DependencyProperty.Register(
            nameof(IsOverwriteMode), typeof(bool), typeof(TextBox),
            new PropertyMetadata(false) { AffectsRender = true });

    /// <summary>The opacity of the overwrite caret block in the range 0..1.</summary>
    /// <remarks>The block stays translucent so the character under it remains readable.</remarks>
    public static readonly DependencyProperty CaretBlockOpacityProperty =
        DependencyProperty.Register(
            nameof(CaretBlockOpacity), typeof(double), typeof(TextBox),
            new PropertyMetadata(0.35d, null, static (_, value) =>
            {
                var opacity = (double)value;
                return opacity < 0d ? 0d : opacity > 1d ? 1d : opacity;
            })
            {
                AffectsRender = true,
            });

    private readonly System.Windows.Forms.Timer _caretTimer;
    private readonly System.Windows.Forms.TextBox _editor;
    private readonly NativeControlHost _nativeHost;
    private readonly Stack<TextSnapshot> _undo = new();
    private readonly Stack<TextSnapshot> _redo = new();
    private readonly List<TextLine> _lines = [];
    private int _selectionAnchor;
    private int _caretIndex;
    private bool _dragging;
    private bool _caretVisible;
    private bool _nativeActive;
    private bool _syncingNative;
    private bool _suppressHistoryReset;
    private bool _focusHandoff;
    private bool _focusOnNextLayout;
    private int _focusAttempts;
    private readonly System.Windows.Forms.Timer _editorFocusTimer;
    private float _horizontalOffset;
    private float _lineHeight = 16f;
    private float _laidOutWidth = -1f;
    private bool _layoutDirty = true;
    private TextBoxContentHost? _contentHost;
    private string? _templateDiagnostic;

    /// <summary>Creates a focusable text field with theme metrics and its native editor island.</summary>
    public TextBox()
    {
        _caretTimer = new System.Windows.Forms.Timer { Interval = 530 };
        _caretTimer.Tick += (_, _) =>
        {
            _caretVisible = !_caretVisible;
            InvalidateVisual();
        };

        _editorFocusTimer = new System.Windows.Forms.Timer { Interval = 40 };
        _editorFocusTimer.Tick += (_, _) =>
        {
            _editorFocusTimer.Stop();
            if (!_nativeActive || _editor.IsDisposed || _editor.Focused || !IsFocused)
            {
                _focusAttempts = 0;
                return;
            }

            FocusNativeEditorIfNeeded();
            if (!_editor.Focused && ++_focusAttempts < 25)
            {
                Surface?.InvalidateSurface();
                _editorFocusTimer.Start();
            }
            else
            {
                _focusAttempts = 0;
            }
        };

        _editor = new System.Windows.Forms.TextBox
        {
            BorderStyle = BorderStyle.None,
            AutoSize = false,
            TabStop = false,
            HideSelection = false,
            Visible = false,
        };
        _editor.TextChanged += OnNativeTextChanged;
        _editor.KeyUp += (_, _) => SyncSelectionFromNative();
        _editor.MouseUp += (_, _) => SyncSelectionFromNative();
        _editor.LostFocus += OnNativeEditorLostFocus;

        _nativeHost = Add(new NativeControlHost());
        _nativeHost.Child = _editor;
        _nativeHost.Visibility = Easel.Controls.Visibility.Collapsed;

        Focusable = true;
        IsHitTestVisible = true;
        Cursor = Cursors.IBeam;
        Padding = new XamlThickness(8f, 6f);
        MinHeight = Theme.Current.ControlHeight;
        CornerRadius = Theme.Current.CornerRadius;
    }

    /// <summary>The current text.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty)!;
        set => SetValue(TextProperty, value ?? string.Empty);
    }

    /// <summary>The text shown while the field is empty.</summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty)!;
        set => SetValue(PlaceholderProperty, value ?? string.Empty);
    }

    /// <summary>The font used to draw the text, composed with the inherited typography properties.</summary>
    public Font Font
    {
        get => ResolveFont((Font?)GetValue(FontProperty) ?? DefaultFont);
        set
        {
            if (value is not null)
            {
                SetValue(FontProperty, value);
            }
        }
    }

    /// <summary>The text colour; an empty colour uses the active theme.</summary>
    /// <remarks>
    /// The dependency property stores a brush, so a style, trigger or binding may supply a
    /// <see cref="Easel.Brush"/>, a hex colour string or a <see cref="Color"/>. The CLR wrapper
    /// reads back the representative colour. A TwoWay binding writes the brush back to its source, so a source
    /// property must be brush-typed (or supply a converter); a <see cref="Color"/> source reports a binding failure
    /// on the write-back path instead of converting implicitly.
    /// </remarks>
    public Color Foreground
    {
        get => GetValue(ForegroundProperty) is XamlBrush brush ? brush.ApproximateColor : Color.Empty;
        set
        {
            if (value.IsEmpty)
            {
                ClearValue(ForegroundProperty);
            }
            else
            {
                SetValue(ForegroundProperty, new SolidColorBrush(value));
            }
        }
    }

    /// <summary>Whether the text can be edited.</summary>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty)!;
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Whether the focused field hosts the native editor island for IME and accessibility.</summary>
    /// <remarks>
    /// The visual text, caret, selection, clipboard and undo path is used either way. Disable it to keep the control
    /// entirely on the visual layer (no child HWND, no airspace clipping limit), at the cost of IME composition and the
    /// native accessibility peer.
    /// </remarks>
    public bool UseNativeEditor
    {
        get => (bool)GetValue(UseNativeEditorProperty)!;
        set => SetValue(UseNativeEditorProperty, value);
    }

    /// <summary>Whether the text is drawn as bullets.</summary>
    public bool IsPassword
    {
        get => (bool)GetValue(IsPasswordProperty)!;
        set => SetValue(IsPasswordProperty, value);
    }

    /// <summary>Whether Enter inserts a line break and the field measures and draws multiple lines.</summary>
    public bool AcceptsReturn
    {
        get => (bool)GetValue(AcceptsReturnProperty)!;
        set => SetValue(AcceptsReturnProperty, value);
    }

    /// <summary>Whether overlong lines wrap at the content width instead of staying on one line.</summary>
    public bool Wrap
    {
        get => (bool)GetValue(WrapProperty)!;
        set => SetValue(WrapProperty, value);
    }

    /// <summary>The selection highlight brush; <see langword="null"/> uses the active theme.</summary>
    public XamlBrush? SelectionBrush
    {
        get => GetValue(SelectionBrushProperty) as XamlBrush;
        set => SetValue(SelectionBrushProperty, value);
    }

    /// <summary>The colour of text inside the selection; <see langword="null"/> keeps the field's foreground.</summary>
    public XamlBrush? SelectionForeground
    {
        get => GetValue(SelectionForegroundProperty) as XamlBrush;
        set => SetValue(SelectionForegroundProperty, value);
    }

    /// <summary>The caret brush; <see langword="null"/> uses the field's foreground.</summary>
    public XamlBrush? CaretBrush
    {
        get => GetValue(CaretBrushProperty) as XamlBrush;
        set => SetValue(CaretBrushProperty, value);
    }

    /// <summary>The caret stroke width in device-independent pixels.</summary>
    public double CaretThickness
    {
        get => (double)GetValue(CaretThicknessProperty)!;
        set => SetValue(CaretThicknessProperty, value);
    }

    /// <summary>The caret blink period in milliseconds; <c>0</c> keeps the caret steady.</summary>
    public int CaretBlinkInterval
    {
        get => (int)GetValue(CaretBlinkIntervalProperty)!;
        set => SetValue(CaretBlinkIntervalProperty, value);
    }

    /// <summary>Whether typing replaces the character after the caret instead of inserting.</summary>
    public bool IsOverwriteMode
    {
        get => (bool)GetValue(IsOverwriteModeProperty)!;
        set => SetValue(IsOverwriteModeProperty, value);
    }

    /// <summary>The opacity of the overwrite caret block in the range 0..1.</summary>
    public double CaretBlockOpacity
    {
        get => (double)GetValue(CaretBlockOpacityProperty)!;
        set => SetValue(CaretBlockOpacityProperty, value);
    }

    /// <summary>The first selected character.</summary>
    public int SelectionStart => XamlMath.Clamp(Math.Min(_selectionAnchor, _caretIndex), 0, Text.Length);

    /// <summary>The number of selected characters.</summary>
    public int SelectionLength => XamlMath.Clamp(Math.Abs(_selectionAnchor - _caretIndex), 0, Text.Length - SelectionStart);

    /// <summary>The caret index, which can be either end of the selection.</summary>
    public int CaretIndex => XamlMath.Clamp(_caretIndex, 0, Text.Length);

    /// <summary>The horizontal scroll offset applied to long single-line text.</summary>
    public float HorizontalOffset
    {
        get => _horizontalOffset;
        set => _horizontalOffset = XamlMath.Clamp(value, 0f, MaxHorizontalOffset);
    }

    /// <summary>The largest valid <see cref="HorizontalOffset"/>.</summary>
    public float MaxHorizontalOffset
    {
        get
        {
            if (AcceptsReturn)
            {
                return 0f;
            }

            var content = ActiveContentRectangle();
            var textWidth = (float)Math.Ceiling(VisualText.MeasureWidthTight(DisplayText, Font, RenderDpi));
            return Math.Max(0f, textWidth - content.Width);
        }
    }

    /// <summary>The native editor used for IME and accessibility while the field has focus.</summary>
    /// <remarks>The editor is hosted through <see cref="NativeControlHost"/> and only parented while focused.</remarks>
    public System.Windows.Forms.TextBox NativeEditor => _editor;

    /// <summary>Whether the native editor island is currently shown over the content rectangle.</summary>
    public bool IsUsingNativeEditor => _nativeActive;

    /// <summary>Why the applied control template cannot host the editor, or <see langword="null"/> when the template is usable.</summary>
    public string? TemplateDiagnostic => _templateDiagnostic;

    private bool MultiLine => AcceptsReturn || Wrap;

    private string DisplayText => IsPassword ? new string('●', Text.Length) : Text;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
        => IsTemplated ? MeasureTemplate(availableSize) : MeasureEditor(availableSize);

    // 按自身文字量期望尺寸；模板下改由模板决定尺寸。
    internal Size MeasureEditor(Size availableSize)
    {
        var contentWidth = Math.Max(0, availableSize.Width - (int)Math.Ceiling(Padding.Horizontal));
        var sample = string.IsNullOrEmpty(Text)
            ? (string.IsNullOrEmpty(Placeholder) ? "M" : Placeholder)
            : DisplayText;
        var wrap = Wrap && contentWidth > 0;
        var measured = VisualText.Measure(sample, Font, wrap ? contentWidth : 0f, wrap, RenderDpi);
        var width = Math.Max(120, (int)Math.Ceiling(measured.Width + Padding.Horizontal + 2));
        var height = Math.Max(
            Theme.Current.ControlHeight,
            (int)Math.Ceiling(measured.Height + Padding.Vertical + 2));
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(finalSize);
        if (_nativeActive)
        {
            ArrangeNativeEditor();
        }
    }

    /// <inheritdoc />
    protected override void RenderContent(Graphics graphics)
    {
        // 模板根负责外观，文字交给 PART_ContentHost；只有无模板时才走整块自绘。
        if (!IsTemplated)
        {
            OnRender(graphics);
        }
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        if (IsTemplated)
        {
            return;
        }

        var theme = Theme.Current;
        var focused = IsFocused || _nativeActive;
        var bounds = new Rectangle(0, 0, Math.Max(1, (int)Math.Ceiling(Bounds.Width)), Math.Max(1, (int)Math.Ceiling(Bounds.Height)));
        var background = Background?.ApproximateColor ?? theme.ControlBackground;
        var border = focused ? theme.FocusRing : BorderBrush?.ApproximateColor ?? theme.ControlBorder;
        var thickness = BorderThickness.IsEmpty ? theme.ControlBorderThickness : BorderThickness;
        XamlDrawing.PaintChrome(
            graphics,
            bounds,
            background,
            border,
            thickness,
            CornerRadius > 0 ? CornerRadius : theme.CornerRadius);

        RenderEditorSurface(graphics, ContentRectangle());
    }

    // 在给定矩形里画文字、选区、占位符与插入符；自绘路径和模板 ContentHost 共用。
    internal void RenderEditorSurface(Graphics graphics, Rectangle content)
    {
        if (content.Width <= 0 || content.Height <= 0)
        {
            return;
        }

        // 原生岛是独立 HWND，压在视觉树之上；它显示时不要再画一份文字，否则会透出两层。
        if (_nativeActive)
        {
            return;
        }

        var theme = Theme.Current;
        EnsureLines(content.Width);
        var foreground = Foreground.IsEmpty
            ? (IsEnabled ? theme.Foreground : theme.DisabledForeground)
            : Foreground;
        var textBrush = GetValue(ForegroundProperty) as XamlBrush ?? new SolidColorBrush(foreground);
        var offset = AcceptsReturn ? 0f : _horizontalOffset;
        // 单行字段在内容矩形里垂直居中；多行按顶部对齐，和文本编辑器一致。
        var textTop = content.Top + (MultiLine ? 0f : Math.Max(0f, (content.Height - _lineHeight) / 2f));

        var state = graphics.Save();
        try
        {
            graphics.SetClip(content, CombineMode.Intersect);

            var selectionStart = IsFocused ? SelectionStart : 0;
            var selectionLength = IsFocused ? SelectionLength : 0;
            if (selectionLength > 0)
            {
                DrawSelection(graphics, content, offset, textTop, SelectionBrush ?? new SolidColorBrush(theme.Selection));
            }

            if (string.IsNullOrEmpty(Text))
            {
                // 聚焦后插入符与占位文字会叠在同一行；占位只在未聚焦时显示。
                if (!string.IsNullOrEmpty(Placeholder) && !IsFocused)
                {
                    VisualText.Draw(this,
                        graphics,
                        Placeholder,
                        Font,
                        theme.MutedForeground,
                        new RectangleF(content.Left, textTop, content.Width, _lineHeight),
                        wrap: false,
                        StringAlignment.Near,
                        StringAlignment.Near);
                }
            }
            else
            {
                DrawLines(graphics, content, offset, textTop, textBrush, selectionStart, selectionLength, SelectionForeground);
            }

            if (IsFocused && _caretVisible && !IsReadOnly)
            {
                DrawCaret(graphics, content, offset, textTop, textBrush);
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, CaretBlinkIntervalProperty))
        {
            ApplyCaretBlinkInterval();
        }
        else if (ReferenceEquals(e.Property, UseNativeEditorProperty))
        {
            if (UseNativeEditor)
            {
                if (IsFocused)
                {
                    ShowNativeEditor();
                }
            }
            else
            {
                HideNativeEditor();
                if (IsFocused)
                {
                    _caretVisible = true;
                    _caretTimer.Start();
                }
            }

            UpdateFocusState();
            InvalidateVisual();
            return;
        }

        if (ReferenceEquals(e.Property, TextProperty))
        {
            ClampSelection();
            ResetCaretBlink();
            _layoutDirty = true;
            if (!_suppressHistoryReset && !_syncingNative)
            {
                _undo.Clear();
                _redo.Clear();
            }

            ClampHorizontalOffset();
            if (_nativeActive && !_syncingNative)
            {
                SyncEditorFromVisual();
            }

            return;
        }

        if (ReferenceEquals(e.Property, FontProperty)
            || ReferenceEquals(e.Property, FontSizeProperty)
            || ReferenceEquals(e.Property, FontFamilyProperty)
            || ReferenceEquals(e.Property, FontWeightProperty)
            || ReferenceEquals(e.Property, WrapProperty)
            || ReferenceEquals(e.Property, AcceptsReturnProperty)
            || ReferenceEquals(e.Property, IsPasswordProperty)
            || ReferenceEquals(e.Property, PlaceholderProperty))
        {
            _layoutDirty = true;
            ClampHorizontalOffset();
            ApplyNativeProperties();
            return;
        }

        if (ReferenceEquals(e.Property, ForegroundProperty)
            || ReferenceEquals(e.Property, IsReadOnlyProperty)
            || ReferenceEquals(e.Property, IsEnabledProperty))
        {
            ApplyNativeProperties();
        }
    }

    /// <inheritdoc />
    protected override void OnGotFocus()
    {
        _caretVisible = true;
        if (CaretBlinkInterval > 0)
        {
            _caretTimer.Start();
        }

        ShowNativeEditor();
        UpdateFocusState();
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnLostFocus()
    {
        _caretTimer.Stop();
        _caretVisible = false;
        _dragging = false;

        // 原生岛自己拿到焦点时 surface 会先收到 WM_KILLFOCUS；那不是「离开字段」，不能把岛撤掉。
        if (!_focusHandoff && !_editor.Focused && !_editor.ContainsFocus)
        {
            HideNativeEditor();
        }

        UpdateFocusState();
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnTextInput(VisualTextInputEventArgs args)
    {
        if (!IsEnabled || IsReadOnly || string.IsNullOrEmpty(args.Text))
        {
            return;
        }

        ReplaceSelection(args.Text);
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        if (args.Button != MouseButtons.Left)
        {
            return;
        }

        var index = HitTestIndex(ContentPoint(args), ActiveContentRectangle());
        if (args.ClickCount >= 2)
        {
            var range = WordRange(Text, index);
            _selectionAnchor = range.Start;
            _caretIndex = range.End;
            _dragging = false;
        }
        else
        {
            _selectionAnchor = index;
            _caretIndex = index;
            _dragging = true;
        }

        _caretVisible = true;
        ResetCaretBlink();
        EnsureCaretVisible();
        InvalidateVisual();
        if (_nativeActive)
        {
            // 第一次点击通常同时打开原生编辑器：把刚算出的光标同步过去，否则编辑器从旧位置开始。
            SyncEditorFromVisual();
        }

        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(VisualPointerEventArgs args)
    {
        if (!_dragging)
        {
            return;
        }

        _caretIndex = HitTestIndex(ContentPoint(args), ActiveContentRectangle());
        ResetCaretBlink();
        EnsureCaretVisible();
        InvalidateVisual();
        if (_nativeActive)
        {
            SyncEditorFromVisual();
        }

        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(VisualPointerEventArgs args)
    {
        if (_nativeActive)
        {
            // 按下时补的焦点会被这次 WinForms 焦点处理收走；抬起时消息已经处理完，补一次就能留住。
            FocusNativeEditorIfNeeded();
        }

        if (_dragging)
        {
            _dragging = false;
            args.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(VisualKeyEventArgs args)
    {
        if (!IsEnabled)
        {
            return;
        }

        var shift = (args.Modifiers & Keys.Shift) != 0;
        var control = (args.Modifiers & Keys.Control) != 0;
        switch (args.KeyCode)
        {
            case Keys.Insert when !shift && !control:
                IsOverwriteMode = !IsOverwriteMode;
                ResetCaretBlink();
                InvalidateVisual();
                args.Handled = true;
                break;
            case Keys.Left when control:
                MoveCaret(FindPreviousWord(Text, CaretIndex), shift);
                args.Handled = true;
                break;
            case Keys.Right when control:
                MoveCaret(FindNextWord(Text, CaretIndex), shift);
                args.Handled = true;
                break;
            case Keys.Left:
                MoveCaret(Math.Max(0, CaretIndex - 1), shift);
                args.Handled = true;
                break;
            case Keys.Right:
                MoveCaret(Math.Min(Text.Length, CaretIndex + 1), shift);
                args.Handled = true;
                break;
            case Keys.Home:
                MoveCaret(control ? 0 : CurrentLineStart(), shift);
                args.Handled = true;
                break;
            case Keys.End:
                MoveCaret(control ? Text.Length : CurrentLineEnd(), shift);
                args.Handled = true;
                break;
            case Keys.Back:
                if (!IsReadOnly)
                {
                    Backspace();
                    args.Handled = true;
                }

                break;
            case Keys.Delete:
                if (!IsReadOnly)
                {
                    Delete();
                    args.Handled = true;
                }

                break;
            case Keys.Z when control:
                if (shift)
                {
                    Redo();
                }
                else
                {
                    Undo();
                }

                args.Handled = true;
                break;
            case Keys.Y when control:
                Redo();
                args.Handled = true;
                break;
            case Keys.A when control:
                _selectionAnchor = 0;
                _caretIndex = Text.Length;
                ResetCaretBlink();
                EnsureCaretVisible();
                InvalidateVisual();
                args.Handled = true;
                break;
            case Keys.C when control:
                Copy();
                args.Handled = true;
                break;
            case Keys.X when control:
                if (!IsReadOnly)
                {
                    Copy();
                    ReplaceSelection(string.Empty);
                }

                args.Handled = true;
                break;
            case Keys.V when control:
                if (!IsReadOnly && TryGetClipboardText(out var pasted))
                {
                    ReplaceSelection(pasted);
                }

                args.Handled = true;
                break;
            case Keys.Enter:
                if (AcceptsReturn && !IsReadOnly)
                {
                    ReplaceSelection("\n");
                }

                args.Handled = true;
                break;
        }
    }

    private static int FindPreviousWord(string text, int index)
    {
        var cursor = XamlMath.Clamp(index, 0, text.Length);
        while (cursor > 0 && char.IsWhiteSpace(text[cursor - 1]))
        {
            cursor--;
        }

        while (cursor > 0 && !char.IsWhiteSpace(text[cursor - 1]))
        {
            cursor--;
        }

        return cursor;
    }

    private static int FindNextWord(string text, int index)
    {
        var cursor = XamlMath.Clamp(index, 0, text.Length);
        while (cursor < text.Length && !char.IsWhiteSpace(text[cursor]))
        {
            cursor++;
        }

        while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
        {
            cursor++;
        }

        return cursor;
    }

    private static (int Start, int End) WordRange(string text, int index)
    {
        if (text.Length == 0)
        {
            return (0, 0);
        }

        var cursor = XamlMath.Clamp(index, 0, text.Length - 1);
        if (char.IsWhiteSpace(text[cursor]))
        {
            var start = cursor;
            while (start > 0 && char.IsWhiteSpace(text[start - 1]))
            {
                start--;
            }

            var end = cursor;
            while (end < text.Length && char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            return (start, end);
        }

        var wordStart = cursor;
        while (wordStart > 0 && !char.IsWhiteSpace(text[wordStart - 1]))
        {
            wordStart--;
        }

        var wordEnd = cursor;
        while (wordEnd < text.Length && !char.IsWhiteSpace(text[wordEnd]))
        {
            wordEnd++;
        }

        return (wordStart, wordEnd);
    }

    private Rectangle ContentRectangle()
    {
        var bounds = new Rectangle(
            0,
            0,
            Math.Max(1, (int)Math.Ceiling(Bounds.Width)),
            Math.Max(1, (int)Math.Ceiling(Bounds.Height)));
        return new Rectangle(
            (int)Math.Ceiling(Padding.Left) + 1,
            (int)Math.Ceiling(Padding.Top) + 1,
            Math.Max(0, bounds.Width - (int)Math.Ceiling(Padding.Horizontal) - 2),
            Math.Max(0, bounds.Height - (int)Math.Ceiling(Padding.Vertical) - 2));
    }

    // 模板下文字区域就是部件自身的局部矩形；模板负责自己的内边距。
    private Rectangle ActiveContentRectangle()
    {
        if (IsTemplated && _contentHost is { } host)
        {
            return new Rectangle(
                0,
                0,
                Math.Max(1, (int)Math.Ceiling(host.Bounds.Width)),
                Math.Max(1, (int)Math.Ceiling(host.Bounds.Height)));
        }

        return ContentRectangle();
    }

    private PointF ContentPoint(VisualPointerEventArgs args)
        => IsTemplated && _contentHost is { } host ? args.GetPosition(host) : args.GetPosition(this);

    private void EnsureLines(float contentWidth)
    {
        if (!_layoutDirty && Math.Abs(contentWidth - _laidOutWidth) < 0.5f)
        {
            return;
        }

        _layoutDirty = false;
        _laidOutWidth = contentWidth;
        _lineHeight = Math.Max(1f, VisualText.Measure("Mg", Font, 0f, wrap: false, RenderDpi).Height);
        _lines.Clear();

        var display = DisplayText;
        var wrapWidth = Wrap && contentWidth > 0f ? contentWidth : 0f;
        var cursor = 0;
        while (true)
        {
            var start = cursor;
            while (cursor < display.Length && display[cursor] != '\n' && display[cursor] != '\r')
            {
                cursor++;
            }

            AppendWrappedLines(display, start, cursor - start, wrapWidth);
            if (cursor >= display.Length)
            {
                break;
            }

            // \r\n 是一个换行，索引上占两个字符；视觉行只记录可见片段。
            cursor += display[cursor] == '\r' && cursor + 1 < display.Length && display[cursor + 1] == '\n' ? 2 : 1;
        }

        if (_lines.Count == 0)
        {
            _lines.Add(new TextLine(0, 0));
        }
    }

    private void AppendWrappedLines(string display, int start, int length, float wrapWidth)
    {
        if (length <= 0)
        {
            _lines.Add(new TextLine(start, 0));
            return;
        }

        if (wrapWidth <= 0f)
        {
            _lines.Add(new TextLine(start, length));
            return;
        }

        var lineStart = start;
        var hardEnd = start + length;
        while (lineStart < hardEnd)
        {
            var cursor = lineStart;
            var lastFit = lineStart;
            while (cursor < hardEnd)
            {
                var wordEnd = cursor;
                var wordSpace = char.IsWhiteSpace(display[wordEnd]);
                do
                {
                    wordEnd++;
                }
                while (wordEnd < hardEnd && char.IsWhiteSpace(display[wordEnd]) == wordSpace);

                var width = VisualText.MeasureWidthTight(display.Substring(lineStart, wordEnd - lineStart), Font, RenderDpi);
                if (width <= wrapWidth)
                {
                    lastFit = wordEnd;
                    cursor = wordEnd;
                    continue;
                }

                if (lastFit == lineStart)
                {
                    // 第一个词就比整行宽：按字符切到放得下的位置，至少留下一个字符。
                    var cut = lineStart + 1;
                    while (cut < wordEnd
                        && VisualText.MeasureWidthTight(display.Substring(lineStart, cut + 1 - lineStart), Font, RenderDpi) <= wrapWidth)
                    {
                        cut++;
                    }

                    lastFit = cut;
                }

                break;
            }

            if (lastFit <= lineStart)
            {
                lastFit = lineStart + 1;
            }

            _lines.Add(new TextLine(lineStart, lastFit - lineStart));
            lineStart = lastFit;
        }
    }

    private int FindLineIndex(int index)
    {
        for (var line = 0; line < _lines.Count; line++)
        {
            var current = _lines[line];
            if (index >= current.Start && index <= current.Start + current.Length)
            {
                return line;
            }
        }

        return Math.Max(0, _lines.Count - 1);
    }

    private float MeasurePrefix(TextLine line, int length)
    {
        var clamped = XamlMath.Clamp(length, 0, line.Length);
        if (clamped <= 0)
        {
            return 0f;
        }

        return VisualText.MeasureWidthTight(DisplayText.Substring(line.Start, clamped), Font, RenderDpi);
    }

    private void DrawLines(
        Graphics graphics,
        Rectangle content,
        float offset,
        float textTop,
        XamlBrush foreground,
        int selectionStart,
        int selectionLength,
        XamlBrush? selectionForeground)
    {
        var display = DisplayText;
        var selectionEnd = selectionStart + selectionLength;
        for (var index = 0; index < _lines.Count; index++)
        {
            var line = _lines[index];
            if (line.Length <= 0)
            {
                continue;
            }

            var lineEnd = line.Start + line.Length;
            if (selectionForeground is null || selectionLength <= 0 || selectionEnd <= line.Start || selectionStart >= lineEnd)
            {
                DrawLineSegment(graphics, content, offset, textTop, index, line, display, foreground, 0, line.Length);
                continue;
            }

            var from = XamlMath.Clamp(selectionStart - line.Start, 0, line.Length);
            var to = XamlMath.Clamp(selectionEnd - line.Start, 0, line.Length);
            DrawLineSegment(graphics, content, offset, textTop, index, line, display, foreground, 0, from);
            DrawLineSegment(graphics, content, offset, textTop, index, line, display, selectionForeground, from, to - from);
            DrawLineSegment(graphics, content, offset, textTop, index, line, display, foreground, to, line.Length - to);
        }
    }

    private void DrawLineSegment(
        Graphics graphics,
        Rectangle content,
        float offset,
        float textTop,
        int lineIndex,
        TextLine line,
        string display,
        XamlBrush brush,
        int start,
        int length)
    {
        if (length <= 0)
        {
            return;
        }

        var text = display.Substring(line.Start + start, length);
        var width = (float)Math.Ceiling(VisualText.MeasureWidthTight(text, Font, RenderDpi));
        var destination = new RectangleF(
            content.Left - offset + MeasurePrefix(line, start),
            textTop + (lineIndex * _lineHeight),
            Math.Max(1f, width + 2f),
            _lineHeight);
        VisualText.DrawTight(this, graphics, text, Font, brush, destination);
    }

    private void DrawSelection(Graphics graphics, Rectangle content, float offset, float textTop, XamlBrush selection)
    {
        var selectionStart = SelectionStart;
        var selectionEnd = selectionStart + SelectionLength;
        for (var index = 0; index < _lines.Count; index++)
        {
            var line = _lines[index];
            var from = Math.Max(selectionStart, line.Start);
            var to = Math.Min(selectionEnd, line.Start + line.Length);
            if (from >= to)
            {
                continue;
            }

            var left = content.Left - offset + MeasurePrefix(line, from - line.Start);
            var right = content.Left - offset + MeasurePrefix(line, to - line.Start);
            selection.Paint(
                graphics,
                new RectangleF(
                    left,
                    textTop + (index * _lineHeight),
                    Math.Max(1f, right - left),
                    _lineHeight));
        }
    }

    private void DrawCaret(Graphics graphics, Rectangle content, float offset, float textTop, XamlBrush foreground)
    {
        var brush = CaretBrush ?? foreground;
        var color = brush.ApproximateColor;
        if (color.A == 0)
        {
            return;
        }

        var thickness = (float)CaretThickness;
        var lineIndex = FindLineIndex(CaretIndex);
        var line = _lines[lineIndex];
        var caretX = content.Left - offset + MeasurePrefix(line, CaretIndex - line.Start);
        var top = textTop + (lineIndex * _lineHeight) + 2f;
        var height = Math.Max(1f, _lineHeight - 4f);
        if (IsOverwriteMode)
        {
            // 覆盖模式的方块占住下一个字符的推进宽度；到行尾没有下一个字符时给一个可见的默认宽度。
            var width = CaretIndex < line.Start + line.Length
                ? MeasurePrefix(line, CaretIndex - line.Start + 1) - MeasurePrefix(line, CaretIndex - line.Start)
                : Math.Max(6f, thickness * 4f);
            var alpha = (int)Math.Round(color.A * CaretBlockOpacity);
            if (alpha > 0)
            {
                using var block = new SolidBrush(Color.FromArgb(alpha, color));
                graphics.FillRectangle(block, caretX, top, Math.Max(1f, width), height);
            }

            return;
        }

        using var pen = new Pen(color, thickness);
        graphics.DrawLine(pen, caretX + (thickness / 2f), top, caretX + (thickness / 2f), top + height);
    }

    private int HitTestIndex(PointF local, Rectangle content)
    {
        EnsureLines(content.Width);
        if (_lines.Count == 0)
        {
            return 0;
        }

        var offset = AcceptsReturn ? 0f : _horizontalOffset;
        var lineIndex = (int)Math.Floor((local.Y - content.Top) / Math.Max(1f, _lineHeight));
        lineIndex = XamlMath.Clamp(lineIndex, 0, _lines.Count - 1);
        var line = _lines[lineIndex];
        var x = local.X - content.Left + offset;
        var best = line.Start;
        var bestDistance = Math.Abs(x);
        for (var index = 1; index <= line.Length; index++)
        {
            var distance = Math.Abs(x - MeasurePrefix(line, index));
            if (distance < bestDistance)
            {
                best = line.Start + index;
                bestDistance = distance;
            }
        }

        return best;
    }

    private int CurrentLineStart()
    {
        EnsureLines(ActiveContentRectangle().Width);
        return _lines[FindLineIndex(CaretIndex)].Start;
    }

    private int CurrentLineEnd()
    {
        EnsureLines(ActiveContentRectangle().Width);
        var line = _lines[FindLineIndex(CaretIndex)];
        return line.Start + line.Length;
    }

    private void MoveCaret(int index, bool extend)
    {
        _caretIndex = XamlMath.Clamp(index, 0, Text.Length);
        if (!extend)
        {
            _selectionAnchor = _caretIndex;
        }

        ResetCaretBlink();
        EnsureCaretVisible();
        InvalidateVisual();
    }

    private void EnsureCaretVisible()
    {
        if (AcceptsReturn)
        {
            _horizontalOffset = 0f;
            return;
        }

        var content = ActiveContentRectangle();
        if (content.Width <= 0)
        {
            return;
        }

        EnsureLines(content.Width);
        var line = _lines[FindLineIndex(CaretIndex)];
        var caretX = MeasurePrefix(line, CaretIndex - line.Start);
        if (caretX < _horizontalOffset)
        {
            _horizontalOffset = caretX;
        }
        else if (caretX > _horizontalOffset + content.Width)
        {
            _horizontalOffset = caretX - content.Width;
        }

        _horizontalOffset = XamlMath.Clamp(_horizontalOffset, 0f, MaxHorizontalOffset);
    }

    private void ClampHorizontalOffset()
    {
        _horizontalOffset = XamlMath.Clamp(_horizontalOffset, 0f, MaxHorizontalOffset);
    }

    private void PushUndo()
    {
        _undo.Push(new TextSnapshot(Text, _caretIndex, _selectionAnchor));
        _redo.Clear();
    }

    private void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        _redo.Push(new TextSnapshot(Text, _caretIndex, _selectionAnchor));
        RestoreSnapshot(_undo.Pop());
    }

    private void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }

        _undo.Push(new TextSnapshot(Text, _caretIndex, _selectionAnchor));
        RestoreSnapshot(_redo.Pop());
    }

    private void RestoreSnapshot(TextSnapshot snapshot)
    {
        SetTextInternal(snapshot.Text);
        _caretIndex = XamlMath.Clamp(snapshot.CaretIndex, 0, Text.Length);
        _selectionAnchor = XamlMath.Clamp(snapshot.Anchor, 0, Text.Length);
        ResetCaretBlink();
        EnsureCaretVisible();
        InvalidateMeasure();
        InvalidateVisual();
        if (_nativeActive)
        {
            SyncEditorFromVisual();
        }
    }

    private void SetTextInternal(string value)
    {
        _suppressHistoryReset = true;
        try
        {
            Text = value;
        }
        finally
        {
            _suppressHistoryReset = false;
        }
    }

    private void ReplaceSelection(string replacement)
    {
        // 原生编辑器同步、绑定回写和布局都可能让选择落在文本之外；先夹回边界再切片。
        ClampSelection();
        PushUndo();
        var start = SelectionStart;
        var length = SelectionLength;
        // 覆盖模式只覆盖普通字符；换行（Enter）仍然插入，避免吃掉下一行。
        if (length == 0
            && IsOverwriteMode
            && replacement.Length > 0
            && replacement.IndexOf('\n') < 0
            && replacement.IndexOf('\r') < 0)
        {
            length = Math.Min(replacement.Length, Text.Length - start);
        }

        var before = Text.Substring(0, start);
        var after = Text.Substring(start + length);
        SetTextInternal(before + replacement + after);
        _caretIndex = XamlMath.Clamp(before.Length + replacement.Length, 0, Text.Length);
        _selectionAnchor = _caretIndex;
        ResetCaretBlink();
        EnsureCaretVisible();
        InvalidateMeasure();
        InvalidateVisual();
        if (_nativeActive)
        {
            SyncEditorFromVisual();
        }
    }

    private void Backspace()
    {
        ClampSelection();
        if (SelectionLength > 0)
        {
            ReplaceSelection(string.Empty);
            return;
        }

        if (_caretIndex == 0)
        {
            return;
        }

        PushUndo();
        SetTextInternal(Text.Remove(_caretIndex - 1, 1));
        _caretIndex--;
        _selectionAnchor = _caretIndex;
        ResetCaretBlink();
        EnsureCaretVisible();
        InvalidateMeasure();
        InvalidateVisual();
        if (_nativeActive)
        {
            SyncEditorFromVisual();
        }
    }

    private void Delete()
    {
        ClampSelection();
        if (SelectionLength > 0)
        {
            ReplaceSelection(string.Empty);
            return;
        }

        if (_caretIndex >= Text.Length)
        {
            return;
        }

        PushUndo();
        SetTextInternal(Text.Remove(_caretIndex, 1));
        _selectionAnchor = _caretIndex;
        ResetCaretBlink();
        EnsureCaretVisible();
        InvalidateMeasure();
        InvalidateVisual();
        if (_nativeActive)
        {
            SyncEditorFromVisual();
        }
    }

    private void Copy()
    {
        ClampSelection();
        if (SelectionLength > 0 && !IsPassword)
        {
            try
            {
                Clipboard.SetText(Text.Substring(SelectionStart, SelectionLength));
            }
            catch (ExternalException)
            {
                // The clipboard can be locked by another process; editing must keep working.
            }
        }
    }

    private static bool TryGetClipboardText(out string text)
    {
        text = string.Empty;
        try
        {
            if (!Clipboard.ContainsText())
            {
                return false;
            }

            text = Clipboard.GetText();
            return text.Length > 0;
        }
        catch (ExternalException)
        {
            return false;
        }
    }

    private void ClampSelection()
    {
        _selectionAnchor = XamlMath.Clamp(_selectionAnchor, 0, Text.Length);
        _caretIndex = XamlMath.Clamp(_caretIndex, 0, Text.Length);
    }

    private void ResetCaretBlink()
    {
        if (!IsFocused)
        {
            return;
        }

        _caretVisible = true;
        _caretTimer.Stop();
        if (CaretBlinkInterval > 0)
        {
            _caretTimer.Start();
        }
    }

    private void ApplyCaretBlinkInterval()
    {
        if (CaretBlinkInterval <= 0)
        {
            _caretTimer.Stop();
            _caretVisible = true;
            InvalidateVisual();
            return;
        }

        _caretTimer.Interval = CaretBlinkInterval;
        if (!IsFocused)
        {
            return;
        }

        _caretVisible = true;
        _caretTimer.Stop();
        _caretTimer.Start();
    }

    private void ShowNativeEditor()
    {
        // 没有窗口句柄时原生岛无处安放；UseNativeEditor=false 时整个编辑都留在自绘路径。
        if (!UseNativeEditor || Surface is not { IsHandleCreated: true })
        {
            return;
        }

        _nativeActive = true;
        ApplyNativeProperties();
        SyncEditorFromVisual();
        _nativeHost.Visibility = Easel.Controls.Visibility.Visible;
        ArrangeNativeEditor();
        UpdateFocusState();

        _focusHandoff = true;
        try
        {
            if (_editor.CanFocus)
            {
                _editor.Focus();
            }
        }
        finally
        {
            _focusHandoff = false;
        }

        // 第一次点击时焦点可能先落到编辑器、随后又被这次 WinForms 焦点处理收走；
        // 所以不判断当前是否已聚焦，统一排到下一次布局之后（PlaceNative 已经跑完）再补一次。
        _focusOnNextLayout = true;
        _focusAttempts = 0;
        if (Surface is { IsHandleCreated: true, IsDisposed: false } surface)
        {
            surface.LayoutUpdated -= OnSurfaceLayoutUpdated;
            surface.LayoutUpdated += OnSurfaceLayoutUpdated;
            surface.GotFocus -= OnSurfaceGotFocus;
            surface.GotFocus += OnSurfaceGotFocus;
        }

        _editorFocusTimer.Stop();
        _editorFocusTimer.Start();

        InvalidateVisual();
    }

    private void OnSurfaceLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_focusOnNextLayout)
        {
            return;
        }

        _focusOnNextLayout = false;
        if (Surface is { } surface)
        {
            surface.LayoutUpdated -= OnSurfaceLayoutUpdated;
            if (surface.IsHandleCreated && !surface.IsDisposed)
            {
                // 布局发生在绘制过程中；投回消息队列，等这一帧画完再抢焦点。
                surface.BeginInvoke(new Action(FocusNativeEditorIfNeeded));
            }
        }

        _editorFocusTimer.Stop();
    }

    private void OnSurfaceGotFocus(object? sender, EventArgs e) => FocusNativeEditorIfNeeded();

    private void FocusNativeEditorIfNeeded()
    {
        if (!_nativeActive || _editor.IsDisposed || !_editor.Visible || _editor.Focused || !_editor.CanFocus)
        {
            return;
        }

        _focusHandoff = true;
        try
        {
            _editor.Focus();
        }
        finally
        {
            _focusHandoff = false;
        }
    }

    private void HideNativeEditor()
    {
        if (!_nativeActive)
        {
            return;
        }

        _nativeActive = false;
        _focusOnNextLayout = false;
        _focusAttempts = 0;
        _editorFocusTimer.Stop();
        if (Surface is { } surface)
        {
            surface.LayoutUpdated -= OnSurfaceLayoutUpdated;
            surface.GotFocus -= OnSurfaceGotFocus;
        }

        _nativeHost.Visibility = Easel.Controls.Visibility.Collapsed;
        Surface?.RemoveNative(_nativeHost);
        if (!_editor.IsDisposed)
        {
            _editor.Visible = false;
        }

        UpdateFocusState();
        InvalidateVisual();
    }

    private void ArrangeNativeEditor()
    {
        var content = ActiveEditorRectangle();
        var size = new Size(Math.Max(0, content.Width), Math.Max(0, content.Height));
        _nativeHost.Measure(size);
        _nativeHost.Arrange(new RectangleF(content.Left, content.Top, size.Width, size.Height));
    }

    // 模板下原生岛盖住 PART_ContentHost；无模板时还是控件自己的内容矩形。
    private Rectangle ActiveEditorRectangle()
    {
        if (IsTemplated && _contentHost is { } host)
        {
            var origin = PointFromSurface(host.AbsoluteBounds.Location);
            return new Rectangle(
                (int)Math.Round(origin.X),
                (int)Math.Round(origin.Y),
                Math.Max(0, (int)Math.Round(host.AbsoluteBounds.Width)),
                Math.Max(0, (int)Math.Round(host.AbsoluteBounds.Height)));
        }

        return ContentRectangle();
    }

    private void UpdateFocusState()
        => SetValue(IsFocusedProperty, IsFocused || _nativeActive);

    private void ApplyNativeProperties()
    {
        if (!_nativeActive || _editor.IsDisposed)
        {
            return;
        }

        var theme = Theme.Current;
        _editor.Multiline = MultiLine;
        _editor.WordWrap = Wrap;
        _editor.ReadOnly = IsReadOnly;
        _editor.UseSystemPasswordChar = IsPassword;
        _editor.Font = Font;
        _editor.BackColor = ResolveNativeBackground();
        _editor.ForeColor = Foreground.IsEmpty
            ? (IsEnabled ? theme.Foreground : theme.DisabledForeground)
            : Foreground;
#if NET5_0_OR_GREATER
        _editor.PlaceholderText = Placeholder;
#endif
    }

    // 原生 TextBox 不支持透明背景色；视觉层的 Transparent 要落到最近一层不透明祖先或主题底色上。
    private Color ResolveNativeBackground()
    {
        // 模板下从内容部件向上找：模板的边框/底色才是岛该落的那一层。
        VisualElement? origin = _contentHost;
        for (VisualElement? current = origin ?? this; current is not null; current = current.ParentElement)
        {
            if (current.Background?.ApproximateColor is Color inherited && inherited.A > 0)
            {
                return Color.FromArgb(255, inherited.R, inherited.G, inherited.B);
            }
        }

        var fallback = Theme.Current.ControlBackground;
        return Color.FromArgb(255, fallback.R, fallback.G, fallback.B);
    }

    private void SyncEditorFromVisual()
    {
        if (!_nativeActive || _editor.IsDisposed)
        {
            return;
        }

        var text = Text.Replace("\r\n", "\n").Replace('\r', '\n');
        var editorText = text.Replace("\n", Environment.NewLine);
        _syncingNative = true;
        try
        {
            if (!string.Equals(_editor.Text, editorText, StringComparison.Ordinal))
            {
                _editor.Text = editorText;
            }

            var length = _editor.TextLength;
            _editor.SelectionStart = XamlMath.Clamp(SelectionStart, 0, length);
            _editor.SelectionLength = XamlMath.Clamp(SelectionLength, 0, length - _editor.SelectionStart);
        }
        finally
        {
            _syncingNative = false;
        }
    }

    private void SyncSelectionFromNative()
    {
        if (!_nativeActive || _syncingNative || _editor.IsDisposed)
        {
            return;
        }

        var start = XamlMath.Clamp(_editor.SelectionStart, 0, Text.Length);
        var length = XamlMath.Clamp(_editor.SelectionLength, 0, Text.Length - start);
        _selectionAnchor = start;
        _caretIndex = start + length;
        ResetCaretBlink();
        InvalidateVisual();
    }

    private void OnNativeTextChanged(object? sender, EventArgs e)
    {
        if (_syncingNative || _editor.IsDisposed)
        {
            return;
        }

        var text = _editor.Text.Replace("\r\n", "\n").Replace('\r', '\n');
        var length = text.Length;
        _selectionAnchor = XamlMath.Clamp(_editor.SelectionStart, 0, length);
        _caretIndex = XamlMath.Clamp(_editor.SelectionStart + _editor.SelectionLength, 0, length);

        // 原生编辑器的撤销栈由它自己维护；自绘路径的快照到这里已经对不上，直接作废。
        _undo.Clear();
        _redo.Clear();
        _syncingNative = true;
        try
        {
            SetTextInternal(text);
        }
        finally
        {
            _syncingNative = false;
        }

        InvalidateVisual();
    }

    private void OnNativeEditorLostFocus(object? sender, EventArgs e)
    {
        SyncSelectionFromNative();
        if (_nativeActive && !IsFocused && !_focusHandoff)
        {
            HideNativeEditor();
        }

        UpdateFocusState();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _caretTimer.Stop();
            _caretTimer.Dispose();
            _editorFocusTimer.Stop();
            _editorFocusTimer.Dispose();
            if (Surface is { } surface)
            {
                surface.LayoutUpdated -= OnSurfaceLayoutUpdated;
                surface.GotFocus -= OnSurfaceGotFocus;
            }

            Surface?.RemoveNative(_nativeHost);
            _nativeHost.Child = null;
            _editor.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void OnTemplateChanging()
    {
        _contentHost = null;
        _templateDiagnostic = null;
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        _contentHost = GetTemplateChild<TextBoxContentHost>("PART_ContentHost")
                       ?? GetTemplateChild<TextBoxContentHost>("PART_EditorHost");
        _templateDiagnostic = null;
        if (IsTemplated && _contentHost is null)
        {
            // 缺 ContentHost 时模板只剩外壳，文字与原生岛都没有落点；留可见诊断而不是静默降级。
            _templateDiagnostic =
                "The TextBox control template must contain a TextBoxContentHost named "
                + "PART_ContentHost or PART_EditorHost.";
            System.Diagnostics.Trace.TraceWarning(_templateDiagnostic);
        }

        _layoutDirty = true;
        if (_nativeActive)
        {
            ArrangeNativeEditor();
        }

        InvalidateVisual();
    }

    private readonly struct TextLine(int start, int length)
    {
        public int Start { get; } = start;

        public int Length { get; } = length;
    }

    private readonly struct TextSnapshot(string text, int caretIndex, int anchor)
    {
        public string Text { get; } = text;

        public int CaretIndex { get; } = caretIndex;

        public int Anchor { get; } = anchor;
    }
}
