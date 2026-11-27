namespace Demo.Views;

/// <summary>A fixed-row virtual list inside a scroll viewer.</summary>
public sealed partial class VirtualizingListBoxCard : ThemedStackPanel
{
    /// <summary>Creates the card and points its binding at the shared rows.</summary>
    /// <param name="model">The shared sample data.</param>
    public VirtualizingListBoxCard(GalleryModel model)
    {
        DataContext = model;
        InitializeComponent();
        list.ItemsRebuilt += OnItemsRebuilt;
        UpdateStatus();
    }

    private void OnScrollChanged(object? sender, Easel.Visuals.Controls.ScrollChangedEventArgs e) => UpdateStatus();

    private void OnItemsRebuilt(object? sender, EventArgs e) => UpdateStatus();

    private void UpdateStatus()
    {
        if (list.FirstRealizedIndex < 0 || list.RealizedCount == 0)
        {
            status.Text = "Scroll to realize rows";
            return;
        }

        var first = list.FirstRealizedIndex + 1;
        var last = first + list.RealizedCount - 1;
        status.Text = $"{first:N0}–{last:N0} of 5,000 realized · offset {viewer.VerticalOffset:N0} px";
    }
}
