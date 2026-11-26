using System.Drawing;
using System.Drawing.Imaging;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;
using DataGrid = Easel.Visuals.Controls.DataGrid;

namespace Easel.Test;

/// <summary>The default ListBox, TreeView, DataGrid, Calendar and menu surfaces stay transparent so the host owns the fill.</summary>
[STATestClass]
public sealed class TransparentDefaultSurfacesTests
{
    private static readonly Color Backdrop = Color.Magenta;

    [TestMethod]
    public void ListBox_PaintsNoCarryingSurface()
    {
        using var surface = new VisualHost { Size = new Size(120, 80), DpiScaleOverride = 1f };
        surface.Root = new VC.ListBox { Width = 120d, Height = 80d };
        using var bitmap = Render(surface, 120, 80);

        AssertBackdrop(bitmap, "ListBox", new Point(2, 2), new Point(117, 2), new Point(2, 77), new Point(117, 77), new Point(60, 40));
    }

    [TestMethod]
    public void TreeView_PaintsNoCarryingSurface()
    {
        using var surface = new VisualHost { Size = new Size(120, 80), DpiScaleOverride = 1f };
        surface.Root = new VC.TreeView { Width = 120d, Height = 80d };
        using var bitmap = Render(surface, 120, 80);

        AssertBackdrop(bitmap, "TreeView", new Point(2, 2), new Point(117, 2), new Point(2, 77), new Point(117, 77), new Point(60, 40));
    }

    [TestMethod]
    public void DataGrid_PaintsOnlyItsHeaderAndEmptyStateNotACarryingSurface()
    {
        using var surface = new VisualHost { Size = new Size(160, 100), DpiScaleOverride = 1f };
        surface.Root = new DataGrid
        {
            Width = 160d,
            Height = 100d,
            HeaderHeight = 30d,
            ShowGridLines = false,
            AlternateRows = false,
            ItemsSource = Array.Empty<object>(),
        };
        using var bitmap = Render(surface, 160, 100);

        // 表头是内容；表体空区域必须透出宿主底色。
        AssertBackdrop(bitmap, "DataGrid", new Point(2, 97), new Point(157, 97), new Point(80, 97));
    }

    [TestMethod]
    public void Calendar_PaintsOnlyItsMonthContentNotACarryingSurface()
    {
        using var surface = new VisualHost { Size = new Size(250, 240), DpiScaleOverride = 1f };
        surface.Root = new VC.Calendar
        {
            Width = 250d,
            Height = 240d,
            DisplayMonth = new DateTime(2024, 5, 1),
        };
        using var bitmap = Render(surface, 250, 240);

        AssertBackdrop(bitmap, "Calendar", new Point(2, 2), new Point(247, 2), new Point(2, 237), new Point(247, 237));
    }

    [TestMethod]
    public void VisualContextMenu_KeepsItsDefaultPopupSurface()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var root = new VC.StackPanel();
        using var target = new VC.Border { Width = 80f, Height = 30f, Background = Color.Lime };
        root.Add(target);
        surface.Root = root;

        using var menu = new VC.VisualContextMenu();
        menu.Items.Add(new VC.VisualMenuItem { Header = "Delete" });
        menu.OpenAt(target, new PointF(target.AbsoluteBounds.X + 2f, target.AbsoluteBounds.Y + 2f));

        using var bitmap = Render(surface, 320, 240);

        // 弹层没有宿主可依托，子菜单又只共享模板：默认表面必须在这里，不能被背景透过。
        var bounds = menu.AbsoluteBounds;
        var expected = Theme.Current.SurfaceAlt.ToArgb();
        foreach (var point in new[]
                 {
                     new Point((int)bounds.X + 3, (int)bounds.Y + 3),
                     new Point((int)bounds.X + 3, (int)bounds.Bottom - 3),
                 })
        {
            Assert.AreEqual(expected, bitmap.GetPixel(point.X, point.Y).ToArgb(),
                $"the popup menu painted no surface at {point}");
        }
    }

    [TestMethod]
    public void VisualMenuBar_PaintsNoCarryingSurface()
    {
        using var surface = new VisualHost { Size = new Size(160, 80), DpiScaleOverride = 1f };
        surface.Root = new VC.VisualMenu { Width = 160d, Height = 40d };
        using var bitmap = Render(surface, 160, 80);

        AssertBackdrop(bitmap, "VisualMenu", new Point(2, 2), new Point(157, 2), new Point(2, 37), new Point(157, 37));
    }

    [TestMethod]
    public void SelfDrawnFallbacks_PaintNoCarryingSurface()
    {
        // 模板被显式空样式关掉后，自绘回退路径同样不能铺底色或边框。
        using var surface = new VisualHost { Size = new Size(320, 320), DpiScaleOverride = 1f };

        using var grid = new DataGrid
        {
            Width = 160d,
            Height = 100d,
            HeaderHeight = 30d,
            ShowGridLines = false,
            AlternateRows = false,
            ItemsSource = Array.Empty<object>(),
            Style = new VisualStyle { TargetType = typeof(DataGrid) },
        };
        surface.Root = grid;
        using (var bitmap = Render(surface, 320, 320))
        {
            Assert.IsNull(grid.TemplateRoot, "the explicit empty style opts out of the default template");
            AssertBackdrop(bitmap, "self-drawn DataGrid", new Point(2, 97), new Point(157, 97));
        }

        using var calendar = new VC.Calendar
        {
            Width = 250d,
            Height = 240d,
            DisplayMonth = new DateTime(2024, 5, 1),
            Style = new VisualStyle { TargetType = typeof(VC.Calendar) },
        };
        surface.Root = calendar;
        using (var bitmap = Render(surface, 320, 320))
        {
            Assert.IsNull(calendar.TemplateRoot, "the explicit empty style opts out of the default template");
            AssertBackdrop(bitmap, "self-drawn Calendar", new Point(2, 2), new Point(247, 237));
        }
    }

    private static Bitmap Render(VisualHost surface, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Backdrop);
            surface.RenderTo(graphics, new Size(width, height));
        }

        return bitmap;
    }

    private static void AssertBackdrop(Bitmap bitmap, string control, params Point[] points)
    {
        foreach (var point in points)
        {
            Assert.AreEqual(
                Backdrop.ToArgb(),
                bitmap.GetPixel(point.X, point.Y).ToArgb(),
                $"a default {control} painted its own carrying surface at {point}");
        }
    }
}
