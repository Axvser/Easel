using System.ComponentModel;
using System.Windows.Forms;

namespace Demo.Views;

/// <summary>
/// A native <see cref="PropertyGrid"/> editing a plain CLR settings object, with a visual summary and visual
/// buttons writing the same object back to the grid.
/// </summary>
public sealed partial class NativePropertyCard : ThemedStackPanel
{
    private readonly MeterSettings _settings = new();
    private bool _detached;

    /// <summary>Creates the view and hands the settings object to the native grid.</summary>
    public NativePropertyCard()
    {
        InitializeComponent();

        settingsGrid.Native.SelectedObject = _settings;
        settingsGrid.Native.HelpVisible = true;
        UpdateSummary("Edit a property in the native grid.");
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _detached = true;
        }

        base.Dispose(disposing);
    }

    private void OnPropertyValueChanged(object? sender, PropertyValueChangedEventArgs e)
    {
        if (_detached)
        {
            return;
        }

        var label = e.ChangedItem?.Label ?? "property";
        UpdateSummary($"Grid changed {label}; the visual summary follows the same CLR object.");
    }

    private void OnResetClick(object? sender, System.EventArgs e)
    {
        if (_detached)
        {
            return;
        }

        _settings.Reset();

        // PropertyGrid 不认识 INotifyPropertyChanged；代码改了对象要自己 Refresh。
        settingsGrid.Native.Refresh();
        UpdateSummary("Defaults restored; PropertyGrid.Refresh() shows the values.");
    }

    private void OnDoubleCapacityClick(object? sender, System.EventArgs e)
    {
        if (_detached)
        {
            return;
        }

        _settings.Capacity = System.Math.Clamp(_settings.Capacity * 2, 10, 10000);
        settingsGrid.Native.Refresh();
        UpdateSummary("Visual Button doubled capacity; the grid was refreshed programmatically.");
    }

    private void UpdateSummary(string message)
    {
        if (_detached)
        {
            return;
        }

        settingsTitle.Text = $"{_settings.Name}  ·  {_settings.Line}";
        settingsLine.Text =
            $"Capacity {_settings.Capacity}  ·  alarm at {_settings.AlarmLevel}%  ·  auto-start {(_settings.AutoStart ? "on" : "off")}";
        alarmBar.Value = System.Math.Clamp(_settings.AlarmLevel, 0, 100);
        dirtyText.Text = message;
    }

    private sealed class MeterSettings
    {
        public MeterSettings() => Reset();

        [Category("Identity")]
        [DisplayName("Meter name")]
        [Description("Shown by the visual summary beside the grid.")]
        public string Name { get; set; } = string.Empty;

        [Category("Identity")]
        [DisplayName("Production line")]
        public string Line { get; set; } = string.Empty;

        [Category("Range")]
        [DisplayName("Capacity")]
        [Description("Units per hour; the visual button can double it.")]
        public int Capacity { get; set; }

        [Category("Range")]
        [DisplayName("Alarm level (%)")]
        [Description("Drives the visual alarm bar.")]
        public int AlarmLevel { get; set; }

        [Category("Behaviour")]
        [DisplayName("Auto start")]
        public bool AutoStart { get; set; }

        [Category("Behaviour")]
        public string Notes { get; set; } = string.Empty;

        public void Reset()
        {
            Name = "Pump A";
            Line = "Line 1";
            Capacity = 120;
            AlarmLevel = 75;
            AutoStart = true;
            Notes = "Inspect every 500 hours.";
        }
    }
}
