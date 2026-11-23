using Easel;
using Easel.Visuals.Controls;

namespace Demo.Views;

/// <summary>The slider card; the readout is bound to the slider in code-behind.</summary>
public sealed partial class SliderCard : ThemedStackPanel
{
    /// <summary>Creates the view and binds the readout to the slider value.</summary>
    public SliderCard()
    {
        InitializeComponent();

        readout.SetBinding(
            TextBlock.TextProperty,
            new Binding("Value") { Source = slider, StringFormat = "F0" });
    }
}
