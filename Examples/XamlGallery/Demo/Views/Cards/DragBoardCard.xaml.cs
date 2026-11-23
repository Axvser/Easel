using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Easel.Visuals.Controls;
using DataGrid = Easel.Visuals.Controls.DataGrid;

namespace Demo.Views;

/// <summary>Two grids that move the same row objects between their collections, and accept file drops.</summary>
public sealed partial class DragBoardCard : ThemedStackPanel
{
    private int _imported;

    /// <summary>Creates the two-grid board.</summary>
    /// <param name="model">The shared sample data.</param>
    public DragBoardCard(GalleryModel model)
    {
        DataContext = model;
        InitializeComponent();

        ConfigureGrid(inbox);
        ConfigureGrid(done);
        inbox.SelectedIndex = 0;
        done.SelectedIndex = 0;
        UpdateStats();
    }

    private static void ConfigureGrid(DataGrid grid)
    {
        grid.Columns[0].TextSelector = static item => item is SampleRow row ? $"{row.Code}   {row.Name}" : null;
        grid.Columns[1].ColorSelector = static item => item is SampleRow row ? StatusColor(row.Status) : Color.Empty;
    }

    private void OnInboxSelectionChanged(object? sender, EventArgs e) => UpdateStats();

    private void OnDoneSelectionChanged(object? sender, EventArgs e) => UpdateStats();

    private void OnInboxDragStarting(object? sender, DataGridDragStartingEventArgs e) => SetGhost(e, inbox);

    private void OnDoneDragStarting(object? sender, DataGridDragStartingEventArgs e) => SetGhost(e, done);

    private static void SetGhost(DataGridDragStartingEventArgs e, DataGrid grid)
    {
        if (grid.SelectedItem is SampleRow row)
        {
            e.GhostText = $"{row.Code} · {row.Name}";
        }
    }

    private void OnInboxDropped(object? sender, DataGridDropEventArgs e)
    {
        HandleDrop(inbox, "Inbox", e);
    }

    private void OnDoneDropped(object? sender, DataGridDropEventArgs e)
    {
        HandleDrop(done, "Done", e);
    }

    private void HandleDrop(DataGrid grid, string caption, DataGridDropEventArgs e)
    {
        if (e.Files.Count > 0)
        {
            var added = ImportFiles(e.Files, e.InsertIndex, grid);
            action.Text = added > 0 ? $"Imported {added} into {caption}" : "Nothing to import";
            e.Handled = added > 0;
            UpdateStats();
            return;
        }

        if (e.Payload is { } payload)
        {
            if (ReferenceEquals(payload.Source, grid))
            {
                action.Text = $"{caption} reordered";
            }
            else
            {
                grid.SelectedItem = payload.Items.Count > 0 ? payload.Items[0] : null;
                action.Text = $"{caption} received {payload.Items.Count} row{(payload.Items.Count == 1 ? string.Empty : "s")}";
            }

            UpdateStats();
            return;
        }

        if (!string.IsNullOrWhiteSpace(e.Text))
        {
            var added = ImportText(e.Text, e.InsertIndex, grid);
            action.Text = added > 0 ? $"Imported {added} into {caption}" : "Nothing to import";
            e.Handled = added > 0;
            UpdateStats();
        }
    }

    private int ImportFiles(IReadOnlyList<string> files, int index, DataGrid grid)
    {
        var added = 0;
        foreach (var path in files)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            Insert(grid, NewImportedRow(name, "Imported file"), index + added);
            added++;
        }

        return added;
    }

    private int ImportText(string text, int index, DataGrid grid)
    {
        var added = 0;
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var cells = line.Split(',');
            var row = NewImportedRow(cells.Length > 1 ? cells[1].Trim() : line, "Imported text");
            if (cells.Length > 0 && !string.IsNullOrWhiteSpace(cells[0]))
            {
                row.Code = cells[0].Trim();
            }

            Insert(grid, row, index + added);
            added++;
            if (added >= 100)
            {
                break;
            }
        }

        return added;
    }

    private SampleRow NewImportedRow(string name, string team)
        => new()
        {
            Code = $"IMP-{++_imported:000}",
            Name = string.IsNullOrWhiteSpace(name) ? "Untitled import" : name,
            Department = team,
            Owner = Environment.UserName,
            Status = "On track",
            Value = 0d,
            Progress = 0d,
            Enabled = true,
            Updated = DateTime.Now,
        };

    private static void Insert(DataGrid grid, SampleRow row, int index)
    {
        if (grid.ItemsSource is not IList<SampleRow> list)
        {
            return;
        }

        var target = index < 0 || index > list.Count ? list.Count : index;
        list.Insert(target, row);
        grid.SelectedItem = row;
    }

    private void UpdateStats()
    {
        inboxStat.Text = inbox.SelectedItem is SampleRow left
            ? $"{left.Code} · {inbox.RowCount} rows"
            : $"{inbox.RowCount} rows";
        doneStat.Text = done.SelectedItem is SampleRow right
            ? $"{right.Code} · {done.RowCount} rows"
            : $"{done.RowCount} rows";
    }

    private static Color StatusColor(string status)
        => status switch
        {
            "On track" => Color.FromArgb(0xFF, 0x2E, 0x9E, 0x6E),
            "At risk" => Color.FromArgb(0xFF, 0xD9, 0x8A, 0x1F),
            "Blocked" => Color.FromArgb(0xFF, 0xD1, 0x4B, 0x4B),
            "Shipped" => Color.FromArgb(0xFF, 0x00, 0x78, 0xD4),
            _ => Color.FromArgb(0xFF, 0x8A, 0x8A, 0x8A),
        };
}
