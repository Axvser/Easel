using Demo.ViewModels;
using Easel.Visuals.Controls;

namespace Demo.Views;

/// <summary>The gallery card for the generic <see cref="VirtualizingPanel"/> base, driven by a million-row log.</summary>
public sealed partial class VirtualizationPanelCard : ThemedStackPanel
{
    private readonly VirtualizationLogViewModel _model = new();

    /// <summary>Creates the card and points its binding at the private log model.</summary>
    public VirtualizationPanelCard()
    {
        DataContext = _model;
        InitializeComponent();
        panel.ItemsRebuilt += OnPanelItemsRebuilt;
        panel.SelectionChanged += OnPanelSelectionChanged;
        panel.WindowArranged += OnPanelWindowArranged;
        UpdateMetrics();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateMetrics();

    private void OnPanelItemsRebuilt(object? sender, EventArgs e) => UpdateMetrics();

    private void OnPanelSelectionChanged(object? sender, EventArgs e) => UpdateMetrics();

    private void OnPanelWindowArranged(object? sender, EventArgs e) => UpdateMetrics();

    private void OnTopClicked(object? sender, EventArgs e) => ScrollTo(0f);

    private void OnMiddleClicked(object? sender, EventArgs e) => ScrollTo(viewer.MaxVerticalOffset / 2f);

    private void OnBottomClicked(object? sender, EventArgs e) => ScrollTo(viewer.MaxVerticalOffset);

    private void OnRandomClicked(object? sender, EventArgs e)
    {
        if (_model.RowCount == 0)
        {
            return;
        }

        var index = Random.Shared.Next(_model.RowCount);
        ScrollTo(index * (float)panel.ItemExtent);
    }

    private void OnSelectClicked(object? sender, EventArgs e)
    {
        if (panel.FirstRealizedIndex < 0 || panel.RealizedCount == 0)
        {
            return;
        }

        panel.SelectIndex(panel.FirstRealizedIndex + (panel.RealizedCount / 2));
        UpdateMetrics();
    }

    private void OnEmptyClicked(object? sender, EventArgs e) => ReplaceSource(0);

    private void OnHundredThousandClicked(object? sender, EventArgs e) => ReplaceSource(100_000);

    private void OnMillionClicked(object? sender, EventArgs e) => ReplaceSource(1_000_000);

    private void ReplaceSource(int count)
    {
        _model.Replace(count);
        viewer.VerticalOffset = 0f;
        UpdateMetrics();
    }

    private void ScrollTo(float offset)
    {
        viewer.VerticalOffset = offset;
        UpdateMetrics();
    }

    private void UpdateMetrics()
    {
        var count = _model.RowCount;
        var realized = panel.RealizedCount;
        var first = panel.FirstRealizedIndex;
        var extent = Math.Max(1d, panel.ItemExtent);
        var visible = Math.Min(count, (int)Math.Ceiling(panel.ViewportHeight / extent));

        visibleText.Text = count == 0 ? "0" : visible.ToString("N0");
        firstText.Text = first < 0 ? "—" : first.ToString("N0");
        realizedText.Text = realized.ToString("N0");
        createdText.Text = panel.CreatedCount.ToString("N0");
        reusedText.Text = panel.ReusedCount.ToString("N0");
        recycledText.Text = panel.RecycledCount.ToString("N0");

        sourceLabel.Text = _model.SourceLabel;
        status.Text = count == 0
            ? "Empty source · no rows realized"
            : $"offset {viewer.VerticalOffset:N0} px · extent {viewer.ExtentHeight:N0} px · {count:N0} source rows";

        selectionText.Text = panel.SelectedRow is { } row
            ? $"Selected #{row.Number} · {row.Level}"
            : "No row selected";
    }
}
