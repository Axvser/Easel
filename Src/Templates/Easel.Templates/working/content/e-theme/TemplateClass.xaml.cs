using Easel;

// Easel customization: change the palette, typography and keyed styles; the palette keys feed ThemePalette.FromResourceDictionary.

namespace TemplateNamespace;

/// <summary>A global theme resource dictionary for an Easel application.</summary>
public sealed partial class TemplateClass : ResourceDictionary
{
    /// <summary>Creates the dictionary and runs the generated initializer.</summary>
    public TemplateClass()
    {
        InitializeComponent();
    }
}
