namespace Demo.Views;

/// <summary>An editor that writes its value back to the source, with a mirror of the same property.</summary>
public sealed partial class TwoWayBindingCard : ThemedStackPanel
{
    /// <summary>Creates the card and points its bindings at the shared model.</summary>
    /// <param name="model">The shared sample data.</param>
    public TwoWayBindingCard(GalleryModel model)
    {
        DataContext = model;
        InitializeComponent();
    }
}
