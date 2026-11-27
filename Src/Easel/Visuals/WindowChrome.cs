using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Easel.Visuals.Controls;
using EaselButtonBase = Easel.Visuals.Controls.ButtonBase;

namespace Easel.Visuals;

/// <summary>Turns a borderless <see cref="Form"/> into a modern window with a draggable, resizable client area.</summary>
/// <remarks>
/// <para>
/// <see cref="Attach(Form, VisualTitleBar)"/> sets <see cref="Form.FormBorderStyle"/> to
/// <see cref="FormBorderStyle.None"/>, installs one message hook on the form and one on every
/// <see cref="VisualHost"/> inside it, and keeps the native snap, resize and maximize behaviour that a plain
/// borderless window loses. A <see cref="VisualTitleBar"/> that joins a hosted visual tree does this by itself
/// unless <see cref="VisualTitleBar.AutoAttachChrome"/> is turned off.
/// </para>
/// <para>
/// The hit test walks the visual tree at the pointer's screen position. Caption buttons, focusable template content,
/// menus and native islands answer <c>HTCLIENT</c>; the empty title area answers <c>HTCAPTION</c>; the eight-pixel
/// edge answers the matching resize code while the window is not maximized. Everything else stays <c>HTCLIENT</c>.
/// </para>
/// <para>
/// The helper keeps exactly one session per form. A second title bar on the same form replaces the first one's
/// routing; disposing the active bar detaches the session. No extra window handles are created.
/// </para>
/// </remarks>
public static class WindowChrome
{
    private const int WM_NCCALCSIZE = 0x0083;
    private const int WM_GETMINMAXINFO = 0x0024;
    private const int WM_NCHITTEST = 0x0084;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int WM_NCLBUTTONDBLCLK = 0x00A3;

    private const int HTCLIENT = 1;
    private const int HTCAPTION = 2;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    private const int ResizeBorder = 8;

    private const int GWL_STYLE = -16;
    private const long WS_MAXIMIZEBOX = 0x00010000L;
    private const long WS_MINIMIZEBOX = 0x00020000L;
    private const long WS_THICKFRAME = 0x00040000L;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    private static readonly Dictionary<Form, ChromeSession> Sessions = [];

    /// <summary>Attaches a title bar to a form and makes the form borderless.</summary>
    /// <param name="form">The window to convert.</param>
    /// <param name="bar">The title bar whose caption buttons drive the window.</param>
    /// <exception cref="ArgumentNullException"><paramref name="form"/> or <paramref name="bar"/> is <see langword="null"/>.</exception>
    public static void Attach(Form form, VisualTitleBar bar) => Attach(form, bar, setBorderless: true);

    /// <summary>Attaches a title bar to a form, optionally keeping the caller's border style.</summary>
    /// <param name="form">The window to attach to.</param>
    /// <param name="bar">The title bar whose caption buttons drive the window.</param>
    /// <param name="setBorderless">Whether to switch the form to <see cref="FormBorderStyle.None"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="form"/> or <paramref name="bar"/> is <see langword="null"/>.</exception>
    public static void Attach(Form form, VisualTitleBar bar, bool setBorderless)
    {
        if (form is null) throw new ArgumentNullException(nameof(form));
        if (bar is null) throw new ArgumentNullException(nameof(bar));

        if (Sessions.TryGetValue(form, out var existing))
        {
            existing.SetBar(bar, setBorderless);
            bar.AdoptChromeTarget(form);
            return;
        }

        Sessions[form] = new ChromeSession(form, bar, setBorderless);
        bar.AdoptChromeTarget(form);
    }

    /// <summary>Detaches the chrome session attached to <paramref name="form"/>, if any.</summary>
    /// <param name="form">The window to restore.</param>
    public static void Detach(Form form)
    {
        if (form is null)
        {
            return;
        }

        if (Sessions.TryGetValue(form, out var session))
        {
            Sessions.Remove(form);
            session.Dispose();
        }
    }

    /// <summary>Returns whether <paramref name="form"/> currently has a chrome session.</summary>
    /// <param name="form">The window to inspect.</param>
    /// <returns><see langword="true"/> when a session is attached.</returns>
    public static bool IsAttached(Form form) => form is not null && Sessions.ContainsKey(form);

    /// <summary>Returns the title bar currently routed to <paramref name="form"/>, or <see langword="null"/>.</summary>
    /// <param name="form">The window to inspect.</param>
    /// <returns>The attached title bar, or <see langword="null"/>.</returns>
    public static VisualTitleBar? GetTitleBar(Form form)
        => form is not null && Sessions.TryGetValue(form, out var session) ? session.Bar : null;

    // 只在这个标题栏仍负责该窗口时拆会话；同一窗口的其它标题栏不受影响。
    internal static void DetachBar(Form form, VisualTitleBar bar)
    {
        if (form is null || bar is null)
        {
            return;
        }

        if (Sessions.TryGetValue(form, out var session) && ReferenceEquals(session.Bar, bar))
        {
            Detach(form);
        }
    }

    private static void CollectHosts(Control parent, List<VisualHost> hosts)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is VisualHost host)
            {
                hosts.Add(host);
            }

            CollectHosts(child, hosts);
        }
    }

    private static bool IsInteractive(VisualElement element)
    {
        if (element is EaselButtonBase || element is NativeElement || element is VisualMenu
            || element is VisualMenuItemContainer)
        {
            return true;
        }

        return element.Focusable || element.Cursor is not null;
    }

    private static Point PointFromLParam(IntPtr lParam)
    {
        var value = lParam.ToInt64();
        return new Point(unchecked((short)(value & 0xFFFFL)), unchecked((short)((value >> 16) & 0xFFFFL)));
    }

    private sealed class ChromeSession : IDisposable
    {
        private readonly Form _form;
        private readonly FormBorderStyle _originalBorderStyle;
        private readonly HashSet<VisualHost> _watchedHosts = [];
        private readonly Dictionary<VisualHost, WindowMessageHook> _hostHooks = [];
        private VisualTitleBar _bar;
        private WindowMessageHook? _formHook;
        private bool _setBorderless;
        private long _originalStyle;
        private bool _originalStyleValid;
        private bool _formClosed;
        private bool _disposed;

        internal ChromeSession(Form form, VisualTitleBar bar, bool setBorderless)
        {
            _form = form;
            _bar = bar;
            _setBorderless = setBorderless;
            _originalBorderStyle = form.FormBorderStyle;

            form.FormClosed += OnFormClosed;
            form.Disposed += OnFormDisposed;
            form.HandleCreated += OnFormHandleCreated;
            form.HandleDestroyed += OnFormHandleDestroyed;
            form.Shown += OnFormShown;
            form.ControlAdded += OnFormControlChanged;
            form.ControlRemoved += OnFormControlChanged;
            Theme.Changed += OnThemeChanged;

            if (setBorderless && form.FormBorderStyle != FormBorderStyle.None)
            {
                form.FormBorderStyle = FormBorderStyle.None;
            }

            if (form.IsHandleCreated)
            {
                InstallFormHook();
                RefreshHosts();
            }
        }

        internal VisualTitleBar Bar => _bar;

        internal void SetBar(VisualTitleBar bar, bool setBorderless)
        {
            if (_disposed)
            {
                return;
            }

            _bar = bar;
            if (setBorderless && !_setBorderless)
            {
                _setBorderless = true;
                if (_form.FormBorderStyle != FormBorderStyle.None)
                {
                    _form.FormBorderStyle = FormBorderStyle.None;
                }

                ApplySnapStyles();
            }

            RefreshHosts();
            ApplyDwm();
        }

        internal bool HandleMessage(ref Message m, bool topLevel)
        {
            if (_disposed)
            {
                return false;
            }

            switch (m.Msg)
            {
                case WM_NCCALCSIZE:
                    if (_setBorderless && _form.FormBorderStyle == FormBorderStyle.None
                        && m.WParam != IntPtr.Zero)
                    {
                        // 客户区就是整个窗口；边框样式只是为原生吸附保留。
                        m.Result = IntPtr.Zero;
                        return true;
                    }

                    break;

                case WM_GETMINMAXINFO:
                    if (topLevel && _setBorderless)
                    {
                        ApplyMinMaxInfo(m.LParam);
                        m.Result = IntPtr.Zero;
                        return true;
                    }

                    break;

                case WM_NCHITTEST:
                {
                    // 命中测试时顺手补一次：任何一次窗口样式重算都不该让吸附失效。
                    ApplySnapStyles();
                    var hit = ComputeHitTest(PointFromLParam(m.LParam));
                    m.Result = (IntPtr)hit;
                    return true;
                }

                case WM_NCLBUTTONDOWN:
                    if (!topLevel)
                    {
                        // 子宿主自己拿不到顶层移动/缩放循环，把这次非客户区按下转给窗体。
                        var childHit = ComputeHitTest(PointFromLParam(m.LParam));
                        if (childHit != HTCLIENT)
                        {
                            ReleaseCapture();
                            SendMessage(_form.Handle, WM_NCLBUTTONDOWN, (IntPtr)childHit, m.LParam);
                            m.Result = IntPtr.Zero;
                            return true;
                        }
                    }

                    break;

                case WM_NCLBUTTONDBLCLK:
                    if (!topLevel)
                    {
                        var childHit = ComputeHitTest(PointFromLParam(m.LParam));
                        if (childHit == HTCAPTION)
                        {
                            ReleaseCapture();
                            SendMessage(_form.Handle, WM_NCLBUTTONDBLCLK, (IntPtr)childHit, m.LParam);
                            m.Result = IntPtr.Zero;
                            return true;
                        }
                    }
                    else if (ComputeHitTest(PointFromLParam(m.LParam)) == HTCAPTION)
                    {
                        ToggleMaximize();
                        m.Result = IntPtr.Zero;
                        return true;
                    }

                    break;
            }

            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Theme.Changed -= OnThemeChanged;
            _form.FormClosed -= OnFormClosed;
            _form.Disposed -= OnFormDisposed;
            _form.HandleCreated -= OnFormHandleCreated;
            _form.HandleDestroyed -= OnFormHandleDestroyed;
            _form.Shown -= OnFormShown;
            _form.ControlAdded -= OnFormControlChanged;
            _form.ControlRemoved -= OnFormControlChanged;

            foreach (var host in _watchedHosts.ToArray())
            {
                UnwatchHost(host);
            }

            _watchedHosts.Clear();
            _formHook?.Dispose();
            _formHook = null;

            RestoreWindowState();
        }

        private void RestoreWindowState()
        {
            if (!_setBorderless || _formClosed || _form.IsDisposed)
            {
                return;
            }

            try
            {
                if (_form.IsHandleCreated)
                {
                    if (_originalStyleValid)
                    {
                        SetWindowLongPtr(_form.Handle, GWL_STYLE, new IntPtr(_originalStyle));
                        SetWindowPos(
                            _form.Handle,
                            IntPtr.Zero,
                            0,
                            0,
                            0,
                            0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                    }
                }

                if (_form.FormBorderStyle == FormBorderStyle.None && _originalBorderStyle != FormBorderStyle.None)
                {
                    _form.FormBorderStyle = _originalBorderStyle;
                }
            }
            catch (ObjectDisposedException)
            {
                // 窗口已经在拆；没有可恢复的样式。
            }
        }

        private void OnFormHandleCreated(object? sender, EventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            CaptureOriginalStyle();
            ApplySnapStyles();
            InstallFormHook();
            RefreshHosts();
            ApplyDwm();
        }

        // WinForms 在窗体显示阶段还会按 FormBorderStyle 重算一次窗口样式；
        // 那一步会抹掉为本机吸附补的 WS_THICKFRAME，显示完再补一次。
        private void OnFormShown(object? sender, EventArgs e)
        {
            ApplySnapStyles();
            ApplyDwm();
        }

        private void OnFormHandleDestroyed(object? sender, EventArgs e)
        {
            _formHook?.Dispose();
            _formHook = null;
            foreach (var host in _hostHooks.Keys.ToArray())
            {
                _hostHooks[host].Dispose();
                _hostHooks.Remove(host);
            }
        }

        private void OnFormControlChanged(object? sender, ControlEventArgs e)
        {
            if (!_disposed)
            {
                RefreshHosts();
            }
        }

        private void OnFormClosed(object? sender, FormClosedEventArgs e)
        {
            _formClosed = true;
            Detach(_form);
        }

        private void OnFormDisposed(object? sender, EventArgs e) => Detach(_form);

        private void OnThemeChanged(object? sender, EventArgs e) => ApplyDwm();

        private void InstallFormHook()
        {
            _formHook?.Dispose();
            _formHook = null;
            if (_disposed || !_form.IsHandleCreated)
            {
                return;
            }

            _formHook = new WindowMessageHook(this, _form.Handle, topLevel: true);
        }

        private void RefreshHosts()
        {
            if (_disposed || !_form.IsHandleCreated)
            {
                return;
            }

            var hosts = new List<VisualHost>();
            CollectHosts(_form, hosts);
            var present = new HashSet<VisualHost>(hosts);

            foreach (var watched in _watchedHosts.ToArray())
            {
                if (!present.Contains(watched))
                {
                    UnwatchHost(watched);
                    _watchedHosts.Remove(watched);
                }
            }

            foreach (var host in hosts)
            {
                WatchHost(host);
            }
        }

        private void WatchHost(VisualHost host)
        {
            if (!_watchedHosts.Add(host))
            {
                InstallHostHook(host);
                return;
            }

            host.HandleCreated += OnHostHandleCreated;
            host.HandleDestroyed += OnHostHandleDestroyed;
            InstallHostHook(host);
        }

        private void UnwatchHost(VisualHost host)
        {
            host.HandleCreated -= OnHostHandleCreated;
            host.HandleDestroyed -= OnHostHandleDestroyed;
            if (_hostHooks.TryGetValue(host, out var hook))
            {
                hook.Dispose();
                _hostHooks.Remove(host);
            }
        }

        private void OnHostHandleCreated(object? sender, EventArgs e)
        {
            if (!_disposed && sender is VisualHost host && _watchedHosts.Contains(host))
            {
                InstallHostHook(host);
            }
        }

        private void OnHostHandleDestroyed(object? sender, EventArgs e)
        {
            if (sender is VisualHost host && _hostHooks.TryGetValue(host, out var hook))
            {
                hook.Dispose();
                _hostHooks.Remove(host);
            }
        }

        private void InstallHostHook(VisualHost host)
        {
            if (_disposed || !host.IsHandleCreated || _hostHooks.ContainsKey(host))
            {
                return;
            }

            _hostHooks[host] = new WindowMessageHook(this, host.Handle, topLevel: false);
        }

        private VisualHost? ResolveHost()
        {
            if (_bar.Surface is { } surface)
            {
                return surface;
            }

            var hosts = new List<VisualHost>();
            CollectHosts(_form, hosts);
            return hosts.Count > 0 ? hosts[0] : null;
        }

        private int ComputeHitTest(Point screen)
        {
            if (_disposed || !_form.IsHandleCreated || _form.WindowState == FormWindowState.Minimized)
            {
                return HTCLIENT;
            }

            var host = ResolveHost();
            if (host is null || !host.IsHandleCreated || !host.Visible)
            {
                return HTCLIENT;
            }

            if (_setBorderless && _form.WindowState != FormWindowState.Maximized)
            {
                var client = _form.PointToClient(screen);
                var width = _form.ClientSize.Width;
                var height = _form.ClientSize.Height;
                if (client.X >= 0 && client.Y >= 0 && client.X < width && client.Y < height)
                {
                    var border = Math.Max(4, (int)Math.Round(ResizeBorder * host.DpiScale));
                    var left = client.X < border;
                    var right = client.X >= width - border;
                    var top = client.Y < border;
                    var bottom = client.Y >= height - border;
                    if (left || right || top || bottom)
                    {
                        if (top && left) return HTTOPLEFT;
                        if (top && right) return HTTOPRIGHT;
                        if (bottom && left) return HTBOTTOMLEFT;
                        if (bottom && right) return HTBOTTOMRIGHT;
                        if (left) return HTLEFT;
                        if (right) return HTRIGHT;
                        if (top) return HTTOP;
                        return HTBOTTOM;
                    }
                }
            }

            var hostPoint = host.PointToClient(screen);
            if (hostPoint.X < 0 || hostPoint.Y < 0
                || hostPoint.X >= host.ClientSize.Width || hostPoint.Y >= host.ClientSize.Height)
            {
                return HTCLIENT;
            }

            var scale = host.DpiScale;
            if (scale <= 0f)
            {
                scale = 1f;
            }

            var logical = new PointF(hostPoint.X / scale, hostPoint.Y / scale);
            var hit = host.FindElementAt(logical);
            if (hit is null)
            {
                return _bar.AbsoluteBounds.Contains(logical) && _bar.ContainsDragArea(logical)
                    ? HTCAPTION
                    : HTCLIENT;
            }

            var interactive = false;
            var insideBar = false;
            for (var current = hit; current is not null; current = current.ParentElement)
            {
                if (ReferenceEquals(current, _bar))
                {
                    insideBar = true;
                    break;
                }

                if (IsInteractive(current))
                {
                    interactive = true;
                }
            }

            if (!insideBar || interactive)
            {
                return HTCLIENT;
            }

            return _bar.ContainsDragArea(logical) ? HTCAPTION : HTCLIENT;
        }

        private void ToggleMaximize()
        {
            _form.WindowState = _form.WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        }

        private void CaptureOriginalStyle()
        {
            if (!_form.IsHandleCreated)
            {
                return;
            }

            _originalStyle = GetWindowLongPtr(_form.Handle, GWL_STYLE).ToInt64();
            _originalStyleValid = true;
        }

        private void ApplySnapStyles()
        {
            if (_disposed || !_setBorderless || !_form.IsHandleCreated)
            {
                return;
            }

            var style = GetWindowLongPtr(_form.Handle, GWL_STYLE).ToInt64();
            var wanted = style | WS_THICKFRAME;
            if (_form.MaximizeBox)
            {
                wanted |= WS_MAXIMIZEBOX;
            }

            if (_form.MinimizeBox)
            {
                wanted |= WS_MINIMIZEBOX;
            }

            if (wanted == style)
            {
                return;
            }

            SetWindowLongPtr(_form.Handle, GWL_STYLE, new IntPtr(wanted));
            SetWindowPos(
                _form.Handle,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        private void ApplyMinMaxInfo(IntPtr lParam)
        {
            if (lParam == IntPtr.Zero)
            {
                return;
            }

            var monitor = MonitorFromWindow(_form.Handle, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
            {
                return;
            }

            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref info))
            {
                return;
            }

            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            mmi.ptMaxPosition.X = info.rcWork.Left - info.rcMonitor.Left;
            mmi.ptMaxPosition.Y = info.rcWork.Top - info.rcMonitor.Top;
            mmi.ptMaxSize.X = info.rcWork.Right - info.rcWork.Left;
            mmi.ptMaxSize.Y = info.rcWork.Bottom - info.rcWork.Top;
            Marshal.StructureToPtr(mmi, lParam, false);
        }

        private void ApplyDwm()
        {
            if (_disposed || !_form.IsHandleCreated)
            {
                return;
            }

            NativeWindowTheme.ApplyDarkMode(_form);

            try
            {
                var corner = DWMWCP_ROUND;
                _ = DwmSetWindowAttribute(
                    _form.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            }
            catch (DllNotFoundException)
            {
                // 没有 DWM（旧系统或服务器核心）时保持方角；其余行为不受影响。
            }
            catch (EntryPointNotFoundException)
            {
            }
        }
    }

    // 每个顶层/子窗口一条消息钩子；子窗口只负责把非客户区命中让给顶层。
    private sealed class WindowMessageHook : NativeWindow, IDisposable
    {
        private readonly ChromeSession _session;
        private readonly bool _topLevel;

        internal WindowMessageHook(ChromeSession session, IntPtr handle, bool topLevel)
        {
            _session = session;
            _topLevel = topLevel;
            AssignHandle(handle);
        }

        protected override void WndProc(ref Message m)
        {
            if (_session.HandleMessage(ref m, _topLevel))
            {
                return;
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                ReleaseHandle();
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private static IntPtr GetWindowLongPtr(IntPtr hwnd, int index)
        => IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, index) : (IntPtr)GetWindowLong32(hwnd, index);

    private static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value)
        => IntPtr.Size == 8
            ? SetWindowLongPtr64(hwnd, index, value)
            : (IntPtr)SetWindowLong32(hwnd, index, value.ToInt32());
}
