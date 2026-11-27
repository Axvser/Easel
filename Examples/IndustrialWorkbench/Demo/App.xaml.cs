using System.Windows.Forms;
using Easel;
using Easel.Visuals;

namespace IndustrialWorkbench;

/// <summary>Application resources, startup configuration and entry point.</summary>
public sealed partial class App : ResourceDictionary
{
    /// <summary>Creates the application resources and runs the generated initializer.</summary>
    public App()
    {
        InitializeComponent();
    }

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var app = new App();
        app.ApplyStartupTheme(args);
        EaselApplication.Resources.MergedDictionaries.Add(app);
        Application.SetColorMode(
            Theme.EffectiveMode == ThemeMode.Light ? SystemColorMode.Classic : SystemColorMode.Dark);

        using var form = new MainForm();
        WorkbenchCapture.Configure(form, args);
        Application.Run(form);
    }

    // --light / --contrast 覆盖 App.xaml 的 StartupTheme。
    private void ApplyStartupTheme(string[] args)
    {
        Theme.Mode = args.Contains("--contrast", StringComparer.OrdinalIgnoreCase) ? ThemeMode.HighContrast
            : args.Contains("--light", StringComparer.OrdinalIgnoreCase) ? ThemeMode.Light
            : ReadStartupTheme();
    }

    private ThemeMode ReadStartupTheme()
        => TryGetValue("StartupTheme", out var value)
           && value is string text
           && Enum.TryParse<ThemeMode>(text, ignoreCase: true, out var mode)
            ? mode
            : ThemeMode.Dark;
}
