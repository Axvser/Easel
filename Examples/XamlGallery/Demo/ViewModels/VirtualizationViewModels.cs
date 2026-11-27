using System.Drawing;
using System.Globalization;
using Demo.Mvvm;
using XamlBrush = Easel.Brush;

namespace Demo.ViewModels;

/// <summary>One row of the windowed log shown by the <c>VirtualizingStackPanel</c> card.</summary>
/// <remarks>
/// The row keeps only its index; every displayed field is computed on read so a million-row source costs one small
/// object per row instead of five strings. That is what makes the 1,000,000-row preset practical to build in a
/// gallery card.
/// </remarks>
public sealed class VirtualLogRow : ObservableObject
{
    private static readonly string[] Messages =
    [
        "feed axis position within tolerance",
        "spindle load nominal after ramp",
        "tool change completed on station 4",
        "recipe step acknowledged by PLC",
        "conveyor photo-eye cleared",
        "batch counter incremented",
        "coolant pressure restored",
        "vision inspection passed",
        "safety gate interlock confirmed",
        "servo drive reported ready",
        "torque curve within envelope",
        "historian batch flushed",
    ];

    private static readonly XamlBrush InfoBrush = new Easel.SolidColorBrush(Color.FromArgb(0xFF, 0x22, 0xD3, 0xEE));
    private static readonly XamlBrush DebugBrush = new Easel.SolidColorBrush(Color.FromArgb(0xFF, 0x94, 0xA3, 0xB8));
    private static readonly XamlBrush WarningBrush = new Easel.SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B));
    private static readonly XamlBrush ErrorBrush = new Easel.SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));
    private static readonly XamlBrush FatalBrush = new Easel.SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x3B, 0x6B));
    private static readonly XamlBrush SelectedBrush = new Easel.SolidColorBrush(Color.FromArgb(0x48, 0x22, 0xD3, 0xEE));
    private static readonly XamlBrush RowBrushDefault = new Easel.SolidColorBrush(Color.Transparent);

    private bool _isSelected;

    /// <summary>Creates the row for a zero-based source index.</summary>
    /// <param name="index">The zero-based position in the log.</param>
    public VirtualLogRow(int index) => Index = index;

    /// <summary>The zero-based source index.</summary>
    public int Index { get; }

    /// <summary>The one-based sequence number, grouped for readability.</summary>
    public string Number => (Index + 1).ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>The simulated capture timestamp; 137 ms apart and stable across scrolls.</summary>
    public string Timestamp => TimeSpan
        .FromMilliseconds(Index * 137L)
        .ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

    /// <summary>The severity derived from the index; the distribution is uneven on purpose.</summary>
    public string Level =>
        Index % 997 == 0 ? "FATAL"
        : Index % 53 == 0 ? "ERROR"
        : Index % 7 == 0 ? "WARN"
        : Index % 3 == 0 ? "DEBUG"
        : "INFO";

    /// <summary>The severity colour; these stay fixed across themes like every HMI semantic colour.</summary>
    public XamlBrush LevelBrush => Level switch
    {
        "FATAL" => FatalBrush,
        "ERROR" => ErrorBrush,
        "WARN" => WarningBrush,
        "DEBUG" => DebugBrush,
        _ => InfoBrush,
    };

    /// <summary>The message text; one of twelve fixed lines selected by index.</summary>
    public string Message => Messages[Index % Messages.Length];

    /// <summary>The simulated operation time, in milliseconds.</summary>
    public string Elapsed => (((Index * 37) % 900) + 1)
        .ToString("0.0", CultureInfo.InvariantCulture) + " ms";

    /// <summary>Whether the row is the current selection.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(RowBrush));
            }
        }
    }

    /// <summary>The row background; the selected row gets a translucent accent wash.</summary>
    public XamlBrush RowBrush => IsSelected ? SelectedBrush : RowBrushDefault;
}

/// <summary>The source behind the <c>VirtualizingStackPanel</c> card.</summary>
/// <remarks>Replacing <see cref="Rows"/> swaps the whole source; the panel rebuilds its window from the new count.</remarks>
public sealed class VirtualizationLogViewModel : ObservableObject
{
    private IReadOnlyList<VirtualLogRow> _rows;
    private long _lastBuildMilliseconds;

    /// <summary>Creates the model with the default 100,000-row log.</summary>
    public VirtualizationLogViewModel()
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        _rows = Build(100_000);
        _lastBuildMilliseconds = ElapsedMilliseconds(started);
    }

    /// <summary>The current log rows.</summary>
    public IReadOnlyList<VirtualLogRow> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    /// <summary>The number of rows in the current source.</summary>
    public int RowCount => _rows.Count;

    /// <summary>The number of milliseconds the last <see cref="Build"/> call took.</summary>
    public long LastBuildMilliseconds
    {
        get => _lastBuildMilliseconds;
        private set => SetProperty(ref _lastBuildMilliseconds, value);
    }

    /// <summary>A one-line description of the current source and its build cost.</summary>
    public string SourceLabel => $"{RowCount:N0} rows · built in {LastBuildMilliseconds:N0} ms";

    /// <summary>Builds a deterministic log of <paramref name="count"/> rows.</summary>
    /// <param name="count">The number of rows; zero produces an empty source.</param>
    /// <returns>The new row list.</returns>
    public static List<VirtualLogRow> Build(int count)
    {
        var rows = new List<VirtualLogRow>(Math.Max(0, count));
        for (var index = 0; index < count; index++)
        {
            rows.Add(new VirtualLogRow(index));
        }

        return rows;
    }

    /// <summary>Replaces the source with a new log of <paramref name="count"/> rows.</summary>
    /// <param name="count">The new row count.</param>
    public void Replace(int count)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        Rows = Build(count);
        LastBuildMilliseconds = ElapsedMilliseconds(started);
        OnPropertyChanged(nameof(RowCount));
        OnPropertyChanged(nameof(SourceLabel));
    }

    private static long ElapsedMilliseconds(long started)
        => (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
