using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Demo.ViewModels;
using Easel.Visuals;
using XamlBrush = Easel.Brush;
using XamlVisibility = Easel.Controls.Visibility;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace Demo.Views.Industrial;

/// <summary>A line-status panel that moves the same visual subtree between the page and its own window.</summary>
/// <remarks>
/// Dragging the panel header past a small threshold tears the panel out into a <see cref="Window"/>; dragging
/// that window back over the empty slot docks it again. The page owns the panel, so closing the card closes the
/// floating window as well.
/// </remarks>
public sealed partial class TearOffView : ThemedDockPanel
{
    private readonly WinFormsTimer _timer = new() { Interval = 900 };
    private readonly ProductionLineViewModel _viewModel;
    private readonly XamlBrush? _idleDockBorder;
    private readonly XamlBrush? _idleDockBackground;

    private Window? _window;
    private bool _isDetached;
    private bool _pressed;
    private bool _moved;
    private bool _disposed;
    private PointF _pressPosition;
    private Point _pagePressScreen;
    private Point _windowPressScreen;
    private Point _windowPressLocation;

    /// <summary>Creates the page and starts the line simulator.</summary>
    public TearOffView()
    {
        _viewModel = new ProductionLineViewModel();
        DataContext = _viewModel;
        InitializeComponent();
        DragBar.Cursor = Cursors.SizeAll;
        _idleDockBorder = DockPlaceholder.BorderBrush;
        _idleDockBackground = DockPlaceholder.Background;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>The floating window while the panel is detached, or <see langword="null"/>.</summary>
    internal Window? DetachedWindow => _window;

    /// <summary>Tears the panel out to <paramref name="screenPoint"/>; used by the capture runner.</summary>
    internal void DetachForCapture(Point screenPoint) => DetachAt(screenPoint);

    /// <summary>Runs the real header drag gesture; used by the capture runner.</summary>
    internal bool DragOutForCapture()
    {
        if (_isDetached || DragBar.Surface is not { } surface)
        {
            return false;
        }

        var bounds = DragBar.AbsoluteBounds;
        var scale = surface.DpiScale;
        var start = new PointF(
            (bounds.X + 24f) * scale,
            (bounds.Y + (bounds.Height / 2f)) * scale);
        var end = new PointF(
            (bounds.X + 72f) * scale,
            (bounds.Y + (bounds.Height / 2f) + 14f) * scale);
        surface.ProcessPointerDown(start, MouseButtons.Left);
        surface.ProcessPointerMove(end);
        surface.ProcessPointerUp(end, MouseButtons.Left);
        return _isDetached;
    }

    /// <summary>Drags the floating window back over its slot; used by the capture runner.</summary>
    internal bool DragInForCapture()
    {
        if (!_isDetached || _window is not { } window || DragBar.Surface is not { } surface)
        {
            return false;
        }

        var slot = DockSlotScreenBounds();
        if (slot.IsEmpty)
        {
            return false;
        }

        var bounds = DragBar.AbsoluteBounds;
        var scale = surface.DpiScale;
        var headerCenter = new Point(
            (int)Math.Round((bounds.X + (bounds.Width / 2f)) * scale),
            (int)Math.Round((bounds.Y + (bounds.Height / 2f)) * scale));
        var currentHeaderScreen = surface.PointToScreen(headerCenter);
        var target = new Point(slot.X + (slot.Width / 2), slot.Y + 40);
        window.Location = new Point(
            window.Location.X + (target.X - currentHeaderScreen.X),
            window.Location.Y + (target.Y - currentHeaderScreen.Y));

        surface.ProcessPointerDown(headerCenter, MouseButtons.Left);
        surface.ProcessPointerMove(new PointF(headerCenter.X + 12, headerCenter.Y + 4));
        surface.ProcessPointerUp(new PointF(headerCenter.X + 12, headerCenter.Y + 4), MouseButtons.Left);
        return !_isDetached;
    }

    /// <summary>Docks the panel again; used by the capture runner.</summary>
    internal void DockForCapture() => DockBack();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposed = true;
            _timer.Stop();
            _timer.Dispose();
            if (_window is { } window)
            {
                _window = null;
                window.FormClosing -= OnWindowClosing;
                window.Close();
                window.Dispose();
            }

            if (_isDetached)
            {
                _isDetached = false;
                DetachablePanel.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void OnTick(object? sender, EventArgs e) => _viewModel.Tick();

    private void OnDragBarPressed(object? sender, VisualPointerEventArgs e)
    {
        if (_disposed || e.Button != MouseButtons.Left || IsInside(DetachButton, e.Position))
        {
            return;
        }

        _pressed = true;
        _moved = false;
        _pressPosition = e.Position;
        if (_isDetached && _window is { } window)
        {
            _windowPressScreen = ToScreen(e);
            _windowPressLocation = window.Location;
        }
        else
        {
            _pagePressScreen = ToScreen(e);
        }
    }

    private void OnDragBarMoved(object? sender, VisualPointerEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        if (!_moved && Distance(e.Position, _pressPosition) < 8f)
        {
            return;
        }

        _moved = true;
        if (_isDetached && _window is { } window)
        {
            var screen = ToScreen(e);
            window.Location = new Point(
                _windowPressLocation.X + (screen.X - _windowPressScreen.X),
                _windowPressLocation.Y + (screen.Y - _windowPressScreen.Y));
            UpdateDockHint(screen);
            return;
        }

        if (DragBar.Surface is { } surface)
        {
            var screen = ToScreen(e);
            var scale = surface.DpiScale;
            DetachablePanel.Transform = new Matrix(
                1f, 0f, 0f, 1f,
                (screen.X - _pagePressScreen.X) / scale,
                (screen.Y - _pagePressScreen.Y) / scale);
        }
    }

    private void OnDragBarReleased(object? sender, VisualPointerEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        var moved = _moved;
        _moved = false;

        if (_isDetached)
        {
            var screen = ToScreen(e);
            if (moved && DockSlotScreenBounds().Contains(screen))
            {
                DockBack();
            }
            else
            {
                UpdateDockHint(null);
            }

            return;
        }

        if (moved)
        {
            DetachablePanel.Transform = new Matrix();
            DetachAt(ToScreen(e));
            return;
        }

        DetachablePanel.Transform = new Matrix();
    }

    private void OnDetachClicked(object? sender, EventArgs e)
    {
        if (_isDetached)
        {
            DockBack();
            return;
        }

        DetachAt(Cursor.Position);
    }

    // 面板在页里时只记录拖动；松手超过阈值才创建窗口，指针捕获始终留在同一个宿主里。
    private void DetachAt(Point screenPoint)
    {
        if (_isDetached || _disposed)
        {
            return;
        }

        DetachablePanel.Transform = new Matrix();
        DockSlot.Child = null;
        DockPlaceholder.Visibility = XamlVisibility.Visible;
        DetachButton.Text = "DOCK";
        _isDetached = true;

        var window = new Window
        {
            Title = "Line status — detached",
            WidthDips = 340,
            HeightDips = 330,
            StartPosition = FormStartPosition.Manual,
            ShowInTaskbar = false,
            Location = new Point(screenPoint.X - 70, screenPoint.Y - 20),
            Padding = new Padding(6),
        };
        if (Surface?.FindForm() is { } owner)
        {
            window.Owner = owner;
        }

        window.DataContext = DataContext;
        window.FormClosing += OnWindowClosing;
        window.Content = DetachablePanel;
        _window = window;
        window.Show();
        window.Activate();
    }

    private void DockBack()
    {
        if (!_isDetached)
        {
            return;
        }

        var window = _window;
        _window = null;
        _isDetached = false;
        if (window is not null)
        {
            window.FormClosing -= OnWindowClosing;
        }

        DetachablePanel.ParentElement?.RemoveChild(DetachablePanel);
        if (window is not null)
        {
            window.Content = null;
        }

        DockSlot.Child = DetachablePanel;
        DetachablePanel.Transform = new Matrix();
        DockPlaceholder.Visibility = XamlVisibility.Collapsed;
        DetachButton.Text = "DETACH";
        UpdateDockHint(null);

        if (window is not null)
        {
            window.Close();
            window.Dispose();
        }
    }

    private void OnWindowClosing(object? sender, FormClosingEventArgs e)
    {
        if (_disposed
            || e.CloseReason is CloseReason.WindowsShutDown
                or CloseReason.ApplicationExitCall
                or CloseReason.FormOwnerClosing
                or CloseReason.TaskManagerClosing)
        {
            return;
        }

        e.Cancel = true;
        DockBack();
    }

    private void UpdateDockHint(Point? screen)
    {
        var hot = screen is { } point && DockSlotScreenBounds().Contains(point);
        DockPlaceholder.BorderBrush = hot
            ? Resources["Signal"] as XamlBrush
            : _idleDockBorder;
        DockPlaceholder.Background = hot
            ? Resources["SignalWash"] as XamlBrush
            : _idleDockBackground;
    }

    // 页面里的槽位仍在树上；用它当放下目标，再按宿主 DPI 换算成屏幕像素。
    private Rectangle DockSlotScreenBounds()
    {
        if (DockSlot.Surface is not { } surface)
        {
            return Rectangle.Empty;
        }

        var bounds = DockSlot.AbsoluteBounds;
        var scale = surface.DpiScale;
        var origin = surface.PointToScreen(new Point(
            (int)Math.Round(bounds.X * scale),
            (int)Math.Round(bounds.Y * scale)));
        return new Rectangle(
            origin,
            new Size(
                (int)Math.Round(bounds.Width * scale),
                (int)Math.Round(bounds.Height * scale)));
    }

    private Point ToScreen(VisualPointerEventArgs e)
    {
        if (DragBar.Surface is not { } surface)
        {
            return Cursor.Position;
        }

        var scale = surface.DpiScale;
        return surface.PointToScreen(new Point(
            (int)Math.Round(e.Position.X * scale),
            (int)Math.Round(e.Position.Y * scale)));
    }

    private static bool IsInside(VisualElement element, PointF surfacePoint)
        => element.AbsoluteBounds.Contains(surfacePoint.X, surfacePoint.Y);

    private static double Distance(PointF left, PointF right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
