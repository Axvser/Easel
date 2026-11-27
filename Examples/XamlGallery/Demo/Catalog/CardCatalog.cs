namespace Demo;

/// <summary>Concatenates the per-group card lists into the catalog order the shell walks.</summary>
internal static class CardCatalog
{
    /// <summary>Creates every card in catalog order.</summary>
    /// <param name="model">The shared sample data.</param>
    /// <param name="theme">The palette service used by the theme cards.</param>
    public static IReadOnlyList<GalleryCard> Create(GalleryModel model, IThemeService theme)
    {
        var cards = new List<GalleryCard>();

        // Scenarios: industrial pages first, then general application pages and visual studies.
        cards.AddRange(IndustrialScenarioCards.Create());
        cards.AddRange(ScenarioCards.Create());

        // Easel-native groups in industrial-relevance order: controls and data an HMI is built from first.
        cards.AddRange(InputCards.Create());
        cards.AddRange(DataCards.Create(model));
        cards.AddRange(GraphicsCards.Create());
        cards.AddRange(AnimationCards.Create());
        cards.AddRange(BindingCards.Create(model));
        cards.AddRange(StylingCards.Create());
        cards.AddRange(LayoutCards.Create());
        cards.AddRange(TextCards.Create());
        cards.AddRange(OverlayCards.Create());
        cards.AddRange(ThemeCards.Create(theme));
        cards.AddRange(DefaultsCards.Create(model));
        cards.AddRange(VisualsCards.Create());

        // WinForms interop last: it is the seam, not the default path.
        cards.AddRange(NativeCards.Create());
        return cards;
    }
}
