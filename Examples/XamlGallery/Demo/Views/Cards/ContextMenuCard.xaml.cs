using System.Drawing;
using Easel.Visuals.Controls;

namespace Demo.Views;

/// <summary>The context-menu card; the detached menu is built here and opened at a surface point.</summary>
public sealed partial class ContextMenuCard : ThemedStackPanel
{
    private readonly VisualContextMenu _menu = new();

    /// <summary>Creates the view and fills the detached menu.</summary>
    public ContextMenuCard()
    {
        InitializeComponent();

        _menu.Items.Add(Item("Duplicate", "Ctrl+D", "Duplicate"));
        _menu.Items.Add(Item("Rename", "F2", "Rename"));
        _menu.Items.Add(new VisualMenuItem { IsSeparator = true });
        _menu.Items.Add(Item("Delete", "Del", "Delete"));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _menu.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnOpenMenuClick(object? sender, EventArgs e)
        => _menu.OpenAt(
            openButton,
            new PointF(openButton.AbsoluteBounds.X + 12f, openButton.AbsoluteBounds.Bottom));

    private VisualMenuItem Item(string header, string? gesture, string status)
        => new() { Header = header, InputGestureText = gesture, Command = () => menuStatus.Text = status };
}
