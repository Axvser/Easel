using Demo.ViewModels;

namespace Demo.Views.Cards;

/// <summary>The chat page; the bubbles come from an item template and the composer is a two-way binding.</summary>
public sealed partial class ChatView : ThemedDockPanel
{
    /// <summary>Creates the page and attaches the view model.</summary>
    public ChatView()
    {
        DataContext = new ChatViewModel();
        InitializeComponent();
    }
}
