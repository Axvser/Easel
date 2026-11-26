using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Easel.Controls;
using Easel.Visuals.Controls;
using XamlBrush = Easel.Brush;
using XamlHorizontalAlignment = Easel.Controls.HorizontalAlignment;
using XamlThickness = Easel.Controls.Thickness;
using XamlVerticalAlignment = Easel.Controls.VerticalAlignment;
using XamlVisibility = Easel.Controls.Visibility;

namespace Easel.Visuals;

/// <summary>The relative weight of a typeface, from thin to black.</summary>
/// <remarks>
/// GDI+ exposes only regular and bold faces, so <see cref="SemiBold"/> and heavier compose as bold while lighter
/// weights compose as the base font's regular style.
/// </remarks>
public enum FontWeight
{
    /// <summary>The thinnest weight.</summary>
    Thin,

    /// <summary>An extra-light weight.</summary>
    ExtraLight,

    /// <summary>A light weight.</summary>
    Light,

    /// <summary>The regular weight; the default.</summary>
    Normal,

    /// <summary>A medium weight.</summary>
    Medium,

    /// <summary>A semibold weight; the lightest weight GDI+ paints as bold.</summary>
    SemiBold,

    /// <summary>A bold weight.</summary>
    Bold,

    /// <summary>An extra-bold weight.</summary>
    ExtraBold,

    /// <summary>The heaviest weight.</summary>
    Black,
}

/// <summary>
/// The layout, input and rendering base of the HWND-free visual control library.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="VisualElement"/> is a <see cref="ContainerVisual"/> with a measure/arrange pass, a background and
/// border, pointer and keyboard events, and logical focus. It never creates a window; a <see cref="VisualHost"/>
/// owns the single HWND and paints the whole tree in one pass.
/// </para>
/// <para>
/// <see cref="Visual.Bounds"/> is the arranged rectangle in the parent's coordinates; panels arrange children into
/// local coordinates, and the inherited <see cref="Visual.Render"/> pass translates before drawing content.
/// </para>
/// </remarks>
public abstract class VisualElement : ContainerVisual, IDisposable
{
    /// <summary>The font used by text-bearing elements that do not set one.</summary>
    /// <summary>The fallback font; it follows the active <see cref="Theme"/> palette.</summary>
    public static Font DefaultFont => Theme.Current.DefaultFont;

    /// <summary>The outer spacing around an element.</summary>
    public static readonly DependencyProperty MarginProperty = DependencyProperty.Register(
        nameof(Margin), typeof(XamlThickness), typeof(VisualElement),
        new PropertyMetadata(XamlThickness.Empty) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The inner spacing between an element's edge and its children.</summary>
    public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
        nameof(Padding), typeof(XamlThickness), typeof(VisualElement),
        new PropertyMetadata(XamlThickness.Empty) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The requested width; <see cref="double.NaN"/> sizes to content and alignment.</summary>
    public static readonly DependencyProperty WidthProperty = DependencyProperty.Register(
        nameof(Width), typeof(double), typeof(VisualElement),
        new PropertyMetadata(double.NaN) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The requested height; <see cref="double.NaN"/> sizes to content and alignment.</summary>
    public static readonly DependencyProperty HeightProperty = DependencyProperty.Register(
        nameof(Height), typeof(double), typeof(VisualElement),
        new PropertyMetadata(double.NaN) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The smallest width layout may assign.</summary>
    public static readonly DependencyProperty MinWidthProperty = DependencyProperty.Register(
        nameof(MinWidth), typeof(double), typeof(VisualElement),
        new PropertyMetadata(0d) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The smallest height layout may assign.</summary>
    public static readonly DependencyProperty MinHeightProperty = DependencyProperty.Register(
        nameof(MinHeight), typeof(double), typeof(VisualElement),
        new PropertyMetadata(0d) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The largest width layout may assign.</summary>
    public static readonly DependencyProperty MaxWidthProperty = DependencyProperty.Register(
        nameof(MaxWidth), typeof(double), typeof(VisualElement),
        new PropertyMetadata(double.PositiveInfinity) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The largest height layout may assign.</summary>
    public static readonly DependencyProperty MaxHeightProperty = DependencyProperty.Register(
        nameof(MaxHeight), typeof(double), typeof(VisualElement),
        new PropertyMetadata(double.PositiveInfinity) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>How an element is placed inside its slot horizontally.</summary>
    public static readonly DependencyProperty HorizontalAlignmentProperty = DependencyProperty.Register(
        nameof(HorizontalAlignment), typeof(XamlHorizontalAlignment), typeof(VisualElement),
        new PropertyMetadata(XamlHorizontalAlignment.Stretch) { AffectsArrange = true, AffectsRender = false });

    /// <summary>How an element is placed inside its slot vertically.</summary>
    public static readonly DependencyProperty VerticalAlignmentProperty = DependencyProperty.Register(
        nameof(VerticalAlignment), typeof(XamlVerticalAlignment), typeof(VisualElement),
        new PropertyMetadata(XamlVerticalAlignment.Stretch) { AffectsArrange = true, AffectsRender = false });

    /// <summary>Whether an element is painted, laid out and hit-tested.</summary>
    public static readonly DependencyProperty VisibilityProperty = DependencyProperty.Register(
        nameof(Visibility), typeof(XamlVisibility), typeof(VisualElement),
        new PropertyMetadata(XamlVisibility.Visible) { AffectsMeasure = true });

    /// <summary>Whether an element accepts input and draws an enabled state.</summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.Register(
        nameof(IsEnabled), typeof(bool), typeof(VisualElement),
        new PropertyMetadata(true) { Inherits = true });

    /// <summary>Whether an element participates in the focus chain.</summary>
    public static readonly DependencyProperty FocusableProperty = DependencyProperty.Register(
        nameof(Focusable), typeof(bool), typeof(VisualElement),
        new PropertyMetadata(false) { AffectsRender = false });

    /// <summary>Whether a pointer hit lands on an element's own rectangle.</summary>
    public static readonly DependencyProperty IsHitTestVisibleProperty = DependencyProperty.Register(
        nameof(IsHitTestVisible), typeof(bool), typeof(VisualElement),
        new PropertyMetadata(true) { AffectsRender = false });

    /// <summary>The cursor shown while the pointer is over an element.</summary>
    public static readonly DependencyProperty CursorProperty = DependencyProperty.Register(
        nameof(Cursor), typeof(Cursor), typeof(VisualElement),
        new PropertyMetadata(null) { AffectsRender = false });

    /// <summary>The outline brush drawn around an element's rectangle.</summary>
    public static readonly DependencyProperty BorderBrushProperty = DependencyProperty.Register(
        nameof(BorderBrush), typeof(XamlBrush), typeof(VisualElement),
        new PropertyMetadata(null));

    /// <summary>The outline width per side, in pixels.</summary>
    public static readonly DependencyProperty BorderThicknessProperty = DependencyProperty.Register(
        nameof(BorderThickness), typeof(XamlThickness), typeof(VisualElement),
        new PropertyMetadata(XamlThickness.Empty) { AffectsMeasure = true });

    /// <summary>The background brush painted behind an element's content.</summary>
    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background), typeof(XamlBrush), typeof(VisualElement),
        new PropertyMetadata(null));

    /// <summary>The background and border corner radius in pixels.</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(float), typeof(VisualElement),
        new PropertyMetadata(0f));

    /// <summary>The opacity of this element and its subtree, in the range 0..1.</summary>
    public static readonly DependencyProperty OpacityProperty = DependencyProperty.Register(
        nameof(Opacity), typeof(double), typeof(VisualElement),
        new PropertyMetadata(
            1d,
            null,
            static (_, value) => value is double opacity ? XamlMath.Clamp(opacity, 0d, 1d) : 1d)
        {
            AffectsRender = true,
        });

    /// <summary>The local transform applied after this element's bounds are translated to the origin.</summary>
    public static readonly DependencyProperty TransformProperty = DependencyProperty.Register(
        nameof(Transform), typeof(Matrix), typeof(VisualElement),
        new PropertyMetadata(
            null,
            null,
            static (_, value) => value is Matrix matrix ? matrix : new Matrix())
        {
            AffectsRender = true,
        });

    /// <summary>The optional 3D transform projected into this element's local plane.</summary>
    public static readonly DependencyProperty Transform3DProperty = DependencyProperty.Register(
        nameof(Transform3D), typeof(Transform3D), typeof(VisualElement),
        new PropertyMetadata(null)
        {
            AffectsRender = true,
        });

    /// <summary>An optional brush whose alpha masks this element's composited subtree.</summary>
    public static readonly DependencyProperty OpacityMaskProperty = DependencyProperty.Register(
        nameof(OpacityMask), typeof(Brush), typeof(VisualElement),
        new PropertyMetadata(null)
        {
            AffectsRender = true,
        });

    /// <summary>Whether the opacity mask uses brush alpha or premultiplied luminance.</summary>
    public static readonly DependencyProperty OpacityMaskModeProperty = DependencyProperty.Register(
        nameof(OpacityMaskMode), typeof(OpacityMaskMode), typeof(VisualElement),
        new PropertyMetadata(OpacityMaskMode.Alpha)
        {
            AffectsRender = true,
        });

    /// <summary>An optional clip rectangle in this element's local coordinates.</summary>
    public static readonly DependencyProperty ClipProperty = DependencyProperty.Register(
        nameof(Clip), typeof(RectangleF?), typeof(VisualElement),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>Whether this element's children are painted and hit-tested only inside its bounds.</summary>
    public static readonly DependencyProperty ClipToBoundsProperty = DependencyProperty.Register(
        nameof(ClipToBounds), typeof(bool), typeof(VisualElement),
        new PropertyMetadata(false) { AffectsRender = true });

    /// <summary>An optional effect applied to the rendered subtree.</summary>
    public static readonly DependencyProperty EffectProperty = DependencyProperty.Register(
        nameof(Effect), typeof(VisualEffect), typeof(VisualElement),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>Whether this element's subtree is cached in a bitmap between frames.</summary>
    public static readonly DependencyProperty CacheAsBitmapProperty = DependencyProperty.Register(
        nameof(CacheAsBitmap), typeof(bool), typeof(VisualElement),
        new PropertyMetadata(false) { AffectsRender = true });

    /// <summary>The font size in points, inherited from ancestors; <see cref="double.NaN"/> derives it from <c>Font</c>.</summary>
    public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(
        nameof(FontSize), typeof(double), typeof(VisualElement),
        new PropertyMetadata(double.NaN) { Inherits = true, AffectsMeasure = true, AffectsRender = true });

    /// <summary>The font family name, inherited from ancestors; <see langword="null"/> derives it from <c>Font</c>.</summary>
    public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.Register(
        nameof(FontFamily), typeof(string), typeof(VisualElement),
        new PropertyMetadata(null) { Inherits = true, AffectsMeasure = true, AffectsRender = true });

    /// <summary>The relative typeface weight, inherited from ancestors; <see cref="FontWeight.Normal"/> derives it from <c>Font</c>.</summary>
    public static readonly DependencyProperty FontWeightProperty = DependencyProperty.Register(
        nameof(FontWeight), typeof(FontWeight), typeof(VisualElement),
        new PropertyMetadata(FontWeight.Normal) { Inherits = true, AffectsMeasure = true, AffectsRender = true });

    /// <summary>The order in which <see cref="Keys.Tab"/> visits focusable elements; lower values come first.</summary>
    public static readonly DependencyProperty TabIndexProperty = DependencyProperty.Register(
        nameof(TabIndex), typeof(int), typeof(VisualElement),
        new PropertyMetadata(0) { AffectsRender = false });

    /// <summary>Whether <see cref="Keys.Tab"/> stops at this element when it is focusable.</summary>
    public static readonly DependencyProperty IsTabStopProperty = DependencyProperty.Register(
        nameof(IsTabStop), typeof(bool), typeof(VisualElement),
        new PropertyMetadata(true) { AffectsRender = false });

    /// <summary>Whether this element accepts drag-and-drop data from the pointer.</summary>
    public static readonly DependencyProperty AllowDropProperty = DependencyProperty.Register(
        nameof(AllowDrop), typeof(bool), typeof(VisualElement),
        new PropertyMetadata(false) { AffectsRender = false });

    /// <summary>The inherited data context used by bindings and templates.</summary>
    public static readonly DependencyProperty DataContextProperty = DependencyProperty.Register(
        nameof(DataContext), typeof(object), typeof(VisualElement),
        new PropertyMetadata(null) { Inherits = true, AffectsRender = false });

    /// <summary>The control whose template created this element, or <see langword="null"/> outside a template.</summary>
    public static readonly DependencyProperty TemplatedParentProperty = DependencyProperty.Register(
        nameof(TemplatedParent), typeof(VisualElement), typeof(VisualElement),
        new PropertyMetadata(null) { AffectsRender = false });

    private bool _measureValid;
    private bool _arrangeValid;
    private Size _measureAvailable;
    private Size _desiredSize;

    // ScrollContentPresenter 用这个有限宽探测可横向滚动的内容（其私有常量是 100_000）。
    // 内容原样报回该宽会被当成“填满”而按视口重测；按不设限处理，星号轨道就退回内容尺寸。
    internal const int UnboundedMeasureProbe = 100_000;

    /// <summary>When true, layout changes inside this element stop here; its parent slot is assumed fixed.</summary>
    public bool IsLayoutBoundary { get; set; }

    internal Size MeasureAvailable => _measureAvailable;

    // 布局缓存的复用前提：Measure 之后没有被 InvalidateMeasure 推翻。Grid 用它判断能否跳过重测。
    internal bool IsMeasureValid => _measureValid;
    private VisualHost? _surface;
    private bool _focused;
    private bool _focusVisible;
    private bool _pointerOver;
    private VisualStyle? _explicitStyle;
    private VisualStyle? _implicitStyle;
    private VisualStyleApplication? _styleApplication;
    private VisualStyleCollection? _styles;
    private ResourceDictionary? _resources;
    private VisualNameScope? _templateScope;
    private VisualNameScope? _instanceScope;
    private List<IDisposable>? _resourceBindings;
    private Dictionary<object, ResourceBinding>? _resourceBindingsByTarget;
    private Font? _resolvedFont;
    private Font? _resolvedFontBase;
    private float _resolvedFontSize;
    private string? _resolvedFontFamily;
    private bool _resolvedFontWeighted;
    private bool _resolvedFontBold;
    private List<ThemeMetric>? _themeMetrics;
    private bool _themeMetricsSubscribed;

    // 主题度量必须单占一层：与 Style 同层会在样式卸载时被一并清掉；Inherited(4) 让本地值、
    // 样式触发器、模板与样式 setter 都排在它前面，主题只做兜底。
    private const BindingPriority ThemeMetricPriority = BindingPriority.Inherited;

    /// <summary>The outer spacing around this element.</summary>
    public XamlThickness Margin
    {
        get => (XamlThickness)GetValue(MarginProperty)!;
        set => SetValue(MarginProperty, value);
    }

    /// <summary>The inner spacing between this element's edge and its children.</summary>
    public XamlThickness Padding
    {
        get => (XamlThickness)GetValue(PaddingProperty)!;
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>The requested width; <see cref="double.NaN"/> sizes to content and alignment.</summary>
    public double Width
    {
        get => (double)GetValue(WidthProperty)!;
        set => SetValue(WidthProperty, value);
    }

    /// <summary>The requested height; <see cref="double.NaN"/> sizes to content and alignment.</summary>
    public double Height
    {
        get => (double)GetValue(HeightProperty)!;
        set => SetValue(HeightProperty, value);
    }

    /// <summary>The smallest width layout may assign.</summary>
    public double MinWidth
    {
        get => (double)GetValue(MinWidthProperty)!;
        set => SetValue(MinWidthProperty, value);
    }

    /// <summary>The smallest height layout may assign.</summary>
    public double MinHeight
    {
        get => (double)GetValue(MinHeightProperty)!;
        set => SetValue(MinHeightProperty, value);
    }

    /// <summary>The largest width layout may assign.</summary>
    public double MaxWidth
    {
        get => (double)GetValue(MaxWidthProperty)!;
        set => SetValue(MaxWidthProperty, value);
    }

    /// <summary>The largest height layout may assign.</summary>
    public double MaxHeight
    {
        get => (double)GetValue(MaxHeightProperty)!;
        set => SetValue(MaxHeightProperty, value);
    }

    /// <summary>How this element is placed inside its slot horizontally.</summary>
    public XamlHorizontalAlignment HorizontalAlignment
    {
        get => (XamlHorizontalAlignment)GetValue(HorizontalAlignmentProperty)!;
        set => SetValue(HorizontalAlignmentProperty, value);
    }

    /// <summary>How this element is placed inside its slot vertically.</summary>
    public XamlVerticalAlignment VerticalAlignment
    {
        get => (XamlVerticalAlignment)GetValue(VerticalAlignmentProperty)!;
        set => SetValue(VerticalAlignmentProperty, value);
    }

    /// <summary>Whether this element is painted, laid out and hit-tested.</summary>
    public XamlVisibility Visibility
    {
        get => (XamlVisibility)GetValue(VisibilityProperty)!;
        set => SetValue(VisibilityProperty, value);
    }

    /// <summary>Whether this element accepts input and draws an enabled state.</summary>
    public bool IsEnabled
    {
        get => (bool)GetValue(IsEnabledProperty)!;
        set => SetValue(IsEnabledProperty, value);
    }

    /// <summary>Whether this element participates in the focus chain.</summary>
    public bool Focusable
    {
        get => (bool)GetValue(FocusableProperty)!;
        set => SetValue(FocusableProperty, value);
    }

    /// <summary>Whether a pointer hit lands on this element's own rectangle.</summary>
    /// <remarks>Panels set this to <see langword="false"/> when they have no background so content clicks pass through.</remarks>
    // 模板内容里的装饰部件用 IsHitTestVisible=false 关掉自身命中；子元素仍可命中（内容区要能接收输入）。
    internal bool IsTemplateContent { get; private set; }

    // 模板实例一次走完两件事：认施模板的控件、标模板内容。以前是两次全树递归，模板节点每建一次走两遍。
    internal void InitializeTemplateContent(VisualElement? parent)
    {
        if (TemplatedParent is null || ReferenceEquals(TemplatedParent, parent))
        {
            TemplatedParent = parent;
        }

        IsTemplateContent = true;
        // Children 是 IList<Visual>；按索引走，避免 foreach 装箱结构体枚举器。
        var children = Children;
        for (var index = 0; index < children.Count; index++)
        {
            if (children[index] is VisualElement element)
            {
                element.InitializeTemplateContent(parent);
            }
        }
    }

    public bool IsHitTestVisible
    {
        get => (bool)GetValue(IsHitTestVisibleProperty)!;
        set => SetValue(IsHitTestVisibleProperty, value);
    }

    /// <summary>The cursor shown while the pointer is over this element, or <see langword="null"/> for the default.</summary>
    public Cursor? Cursor
    {
        get => (Cursor?)GetValue(CursorProperty)!;
        set => SetValue(CursorProperty, value);
    }

    /// <summary>Whether this element accepts drag-and-drop data from the pointer.</summary>
    /// <remarks>
    /// A <see cref="VisualHost"/> routes an OS drag to the deepest hit element with this flag set, then bubbles
    /// <see cref="DragEnter"/>, <see cref="DragOver"/>, <see cref="DragLeave"/> and <see cref="Drop"/> through its
    /// ancestors. Set it on a panel or a border to make the whole subtree a drop surface.
    /// </remarks>
    public bool AllowDrop
    {
        get => (bool)GetValue(AllowDropProperty)!;
        set => SetValue(AllowDropProperty, value);
    }

    /// <summary>The outline brush drawn around the element's rectangle.</summary>
    public XamlBrush? BorderBrush
    {
        get => (XamlBrush?)GetValue(BorderBrushProperty)!;
        set => SetValue(BorderBrushProperty, value);
    }

    /// <summary>The outline width per side, in pixels.</summary>
    public XamlThickness BorderThickness
    {
        get => (XamlThickness)GetValue(BorderThicknessProperty)!;
        set => SetValue(BorderThicknessProperty, value);
    }

    /// <summary>The background brush painted behind the element's content.</summary>
    public new XamlBrush? Background
    {
        get => (XamlBrush?)GetValue(BackgroundProperty)!;
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>The background and border corner radius in pixels.</summary>
    public new float CornerRadius
    {
        get => (float)GetValue(CornerRadiusProperty)!;
        set => SetValue(CornerRadiusProperty, value);
    }

    // 未写本地值时返回每元素独立的单位矩阵，避免多个元素共享同一个可变的 Matrix 默认值。
    private Matrix? _identityTransform;

    /// <inheritdoc />
    public override Matrix Transform
    {
        get => GetValue(TransformProperty) as Matrix ?? (_identityTransform ??= new Matrix());
        set => SetValue(TransformProperty, value);
    }

    internal override bool HasTransform => GetValue(TransformProperty) is not null;

    /// <inheritdoc />
    public override Transform3D? Transform3D
    {
        get => (Transform3D?)GetValue(Transform3DProperty);
        set => SetValue(Transform3DProperty, value);
    }

    /// <inheritdoc />
    public override Brush? OpacityMask
    {
        get => (Brush?)GetValue(OpacityMaskProperty);
        set => SetValue(OpacityMaskProperty, value);
    }

    /// <inheritdoc />
    public override OpacityMaskMode OpacityMaskMode
    {
        get => (OpacityMaskMode)GetValue(OpacityMaskModeProperty)!;
        set => SetValue(OpacityMaskModeProperty, value);
    }

    /// <inheritdoc />
    public override double Opacity
    {
        get => (double)GetValue(OpacityProperty)!;
        set => SetValue(OpacityProperty, value);
    }

    /// <inheritdoc />
    public override RectangleF? Clip
    {
        get => (RectangleF?)GetValue(ClipProperty);
        set => SetValue(ClipProperty, value);
    }

    /// <inheritdoc />
    public override bool ClipToBounds
    {
        get => (bool)GetValue(ClipToBoundsProperty)!;
        set => SetValue(ClipToBoundsProperty, value);
    }

    /// <inheritdoc />
    public override VisualEffect? Effect
    {
        get => (VisualEffect?)GetValue(EffectProperty);
        set => SetValue(EffectProperty, value);
    }

    /// <inheritdoc />
    public override bool CacheAsBitmap
    {
        get => (bool)GetValue(CacheAsBitmapProperty)!;
        set => SetValue(CacheAsBitmapProperty, value);
    }

    /// <summary>The font size in points, inherited from ancestors; <see cref="double.NaN"/> derives it from the control font.</summary>
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty)!;
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>The font family name, inherited from ancestors; <see langword="null"/> derives it from the control font.</summary>
    public string? FontFamily
    {
        get => (string?)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>The relative typeface weight, inherited from ancestors; <see cref="FontWeight.Normal"/> derives it from the control font.</summary>
    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty)!;
        set => SetValue(FontWeightProperty, value);
    }

    /// <summary>The order in which <see cref="Keys.Tab"/> visits focusable elements; lower values come first.</summary>
    public int TabIndex
    {
        get => (int)GetValue(TabIndexProperty)!;
        set => SetValue(TabIndexProperty, value);
    }

    /// <summary>Whether <see cref="Keys.Tab"/> stops at this element when it is focusable.</summary>
    public bool IsTabStop
    {
        get => (bool)GetValue(IsTabStopProperty)!;
        set => SetValue(IsTabStopProperty, value);
    }

    /// <summary>Whether this element and every ancestor are enabled.</summary>
    /// <remarks>Use it when deciding whether a subtree may receive input or focus; <see cref="IsEnabled"/> alone ignores disabled ancestors.</remarks>
    public bool IsEffectivelyEnabled
        => IsEnabled && (ParentElement?.IsEffectivelyEnabled ?? true);

    /// <summary>A caller-facing identifier, or <see langword="null"/> when none was assigned.</summary>
    /// <remarks>
    /// The visual backend has no <c>NameScope</c> for ordinary elements, so this is a plain label used by the gallery
    /// inspector and tree dumps. Template parts are tracked by <see cref="VisualNameScope"/> instead.
    /// </remarks>
    public string? Name { get; set; }

    /// <summary>The inherited data context used by bindings and templates.</summary>
    public object? DataContext
    {
        get => GetValue(DataContextProperty);
        set => SetValue(DataContextProperty, value);
    }

    /// <summary>The control whose template created this element, or <see langword="null"/> outside a template.</summary>
    public VisualElement? TemplatedParent
    {
        get => (VisualElement?)GetValue(TemplatedParentProperty);
        set => SetValue(TemplatedParentProperty, value);
    }

    /// <summary>Resources offered to this element and its descendants by name.</summary>
    /// <remarks>
    /// A lookup checks this dictionary first, then each ancestor's, then <see cref="VisualHost.Resources"/>.
    /// Style resources are ordinary entries; <see cref="VisualResourceLookup.Get{T}"/> converts a stored string to
    /// the requested type.
    /// </remarks>
    public ResourceDictionary Resources
    {
        get
        {
            if (_resources is { } existing)
            {
                return existing;
            }

            var resources = new ResourceDictionary();
            _resources = resources;
            // 动态资源在字典出现前不订阅它；第一次真正拿到字典时补一次解析。
            NotifyResourcesCreated();
            return resources;
        }
    }

    private void NotifyResourcesCreated()
    {
        var bindings = _resourceBindings;
        if (bindings is null)
        {
            return;
        }

        for (var index = 0; index < bindings.Count; index++)
        {
            if (bindings[index] is ResourceBinding binding)
            {
                binding.OnOwnerResourcesCreated();
            }
        }
    }

    // 非创建访问器：资源探测不应给每个模板元素永久挂上空字典。
    internal ResourceDictionary? ResourcesOrNull => _resources;

    /// <summary>The size this element wants inside <paramref name="availableSize"/>.</summary>
    public Size DesiredSize => _desiredSize;

    /// <summary>Whether this element currently has logical focus inside its surface.</summary>
    public bool IsFocused => _focused;

    /// <summary>Whether focus was established by the keyboard, so the focus ring should be painted.</summary>
    /// <remarks>
    /// A pointer click focuses the control it lands on but does not raise this flag; it clears on the next pointer
    /// press. This keeps the visual focus ring meaningful without drawing a blue box after every mouse click.
    /// </remarks>
    public bool IsFocusVisible => _focused && _focusVisible;

    /// <summary>Whether the pointer is over this element or one of its descendants.</summary>
    public bool IsPointerOver => _pointerOver;

    /// <summary>Raised after this element is attached to or detached from a parent.</summary>
    public event EventHandler? ParentChanged;

    // 资源绑定在每个父链元素上挂一次观察；共享列表代替多播事件，避免增删时重建委托数组。
    private List<Action>? _parentChangedObservers;

    internal void AddParentChangedObserver(Action observer)
        => (_parentChangedObservers ??= new List<Action>()).Add(observer);

    internal void RemoveParentChangedObserver(Action observer)
        => _parentChangedObservers?.Remove(observer);

    private void RaiseParentChangedObservers()
    {
        var observers = _parentChangedObservers;
        if (observers is null)
        {
            return;
        }

        // 观察者在回调里可能增删链上其它元素（不一定是本元素）的订阅；倒序并复查边界。
        for (var index = observers.Count - 1; index >= 0; index--)
        {
            if (index < observers.Count)
            {
                observers[index]();
            }
        }
    }

    /// <summary>The element that owns this one, or <see langword="null"/> for a root.</summary>
    public VisualElement? ParentElement => Parent as VisualElement;

    /// <inheritdoc />
    protected override void OnParentChanged(ContainerVisual? oldParent, ContainerVisual? newParent)
    {
        base.OnParentChanged(oldParent, newParent);
        ParentChanged?.Invoke(this, EventArgs.Empty);
        RaiseParentChangedObservers();

        // 换父级会让整个子树重新继承 DataContext；绑定只监听本元素的 DataContext 变化，
        // 预构造的子树（x:Class 视图里绑在嵌套面板上的按钮就是这种）挂到有上下文的父级后，
        // 不补这一跳就会一直停在空源上。
        if (!IsSet(DataContextProperty))
        {
            var oldContext = (oldParent as VisualElement)?.DataContext;
            var newContext = (newParent as VisualElement)?.DataContext;
            if (!ReferenceEquals(oldContext, newContext))
            {
                RaisePropertyChanged(DataContextProperty, oldContext, newContext, BindingPriority.Inherited);
            }
        }

        // 只有前后父级解析出的宿主不同才需要走整棵子树；同宿主内换父级是常见路径，不做无谓遍历。
        var oldSurface = (oldParent as VisualElement)?.Surface;
        var newSurface = (newParent as VisualElement)?.Surface;
        if (!ReferenceEquals(oldSurface, newSurface))
        {
            SyncHostBehaviorsRecursive();
        }
    }

    /// <summary>The surface that hosts this element, resolved through the parent chain.</summary>
    public VisualHost? Surface => _surface ?? ParentElement?.Surface;

    /// <summary>Registers a behavior that needs the hosting <see cref="VisualHost"/>.</summary>
    /// <param name="behavior">The behavior to attach when this element resolves a surface.</param>
    /// <remarks>
    /// A behavior added before the element is parented is attached as soon as <see cref="Surface"/> becomes
    /// non-null. Adding the same instance twice has no effect.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="behavior"/> is <see langword="null"/>.</exception>
    public void AddHostBehavior(IVisualHostBehavior behavior)
    {
        if (behavior is null) throw new ArgumentNullException(nameof(behavior));
        _hostBehaviors ??= [];
        if (_hostBehaviors.Contains(behavior))
        {
            return;
        }

        _hostBehaviors.Add(behavior);
        SyncHostBehavior(behavior);
    }

    /// <summary>Unregisters a host behavior and detaches it if it was attached.</summary>
    /// <param name="behavior">The behavior to remove.</param>
    public void RemoveHostBehavior(IVisualHostBehavior behavior)
    {
        if (behavior is null || _hostBehaviors is null || !_hostBehaviors.Remove(behavior))
        {
            return;
        }

        DetachHostBehavior(behavior);
    }

    /// <summary>The DPI used to measure text.</summary>
    /// <remarks>
    /// Always 96: the visual tree is laid out in logical units and the surface applies the monitor scale once when
    /// painting. Reporting the physical control DPI here would make a point font both measure and render at the
    /// high DPI, then get scaled a second time by the world transform.
    /// </remarks>
    protected int RenderDpi => 96;

    /// <summary>Composes <paramref name="font"/> with the inherited typography properties.</summary>
    /// <param name="font">The control's base font, used for every field the typography properties leave unset.</param>
    /// <returns>The base font when no typography override applies; otherwise a cached derived font owned by this element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="font"/> is <see langword="null"/>.</exception>
    public Font ResolveFont(Font font)
    {
        if (font is null) throw new ArgumentNullException(nameof(font));

        var size = FontSize;
        var family = FontFamily;
        var weight = FontWeight;
        var hasSize = !double.IsNaN(size) && size > 0d;
        var hasFamily = !string.IsNullOrEmpty(family);
        var hasWeight = weight != FontWeight.Normal;
        if (!hasSize && !hasFamily && !hasWeight)
        {
            return font;
        }

        var targetSize = hasSize ? (float)size : font.Size;
        var targetFamily = hasFamily ? family! : font.FontFamily.Name;
        var bold = weight >= FontWeight.SemiBold;
        var style = hasWeight
            ? (bold ? font.Style | FontStyle.Bold : font.Style & ~FontStyle.Bold)
            : font.Style;

        if (_resolvedFont is not null
            && ReferenceEquals(_resolvedFontBase, font)
            && Math.Abs(_resolvedFontSize - targetSize) < 0.001f
            && string.Equals(_resolvedFontFamily, targetFamily, StringComparison.Ordinal)
            && _resolvedFontWeighted == hasWeight
            && _resolvedFontBold == bold)
        {
            return _resolvedFont;
        }

        _resolvedFont?.Dispose();
        _resolvedFont = new Font(
            targetFamily,
            targetSize,
            style,
            font.Unit,
            font.GdiCharSet,
            font.GdiVerticalFont);
        _resolvedFontBase = font;
        _resolvedFontSize = targetSize;
        _resolvedFontFamily = targetFamily;
        _resolvedFontWeighted = hasWeight;
        _resolvedFontBold = bold;
        return _resolvedFont;
    }

    /// <summary>The style explicitly assigned to this element, or <see langword="null"/>.</summary>
    /// <remarks>
    /// An explicit style takes precedence over any implicit style resolved from a <see cref="Styles"/> collection or
    /// <see cref="VisualHost.Styles"/>. Assigning <see langword="null"/> re-applies the implicit style, if any.
    /// </remarks>
    public VisualStyle? Style
    {
        get => _explicitStyle;
        set
        {
            if (ReferenceEquals(_explicitStyle, value))
            {
                return;
            }

            _explicitStyle = value;
            ApplyEffectiveStyle();
        }
    }

    /// <summary>Styles offered implicitly to this element and its descendants.</summary>
    /// <remarks>
    /// The nearest collection wins: this element's own collection is consulted first, then each ancestor's, then
    /// <see cref="VisualHost.Styles"/>. Within a collection the first style whose
    /// <see cref="VisualStyle.TargetType"/> accepts the element is used. The subtree is re-resolved when this
    /// collection changes, when a child is added through <see cref="Add{T}"/>, and when the surface root is assigned
    /// or replaced.
    /// </remarks>
    public VisualStyleCollection Styles => _styles ??= CreateStyles();

    /// <summary>Raised after the pointer is pressed on this element or bubbles from a child.</summary>
    public event EventHandler<VisualPointerEventArgs>? PointerPressed;

    /// <summary>Raised from the root down to the hit element before <see cref="PointerPressed"/>.</summary>
    public event EventHandler<VisualPointerEventArgs>? PreviewPointerPressed;

    /// <summary>Raised after the pointer moves over this element or bubbles from a child.</summary>
    public event EventHandler<VisualPointerEventArgs>? PointerMoved;

    /// <summary>Raised after the pointer is released on this element or bubbles from a child.</summary>
    public event EventHandler<VisualPointerEventArgs>? PointerReleased;

    /// <summary>Raised when the pointer enters this element.</summary>
    public event EventHandler<VisualPointerEventArgs>? PointerEntered;

    /// <summary>Raised when the pointer leaves this element.</summary>
    public event EventHandler<VisualPointerEventArgs>? PointerExited;

    /// <summary>Raised when the wheel turns over this element or bubbles from a child.</summary>
    public event EventHandler<VisualPointerEventArgs>? PointerWheel;

    /// <summary>Raised when a drag enters this element or bubbles from a child.</summary>
    public event EventHandler<VisualDragEventArgs>? DragEnter;

    /// <summary>Raised while a drag moves over this element or bubbles from a child.</summary>
    public event EventHandler<VisualDragEventArgs>? DragOver;

    /// <summary>Raised when a drag leaves this element or bubbles from a child.</summary>
    public event EventHandler<VisualDragEventArgs>? DragLeave;

    /// <summary>Raised when the user releases a drag over this element or bubbles from a child.</summary>
    public event EventHandler<VisualDragEventArgs>? Drop;

    /// <summary>Raised when a key goes down on this element or bubbles from a child.</summary>
    public event EventHandler<VisualKeyEventArgs>? KeyDown;

    /// <summary>Raised when a key goes up on this element or bubbles from a child.</summary>
    public event EventHandler<VisualKeyEventArgs>? KeyUp;

    /// <summary>Raised when text input reaches the focused element.</summary>
    public event EventHandler<VisualTextInputEventArgs>? TextInput;

    /// <summary>Raised after this element receives logical focus.</summary>
    public event EventHandler? GotFocus;

    /// <summary>Raised after this element loses logical focus.</summary>
    public event EventHandler? LostFocus;

    /// <summary>Adds a child element, applies the implicit styles it inherits, clears a stale root surface and invalidates layout.</summary>
    /// <param name="child">The child to add.</param>
    /// <typeparam name="T">The child's concrete type.</typeparam>
    /// <returns>The added child.</returns>
    public T Add<T>(T child) where T : VisualElement
    {
        if (child is null) throw new ArgumentNullException(nameof(child));
        child._surface = null;
        base.Add(child);
        child.RefreshImplicitStyles();
        if (Surface is not null)
        {
            child.NotifySurfaceRootChanged();
        }

        InvalidateMeasure();
        return child;
    }

    /// <summary>Adds a child element and returns this element for chaining.</summary>
    /// <param name="child">The child to add.</param>
    /// <returns>This element.</returns>
    public VisualElement AddChild(VisualElement child)
    {
        Add(child);
        return this;
    }

    /// <summary>Re-resolves the implicit style from the current ancestor chain and applies it to this subtree.</summary>
    /// <remarks>
    /// <see cref="Add{T}"/> does this for a child appended at the end. A host that inserts a child at a specific
    /// position through the raw <see cref="Children"/> collection calls this to get the same styling.
    /// </remarks>
    public void RefreshStyle() => RefreshImplicitStyles();

    /// <summary>Removes a child element.</summary>
    /// <param name="child">The child to remove.</param>
    /// <returns><see langword="true"/> when the child was present.</returns>
    public bool RemoveChild(VisualElement child)
    {
        if (Surface?.FocusedElement is { } focused && IsAncestorOf(focused))
        {
            Surface.FocusElement(null);
        }

        if (!Remove(child))
        {
            return false;
        }

        child._surface = null;
        child.RefreshImplicitStyles();
        child.NotifySurfaceRootChanged();
        InvalidateMeasure();
        return true;
    }

    // 被移除的子树里可能正握着逻辑焦点；先交还焦点，TextBox 的插入符计时器才会停。
    private bool IsAncestorOf(VisualElement element)
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

    /// <summary>Measures this element inside <paramref name="availableSize"/> and caches the result.</summary>
    /// <param name="availableSize">The room the parent can offer.</param>
    /// <returns>The desired size including the margin.</returns>
    public Size Measure(Size availableSize)
    {
        if (_measureValid && _measureAvailable == availableSize)
        {
            return _desiredSize;
        }

        if (Visibility == XamlVisibility.Collapsed)
        {
            _measureValid = true;
            _measureAvailable = availableSize;
            _desiredSize = Size.Empty;
            return _desiredSize;
        }

        var margin = Margin;
        var innerAvailable = new Size(
            Math.Max(0, availableSize.Width - (int)Math.Ceiling(margin.Horizontal)),
            Math.Max(0, availableSize.Height - (int)Math.Ceiling(margin.Vertical)));

        // 显式尺寸是内容可用的上限：固定宽的 Border/StackPanel 必须把宽度传给子项，
        // 否则子项先按父级的宽量成一行，换行高度在 Arrange 时才需要，已经被裁掉。
        var explicitWidth = (double)GetValue(WidthProperty)!;
        var explicitHeight = (double)GetValue(HeightProperty)!;
        if (!double.IsNaN(explicitWidth) && explicitWidth >= 0d)
        {
            innerAvailable.Width = (int)Math.Ceiling(explicitWidth);
        }

        if (!double.IsNaN(explicitHeight) && explicitHeight >= 0d)
        {
            innerAvailable.Height = (int)Math.Ceiling(explicitHeight);
        }

        var desired = MeasureOverride(innerAvailable);
        var width = ResolveAxis(Width, desired.Width, MinWidth, MaxWidth);
        var height = ResolveAxis(Height, desired.Height, MinHeight, MaxHeight);
        _desiredSize = new Size(
            (int)Math.Ceiling(width + margin.Horizontal),
            (int)Math.Ceiling(height + margin.Vertical));
        _measureAvailable = availableSize;
        _measureValid = true;
        _arrangeValid = false;
        return _desiredSize;
    }

    /// <summary>Arranges this element inside <paramref name="finalRect"/>, in the parent's coordinates.</summary>
    /// <param name="finalRect">The slot the parent assigns, including this element's margin.</param>
    public void Arrange(RectangleF finalRect)
    {
        if (Visibility == XamlVisibility.Collapsed)
        {
            Bounds = RectangleF.Empty;
            _arrangeValid = false;
            return;
        }

        var margin = Margin;
        var slot = new RectangleF(
            finalRect.X + margin.Left,
            finalRect.Y + margin.Top,
            Math.Max(0f, finalRect.Width - margin.Horizontal),
            Math.Max(0f, finalRect.Height - margin.Vertical));

        var desired = new Size(
            Math.Max(0, _desiredSize.Width - (int)Math.Ceiling(margin.Horizontal)),
            Math.Max(0, _desiredSize.Height - (int)Math.Ceiling(margin.Vertical)));
        var width = ResolveArranged(Width, desired.Width, MinWidth, MaxWidth, slot.Width, HorizontalAlignment == XamlHorizontalAlignment.Stretch);
        var height = ResolveArranged(Height, desired.Height, MinHeight, MaxHeight, slot.Height, VerticalAlignment == XamlVerticalAlignment.Stretch);

        var x = HorizontalAlignment switch
        {
            XamlHorizontalAlignment.Right => slot.Right - width,
            XamlHorizontalAlignment.Center => slot.X + ((slot.Width - width) / 2f),
            XamlHorizontalAlignment.Stretch when !double.IsNaN(Width) => slot.X + ((slot.Width - width) / 2f),
            _ => slot.X,
        };
        var y = VerticalAlignment switch
        {
            XamlVerticalAlignment.Bottom => slot.Bottom - height,
            XamlVerticalAlignment.Center => slot.Y + ((slot.Height - height) / 2f),
            XamlVerticalAlignment.Stretch when !double.IsNaN(Height) => slot.Y + ((slot.Height - height) / 2f),
            _ => slot.Y,
        };

        var sizeChanged = Math.Abs(Bounds.Width - width) > 0.01f || Math.Abs(Bounds.Height - height) > 0.01f;
        Bounds = new RectangleF(x, y, Math.Max(0f, width), Math.Max(0f, height));
        if (sizeChanged || !_arrangeValid)
        {
            _arrangeValid = true;
            ArrangeOverride(new Size((int)Math.Ceiling(width), (int)Math.Ceiling(height)));
        }
    }

    /// <summary>Whether a property change must invalidate the measure chain (fixed-size text can skip it).</summary>
    protected virtual bool ShouldInvalidateMeasure(DependencyPropertyChangedEventArgs e) => true;

    /// <summary>Marks this element and its ancestors as needing a new measure pass.</summary>
    public void InvalidateMeasure()
    {
        _measureValid = false;
        _arrangeValid = false;

        if (IsLayoutBoundary)
        {
            Surface?.NotifyBoundaryLayoutInvalidated(this, frameFull: true);
            return;
        }

        for (var parent = ParentElement; parent is not null; parent = parent.ParentElement)
        {
            parent._measureValid = false;
            parent._arrangeValid = false;
            if (parent.IsLayoutBoundary)
            {
                Surface?.NotifyBoundaryLayoutInvalidated(parent, frameFull: true);
                return;
            }
        }

        Surface?.InvalidateSurface();
    }

    /// <summary>Marks this element and its ancestors as needing a new arrange pass.</summary>
    /// <remarks>
    /// Measure results stay valid: an arrange-only change (a canvas coordinate, an alignment) does not change a
    /// child's desired size, and re-measuring the whole tree is what made a node drag re-lay-out every card per move.
    /// A size that actually changes still re-measures through <see cref="Measure"/>'s available-size comparison.
    /// </remarks>
    public void InvalidateArrange() => InvalidateArrangeCore(frameFull: true);

    // 只是移动了某个已定位子项：窗口照旧整面重画，帧缓存可以只记局部脏区。
    internal void InvalidateArrangeLocal() => InvalidateArrangeCore(frameFull: false);

    private void InvalidateArrangeCore(bool frameFull)
    {
        _arrangeValid = false;

        if (IsLayoutBoundary)
        {
            Surface?.NotifyBoundaryLayoutInvalidated(this, frameFull);
            return;
        }

        for (var parent = ParentElement; parent is not null; parent = parent.ParentElement)
        {
            parent._arrangeValid = false;
            if (parent.IsLayoutBoundary)
            {
                Surface?.NotifyBoundaryLayoutInvalidated(parent, frameFull);
                return;
            }
        }

        Surface?.NotifyArrangeInvalidated(frameFull);
    }

    /// <summary>Requests a repaint of the hosting surface without a new layout pass.</summary>
    public void InvalidateVisual()
        => InvalidateVisual(GetInvalidationBounds());

    /// <summary>Marks a local rectangle dirty, transformed through the ancestor bounds and transforms.</summary>
    /// <param name="localBounds">The rectangle in this element's local coordinates.</param>
    public void InvalidateVisual(RectangleF localBounds)
    {
        // 最近的位图缓存要重画；InvalidateCache 会把外层缓存也一并标脏。
        VisualElement? cached = null;
        for (var current = this; current is not null; current = current.ParentElement)
        {
            if (current.CacheAsBitmap)
            {
                current.InvalidateCache();
                cached = current;
                break;
            }
        }

        if (Surface is not { } surface)
        {
            return;
        }

        // 缓存节点重画时可能同时刷新外层缓存；直接整面重画，避免漏掉更大的缓存区域。
        if (cached is not null)
        {
            surface.InvalidatePaint();
            return;
        }

        if (localBounds.Width <= 0f || localBounds.Height <= 0f)
        {
            surface.InvalidatePaint();
            return;
        }

        var damage = GetAbsoluteBounds(localBounds);

        // AbsoluteBounds 已经过祖先的 Transform；只有位图缓存和 Effect 越界需要整面/额外外扩。
        // 3D 投影按分格贴图，边缘会因双线性采样再多出一点。
        var margin = 2f + (Effect?.Padding ?? 0f)
                    + (Transform3D is { IsIdentity: false } ? 8f : 0f)
                    + (OpacityMask is not null ? 2f : 0f);
        damage.Inflate(margin, margin);
        // 带上失效来源：整树平移的帧里，只有普通根自身的整块失效才能当成变换的附带结果。
        surface.InvalidatePaint(damage, this);
    }

    /// <summary>Moves logical focus to this element.</summary>
    /// <returns><see langword="true"/> when the surface accepted the focus.</returns>
    public bool Focus() => Surface?.FocusElement(this) ?? false;

    /// <summary>Converts a surface point into this element's local coordinates.</summary>
    /// <param name="surfacePoint">A point in the surface's client coordinates.</param>
    public PointF PointFromSurface(PointF surfacePoint)
    {
        // Render walks leaf → root: 3D, Transform, then Bounds. The inverse therefore has to undo the root first;
        // doing it leaf-first scales the ancestors' Bounds translation again and drifts at every zoom.
        var chain = new List<VisualElement>();
        for (var current = this; current is not null; current = current.ParentElement)
        {
            chain.Add(current);
        }

        var point = surfacePoint;
        for (var index = chain.Count - 1; index >= 0; index--)
        {
            var current = chain[index];
            point.X -= current.Bounds.X;
            point.Y -= current.Bounds.Y;
            if (current.HasTransform)
            {
                using var inverse = current.Transform.Clone();
                inverse.Invert();
                var points = new[] { point };
                inverse.TransformPoints(points);
                point = points[0];
            }

            if (current.Transform3D is { IsIdentity: false } transform3D)
            {
                point = new Projection3D(
                    new RectangleF(0f, 0f, current.Bounds.Width, current.Bounds.Height),
                    transform3D).Unmap(point);
            }
        }

        return point;
    }

    /// <summary>Converts this element's local point into the surface's client coordinates.</summary>
    /// <param name="localPoint">A point in this element's local coordinates.</param>
    public PointF PointToSurface(PointF localPoint)
    {
        var point = localPoint;
        for (var current = this; current is not null; current = current.ParentElement)
        {
            if (current.Transform3D is { IsIdentity: false } transform3D)
            {
                point = new Projection3D(
                    new RectangleF(0f, 0f, current.Bounds.Width, current.Bounds.Height),
                    transform3D).Map(point);
            }

            if (current.HasTransform)
            {
                var points = new[] { point };
                current.Transform.TransformPoints(points);
                point = points[0];
            }

            point.X += current.Bounds.X;
            point.Y += current.Bounds.Y;
        }

        return point;
    }

    /// <summary>Returns this element's transformed rectangle in the surface's client coordinates.</summary>
    public RectangleF AbsoluteBounds
        => GetAbsoluteBounds(new RectangleF(0f, 0f, Bounds.Width, Bounds.Height));

    /// <summary>The local rectangle that a repaint of this element must cover.</summary>
    /// <remarks>An element that draws inside a small part of a large bounds (a link spanning the canvas, for
    /// example) can narrow the damage rectangle and avoid repainting the whole surface at animation rate.</remarks>
    protected virtual RectangleF GetInvalidationBounds()
        => new(0f, 0f, Bounds.Width, Bounds.Height);

    // 移动一个元素时，旧/新包围盒能否覆盖全部变化；子树有任何越界绘制就交给整面重画。
    internal bool TryCollectMovementDamage(out RectangleF damage)
    {
        damage = RectangleF.Empty;
        if (HasUnboundedDrawing(this))
        {
            return false;
        }

        if (AccumulateSubtreeBounds(this) is { } bounds)
        {
            bounds.Inflate(2f, 2f);
            damage = bounds;
        }

        return true;
    }

    private static bool HasUnboundedDrawing(Visual visual)
    {
        if (visual.CacheAsBitmap
            || visual.Effect is not null
            || visual.OpacityMask is not null
            || visual.Transform3D is { IsIdentity: false }
            || visual.UnboundedHitTest)
        {
            return true;
        }

        if (visual is ContainerVisual container)
        {
            var children = container.Children;
            for (var index = 0; index < children.Count; index++)
            {
                if (children[index] is { } child && HasUnboundedDrawing(child))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static RectangleF? AccumulateSubtreeBounds(Visual visual)
    {
        RectangleF? bounds = visual.GetSurfaceBounds() is { Width: > 0f, Height: > 0f } own ? own : null;
        if (visual is not ContainerVisual container)
        {
            return bounds;
        }

        var children = container.Children;
        for (var index = 0; index < children.Count; index++)
        {
            var child = children[index];
            if (child is null || AccumulateSubtreeBounds(child) is not { } childBounds)
            {
                continue;
            }

            bounds = bounds is { } current ? RectangleF.Union(current, childBounds) : childBounds;
        }

        return bounds;
    }

    // 线性部分完全一致、只有 OffsetX/OffsetY 变化，才算整帧可平移的纯平移。
    private static bool TryGetTranslationDelta(Matrix? oldTransform, Matrix? newTransform, out float dx, out float dy)
    {
        var oldElements = oldTransform?.Elements;
        var newElements = newTransform?.Elements;

        // 只有位移变化才允许整帧平移；矩阵元素缺失时按单位矩阵处理。
        for (var index = 0; index < 4; index++)
        {
            var oldValue = oldElements is null ? (index is 0 or 3 ? 1f : 0f) : oldElements[index];
            var newValue = newElements is null ? (index is 0 or 3 ? 1f : 0f) : newElements[index];
            if (Math.Abs(oldValue - newValue) > 0.0001f)
            {
                dx = 0f;
                dy = 0f;
                return false;
            }
        }

        var oldX = oldElements is null ? 0f : oldElements[4];
        var oldY = oldElements is null ? 0f : oldElements[5];
        var newX = newElements is null ? 0f : newElements[4];
        var newY = newElements is null ? 0f : newElements[5];
        dx = newX - oldX;
        dy = newY - oldY;
        return dx != 0f || dy != 0f;
    }

    private RectangleF GetAbsoluteBounds(RectangleF local)
    {
        // A scaled/rotated element is no longer axis-aligned; use the transformed corners.
        var topLeft = PointToSurface(new PointF(local.Left, local.Top));
        var topRight = PointToSurface(new PointF(local.Right, local.Top));
        var bottomLeft = PointToSurface(new PointF(local.Left, local.Bottom));
        var bottomRight = PointToSurface(new PointF(local.Right, local.Bottom));
        var minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        var minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        var maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        var maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));
        return new RectangleF(minX, minY, Math.Max(0f, maxX - minX), Math.Max(0f, maxY - minY));
    }

    /// <summary>Disposes this element and its descendants.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases element resources; derived controls stop timers here.</summary>
    /// <param name="disposing">Whether managed resources are released.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        // 宿主行为先摘掉：它们握着宿主的事件与部件订阅，不能等到子元素释放完再断。
        DetachHostBehaviors();

        // 元素被拆掉时动画必须停下并把层位落回本地层；否则调度器会继续往一个已释放的节点写值。
        if (VisualTransition.HasActive(this))
        {
            VisualTransition.Stop(this);
        }

        // 样式应用握着本元素的 PropertyChanged 订阅；释放时摘掉，避免事件把已释放对象拉回来。
        _styleApplication?.Apply(null);
        _explicitStyle = null;
        _implicitStyle = null;

        // 主题是静态事件；不摘订阅，切换主题时会把已释放的元素留在内存里。
        if (_themeMetricsSubscribed)
        {
            UnregisterThemeMetrics();
            _themeMetricsSubscribed = false;
        }

        _themeMetrics = null;

        // 动态/延迟资源订阅也要摘掉，否则字典的 Changed 会把已释放的元素一直留在内存里。
        if (_resourceBindings is not null)
        {
            foreach (var binding in _resourceBindings)
            {
                binding.Dispose();
            }

            _resourceBindings.Clear();
            _resourceBindingsByTarget?.Clear();
        }

        DisposeCache();

        _resolvedFont?.Dispose();
        _resolvedFont = null;
        _resolvedFontBase = null;
        _identityTransform?.Dispose();
        _identityTransform = null;

        // 倒序按索引走：Dispose 不会移除兄弟节点，倒序还能容忍子树在释放时改动本层集合。
        var children = Children;
        for (var index = children.Count - 1; index >= 0; index--)
        {
            var child = children[index];
            if (child is VisualElement element)
            {
                element.Dispose();
            }
            else
            {
                child.DisposeCache();
            }
        }

        Children.Clear();
    }

    /// <inheritdoc />
    protected override object? GetInheritedValue(DependencyProperty property)
    {
        if (ReferenceEquals(property, DataContextProperty))
        {
            // 父链没有上下文时，把宿主（VisualHost）当成树的根；本地值仍然在自己的优先级层里胜出。
            return ParentElement?.DataContext ?? Surface?.DataContext;
        }

        if (ReferenceEquals(property, IsEnabledProperty))
        {
            return ParentElement?.IsEnabled;
        }

        if (ReferenceEquals(property, FontSizeProperty))
        {
            // NaN 表示“没有自己的值”，继续向上找，而不是把一个无效尺寸当成继承值。
            return ParentElement?.GetValue(FontSizeProperty) is double size && !double.IsNaN(size)
                ? size
                : null;
        }

        if (ReferenceEquals(property, FontFamilyProperty))
        {
            return ParentElement?.GetValue(FontFamilyProperty) is string family && !string.IsNullOrEmpty(family)
                ? family
                : null;
        }

        if (ReferenceEquals(property, FontWeightProperty))
        {
            return ParentElement?.GetValue(FontWeightProperty) is FontWeight weight && weight != FontWeight.Normal
                ? weight
                : null;
        }

        return base.GetInheritedValue(property);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // 根节点只有位移变化时，整帧像素就是平移；本地记录平移量，避免把整棵树重画一遍。
        // 只有变换与绘制属性才需要沿父链找表面；模板初始化写 TemplatedParent 这类属性不该付这次查找。
        var rootTranslation = false;
        var affectsRender = e.Property.Metadata.AffectsRender;
        if ((affectsRender || ReferenceEquals(e.Property, TransformProperty))
            && Surface is { } rootSurface && ReferenceEquals(rootSurface.Root, this))
        {
            if (ReferenceEquals(e.Property, TransformProperty)
                && TryGetTranslationDelta(e.OldValue as Matrix, e.NewValue as Matrix, out var panDx, out var panDy))
            {
                rootTranslation = true;
                rootSurface.RecordRootTranslation(panDx, panDy);
            }
            else if (affectsRender)
            {
                // 根自身的其它绘制属性变化不能被当成平移的一部分吞掉。
                rootSurface.NotifyRootContentChanged();
            }
        }

        if (ReferenceEquals(e.Property, VisibilityProperty))
        {
            IsVisible = Visibility == XamlVisibility.Visible;
        }
        else if (ReferenceEquals(e.Property, DataContextProperty))
        {
            NotifyDataContextDescendants(this, e.OldValue, e.NewValue);
        }
        else if (ReferenceEquals(e.Property, IsEnabledProperty)
                 || ReferenceEquals(e.Property, FontSizeProperty)
                 || ReferenceEquals(e.Property, FontFamilyProperty)
                 || ReferenceEquals(e.Property, FontWeightProperty))
        {
            // 继承属性变了要让没写本地值的后代收到通知；绑定、动画与重绘都靠这一跳。
            NotifyInheritedDescendants(this, e.Property, e.OldValue, e.NewValue);

            // 子树被禁用时，正握着焦点的后代必须立刻交还焦点。
            if (ReferenceEquals(e.Property, IsEnabledProperty)
                && !IsEnabled
                && Surface?.FocusedElement is { } focused
                && IsAncestorOf(focused))
            {
                Surface.FocusElement(null);
            }
        }
        else if (ReferenceEquals(e.Property, Canvas.LeftProperty)
                 || ReferenceEquals(e.Property, Canvas.TopProperty))
        {
            // 画布坐标只让父级重新摆放这一项；旧/新包围盒都记进脏区，帧缓存就不必整面重画。
            if (Surface is { } surface && TryCollectMovementDamage(out var oldDamage))
            {
                surface.RecordLocalMove(this, oldDamage);
                InvalidateArrangeLocal();
            }
            else
            {
                ParentElement?.InvalidateArrange();
            }
        }

        if (ReferenceEquals(e.Property, CacheAsBitmapProperty) && !CacheAsBitmap)
        {
            DisposeCache();
        }

        // 3D 投影/透明度/2D 变换不改缓存位图的内容；只重画输出，并让父级覆盖旧投影区域。
        // 若走 InvalidateVisual，最近的 CacheAsBitmap 祖先（可能就是自己）会被标脏，缓存就白做了。
        var preserveContentCache = ReferenceEquals(e.Property, Transform3DProperty)
            || (CacheAsBitmap
                && Transform3D is { IsIdentity: false }
                && (ReferenceEquals(e.Property, OpacityProperty)
                    || ReferenceEquals(e.Property, TransformProperty)));

        if (preserveContentCache)
        {
            if (ParentElement is { } parent)
            {
                parent.InvalidateVisual();
            }
            else
            {
                Surface?.InvalidateSurface();
            }
        }

        if (e.Property.Metadata.AffectsMeasure && ShouldInvalidateMeasure(e))
        {
            InvalidateMeasure();
        }
        else if (e.Property.Metadata.AffectsArrange)
        {
            InvalidateArrange();
        }

        if (e.Property.Metadata.AffectsRender && !preserveContentCache && !rootTranslation)
        {
            InvalidateVisual();
        }
    }

    // 宿主 DataContext 变化时由 VisualHost 调用：根没有本地值时补一次 inherited 通知，
    // 现有的 OnPropertyChanged → NotifyDataContextDescendants 会把它传遍整棵子树。
    internal void RaiseHostDataContextChanged(object? oldValue, object? newValue)
    {
        if (IsSet(DataContextProperty) || ReferenceEquals(oldValue, newValue))
        {
            return;
        }

        RaisePropertyChanged(DataContextProperty, oldValue, newValue, BindingPriority.Inherited);
    }

    // 子元素自己写了 DataContext 就截断继承；其余后代要收到 inherited 变更，绑定在祖先切换时才能重读。
    private static void NotifyDataContextDescendants(VisualElement parent, object? oldValue, object? newValue)
    {
        var children = parent.Children;
        for (var index = 0; index < children.Count; index++)
        {
            if (children[index] is not VisualElement element || element.IsSet(DataContextProperty))
            {
                continue;
            }

            // RaisePropertyChanged → element.OnPropertyChanged 会继续通知它的子级；
            // 这里再显式递归会让同一棵子树被走两遍，深度一大就变成指数级。
            element.RaisePropertyChanged(DataContextProperty, oldValue, newValue, BindingPriority.Inherited);
        }
    }

    // 只通知直接子级；每个子级的 OnPropertyChanged 会继续向下传，避免同一层被通知两次。
    private static void NotifyInheritedDescendants(
        VisualElement parent,
        DependencyProperty property,
        object? oldValue,
        object? newValue)
    {
        var children = parent.Children;
        for (var index = 0; index < children.Count; index++)
        {
            if (children[index] is VisualElement element && !element.IsSet(property))
            {
                element.RaisePropertyChanged(property, oldValue, newValue, BindingPriority.Inherited);
            }
        }
    }

    internal void SetSurfaceRoot(VisualHost? surface)
    {
        _surface = surface;
        // 表面资源是资源链的最后一站；根挂到 VisualHost（或从它摘下）时延迟解析要重试一次。
        SurfaceChanged?.Invoke(this, EventArgs.Empty);
        RaiseSurfaceChangedObservers();
        NotifySurfaceRootChanged();
        SyncHostBehaviors();
    }

    private List<IVisualHostBehavior>? _hostBehaviors;
    private Dictionary<IVisualHostBehavior, VisualHost>? _hostBehaviorHosts;

    // 宿主变更只做一次比较：已经挂在正确宿主上的行为不会被重复 Attach。
    private void SyncHostBehavior(IVisualHostBehavior behavior)
    {
        var host = Surface;
        _hostBehaviorHosts ??= [];
        _hostBehaviorHosts.TryGetValue(behavior, out var current);
        if (ReferenceEquals(current, host))
        {
            return;
        }

        if (current is not null)
        {
            _hostBehaviorHosts.Remove(behavior);
            behavior.Detach();
        }

        if (host is not null)
        {
            _hostBehaviorHosts[behavior] = host;
            behavior.Attach(host);
        }
    }

    private void SyncHostBehaviors()
    {
        if (_hostBehaviors is not { Count: > 0 } behaviors)
        {
            return;
        }

        for (var index = behaviors.Count - 1; index >= 0; index--)
        {
            SyncHostBehavior(behaviors[index]);
        }
    }

    // 换父级/换宿主会让整棵子树的 Surface 变化；没有行为的子树只多一次空检查。
    private void SyncHostBehaviorsRecursive()
    {
        SyncHostBehaviors();
        var children = Children;
        for (var index = 0; index < children.Count; index++)
        {
            if (children[index] is VisualElement element)
            {
                element.SyncHostBehaviorsRecursive();
            }
        }
    }

    private void DetachHostBehavior(IVisualHostBehavior behavior)
    {
        if (_hostBehaviorHosts is null || !_hostBehaviorHosts.TryGetValue(behavior, out _))
        {
            return;
        }

        _hostBehaviorHosts.Remove(behavior);
        behavior.Detach();
    }

    private void DetachHostBehaviors()
    {
        if (_hostBehaviors is not { Count: > 0 } behaviors)
        {
            return;
        }

        for (var index = behaviors.Count - 1; index >= 0; index--)
        {
            DetachHostBehavior(behaviors[index]);
        }

        behaviors.Clear();
        _hostBehaviorHosts = null;
    }

    internal event EventHandler? SurfaceChanged;

    // 与 ParentChanged 同理：资源绑定的 SurfaceChanged 订阅走共享列表。
    private List<Action>? _surfaceChangedObservers;

    internal void AddSurfaceChangedObserver(Action observer)
        => (_surfaceChangedObservers ??= new List<Action>()).Add(observer);

    internal void RemoveSurfaceChangedObserver(Action observer)
        => _surfaceChangedObservers?.Remove(observer);

    private void RaiseSurfaceChangedObservers()
    {
        var observers = _surfaceChangedObservers;
        if (observers is null)
        {
            return;
        }

        for (var index = observers.Count - 1; index >= 0; index--)
        {
            if (index < observers.Count)
            {
                observers[index]();
            }
        }
    }

    // 根换表面时整棵子树都要知道；标题栏靠它在挂上宿主后才解析出 Form。
    internal event EventHandler? SurfaceRootChanged;

    private void NotifySurfaceRootChanged()
    {
        SurfaceRootChanged?.Invoke(this, EventArgs.Empty);
        var children = Children;
        for (var index = 0; index < children.Count; index++)
        {
            if (children[index] is VisualElement element)
            {
                element.NotifySurfaceRootChanged();
                element.SyncHostBehaviors();
            }
        }
    }

    // 模板里的每个后代都记着施模板的控件；TemplateBinding 靠它找源，模板触发器也写回它。
    internal void SetTemplatedParentRecursive(VisualElement? parent)
    {
        // 内层控件自己的模板部件已经指向内层控件，不能被外层模板改源；但它们的子树里可能有
        // 属于外层模板的内容（例如 ScrollViewer 的 Content），所以保留部件本身、继续向下走。
        if (TemplatedParent is null || ReferenceEquals(TemplatedParent, parent))
        {
            TemplatedParent = parent;
        }

        var children = Children;
        for (var index = 0; index < children.Count; index++)
        {
            if (children[index] is VisualElement element)
            {
                element.SetTemplatedParentRecursive(parent);
            }
        }
    }

    internal void SetTemplateScope(VisualNameScope? scope) => _templateScope = scope;

    // DataTemplate/ItemsPanelTemplate 实例根自己的名字域；控件模板作用在自己的 _templateScope 上，两者不互相覆盖。
    internal void SetNameScope(VisualNameScope? scope) => _instanceScope = scope;

    // 资源订阅由 VisualResourceLookup 建立；元素释放时统一摘掉。
    internal void TrackResourceBinding(IDisposable binding) => (_resourceBindings ??= []).Add(binding);

    // 同一目标属性上的后一个 StaticResource/DynamicResource 接管前一个引用；否则旧的动态引用会在字典
    // 变化时把后来的静态值再次覆盖。
    internal void RegisterResourceBinding(object targetToken, ResourceBinding binding)
    {
        _resourceBindingsByTarget ??= [];
        if (_resourceBindingsByTarget.TryGetValue(targetToken, out var previous)
            && !ReferenceEquals(previous, binding))
        {
            previous.Dispose();
        }

        _resourceBindingsByTarget[targetToken] = binding;
    }

    internal void UntrackResourceBinding(object targetToken, ResourceBinding binding)
    {
        if (_resourceBindingsByTarget is { } map
            && map.TryGetValue(targetToken, out var current)
            && ReferenceEquals(current, binding))
        {
            map.Remove(targetToken);
        }
    }

    // 从本元素向上找到最近的名字域；ElementName 先在这里查，查不到才回退到树遍历。
    internal VisualNameScope? EnclosingNameScope
    {
        get
        {
            for (var current = this; current is not null; current = current.ParentElement)
            {
                if ((current._templateScope ?? current._instanceScope) is { } scope)
                {
                    return scope;
                }
            }

            return null;
        }
    }

    // 模板实例内的 ElementName 只能命中本实例的名字域；跨实例查找会串到别的 item。
    internal object? FindInEnclosingNameScope(string name)
        => EnclosingNameScope?.Find(name);

    /// <summary>Returns the named template part created for this element, or <see langword="null"/>.</summary>
    /// <typeparam name="T">The type the caller expects.</typeparam>
    /// <param name="name">The <c>x:Name</c> the part was declared with inside the template.</param>
    /// <remarks>The scope belongs to one template instance; parts of another instance never resolve here.</remarks>
    public T? GetTemplateChild<T>(string name) where T : class
        => (_templateScope ?? _instanceScope)?.Find<T>(name);

    // 样式有 Template 时由 VisualStyleApplication 回调；ContentControl 与 Controls.TemplatedControl 覆写它。
    internal virtual void ApplyStyleTemplate(VisualControlTemplate? template)
    {
    }

    // 显式样式压过隐式；两者都为空时把两个层清干净。
    private void ApplyEffectiveStyle()
        => (_styleApplication ??= new VisualStyleApplication(this)).Apply(_explicitStyle ?? _implicitStyle);

    private VisualStyleCollection CreateStyles()
    {
        var styles = new VisualStyleCollection();
        styles.Changed += (_, _) => RefreshImplicitStyles();
        return styles;
    }

    internal void SetImplicitStyle(VisualStyle? style)
    {
        if (ReferenceEquals(_implicitStyle, style))
        {
            return;
        }

        _implicitStyle = style;
        if (_explicitStyle is null)
        {
            ApplyEffectiveStyle();
        }
    }

    // 从本元素起向下重解隐式样式；挂在祖先集合上的样式变化只需从那个祖先调用一次。
    internal void RefreshImplicitStyles()
    {
        SetImplicitStyle(ResolveImplicitStyle());
        var children = Children;
        for (var index = 0; index < children.Count; index++)
        {
            if (children[index] is VisualElement element)
            {
                element.RefreshImplicitStyles();
            }
        }
    }

    private VisualStyle? ResolveImplicitStyle()
    {
        for (var current = this; current is not null; current = current.ParentElement)
        {
            if (current._styles?.FindMatch(this) is { } match)
            {
                return match;
            }
        }

        return Surface?.Styles.FindMatch(this) ?? Easel.Theme.FindDefaultStyle(this);
    }

    /// <summary>Seeds a dependency property from the active palette and keeps it current when the palette changes.</summary>
    /// <param name="property">The dependency property to seed, such as a corner radius or a minimum control height.</param>
    /// <param name="select">Returns the value that <paramref name="property"/> should take for a palette.</param>
    /// <remarks>
    /// The seeded value lives below local values, bindings, style triggers, templates and style setters, so an
    /// explicit assignment or a host style still wins. The element subscribes to <see cref="Theme.Changed"/> once and
    /// rewrites only this layer, which re-measures and repaints a control whose metric changed without rebuilding it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> or <paramref name="select"/> is
    /// <see langword="null"/>.</exception>
    protected void SetThemeMetric(DependencyProperty property, Func<ThemePalette, object?> select)
    {
        if (property is null) throw new ArgumentNullException(nameof(property));
        if (select is null) throw new ArgumentNullException(nameof(select));

        _themeMetrics ??= [];
        var metric = new ThemeMetric(property, select);
        var index = _themeMetrics.FindIndex(entry => ReferenceEquals(entry.Property, property));
        if (index >= 0)
        {
            _themeMetrics[index] = metric;
        }
        else
        {
            _themeMetrics.Add(metric);
        }

        if (!_themeMetricsSubscribed)
        {
            RegisterThemeMetrics();
        }

        SetValue(property, select(Theme.Current), ThemeMetricPriority);
    }

    // Theme.Changed 是静态事件；只挂一个静态处理器，元素进出登记表，避免逐个摘订阅时重建多播数组。
    private static readonly List<VisualElement> s_themeMetricElements = [];
    private static bool s_themeMetricHookInstalled;

    private void RegisterThemeMetrics()
    {
        if (!s_themeMetricHookInstalled)
        {
            s_themeMetricHookInstalled = true;
            Theme.Changed += OnAnyThemeChanged;
        }

        // 调用点只在 _themeMetricsSubscribed == false 时进来；已登记过的元素不会走到这里。
        s_themeMetricElements.Add(this);
        _themeMetricsSubscribed = true;
    }

    private void UnregisterThemeMetrics()
    {
        s_themeMetricElements.Remove(this);
    }

    private static void OnAnyThemeChanged(object? sender, EventArgs e)
    {
        var palette = Theme.Current;
        for (var index = s_themeMetricElements.Count - 1; index >= 0; index--)
        {
            s_themeMetricElements[index].ApplyThemeMetrics(palette);
        }
    }

    private void ApplyThemeMetrics(ThemePalette palette)
    {
        if (_themeMetrics is null)
        {
            return;
        }

        foreach (var metric in _themeMetrics)
        {
            SetValue(metric.Property, metric.Select(palette), ThemeMetricPriority);
        }
    }

    private readonly record struct ThemeMetric(
        DependencyProperty Property,
        Func<ThemePalette, object?> Select);

    internal void SetFocused(bool focused)
    {
        if (_focused == focused)
        {
            return;
        }

        _focused = focused;
        if (!focused)
        {
            _focusVisible = false;
        }

        InvalidateVisual();
        if (focused)
        {
            OnGotFocus();
            GotFocus?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            OnLostFocus();
            LostFocus?.Invoke(this, EventArgs.Empty);
        }
    }

    // 原生控件的 Leave 不代表表面逻辑焦点曾经落在本元素上；直接发一次 LostFocus，绑定触发器才可靠。
    internal void RaiseLostFocus()
    {
        _focused = false;
        _focusVisible = false;
        InvalidateVisual();
        OnLostFocus();
        LostFocus?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Called after <see cref="IsFocusVisible"/> changes; templated controls mirror it into a trigger-friendly property.</summary>
    /// <param name="visible">The new focus-visible state.</param>
    protected virtual void OnFocusVisibleChanged(bool visible)
    {
    }

    internal void SetFocusVisible(bool visible)
    {
        if (_focusVisible == visible)
        {
            return;
        }

        _focusVisible = visible;
        OnFocusVisibleChanged(visible);
        InvalidateVisual();
    }

    internal void SetPointerOver(bool over, VisualPointerEventArgs? args)
    {
        if (_pointerOver == over)
        {
            return;
        }

        _pointerOver = over;
        InvalidateVisual();
        if (over)
        {
            OnPointerEntered(args ?? new VisualPointerEventArgs(PointF.Empty, MouseButtons.None, MouseButtons.None, 1, 0, Keys.None));
            PointerEntered?.Invoke(this, args ?? new VisualPointerEventArgs(PointF.Empty, MouseButtons.None, MouseButtons.None, 1, 0, Keys.None));
        }
        else
        {
            OnPointerExited(args ?? new VisualPointerEventArgs(PointF.Empty, MouseButtons.None, MouseButtons.None, 1, 0, Keys.None));
            PointerExited?.Invoke(this, args ?? new VisualPointerEventArgs(PointF.Empty, MouseButtons.None, MouseButtons.None, 1, 0, Keys.None));
        }
    }

    internal void RaisePreviewPointerPressed(VisualPointerEventArgs args)
        => PreviewPointerPressed?.Invoke(this, args);

    internal void RaisePointerPressed(VisualPointerEventArgs args)
    {
        for (var current = this; current is not null && !args.Handled; current = current.ParentElement)
        {
            current.OnPointerPressed(args);
            current.PointerPressed?.Invoke(current, args);
        }
    }

    internal void RaisePointerMoved(VisualPointerEventArgs args)
    {
        for (var current = this; current is not null && !args.Handled; current = current.ParentElement)
        {
            current.OnPointerMoved(args);
            current.PointerMoved?.Invoke(current, args);
        }
    }

    internal void RaisePointerReleased(VisualPointerEventArgs args)
    {
        for (var current = this; current is not null && !args.Handled; current = current.ParentElement)
        {
            current.OnPointerReleased(args);
            current.PointerReleased?.Invoke(current, args);
        }
    }

    internal void RaisePointerWheel(VisualPointerEventArgs args)
    {
        for (var current = this; current is not null && !args.Handled; current = current.ParentElement)
        {
            current.OnPointerWheel(args);
            current.PointerWheel?.Invoke(current, args);
        }
    }

    internal void RaiseDragEnter(VisualDragEventArgs args)
    {
        for (var current = this; current is not null && !args.Handled; current = current.ParentElement)
        {
            if (!current.AllowDrop)
            {
                continue;
            }

            current.OnDragEnter(args);
            current.DragEnter?.Invoke(current, args);
        }
    }

    internal void RaiseDragOver(VisualDragEventArgs args)
    {
        for (var current = this; current is not null && !args.Handled; current = current.ParentElement)
        {
            if (!current.AllowDrop)
            {
                continue;
            }

            current.OnDragOver(args);
            current.DragOver?.Invoke(current, args);
        }
    }

    internal void RaiseDragLeave(VisualDragEventArgs args)
    {
        for (var current = this; current is not null && !args.Handled; current = current.ParentElement)
        {
            if (!current.AllowDrop)
            {
                continue;
            }

            current.OnDragLeave(args);
            current.DragLeave?.Invoke(current, args);
        }
    }

    internal void RaiseDrop(VisualDragEventArgs args)
    {
        for (var current = this; current is not null && !args.Handled; current = current.ParentElement)
        {
            if (!current.AllowDrop)
            {
                continue;
            }

            current.OnDrop(args);
            current.Drop?.Invoke(current, args);
        }
    }

    internal void RaiseKeyDown(VisualKeyEventArgs args)
    {
        for (var current = this; current is not null && !args.Handled; current = current.ParentElement)
        {
            current.OnKeyDown(args);
            current.KeyDown?.Invoke(current, args);
        }
    }

    internal void RaiseKeyUp(VisualKeyEventArgs args)
    {
        for (var current = this; current is not null && !args.Handled; current = current.ParentElement)
        {
            current.OnKeyUp(args);
            current.KeyUp?.Invoke(current, args);
        }
    }

    internal void RaiseTextInput(VisualTextInputEventArgs args)
    {
        for (var current = this; current is not null && !args.Handled; current = current.ParentElement)
        {
            current.OnTextInput(args);
            current.TextInput?.Invoke(current, args);
        }
    }

    /// <summary>Returns the size this element wants inside the available room.</summary>
    /// <param name="availableSize">The room left after the margin and padding.</param>
    protected virtual Size MeasureOverride(Size availableSize)
        => new(
            (int)Math.Ceiling(Padding.Horizontal),
            (int)Math.Ceiling(Padding.Vertical));

    /// <summary>Arranges this element's children inside its own local size.</summary>
    /// <param name="finalSize">The arranged content size.</param>
    protected virtual void ArrangeOverride(Size finalSize)
    {
    }

    /// <inheritdoc />
    protected override void RenderContent(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(
            0,
            0,
            Math.Max(0, (int)Math.Ceiling(Bounds.Width)),
            Math.Max(0, (int)Math.Ceiling(Bounds.Height)));
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (Background is { } || (BorderBrush is { } && !BorderThickness.IsEmpty))
        {
            XamlDrawing.PaintChromeWithBrushes(graphics, bounds, Background, BorderBrush, BorderThickness, CornerRadius);
        }

        OnRender(graphics);
    }

    /// <summary>Paints this element's own content in local coordinates.</summary>
    /// <param name="graphics">The target surface.</param>
    protected virtual void OnRender(Graphics graphics)
    {
    }

    /// <inheritdoc />
    protected override bool HitTestContent(PointF point) => IsHitTestVisible;

    /// <summary>Called when the pointer is pressed on this element.</summary>
    /// <param name="args">The pointer data.</param>
    protected virtual void OnPointerPressed(VisualPointerEventArgs args)
    {
    }

    /// <summary>Called when the pointer moves over this element.</summary>
    /// <param name="args">The pointer data.</param>
    protected virtual void OnPointerMoved(VisualPointerEventArgs args)
    {
    }

    /// <summary>Called when the pointer is released on this element.</summary>
    /// <param name="args">The pointer data.</param>
    protected virtual void OnPointerReleased(VisualPointerEventArgs args)
    {
    }

    /// <summary>Called when the pointer enters this element.</summary>
    /// <param name="args">The pointer data.</param>
    protected virtual void OnPointerEntered(VisualPointerEventArgs args)
    {
    }

    /// <summary>Called when the pointer leaves this element.</summary>
    /// <param name="args">The pointer data.</param>
    protected virtual void OnPointerExited(VisualPointerEventArgs args)
    {
    }

    /// <summary>Called when the wheel turns over this element.</summary>
    /// <param name="args">The pointer data; <see cref="VisualPointerEventArgs.Delta"/> carries the wheel delta.</param>
    protected virtual void OnPointerWheel(VisualPointerEventArgs args)
    {
    }

    /// <summary>Called when a drag enters this element.</summary>
    /// <param name="args">The drag data; set <see cref="VisualDragEventArgs.Handled"/> to stop bubbling.</param>
    protected virtual void OnDragEnter(VisualDragEventArgs args)
    {
    }

    /// <summary>Called while a drag moves over this element.</summary>
    /// <param name="args">The drag data; set <see cref="VisualDragEventArgs.Effects"/> to choose the cursor.</param>
    protected virtual void OnDragOver(VisualDragEventArgs args)
    {
    }

    /// <summary>Called when a drag leaves this element.</summary>
    /// <param name="args">The drag data.</param>
    protected virtual void OnDragLeave(VisualDragEventArgs args)
    {
    }

    /// <summary>Called when the user drops data on this element.</summary>
    /// <param name="args">The drag data; set <see cref="VisualDragEventArgs.Effects"/> to report the result.</param>
    protected virtual void OnDrop(VisualDragEventArgs args)
    {
    }

    /// <summary>Called when a key goes down on this element.</summary>
    /// <param name="args">The key data.</param>
    protected virtual void OnKeyDown(VisualKeyEventArgs args)
    {
    }

    /// <summary>Called when a key goes up on this element.</summary>
    /// <param name="args">The key data.</param>
    protected virtual void OnKeyUp(VisualKeyEventArgs args)
    {
    }

    /// <summary>Called when text input reaches this element.</summary>
    /// <param name="args">The text data.</param>
    protected virtual void OnTextInput(VisualTextInputEventArgs args)
    {
    }

    /// <summary>Called after this element receives logical focus.</summary>
    protected virtual void OnGotFocus()
    {
    }

    /// <summary>Called after this element loses logical focus.</summary>
    protected virtual void OnLostFocus()
    {
    }

    private static double ResolveAxis(double explicitValue, double desired, double minimum, double maximum)
    {
        var value = double.IsNaN(explicitValue) ? desired : explicitValue;
        value = Math.Max(value, Math.Max(0, minimum));
        return Math.Min(value, Math.Max(0, maximum));
    }

    private static float ResolveArranged(
        double explicitValue,
        int desired,
        double minimum,
        double maximum,
        float slot,
        bool stretch)
    {
        var value = double.IsNaN(explicitValue)
            ? (stretch ? slot : Math.Min(slot, desired))
            : explicitValue;
        value = Math.Max(value, Math.Max(0, minimum));
        value = Math.Min(value, Math.Max(0, maximum));
        return (float)Math.Min(value, slot);
    }
}
