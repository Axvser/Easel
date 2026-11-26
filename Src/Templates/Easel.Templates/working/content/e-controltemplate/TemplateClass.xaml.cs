using Easel;

// Easel customization: add keyed ControlTemplates; parts are read with GetTemplateChild<T>.

namespace TemplateNamespace;

/// <summary>A keyed ControlTemplate resource dictionary.</summary>
public sealed partial class TemplateClass : ResourceDictionary
{
    /// <summary>Creates the dictionary and runs the generated initializer.</summary>
    public TemplateClass()
    {
        InitializeComponent();
    }
}
