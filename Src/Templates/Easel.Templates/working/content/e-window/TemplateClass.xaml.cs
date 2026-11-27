// Easel customization: assign a DataContext here when the window needs a view model.

using Easel.Visuals;

namespace TemplateNamespace;

/// <summary>A XAML-declared window that runs with <c>Application.Run</c>.</summary>
public sealed partial class TemplateClass : Window
{
    /// <summary>Creates the window and runs the generated initializer.</summary>
    public TemplateClass()
    {
        InitializeComponent();
    }
}
