namespace Demo.Views;

/// <summary>A date field whose calendar opens in the overlay.</summary>
public sealed partial class DatePickerCard : ThemedStackPanel
{
    /// <summary>Creates the card and selects today.</summary>
    public DatePickerCard()
    {
        InitializeComponent();
        datePicker.SelectedDate = DateTime.Today;
        OnSelectedDateChanged(this, EventArgs.Empty);
    }

    private void OnSelectedDateChanged(object? sender, EventArgs e)
    {
        status.Text = datePicker.SelectedDate is { } date
            ? date.ToString("dddd, MMMM d")
            : "No date";
    }
}
