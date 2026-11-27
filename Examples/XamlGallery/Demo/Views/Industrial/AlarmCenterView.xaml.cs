using Demo.ViewModels;

namespace Demo.Views.Industrial;

/// <summary>The alarm board; the filter and acknowledgement buttons call the view model.</summary>
public sealed partial class AlarmCenterView : ThemedDockPanel
{
    private readonly AlarmCenterViewModel _viewModel;

    /// <summary>Creates the page and attaches its view model.</summary>
    public AlarmCenterView()
    {
        _viewModel = new AlarmCenterViewModel();
        DataContext = _viewModel;
        InitializeComponent();
    }

    private void OnShowAll(object? sender, EventArgs e) => _viewModel.ShowAll();

    private void OnShowUnacked(object? sender, EventArgs e) => _viewModel.ShowUnacked();

    private void OnShowCritical(object? sender, EventArgs e) => _viewModel.ShowCritical();

    private void OnAcknowledgeVisible(object? sender, EventArgs e) => _viewModel.AcknowledgeVisible();
}
