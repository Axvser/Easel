using System.Windows.Forms;
using Easel;

namespace Demo;

/// <summary>Owns the requested palette mode and the five demo choices; forwards system-theme changes.</summary>
public interface IThemeService : IDisposable
{
    /// <summary>The underlying theme mode; the custom Midnight theme applies over <see cref="ThemeMode.Dark"/>.</summary>
    ThemeMode Mode { get; }

    /// <summary>The palette mode actually in use (System resolves to Dark+/Light+/HighContrast).</summary>
    ThemeMode EffectiveMode { get; }

    /// <summary>The selector labels: Dark+, Light+, High contrast, Midnight, Follow system.</summary>
    IReadOnlyList<string> Options { get; }

    /// <summary>The selector index for the current choice; a custom theme keeps its own index.</summary>
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
    private static readonly Themes.MidnightTheme CustomTheme = new();
    private int _selectedIndex;
    private bool _disposed;

    /// <summary>Creates the service and starts forwarding <see cref="Theme.Changed"/>.</summary>
    public ThemeService()
    {
        _selectedIndex = Theme.Mode switch
        {
            ThemeMode.Light => 1,
            ThemeMode.HighContrast => 2,
            ThemeMode.System => 4,
            _ => 0,
        };
        Theme.Changed += OnThemeChanged;
    }

    /// <inheritdoc />
    public ThemeMode Mode => Theme.Mode;

    /// <inheritdoc />
    public ThemeMode EffectiveMode => Theme.EffectiveMode;

    /// <inheritdoc />
    public IReadOnlyList<string> Options { get; } =
        ["Dark+", "Light+", "High contrast", "Midnight", "Follow system"];

    /// <inheritdoc />
    public int SelectedIndex => _selectedIndex;

    /// <inheritdoc />
    public void SelectIndex(int index)
    {
        if (index < 0 || index >= Options.Count)
        {
            return;
        }

        _selectedIndex = index;
        if (index == 3)
        {
            // The demo owns this palette; the other modes use the library's XAML themes.
            ApplyCustomTheme();
            return;
        }

        Demo.Views.GalleryTheme.UseCustom(null);
        switch (index)
        {
            case 0:
                Theme.ApplyPalette(ThemeMode.Dark, Theme.CreateDark());
                break;
            case 1:
                Theme.ApplyPalette(ThemeMode.Light, Theme.CreateLight());
                break;
            case 2:
                Theme.ApplyPalette(ThemeMode.HighContrast, Theme.CreateHighContrast());
                break;
            case 4:
                Theme.Mode = ThemeMode.System;
                break;
        }
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

    private static void ApplyCustomTheme()
    {
        Demo.Views.GalleryTheme.UseCustom(CustomTheme);
        Theme.ApplyPalette(ThemeMode.Dark, ThemePalette.FromResourceDictionary(CustomTheme));
    }
}
