using Demo.Views;

namespace Demo;

/// <summary>The visual-XAML card group.</summary>
internal static class VisualsCards
{
    /// <summary>Creates the visual-XAML group.</summary>
    public static IReadOnlyList<GalleryCard> Create() =>
    [
        Card(
            "visuals.visual-xaml",
            "Visuals",
            "Visual XAML",
            "A generated view on the visual runtime with a click handler wired in XAML.",
            "x:Class=\"Demo.Views.VisualSample\"",
            _ => new VisualSample()),
    ];

    private static GalleryCard Card(
        string id, string group, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create)
        => new(new GalleryCardInfo(id, group, title, summary, usage), create);
}
