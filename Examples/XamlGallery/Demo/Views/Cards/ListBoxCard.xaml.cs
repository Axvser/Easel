namespace Demo.Views;

/// <summary>A ListBox whose rows come from the shared model.</summary>
public sealed partial class ListBoxCard : ThemedStackPanel
{
    /// <summary>Creates the card and points its binding at the shared data.</summary>
    /// <param name="model">The shared sample data.</param>
    public ListBoxCard(GalleryModel model)
    {
        DataContext = model;
        InitializeComponent();
        listBox.SelectedIndex = 0;
        OnSelectionChanged(this, EventArgs.Empty);
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (status is null)
        {
            return;
        }

        status.Text = listBox.SelectedItem is SampleRow row
            ? $"{row.Code} selected · {row.Owner} · updated {row.Updated:MMM d}"
            : "No selection";
    }
}
