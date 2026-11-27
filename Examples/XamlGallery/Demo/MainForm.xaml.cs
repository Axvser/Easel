using Easel.Visuals;

namespace Demo;

/// <summary>The gallery window; the session and its shell are declared in XAML.</summary>
public sealed partial class MainForm : Window
{
    public MainForm() => InitializeComponent();

    /// <summary>The composition root the XAML declared as this window's data context.</summary>
    internal GallerySession Session => (GallerySession?)DataContext
        ?? throw new InvalidOperationException("The window was built without a gallery session.");
}
