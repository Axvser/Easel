using System.Drawing;
using Easel;

namespace Demo.Views;

/// <summary>Seeds the palette keys and the shared gradient brushes every XAML demo view resolves.</summary>
/// <remarks>
/// A visual view resolves <c>{StaticResource}</c> during <c>InitializeComponent</c>, before it is attached to the
/// surface, so the keys must be in the view's own dictionary. The themed base classes below call
/// <see cref="Fill"/> in their constructors; views created by the gallery only need to derive from one of them.
/// The fallback palettes themselves live in <c>Themes/*.xaml</c>.
/// </remarks>
internal static class GalleryTheme
{
    private static ResourceDictionary? _custom;

    /// <summary>Uses a demo-owned override dictionary instead of the built-in theme resources.</summary>
    /// <param name="resources">The custom dictionary, or <see langword="null"/> for the built-in resources.</param>
    public static void UseCustom(ResourceDictionary? resources) => _custom = resources;

    /// <summary>Adds the current palette and the demo gradients to <paramref name="resources"/>.</summary>
    public static void Fill(ResourceDictionary resources)
    {
        var source = _custom ?? Theme.CreateResourceDictionary(Theme.EffectiveMode);
        resources.MergedDictionaries.Add(source);

        var theme = Theme.Current;

        // Gradients are the same in every palette: they are decoration, not chrome.
        resources["AccentGradient"] = Linear(
            (theme.Accent, 0d),
            (Color.FromArgb(0xFF, 0x8B, 0x5C, 0xF6), 1d));
        resources["AuroraGradient"] = Linear(
            (Color.FromArgb(0xFF, 0x00, 0xD2, 0xFF), 0d),
            (Color.FromArgb(0xFF, 0x00, 0x72, 0xFF), 0.5d),
            (Color.FromArgb(0xFF, 0x7B, 0x2F, 0xF7), 1d));
        resources["SunsetGradient"] = Linear(
            (Color.FromArgb(0xFF, 0xFF, 0x51, 0x2F), 0d),
            (Color.FromArgb(0xFF, 0xF0, 0x98, 0x19), 1d));
        resources["EmeraldGradient"] = Linear(
            (Color.FromArgb(0xFF, 0x43, 0xE9, 0x7B), 0d),
            (Color.FromArgb(0xFF, 0x38, 0xF9, 0xD7), 1d));
        resources["MagentaGradient"] = Linear(
            (Color.FromArgb(0xFF, 0xFF, 0x61, 0xD2), 0d),
            (Color.FromArgb(0xFF, 0xFE, 0x90, 0x90), 1d));
        resources["SpectrumGradient"] = Linear(
            (Color.FromArgb(0xFF, 0xFF, 0x3B, 0x30), 0d),
            (Color.FromArgb(0xFF, 0xFF, 0xD6, 0x0A), 0.33d),
            (Color.FromArgb(0xFF, 0x3D, 0xD6, 0x8C), 0.66d),
            (Color.FromArgb(0xFF, 0x00, 0x72, 0xFF), 1d));
        var textSpectrum = GalleryTheme.Linear(
            (Color.FromArgb(0xFF, 0xFF, 0x3B, 0x30), 0d),
            (Color.FromArgb(0xFF, 0xFF, 0xD6, 0x0A), 0.33d),
            (Color.FromArgb(0xFF, 0x3D, 0xD6, 0x8C), 0.66d),
            (Color.FromArgb(0xFF, 0x00, 0x72, 0xFF), 1d));
        textSpectrum.EndPoint = new PointF(1f, 0f);
        resources["TextSpectrumGradient"] = textSpectrum;
        resources["DeepGradient"] = Linear(
            (Color.FromArgb(0xFF, 0x0F, 0x20, 0x2E), 0d),
            (Color.FromArgb(0xFF, 0x15, 0x3B, 0x4F), 0.55d),
            (Color.FromArgb(0xFF, 0x2A, 0x1B, 0x4A), 1d));
        resources["FadeMask"] = Linear(
            (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), 0d),
            (Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.75d));
        // 白色到黑色的不透明渐变：Alpha 遮罩下整条都可见，Luminance 遮罩下才向右淡出。
        resources["LuminanceMask"] = Linear(
            (Color.White, 0d),
            (Color.Black, 1d));

        var radial = new RadialGradientBrush { Center = new PointF(0.5f, 0.45f), Radius = 0.65f };
        radial.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0x00, 0xD2, 0xFF), 0d));
        radial.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0x7B, 0x2F, 0xF7), 1d));
        resources["RadialGlow"] = radial;

        // 半径 1.1 的径向渐变铺满整个矩形（GDI+ 的 PathGradientBrush 只画椭圆内部），
        // 这样它与线性渐变做交叉淡化时，边缘不会出现椭圆形状的硬边。
        var radialCover = new RadialGradientBrush { Center = new PointF(0.5f, 0.5f), Radius = 1.1f };
        radialCover.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0x7B, 0x2F, 0xF7), 0d));
        radialCover.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0x00, 0xD2, 0xFF), 1d));
        resources["RadialCover"] = radialCover;
    }

    /// <summary>Builds a left-to-right or diagonal gradient from colour stops.</summary>
    public static LinearGradientBrush Linear(params (Color Color, double Offset)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = new PointF(0f, 0f), EndPoint = new PointF(1f, 1f) };
        foreach (var (color, offset) in stops)
        {
            brush.GradientStops.Add(new GradientStop(color, offset));
        }

        return brush;
    }
}

/// <summary>A stack panel that seeds the palette resources before a XAML view builds its content.</summary>
public class ThemedStackPanel : Easel.Visuals.Controls.StackPanel
{
    /// <summary>Seeds the palette resources.</summary>
    protected ThemedStackPanel() => GalleryTheme.Fill(Resources);
}

/// <summary>A grid that seeds the palette resources before a XAML view builds its content.</summary>
public class ThemedGrid : Easel.Visuals.Controls.Grid
{
    /// <summary>Seeds the palette resources.</summary>
    protected ThemedGrid() => GalleryTheme.Fill(Resources);
}

/// <summary>A dock panel that seeds the palette resources before a XAML view builds its content.</summary>
public class ThemedDockPanel : Easel.Visuals.Controls.DockPanel
{
    /// <summary>Seeds the palette resources.</summary>
    protected ThemedDockPanel() => GalleryTheme.Fill(Resources);
}

/// <summary>A border that seeds the palette resources before a XAML view builds its content.</summary>
public class ThemedBorder : Easel.Visuals.Controls.Border
{
    /// <summary>Seeds the palette resources.</summary>
    protected ThemedBorder() => GalleryTheme.Fill(Resources);
}

/// <summary>A wrap panel that seeds the palette resources before a XAML view builds its content.</summary>
public class ThemedWrapPanel : Easel.Visuals.Controls.WrapPanel
{
    /// <summary>Seeds the palette resources.</summary>
    protected ThemedWrapPanel() => GalleryTheme.Fill(Resources);
}

/// <summary>A user control that seeds the palette resources before a XAML view builds its content.</summary>
public class ThemedUserControl : Easel.Visuals.Controls.UserControl
{
    /// <summary>Seeds the palette resources.</summary>
    protected ThemedUserControl() => GalleryTheme.Fill(Resources);
}
