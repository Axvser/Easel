using Demo.ViewModels;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace Demo.Views;

/// <summary>The direct-use card for the concrete <see cref="Easel.Visuals.Controls.VirtualizingStackPanel"/>.</summary>
public sealed partial class VirtualizingStackCard : ThemedDockPanel
{
    private readonly VirtualizationLogViewModel _viewModel = new();
    private readonly WinFormsTimer _timer = new() { Interval = 150 };

    /// <summary>Creates the card, binds the 100,000-row source and starts the metrics timer.</summary>
    public VirtualizingStackCard()
    {
        DataContext = _viewModel;
        InitializeComponent();
        _timer.Tick += OnTimerTick;
        _timer.Start();
        UpdateMetrics();
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

    private void OnTimerTick(object? sender, EventArgs e) => UpdateMetrics();

    private void UpdateMetrics()
    {
        firstText.Text = $"first {panel.FirstRealizedIndex:N0}";
        realizedText.Text = $"realized {panel.RealizedCount}";
        totalText.Text = $"{_viewModel.Rows.Count:N0} rows";
    }

    private void OnTop(object? sender, EventArgs e) => viewer.VerticalOffset = 0;

    private void OnMiddle(object? sender, EventArgs e) => viewer.VerticalOffset = viewer.MaxVerticalOffset / 2f;

    private void OnBottom(object? sender, EventArgs e) => viewer.VerticalOffset = viewer.MaxVerticalOffset;
}
