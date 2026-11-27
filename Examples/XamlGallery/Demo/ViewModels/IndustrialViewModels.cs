using System.Collections.ObjectModel;
using System.Drawing;
using Demo.Mvvm;
using Easel;
using XamlBrush = Easel.Brush;

namespace Demo.ViewModels;

/// <summary>Builds the SVG path data used by the industrial pages' arc gauges and tick rings.</summary>
/// <remarks>
/// Angles are screen angles: 0° points right and positive angles sweep clockwise because y grows downward.
/// A gauge opens at the bottom with the default 150° → 390° arc.
/// </remarks>
internal static class GaugeGeometry
{
    /// <summary>Builds one stroked arc from <paramref name="value"/> as a fraction of the sweep.</summary>
    public static string Arc(
        double value,
        double minimum,
        double maximum,
        float centerX,
        float centerY,
        float radius,
        double startDegrees = 150d,
        double sweepDegrees = 240d)
    {
        var range = maximum - minimum;
        var fraction = range <= 0d ? 0d : (value - minimum) / range;
        fraction = Math.Clamp(fraction, 0d, 1d);
        if (fraction <= 0.0001d)
        {
            return string.Empty;
        }

        var sweep = sweepDegrees * fraction;
        var start = PointOnCircle(centerX, centerY, radius, startDegrees);
        var end = PointOnCircle(centerX, centerY, radius, startDegrees + sweep);
        var largeArc = sweep > 180d ? 1 : 0;
        return FormattableString.Invariant(
            $"M {start.X:0.##},{start.Y:0.##} A {radius:0.##},{radius:0.##} 0 {largeArc},1 {end.X:0.##},{end.Y:0.##}");
    }

    /// <summary>Builds the tick ring as one path of radial segments.</summary>
    public static string Ticks(
        float centerX,
        float centerY,
        float outerRadius,
        float innerRadius,
        int count,
        double startDegrees = 150d,
        double sweepDegrees = 240d)
    {
        if (count < 2)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        for (var index = 0; index < count; index++)
        {
            var angle = startDegrees + (sweepDegrees * index / (count - 1d));
            var outer = PointOnCircle(centerX, centerY, outerRadius, angle);
            var inner = PointOnCircle(centerX, centerY, innerRadius, angle);
            builder.Append(FormattableString.Invariant($"M {inner.X:0.##},{inner.Y:0.##} L {outer.X:0.##},{outer.Y:0.##} "));
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>A point on a circle in screen coordinates.</summary>
    public static PointF PointOnCircle(float centerX, float centerY, float radius, double degrees)
    {
        var radians = degrees * Math.PI / 180d;
        return new PointF(
            (float)(centerX + (radius * Math.Cos(radians))),
            (float)(centerY + (radius * Math.Sin(radians))));
    }
}

/// <summary>One station tile on the production line.</summary>
public sealed class StationRow : ObservableObject
{
    private string _state;
    private double _cycle;

    /// <summary>Creates a station in the given state.</summary>
    public StationRow(string name, string state, double cycle)
    {
        Name = name;
        _state = state;
        _cycle = cycle;
    }

    /// <summary>The station tag, such as <c>OP10</c>.</summary>
    public string Name { get; }

    /// <summary>The run state: <c>RUN</c>, <c>IDLE</c>, <c>SETUP</c> or <c>FAULT</c>.</summary>
    public string State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(StateBrush));
            }
        }
    }

    /// <summary>The last cycle time in seconds.</summary>
    public double Cycle
    {
        get => _cycle;
        set
        {
            if (SetProperty(ref _cycle, value))
            {
                OnPropertyChanged(nameof(CycleText));
            }
        }
    }

    /// <summary>The cycle time formatted for the tile.</summary>
    public string CycleText => $"{Cycle:0.0}s";

    /// <summary>The colour that carries the station state.</summary>
    public XamlBrush StateBrush => State switch
    {
        "FAULT" => IndustrialPalette.Danger,
        "IDLE" => IndustrialPalette.Warning,
        "SETUP" => IndustrialPalette.Signal,
        _ => IndustrialPalette.Positive,
    };
}

/// <summary>One row of the alarm list.</summary>
public sealed class AlarmRow : ObservableObject
{
    private bool _isAcked;
    private string _message;

    /// <summary>Creates an active alarm.</summary>
    public AlarmRow(string time, string tag, string message, string severity)
    {
        Time = time;
        Tag = tag;
        _message = message;
        Severity = severity;
        AckCommand = new RelayCommand(_ => Acknowledge());
    }

    /// <summary>Raised after the row is acknowledged.</summary>
    public event EventHandler? Acknowledged;

    /// <summary>The timestamp shown in the row.</summary>
    public string Time { get; }

    /// <summary>The equipment tag.</summary>
    public string Tag { get; }

    /// <summary>The alarm text.</summary>
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    /// <summary>The severity: <c>CRITICAL</c>, <c>WARNING</c> or <c>INFO</c>.</summary>
    public string Severity { get; }

    /// <summary>Whether the operator acknowledged the alarm.</summary>
    public bool IsAcked
    {
        get => _isAcked;
        private set
        {
            if (SetProperty(ref _isAcked, value))
            {
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(StateBrush));
            }
        }
    }

    /// <summary>The state label shown in the row.</summary>
    public string StateText => IsAcked ? "ACKED" : "ACTIVE";

    /// <summary>The colour that carries the severity.</summary>
    public XamlBrush SeverityBrush => Severity switch
    {
        "CRITICAL" => IndustrialPalette.Danger,
        "WARNING" => IndustrialPalette.Warning,
        _ => IndustrialPalette.Signal,
    };

    /// <summary>The colour that carries the acknowledgement state.</summary>
    public XamlBrush StateBrush => IsAcked ? IndustrialPalette.Positive : IndustrialPalette.Warning;

    /// <summary>Acknowledges this row.</summary>
    public RelayCommand AckCommand { get; }

    /// <summary>Marks the row acknowledged and raises <see cref="Acknowledged"/> once.</summary>
    public void Acknowledge()
    {
        if (IsAcked)
        {
            return;
        }

        IsAcked = true;
        Acknowledged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>The shared industrial accent brushes built once, so a view model can bind a colour directly.</summary>
internal static class IndustrialPalette
{
    /// <summary>Normal/healthy state.</summary>
    public static readonly XamlBrush Positive = new SolidColorBrush(Color.FromArgb(0xFF, 0x22, 0xC5, 0x5E));

    /// <summary>Attention state.</summary>
    public static readonly XamlBrush Warning = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B));

    /// <summary>Fault or critical state.</summary>
    public static readonly XamlBrush Danger = new SolidColorBrush(Color.FromArgb(0xFF, 0xEF, 0x44, 0x44));

    /// <summary>The HMI signal cyan.</summary>
    public static readonly XamlBrush Signal = new SolidColorBrush(Color.FromArgb(0xFF, 0x22, 0xD3, 0xEE));

    /// <summary>An inactive state.</summary>
    public static readonly XamlBrush Idle = new SolidColorBrush(Color.FromArgb(0xFF, 0x64, 0x74, 0x8B));
}

/// <summary>One recipe step shown beside the process mimic.</summary>
public sealed class ProcessStep : ObservableObject
{
    private string _state;

    /// <summary>Creates a step in the given state: <c>DONE</c>, <c>ACTIVE</c> or <c>NEXT</c>.</summary>
    public ProcessStep(string name, string state)
    {
        Name = name;
        _state = state;
    }

    /// <summary>The step name.</summary>
    public string Name { get; }

    /// <summary>The step state.</summary>
    public string State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(StateBrush));
            }
        }
    }

    /// <summary>The colour that carries the step state.</summary>
    public XamlBrush StateBrush => State switch
    {
        "DONE" => IndustrialPalette.Positive,
        "ACTIVE" => IndustrialPalette.Warning,
        _ => IndustrialPalette.Idle,
    };
}

/// <summary>The production-line page state: KPI values, station rows, alarm rows and the throughput sparkline.</summary>
public sealed class ProductionLineViewModel : ObservableObject
{
    private const float ChartWidth = 470f;
    private const float ChartHeight = 118f;
    private const double ChartMaximum = 160d;

    private readonly Random _random = new(20261127);
    private readonly List<double> _history = [];
    private double _oee = 84.6d;
    private double _throughput = 118.4d;
    private double _scrap = 0.8d;
    private double _downtime = 12d;
    private string _status = "Shift B · line running";

    /// <summary>Creates the station rows, the alarm feed and the initial sparkline.</summary>
    public ProductionLineViewModel()
    {
        Stations =
        [
            new StationRow("OP10", "RUN", 41.2d),
            new StationRow("OP20", "RUN", 43.8d),
            new StationRow("OP30", "SETUP", 0d),
            new StationRow("OP40", "RUN", 39.4d),
            new StationRow("OP50", "IDLE", 0d),
            new StationRow("OP60", "RUN", 44.1d),
        ];

        Alarms =
        [
            new AlarmRow("09:42:16", "OP30", "Tool change overdue · T14", "WARNING"),
            new AlarmRow("09:38:02", "PUMP-2", "Coolant pressure low", "CRITICAL"),
            new AlarmRow("09:31:47", "OP50", "Waiting for upstream part", "INFO"),
        ];

        for (var index = 0; index < 32; index++)
        {
            _history.Add(104d + (8d * Math.Sin(index / 3.4d)) + _random.NextDouble() * 12d);
        }

        ThroughputPoints = BuildPoints(_history);
        ThroughputAreaPoints = BuildArea(_history);
        OeeArc = GaugeGeometry.Arc(_oee, 0d, 100d, 32f, 32f, 25f);
        OeeTicks = GaugeGeometry.Ticks(32f, 32f, 30f, 26f, 13);
        Updated = DateTime.Now.ToString("HH:mm:ss");
    }

    /// <summary>The line's OEE percentage.</summary>
    public double Oee
    {
        get => _oee;
        private set
        {
            if (SetProperty(ref _oee, value))
            {
                OeeText = $"{value:0.0}%";
                OnPropertyChanged(nameof(OeeText));
                OeeArc = GaugeGeometry.Arc(value, 0d, 100d, 32f, 32f, 25f);
                OnPropertyChanged(nameof(OeeArc));
            }
        }
    }

    /// <summary>The OEE formatted for the tile.</summary>
    public string OeeText { get; private set; } = "84.6%";

    /// <summary>The parts-per-hour rate.</summary>
    public double Throughput
    {
        get => _throughput;
        private set
        {
            if (SetProperty(ref _throughput, value))
            {
                ThroughputText = $"{value:0.0}";
                OnPropertyChanged(nameof(ThroughputText));
            }
        }
    }

    /// <summary>The throughput formatted for the tile.</summary>
    public string ThroughputText { get; private set; } = "118.4";

    /// <summary>The scrap percentage.</summary>
    public double Scrap
    {
        get => _scrap;
        private set
        {
            if (SetProperty(ref _scrap, value))
            {
                ScrapText = $"{value:0.0}%";
                OnPropertyChanged(nameof(ScrapText));
            }
        }
    }

    /// <summary>The scrap formatted for the tile.</summary>
    public string ScrapText { get; private set; } = "0.8%";

    /// <summary>Downtime in minutes for the current shift.</summary>
    public double Downtime
    {
        get => _downtime;
        private set
        {
            if (SetProperty(ref _downtime, value))
            {
                DowntimeText = $"{value:0}";
                OnPropertyChanged(nameof(DowntimeText));
            }
        }
    }

    /// <summary>The downtime formatted for the tile.</summary>
    public string DowntimeText { get; private set; } = "12";

    /// <summary>The live status line.</summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>The last sample time.</summary>
    public string Updated { get; private set; } = string.Empty;

    /// <summary>The stroke points of the throughput trend.</summary>
    public PointF[] ThroughputPoints { get; private set; } = [];

    /// <summary>The closed polygon that fills the area under the trend.</summary>
    public PointF[] ThroughputAreaPoints { get; private set; } = [];

    /// <summary>The OEE arc path.</summary>
    public string OeeArc { get; private set; } = string.Empty;

    /// <summary>The OEE tick ring.</summary>
    public string OeeTicks { get; } = string.Empty;

    /// <summary>The station tiles.</summary>
    public ObservableCollection<StationRow> Stations { get; }

    /// <summary>The alarm feed.</summary>
    public ObservableCollection<AlarmRow> Alarms { get; }

    /// <summary>Advances the simulated line by one sample.</summary>
    public void Tick()
    {
        Throughput = Math.Clamp(Throughput + ((_random.NextDouble() - 0.45d) * 7d), 92d, 142d);
        Oee = Math.Clamp(Oee + ((_random.NextDouble() - 0.5d) * 0.9d), 78d, 92d);
        Scrap = Math.Clamp(Scrap + ((_random.NextDouble() - 0.5d) * 0.1d), 0.2d, 1.8d);

        foreach (var station in Stations)
        {
            if (station.State is "RUN")
            {
                station.Cycle = Math.Clamp(station.Cycle + ((_random.NextDouble() - 0.5d) * 1.6d), 34d, 52d);
            }
        }

        _history.Add(Throughput);
        if (_history.Count > 32)
        {
            _history.RemoveAt(0);
        }

        ThroughputPoints = BuildPoints(_history);
        ThroughputAreaPoints = BuildArea(_history);
        Updated = DateTime.Now.ToString("HH:mm:ss");
        OnPropertyChanged(nameof(ThroughputPoints));
        OnPropertyChanged(nameof(ThroughputAreaPoints));
        OnPropertyChanged(nameof(Updated));
        Status = $"Shift B · line running · scrap {Scrap:0.0}%";
    }

    private static PointF[] BuildPoints(IReadOnlyList<double> values)
    {
        var points = new PointF[values.Count];
        var step = values.Count <= 1 ? ChartWidth : ChartWidth / (values.Count - 1);
        for (var index = 0; index < values.Count; index++)
        {
            var x = index * step;
            var y = (float)(ChartHeight - (Math.Clamp(values[index], 0d, ChartMaximum) / ChartMaximum * ChartHeight));
            points[index] = new PointF(x, y);
        }

        return points;
    }

    private static PointF[] BuildArea(IReadOnlyList<double> values)
    {
        var line = BuildPoints(values);
        var area = new PointF[line.Length + 2];
        Array.Copy(line, area, line.Length);
        area[^2] = new PointF(ChartWidth, ChartHeight);
        area[^1] = new PointF(0f, ChartHeight);
        return area;
    }
}

/// <summary>The process-flow page state: vessel levels, flow-dot positions and the live process values.</summary>
public sealed class ProcessFlowViewModel : ObservableObject
{
    private readonly Random _random = new(118);
    private double _feedLevel = 72d;
    private double _reactorLevel = 48d;
    private double _productLevel = 31d;
    private double _temperature = 184.2d;
    private double _pressure = 2.4d;
    private double _flowRate = 18.4d;
    private double _flowDot1;
    private string _phase = "MIXING";
    private string _status = "Batch 24-118 · mixing stage";
    private int _stepIndex = 1;
    private int _ticks;

    /// <summary>Creates the process state and its initial dot positions.</summary>
    public ProcessFlowViewModel()
    {
        Steps =
        [
            new ProcessStep("Charge feed", "DONE"),
            new ProcessStep("Heat to 180 °C", "ACTIVE"),
            new ProcessStep("Mix 40 min", "NEXT"),
            new ProcessStep("Discharge", "NEXT"),
        ];

        FlowDot1X = 0d;
        FlowDot2X = 107d;
        FlowDot3X = 214d;
    }

    /// <summary>The feed vessel level percentage.</summary>
    public double FeedLevel
    {
        get => _feedLevel;
        private set
        {
            if (SetProperty(ref _feedLevel, value))
            {
                FeedText = $"{value:0}%";
                OnPropertyChanged(nameof(FeedFillHeight));
            }
        }
    }

    /// <summary>The feed level formatted for the mimic.</summary>
    public string FeedText { get; private set; } = "72%";

    /// <summary>The feed fill height in pixels.</summary>
    public double FeedFillHeight => 138d * FeedLevel / 100d;

    /// <summary>The reactor level percentage.</summary>
    public double ReactorLevel
    {
        get => _reactorLevel;
        private set
        {
            if (SetProperty(ref _reactorLevel, value))
            {
                ReactorText = $"{value:0}%";
                OnPropertyChanged(nameof(ReactorFillHeight));
            }
        }
    }

    /// <summary>The reactor level formatted for the mimic.</summary>
    public string ReactorText { get; private set; } = "48%";

    /// <summary>The reactor fill height in pixels.</summary>
    public double ReactorFillHeight => 158d * ReactorLevel / 100d;

    /// <summary>The product vessel level percentage.</summary>
    public double ProductLevel
    {
        get => _productLevel;
        private set
        {
            if (SetProperty(ref _productLevel, value))
            {
                ProductText = $"{value:0}%";
                OnPropertyChanged(nameof(ProductFillHeight));
            }
        }
    }

    /// <summary>The product level formatted for the mimic.</summary>
    public string ProductText { get; private set; } = "31%";

    /// <summary>The product fill height in pixels.</summary>
    public double ProductFillHeight => 118d * ProductLevel / 100d;

    /// <summary>The reactor temperature in °C.</summary>
    public double Temperature
    {
        get => _temperature;
        private set
        {
            if (SetProperty(ref _temperature, value))
            {
                TemperatureText = $"{value:0.0} °C";
            }
        }
    }

    /// <summary>The temperature formatted for the tile.</summary>
    public string TemperatureText { get; private set; } = "184.2 °C";

    /// <summary>The reactor pressure in bar.</summary>
    public double Pressure
    {
        get => _pressure;
        private set
        {
            if (SetProperty(ref _pressure, value))
            {
                PressureText = $"{value:0.00} bar";
            }
        }
    }

    /// <summary>The pressure formatted for the tile.</summary>
    public string PressureText { get; private set; } = "2.40 bar";

    /// <summary>The transfer flow in m³/h.</summary>
    public double FlowRate
    {
        get => _flowRate;
        private set
        {
            if (SetProperty(ref _flowRate, value))
            {
                FlowText = $"{value:0.0} m³/h";
            }
        }
    }

    /// <summary>The flow formatted for the tile.</summary>
    public string FlowText { get; private set; } = "18.4 m³/h";

    /// <summary>The batch phase shown in the header.</summary>
    public string Phase
    {
        get => _phase;
        private set => SetProperty(ref _phase, value);
    }

    /// <summary>The status line.</summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>The first flow dot's horizontal position.</summary>
    public double FlowDot1X
    {
        get => _flowDot1;
        private set => SetProperty(ref _flowDot1, value);
    }

    /// <summary>The second flow dot's horizontal position.</summary>
    public double FlowDot2X { get; private set; }

    /// <summary>The third flow dot's horizontal position.</summary>
    public double FlowDot3X { get; private set; }

    /// <summary>The recipe steps shown beside the mimic.</summary>
    public ObservableCollection<ProcessStep> Steps { get; }

    /// <summary>The batch progress percentage.</summary>
    public double BatchProgress => Math.Min(100d, (_stepIndex + 1) * 22d);

    /// <summary>The batch progress formatted for the panel.</summary>
    public string BatchProgressText => $"{BatchProgress:0}%";

    /// <summary>Advances the simulated batch by one sample.</summary>
    public void Tick()
    {
        var feed = Math.Max(18d, FeedLevel - (_random.NextDouble() * 0.35d));
        var reactor = Math.Clamp(ReactorLevel + ((_random.NextDouble() - 0.35d) * 0.5d), 30d, 74d);
        var product = Math.Min(88d, ProductLevel + (_random.NextDouble() * 0.22d));
        FeedLevel = feed;
        ReactorLevel = reactor;
        ProductLevel = product;

        Temperature = Math.Clamp(Temperature + ((_random.NextDouble() - 0.5d) * 1.4d), 176d, 192d);
        Pressure = Math.Clamp(Pressure + ((_random.NextDouble() - 0.5d) * 0.08d), 2.1d, 2.8d);
        FlowRate = Math.Clamp(FlowRate + ((_random.NextDouble() - 0.5d) * 0.9d), 12d, 24d);

        _flowDot1 += 5d;
        if (_flowDot1 > 320d)
        {
            _flowDot1 -= 320d;
        }

        OnPropertyChanged(nameof(FlowDot1X));
        FlowDot2X = (_flowDot1 + 107d) % 320d;
        FlowDot3X = (_flowDot1 + 214d) % 320d;
        OnPropertyChanged(nameof(FlowDot2X));
        OnPropertyChanged(nameof(FlowDot3X));

        Phase = feed < 26d ? "CHARGING" : reactor > 66d ? "HEATING" : "MIXING";
        Status = $"Batch 24-118 · {Phase.ToLowerInvariant()} stage · {FlowRate:0.0} m³/h";

        _ticks++;
        if (_ticks % 24 == 0)
        {
            AdvanceStep();
        }
    }

    private void AdvanceStep()
    {
        if (_stepIndex >= Steps.Count - 1)
        {
            return;
        }

        Steps[_stepIndex].State = "DONE";
        _stepIndex++;
        Steps[_stepIndex].State = "ACTIVE";
        OnPropertyChanged(nameof(BatchProgress));
        OnPropertyChanged(nameof(BatchProgressText));
    }
}

/// <summary>The alarm-center page state: counts, the filtered list and acknowledgement plumbing.</summary>
public sealed class AlarmCenterViewModel : ObservableObject
{
    private readonly List<AlarmRow> _all = [];
    private string _filter = "All";
    private string _status = "4 active · 3 unacknowledged";

    /// <summary>Creates the alarm board.</summary>
    public AlarmCenterViewModel()
    {
        _all.AddRange(
        [
            new AlarmRow("09:42:16", "OP30", "Tool change overdue · T14", "WARNING"),
            new AlarmRow("09:38:02", "PUMP-2", "Coolant pressure low", "CRITICAL"),
            new AlarmRow("09:31:47", "OP50", "Waiting for upstream part", "INFO"),
            new AlarmRow("09:22:11", "TANK-1", "Level below 20%", "WARNING"),
            new AlarmRow("09:14:39", "VMC-850", "Spindle load above 85%", "CRITICAL"),
        ]);

        foreach (var alarm in _all)
        {
            alarm.Acknowledged += OnAlarmAcknowledged;
        }

        ApplyFilter();
    }

    /// <summary>The filter label shown by the active filter button.</summary>
    public string Filter
    {
        get => _filter;
        private set => SetProperty(ref _filter, value);
    }

    /// <summary>The status line.</summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>The rows that pass the current filter.</summary>
    public ObservableCollection<AlarmRow> Visible { get; } = [];

    /// <summary>The number of active critical alarms.</summary>
    public int CriticalCount => _all.Count(alarm => !alarm.IsAcked && alarm.Severity == "CRITICAL");

    /// <summary>The number of active warning alarms.</summary>
    public int WarningCount => _all.Count(alarm => !alarm.IsAcked && alarm.Severity == "WARNING");

    /// <summary>The number of active info alarms.</summary>
    public int InfoCount => _all.Count(alarm => !alarm.IsAcked && alarm.Severity == "INFO");

    /// <summary>The number of unacknowledged rows.</summary>
    public int UnackedCount => _all.Count(alarm => !alarm.IsAcked);

    /// <summary>Switches the filter to all alarms.</summary>
    public void ShowAll() => SelectFilter("All");

    /// <summary>Switches the filter to unacknowledged alarms.</summary>
    public void ShowUnacked() => SelectFilter("Unacked");

    /// <summary>Switches the filter to critical alarms.</summary>
    public void ShowCritical() => SelectFilter("Critical");

    /// <summary>Acknowledges every row that passes the current filter.</summary>
    public void AcknowledgeVisible()
    {
        foreach (var alarm in Visible.ToArray())
        {
            alarm.Acknowledge();
        }
    }

    private void SelectFilter(string filter)
    {
        if (string.Equals(_filter, filter, StringComparison.Ordinal))
        {
            return;
        }

        _filter = filter;
        OnPropertyChanged(nameof(Filter));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<AlarmRow> rows = _filter switch
        {
            "Unacked" => _all.Where(alarm => !alarm.IsAcked),
            "Critical" => _all.Where(alarm => alarm.Severity == "CRITICAL"),
            _ => _all,
        };

        Visible.Clear();
        foreach (var row in rows)
        {
            Visible.Add(row);
        }

        RaiseCounts();
        Status = $"{_all.Count(alarm => !alarm.IsAcked)} active · {UnackedCount} unacknowledged · filter {_filter}";
    }

    private void OnAlarmAcknowledged(object? sender, EventArgs e)
    {
        RaiseCounts();
        if (_filter == "Unacked")
        {
            ApplyFilter();
            return;
        }

        Status = $"{_all.Count(alarm => !alarm.IsAcked)} active · {UnackedCount} unacknowledged · filter {_filter}";
    }

    private void RaiseCounts()
    {
        OnPropertyChanged(nameof(CriticalCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(InfoCount));
        OnPropertyChanged(nameof(UnackedCount));
    }
}

/// <summary>The machine-panel page state: axis positions, spindle load, override and cycle controls.</summary>
public sealed class MachinePanelViewModel : ObservableObject
{
    private readonly Random _random = new(850);
    private double _x = 128.44d;
    private double _y = -42.18d;
    private double _z = 86.02d;
    private double _a;
    private double _feedOverride = 100d;
    private double _spindleLoad = 42d;
    private double _spindleRpm = 8200d;
    private bool _isRunning = true;
    private bool _coolant = true;
    private bool _emergencyStop;
    private string _status = "Cycle running · O1204";
    private TimeSpan _cycleTime = TimeSpan.FromMinutes(42) + TimeSpan.FromSeconds(18);

    /// <summary>Creates the panel state and its spindle arc.</summary>
    public MachinePanelViewModel()
    {
        SpindleArc = GaugeGeometry.Arc(_spindleLoad, 0d, 100d, 54f, 54f, 42f);
        SpindleTicks = GaugeGeometry.Ticks(54f, 54f, 50f, 45f, 17);
        Tools =
        [
            new ToolRow("T01", "Ø12 flat", 12.0d, 3200d),
            new ToolRow("T02", "Ø6 ball", 6.0d, 5400d),
            new ToolRow("T03", "Ø3 drill", 3.0d, 7200d),
        ];

        JogCommand = new RelayCommand(parameter => JogFrom(parameter as string));
        ToggleCycleCommand = new RelayCommand(_ => ToggleCycle());
        ToggleCoolantCommand = new RelayCommand(_ => ToggleCoolant());
        ToggleEmergencyCommand = new RelayCommand(_ => ToggleEmergency());
    }

    /// <summary>The X axis position in mm.</summary>
    public double X
    {
        get => _x;
        private set
        {
            if (SetProperty(ref _x, value))
            {
                XText = $"{value:0.000}";
            }
        }
    }

    /// <summary>The X position formatted for the DRO.</summary>
    public string XText { get; private set; } = "128.440";

    /// <summary>The Y axis position in mm.</summary>
    public double Y
    {
        get => _y;
        private set
        {
            if (SetProperty(ref _y, value))
            {
                YText = $"{value:0.000}";
            }
        }
    }

    /// <summary>The Y position formatted for the DRO.</summary>
    public string YText { get; private set; } = "-42.180";

    /// <summary>The Z axis position in mm.</summary>
    public double Z
    {
        get => _z;
        private set
        {
            if (SetProperty(ref _z, value))
            {
                ZText = $"{value:0.000}";
            }
        }
    }

    /// <summary>The Z position formatted for the DRO.</summary>
    public string ZText { get; private set; } = "86.020";

    /// <summary>The A axis position in degrees.</summary>
    public double A
    {
        get => _a;
        private set
        {
            if (SetProperty(ref _a, value))
            {
                AText = $"{value:0.000}";
            }
        }
    }

    /// <summary>The A position formatted for the DRO.</summary>
    public string AText { get; private set; } = "0.000";

    /// <summary>The feed override percentage.</summary>
    public double FeedOverride
    {
        get => _feedOverride;
        set
        {
            if (SetProperty(ref _feedOverride, value))
            {
                FeedOverrideText = $"{value:0}%";
            }
        }
    }

    /// <summary>The override formatted for the panel.</summary>
    public string FeedOverrideText { get; private set; } = "100%";

    /// <summary>The spindle load percentage.</summary>
    public double SpindleLoad
    {
        get => _spindleLoad;
        private set
        {
            if (SetProperty(ref _spindleLoad, value))
            {
                SpindleArc = GaugeGeometry.Arc(value, 0d, 100d, 54f, 54f, 42f);
                OnPropertyChanged(nameof(SpindleArc));
                OnPropertyChanged(nameof(SpindleText));
                OnPropertyChanged(nameof(SpindleBrush));
            }
        }
    }

    /// <summary>The spindle load formatted for the gauge.</summary>
    public string SpindleText => $"{SpindleLoad:0}%";

    /// <summary>The colour the spindle arc uses; it turns red above 80% load.</summary>
    public XamlBrush SpindleBrush => SpindleLoad > 80d ? IndustrialPalette.Danger : IndustrialPalette.Warning;

    /// <summary>The spindle speed in rpm.</summary>
    public double SpindleRpm
    {
        get => _spindleRpm;
        private set
        {
            if (SetProperty(ref _spindleRpm, value))
            {
                OnPropertyChanged(nameof(SpindleRpmText));
            }
        }
    }

    /// <summary>The spindle speed formatted for the panel.</summary>
    public string SpindleRpmText => $"{SpindleRpm:0} rpm";

    /// <summary>Whether the program is cycling.</summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(CycleBrush));
                OnPropertyChanged(nameof(CycleText));
            }
        }
    }

    /// <summary>Whether coolant is on.</summary>
    public bool Coolant
    {
        get => _coolant;
        private set
        {
            if (SetProperty(ref _coolant, value))
            {
                OnPropertyChanged(nameof(CoolantBrush));
                OnPropertyChanged(nameof(CoolantText));
            }
        }
    }

    /// <summary>Whether the emergency stop is latched.</summary>
    public bool EmergencyStop
    {
        get => _emergencyStop;
        private set
        {
            if (SetProperty(ref _emergencyStop, value))
            {
                OnPropertyChanged(nameof(EmergencyBrush));
                OnPropertyChanged(nameof(EmergencyText));
                Status = value ? "E-STOP latched · motion inhibited" : "Cycle running · O1204";
            }
        }
    }

    /// <summary>The cycle state label.</summary>
    public string CycleText => IsRunning ? "RUNNING" : "HOLD";

    /// <summary>The cycle state colour.</summary>
    public XamlBrush CycleBrush => IsRunning ? IndustrialPalette.Positive : IndustrialPalette.Warning;

    /// <summary>The coolant state label.</summary>
    public string CoolantText => Coolant ? "COOLANT ON" : "COOLANT OFF";

    /// <summary>The coolant state colour.</summary>
    public XamlBrush CoolantBrush => Coolant ? IndustrialPalette.Signal : IndustrialPalette.Idle;

    /// <summary>The emergency-stop state label.</summary>
    public string EmergencyText => EmergencyStop ? "RESET E-STOP" : "E-STOP";

    /// <summary>The emergency-stop state colour.</summary>
    public XamlBrush EmergencyBrush => IndustrialPalette.Danger;

    /// <summary>The status line.</summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>The program run time.</summary>
    public TimeSpan CycleTime
    {
        get => _cycleTime;
        private set
        {
            if (SetProperty(ref _cycleTime, value))
            {
                CycleTimeText = $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
            }
        }
    }

    /// <summary>The cycle time formatted for the panel.</summary>
    public string CycleTimeText { get; private set; } = "00:42:18";

    /// <summary>The spindle arc path.</summary>
    public string SpindleArc { get; private set; } = string.Empty;

    /// <summary>The spindle tick ring.</summary>
    public string SpindleTicks { get; } = string.Empty;

    /// <summary>The tool table.</summary>
    public ObservableCollection<ToolRow> Tools { get; }

    /// <summary>Jogs an axis; the parameter is <c>axis:delta</c>, such as <c>X:1</c>.</summary>
    public RelayCommand JogCommand { get; }

    /// <summary>Starts or holds the cycle.</summary>
    public RelayCommand ToggleCycleCommand { get; }

    /// <summary>Latches or resets the emergency stop.</summary>
    public RelayCommand ToggleEmergencyCommand { get; }

    /// <summary>Toggles the coolant output.</summary>
    public RelayCommand ToggleCoolantCommand { get; }

    /// <summary>Parses a jog command parameter such as <c>X:1</c>.</summary>
    private void JogFrom(string? parameter)
    {
        if (string.IsNullOrEmpty(parameter))
        {
            return;
        }

        var separator = parameter.IndexOf(':');
        if (separator <= 0
            || !double.TryParse(
                parameter[(separator + 1)..],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var delta))
        {
            return;
        }

        Jog(parameter[..separator], delta);
    }

    /// <summary>Jogs one axis by a signed increment.</summary>
    /// <param name="axis">The axis name, <c>X</c>, <c>Y</c>, <c>Z</c> or <c>A</c>.</param>
    /// <param name="delta">The increment in millimetres or degrees.</param>
    public void Jog(string axis, double delta)
    {
        if (EmergencyStop)
        {
            Status = "E-STOP latched · jog inhibited";
            return;
        }

        switch (axis)
        {
            case "X":
                X += delta;
                break;
            case "Y":
                Y += delta;
                break;
            case "Z":
                Z += delta;
                break;
            case "A":
                A = (A + delta) % 360d;
                break;
        }

        Status = $"Jog {axis} {delta:+0.0;-0.0;0} mm · {FeedOverrideText} override";
    }

    /// <summary>Starts or holds the cycle.</summary>
    public void ToggleCycle()
    {
        if (EmergencyStop)
        {
            Status = "E-STOP latched · cycle inhibited";
            return;
        }

        IsRunning = !IsRunning;
        Status = IsRunning ? "Cycle running · O1204" : "Feed hold · axes stationary";
    }

    /// <summary>Latches or resets the emergency stop.</summary>
    public void ToggleEmergency()
    {
        EmergencyStop = !EmergencyStop;
        if (EmergencyStop)
        {
            IsRunning = false;
        }
    }

    /// <summary>Toggles the coolant output.</summary>
    public void ToggleCoolant()
    {
        Coolant = !Coolant;
        Status = Coolant ? "Coolant on · M08" : "Coolant off · M09";
    }

    /// <summary>Advances the simulated program by one sample.</summary>
    public void Tick()
    {
        if (EmergencyStop)
        {
            SpindleLoad = Math.Max(0d, SpindleLoad - 9d);
            SpindleRpm = Math.Max(0d, SpindleRpm - 900d);
            return;
        }

        if (!IsRunning)
        {
            SpindleLoad = Math.Max(8d, SpindleLoad - 2d);
            SpindleRpm = Math.Max(1200d, SpindleRpm - 240d);
            return;
        }

        var factor = FeedOverride / 100d;
        X += (0.012d * factor) * (_random.NextDouble() + 0.2d);
        Y += (0.006d * factor) * (_random.NextDouble() - 0.5d);
        Z += (0.004d * factor) * (_random.NextDouble() - 0.5d);
        A = (A + (0.35d * factor)) % 360d;
        SpindleLoad = Math.Clamp(SpindleLoad + ((_random.NextDouble() - 0.5d) * 9d), 18d, 92d);
        SpindleRpm = Math.Clamp(SpindleRpm + ((_random.NextDouble() - 0.5d) * 260d), 6400d, 9800d);
        CycleTime += TimeSpan.FromMilliseconds(800);
    }
}

/// <summary>One row of the machine panel's tool table.</summary>
public sealed class ToolRow
{
    /// <summary>Creates a tool row.</summary>
    public ToolRow(string slot, string description, double diameter, double speed)
    {
        Slot = slot;
        Description = description;
        Diameter = diameter;
        Speed = speed;
    }

    /// <summary>The tool slot.</summary>
    public string Slot { get; }

    /// <summary>The tool description.</summary>
    public string Description { get; }

    /// <summary>The tool diameter in millimetres.</summary>
    public double Diameter { get; }

    /// <summary>The spindle speed in rpm.</summary>
    public double Speed { get; }

    /// <summary>The diameter formatted for the table.</summary>
    public string DiameterText => $"Ø{Diameter:0.0}";

    /// <summary>The speed formatted for the table.</summary>
    public string SpeedText => $"{Speed:0} rpm";
}
