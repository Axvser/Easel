namespace Easel.Themes;

/// <summary>The built-in Light+ theme as XAML resources; <see cref="Theme"/> reads its colours and styles.</summary>
internal sealed partial class LightTheme : ResourceDictionary
{
    /// <summary>Creates the dictionary and runs the generated initializer.</summary>
    public LightTheme()
    {
        InitializeComponent();
    }
}
