using Easel;

// Easel customization: add keyed DataTemplates for items, list rows and DataGrid cells.

namespace TemplateNamespace;

/// <summary>A keyed DataTemplate resource dictionary.</summary>
public sealed partial class TemplateClass : ResourceDictionary
{
    /// <summary>Creates the dictionary and runs the generated initializer.</summary>
    public TemplateClass()
    {
        InitializeComponent();
    }
}
