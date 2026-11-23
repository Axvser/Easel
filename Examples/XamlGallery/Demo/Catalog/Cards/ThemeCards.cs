using Demo.Views;

namespace Demo;

/// <summary>The theme card group, authored as visual XAML views.</summary>
internal static class ThemeCards
{
    /// <summary>Creates the theme group.</summary>
    /// <param name="theme">The palette service the mode card cycles.</param>
    public static IReadOnlyList<GalleryCard> Create(IThemeService theme) =>
    [
        Card(
            "theme.palette",
            "Theme",
            "Palette",
            "The Dark+, Light+ and high-contrast tokens used by the visual backend.",
            "<Border Background=\"{StaticResource Accent}\" />",
            _ => new PaletteCard()),
        Card(
            "theme.typography",
            "Theme",
            "Typography",
            "Font sizes and weights from the visual text pipeline.",
            "<TextBlock FontSize=\"18\" FontWeight=\"SemiBold\" />",
            _ => new TypographyCard()),
        Card(
            "theme.mode",
            "Theme",
            "Theme mode",
            "Dark+, Light+, high contrast and follow-system modes; rebuilding the root applies the palette.",
            "<ComboBox SelectedIndex=\"{Binding ThemeIndex, Mode=TwoWay}\" />",
            _ => new ThemeModeCard(theme)),
        Card(
            "theme.resources",
            "Theme",
            "Resources in XAML",
            "Palette colours, keyed styles, a trigger, a control template and a data template declared in visual XAML.",
            "x:Key=\"Primary\" · {StaticResource Primary}",
            _ => new ResourceShowcase()),
    ];

    private static GalleryCard Card(
        string id, string group, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create)
        => new(new GalleryCardInfo(id, group, title, summary, usage), create);
}
