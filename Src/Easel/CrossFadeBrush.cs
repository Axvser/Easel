using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Easel;

/// <summary>
/// Paints two brushes into separate layers and blends the layers, so a pair with no shared structure can still
/// cross over.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BrushInterpolation"/> interpolates brushes that share a kind. A linear gradient against a radial
/// one, or any pair involving an <see cref="ImageBrush"/>, has no shared parameter to move, so this brush takes
/// the composition route instead: each side paints once into its own layer and the layers are drawn with the
/// complementary alpha. The result is the same blend a renderer with a compositor would produce, without
/// teaching every brush kind about every other one.
/// </para>
/// <para>
/// Layers are rasterized at the target surface's device scale, so a cross-fade stays sharp on a high-DPI
/// monitor. The cost is two offscreen layers per paint; reach for it only where <see cref="BrushInterpolation"/>
/// cannot stay structural.
/// </para>
/// </remarks>
public sealed class CrossFadeBrush : Brush
{
    /// <summary>Layers larger than this on either axis are not composed; the dominant side paints alone.</summary>
    private const int MaxLayerSize = 4096;

    /// <summary>Creates a cross-fade between two brushes.</summary>
    /// <param name="from">The brush at progress zero; <see langword="null"/> paints nothing.</param>
    /// <param name="to">The brush at progress one; <see langword="null"/> paints nothing.</param>
    /// <param name="progress">How far the blend has moved toward <paramref name="to"/>.</param>
    public CrossFadeBrush(Brush? from, Brush? to, double progress)
    {
        From = from;
        To = to;
        Progress = progress;
    }

    /// <summary>The brush painted at progress zero.</summary>
    public Brush? From { get; }

    /// <summary>The brush painted at progress one.</summary>
    public Brush? To { get; }

    /// <summary>How far the blend has moved from <see cref="From"/> to <see cref="To"/>.</summary>
    public double Progress { get; }

    /// <inheritdoc />
    public override Color ApproximateColor
        => BrushInterpolation.LerpColor(
            From?.ApproximateColor ?? Color.Transparent,
            To?.ApproximateColor ?? Color.Transparent,
            Progress);

    /// <inheritdoc />
    public override void Paint(Graphics graphics, RectangleF bounds)
    {
        if (graphics is null || bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        if (Progress <= 0d)
        {
            From?.Paint(graphics, bounds);
            return;
        }

        if (Progress >= 1d)
        {
            To?.Paint(graphics, bounds);
            return;
        }

        PaintLayers(graphics, bounds, null);
    }

    /// <inheritdoc />
    public override void FillPath(Graphics graphics, GraphicsPath path)
    {
        if (graphics is null || path is null || path.PointCount == 0)
        {
            return;
        }

        if (Progress <= 0d)
        {
            From?.FillPath(graphics, path);
            return;
        }

        if (Progress >= 1d)
        {
            To?.FillPath(graphics, path);
            return;
        }

        PaintLayers(graphics, path.GetBounds(), path);
    }

    private void PaintLayers(Graphics graphics, RectangleF bounds, GraphicsPath? path)
    {
        var scale = DeviceScale(graphics);
        var width = (int)Math.Ceiling(bounds.Width * scale);
        var height = (int)Math.Ceiling(bounds.Height * scale);
        if (width <= 0 || height <= 0 || width > MaxLayerSize || height > MaxLayerSize)
        {
            // 画面太大时不为它分配两层位图：退化成占优的那一侧，至少不会空白。
            var dominant = Progress < 0.5d ? From : To;
            if (path is null)
            {
                dominant?.Paint(graphics, bounds);
            }
            else
            {
                dominant?.FillPath(graphics, path);
            }

            return;
        }

        var state = graphics.Save();
        try
        {
            if (path is not null)
            {
                graphics.SetClip(path, CombineMode.Intersect);
            }

            var start = RenderLayer(From, bounds, width, height, scale);
            var end = RenderLayer(To, bounds, width, height, scale);
            try
            {
                if (start is null)
                {
                    // 只有终点：按 Progress 淡入，而不是一上来就全显。
                    Blit(graphics, end, bounds, width, height, Progress);
                    return;
                }

                if (end is null)
                {
                    // 只有起点：按 1 - Progress 淡出。
                    Blit(graphics, start, bounds, width, height, 1d - Progress);
                    return;
                }

                // 交叉淡化必须在图层里完成，而且必须是逐像素的线性混合：over 叠放只在源完全不透明时
                // 才等于淡化 —— 径向渐变只覆盖椭圆内部，椭圆外会原样留下起点层，交界处就是一道硬边。
                Blend(start, end, Progress);
                Blit(graphics, start, bounds, width, height, 1d);
            }
            finally
            {
                BrushLayerPool.Return(start);
                BrushLayerPool.Return(end);
            }
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    private static Bitmap? RenderLayer(
        Brush? brush,
        RectangleF bounds,
        int width,
        int height,
        float scale)
    {
        if (brush is null)
        {
            return null;
        }

        Bitmap layer;
        try
        {
            // 池里拿到的是已经清空、尺寸正好的图层；动画期间同一尺寸会一直复用这两张。
            layer = BrushLayerPool.Rent(width, height);
        }
        catch (Exception exception) when (exception is OutOfMemoryException or ArgumentException)
        {
            return null;
        }

        try
        {
            using var offscreen = Graphics.FromImage(layer);
            offscreen.ScaleTransform(scale, scale);
            offscreen.TranslateTransform(-bounds.X, -bounds.Y);
            brush.Paint(offscreen, bounds);
            return layer;
        }
        catch
        {
            BrushLayerPool.Return(layer);
            throw;
        }
    }

    // 预乘 32bpp 图层的逐字节线性混合：out = (1 - progress) * start + progress * end。
    // 四个通道一起插值，alpha 因此也按比例走；椭圆外的终点像素是透明的，起点层就按 1 - progress 淡出。
    private static void Blend(Bitmap start, Bitmap end, double progress)
    {
        if (progress <= 0d || start.Width != end.Width || start.Height != end.Height)
        {
            return;
        }

        var bounds = new Rectangle(0, 0, start.Width, start.Height);
        var write = start.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
        var read = end.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var length = Math.Abs(write.Stride) * start.Height;
            var source = new byte[length];
            var destination = new byte[length];
            Marshal.Copy(write.Scan0, destination, 0, length);
            Marshal.Copy(read.Scan0, source, 0, length);

            var amount = (float)Math.Min(1d, progress);
            var rest = 1f - amount;
            for (var index = 0; index < length; index++)
            {
                destination[index] = (byte)((rest * destination[index]) + (amount * source[index]) + 0.5f);
            }

            Marshal.Copy(destination, 0, write.Scan0, length);
        }
        finally
        {
            start.UnlockBits(write);
            end.UnlockBits(read);
        }
    }

    // 三点形式与 ImageBrush 用的是同一条重载：目标矩形为 bounds，源矩形为整张图层。
    private static void Blit(
        Graphics graphics,
        Bitmap? layer,
        RectangleF bounds,
        int width,
        int height,
        double alpha)
    {
        if (layer is null || alpha <= 0d)
        {
            return;
        }

        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix { Matrix33 = (float)Math.Min(1d, alpha) });
        graphics.DrawImage(
            layer,
            new[]
            {
                new PointF(bounds.Left, bounds.Top),
                new PointF(bounds.Right, bounds.Top),
                new PointF(bounds.Left, bounds.Bottom),
            },
            new RectangleF(0f, 0f, width, height),
            GraphicsUnit.Pixel,
            attributes);
    }

    // 目标 Graphics 可能带着 DPI 的世界变换；按它栅格化，高 DPI 下这一层才不会先按逻辑尺寸画再放大。
    private static float DeviceScale(Graphics graphics)
    {
        // Graphics.Transform 返回的是副本，用完要还回 GDI+。
        using var transform = graphics.Transform;
        var elements = transform.Elements;
        var scale = Math.Max(Math.Abs(elements[0]), Math.Abs(elements[3]));
        if (!XamlMath.IsFinite(scale) || scale < 1f)
        {
            return 1f;
        }

        return Math.Min(scale, 4f);
    }
}
