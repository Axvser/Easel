using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;
using XamlThickness = Easel.Controls.Thickness;

namespace Easel.Test;

/// <summary>
/// Native controls as children of a visual element: <see cref="VisualElement.AddNative"/> creates the wrapper,
/// the host lays it out (panel or content box), the collection removes/reorders it, and the island keeps real input.
/// </summary>
[STATestClass]
public sealed class NativeChildrenTests
{
    [TestMethod]
    public void AddNativeFillsTheContentBoxOfANonLayoutHost()
    {
        using var surface = new VisualHost { Size = new Size(300, 200) };
        using var native = new Panel();
        var host = new VC.Border { Padding = new XamlThickness(8) };
        var island = host.AddNative(native);
        surface.Root = host;

        Layout(surface, 300, 200);

        Assert.AreSame(island, host.NativeChildren[0]);
        Assert.AreSame(native, island.Native);
        Assert.IsTrue(surface.Controls.Contains(native), "the island is a child HWND of the surface");
        Assert.AreEqual(new Rectangle(8, 8, 284, 184), native.Bounds, "the island fills the host's padded content box");
    }

    [TestMethod]
    public void AddNativeFollowsTheHostWhenItResizes()
    {
        using var surface = new VisualHost { Size = new Size(300, 200) };
        using var native = new Panel();
        var host = new VC.Border { Padding = new XamlThickness(6) };
        host.AddNative(native);
        surface.Root = host;

        Layout(surface, 300, 200);
        Assert.AreEqual(new Rectangle(6, 6, 288, 188), native.Bounds);

        Layout(surface, 420, 260);
        Assert.AreEqual(new Rectangle(6, 6, 408, 248), native.Bounds, "a content-box island follows a host resize");
    }

    [TestMethod]
    public void AddNativeOnAPanelParticipatesInThePanelLayout()
    {
        using var surface = new VisualHost { Size = new Size(300, 200) };
        using var first = new Panel();
        using var second = new Panel();
        var stack = new VC.StackPanel { Orientation = Orientation.Vertical };
        stack.AddNative(first);
        stack.AddNative(second);
        surface.Root = stack;

        Layout(surface, 300, 200);

        Assert.IsTrue(surface.Controls.Contains(first));
        Assert.IsTrue(surface.Controls.Contains(second));
        Assert.AreEqual(first.Bottom, second.Top, "the panel stacks the two islands");
        Assert.IsTrue(second.Top > 0, "the second island is not placed on top of the first");
    }

    [TestMethod]
    public void APanelLayoutIsNotOverriddenByTheContentBoxFallback()
    {
        using var surface = new VisualHost { Size = new Size(300, 200) };
        using var native = new Panel();
        var stack = new VC.StackPanel { Orientation = Orientation.Vertical };
        var island = stack.AddNative(native);
        island.Width = 50;
        island.HorizontalAlignment = Easel.Controls.HorizontalAlignment.Left;
        surface.Root = stack;

        Layout(surface, 300, 200);

        Assert.AreEqual(50, native.Width, "the panel's slot wins; the fallback must not stretch it to the host width");
    }

    [TestMethod]
    public void RemoveNativeDetachesTheWindowAndLeavesTheControlUsable()
    {
        using var surface = new VisualHost { Size = new Size(300, 200) };
        using var native = new Panel();
        var host = new VC.Border();
        host.AddNative(native);
        surface.Root = host;
        Layout(surface, 300, 200);
        Assert.IsTrue(surface.Controls.Contains(native));

        Assert.IsTrue(host.RemoveNative(native));

        Assert.IsFalse(surface.Controls.Contains(native), "the HWND leaves the surface");
        Assert.IsNull(native.Parent, "the control is unparented");
        Assert.IsFalse(native.IsDisposed, "removing does not dispose a control the caller still owns");
        Assert.AreEqual(0, host.NativeChildren.Count);
        Assert.IsFalse(host.RemoveNative(native), "removing twice reports false");
    }

    [TestMethod]
    public void AnOwnedNativeControlIsDisposedOnRemove()
    {
        using var surface = new VisualHost { Size = new Size(300, 200) };
        var native = new Panel();
        var host = new VC.Border();
        host.AddNative(native, ownsNative: true);
        surface.Root = host;
        Layout(surface, 300, 200);

        host.RemoveNative(native);

        Assert.IsTrue(native.IsDisposed);
    }

    [TestMethod]
    public void MovingANativeChildReordersTheNativeWindows()
    {
        using var surface = new VisualHost { Size = new Size(300, 200) };
        using var lower = new Panel();
        using var upper = new Panel();
        var host = new VC.Border();
        var lowerIsland = host.AddNative(lower);
        var upperIsland = host.AddNative(upper);
        surface.Root = host;
        Layout(surface, 300, 200);

        var lowerIndex = surface.Controls.GetChildIndex(lower);
        var upperIndex = surface.Controls.GetChildIndex(upper);
        Assert.IsTrue(upperIndex > lowerIndex, "the later child paints above");

        Assert.IsTrue(host.NativeChildren.Move(upperIsland, 0));

        Assert.AreSame(upperIsland, host.NativeChildren[0]);
        Assert.IsTrue(
            surface.Controls.GetChildIndex(upper) < surface.Controls.GetChildIndex(lower),
            "moving the child to the front also moves its HWND below the other island");
        Assert.IsTrue(host.NativeChildren.Move(lowerIsland, 1));
    }

    [TestMethod]
    public void ANativeChildInheritsTheHostsDataContext()
    {
        var context = new object();
        var host = new VC.ContentControl { DataContext = context };
        using var native = new Panel();
        var island = host.AddNative(native);

        Assert.AreSame(context, island.DataContext);
    }

    [TestMethod]
    public void DisposingTheHostDetachesTheNativeChild()
    {
        using var surface = new VisualHost { Size = new Size(300, 200) };
        using var native = new Panel();
        var host = new VC.Border();
        host.AddNative(native);
        surface.Root = host;
        Layout(surface, 300, 200);

        surface.Root = null;
        host.Dispose();

        Assert.IsFalse(surface.Controls.Contains(native));
        Assert.IsFalse(native.IsDisposed);
        Assert.IsNull(native.Parent);
    }

    [TestMethod]
    public void AddingTheSameControlTwiceIsRejected()
    {
        var host = new VC.Border();
        using var native = new Panel();
        host.AddNative(native);

        Assert.ThrowsExactly<InvalidOperationException>(() => host.AddNative(native));
    }

    [TestMethod]
    public void AFractionalIslandCoversWholePhysicalPixels()
    {
        using var surface = new VisualHost { Size = new Size(300, 300) };
        surface.DpiScaleOverride = 1.5f;
        using var native = new Panel();
        var root = new VC.Canvas { Width = 200, Height = 150 };
        var island = new VC.NativeElement(native)
        {
            Width = 100,
            Height = 40,
            HorizontalAlignment = Easel.Controls.HorizontalAlignment.Left,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Top,
        };
        VC.Canvas.SetLeft(island, 41.4);
        VC.Canvas.SetTop(island, 20.6);
        root.Add(island);
        surface.Root = root;

        Layout(surface, 200, 150);

        // 41.4*1.5 = 62.1 → 左边向下取整 62；(41.4+100)*1.5 = 212.1 → 右边向上取整 213。
        Assert.AreEqual(62, native.Left, "the island starts at the pixel under the logical edge");
        Assert.AreEqual(213, native.Right, "the island covers the last pixel the element paints");
        Assert.AreEqual(30, native.Top);
        Assert.AreEqual(91, native.Bottom);
    }

    [TestMethod]
    public void AChangeOfScaleReplacesTheIslandInPhysicalPixels()
    {
        using var surface = new VisualHost { Size = new Size(300, 300) };
        surface.DpiScaleOverride = 1f;
        using var native = new Panel();
        var root = new VC.Canvas { Width = 300, Height = 300 };
        var island = new VC.NativeElement(native)
        {
            Width = 100,
            Height = 40,
            HorizontalAlignment = Easel.Controls.HorizontalAlignment.Left,
            VerticalAlignment = Easel.Controls.VerticalAlignment.Top,
        };
        root.Add(island);
        surface.Root = root;

        Layout(surface, 300, 300);
        Assert.AreEqual(new Rectangle(0, 0, 100, 40), native.Bounds);

        surface.DpiScaleOverride = 1.5f;
        Layout(surface, 200, 200);

        Assert.AreEqual(new Rectangle(0, 0, 150, 60), native.Bounds, "a DPI change re-places the island at the new scale");
    }

    [TestMethod]
    public void ANativeChildReceivesRealPointerInput()
    {
        using var form = new Form
        {
            Size = new Size(320, 240),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(80, 80),
            TopMost = true,
        };
        using var surface = new VisualHost { Dock = DockStyle.Fill, DpiScaleOverride = 1f };
        form.Controls.Add(surface);
        using var root = new VC.Canvas { Width = 320, Height = 240 };
        var host = new VC.Border { Width = 200, Height = 80 };
        VC.Canvas.SetLeft(host, 40);
        VC.Canvas.SetTop(host, 40);
        root.Add(host);
        using var button = new ClickCountingButton { Text = "click" };
        host.AddNative(button);
        surface.Root = root;
        form.Show();
        SetForegroundWindow(form.Handle);
        form.Activate();
        Pump(form, 10);

        // 同批运行的其他有头测试可能抢走前台；点击落在坐标上，先确保本窗口在前台再点。
        for (var attempt = 0; attempt < 2 && button.Clicks == 0; attempt++)
        {
            SetForegroundWindow(form.Handle);
            form.Activate();
            Pump(form, 4);
            var screen = button.PointToScreen(new Point(button.Width / 2, button.Height / 2));
            SetCursorPos(screen.X, screen.Y);
            Application.DoEvents();
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            Pump(form, 4);
        }

        form.Close();
        Assert.AreEqual(1, button.Clicks, "a real click lands on the hosted native control");
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);

    private sealed class ClickCountingButton : Button
    {
        public ClickCountingButton()
        {
            Click += (_, _) => Clicks++;
        }

        public int Clicks { get; private set; }
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }

    private static void Pump(Form form, int frames)
    {
        for (var frame = 0; frame < frames; frame++)
        {
            Application.DoEvents();
            Thread.Sleep(40);
        }
    }
}
