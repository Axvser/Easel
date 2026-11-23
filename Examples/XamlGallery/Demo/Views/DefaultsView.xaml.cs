using Easel;
using Easel.Visuals.Controls;

namespace Demo.Views;

/// <summary>The default-controls card declared in visual XAML; only the data-bound parts are filled here.</summary>
public sealed partial class DefaultsView : ThemedStackPanel
{
    /// <summary>Creates the view and fills its list, tree, combo box and date examples.</summary>
    /// <param name="model">The shared sample data behind the tree view.</param>
    public DefaultsView(GalleryModel model)
    {
        InitializeComponent();

        listBox.ItemsSource = new[] { "Alpha", "Bravo", "Charlie", "Delta" };
        comboBox.Items.Add("Balanced");
        comboBox.Items.Add("Throughput");
        comboBox.Items.Add("Latency");
        comboBox.SelectedIndex = 0;
        foreach (var node in model.TreeNodes)
        {
            tree.Items.Add(CardVisuals.BuildTreeItem(node));
        }

        calendar.SelectedDate = DateTime.Today;
        datePicker.SelectedDate = DateTime.Today;
    }
}
