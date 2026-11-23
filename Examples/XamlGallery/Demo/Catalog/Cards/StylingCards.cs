using Demo.Views;

namespace Demo;

/// <summary>The styling card group, authored as visual XAML views.</summary>
internal static class StylingCards
{
    /// <summary>Creates the styling group.</summary>
    public static IReadOnlyList<GalleryCard> Create() =>
    [
        Card(
            "style.implicit",
            "Styling",
            "Implicit style",
            "A TargetType style applies to every matching descendant.",
            "<Style TargetType=\"Button\"><Style.Setters>…",
            _ => new ImplicitStyleCard()),
        Card(
            "style.explicit",
            "Styling",
            "Explicit style",
            "An assigned style outranks the implicit one until cleared.",
            "<Button Style=\"{StaticResource ExplicitButton}\" />",
            _ => new ExplicitStyleCard()),
        Card(
            "style.trigger",
            "Styling",
            "Trigger",
            "A property equality activates setters at the trigger layer.",
            "<Trigger Property=\"IsEnabled\" Value=\"False\">…",
            _ => new TriggerCard()),
        Card(
            "style.template",
            "Styling",
            "ControlTemplate",
            "A visual template with a content presenter and a named part.",
            "<ControlTemplate TargetType=\"ContentControl\">…",
            _ => new ControlTemplateCard()),
        Card(
            "style.resources",
            "Styling",
            "Resources",
            "Named resources resolve through the ancestor chain.",
            "<StackPanel.Resources><Style x:Key=\"AccentPlate\">…",
            _ => new StyleResourcesCard()),
        Card(
            "style.dynamic-resource",
            "Styling",
            "Dynamic resource",
            "DynamicResource follows dictionary changes; StaticResource keeps the value it resolved at construction.",
            "<Border Background=\"{DynamicResource Accent}\" />",
            _ => new DynamicResourceCard()),
    ];

    private static GalleryCard Card(
        string id, string group, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create)
        => new(new GalleryCardInfo(id, group, title, summary, usage), create);
}
