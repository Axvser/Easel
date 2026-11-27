using Demo.ViewModels;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace Demo.Views.Industrial;

/// <summary>The batch process mimic; the simulator moves levels, flow dots and reactor values.</summary>
public sealed partial class ProcessFlowView : ThemedDockPanel
{
    private readonly WinFormsTimer _timer = new() { Interval = 800 };
    private readonly ProcessFlowViewModel _viewModel;

    /// <summary>Creates the page and starts the simulator.</summary>
    public ProcessFlowView()
    {
        _viewModel = new ProcessFlowViewModel();
        DataContext = _viewModel;
        InitializeComponent();
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnTick(object? sender, EventArgs e) => _viewModel.Tick();
}
