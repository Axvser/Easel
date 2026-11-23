using Demo.Views;

namespace Demo;

/// <summary>The Layout card group: each card is a XAML view built from the themed base types.</summary>
internal static class LayoutCards
{
    /// <summary>Creates the layout group in catalog order.</summary>
    public static IReadOnlyList<GalleryCard> Create() =>
    [
        Card(
            "layout.stack-panel",
            "Layout",
            "StackPanel",
            "Vertical and horizontal stacks with an explicit gap.",
            "<StackPanel Orientation=\"Horizontal\" Spacing=\"10\">",
            _ => new StackPanelCard()),
        Card(
            "layout.dock-panel",
            "Layout",
            "DockPanel",
            "Children dock to the four edges; the last child fills.",
            "DockPanel.Dock=\"Left\"",
            _ => new DockPanelCard()),
        Card(
            "layout.grid",
            "Layout",
            "Grid",
            "Pixel, auto and star tracks with attached row/column placement.",
            "<Grid.ColumnDefinitions>…<ColumnDefinition Width=\"*\" />",
            _ => new GridCard()),
        Card(
            "layout.wrap-panel",
            "Layout",
            "WrapPanel",
            "Wraps items into lines within the available width.",
            "<WrapPanel ItemWidth=\"70\" ItemHeight=\"32\" Spacing=\"6\">",
            _ => new WrapPanelCard()),
        Card(
            "layout.canvas",
            "Layout",
            "Canvas",
            "Absolute placement with attached left/top.",
            "Canvas.Left=\"30\" Canvas.Top=\"30\"",
            _ => new CanvasCard()),
        Card(
            "layout.uniform-grid",
            "Layout",
            "UniformGrid",
            "Equal cells; the column count wraps the rows.",
            "<UniformGrid Columns=\"3\">",
            _ => new UniformGridCard()),
        Card(
            "layout.viewbox",
            "Layout",
            "Viewbox",
            "Scales one child into the box with a visual transform.",
            "<Viewbox Width=\"260\" Height=\"120\">",
            _ => new ViewboxCard()),
        Card(
            "layout.splitter",
            "Layout",
            "GridSplitter",
            "Drag the sash to resize the adjacent pane.",
            "<GridSplitter Orientation=\"Vertical\" Width=\"6\" />",
            _ => new GridSplitterCard()),
        Card(
            "layout.group-box",
            "Layout",
            "GroupBox",
            "A titled frame with one child.",
            "<GroupBox Header=\"Settings\">",
            _ => new GroupBoxCard()),
        Card(
            "layout.expander",
            "Layout",
            "Expander",
            "A header toggles one content element.",
            "<Expander Header=\"Advanced options\" IsExpanded=\"True\">",
            _ => new ExpanderCard()),
        Card(
            "layout.tab-control",
            "Layout",
            "TabControl",
            "One visible page at a time, switched by pointer or arrows.",
            "<TabControl><TabItem Header=\"Layout\">…",
            _ => new TabControlCard()),
    ];

    private static GalleryCard Card(
        string id, string group, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create)
        => new(new GalleryCardInfo(id, group, title, summary, usage), create);
}
