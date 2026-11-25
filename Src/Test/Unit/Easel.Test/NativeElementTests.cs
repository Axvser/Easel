using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Easel;
using Easel.Test.Fixtures;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;
using XamlHorizontalAlignment = Easel.Controls.HorizontalAlignment;
using XamlThickness = Easel.Controls.Thickness;
using XamlVisibility = Easel.Controls.Visibility;

namespace Easel.Test;

/// <summary>
/// Native controls as layout citizens: preferred-size measurement, re-measure on native changes, placement in the
/// same panel pass, island z-order, and visual content hosted inside a native container.
/// </summary>
[STATestClass]
public sealed class NativeElementTests
{
    [TestMethod]
    public void ANativeElementParticipatesInStackPanelLayout()
    {
        using var surface = CreateSurface(300, 200);
        var root = new VC.StackPanel { Orientation = Orientation.Vertical };
        using var button = new Button { Text = "OK" };
        var native = new VC.NativeElement(button)
        {
            Width = 120,
            Margin = new XamlThickness(4),
            HorizontalAlignment = XamlHorizontalAlignment.Left,
        };
        root.Add(native);
        surface.Root = root;

        Layout(surface, 300, 200);

        Assert.AreEqual(4, button.Left, "the wrapper's margin reaches the native window");
        Assert.AreEqual(4, button.Top);
        Assert.AreEqual(120, button.Width, "the wrapper's explicit width is the native window width");
        Assert.IsTrue(button.Height > 0);
        Assert.IsTrue(surface.Controls.Contains(button));
    }

    [TestMethod]
    public void AutoSizeNativeElementFollowsItsPreferredSize()
    {
        using var surface = CreateSurface(400, 100);
        var root = new VC.StackPanel { Orientation = Orientation.Vertical };
        using var label = new Label { AutoSize = true, Text = "short" };
        var native = new VC.NativeElement(label);
        root.Add(native);
        surface.Root = root;

        Layout(surface, 400, 100);
        var narrow = label.Width;

        label.Text = "a noticeably longer native caption that must re-measure";
        Layout(surface, 400, 100);

        Assert.IsTrue(label.Width > narrow, "a text change on the native control must invalidate the wrapper's measure");
    }

    [TestMethod]
    public void ANativeContainerHostsVisualContentInANestedSurface()
    {
        using var surface = CreateSurface(300, 200);
        using var panel = new Panel { Padding = new Padding(6) };
        var native = new VC.NativeElement(panel) { Width = 180, Height = 90 };
        var content = new VC.StackPanel { Orientation = Orientation.Vertical };
        content.Add(new VC.TextBlock { Text = "inside", Height = 18 });
        native.Content = content;
        surface.Root = native;

        Layout(surface, 300, 200);

        Assert.IsNotNull(native.ContentSurface);
        Assert.AreSame(panel, native.ContentSurface!.Parent, "the nested surface is a child of the native control");
        Assert.AreEqual(panel.DisplayRectangle, native.ContentSurface.Bounds);

        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        native.ContentSurface.RenderTo(
            graphics,
            new Size(Math.Max(1, panel.DisplayRectangle.Width), Math.Max(1, panel.DisplayRectangle.Height)));

        Assert.AreEqual(0, content.Bounds.X, 0.1f, "the content fills the nested surface from its origin");
        Assert.AreEqual(0, content.Bounds.Y, 0.1f);
        Assert.AreEqual(panel.DisplayRectangle.Width, content.Bounds.Width, 1f, "the nested surface covers the display rectangle");
        Assert.AreEqual(panel.DisplayRectangle.Height, content.Bounds.Height, 1f);
    }

    [TestMethod]
    public void NativeContentInheritsTheWrappersDataContext()
    {
        using var surface = CreateSurface(300, 200);
        using var panel = new Panel();
        var native = new VC.NativeElement(panel) { Width = 180, Height = 90 };
        var content = new VC.TextBlock { Text = "text" };
        native.Content = content;
        surface.Root = native;
        var context = new object();
        native.DataContext = context;

        Layout(surface, 300, 200);

        Assert.AreSame(context, content.DataContext, "visual content resolves the wrapper's context through the nested root");
    }

    [TestMethod]
    public void NativeContentSeesTheWrappersResources()
    {
        using var surface = CreateSurface(300, 200);
        using var panel = new Panel();
        var native = new VC.NativeElement(panel) { Width = 180, Height = 90 };
        var content = new VC.TextBlock { Text = "text" };
        native.Content = content;
        surface.Root = native;
        var brush = new SolidColorBrush(Color.Red);
        native.Resources["Accent"] = brush;

        Layout(surface, 300, 200);

        Assert.IsNotNull(native.ContentSurface);
        Assert.IsTrue(
            native.ContentSurface!.Resources.MergedDictionaries.Contains(native.Resources),
            "the nested surface merges the element's resource chain");
    }

    [TestMethod]
    public void ZIndexOrdersNativeIslands()
    {
        using var surface = CreateSurface(200, 200);
        var root = new VC.Canvas { Width = 200, Height = 200 };
        using var lower = new Label { Text = "lower" };
        using var upper = new Label { Text = "upper" };
        var first = new VC.NativeElement(lower) { Width = 80, Height = 30 };
        var second = new VC.NativeElement(upper) { Width = 80, Height = 30 };
        Easel.Visuals.Controls.Panel.SetZIndex(first, 2);
        Easel.Visuals.Controls.Panel.SetZIndex(second, 1);
        root.Add(first);
        root.Add(second);
        surface.Root = root;

        Layout(surface, 200, 200);

        var lowerIndex = surface.Controls.GetChildIndex(second.Native!);
        var upperIndex = surface.Controls.GetChildIndex(first.Native!);
        Assert.IsTrue(upperIndex > lowerIndex, "Panel.ZIndex orders the native windows among themselves");
    }

    [TestMethod]
    public void CollapsingTheWrapperHidesTheContentSurface()
    {
        using var surface = CreateSurface(200, 150);
        using var panel = new Panel();
        var native = new VC.NativeElement(panel) { Width = 100, Height = 60 };
        native.Content = new VC.TextBlock { Text = "inside" };
        surface.Root = native;

        Layout(surface, 200, 150);
        Assert.IsTrue(native.ContentSurface!.Visible);

        native.Visibility = XamlVisibility.Collapsed;
        Layout(surface, 200, 150);

        Assert.IsFalse(native.ContentSurface.Visible, "a collapsed native island must also hide its nested surface");
    }

    [TestMethod]
    public void ANativeElementIsPositionedInPhysicalPixels()
    {
        using var surface = new VisualHost { Size = new Size(300, 300) };
        surface.DpiScaleOverride = 1.5f;
        using var panel = new Panel();
        var native = new VC.NativeElement(panel)
        {
            Width = 100,
            Height = 60,
            HorizontalAlignment = XamlHorizontalAlignment.Left,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Top,
        };
        surface.Root = native;

        Layout(surface, 200, 200);

        Assert.AreEqual(
            new Rectangle(0, 0, 150, 90),
            panel.Bounds,
            "a logical 100x60 element becomes a 150x90 native window at 1.5x");
    }

    [TestMethod]
    public void NativeContentSeesTheWrappersImplicitStyles()
    {
        using var surface = CreateSurface(300, 200);
        using var panel = new Panel();
        var native = new VC.NativeElement(panel) { Width = 180, Height = 90 };
        var content = new VC.TextBlock { Text = "styled" };
        native.Content = content;
        surface.Root = native;

        native.Styles.Add(new VisualStyle
        {
            TargetType = typeof(VC.TextBlock),
            Setters =
            {
                new VisualSetter(VisualElement.FontSizeProperty, 21d),
            },
        });

        Layout(surface, 300, 200);

        Assert.AreEqual(21d, content.FontSize, "the nested surface mirrors the element's implicit styles");
    }

    [TestMethod]
    public void HidingAnAncestorHidesTheNativeWindow()
    {
        using var surface = CreateSurface(200, 150);
        var root = new VC.Canvas { Width = 200, Height = 150 };
        using var label = new Label { Text = "native" };
        var native = new VC.NativeElement(label) { Width = 80, Height = 30 };
        root.Add(native);
        surface.Root = root;
        Layout(surface, 200, 150);
        Assert.IsTrue(label.Visible);

        root.Visibility = XamlVisibility.Hidden;
        Layout(surface, 200, 150);

        Assert.IsFalse(label.Visible, "a hidden ancestor must hide the native island");
    }

    [TestMethod]
    public void PartiallyClippedNativeIslandKeepsItsArrangeSize()
    {
        using var surface = CreateSurface(200, 150);
        var root = new VC.Canvas { Width = 200, Height = 150 };
        var clip = new VC.Canvas { Width = 100, Height = 150, ClipToBounds = true };
        using var label = new Label { Text = "native" };
        var native = new VC.NativeElement(label) { Width = 120, Height = 30 };
        VC.Canvas.SetLeft(native, 40);
        VC.Canvas.SetTop(native, 20);
        clip.Add(native);
        root.Add(clip);
        surface.Root = root;

        Layout(surface, 200, 150);

        Assert.AreEqual(120, label.Width, "the island keeps its arranged width; the ancestor clip is a window region");
        Assert.AreEqual(40, label.Left);
    }

    [TestMethod]
    public void NativeRegionClipsToTheVisibleIntersection()
    {
        using var panel = new Panel { Size = new Size(120, 30) };
        _ = panel.Handle;

        NativeWindowRegion.Apply(panel, 0d, [], new Rectangle(0, 0, 60, 30));

        Assert.IsTrue(NativeWindowRegion.Contains(panel, new Point(30, 10)), "the visible part stays in the region");
        Assert.IsFalse(NativeWindowRegion.Contains(panel, new Point(90, 10)), "the clipped part leaves the region");

        NativeWindowRegion.Restore(panel);
        Assert.IsFalse(NativeWindowRegion.Contains(panel, new Point(30, 10)), "restoring drops the region");
    }

    [TestMethod]
    public void NativeIslandMovesWhenTheFrameIsPresented()
    {
        using var form = new Form
        {
            Size = new Size(320, 220),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80),
        };
        using var surface = new VisualHost { Dock = DockStyle.Fill };
        form.Controls.Add(surface);
        var root = new VC.Canvas { Width = 320, Height = 220 };
        using var label = new Label { Text = "native" };
        var native = new VC.NativeElement(label) { Width = 80, Height = 30 };
        VC.Canvas.SetLeft(native, 20);
        VC.Canvas.SetTop(native, 20);
        root.Add(native);
        surface.Root = root;
        form.Show();
        form.Activate();
        Application.DoEvents();
        Thread.Sleep(60);
        Application.DoEvents();

        VC.Canvas.SetLeft(native, 160);
        var atPaint = Rectangle.Empty;
        surface.Paint += (_, _) => atPaint = label.Bounds;
        surface.Invalidate();
        surface.Update();

        var oldX = (int)Math.Floor(20f * surface.DpiScale);
        var newX = (int)Math.Floor(160f * surface.DpiScale);
        Assert.IsTrue(atPaint.Width > 0, "the surface painted");
        Assert.AreEqual(oldX, atPaint.X, "the island stays on the old frame while the parent paints");
        Assert.AreEqual(newX, label.Bounds.X, "the island moves before the frame reaches the screen");
        form.Close();
    }

    [TestMethod]
    public void AGestureFreezesNativeIslandsAndReleaseRestoresThem()
    {
        using var form = new Form
        {
            Size = new Size(320, 220),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80),
        };
        using var surface = new VisualHost { Dock = DockStyle.Fill };
        form.Controls.Add(surface);
        var root = new VC.Canvas { Width = 320, Height = 220 };
        using var label = new Label { Text = "native" };
        var native = new VC.NativeElement(label) { Width = 80, Height = 30 };
        root.Add(native);
        surface.Root = root;
        form.Show();
        form.Activate();
        Application.DoEvents();
        Thread.Sleep(60);
        Application.DoEvents();

        surface.ProcessPointerDown(new PointF(200f, 150f), MouseButtons.Left);

        Assert.IsTrue(native.IsFrozen, "the island freezes for the gesture");
        Assert.IsFalse(label.Visible, "the live window hides while frozen");

        surface.ProcessPointerUp(new PointF(200f, 150f), MouseButtons.Left);

        Assert.IsFalse(native.IsFrozen, "release restores the live window");
        Assert.IsTrue(label.Visible);
        form.Close();
    }

    [TestMethod]
    public void ANativePropertyBindingWritesBothDirections()
    {
        using var surface = CreateSurface(300, 200);
        using var textBox = new TextBox();
        var native = new VC.NativeElement(textBox) { Width = 150, Height = 30 };
        surface.Root = native;

        var source = new BindingSource { Name = "first" };
        native.DataContext = source;
        var property = VC.NativePropertyBinding.For(typeof(TextBox), nameof(TextBox.Text));
        VisualBindingOperations.Bind(
            native,
            property,
            new Easel.Binding(nameof(BindingSource.Name)) { Mode = BindingMode.TwoWay });
        VC.NativePropertyBinding.TrackTwoWay(native, property, nameof(TextBox.Text));

        Layout(surface, 300, 200);
        Assert.AreEqual("first", textBox.Text, "the source pushes into the native control");

        textBox.Text = "second";
        Assert.AreEqual("second", source.Name, "the native change writes the source");

        source.Name = "third";
        Assert.AreEqual("third", textBox.Text, "the next source change reaches the native control again");
    }

    [TestMethod]
    public void ALostFocusNativeBindingFlushesWhenTheControlLeaves()
    {
        using var surface = CreateSurface(300, 200);
        using var textBox = new LeaveAwareTextBox();
        var native = new VC.NativeElement(textBox) { Width = 150, Height = 30 };
        surface.Root = native;

        var source = new BindingSource { Name = "initial" };
        native.DataContext = source;
        var property = VC.NativePropertyBinding.For(typeof(LeaveAwareTextBox), nameof(TextBox.Text));
        VisualBindingOperations.Bind(
            native,
            property,
            new Easel.Binding(nameof(BindingSource.Name))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
            });
        VC.NativePropertyBinding.TrackTwoWay(native, property, nameof(TextBox.Text));

        Layout(surface, 300, 200);
        Assert.AreEqual("initial", textBox.Text);

        textBox.Text = "typed";
        Assert.AreEqual("initial", source.Name, "LostFocus defers the source write");

        textBox.RaiseLeave();
        Assert.AreEqual("typed", source.Name, "the native Leave event flushes the pending update");
    }

    [TestMethod]
    public void ANativePropertyWithoutAChangeEventFallsBackToPolling()
    {
        using var surface = CreateSurface(300, 200);
        using var control = new EventlessControl();
        var native = new VC.NativeElement(control) { Width = 150, Height = 30 };
        surface.Root = native;

        var source = new BindingSource { Name = "initial" };
        native.DataContext = source;
        var property = VC.NativePropertyBinding.For(typeof(EventlessControl), nameof(EventlessControl.Caption));
        VisualBindingOperations.Bind(
            native,
            property,
            new Easel.Binding(nameof(BindingSource.Name)) { Mode = BindingMode.TwoWay });
        VC.NativePropertyBinding.TrackTwoWay(native, property, nameof(EventlessControl.Caption));

        Layout(surface, 300, 200);
        Assert.AreEqual("initial", control.Caption);

        control.Caption = "edited";
        VC.NativePropertyBinding.PollAll();
        Assert.AreEqual("edited", source.Name, "polling covers properties with neither INotifyPropertyChanged nor a Changed event");
    }

    [TestMethod]
    public void APopupCutsTheNativeWindowRegion()
    {
        using var surface = new VisualHost { Size = new Size(300, 200) };
        _ = surface.Handle;
        var root = new VC.Canvas { Width = 300, Height = 200 };
        using var button = new Button { Text = "native" };
        var native = new VC.NativeElement(button) { Width = 100, Height = 40 };
        VC.Canvas.SetLeft(native, 20);
        VC.Canvas.SetTop(native, 20);
        root.Add(native);
        surface.Root = root;
        Layout(surface, 300, 200);
        Assert.IsTrue(button.IsHandleCreated);

        var popup = new VC.VisualPopup
        {
            PlacementTarget = native,
            Placement = VC.VisualPlacement.Bottom,
            VerticalOffset = -30f,
            MatchTargetWidth = false,
            Child = new VC.Border
            {
                Width = 60,
                Height = 100,
                Background = new SolidColorBrush(Color.White),
            },
        };
        popup.Open();
        Layout(surface, 300, 200);

        Assert.IsFalse(
            NativeWindowRegion.Contains(button, new Point(30, 20)),
            "the popup rectangle is cut out of the island's window region");
        Assert.IsTrue(
            NativeWindowRegion.Contains(button, new Point(90, 20)),
            $"the rest of the island keeps its region (child={button.Bounds}, popup={popup.Bounds})");
    }

    [TestMethod]
    public void ACornerRadiusRoundsTheNativeWindowRegion()
    {
        using var surface = new VisualHost { Size = new Size(200, 100) };
        _ = surface.Handle;
        using var button = new Button { Text = "native" };
        var native = new VC.NativeElement(button) { Width = 100, Height = 40, CornerRadius = 10f };
        surface.Root = native;
        Layout(surface, 200, 100);

        Assert.IsTrue(button.IsHandleCreated);
        Assert.IsFalse(
            NativeWindowRegion.Contains(button, new Point(1, 1)),
            "the rounded corner is outside the window region");
        Assert.IsTrue(
            NativeWindowRegion.Contains(button, new Point(50, 20)),
            "the centre stays inside the window region");
    }

    [TestMethod]
    public void SnapshotModeHidesTheWindowAndPaintsTheBitmap()
    {
        using var surface = CreateSurface(100, 60);
        surface.BackColor = Color.Blue;
        using var panel = new Panel { BackColor = Color.Red };
        var native = new VC.NativeElement(panel) { Width = 100, Height = 60, IsSnapshot = true };
        surface.Root = native;
        Layout(surface, 100, 60);

        Assert.IsFalse(surface.Controls.Contains(panel), "the HWND is detached in snapshot mode");

        using var bitmap = new Bitmap(100, 60);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            surface.RenderTo(graphics, new Size(100, 60));
        }

        var pixel = bitmap.GetPixel(50, 30);
        Assert.IsTrue(pixel.R > 150 && pixel.B < 100, $"the snapshot paints the control (pixel={pixel})");
    }

    [TestMethod]
    public void SnapshotModeClipsToTheCornerRadius()
    {
        using var surface = CreateSurface(120, 80);
        var root = new VC.Canvas
        {
            Width = 120,
            Height = 80,
            Background = new SolidColorBrush(Color.Blue),
        };
        using var panel = new Panel { BackColor = Color.Red };
        var native = new VC.NativeElement(panel)
        {
            Width = 100,
            Height = 60,
            IsSnapshot = true,
            CornerRadius = 10f,
            BorderBrush = new SolidColorBrush(Color.Lime),
            BorderThickness = new Easel.Controls.Thickness(1),
            HorizontalAlignment = XamlHorizontalAlignment.Left,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Top,
        };
        root.Add(native);
        surface.Root = root;
        Layout(surface, 120, 80);

        using var bitmap = new Bitmap(120, 80);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            surface.RenderTo(graphics, new Size(120, 80));
        }

        var corner = bitmap.GetPixel(2, 2);
        var center = bitmap.GetPixel(50, 30);
        var edge = bitmap.GetPixel(50, 0);
        Assert.IsTrue(corner.B > 150 && corner.R < 100, $"the rounded corner shows the surface (pixel={corner})");
        Assert.IsTrue(center.R > 150 && center.B < 100, $"the snapshot fills the center (pixel={center})");
        Assert.IsTrue(edge.G > 150 && edge.R < 100, $"the visual border follows the edge (pixel={edge})");
    }

    [TestMethod]
    public void SnapshotModeAppliesOpacity()
    {
        using var surface = CreateSurface(100, 60);
        var root = new VC.Canvas
        {
            Width = 100,
            Height = 60,
            Background = new SolidColorBrush(Color.Blue),
        };
        using var panel = new Panel { BackColor = Color.Red };
        var native = new VC.NativeElement(panel)
        {
            Width = 100,
            Height = 60,
            IsSnapshot = true,
            Opacity = 0.5,
        };
        root.Add(native);
        surface.Root = root;
        Layout(surface, 100, 60);

        using var bitmap = new Bitmap(100, 60);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            surface.RenderTo(graphics, new Size(100, 60));
        }

        var pixel = bitmap.GetPixel(50, 30);
        Assert.IsTrue(
            Math.Abs(pixel.R - 127) < 40 && Math.Abs(pixel.B - 127) < 40,
            $"red over blue at half opacity (pixel={pixel})");
    }

    [TestMethod]
    public void SnapshotModeAppliesTransform()
    {
        using var surface = CreateSurface(120, 60);
        var root = new VC.Canvas
        {
            Width = 120,
            Height = 60,
            Background = new SolidColorBrush(Color.Blue),
        };
        using var panel = new Panel { BackColor = Color.Red };
        var native = new VC.NativeElement(panel)
        {
            Width = 40,
            Height = 40,
            IsSnapshot = true,
            HorizontalAlignment = XamlHorizontalAlignment.Left,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Top,
            Transform = new System.Drawing.Drawing2D.Matrix(1f, 0f, 0f, 1f, 60f, 0f),
        };
        root.Add(native);
        surface.Root = root;
        Layout(surface, 120, 60);

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

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);

    [TestMethod]
    public void ASnapshotWithARotationReceivesRealPointerInput()
    {
        using var form = new Form
        {
            Size = new Size(320, 240),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80),
        };
        using var host = new VisualHost { Dock = DockStyle.Fill };
        form.Controls.Add(host);
        var root = new VC.Canvas { Width = 320, Height = 240 };
        using var button = new ClickCountingButton { Text = "rotated" };
        var native = new VC.NativeElement(button)
        {
            Width = 100,
            Height = 40,
            IsSnapshot = true,
            HorizontalAlignment = XamlHorizontalAlignment.Left,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Top,
            Transform = new System.Drawing.Drawing2D.Matrix(
                (float)Math.Cos(Math.PI / 6), (float)Math.Sin(Math.PI / 6),
                -(float)Math.Sin(Math.PI / 6), (float)Math.Cos(Math.PI / 6),
                120f, 100f),
        };
        root.Add(native);
        host.Root = root;
        form.Show();
        form.Activate();
        Application.DoEvents();
        Thread.Sleep(80);
        Application.DoEvents();

        ClickAt(host, native, new PointF(50f, 20f), () => button.Clicks);

        form.Close();
        Assert.AreEqual(1, button.Clicks, "the rotated snapshot proxy forwards the real click");
    }

    [TestMethod]
    public void ASnapshotWithAScaleReceivesRealPointerInput()
    {
        using var form = new Form
        {
            Size = new Size(320, 240),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80),
        };
        using var host = new VisualHost { Dock = DockStyle.Fill };
        form.Controls.Add(host);
        var root = new VC.Canvas { Width = 320, Height = 240 };
        using var button = new ClickCountingButton { Text = "scaled" };
        var native = new VC.NativeElement(button)
        {
            Width = 100,
            Height = 40,
            IsSnapshot = true,
            HorizontalAlignment = XamlHorizontalAlignment.Left,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Top,
            Transform = new System.Drawing.Drawing2D.Matrix(1.5f, 0f, 0f, 1.5f, 20f, 20f),
        };
        root.Add(native);
        host.Root = root;
        form.Show();
        form.Activate();
        Application.DoEvents();
        Thread.Sleep(80);
        Application.DoEvents();

        ClickAt(host, native, new PointF(50f, 20f), () => button.Clicks);

        form.Close();
        Assert.AreEqual(1, button.Clicks, "the scaled snapshot proxy maps the real click back to the control");
    }

    private static void ClickAt(VisualHost host, VC.NativeElement element, PointF local, Func<int>? successfulClicks = null)
    {
        var scale = host.DpiScale > 0f ? host.DpiScale : 1f;
        var surface = element.PointToSurface(local);
        var physical = new Point(
            (int)Math.Round(surface.X * scale),
            (int)Math.Round(surface.Y * scale));
        var screen = host.PointToScreen(physical);
        var proxy = element.InputProxy;
        Assert.IsNotNull(proxy, "the snapshot input proxy exists");
        Assert.IsTrue(proxy.Visible, $"the proxy is visible (proxy={proxy.Bounds}, screen={screen})");
        Assert.IsTrue(proxy.Bounds.Contains(screen), $"the proxy covers the click (proxy={proxy.Bounds}, screen={screen})");
        var control = proxy.NativeControl;
        var controlScreen = control.RectangleToScreen(control.ClientRectangle);
        Assert.IsTrue(controlScreen.Contains(screen), $"the native control covers the click (control={controlScreen}, screen={screen}, client={control.ClientRectangle})");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            proxy.Activate();
            Application.DoEvents();
            Thread.Sleep(30);
            SetCursorPos(screen.X, screen.Y);
            Application.DoEvents();
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            Application.DoEvents();
            Thread.Sleep(80);
            Application.DoEvents();
            if (successfulClicks is null || successfulClicks() > 0)
            {
                return;
            }
        }
    }

    private sealed class ClickCountingButton : Button
    {
        public ClickCountingButton()
        {
            Click += (_, _) => Clicks++;
        }

        public int Clicks { get; private set; }
    }

    [TestMethod]
    public void TheNativeButtonInTheFixtureRaisesItsHandler()
    {
        using var view = new NativeFormsView();

        view.OkButton.Native.PerformClick();

        Assert.AreEqual(1, view.OkClicks);
    }

    [TestMethod]
    public void TheFixtureNestsAVisualPanelInsideANativeGroupBox()
    {
        using var surface = CreateSurface(300, 300);
        using var view = new NativeFormsView();
        surface.Root = view;

        Layout(surface, 300, 300);

        Assert.IsNotNull(view.Group.ContentSurface);
        Assert.AreSame(view.Group.Native, view.Group.ContentSurface!.Parent);
        Assert.AreSame(view.GroupContent, view.Group.ContentSurface.Root is VC.ContentControl root ? root.Content : null);
        Assert.IsInstanceOfType(view.GroupContent.Children[1], typeof(VC.NativeElement<Label>));
    }

    private sealed class BindingSource : INotifyPropertyChanged
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class LeaveAwareTextBox : TextBox
    {
        public void RaiseLeave() => OnLeave(EventArgs.Empty);
    }

    private sealed class EventlessControl : Control
    {
        public string Caption { get; set; } = string.Empty;
    }

    private static VisualHost CreateSurface(int width, int height)
        => new() { Size = new Size(width, height) };

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
