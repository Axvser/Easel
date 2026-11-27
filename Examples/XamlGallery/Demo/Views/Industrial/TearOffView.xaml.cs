using System.Drawing;
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
    private Point _windowPressScreen;
    private Point _windowPressLocation;

    /// <summary>Creates the page and starts the line simulator.</summary>
    public TearOffView()
    {
        _viewModel = new ProductionLineViewModel();
        DataContext = _viewModel;
        InitializeComponent();
        _idleDockBorder = DockPlaceholder.BorderBrush;
        _idleDockBackground = DockPlaceholder.Background;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>The floating window while the panel is detached, or <see langword="null"/>.</summary>
    internal Window? DetachedWindow => _window;

    /// <summary>Tears the panel out to <paramref name="screenPoint"/>; used by the capture runner.</summary>
    internal void DetachForCapture(Point screenPoint) => DetachAt(screenPoint);

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
            DetachAt(ToScreen(e));
        }
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
            ? Resources["Raised"] as XamlBrush
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
