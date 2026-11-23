using Easel;

namespace Demo.Views;

/// <summary>A binding whose value passes through a converter before it reaches the target.</summary>
public sealed partial class ConverterCard : ThemedStackPanel
{
    /// <summary>Creates the card, registers the converter and points the binding at the shared amount.</summary>
    /// <param name="model">The shared sample data.</param>
    public ConverterCard(GalleryModel model)
    {
        Resources["Money"] = new MoneyConverter();
        DataContext = model;
        InitializeComponent();
    }
}

/// <summary>Formats a number as currency and parses it back.</summary>
internal sealed class MoneyConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType)
        => value is double amount ? amount.ToString("C2") : value;

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type sourceType)
        => value is string text && double.TryParse(text, out var amount) ? amount : value;
}
