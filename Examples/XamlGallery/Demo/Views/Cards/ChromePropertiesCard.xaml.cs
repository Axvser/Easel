namespace Demo.Views;

/// <summary>The control-chrome card; the demo rows are added from code.</summary>
public sealed partial class ChromePropertiesCard : ThemedStackPanel
{
    /// <summary>Creates the card and fills its list and editable combo.</summary>
    public ChromePropertiesCard()
    {
        InitializeComponent();

        var rows = new[] { "Surface from the control", "Border from the control", "Role brushes" };
        listBox.ItemsSource = rows;
        listBox.SelectedIndex = 0;
        foreach (var row in rows)
        {
            editableCombo.Items.Add(row);
        }
    }
}
