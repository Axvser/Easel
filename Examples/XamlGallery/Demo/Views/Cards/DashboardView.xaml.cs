using Demo.ViewModels;

namespace Demo.Views.Cards;

/// <summary>The analytics page; every visual is declared in XAML and bound to the dashboard view model.</summary>
public sealed partial class DashboardView : ThemedDockPanel
{
    /// <summary>Creates the page and attaches its view model.</summary>
    public DashboardView()
    {
        DataContext = new DashboardViewModel();
        InitializeComponent();
    }
}
