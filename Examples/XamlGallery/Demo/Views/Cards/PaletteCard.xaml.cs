namespace Demo.Views;

/// <summary>The palette card; every swatch reads a key from the themed base dictionary.</summary>
public sealed partial class PaletteCard : ThemedStackPanel
{
    /// <summary>Creates the view and runs the generated initializer.</summary>
    public PaletteCard() => InitializeComponent();
}
