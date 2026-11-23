using Demo.ViewModels;

namespace Demo.Views.Cards;

/// <summary>The settings page; the fields are XAML controls bound two-way to the view model.</summary>
public sealed partial class SettingsView : ThemedDockPanel
{
    /// <summary>Creates the page, attaches the view model and fills the language selector.</summary>
    public SettingsView()
    {
        DataContext = new SettingsViewModel();
        InitializeComponent();

        language.Items.Add("System default");
        language.Items.Add("English (US)");
        language.Items.Add("中文（简体）");
        language.SelectedIndex = 1;
    }
}
