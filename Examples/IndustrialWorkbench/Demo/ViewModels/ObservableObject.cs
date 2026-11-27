using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IndustrialWorkbench.ViewModels;

/// <summary>A minimal <see cref="INotifyPropertyChanged"/> base for the hand-written view models.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Assigns <paramref name="field"/> and raises the change when the value differs.</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Raises <see cref="PropertyChanged"/> for <paramref name="propertyName"/>.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
