using Easel.Visuals;
using IndustrialWorkbench.Views.Controls;

namespace IndustrialWorkbench.ViewModels;

/// <summary>One live property row shown in a control's detail readout.</summary>
public readonly record struct ReadoutRow(string Name, string Value);

/// <summary>The catalog entry for one industrial control.</summary>
public sealed class ControlInfo
{
    /// <summary>The stable id used by selection and capture.</summary>
    public required string Id { get; init; }

    /// <summary>The display title.</summary>
    public required string Title { get; init; }

    /// <summary>The catalog group.</summary>
    public required string Group { get; init; }

    /// <summary>A one-sentence description.</summary>
    public required string Summary { get; init; }

    /// <summary>The XAML usage snippet shown on the detail page.</summary>
    public required string Usage { get; init; }

    /// <summary>Creates the large interactive instance for the detail page.</summary>
    public required Func<VisualElement> CreateView { get; init; }

    /// <summary>Reads the live property rows for the readout column.</summary>
    public required Func<WorkbenchViewModel, IReadOnlyList<ReadoutRow>> Readout { get; init; }
}

/// <summary>The static catalog of the industrial control kit.</summary>
public static class ControlCatalog
{
    /// <summary>The id of the overview entry, always pinned first in the tree.</summary>
    public const string OverviewId = "overview";

    /// <summary>The group order used by the catalog tree.</summary>
    public static readonly string[] Groups =
    [
        "仪表",
        "状态与报警",
        "流程与设备",
        "数据与趋势",
        "操作",
    ];

    /// <summary>Builds every catalog entry.</summary>
    public static IReadOnlyList<ControlInfo> Create() =>
    [
        new ControlInfo
        {
            Id = "gauge.circular",
            Title = "CircularGauge · 圆形仪表",
            Group = "仪表",
            Summary = "270° 刻度弧、警戒色带、指针与发光弧，中心显示实时数值。",
            Usage =
                "<ind:CircularGauge Title=\"主泵压力\" Unit=\"bar\"\n" +
                "                  Minimum=\"0\" Maximum=\"100\"\n" +
                "                  Value=\"{Binding Pressure}\"\n" +
                "                  WarnThreshold=\"70\" DangerThreshold=\"90\" />",
            CreateView = static () => new CircularGaugeView(),
            Readout = static vm =>
            [
                new("Value", vm.Pressure.ToString("0.0")),
                new("Warn", "70"),
                new("Danger", "90"),
                new("Unit", "bar"),
            ],
        },
        new ControlInfo
        {
            Id = "gauge.linear",
            Title = "LinearGauge · 条形量表",
            Group = "仪表",
            Summary = "水平条形量表，带警戒区间、填充头部标记和底部刻度。",
            Usage =
                "<ind:LinearGauge Title=\"回流流量\" Unit=\"m³/h\"\n" +
                "                 Minimum=\"0\" Maximum=\"100\"\n" +
                "                 Value=\"{Binding FlowRate}\"\n" +
                "                 WarnThreshold=\"65\" DangerThreshold=\"85\" />",
            CreateView = static () => new LinearGaugeView(),
            Readout = static vm =>
            [
                new("Value", vm.FlowRate.ToString("0.0")),
                new("Setpoint", vm.Setpoint.ToString("0")),
                new("Warn", "65"),
                new("Danger", "85"),
            ],
        },
        new ControlInfo
        {
            Id = "readout.sevensegment",
            Title = "SevenSegmentDisplay · 七段数码",
            Group = "仪表",
            Summary = "七段数码管，支持小数、负号、单位和发光段。",
            Usage =
                "<ind:SevenSegmentDisplay Value=\"{Binding Throughput}\"\n" +
                "                        Digits=\"5\" Decimals=\"1\" Unit=\"m³/h\"\n" +
                "                        SegmentBrush=\"{StaticResource HmiAmber}\" />",
            CreateView = static () => new SevenSegmentDisplayView(),
            Readout = static vm =>
            [
                new("Value", vm.Throughput.ToString("0.0")),
                new("Digits", "5"),
                new("Decimals", "1"),
                new("Unit", "m³/h"),
            ],
        },
        new ControlInfo
        {
            Id = "status.led",
            Title = "LedLamp · 指示灯",
            Group = "状态与报警",
            Summary = "工业指示灯：圆顶高光、外发光晕、可选闪烁与文字标签。",
            Usage =
                "<ind:LedLamp Width=\"44\" Height=\"56\" Label=\"RUN\"\n" +
                "              IsOn=\"{Binding LedRun}\"\n" +
                "              LampColor=\"{StaticResource HmiNormal}\" />",
            CreateView = static () => new LedLampView(),
            Readout = static vm =>
            [
                new("RUN", vm.LedRun ? "ON" : "OFF"),
                new("WARN", vm.LedWarn ? "ON" : "OFF"),
                new("ALARM", vm.LedAlarm ? "ON" : "OFF"),
                new("Pulse", vm.Pulse.ToString("0.0")),
            ],
        },
        new ControlInfo
        {
            Id = "status.alarm",
            Title = "AlarmBanner · 报警横幅",
            Group = "状态与报警",
            Summary = "按严重级别着色并可点击确认的报警横幅；危险级别会脉冲闪烁。",
            Usage =
                "<ind:AlarmBanner Message=\"{Binding AlarmMessage}\"\n" +
                "                 Severity=\"{Binding AlarmSeverity}\"\n" +
                "                 IsActive=\"{Binding AlarmActive}\"\n" +
                "                 Acknowledged=\"OnAlarmAcknowledged\" />",
            CreateView = static () => new AlarmBannerView(),
            Readout = static vm =>
            [
                new("Severity", vm.AlarmSeverity.ToString()),
                new("Active", vm.AlarmActive ? "TRUE" : "FALSE"),
                new("Acked", vm.AlarmAcknowledged ? "YES" : "NO"),
                new("Pending", vm.AlarmCount.ToString()),
            ],
        },
        new ControlInfo
        {
            Id = "status.tile",
            Title = "StatusTile · 状态卡片",
            Group = "状态与报警",
            Summary = "XAML + code-behind 复合控件：指示灯、标题、大号数值与说明。",
            Usage =
                "<ind:StatusTile Title=\"LINE STATUS\" Value=\"RUNNING\"\n" +
                "                Caption=\"P-204 · 720 rpm\"\n" +
                "                IsOn=\"{Binding LedRun}\"\n" +
                "                LampBrush=\"{StaticResource HmiNormal}\" />",
            CreateView = static () => new StatusTileView(),
            Readout = static vm =>
            [
                new("Title", "LINE STATUS"),
                new("Value", vm.IsRunning ? "RUNNING" : "STOPPED"),
                new("Caption", $"{vm.FanSpeed:0} rpm"),
                new("Lamp", vm.LedRun ? "ON" : "OFF"),
            ],
        },
        new ControlInfo
        {
            Id = "process.tank",
            Title = "TankLevel · 罐体液位",
            Group = "流程与设备",
            Summary = "立式罐体液位：液面波动、刻度、百分比和上下限报警。",
            Usage =
                "<ind:TankLevel Title=\"T-301 清水罐\" Unit=\"%\"\n" +
                "               Value=\"{Binding TankLevel}\"\n" +
                "               LowLimit=\"15\" HighLimit=\"85\"\n" +
                "               Phase=\"{Binding FlowPhase}\" />",
            CreateView = static () => new TankLevelView(),
            Readout = static vm =>
            [
                new("Level", vm.TankLevel.ToString("0.0") + " %"),
                new("Low", "15"),
                new("High", "85"),
                new("State", vm.TankLevel > 85 ? "HIGH" : vm.TankLevel < 15 ? "LOW" : "NORMAL"),
            ],
        },
        new ControlInfo
        {
            Id = "process.pipe",
            Title = "PipeFlow · 管道流动",
            Group = "流程与设备",
            Summary = "带流动虚线与方向箭头的管道，速度随流量变化。",
            Usage =
                "<ind:PipeFlow Label=\"{Binding FlowLabel}\"\n" +
                "              IsFlowing=\"{Binding IsRunning}\"\n" +
                "              FlowRate=\"{Binding FlowRate}\"\n" +
                "              Phase=\"{Binding FlowPhase}\" />",
            CreateView = static () => new PipeFlowView(),
            Readout = static vm =>
            [
                new("Flowing", vm.IsRunning ? "TRUE" : "FALSE"),
                new("Rate", vm.FlowRate.ToString("0.0") + " m³/h"),
                new("Direction", "L → R"),
                new("Valve", vm.ValveOpen ? "OPEN" : "CLOSED"),
            ],
        },
        new ControlInfo
        {
            Id = "process.fan",
            Title = "MotorFan · 电机风扇",
            Group = "流程与设备",
            Summary = "旋转叶轮、转速读数与运行光晕。",
            Usage =
                "<ind:MotorFan Label=\"P-204\"\n" +
                "              Angle=\"{Binding FanAngle}\"\n" +
                "              Speed=\"{Binding FanSpeed}\"\n" +
                "              IsRunning=\"{Binding PumpOn}\" />",
            CreateView = static () => new MotorFanView(),
            Readout = static vm =>
            [
                new("Speed", vm.FanSpeed.ToString("0") + " rpm"),
                new("Angle", vm.FanAngle.ToString("0") + "°"),
                new("Pump", vm.PumpOn ? "ON" : "OFF"),
                new("Running", vm.IsRunning ? "TRUE" : "FALSE"),
            ],
        },
        new ControlInfo
        {
            Id = "process.valve",
            Title = "Valve · 阀门",
            Group = "流程与设备",
            Summary = "闸阀符号：蝶形阀体、阀杆与手轮，点击切换开闭。",
            Usage =
                "<ind:Valve Title=\"V-12 进水阀\"\n" +
                "           IsOpen=\"{Binding ValveOpen, Mode=TwoWay}\" />",
            CreateView = static () => new ValveView(),
            Readout = static vm =>
            [
                new("State", vm.ValveOpen ? "OPEN" : "CLOSED"),
                new("Flow", vm.FlowRate.ToString("0.0") + " m³/h"),
                new("Interaction", "click to toggle"),
            ],
        },
        new ControlInfo
        {
            Id = "process.hazard",
            Title = "HazardStripe · 警示条",
            Group = "流程与设备",
            Summary = "斜向警示条纹装饰条，可随相位滚动。",
            Usage =
                "<ind:HazardStripe Height=\"14\"\n" +
                "                  Phase=\"{Binding FlowPhase}\"\n" +
                "                  StripeBrush=\"{StaticResource HmiAmber}\" />",
            CreateView = static () => new HazardStripeView(),
            Readout = static vm =>
            [
                new("Phase", vm.FlowPhase.ToString("0.0")),
                new("Stripe", "10 px"),
                new("Angle", "45°"),
            ],
        },
        new ControlInfo
        {
            Id = "data.trend",
            Title = "TrendChart · 实时趋势",
            Group = "数据与趋势",
            Summary = "示波式趋势图：网格、双序列、面积填充与实时头部标记。",
            Usage =
                "<ind:TrendChart Title=\"压力 / 流量\" Unit=\"%\"\n" +
                "                SeriesA=\"{Binding TrendA}\"\n" +
                "                SeriesB=\"{Binding TrendB}\"\n" +
                "                Minimum=\"0\" Maximum=\"100\" />",
            CreateView = static () => new TrendChartView(),
            Readout = static vm =>
            [
                new("Pressure", vm.Pressure.ToString("0.0")),
                new("Flow", vm.FlowRate.ToString("0.0")),
                new("Samples", vm.TrendA.Length.ToString()),
                new("Range", "0 – 100"),
            ],
        },
        new ControlInfo
        {
            Id = "control.knob",
            Title = "Knob · 旋钮",
            Group = "操作",
            Summary = "可拖动旋钮：指针、刻度、活动弧，两向绑定写回设定值。",
            Usage =
                "<ind:Knob Title=\"SETPOINT\" Unit=\"%\" Minimum=\"0\" Maximum=\"100\"\n" +
                "          Value=\"{Binding Setpoint, Mode=TwoWay}\" />",
            CreateView = static () => new KnobView(),
            Readout = static vm =>
            [
                new("Setpoint", vm.Setpoint.ToString("0")),
                new("Pressure", vm.Pressure.ToString("0.0")),
                new("Flow", vm.FlowRate.ToString("0.0")),
                new("Interaction", "drag / wheel / arrows"),
            ],
        },
        new ControlInfo
        {
            Id = "control.toggle",
            Title = "ToggleSwitch · 拨杆开关",
            Group = "操作",
            Summary = "工业拨杆：点击或空格切换，两向绑定控制泵启停。",
            Usage =
                "<ind:ToggleSwitch Title=\"PUMP P-204\"\n" +
                "                  IsOn=\"{Binding PumpOn, Mode=TwoWay}\" />",
            CreateView = static () => new ToggleSwitchView(),
            Readout = static vm =>
            [
                new("Pump", vm.PumpOn ? "ON" : "OFF"),
                new("Running", vm.IsRunning ? "TRUE" : "FALSE"),
                new("Flow", vm.FlowRate.ToString("0.0")),
                new("Interaction", "click / space"),
            ],
        },
    ];
}
