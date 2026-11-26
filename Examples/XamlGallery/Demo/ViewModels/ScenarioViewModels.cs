using System.Collections.ObjectModel;
using Demo.Mvvm;

namespace Demo.ViewModels;

/// <summary>The dashboard page state: refresh command, conversion gauge and the live status line.</summary>
public sealed class DashboardViewModel : ObservableObject
{
    private string _status = "Live · updated just now";
    private double _conversion = 68;
    private string _lastRefresh = "09:41";
    private int _refreshes;

    /// <summary>Creates the dashboard state and its refresh command.</summary>
    public DashboardViewModel()
    {
        RefreshCommand = new RelayCommand(_ => Refresh());
    }

    /// <summary>The live status line.</summary>
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    /// <summary>The conversion gauge value.</summary>
    public double Conversion
    {
        get => _conversion;
        set => SetProperty(ref _conversion, value);
    }

    /// <summary>The last refresh time.</summary>
    public string LastRefresh
    {
        get => _lastRefresh;
        set => SetProperty(ref _lastRefresh, value);
    }

    /// <summary>Refreshes the sample values.</summary>
    public RelayCommand RefreshCommand { get; }

    private void Refresh()
    {
        _refreshes++;
        LastRefresh = DateTime.Now.ToString("HH:mm:ss");
        Conversion = 55 + ((_refreshes * 9) % 40);
        Status = $"Refreshed {_refreshes}× · every source healthy";
    }
}

/// <summary>The settings page state: two-way bound fields and a save command that reports the snapshot.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private bool? _notifications = true;
    private bool _autoSave = true;
    private double _volume = 62;
    private string _displayName = "Ada Lovelace";
    private int _languageIndex = 1;
    private string _status = "No changes saved yet";

    /// <summary>Creates the settings state and its save command.</summary>
    public SettingsViewModel()
    {
        SaveCommand = new RelayCommand(_ => Save());
    }

    /// <summary>Whether desktop notifications are enabled.</summary>
    public bool? Notifications
    {
        get => _notifications;
        set => SetProperty(ref _notifications, value);
    }

    /// <summary>Whether edits save automatically.</summary>
    public bool AutoSave
    {
        get => _autoSave;
        set => SetProperty(ref _autoSave, value);
    }

    /// <summary>The master volume.</summary>
    public double Volume
    {
        get => _volume;
        set => SetProperty(ref _volume, value);
    }

    /// <summary>The display name.</summary>
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    /// <summary>The selected language option.</summary>
    public int LanguageIndex
    {
        get => _languageIndex;
        set => SetProperty(ref _languageIndex, value);
    }

    /// <summary>The save status line.</summary>
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    /// <summary>Saves the current settings snapshot.</summary>
    public RelayCommand SaveCommand { get; }

    private void Save()
        => Status = $"Saved · notifications {(Notifications == true ? "on" : "off")} · auto-save {(AutoSave ? "on" : "off")} · volume {Volume:F0}% · {DisplayName}";
}

/// <summary>The music page state: transport toggle, progress and the selected track.</summary>
public sealed class PlayerViewModel : ObservableObject
{
    private bool _isPlaying;
    private string _playLabel = "Play";
    private double _progress;
    private string _trackTitle = "Aurora Drift";
    private string _artist = "Easel Sound System";
    private string? _selectedTrack;

    /// <summary>Creates the page state and selects the first track.</summary>
    public PlayerViewModel()
    {
        TogglePlaybackCommand = new RelayCommand(_ => TogglePlayback());
        SelectedTrack = Tracks[0];
    }

    /// <summary>Whether the player is running.</summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetProperty(ref _isPlaying, value);
    }

    /// <summary>The transport button label.</summary>
    public string PlayLabel
    {
        get => _playLabel;
        set => SetProperty(ref _playLabel, value);
    }

    /// <summary>The playback progress in percent.</summary>
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    /// <summary>The current track title.</summary>
    public string TrackTitle
    {
        get => _trackTitle;
        set => SetProperty(ref _trackTitle, value);
    }

    /// <summary>The current artist.</summary>
    public string Artist
    {
        get => _artist;
        set => SetProperty(ref _artist, value);
    }

    /// <summary>The selected playlist row.</summary>
    public string? SelectedTrack
    {
        get => _selectedTrack;
        set
        {
            if (SetProperty(ref _selectedTrack, value))
            {
                OnSelectedTrackChanged(value);
            }
        }
    }

    /// <summary>Starts or pauses playback.</summary>
    public RelayCommand TogglePlaybackCommand { get; }

    /// <summary>The playlist shown by the page.</summary>
    public ObservableCollection<string> Tracks { get; } =
    [
        "Aurora Drift",
        "Perspective",
        "Gradient Bloom",
    ];

    private void TogglePlayback()
    {
        IsPlaying = !IsPlaying;
        PlayLabel = IsPlaying ? "Pause" : "Play";
    }

    private void OnSelectedTrackChanged(string? newValue)
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
public sealed class ChatViewModel : ObservableObject
{
    private string _draft = string.Empty;
    private string _status = "3 messages · visual item template";

    /// <summary>Creates the sample conversation.</summary>
    public ChatViewModel()
    {
        Messages.Add(new ChatMessage("Ada", "The shell is XAML now — even the split sashes.", false));
        Messages.Add(new ChatMessage("You", "And the cards bind to view models instead of building visuals.", true));
        Messages.Add(new ChatMessage("Ada", "Ship it. The 3D demo is the best part.", false));
        SendCommand = new RelayCommand(_ => Send(), _ => CanExecuteSendCommand(_));
    }

    /// <summary>The draft text.</summary>
    public string Draft
    {
        get => _draft;
        set
        {
            if (SetProperty(ref _draft, value))
            {
                SendCommand.Notify();
            }
        }
    }

    /// <summary>The chat status line.</summary>
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    /// <summary>The conversation rows.</summary>
    public ObservableCollection<ChatMessage> Messages { get; } = [];

    /// <summary>Sends the current draft.</summary>
    public RelayCommand SendCommand { get; }

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

    private bool CanExecuteSendCommand(object? parameter) => !string.IsNullOrWhiteSpace(_draft);
}
