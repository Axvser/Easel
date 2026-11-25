using Easel.Visuals;
using Easel.Visuals.Dialogs;

namespace Demo.Views;

/// <summary>The dialog card; each button opens a modal in-surface dialog on the hosting surface.</summary>
public sealed partial class DialogCard : ThemedStackPanel
{
    /// <summary>Creates the view and runs the generated initializer.</summary>
    public DialogCard() => InitializeComponent();

    private async void OnMessageClick(object? sender, EventArgs e)
    {
        if (Surface is not { } surface)
        {
            return;
        }

        var result = await DialogService.MessageAsync(
            surface,
            "Build finished",
            "Every target compiled and the test run stayed green.",
            DialogKind.Success);
        status.Text = result == DialogResult.Primary ? "Message dismissed" : "Message cancelled";
    }

    private async void OnConfirmClick(object? sender, EventArgs e)
    {
        if (Surface is not { } surface)
        {
            return;
        }

        var result = await DialogService.ConfirmAsync(
            surface,
            "Delete the draft?",
            "The file is removed from this project; it cannot be restored.",
            "Delete",
            "Keep",
            DialogKind.Warning);
        status.Text = $"Confirm returned {result}";
    }

    private async void OnPromptClick(object? sender, EventArgs e)
    {
        if (Surface is not { } surface)
        {
            return;
        }

        var text = await DialogService.PromptAsync(surface, "Rename", "New name", "Untitled");
        status.Text = text is null ? "Prompt cancelled" : $"Prompt accepted: {text}";
    }
}
