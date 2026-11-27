using Easel.Visuals.Controls;
using IndustrialWorkbench.ViewModels;

namespace IndustrialWorkbench.Views.Controls;

/// <summary>The alarm banner preview; acknowledges the model's alarm when clicked.</summary>
public sealed partial class AlarmBannerView : UserControl
{
    /// <summary>Creates the view and its generated initializer.</summary>
    public AlarmBannerView() => InitializeComponent();

    private void OnAlarmAcknowledged(object? sender, EventArgs e)
        => (DataContext as WorkbenchViewModel)?.AcknowledgeAlarm();
}
