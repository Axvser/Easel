using Easel;
using Easel.Visuals.Controls;

namespace Demo.Views;

/// <summary>The progress-bar card; the fill is bound to the slider in code-behind.</summary>
public sealed partial class ProgressCard : ThemedStackPanel
{
    /// <summary>Creates the view and binds the bar to the slider value.</summary>
    public ProgressCard()
    {
        InitializeComponent();

        bar.SetBinding(ProgressBar.ValueProperty, new Binding("Value") { Source = slider });
    }
}
