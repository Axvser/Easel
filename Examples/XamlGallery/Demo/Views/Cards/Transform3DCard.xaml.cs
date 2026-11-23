using Easel;

namespace Demo.Views.Cards;

/// <summary>The 3D-transform card; the sliders rebuild the live card's perspective transform.</summary>
public sealed partial class Transform3DCard : ThemedStackPanel
{
    /// <summary>Creates the view and runs the generated initializer.</summary>
    public Transform3DCard()
    {
        InitializeComponent();
    }

    private void OnTiltChanged(object? sender, EventArgs e)
    {
        // The event can fire while a later-declared slider is still being built.
        if (liveCard is null || tiltReadout is null || angleX is null || angleY is null)
        {
            return;
        }

        liveCard.Transform3D = new Rotate3DTransform
        {
            AngleX = angleX.Value,
            AngleY = angleY.Value,
            CenterX = 85d,
            CenterY = 65d,
            Depth = 600d,
        };

        tiltReadout.Text = $"AngleX {angleX.Value:F0} · AngleY {angleY.Value:F0}";
    }
}
