using System.Windows.Input;

namespace IndustrialWorkbench.ViewModels;

/// <summary>A small <see cref="ICommand"/> for the status-bar buttons.</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    /// <summary>Creates a command over <paramref name="execute"/>.</summary>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>Raises <see cref="CanExecuteChanged"/>.</summary>
    public void Notify() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
