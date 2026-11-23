using Easel;
using Easel.Visuals;

namespace Demo.Views;

/// <summary>The trigger card; the check box drives the button's IsEnabled, the style reacts to it.</summary>
public sealed partial class TriggerCard : ThemedStackPanel
{
    /// <summary>Creates the view and binds the switch to the button's enabled state.</summary>
    public TriggerCard()
    {
        InitializeComponent();

        triggerButton.SetBinding(
            VisualElement.IsEnabledProperty,
            new Binding("IsChecked") { Source = enableSwitch, Mode = BindingMode.OneWay });
    }
}
