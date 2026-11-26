using Easel;

// Easel customization: this is a partial overlay; merge it after the built-in theme and override only the keys you need.

namespace TemplateNamespace;

/// <summary>A partial theme overlay for an Easel application.</summary>
public sealed partial class TemplateClass : ResourceDictionary
{
    /// <summary>Creates the dictionary and runs the generated initializer.</summary>
    public TemplateClass()
    {
        InitializeComponent();
    }
}
