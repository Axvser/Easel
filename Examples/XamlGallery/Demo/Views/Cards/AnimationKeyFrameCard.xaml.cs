namespace Demo.Views.Cards;

/// <summary>The keyframe card: one chain moves the surface through three widths.</summary>
public sealed partial class AnimationKeyFrameCard : ThemedStackPanel
{
    /// <summary>Creates the view and runs the generated initializer.</summary>
    public AnimationKeyFrameCard() => InitializeComponent();

    private void OnStart(object? sender, EventArgs e) => surface.IsEnabled = false;

    private void OnReset(object? sender, EventArgs e) => surface.IsEnabled = true;
}
