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
        cards.AddRange(ScenarioCards.Create());
        cards.AddRange(DefaultsCards.Create(model));
        cards.AddRange(LayoutCards.Create());
        cards.AddRange(TextCards.Create());
        cards.AddRange(DataCards.Create(model));
        cards.AddRange(InputCards.Create());
        cards.AddRange(GraphicsCards.Create());
        cards.AddRange(OverlayCards.Create());
        cards.AddRange(StylingCards.Create());
        cards.AddRange(BindingCards.Create(model));
        cards.AddRange(ThemeCards.Create(theme));
        cards.AddRange(VisualsCards.Create());
        return cards;
    }
}
