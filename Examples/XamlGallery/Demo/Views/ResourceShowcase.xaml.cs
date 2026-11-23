using Easel;
using Easel.Visuals;
using Easel.Visuals.Controls;

namespace Demo.Views;

/// <summary>A card whose colours, styles, trigger and template all come from XAML resources.</summary>
public sealed partial class ResourceShowcase : ThemedStackPanel
{
    /// <summary>Creates the view and fills the templated row list.</summary>
    public ResourceShowcase()
    {
        InitializeComponent();

        rows.ItemsSource = new[]
        {
            "Row one · StaticResource brush",
            "Row two · keyed DataTemplate",
            "Row three · current palette",
        };

        primaryButton.SetBinding(
            VisualElement.IsEnabledProperty,
            new Binding("IsChecked") { Source = enableSwitch, Mode = BindingMode.OneWay });
    }
}
