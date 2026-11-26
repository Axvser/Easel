using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;

namespace Demo;

/// <summary>The XAML window that hosts the visual-backend gallery surface.</summary>
public sealed partial class GalleryForm : Window
{
    private readonly GalleryModel _model;
    private readonly IThemeService _theme;
    private readonly GalleryCardCatalog _catalog;
    private readonly GalleryShellViewModel _vm;
    private GalleryVisualShell _shell;
    private static int _mediaWarmupStarted;

    /// <summary>Creates the window and docks the single visual surface into it.</summary>
    /// <param name="args">Command-line arguments forwarded from <see cref="Program"/>.</param>
    public GalleryForm(string[] args)
    {
        _ = args;
        _model = new GalleryModel();
        _theme = new ThemeService();
        _catalog = new GalleryCardCatalog(_model, _theme);
        _vm = new GalleryShellViewModel(_catalog.Infos, _theme);

        InitializeComponent();

        var work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1440, 900);
        ClientSize = new Size(
            Math.Max(1100, work.Width - 80),
            Math.Max(640, work.Height - 100));
        MinimumSize = new Size(1100, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Current.WindowBackground;

        _shell = new GalleryVisualShell(_model, _vm, _catalog);
        Content = _shell.Root;
        _theme.Changed += OnThemeChanged;
    }

    /// <summary>The visual shell (catalog, preview, inspector).</summary>
    internal GalleryVisualShell Shell => _shell;

    /// <summary>Selects a card by id.</summary>
    /// <param name="id">The card id.</param>
    public bool SelectEntry(string id)
    {
        var selected = _shell.SelectEntry(id);
        Surface.InvalidateSurface();
        return selected;
    }

    /// <summary>Scrolls the preview viewport.</summary>
    /// <param name="offset">The vertical offset in pixels.</param>
    public void SetScrollOffset(float offset) => _shell.SetPreviewScrollOffset(offset);

    /// <summary>Hides the gallery chrome so only the preview is shown.</summary>
    /// <param name="visible">Whether the chrome is shown.</param>
    public void SetChromeVisible(bool visible) => _shell.SetChromeVisible(visible);

    /// <summary>Switches the palette; used by capture mode to verify the visual rebuild.</summary>
    public void ToggleThemeForCapture()
        => _theme.SelectIndex((_theme.SelectedIndex + 1) % _theme.Options.Count);

    /// <summary>Selects one palette option; used by capture mode to walk a fixed theme sequence.</summary>
    /// <param name="index">The option index (0 = Dark+, 1 = Light+, 2 = High contrast, 3 = Follow system).</param>
    public void SelectThemeForCapture(int index) => _theme.SelectIndex(index);

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTitleBarTheme();
    }

    /// <inheritdoc />
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ApplyTitleBarTheme();
        WarmUpMediaStack();
        Surface.InvalidateSurface();
        BeginInvoke(new Action(() =>
        {
            if (!IsDisposed)
            {
                Surface.Refresh();
            }
        }));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _theme.Changed -= OnThemeChanged;
            _theme.Dispose();
            _vm.Dispose();
        }

        base.Dispose(disposing);
    }

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

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        BackColor = Theme.Current.WindowBackground;
        Surface.BackColor = Theme.Current.WindowBackground;
        Demo.Views.GalleryTheme.RefreshPalette();
        _shell.RefreshTheme();
        Surface.InvalidateSurface();
        ApplyTitleBarTheme();
    }

    private void RebuildShell()
    {
        BackColor = Theme.Current.WindowBackground;
        Surface.BackColor = Theme.Current.WindowBackground;

        var previousRoot = Surface.Root;
        var previousShell = _shell;
        _shell = new GalleryVisualShell(_model, _vm, _catalog);
        Content = _shell.Root;
        if (IsHandleCreated)
        {
            // 事件处理器可能就在被替换的树里；等这一手返回后再释放旧根和旧壳。
            BeginInvoke(new Action(() =>
            {
                if (!ReferenceEquals(previousRoot, previousShell))
                {
                    (previousRoot as IDisposable)?.Dispose();
                }

                previousShell.Dispose();
            }));
        }
        else
        {
            if (!ReferenceEquals(previousRoot, previousShell))
            {
                (previousRoot as IDisposable)?.Dispose();
            }

            previousShell.Dispose();
        }

        Surface.InvalidateSurface();
        ApplyTitleBarTheme();
    }

    // WinForms 的暗色模式不一定改 DWM 标题栏；显式按当前调色板设置，浅色模式才不会留一条黑标题栏。
    private void ApplyTitleBarTheme()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        var dark = Theme.EffectiveMode == ThemeMode.Light ? 0 : 1;
        _ = DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        _ = DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
