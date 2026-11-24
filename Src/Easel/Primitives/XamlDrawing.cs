using System.Drawing;
using System.Drawing.Drawing2D;

namespace Easel.Controls;

// 自绘控件共用的小工具：圆角路径与「底色 + 描边」的单一入口，避免每个控件各写一份 GDI+ 逻辑。
internal static class XamlDrawing
{
    // 原生子窗口不认 alpha：透明落到 fallback，半透明取不透明部分。
    public static Color ToOpaque(Color color, Color fallback)
    {
        if (color.A == 0)
        {
            return fallback;
        }

        return color.A == 255 ? color : Color.FromArgb(255, color.R, color.G, color.B);
    }

    public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        => RoundedRectangle(bounds, radius, radius);

    public static GraphicsPath RoundedRectangle(RectangleF bounds, float radiusX, float radiusY)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return path;
        }

        var rx = Math.Min(Math.Max(0f, radiusX), bounds.Width / 2f);
        var ry = Math.Min(Math.Max(0f, radiusY), bounds.Height / 2f);
        if (rx <= 0f || ry <= 0f)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var diameterX = 2f * rx;
        var diameterY = 2f * ry;
        path.AddArc(bounds.X, bounds.Y, diameterX, diameterY, 180f, 90f);
        path.AddArc(bounds.Right - diameterX, bounds.Y, diameterX, diameterY, 270f, 90f);
        path.AddArc(bounds.Right - diameterX, bounds.Bottom - diameterY, diameterX, diameterY, 0f, 90f);
        path.AddArc(bounds.X, bounds.Bottom - diameterY, diameterX, diameterY, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    public static void PaintChrome(
        Graphics graphics,
        Rectangle bounds,
        Color background,
        Color borderBrush,
        Thickness borderThickness,
        float cornerRadius)
        => PaintChromeCore(
            graphics,
            bounds,
            borderThickness,
            cornerRadius,
            background.A > 0 ? background : null,
            borderBrush.A > 0 ? borderBrush : null,
            fillBrush: null,
            strokeBrush: null);

    // Brush 版单独命名：Brush 与 Color 之间有双向隐式转换，同名重载会二义。
    public static void PaintChromeWithBrushes(
        Graphics graphics,
        Rectangle bounds,
        Brush? background,
        Brush? borderBrush,
        Thickness borderThickness,
        float cornerRadius)
        => PaintChromeCore(
            graphics,
            bounds,
            borderThickness,
            cornerRadius,
            fillColor: null,
            strokeColor: null,
            fillBrush: background is { ApproximateColor.A: > 0 } fill ? fill : null,
            strokeBrush: borderBrush is { ApproximateColor.A: > 0 } stroke ? stroke : null);

    // 纯色走线程内的画笔池，非纯色笔刷交给它自己的 FillPath；两条路都不为每次绘制建委托。
    private static void PaintChromeCore(
        Graphics graphics,
        Rectangle bounds,
        Thickness borderThickness,
        float cornerRadius,
        Color? fillColor,
        Color? strokeColor,
        Brush? fillBrush,
        Brush? strokeBrush)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        // 外轮廓压到 -0.5：GDI+ 以像素中心取样，贴 0 画环会让左、上各占半像素、右、下被裁掉半像素。
        // bounds 可能不是从 (0,0) 开始（勾选块/开关轨道要在控件内居中），偏移也必须带上。
        var body = new RectangleF(bounds.X - 0.5f, bounds.Y - 0.5f, bounds.Width, bounds.Height);
        using var path = RoundedRectangle(body, cornerRadius);
        FillPath(graphics, path, fillColor, fillBrush);

        var left = Math.Max(0f, borderThickness.Left);
        var top = Math.Max(0f, borderThickness.Top);
        var right = Math.Max(0f, borderThickness.Right);
        var bottom = Math.Max(0f, borderThickness.Bottom);
        if ((strokeColor is null && strokeBrush is null) || (left <= 0f && top <= 0f && right <= 0f && bottom <= 0f))
        {
            return;
        }

        var inner = new RectangleF(
            body.X + left,
            body.Y + top,
            body.Width - left - right,
            body.Height - top - bottom);
        if (inner.Width <= 0f || inner.Height <= 0f)
        {
            FillPath(graphics, path, strokeColor, strokeBrush);
            return;
        }

        var inset = Math.Max(Math.Max(left, right), Math.Max(top, bottom));
        using var innerPath = RoundedRectangle(inner, Math.Max(0f, cornerRadius - inset));
        path.FillMode = FillMode.Alternate;
        path.AddPath(innerPath, connect: false);
        FillPath(graphics, path, strokeColor, strokeBrush);
    }

    private static void FillPath(Graphics graphics, GraphicsPath path, Color? color, Brush? brush)
    {
        if (color is { } solid)
        {
            var native = NativeBrushPool.Get(solid, out var owns);
            try
            {
                graphics.FillPath(native, path);
            }
            finally
            {
                if (owns)
                {
                    native.Dispose();
                }
            }

            return;
        }

        if (brush is not null)
        {
            brush.FillPath(graphics, path);
        }
    }
}
