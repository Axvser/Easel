using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Easel.Visuals.Controls;
using XamlVisibility = Easel.Controls.Visibility;

namespace Easel.Visuals;

/// <summary>
/// The single window that hosts a tree of <see cref="VisualElement"/> objects (or a plain <see cref="ContainerVisual"/>
/// tree) and routes all input to the visual elements it contains.
/// </summary>
/// <remarks>
/// <para>
/// Exactly one HWND is created for the whole tree. Elements are painted in one pass into the double buffer, so a
/// structural change cannot show half of an old and half of a new tree. A native WinForms control stays an HWND
/// island through <see cref="NativeElement"/>; it participates in layout without joining this paint pass.
/// </para>
/// <para>
/// Focus is logical: the surface keeps the native keyboard focus and <see cref="FocusedElement"/> receives key and
/// text input. Pointer events hit-test the visual tree, capture through <see cref="VisualElement"/> and bubble.
/// </para>
/// </remarks>
public class VisualHost : System.Windows.Forms.Control
{

    private ContainerVisual? _root;
    private VisualElement? _rootElement;
    private object? _dataContextSnapshot;
    private VisualElement? _focused;
    private VisualElement? _capture;
    private bool _focusVisible = true;
    private readonly List<VisualElement> _hoverChain = [];
    private readonly Dictionary<NativeElement, System.Windows.Forms.Control> _nativeChildren = [];
    private readonly Dictionary<NativeElement, int> _nativeTreeOrder = [];
    private readonly HashSet<NativeElement> _placedNative = [];
    private readonly HashSet<NativeElement> _autoNativeChildren = [];
    private readonly HashSet<NativeElement> _snapshotProxies = [];
    private readonly Dictionary<NativeElement, (System.Windows.Forms.Control Child, Rectangle Physical, bool Show, RectangleF Visible)> _pendingPlacements = [];
    private readonly List<NativeElement> _frozenNatives = [];
    private bool _rendering;
    private readonly Dictionary<System.Windows.Forms.Control, (int Width, int Height, float Radius, Rectangle? Clip, Rectangle[] Cuts)> _nativeRegionStates = [];
    private int _nativeOrderCounter;
    private int _nativeSuppressionCount;
    private readonly VisualStyleCollection _styles = new();
    private readonly ResourceDictionary _resources = new();
    private readonly ResourceDictionary _themeResources = new();
    private readonly OverlayLayer _overlay = new();
    private readonly List<VisualPopup> _openPopups = [];
    private readonly List<Dialogs.VisualDialog> _openDialogs = [];
    private Rectangle _damage;
    private bool _damageFull = true;
    private MouseButtons _buttons;
    private bool _layoutValid;
    private bool _layoutInProgress;
    private bool _paintingFullDamage;
    private Size _layoutSize;

    // 每趟完整布局递增；AddNative 的岛用它判断宿主这一趟有没有亲自排过自己。
    internal int LayoutPass { get; private set; }

    // 每开始一趟布局递增。Grid 用它区分「同一趟里被父级第二遍收紧」与「下一趟换了约束」。
    internal static int LayoutGeneration { get; private set; }
    private float _dpiScale;
    private bool _dpiScaleValid;
    private float _dpiScaleOverride;
    private Bitmap? _motionBitmap;
    private float _motionBitmapScale;
    private VisualElement? _dragTarget;
    private VisualDragGhost? _dragGhost;
    private IDataObject? _dragData;
    private DragDropEffects _dragAllowed;
    private DragDropEffects _dragEffects;
    private Keys _dragModifiers;
    private PointF _dragPoint;
    private bool _renderingMotion;
    private bool _gestureDrewMotionFrame;

    // RenderTo 的整帧缓存。只在根为 VisualElement、目标为恒等世界变换时启用。
    private Bitmap? _frameCache;
    private Graphics? _frameCacheGraphics;
    private Bitmap? _frameScratch;
    private Graphics? _frameScratchGraphics;
    private bool _frameCacheValid;
    private bool _frameCacheStale;
    private RectangleF _frameDamage;
    private bool _frameDamageFull = true;
    private bool _framePanPending;
    private float _framePanDx;
    private float _framePanDy;
    private int _frameContentStamp;
    private int _framePanStamp;
    private readonly List<VisualElement> _frameMovedVisuals = [];
    private SolidBrush? _frameClearBrush;

    /// <summary>Creates the surface with the double-buffered paint style it needs.</summary>
    public VisualHost()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor |
            ControlStyles.Selectable,
            true);
        DoubleBuffered = true;
        TabStop = true;
        // OLE 拖放只能在 STA 线程注册；MTA 线程上的宿主没有原生拖放，视觉树本身仍然可用。
        if (System.Threading.Thread.CurrentThread.GetApartmentState() == System.Threading.ApartmentState.STA)
        {
            AllowDrop = true;
        }

        Root = new ContainerVisual();
        // .NET 8 的 Control 自带 DataContext/DataContextChanged；旧目标框架在这里补上同名成员，
        // 下面这条订阅在四个 TFM 上走同一个入口。
        DataContextChanged += OnHostDataContextChanged;
        _dataContextSnapshot = DataContext;
        _styles.Changed += (_, _) => RefreshStyles();
        _overlay.SetSurfaceRoot(this);
        _resources.MergedDictionaries.Add(_themeResources);
        RefreshThemeResources();
        Theme.Changed += OnThemeChanged;
    }

    /// <summary>Raised after the root tree has been measured and arranged.</summary>
    public event EventHandler? LayoutUpdated;

    /// <summary>The <see cref="System.Diagnostics.Stopwatch"/> timestamp of the last completed paint.</summary>
    public long LastPaintTimestamp { get; private set; }

    /// <summary>The number of completed paints; animation drivers use it to tell whether a drag already advanced them.</summary>
    public long PaintCount { get; private set; }

    /// <summary>Whether the last completed paint was requested by an animation tick rather than input.</summary>
    public bool LastPaintWasClockDriven { get; private set; }

    private bool nextPaintClockDriven;

    /// <summary>Marks the next paint as an animation tick, so a drag paint and a clock paint can be told apart.</summary>
    public void MarkNextPaintAsClockDriven() => nextPaintClockDriven = true;

    /// <summary>Runs <paramref name="action"/> on the control's UI thread.</summary>
    /// <param name="action">The action to run.</param>
    /// <remarks>
    /// Use this to marshal work back to the host after an asynchronous operation. When the control has a window
    /// handle the action is posted with <see cref="System.Windows.Forms.Control.BeginInvoke(Delegate)"/>; otherwise
    /// it runs synchronously on the calling thread so headless hosts and tests still observe it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public void Post(Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        if (IsHandleCreated && !IsDisposed)
        {
            BeginInvoke(action);
            return;
        }

        action();
    }

    /// <summary>
    /// The render scale used for frames painted while a pointer gesture is in progress; <c>1</c> disables it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// While <see cref="IsPointerGestureActive"/> is <see langword="true"/> and this value is below <c>1</c>, the
    /// client area is painted into a reusable offscreen bitmap at this fraction of its logical resolution and then
    /// stretched back to full size. Motion frames keep the flow animation ticking; the next frame after the gesture
    /// ends is painted at full resolution.
    /// </para>
    /// <para>
    /// Only the window paint path is affected: <see cref="RenderTo"/> always renders at full resolution. Values below
    /// <c>0.1</c> are treated as <c>0.1</c>. The default is <c>1</c>.
    /// </para>
    /// </remarks>
    public float MotionScale { get; set; } = 1f;

    /// <summary>Whether a pointer press is still waiting for its release.</summary>
    /// <remarks>
    /// Set by <see cref="ProcessPointerDown"/>, cleared by <see cref="ProcessPointerUp"/> and when the native mouse
    /// capture is lost. A synthesized press without a window handle stays active until an explicit release.
    /// </remarks>
    public bool IsPointerGestureActive { get; private set; }

    /// <summary>
    /// Whether live native islands freeze to the current frame while a pointer gesture is active.
    /// </summary>
    /// <remarks>
    /// A child HWND's position and the parent's frame are committed by the compositor independently, so a fast
    /// drag can leave the island's picture trailing its card. Freezing swaps the island for a bitmap that moves
    /// with the visual tree and restores the live control when the pointer is released. Defaults to
    /// <see langword="true"/>; turn it off when the island must stay interactive during a drag.
    /// </remarks>
    public bool FreezeNativeIslandsDuringGestures { get; set; } = true;

    /// <summary>Styles applied implicitly to matching elements in this surface's visual tree.</summary>
    /// <remarks>
    /// An element's own <see cref="VisualElement.Styles"/> collection and its ancestors' collections take precedence
    /// over this one; <see cref="EaselApplication.Styles"/> follows it, and an explicit
    /// <see cref="VisualElement.Style"/> takes precedence over all of them. The tree is re-resolved whenever this
    /// collection changes or the root is assigned.
    /// </remarks>
    public VisualStyleCollection Styles => _styles;

    /// <summary>Resources offered to every element in this surface's tree after its own and its ancestors' dictionaries.</summary>
    public ResourceDictionary Resources => _resources;

#if !NET8_0_OR_GREATER
    private object? _dataContext;

    /// <summary>The data context inherited by the visual tree rooted at <see cref="Root"/>.</summary>
    /// <remarks>
    /// On .NET 8 and later this is <see cref="System.Windows.Forms.Control.DataContext"/>. The older target
    /// frameworks expose the same property, event and get/set behaviour here, so XAML and bindings are portable
    /// across every target framework.
    /// </remarks>
    public object? DataContext
    {
        get => _dataContext;
        set
        {
            if (ReferenceEquals(_dataContext, value))
            {
                return;
            }

            _dataContext = value;
            DataContextChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Raised when <see cref="DataContext"/> changes.</summary>
    public event EventHandler? DataContextChanged;
#endif

    /// <summary>The root visual painted by this host; a <see cref="VisualElement"/> also receives layout and input.</summary>
    public ContainerVisual? Root
    {
        get => _root;
        set
        {
            if (ReferenceEquals(_root, value))
            {
                return;
            }

            _focused?.SetFocused(false);
            _focused = null;
            _capture = null;
            _hoverChain.Clear();
            CloseAllDialogs();
            CloseAllPopups();
            ClearNativeChildren();
            _rootElement?.SetSurfaceRoot(null);
            _rootElement?.RefreshImplicitStyles();
            _root = value;
            _rootElement = value as VisualElement;
            // 换根前先记下它当时有效的上下文；挂上本宿主后再补一次 inherited 通知。
            var inheritedBeforeHosting = _rootElement?.GetValue(VisualElement.DataContextProperty);
            _rootElement?.SetSurfaceRoot(this);
            RefreshStyles();
            _rootElement?.RaiseHostDataContextChanged(inheritedBeforeHosting, DataContext);
            InvalidateSurface();
        }
    }

    /// <summary>The element that currently receives keyboard and text input.</summary>
    public VisualElement? FocusedElement => _focused;

    /// <summary>Whether the surface currently has the native keyboard focus.</summary>
    public bool HasKeyboardFocus => Focused;

    /// <summary>The physical pixels per logical unit; 1 while the control has no handle.</summary>
    /// <remarks>
    /// Layout, input and painting all use logical device-independent pixels (DIPs). The control scales the logical
    /// tree by <see cref="DpiScale"/> when it paints, so element sizes and authored fonts stay identical at every
    /// monitor scale. Tests can pin a scale with <see cref="DpiScaleOverride"/>.
    /// </remarks>
    public float DpiScale
    {
        get
        {
            if (_dpiScaleOverride > 0f)
            {
                return _dpiScaleOverride;
            }

            if (!IsHandleCreated)
            {
                return 1f;
            }

            if (_dpiScaleValid)
            {
                return _dpiScale;
            }

            _dpiScale = DeviceDpiValue / 96f;
            _dpiScaleValid = true;
            return _dpiScale;
        }
    }

    /// <summary>The client area in logical units: <see cref="Control.ClientSize"/> divided by <see cref="DpiScale"/>.</summary>
    public Size LogicalClientSize
    {
        get
        {
            var scale = DpiScale;
            if (scale <= 0f)
            {
                scale = 1f;
            }

            return new Size(
                Math.Max(0, (int)Math.Round(ClientSize.Width / scale)),
                Math.Max(0, (int)Math.Round(ClientSize.Height / scale)));
        }
    }

    /// <summary>Pins <see cref="DpiScale"/> instead of deriving it from the device; 0 restores automatic scaling.</summary>
    /// <remarks>
    /// Intended for tests and for hosts that preview a different monitor scale. It does not change the control's
    /// actual DPI awareness; it only tells layout, painting and input which scale to assume.
    /// </remarks>
    public float DpiScaleOverride
    {
        get => _dpiScaleOverride;
        set
        {
            var normalized = Math.Max(0f, value);
            if (Math.Abs(_dpiScaleOverride - normalized) < 0.0001f)
            {
                return;
            }

            _dpiScaleOverride = normalized;
            _dpiScaleValid = false;
            InvalidateSurface();
        }
    }

    private int DeviceDpiValue
    {
        get
        {
#if NETFRAMEWORK
            using var graphics = Graphics.FromHwnd(Handle);
            return (int)Math.Round(graphics.DpiX);
#else
            return DeviceDpi;
#endif
        }
    }

    /// <inheritdoc />
    protected override CreateParams CreateParams
    {
        get
        {
            // 子 HWND 是独立表面，父窗口再怎么画也不会盖住它；不排除子窗口，父窗口才能在同一帧
            // 把岛腾出的旧位置一起补画，快速拖动时不会留下画面残条。
            return base.CreateParams;
        }
    }

    /// <summary>Requests a layout pass and a repaint of the whole surface.</summary>
    public void InvalidateSurface()
    {
        _layoutValid = false;
        _damageFull = true;
        MarkFrameFull();
        Invalidate();
    }

    internal void InvalidatePaint()
    {
        // 排版过程中的重画请求（滚动条在布局里改 ViewportSize/LargeChange 就会走这里）：
        // 这一帧本来就是整面绘制，而且绘制在排版之后才发生，不需要再排一帧。
        if (_layoutInProgress && _paintingFullDamage)
        {
            return;
        }

        _damageFull = true;
        MarkFrameFull();
        Invalidate();
    }

    internal void InvalidatePaint(RectangleF damage, VisualElement? source = null)
    {
        RecordFrameDamage(damage, source);

        if (_damageFull)
        {
            return;
        }

        // 脏区以逻辑单位传入，Invalidate 却按物理客户区像素工作。
        var rectangle = ToPhysical(Rectangle.Round(damage));
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return;
        }

        _damage = _damage.Width == 0 ? rectangle : Rectangle.Union(_damage, rectangle);
        Invalidate(rectangle);
    }

    // 帧缓存脏区以逻辑单位累积；整帧失效后不再需要局部脏区。
    private void MarkFrameFull()
    {
        _frameDamageFull = true;
        _frameDamage = RectangleF.Empty;
        _framePanPending = false;
        _framePanDx = 0f;
        _framePanDy = 0f;
    }

    private void RecordFrameDamage(RectangleF damage, VisualElement? source = null)
    {
        if (_frameDamageFull || damage.Width <= 0f || damage.Height <= 0f)
        {
            return;
        }

        // 已记录整树平移后再出现普通脏区，一次位移解释不了这一帧，退回整帧。
        if (_framePanPending)
        {
            // 设置变换后宿主会显式调用根节点的 InvalidateVisual 并送来整块根脏区；只有普通根的根自身失效
            // 才能确定这是变换的附带结果。根类型自己绘制内容、或脏区来自子树时，缓存帧已经过期。
            if (_frameContentStamp == _framePanStamp && IsTransformOnlyDamage(source, damage))
            {
                return;
            }

            MarkFrameFull();
            return;
        }

        _frameDamage = _frameDamage.Width <= 0f ? damage : RectangleF.Union(_frameDamage, damage);
    }

    // 只有根元素自身、且其类型不绘制自定义内容时，覆盖整块包围盒的失效才只可能来自变换本身。
    private bool IsTransformOnlyDamage(VisualElement? source, RectangleF damage)
    {
        if (source is null || !ReferenceEquals(source, _rootElement) || HasCustomContentRender(source.GetType()))
        {
            return false;
        }

        if (_rootElement is not { Background: null, BorderBrush: null } root)
        {
            return false;
        }

        var bounds = root.AbsoluteBounds;
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return false;
        }

        bounds.Inflate(3f, 3f);
        return bounds.Contains(damage);
    }

    // 逐类型缓存「是否覆写了内容绘制」；普通 Canvas 直到 VisualElement 都没有覆写，仍可走平移快路径。
    private static readonly ConditionalWeakTable<Type, StrongBox<bool>> CustomContentRenderTypes = new();

    private static bool HasCustomContentRender(Type type)
    {
        if (CustomContentRenderTypes.TryGetValue(type, out var cached))
        {
            return cached.Value;
        }

        var custom = false;
        for (var current = type; current is not null && current != typeof(VisualElement); current = current.BaseType)
        {
#pragma warning disable IL2070 // 运行时的具体类型只能反射读取；Easel 不作为裁剪发布的目标。
            foreach (var method in current.GetMethods(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
#pragma warning restore IL2070
            {
                if (method.Name is "RenderContent" or "OnRender")
                {
                    custom = true;
                    break;
                }
            }

            if (custom)
            {
                break;
            }
        }

        // 结果按类型幂等；并发的重复计算只会被 GetValue 丢弃。
        var box = new StrongBox<bool>(custom);
        return CustomContentRenderTypes.GetValue(type, _ => box).Value;
    }

    // 根节点的恒等线性部分只改了位移：整帧可以按像素平移复用。
    internal void RecordRootTranslation(float dx, float dy)
    {
        // 窗口路径没有帧缓存，照旧整面重画。
        _damageFull = true;
        Invalidate();

        if (_frameDamageFull || _frameDamage.Width > 0f || _frameDamage.Height > 0f)
        {
            MarkFrameFull();
            return;
        }

        _framePanPending = true;
        _framePanDx += dx;
        _framePanDy += dy;
        _framePanStamp = _frameContentStamp;
    }

    // 根节点自身的属性变化会整块失效；记录一笔，后续的整块脏区不能再当成平移的附带失效。
    internal void NotifyRootContentChanged() => _frameContentStamp++;

    private readonly List<VisualElement> _dirtyLayoutBoundaries = [];

    // 布局边界内部的失效只重排边界子树；父级槽位不变，整窗仍重画。
    internal void NotifyBoundaryLayoutInvalidated(VisualElement boundary, bool frameFull)
    {
        if (boundary.Surface == this && !_dirtyLayoutBoundaries.Contains(boundary))
        {
            _dirtyLayoutBoundaries.Add(boundary);
        }

        _damageFull = true;
        if (frameFull)
        {
            MarkFrameFull();
        }

        Invalidate();
    }

    // 布局失效：窗口路径整面重画；frameFull 决定帧缓存是否也必须整帧重来。
    internal void NotifyArrangeInvalidated(bool frameFull)
    {
        _layoutValid = false;
        _damageFull = true;
        if (frameFull)
        {
            MarkFrameFull();
        }

        Invalidate();
    }

    // 画布坐标变更只移动一项：先记旧位置，排版完成后由 FlushMovedVisuals 补记新位置。
    internal void RecordLocalMove(VisualElement element, RectangleF oldDamage)
    {
        RecordFrameDamage(oldDamage);
        if (_frameDamageFull)
        {
            return;
        }

        // 未调用 RenderTo 的宿主不会清空列表；超过上限直接退回整帧，避免列表无限增长。
        if (_frameMovedVisuals.Count >= 64)
        {
            MarkFrameFull();
            return;
        }

        _frameMovedVisuals.Add(element);
    }

    private void FlushMovedVisuals()
    {
        foreach (var element in _frameMovedVisuals)
        {
            if (element.Surface == this && element.TryCollectMovementDamage(out var damage))
            {
                RecordFrameDamage(damage);
            }
            else
            {
                MarkFrameFull();
            }
        }

        _frameMovedVisuals.Clear();
    }

    /// <inheritdoc />
    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        _damageFull = true;
        Invalidate();
    }

    /// <summary>Renders the tree into <paramref name="graphics"/> at <paramref name="size"/>.</summary>
    /// <param name="graphics">The target surface.</param>
    /// <param name="size">The logical size of the surface.</param>
    /// <remarks>
    /// This is the deterministic test and export path: <paramref name="size"/> and every coordinate are logical,
    /// and no DPI transform is applied even when the control itself runs at a non-96 DPI. Use
    /// <see cref="RenderPhysical"/> to paint a real client area at the current <see cref="DpiScale"/>.
    /// </remarks>
    public void RenderTo(Graphics graphics, Size size)
    {
        if (graphics is null) throw new ArgumentNullException(nameof(graphics));
        if (!TryRenderCachedFrame(graphics, size))
        {
            RenderCore(graphics, size, new Rectangle(0, 0, size.Width, size.Height));
        }
    }

    // 窗口路径不使用帧缓存；先失效，下一次 RenderTo 才会以整帧重建，避免贴出旧画面。
    private void InvalidateFrameCache()
    {
        _frameCacheValid = false;
        _frameCacheStale = false;
        _frameMovedVisuals.Clear();
        MarkFrameFull();
    }

    // 帧缓存只用于逻辑坐标下的导出路径：根必须是带属性通知的 VisualElement，目标不能带自定义世界变换。
    private bool TryRenderCachedFrame(Graphics target, Size size)
    {
        if (_root is null || _rootElement is null || size.Width <= 0 || size.Height <= 0)
        {
            return false;
        }

        if (!target.Transform.IsIdentity)
        {
            InvalidateFrameCache();
            return false;
        }

        // 排版会移动节点；Arrange 之后才拿到新包围盒，必须先跑完再决定重画区域。
        EnsureLayout(size);
        FlushMovedVisuals();

        var sizeMatches = _frameCache is not null
            && _frameCache.Width == size.Width
            && _frameCache.Height == size.Height;

        // 过期缓存上没有局部脏区时重画整帧比补建缓存再贴图更便宜，输出也与上一次直画一致。
        var hasDamage = _frameDamage.Width > 0f
                        || _frameDamage.Height > 0f
                        || _framePanPending;

        if (_frameDamageFull || (_frameCacheStale && !hasDamage))
        {
            // 整帧重画直接落到目标，不再经缓存再整幅贴回：本帧无论如何都要重画整棵树，
            // 而 PArgb 缓存到调用方位图的全尺寸贴图常常和整棵树一样贵。缓存内容与
            // 目标不再一致，标为过期；后续局部脏区帧会先补建一次缓存，动画增量贴图不受影响。
            RenderCore(target, size, new Rectangle(0, 0, size.Width, size.Height));
            _frameCacheStale = sizeMatches;
            ResetFrameDamage();
            return true;
        }

        if (!sizeMatches || !_frameCacheValid || _frameCacheStale)
        {
            var full = new RectangleF(0f, 0f, size.Width, size.Height);
            var graphics = EnsureFrameCache(size);
            ClearFrameRegion(graphics, full, wholeSurface: true);
            RenderRootInto(graphics, size, full, cull: false);
            _frameCacheValid = true;
            _frameCacheStale = false;
            ResetFrameDamage();
        }
        else if (!TryShiftFrame(size))
        {
            // 没有整树平移就只重画累积的局部脏区；脏区为空即静态帧，缓存已是最新。
            RenderFrameDamage(size);
        }

        AdvancePaintClock();
        target.DrawImageUnscaled(_frameCache!, 0, 0);
        return true;
    }

    private Graphics EnsureFrameCache(Size size)
    {
        if (_frameCache is null
            || _frameCache.Width != size.Width
            || _frameCache.Height != size.Height)
        {
            _frameCacheGraphics?.Dispose();
            _frameCache?.Dispose();
            _frameScratchGraphics?.Dispose();
            _frameScratch?.Dispose();
            _frameCache = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            _frameCacheGraphics = Graphics.FromImage(_frameCache);
            _frameScratch = null;
            _frameScratchGraphics = null;
            _frameCacheValid = false;
            MarkFrameFull();
        }

        return _frameCacheGraphics!;
    }

    // 把旧帧按整像素平移画进后备位图，再重画暴露的条带，最后交换两帧。
    private bool TryShiftFrame(Size size)
    {
        if (!_framePanPending || _frameCache is null || _frameCacheGraphics is null)
        {
            return false;
        }

        // 覆盖层不受根变换影响，有内容时不能跟着一起平移。
        if (_overlay.Children.Count > 0 || _dragGhost is not null)
        {
            return false;
        }

        var dx = _framePanDx;
        var dy = _framePanDy;
        if (Math.Abs(dx - Math.Round(dx)) > 0.001f || Math.Abs(dy - Math.Round(dy)) > 0.001f)
        {
            return false;
        }

        var sx = (int)Math.Round(dx);
        var sy = (int)Math.Round(dy);
        if (Math.Abs(sx) >= size.Width || Math.Abs(sy) >= size.Height)
        {
            return false;
        }

        if (sx == 0 && sy == 0)
        {
            ResetFrameDamage();
            return true;
        }

        if (_frameScratch is null || _frameScratch.Width != size.Width || _frameScratch.Height != size.Height)
        {
            _frameScratchGraphics?.Dispose();
            _frameScratch?.Dispose();
            _frameScratch = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            _frameScratchGraphics = Graphics.FromImage(_frameScratch);
        }

        var back = _frameCache;
        var front = _frameScratch!;
        var frontGraphics = _frameScratchGraphics!;
        frontGraphics.Clear(Color.Transparent);
        frontGraphics.DrawImageUnscaled(back, sx, sy);

        if (sx > 0)
        {
            RenderStrip(frontGraphics, size, new RectangleF(0f, 0f, sx, size.Height));
        }
        else if (sx < 0)
        {
            RenderStrip(frontGraphics, size, new RectangleF(size.Width + sx, 0f, -sx, size.Height));
        }

        // 横向条带必须避开纵向条带已画的列；叠在一起会让半透明/抗锯齿像素二次混合。
        var horizontalLeft = sx > 0 ? sx : 0;
        var horizontalWidth = size.Width - Math.Abs(sx);
        if (sy > 0)
        {
            RenderStrip(frontGraphics, size, new RectangleF(horizontalLeft, 0f, horizontalWidth, sy));
        }
        else if (sy < 0)
        {
            RenderStrip(frontGraphics, size, new RectangleF(horizontalLeft, size.Height + sy, horizontalWidth, -sy));
        }

        (_frameCache, _frameScratch) = (_frameScratch, _frameCache);
        (_frameCacheGraphics, _frameScratchGraphics) = (_frameScratchGraphics, _frameCacheGraphics);
        ResetFrameDamage();
        return true;
    }

    private void RenderStrip(Graphics graphics, Size size, RectangleF strip)
    {
        var state = graphics.Save();
        try
        {
            graphics.SetClip(strip);
            RenderRootInto(graphics, size, strip, cull: true);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    // 把累积的局部脏区从缓存里清成透明，再只重画这一块。
    private bool RenderFrameDamage(Size size)
    {
        if (_frameDamage.Width <= 0f || _frameDamage.Height <= 0f || _frameCacheGraphics is null)
        {
            return false;
        }

        var surface = new RectangleF(0f, 0f, size.Width, size.Height);
        var damage = RectangleF.Intersect(RectangleF.FromLTRB(
            (float)Math.Floor(_frameDamage.Left),
            (float)Math.Floor(_frameDamage.Top),
            (float)Math.Ceiling(_frameDamage.Right),
            (float)Math.Ceiling(_frameDamage.Bottom)), surface);
        if (damage.Width <= 0f || damage.Height <= 0f)
        {
            ResetFrameDamage();
            return true;
        }

        var graphics = _frameCacheGraphics;
        ClearFrameRegion(graphics, damage, wholeSurface: false);
        var state = graphics.Save();
        try
        {
            graphics.SetClip(damage);
            RenderRootInto(graphics, size, damage, cull: true);
        }
        finally
        {
            graphics.Restore(state);
        }

        ResetFrameDamage();
        return true;
    }

    private void ClearFrameRegion(Graphics graphics, RectangleF region, bool wholeSurface)
    {
        _frameClearBrush ??= new SolidBrush(Color.Transparent);
        if (wholeSurface)
        {
            graphics.Clear(Color.Transparent);
            return;
        }

        var state = graphics.Save();
        try
        {
            graphics.SetClip(region, CombineMode.Replace);
            // SourceCopy 才能把旧像素连同 alpha 一起抹成透明。
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.FillRectangle(_frameClearBrush, region);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    private void ResetFrameDamage()
    {
        _frameDamage = RectangleF.Empty;
        _frameDamageFull = false;
        _framePanPending = false;
        _framePanDx = 0f;
        _framePanDy = 0f;
    }

    private void AdvancePaintClock()
    {
        LastPaintTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        PaintCount++;
        LastPaintWasClockDriven = nextPaintClockDriven;
        nextPaintClockDriven = false;
    }

    // 把逻辑树画到物理客户区：先按 DPI 缩放世界坐标，再由 RenderCore 按逻辑尺寸排版。
    internal void RenderPhysical(Graphics graphics, Rectangle deviceClip)
    {
        if (graphics is null) throw new ArgumentNullException(nameof(graphics));

        // 窗口绘制不更新帧缓存，下一次 RenderTo 必须整帧重建。
        InvalidateFrameCache();

        var scale = DpiScale;
        if (scale <= 0f)
        {
            scale = 1f;
        }

        var logicalClip = ToLogical(deviceClip, scale);
        if (logicalClip.Width <= 0 || logicalClip.Height <= 0)
        {
            return;
        }

        // OnPaint 会先用 BackColor 铺满脏区；DPI 与 GDI+ 裁剪取整可能让脏区最后一列/行没被视觉树覆盖，
        // 拖动时就会在失效矩形边缘留下一条宿主底色的 1px 竖线。裁剪区向外放 1 个逻辑单位即可盖住。
        logicalClip.Inflate(1, 1);

        var logicalSize = LogicalClientSize;
        var state = graphics.Save();
        try
        {
            if (Math.Abs(scale - 1f) > 0.0001f)
            {
                graphics.ScaleTransform(scale, scale);
            }

            // 裁剪区必须在缩放之后设置，才会和其余坐标一样按逻辑单位解释。
            graphics.SetClip(logicalClip);

            // 手势期间按 MotionScale 降分辨率重画到离屏位图，再整体贴回逻辑矩形；目标已带 DPI 变换与裁剪。
            if (IsPointerGestureActive && MotionScale < 0.999f && logicalSize.Width > 0 && logicalSize.Height > 0)
            {
                RenderMotion(graphics, logicalSize, logicalClip);
            }
            else
            {
                RenderCore(graphics, logicalSize, logicalClip);
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    // 整面画进按 MotionScale 缩小的复用位图；DPI 缩放仍由目标 Graphics 承担，这里只处理逻辑坐标。
    private void RenderMotion(Graphics target, Size logicalSize, Rectangle damage)
    {
        if (_renderingMotion)
        {
            RenderCore(target, logicalSize, damage);
            return;
        }

        var motionScale = MotionScale;
        if (motionScale < 0.1f)
        {
            motionScale = 0.1f;
        }

        var bitmapWidth = Math.Max(1, (int)Math.Ceiling(logicalSize.Width * motionScale));
        var bitmapHeight = Math.Max(1, (int)Math.Ceiling(logicalSize.Height * motionScale));
        var bitmap = _motionBitmap;
        if (bitmap is null
            || bitmap.Width != bitmapWidth
            || bitmap.Height != bitmapHeight
            || Math.Abs(_motionBitmapScale - motionScale) > 0.0001f)
        {
            _motionBitmap?.Dispose();
            bitmap = new Bitmap(bitmapWidth, bitmapHeight, PixelFormat.Format32bppPArgb);
            _motionBitmap = bitmap;
            _motionBitmapScale = motionScale;
        }

        _renderingMotion = true;
        try
        {
            using var offscreen = Graphics.FromImage(bitmap);
            offscreen.ScaleTransform(motionScale, motionScale);
            // 离屏装的是整张位图，damage 只用于剔除远端子树；位图外的旧像素由目标裁剪区遮住。
            RenderCore(offscreen, logicalSize, damage);
        }
        finally
        {
            _renderingMotion = false;
        }

        _gestureDrewMotionFrame = true;
        target.DrawImage(
            bitmap,
            new RectangleF(0f, 0f, logicalSize.Width, logicalSize.Height));
    }

    private void RenderCore(Graphics graphics, Size size, Rectangle clearArea)
    {
        if (graphics is null) throw new ArgumentNullException(nameof(graphics));

        if (_root is null)
        {
            return;
        }

        // 脏区小于整面时才参与子树剔除；整面重画（RenderTo / 手势结束帧）照旧画完整棵树。
        var damage = new RectangleF(clearArea.X, clearArea.Y, clearArea.Width, clearArea.Height);
        var cull = clearArea.Left > 0
                   || clearArea.Top > 0
                   || clearArea.Right < size.Width
                   || clearArea.Bottom < size.Height;
        EnsureLayout(size);
        RenderRootInto(graphics, size, damage, cull);
        AdvancePaintClock();
    }

    // 画根树与覆盖层；damage 只影响子树剔除。
    private void RenderRootInto(Graphics graphics, Size size, RectangleF damage, bool cull)
    {
        var previousDamage = Visual.RenderDamage;
        var previousCull = Visual.CullToDamage;
        var previousGraphics = Visual.RenderGraphics;
        Visual.PushRenderDamage(damage, cull);
        Visual.PushRenderGraphics(graphics);
        Visual.PushRenderOffset(0f, 0f, true, out var previousX, out var previousY, out var previousIdentity);

        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            _root!.Render(graphics);
            _overlay.Render(graphics);
        }
        finally
        {
            graphics.Restore(state);
            Visual.PushRenderDamage(previousDamage, previousCull);
            Visual.PushRenderGraphics(previousGraphics);
            Visual.PushRenderOffset(previousX, previousY, previousIdentity);
        }
    }

    /// <summary>Closes every popup currently hosted by the overlay layer.</summary>
    public void ClosePopups() => CloseAllPopups();

    /// <summary>Moves logical focus to <paramref name="element"/>.</summary>
    /// <param name="element">The element to focus, or <see langword="null"/> to clear focus.</param>
    /// <returns><see langword="true"/> when the element belongs to this surface and is focusable.</returns>
    public bool FocusElement(VisualElement? element)
    {
        if (element is not null
            && (element.Surface != this || !element.Focusable || !element.IsEffectivelyEnabled || !element.IsVisible))
        {
            return false;
        }

        if (ReferenceEquals(_focused, element))
        {
            if (element is not null)
            {
                element.SetFocusVisible(_focusVisible);
                FocusHostUnlessNativeChildTookFocus();
            }

            return true;
        }

        _focused?.SetFocused(false);
        _focused = element;
        _focused?.SetFocused(true);
        _focused?.SetFocusVisible(_focusVisible);
        if (element is not null)
        {
            FocusHostUnlessNativeChildTookFocus();
        }

        return true;
    }

    // 逻辑焦点元素可能是一个原生岛（文本框的 IME 编辑器）：它在 OnGotFocus 里拿走原生焦点，
    // 这里若再把焦点拉回宿主，宿主就会丢掉焦点并触发岛自己的 OnLostFocus，把岛藏掉。
    private void FocusHostUnlessNativeChildTookFocus()
    {
        if (!ContainsFocus)
        {
            Focus();
        }
    }

    /// <summary>Injects a pointer press at a client point.</summary>
    /// <param name="point">The point in physical client pixels; it is converted to logical surface coordinates.</param>
    /// <param name="button">The pressed button.</param>
    /// <param name="clickCount">The click count; at least one.</param>
    /// <returns><see langword="true"/> when an element handled the event.</returns>
    public bool ProcessPointerDown(PointF point, MouseButtons button, int clickCount = 1)
    {
        // 宿主先拿到 WinForms 焦点：原生编辑器是宿主的子 HWND，宿主没焦点时 _editor.Focus() 会失败，
        // 第一次点击就只打开编辑器、光标不进编辑器，得再点一次。
        if (IsHandleCreated && CanFocus && !Focused)
        {
            Focus();
        }

        // 鼠标点击只转移焦点，不显示键盘焦点环；否则 Gallery 里每个 Toggle/Button 点一下都会多一圈蓝框。
        _focusVisible = false;
        _focused?.SetFocusVisible(false);
        point = ToLogical(point);
        var target = FindElementAt(point);
        DismissPopupsFor(target);
        // 禁用的祖先连同其子树都不接收这一笔按下；本地把 IsEnabled 写回 true 的子项也一样。
        var inputTarget = ResolveInputTarget(target);
        if (inputTarget is not null)
        {
            var focusable = FindFocusableAncestor(inputTarget);
            if (focusable is not null)
            {
                FocusElement(focusable);
            }
            else if (_focused is { } focused
                     && !IsInside(focused, inputTarget)
                     && !IsInsidePopup(inputTarget))
            {
                // 点在不可聚焦、且不在当前焦点子树里的区域：按“点空白处失焦”的直觉清掉焦点。
                // 弹层自己管理焦点与键盘转发，点它的非可聚焦背景不清焦点。
                FocusElement(null);
            }
        }

        _capture = inputTarget;
        if (inputTarget is not null && IsHandleCreated)
        {
            Capture = true;
        }

        _buttons |= button;
        if (!IsPointerGestureActive)
        {
            _gestureDrewMotionFrame = false;
        }

        IsPointerGestureActive = true;
        if (FreezeNativeIslandsDuringGestures)
        {
            FreezeNativeIslands();
        }

        if (inputTarget is not null)
        {
            var chain = new List<VisualElement>();
            for (var current = inputTarget; current is not null; current = current.ParentElement)
            {
                chain.Add(current);
            }

            for (var index = chain.Count - 1; index >= 0; index--)
            {
                var preview = new VisualPointerEventArgs(point, _buttons, button, clickCount, 0, ModifierKeys);
                chain[index].RaisePreviewPointerPressed(preview);
                if (preview.Handled)
                {
                    return true;
                }
            }
        }

        var args = new VisualPointerEventArgs(point, _buttons, button, clickCount, 0, ModifierKeys);
        inputTarget?.RaisePointerPressed(args);
        return args.Handled;
    }

    /// <summary>Injects a pointer move at a client point.</summary>
    /// <param name="point">The point in physical client pixels; it is converted to logical surface coordinates.</param>
    /// <returns><see langword="true"/> when an element handled the event.</returns>
    public bool ProcessPointerMove(PointF point)
    {
        point = ToLogical(point);
        var hoverTarget = FindElementAt(point);
        // 禁用子树不进入悬停链，悬停态与按下态保持同一套启用判定。
        UpdateHover(point, ResolveInputTarget(hoverTarget));
        var target = ResolveInputTarget(_capture ?? hoverTarget);
        var args = new VisualPointerEventArgs(point, _buttons, MouseButtons.None, 1, 0, ModifierKeys);
        target?.RaisePointerMoved(args);
        Cursor = hoverTarget?.Cursor ?? Cursors.Default;
        return args.Handled;
    }

    /// <summary>Injects a pointer release at a client point.</summary>
    /// <param name="point">The point in physical client pixels; it is converted to logical surface coordinates.</param>
    /// <param name="button">The released button.</param>
    /// <returns><see langword="true"/> when an element handled the event.</returns>
    public bool ProcessPointerUp(PointF point, MouseButtons button)
    {
        point = ToLogical(point);
        var target = ResolveInputTarget(_capture ?? FindElementAt(point) ?? _root as VisualElement);
        _buttons &= ~button;
        var args = new VisualPointerEventArgs(point, _buttons, button, 1, 0, ModifierKeys);
        target?.RaisePointerReleased(args);
        _capture = null;
        if (IsHandleCreated)
        {
            Capture = false;
        }

        EndPointerGesture();
        return args.Handled;
    }

    /// <summary>Continues a pointer gesture that started on another surface.</summary>
    /// <param name="screenPoint">The pointer position in screen pixels.</param>
    /// <param name="button">The button that is still held.</param>
    /// <returns><see langword="true"/> when an element under the pointer accepted the gesture.</returns>
    /// <remarks>
    /// A gesture cannot keep the source surface's Win32 capture after its element moves into another window.
    /// The caller cancels the source gesture, places the receiving element under the pointer, and calls this
    /// method; the adopted press starts at the current pointer position and subsequent moves route here.
    /// </remarks>
    public bool AdoptPointerGesture(Point screenPoint, MouseButtons button = MouseButtons.Left)
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return false;
        }

        var client = PointToClient(screenPoint);
        ProcessPointerDown(new PointF(client.X, client.Y), button);
        return _capture is not null;
    }

    /// <summary>Ends the current pointer gesture without raising <see cref="VisualElement.PointerReleased"/>.</summary>
    /// <remarks>Use on the source surface before moving the captured element to another host.</remarks>
    public void CancelPointerGesture()
    {
        _capture = null;
        _buttons = MouseButtons.None;
        if (IsHandleCreated && !IsDisposed)
        {
            Capture = false;
        }

        EndPointerGesture();
    }

    // 手势期间把所有活体岛冻结成位图：绘制期间它们不移动 HWND，松手后按当前排列矩形恢复。
    private void FreezeNativeIslands()
    {
        _frozenNatives.Clear();
        foreach (var owner in _nativeChildren.Keys.ToArray())
        {
            if (owner.FreezeForGesture())
            {
                _frozenNatives.Add(owner);
            }
        }
    }

    private void UnfreezeNativeIslands()
    {
        foreach (var owner in _frozenNatives)
        {
            owner.UnfreezeAfterGesture();
        }

        _frozenNatives.Clear();
    }

    // 手势结束：若期间画过低分辨率帧，排一次整面重画换回清晰帧。
    private void EndPointerGesture()
    {
        if (!IsPointerGestureActive)
        {
            return;
        }

        IsPointerGestureActive = false;
        UnfreezeNativeIslands();
        if (_gestureDrewMotionFrame)
        {
            _gestureDrewMotionFrame = false;
            InvalidatePaint();
        }
    }

    /// <summary>Injects a wheel turn at a client point.</summary>
    /// <param name="point">The point in physical client pixels; it is converted to logical surface coordinates.</param>
    /// <param name="delta">The wheel delta; positive scrolls up.</param>
    /// <returns><see langword="true"/> when an element handled the event.</returns>
    public bool ProcessPointerWheel(PointF point, int delta)
    {
        point = ToLogical(point);
        var target = ResolveInputTarget(FindElementAt(point));
        var args = new VisualPointerEventArgs(point, _buttons, MouseButtons.None, 1, delta, ModifierKeys);
        target?.RaisePointerWheel(args);
        return args.Handled;
    }

    /// <summary>Injects a horizontal wheel turn at a client point.</summary>
    /// <param name="point">The point in physical client pixels; it is converted to logical surface coordinates.</param>
    /// <param name="delta">The horizontal wheel delta; positive scrolls right.</param>
    /// <returns><see langword="true"/> when an element handled the event.</returns>
    public bool ProcessPointerHWheel(PointF point, int delta)
    {
        point = ToLogical(point);
        var target = FindElementAt(point);
        var args = new VisualPointerEventArgs(
            point, _buttons, MouseButtons.None, 1, delta, ModifierKeys, isHorizontal: true);
        target?.RaisePointerWheel(args);
        return args.Handled;
    }

    /// <summary>Injects the start of a drag at a client point.</summary>
    /// <param name="point">The point in physical client pixels; it is converted to logical surface coordinates.</param>
    /// <param name="data">The dragged data; a null value is replaced by an empty data object.</param>
    /// <param name="allowedEffects">The effects the drag source permits.</param>
    /// <param name="modifiers">The modifier keys to report.</param>
    /// <returns>The effect the deepest drop target wants.</returns>
    public DragDropEffects ProcessDragEnter(
        PointF point,
        IDataObject? data,
        DragDropEffects allowedEffects,
        Keys modifiers = Keys.None)
    {
        point = ToLogical(point);
        data ??= new DataObject();
        var target = FindDragTarget(point);
        if (!ReferenceEquals(target, _dragTarget))
        {
            LeaveDragTarget();
            _dragTarget = target;
        }

        RememberDrag(point, data, allowedEffects, modifiers);
        if (target is null)
        {
            _dragEffects = DragDropEffects.None;
            return DragDropEffects.None;
        }

        var args = new VisualDragEventArgs(data, allowedEffects, DragDropEffects.None, point, modifiers);
        target.RaiseDragEnter(args);
        _dragEffects = args.Effects & allowedEffects;
        return _dragEffects;
    }

    /// <summary>Injects a drag move at a client point.</summary>
    /// <param name="point">The point in physical client pixels; it is converted to logical surface coordinates.</param>
    /// <param name="data">The dragged data; a null value is replaced by an empty data object.</param>
    /// <param name="allowedEffects">The effects the drag source permits.</param>
    /// <param name="modifiers">The modifier keys to report.</param>
    /// <returns>The effect the deepest drop target wants.</returns>
    public DragDropEffects ProcessDragOver(
        PointF point,
        IDataObject? data,
        DragDropEffects allowedEffects,
        Keys modifiers = Keys.None)
    {
        point = ToLogical(point);
        data ??= new DataObject();
        var target = FindDragTarget(point);
        if (!ReferenceEquals(target, _dragTarget))
        {
            LeaveDragTarget();
            _dragTarget = target;
            if (target is not null)
            {
                var enter = new VisualDragEventArgs(data, allowedEffects, DragDropEffects.None, point, modifiers);
                target.RaiseDragEnter(enter);
                _dragEffects = enter.Effects & allowedEffects;
            }
        }

        RememberDrag(point, data, allowedEffects, modifiers);
        if (target is null)
        {
            _dragEffects = DragDropEffects.None;
            return DragDropEffects.None;
        }

        var args = new VisualDragEventArgs(data, allowedEffects, _dragEffects, point, modifiers);
        target.RaiseDragOver(args);
        _dragEffects = args.Effects & allowedEffects;
        return _dragEffects;
    }

    /// <summary>Injects the end of a drag without a drop.</summary>
    public void ProcessDragLeave()
    {
        LeaveDragTarget();
        _dragData = null;
        _dragEffects = DragDropEffects.None;
    }

    /// <summary>Injects a drop at a client point.</summary>
    /// <param name="point">The point in physical client pixels; it is converted to logical surface coordinates.</param>
    /// <param name="data">The dropped data; a null value is replaced by an empty data object.</param>
    /// <param name="allowedEffects">The effects the drag source permits.</param>
    /// <param name="modifiers">The modifier keys to report.</param>
    /// <returns>The effect the drop reported.</returns>
    public DragDropEffects ProcessDragDrop(
        PointF point,
        IDataObject? data,
        DragDropEffects allowedEffects,
        Keys modifiers = Keys.None)
    {
        point = ToLogical(point);
        data ??= new DataObject();
        var target = FindDragTarget(point);
        if (!ReferenceEquals(target, _dragTarget))
        {
            LeaveDragTarget();
            _dragTarget = target;
        }

        RememberDrag(point, data, allowedEffects, modifiers);
        if (target is null)
        {
            _dragData = null;
            _dragEffects = DragDropEffects.None;
            return DragDropEffects.None;
        }

        var args = new VisualDragEventArgs(data, allowedEffects, _dragEffects, point, modifiers);
        target.RaiseDrop(args);
        _dragEffects = args.Effects & allowedEffects;
        _dragTarget = null;
        _dragData = null;
        return _dragEffects;
    }

    /// <summary>Starts a native OLE drag and blocks until the user drops or cancels it.</summary>
    /// <param name="source">The element that owns the drag.</param>
    /// <param name="data">The data carried by the drag.</param>
    /// <param name="allowedEffects">The effects the source permits.</param>
    /// <param name="ghostText">Text for the floating drag label, or <see langword="null"/> for no label.</param>
    /// <returns>The effect the drop target reported.</returns>
    public DragDropEffects StartVisualDrag(
        VisualElement source,
        IDataObject data,
        DragDropEffects allowedEffects,
        string? ghostText = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (data is null) throw new ArgumentNullException(nameof(data));
        if (source.Surface is { } surface && !ReferenceEquals(surface, this))
        {
            return surface.StartVisualDrag(source, data, allowedEffects, ghostText);
        }

        if (!IsHandleCreated || VisualDragDrop.IsDragging)
        {
            return DragDropEffects.None;
        }

        VisualDragDrop.IsDragging = true;
        VisualDragDrop.DragSource = source;
        VisualDragDrop.DragData = data;
        VisualDragDrop.CurrentEffects = allowedEffects;
        RememberDrag(ToLogical(PointToClient(System.Windows.Forms.Cursor.Position)), data, allowedEffects, ModifierKeys);
        _dragEffects = allowedEffects;

        if (!string.IsNullOrEmpty(ghostText))
        {
            _dragGhost = new VisualDragGhost(ghostText!);
            _overlay.Add(_dragGhost);
            UpdateDragGhost(PointToClient(System.Windows.Forms.Cursor.Position));
        }

        try
        {
            return DoDragDrop(data, allowedEffects);
        }
        finally
        {
            ProcessDragLeave();
            VisualDragDrop.IsDragging = false;
            VisualDragDrop.DragSource = null;
            VisualDragDrop.DragData = null;
            VisualDragDrop.CurrentEffects = DragDropEffects.None;
            if (_dragGhost is { } ghost)
            {
                _overlay.Remove(ghost);
                _dragGhost = null;
            }

            _buttons = MouseButtons.None;
            _capture = null;
            if (IsHandleCreated)
            {
                Capture = false;
            }

            EndPointerGesture();
            Cursor = Cursors.Default;
            InvalidateSurface();
        }
    }

    private void RememberDrag(PointF point, IDataObject data, DragDropEffects allowedEffects, Keys modifiers)
    {
        _dragPoint = point;
        _dragData = data;
        _dragAllowed = allowedEffects;
        _dragModifiers = modifiers;
    }

    private void LeaveDragTarget()
    {
        if (_dragTarget is not { } target)
        {
            return;
        }

        _dragTarget = null;
        var data = _dragData ?? new DataObject();
        var args = new VisualDragEventArgs(data, _dragAllowed, _dragEffects, _dragPoint, _dragModifiers);
        target.RaiseDragLeave(args);
        InvalidateSurface();
    }

    private VisualElement? FindDragTarget(PointF point)
    {
        var hit = FindElementAt(point);
        for (var current = hit; current is not null; current = current.ParentElement)
        {
            if (current.AllowDrop)
            {
                return current;
            }
        }

        return null;
    }

    private void UpdateDragGhost(Point clientPoint)
    {
        if (_dragGhost is null)
        {
            return;
        }

        if (_dragGhost.DesiredSize.Width <= 0 || _dragGhost.DesiredSize.Height <= 0)
        {
            _dragGhost.Measure(new Size(4096, 4096));
        }

        var size = _dragGhost.DesiredSize;
        var point = ToLogical(clientPoint);
        var client = ToLogical(new PointF(ClientSize.Width, ClientSize.Height));
        var x = Math.Min(point.X + 16f, Math.Max(0f, client.X - size.Width - 4f));
        var y = Math.Min(point.Y + 18f, Math.Max(0f, client.Y - size.Height - 4f));
        _dragGhost.Bounds = new RectangleF(Math.Max(0f, x), Math.Max(0f, y), size.Width, size.Height);
        _dragGhost.InvalidateVisual();
        InvalidateSurface();
    }

    private const int WmMouseHWheel = 0x020E;

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    /// <inheritdoc />
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmMouseHWheel)
        {
            var raw = m.LParam.ToInt64();
            var screen = new Point(unchecked((short)(raw & 0xFFFFL)), unchecked((short)((raw >> 16) & 0xFFFFL)));
            var point = PointToClient(screen);
            var delta = unchecked((short)((m.WParam.ToInt64() >> 16) & 0xFFFFL));
            ProcessPointerHWheel(point, delta);
            m.Result = IntPtr.Zero;
            return;
        }

        base.WndProc(ref m);
    }

    /// <summary>Injects a key press into the focused element with the current modifiers.</summary>
    /// <param name="keyCode">The key code.</param>
    /// <returns><see langword="true"/> when an element handled the event.</returns>
    public bool ProcessKeyDown(Keys keyCode) => ProcessKeyDown(keyCode, ModifierKeys);

    /// <summary>Injects a key press with explicit modifiers; used by hosts and tests.</summary>
    /// <param name="keyCode">The key code.</param>
    /// <param name="modifiers">The modifier keys to report.</param>
    /// <returns><see langword="true"/> when an element handled the event.</returns>
    public bool ProcessKeyDown(Keys keyCode, Keys modifiers)
    {
        _focusVisible = true;
        _focused?.SetFocusVisible(true);
        var args = new VisualKeyEventArgs(keyCode, modifiers, isRepeat: false);
        _focused?.RaiseKeyDown(args);
        if (!args.Handled && keyCode == Keys.Tab)
        {
            MoveFocus((modifiers & Keys.Shift) == 0);
            return true;
        }

        return args.Handled;
    }

    /// <summary>Injects a key release into the focused element with the current modifiers.</summary>
    /// <param name="keyCode">The key code.</param>
    /// <returns><see langword="true"/> when an element handled the event.</returns>
    public bool ProcessKeyUp(Keys keyCode) => ProcessKeyUp(keyCode, ModifierKeys);

    /// <summary>Injects a key release with explicit modifiers; used by hosts and tests.</summary>
    /// <param name="keyCode">The key code.</param>
    /// <param name="modifiers">The modifier keys to report.</param>
    /// <returns><see langword="true"/> when an element handled the event.</returns>
    public bool ProcessKeyUp(Keys keyCode, Keys modifiers)
    {
        var args = new VisualKeyEventArgs(keyCode, modifiers, isRepeat: false);
        _focused?.RaiseKeyUp(args);
        return args.Handled;
    }

    /// <summary>Injects text input into the focused element.</summary>
    /// <param name="text">The characters to insert.</param>
    /// <returns><see langword="true"/> when an element handled the event.</returns>
    public bool ProcessTextInput(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var args = new VisualTextInputEventArgs(text);
        _focused?.RaiseTextInput(args);
        return args.Handled;
    }

    /// <summary>Returns the deepest visual under a logical client point, or <see langword="null"/>.</summary>
    /// <param name="point">The point in logical client coordinates.</param>
    public Visual? HitTestVisual(Point point)
    {
        EnsureLayout(LogicalClientSize);
        return _root?.HitTestVisual(point);
    }

    /// <summary>Finds the deepest element under a logical surface point.</summary>
    /// <param name="point">The point in logical surface coordinates (DIPs), never physical client pixels.</param>
    public VisualElement? FindElementAt(PointF point)
    {
        if (_root is null)
        {
            return null;
        }

        EnsureLayout(LogicalClientSize);
        var hit = _overlay.HitTestVisual(point) ?? _root.HitTestVisual(point);
        for (var current = hit; current is not null; current = current.Parent)
        {
            if (current is VisualElement element)
            {
                return element;
            }
        }

        return null;
    }

    // bounds 是逻辑单位；原生 HWND 必须按物理客户区像素摆放。
    internal void PlaceNative(NativeElement owner, System.Windows.Forms.Control child, RectangleF bounds)
    {
        if (owner.IsFrozen)
        {
            // 冻结期间岛不参与原生摆放；松手时自己重新摆回。
            return;
        }

        if (owner.RequiresSnapshotComposition)
        {
            // 组合（透明度/旋转/效果/遮罩）无法作用在子 HWND 上；转快照路径，位图跟着视觉管线走。
            owner.EnterSnapshotComposition();
            return;
        }

        if (!_nativeChildren.TryGetValue(owner, out var existing))
        {
            _nativeChildren[owner] = child;
            _nativeTreeOrder[owner] = _nativeOrderCounter++;
            Controls.Add(child);
            ReorderNativeChildren();
        }
        else if (!ReferenceEquals(existing, child))
        {
            Controls.Remove(existing);
            RestoreNativeRegion(existing);
            _nativeChildren[owner] = child;
            Controls.Add(child);
            ReorderNativeChildren();
        }

        _placedNative.Add(owner);

        // 原生岛是独立 HWND，视觉层的 Clip 管不到它；不裁的话，滚动到视口边缘的文本框编辑器
        // 或 NativeElement 会浮在滚动条和其它面板之上。按祖先剪辑矩形与客户区的交集摆放。
        var visible = RectangleF.Intersect(bounds, GetNativeClipBounds(owner));
        if (visible.Width < 1f || visible.Height < 1f)
        {
            QueueOrApplyNative(owner, child, Rectangle.Empty, show: false, RectangleF.Empty);
            return;
        }

        QueueOrApplyNative(
            owner,
            child,
            ToPhysical(bounds),
            show: owner.IsVisible && owner.Visibility == XamlVisibility.Visible,
            visible);
    }

    // 绘制期间的摆放先排队，等父窗口这一帧画完、交给 WinForms 贴屏之前再统一移动子 HWND。
    // 岛若在绘制中途就挪走，长一帧里屏幕会同时出现"新位置的岛 + 旧一帧的卡片"，看起来就是画面拖影。
    private void QueueOrApplyNative(NativeElement owner, System.Windows.Forms.Control child, Rectangle physical, bool show, RectangleF visible)
    {
        if (_rendering)
        {
            _pendingPlacements[owner] = (child, physical, show, visible);
            return;
        }

        ApplyNativePlacement(owner, child, physical, show, visible);
    }

    private void ApplyNativePlacement(NativeElement owner, System.Windows.Forms.Control child, Rectangle physical, bool show, RectangleF visible)
    {
        // 模态对话期间原生岛整块收起：子 HWND 永远压在视觉覆盖层之上，不摘掉就会盖住遮罩。
        if (_nativeSuppressionCount > 0)
        {
            if (child.Visible)
            {
                child.Visible = false;
            }

            return;
        }

        if (!show)
        {
            child.Visible = false;
            return;
        }

        owner.IsBeingPlaced = true;
        try
        {
            var previous = child.Bounds;
            if (previous != physical)
            {
                child.Bounds = physical;
                // 岛占的屏幕区域不会被父窗口绘制（WS_CLIPCHILDREN）；移动后把新旧矩形都标脏，
                // 下一次父窗口绘制才会把旧位置补画干净。
                Invalidate(Rectangle.Union(previous, physical));
            }
        }
        finally
        {
            owner.IsBeingPlaced = false;
        }

        child.Visible = true;

        // 可见交集用窗口区域表达，而不是把窗口本身缩小：缩小会连原生内容一起重排
        // （视频会被缩放、文本框会重折行），拖动分割条时就会看到画面尺寸跳变。
        var actual = child.Bounds;
        var physicalVisible = Rectangle.Intersect(actual, ToPhysical(visible));
        var localVisible = new Rectangle(
            physicalVisible.X - actual.X,
            physicalVisible.Y - actual.Y,
            physicalVisible.Width,
            physicalVisible.Height);
        var clip = localVisible.Left <= 0
                   && localVisible.Top <= 0
                   && localVisible.Right >= child.Width
                   && localVisible.Bottom >= child.Height
            ? (Rectangle?)null
            : localVisible;
        ApplyNativeRegion(owner, child, clip);
    }

    private bool FlushNativePlacements()
    {
        if (_pendingPlacements.Count == 0)
        {
            return false;
        }

        var pending = _pendingPlacements.ToArray();
        _pendingPlacements.Clear();
        var applied = false;
        foreach (var pair in pending)
        {
            if (_nativeChildren.TryGetValue(pair.Key, out var current)
                && ReferenceEquals(current, pair.Value.Child))
            {
                applied = true;
                ApplyNativePlacement(
                    pair.Key,
                    pair.Value.Child,
                    pair.Value.Physical,
                    pair.Value.Show,
                    pair.Value.Visible);
            }
        }

        return applied;
    }

    // 弹层画在主表面的覆盖层里，子 HWND 永远盖在它上面；把弹层的物理矩形从岛的窗口区域里挖掉，
    // 弹层就能从洞里露出来。CornerRadius > 0 时区域换成圆角矩形。
    private void ApplyNativeRegion(NativeElement owner, System.Windows.Forms.Control child, Rectangle? clip = null)
    {
        if (!child.IsHandleCreated)
        {
            return;
        }

        var cuts = CollectPopupCuts(child.Bounds);
        var corner = (float)(owner.CornerRadius * DpiScale);
        var current = (Width: child.Width, Height: child.Height, Radius: corner, Clip: clip, Cuts: cuts.ToArray());
        if (_nativeRegionStates.TryGetValue(child, out var previous)
            && previous.Width == current.Width
            && previous.Height == current.Height
            && previous.Radius.Equals(current.Radius)
            && Nullable.Equals(previous.Clip, current.Clip)
            && previous.Cuts.SequenceEqual(current.Cuts))
        {
            return;
        }

        _nativeRegionStates[child] = current;
        NativeWindowRegion.Apply(child, corner, current.Cuts, clip);
        // 圆角/挖洞会把原本由岛覆盖的像素还给父窗口；不补画，改半径或关弹层后旧像素会留在角上。
        Invalidate(Rectangle.Inflate(child.Bounds, 1, 1));
    }

    // 以 target（表面客户区物理坐标）为坐标系，返回被弹层覆盖的矩形；调用方把它们从窗口区域里减掉。
    internal List<Rectangle> CollectPopupCuts(Rectangle target)
    {
        var cuts = new List<Rectangle>();
        foreach (var popup in _openPopups)
        {
            var logical = popup.Bounds;
            if (logical.Width <= 0f || logical.Height <= 0f)
            {
                continue;
            }

            var physical = ToPhysical(logical);
            var relative = new Rectangle(
                physical.X - target.X,
                physical.Y - target.Y,
                physical.Width,
                physical.Height);
            var overlap = Rectangle.Intersect(relative, new Rectangle(0, 0, target.Width, target.Height));
            if (overlap.Width > 0 && overlap.Height > 0)
            {
                cuts.Add(overlap);
            }
        }

        return cuts;
    }

    // 快照岛的可视部分由一个独立顶层输入代理承载；弹层打开/关闭时它的窗口区域也要重算。
    internal void RegisterSnapshotProxy(NativeElement element) => _snapshotProxies.Add(element);

    internal void UnregisterSnapshotProxy(NativeElement element) => _snapshotProxies.Remove(element);

    internal void RefreshNativeRegions()
    {
        foreach (var pair in _nativeChildren)
        {
            Rectangle? clip = _nativeRegionStates.TryGetValue(pair.Value, out var state) ? state.Clip : null;
            ApplyNativeRegion(pair.Key, pair.Value, clip);
        }

        foreach (var proxy in _snapshotProxies)
        {
            proxy.RefreshProxyRegion();
        }
    }

    private void RestoreNativeRegion(System.Windows.Forms.Control child)
    {
        if (_nativeRegionStates.Remove(child))
        {
            NativeWindowRegion.Restore(child);
        }
    }

    private RectangleF GetNativeClipBounds(NativeElement owner)
    {
        var clip = new RectangleF(0f, 0f, LogicalClientSize.Width, LogicalClientSize.Height);
        for (var current = (VisualElement?)owner; current is not null; current = current.ParentElement)
        {
            // HWND 不继承视觉树的可见性/透明度；祖先一藏，岛必须整块摘掉。
            if (!current.IsVisible || current.Opacity <= 0d)
            {
                return RectangleF.Empty;
            }

            if (current.ClipToBounds)
            {
                clip = RectangleF.Intersect(clip, current.AbsoluteBounds);
            }

            if (current.Clip is { } local)
            {
                clip = RectangleF.Intersect(
                    clip,
                    new RectangleF(
                        current.AbsoluteBounds.X + local.X,
                        current.AbsoluteBounds.Y + local.Y,
                        local.Width,
                        local.Height));
            }

            if (clip.Width <= 0f || clip.Height <= 0f)
            {
                break;
            }
        }

        return clip;
    }

    internal void RemoveNative(NativeElement owner)
    {
        if (!_nativeChildren.TryGetValue(owner, out var child))
        {
            return;
        }

        _nativeChildren.Remove(owner);
        _nativeTreeOrder.Remove(owner);
        _placedNative.Remove(owner);
        RestoreNativeRegion(child);
        if (!child.IsDisposed && Controls.Contains(child))
        {
            Controls.Remove(child);
        }

        // 摘掉的岛不会再画自己，它原先占的像素必须由父窗口补画。
        Invalidate(Rectangle.Inflate(child.Bounds, 1, 1));
    }

    internal void RegisterAutoNative(NativeElement child) => _autoNativeChildren.Add(child);

    internal void UnregisterAutoNative(NativeElement child) => _autoNativeChildren.Remove(child);

    private void ArrangeAutoNativeChildren()
    {
        if (_autoNativeChildren.Count == 0)
        {
            return;
        }

        foreach (var child in _autoNativeChildren.ToArray())
        {
            if (!ReferenceEquals(child.Surface, this))
            {
                _autoNativeChildren.Remove(child);
                continue;
            }

            child.ArrangeInOwnerContentBox(LayoutPass);
        }
    }

    // 岛与岛之间的 z 序按 Panel.ZIndex，再按视觉树里的先后；岛整体仍在所有视觉内容之上。
    internal void ReorderNativeChildren()
    {
        if (_nativeChildren.Count < 2)
        {
            return;
        }

        var treeOrder = BuildNativeTreeOrder();
        var ordered = _nativeChildren
            .OrderBy(static pair => Easel.Visuals.Controls.Panel.GetZIndex(pair.Key))
            .ThenBy(pair => treeOrder.TryGetValue(pair.Key, out var index)
                ? index
                : _nativeTreeOrder.TryGetValue(pair.Key, out var seen) ? 1_000_000 + seen : int.MaxValue)
            .ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var child = ordered[index].Value;
            if (!Controls.Contains(child))
            {
                continue;
            }

            if (Controls.GetChildIndex(child) != index)
            {
                Controls.SetChildIndex(child, index);
            }
        }
    }

    // 视觉树先序里每个岛的位置；集合重排或改 ZIndex 后，子 HWND 的次序要跟着变。
    private Dictionary<NativeElement, int> BuildNativeTreeOrder()
    {
        var order = new Dictionary<NativeElement, int>();
        var next = 0;
        void Walk(Visual visual)
        {
            if (visual is NativeElement native)
            {
                order[native] = next++;
            }

            if (visual is ContainerVisual container)
            {
                var children = container.Children;
                for (var index = 0; index < children.Count; index++)
                {
                    if (children[index] is { } child)
                    {
                        Walk(child);
                    }
                }
            }
        }

        if (_root is not null)
        {
            Walk(_root);
        }

        Walk(_overlay);
        return order;
    }

    /// <inheritdoc />
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // 这里追踪的脏区是物理像素；RenderPhysical 再把它换算成逻辑单位。
        var damage = _damageFull
            ? new Rectangle(0, 0, ClientSize.Width, ClientSize.Height)
            : e.ClipRectangle.Width > 0 && e.ClipRectangle.Height > 0
                ? e.ClipRectangle
                : _damage;
        if (damage.Width <= 0 || damage.Height <= 0)
        {
            return;
        }

        using (var background = new SolidBrush(BackColor))
        {
            e.Graphics.FillRectangle(background, damage);
        }

        _rendering = true;
        _paintingFullDamage = _damageFull;
        try
        {
            RenderPhysical(e.Graphics, damage);
        }
        finally
        {
            _rendering = false;
            _paintingFullDamage = false;
        }

        // WinForms 在 OnPaint 返回后才把这一帧贴到屏幕；子 HWND 必须赶在那一刻之前落位。
        var placedNative = FlushNativePlacements();

        // 拖动期间等合成器提交这一帧，子 HWND 的位置和父窗口的新画面才会一起上屏。
        // 没有子 HWND 落位时没有需要同步的对象，等待只会把每一帧拖长。
        if (placedNative && IsPointerGestureActive)
        {
            _ = DwmFlush();
        }

        _damage = Rectangle.Empty;
        _damageFull = false;
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _dpiScaleValid = false;
        InvalidateSurface();
    }

    /// <inheritdoc />
    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        // 捕获被系统或别的控件拿走时，按起事件的释放不会再到达；手势必须在这里收尾。
        if (!Capture)
        {
            EndPointerGesture();
        }
    }

#if !NETFRAMEWORK
    /// <inheritdoc />
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        _dpiScaleValid = false;
        InvalidateSurface();
    }
#endif

    /// <inheritdoc />
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _dpiScaleValid = false;
        _motionBitmap?.Dispose();
        _motionBitmap = null;
        InvalidateSurface();
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        ProcessPointerDown(e.Location, e.Button, e.Clicks);
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        ProcessPointerMove(e.Location);
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        ProcessPointerUp(e.Location, e.Button);
    }

    /// <inheritdoc />
    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);
        var point = PointToClient(new Point(e.X, e.Y));
        var effect = ProcessDragEnter(point, e.Data, e.AllowedEffect, ModifierKeys);
        if (_dragTarget is not null)
        {
            e.Effect = effect;
        }
    }

    /// <inheritdoc />
    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        var point = PointToClient(new Point(e.X, e.Y));
        var effect = ProcessDragOver(point, e.Data, e.AllowedEffect, ModifierKeys);
        if (_dragTarget is not null)
        {
            e.Effect = effect;
        }
    }

    /// <inheritdoc />
    protected override void OnDragLeave(EventArgs e)
    {
        base.OnDragLeave(e);
        ProcessDragLeave();
    }

    /// <inheritdoc />
    protected override void OnDragDrop(DragEventArgs e)
    {
        base.OnDragDrop(e);
        var point = PointToClient(new Point(e.X, e.Y));
        var effect = ProcessDragDrop(point, e.Data, e.AllowedEffect, ModifierKeys);
        if (effect != DragDropEffects.None)
        {
            e.Effect = effect;
        }
    }

    /// <inheritdoc />
    protected override void OnGiveFeedback(GiveFeedbackEventArgs e)
    {
        base.OnGiveFeedback(e);
        if (!VisualDragDrop.IsDragging)
        {
            return;
        }

        // 拖动中的光标由当前目标的效果决定：可放置时是手形，无处可放时是禁止符。
        e.UseDefaultCursors = false;
        Cursor = _dragEffects == DragDropEffects.None ? Cursors.No : Cursors.Hand;
        UpdateDragGhost(PointToClient(System.Windows.Forms.Cursor.Position));
    }

    /// <inheritdoc />
    protected override void OnQueryContinueDrag(QueryContinueDragEventArgs e)
    {
        base.OnQueryContinueDrag(e);
        if (e.EscapePressed)
        {
            e.Action = DragAction.Cancel;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (ProcessPointerWheel(e.Location, e.Delta))
        {
            // handled; WinForms does not expose a cancel flag here
        }
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        // PointToClient 返回物理坐标，悬停链用的是逻辑坐标。
        var point = ToLogical(PointToClient(Cursor.Position));
        UpdateHover(point, ResolveInputTarget(FindElementAt(point)));
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        UpdateHover(null, null);
        Cursor = Cursors.Default;
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData)
    {
        // Tab/Enter/Escape/arrows are dialog keys by default; the logical focus chain owns them here.
        var key = keyData & Keys.KeyCode;
        if (key is Keys.Tab or Keys.Enter or Keys.Escape or Keys.Left or Keys.Right or Keys.Up or Keys.Down)
        {
            return true;
        }

        return base.IsInputKey(keyData);
    }

    /// <inheritdoc />
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if ((keyData & Keys.KeyCode) == Keys.Tab)
        {
            return ProcessKeyDown(Keys.Tab, keyData & Keys.Modifiers);
        }

        return base.ProcessDialogKey(keyData);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (ProcessKeyDown(e.KeyCode, e.Modifiers))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (ProcessKeyUp(e.KeyCode, e.Modifiers))
        {
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (char.IsControl(e.KeyChar))
        {
            return;
        }

        if (ProcessTextInput(e.KeyChar.ToString()))
        {
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _focused?.SetFocused(true);
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        // 焦点交给宿主里的原生岛（文本框的 IME 编辑器）不算离开逻辑焦点元素；否则元素会隐藏岛，
        // 焦点弹回宿主，再触发 OnGotFocus 重新显示岛，形成焦点来回拉锯。
        if (!ContainsFocus)
        {
            _focused?.SetFocused(false);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 主题是静态状态；不摘订阅，静态事件会把宿主钉在内存里。
            Theme.Changed -= OnThemeChanged;
            DataContextChanged -= OnHostDataContextChanged;
            CloseAllDialogs();
            CloseAllPopups();
            ClearNativeChildren();
            _focused = null;
            _capture = null;
            _hoverChain.Clear();
            IsPointerGestureActive = false;
            _motionBitmap?.Dispose();
            _motionBitmap = null;
            _frameCacheGraphics?.Dispose();
            _frameCacheGraphics = null;
            _frameCache?.Dispose();
            _frameCache = null;
            _frameScratchGraphics?.Dispose();
            _frameScratchGraphics = null;
            _frameScratch?.Dispose();
            _frameScratch = null;
            _frameClearBrush?.Dispose();
            _frameClearBrush = null;
            _frameMovedVisuals.Clear();
            _rootElement?.Dispose();
            _rootElement = null;
            _root = null;
            _overlay.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RefreshStyles()
    {
        _rootElement?.RefreshImplicitStyles();
        _overlay.RefreshImplicitStyles();
    }

    // 宿主 DataContext 是树的继承源：把变化转给根与覆盖层，由它们通知没有本地值的后代。
    private void OnHostDataContextChanged(object? sender, EventArgs e)
    {
        var oldValue = _dataContextSnapshot;
        var newValue = DataContext;
        _dataContextSnapshot = newValue;
        _rootElement?.RaiseHostDataContextChanged(oldValue, newValue);
        _overlay.RaiseHostDataContextChanged(oldValue, newValue);
    }

    // 主题色由控件在绘制时读取，切换 Theme.Mode 失效表面即可；资源同步会原地改色。
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        RefreshThemeResources();
        InvalidateSurface();
    }

    // 宿主自带当前主题字典，默认模板里的 DynamicResource 才能找到主题键；应用自己的字典后合并、优先级更高。
    private void RefreshThemeResources()
    {
        // 画笔原地改色，模板部件持有的同一个 brush 会一起变色；非画笔键值相同则不写，
        // 避免每个 DynamicResource 收到一次 Changed 的事件风暴。
        Theme.SyncResourceDictionary(_themeResources);
    }

    internal VisualElement OverlayHost => _overlay;

    internal void RegisterPopup(VisualPopup popup)
    {
        if (!_openPopups.Contains(popup))
        {
            _openPopups.Add(popup);
            RefreshNativeRegions();
        }
    }

    internal void UnregisterPopup(VisualPopup popup)
    {
        if (_openPopups.Remove(popup))
        {
            RefreshNativeRegions();
        }
    }

    // 对话按打开顺序叠放：后注册的在覆盖层末尾，绘制与命中都在最上层。
    internal void RegisterDialog(Dialogs.VisualDialog dialog)
    {
        if (_openDialogs.Contains(dialog))
        {
            return;
        }

        _openDialogs.Add(dialog);
        if (_openDialogs.Count == 1)
        {
            HideNativeIslands();
        }

        InvalidateSurface();
    }

    internal void UnregisterDialog(Dialogs.VisualDialog dialog)
    {
        if (!_openDialogs.Remove(dialog))
        {
            return;
        }

        if (_openDialogs.Count == 0)
        {
            RestoreNativeIslands();
        }

        InvalidateSurface();
    }

    // 最新的对话接收焦点与按键；栈里更低的对话只负责留在原位。
    internal Dialogs.VisualDialog? ActiveDialog
        => _openDialogs.Count == 0 ? null : _openDialogs[_openDialogs.Count - 1];

    private void HideNativeIslands()
    {
        _nativeSuppressionCount++;
        foreach (var child in _nativeChildren.Values)
        {
            if (child.Visible)
            {
                child.Visible = false;
            }
        }

        foreach (var proxy in _snapshotProxies)
        {
            proxy.SetProxySuppressed(true);
        }
    }

    private void RestoreNativeIslands()
    {
        if (_nativeSuppressionCount == 0)
        {
            return;
        }

        if (--_nativeSuppressionCount > 0 || IsDisposed)
        {
            return;
        }

        foreach (var proxy in _snapshotProxies)
        {
            proxy.SetProxySuppressed(false);
        }

        // 隐藏期间布局可能变过；恢复时按元素当前排列矩形重新摆放，而不是沿用旧 Bounds。
        foreach (var pair in _nativeChildren.ToArray())
        {
            PlaceNative(pair.Key, pair.Value, pair.Key.AbsoluteBounds);
        }

        RefreshNativeRegions();
        InvalidateSurface();
    }

    private void CloseAllPopups()
    {
        foreach (var popup in _openPopups.ToArray())
        {
            popup.Close();
        }

        _openPopups.Clear();
    }

    private void CloseAllDialogs()
    {
        foreach (var dialog in _openDialogs.ToArray())
        {
            dialog.Close(Dialogs.DialogResult.Cancel);
        }

        _openDialogs.Clear();
    }

    // 点击落在弹层或它的 PlacementTarget 里时保持打开；其余位置按 StaysOpen 关闭。
    private void DismissPopupsFor(VisualElement? target)
    {
        foreach (var popup in _openPopups.ToArray())
        {
            if (!popup.StaysOpen && !popup.ShouldStayOpen(target))
            {
                popup.Close();
            }
        }
    }

    private void ClearNativeChildren()
    {
        foreach (var child in _nativeChildren.Values)
        {
            RestoreNativeRegion(child);
            if (!child.IsDisposed && Controls.Contains(child))
            {
                Controls.Remove(child);
            }

            Invalidate(Rectangle.Inflate(child.Bounds, 1, 1));
        }

        _nativeChildren.Clear();
        _nativeTreeOrder.Clear();
        _placedNative.Clear();
        _nativeRegionStates.Clear();
        _autoNativeChildren.Clear();
        _snapshotProxies.Clear();
    }

    private void UpdateHover(PointF? point, VisualElement? target)
    {
        var next = new List<VisualElement>();
        for (var current = target; current is not null; current = current.ParentElement)
        {
            next.Add(current);
        }

        var args = point is { } position
            ? new VisualPointerEventArgs(position, MouseButtons.None, MouseButtons.None, 1, 0, ModifierKeys)
            : null;
        foreach (var element in _hoverChain)
        {
            if (!next.Contains(element))
            {
                element.SetPointerOver(false, args);
            }
        }

        foreach (var element in next)
        {
            if (!_hoverChain.Contains(element))
            {
                element.SetPointerOver(true, args);
            }
        }

        _hoverChain.Clear();
        _hoverChain.AddRange(next);
    }

    private PointF ToLogical(PointF point)
    {
        var scale = DpiScale;
        return scale <= 0f || Math.Abs(scale - 1f) < 0.0001f
            ? point
            : new PointF(point.X / scale, point.Y / scale);
    }

    private Rectangle ToPhysical(Rectangle rectangle)
    {
        var scale = DpiScale;
        if (scale <= 0f || Math.Abs(scale - 1f) < 0.0001f)
        {
            return rectangle;
        }

        return new Rectangle(
            (int)Math.Floor(rectangle.X * scale),
            (int)Math.Floor(rectangle.Y * scale),
            Math.Max(1, (int)Math.Ceiling(rectangle.Width * scale)),
            Math.Max(1, (int)Math.Ceiling(rectangle.Height * scale)));
    }

    // 逻辑矩形 → 物理像素：起点向下取整、终点向上取整，岛至少覆盖元素画到的每个像素。
    // 先 Round 再缩放会让右/下边缩进 1px（1.5 倍屏上很常见），露出岛后面的一条底色。
    private Rectangle ToPhysical(RectangleF logical)
    {
        var scale = DpiScale;
        if (scale <= 0f)
        {
            scale = 1f;
        }

        var left = (int)Math.Floor(logical.X * scale);
        var top = (int)Math.Floor(logical.Y * scale);
        var right = (int)Math.Ceiling(logical.Right * scale);
        var bottom = (int)Math.Ceiling(logical.Bottom * scale);
        return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    private static Rectangle ToLogical(Rectangle rectangle, float scale)
        => scale <= 0f || Math.Abs(scale - 1f) < 0.0001f
            ? rectangle
            : new Rectangle(
                (int)Math.Floor(rectangle.X / scale),
                (int)Math.Floor(rectangle.Y / scale),
                Math.Max(1, (int)Math.Ceiling(rectangle.Width / scale)),
                Math.Max(1, (int)Math.Ceiling(rectangle.Height / scale)));

    private void EnsureLayout(Size size)
    {
        if (_root is null)
        {
            return;
        }

        LayoutGeneration++;
        _layoutInProgress = true;
        try
        {
            EnsureLayoutCore(size);
        }
        finally
        {
            _layoutInProgress = false;
        }
    }

    private void EnsureLayoutCore(Size size)
    {
        if (_layoutValid && _layoutSize == size)
        {
            if (_dirtyLayoutBoundaries.Count == 0)
            {
                return;
            }

            var boundaries = _dirtyLayoutBoundaries.ToArray();
            _dirtyLayoutBoundaries.Clear();
            foreach (var boundary in boundaries)
            {
                if (boundary.Surface != this || !boundary.IsVisible)
                {
                    continue;
                }

                var available = boundary.MeasureAvailable;
                boundary.Measure(available);
                boundary.Arrange(boundary.Bounds);
            }

            return;
        }

        _dirtyLayoutBoundaries.Clear();

        // 先认领这一趟：measure/arrange 里再有 InvalidateMeasure（滚动条折叠/展开就是这种）
        // 会把 _layoutValid 打回 false，下一帧继续排版，而不是被本次结尾的 true 吞掉。
        _layoutValid = true;
        LayoutPass++;
        _placedNative.Clear();
        if (_rootElement is { } elementRoot)
        {
            elementRoot.Measure(size);
            elementRoot.Arrange(new RectangleF(0f, 0f, size.Width, size.Height));
        }
        else
        {
            _root!.Bounds = new RectangleF(0f, 0f, size.Width, size.Height);
        }

        // 覆盖层不属于根树：它铺满客户区，子项（Popup）自己决定位置，不参与面板排版。
        _overlay.Measure(size);
        _overlay.Arrange(new RectangleF(0f, 0f, size.Width, size.Height));
        foreach (var popup in _openPopups.ToArray())
        {
            popup.Reposition();
        }

        foreach (var dialog in _openDialogs.ToArray())
        {
            dialog.Reposition(size);
        }

        // 宿主自己不摆放的子级（AddNative 到非面板元素）由这里补一次内容区排版，再进入原生落位扫描。
        ArrangeAutoNativeChildren();

        // A host that was collapsed or removed from the tree never reaches ArrangeOverride, so its HWND would stay
        // floating above the surface. Anything not visited by this pass is detached — unless it is still live in this
        // tree and merely skipped the pass because its arranged size did not change.
        foreach (var pair in _nativeChildren.ToArray())
        {
            if (_placedNative.Contains(pair.Key))
            {
                continue;
            }

            if (pair.Key.Surface == this && pair.Key.IsVisible)
            {
                PlaceNative(pair.Key, pair.Value, pair.Key.AbsoluteBounds);
            }
            else
            {
                RemoveNative(pair.Key);
            }
        }

        RefreshNativeRegions();
        _layoutSize = size;
        LayoutUpdated?.Invoke(this, EventArgs.Empty);
    }

    // 输入目标必须自身与所有祖先都启用；否则这一笔输入整段作废。
    private static VisualElement? ResolveInputTarget(VisualElement? element)
        => element is not null && element.IsEffectivelyEnabled ? element : null;

    private static bool IsInsidePopup(VisualElement element)
    {
        for (VisualElement? current = element; current is not null; current = current.ParentElement)
        {
            if (current is Controls.VisualPopup)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInside(VisualElement ancestor, VisualElement element)
    {
        for (VisualElement? current = element; current is not null; current = current.ParentElement)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static VisualElement? FindFocusableAncestor(VisualElement? element)
    {
        for (var current = element; current is not null; current = current.ParentElement)
        {
            if (current.Focusable && current.IsEffectivelyEnabled && current.IsVisible)
            {
                return current;
            }
        }

        return null;
    }

    private void MoveFocus(bool forward)
    {
        var order = new List<VisualElement>();

        // 模态对话打开时 Tab 只在最上层对话内部循环，不能走回被遮住的根树。
        if (ActiveDialog is { } dialog)
        {
            dialog.CollectFocusOrder(order);
        }
        else if (_rootElement is not null)
        {
            CollectFocusable(_rootElement, order);
        }

        if (order.Count == 0)
        {
            return;
        }

        // TabIndex 升序；同值时保持遍历顺序（稳定 DFS），不会因排序实现而抖动。
        var ranked = new List<(VisualElement Element, int Index)>(order.Count);
        for (var index = 0; index < order.Count; index++)
        {
            ranked.Add((order[index], index));
        }

        ranked.Sort(static (left, right) =>
        {
            var byTabIndex = left.Element.TabIndex.CompareTo(right.Element.TabIndex);
            return byTabIndex != 0 ? byTabIndex : left.Index.CompareTo(right.Index);
        });

        var currentIndex = -1;
        for (var position = 0; position < ranked.Count; position++)
        {
            if (ReferenceEquals(ranked[position].Element, _focused))
            {
                currentIndex = position;
                break;
            }
        }

        var next = forward
            ? (currentIndex + 1) % ranked.Count
            : (currentIndex <= 0 ? ranked.Count - 1 : currentIndex - 1);
        FocusElement(ranked[next].Element);
    }

    internal static void CollectFocusable(VisualElement element, List<VisualElement> order)
    {
        // 禁用的容器连子树一起跳过：里面的控件即使本地是启用态也不能被 Tab 到。
        if (!element.IsVisible || !element.IsEffectivelyEnabled)
        {
            return;
        }

        if (element.Focusable && element.IsTabStop)
        {
            order.Add(element);
        }

        foreach (var child in element.Children)
        {
            if (child is VisualElement elementChild)
            {
                CollectFocusable(elementChild, order);
            }
        }
    }
}

// 覆盖层只承载弹层：它铺满客户区、自己不取命中，子项位置由 VisualPopup 直接写 Bounds。
internal sealed class OverlayLayer : VisualElement
{
    internal OverlayLayer()
    {
        IsHitTestVisible = false;
    }

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    protected override void ArrangeOverride(Size finalSize)
    {
    }
}
