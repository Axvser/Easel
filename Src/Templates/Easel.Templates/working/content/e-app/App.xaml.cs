using System.Windows.Forms;
using Easel;
using Easel.Visuals;

namespace TemplateClass;

/// <summary>Application resources, startup configuration and entry point.</summary>
public sealed partial class App : ResourceDictionary
{
    /// <summary>Creates the application resources and runs the generated initializer.</summary>
    public App()
    {
        InitializeComponent();
    }

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var app = new App();
        app.ApplyStartupTheme();
        EaselApplication.Resources.MergedDictionaries.Add(app);

        Application.Run(new MainForm());
    }

    private void ApplyStartupTheme()
    {
        if (TryGetValue("StartupTheme", out var value)
            && value is string text
            && Enum.TryParse<ThemeMode>(text, ignoreCase: true, out var mode))
        {
            Theme.Mode = mode;
        }
    }
}
