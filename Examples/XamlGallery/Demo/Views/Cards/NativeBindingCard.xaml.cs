using System.ComponentModel;

namespace Demo.Views;

/// <summary>A native TextBox bound two-way to <see cref="Name"/>; the visual TextBlock shows the same source.</summary>
public sealed partial class NativeBindingCard : ThemedStackPanel, INotifyPropertyChanged
{
    private string _name = "type here";
    private PropertyChangedEventHandler? _propertyChanged;

    /// <summary>Creates the view and runs the generated initializer.</summary>
    public NativeBindingCard()
    {
        DataContext = this;
        InitializeComponent();
    }

    /// <summary>The source both the native field and the visual label bind to.</summary>
    public string Caption
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Caption)));
        }
    }

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => _propertyChanged += value;
        remove => _propertyChanged -= value;
    }
}
