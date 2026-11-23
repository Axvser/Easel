namespace Easel.Themes;

/// <summary>The built-in Dark+ theme as XAML resources; <see cref="Theme"/> reads its colours and styles.</summary>
internal sealed partial class DarkTheme : ResourceDictionary
{
    /// <summary>Creates the dictionary and runs the generated initializer.</summary>
    public DarkTheme()
    {
        InitializeComponent();
    }
}
