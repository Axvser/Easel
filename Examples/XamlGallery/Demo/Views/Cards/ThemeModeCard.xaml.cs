namespace Demo.Views;

/// <summary>The theme-mode card; the button cycles the injected palette service.</summary>
internal sealed partial class ThemeModeCard : ThemedStackPanel
{
    private readonly IThemeService _theme;

    /// <summary>Creates the view and shows the current palette mode.</summary>
    /// <param name="theme">The palette service the button cycles.</param>
    public ThemeModeCard(IThemeService theme)
    {
        _theme = theme;
        InitializeComponent();

        modeLabel.Text = $"Current mode: {_theme.Mode}";
    }

    private void OnSwitchPaletteClick(object? sender, EventArgs e)
        => _theme.SelectIndex((_theme.SelectedIndex + 1) % _theme.Options.Count);
}
