using Easel;

namespace Demo.Themes;

/// <summary>A demo-defined custom theme; it shows how an application owns its own resource dictionary.</summary>
public sealed partial class HighContrastTheme : ResourceDictionary
{
    /// <summary>Creates the dictionary and runs the generated initializer.</summary>
    public HighContrastTheme()
    {
        InitializeComponent();
    }
}
