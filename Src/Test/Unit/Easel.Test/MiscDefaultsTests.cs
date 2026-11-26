using System.Drawing;
using System.Windows.Forms;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>The built-in ScrollBar, HeaderedContentControl and DropZone styles keep their parts and input paths.</summary>
[STATestClass]
public sealed class MiscDefaultsTests
{
    [TestMethod]
    public void ScrollBarDefaultTemplate_ExposesThemeTrackAndKeepsPointerInput()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        var trackColor = Color.FromArgb(0xFF, 0x33, 0x44, 0x55);

        using var bar = new VC.ScrollBar
        {
            Width = 8,
            Height = 100,
            Maximum = 1000,
            ViewportSize = 300,
            LargeChange = 150,
            TrackBrush = new SolidColorBrush(trackColor),
        };
        surface.Root = bar;
        Layout(surface, 320, 240);

        Assert.IsNotNull(bar.TemplateRoot, "the implicit ScrollBar style supplies the default template");
        var track = bar.GetTemplateChild<VisualElement>("PART_Track");
        Assert.IsNotNull(track);
        Assert.IsFalse(track!.IsHitTestVisible, "the track is decorative");
        Assert.AreEqual(trackColor.ToArgb(), track.Background!.ApproximateColor.ToArgb(),
            "the track paints the control's TrackBrush");

        var press = bar.PointToSurface(new PointF(3f, 80f));
        surface.ProcessPointerDown(press, MouseButtons.Left);
        Assert.AreEqual(150d, bar.Value, 0.6d, "a track press still pages");
        surface.ProcessPointerUp(press, MouseButtons.Left);

        var start = bar.PointToSurface(new PointF(3f, 10f));
        var move = bar.PointToSurface(new PointF(3f, 45f));
        surface.ProcessPointerDown(start, MouseButtons.Left);
        surface.ProcessPointerMove(move);
        surface.ProcessPointerUp(move, MouseButtons.Left);
        Assert.IsGreaterThan(200d, bar.Value, "the fallback thumb still drags");
    }

    [TestMethod]
    public void ScrollBarDefaultTemplate_ReservesTheCornerOutsideTheTrack()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var bar = new VC.ScrollBar
        {
            Width = 8,
            Height = 100,
            Maximum = 1000,
            ViewportSize = 300,
            ReserveCornerSize = 8,
        };
        surface.Root = bar;
        Layout(surface, 320, 240);

        Assert.AreSame(bar, surface.FindElementAt(bar.PointToSurface(new PointF(3f, 50f))), "the track is hit");
        Assert.IsNull(surface.FindElementAt(bar.PointToSurface(new PointF(3f, 97f))),
            "a template track that spans the bar still leaves the reserved corner out");
    }

    [TestMethod]
    public void HeaderedContentControlDefaultTemplate_HostsTheHeaderAndContent()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var header = new VC.HeaderedContentControl { Width = 200, Height = 120, Header = "Details" };
        var content = new VC.Border { Padding = default, Background = Color.Orange };
        header.Content = content;
        surface.Root = header;
        Layout(surface, 320, 240);

        Assert.IsNotNull(header.TemplateRoot, "the implicit HeaderedContentControl style supplies the default template");
        var label = header.GetTemplateChild<VC.TextBlock>("PART_Header");
        var host = header.GetTemplateChild<VisualElement>("PART_ContentHost");
        Assert.IsNotNull(label);
        Assert.IsNotNull(host);
        Assert.AreEqual("Details", label!.Text);
        Assert.IsFalse(label.IsHitTestVisible, "the header label is decorative");
        Assert.AreSame(host, content.ParentElement, "content moves into the template host");
        Assert.AreEqual(24f, host!.Bounds.Y, 0.75f, "the host starts below the header row");
        Assert.AreEqual(96f, host.Bounds.Height, 0.75f, "the host fills the remaining row");
        Assert.AreEqual(96f, content.Bounds.Height, 0.75f, "content fills the host");

        header.Header = "Updated";
        Assert.AreEqual("Updated", label.Text, "a later Header write reaches the template label");

        var replacement = new VC.Border { Padding = default, Background = Color.Lime };
        header.Content = replacement;
        Layout(surface, 320, 240);

        Assert.AreSame(host, replacement.ParentElement, "changing Content re-parents the new visual");
        Assert.IsNull(content.ParentElement, "the old content leaves the host");
    }

    [TestMethod]
    public void DropZoneDefaultTemplate_KeepsTheHitTargetAndDragState()
    {
        using var surface = new VisualHost { Size = new Size(400, 240), DpiScaleOverride = 1f };
        using var zone = new VC.DropZone { Width = 320, Height = 120, Title = "Drop", Detail = "Anything" };
        surface.Root = zone;
        Layout(surface, 400, 240);

        Assert.IsNotNull(zone.TemplateRoot, "the implicit DropZone style supplies the default template");
        var chrome = zone.GetTemplateChild<VC.DropZoneChrome>("Chrome");
        Assert.IsNotNull(chrome);
        Assert.IsFalse(chrome!.IsHitTestVisible, "the chrome is decorative");
        Assert.AreEqual("Drop", chrome.Title);
        Assert.AreEqual("Anything", chrome.Detail);

        var point = new PointF(zone.AbsoluteBounds.X + 40f, zone.AbsoluteBounds.Y + 40f);
        Assert.AreSame(zone, surface.FindElementAt(point), "the zone itself owns the hit, not its chrome");

        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, "hello");
        Assert.AreEqual(DragDropEffects.Copy, surface.ProcessDragOver(point, data, DragDropEffects.Copy));
        Assert.IsTrue(zone.IsHot, "drag-over raises the hot state");
        Assert.IsTrue(chrome.IsHot, "the chrome receives the hot state");

        VC.DropZoneEventArgs? dropped = null;
        zone.Dropped += (_, e) => dropped = e;
        Assert.AreEqual(DragDropEffects.Copy, surface.ProcessDragDrop(point, data, DragDropEffects.Copy));
        Assert.IsNotNull(dropped);
        Assert.AreEqual("hello", dropped!.Text);
        Assert.IsFalse(zone.IsHot, "a completed drop clears the hot state");
        Assert.IsFalse(chrome.IsHot);
    }

    [TestMethod]
    public void DropZoneDefaultTemplate_FollowsThePointerHoverState()
    {
        using var surface = new VisualHost { Size = new Size(400, 240), DpiScaleOverride = 1f };
        using var zone = new VC.DropZone { Width = 320, Height = 120 };
        surface.Root = zone;
        Layout(surface, 400, 240);

        var chrome = zone.GetTemplateChild<VC.DropZoneChrome>("Chrome");
        Assert.IsNotNull(chrome);
        var point = new PointF(zone.AbsoluteBounds.X + 40f, zone.AbsoluteBounds.Y + 40f);

        surface.ProcessPointerMove(point);
        Assert.IsTrue(zone.IsPointerOver);
        Assert.IsTrue(chrome!.IsPointerOver);

        surface.ProcessPointerMove(new PointF(1f, 1f));
        Assert.IsFalse(zone.IsPointerOver);
        Assert.IsFalse(chrome.IsPointerOver);
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
