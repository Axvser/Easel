namespace Demo.Views;

/// <summary>A ComboBox whose items are added from code; the drop-down itself is markup.</summary>
public sealed partial class ComboBoxCard : ThemedStackPanel
{
    /// <summary>Creates the card and fills the drop-down.</summary>
    public ComboBoxCard()
    {
        InitializeComponent();

        comboBox.Items.Add("Balanced");
        comboBox.Items.Add("Throughput");
        comboBox.Items.Add("Latency");
        comboBox.Items.Add("Custom");
        editableCombo.Items.Add("Balanced");
        editableCombo.Items.Add("Throughput");
        editableCombo.Items.Add("Latency");
        editableCombo.Items.Add("Custom");
        OnSelectionChanged(this, EventArgs.Empty);
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (status is null)
        {
            return;
        }

        status.Text = comboBox.SelectedItem?.ToString() ?? "No policy";
    }
}
