using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XamlVisibility = Easel.Controls.Visibility;

namespace Easel.Visuals.Controls;

/// <summary>
/// A visual-tree element that represents a native WinForms control and gives it the visual layout contract.
/// </summary>
/// <remarks>
/// <para>
/// The native control keeps its own HWND and its own painting/input; this element measures with
/// <see cref="Control.GetPreferredSize"/>, arranges by writing the final rectangle back to <see cref="Control.Bounds"/>
/// in physical pixels, and re-measures when the native preferred size changes. Layout therefore runs through the
/// same <c>Grid</c>/<c>StackPanel</c>/<c>Canvas</c> pass as any <see cref="VisualElement"/>.
/// </para>
/// <para>
/// <see cref="Content"/> is the visual content shown inside the native control's <see cref="Control.DisplayRectangle"/>.
/// It is painted by a nested <see cref="VisualHost"/> hosted as a child of the native control, because a child HWND
/// always paints above the surface that owns it: visual children of a native control cannot join the outer paint pass.
/// The nested surface receives its own input and bridges <see cref="VisualElement.DataContext"/>, resources, styles
/// and inherited font/enabled values from this element.
/// </para>
/// <para>
/// The native HWND still cannot be transformed, alpha-blended or clipped to a shape by the outer tree, and it always
/// paints above the visual content around it. Those are window-system limits, not layout limits. Snapshot mode
/// (<see cref="IsSnapshot"/>) paints a bitmap in the visual pass when composition is required; an almost invisible
/// input proxy keeps real mouse, keyboard and IME input alive in a Form.
/// </para>
/// </remarks>
public class NativeElement : VisualElement
{
    /// <summary>Whether the element paints a bitmap snapshot of the control instead of showing its HWND.</summary>
    /// <remarks>
    /// A snapshot takes part in the normal visual pass, so <see cref="VisualElement.Opacity"/>,
    /// <see cref="VisualElement.Transform"/> and clip apply. The window is detached while snapshot mode is on, so the
    /// control loses live input, IME and focus. Use it for read-only or purely visual content; call
    /// <see cref="RefreshSnapshot"/> after changing it.
    /// </remarks>
    public static readonly DependencyProperty IsSnapshotProperty = DependencyProperty.Register(
        nameof(IsSnapshot), typeof(bool), typeof(NativeElement),
        new PropertyMetadata(false) { AffectsRender = true });

    private Control? _native;
    private VisualElement? _content;
    private Bitmap? _snapshot;
    private bool _snapshotDirty;
    private NativeSnapshotProxy? _inputProxy;
    private Rectangle? _proxyPhysicalBounds;
    private VisualHost? _contentSurface;
    private ContentControl? _contentRoot;
    private readonly List<(VisualStyleCollection Collection, EventHandler Handler)> _styleSubscriptions = [];
    private readonly List<(string PropertyName, DependencyProperty Property)> _twoWayBindings = [];
    private List<IDisposable>? _twoWaySubscriptions;
    private Size _declaredSize;
    private bool _placing;

    /// <summary>Creates an element with no native control yet.</summary>
    public NativeElement()
    {
        IsHitTestVisible = false;
        SurfaceChanged += OnSurfaceChanged;
    }

    /// <summary>Creates an element over <paramref name="native"/>.</summary>
    /// <param name="native">The native control placed over this element's rectangle.</param>
    /// <param name="ownsNative">Whether disposing this element disposes <paramref name="native"/>.</param>
    public NativeElement(Control native, bool ownsNative = false)
        : this()
    {
        OwnsNative = ownsNative;
        Native = native;
    }

    /// <summary>The native control placed over this element's rectangle, or <see langword="null"/>.</summary>
    /// <remarks>
    /// Setting a new control detaches the previous one and drops its window from the surface, but does not dispose
    /// it. The element does not take ownership unless <see cref="OwnsNative"/> asks for it.
    /// </remarks>
    public Control? Native
    {
        get => _native;
        set
        {
            if (ReferenceEquals(_native, value))
            {
                return;
            }

            DetachNative(_native);
            Surface?.RemoveNative(this);
            if (value is null && _contentSurface is not null)
            {
                _contentSurface.Parent?.Controls.Remove(_contentSurface);
                _contentSurface.Visible = false;
            }

            _native = value;
            AttachNative(_native);
            InvalidateMeasure();
            InvalidateArrange();
        }
    }

    /// <summary>Whether disposing this element disposes <see cref="Native"/>.</summary>
    public bool OwnsNative { get; set; }

    /// <summary>Whether the element paints a bitmap snapshot of the control instead of showing its HWND.</summary>
    public bool IsSnapshot
    {
        get => (bool)GetValue(IsSnapshotProperty)!;
        set => SetValue(IsSnapshotProperty, value);
    }

    /// <summary>The visual content shown inside the native control's display rectangle, or <see langword="null"/>.</summary>
    /// <remarks>
    /// A native element hosts one visual root. Multiple direct children need an explicit panel such as
    /// <c>&lt;StackPanel&gt;</c>; that mirrors how a single content slot works everywhere else in the runtime.
    /// </remarks>
    public VisualElement? Content
    {
        get => _content;
        set
        {
            if (ReferenceEquals(_content, value))
            {
                return;
            }

            _content = value;
            if (value is not null)
            {
                EnsureContentSurface();
                if (_contentRoot is not null)
                {
                    _contentRoot.Content = value;
                }
            }
            else
            {
                // 先摘内容再释放表面：替换/清空内容不应该顺手 Dispose 调用方还打算复用的视觉树。
                if (_contentRoot is not null)
                {
                    _contentRoot.Content = null;
                }

                ReleaseContentSurface();
            }

            InvalidateMeasure();
        }
    }

    /// <summary>The nested surface that paints <see cref="Content"/>, or <see langword="null"/> when there is none.</summary>
    public VisualHost? ContentSurface => _contentSurface;

    // 表面扫描也会摆放岛（为跳过 ArrangeOverride 的移动补课）；那种 SizeChanged 不是调用方改的声明尺寸。
    internal bool IsBeingPlaced { get; set; }

    internal NativeSnapshotProxy? InputProxy => _inputProxy;

    /// <summary>Whether this island is frozen to a bitmap for the duration of a pointer gesture.</summary>
    internal bool IsFrozen { get; private set; }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (_native is null)
        {
            return Size.Empty;
        }

        var scale = CurrentScale;
        var size = _native.AutoSize ? MeasurePreferred(availableSize, scale) : _declaredSize;
        return ToDips(size, scale);
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        if (_native is null)
        {
            return;
        }

        if (IsFrozen)
        {
            // 手势期间岛冻结成位图，由视觉管线绘制；松手后再按当前排列矩形摆回原生窗口。
            return;
        }

        if (IsSnapshot)
        {
            // 快照模式：摘掉 HWND，位图由视觉管线绘制，Opacity/Transform/Clip 这才生效。
            // 先让输入代理把控件铺到变换后的包围盒，再按这个尺寸抓快照。
            Surface?.RemoveNative(this);
            UpdateInputProxy();
            CaptureSnapshot();
            return;
        }

        _placing = true;
        try
        {
            // 摆放权归视觉布局：Anchor/Dock 会把控件重新挪回 WinForms 自己的 LayoutEngine。
            _native.Anchor = AnchorStyles.None;
            _native.Dock = DockStyle.None;
            Surface?.PlaceNative(this, _native, Rectangle.Round(AbsoluteBounds));
        }
        finally
        {
            _placing = false;
        }

        PlaceContentSurface();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (ReferenceEquals(e.Property, DataContextProperty))
        {
            if (_contentRoot is not null)
            {
                _contentRoot.DataContext = DataContext;
            }

            return;
        }

        if (ReferenceEquals(e.Property, IsEnabledProperty))
        {
            if (_native is not null)
            {
                _native.Enabled = IsEnabled;
            }

            if (_contentRoot is not null)
            {
                _contentRoot.IsEnabled = IsEnabled;
            }

            return;
        }

        if (ReferenceEquals(e.Property, FontSizeProperty)
            || ReferenceEquals(e.Property, FontFamilyProperty)
            || ReferenceEquals(e.Property, FontWeightProperty))
        {
            ApplyInheritedTypography();
            return;
        }

        if (ReferenceEquals(e.Property, VisibilityProperty))
        {
            PlaceContentSurface();
            return;
        }

        if (ReferenceEquals(e.Property, CornerRadiusProperty))
        {
            Surface?.RefreshNativeRegions();
            return;
        }

        if (ReferenceEquals(e.Property, IsSnapshotProperty))
        {
            if (IsSnapshot)
            {
                Surface?.RemoveNative(this);
            }
            else
            {
                DisposeInputProxy();
            }

            _snapshotDirty = true;
            InvalidateArrange();
            InvalidateVisual();
            return;
        }

        if (ReferenceEquals(e.Property, Easel.Visuals.Controls.Panel.ZIndexProperty))
        {
            Surface?.ReorderNativeChildren();
        }
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        if ((!IsSnapshot && !IsFrozen) || _snapshot is null)
        {
            return;
        }

        var bounds = new RectangleF(0f, 0f, Bounds.Width, Bounds.Height);
        if (bounds.Width > 0f && bounds.Height > 0f)
        {
            // 活体 HWND 只能被窗口区域切角（二值、原生边框会被切断）；快照模式可以按圆角路径裁位图并补画视觉边框。
            using var path = CornerRadius > 0f
                ? Easel.Controls.XamlDrawing.RoundedRectangle(bounds, CornerRadius)
                : null;
            var state = graphics.Save();
            try
            {
                if (path is not null)
                {
                    graphics.SetClip(path, CombineMode.Intersect);
                }

                graphics.DrawImage(_snapshot, bounds);
                PaintSnapshotBorder(graphics, bounds);
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        if (IsFrozen)
        {
            return;
        }

        // 祖先滚动了、变换动画推进了：输入代理跟着元素的屏幕矩形走，快照按新尺寸重抓。
        UpdateInputProxy();
        CaptureSnapshot();
    }

    // 手势期间把岛冻结成当前画面：子 HWND 的位置与父窗口这一帧由合成器分别提交，
    // 快速拖动分割条时画面会落后于卡片。冻结后位图走视觉管线，和卡片严格同帧。
    internal bool FreezeForGesture()
    {
        if (IsSnapshot || IsFrozen || _native is null || _native.IsDisposed || !_native.IsHandleCreated)
        {
            return false;
        }

        var size = _native.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return false;
        }

        Bitmap bitmap;
        try
        {
            bitmap = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(_native.PointToScreen(Point.Empty), Point.Empty, size);
        }
        catch (Exception)
        {
            return false;
        }

        _snapshot?.Dispose();
        _snapshot = bitmap;
        IsFrozen = true;
        _native.Visible = false;
        InvalidateVisual();
        return true;
    }

    internal void UnfreezeAfterGesture()
    {
        if (!IsFrozen)
        {
            return;
        }

        IsFrozen = false;
        _snapshot?.Dispose();
        _snapshot = null;
        if (Surface is { } surface && _native is { IsDisposed: false })
        {
            surface.PlaceNative(this, _native, Rectangle.Round(AbsoluteBounds));
        }

        InvalidateVisual();
    }

    /// <summary>Re-captures the bitmap after the native control's content changed.</summary>
    public void RefreshSnapshot()
    {
        _snapshotDirty = true;
        InvalidateArrange();
        InvalidateVisual();
    }

    // 边框画在元素内侧、并与裁切路径同一轮廓：位图自带的原生边框正好被盖住，圆角处也有连续的弧线。
    private void PaintSnapshotBorder(Graphics graphics, RectangleF bounds)
    {
        if (BorderBrush is null)
        {
            return;
        }

        var left = Math.Max(0f, BorderThickness.Left);
        var top = Math.Max(0f, BorderThickness.Top);
        var right = Math.Max(0f, BorderThickness.Right);
        var bottom = Math.Max(0f, BorderThickness.Bottom);
        if (left <= 0f && top <= 0f && right <= 0f && bottom <= 0f)
        {
            return;
        }

        using var ring = Easel.Controls.XamlDrawing.RoundedRectangle(bounds, CornerRadius);
        var inner = new RectangleF(
            bounds.X + left,
            bounds.Y + top,
            bounds.Width - left - right,
            bounds.Height - top - bottom);
        if (inner.Width <= 0f || inner.Height <= 0f)
        {
            BorderBrush.FillPath(graphics, ring);
            return;
        }

        var inset = Math.Max(Math.Max(left, right), Math.Max(top, bottom));
        using var innerPath = Easel.Controls.XamlDrawing.RoundedRectangle(
            inner,
            Math.Max(0f, CornerRadius - inset));
        ring.FillMode = FillMode.Alternate;
        ring.AddPath(innerPath, connect: false);
        BorderBrush.FillPath(graphics, ring);
    }

    private void CaptureSnapshot()
    {
        if (_native is null)
        {
            return;
        }

        var rotated = HasRotatedTransform();
        var inProxy = _inputProxy is { IsDisposed: false } proxy && ReferenceEquals(_native.Parent, proxy);
        int width;
        int height;
        if (inProxy && !rotated)
        {
            // 纯缩放/平移：代理里的控件按变换后的包围盒重排，快照与命中区一致。
            _native.Dock = DockStyle.None;
            width = Math.Max(1, _native.Width);
            height = Math.Max(1, _native.Height);
        }
        else
        {
            // 旋转/斜切：按原始尺寸抓，位图才是“基础布局旋转”，不会被包围盒拉伸。
            var scale = CurrentScale;
            width = Math.Max(1, (int)Math.Ceiling(Bounds.Width * scale));
            height = Math.Max(1, (int)Math.Ceiling(Bounds.Height * scale));
        }

        if (_snapshot is not null && _snapshot.Width == width && _snapshot.Height == height && !_snapshotDirty)
        {
            return;
        }

        IsBeingPlaced = true;
        try
        {
            _native.Anchor = AnchorStyles.None;
            if (!inProxy || rotated)
            {
                _native.Dock = DockStyle.None;
                _native.SetBounds(0, 0, width, height);
            }

            if (!_native.IsHandleCreated)
            {
                _native.CreateControl();
            }

            if (rotated)
            {
                // 抓图要完整矩形；输入用的多边形区域在抓完之后恢复。
                NativeWindowRegion.Restore(_native);
            }

            var bitmap = new Bitmap(width, height);
            _native.DrawToBitmap(bitmap, new Rectangle(0, 0, width, height));
            _snapshot?.Dispose();
            _snapshot = bitmap;
            _snapshotDirty = false;
        }
        catch (Exception)
        {
            // 绘制失败保留上一帧；没有上一帧就留空，不阻断布局。
        }
        finally
        {
            IsBeingPlaced = false;
        }

        IsBeingPlaced = true;
        try
        {
            if (inProxy && _inputProxy is { IsDisposed: false } active)
            {
                _native.Dock = DockStyle.None;
                if (_native.Bounds != active.ClientRectangle)
                {
                    _native.Bounds = active.ClientRectangle;
                }
            }

            ApplyProxyRegion();
        }
        finally
        {
            IsBeingPlaced = false;
        }
    }

    private bool HasRotatedTransform()
    {
        if (Transform.IsIdentity)
        {
            return false;
        }

        var elements = Transform.Elements;
        return Math.Abs(elements[1]) > 0.0001f || Math.Abs(elements[2]) > 0.0001f;
    }

    private void ApplyProxyRegion()
    {
        if (_native is null || _native.IsDisposed)
        {
            return;
        }

        if (_proxyPhysicalBounds is not { } physical || _inputProxy is null
            || !ReferenceEquals(_native.Parent, _inputProxy))
        {
            NativeWindowRegion.Restore(_native);
            return;
        }

        var scale = Surface?.DpiScale ?? 1f;
        if (scale <= 0f)
        {
            scale = 1f;
        }

        if (HasRotatedTransform())
        {
            var corners = new[]
            {
                new PointF(0f, 0f),
                new PointF(Bounds.Width, 0f),
                new PointF(Bounds.Width, Bounds.Height),
                new PointF(0f, Bounds.Height),
            };
            var points = new Point[4];
            for (var index = 0; index < 4; index++)
            {
                var surfacePoint = PointToSurface(corners[index]);
                points[index] = new Point(
                    (int)Math.Round(surfacePoint.X * scale) - physical.X,
                    (int)Math.Round(surfacePoint.Y * scale) - physical.Y);
            }

            NativeWindowRegion.SetPolygon(_native, points);
            NativeWindowRegion.SetPolygon(_inputProxy, points);
            return;
        }

        if (CornerRadius > 0f)
        {
            NativeWindowRegion.Apply(_native, (float)(CornerRadius * scale), []);
            NativeWindowRegion.Apply(_inputProxy, (float)(CornerRadius * scale), []);
            return;
        }

        NativeWindowRegion.Restore(_native);
        NativeWindowRegion.Restore(_inputProxy);
    }

    private void UpdateInputProxy()
    {
        if (!IsSnapshot || _native is null || _native.IsDisposed || !HasVisibleOpacity())
        {
            DisposeInputProxy();
            return;
        }

        var surface = Surface;
        if (surface is not { IsHandleCreated: true } || !IsVisible || Visibility != XamlVisibility.Visible)
        {
            DisposeInputProxy();
            return;
        }

        var owner = surface.FindForm();
        if (owner is not { IsHandleCreated: true, Visible: true } || owner.IsDisposed)
        {
            DisposeInputProxy();
            return;
        }

        if (_inputProxy is null || _inputProxy.IsDisposed || !ReferenceEquals(_inputProxy.Owner, owner))
        {
            DisposeInputProxy();
            _inputProxy = new NativeSnapshotProxy(this, _native);
            _inputProxy.ShowFor(owner);
        }

        if (!ReferenceEquals(_native.Parent, _inputProxy))
        {
            _native.Parent?.Controls.Remove(_native);
            _inputProxy.Controls.Add(_native);
            _native.Visible = true;
        }

        var client = new RectangleF(0f, 0f, surface.LogicalClientSize.Width, surface.LogicalClientSize.Height);
        var bounds = RectangleF.Intersect(AbsoluteBounds, client);
        if (bounds.Width < 1f || bounds.Height < 1f)
        {
            _inputProxy.Visible = false;
            return;
        }

        var scale = surface.DpiScale > 0f ? surface.DpiScale : 1f;
        var physical = new Rectangle(
            (int)Math.Floor(bounds.X * scale),
            (int)Math.Floor(bounds.Y * scale),
            (int)Math.Ceiling(bounds.Width * scale),
            (int)Math.Ceiling(bounds.Height * scale));
        _proxyPhysicalBounds = physical;
        _inputProxy.Place(surface.RectangleToScreen(physical));
        _inputProxy.Visible = true;
        ApplyProxyRegion();
    }

    private bool HasVisibleOpacity()
    {
        var opacity = 1d;
        for (VisualElement? current = this; current is not null; current = current.ParentElement)
        {
            opacity *= current.Opacity;
            if (opacity <= 0d)
            {
                return false;
            }
        }

        return true;
    }

    private void DisposeInputProxy()
    {
        if (_inputProxy is null)
        {
            return;
        }

        var proxy = _inputProxy;
        _inputProxy = null;
        _proxyPhysicalBounds = null;
        if (_native is not null && !_native.IsDisposed)
        {
            NativeWindowRegion.Restore(_native);
            if (proxy.Controls.Contains(_native))
            {
                proxy.Controls.Remove(_native);
            }
        }

        if (!proxy.IsDisposed)
        {
            proxy.Hide();
            proxy.Close();
            proxy.Dispose();
        }
    }

    /// <inheritdoc />
    protected override void OnParentChanged(ContainerVisual? oldParent, ContainerVisual? newParent)
    {
        base.OnParentChanged(oldParent, newParent);

        if (IsSnapshot)
        {
            UpdateInputProxy();
        }

        if (_contentSurface is not null)
        {
            MirrorResources();
            MirrorStyles();
        }
    }

    // 挂到另一个表面时资源链/样式链的最后一站变了，嵌套表面要重挂一次。
    private void OnSurfaceChanged(object? sender, EventArgs e)
    {
        if (IsSnapshot)
        {
            UpdateInputProxy();
        }

        if (_contentSurface is null)
        {
            return;
        }

        MirrorResources();
        MirrorStyles();
        PlaceContentSurface();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeInputProxy();
            SurfaceChanged -= OnSurfaceChanged;
            Surface?.RemoveNative(this);
            DetachNative(_native);
            ReleaseContentSurface();
            _snapshot?.Dispose();
            _snapshot = null;
            if (OwnsNative)
            {
                _native?.Dispose();
            }

            _native = null;
        }

        base.Dispose(disposing);
    }

    private float CurrentScale
    {
        get
        {
            var scale = Surface?.DpiScale ?? 1f;
            return scale > 0f ? scale : 1f;
        }
    }

    private System.Drawing.Size MeasurePreferred(Size availableSize, float scale)
    {
        if (_native is null)
        {
            return System.Drawing.Size.Empty;
        }

        // int.MaxValue 是布局层表达「无约束」的方式；乘以缩放前先挡住，免得 GetPreferredSize 拿到溢出尺寸。
        var proposed = System.Drawing.Size.Empty;
        if (availableSize.Width > 0 && availableSize.Width < int.MaxValue / 2
            && availableSize.Height > 0 && availableSize.Height < int.MaxValue / 2)
        {
            proposed = new System.Drawing.Size(
                Math.Max(1, (int)Math.Ceiling(availableSize.Width * scale)),
                Math.Max(1, (int)Math.Ceiling(availableSize.Height * scale)));
        }

        return _native.GetPreferredSize(proposed);
    }

    private static Size ToDips(System.Drawing.Size size, float scale)
        => new(
            Math.Max(0, (int)Math.Ceiling(size.Width / scale)),
            Math.Max(0, (int)Math.Ceiling(size.Height / scale)));

    private void AttachNative(Control? native)
    {
        if (native is null)
        {
            return;
        }

        _declaredSize = native.Size;
        native.AutoSizeChanged += OnNativePreferredChanged;
        native.TextChanged += OnNativePreferredChanged;
        native.FontChanged += OnNativePreferredChanged;
        native.PaddingChanged += OnNativePreferredChanged;
        native.RightToLeftChanged += OnNativePreferredChanged;
        native.SizeChanged += OnNativeSizeChanged;
        native.Layout += OnNativeLayout;
        native.HandleCreated += OnNativeLayout;
        native.BackColorChanged += OnNativeBackColorChanged;
        native.Enter += OnNativeEnter;
        native.Leave += OnNativeLeave;
        native.Enabled = IsEnabled;
        AttachTwoWay(native);
    }

    private void DetachNative(Control? native)
    {
        ClearTwoWaySubscriptions();
        if (native is null)
        {
            return;
        }

        native.AutoSizeChanged -= OnNativePreferredChanged;
        native.TextChanged -= OnNativePreferredChanged;
        native.FontChanged -= OnNativePreferredChanged;
        native.PaddingChanged -= OnNativePreferredChanged;
        native.RightToLeftChanged -= OnNativePreferredChanged;
        native.SizeChanged -= OnNativeSizeChanged;
        native.Layout -= OnNativeLayout;
        native.HandleCreated -= OnNativeLayout;
        native.BackColorChanged -= OnNativeBackColorChanged;
        native.Enter -= OnNativeEnter;
        native.Leave -= OnNativeLeave;
    }

    /// <summary>Pushes a native property's current value into the two-way binding registered on this wrapper.</summary>
    /// <param name="propertyName">The native property name, for example <c>nameof(TextBox.Text)</c>.</param>
    /// <remarks>
    /// Use it from a control that exposes neither <see cref="System.ComponentModel.INotifyPropertyChanged"/> nor a
    /// conventional <c>XxxChanged</c> event; the value reaches the proxy dependency property and the binding updates
    /// its source. Controls that expose those notifications do not need the call.
    /// </remarks>
    public void NotifyNativePropertyChanged(string propertyName)
    {
        if (_native is null)
        {
            return;
        }

        NativePropertyBinding.NotifyChanged(_native, propertyName);
    }

    internal void TrackTwoWay(string propertyName, DependencyProperty property)
    {
        if (_twoWayBindings.Exists(binding => ReferenceEquals(binding.Property, property)))
        {
            return;
        }

        _twoWayBindings.Add((propertyName, property));
        AttachTwoWay(_native);
    }

    private void AttachTwoWay(Control? native)
    {
        ClearTwoWaySubscriptions();
        if (native is null)
        {
            return;
        }

        foreach (var (propertyName, property) in _twoWayBindings)
        {
            var subscription = NativePropertyBinding.Subscribe(this, native, propertyName, property);
            if (subscription is not null)
            {
                (_twoWaySubscriptions ??= []).Add(subscription);
            }
        }
    }

    private void ClearTwoWaySubscriptions()
    {
        if (_twoWaySubscriptions is null)
        {
            return;
        }

        foreach (var subscription in _twoWaySubscriptions)
        {
            subscription.Dispose();
        }

        _twoWaySubscriptions.Clear();
    }

    private void OnNativePreferredChanged(object? sender, EventArgs e)
    {
        if (IsSnapshot)
        {
            _snapshotDirty = true;
        }

        if (!_placing)
        {
            InvalidateMeasure();
        }
    }

    private void OnNativeSizeChanged(object? sender, EventArgs e)
    {
        // 摆放本身会改 Size；消费方显式改 Size 时才是新的「声明尺寸」。
        if (_placing || IsBeingPlaced || _native is not { AutoSize: false } native)
        {
            return;
        }

        if (native.Size == _declaredSize)
        {
            return;
        }

        _declaredSize = native.Size;
        InvalidateMeasure();
    }

    private void OnNativeLayout(object? sender, EventArgs e)
    {
        if (_placing || IsBeingPlaced)
        {
            return;
        }

        PlaceContentSurface();
        // 子 HWND 的句柄可能晚于第一次摆放才创建；补一次窗口区域。
        Surface?.RefreshNativeRegions();
        if (IsSnapshot)
        {
            RefreshSnapshot();
        }
    }

    private void OnNativeBackColorChanged(object? sender, EventArgs e)
    {
        if (_contentSurface is not null && _native is not null)
        {
            _contentSurface.BackColor = _native.BackColor;
        }
    }

    // 原生控件的 Enter/Leave 就是包装元素的逻辑焦点；绑定引擎的 LostFocus 触发器随之触发。
    private void OnNativeEnter(object? sender, EventArgs e) => SetFocused(true);

    private void OnNativeLeave(object? sender, EventArgs e) => RaiseLostFocus();

    private void EnsureContentSurface()
    {
        if (_contentSurface is not null)
        {
            return;
        }

        _contentRoot = new ContentControl();
        _contentSurface = new NativeContentHost
        {
            Root = _contentRoot,
            BackColor = _native?.BackColor ?? Theme.Current.ControlBackground,
            DpiScaleOverride = Surface?.DpiScaleOverride ?? 0f,
        };

        BridgeContentContext();
        MirrorResources();
        MirrorStyles();
    }

    private void ReleaseContentSurface()
    {
        if (_contentSurface is null)
        {
            return;
        }

        _contentSurface.Parent?.Controls.Remove(_contentSurface);
        _contentSurface.Dispose();
        _contentSurface = null;
        _contentRoot = null;
        ClearStyleSubscriptions();
    }

    private void PlaceContentSurface()
    {
        if (_contentSurface is null || _native is null)
        {
            return;
        }

        if (!IsVisible || Visibility != XamlVisibility.Visible)
        {
            _contentSurface.Visible = false;
            return;
        }

        if (!ReferenceEquals(_contentSurface.Parent, _native))
        {
            _contentSurface.Parent?.Controls.Remove(_contentSurface);
            _native.Controls.Add(_contentSurface);
        }

        var surface = Surface;
        if (surface is not null)
        {
            _contentSurface.DpiScaleOverride = surface.DpiScaleOverride;
        }

        var area = _native.DisplayRectangle;
        if (_contentSurface.Bounds != area)
        {
            _contentSurface.Bounds = area;
        }

        _contentSurface.Visible = true;
    }

    private void BridgeContentContext()
    {
        if (_contentRoot is null)
        {
            return;
        }

        _contentRoot.DataContext = DataContext;
        _contentRoot.IsEnabled = IsEnabled;
        ApplyInheritedTypography();
    }

    private void ApplyInheritedTypography()
    {
        if (_contentRoot is null)
        {
            return;
        }

        var fontSize = FontSize;
        if (!double.IsNaN(fontSize) && fontSize > 0d)
        {
            _contentRoot.FontSize = fontSize;
        }

        var fontFamily = FontFamily;
        if (!string.IsNullOrEmpty(fontFamily))
        {
            _contentRoot.FontFamily = fontFamily;
        }

        _contentRoot.FontWeight = FontWeight;
    }

    private void MirrorResources()
    {
        if (_contentSurface is null)
        {
            return;
        }

        var surface = Surface;
        var dictionaries = new List<ResourceDictionary>();
        for (VisualElement? element = this; element is not null; element = element.ParentElement)
        {
            dictionaries.Add(element.Resources);
        }

        if (surface is not null)
        {
            dictionaries.Add(surface.Resources);
        }

        var merged = _contentSurface.Resources.MergedDictionaries;
        merged.Clear();
        // 近的负责覆盖远的；合并表按「后加先查」，所以从最远的宿主开始加。
        for (var index = dictionaries.Count - 1; index >= 0; index--)
        {
            merged.Add(dictionaries[index]);
        }
    }

    private void MirrorStyles()
    {
        if (_contentSurface is null)
        {
            return;
        }

        var surface = Surface;
        var target = _contentSurface.Styles;
        ClearStyleSubscriptions();
        target.Clear();

        // 近的优先：本元素、祖先、最后是宿主。Collection 的声明顺序就是优先级。
        for (VisualElement? element = this; element is not null; element = element.ParentElement)
        {
            CopyStyles(element.Styles, target);
            SubscribeStyles(element.Styles);
        }

        if (surface is not null)
        {
            CopyStyles(surface.Styles, target);
            SubscribeStyles(surface.Styles);
        }
    }

    private static void CopyStyles(VisualStyleCollection source, VisualStyleCollection target)
    {
        foreach (var style in source)
        {
            target.Add(style);
        }
    }

    private void SubscribeStyles(VisualStyleCollection collection)
    {
        void Handler(object? sender, EventArgs e)
        {
            // 源样式表变了：重放一遍合并表。样式是描述对象，可以同时挂在两条树上。
            MirrorStylesFromSource();
        }

        collection.Changed += Handler;
        _styleSubscriptions.Add((collection, Handler));
    }

    private void MirrorStylesFromSource()
    {
        if (_contentSurface is null)
        {
            return;
        }

        var target = _contentSurface.Styles;
        target.Clear();

        for (VisualElement? element = this; element is not null; element = element.ParentElement)
        {
            CopyStyles(element.Styles, target);
        }

        if (Surface is { } surface)
        {
            CopyStyles(surface.Styles, target);
        }
    }

    private void ClearStyleSubscriptions()
    {
        foreach (var (collection, handler) in _styleSubscriptions)
        {
            collection.Changed -= handler;
        }

        _styleSubscriptions.Clear();
    }

    // 覆盖层盖住原生内容区；没有视觉元素命中的位置把点击还给下面的原生控件。
    private sealed class NativeContentHost : VisualHost
    {
        private const int WmNcHitTest = 0x0084;
        private const int HtTransparent = -1;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmNcHitTest && IsHandleCreated)
            {
                var value = m.LParam.ToInt64();
                var screen = new Point(
                    (short)(value & 0xFFFF),
                    (short)((value >> 16) & 0xFFFF));
                var client = PointToClient(screen);
                var scale = DpiScale > 0f ? DpiScale : 1f;
                if (FindElementAt(new PointF(client.X / scale, client.Y / scale)) is null)
                {
                    m.Result = (IntPtr)HtTransparent;
                    return;
                }
            }

            base.WndProc(ref m);
        }
    }
}

/// <summary>An element that creates and owns a native <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The native control type declared in XAML.</typeparam>
public class NativeElement<T> : NativeElement
    where T : Control, new()
{
    /// <summary>Creates the native control and takes ownership of it.</summary>
    public NativeElement()
        : base(new T(), ownsNative: true)
    {
    }

    /// <summary>The native control, already typed.</summary>
    public new T Native => (T)base.Native!;
}
