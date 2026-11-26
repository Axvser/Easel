using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Easel.Visuals;
using Easel.Visuals.Controls;
using DataGrid = Easel.Visuals.Controls.DataGrid;

namespace Easel.Test;

/// <summary>The 2-D table, its drop contract and the generic drop zone.</summary>
[STATestClass]
public sealed class DataGridTests
{
    [TestMethod]
    public void GridReportsTheFullViewAndOnlyTheVisibleRows()
    {
        var rows = Enumerable.Range(0, 1000).Select(index => new Row { Name = $"Row {index}", Value = index }).ToList();
        var grid = CreateGrid(rows, 480, 260);
        using var host = BuildHost(grid);

        Assert.AreEqual(1000, grid.RowCount);
        Assert.AreEqual(0, grid.FirstVisibleRow);
        Assert.IsGreaterThan(4, grid.VisibleRowCount);
        Assert.IsLessThan(20, grid.VisibleRowCount);
        Assert.IsTrue(grid.VisibleRowCount < grid.RowCount, "only a window of rows is visible");
    }

    [TestMethod]
    public void WheelScrollingMovesTheFirstVisibleRow()
    {
        var rows = Enumerable.Range(0, 1000).Select(index => new Row { Name = $"Row {index}", Value = index }).ToList();
        var grid = CreateGrid(rows, 480, 260);
        using var host = BuildHost(grid);

        host.ProcessPointerWheel(RowPoint(grid, 2), -360);

        Assert.IsGreaterThan(0, grid.FirstVisibleRow);
    }

    [TestMethod]
    public void HeaderSortCyclesAscendingDescendingAndSourceOrder()
    {
        var rows = new List<Row>
        {
            new() { Name = "Charlie", Value = 3 },
            new() { Name = "Alpha", Value = 1 },
            new() { Name = "Bravo", Value = 2 },
        };
        var grid = CreateGrid(rows, 480, 260);
        using var host = BuildHost(grid);

        grid.SelectedItem = rows[0];
        grid.SortBy(1);

        Assert.AreEqual(1d, ((Row)grid.GetRow(0)!).Value);
        Assert.AreSame(rows[0], grid.SelectedItem, "selection follows the item, not the row index");
        Assert.AreEqual(2, grid.IndexOf(rows[0]));

        grid.SortBy(1);
        Assert.AreEqual(3d, ((Row)grid.GetRow(0)!).Value);

        grid.SortBy(1);
        Assert.AreSame(rows[0], grid.GetRow(0), "a third click restores source order");
    }

    [TestMethod]
    public void DraggingARowBetweenGridsMovesTheSameObject()
    {
        var sourceRows = new ObservableCollection<Row>
        {
            new() { Name = "First", Value = 1 },
            new() { Name = "Second", Value = 2 },
        };
        var targetRows = new ObservableCollection<Row>();
        var source = CreateGrid(sourceRows, 480, 250);
        var target = CreateGrid(targetRows, 480, 250);
        var root = new StackPanel();
        root.Add(source);
        root.Add(target);
        using var host = BuildHost(root, 800, 600);

        var item = sourceRows[0];
        var payload = new DataGridDragPayload(source, new object?[] { item }, 0);
        var data = VisualDragDrop.CreateData(payload, "First");
        var point = RowPoint(target, 0);

        var enter = host.ProcessDragEnter(point, data, DragDropEffects.Move);
        Assert.AreEqual(DragDropEffects.Move, enter);
        var effects = host.ProcessDragDrop(point, data, DragDropEffects.Move);

        Assert.AreEqual(DragDropEffects.Move, effects);
        Assert.IsFalse(sourceRows.Contains(item), "the source list gave up the row");
        Assert.IsTrue(targetRows.Contains(item), "the target list received it");
        Assert.AreSame(item, targetRows[0], "the same instance moved");
    }

    [TestMethod]
    public void ExternalTextDropRaisesDroppedWithoutChangingTheSource()
    {
        var rows = new List<Row> { new() { Name = "Only", Value = 1 } };
        var grid = CreateGrid(rows, 480, 260);
        using var host = BuildHost(grid);
        string? text = null;
        IReadOnlyList<string>? files = null;
        grid.Dropped += (_, e) =>
        {
            text = e.Text;
            files = e.Files;
            e.Handled = true;
        };

        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, "line one\nline two");
        var effects = host.ProcessDragDrop(RowPoint(grid, 0), data, DragDropEffects.Copy);

        Assert.AreEqual(DragDropEffects.Copy, effects);
        Assert.AreEqual("line one\nline two", text);
        Assert.AreEqual(0, files!.Count);
        Assert.AreEqual(1, rows.Count, "the grid leaves external data to the Dropped handler");
    }

    [TestMethod]
    public void FileDropCarriesEveryPathToTheHandler()
    {
        var rows = new List<Row> { new() { Name = "Only", Value = 1 } };
        var grid = CreateGrid(rows, 480, 260);
        using var host = BuildHost(grid);
        string[]? received = null;
        grid.Dropped += (_, e) =>
        {
            received = e.Files.ToArray();
            e.Handled = true;
        };

        var data = new DataObject();
        data.SetData(DataFormats.FileDrop, new[] { @"C:\data\one.csv", @"C:\data\two.txt" });
        var effects = host.ProcessDragDrop(RowPoint(grid, 0), data, DragDropEffects.Copy);

        Assert.AreEqual(DragDropEffects.Copy, effects);
        CollectionAssert.AreEqual(new[] { @"C:\data\one.csv", @"C:\data\two.txt" }, received);
    }

    [TestMethod]
    public void HeaderEdgeDragResizesAColumn()
    {
        var rows = new List<Row> { new() { Name = "Only", Value = 1 } };
        var grid = CreateGrid(rows, 480, 260);
        grid.ShowRowNumbers = false;
        using var host = BuildHost(grid);
        var column = grid.Columns[0];

        var bounds = grid.AbsoluteBounds;
        var edge = new PointF(bounds.X + 100f - 1f, bounds.Y + 15f);
        host.ProcessPointerDown(edge, MouseButtons.Left);
        host.ProcessPointerMove(new PointF(edge.X + 40f, edge.Y));
        host.ProcessPointerUp(new PointF(edge.X + 40f, edge.Y), MouseButtons.Left);

        Assert.AreEqual(140d, column.Width, 0.6d, "the header edge sets the column width");
    }

    [TestMethod]
    public void ClickingAHeaderSortsByThatColumn()
    {
        var rows = new List<Row>
        {
            new() { Name = "Charlie", Value = 3 },
            new() { Name = "Alpha", Value = 1 },
            new() { Name = "Bravo", Value = 2 },
        };
        var grid = CreateGrid(rows, 480, 260);
        grid.ShowRowNumbers = false;
        using var host = BuildHost(grid);
        var bounds = grid.AbsoluteBounds;
        var header = new PointF(bounds.X + 20f, bounds.Y + 15f);

        host.ProcessPointerDown(header, MouseButtons.Left);
        host.ProcessPointerUp(header, MouseButtons.Left);

        Assert.AreEqual(0, grid.SortColumnIndex);
        Assert.AreSame(rows[1], grid.GetRow(0), "the Name column sorted ascending");
    }

    [TestMethod]
    public void RowDragBuildsTextAndLetsTheAppAddAFile()
    {
        var rows = new List<Row> { new() { Name = "First", Value = 1 }, new() { Name = "Second", Value = 2 } };
        var grid = CreateGrid(rows, 480, 260);
        using var host = BuildHost(grid);
        DataGridDragStartingEventArgs? start = null;
        grid.DragStarting += (_, e) =>
        {
            start = e;
            e.Data.SetData(DataFormats.FileDrop, new[] { @"C:\temp\row.csv" });
        };

        var point = RowPoint(grid, 0);
        host.ProcessPointerDown(point, MouseButtons.Left);
        host.ProcessPointerMove(new PointF(point.X + 10f, point.Y));
        host.ProcessPointerUp(new PointF(point.X + 10f, point.Y), MouseButtons.Left);

        Assert.IsNotNull(start);
        Assert.IsTrue(start!.Data.GetDataPresent(DataFormats.UnicodeText));
        Assert.IsTrue(start.Data.GetDataPresent(DataFormats.FileDrop));
        Assert.IsTrue(VisualDragDrop.TryGetData(start.Data, VisualDragDrop.DataGridRowsFormat, out DataGridDragPayload? payload));
        Assert.AreSame(rows[0], payload!.Items[0]);
    }

    [TestMethod]
    public void GridChromeFollowsTheActiveFallbackPalette()
    {
        var original = Theme.Current;
        try
        {
            var palette = Theme.CreateDark();
            palette.Surface = Color.FromArgb(0xFF, 0x10, 0x20, 0x30);
            palette.SurfaceAlt = Color.FromArgb(0xFF, 0x20, 0x30, 0x40);
            palette.ControlBorder = Color.FromArgb(0xFF, 0x30, 0x40, 0x50);
            palette.Selection = Color.FromArgb(0xFF, 0x40, 0x50, 0x60);
            palette.Foreground = Color.FromArgb(0xFF, 0xE0, 0xE0, 0xE0);
            palette.CornerRadius = 0f;
            Theme.Current = palette;

            var grid = CreateGrid(new List<Row> { new() { Name = "Only", Value = 1 } }, 200, 100);
            grid.ShowRowNumbers = false;
            grid.SelectedIndex = 0;
            using var host = BuildHost(grid, 200, 100);
            using var bitmap = new Bitmap(200, 100, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                host.RenderTo(graphics, new Size(200, 100));
            }

            Assert.AreEqual(
                palette.SurfaceAlt.ToArgb(),
                bitmap.GetPixel(90, 10).ToArgb(),
                "the header uses the surface-alt role");
            Assert.AreEqual(
                palette.Selection.ToArgb(),
                bitmap.GetPixel(150, 45).ToArgb(),
                "the selected row uses the selection role");
        }
        finally
        {
            Theme.Current = original;
        }
    }

    [TestMethod]
    public void ScrollingAPartialRowKeepsItUnderTheHeader()
    {
        var original = Theme.Current;
        try
        {
            var palette = Theme.CreateDark();
            palette.SurfaceAlt = Color.FromArgb(0xFF, 0x20, 0x30, 0x40);
            palette.Selection = Color.FromArgb(0xFF, 0x40, 0x50, 0x60);
            palette.Foreground = Color.FromArgb(0xFF, 0xE0, 0xE0, 0xE0);
            palette.CornerRadius = 0f;
            Theme.Current = palette;

            var rows = Enumerable.Range(0, 100).Select(index => new Row { Name = $"Row {index}", Value = index }).ToList();
            var grid = CreateGrid(rows, 300, 160);
            grid.SelectedIndex = 0;
            using var host = BuildHost(grid, 300, 160);
            grid.Children.OfType<Easel.Visuals.Controls.ScrollBar>().First().Value = 10d;

            using var bitmap = new Bitmap(300, 160, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                host.RenderTo(graphics, new Size(300, 160));
            }

            Assert.AreEqual(
                palette.SurfaceAlt.ToArgb(),
                bitmap.GetPixel(280, 25).ToArgb(),
                "a partially scrolled row must not paint over the header");
        }
        finally
        {
            Theme.Current = original;
        }
    }

    [TestMethod]
    public void WideningAColumnShowsTheHorizontalScrollBarAndOffsetsHitTesting()
    {
        var grid = new DataGrid
        {
            Width = 252,
            Height = 140,
            RowHeight = 30d,
            HeaderHeight = 30d,
            ItemsSource = Array.Empty<object>(),
        };
        grid.Columns.Add(new DataGridColumn { Header = "A", BindingPath = "Name", Width = 100d });
        grid.Columns.Add(new DataGridColumn { Header = "B", BindingPath = "Value", Width = 100d });
        grid.ShowRowNumbers = false;
        using var host = BuildHost(grid, 252, 140);

        var bar = grid.Children.OfType<Easel.Visuals.Controls.ScrollBar>()
            .Single(item => item.Orientation == Orientation.Horizontal);
        var rowsPart = grid.GetTemplateChild<VisualElement>(DataGrid.RowsPartName)!;
        Assert.AreEqual(Easel.Controls.Visibility.Collapsed, bar.Visibility, "columns that fit show no horizontal bar");
        Assert.AreEqual(1, grid.GetColumnIndexAt(new PointF(150f, 10f)), "the second column sits under x=150");

        // 拖动第一列的表头边缘，把列宽从 100 拉到 200，列总宽就超过行区。
        host.ProcessPointerDown(new PointF(99f, 15f), MouseButtons.Left);
        host.ProcessPointerMove(new PointF(199f, 15f));
        host.ProcessPointerUp(new PointF(199f, 15f), MouseButtons.Left);
        Assert.AreEqual(200d, grid.Columns[0].Width, 0.6d, "the header drag sets the column width");
        Render(host, 252, 140);
        Assert.AreEqual(Easel.Controls.Visibility.Visible, bar.Visibility, "the widened columns no longer fit");
        Assert.AreEqual(300d, bar.Maximum, 0.6d, "the extent includes both columns");
        Assert.AreEqual(rowsPart.Bounds.Width, bar.ViewportSize, 0.6d,
            "the viewport is the rows part, excluding the vertical-bar column");
        Assert.AreEqual(0, grid.GetColumnIndexAt(new PointF(150f, 10f)), "the widened first column covers x=150");

        bar.Value = 60d;
        Render(host, 252, 140);
        Assert.AreEqual(60d, bar.Value, 0.6d);
        Assert.AreEqual(1, grid.GetColumnIndexAt(new PointF(150f, 10f)), "hit testing follows the horizontal offset");

        grid.Columns[0].Width = 100d;
        Render(host, 252, 140);
        Assert.AreEqual(Easel.Controls.Visibility.Collapsed, bar.Visibility, "shrinking the columns hides the bar");
        Assert.AreEqual(0d, bar.Value, 0.6d, "hiding the bar resets the offset");
        Assert.AreEqual(1, grid.GetColumnIndexAt(new PointF(150f, 10f)), "hit testing returns to the unshifted columns");
    }

    private static void Render(VisualHost host, int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        host.RenderTo(graphics, new Size(width, height));
    }

    [TestMethod]
    public void DropZoneAcceptsTextAndReportsCopy()
    {
        var zone = new DropZone { Width = 320, Height = 120, Title = "Drop", Detail = "Anything" };
        using var host = BuildHost(zone, 400, 200);
        DropZoneEventArgs? received = null;
        zone.Dropped += (_, e) =>
        {
            received = e;
            e.Handled = true;
        };

        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, "hello");
        var point = new PointF(zone.AbsoluteBounds.X + 40f, zone.AbsoluteBounds.Y + 40f);

        var over = host.ProcessDragOver(point, data, DragDropEffects.Copy);
        var effects = host.ProcessDragDrop(point, data, DragDropEffects.Copy);

        Assert.AreEqual(DragDropEffects.Copy, over);
        Assert.AreEqual(DragDropEffects.Copy, effects);
        Assert.IsNotNull(received);
        Assert.AreEqual("hello", received!.Text);
    }

    [TestMethod]
    public void CreateDataPublishesTextAndInProcessPayload()
    {
        var grid = CreateGrid(new List<Row>(), 200, 100);
        var item = new Row { Name = "Payload", Value = 7 };
        var payload = new DataGridDragPayload(grid, new object?[] { item }, 0);
        var data = VisualDragDrop.CreateData(payload, "Payload", new[] { @"C:\temp\x.csv" });

        Assert.IsTrue(data.GetDataPresent(VisualDragDrop.DataGridRowsFormat));
        Assert.IsTrue(data.GetDataPresent(DataFormats.UnicodeText));
        Assert.IsTrue(data.GetDataPresent(DataFormats.FileDrop));
        Assert.IsTrue(VisualDragDrop.TryGetData(data, VisualDragDrop.DataGridRowsFormat, out DataGridDragPayload? restored));
        Assert.AreSame(payload, restored);
        Assert.AreEqual("Payload", VisualDragDrop.GetText(data));
        CollectionAssert.AreEqual(new[] { @"C:\temp\x.csv" }, VisualDragDrop.GetFiles(data));
    }

    private static DataGrid CreateGrid(System.Collections.IEnumerable rows, int width, int height)
    {
        var grid = new DataGrid
        {
            Width = width,
            Height = height,
            RowHeight = 30d,
            HeaderHeight = 30d,
            ItemsSource = rows,
        };
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = "Name", Width = 100d });
        grid.Columns.Add(new DataGridColumn { Header = "Value", BindingPath = "Value", Width = 0d });
        return grid;
    }

    private static VisualHost BuildHost(VisualElement root, int width = 480, int height = 300)
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

        public double Value { get; set; }
    }
}
