using Demo.ViewModels;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace Demo.Views.Industrial;

/// <summary>The production-line HMI page; the simulator advances the bound values on a timer.</summary>
public sealed partial class ProductionLineView : ThemedDockPanel
{
    private readonly WinFormsTimer _timer = new() { Interval = 800 };
    private readonly ProductionLineViewModel _viewModel;

    /// <summary>Creates the page and starts the simulator.</summary>
    public ProductionLineView()
    {
        _viewModel = new ProductionLineViewModel();
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
