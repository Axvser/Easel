using System.Threading.Tasks;

namespace Easel.Visuals.Dialogs;

/// <summary>Convenience entry points for showing a <see cref="VisualDialog"/> on a <see cref="VisualHost"/>.</summary>
/// <remarks>
/// Every method creates a fresh dialog, shows it in the host's overlay and completes when it closes. The dialogs are
/// modal: the surface below receives no pointer input, Tab cycles inside the top dialog and Escape cancels when the
/// options allow it.
/// </remarks>
public static class DialogService
{
    /// <summary>Shows <paramref name="options"/> and returns the result it closed with.</summary>
    /// <param name="host">The surface whose overlay hosts the dialog.</param>
    /// <param name="options">The content and behaviour of the dialog.</param>
    /// <returns>A task completed when the dialog closes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static Task<DialogResult> ShowAsync(VisualHost host, DialogOptions options)
    {
        if (host is null) throw new ArgumentNullException(nameof(host));
        if (options is null) throw new ArgumentNullException(nameof(options));
        return new VisualDialog(options).ShowAsync(host);
    }

    /// <summary>Shows a single-button message and returns the button or dismissal that closed it.</summary>
    /// <param name="host">The surface whose overlay hosts the dialog.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The body message.</param>
    /// <param name="kind">The semantic tone that selects the icon and accent colour.</param>
    /// <returns>A task completed with <see cref="DialogResult.Primary"/> when the button is pressed, otherwise
    /// <see cref="DialogResult.Cancel"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is <see langword="null"/>.</exception>
    public static Task<DialogResult> MessageAsync(
        VisualHost host,
        string title,
        string message,
        DialogKind kind = DialogKind.Info)
        => ShowAsync(host, new DialogOptions
        {
            Title = title ?? string.Empty,
            Message = message ?? string.Empty,
            Kind = kind,
        });

    /// <summary>Shows a primary/secondary confirmation and returns the button or dismissal that closed it.</summary>
    /// <param name="host">The surface whose overlay hosts the dialog.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The body message.</param>
    /// <param name="primaryText">The filled primary button text.</param>
    /// <param name="secondaryText">The outlined secondary button text.</param>
    /// <param name="kind">The semantic tone that selects the icon and accent colour.</param>
    /// <returns>A task completed with <see cref="DialogResult.Primary"/>, <see cref="DialogResult.Secondary"/> or
    /// <see cref="DialogResult.Cancel"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is <see langword="null"/>.</exception>
    public static Task<DialogResult> ConfirmAsync(
        VisualHost host,
        string title,
        string message,
        string primaryText,
        string secondaryText,
        DialogKind kind = DialogKind.Info)
        => ShowAsync(host, new DialogOptions
        {
            Title = title ?? string.Empty,
            Message = message ?? string.Empty,
            PrimaryText = primaryText ?? "OK",
            SecondaryText = secondaryText,
            Kind = kind,
        });

    /// <summary>Shows a text prompt and returns the entered text.</summary>
    /// <param name="host">The surface whose overlay hosts the dialog.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="label">The caption shown above the input box; empty hides it.</param>
    /// <param name="initial">The text the input box starts with.</param>
    /// <returns>The entered text when the primary button closes the dialog; <see langword="null"/> when it is
    /// cancelled or dismissed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is <see langword="null"/>.</exception>
    public static Task<string?> PromptAsync(
        VisualHost host,
        string title,
        string label,
        string initial = "")
    {
        if (host is null) throw new ArgumentNullException(nameof(host));

        var dialog = new VisualDialog(new DialogOptions
        {
            Title = title ?? string.Empty,
            PrimaryText = "OK",
            CancelText = "Cancel",
            ShowCancel = true,
            IsPrompt = true,
            PromptLabel = label,
            PromptInitial = initial ?? string.Empty,
        });

        // 结果在 Closed 事件里就地落下；再开一个 async 续体会把读文本框挪到别的线程上。
        var completion = new TaskCompletionSource<string?>();
        dialog.Closed += (_, _) => completion.TrySetResult(
            dialog.Result == DialogResult.Primary ? dialog.PromptText : null);
        dialog.ShowAsync(host);
        return completion.Task;
    }
}
