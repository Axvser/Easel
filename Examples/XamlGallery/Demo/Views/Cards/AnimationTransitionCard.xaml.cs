namespace Demo.Views.Cards;

/// <summary>The transition card: Start disables the surface so the style's trigger runs its enter actions.</summary>
public sealed partial class AnimationTransitionCard : ThemedStackPanel
{
    /// <summary>Creates the view and runs the generated initializer.</summary>
    public AnimationTransitionCard() => InitializeComponent();

    private void OnStart(object? sender, EventArgs e) => surface.IsEnabled = false;

    private void OnReset(object? sender, EventArgs e) => surface.IsEnabled = true;
}
