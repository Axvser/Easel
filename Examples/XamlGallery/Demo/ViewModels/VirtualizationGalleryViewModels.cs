using System.Globalization;
using Demo.Mvvm;
using XamlBrush = Easel.Brush;

namespace Demo.ViewModels;

/// <summary>One tile in the <c>VirtualizingWrapPanel</c> gallery card.</summary>
public sealed class WrapTile
{
    private static readonly XamlBrush InfoBrush = new Easel.SolidColorBrush(System.Drawing.Color.FromArgb(0xFF, 0x22, 0xD3, 0xEE));
    private static readonly XamlBrush WarningBrush = new Easel.SolidColorBrush(System.Drawing.Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B));
    private static readonly XamlBrush ErrorBrush = new Easel.SolidColorBrush(System.Drawing.Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));

    /// <summary>Creates a tile for a zero-based index.</summary>
    public WrapTile(int index) => Index = index;

    /// <summary>The zero-based index.</summary>
    public int Index { get; }

    /// <summary>The one-based tag.</summary>
    public string Tag => $"DEV-{(Index + 1):0000}";

    /// <summary>The state label.</summary>
    public string State =>
        Index % 17 == 0 ? "FAULT"
        : Index % 5 == 0 ? "WARN"
        : "OK";

    /// <summary>The state colour.</summary>
    public XamlBrush StateBrush => State switch
    {
        "FAULT" => ErrorBrush,
        "WARN" => WarningBrush,
        _ => InfoBrush,
    };

    /// <summary>A stable simulated value.</summary>
    public string Value => (20d + ((Index * 37) % 760) / 10d).ToString("0.0", CultureInfo.InvariantCulture);
}

/// <summary>The source behind the wrap-panel gallery card.</summary>
public sealed class VirtualizationWrapViewModel : ObservableObject
{
    /// <summary>Creates a 5,000-tile source.</summary>
    public VirtualizationWrapViewModel()
    {
        Tiles = Enumerable.Range(0, 5_000).Select(static index => new WrapTile(index)).ToArray();
    }

    /// <summary>The tile source.</summary>
    public IReadOnlyList<WrapTile> Tiles { get; }
}
