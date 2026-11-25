// Easel customization: assign a DataContext here when the view needs a view model.

namespace TemplateNamespace;

/// <summary>A XAML view hosted by a <see cref="Easel.Visuals.VisualHost"/>.</summary>
public sealed partial class TemplateClass : Easel.Visuals.Controls.UserControl
{
    /// <summary>Creates the view and runs the generated initializer.</summary>
    public TemplateClass()
    {
        InitializeComponent();
    }
}
