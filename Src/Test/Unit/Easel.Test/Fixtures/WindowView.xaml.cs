using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test.Fixtures;

/// <summary>The code-behind of the XAML window fixture: the root element is the window itself.</summary>
public partial class WindowView : Window
{
    /// <summary>Creates the window and runs the generated initializer.</summary>
    public WindowView()
    {
        DataContext = new WindowViewModel { Greeting = "Hello from the XAML window" };
        InitializeComponent();
    }

    /// <summary>The generated content field.</summary>
    public VC.StackPanel LayoutRoot => layoutRoot;

    /// <summary>The generated text field.</summary>
    public VC.TextBlock GreetingText => greetingText;
}

/// <summary>The data context used by the XAML window fixture.</summary>
public sealed class WindowViewModel
{
    /// <summary>The greeting rendered by the fixture.</summary>
    public string Greeting { get; set; } = "";
}
