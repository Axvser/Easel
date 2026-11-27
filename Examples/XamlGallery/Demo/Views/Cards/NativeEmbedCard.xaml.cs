using System.Windows.Forms;

namespace Demo.Views;

/// <summary>
/// A native <c>GroupBox</c> whose display rectangle hosts an Easel subtree: ProgressBars, text and a visual
/// button. The native ListBox selection drives that subtree, and the visual button writes back into the list.
/// </summary>
public sealed partial class NativeEmbedCard : ThemedStackPanel
{
    private readonly List<Meter> _meters =
    [
        new("Pump A", "Line 1", 62, 48, 71),
        new("Valve B", "Line 2", 38, 64, 44),
        new("Tank C", "Line 3", 81, 55, 29),
        new("Pump D", "Line 4", 47, 72, 58),
    ];

    private bool _detached;
    private bool _repairScheduled;
    private bool _repairing;
    private bool _syncing;

    /// <summary>Creates the view and fills the native list with the sample meters.</summary>
    public NativeEmbedCard()
    {
        InitializeComponent();

        ParentChanged += (_, _) =>
        {
            if (ParentElement is null)
            {
                _detached = true;
            }
        };

        // 视觉内容首次落位发生在原生 GroupBox 真正移动之前（子 HWND 的移动排到渲染之后），
        // 内容表面会停在控件默认显示区。等原生布局稳定后让 GroupBox 重排一次，内容就铺满显示区。
        readoutGroup.Native.Layout += OnReadoutLayout;

        foreach (var meter in _meters)
        {
            meterList.Native.Items.Add(Describe(meter));
        }

        meterList.Native.SelectedIndex = 0;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _detached = true;
            if (readoutGroup.Native is { IsDisposed: false } box)
            {
                box.Layout -= OnReadoutLayout;
            }
        }

        base.Dispose(disposing);
    }

    private static string Describe(Meter meter) => $"{meter.Name}  ·  {meter.Line}  ·  {meter.Flow}%";

    private void OnReadoutLayout(object? sender, LayoutEventArgs e)
    {
        if (_detached || _repairScheduled || _repairing)
        {
            return;
        }

        if (readoutGroup.Native is not { IsDisposed: false, IsHandleCreated: true } box)
        {
            return;
        }

        var content = readoutGroup.ContentSurface;
        if (content is null || content.Bounds == box.DisplayRectangle)
        {
            return;
        }

        _repairScheduled = true;
        box.BeginInvoke(new Action(RepairContentSurface));
    }

    private void RepairContentSurface()
    {
        _repairScheduled = false;
        if (_detached || readoutGroup.Native is not { IsDisposed: false } box)
        {
            return;
        }

        _repairing = true;
        try
        {
            box.PerformLayout();
        }
        finally
        {
            _repairing = false;
        }
    }

    private Meter? CurrentMeter()
    {
        if (_detached || meterList.Native is not { IsDisposed: false } list)
        {
            return null;
        }

        var index = list.SelectedIndex;
        return index >= 0 && index < _meters.Count ? _meters[index] : null;
    }

    private void OnMeterSelected(object? sender, System.EventArgs e)
    {
        if (_detached || _syncing)
        {
            return;
        }

        if (CurrentMeter() is { } meter)
        {
            ShowMeter(meter);
            readoutStatus.Text = "Selected from the native ListBox.";
        }
    }

    private void OnNextClick(object? sender, System.EventArgs e)
    {
        if (_detached || meterList.Native is not { IsDisposed: false } list)
        {
            return;
        }

        // 原生按钮推进 ListBox 的选择，SelectedIndexChanged 再把视觉读数刷新一遍。
        list.SelectedIndex = (list.SelectedIndex + 1) % _meters.Count;
    }

    private void OnBoostClick(object? sender, System.EventArgs e)
    {
        if (_detached || meterList.Native is not { IsDisposed: false } list)
        {
            return;
        }

        var index = list.SelectedIndex;
        if (index < 0 || index >= _meters.Count)
        {
            return;
        }

        var meter = _meters[index];
        meter.Flow = System.Math.Min(100, meter.Flow + 8);

        // 替换条目会清空选择；在 _syncing 里换回同一项，避免 SelectedIndexChanged 再进来一次。
        _syncing = true;
        try
        {
            list.Items[index] = Describe(meter);
            list.SelectedIndex = index;
        }
        finally
        {
            _syncing = false;
        }

        ShowMeter(meter);
        readoutStatus.Text = $"Visual Button wrote {meter.Flow}% back to the native ListBox item.";
    }

    private void ShowMeter(Meter meter)
    {
        if (_detached)
        {
            return;
        }

        meterTitle.Text = $"{meter.Name}  ·  {meter.Line}";
        meterSubtitle.Text = "Nested visual surface inside the native GroupBox.";
        flowBar.Value = meter.Flow;
        pressureBar.Value = meter.Pressure;
        temperatureBar.Value = meter.Temperature;
    }

    private sealed class Meter(string name, string line, int flow, int pressure, int temperature)
    {
        public string Name { get; } = name;

        public string Line { get; } = line;

        public int Flow { get; set; } = flow;

        public int Pressure { get; } = pressure;

        public int Temperature { get; } = temperature;
    }
}
