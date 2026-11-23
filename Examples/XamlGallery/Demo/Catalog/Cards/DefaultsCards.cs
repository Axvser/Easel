using Demo.Views;

namespace Demo;

/// <summary>The opening card: the implicit fallback appearance, authored in visual XAML.</summary>
internal static class DefaultsCards
{
    /// <summary>Creates the defaults group.</summary>
    public static IReadOnlyList<GalleryCard> Create(GalleryModel model) =>
    [
        Card(
            "defaults.controls",
            "Basics",
            "Default controls",
            "The implicit fallback style, authored as visual XAML: no control below sets a style, background or foreground.",
            "x:Class=\"Demo.Views.DefaultsView\"",
            _ => new DefaultsView(model)),
    ];

    private static GalleryCard Card(
        string id, string group, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create)
        => new(new GalleryCardInfo(id, group, title, summary, usage), create);
}
