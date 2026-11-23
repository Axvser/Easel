using Easel;

namespace Demo.Views.Cards;

/// <summary>Drives a live <see cref="Rotate3DTransform"/> from the three sliders.</summary>
public sealed partial class Showcase3DView : ThemedStackPanel
{
    /// <summary>Creates the view and applies the initial camera.</summary>
    public Showcase3DView()
    {
        InitializeComponent();
        ApplyTransform();
    }

    private void OnAngleChanged(object? sender, EventArgs e) => ApplyTransform();

    private void ApplyTransform()
    {
        if (liveCard is null || angleXSlider is null || angleYSlider is null || depthSlider is null)
        {
            return;
        }

        var angleX = angleXSlider.Value;
        var angleY = angleYSlider.Value;
        var depth = depthSlider.Value;

        liveCard.Transform3D = new Rotate3DTransform
        {
            AngleX = angleX,
            AngleY = angleY,
            CenterX = 100d,
            CenterY = 75d,
            Depth = depth,
        };

        angleXLabel.Text = $"Angle X · {angleX:F0}°";
        angleYLabel.Text = $"Angle Y · {angleY:F0}°";
        depthLabel.Text = $"Depth · {depth:F0}";
    }
}
