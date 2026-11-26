using System.Collections.ObjectModel;
using Demo.Mvvm;

namespace Demo.ViewModels;

/// <summary>One row of the MVVM card's task list.</summary>
public sealed class TaskBoardItem
{
    /// <summary>Creates a row with the given title.</summary>
    /// <param name="title">The text shown in the row.</param>
    public TaskBoardItem(string title) => Title = title;

    /// <summary>The row's title.</summary>
    public string Title { get; }

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>
/// The MVVM card's view model: a draft, a selected row and commands the XAML binds to.
/// </summary>
public sealed class TaskBoardViewModel : ObservableObject
{
    private string _draft = string.Empty;
    private TaskBoardItem? _selected;
    private string _status = "Ready";

    /// <summary>Creates the sample rows and selects the first one.</summary>
    public TaskBoardViewModel()
    {
        AddCommand = new RelayCommand(_ => Add(), _ => CanExecuteAddCommand(_));
        RemoveCommand = new RelayCommand(_ => Remove(), _ => CanExecuteRemoveCommand(_));
        ClearCommand = new RelayCommand(_ => Clear());
        Tasks.Add(new TaskBoardItem("Bind Text to Draft"));
        Tasks.Add(new TaskBoardItem("Run AddCommand"));
        Tasks.Add(new TaskBoardItem("Remove drops the selected row"));
        Selected = Tasks[0];
        Status = $"{Tasks.Count} tasks · select one, then Remove";
    }

    /// <summary>The draft title for a new task.</summary>
    public string Draft
    {
        get => _draft;
        set
        {
            if (SetProperty(ref _draft, value))
            {
                AddCommand.Notify();
            }
        }
    }

    /// <summary>The selected row.</summary>
    public TaskBoardItem? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                RemoveCommand.Notify();
            }
        }
    }

    /// <summary>The status line.</summary>
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    /// <summary>The rows; the list box tracks this observable collection.</summary>
    public ObservableCollection<TaskBoardItem> Tasks { get; } = [];

    /// <summary>Adds the draft as a task.</summary>
    public RelayCommand AddCommand { get; }

    /// <summary>Removes the selected task.</summary>
    public RelayCommand RemoveCommand { get; }

    /// <summary>Clears every task.</summary>
    public RelayCommand ClearCommand { get; }

    private void Add()
    {
        var title = Draft.Trim();
        if (title.Length == 0)
        {
            Status = "Type a title first.";
            return;
        }

        var item = new TaskBoardItem(title);
        Tasks.Add(item);
        Selected = item;
        Draft = string.Empty;
        Status = $"Added '{title}' · {Tasks.Count} tasks";
    }

    private bool CanExecuteAddCommand(object? parameter) => !string.IsNullOrWhiteSpace(_draft);

    private void Remove()
    {
        if (Selected is null)
        {
            Status = "Select a task first.";
            return;
        }

        var title = Selected.Title;
        Tasks.Remove(Selected);
        Selected = Tasks.Count > 0 ? Tasks[0] : null;
        Status = $"Removed '{title}' · {Tasks.Count} tasks";
    }

    private bool CanExecuteRemoveCommand(object? parameter) => _selected is not null;

    private void Clear()
    {
        Tasks.Clear();
        Selected = null;
        Status = "Cleared";
    }
}
