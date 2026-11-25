using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using Timer = System.Windows.Forms.Timer;

namespace Demo.Views.Cards;

/// <summary>A real video page: WPF MediaElement hosted in the single native island, XAML transport controls.</summary>
public sealed partial class VideoPlayerView : ThemedDockPanel
{
    private readonly Timer _timer = new() { Interval = 60 };
    private ElementHost? _elementHost;
    private MediaElement? _video;
    private bool _updatingFromVideo;
    private bool _mediaStarted;

    /// <summary>Creates the page, schedules the embedded clip and starts polling playback state.</summary>
    public VideoPlayerView()
    {
        InitializeComponent();

        _video = new MediaElement
        {
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Manual,
            ScrubbingEnabled = true,
            Stretch = Stretch.Uniform,
            Volume = 0.75,
        };
        _video.MediaOpened += OnMediaOpened;
        _video.MediaEnded += OnMediaEnded;
        _video.MediaFailed += OnMediaFailed;

        // NativeControlHost positions the child explicitly; a Dock style would make it fill the VisualHost.
        _elementHost = new ElementHost { Child = _video };
        videoHost.Child = _elementHost;
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    // 媒体管线启动会把页面首帧拖慢；第一次 Tick 在页面画出后打开媒体，宿主先就位。
    private void StartMedia()
    {
        _mediaStarted = true;
        _timer.Interval = 200;
        if (_video is not { } video)
        {
            return;
        }

        video.Source = new Uri(MediaAssets.Resolve("aurora-drift.mp4"));
        // With LoadedBehavior.Manual the clip does not open until the first Play; start it here.
        video.Play();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Stop();
            _timer.Dispose();
            if (_video is not null)
            {
                _video.MediaOpened -= OnMediaOpened;
                _video.MediaEnded -= OnMediaEnded;
                _video.MediaFailed -= OnMediaFailed;
                _video.Stop();
                _video.Close();
                _video = null;
            }

            if (_elementHost is not null)
            {
                videoHost.Child = null;
                _elementHost.Dispose();
                _elementHost = null;
            }
        }

        base.Dispose(disposing);
    }

    private void OnMediaOpened(object? sender, RoutedEventArgs e)
    {
        // Start muted-free on open so the page shows the clip immediately; the button still toggles playback.
        if (_video is { } video)
        {
            durationText.Text = Format(video.NaturalDuration.HasTimeSpan
                ? video.NaturalDuration.TimeSpan
                : TimeSpan.Zero);
            video.Play();
            playButton.Text = "Pause";
        }
    }

    private void OnMediaEnded(object? sender, RoutedEventArgs e)
    {
        playButton.Text = "Play";
        if (_video is { } video)
        {
            video.Position = video.NaturalDuration.HasTimeSpan ? video.NaturalDuration.TimeSpan : TimeSpan.Zero;
        }
    }

    private void OnMediaFailed(object? sender, ExceptionRoutedEventArgs e)
    {
        playButton.Text = "Unavailable";
        positionText.Text = e.ErrorException?.GetType().Name ?? "media error";
    }

    private void OnPlayClicked(object? sender, EventArgs e)
    {
        if (_video is not { } video)
        {
            return;
        }

        if (video.CanPause && playButton.Text == "Pause")
        {
            video.Pause();
            playButton.Text = "Play";
            return;
        }

        if (video.NaturalDuration.HasTimeSpan && video.Position >= video.NaturalDuration.TimeSpan)
        {
            video.Position = TimeSpan.Zero;
        }

        video.Play();
        playButton.Text = "Pause";
    }

    private void OnSeekChanged(object? sender, EventArgs e)
    {
        if (_updatingFromVideo || _video is not { NaturalDuration.HasTimeSpan: true } video || seekSlider is null)
        {
            return;
        }

        var target = TimeSpan.FromSeconds(video.NaturalDuration.TimeSpan.TotalSeconds * (seekSlider.Value / 100d));
        video.Position = target;
        positionText.Text = Format(target);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_mediaStarted)
        {
            StartMedia();
            return;
        }

        if (_video is not { NaturalDuration.HasTimeSpan: true } video)
        {
            return;
        }

        var total = video.NaturalDuration.TimeSpan;
        var position = video.Position > total ? total : video.Position;
        _updatingFromVideo = true;
        seekSlider.Value = total.TotalSeconds <= 0d ? 0d : position.TotalSeconds / total.TotalSeconds * 100d;
        _updatingFromVideo = false;
        positionText.Text = Format(position);
        durationText.Text = Format(total);

        if (playButton.Text == "Pause" && position >= total - TimeSpan.FromMilliseconds(250d))
        {
            playButton.Text = "Play";
        }
    }

    private static string Format(TimeSpan value)
        => $"{(int)value.TotalMinutes}:{value.Seconds:00}";
}
