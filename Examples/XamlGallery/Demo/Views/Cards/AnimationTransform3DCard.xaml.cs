namespace Demo.Views.Cards;

/// <summary>The 3D animation card: a Transform3D endpoint animates the perspective tilt.</summary>
public sealed partial class AnimationTransform3DCard : ThemedStackPanel
{
    /// <summary>Creates the view and runs the generated initializer.</summary>
    public AnimationTransform3DCard() => InitializeComponent();

    private void OnStart(object? sender, EventArgs e) => surface.IsEnabled = false;

    private void OnReset(object? sender, EventArgs e) => surface.IsEnabled = true;
}
