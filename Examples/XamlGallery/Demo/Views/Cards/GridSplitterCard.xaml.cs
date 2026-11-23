namespace Demo.Views;

/// <summary>The GridSplitter card; the sash target is a named element wired after initialization.</summary>
public sealed partial class GridSplitterCard : ThemedStackPanel
{
    /// <summary>Creates the view, runs the generated initializer and points the sash at the pane.</summary>
    public GridSplitterCard()
    {
        InitializeComponent();
        splitter.Target = pane;
    }
}
