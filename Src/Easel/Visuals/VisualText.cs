using System.Drawing;
using System.Drawing.Text;

namespace Easel.Visuals;

// GDI TextRenderer 直接写 HDC，不认 GDI+ 的 world transform；视觉树的平移全靠 Graphics.Transform，
// 所以这里的文字必须走 DrawString。测量也走同一个 Graphics，字符宽度才会和绘制一致（TextBox 的插入符依赖它）。
internal static class VisualText
{
    private static readonly Dictionary<int, Graphics> MeasureContexts = [];

    private static Graphics MeasureContext(int dpi)
    {
        if (MeasureContexts.TryGetValue(dpi, out var graphics))
        {
            return graphics;
        }

        var bitmap = new Bitmap(1, 1);
        bitmap.SetResolution(dpi, dpi);
        graphics = Graphics.FromImage(bitmap);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        MeasureContexts[dpi] = graphics;
        return graphics;
    }

    public static SizeF Measure(string text, Font font, float maxWidth, bool wrap, int dpi)
    {
        var format = Format(wrap, StringAlignment.Near, StringAlignment.Near);
        var width = maxWidth > 0f ? maxWidth : 1_000_000f;
        var measured = MeasureContext(dpi).MeasureString(text, font, new SizeF(width, 1_000_000f), format);
        return new SizeF((float)Math.Ceiling(measured.Width), (float)Math.Ceiling(measured.Height));
    }

    public static float MeasureWidth(string text, Font font, int dpi)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        var format = NoTrimFormat();
        return MeasureContext(dpi).MeasureString(text, font, PointF.Empty, format).Width;
    }

    public static void Draw(
        Visual owner,
        Graphics graphics,
        string text,
        Font font,
        Color color,
        RectangleF bounds,
        bool wrap,
        StringAlignment horizontal,
        StringAlignment vertical)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        var native = NativeBrushPool.Get(color, out var owns);
        try
        {
            DrawCore(owner, graphics, text, font, native, bounds, wrap, horizontal, vertical);
        }
        finally
        {
            if (owns)
            {
                native.Dispose();
            }
        }
    }

    public static void Draw(
        Visual owner,
        Graphics graphics,
        string text,
        Font font,
        Easel.Brush? brush,
        RectangleF bounds,
        bool wrap,
        StringAlignment horizontal,
        StringAlignment vertical)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        // 文本用 DrawString 的光栅笔刷路径：渐变/图片笔刷直接交给 GDI+，不再退化成 ApproximateColor。
        // 纯色笔刷借线程内的原生画笔，不再每帧新建一个 GDI+ 对象。
        bool owns = false;
        System.Drawing.Brush? native = null;
        if (brush is not null)
        {
            native = brush.AcquireNativeBrush(bounds, out owns);
        }

        if (native is null)
        {
            native = NativeBrushPool.Get(brush?.ApproximateColor ?? Theme.Current.Foreground, out owns);
        }

        try
        {
            DrawCore(owner, graphics, text, font, native, bounds, wrap, horizontal, vertical);
        }
        finally
        {
            if (owns)
            {
                native.Dispose();
            }
        }
    }

    private static void DrawCore(
        Visual owner,
        Graphics graphics,
        string text,
        Font font,
        System.Drawing.Brush native,
        RectangleF bounds,
        bool wrap,
        StringAlignment horizontal,
        StringAlignment vertical)
    {
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.DrawString(text, owner.ResolveDrawFont(font, graphics), native, bounds, Format(wrap, horizontal, vertical));
    }

    // 格式对象按「换行 × 水平 × 垂直」缓存到当前线程：绘制期每帧都要，GDI+ 格式本身不必每帧新建。
    [ThreadStatic]
    private static StringFormat?[,]? formatCache;

    internal static StringFormat Format(
        bool wrap, StringAlignment horizontal, StringAlignment vertical, bool ellipsisWhenWrapped = false)
    {
        var table = formatCache ??= new StringFormat?[12, 3];
        var row = (wrap ? 3 : 0) + (ellipsisWhenWrapped ? 6 : 0) + ClampAlignment(horizontal);
        var column = ClampAlignment(vertical);
        return table[row, column] ??= new StringFormat
        {
            Alignment = horizontal,
            LineAlignment = vertical,
            Trimming = ellipsisWhenWrapped || !wrap ? StringTrimming.EllipsisCharacter : StringTrimming.None,
            FormatFlags = wrap ? 0 : StringFormatFlags.NoWrap,
        };
    }

    [ThreadStatic]
    private static StringFormat? noTrimFormat;

    private static StringFormat NoTrimFormat()
        => noTrimFormat ??= new StringFormat(StringFormatFlags.NoWrap)
        {
            Trimming = StringTrimming.None,
        };

    private static int ClampAlignment(StringAlignment alignment)
        => alignment switch
        {
            StringAlignment.Near => 0,
            StringAlignment.Center => 1,
            StringAlignment.Far => 2,
            _ => 0,
        };
}
