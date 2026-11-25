using System.Drawing;
using Easel.Controls;
using Easel.Visuals.Controls;
using XamlButton = Easel.Visuals.Controls.Button;

namespace Easel.Visuals.Dialogs;

// 全屏遮罩层：自绘半透明底色，并且自己回答命中的按下，阻止输入穿到下面的根树。
internal sealed class DialogScrim : VisualElement
{
    internal DialogScrim()
    {
        IsHitTestVisible = true;
    }
}

// 对话卡片用的语义色：高对比度下统一取主题强调色，普通主题按 kind 区分。
internal static class DialogPalette
{    internal static Color KindColor(DialogKind kind)
    {
        if (Theme.EffectiveMode == ThemeMode.HighContrast)
        {
            return Theme.Current.Accent;
        }

        return kind switch
        {
            DialogKind.Warning => Color.FromArgb(0xF5, 0x9E, 0x0B),
            DialogKind.Error => Color.FromArgb(0xEF, 0x44, 0x44),
            DialogKind.Success => Color.FromArgb(0x22, 0xC5, 0x5E),
            _ => Color.FromArgb(0x3B, 0x82, 0xF6),
        };
    }

    internal static string Glyph(DialogKind kind) => kind switch
    {
        DialogKind.Warning => "!",
        DialogKind.Error => "×",
        DialogKind.Success => "✓",
        _ => "i",
    };

    // 悬停/按下色：按亮度决定向黑还是向白混合，浅色强调色上加深、深色强调色上提亮。
    internal static Color Shift(Color color, double amount)
    {
        var target = Luminance(color) > 0.55d ? Color.Black : Color.White;
        return Blend(color, target, amount);
    }

    internal static Color Blend(Color from, Color to, double amount)
    {
        var t = (float)Math.Max(0d, Math.Min(1d, amount));
        return Color.FromArgb(
            from.A,
            (int)Math.Round(from.R + (to.R - from.R) * t),
            (int)Math.Round(from.G + (to.G - from.G) * t),
            (int)Math.Round(from.B + (to.B - from.B) * t));
    }

    private static double Luminance(Color color)
        => (0.2126d * color.R + 0.7152d * color.G + 0.0722d * color.B) / 255d;
}

// 对话默认底部的按钮：角色决定填充、描边与文字色，悬停/按下也随角色走。
internal enum DialogButtonRole
{
    Primary,
    Secondary,
    Cancel,
}

internal sealed class DialogButton : XamlButton
{
    internal DialogButton(DialogButtonRole role, string text)
    {
        Role = role;
        Text = text ?? string.Empty;
        Height = 32;
        MinWidth = 82;
        Padding = new Thickness(16f, 6f);
        CornerRadius = 8f;
        IsHitTestVisible = true;
    }

    internal DialogButtonRole Role { get; }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var theme = Theme.Current;
        var enabled = IsEnabled;
        var radius = CornerRadius;
        var bounds = new Rectangle(
            0,
            0,
            Math.Max(1, (int)Math.Ceiling(Bounds.Width)),
            Math.Max(1, (int)Math.Ceiling(Bounds.Height)));

        Color background;
        Color border;
        Color foreground;
        var thickness = new Thickness(1f);

        if (!enabled)
        {
            background = theme.DisabledBackground;
            border = theme.ControlBorder;
            foreground = theme.DisabledForeground;
        }
        else
        {
            switch (Role)
            {
                case DialogButtonRole.Primary:
                    background = IsPressed
                        ? DialogPalette.Shift(theme.Accent, 0.22d)
                        : IsPointerOver
                            ? DialogPalette.Shift(theme.Accent, 0.12d)
                            : theme.Accent;
                    border = background;
                    foreground = theme.AccentForeground;
                    thickness = Thickness.Empty;
                    break;

                case DialogButtonRole.Secondary:
                    background = IsPressed
                        ? theme.ControlPressed
                        : IsPointerOver
                            ? theme.ControlHover
                            : theme.ControlBackground;
                    border = theme.ControlBorder;
                    foreground = theme.Foreground;
                    break;

                default:
                    background = IsPressed
                        ? theme.ControlPressed
                        : IsPointerOver
                            ? theme.ControlHover
                            : Color.Transparent;
                    border = Color.Transparent;
                    foreground = theme.MutedForeground;
                    thickness = Thickness.Empty;
                    break;
            }
        }

        XamlDrawing.PaintChrome(graphics, bounds, background, border, thickness, radius);

        if (IsFocusVisible)
        {
            using var focusPath = XamlDrawing.RoundedRectangle(
                new RectangleF(2f, 2f, Math.Max(0f, bounds.Width - 4f), Math.Max(0f, bounds.Height - 4f)),
                Math.Max(0f, radius - 1f));
            using var focusPen = new Pen(theme.FocusRing, theme.FocusThickness);
            graphics.DrawPath(focusPen, focusPath);
        }

        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        var content = new Rectangle(
            (int)Math.Ceiling(Padding.Left),
            (int)Math.Ceiling(Padding.Top),
            Math.Max(0, bounds.Width - (int)Math.Ceiling(Padding.Horizontal)),
            Math.Max(0, bounds.Height - (int)Math.Ceiling(Padding.Vertical)));
        VisualText.Draw(
            this,
            graphics,
            Text,
            Font,
            foreground,
            content,
            wrap: false,
            StringAlignment.Center,
            StringAlignment.Center);
    }
}

// 语义图标：浅色圆底加字形；颜色每次绘制时读主题与 kind。
internal sealed class DialogIcon : VisualElement
{
    private Font? _font;
    private string? _fontFamily;

    internal DialogIcon(DialogKind kind)
    {
        Kind = kind;
        IsHitTestVisible = false;
    }

    internal DialogKind Kind { get; }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var theme = Theme.Current;
        var color = DialogPalette.KindColor(Kind);
        var diameter = Math.Max(0f, Math.Min(Bounds.Width, Bounds.Height));
        if (diameter <= 0f)
        {
            return;
        }

        var circle = new RectangleF(
            (Bounds.Width - diameter) / 2f,
            (Bounds.Height - diameter) / 2f,
            diameter,
            diameter);
        var tint = Theme.EffectiveMode == ThemeMode.HighContrast ? 90 : 40;
        using (var path = XamlDrawing.RoundedRectangle(circle, diameter / 2f))
        using (var fill = new SolidBrush(Color.FromArgb(tint, color.R, color.G, color.B)))
        {
            graphics.FillPath(fill, path);
        }

        var glyphColor = Theme.EffectiveMode == ThemeMode.HighContrast ? theme.Accent : color;
        VisualText.Draw(
            this,
            graphics,
            DialogPalette.Glyph(Kind),
            GlyphFont(theme),
            glyphColor,
            circle,
            wrap: false,
            StringAlignment.Center,
            StringAlignment.Center);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _font?.Dispose();
            _font = null;
        }

        base.Dispose(disposing);
    }

    private Font GlyphFont(ThemePalette theme)
    {
        if (_font is null || !string.Equals(_fontFamily, theme.FontFamily, StringComparison.Ordinal))
        {
            _font?.Dispose();
            _fontFamily = theme.FontFamily;
            _font = new Font(
                string.IsNullOrWhiteSpace(theme.FontFamily) ? "Segoe UI" : theme.FontFamily,
                13f,
                FontStyle.Bold);
        }

        return _font;
    }
}
