namespace Demo.Views;

/// <summary>A month calendar showing today's date.</summary>
public sealed partial class CalendarCard : ThemedStackPanel
{
    /// <summary>Creates the card and selects today.</summary>
    public CalendarCard()
    {
        InitializeComponent();
        calendar.SelectedDate = DateTime.Today;
        OnSelectedDateChanged(this, EventArgs.Empty);
    }

    private void OnSelectedDateChanged(object? sender, EventArgs e)
    {
        status.Text = calendar.SelectedDate is { } date
            ? date.ToString("dddd, MMMM d, yyyy")
            : "No date";
    }
}
