using Easel;
using Easel.Visuals.Controls;

namespace Demo.Views;

/// <summary>A fully visual, XAML-templated text field with the native editor island turned off.</summary>
public sealed partial class CustomTextBox : ThemedStackPanel
{
    /// <summary>Creates the view and runs the generated initializer.</summary>
    public CustomTextBox()
    {
        InitializeComponent();
    }
}
