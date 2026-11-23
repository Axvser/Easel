using Easel.Visuals.Controls;

namespace Demo.Views;

/// <summary>The menu-bar card; the rows and their commands are built here.</summary>
public sealed partial class MenuCard : ThemedStackPanel
{
    /// <summary>Creates the view, its menu rows and their commands.</summary>
    public MenuCard()
    {
        InitializeComponent();

        var file = new VisualMenuItem { Header = "File" };
        file.Items.Add(Item("New", "Ctrl+N", "File · New"));
        file.Items.Add(Item("Open…", "Ctrl+O", "File · Open"));
        file.Items.Add(new VisualMenuItem { IsSeparator = true });
        file.Items.Add(Item("Exit", "Alt+F4", "File · Exit"));

        var edit = new VisualMenuItem { Header = "Edit" };
        edit.Items.Add(Item("Undo", "Ctrl+Z", "Edit · Undo"));
        edit.Items.Add(Item("Redo", "Ctrl+Y", "Edit · Redo"));

        var view = new VisualMenuItem { Header = "View" };
        var grid = Item("Show grid", null, "View · Show grid");
        grid.IsChecked = true;
        view.Items.Add(grid);

        menu.Items.Add(file);
        menu.Items.Add(edit);
        menu.Items.Add(view);
    }

    private VisualMenuItem Item(string header, string? gesture, string status)
        => new() { Header = header, InputGestureText = gesture, Command = () => menuStatus.Text = status };
}
