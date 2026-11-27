using System.ComponentModel;

namespace Demo.Views;

/// <summary>A native TextBox bound two-way to <see cref="Note"/>; the visual preview shows the same source.</summary>
public sealed partial class NativeBindingCard : ThemedStackPanel, INotifyPropertyChanged
{
    private string _note = "Call the customer before the noon cut-off.";
    private PropertyChangedEventHandler? _propertyChanged;

    /// <summary>Creates the view and runs the generated initializer.</summary>
    public NativeBindingCard()
    {
        DataContext = this;
        InitializeComponent();
    }

    /// <summary>The source both the native field and the visual preview bind to.</summary>
    public string Note
    {
        get => _note;
        set
        {
            if (_note == value)
            {
                return;
            }

            _note = value;
            _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Note)));
        }
    }

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => _propertyChanged += value;
        remove => _propertyChanged -= value;
    }
}
