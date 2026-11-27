using System.Drawing;
using System.Windows.Forms;
using Easel.Visuals;
using IndustrialWorkbench.ViewModels;

namespace IndustrialWorkbench;

/// <summary>The workbench window; the session model and shell are declared in XAML.</summary>
public sealed partial class MainForm : Window
{
    /// <summary>Creates the window, sizes it for the primary screen and runs the initializer.</summary>
    public MainForm()
    {
        InitializeComponent();
        StartPosition = FormStartPosition.CenterScreen;
        // MinimumSize first: it changes the physical size, and WidthDips only applies at handle creation
        // when the physical size is still the one it recorded.
        MinimumSize = new Size(1024, 680);
        WidthDips = 1500;
        HeightDips = 930;
    }

    /// <summary>The simulation model the XAML declared as this window's data context.</summary>
    internal WorkbenchViewModel Model => DataContext as WorkbenchViewModel
        ?? throw new InvalidOperationException("The workbench window was built without a model.");
}
