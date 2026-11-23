namespace Demo.Views;

/// <summary>A TreeView whose hierarchical rows are built from the shared sample nodes.</summary>
public sealed partial class TreeViewCard : ThemedStackPanel
{
    /// <summary>Creates the card and fills the tree.</summary>
    /// <param name="model">The shared sample data.</param>
    public TreeViewCard(GalleryModel model)
    {
        InitializeComponent();

        foreach (var node in model.TreeNodes)
        {
            tree.Items.Add(CardVisuals.BuildTreeItem(node));
        }

        tree.SelectedItem = tree.Items[0];
        OnSelectionChanged(this, EventArgs.Empty);
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        status.Text = tree.SelectedItem is Easel.Visuals.Controls.TreeViewItem item
            ? $"Selected: {item.Header} · depth {item.Depth}"
            : "No selection";
    }
}
