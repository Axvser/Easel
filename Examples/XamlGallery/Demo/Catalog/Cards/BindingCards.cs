using Demo.Views;

namespace Demo;

/// <summary>The binding cards, each authored as a XAML view bound to the shared model.</summary>
internal static class BindingCards
{
    /// <summary>Creates the binding group in catalog order.</summary>
    /// <param name="model">The shared sample data.</param>
    public static IReadOnlyList<GalleryCard> Create(GalleryModel model) =>
    [
        Card(
            "binding.one-way",
            "Binding",
            "One-way binding",
            "A path binding follows the source property.",
            "<TextBlock Text=\"{Binding Amount, StringFormat=N2}\" />",
            _ => new OneWayBindingCard(model)),
        Card(
            "binding.two-way",
            "Binding",
            "Two-way binding",
            "The editor writes the value back to the source.",
            "<TextBox Text=\"{Binding OptionalName, Mode=TwoWay}\" />",
            _ => new TwoWayBindingCard(model)),
        Card(
            "binding.data-context",
            "Binding",
            "DataContext",
            "Descendants inherit one data context.",
            "<Button Command=\"{Binding SaveCommand}\" />",
            _ => new DataContextCard(model)),
        Card(
            "binding.converter",
            "Binding",
            "Converter",
            "A value converter formats a number for display.",
            "<TextBlock Text=\"{Binding Amount, Converter={StaticResource Money}}\" />",
            _ => new ConverterCard(model)),
        Card(
            "binding.mvvm",
            "Binding",
            "MVVM view model",
            "A [VeloxProperty]/[VeloxCommand] view model bound from visual XAML.",
            "[VeloxProperty] · [VeloxCommand]",
            _ => new ViewModelBoard()),
    ];

    private static GalleryCard Card(
        string id, string group, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create)
        => new(new GalleryCardInfo(id, group, title, summary, usage), create);
}
