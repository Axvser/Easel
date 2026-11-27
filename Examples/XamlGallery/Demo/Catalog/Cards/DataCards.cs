using Demo.Views;

namespace Demo;

/// <summary>The data-control cards, each authored as a XAML view.</summary>
internal static class DataCards
{
    /// <summary>Creates the data group in catalog order.</summary>
    /// <param name="model">The shared sample data.</param>
    public static IReadOnlyList<GalleryCard> Create(GalleryModel model) =>
    [
        Card(
            "data.data-grid",
            "Data",
            "DataGrid",
            "A virtualized two-dimensional table: sortable headers, badge and progress cells, row numbers and native row drag.",
            "<DataGrid ItemsSource=\"{Binding TableRows}\" ShowRowNumbers=\"True\"><DataGrid.Columns>…",
            _ => new DataGridCard(model)),
        Card(
            "data.drag-board",
            "Data",
            "Drag board",
            "Two grids that move the same row objects between collections; files from Explorer land as rows.",
            "<DataGrid AllowDrop=\"True\" Dropped=\"OnDropped\" />",
            _ => new DragBoardCard(model)),
        Card(
            "data.list-box",
            "Data",
            "ListBox",
            "Rows from a collection; selection is a dependency property.",
            "<ListBox ItemsSource=\"{Binding Rows}\" DisplayMemberPath=\"Name\" />",
            _ => new ListBoxCard(model)),
        Card(
            "data.combo-box",
            "Data",
            "ComboBox",
            "Read-only and editable drop-downs that open in the surface overlay; hover is neutral and the open/focus rings fade in.",
            "<ComboBox IsEditable=\"True\" Placeholder=\"Type or choose…\" />",
            _ => new ComboBoxCard()),
        Card(
            "data.virtualizing-list-box",
            "Data",
            "VirtualizingListBox",
            "Fixed-row virtualization driven by a ScrollViewer.",
            "<ScrollViewer><VirtualizingListBox ItemsSource=\"{Binding VirtualRows}\" ItemHeight=\"26\" /></ScrollViewer>",
            _ => new VirtualizingListBoxCard(model)),
        Card(
            "data.tree-view",
            "Data",
            "TreeView",
            "A hierarchical catalog with expansion and selection.",
            "<TreeView><TreeViewItem Header=\"Surface\" IsExpanded=\"True\" /></TreeView>",
            _ => new TreeViewCard(model)),
        Card(
            "data.calendar",
            "Data",
            "Calendar",
            "A month grid drawn by GDI+.",
            "<Calendar Width=\"250\" Height=\"226\" />",
            _ => new CalendarCard()),
        Card(
            "data.date-picker",
            "Data",
            "DatePicker",
            "A date field whose calendar opens in the overlay.",
            "<DatePicker Width=\"220\" />",
            _ => new DatePickerCard()),
        Card(
            "data.virtualizing-panel",
            "Data",
            "VirtualizingPanel · 100k log",
            "A custom VirtualizingPanel subclass windows a 100,000-row log and switches to 1,000,000 rows; scrolling rebinds pooled rows instead of creating them.",
            "<local:VirtualLogPanel ItemsSource=\"{Binding Rows}\" ItemExtent=\"28\" />",
            _ => new VirtualizationPanelCard()),
    ];

    private static GalleryCard Card(
        string id, string group, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create)
        => new(new GalleryCardInfo(id, group, title, summary, usage), create);
}
