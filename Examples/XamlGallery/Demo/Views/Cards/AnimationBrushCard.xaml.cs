namespace Demo.Views.Cards;

/// <summary>The brush card: structural gradient interpolation next to the composed cross-fade fallback.</summary>
public sealed partial class AnimationBrushCard : ThemedStackPanel
{
    /// <summary>Creates the view and runs the generated initializer.</summary>
    public AnimationBrushCard() => InitializeComponent();

    private void OnStart(object? sender, EventArgs e)
    {
        structural.IsEnabled = false;
        composed.IsEnabled = false;
    }

    private void OnReset(object? sender, EventArgs e)
    {
        structural.IsEnabled = true;
        composed.IsEnabled = true;
    }
}
