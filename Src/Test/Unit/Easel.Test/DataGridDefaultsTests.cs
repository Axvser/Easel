using System.Drawing;
using System.Drawing.Imaging;
using Easel.Visuals;
using Easel.Visuals.Controls;
using DataGrid = Easel.Visuals.Controls.DataGrid;

namespace Easel.Test;

/// <summary>The DataGrid default style, its shell parts and the row/cell/header appearance seams.</summary>
[STATestClass]
public sealed class DataGridDefaultsTests
{
    [TestMethod]
    public void DefaultStyleDeclaresTheAppearanceSeamsAndShellTemplate()
    {
        Assert.IsTrue(
            Theme.DefaultStyles.TryGetValue(typeof(DataGrid), out var raw),
            "the default style table carries an exact-type DataGrid entry");
        var style = raw as VisualStyle;
        Assert.IsNotNull(style);
        Assert.AreSame(typeof(DataGrid), style!.TargetType);
        Assert.IsNotNull(style.Template, "the fallback appearance ships a shell template");

        var properties = style.Setters
            .Where(static setter => setter.Property is not null)
            .Select(static setter => setter.Property!)
            .ToHashSet();
        foreach (var expected in new[]
                 {
                     DataGrid.CellPaddingProperty,
                     DataGrid.HeaderPaddingProperty,
                     DataGrid.ColumnHeaderFontStyleProperty,
                     DataGrid.BadgeHeightProperty,
                     DataGrid.BadgePaddingProperty,
                     DataGrid.CheckSizeProperty,
                     DataGrid.CheckCornerRadiusProperty,
                     DataGrid.ProgressTrackHeightProperty,
                     DataGrid.ProgressCornerRadiusProperty,
                 })
        {
            Assert.IsTrue(properties.Contains(expected), $"{expected.Name} is a default-style setter");
        }
    }

    [TestMethod]
    public void ShellTemplateExposesTheHeaderRowCellHostAndScrollBarParts()
    {
        var grid = CreateGrid(new List<Row> { new() { Name = "Only" } }, 240, 120);
        grid.HeaderHeight = 40d;
        using var host = BuildHost(grid, 240, 120);
        using var bitmap = Render(host, 240, 120);

        Assert.IsNotNull(grid.TemplateRoot, "the default style applies the shell template");

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
            Assert.IsNotNull(part, $"{name} is resolved from the applied template");
            Assert.IsFalse(part!.IsHitTestVisible, "decorative shell parts stay out of hit testing");
        }

        var headers = grid.GetTemplateChild<VisualElement>(DataGrid.ColumnHeadersPartName)!;
        Assert.AreEqual(40d, headers.Bounds.Height, 0.6d, "the header part follows HeaderHeight");
    }

    [TestMethod]
    public void RowHeaderAndSelectionSeamsDriveThePaintedPixels()
    {
        var rows = new List<Row> { new() { Name = "First" }, new() { Name = "Second" } };
        var grid = CreateGrid(rows, 240, 120);
        grid.AlternateRows = false;
        grid.ShowGridLines = false;
        grid.SelectedIndex = 0;

        var header = Color.FromArgb(0xFF, 0x20, 0x40, 0x80);
        var row = Color.FromArgb(0xFF, 0xC0, 0x30, 0x30);
        var selection = Color.FromArgb(0xFF, 0x30, 0xA0, 0x50);
        var style = new VisualStyle { TargetType = typeof(DataGrid) };
        style.Setters.Add(new VisualSetter(DataGrid.ColumnHeaderBackgroundProperty, new SolidColorBrush(header)));
        style.Setters.Add(new VisualSetter(DataGrid.RowBackgroundProperty, new SolidColorBrush(row)));
        style.Setters.Add(new VisualSetter(DataGrid.SelectedRowBackgroundProperty, new SolidColorBrush(selection)));
        grid.Styles.Add(style);

        using var host = BuildHost(grid, 240, 120);
        using var bitmap = Render(host, 240, 120);

        Assert.AreEqual(header.ToArgb(), bitmap.GetPixel(180, 10).ToArgb(), "the header seam paints the header strip");
        Assert.AreEqual(selection.ToArgb(), bitmap.GetPixel(180, 45).ToArgb(), "the selection seam paints row 0");
        Assert.AreEqual(row.ToArgb(), bitmap.GetPixel(180, 75).ToArgb(), "the row seam paints the unselected row");
    }

    [TestMethod]
    public void BadgeCheckAndProgressSeamsDriveTheirPaintedFills()
    {
        var rows = new List<Row> { new() { Name = "OK", Active = true, Progress = 100d } };
        var grid = CreateGrid(rows, 300, 120);
        grid.ShowGridLines = false;
        grid.Columns.Clear();
        grid.Columns.Add(new DataGridColumn { Header = "Badge", BindingPath = "Name", Width = 100d, Kind = DataGridColumnKind.Badge });
        grid.Columns.Add(new DataGridColumn { Header = "Check", BindingPath = "Active", Width = 100d, Kind = DataGridColumnKind.Check });
        grid.Columns.Add(new DataGridColumn { Header = "Progress", BindingPath = "Progress", Width = 0d, Kind = DataGridColumnKind.Progress });

        var badge = Color.FromArgb(0xFF, 0xC0, 0x20, 0xC0);
        var check = Color.FromArgb(0xFF, 0x20, 0xA0, 0x40);
        var progress = Color.FromArgb(0xFF, 0xE0, 0x80, 0x10);
        grid.BadgeBackground = new SolidColorBrush(badge);
        grid.CheckFill = new SolidColorBrush(check);
        grid.ProgressFillBrush = new SolidColorBrush(progress);

        using var host = BuildHost(grid, 300, 120);
        using var bitmap = Render(host, 300, 120);

        Assert.AreEqual(badge.ToArgb(), bitmap.GetPixel(13, 45).ToArgb(), "the badge seam fills the pill");
        Assert.AreEqual(check.ToArgb(), bitmap.GetPixel(113, 45).ToArgb(), "the check seam fills the box");
        Assert.AreEqual(progress.ToArgb(), bitmap.GetPixel(220, 45).ToArgb(), "the progress seam fills the track");
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
        grid.Columns.Add(new DataGridColumn { Header = "Name", BindingPath = "Name", Width = 0d });
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

    private sealed class Row
    {
        public string Name { get; set; } = string.Empty;

        public bool Active { get; set; }

        public double Progress { get; set; }
    }
}
