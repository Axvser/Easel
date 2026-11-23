using Demo.Views;

namespace Demo;

/// <summary>The Text card group: each card is a XAML view built from the themed base types.</summary>
internal static class TextCards
{
    /// <summary>Creates the text group in catalog order.</summary>
    public static IReadOnlyList<GalleryCard> Create() =>
    [
        Card(
            "text.text-block",
            "Text",
            "TextBlock",
            "Single-line and wrapped text with theme colours.",
            "<TextBlock Text=\"Heading\" Foreground=\"{StaticResource Accent}\" />",
            _ => new TextBlockCard()),
        Card(
            "text.text-box",
            "Text",
            "TextBox",
            "Self-drawn editing with placeholder and selection.",
            "<TextBox Width=\"320\" Placeholder=\"Type here\" />",
            _ => new TextBoxCard()),
        Card(
            "text.custom-field",
            "Text",
            "XAML custom field",
            "A templated TextBox with UseNativeEditor=false: every DP stays visual, with no child HWND.",
            "<TextBox UseNativeEditor=\"False\" Template=\"{StaticResource PillField}\" />",
            _ => new CustomTextBox()),
        Card(
            "text.password-box",
            "Text",
            "PasswordBox",
            "The same editor with the password glyphs.",
            "<TextBox Width=\"320\" IsPassword=\"True\" Placeholder=\"Password\" />",
            _ => new PasswordBoxCard()),
        Card(
            "text.multiline",
            "Text",
            "Multiline TextBox",
            "Return inserts a line; wrapping and wheel scrolling work.",
            "<TextBox Width=\"420\" Height=\"130\" AcceptsReturn=\"True\" Wrap=\"True\" />",
            _ => new MultilineTextCard()),
        Card(
            "text.image",
            "Text",
            "Image",
            "A bitmap drawn inside the visual tree.",
            "<Image Width=\"220\" Height=\"120\" Stretch=\"Uniform\" />",
            _ => new ImageCard()),
        Card(
            "text.rich-text-box",
            "Text",
            "RichTextBox",
            "Styled runs, selection, clipboard, undo and keyboard formatting.",
            "<RichTextBox Width=\"440\" Height=\"170\" />",
            _ => new RichTextCard()),
    ];

    private static GalleryCard Card(
        string id, string group, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create)
        => new(new GalleryCardInfo(id, group, title, summary, usage), create);
}
