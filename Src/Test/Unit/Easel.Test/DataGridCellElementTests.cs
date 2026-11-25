using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Easel.Visuals;
using Easel.Visuals.Controls;
using DataGrid = Easel.Visuals.Controls.DataGrid;

namespace Easel.Test;

/// <summary>Column cell templates realize only the visible cells as elements with real bounds.</summary>
[STATestClass]
public sealed class DataGridCellElementTests
{
    [TestMethod]
    public void TemplatedCellsRealizeOnlyTheVisibleWindow()
    {
        var rows = Enumerable.Range(0, 2000).Select(index => new Row { Name = $"Row {index}", Value = index }).ToList();
        var grid = CreateGrid(rows);
        using var host = BuildHost(grid, 480, 300);

        Assert.IsTrue(grid.Columns[0].HasCellElement, "the first column realizes its cells as elements");
        Assert.IsFalse(grid.Columns[1].HasCellElement, "the second column keeps the self-drawn path");
        Assert.IsGreaterThan(0, grid.RealizedCellCount);
        Assert.IsLessThan(40, grid.RealizedCellCount, "a 2000-row grid realizes only the visible cells");

        foreach (var cell in grid.RealizedCells)
        {
            Assert.IsInstanceOfType<TextBlock>(cell);
            Assert.AreEqual(grid.RowHeight, cell.Bounds.Height, 0.6d, "the cell is measured to the row height");
            Assert.IsNotNull(cell.DataContext, "the cell receives its row as the data context");
            Assert.IsTrue(cell.AbsoluteBounds.Top >= grid.AbsoluteBounds.Top, "the cell starts inside the grid");
        }

        var selectedRow = grid.GetRow(0);
        Assert.IsTrue(grid.RealizedCells.Any(cell => ReferenceEquals(cell.DataContext, selectedRow)),
            "the first visible row's cell is bound to its item");
    }

    [TestMethod]
    public void ScrollingRecyclesCellsAndKeepsTheCountBounded()
    {
        var rows = Enumerable.Range(0, 2000).Select(index => new Row { Name = $"Row {index}", Value = index }).ToList();
        var grid = CreateGrid(rows);
        using var host = BuildHost(grid, 480, 300);
        var firstCell = grid.RealizedCells[0];
        var before = grid.RealizedCellCount;

        host.ProcessPointerWheel(RowPoint(grid, 2), -360);
        using var bitmap = new Bitmap(480, 300, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            host.RenderTo(graphics, new Size(480, 300));
        }

        Assert.IsGreaterThan(0, grid.FirstVisibleRow, "the wheel moved the viewport");
        Assert.IsLessThan(40, grid.RealizedCellCount, "the realized window stays bounded after scrolling");
        Assert.IsTrue(grid.RealizedCellCount <= before + 4, "scrolling recycles instead of realizing the whole column");
        Assert.IsFalse(ReferenceEquals(firstCell, grid.RealizedCells[0]) && grid.FirstVisibleRow == 0,
            "the first cell follows the viewport");
    }

    [TestMethod]
    public void CellTemplateCreatesTheElement()
    {
        var rows = Enumerable.Range(0, 50).Select(index => new Row { Name = $"Row {index}", Value = index }).ToList();
        var grid = new DataGrid
        {
            Width = 480,
            Height = 300,
            RowHeight = 30d,
            HeaderHeight = 30d,
            ItemsSource = rows,
        };
        grid.Columns.Add(new DataGridColumn
        {
            Header = "Name",
            BindingPath = "Name",
            Width = 140d,
            CellTemplate = new DataTemplate(() => new TextBlock()),
        });
        using var host = BuildHost(grid, 480, 300);

        Assert.IsGreaterThan(0, grid.RealizedCellCount);
        Assert.IsInstanceOfType<TextBlock>(grid.RealizedCells[0]);
        Assert.AreSame(rows[0], grid.RealizedCells[0].DataContext, "the template element receives the row");
    }

    [TestMethod]
    public void ClickingATemplatedCellStillSelectsItsRow()
    {
        var rows = Enumerable.Range(0, 200).Select(index => new Row { Name = $"Row {index}", Value = index }).ToList();
        var grid = CreateGrid(rows);
        using var host = BuildHost(grid, 480, 300);
        var target = Center(grid.RealizedCells[0].AbsoluteBounds);

        host.ProcessPointerDown(target, MouseButtons.Left);
        host.ProcessPointerUp(target, MouseButtons.Left);

        Assert.AreEqual(0, grid.SelectedIndex, "a click on the cell element bubbles to the grid");
        Assert.AreSame(rows[0], grid.SelectedItem);
    }

    [TestMethod]
    public void CellsWithoutAFactoryKeepTheSelfDrawnPath()
    {
        var rows = Enumerable.Range(0, 100).Select(index => new Row { Name = $"Row {index}", Value = index }).ToList();
        var grid = new DataGrid
        {
            Width = 480,
            Height = 300,
            RowHeight = 30d,
            HeaderHeight = 30d,
            ItemsSource = rows,
        };
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = "Name", Width = 120d });
        grid.Columns.Add(new DataGridColumn { Header = "Value", BindingPath = "Value", Width = 0d });
        using var host = BuildHost(grid, 480, 300);

        Assert.AreEqual(0, grid.RealizedCellCount, "no template means no cell elements");
    }

    private static DataGrid CreateGrid(IEnumerable<Row> rows)
    {
        var grid = new DataGrid
        {
            Width = 480,
            Height = 300,
            RowHeight = 30d,
            HeaderHeight = 30d,
            ItemsSource = rows,
        };
        grid.Columns.Add(new DataGridColumn
        {
            Header = "Name",
            BindingPath = "Name",
            Width = 140d,
            CellFactory = item => new TextBlock { Text = ((Row)item!).Name },
        });
        grid.Columns.Add(new DataGridColumn { Header = "Value", BindingPath = "Value", Width = 0d });
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

    private static PointF RowPoint(DataGrid grid, int row)
    {
        var bounds = grid.AbsoluteBounds;
        return new PointF(
            bounds.X + 20f,
            bounds.Y + (float)grid.HeaderHeight + (row * (float)grid.RowHeight) + ((float)grid.RowHeight / 2f));
    }

    private static PointF Center(RectangleF bounds)
        => new(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));

    private sealed class Row
    {
        public string Name { get; set; } = string.Empty;

        public double Value { get; set; }
    }
}
