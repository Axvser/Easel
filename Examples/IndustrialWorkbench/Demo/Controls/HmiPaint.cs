using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using XamlBrush = Easel.Brush;

namespace IndustrialWorkbench.Controls;

/// <summary>Shared GDI+ helpers used by the self-drawn HMI controls.</summary>
/// <remarks>Everything is measured in logical DIPs; the host scales the <see cref="Graphics"/> for DPI.</remarks>
internal static class HmiPaint
{
    private static readonly ConcurrentDictionary<(int Size, FontStyle Style), Font> Fonts = new();

    /// <summary>The monospaced face used for numeric readouts.</summary>
    public const string MonoFamily = "Consolas";

    /// <summary>Returns a cached pixel-sized monospaced font.</summary>
    public static Font Mono(float pixelSize, FontStyle style = FontStyle.Regular)
    {
        var key = (Math.Max(6, (int)Math.Round(pixelSize)), style);
        return Fonts.GetOrAdd(key, static k => new Font(MonoFamily, k.Size, k.Style, GraphicsUnit.Pixel));
    }

    /// <summary>Returns a cached pixel-sized UI font.</summary>
    public static Font Ui(float pixelSize, FontStyle style = FontStyle.Regular)
    {
        var key = (Math.Max(6, (int)Math.Round(pixelSize)), style | FontStyle.Regular);
        return Fonts.GetOrAdd(key, static k => new Font("Segoe UI", k.Size, k.Style, GraphicsUnit.Pixel));
    }

    /// <summary>Multiplies a colour's alpha channel.</summary>
    public static Color Fade(Color color, double factor)
        => Color.FromArgb(
            Math.Clamp((int)Math.Round(color.A * factor), 0, 255),
            color.R,
            color.G,
            color.B);

    /// <summary>Mixes two colours; <paramref name="amount"/> 0 returns <paramref name="a"/>.</summary>
    public static Color Mix(Color a, Color b, double amount)
    {
        var t = Math.Clamp(amount, 0d, 1d);
        return Color.FromArgb(
            (int)Math.Round(a.A + ((b.A - a.A) * t)),
            (int)Math.Round(a.R + ((b.R - a.R) * t)),
            (int)Math.Round(a.G + ((b.G - a.G) * t)),
            (int)Math.Round(a.B + ((b.B - a.B) * t)));
    }

    /// <summary>Reads the representative colour of a brush, falling back when it is null or invisible.</summary>
    public static Color ColorOf(XamlBrush? brush, Color fallback)
    {
        var color = brush?.ApproximateColor;
        return color is { A: > 0 } c ? c : fallback;
    }

    /// <summary>Draws text with no wrapping and an ellipsis for overflow.</summary>
    public static void Text(
        Graphics graphics,
        string? text,
        Font font,
        Color color,
        RectangleF rect,
        StringAlignment horizontal = StringAlignment.Near,
        StringAlignment vertical = StringAlignment.Center,
        bool wrap = false)
    {
        if (string.IsNullOrEmpty(text) || rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        using var brush = new SolidBrush(color);
        using var format = new StringFormat
        {
            Alignment = horizontal,
            LineAlignment = vertical,
            FormatFlags = wrap ? 0 : StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter,
        };
        graphics.DrawString(text, font, brush, rect, format);
    }

    /// <summary>Draws a horizontal tick mark.</summary>
    public static void HLine(Graphics graphics, Pen pen, float x1, float x2, float y)
        => graphics.DrawLine(pen, x1, y, x2, y);

    /// <summary>Draws a vertical tick mark.</summary>
    public static void VLine(Graphics graphics, Pen pen, float x, float y1, float y2)
        => graphics.DrawLine(pen, x, y1, x, y2);

    /// <summary>Converts an angle in degrees to a point on a circle.</summary>
    public static PointF PointOn(float cx, float cy, float radius, float degrees)
    {
        var radians = degrees * (float)(Math.PI / 180d);
        return new PointF(cx + ((float)Math.Cos(radians) * radius), cy + ((float)Math.Sin(radians) * radius));
    }

    /// <summary>Builds a rounded-rectangle path.</summary>
    public static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0.5f || rect.Width <= 0f || rect.Height <= 0f)
        {
            path.AddRectangle(rect);
            return path;
        }

        var diameter = Math.Min(Math.Min(radius * 2f, rect.Width), rect.Height);
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180f, 90f);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270f, 90f);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0f, 90f);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    /// <summary>Fills a rounded rectangle.</summary>
    public static void FillRounded(Graphics graphics, Color color, RectangleF rect, float radius)
    {
        if (rect.Width <= 0f || rect.Height <= 0f || color.A == 0)
        {
            return;
        }

        using var path = RoundedRect(rect, radius);
        using var brush = new SolidBrush(color);
        graphics.FillPath(brush, path);
    }

    /// <summary>Strokes a rounded rectangle.</summary>
    public static void DrawRounded(Graphics graphics, Color color, RectangleF rect, float radius, float thickness)
    {
        if (rect.Width <= 0f || rect.Height <= 0f || thickness <= 0f || color.A == 0)
        {
            return;
        }

        using var path = RoundedRect(rect, radius);
        using var pen = new Pen(color, thickness);
        graphics.DrawPath(pen, path);
    }

    /// <summary>Strokes an arc with a round cap and a faint glow behind it.</summary>
    public static void GlowArc(
        Graphics graphics,
        RectangleF rect,
        float start,
        float sweep,
        Color color,
        float thickness,
        bool glow = true)
    {
        if (rect.Width <= 0f || rect.Height <= 0f || sweep == 0f)
        {
            return;
        }

        if (glow)
        {
            for (var layer = 3; layer >= 1; layer--)
            {
                using var glowPen = new Pen(Fade(color, 0.10 * layer), thickness + (layer * 4f))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                };
                graphics.DrawArc(glowPen, rect, start, sweep);
            }
        }

        using var pen = new Pen(color, thickness)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        graphics.DrawArc(pen, rect, start, sweep);
    }
}
