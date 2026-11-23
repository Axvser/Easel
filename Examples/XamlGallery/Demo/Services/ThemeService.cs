using System.Windows.Forms;
using Easel;

namespace Demo;

/// <summary>Owns the requested palette mode and the four demo choices; forwards system-theme changes.</summary>
public interface IThemeService : IDisposable
{
    /// <summary>The requested mode; <see cref="ThemeMode.System"/> follows Windows.</summary>
    ThemeMode Mode { get; }

    /// <summary>The palette mode actually in use (System resolves to Dark+/Light+/High contrast).</summary>
    ThemeMode EffectiveMode { get; }

    /// <summary>The selector labels: Dark+, Light+, High contrast, Follow system.</summary>
    IReadOnlyList<string> Options { get; }

    /// <summary>The selector index for the current requested mode.</summary>
    int SelectedIndex { get; }

    /// <summary>Selects one of <see cref="Options"/>.</summary>
    /// <param name="index">The option index; out-of-range values are ignored.</param>
    void SelectIndex(int index);

    /// <summary>Raised after the palette mode or the followed system theme changed.</summary>
    event EventHandler? Changed;
}

/// <summary>The gallery's palette service: sets the static theme and announces switches, including system follows.</summary>
public sealed class ThemeService : IThemeService
{
    private static readonly Themes.HighContrastTheme CustomContrast = new();
    private bool _disposed;

    /// <summary>Creates the service, starts forwarding <see cref="Theme.Changed"/> and applies the custom theme if
    /// the process started in high-contrast mode.</summary>
    public ThemeService()
    {
        Theme.Changed += OnThemeChanged;
        if (Theme.Mode == ThemeMode.HighContrast)
        {
            ApplyCustomContrast();
        }
    }

    /// <inheritdoc />
    public ThemeMode Mode => Theme.Mode;

    /// <inheritdoc />
    public ThemeMode EffectiveMode => Theme.EffectiveMode;

    /// <inheritdoc />
    public IReadOnlyList<string> Options { get; } =
        ["Dark+", "Light+", "High contrast", "Follow system"];

    /// <inheritdoc />
    public int SelectedIndex => Theme.Mode switch
    {
        ThemeMode.Light => 1,
        ThemeMode.HighContrast => 2,
        ThemeMode.System => 3,
        _ => 0,
    };

    /// <inheritdoc />
    public void SelectIndex(int index)
    {
        if (index == 2)
        {
            // The demo's high-contrast option is a custom ResourceDictionary, not a library fallback.
            ApplyCustomContrast();
            return;
        }

        Demo.Views.GalleryTheme.UseCustom(null);
        var mode = index switch
        {
            0 => ThemeMode.Dark,
            1 => ThemeMode.Light,
            3 => ThemeMode.System,
            _ => Mode,
        };
        Theme.Mode = mode;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Theme.Changed -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        // Native islands (the focused TextBox editor) only pick a colour mode for new handles.
        try
        {
            Application.SetColorMode(
                Theme.EffectiveMode == ThemeMode.Light ? SystemColorMode.Classic : SystemColorMode.Dark);
        }
        catch
        {
            // The session can reject a late colour-mode change; the visual palette still switched.
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static void ApplyCustomContrast()
    {
        Demo.Views.GalleryTheme.UseCustom(CustomContrast);
        Theme.ApplyPalette(ThemeMode.HighContrast, ThemePalette.FromResourceDictionary(CustomContrast));
    }
}
