using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Easel.Test.Infrastructure;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>The self-drawn title bar and the message-based window chrome behind it.</summary>
[STATestClass]
public sealed class WindowChromeTests
{
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_NCLBUTTONDBLCLK = 0x00A3;
    private const int HTCAPTION = 2;
    private const int HTCLIENT = 1;

    private static readonly Color CloseHover = Color.FromArgb(0xE8, 0x11, 0x23);

    [TestMethod]
    public void DefaultTitleBarPaintsBackgroundBorderIconTitleAndGlyphs()
    {
        var previous = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;
            using var host = new VisualHost { Size = new Size(460, 32), DpiScaleOverride = 1f };
            using var bar = new VC.VisualTitleBar { Title = "Easel", Width = 460, Height = 32 };
            host.Root = bar;

            using var iconBitmap = new Bitmap(16, 16);
            using (var graphics = Graphics.FromImage(iconBitmap))
            {
                graphics.Clear(Color.FromArgb(255, 220, 30, 30));
            }

            using var icon = new VC.Image { Source = iconBitmap, Width = 16, Height = 16 };
            bar.Icon = icon;

            using var frame = Render(host);
            var theme = Theme.Current;

            AssertColor(theme.WindowBackground, frame.GetPixel(200, 4), "the bar paints the window background");
            AssertColor(theme.ControlBorder, frame.GetPixel(200, 31), "a one-pixel bottom border closes the bar");
            AssertColor(Color.FromArgb(255, 220, 30, 30), frame.GetPixel(20, 16), "the icon slot shows the assigned logo");

            var titlePixels = CountDifferent(frame, new Rectangle(36, 4, 260, 24), theme.WindowBackground);
            Assert.IsTrue(titlePixels > 12, $"the title text is drawn (pixels={titlePixels})");

            Assert.IsTrue(CountDifferent(frame, MinimizeGlyphArea(460), theme.WindowBackground) > 3,
                "the minimize glyph is drawn");
            Assert.IsTrue(CountDifferent(frame, MaximizeGlyphArea(460), theme.WindowBackground) > 3,
                "the maximize glyph is drawn");
            Assert.IsTrue(CountDifferent(frame, CloseGlyphArea(460), theme.WindowBackground) > 3,
                "the close glyph is drawn");
        }
        finally
        {
            Theme.Mode = previous;
        }
    }

    [TestMethod]
    public void MaximizedTitleBarDrawsTheRestoreGlyph()
    {
        var previous = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;
            using var host = new VisualHost { Size = new Size(460, 32), DpiScaleOverride = 1f };
            using var bar = new VC.VisualTitleBar { Title = "Easel", Width = 460, Height = 32 };
            host.Root = bar;

            using var normal = Render(host);
            bar.SetMaximizedState(true);
            using var maximized = Render(host);

            var area = MaximizeGlyphArea(460);
            var changed = CountPixelDiff(normal, maximized, area);
            Assert.IsTrue(changed > 8, $"maximizing swaps the square for the two overlapping restore squares (changed={changed})");
            Assert.IsTrue(CountDifferent(maximized, area, Theme.Current.WindowBackground) > 6,
                "the restore glyph still has visible strokes");
        }
        finally
        {
            Theme.Mode = previous;
        }
    }

    [TestMethod]
    public void CloseButtonTurnsRedUnderThePointer()
    {
        var previous = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;
            using var host = new VisualHost { Size = new Size(460, 32), DpiScaleOverride = 1f };
            using var bar = new VC.VisualTitleBar { Title = "Easel", Width = 460, Height = 32 };
            host.Root = bar;
            using (Render(host))
            {
            }

            var closeArea = CloseGlyphArea(460);
            host.ProcessPointerMove(new PointF(closeArea.Left + (closeArea.Width / 2f), 16f));

            using var frame = Render(host);
            AssertColor(CloseHover, frame.GetPixel(closeArea.Left + 4, 4), "the close button paints its red hover state");
            var crossing = frame.GetPixel(closeArea.Left + (closeArea.Width / 2), 16);
            Assert.IsTrue(crossing.R > 190 && crossing.G > 190 && crossing.B > 190,
                $"the close glyph turns white on red (pixel={crossing})");
        }
        finally
        {
            Theme.Mode = previous;
        }
    }

    [TestMethod]
    public void AutoAttachTurnsTheHostingFormBorderless()
    {
        using var form = CreateForm(out var host, out var bar);
        try
        {
            form.Show();
            XamlTestSurface.PumpUntil(() => form.IsHandleCreated && bar.Target == form);
            XamlTestSurface.Pump(60);

            Assert.AreEqual(FormBorderStyle.None, form.FormBorderStyle, "auto-attach removes the native border");
            Assert.IsTrue(WindowChrome.IsAttached(form), "the form has a chrome session");
            Assert.AreSame(bar, WindowChrome.GetTitleBar(form), "the title bar routes the form");
            Assert.AreEqual(FormWindowState.Normal, form.WindowState);
        }
        finally
        {
            form.Close();
        }
    }

    [TestMethod]
    public void AutoAttachCanBeOptedOut()
    {
        using var form = CreateForm(out _, out var bar, autoAttach: false);
        var original = form.FormBorderStyle;
        try
        {
            form.Show();
            XamlTestSurface.Pump(80);
            Assert.AreEqual(original, form.FormBorderStyle, "opting out keeps the caller's border style");
            Assert.IsFalse(WindowChrome.IsAttached(form), "no chrome session was installed");
        }
        finally
        {
            form.Close();
        }
    }

    [TestMethod]
    public void ExplicitAttachAndDetachRestoreTheBorderStyle()
    {
        using var form = CreateForm(out _, out var bar, autoAttach: false);
        var original = form.FormBorderStyle;
        WindowChrome.Attach(form, bar);
        try
        {
            form.Show();
            XamlTestSurface.PumpUntil(() => WindowChrome.IsAttached(form) && form.IsHandleCreated);
            XamlTestSurface.Pump(60);
            Assert.AreEqual(FormBorderStyle.None, form.FormBorderStyle, "explicit attach goes borderless");
            Assert.AreSame(bar, WindowChrome.GetTitleBar(form));
        }
        finally
        {
            WindowChrome.Detach(form);
        }

        Assert.IsFalse(WindowChrome.IsAttached(form), "detach removes the session");
        Assert.AreEqual(original, form.FormBorderStyle, "detach restores the caller's border style");
        WindowChrome.Detach(form);
        form.Close();
    }

    [TestMethod]
    public void HitTestRoutesTheTitleAreaCaptionButtonsAndResizeEdges()
    {
        using var form = CreateForm(out var host, out var bar);
        try
        {
            form.Show();
            XamlTestSurface.PumpUntil(() => WindowChrome.IsAttached(form) && host.IsHandleCreated);
            XamlTestSurface.Pump(80);

            var logicalWidth = host.LogicalClientSize.Width;

            Assert.AreEqual(HTCAPTION, HitTest(form, host, 160f, 16f), "empty title area is the caption");
            Assert.AreEqual(HTCLIENT, HitTest(form, host, bar.AbsoluteBounds.Width - 23f, 16f),
                "the close button is a client area");
            Assert.AreEqual(10, HitTest(form, host, 3f, 160f), "the left edge resizes");
            Assert.AreEqual(14, HitTest(form, host, logicalWidth - 3f, 3f), "the top-right corner resizes");

            form.WindowState = FormWindowState.Maximized;
            XamlTestSurface.PumpUntil(() => form.WindowState == FormWindowState.Maximized);
            XamlTestSurface.Pump(80);
            Assert.AreEqual(HTCLIENT, HitTest(form, host, 3f, 160f),
                "a maximized window has no resize edge");

            var work = Screen.FromControl(form).WorkingArea;
            Assert.IsTrue(form.Bounds.Top >= work.Top && form.Bounds.Bottom <= work.Bottom + 1,
                $"a maximized borderless window stays inside the work area (bounds={form.Bounds}, work={work})");
        }
        finally
        {
            form.Close();
        }
    }

    [TestMethod]
    public void CaptionButtonsDriveWindowStateAndClose()
    {
        using var form = CreateForm(out var host, out var bar);
        var closing = false;
        FormClosingEventHandler? cancelClosing = (_, e) =>
        {
            closing = true;
            e.Cancel = true;
        };
        form.FormClosing += cancelClosing;
        try
        {
            form.Show();
            XamlTestSurface.PumpUntil(() => WindowChrome.IsAttached(form) && host.IsHandleCreated);
            XamlTestSurface.Pump(80);

            var width = bar.AbsoluteBounds.Width;

            ClickAt(host, width - 115f, 16f);
            XamlTestSurface.PumpUntil(() => form.WindowState == FormWindowState.Minimized);
            Assert.AreEqual(FormWindowState.Minimized, form.WindowState, "the minimize button minimizes");
            form.WindowState = FormWindowState.Normal;
            XamlTestSurface.PumpUntil(() => form.WindowState == FormWindowState.Normal);

            width = bar.AbsoluteBounds.Width;
            ClickAt(host, width - 69f, 16f);
            XamlTestSurface.PumpUntil(() => form.WindowState == FormWindowState.Maximized);
            Assert.AreEqual(FormWindowState.Maximized, form.WindowState, "the maximize button maximizes");
            Assert.IsTrue(bar.IsMaximized, "the bar reports the maximized state");

            width = bar.AbsoluteBounds.Width;
            ClickAt(host, width - 69f, 16f);
            XamlTestSurface.PumpUntil(() => form.WindowState == FormWindowState.Normal);
            Assert.AreEqual(FormWindowState.Normal, form.WindowState, "the same button restores");

            width = bar.AbsoluteBounds.Width;
            ClickAt(host, width - 23f, 16f);
            XamlTestSurface.PumpUntil(() => closing);
            Assert.IsTrue(closing, "the close button raises FormClosing");
        }
        finally
        {
            if (cancelClosing is not null)
            {
                form.FormClosing -= cancelClosing;
            }

            form.Close();
        }
    }

    [TestMethod]
    public void DoubleClickOnTheCaptionTogglesMaximize()
    {
        using var form = CreateForm(out var host, out _);
        try
        {
            form.Show();
            XamlTestSurface.PumpUntil(() => WindowChrome.IsAttached(form) && host.IsHandleCreated);
            XamlTestSurface.Pump(80);

            SendMessage(form.Handle, WM_NCLBUTTONDBLCLK, (IntPtr)HTCAPTION, LParam(ScreenPoint(host, 160f, 16f)));
            XamlTestSurface.PumpUntil(() => form.WindowState == FormWindowState.Maximized);
            Assert.AreEqual(FormWindowState.Maximized, form.WindowState, "the first double click maximizes");

            SendMessage(form.Handle, WM_NCLBUTTONDBLCLK, (IntPtr)HTCAPTION, LParam(ScreenPoint(host, 160f, 16f)));
            XamlTestSurface.PumpUntil(() => form.WindowState == FormWindowState.Normal);
            Assert.AreEqual(FormWindowState.Normal, form.WindowState, "the second double click restores");
        }
        finally
        {
            form.Close();
        }
    }

    [TestMethod]
    public void DoubleClickOnTheBarTogglesEvenWithoutChrome()
    {
        using var form = CreateForm(out var host, out _, autoAttach: false);
        try
        {
            form.Show();
            XamlTestSurface.PumpUntil(() => host.IsHandleCreated);
            XamlTestSurface.Pump(80);

            var scale = host.DpiScale > 0f ? host.DpiScale : 1f;
            var point = new PointF(160f * scale, 16f * scale);
            host.ProcessPointerDown(point, MouseButtons.Left, clickCount: 2);
            host.ProcessPointerUp(point, MouseButtons.Left);
            XamlTestSurface.PumpUntil(() => form.WindowState == FormWindowState.Maximized);
            Assert.AreEqual(FormWindowState.Maximized, form.WindowState, "a visual double click maximizes");
        }
        finally
        {
            form.Close();
        }
    }

    [TestMethod]
    public void TemplatePartsAreWiredAndArbitraryContentStaysInteractive()
    {
        using var form = CreateForm(out var host, out var bar);
        VC.TextBlock? title = null;
        VC.Button? close = null;
        VC.Button? maximize = null;
        FormClosingEventHandler? cancelClosing = null;
        try
        {
            var template = new VisualControlTemplate(scope =>
            {
                var root = new VC.Canvas();
                title = new VC.TextBlock { Width = 220, Height = 24 };
                VC.Canvas.SetLeft(title, 40);
                VC.Canvas.SetTop(title, 4);
                root.Add(title);
                scope.Register("PART_Title", title);

                var menu = new VC.Button { Width = 64, Height = 32, Text = "menu" };
                VC.Canvas.SetLeft(menu, 280);
                root.Add(menu);

                maximize = new VC.Button { Width = 40, Height = 32, Text = "max" };
                VC.Canvas.SetLeft(maximize, 400);
                root.Add(maximize);
                scope.Register("PART_Maximize", maximize);

                close = new VC.Button { Width = 40, Height = 32, Text = "x" };
                VC.Canvas.SetLeft(close, 440);
                root.Add(close);
                scope.Register("PART_Close", close);

                return root;
            })
            {
                TargetType = typeof(VC.VisualTitleBar),
            };

            bar.Title = "Template title";
            bar.Template = template;
            form.Show();
            XamlTestSurface.PumpUntil(() => WindowChrome.IsAttached(form) && host.IsHandleCreated);
            XamlTestSurface.Pump(80);

            Assert.IsNotNull(title, "the template built its title part");
            Assert.AreEqual("Template title", title!.Text, "PART_Title receives the bar title");

            const float menuX = 280f + 32f;
            Assert.AreEqual(HTCLIENT, HitTest(form, host, menuX, 16f), "template content answers as a client area");
            Assert.AreEqual(HTCAPTION, HitTest(form, host, 160f, 16f), "empty template space stays draggable");

            ClickAt(host, 420f, 16f);
            XamlTestSurface.PumpUntil(() => form.WindowState == FormWindowState.Maximized);
            Assert.AreEqual(FormWindowState.Maximized, form.WindowState, "PART_Maximize is wired");

            var closing = false;
            cancelClosing = (_, e) =>
            {
                closing = true;
                e.Cancel = true;
            };
            form.FormClosing += cancelClosing;
            ClickAt(host, 460f, 16f);
            XamlTestSurface.PumpUntil(() => closing);
            Assert.IsTrue(closing, "PART_Close is wired");
        }
        finally
        {
            if (cancelClosing is not null)
            {
                form.FormClosing -= cancelClosing;
            }

            bar.Template = null;
            form.Close();
        }
    }

    [TestMethod]
    public void DraggingTheTitleBarToTheTopSnapsAndDraggingDownRestores()
    {
        using var form = CreateForm(out var host, out _);
        var sent = false;
        try
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(120, 120);
            // 测试进程的控制台窗口可能一直压在普通窗口上方；TopMost 让探针点一定落在窗体上。
            form.TopMost = true;
            form.Show();
            form.BringToFront();
            XamlTestSurface.PumpUntil(() => WindowChrome.IsAttached(form) && host.IsHandleCreated);
            XamlTestSurface.Pump(120);
            form.Activate();
            SetForegroundWindow(form.Handle);
            XamlTestSurface.Pump(80);

            var start = ScreenPoint(host, 160f, 16f);
            var top = new Point(start.X, SystemInformation.WorkingArea.Top);
            var hostHit = (int)SendMessage(host.Handle, WM_NCHITTEST, IntPtr.Zero, LParam(start));
            var windowAtStart = WindowFromPoint(new POINT { X = start.X, Y = start.Y });

            sent = RunDrag(start, top);
            var snapped = XamlTestSurface.PumpUntil(
                () => form.WindowState == FormWindowState.Maximized
                      || Math.Abs(form.Top - SystemInformation.WorkingArea.Top) <= 2,
                2500);

            Assert.IsTrue(sent, "SetCursorPos queued the drag");
            Assert.IsTrue(
                snapped,
                $"dragging the title bar to the top of the screen snaps or maximizes "
                + $"(state={form.WindowState}, bounds={form.Bounds}, hostHit={hostHit}, "
                + $"windowAt={windowAtStart}, host={host.Handle}, cursor={Cursor.Position}, start={start}, top={top})");

            if (form.WindowState == FormWindowState.Maximized)
            {
                var dragStart = ScreenPoint(host, 160f, 16f);
                var middle = new Point(
                    SystemInformation.WorkingArea.Left + (SystemInformation.WorkingArea.Width / 2),
                    SystemInformation.WorkingArea.Top + 220);
                Assert.IsTrue(RunDrag(dragStart, middle), "SetCursorPos queued the restore drag");
                var restored = XamlTestSurface.PumpUntil(
                    () => form.WindowState == FormWindowState.Normal,
                    2500);
                Assert.IsTrue(restored, $"dragging a maximized title bar down restores the window (state={form.WindowState})");
            }
        }
        catch (AssertInconclusiveException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"the headed snap probe could not run in this session: {ex.Message}");
        }
        finally
        {
            form.TopMost = false;
            form.Close();
        }
    }

    [TestMethod]
    public void DraggingAnEdgeResizesTheWindow()
    {
        using var form = CreateForm(out var host, out _);
        try
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(200, 200);
            form.TopMost = true;
            form.Show();
            form.BringToFront();
            XamlTestSurface.PumpUntil(() => WindowChrome.IsAttached(form) && host.IsHandleCreated);
            XamlTestSurface.Pump(120);
            form.Activate();
            SetForegroundWindow(form.Handle);
            XamlTestSurface.Pump(80);

            var before = form.Bounds;
            var start = ScreenPoint(host, 3f, 200f);
            var end = new Point(start.X - 70, start.Y);

            var sent = RunDrag(start, end);
            var resized = XamlTestSurface.PumpUntil(() => form.Width >= before.Width + 30, 2500);
            Assert.IsTrue(sent, "SetCursorPos queued the resize drag");
            Assert.IsTrue(
                resized,
                $"dragging the left edge widens the window (before={before}, after={form.Bounds})");
        }
        catch (AssertInconclusiveException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"the headed resize probe could not run in this session: {ex.Message}");
        }
        finally
        {
            form.TopMost = false;
            form.Close();
        }
    }

    [TestMethod]
    public void AutoAttachedChromeKeepsTheSnapStylesAfterTheFormIsShown()
    {
        var form = CreateForm(out var host, out var bar);
        using (form)
        using (host)
        using (bar)
        {
            form.Show();
            Application.DoEvents();
            Thread.Sleep(80);
            Application.DoEvents();

            var style = GetWindowLongPtr(form.Handle, -16).ToInt64();
            Assert.AreNotEqual(0L, style & 0x00040000L, "WS_THICKFRAME survives the show sequence");
            Assert.AreNotEqual(0L, style & 0x00020000L, "WS_MINIMIZEBOX survives the show sequence");
            Assert.AreNotEqual(0L, style & 0x00010000L, "WS_MAXIMIZEBOX survives the show sequence");
        }
    }

    private static Form CreateForm(out VisualHost host, out VC.VisualTitleBar bar, bool autoAttach = true)
    {
        var form = new Form
        {
            Size = new Size(800, 600),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(100, 100),
            Text = "Chrome probe",
        };

        host = new VisualHost { Dock = DockStyle.Fill };
        form.Controls.Add(host);

        var root = new VC.Canvas { Background = new SolidColorBrush(Theme.Current.WindowBackground) };
        bar = new VC.VisualTitleBar { Width = 800, Height = 32, Title = "Chrome probe", AutoAttachChrome = autoAttach };
        VC.Canvas.SetLeft(bar, 0);
        VC.Canvas.SetTop(bar, 0);
        root.Add(bar);
        host.Root = root;
        return form;
    }

    private static void ClickAt(VisualHost host, float logicalX, float logicalY)
    {
        var scale = host.DpiScale > 0f ? host.DpiScale : 1f;
        var physical = new PointF(logicalX * scale, logicalY * scale);
        host.ProcessPointerDown(physical, MouseButtons.Left);
        host.ProcessPointerUp(physical, MouseButtons.Left);
    }

    private static int HitTest(Form form, VisualHost host, float logicalX, float logicalY)
    {
        var screen = ScreenPoint(host, logicalX, logicalY);
        return (int)SendMessage(form.Handle, WM_NCHITTEST, IntPtr.Zero, LParam(screen));
    }

    private static Point ScreenPoint(VisualHost host, float logicalX, float logicalY)
    {
        var scale = host.DpiScale > 0f ? host.DpiScale : 1f;
        return host.PointToScreen(new Point((int)Math.Round(logicalX * scale), (int)Math.Round(logicalY * scale)));
    }

    private static IntPtr LParam(Point point)
        => new(unchecked((int)((point.Y << 16) | (point.X & 0xFFFF))));

    private static Bitmap Render(VisualHost host)
    {
        host.InvalidateSurface();
        var bitmap = new Bitmap(host.Size.Width, host.Size.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.FromArgb(255, 10, 10, 10));
            host.RenderTo(graphics, host.Size);
        }

        return bitmap;
    }

    private static Rectangle MinimizeGlyphArea(int width) => new(width - 138, 4, 46, 24);

    private static Rectangle MaximizeGlyphArea(int width) => new(width - 92, 4, 46, 24);

    private static Rectangle CloseGlyphArea(int width) => new(width - 46, 0, 46, 32);

    private static int CountDifferent(Bitmap bitmap, Rectangle area, Color background, int threshold = 24)
    {
        var count = 0;
        for (var y = area.Top; y < area.Bottom; y++)
        {
            for (var x = area.Left; x < area.Right; x++)
            {
                if (Distance(bitmap.GetPixel(x, y), background) > threshold)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int CountPixelDiff(Bitmap left, Bitmap right, Rectangle area)
    {
        var count = 0;
        for (var y = area.Top; y < area.Bottom; y++)
        {
            for (var x = area.Left; x < area.Right; x++)
            {
                if (Distance(left.GetPixel(x, y), right.GetPixel(x, y)) > 24)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int Distance(Color left, Color right)
        => Math.Abs(left.R - right.R) + Math.Abs(left.G - right.G) + Math.Abs(left.B - right.B);

    private static void AssertColor(Color expected, Color actual, string message)
    {
        Assert.IsTrue(
            Distance(expected, actual) <= 6,
            $"{message}: expected {expected}, got {actual}");
    }

    // 拖动探针：独立线程发真实鼠标输入，测试线程同时泵消息，原生模态移动循环才能跑起来。
    private static bool RunDrag(Point start, Point end)
    {
        var sent = false;
        var thread = new Thread(() =>
        {
            try
            {
                SetCursorPos(start.X, start.Y);
                Thread.Sleep(150);
                mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(150);
                for (var step = 1; step <= 10; step++)
                {
                    SetCursorPos(
                        start.X + ((end.X - start.X) * step / 10),
                        start.Y + ((end.Y - start.Y) * step / 10));
                    Thread.Sleep(30);
                }

                Thread.Sleep(200);
                mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
                sent = true;
            }
            finally
            {
            }
        })
        {
            IsBackground = true,
        };

        thread.Start();
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < 3000 && thread.IsAlive)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }

        Application.DoEvents();
        thread.Join(500);
        return sent;
    }

    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hwnd, int index);

    private static IntPtr GetWindowLongPtr(IntPtr hwnd, int index)
        => IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, index) : new IntPtr(GetWindowLong32(hwnd, index));

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}
