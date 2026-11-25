using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Easel.Visuals;
using Easel.Visuals.Controls;
using DataGrid = Easel.Visuals.Controls.DataGrid;
using VisualDataGridCell = Easel.Visuals.Controls.DataGridCell;

namespace Easel.Test;

/// <summary>The DataGrid kind cells come from keyed templates, and the shell parts stay a geometry contract.</summary>
[STATestClass]
public sealed class DataGridResidualTests
{
    [TestMethod]
    public void KindCellTemplatesAreKeyedDefaultResources()
    {
        foreach (var key in new[]
                 {
                     DataGrid.BadgeCellTemplateKey,
                     DataGrid.CheckCellTemplateKey,
                     DataGrid.ProgressCellTemplateKey,
                 })
        {
            Assert.IsTrue(Theme.DefaultStyles.TryGetValue(key, out var raw), $"{key} is a keyed default resource");
            Assert.IsInstanceOfType<DataTemplate>(raw);
        }

        Assert.IsFalse(
            Theme.DefaultStyles.ContainsKey(DataGrid.TextCellTemplateKey),
            "no default text-cell template is registered, so text columns stay self-drawn");
    }

    [TestMethod]
    public void BuiltInCellContainersHaveExactTypeDefaultStyles()
    {
        foreach (var type in new[]
                 {
                     typeof(DataGridBadgeCell),
                     typeof(DataGridCheckCell),
                     typeof(DataGridProgressCell),
                 })
        {
            Assert.IsTrue(Theme.DefaultStyles.TryGetValue(type, out var raw), $"{type.Name} has an exact-type default style");
            var style = raw as VisualStyle;
            Assert.IsNotNull(style);
            Assert.AreSame(type, style!.TargetType);
            Assert.IsNotNull(style.BasedOn, "the derived cell styles share one decorative base");
            Assert.IsTrue(
                style.BasedOn!.Setters.Any(setter =>
                    ReferenceEquals(setter.Property, VisualElement.IsHitTestVisibleProperty) && Equals(setter.Value, false)),
                "the shared cell base keeps the containers out of hit testing");
        }
    }

    [TestMethod]
    public void NonTextKindsRealizeBuiltInCellElements()
    {
        var rows = Enumerable.Range(0, 500).Select(index => new Row
        {
            Name = $"Row {index}",
            Active = (index % 2) == 0,
            Progress = index % 101,
            Value = index,
        }).ToList();
        var grid = CreateKindGrid(rows);
        using var host = BuildHost(grid, 360, 150);

        var cells = grid.RealizedCells.OfType<VisualDataGridCell>().ToList();
        Assert.IsTrue(cells.OfType<DataGridBadgeCell>().Any(), "a badge column realizes badge cells");
        Assert.IsTrue(cells.OfType<DataGridCheckCell>().Any(), "a check column realizes check cells");
        Assert.IsTrue(cells.OfType<DataGridProgressCell>().Any(), "a progress column realizes progress cells");
        Assert.IsLessThan(40, grid.RealizedCellCount, "only the visible window is realized");

        foreach (var cell in cells)
        {
            Assert.AreSame(grid, cell.Owner, "the cell knows the grid that styles it");
            Assert.IsNotNull(cell.Column, "the cell knows the column that supplies its value");
            Assert.IsNotNull(cell.DataContext, "the row item stays the cell's data context");
        }
    }

    [TestMethod]
    public void PerColumnCellFactoryStillWinsOverTheKindTemplate()
    {
        var rows = Enumerable.Range(0, 100).Select(index => new Row { Name = $"Row {index}" }).ToList();
        var grid = new DataGrid
        {
            Width = 240,
            Height = 120,
            RowHeight = 30d,
            HeaderHeight = 30d,
            ItemsSource = rows,
            ShowRowNumbers = false,
        };
        grid.Columns.Add(new DataGridColumn
        {
            Header = "Status",
            BindingPath = "Name",
            Width = 0d,
            Kind = DataGridColumnKind.Badge,
            CellFactory = static _ => new TextBlock { Text = "custom" },
        });
        using var host = BuildHost(grid, 240, 120);

        Assert.IsGreaterThan(0, grid.RealizedCellCount);
        Assert.AreEqual(0, grid.RealizedCells.OfType<DataGridBadgeCell>().Count(), "the column factory replaces the kind cell");
        Assert.IsTrue(grid.RealizedCells.All(static cell => cell is TextBlock));
    }

    [TestMethod]
    public void HostResourcesCanReplaceAKindTemplate()
    {
        var rows = Enumerable.Range(0, 100).Select(index => new Row { Name = $"Row {index}" }).ToList();
        var grid = new DataGrid
        {
            Width = 240,
            Height = 120,
            RowHeight = 30d,
            HeaderHeight = 30d,
            ItemsSource = rows,
            ShowRowNumbers = false,
        };
        grid.Resources[DataGrid.BadgeCellTemplateKey] = new DataTemplate(() => new TextBlock { Text = "override" });
        grid.Columns.Add(new DataGridColumn
        {
            Header = "Status",
            BindingPath = "Name",
            Width = 0d,
            Kind = DataGridColumnKind.Badge,
        });
        using var host = BuildHost(grid, 240, 120);

        Assert.IsGreaterThan(0, grid.RealizedCellCount);
        Assert.AreEqual(0, grid.RealizedCells.OfType<DataGridBadgeCell>().Count(), "the host template wins over the default");
        Assert.IsTrue(grid.RealizedCells.All(static cell => cell is TextBlock));
        Assert.AreSame(rows[grid.FirstVisibleRow], grid.RealizedCells[0].DataContext);
    }

    [TestMethod]
    public void KindCellsPaintTheBadgeCheckProgressAndSelectionSeams()
    {
        var rows = new List<Row> { new() { Name = "OK", Active = true, Progress = 100d } };
        var grid = new DataGrid
        {
            Width = 300,
            Height = 120,
            RowHeight = 30d,
            HeaderHeight = 30d,
            ItemsSource = rows,
            ShowRowNumbers = false,
            ShowGridLines = false,
            SelectedIndex = 0,
        };
        grid.Columns.Add(new DataGridColumn { Header = "Badge", BindingPath = "Name", Width = 100d, Kind = DataGridColumnKind.Badge });
        grid.Columns.Add(new DataGridColumn { Header = "Check", BindingPath = "Active", Width = 100d, Kind = DataGridColumnKind.Check });
        grid.Columns.Add(new DataGridColumn { Header = "Progress", BindingPath = "Progress", Width = 0d, Kind = DataGridColumnKind.Progress });

        var badge = Color.FromArgb(0xFF, 0xC0, 0x20, 0xC0);
        var check = Color.FromArgb(0xFF, 0x20, 0xA0, 0x40);
        var progress = Color.FromArgb(0xFF, 0xE0, 0x80, 0x10);
        var selection = Color.FromArgb(0xFF, 0x30, 0xA0, 0x50);
        grid.BadgeBackground = new SolidColorBrush(badge);
        grid.CheckFill = new SolidColorBrush(check);
        grid.ProgressFillBrush = new SolidColorBrush(progress);
        grid.SelectedRowBackground = new SolidColorBrush(selection);

        using var host = BuildHost(grid, 300, 120);
        using var bitmap = Render(host, 300, 120);

        Assert.AreEqual(badge.ToArgb(), bitmap.GetPixel(13, 45).ToArgb(), "the badge kind cell fills the pill");
        Assert.AreEqual(check.ToArgb(), bitmap.GetPixel(113, 45).ToArgb(), "the check kind cell fills the box");
        Assert.AreEqual(progress.ToArgb(), bitmap.GetPixel(220, 45).ToArgb(), "the progress kind cell fills the track");
        Assert.AreEqual(selection.ToArgb(), bitmap.GetPixel(180, 45).ToArgb(), "the selected row still shows around the kind cells");
    }

    [TestMethod]
    public void ShellPartsStayGeometryOnlyWhileTheStyleSeamsPaint()
    {
        var rows = new List<Row> { new() { Name = "First", Value = 1 }, new() { Name = "Second", Value = 2 } };
        var grid = new DataGrid
        {
            Width = 300,
            Height = 120,
            RowHeight = 30d,
            HeaderHeight = 30d,
            ItemsSource = rows,
            RowBackground = new SolidColorBrush(Color.FromArgb(0xFF, 0x10, 0x20, 0x30)),
            ColumnHeaderBackground = new SolidColorBrush(Color.FromArgb(0xFF, 0x40, 0x50, 0x60)),
        };
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = "Name", Width = 0d });
        using var host = BuildHost(grid, 300, 120);

        foreach (var name in new[]
                 {
                     DataGrid.ColumnHeadersPartName,
                     DataGrid.RowsPartName,
                     DataGrid.CellHostPartName,
                     DataGrid.ScrollBarPartName,
                     DataGrid.CornerPartName,
                 })
        {
            var part = grid.GetTemplateChild<VisualElement>(name);
            Assert.IsNotNull(part, $"{name} is resolved from the shell template");
            Assert.IsFalse(part!.IsHitTestVisible, "a shell part stays out of hit testing");
            Assert.IsNull(part.Background, "a shell part never takes over a style brush");
            Assert.IsNull(part.BorderBrush, "a shell part never paints its own outline");
            Assert.AreEqual(0, part.Children.Count, "a shell part only supplies geometry");
        }
    }

    [TestMethod]
    public void KindCellsKeepScrollingAndSelection()
    {
        var rows = Enumerable.Range(0, 100).Select(index => new Row
        {
            Name = $"Row {index}",
            Active = index % 2 == 0,
            Progress = index % 101,
            Value = index,
        }).ToList();
        var grid = CreateKindGrid(rows);
        using var host = BuildHost(grid, 360, 150);

        host.ProcessPointerWheel(RowPoint(grid, 2), -360);
        Assert.IsGreaterThan(0, grid.FirstVisibleRow, "the wheel still scrolls kind cells");
        Assert.IsLessThan(40, grid.RealizedCellCount, "scrolling keeps the realized window bounded");

        grid.SelectedIndex = 1;
        Assert.AreSame(rows[1], grid.SelectedItem, "selection still follows the row item");
    }

    private static DataGrid CreateKindGrid(IEnumerable<Row> rows)
    {
        var grid = new DataGrid
        {
            Width = 360,
            Height = 150,
            RowHeight = 30d,
            HeaderHeight = 30d,
            ItemsSource = rows,
            ShowRowNumbers = false,
            ShowGridLines = false,
        };
        grid.Columns.Add(new DataGridColumn { Header = "Status", BindingPath = "Name", Width = 120d, Kind = DataGridColumnKind.Badge });
        grid.Columns.Add(new DataGridColumn { Header = "Live", BindingPath = "Active", Width = 120d, Kind = DataGridColumnKind.Check });
        grid.Columns.Add(new DataGridColumn { Header = "Progress", BindingPath = "Progress", Width = 120d, Kind = DataGridColumnKind.Progress });
        return grid;
    }

    private static VisualHost BuildHost(VisualElement root, int width, int height)
    {
        var host = new VisualHost
        {
            Root = root,
            DpiScaleOverride = 1f,
            Size = new Size(width, height),
        };
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        host.RenderTo(graphics, new Size(width, height));
        return host;
    }

    private static Bitmap Render(VisualHost host, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            host.RenderTo(graphics, new Size(width, height));
        }

        return bitmap;
    }

    private static PointF RowPoint(DataGrid grid, int row)
    {
        var bounds = grid.AbsoluteBounds;
        return new PointF(
            bounds.X + 20f,
            bounds.Y + (float)grid.HeaderHeight + (row * (float)grid.RowHeight) + ((float)grid.RowHeight / 2f));
    }

    private sealed class Row
    {
        public string Name { get; set; } = string.Empty;

        public bool Active { get; set; }

        public double Progress { get; set; }

        public double Value { get; set; }
    }
}
