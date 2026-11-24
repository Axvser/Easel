using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Easel.Visuals;

/// <summary>
/// A retained, lightweight visual node: bounds, transform, opacity, clip, effect and children, painted without an
/// HWND.
/// </summary>
/// <remarks>
/// <para>
/// This is the opt-in lightweight layer of the WinForms XAML runtime. It is intended for non-interactive content such
/// as decoration, text, shapes and connector batches; native controls and WinForms controls
/// stay real child windows. A tree of visuals is hosted by <see cref="VisualHost"/> and painted in one pass.
/// </para>
/// <para>
/// <see cref="Bounds"/> is expressed in the parent's coordinates; content is painted in the visual's own local
/// coordinates <c>0..Width/Height</c>. <see cref="Transform"/> is applied in that local coordinate space before the
/// bounds translation, <see cref="Clip"/> is applied in local coordinates, and <see cref="Opacity"/> and
/// <see cref="Effect"/> compose the whole subtree through a temporary bitmap.
/// </para>
/// </remarks>
public abstract class Visual : WinformsObject
{
    /// <summary>The rectangle this visual occupies in its parent's coordinates.</summary>
    public RectangleF Bounds { get; set; }

    /// <summary>A local transform applied after <see cref="Bounds"/> is translated to the origin.</summary>
    /// <remarks>The identity matrix is created lazily so an unset visual holds no native GDI+ matrix.</remarks>
    public virtual Matrix Transform
    {
        get => _transform ??= new Matrix();
        set => _transform = value ?? new Matrix();
    }

    private Matrix? _transform;

    /// <summary>An optional 3D transform projected into this visual's local plane.</summary>
    /// <remarks>
    /// The subtree is composited into a bitmap and drawn through the projected quad; a perspective transform
    /// therefore makes the near edge larger and the far edge smaller.
    /// </remarks>
    public virtual Transform3D? Transform3D { get; set; }

    /// <summary>The opacity of this visual and its subtree, in the range 0..1.</summary>
    public virtual double Opacity { get; set; } = 1d;

    /// <summary>An optional brush that masks the composited subtree.</summary>
    /// <remarks>
    /// <see cref="OpacityMaskMode.Alpha"/> uses the brush's alpha, matching Windows Presentation Foundation's
    /// opacity mask. <see cref="OpacityMaskMode.Luminance"/> uses the brush's premultiplied luminance times alpha,
    /// so a white-to-black opaque gradient works as a fade. A mask forces the subtree through the composition
    /// bitmap even when its opacity is one.
    /// </remarks>
    public virtual Brush? OpacityMask { get; set; }

    /// <summary>How <see cref="OpacityMask"/> becomes coverage; alpha by default.</summary>
    public virtual OpacityMaskMode OpacityMaskMode { get; set; } = OpacityMaskMode.Alpha;

    /// <summary>An optional clip rectangle in the visual's local coordinates.</summary>
    public virtual RectangleF? Clip { get; set; }

    /// <summary>Whether children are painted and hit-tested only inside this visual's local bounds.</summary>
    /// <remarks>WPF's <c>UIElement.ClipToBounds</c> equivalent; defaults to <see langword="false"/>.</remarks>
    public virtual bool ClipToBounds { get; set; }

    /// <summary>An optional effect applied to the rendered subtree.</summary>
    public virtual VisualEffect? Effect { get; set; }

    /// <summary>Whether this visual and its subtree are painted and hit-tested.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Whether a hit outside <see cref="Bounds"/> still reaches this visual's content and children.</summary>
    /// <remarks>
    /// A camera container uses this: its children are painted in world coordinates far outside the viewport-sized
    /// layout box, and the surface clip (not the camera rectangle) decides what is visible.
    /// </remarks>
    public bool UnboundedHitTest { get; set; }

    /// <summary>Whether the subtree is cached in a bitmap between frames.</summary>
    /// <remarks>
    /// Use it for static groups over which only cheap changes happen: moving a cached visual does not re-render its
    /// content. A dependency-property change inside a <see cref="VisualElement"/> subtree marks the nearest cache
    /// dirty automatically; a custom visual must call <see cref="InvalidateCache"/> after changing the pixels it
    /// draws. An <see cref="Effect"/> disables the cache for that visual.
    /// </remarks>
    public virtual bool CacheAsBitmap { get; set; }

    private ContainerVisual? _parent;

    /// <summary>The container that owns this visual, or <see langword="null"/> for a root.</summary>
    public ContainerVisual? Parent
    {
        get => _parent;
        internal set
        {
            if (ReferenceEquals(_parent, value))
            {
                return;
            }

            var previous = _parent;
            _parent = value;
            OnParentChanged(previous, value);
        }
    }

    /// <summary>Called after <see cref="Parent"/> changes; a visual tree hook for bindings and behaviors.</summary>
    protected virtual void OnParentChanged(ContainerVisual? oldParent, ContainerVisual? newParent)
    {
    }

    private Bitmap? _cache;
    private bool _cacheDirty = true;
    private CacheKey _cacheKey;
    private Dictionary<(Font Font, int Dpi), Font>? _drawFonts;

    // 包围盒换算复用的单点缓冲；每个线程独立，避免在热路径上反复分配。
    [ThreadStatic] private static PointF[]? _transformPointBuffer;

    private static PointF[] TransformPointBuffer => _transformPointBuffer ??= new PointF[1];

    // 当前帧的脏区（表面逻辑坐标）。只在 VisualHost 的 RenderCore 内推入，遍历结束即恢复。
    [ThreadStatic] private static RectangleF _renderDamage;

    // 是否按脏区剔除子树。整面重画时为 false。
    [ThreadStatic] private static bool _cullToDamage;

    // 阴影、槽环与连线端点会画出 Bounds 之外，剔除时统一外扩这个余量。
    private const float DamageCullMargin = 32f;

    internal static RectangleF RenderDamage => _renderDamage;

    internal static bool CullToDamage => _cullToDamage;

    internal static void PushRenderDamage(RectangleF damage, bool cull)
    {
        _renderDamage = damage;
        _cullToDamage = cull;
    }

    // 子项整棵子树都落在脏区外才跳过；拿不准是否越界的一律照画。
    internal static bool ShouldRenderChild(Visual? child)
    {
        if (child is null)
        {
            return false;
        }

        return !_cullToDamage || IntersectsRenderDamage(child, _renderDamage);
    }

    private static bool IntersectsRenderDamage(Visual visual, RectangleF damage)
    {
        if (!visual.IsVisible || visual.Opacity <= 0d)
        {
            return false;
        }

        // 位图缓存、效果与无界命中的绘制范围不受 Bounds 约束，保守起见照画。
        if (visual.CacheAsBitmap
            || visual.Effect is not null
            || visual.OpacityMask is not null
            || (visual.Transform3D is { IsIdentity: false })
            || visual.UnboundedHitTest)
        {
            return true;
        }

        var bounds = visual.GetSurfaceBounds();
        if (bounds.Width > 0f && bounds.Height > 0f)
        {
            bounds.Inflate(DamageCullMargin, DamageCullMargin);
            if (bounds.IntersectsWith(damage))
            {
                return true;
            }
        }

        // 未裁剪的容器允许子项越界绘制；零尺寸容器（在栈里被量成 0 高）同样要检查子项，
        // 否则父级的脏区剔除会把它连同整棵子树一起丢掉。
        if (visual is ContainerVisual container && !visual.ClipToBounds)
        {
            foreach (var child in container.Children)
            {
                if (child is not null && IntersectsRenderDamage(child, damage))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // 本视觉在表面坐标系里的轴对齐包围盒：逐层叠加 Transform 与 Bounds，不依赖 Graphics 的当前变换。
    internal RectangleF GetSurfaceBounds()
    {
        var x0 = 0f;
        var y0 = 0f;
        var x1 = Bounds.Width;
        var y1 = 0f;
        var x2 = 0f;
        var y2 = Bounds.Height;
        var x3 = Bounds.Width;
        var y3 = Bounds.Height;
        TransformPointToSurface(ref x0, ref y0);
        TransformPointToSurface(ref x1, ref y1);
        TransformPointToSurface(ref x2, ref y2);
        TransformPointToSurface(ref x3, ref y3);

        var minX = Math.Min(Math.Min(x0, x1), Math.Min(x2, x3));
        var minY = Math.Min(Math.Min(y0, y1), Math.Min(y2, y3));
        var maxX = Math.Max(Math.Max(x0, x1), Math.Max(x2, x3));
        var maxY = Math.Max(Math.Max(y0, y1), Math.Max(y2, y3));
        return new RectangleF(minX, minY, Math.Max(0f, maxX - minX), Math.Max(0f, maxY - minY));
    }

    // 与 VisualElement.PointToSurface 同一套顺序：先 3D 投影，再 Transform，最后叠加 Bounds。
    private void TransformPointToSurface(ref float x, ref float y)
    {
        for (Visual? current = this; current is not null; current = current.Parent)
        {
            if (current.Transform3D is { IsIdentity: false } transform3D)
            {
                var projected = new Projection3D(
                    new RectangleF(0f, 0f, current.Bounds.Width, current.Bounds.Height),
                    transform3D).Map(new PointF(x, y));
                x = projected.X;
                y = projected.Y;
            }

            if (current.Transform is { IsIdentity: false } transform)
            {
                // Elements 每次返回新数组；剔除会按节点反复取包围盒，这里改用可复用的单点缓冲。
                var buffer = TransformPointBuffer;
                buffer[0] = new PointF(x, y);
                transform.TransformPoints(buffer);
                x = buffer[0].X;
                y = buffer[0].Y;
            }

            x += current.Bounds.X;
            y += current.Bounds.Y;
        }
    }

    /// <summary>Marks this visual's bitmap cache and every enclosing cache dirty.</summary>
    public void InvalidateCache()
    {
        _cacheDirty = true;
        Parent?.InvalidateCache();
    }

    // 释放位图缓存与本视觉派生出的绘制字体。
    internal void DisposeCache()
    {
        _cache?.Dispose();
        _cache = null;
        _cacheDirty = true;

        if (_drawFonts is { } fonts)
        {
            foreach (var font in fonts.Values)
            {
                font.Dispose();
            }

            fonts.Clear();
        }
    }

    // 点位字体由 GDI+ 按 Graphics 的 DPI 解释；表面又叠了一层世界缩放，先折回 96 DPI 才不会放大两次。
    // 像素字体本身就是逻辑像素，保持原样。
    internal Font ResolveDrawFont(Font font, Graphics graphics)
    {
        if (font.Unit != GraphicsUnit.Point)
        {
            return font;
        }

        var dpi = (int)Math.Round(graphics.DpiY);
        if (dpi <= 0 || dpi == 96)
        {
            return font;
        }

        _drawFonts ??= [];
        var key = (font, dpi);
        if (!_drawFonts.TryGetValue(key, out var derived))
        {
            derived = new Font(
                font.FontFamily,
                font.Size * 96f / dpi,
                font.Style,
                GraphicsUnit.Point,
                font.GdiCharSet,
                font.GdiVerticalFont);
            _drawFonts[key] = derived;
        }

        return derived;
    }

    /// <summary>Paints this visual and its children into <paramref name="graphics"/>.</summary>
    /// <param name="graphics">The target surface, already in the parent's coordinates.</param>
    public void Render(Graphics graphics)
    {
        if (graphics is null) throw new ArgumentNullException(nameof(graphics));
        if (!IsVisible || Opacity <= 0d)
        {
            return;
        }

        var zeroSized = Bounds.Width <= 0f || Bounds.Height <= 0f;
        // 零尺寸且不裁剪的容器仍要画子项：Canvas 在 StackPanel 里被量成 0 高就是这样。
        if (zeroSized && !(this is ContainerVisual && !ClipToBounds))
        {
            return;
        }

        var state = graphics.Save();
        try
        {
            graphics.TranslateTransform(Bounds.X, Bounds.Y);
            if (!Transform.IsIdentity)
            {
                // The transform belongs to the visual's own coordinate space: prepending it keeps the Bounds
                // translation (and every ancestor translation) unscaled. Appending would scale the card's
                // position as well, which is what made a Viewbox's child drift at any zoom other than 1.
                graphics.MultiplyTransform(Transform, MatrixOrder.Prepend);
            }

            if (ClipToBounds)
            {
                graphics.SetClip(new RectangleF(0f, 0f, Bounds.Width, Bounds.Height), CombineMode.Intersect);
            }

            if (Clip is { } clip)
            {
                graphics.SetClip(clip, CombineMode.Intersect);
            }

            // 零尺寸装不下一张合成位图，缓存也没有意义；直接画子项。
            if (zeroSized)
            {
                RenderChildren(graphics);
                return;
            }

            var has3D = Transform3D is { IsIdentity: false };

            if (CacheAsBitmap && Effect is null && OpacityMask is null && !has3D)
            {
                RenderCached(graphics);
                return;
            }

            if (Opacity < 0.999d || Effect is not null || OpacityMask is not null || has3D)
            {
                RenderComposited(graphics);
                return;
            }

            RenderContent(graphics);
            RenderChildren(graphics);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    /// <summary>Returns whether <paramref name="point"/>, in the parent's coordinates, hits this visual.</summary>
    /// <param name="point">The point to test.</param>
    public bool HitTest(PointF point)
    {
        if (!IsVisible || Opacity <= 0d)
        {
            return false;
        }

        var zeroSized = Bounds.Width <= 0f || Bounds.Height <= 0f;
        var allowOutside = !ClipToBounds;
        // 零尺寸不裁剪的容器仍要命中子项：子项本来就在它的 0 尺寸框之外。
        if (zeroSized && !allowOutside)
        {
            return false;
        }

        var local = point;
        local.X -= Bounds.X;
        local.Y -= Bounds.Y;

        if (!Transform.IsIdentity)
        {
            using var inverse = Transform.Clone();
            inverse.Invert();
            var points = new[] { local };
            inverse.TransformPoints(points);
            local = points[0];
        }

        if (Transform3D is { IsIdentity: false } transform3D)
        {
            local = new Projection3D(
                new RectangleF(0f, 0f, Bounds.Width, Bounds.Height),
                transform3D).Unmap(local);
        }

        if (Clip is { } clip && !clip.Contains(local))
        {
            return false;
        }

        // 自身内容仍按自己的布局框判定；零尺寸时只让子项作答。
        if (zeroSized)
        {
            return HitTestChildren(local);
        }

        if (!UnboundedHitTest && (local.X < 0f || local.Y < 0f || local.X > Bounds.Width || local.Y > Bounds.Height))
        {
            return false;
        }

        if (ClipToBounds && (local.X < 0f || local.Y < 0f || local.X > Bounds.Width || local.Y > Bounds.Height))
        {
            return false;
        }

        return HitTestContent(local) || HitTestChildren(local);
    }

    /// <summary>Finds the deepest visible visual under <paramref name="point"/>.</summary>
    /// <param name="point">The point to test, in the parent's coordinates.</param>
    /// <returns>The hit visual, or <see langword="null"/>.</returns>
    public Visual? HitTestVisual(PointF point)
    {
        if (!IsVisible || Opacity <= 0d)
        {
            return null;
        }

        // An unclipped parent (ClipToBounds=false) does not swallow a child that straddles its bounds:
        // the child is hit-tested first in the parent's local coordinates. The parent's own content still
        // only answers inside its layout rectangle.
        var allowOutside = !ClipToBounds;
        var local = ToLocal(point, allowOutside);
        if (local is null)
        {
            return null;
        }

        if (HitTestChildren(local.Value, out var child))
        {
            return child;
        }

        var outside = local.Value.X < 0f || local.Value.Y < 0f
                      || local.Value.X > Bounds.Width || local.Value.Y > Bounds.Height;
        if (outside && !UnboundedHitTest)
        {
            return null;
        }

        return HitTestContent(local.Value) ? this : null;
    }

    /// <summary>Paints the content of this visual in local coordinates.</summary>
    /// <param name="graphics">The target surface.</param>
    protected virtual void RenderContent(Graphics graphics)
    {
    }

    /// <summary>Paints the children of this visual in local coordinates.</summary>
    /// <param name="graphics">The target surface.</param>
    protected virtual void RenderChildren(Graphics graphics)
    {
    }

    /// <summary>Returns whether the local point hits this visual's own content.</summary>
    /// <param name="point">A point in local coordinates.</param>
    protected virtual bool HitTestContent(PointF point) => true;

    /// <summary>Returns whether a child is hit at the local point.</summary>
    /// <param name="point">A point in local coordinates.</param>
    protected virtual bool HitTestChildren(PointF point) => false;

    /// <summary>Finds the child hit at the local point.</summary>
    /// <param name="point">A point in local coordinates.</param>
    /// <param name="visual">The deepest hit visual.</param>
    protected virtual bool HitTestChildren(PointF point, out Visual? visual)
    {
        visual = null;
        return false;
    }

    private PointF? ToLocal(PointF point, bool allowOutside)
    {
        var zeroSized = Bounds.Width <= 0f || Bounds.Height <= 0f;
        // 零尺寸且允许越界的容器（ClipToBounds=false）仍把点交给子项；否则无从命中。
        if (zeroSized && !allowOutside)
        {
            return null;
        }

        var local = point;
        local.X -= Bounds.X;
        local.Y -= Bounds.Y;
        if (!Transform.IsIdentity)
        {
            using var inverse = Transform.Clone();
            inverse.Invert();
            var points = new[] { local };
            inverse.TransformPoints(points);
            local = points[0];
        }

        if (Transform3D is { IsIdentity: false } transform3D)
        {
            local = new Projection3D(
                new RectangleF(0f, 0f, Bounds.Width, Bounds.Height),
                transform3D).Unmap(local);
        }

        var outside = local.X < 0f || local.Y < 0f || local.X > Bounds.Width || local.Y > Bounds.Height;
        if (!allowOutside && !UnboundedHitTest && outside)
        {
            return null;
        }

        if (Clip is { } clip && !clip.Contains(local))
        {
            return null;
        }

        return local;
    }

    // 缓存位图只装本地内容；Bounds 的平移、Transform 与 Clip 仍由外层 Render 应用，移动缓存节点不需要重画子树。
    private void RenderCached(Graphics target)
    {
        var width = (int)Math.Ceiling(Bounds.Width);
        var height = (int)Math.Ceiling(Bounds.Height);
        if (width <= 0 || height <= 0 || width > 8192 || height > 8192)
        {
            RenderContent(target);
            RenderChildren(target);
            return;
        }

        var key = new CacheKey(width, height, Transform, Clip);
        DrawBitmap(target, EnsureCache(width, height, key), PointF.Empty, Opacity);
    }

    // 3D 路径的内容缓存：只按尺寸失效，变换与透明度变化复用同一张子树位图。
    private Bitmap EnsureContentCache(int width, int height)
    {
        using var identity = new Matrix();
        return EnsureCache(width, height, new CacheKey(width, height, identity, null));
    }

    private Bitmap EnsureCache(int width, int height, CacheKey key)
    {
        if (_cache is not null
            && _cache.Width == width
            && _cache.Height == height
            && !_cacheDirty
            && _cacheKey.Equals(key))
        {
            return _cache;
        }

        _cache?.Dispose();
        _cache = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        // 缓存必须按完整子树生成；脏区剔除只约束当前帧，不能写进位图，否则下一帧会缺内容。
        var previousDamage = _renderDamage;
        var previousCull = _cullToDamage;
        _cullToDamage = false;
        try
        {
            using (var graphics = Graphics.FromImage(_cache))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                RenderContent(graphics);
                RenderChildren(graphics);
            }
        }
        finally
        {
            _renderDamage = previousDamage;
            _cullToDamage = previousCull;
        }

        _cacheDirty = false;
        _cacheKey = key;
        return _cache;
    }

    private readonly struct CacheKey
    {
        private readonly int _width;
        private readonly int _height;
        private readonly float[] _transform;
        private readonly bool _hasClip;
        private readonly RectangleF _clip;

        internal CacheKey(int width, int height, Matrix transform, RectangleF? clip)
        {
            _width = width;
            _height = height;
            _transform = transform.IsIdentity ? Array.Empty<float>() : transform.Elements;
            _hasClip = clip is not null;
            _clip = clip ?? RectangleF.Empty;
        }

        public bool Equals(CacheKey other)
            => _width == other._width
               && _height == other._height
               && _hasClip == other._hasClip
               && _clip.Equals(other._clip)
               && _transform.SequenceEqual(other._transform);

        public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);

        public override int GetHashCode()
            => _width ^ (_height << 16) ^ _clip.GetHashCode() ^ (_hasClip ? 1 : 0);
    }

    // 透明度/效果/3D/遮罩需要把整棵子树先落到一张位图，再按目标 alpha 合成；否则重叠子项会各自透，组透明度就不对。
    private void RenderComposited(Graphics target)
    {
        var has3D = Transform3D is { IsIdentity: false };
        var padding = Effect?.Padding ?? 0f;
        var width = Math.Max(1, (int)Math.Ceiling(Bounds.Width + padding * 2f));
        var height = Math.Max(1, (int)Math.Ceiling(Bounds.Height + padding * 2f));
        if (width > 8192 || height > 8192)
        {
            // 超大视觉不为此分配数百 MB；退化为不合成。调用方应把效果放在较小的组上。
            RenderContent(target);
            RenderChildren(target);
            return;
        }

        // 3D 子树缓存：效果与遮罩会改写/扩边这张位图，保持不缓存路径。
        if (has3D && CacheAsBitmap && Effect is null && OpacityMask is null && Transform3D is { } cachedTransform)
        {
            var cacheWidth = Math.Max(1, (int)Math.Ceiling(Bounds.Width));
            var cacheHeight = Math.Max(1, (int)Math.Ceiling(Bounds.Height));
            if (cacheWidth > 8192 || cacheHeight > 8192)
            {
                RenderContent(target);
                RenderChildren(target);
                return;
            }

            var cached = EnsureContentCache(cacheWidth, cacheHeight);
            DrawProjectedComposited(target, cached, cachedTransform, 0f, cacheWidth, cacheHeight);
            return;
        }

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(padding, padding);
            RenderContent(graphics);
            RenderChildren(graphics);
        }

        if (OpacityMask is { } mask)
        {
            using var maskBitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(maskBitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TranslateTransform(padding, padding);
                mask.Paint(graphics, new RectangleF(0f, 0f, Bounds.Width, Bounds.Height));
            }

            OpacityMaskCompositor.Apply(bitmap, maskBitmap, OpacityMaskMode);
        }

        if (has3D && Transform3D is { } transform3D)
        {
            DrawProjectedComposited(target, bitmap, transform3D, padding, width, height);
            return;
        }

        var destination = new PointF(-padding, -padding);
        if (Effect is { } effect)
        {
            effect.Render(bitmap, target, destination, Opacity);
        }
        else
        {
            DrawBitmap(target, bitmap, destination, Opacity);
        }
    }

    // 3D 合成：先让效果（若有）作用到位图，再把整张图按投影四边形分格贴回。
    private void DrawProjectedComposited(
        Graphics target,
        Bitmap bitmap,
        Transform3D transform3D,
        float padding,
        int width,
        int height)
    {
        var projection = new Projection3D(
            new RectangleF(0f, 0f, Bounds.Width, Bounds.Height),
            transform3D);
        if (projection.IsDegenerate)
        {
            DrawBitmap(target, bitmap, new PointF(-padding, -padding), Opacity);
            return;
        }

        Bitmap? effectBitmap = null;
        try
        {
            var source = bitmap;
            if (Effect is { } effect)
            {
                effectBitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                using (var graphics = Graphics.FromImage(effectBitmap))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    effect.Render(bitmap, graphics, PointF.Empty, 1d);
                }

                source = effectBitmap;
            }

            DrawProjected(
                target,
                source,
                projection,
                new RectangleF(padding, padding, Bounds.Width, Bounds.Height),
                Opacity);
        }
        finally
        {
            effectBitmap?.Dispose();
        }
    }

    // 单应性贴图：逐像素逆映射 + 双线性采样。GDI+ 只有仿射贴图，分格近似会留下透视误差。
    internal static void DrawProjected(
        Graphics target,
        Bitmap source,
        Projection3D projection,
        RectangleF sourceRect,
        double opacity)
    {
        if (sourceRect.Width <= 0f || sourceRect.Height <= 0f || opacity <= 0d)
        {
            return;
        }

        if (projection.IsDegenerate || !TryProjectedQuad(projection, out var quad))
        {
            DrawBitmap(target, source, new PointF(sourceRect.Left, sourceRect.Top), opacity);
            return;
        }

        var bounds = QuadBounds(quad);
        var clip = target.VisibleClipBounds;
        if (IsUsableClip(clip))
        {
            bounds.Intersect(clip);
        }

        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        var left = (int)Math.Floor(bounds.Left);
        var top = (int)Math.Floor(bounds.Top);
        var width = (int)Math.Ceiling(bounds.Right) - left;
        var height = (int)Math.Ceiling(bounds.Bottom) - top;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (width > 4096 || height > 4096 || (long)width * height > MaxProjectedPixels)
        {
            DrawProjectedAffine(target, source, projection, sourceRect, opacity);
            return;
        }

        var sourcePixelWidth = source.Width;
        var sourcePixelHeight = source.Height;
        var sourceData = source.LockBits(
            new Rectangle(0, 0, sourcePixelWidth, sourcePixelHeight),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppPArgb);
        try
        {
            var sourceStride = Math.Abs(sourceData.Stride);
            var sourceBytes = new byte[sourceStride * sourcePixelHeight];
            Marshal.Copy(sourceData.Scan0, sourceBytes, 0, sourceBytes.Length);

            var destinationBytes = new byte[width * height * 4];
            var bottomUp = sourceData.Stride < 0;
            var edgeU = 1.5d / Math.Max(1f, sourceRect.Width);
            var edgeV = 1.5d / Math.Max(1f, sourceRect.Height);
            var sourceLeft = (double)sourceRect.Left;
            var sourceTop = (double)sourceRect.Top;
            var sourceWidth = (double)sourceRect.Width;
            var sourceHeight = (double)sourceRect.Height;
            var coverage = XamlMath.Clamp(opacity, 0d, 1d);

            Parallel.For(0, height, row =>
            {
                var centerY = top + row + 0.5d;
                var rowOffset = row * width * 4;
                for (var column = 0; column < width; column++)
                {
                    var centerX = left + column + 0.5d;
                    if (!projection.TryUnmapNormalized(new PointF((float)centerX, (float)centerY), out var u, out var v))
                    {
                        continue;
                    }

                    var inside = u >= 0d && u <= 1d && v >= 0d && v <= 1d;
                    var nearEdge = u >= -edgeU && u <= 1d + edgeU && v >= -edgeV && v <= 1d + edgeV
                                   && (u < edgeU || u > 1d - edgeU || v < edgeV || v > 1d - edgeV);

                    double blue = 0d, green = 0d, red = 0d, alpha = 0d;
                    if (!nearEdge)
                    {
                        if (!inside)
                        {
                            continue;
                        }

                        SampleBilinear(
                            sourceBytes, sourceStride, sourcePixelWidth, sourcePixelHeight, bottomUp,
                            sourceLeft + (u * sourceWidth), sourceTop + (v * sourceHeight),
                            out blue, out green, out red, out alpha);
                    }
                    else
                    {
                        var samples = 0;
                        for (var subY = 0; subY < 2; subY++)
                        {
                            var sampleY = top + row + (subY == 0 ? 0.25d : 0.75d);
                            for (var subX = 0; subX < 2; subX++)
                            {
                                var sampleX = left + column + (subX == 0 ? 0.25d : 0.75d);
                                if (!projection.TryUnmapNormalized(new PointF((float)sampleX, (float)sampleY), out var su, out var sv)
                                    || su < 0d || su > 1d || sv < 0d || sv > 1d)
                                {
                                    continue;
                                }

                                samples++;
                                SampleBilinear(
                                    sourceBytes, sourceStride, sourcePixelWidth, sourcePixelHeight, bottomUp,
                                    sourceLeft + (su * sourceWidth), sourceTop + (sv * sourceHeight),
                                    out var sb, out var sg, out var sr, out var sa);
                                blue += sb;
                                green += sg;
                                red += sr;
                                alpha += sa;
                            }
                        }

                        if (samples == 0)
                        {
                            continue;
                        }

                        // 未覆盖的子样本贡献 0：除以 4 就是覆盖率加权的平均色。
                        blue /= 4d;
                        green /= 4d;
                        red /= 4d;
                        alpha /= 4d;
                    }

                    var offset = rowOffset + (column * 4);
                    destinationBytes[offset] = ToByte(blue * coverage);
                    destinationBytes[offset + 1] = ToByte(green * coverage);
                    destinationBytes[offset + 2] = ToByte(red * coverage);
                    destinationBytes[offset + 3] = ToByte(alpha * coverage);
                }
            });

            using var result = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            var resultData = result.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppPArgb);
            try
            {
                Marshal.Copy(destinationBytes, 0, resultData.Scan0, destinationBytes.Length);
            }
            finally
            {
                result.UnlockBits(resultData);
            }

            var state = target.Save();
            try
            {
                target.InterpolationMode = InterpolationMode.HighQualityBilinear;
                target.PixelOffsetMode = PixelOffsetMode.Half;
                target.DrawImage(result, left, top);
            }
            finally
            {
                target.Restore(state);
            }
        }
        finally
        {
            source.UnlockBits(sourceData);
        }
    }

    private const long MaxProjectedPixels = 16_000_000L;

    private static bool TryProjectedQuad(Projection3D projection, out PointF[] quad)
    {
        quad =
        [
            projection.MapNormalized(0d, 0d),
            projection.MapNormalized(1d, 0d),
            projection.MapNormalized(1d, 1d),
            projection.MapNormalized(0d, 1d),
        ];

        foreach (var point in quad)
        {
            if (!XamlMath.IsFinite(point.X) || !XamlMath.IsFinite(point.Y))
            {
                return false;
            }
        }

        return true;
    }

    private static RectangleF QuadBounds(IReadOnlyList<PointF> quad)
    {
        var minX = quad[0].X;
        var minY = quad[0].Y;
        var maxX = minX;
        var maxY = minY;
        for (var index = 1; index < quad.Count; index++)
        {
            var point = quad[index];
            if (point.X < minX) minX = point.X;
            if (point.Y < minY) minY = point.Y;
            if (point.X > maxX) maxX = point.X;
            if (point.Y > maxY) maxY = point.Y;
        }

        return RectangleF.FromLTRB(minX, minY, maxX, maxY);
    }

    private static bool IsUsableClip(RectangleF clip)
        => clip.Width > 0f
           && clip.Height > 0f
           && XamlMath.IsFinite(clip.Width)
           && XamlMath.IsFinite(clip.Height)
           && clip.Width < 1_000_000f
           && clip.Height < 1_000_000f;

    private static byte ToByte(double value)
        => value <= 0d ? (byte)0 : value >= 255d ? (byte)255 : (byte)Math.Round(value);

    private static void SampleBilinear(
        byte[] pixels,
        int stride,
        int width,
        int height,
        bool bottomUp,
        double x,
        double y,
        out double blue,
        out double green,
        out double red,
        out double alpha)
    {
        var sampleX = x - 0.5d;
        var sampleY = y - 0.5d;
        var x0 = (int)Math.Floor(sampleX);
        var y0 = (int)Math.Floor(sampleY);
        var fx = sampleX - x0;
        var fy = sampleY - y0;

        ReadPremultiplied(pixels, stride, width, height, bottomUp, x0, y0, out var b00, out var g00, out var r00, out var a00);
        ReadPremultiplied(pixels, stride, width, height, bottomUp, x0 + 1, y0, out var b10, out var g10, out var r10, out var a10);
        ReadPremultiplied(pixels, stride, width, height, bottomUp, x0, y0 + 1, out var b01, out var g01, out var r01, out var a01);
        ReadPremultiplied(pixels, stride, width, height, bottomUp, x0 + 1, y0 + 1, out var b11, out var g11, out var r11, out var a11);

        var w00 = (1d - fx) * (1d - fy);
        var w10 = fx * (1d - fy);
        var w01 = (1d - fx) * fy;
        var w11 = fx * fy;
        blue = (b00 * w00) + (b10 * w10) + (b01 * w01) + (b11 * w11);
        green = (g00 * w00) + (g10 * w10) + (g01 * w01) + (g11 * w11);
        red = (r00 * w00) + (r10 * w10) + (r01 * w01) + (r11 * w11);
        alpha = (a00 * w00) + (a10 * w10) + (a01 * w01) + (a11 * w11);
    }

    private static void ReadPremultiplied(
        byte[] pixels,
        int stride,
        int width,
        int height,
        bool bottomUp,
        int x,
        int y,
        out double blue,
        out double green,
        out double red,
        out double alpha)
    {
        x = x < 0 ? 0 : x >= width ? width - 1 : x;
        y = y < 0 ? 0 : y >= height ? height - 1 : y;
        var row = bottomUp ? height - 1 - y : y;
        var offset = (row * stride) + (x * 4);
        blue = pixels[offset];
        green = pixels[offset + 1];
        red = pixels[offset + 2];
        alpha = pixels[offset + 3];
    }

    // 大尺寸/退化时的旧路径：GDI+ 仿射分格贴图。
    private static void DrawProjectedAffine(
        Graphics target,
        Bitmap source,
        Projection3D projection,
        RectangleF sourceRect,
        double opacity)
    {
        if (projection.IsDegenerate || !TryProjectedQuad(projection, out _))
        {
            DrawBitmap(target, source, new PointF(sourceRect.Left, sourceRect.Top), opacity);
            return;
        }

        var state = target.Save();
        try
        {
            target.InterpolationMode = InterpolationMode.HighQualityBilinear;
            target.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using var attributes = opacity < 0.999d ? new ImageAttributes() : null;
            if (attributes is not null)
            {
                var matrix = new ColorMatrix { Matrix33 = (float)XamlMath.Clamp(opacity, 0d, 1d) };
                attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            }

            var cells = XamlMath.Clamp(
                (int)Math.Ceiling(Math.Max(sourceRect.Width, sourceRect.Height) / 24f),
                6,
                32);
            var destination = new PointF[3];
            for (var row = 0; row < cells; row++)
            {
                var v0 = (double)row / cells;
                var v1 = (double)(row + 1) / cells;
                for (var column = 0; column < cells; column++)
                {
                    var u0 = (double)column / cells;
                    var u1 = (double)(column + 1) / cells;

                    // 源格右/下各多取半像素，盖住相邻两次仿射贴图之间的接缝。
                    var left = sourceRect.Left + ((float)u0 * sourceRect.Width);
                    var top = sourceRect.Top + ((float)v0 * sourceRect.Height);
                    var right = Math.Min(
                        sourceRect.Right,
                        sourceRect.Left + ((float)u1 * sourceRect.Width) + 0.5f);
                    var bottom = Math.Min(
                        sourceRect.Bottom,
                        sourceRect.Top + ((float)v1 * sourceRect.Height) + 0.5f);
                    var sourceCell = RectangleF.FromLTRB(left, top, right, bottom);
                    if (sourceCell.Width <= 0f || sourceCell.Height <= 0f)
                    {
                        continue;
                    }

                    destination[0] = projection.MapNormalized(u0, v0);
                    destination[1] = projection.MapNormalized(u1, v0);
                    destination[2] = projection.MapNormalized(u0, v1);
                    if (!IsDrawableCell(destination))
                    {
                        continue;
                    }

                    if (attributes is null)
                    {
                        target.DrawImage(source, destination, sourceCell, GraphicsUnit.Pixel);
                    }
                    else
                    {
                        target.DrawImage(source, destination, sourceCell, GraphicsUnit.Pixel, attributes);
                    }
                }
            }
        }
        finally
        {
            target.Restore(state);
        }
    }

    private static bool IsDrawableCell(PointF[] points)
    {
        foreach (var point in points)
        {
            if (!XamlMath.IsFinite(point.X) || !XamlMath.IsFinite(point.Y))
            {
                return false;
            }
        }

        // GDI+ 拒绝退化平行四边形：任意两边共线时跳过这一格。
        var cross = ((points[1].X - points[0].X) * (double)(points[2].Y - points[0].Y))
                    - ((points[1].Y - points[0].Y) * (double)(points[2].X - points[0].X));
        return Math.Abs(cross) >= 0.01d;
    }

    internal static void DrawBitmap(Graphics target, Bitmap bitmap, PointF destination, double opacity)
    {
        if (opacity >= 0.999d)
        {
            target.DrawImage(bitmap, destination);
            return;
        }

        using var attributes = new ImageAttributes();
        var matrix = new ColorMatrix { Matrix33 = (float)XamlMath.Clamp(opacity, 0d, 1d) };
        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        target.DrawImage(
            bitmap,
            new Rectangle((int)Math.Round(destination.X), (int)Math.Round(destination.Y), bitmap.Width, bitmap.Height),
            0f,
            0f,
            bitmap.Width,
            bitmap.Height,
            GraphicsUnit.Pixel,
            attributes);
    }
}

/// <summary>A visual that owns an ordered collection of child visuals and an optional background.</summary>
public class ContainerVisual : Visual
{
    private readonly VisualCollection _children;
    private readonly List<(Visual Visual, int Index)> _zOrder = [];

    /// <summary>Creates an empty container.</summary>
    public ContainerVisual()
    {
        _children = new VisualCollection(this);
    }

    /// <summary>The child visuals, painted in declaration order.</summary>
    public IList<Visual> Children => _children;

    /// <summary>An optional background brush painted over the local bounds.</summary>
    public Brush? Background { get; set; }

    /// <summary>The background corner radius in pixels.</summary>
    public float CornerRadius { get; set; }

    /// <summary>Adds a child and sets its <see cref="Visual.Parent"/>.</summary>
    /// <param name="visual">The child to add.</param>
    public void Add(Visual visual) => _children.Add(visual);

    /// <summary>Removes a child.</summary>
    /// <param name="visual">The child to remove.</param>
    /// <returns><see langword="true"/> when the child was present.</returns>
    public bool Remove(Visual visual) => _children.Remove(visual);

    /// <inheritdoc />
    protected override void RenderContent(Graphics graphics)
    {
        if (Background is not { } background || Bounds.Width <= 0f || Bounds.Height <= 0f)
        {
            return;
        }

        using var path = Easel.Controls.XamlDrawing.RoundedRectangle(
            new RectangleF(0f, 0f, Bounds.Width, Bounds.Height),
            CornerRadius);
        background.FillPath(graphics, path);
    }

    /// <inheritdoc />
    protected override void RenderChildren(Graphics graphics)
    {
        foreach (var (child, _) in BuildZOrder())
        {
            if (ShouldRenderChild(child))
            {
                child.Render(graphics);
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>A non-null background is hit-testable even when transparent, matching WPF.</remarks>
    protected override bool HitTestContent(PointF point)
        => Background is not null;

    /// <inheritdoc />
    protected override bool HitTestChildren(PointF point) => HitTestChildren(point, out _);

    /// <inheritdoc />
    protected override bool HitTestChildren(PointF point, out Visual? visual)
    {
        // 命中顺序与绘制相反：ZIndex 高的先命中，同值按声明顺序的逆序（后声明的在上）。
        var order = BuildZOrder();
        for (var index = order.Count - 1; index >= 0; index--)
        {
            var child = order[index].Visual;
            if (child is null)
            {
                continue;
            }

            var hit = child.HitTestVisual(point);
            if (hit is not null)
            {
                visual = hit;
                return true;
            }
        }

        visual = null;
        return false;
    }

    // 稳定的 ZIndex 序：List.Sort 不稳定，用原始下标打破平局，保证同值子项保持声明顺序。
    private List<(Visual Visual, int Index)> BuildZOrder()
    {
        _zOrder.Clear();
        for (var index = 0; index < _children.Count; index++)
        {
            var child = _children[index];
            if (child is not null)
            {
                _zOrder.Add((child, index));
            }
        }

        _zOrder.Sort(static (left, right) =>
        {
            var byZIndex = ZIndexOf(left.Visual).CompareTo(ZIndexOf(right.Visual));
            return byZIndex != 0 ? byZIndex : left.Index.CompareTo(right.Index);
        });
        return _zOrder;
    }

    private static int ZIndexOf(Visual visual)
        => visual is VisualElement element ? Controls.Panel.GetZIndex(element) : 0;

    private sealed class VisualCollection(ContainerVisual owner) : System.Collections.ObjectModel.Collection<Visual>
    {
        protected override void InsertItem(int index, Visual item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));
            item.Parent = owner;
            base.InsertItem(index, item);
        }

        protected override void SetItem(int index, Visual item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));
            item.Parent = owner;
            base.SetItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            this[index].Parent = null;
            base.RemoveItem(index);
        }

        protected override void ClearItems()
        {
            foreach (var item in this)
            {
                item.Parent = null;
            }

            base.ClearItems();
        }
    }
}

/// <summary>A visual effect composed over a rendered subtree.</summary>
public abstract class VisualEffect
{
    /// <summary>The extra space the effect needs around the visual's bounds, in pixels.</summary>
    public abstract float Padding { get; }

    /// <summary>Draws <paramref name="source"/> with this effect.</summary>
    /// <param name="source">The rendered subtree.</param>
    /// <param name="target">The destination surface, already in the visual's local coordinates.</param>
    /// <param name="destination">Where the source's top-left corner should be drawn.</param>
    /// <param name="opacity">The visual's opacity in the range 0..1.</param>
    public abstract void Render(Bitmap source, Graphics target, PointF destination, double opacity);
}

/// <summary>A soft drop shadow painted behind the visual's rendered subtree.</summary>
public sealed class DropShadowEffect : VisualEffect
{
    /// <summary>The shadow offset from the source's top-left corner, in pixels.</summary>
    public PointF Offset { get; set; } = Theme.Current.ShadowOffset;

    /// <summary>A softness radius in pixels; zero draws a crisp offset copy.</summary>
    public float Blur { get; set; } = Theme.Current.ShadowBlur;

    /// <summary>The shadow colour.</summary>
    public Color Color { get; set; } = Theme.Current.ShadowColor;

    /// <summary>The shadow opacity in the range 0..1.</summary>
    public double Opacity { get; set; } = Theme.Current.ShadowOpacity;

    /// <inheritdoc />
    public override float Padding => Math.Max(0f, Math.Max(Math.Abs(Offset.X), Math.Abs(Offset.Y)) + Blur);

    /// <inheritdoc />
    public override void Render(Bitmap source, Graphics target, PointF destination, double opacity)
    {
        var shadowOpacity = XamlMath.Clamp(Opacity * opacity, 0d, 1d);
        var blur = Math.Max(0f, Blur);

        // 用若干次低 alpha 偏移绘制近似柔化；GDI+ 没有可用的模糊原语，这样足够小且不分配像素缓冲。
        var taps = blur <= 0f ? 1 : 5;
        for (var tap = 0; tap < taps; tap++)
        {
            var angle = Math.PI * 2d * tap / taps;
            var dx = blur <= 0f ? Offset.X : Offset.X + (float)Math.Cos(angle) * blur * 0.5f;
            var dy = blur <= 0f ? Offset.Y : Offset.Y + (float)Math.Sin(angle) * blur * 0.5f;
            DrawTinted(target, source, new PointF(destination.X + dx, destination.Y + dy), shadowOpacity / taps);
        }

        Visual.DrawBitmap(target, source, destination, opacity);
    }

    private void DrawTinted(Graphics target, Bitmap source, PointF destination, double opacity)
    {
        using var attributes = new ImageAttributes();
        var color = Color;
        var matrix = new ColorMatrix
        {
            Matrix00 = 0f,
            Matrix11 = 0f,
            Matrix22 = 0f,
            Matrix33 = (float)XamlMath.Clamp(opacity, 0d, 1d),
            Matrix40 = color.R / 255f,
            Matrix41 = color.G / 255f,
            Matrix42 = color.B / 255f,
        };
        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        target.DrawImage(
            source,
            new Rectangle((int)Math.Round(destination.X), (int)Math.Round(destination.Y), source.Width, source.Height),
            0f,
            0f,
            source.Width,
            source.Height,
            GraphicsUnit.Pixel,
            attributes);
    }
}
