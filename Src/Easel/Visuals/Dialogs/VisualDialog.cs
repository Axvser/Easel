using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Easel.Animation;
using Easel.Controls;
using Easel.Visuals.Controls;
using XamlButton = Easel.Visuals.Controls.Button;
using XamlTextBox = Easel.Visuals.Controls.TextBox;

namespace Easel.Visuals.Dialogs;

/// <summary>
/// A modal card that paints a full-surface scrim over a <see cref="VisualHost"/> and returns an asynchronous result.
/// </summary>
/// <remarks>
/// <para>
/// The dialog lives in the surface's overlay layer, so it paints and hit-tests above the root tree without creating a
/// second HWND. Its scrim is hit-testable: pointer input never reaches the tree below while the dialog is open, and
/// the newest of several stacked dialogs is the one that receives input and focus.
/// </para>
/// <para>
/// Focus moves to the primary button when the dialog opens and Tab cycles only inside the dialog. Escape closes with
/// <see cref="DialogResult.Cancel"/> when <see cref="DialogOptions.DismissOnEscape"/> allows it; an outside press
/// closes it when <see cref="DialogOptions.DismissOnOutsideClick"/> is set. Closing restores the element that had
/// focus before the dialog opened. Live native islands are hidden for the dialog's lifetime and placed again on close.
/// </para>
/// <para>
/// <see cref="TemplatedControl.Template"/> replaces the card content. The parts <c>PART_Icon</c>, <c>PART_Title</c>,
/// <c>PART_Content</c>, <c>PART_Primary</c>, <c>PART_Secondary</c>, <c>PART_Cancel</c> and <c>PART_Close</c> are
/// populated when present and ignored when absent; the self-drawn card is used when no template is applied.
/// </para>
/// </remarks>
public class VisualDialog : TemplatedControl
{
    private const float OuterMargin = 24f;
    private const float CardCornerRadius = 12f;
    private const float CardPadding = 24f;
    private const float MinimumCardWidth = 320f;

    private readonly DialogOptions _options;
    private readonly TaskCompletionSource<DialogResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly VisualElement _scrim;
    private readonly Border _card;
    private readonly List<PartHook> _partHooks = [];

    private VisualElement? _defaultContent;
    private TextBlock? _titleBlock;
    private TextBlock? _messageBlock;
    private TextBlock? _promptLabel;
    private XamlTextBox? _promptBox;
    private DialogButton? _primaryButton;
    private DialogButton? _secondaryButton;
    private DialogButton? _cancelButton;
    private VisualElement? _templatePrimary;
    private VisualHost? _host;
    private VisualElement? _previousFocus;
    private bool _closed;
    private bool _disposed;
    private DialogResult _result;

    /// <summary>Creates a dialog with default options.</summary>
    public VisualDialog()
        : this(new DialogOptions())
    {
    }

    /// <summary>Creates a dialog that shows <paramref name="options"/>.</summary>
    /// <param name="options">The content and behaviour of the dialog.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public VisualDialog(DialogOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        Focusable = true;
        IsHitTestVisible = true;
        ClipToBounds = false;

        _scrim = new DialogScrim
        {
            Background = new SolidColorBrush(ScrimColor()),
        };
        Add(_scrim);

        _card = new Border
        {
            Background = new SolidColorBrush(Theme.Current.Surface),
            BorderBrush = new SolidColorBrush(Theme.Current.ControlBorder),
            BorderThickness = new Thickness(1f),
            CornerRadius = CardCornerRadius,
            Padding = new Thickness(CardPadding),
            IsHitTestVisible = true,
            Effect = new DropShadowEffect
            {
                Offset = new PointF(0f, 10f),
                Blur = 24f,
                Opacity = 0.35d,
                Color = Color.Black,
            },
        };
        Add(_card);

        Theme.Changed += OnThemeChanged;
    }

    /// <summary>Raised after the dialog joined the surface overlay.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised after the dialog left the surface overlay.</summary>
    public event EventHandler? Closed;

    /// <summary>The options this dialog was created with.</summary>
    public DialogOptions Options => _options;

    /// <summary>Whether the dialog is currently shown.</summary>
    public bool IsOpen => _host is not null;

    /// <summary>The result the dialog completed with, or <see langword="null"/> while it is open.</summary>
    public DialogResult? Result => _closed ? _result : null;

    /// <summary>The text typed into a prompt dialog, or an empty string for other kinds.</summary>
    public string PromptText => _promptBox?.Text ?? string.Empty;

    // 测试与宿主诊断用的几何/部件钩子，不参与公开契约。
    internal VisualElement Card => _card;

    internal VisualElement? PrimaryButton => _templatePrimary ?? _primaryButton;

    internal VisualElement? SecondaryButton => _secondaryButton;

    internal VisualElement? CancelButton => _cancelButton;

    internal XamlTextBox? PromptBox => _promptBox;

    /// <summary>Shows the dialog in <paramref name="host"/> and returns its result when it closes.</summary>
    /// <param name="host">The surface whose overlay hosts the dialog.</param>
    /// <returns>A task completed with the button or dismissal that closed the dialog.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is <see langword="null"/>.</exception>
    public Task<DialogResult> ShowAsync(VisualHost host)
    {
        if (host is null) throw new ArgumentNullException(nameof(host));
        if (_closed)
        {
            return Task.FromResult(_result);
        }

        if (_host is not null)
        {
            return _completion.Task;
        }

        _host = host;
        if (!IsTemplated)
        {
            _card.Child = EnsureDefaultContent();
        }

        _previousFocus = host.FocusedElement;
        host.OverlayHost.Add(this);
        host.RegisterDialog(this);
        Reposition();
        FocusInitial();
        StartEntrance();
        Opened?.Invoke(this, EventArgs.Empty);
        return _completion.Task;
    }

    /// <summary>Closes the dialog with <paramref name="result"/>; repeat calls keep the first result.</summary>
    /// <param name="result">The result reported to the awaiting caller.</param>
    public void Close(DialogResult result)
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _result = result;

        var host = _host;
        _host = null;

        VisualTransition.Stop(_scrim);
        VisualTransition.Stop(_card);
        _scrim.Opacity = 1d;
        _card.Opacity = 1d;
        _card.Transform = new Matrix();

        if (host is not null)
        {
            if (host.FocusedElement is { } focused && ContainsVisual(focused))
            {
                host.FocusElement(null);
            }

            host.UnregisterDialog(this);
            host.OverlayHost.Remove(this);
            RestoreFocus(host);
        }

        Theme.Changed -= OnThemeChanged;
        _completion.TrySetResult(result);
        Closed?.Invoke(this, EventArgs.Empty);
    }

    // 覆盖层自己排不了子项，由表面在每次布局后调这里把对话框铺满客户区并让卡片居中。
    internal void Reposition()
    {
        if (_host is null)
        {
            return;
        }

        Reposition(_host.LogicalClientSize);
    }

    internal void Reposition(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        Measure(size);
        Arrange(new RectangleF(0f, 0f, size.Width, size.Height));
        InvalidateVisual();
    }

    // Tab 的候选只在最上层对话内部收集；遮罩本身没有可聚焦子项。
    internal void CollectFocusOrder(List<VisualElement> order)
    {
        foreach (var child in Children)
        {
            if (child is VisualElement element)
            {
                VisualHost.CollectFocusable(element, order);
            }
        }
    }

    internal bool ContainsVisual(VisualElement element)
    {
        for (var current = element; current is not null; current = current.ParentElement)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
    }

    internal void FocusInitial()
    {
        if (_host is not { } host)
        {
            return;
        }

        var primary = _templatePrimary ?? (VisualElement?)_primaryButton;
        if (primary is not null
            && primary.Focusable
            && primary.IsVisible
            && primary.IsEffectivelyEnabled)
        {
            host.FocusElement(primary);
            return;
        }

        var order = new List<VisualElement>();
        CollectFocusOrder(order);
        if (order.Count > 0)
        {
            host.FocusElement(order[0]);
            return;
        }

        host.FocusElement(this);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Max(0, availableSize.Width);
        var height = Math.Max(0, availableSize.Height);

        _scrim.Measure(availableSize);

        if (_card.Child is not null)
        {
            var maximum = CardWidth(width);
            var maximumHeight = Math.Max(0, height - (int)(OuterMargin * 2f));
            _card.Measure(new Size(
                Math.Max(1, (int)Math.Ceiling(maximum)),
                Math.Max(1, maximumHeight)));

            // 卡片按内容收窄到最小宽度；换行宽度必须以收窄后的宽度再量一次，否则正文按最大宽度折行。
            var target = Math.Min(maximum, Math.Max(
                Math.Min(MinimumCardWidth, maximum),
                _card.DesiredSize.Width));
            if (Math.Abs(target - maximum) >= 0.5f)
            {
                _card.Measure(new Size(
                    Math.Max(1, (int)Math.Ceiling(target)),
                    Math.Max(1, maximumHeight)));
            }
        }

        return availableSize;
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        _scrim.Measure(finalSize);
        _scrim.Arrange(new RectangleF(0f, 0f, finalSize.Width, finalSize.Height));

        if (_card.Child is null)
        {
            return;
        }

        var maximum = CardWidth(finalSize.Width);
        var maximumHeight = Math.Max(0f, finalSize.Height - OuterMargin * 2f);
        var cardWidth = (float)Math.Min(maximum, Math.Max(
            Math.Min(MinimumCardWidth, maximum),
            _card.DesiredSize.Width));
        var cardHeight = Math.Min(maximumHeight, _card.DesiredSize.Height);
        var x = (finalSize.Width - cardWidth) / 2f;
        var y = (finalSize.Height - cardHeight) / 2f;
        _card.Arrange(new RectangleF(x, y, Math.Max(0f, cardWidth), Math.Max(0f, cardHeight)));
    }

    /// <inheritdoc />
    protected override void OnKeyDown(VisualKeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (args.Handled)
        {
            return;
        }

        if (args.KeyCode == Keys.Escape && _options.DismissOnEscape)
        {
            Close(DialogResult.Cancel);
            args.Handled = true;
            return;
        }

        // 焦点落在输入框等不自带 Enter 行为的元素上时，Enter 仍按主按钮处理。
        if (args.KeyCode == Keys.Enter
            && (args.Modifiers & Keys.Alt) == 0)
        {
            Close(DialogResult.Primary);
            args.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        base.OnPointerPressed(args);
        if (args.Handled || !_options.DismissOnOutsideClick)
        {
            return;
        }

        var card = _card.AbsoluteBounds;
        if (card.Width > 0f && card.Height > 0f && card.Contains(args.Position))
        {
            return;
        }

        Close(DialogResult.Cancel);
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnTemplateChanging()
    {
        UnhookParts();
        _templatePrimary = null;

        // 模板根被我们挂进了卡片；先摘出来，基类随后才 Dispose 它，不会再从对话框的 Children 里找。
        if (TemplateRoot is { } root && ReferenceEquals(_card.Child, root))
        {
            _card.Child = null;
        }

        base.OnTemplateChanging();
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        base.OnTemplateChanged();

        if (TemplateRoot is { } root)
        {
            if (Children.Contains(root))
            {
                Remove(root);
            }

            _card.Child = root;
            root.RefreshStyle();
            ApplyTemplateParts();
            InvalidateMeasure();
        }
        else
        {
            _card.Child = EnsureDefaultContent();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposed = true;
            Theme.Changed -= OnThemeChanged;
            UnhookParts();
            if (!_closed)
            {
                _closed = true;
                _result = DialogResult.Cancel;
                _completion.TrySetResult(_result);
                if (_host is { } host)
                {
                    _host = null;
                    if (host.FocusedElement is { } focused && ContainsVisual(focused))
                    {
                        host.FocusElement(null);
                    }

                    host.UnregisterDialog(this);
                    if (host.OverlayHost.Children.Contains(this))
                    {
                        host.OverlayHost.Remove(this);
                    }
                }

                Closed?.Invoke(this, EventArgs.Empty);
            }

            VisualTransition.Stop(_scrim);
            VisualTransition.Stop(_card);
        }

        base.Dispose(disposing);
    }

    private float CardWidth(int surfaceWidth)
    {
        var configured = _options.MaxWidth > 0f ? _options.MaxWidth : 460f;
        return Math.Max(1f, Math.Min(configured, surfaceWidth - OuterMargin * 2f));
    }

    private string BodyText
    {
        get
        {
            if (_options.Content is string text)
            {
                return text;
            }

            if (_options.Content is null || _options.Content is VisualElement)
            {
                return _options.Message ?? string.Empty;
            }

            return _options.Content.ToString() ?? string.Empty;
        }
    }

    private static Color ScrimColor()
    {
        var alpha = Theme.EffectiveMode == ThemeMode.HighContrast ? 0x99 : 0x66;
        return Color.FromArgb(alpha, 0, 0, 0);
    }

    private void StartEntrance()
    {
        var animate = _options.Animate && _host is { IsHandleCreated: true };
        if (!animate)
        {
            _scrim.Opacity = 1d;
            _card.Opacity = 1d;
            _card.Transform = new Matrix();
            return;
        }

        _scrim.Opacity = 0d;
        _card.Opacity = 0d;
        var offset = new Matrix();
        offset.Translate(0f, 10f);
        _card.Transform = offset;

        var spec = new AnimationSpec
        {
            Duration = TimeSpan.FromMilliseconds(180),
            Ease = Eases.Cubic.Out,
        };
        VisualTransition.Animate(_scrim, VisualElement.OpacityProperty, 1d, spec);
        VisualTransition.Animate(_card, VisualElement.OpacityProperty, 1d, spec);
        VisualTransition.Animate(_card, VisualElement.TransformProperty, new Matrix(), spec);
    }

    private void RestoreFocus(VisualHost host)
    {
        var next = host.ActiveDialog;
        if (next is not null)
        {
            // 关掉不是最上层的对话时，焦点不能落回被遮住的根树，而是回到当前最上层对话。
            var current = host.FocusedElement;
            if (current is null || !next.ContainsVisual(current))
            {
                next.FocusInitial();
            }

            return;
        }

        if (_previousFocus is { } previous
            && ReferenceEquals(previous.Surface, host)
            && previous.IsVisible
            && previous.IsEffectivelyEnabled)
        {
            host.FocusElement(previous);
        }
    }

    private VisualElement EnsureDefaultContent()
    {
        if (_defaultContent is not null)
        {
            return _defaultContent;
        }

        var theme = Theme.Current;
        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 0 };

        var header = new Grid
        {
            Margin = new Thickness(0f, 0f, 0f, 14f),
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        var icon = new DialogIcon(_options.Kind)
        {
            Width = 32,
            Height = 32,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Center,
        };
        Grid.SetColumn(icon, 0);
        header.Add(icon);
        _titleBlock = new TextBlock
        {
            Text = _options.Title ?? string.Empty,
            FontSize = 15.5d,
            FontWeight = FontWeight.SemiBold,
            Foreground = theme.Foreground,
            Wrap = true,
            Margin = new Thickness(12f, 0f, 0f, 0f),
            VerticalAlignment = Easel.Controls.VerticalAlignment.Center,
        };
        Grid.SetColumn(_titleBlock, 1);
        header.Add(_titleBlock);
        stack.Add(header);

        var body = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 6,
            Margin = new Thickness(0f, 0f, 0f, 22f),
        };

        if (_options.Content is VisualElement custom)
        {
            if (custom.Parent is ContainerVisual parent)
            {
                parent.Remove(custom);
            }

            body.Add(custom);
        }
        else
        {
            var message = BodyText;
            if (!string.IsNullOrEmpty(message))
            {
                _messageBlock = new TextBlock
                {
                    Text = message,
                    FontSize = 11d,
                    Foreground = theme.MutedForeground,
                    Wrap = true,
                };
                body.Add(_messageBlock);
            }
        }

        if (_options.IsPrompt)
        {
            if (!string.IsNullOrEmpty(_options.PromptLabel))
            {
                _promptLabel = new TextBlock
                {
                    Text = _options.PromptLabel!,
                    FontSize = 10d,
                    Foreground = theme.MutedForeground,
                };
                body.Add(_promptLabel);
            }

            _promptBox = new XamlTextBox
            {
                Text = _options.PromptInitial ?? string.Empty,
                // 提示框走纯视觉编辑：原生编辑器是子 HWND，会盖在遮罩上，也会被对话的原生岛收起重置。
                UseNativeEditor = false,
                MinHeight = 32,
                TabIndex = 0,
            };
            body.Add(_promptBox);
        }

        stack.Add(body);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Easel.Controls.HorizontalAlignment.Right,
        };

        if (!string.IsNullOrWhiteSpace(_options.SecondaryText))
        {
            _secondaryButton = new DialogButton(DialogButtonRole.Secondary, _options.SecondaryText!)
            {
                TabIndex = 2,
            };
            _secondaryButton.Click += (_, _) => Close(DialogResult.Secondary);
            footer.Add(_secondaryButton);
        }

        if (_options.ShowCancel)
        {
            _cancelButton = new DialogButton(
                DialogButtonRole.Cancel,
                string.IsNullOrWhiteSpace(_options.CancelText) ? "Cancel" : _options.CancelText!)
            {
                TabIndex = 3,
            };
            _cancelButton.Click += (_, _) => Close(DialogResult.Cancel);
            footer.Add(_cancelButton);
        }

        _primaryButton = new DialogButton(DialogButtonRole.Primary, _options.PrimaryText ?? "OK")
        {
            TabIndex = 1,
        };
        _primaryButton.Click += (_, _) => Close(DialogResult.Primary);
        footer.Add(_primaryButton);

        stack.Add(footer);
        _defaultContent = stack;
        return stack;
    }

    private void ApplyTemplateParts()
    {
        if (GetTemplateChild<TextBlock>("PART_Title") is { } title)
        {
            title.Text = _options.Title ?? string.Empty;
        }

        if (GetTemplateChild<TextBlock>("PART_Icon") is { } icon)
        {
            icon.Text = DialogPalette.Glyph(_options.Kind);
        }

        var content = GetTemplateChild<VisualElement>("PART_Content");
        if (content is TextBlock contentText)
        {
            contentText.Text = BodyText;
        }
        else if (content is ContentControl contentControl && contentControl.Content is null)
        {
            contentControl.Content = BodyText;
        }

        HookPart("PART_Primary", DialogResult.Primary, _options.PrimaryText ?? "OK", isPrimary: true);
        HookPart("PART_Secondary", DialogResult.Secondary, _options.SecondaryText ?? "Cancel", isPrimary: false);
        HookPart("PART_Cancel", DialogResult.Cancel, _options.CancelText ?? "Cancel", isPrimary: false);
        HookPart("PART_Close", DialogResult.Cancel, null, isPrimary: false);
    }

    private void HookPart(string name, DialogResult result, string? text, bool isPrimary)
    {
        if (GetTemplateChild<VisualElement>(name) is not { } part)
        {
            return;
        }

        if (isPrimary)
        {
            _templatePrimary = part;
        }

        if (part is XamlButton button)
        {
            if (!string.IsNullOrEmpty(text) && string.IsNullOrEmpty(button.Text))
            {
                button.Text = text!;
            }

            EventHandler handler = (_, _) => Close(result);
            button.Click += handler;
            _partHooks.Add(new PartHook(part, handler, null));
            return;
        }

        EventHandler<VisualPointerEventArgs> pointer = (_, args) =>
        {
            if (part.AbsoluteBounds.Contains(args.Position))
            {
                Close(result);
            }
        };
        part.PointerReleased += pointer;
        _partHooks.Add(new PartHook(part, null, pointer));
    }

    private void UnhookParts()
    {
        foreach (var hook in _partHooks)
        {
            if (hook.Click is { } click && hook.Element is XamlButton button)
            {
                button.Click -= click;
            }

            if (hook.Pointer is { } pointer)
            {
                hook.Element.PointerReleased -= pointer;
            }
        }

        _partHooks.Clear();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        var theme = Theme.Current;
        _card.Background = new SolidColorBrush(theme.Surface);
        _card.BorderBrush = new SolidColorBrush(theme.ControlBorder);
        _scrim.Background = new SolidColorBrush(ScrimColor());

        if (_titleBlock is not null)
        {
            _titleBlock.Foreground = theme.Foreground;
        }

        if (_messageBlock is not null)
        {
            _messageBlock.Foreground = theme.MutedForeground;
        }

        if (_promptLabel is not null)
        {
            _promptLabel.Foreground = theme.MutedForeground;
        }

        InvalidateVisual();
    }

    // Element 上的事件类型不同（Button.Click 与 VisualElement.PointerReleased），按钩子种类分别摘。
    private sealed class PartHook
    {
        internal PartHook(
            VisualElement element,
            EventHandler? click,
            EventHandler<VisualPointerEventArgs>? pointer)
        {
            Element = element;
            Click = click;
            Pointer = pointer;
        }

        internal VisualElement Element { get; }

        internal EventHandler? Click { get; }

        internal EventHandler<VisualPointerEventArgs>? Pointer { get; }
    }
}
