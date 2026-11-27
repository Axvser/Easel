using System.Drawing;
using System.Windows.Forms;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>Native islands must follow their visual element when an ancestor scrolls.</summary>
[STATestClass]
public sealed class NativeScrollTests
{
    [TestMethod]
    public void NativeIslandMovesWithTheScrollViewerOffset()
    {
        using var surface = new VisualHost { Size = new Size(200, 120) };
        using var label = new Label { Text = "native" };
        var native = new VC.NativeElement(label) { Width = 80, Height = 30 };
        var content = new VC.StackPanel { Orientation = Orientation.Vertical };
        content.Add(native);
        content.Add(new VC.Border { Height = 300 });
        var viewer = new VC.ScrollViewer { Width = 200, Height = 100, Content = content };
        surface.Root = viewer;

        Layout(surface, 200, 120);
        var before = label.Bounds.Top;
        Assert.IsTrue(label.Bounds.Height > 0, "the island was placed");

        viewer.VerticalOffset = 20f;
        Layout(surface, 200, 120);

        Assert.IsNotNull(viewer.ContentPresenter, "the viewer has a scroll presenter");
        Assert.AreEqual(20f, viewer.VerticalOffset, 0.1f, "the viewer accepted the offset");
        Assert.AreEqual(-10f, content.AbsoluteBounds.Top, 1, "the content moved with the scroll");
        Assert.AreEqual(before - 20, native.AbsoluteBounds.Top, 1, "the visual element moved with the scroll");
        Assert.AreEqual(before - 20, label.Bounds.Top, 1, "the HWND follows the scrolled visual element");
    }

    [TestMethod]
    public void NativeIslandHidesAndReturnsWithTheScrollWindow()
    {
        using var surface = new VisualHost { Size = new Size(200, 120) };
        using var label = new Label { Text = "native" };
        var native = new VC.NativeElement(label) { Width = 80, Height = 30 };
        var content = new VC.StackPanel { Orientation = Orientation.Vertical };
        content.Add(native);
        content.Add(new VC.Border { Height = 300 });
        var viewer = new VC.ScrollViewer { Width = 200, Height = 100, Content = content };
        surface.Root = viewer;

        Layout(surface, 200, 120);
        var before = label.Bounds.Top;

        viewer.VerticalOffset = 200f;
        Layout(surface, 200, 120);
        Assert.IsFalse(label.Visible, "a fully scrolled-out island is hidden instead of floating at its old spot");

        viewer.VerticalOffset = 0f;
        Layout(surface, 200, 120);
        Assert.IsTrue(label.Visible, "the island returns with the visual element");
        Assert.AreEqual(before, label.Bounds.Top, 1, "the returning island is placed at the visual position");
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
