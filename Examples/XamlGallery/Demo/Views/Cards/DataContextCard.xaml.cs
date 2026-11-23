namespace Demo.Views;

/// <summary>A card whose descendants inherit one data context and bind the command and scalar it exposes.</summary>
public sealed partial class DataContextCard : ThemedStackPanel
{
    /// <summary>Creates the card and supplies the inherited data context.</summary>
    /// <param name="model">The shared sample data.</param>
    public DataContextCard(GalleryModel model)
    {
        DataContext = model;
        InitializeComponent();
    }
}
