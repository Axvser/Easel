namespace Demo.Views;

/// <summary>The button and repeat-button card; only the click counters are wired here.</summary>
public sealed partial class ButtonsCard : ThemedStackPanel
{
    private int _clicks;

    /// <summary>Creates the view and runs the generated initializer.</summary>
    public ButtonsCard() => InitializeComponent();

    private void OnRunClick(object? sender, EventArgs e) => status.Text = $"Clicks: {++_clicks}";

    private void OnRepeatClick(object? sender, EventArgs e) => status.Text = $"Repeat ticks: {++_clicks}";
}
