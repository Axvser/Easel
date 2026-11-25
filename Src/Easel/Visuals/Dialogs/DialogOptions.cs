namespace Easel.Visuals.Dialogs;

/// <summary>The semantic tone of a dialog, which selects the icon and accent colour.</summary>
public enum DialogKind
{
    /// <summary>Informational content.</summary>
    Info,

    /// <summary>A caution the user should read before continuing.</summary>
    Warning,

    /// <summary>A failure report.</summary>
    Error,

    /// <summary>A confirmation that the action succeeded.</summary>
    Success,
}

/// <summary>The button a dialog was closed with.</summary>
public enum DialogResult
{
    /// <summary>The primary (filled) button.</summary>
    Primary,

    /// <summary>The secondary (outlined) button.</summary>
    Secondary,

    /// <summary>The cancel button, Escape or an outside click.</summary>
    Cancel,
}

/// <summary>The content and behaviour of one <see cref="VisualDialog"/>.</summary>
/// <remarks>
/// <para>
/// <see cref="Content"/> can be a <see cref="VisualElement"/> shown as the body, a string used as the message, or any
/// other object rendered with <see cref="object.ToString"/>. When it is <see langword="null"/> the body is built from
/// <see cref="Message"/>.
/// </para>
/// <para>
/// A secondary button appears only when <see cref="SecondaryText"/> is non-empty; the cancel button appears only when
/// <see cref="ShowCancel"/> is set. Escape and an outside press are independent switches.
/// </para>
/// </remarks>
public sealed class DialogOptions
{
    /// <summary>The dialog title; an empty value leaves the header with the icon only.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The body content: a visual element, text, or any object rendered as text.</summary>
    public object? Content { get; set; }

    /// <summary>The body message used when <see cref="Content"/> is <see langword="null"/>.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The semantic tone that selects the icon and accent colour.</summary>
    public DialogKind Kind { get; set; } = DialogKind.Info;

    /// <summary>The text of the filled primary button.</summary>
    public string PrimaryText { get; set; } = "OK";

    /// <summary>The text of the outlined secondary button; <see langword="null"/> or empty hides it.</summary>
    public string? SecondaryText { get; set; }

    /// <summary>The text of the text-style cancel button.</summary>
    public string? CancelText { get; set; } = "Cancel";

    /// <summary>Whether the cancel button is shown.</summary>
    public bool ShowCancel { get; set; }

    /// <summary>Whether Escape closes the dialog with <see cref="DialogResult.Cancel"/>.</summary>
    public bool DismissOnEscape { get; set; } = true;

    /// <summary>Whether a press on the scrim outside the card closes the dialog with <see cref="DialogResult.Cancel"/>.</summary>
    public bool DismissOnOutsideClick { get; set; }

    /// <summary>The widest the card may be, in logical pixels.</summary>
    public float MaxWidth { get; set; } = 460f;

    /// <summary>Whether the scrim fades in and the card fades and rises into place.</summary>
    public bool Animate { get; set; } = true;

    // 提示输入只由 DialogService.PromptAsync 设置；默认卡片据此在正文下方插入标签与输入框。
    internal bool IsPrompt { get; set; }

    internal string? PromptLabel { get; set; }

    internal string PromptInitial { get; set; } = string.Empty;
}
