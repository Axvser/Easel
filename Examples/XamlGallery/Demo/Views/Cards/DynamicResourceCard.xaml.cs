using System.Drawing;
using Easel;

namespace Demo.Views;

/// <summary>The dynamic-resource card: a local key update reaches DynamicResource but not StaticResource.</summary>
public sealed partial class DynamicResourceCard : ThemedStackPanel
{
    private int _step;

    /// <summary>Creates the view and runs the generated initializer.</summary>
    public DynamicResourceCard()
    {
        InitializeComponent();
    }

    private void OnChangeClicked(object? sender, EventArgs e)
    {
        var theme = Theme.Current;
        Color[] colors = [theme.Accent, theme.FocusRing, theme.Selection, Color.FromArgb(0xFF, 0xFF, 0x6B, 0x35)];
        var color = colors[_step++ % colors.Length];
        Resources["Accent"] = new SolidColorBrush(color);
        status.Text = $"Local Accent = #{color.R:X2}{color.G:X2}{color.B:X2} · dynamic updated, static unchanged";
    }

    private void OnResetClicked(object? sender, EventArgs e)
    {
        Resources.Remove("Accent");
        status.Text = "Local override removed · the dynamic swatch falls back to the theme dictionary";
    }
}
