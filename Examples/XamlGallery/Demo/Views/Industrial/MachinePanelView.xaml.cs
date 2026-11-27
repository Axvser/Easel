using Demo.ViewModels;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace Demo.Views.Industrial;

/// <summary>The CNC operator panel; the simulator moves the axes and spindle values.</summary>
public sealed partial class MachinePanelView : ThemedDockPanel
{
    private readonly WinFormsTimer _timer = new() { Interval = 800 };
    private readonly MachinePanelViewModel _viewModel;

    /// <summary>Creates the page and starts the simulator.</summary>
    public MachinePanelView()
    {
        _viewModel = new MachinePanelViewModel();
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
