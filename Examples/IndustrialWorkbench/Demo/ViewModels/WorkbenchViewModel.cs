using System.Windows.Forms;

namespace IndustrialWorkbench.ViewModels;

/// <summary>
/// The workbench's simulation model: it owns a 150 ms WinForms timer that drifts the plant values, rotates the
/// machinery, appends trend samples and cycles the alarm queue. Controls bind to the properties; user-driven
/// properties (<see cref="Setpoint"/>, <see cref="PumpOn"/>, <see cref="ValveOpen"/>) feed back into the simulation.
/// </summary>
public sealed class WorkbenchViewModel : ObservableObject, IDisposable
{
    private const int TrendLength = 72;
    private static readonly (string Message, int Severity)[] AlarmQueue =
    [
        ("泵组 P-204 振动偏高 · 建议检查轴承", 1),
        ("罐 T-301 液位接近高限 · 请确认排放", 1),
        ("冷却回路压力低 · 联锁待确认", 2),
        ("变频器 F-12 温度预警 · 检查风机", 1),
    ];

    private readonly System.Windows.Forms.Timer _timer;
    private readonly Random _random = new(20261127);
    private readonly DateTime _started = DateTime.Now;
    private double _t;
    private int _tick;
    private int _alarmIndex;
    private int _ackCountdown = -1;
    private bool _disposed;

    private string _runtimeText = "00:00:00";
    private bool _isConnected = true;
    private bool _isRunning = true;
    private bool _isEmergency;
    private double _pressure = 58;
    private double _temperature = 72;
    private double _throughput = 1500;
    private double _tankLevel = 62;
    private double _flowRate = 35;
    private double _setpoint = 60;
    private bool _pumpOn = true;
    private bool _valveOpen = true;
    private double _fanAngle;
    private double _fanSpeed = 720;
    private double _flowPhase;
    private double _pulse;
    private double[] _trendA;
    private double[] _trendB;
    private string _alarmMessage = AlarmQueue[0].Message;
    private int _alarmSeverity = 1;
    private bool _alarmActive = true;
    private bool _alarmAcknowledged;
    private int _alarmCount = 1;
    private bool _ledRun = true;
    private bool _ledWarn;
    private bool _ledAlarm;
    private int _productionTotal = 128430;
    private string _flowLabel = "主给水 · 35.0 m³/h";
    private string _runStateText = "RUNNING";
    private string _fanSpeedText = "720 rpm";

    /// <summary>Creates the timer and seeds the trend buffers.</summary>
    public WorkbenchViewModel()
    {
        _trendA = Enumerable.Repeat(58d, TrendLength).ToArray();
        _trendB = Enumerable.Repeat(35d, TrendLength).ToArray();

        _timer = new System.Windows.Forms.Timer { Interval = 150 };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>The site name shown in the factory status bar.</summary>
    public string StationName => "PLANT-07 / 东区水处理";

    /// <summary>The production line name.</summary>
    public string LineName => "LINE A · 净水机组";

    /// <summary>The simulated refresh interval label.</summary>
    public string UpdateInterval => "150 ms";

    /// <summary>Elapsed runtime, refreshed every tick.</summary>
    public string RuntimeText
    {
        get => _runtimeText;
        private set => SetProperty(ref _runtimeText, value);
    }

    /// <summary>Whether the SCADA link is up.</summary>
    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value);
    }

    /// <summary>The connection label.</summary>
    public string ConnectionText => IsConnected ? "ONLINE" : "OFFLINE";

    /// <summary>Whether the process is running.</summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    /// <summary>Whether the E-STOP is latched.</summary>
    public bool IsEmergency
    {
        get => _isEmergency;
        private set => SetProperty(ref _isEmergency, value);
    }

    /// <summary>Line pressure (0..100).</summary>
    public double Pressure
    {
        get => _pressure;
        private set => SetProperty(ref _pressure, value);
    }

    /// <summary>Loop temperature (°C).</summary>
    public double Temperature
    {
        get => _temperature;
        private set => SetProperty(ref _temperature, value);
    }

    /// <summary>Throughput (m³/h).</summary>
    public double Throughput
    {
        get => _throughput;
        private set => SetProperty(ref _throughput, value);
    }

    /// <summary>Tank level percentage (0..100).</summary>
    public double TankLevel
    {
        get => _tankLevel;
        private set => SetProperty(ref _tankLevel, value);
    }

    /// <summary>Flow rate (m³/h).</summary>
    public double FlowRate
    {
        get => _flowRate;
        private set => SetProperty(ref _flowRate, value);
    }

    /// <summary>The operator setpoint (0..100), driven by the knob.</summary>
    public double Setpoint
    {
        get => _setpoint;
        set => SetProperty(ref _setpoint, Math.Clamp(value, 0d, 100d));
    }

    /// <summary>Whether the pump is enabled; driven by the toggle switch.</summary>
    public bool PumpOn
    {
        get => _pumpOn;
        set => SetProperty(ref _pumpOn, value);
    }

    /// <summary>Whether the valve is open; driven by the valve control.</summary>
    public bool ValveOpen
    {
        get => _valveOpen;
        set => SetProperty(ref _valveOpen, value);
    }

    /// <summary>The fan blade rotation in degrees.</summary>
    public double FanAngle
    {
        get => _fanAngle;
        private set => SetProperty(ref _fanAngle, value);
    }

    /// <summary>The fan speed in rpm.</summary>
    public double FanSpeed
    {
        get => _fanSpeed;
        private set => SetProperty(ref _fanSpeed, value);
    }

    /// <summary>A scrolling phase for the pipe dashes and hazard stripes.</summary>
    public double FlowPhase
    {
        get => _flowPhase;
        private set => SetProperty(ref _flowPhase, value);
    }

    /// <summary>A general blink/pulse phase.</summary>
    public double Pulse
    {
        get => _pulse;
        private set => SetProperty(ref _pulse, value);
    }

    /// <summary>The primary trend series (pressure).</summary>
    public double[] TrendA
    {
        get => _trendA;
        private set => SetProperty(ref _trendA, value);
    }

    /// <summary>The secondary trend series (flow).</summary>
    public double[] TrendB
    {
        get => _trendB;
        private set => SetProperty(ref _trendB, value);
    }

    /// <summary>The current alarm text.</summary>
    public string AlarmMessage
    {
        get => _alarmMessage;
        private set => SetProperty(ref _alarmMessage, value);
    }

    /// <summary>The current alarm severity (0 info, 1 warning, 2 critical).</summary>
    public int AlarmSeverity
    {
        get => _alarmSeverity;
        private set => SetProperty(ref _alarmSeverity, value);
    }

    /// <summary>Whether an alarm is shown.</summary>
    public bool AlarmActive
    {
        get => _alarmActive;
        private set => SetProperty(ref _alarmActive, value);
    }

    /// <summary>Whether the current alarm was acknowledged.</summary>
    public bool AlarmAcknowledged
    {
        get => _alarmAcknowledged;
        private set => SetProperty(ref _alarmAcknowledged, value);
    }

    /// <summary>The pending alarm count.</summary>
    public int AlarmCount
    {
        get => _alarmCount;
        private set => SetProperty(ref _alarmCount, value);
    }

    /// <summary>Whether the run lamp is lit.</summary>
    public bool LedRun
    {
        get => _ledRun;
        private set => SetProperty(ref _ledRun, value);
    }

    /// <summary>Whether the warning lamp is lit.</summary>
    public bool LedWarn
    {
        get => _ledWarn;
        private set => SetProperty(ref _ledWarn, value);
    }

    /// <summary>Whether the alarm lamp is lit (blinking).</summary>
    public bool LedAlarm
    {
        get => _ledAlarm;
        private set => SetProperty(ref _ledAlarm, value);
    }

    /// <summary>The accumulated production count shown in the status bar.</summary>
    public int ProductionTotal
    {
        get => _productionTotal;
        private set => SetProperty(ref _productionTotal, value);
    }

    /// <summary>The pipe caption with the live flow rate.</summary>
    public string FlowLabel
    {
        get => _flowLabel;
        private set => SetProperty(ref _flowLabel, value);
    }

    /// <summary>The line-state text shown by the composite status tile.</summary>
    public string RunStateText
    {
        get => _runStateText;
        private set => SetProperty(ref _runStateText, value);
    }

    /// <summary>The fan-speed text shown by the composite status tile.</summary>
    public string FanSpeedText
    {
        get => _fanSpeedText;
        private set => SetProperty(ref _fanSpeedText, value);
    }

    /// <summary>Latches the E-STOP and drives the plant to a safe state.</summary>
    public void EmergencyStop()
    {
        IsEmergency = true;
        IsRunning = false;
        PumpOn = false;
        ValveOpen = false;
        AlarmMessage = "急停 E-STOP 已触发 · 全线安全停机";
        AlarmSeverity = 2;
        AlarmActive = true;
        AlarmAcknowledged = false;
        _ackCountdown = -1;
    }

    /// <summary>Clears the E-STOP and restarts the line.</summary>
    public void Reset()
    {
        IsEmergency = false;
        IsRunning = true;
        PumpOn = true;
        ValveOpen = true;
        AlarmActive = false;
        AlarmAcknowledged = true;
        AlarmSeverity = 0;
        AlarmMessage = "系统复位 · 重新启动";
        AlarmCount = 0;
        _ackCountdown = 40;
    }

    /// <summary>Acknowledges the current alarm.</summary>
    public void AcknowledgeAlarm()
    {
        if (!AlarmActive)
        {
            return;
        }

        AlarmAcknowledged = true;
        AlarmActive = false;
        AlarmSeverity = 0;
        AlarmMessage = "报警已确认 · 监视自动恢复";
        _ackCountdown = 50;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _tick++;
        _t += 0.15d;
        RuntimeText = (DateTime.Now - _started).ToString(@"hh\:mm\:ss");
        Pulse += 0.32d;
        FlowPhase += 0.25d * (0.8d + (FlowRate / 45d));

        Simulate();
        UpdateAlarm();
        UpdateTrend();
        UpdateIndicators();
    }

    private void Simulate()
    {
        var noise = (_random.NextDouble() - 0.5d) * 4d;

        if (IsEmergency)
        {
            Pressure = Smooth(Pressure, 4d, 0.12d);
            Temperature = Smooth(Temperature, 52d, 0.06d);
            FlowRate = Smooth(FlowRate, 0d, 0.20d);
            TankLevel = Smooth(TankLevel, Math.Max(4d, TankLevel - 0.3d), 0.3d);
            FanSpeed = Smooth(FanSpeed, 0d, 0.20d);
            Throughput = Smooth(Throughput, 0d, 0.20d);
        }
        else
        {
            var demand = PumpOn && ValveOpen ? 18d + (Setpoint * 0.8d) : 0d;
            FlowRate = Smooth(FlowRate, demand, 0.12d);

            var targetSpeed = PumpOn ? 540d + (Setpoint * 12d) : 0d;
            FanSpeed = Smooth(FanSpeed, targetSpeed, 0.10d);

            var pressureTarget = PumpOn
                ? (25d + (Setpoint * 0.55d) + (Math.Sin(_t * 0.7d) * 3d) + noise)
                : 8d + (Math.Sin(_t) * 1.2d);
            Pressure = Smooth(Pressure, Math.Clamp(pressureTarget, 0d, 100d), 0.15d);

            var temperatureTarget = 54d + (PumpOn ? 18d : 0d) + (Math.Sin(_t * 0.33d) * 4d);
            Temperature = Smooth(Temperature, Math.Clamp(temperatureTarget, 30d, 130d), 0.08d);

            var tankTarget = 58d + (Math.Sin(_t * 0.15d) * 12d) - (FlowRate * 0.06d);
            TankLevel = Smooth(TankLevel, Math.Clamp(tankTarget, 0d, 100d), 0.06d);

            Throughput = Smooth(Throughput, 220d + (FlowRate * 24d), 0.15d);
        }

        FanAngle = (FanAngle + (FanSpeed * 0.02d)) % 360d;
        FlowLabel = $"主给水 · {FlowRate:0.0} m³/h";
        FanSpeedText = $"{FanSpeed:0} rpm";
        if (_tick % 8 == 0)
        {
            ProductionTotal += (int)Math.Round(Throughput * 0.02d);
        }
    }

    private void UpdateAlarm()
    {
        if (IsEmergency)
        {
            return;
        }

        if (_ackCountdown > 0)
        {
            _ackCountdown--;
            if (_ackCountdown == 0)
            {
                _ackCountdown = -1;
                ActivateNextAlarm();
            }

            return;
        }

        if (!AlarmActive)
        {
            ActivateNextAlarm();
            return;
        }

        if (!AlarmAcknowledged && _tick % 90 == 0)
        {
            ActivateNextAlarm();
        }
    }

    private void ActivateNextAlarm()
    {
        _alarmIndex = (_alarmIndex + 1) % AlarmQueue.Length;
        AlarmMessage = AlarmQueue[_alarmIndex].Message;
        AlarmSeverity = AlarmQueue[_alarmIndex].Severity;
        AlarmActive = true;
        AlarmAcknowledged = false;
        AlarmCount++;
    }

    private void UpdateTrend()
    {
        TrendA = Shift(TrendA, Math.Clamp(Pressure, 0d, 100d));
        TrendB = Shift(TrendB, Math.Clamp(FlowRate, 0d, 100d));
    }

    private void UpdateIndicators()
    {
        LedRun = IsRunning;
        LedWarn = Pressure > 72d || TankLevel > 86d;
        LedAlarm = IsEmergency || (AlarmActive && AlarmSeverity >= 2);
        RunStateText = IsRunning ? "RUNNING" : IsEmergency ? "E-STOP" : "STOPPED";
    }

    private static double Smooth(double current, double target, double factor)
        => current + ((target - current) * factor);

    private static double[] Shift(double[] series, double value)
    {
        var next = new double[series.Length];
        Array.Copy(series, 1, next, 0, series.Length - 1);
        next[^1] = value;
        return next;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer.Dispose();
    }
}
