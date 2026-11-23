namespace Demo.Views;

/// <summary>A one-way path binding that follows a numeric source property.</summary>
public sealed partial class OneWayBindingCard : ThemedStackPanel
{
    /// <summary>Creates the card and points its bindings at the shared model.</summary>
    /// <param name="model">The shared sample data.</param>
    public OneWayBindingCard(GalleryModel model)
    {
        DataContext = model;
        InitializeComponent();
    }
}
