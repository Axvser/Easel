using System.ComponentModel;

namespace Demo.Views;

/// <summary>
/// The native-element card: a <c>native:</c> GroupBox whose content is a visual StackPanel with a native button
/// bound back to this view's <see cref="ButtonText"/>.
/// </summary>
public sealed partial class NativeElementsCard : ThemedStackPanel, INotifyPropertyChanged
{
    private int _clicks;
    private PropertyChangedEventHandler? _propertyChanged;

    /// <summary>Creates the view and runs the generated initializer.</summary>
    public NativeElementsCard()
    {
        DataContext = this;
        InitializeComponent();
    }

    /// <summary>The text the native button binds to; it changes after every click.</summary>
    public string ButtonText => _clicks == 0 ? "Native button" : $"Native button ({_clicks})";

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => _propertyChanged += value;
        remove => _propertyChanged -= value;
    }

    private void OnNativeClick(object? sender, System.EventArgs e)
    {
        _clicks++;
        _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ButtonText)));
        clickLabel.Text = $"Clicks: {_clicks}";
    }
}
