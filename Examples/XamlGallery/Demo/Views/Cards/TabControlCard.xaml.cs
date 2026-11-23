namespace Demo.Views;

/// <summary>The TabControl card; the item list is filled after initialization and the visible page synced.</summary>
public sealed partial class TabControlCard : ThemedUserControl
{
    /// <summary>Creates the view, fills the data tab and syncs the selected page.</summary>
    public TabControlCard()
    {
        InitializeComponent();
        tabList.ItemsSource = new[] { "One", "Two", "Three" };
        tabs.Refresh();
    }
}
