using System.Drawing;
using System.Windows.Forms;
using Easel;
using XamlThickness = Easel.Controls.Thickness;
using XamlVisibility = Easel.Controls.Visibility;

namespace Easel.Visuals.Controls;

/// <summary>A header row that shows or hides one content element.</summary>
/// <remarks>
/// The default template parts are <c>PART_Header</c> (the area a click toggles), <c>PART_Toggle</c> (the glyph the
/// control points up or right) and <c>PART_ContentHost</c> (where <see cref="Content"/> is parented while a template
/// is applied).
/// </remarks>
public class Expander : TemplatedControl
{
    /// <summary>The text shown in the header.</summary>
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(Expander),
        new PropertyMetadata(string.Empty) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>Whether the content is shown.</summary>
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(Expander),
        new PropertyMetadata(true) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The content shown while expanded.</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(VisualElement), typeof(Expander),
        new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = false });

    private const float HeaderHeight = 32f;

    private VisualElement? _headerPart;
    private VisualElement? _contentHost;
    private VisualElement? _hostedContent;
    private TextBlock? _togglePart;
    private bool _disposing;

    /// <summary>Creates an expanded expander with the current theme metrics.</summary>
    public Expander()
    {
        Focusable = true;
        Padding = new XamlThickness(0f);
        CornerRadius = Theme.Current.CornerRadius;
    }

    /// <summary>The text shown in the header.</summary>
    public string Header
    {
        get => (string)GetValue(HeaderProperty)!;
        set => SetValue(HeaderProperty, value ?? string.Empty);
    }

    /// <summary>Whether the content is shown.</summary>
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty)!;
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>The content shown while expanded.</summary>
    public VisualElement? Content
    {
        get => (VisualElement?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        // 释放时让模板根一并释放 Content；只有换模板才把 Content 摘出来留给新模板。
        _disposing = disposing;
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsTemplated)
        {
            return base.MeasureOverride(availableSize);
        }

        var width = 0;
        var height = (int)Math.Ceiling(HeaderHeight + Padding.Vertical);
        if (IsExpanded && Content is { } content)
        {
            var measured = content.Measure(new Size(
                Math.Max(0, availableSize.Width - (int)Math.Ceiling(Padding.Horizontal)),
                0));
            width = measured.Width;
            height += measured.Height;
        }

        return new Size(width + (int)Math.Ceiling(Padding.Horizontal), height);
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        if (IsTemplated)
        {
            base.ArrangeOverride(finalSize);
            return;
        }

        if (!IsExpanded || Content is not { } content)
        {
            return;
        }

        content.Arrange(new RectangleF(
            (int)Math.Ceiling(Padding.Left),
            HeaderHeight,
            Math.Max(0f, finalSize.Width - Padding.Horizontal),
            Math.Max(0f, finalSize.Height - HeaderHeight - Padding.Vertical)));
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, ContentProperty))
        {
            SyncContent();
        }
        else if (ReferenceEquals(e.Property, IsExpandedProperty))
        {
            SyncToggle();
            ApplyContentVisibility();
            InvalidateMeasure();
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanging()
    {
        if (!_disposing)
        {
            DetachHostedContent();
        }

        _headerPart = null;
        _contentHost = null;
        _togglePart = null;
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        _headerPart = GetTemplateChild<VisualElement>("PART_Header");
        _contentHost = GetTemplateChild<VisualElement>("PART_ContentHost");
        _togglePart = GetTemplateChild<TextBlock>("PART_Toggle");
        SyncToggle();
        SyncContent();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        base.OnPointerPressed(args);
        if (args.Button != MouseButtons.Left || !IsPointerInHeader(args))
        {
            return;
        }

        Focus();
        IsExpanded = !IsExpanded;
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(VisualKeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (args.KeyCode is Keys.Enter or Keys.Space)
        {
            IsExpanded = !IsExpanded;
            args.Handled = true;
        }
    }

    // 有模板时以 PART_Header 的实际矩形为准；没有模板时沿用旧的 32 像素阈值。
    private bool IsPointerInHeader(VisualPointerEventArgs args)
    {
        if (_headerPart is { } header)
        {
            var point = args.GetPosition(header);
            return point.X >= 0f && point.Y >= 0f
                && point.X <= header.Bounds.Width && point.Y <= header.Bounds.Height;
        }

        return args.GetPosition(this).Y <= HeaderHeight;
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
                    AddToHost(host, content);
                }
            }
            else if (!ReferenceEquals(content.ParentElement, this))
            {
                content.ParentElement?.RemoveChild(content);
                Add(content);
            }
        }

        ApplyContentVisibility();
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
            RemoveFromHost(host, content);
        }

        _hostedContent = null;
    }

    private void ApplyContentVisibility()
    {
        if (_hostedContent is { } content)
        {
            content.Visibility = IsExpanded ? XamlVisibility.Visible : XamlVisibility.Collapsed;
        }
    }

    private void SyncToggle()
    {
        if (_togglePart is { } toggle)
        {
            toggle.Text = IsExpanded ? "▾" : "▸";
        }
    }

    private static void AddToHost(VisualElement host, VisualElement child)
    {
        if (host is Border border)
        {
            border.Child = child;
            return;
        }

        host.Add(child);
    }

    private static void RemoveFromHost(VisualElement host, VisualElement child)
    {
        if (host is Border border)
        {
            if (ReferenceEquals(border.Child, child))
            {
                border.Child = null;
            }

            return;
        }

        if (ReferenceEquals(child.ParentElement, host))
        {
            host.RemoveChild(child);
        }
    }
}

/// <summary>A titled frame with one child and a gap in its top border for the header.</summary>
/// <remarks>
/// The default template parts are <c>PART_Header</c> (the title badge that breaks the frame) and
/// <c>PART_ContentHost</c> (where <see cref="Child"/> is parented while a template is applied).
/// </remarks>
public class GroupBox : TemplatedControl
{
    /// <summary>The text drawn in the top border gap.</summary>
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(GroupBox),
        new PropertyMetadata(string.Empty) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The single child.</summary>
    public static readonly DependencyProperty ChildProperty = DependencyProperty.Register(
        nameof(Child), typeof(VisualElement), typeof(GroupBox),
        new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = false });

    private VisualElement? _contentHost;
    private VisualElement? _hostedChild;
    private bool _disposing;

    /// <summary>Creates an empty group box.</summary>
    public GroupBox()
    {
        IsHitTestVisible = false;
        Padding = new XamlThickness(10f);
        CornerRadius = 6f;
    }

    /// <summary>The text drawn in the top border gap.</summary>
    public string Header
    {
        get => (string)GetValue(HeaderProperty)!;
        set => SetValue(HeaderProperty, value ?? string.Empty);
    }

    /// <summary>The single child.</summary>
    public VisualElement? Child
    {
        get => (VisualElement?)GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    /// <summary>Adds the single child through <see cref="Child"/> so XAML content maps to it.</summary>
    /// <param name="child">The child to add.</param>
    /// <typeparam name="T">The child's concrete type.</typeparam>
    /// <returns>The added child.</returns>
    public new T Add<T>(T child) where T : VisualElement
    {
        Child = child;
        return child;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _disposing = disposing;
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsTemplated)
        {
            return base.MeasureOverride(availableSize);
        }

        var padding = Padding;
        var inner = new Size(
            Math.Max(0, availableSize.Width - (int)Math.Ceiling(padding.Horizontal)),
            Math.Max(0, availableSize.Height - (int)Math.Ceiling(padding.Vertical) - 8));
        var desired = Child?.Measure(inner) ?? Size.Empty;
        return new Size(
            desired.Width + (int)Math.Ceiling(padding.Horizontal),
            desired.Height + (int)Math.Ceiling(padding.Vertical) + 8);
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        if (IsTemplated)
        {
            base.ArrangeOverride(finalSize);
            return;
        }

        if (Child is not { } child)
        {
            return;
        }

        var padding = Padding;
        child.Arrange(new RectangleF(
            (int)Math.Ceiling(padding.Left),
            14f + (int)Math.Ceiling(padding.Top),
            Math.Max(0f, finalSize.Width - padding.Horizontal),
            Math.Max(0f, finalSize.Height - padding.Vertical - 14f)));
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, ChildProperty))
        {
            SyncChild();
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanging()
    {
        if (!_disposing)
        {
            DetachHostedChild();
        }

        _contentHost = null;
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        _contentHost = GetTemplateChild<VisualElement>("PART_ContentHost");
        SyncChild();
    }

    private void SyncChild()
    {
        var child = Child;
        if (!ReferenceEquals(_hostedChild, child))
        {
            DetachHostedChild();
            _hostedChild = child;
        }

        if (child is not null)
        {
            if (_contentHost is { } host)
            {
                if (!ReferenceEquals(child.ParentElement, host))
                {
                    child.ParentElement?.RemoveChild(child);
                    AddToHost(host, child);
                }
            }
            else if (!ReferenceEquals(child.ParentElement, this))
            {
                child.ParentElement?.RemoveChild(child);

                // 本类隐藏了 Add<T> 去写 Child；这里必须走基类，否则不会挂进 Children 参与绘制。
                base.Add(child);
            }
        }

        InvalidateMeasure();
    }

    private void DetachHostedChild()
    {
        if (_hostedChild is not { } child)
        {
            return;
        }

        if (ReferenceEquals(child.ParentElement, this))
        {
            RemoveChild(child);
        }
        else if (_contentHost is { } host && ReferenceEquals(child.ParentElement, host))
        {
            RemoveFromHost(host, child);
        }

        _hostedChild = null;
    }

    private static void AddToHost(VisualElement host, VisualElement child)
    {
        if (host is Border border)
        {
            border.Child = child;
            return;
        }

        host.Add(child);
    }

    private static void RemoveFromHost(VisualElement host, VisualElement child)
    {
        if (host is Border border)
        {
            if (ReferenceEquals(border.Child, child))
            {
                border.Child = null;
            }

            return;
        }

        if (ReferenceEquals(child.ParentElement, host))
        {
            host.RemoveChild(child);
        }
    }
}

/// <summary>One page of a <see cref="TabControl"/>; its template draws the tab header.</summary>
/// <remarks>
/// <para>
/// The tab control parents the page content, not this item, and hosts this item itself as the header container.
/// <see cref="IsSelected"/>, <see cref="IsPointerOver"/> and <see cref="TemplatedControl.IsFocusVisible"/> drive the
/// default template's bands and ring, so a host can replace the whole header look through the implicit
/// <c>TabItem</c> style without retemplating the tab control.
/// </para>
/// <para>
/// The state properties are visual only: the tab control still owns selection and content placement.
/// </para>
/// </remarks>
public class TabItem : TemplatedControl
{
    /// <summary>The text shown on the tab.</summary>
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(TabItem),
        new PropertyMetadata(string.Empty) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The content shown while this tab is selected.</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(VisualElement), typeof(TabItem),
        new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>Whether this tab is the selected page.</summary>
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(TabItem),
        new PropertyMetadata(false) { AffectsRender = true });

    /// <summary>Whether the pointer is over this tab header.</summary>
    public static readonly DependencyProperty IsPointerOverProperty = DependencyProperty.Register(
        nameof(IsPointerOver), typeof(bool), typeof(TabItem),
        new PropertyMetadata(false) { AffectsRender = true });

    /// <summary>Creates an empty tab.</summary>
    public TabItem()
    {
        Padding = new XamlThickness(0f);
    }

    /// <summary>The text shown on the tab.</summary>
    public string Header
    {
        get => (string)GetValue(HeaderProperty)!;
        set => SetValue(HeaderProperty, value ?? string.Empty);
    }

    /// <summary>The content shown while this tab is selected.</summary>
    public VisualElement? Content
    {
        get => (VisualElement?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>Whether this tab is the selected page.</summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty)!;
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Whether the pointer is over this tab header.</summary>
    /// <remarks>This dependency property shadows <see cref="VisualElement.IsPointerOver"/> so a style or template
    /// trigger can observe the tab's hover state.</remarks>
    public new bool IsPointerOver => (bool)GetValue(IsPointerOverProperty)!;

    /// <inheritdoc />
    protected override void OnPointerEntered(VisualPointerEventArgs args)
    {
        base.OnPointerEntered(args);
        SetValue(IsPointerOverProperty, true);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(VisualPointerEventArgs args)
    {
        base.OnPointerExited(args);
        SetValue(IsPointerOverProperty, false);
    }
}

/// <summary>A tab strip with one visible page.</summary>
/// <remarks>
/// The default template parts are <c>PART_HeaderHost</c> (the panel that receives the <see cref="TabItem"/> headers)
/// and <c>PART_ContentHost</c> (the area the selected page is arranged over). The selected page stays a direct child
/// of this control so existing tree walks keep finding it under the tab control. Header appearance belongs to the
/// <see cref="TabItem"/> container style; this control only pushes selection and focus state onto those containers.
/// </remarks>
public class TabControl : TemplatedControl
{
    /// <summary>The zero-based index of the selected page, or -1.</summary>
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(TabControl),
        new PropertyMetadata(-1) { AffectsMeasure = true, AffectsRender = true });

    private const float TabHeight = 32f;

    private Panel? _headerHost;
    private VisualElement? _contentHostPart;

    /// <summary>Creates an empty tab control.</summary>
    public TabControl()
    {
        Focusable = true;
        Padding = new XamlThickness(0f);
    }

    /// <summary>Raised after <see cref="SelectedIndex"/> changes.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>The pages.</summary>
    public IList<TabItem> Items { get; } = new List<TabItem>();

    /// <summary>The zero-based index of the selected page, or -1.</summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty)!;
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsTemplated)
        {
            var template = MeasureTemplate(availableSize);
            var page = MeasureSelectedContent(availableSize);
            return new Size(
                Math.Max(template.Width, page.Width + (int)Math.Ceiling(Padding.Horizontal)),
                template.Height + page.Height + (int)Math.Ceiling(Padding.Vertical));
        }

        var fallback = MeasureSelectedContent(availableSize);
        return new Size(
            fallback.Width + (int)Math.Ceiling(Padding.Horizontal),
            (int)Math.Ceiling(TabHeight + fallback.Height + Padding.Vertical));
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        if (IsTemplated)
        {
            ArrangeTemplate(finalSize);
            if (SelectedContent is { } content)
            {
                var origin = ContentOrigin();
                content.Arrange(new RectangleF(
                    origin.X,
                    origin.Y,
                    Math.Max(0f, finalSize.Width - origin.X - Padding.Right),
                    Math.Max(0f, finalSize.Height - origin.Y - Padding.Bottom)));
            }

            return;
        }

        if (SelectedContent is { } fallback)
        {
            fallback.Arrange(new RectangleF(
                (int)Math.Ceiling(Padding.Left),
                TabHeight,
                Math.Max(0f, finalSize.Width - Padding.Horizontal),
                Math.Max(0f, finalSize.Height - TabHeight - Padding.Vertical)));
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, SelectedIndexProperty))
        {
            SyncSelection();
            SyncContent();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (ReferenceEquals(e.Property, IsFocusVisibleProperty))
        {
            // 焦点环属于选中的页签；容器只接收状态，不自己拿焦点。
            SyncFocusRing();
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanging()
    {
        // 页签写在模板根里，换模板前先摘出来，否则模板根释放时会把它们一起释放。
        if (_headerHost is { } host)
        {
            foreach (var item in Items.ToArray())
            {
                if (ReferenceEquals(item.ParentElement, host))
                {
                    host.RemoveChild(item);
                }
            }
        }

        _headerHost = null;
        _contentHostPart = null;
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        _headerHost = GetTemplateChild<Panel>("PART_HeaderHost");
        _contentHostPart = GetTemplateChild<VisualElement>("PART_ContentHost");
        SyncHeaders();
        SyncSelection();
        SyncContent();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        base.OnPointerPressed(args);
        if (!IsEnabled || args.Button != MouseButtons.Left)
        {
            return;
        }

        var index = HitTestHeader(args);
        if (index < 0)
        {
            return;
        }

        Focus();
        SelectedIndex = index;
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(VisualKeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (args.KeyCode is not (Keys.Left or Keys.Right) || Items.Count == 0)
        {
            return;
        }

        var delta = args.KeyCode == Keys.Right ? 1 : -1;
        SelectedIndex = XamlMath.Clamp(SelectedIndex + delta, 0, Items.Count - 1);
        args.Handled = true;
    }

    /// <summary>Re-syncs the headers and the visible page after <see cref="Items"/> changed.</summary>
    public void Refresh()
    {
        SyncHeaders();
        SyncSelection();
        SyncContent();
        InvalidateMeasure();
    }

    private VisualElement? SelectedContent
        => SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex].Content : null;

    private void SyncHeaders()
    {
        if (_headerHost is not { } host)
        {
            return;
        }

        foreach (var child in host.Children.ToArray())
        {
            if (child is TabItem item && !Items.Contains(item))
            {
                host.RemoveChild(item);
            }
        }

        foreach (var item in Items)
        {
            if (ReferenceEquals(item.ParentElement, host))
            {
                continue;
            }

            item.ParentElement?.RemoveChild(item);
            host.Add(item);
        }
    }

    private void SyncSelection()
    {
        for (var index = 0; index < Items.Count; index++)
        {
            Items[index].IsSelected = index == SelectedIndex;
        }

        SyncFocusRing();
    }

    // 页签本身不拿焦点：把本控件的键盘焦点可见态推给选中的容器，模板触发器就能画出焦点环。
    private void SyncFocusRing()
    {
        var visible = IsFocusVisible;
        for (var index = 0; index < Items.Count; index++)
        {
            Items[index].SetValue(IsFocusVisibleProperty, visible && index == SelectedIndex);
        }
    }

    private void SyncContent()
    {
        for (var index = 0; index < Items.Count; index++)
        {
            if (Items[index].Content is not { } content)
            {
                continue;
            }

            if (index == SelectedIndex)
            {
                if (!ReferenceEquals(content.ParentElement, this))
                {
                    if (content.Parent is ContainerVisual parent)
                    {
                        parent.Remove(content);
                    }

                    Add(content);
                }
            }
            else if (ReferenceEquals(content.ParentElement, this))
            {
                RemoveChild(content);
            }
        }
    }

    private Size MeasureSelectedContent(Size availableSize)
    {
        if (SelectedContent is not { } content)
        {
            return Size.Empty;
        }

        return content.Measure(new Size(
            Math.Max(0, availableSize.Width - (int)Math.Ceiling(Padding.Horizontal)),
            0));
    }

    // 内容区由模板里的 PART_ContentHost 定位；模板没给部件时退回页签条底部。
    private PointF ContentOrigin()
    {
        var self = AbsoluteBounds;
        if (_contentHostPart is { } contentHost)
        {
            var bounds = contentHost.AbsoluteBounds;
            return new PointF(
                Padding.Left + bounds.X - self.X,
                bounds.Y - self.Y);
        }

        if (_headerHost is { } headerHost)
        {
            return new PointF(Padding.Left, headerHost.AbsoluteBounds.Bottom - self.Y);
        }

        return new PointF(Padding.Left, TabHeight);
    }

    private int HitTestHeader(VisualPointerEventArgs args)
    {
        if (_headerHost is null)
        {
            return -1;
        }

        for (var index = 0; index < Items.Count; index++)
        {
            var item = Items[index];
            if (!ReferenceEquals(item.ParentElement, _headerHost))
            {
                continue;
            }

            var point = args.GetPosition(item);
            if (point.X >= 0f && point.Y >= 0f
                && point.X <= item.Bounds.Width && point.Y <= item.Bounds.Height)
            {
                return index;
            }
        }

        return -1;
    }
}
