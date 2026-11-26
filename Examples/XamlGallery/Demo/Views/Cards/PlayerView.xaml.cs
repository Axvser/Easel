using System.ComponentModel;
using System.Drawing;
using System.Windows.Media;
using DrawingColor = System.Drawing.Color;
using EaselGradientStop = Easel.GradientStop;
using EaselLinearGradientBrush = Easel.LinearGradientBrush;
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
    private readonly WinFormsTimer _auroraTimer = new() { Interval = 33 };
    private readonly EaselLinearGradientBrush _auroraBrush = new()
    {
        StartPoint = new PointF(0f, 0f),
        EndPoint = new PointF(1f, 1f),
    };

    private PlayerViewModel? _viewModel;
    private double _auroraPhase;
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
        BuildAuroraBrush();
        // 流动光效落在边框上：内部仍是深色封面，不把整块内容换成渐变。
        artwork.BorderBrush = _auroraBrush;
        _auroraTimer.Tick += OnAuroraTick;
        _auroraTimer.Start();
        OpenTrack(_viewModel.SelectedTrack ?? _viewModel.Tracks[0]);
        _timer.Start();
    }

    // 外观是一块普通 Border + 可变 LinearGradientBrush；没有 Transform，动画直接改画刷内部
    // 的 stops 与起止点，每次 tick 让高光带沿卡片移动。
    private void BuildAuroraBrush()
    {
        _auroraBrush.GradientStops.Add(new EaselGradientStop(DrawingColor.FromArgb(255, 5, 16, 38), 0.0));
        _auroraBrush.GradientStops.Add(new EaselGradientStop(DrawingColor.FromArgb(255, 26, 132, 172), 0.24));
        _auroraBrush.GradientStops.Add(new EaselGradientStop(DrawingColor.FromArgb(255, 92, 92, 255), 0.50));
        _auroraBrush.GradientStops.Add(new EaselGradientStop(DrawingColor.FromArgb(255, 42, 76, 190), 0.76));
        _auroraBrush.GradientStops.Add(new EaselGradientStop(DrawingColor.FromArgb(255, 5, 16, 38), 1.0));
    }

    private void OnAuroraTick(object? sender, EventArgs e)
    {
        _auroraPhase += 0.045;
        if (_auroraPhase > Math.PI * 2d)
        {
            _auroraPhase -= Math.PI * 2d;
        }

        var p1 = 0.24 + (0.10 * Math.Sin(_auroraPhase));
        var p2 = 0.50 + (0.08 * Math.Sin(_auroraPhase + 2.0));
        var p3 = 0.76 + (0.10 * Math.Sin(_auroraPhase + 4.0));
        var ordered = new[] { p1, p2, p3 };
        Array.Sort(ordered);
        _auroraBrush.GradientStops[1].Offset = ordered[0];
        _auroraBrush.GradientStops[2].Offset = ordered[1];
        _auroraBrush.GradientStops[3].Offset = ordered[2];

        var sweep = 0.5 + (0.5 * Math.Sin(_auroraPhase * 0.5));
        _auroraBrush.StartPoint = new PointF((float)sweep, 0f);
        _auroraBrush.EndPoint = new PointF((float)(1d - sweep), 1f);

        artwork.InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            _auroraTimer.Stop();
            _auroraTimer.Dispose();
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
