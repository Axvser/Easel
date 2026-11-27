using Demo.ViewModels;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace Demo.Views;

/// <summary>The direct-use card for the concrete two-dimensional <see cref="Easel.Visuals.Controls.VirtualizingWrapPanel"/>.</summary>
public sealed partial class VirtualizingWrapCard : ThemedDockPanel
{
    private readonly VirtualizationWrapViewModel _viewModel = new();
    private readonly WinFormsTimer _timer = new() { Interval = 150 };

    /// <summary>Creates the card, binds 5,000 tiles and starts the metrics timer.</summary>
    public VirtualizingWrapCard()
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
        columnsText.Text = $"{panel.Columns} cols · {_viewModel.Tiles.Count:N0} tiles";
    }

    private void OnTop(object? sender, EventArgs e) => viewer.VerticalOffset = 0;

    private void OnMiddle(object? sender, EventArgs e) => viewer.VerticalOffset = viewer.MaxVerticalOffset / 2f;

    private void OnBottom(object? sender, EventArgs e) => viewer.VerticalOffset = viewer.MaxVerticalOffset;
}
