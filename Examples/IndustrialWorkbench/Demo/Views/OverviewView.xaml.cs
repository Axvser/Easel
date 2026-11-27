using Easel.Visuals.Controls;
using IndustrialWorkbench.ViewModels;

namespace IndustrialWorkbench.Views;

/// <summary>The home wall: fourteen control types running at once against the simulation model.</summary>
public sealed partial class OverviewView : UserControl
{
    /// <summary>Creates the wall and its generated initializer.</summary>
    public OverviewView() => InitializeComponent();

    private void OnAlarmAcknowledged(object? sender, EventArgs e)
        => (DataContext as WorkbenchViewModel)?.AcknowledgeAlarm();
}
