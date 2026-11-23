using System.ComponentModel;
using System.Windows.Media;
using WinFormsTimer = System.Windows.Forms.Timer;
using Demo.ViewModels;

namespace Demo.Views.Cards;

/// <summary>The music page: real embedded audio through WPF's MediaPlayer, with XAML transport bindings.</summary>
public sealed partial class PlayerView : ThemedDockPanel
{
    private static readonly Dictionary<string, string> TrackFiles = new(StringComparer.Ordinal)
    {
        ["Aurora Drift"] = "aurora-drift.mp3",
        ["Perspective"] = "perspective.mp3",
        ["Gradient Bloom"] = "gradient-bloom.mp3",
    };

    private readonly MediaPlayer _player = new();
    private readonly WinFormsTimer _timer = new() { Interval = 200 };
    private PlayerViewModel? _viewModel;
    private string? _currentTrack;
    private bool _updatingFromPlayer;

    /// <summary>Creates the page, attaches the view model and opens the first track.</summary>
    public PlayerView()
    {
        DataContext = new PlayerViewModel();
        InitializeComponent();

        _viewModel = (PlayerViewModel)DataContext;
        _viewModel.PropertyChanged += OnViewModelChanged;
        _timer.Tick += OnTimerTick;
        OpenTrack(_viewModel.SelectedTrack ?? _viewModel.Tracks[0]);
        _timer.Start();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelChanged;
            }

            _player.Close();
        }

        base.Dispose(disposing);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is not { } viewModel)
        {
            return;
        }

        if (e.PropertyName == nameof(PlayerViewModel.IsPlaying))
        {
            if (viewModel.IsPlaying)
            {
                _player.Play();
            }
            else
            {
                _player.Pause();
            }
        }
        else if (e.PropertyName == nameof(PlayerViewModel.SelectedTrack)
                 && viewModel.SelectedTrack is { } track
                 && !string.Equals(track, _currentTrack, StringComparison.Ordinal))
        {
            OpenTrack(track);
        }
    }

    private void OpenTrack(string track)
    {
        if (_viewModel is null || !TrackFiles.TryGetValue(track, out var file))
        {
            return;
        }

        try
        {
            _player.Close();
            _player.Open(new Uri(MediaAssets.Resolve(file)));
            _player.Volume = 0.75;
            _currentTrack = track;
            positionText.Text = "0:00";
            durationText.Text = "0:00";
            _updatingFromPlayer = true;
            _viewModel.Progress = 0d;
            _updatingFromPlayer = false;

            if (_viewModel.IsPlaying)
            {
                _player.Play();
            }
        }
        catch (Exception exception)
        {
            positionText.Text = "audio unavailable";
            durationText.Text = exception.GetType().Name;
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_viewModel is null || !_player.NaturalDuration.HasTimeSpan)
        {
            return;
        }

        var total = _player.NaturalDuration.TimeSpan;
        var position = _player.Position;
        if (position > total)
        {
            position = total;
        }

        _updatingFromPlayer = true;
        _viewModel.Progress = total.TotalSeconds <= 0d
            ? 0d
            : position.TotalSeconds / total.TotalSeconds * 100d;
        _updatingFromPlayer = false;

        positionText.Text = Format(position);
        durationText.Text = Format(total);

        if (_viewModel.IsPlaying && position >= total - TimeSpan.FromMilliseconds(250d))
        {
            _viewModel.IsPlaying = false;
            _viewModel.PlayLabel = "Play";
        }
    }

    private void OnSeekChanged(object? sender, EventArgs e)
    {
        if (_updatingFromPlayer || _viewModel is null || !_player.NaturalDuration.HasTimeSpan || seekSlider is null)
        {
            return;
        }

        var total = _player.NaturalDuration.TimeSpan;
        var target = TimeSpan.FromSeconds(total.TotalSeconds * (seekSlider.Value / 100d));
        _player.Position = target;
        positionText.Text = Format(target);
    }

    private void OnPrevClicked(object? sender, EventArgs e) => MoveTrack(-1);

    private void OnNextClicked(object? sender, EventArgs e) => MoveTrack(1);

    private void MoveTrack(int delta)
    {
        if (_viewModel is null || _viewModel.Tracks.Count == 0)
        {
            return;
        }

        var current = _viewModel.Tracks.IndexOf(_viewModel.SelectedTrack ?? string.Empty);
        var next = (current + delta + _viewModel.Tracks.Count) % _viewModel.Tracks.Count;
        _viewModel.SelectedTrack = _viewModel.Tracks[next];
    }

    private static string Format(TimeSpan value)
        => $"{(int)value.TotalMinutes}:{value.Seconds:00}";
}
