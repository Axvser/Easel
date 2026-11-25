using Easel;

namespace TemplateNamespace;

/// <summary>A XAML resource dictionary whose keys views resolve with StaticResource or DynamicResource.</summary>
public sealed partial class TemplateClass : ResourceDictionary
{
    /// <summary>Creates the dictionary and runs the generated initializer.</summary>
    public TemplateClass()
    {
        InitializeComponent();
    }
}
