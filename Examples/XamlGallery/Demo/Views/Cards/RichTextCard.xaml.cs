namespace Demo.Views;

/// <summary>The RichTextBox card; its text, selection and initial bold run are set after initialization.</summary>
public sealed partial class RichTextCard : ThemedStackPanel
{
    /// <summary>Creates the view, runs the generated initializer and seeds the document.</summary>
    public RichTextCard()
    {
        InitializeComponent();
        rich.SetPlainText("Easel RichTextBox\nSelect text, then press Ctrl+B / Ctrl+I / Ctrl+U.\nCtrl+Z and Ctrl+Y walk the edit history.");
        rich.Select(0, 8);
        rich.ToggleBold();
        rich.Select(0, 0);
    }
}
