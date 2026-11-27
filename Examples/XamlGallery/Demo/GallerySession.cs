using System.Drawing;
using System.Windows.Forms;
using Demo.Views;
using Easel;
using Easel.Visuals;

namespace Demo;

/// <summary>
/// The gallery's composition root and window lifecycle. It owns the sample data, palette, catalog and shell state,
/// sizes the hosting window, and exposes the automation entry points the capture runner uses.
/// </summary>
internal sealed class GallerySession : IDisposable
{
    private static int _mediaWarmupStarted;
    private GalleryVisualShell? _shell;
    private Window? _window;
    private bool _disposed;

    /// <summary>Creates the services and selects the initial card.</summary>
    public GallerySession()
    {
        Model = new GalleryModel();
        Theme = new ThemeService();
        Catalog = new GalleryCardCatalog(Model, Theme);
        ViewModel = new GalleryShellViewModel(Catalog.Infos, Theme);
        Theme.Changed += OnThemeChanged;
    }

    /// <summary>The sample data every card binds to.</summary>
    public GalleryModel Model { get; }

    /// <summary>The palette service.</summary>
    public IThemeService Theme { get; }

    /// <summary>The card metadata/factory pairs.</summary>
    public GalleryCardCatalog Catalog { get; }

    /// <summary>The shell state.</summary>
    public GalleryShellViewModel ViewModel { get; }

    /// <summary>Every card in catalog order.</summary>
    public IReadOnlyList<GalleryCard> Cards => _shell?.Cards ?? [];

    /// <summary>Called by the shell once it resolves this session as its inherited data context.</summary>
    public void AttachShell(GalleryVisualShell shell)
    {
        if (ReferenceEquals(_shell, shell))
        {
            return;
        }

        _shell = shell;
        if (shell.Surface?.FindForm() is not Window window || ReferenceEquals(window, _window))
        {
            return;
        }

        _window = window;

        // 按主屏工作区（DIP）留边；Window 在句柄创建时按显示器 DPI 换算成物理像素。
        var work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1440, 900);
        using (var screenDpi = Graphics.FromHwnd(IntPtr.Zero))
        {
            var scale = screenDpi.DpiX / 96f;
            window.WidthDips = Math.Max(1100, (work.Width - 80) / scale);
            window.HeightDips = Math.Max(640, (work.Height - 100) / scale);
            window.MinimumSize = new Size((int)Math.Round(1100 * scale), (int)Math.Round(640 * scale));
        }

        window.StartPosition = FormStartPosition.CenterScreen;
        window.Shown += OnWindowShown;
        window.Disposed += OnWindowDisposed;
    }

    /// <summary>Selects a card and repaints the shell.</summary>
    /// <param name="id">The card id.</param>
    public bool SelectEntry(string id)
    {
        if (_shell is not { } shell)
        {
            return false;
        }

        var selected = shell.SelectEntry(id);
        shell.Surface?.InvalidateSurface();
        return selected;
    }

    /// <summary>Scrolls the preview viewport.</summary>
    /// <param name="offset">The vertical offset in pixels.</param>
    public void SetScrollOffset(float offset) => _shell?.SetPreviewScrollOffset(offset);

    /// <summary>Hides the gallery chrome so only the preview is shown.</summary>
    /// <param name="visible">Whether the chrome is shown.</param>
    public void SetChromeVisible(bool visible) => _shell?.SetChromeVisible(visible);

    /// <summary>Switches to the next palette; used by capture mode.</summary>
    public void ToggleTheme() => Theme.SelectIndex((Theme.SelectedIndex + 1) % Theme.Options.Count);

    /// <summary>Selects one palette option; used by capture mode to walk a fixed theme sequence.</summary>
    /// <param name="index">The option index (0 = Dark+, 1 = Light+, 2 = High contrast, 3 = Midnight, 4 = Follow system).</param>
    public void SelectTheme(int index) => Theme.SelectIndex(index);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Theme.Changed -= OnThemeChanged;
        if (_window is { } window)
        {
            window.Shown -= OnWindowShown;
            window.Disposed -= OnWindowDisposed;
        }

        Theme.Dispose();
        ViewModel.Dispose();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        Views.GalleryTheme.RefreshPalette();
        _shell?.RefreshTheme();
        _shell?.Surface?.InvalidateSurface();
    }

    private void OnWindowShown(object? sender, EventArgs e)
    {
        if (sender is Form form)
        {
            form.Shown -= OnWindowShown;
        }

        WarmUpMediaStack();
        _shell?.Surface?.InvalidateSurface();
    }

    private void OnWindowDisposed(object? sender, EventArgs e) => Dispose();

    // 第一次创建 WPF 媒体宿主会初始化一次性的互操作/媒体栈（约 200ms）；放到后台 STA 线程预热，换页不再付这笔钱。
    private static void WarmUpMediaStack()
    {
        if (Interlocked.Exchange(ref _mediaWarmupStarted, 1) != 0)
        {
            return;
        }

        var thread = new Thread(static () =>
        {
            try
            {
                var media = new System.Windows.Controls.MediaElement
                {
                    LoadedBehavior = System.Windows.Controls.MediaState.Manual,
                    UnloadedBehavior = System.Windows.Controls.MediaState.Manual,
                };
                using var host = new System.Windows.Forms.Integration.ElementHost { Child = media };
                var player = new System.Windows.Media.MediaPlayer();
                player.Close();
                host.Child = null;
                media.Close();
            }
            catch (Exception)
            {
                // 预热失败只影响速度；页面仍会在自己的线程上完成初始化。
            }
        })
        {
            IsBackground = true,
            Name = "Easel media warmup",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }
}
