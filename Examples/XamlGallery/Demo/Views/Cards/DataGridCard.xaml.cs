using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Easel.Visuals.Controls;

namespace Demo.Views;

/// <summary>A large data table with annotations, drag out to Explorer and drag in from files or other grids.</summary>
public sealed partial class DataGridCard : ThemedStackPanel
{
    private readonly GalleryModel _model;
    private int _imported;

    /// <summary>Creates the card and wires the table columns and drop surfaces.</summary>
    /// <param name="model">The shared sample data.</param>
    public DataGridCard(GalleryModel model)
    {
        _model = model;
        DataContext = model;
        InitializeComponent();

        grid.Columns[0].TextSelector = static item => item is SampleRow row ? $"{row.Code} {row.Name}" : null;
        grid.Columns[4].ColorSelector = static item => item is SampleRow row ? StatusColor(row.Status) : Color.Empty;
        grid.Columns[5].ColorSelector = static item => item is SampleRow row ? StatusColor(row.Status) : Color.Empty;
        grid.SortChanged += OnSortChanged;
        grid.SelectedIndex = 0;
        UpdateStats();
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (grid.SelectedItem is SampleRow row)
        {
            SelectedStat.Text = row.Code;
            ActionStat.Text = $"{row.Owner} · {row.Status}";
        }
        else
        {
            SelectedStat.Text = "—";
            ActionStat.Text = "Nothing selected";
        }
    }

    private void OnSortChanged(object? sender, EventArgs e)
    {
        if (grid.SortColumnIndex < 0 || grid.SortColumnIndex >= grid.Columns.Count)
        {
            ActionStat.Text = "Source order";
            return;
        }

        var direction = grid.SortDirection == System.ComponentModel.ListSortDirection.Ascending ? "↑" : "↓";
        ActionStat.Text = $"{grid.Columns[grid.SortColumnIndex].Header} {direction}";
    }

    private void OnDragStarting(object? sender, DataGridDragStartingEventArgs e)
    {
        if (grid.SelectedItem is not SampleRow row)
        {
            return;
        }

        e.GhostText = $"{row.Code} · {row.Name}";
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "easel-ledger-row.csv");
            File.WriteAllText(
                path,
                "Code,Name,Owner,Team,Value,Status,Progress" + Environment.NewLine
                + string.Join(
                    ",",
                    Escape(row.Code),
                    Escape(row.Name),
                    Escape(row.Owner),
                    Escape(row.Department),
                    row.Value.ToString(CultureInfo.InvariantCulture),
                    Escape(row.Status),
                    row.Progress.ToString("0", CultureInfo.InvariantCulture)));
            e.Data.SetData(DataFormats.FileDrop, new[] { path });
        }
        catch (IOException)
        {
            // 临时文件写不进去时仍保留文本拖放；Explorer 目标只是可选增强。
        }

        ActionStat.Text = $"Dragging {row.Code}";
    }

    private void OnDropped(object? sender, DataGridDropEventArgs e)
    {
        if (e.Files.Count > 0)
        {
            var added = ImportFiles(e.Files, e.InsertIndex);
            e.Handled = added > 0;
            return;
        }

        if (e.Payload is { } payload)
        {
            if (ReferenceEquals(payload.Source, grid))
            {
                ActionStat.Text = "Row reordered";
            }
            else
            {
                ActionStat.Text = $"Moved {payload.Items.Count} from another grid";
            }

            UpdateStats();
            return;
        }

        if (!string.IsNullOrWhiteSpace(e.Text))
        {
            var added = ImportText(e.Text, e.InsertIndex);
            e.Handled = added > 0;
        }
    }

    private void OnZoneDropped(object? sender, DropZoneEventArgs e)
    {
        if (e.Files.Count > 0)
        {
            e.Handled = ImportFiles(e.Files, _model.TableRows.Count) > 0;
            return;
        }

        if (e.Payload is { } payload && payload.Items.Count > 0)
        {
            var first = payload.Items[0] as SampleRow;
            ActionStat.Text = first is null ? $"Received {payload.Items.Count} rows" : $"Received {first.Code}";
            e.Handled = true;
            return;
        }

        if (!string.IsNullOrWhiteSpace(e.Text))
        {
            e.Handled = ImportText(e.Text, _model.TableRows.Count) > 0;
        }
    }

    private int ImportFiles(IReadOnlyList<string> files, int index)
    {
        var added = 0;
        foreach (var path in files)
        {
            if (IsCsv(path) && File.Exists(path))
            {
                try
                {
                    added += ImportText(File.ReadAllText(path), index + added);
                    continue;
                }
                catch (IOException)
                {
                    // 读不到就按普通文件名导入，拖放不应失败。
                }
            }

            var name = Path.GetFileNameWithoutExtension(path);
            InsertRow(NewImportedRow(name, "Imported file"), index + added);
            added++;
        }

        FinishImport(added, "file");
        return added;
    }

    private int ImportText(string text, int index)
    {
        var added = 0;
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("Code,", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cells = line.Split(',');
            var row = NewImportedRow(cells.Length > 1 ? cells[1].Trim() : line, "Imported text");
            if (cells.Length > 0 && !string.IsNullOrWhiteSpace(cells[0]))
            {
                row.Code = cells[0].Trim();
            }

            if (cells.Length > 2)
            {
                row.Owner = cells[2].Trim();
            }

            if (cells.Length > 5)
            {
                row.Status = cells[5].Trim();
            }

            InsertRow(row, index + added);
            added++;
            if (added >= 200)
            {
                break;
            }
        }

        FinishImport(added, "line");
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

    private void InsertRow(SampleRow row, int index)
    {
        var target = grid.SortColumnIndex >= 0
            ? _model.TableRows.Count
            : Math.Max(0, Math.Min(index, _model.TableRows.Count));
        _model.TableRows.Insert(target, row);
        grid.SelectedItem = row;
    }

    private void FinishImport(int added, string unit)
    {
        if (added <= 0)
        {
            ActionStat.Text = "Nothing to import";
            return;
        }

        ActionStat.Text = $"Imported {added} {unit}{(added == 1 ? string.Empty : "s")}";
        UpdateStats();
    }

    private void UpdateStats()
    {
        RowsStat.Text = _model.TableRows.Count.ToString("N0", CultureInfo.CurrentCulture);
        if (grid.SelectedItem is SampleRow row)
        {
            SelectedStat.Text = row.Code;
        }
    }

    private static bool IsCsv(string path)
        => path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

    private static string Escape(string value)
        => value.IndexOfAny([',', '"', '\n']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

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
