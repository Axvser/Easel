using System.Collections.ObjectModel;
using VeloxDev.MVVM;

namespace Demo.ViewModels;

/// <summary>The dashboard page state: refresh command, conversion gauge and the live status line.</summary>
public sealed partial class DashboardViewModel
{
    [VeloxProperty] private string _status = "Live · updated just now";
    [VeloxProperty] private double _conversion = 68;
    [VeloxProperty] private string _lastRefresh = "09:41";
    private int _refreshes;

    [VeloxCommand]
    private void Refresh()
    {
        _refreshes++;
        LastRefresh = DateTime.Now.ToString("HH:mm:ss");
        Conversion = 55 + ((_refreshes * 9) % 40);
        Status = $"Refreshed {_refreshes}× · every source healthy";
    }
}

/// <summary>The settings page state: two-way bound fields and a save command that reports the snapshot.</summary>
public sealed partial class SettingsViewModel
{
    [VeloxProperty] private bool? _notifications = true;
    [VeloxProperty] private bool _autoSave = true;
    [VeloxProperty] private double _volume = 62;
    [VeloxProperty] private string _displayName = "Ada Lovelace";
    [VeloxProperty] private int _languageIndex = 1;
    [VeloxProperty] private string _status = "No changes saved yet";

    [VeloxCommand]
    private void Save()
        => Status = $"Saved · notifications {(Notifications == true ? "on" : "off")} · auto-save {(AutoSave ? "on" : "off")} · volume {Volume:F0}% · {DisplayName}";
}

/// <summary>The music page state: transport toggle, progress and the selected track.</summary>
public sealed partial class PlayerViewModel
{
    [VeloxProperty] private bool _isPlaying;
    [VeloxProperty] private string _playLabel = "Play";
    [VeloxProperty] private double _progress;
    [VeloxProperty] private string _trackTitle = "Aurora Drift";
    [VeloxProperty] private string _artist = "Easel Sound System";
    [VeloxProperty] private string? _selectedTrack;

    /// <summary>Creates the page state and selects the first track.</summary>
    public PlayerViewModel()
    {
        SelectedTrack = Tracks[0];
    }

    [VeloxCommand]
    private void TogglePlayback()
    {
        IsPlaying = !IsPlaying;
        PlayLabel = IsPlaying ? "Pause" : "Play";
    }

    /// <summary>The playlist shown by the page.</summary>
    public ObservableCollection<string> Tracks { get; } =
    [
        "Aurora Drift",
        "Perspective",
        "Gradient Bloom",
    ];

    partial void OnSelectedTrackChanged(string? oldValue, string? newValue)
    {
        if (newValue is not null)
        {
            TrackTitle = newValue;
        }
    }
}

/// <summary>One chat bubble.</summary>
public sealed class ChatMessage
{
    /// <summary>Creates a message.</summary>
    public ChatMessage(string author, string text, bool mine)
    {
        Author = author;
        Text = text;
        Mine = mine;
    }

    /// <summary>The author line.</summary>
    public string Author { get; }

    /// <summary>The message body.</summary>
    public string Text { get; }

    /// <summary>Whether the message was sent from this side.</summary>
    public bool Mine { get; }
}

/// <summary>The chat page state: message collection, draft composer and send command.</summary>
public sealed partial class ChatViewModel
{
    [VeloxProperty] private string _draft = string.Empty;
    [VeloxProperty] private string _status = "3 messages · visual item template";

    /// <summary>Creates the sample conversation.</summary>
    public ChatViewModel()
    {
        Messages.Add(new ChatMessage("Ada", "The shell is XAML now — even the split sashes.", false));
        Messages.Add(new ChatMessage("You", "And the cards bind to view models instead of building visuals.", true));
        Messages.Add(new ChatMessage("Ada", "Ship it. The 3D demo is the best part.", false));
    }

    /// <summary>The conversation rows.</summary>
    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [VeloxCommand(canValidate: true)]
    private void Send()
    {
        var text = Draft.Trim();
        if (text.Length == 0)
        {
            return;
        }

        Messages.Add(new ChatMessage("You", text, true));
        Draft = string.Empty;
        Status = $"{Messages.Count} messages · visual item template";
    }

    private partial bool CanExecuteSendCommand(object? parameter) => !string.IsNullOrWhiteSpace(_draft);

    partial void OnDraftChanged(string oldValue, string newValue) => SendCommand.Notify();
}
