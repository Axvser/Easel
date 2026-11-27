using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Easel.Visuals;
using Easel.Visuals.Dialogs;
using VC = Easel.Visuals.Controls;
using DialogResult = Easel.Visuals.Dialogs.DialogResult;
using XamlThickness = Easel.Controls.Thickness;

namespace Easel.Test;

/// <summary>
/// Native islands must look like part of the Easel surface: snapshot backdrops come from the visual tree,
/// transparent native backgrounds survive window composition, and composition forces the snapshot path.
/// </summary>
[STATestClass]
public sealed class NativeEmbeddingTests
{
    private static readonly Color Green = Color.FromArgb(0, 140, 0);
    private static readonly Color CanvasColor = Color.FromArgb(20, 20, 20);

    [TestMethod]
    public void SnapshotOfATransparentControlUsesTheAncestorBackground()
    {
        using var form = new Form
        {
            Size = new Size(300, 200),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(60, 60),
        };
        using var surface = new VisualHost { Dock = DockStyle.Fill, DpiScaleOverride = 1f };
        form.Controls.Add(surface);

        var root = new VC.Canvas { Background = new SolidColorBrush(CanvasColor) };
        var green = new VC.Border
        {
            Width = 200,
            Height = 60,
            Padding = new XamlThickness(0),
            Background = new SolidColorBrush(Green),
            HorizontalAlignment = Easel.Controls.HorizontalAlignment.Left,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Top,
        };
        VC.Canvas.SetLeft(green, 20);
        VC.Canvas.SetTop(green, 20);
        root.Add(green);
        using var label = new Label { Text = "t", BackColor = Color.Transparent, ForeColor = Color.White };
        var island = new VC.NativeElement(label) { Width = 120, Height = 40, IsSnapshot = true };
        green.Child = island;
        surface.Root = root;
        form.Show();
        form.Activate();
        Pump(6);

        Assert.IsNotNull(island.InputProxy, "the snapshot keeps an input proxy while a form exists");
        Assert.AreEqual(Green, island.InputProxy!.BackColor, "the proxy backdrop is the visual background, not the theme");

        using var bitmap = new Bitmap(300, 200);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            surface.RenderTo(graphics, new Size(300, 200));
        }

        var pixel = bitmap.GetPixel(170, 65);
        Assert.IsTrue(pixel.G > 100 && pixel.R < 80, $"the transparent control shows the green ancestor (pixel={pixel})");

        form.Close();
    }

    [TestMethod]
    public void LiveTransparentControlTakesTheVisualBackgroundAndRestoresIt()
    {
        using var surface = new VisualHost { Size = new Size(300, 200) };
        using var root = new VC.Canvas { Width = 300, Height = 200 };
        var green = new VC.Border
        {
            Width = 200,
            Height = 60,
            Padding = new XamlThickness(0),
            Background = new SolidColorBrush(Green),
            HorizontalAlignment = Easel.Controls.HorizontalAlignment.Left,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Top,
        };
        root.Add(green);
        using var label = new Label { Text = "x", BackColor = Color.Transparent, ForeColor = Color.White };
        var island = new VC.NativeElement(label) { Width = 120, Height = 40 };
        green.Child = island;
        surface.Root = root;

        Layout(surface, 300, 200);

        Assert.AreEqual(Green, label.BackColor, "a transparent native background is replaced by the surface colour");

        island.Native = null;

        Assert.AreEqual(Color.Transparent, label.BackColor, "detaching restores the caller's transparent background");
    }

    [TestMethod]
    public void OpacityMovesALiveIslandToTheSnapshotPath()
    {
        using var surface = new VisualHost { Size = new Size(100, 60) };
        var root = new VC.Canvas { Width = 100, Height = 60, Background = new SolidColorBrush(Color.Blue) };
        using var panel = new Panel { BackColor = Color.Red };
        var island = new VC.NativeElement(panel) { Width = 100, Height = 60 };
        root.Add(island);
        surface.Root = root;

        Layout(surface, 100, 60);
        Assert.IsTrue(surface.Controls.Contains(panel), "an opaque live island keeps its HWND");

        island.Opacity = 0.5;
        Layout(surface, 100, 60);

        Assert.IsFalse(surface.Controls.Contains(panel), "a half-opacity island cannot be a plain HWND");

        using var bitmap = new Bitmap(100, 60);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            surface.RenderTo(graphics, new Size(100, 60));
        }

        var pixel = bitmap.GetPixel(50, 30);
        Assert.IsTrue(
            Math.Abs(pixel.R - 127) < 40 && Math.Abs(pixel.B - 127) < 40,
            $"red over blue at half opacity (pixel={pixel})");

        island.Opacity = 1d;
        Layout(surface, 100, 60);

        Assert.IsTrue(surface.Controls.Contains(panel), "restoring the opacity returns to the live HWND");
    }

    [TestMethod]
    public void TransformMovesALiveIslandToTheSnapshotPath()
    {
        using var surface = new VisualHost { Size = new Size(120, 60) };
        var root = new VC.Canvas { Width = 120, Height = 60, Background = new SolidColorBrush(Color.Blue) };
        using var panel = new Panel { BackColor = Color.Red };
        var island = new VC.NativeElement(panel)
        {
            Width = 40,
            Height = 40,
            HorizontalAlignment = Easel.Controls.HorizontalAlignment.Left,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Top,
        };
        root.Add(island);
        surface.Root = root;
        Layout(surface, 120, 60);
        Assert.IsTrue(surface.Controls.Contains(panel));

        island.Transform = new Matrix(1f, 0f, 0f, 1f, 60f, 0f);
        Layout(surface, 120, 60);

        Assert.IsFalse(surface.Controls.Contains(panel), "an own transform cannot be expressed by a child HWND");

        using var bitmap = new Bitmap(120, 60);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            surface.RenderTo(graphics, new Size(120, 60));
        }

        var moved = bitmap.GetPixel(80, 20);
        var origin = bitmap.GetPixel(20, 20);
        Assert.IsTrue(moved.R > 150 && moved.B < 100, $"the transformed snapshot moved (pixel={moved})");
        Assert.IsTrue(origin.B > 150 && origin.R < 100, $"the old position shows the surface (pixel={origin})");
    }

    [TestMethod]
    public void RuntimeCornerRadiusUpdatesTheWindowRegion()
    {
        using var surface = new VisualHost { Size = new Size(200, 100) };
        _ = surface.Handle;
        using var panel = new Panel { Size = new Size(100, 40) };
        var island = new VC.NativeElement(panel) { Width = 100, Height = 40 };
        surface.Root = island;
        Layout(surface, 200, 100);

        island.CornerRadius = 10f;
        Layout(surface, 200, 100);

        Assert.IsTrue(panel.IsHandleCreated, "the island was placed");
        Assert.IsFalse(NativeWindowRegion.Contains(panel, new Point(1, 1)), "the rounded corner leaves the region");
        Assert.IsTrue(NativeWindowRegion.Contains(panel, new Point(50, 20)));

        island.CornerRadius = 0f;
        Layout(surface, 200, 100);

        Assert.IsFalse(
            NativeWindowRegion.Contains(panel, new Point(50, 20)),
            "removing the radius drops the window region entirely (GetWindowRgn reports none)");
    }

    [TestMethod]
    public void ASnapshotWithATransparentBackgroundDoesNotThrow()
    {
        using var form = new Form
        {
            Size = new Size(200, 120),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(60, 60),
        };
        using var surface = new VisualHost { Dock = DockStyle.Fill, DpiScaleOverride = 1f };
        form.Controls.Add(surface);
        using var label = new Label { Text = "transparent", BackColor = Color.Transparent };
        var island = new VC.NativeElement(label) { Width = 160, Height = 30, IsSnapshot = true };
        surface.Root = island;
        form.Show();
        form.Activate();
        Pump(6);

        Assert.IsNotNull(island.InputProxy);
        Assert.AreEqual(Color.Transparent, label.BackColor, "the control's own background stays untouched in snapshot mode");

        form.Close();
    }

    [TestMethod]
    public void APopupCutsAHoleInTheSnapshotInputProxy()
    {
        using var form = new Form
        {
            Size = new Size(300, 200),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(60, 60),
        };
        using var surface = new VisualHost { Dock = DockStyle.Fill, DpiScaleOverride = 1f };
        form.Controls.Add(surface);
        var root = new VC.Canvas { Background = new SolidColorBrush(CanvasColor) };
        using var panel = new Panel { BackColor = Color.Red };
        var island = new VC.NativeElement(panel)
        {
            Width = 200,
            Height = 120,
            IsSnapshot = true,
            CornerRadius = 12f,
        };
        VC.Canvas.SetLeft(island, 20);
        VC.Canvas.SetTop(island, 20);
        root.Add(island);
        surface.Root = root;
        form.Show();
        form.Activate();
        Pump(6);

        var proxy = island.InputProxy;
        Assert.IsNotNull(proxy, "the snapshot keeps an input proxy while a form exists");
        Assert.IsTrue(
            NativeWindowRegion.Contains(proxy!, new Point(120, 110)),
            "the proxy covers the island by default");

        var popup = new VC.VisualPopup
        {
            PlacementTarget = island,
            Placement = VC.VisualPlacement.Bottom,
            VerticalOffset = -40f,
            MatchTargetWidth = false,
            Child = new VC.Border { Width = 80, Height = 80, Background = new SolidColorBrush(Color.White) },
        };
        popup.Open();
        Pump(6);

        Assert.IsFalse(
            NativeWindowRegion.Contains(proxy!, new Point(40, 100)),
            "the popup rectangle is cut out of the proxy region so the popup can be clicked");
        Assert.IsTrue(
            NativeWindowRegion.Contains(proxy!, new Point(120, 40)),
            "the rest of the proxy keeps its region");
        Assert.IsFalse(
            NativeWindowRegion.Contains(proxy!.NativeControl, new Point(40, 100)),
            "the native control inside the proxy is cut the same way");

        popup.Close();
        Pump(6);
        Assert.IsTrue(
            NativeWindowRegion.Contains(proxy!, new Point(40, 100)),
            "closing the popup restores the proxy region");

        form.Close();
    }

    [TestMethod]
    public void ADialogHidesTheSnapshotInputProxy()
    {
        using var form = new Form
        {
            Size = new Size(400, 300),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(60, 60),
        };
        using var surface = new VisualHost { Dock = DockStyle.Fill, DpiScaleOverride = 1f };
        form.Controls.Add(surface);
        var root = new VC.Canvas { Background = new SolidColorBrush(CanvasColor) };
        using var panel = new Panel { BackColor = Color.Red };
        var island = new VC.NativeElement(panel) { Width = 200, Height = 120, IsSnapshot = true };
        VC.Canvas.SetLeft(island, 20);
        VC.Canvas.SetTop(island, 20);
        root.Add(island);
        surface.Root = root;
        form.Show();
        form.Activate();
        Pump(6);

        var proxy = island.InputProxy;
        Assert.IsNotNull(proxy);
        Assert.IsTrue(proxy!.Visible);

        using var dialog = new VisualDialog(new DialogOptions
        {
            Title = "Confirm",
            Message = "Hides the island",
            Animate = false,
        });
        var task = dialog.ShowAsync(surface);
        Pump(4);

        Assert.IsFalse(proxy.Visible, "the dialog scrim must cover the snapshot proxy too");

        dialog.Close(DialogResult.Cancel);
        Pump(4);
        _ = task;

        Assert.IsTrue(proxy.Visible, "closing the dialog brings the proxy back");

        form.Close();
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }

    private static void Pump(int frames)
    {
        for (var frame = 0; frame < frames; frame++)
        {
            Application.DoEvents();
            Thread.Sleep(40);
        }
    }
}
