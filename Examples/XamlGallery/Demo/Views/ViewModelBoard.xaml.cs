using Easel;
using Easel.Visuals.Controls;
using Demo.ViewModels;

namespace Demo.Views;

/// <summary>A XAML board bound to a generated view model and its generated commands.</summary>
public sealed partial class ViewModelBoard : ThemedStackPanel
{
    /// <summary>Creates the board and binds it to a fresh view model.</summary>
    public ViewModelBoard()
    {
        DataContext = new TaskBoardViewModel();
        InitializeComponent();
    }
}
