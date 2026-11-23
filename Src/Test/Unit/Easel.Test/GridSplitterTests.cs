using Easel.Controls;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Easel.Visuals;
using Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>A sash grows the pane on the side the pointer moves toward.</summary>
[TestClass]
public sealed class GridSplitterTests
{
    private static (VisualHost Host, Border Target, GridSplitter Splitter) Build(bool dockRight)
    {
        var target = new Border { Width = 120d };
        var splitter = new GridSplitter { Target = target, Orientation = Orientation.Vertical };
        var fill = new Border();
        var dock = new DockPanel { LastChildFill = true };
        dock.Add(target);
        DockPanel.SetDock(target, dockRight ? DockPlacement.Right : DockPlacement.Left);
        dock.Add(splitter);
        DockPanel.SetDock(splitter, dockRight ? DockPlacement.Right : DockPlacement.Left);
        dock.Add(fill);

        return (CreateHost(dock), target, splitter);
    }

    private static (VisualHost Host, Border Target, GridSplitter Splitter) BuildHorizontal(bool dockBottom)
    {
        var target = new Border { Height = 120d };
        var splitter = new GridSplitter { Target = target, Orientation = Orientation.Horizontal };
        var fill = new Border();
        var dock = new DockPanel { LastChildFill = true };
        dock.Add(target);
        DockPanel.SetDock(target, dockBottom ? DockPlacement.Bottom : DockPlacement.Top);
        dock.Add(splitter);
        DockPanel.SetDock(splitter, dockBottom ? DockPlacement.Bottom : DockPlacement.Top);
        dock.Add(fill);

        return (CreateHost(dock), target, splitter);
    }

    private static VisualHost CreateHost(DockPanel dock)
    {
        var host = new VisualHost { Root = dock, DpiScaleOverride = 1f, Size = new Size(400, 200) };
        using var bitmap = new Bitmap(400, 200, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            host.RenderTo(graphics, new Size(400, 200));
        }

        return host;
    }

    private static void Drag(VisualHost host, GridSplitter splitter, float dx)
    {
        var bounds = splitter.AbsoluteBounds;
        Assert.IsTrue(bounds.Width > 0f && bounds.Height > 0f, $"splitter bounds {bounds}");
        var start = new PointF(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));
        host.ProcessPointerDown(start, MouseButtons.Left);
        host.ProcessPointerMove(new PointF(start.X + dx, start.Y));
        host.ProcessPointerUp(new PointF(start.X + dx, start.Y), MouseButtons.Left);
    }

    private static void DragVertical(VisualHost host, GridSplitter splitter, float dy)
    {
        var bounds = splitter.AbsoluteBounds;
        Assert.IsTrue(bounds.Width > 0f && bounds.Height > 0f, $"splitter bounds {bounds}");
        var start = new PointF(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));
        host.ProcessPointerDown(start, MouseButtons.Left);
        host.ProcessPointerMove(new PointF(start.X, start.Y + dy));
        host.ProcessPointerUp(new PointF(start.X, start.Y + dy), MouseButtons.Left);
    }

    [TestMethod]
    public void LeftDockedTarget_DraggingRightWidensIt()
    {
        var (host, target, splitter) = Build(dockRight: false);
        using (host)
        {
            Drag(host, splitter, 20f);
            Assert.AreEqual(140d, target.Width, 0.5d);
        }
    }

    [TestMethod]
    public void RightDockedTarget_DraggingLeftWidensIt()
    {
        var (host, target, splitter) = Build(dockRight: true);
        using (host)
        {
            Drag(host, splitter, -20f);
            Assert.AreEqual(140d, target.Width, 0.5d);
        }
    }

    [TestMethod]
    public void TopDockedTarget_DraggingDownGrowsHeight()
    {
        var (host, target, splitter) = BuildHorizontal(dockBottom: false);
        using (host)
        {
            DragVertical(host, splitter, 20f);
            Assert.AreEqual(140d, target.Height, 0.5d);
        }
    }

    [TestMethod]
    public void BottomDockedTarget_DraggingUpGrowsHeight()
    {
        var (host, target, splitter) = BuildHorizontal(dockBottom: true);
        using (host)
        {
            DragVertical(host, splitter, -20f);
            Assert.AreEqual(140d, target.Height, 0.5d);
        }
    }

    [TestMethod]
    public void AutoSizedTarget_DragStartsFromItsArrangedSize()
    {
        var target = new Border
        {
            Padding = new Thickness(0f),
            Child = new StackPanel { Children = { new Border { Width = 120d, Height = 120d } } },
        };
        var splitter = new GridSplitter { Target = target, Orientation = Orientation.Vertical };
        var fill = new Border();
        var dock = new DockPanel { LastChildFill = true };
        dock.Add(target);
        DockPanel.SetDock(target, DockPlacement.Left);
        dock.Add(splitter);
        DockPanel.SetDock(splitter, DockPlacement.Left);
        dock.Add(fill);

        using var host = CreateHost(dock);
        Assert.AreEqual(120d, target.Bounds.Width, 0.5d, "the auto-sized target arranged at its content width");

        Drag(host, splitter, 20f);
        Assert.AreEqual(140d, target.Width, 0.5d, "the drag starts from the arranged width, not double.NaN");
    }
}
