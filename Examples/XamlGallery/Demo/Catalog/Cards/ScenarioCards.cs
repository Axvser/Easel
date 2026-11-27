using Demo.Views.Cards;

namespace Demo;

/// <summary>Full-page demos that combine several controls, bindings and visual effects in one scenario.</summary>
internal static class ScenarioCards
{
    /// <summary>Creates the scenario group.</summary>
    public static IReadOnlyList<GalleryCard> Create()
    {
        return
        [
            Card(
                "scenario.dashboard",
                "Analytics dashboard",
                "A full workbench page: gradient KPI tiles, a hand-drawn chart, progress goals and a live activity feed — all XAML, bound to a view model.",
                "x:Class=\"Demo.Views.Cards.DashboardView\"",
                _ => new DashboardView()),
            Card(
                "scenario.settings",
                "Settings page",
                "A form page with two-way bindings: sections, toggles, sliders, selectors and a command that reports the saved state.",
                "<CheckBox IsChecked=\"{Binding Notifications, Mode=TwoWay}\" />",
                _ => new SettingsView()),
            Card(
                "scenario.player",
                "Music player",
                "A real audio player with three embedded tracks, a gradient-animated album card, a masked strip and transport/seek controls.",
                "<Slider Value=\"{Binding Progress}\" ValueChanged=\"OnSeekChanged\" />",
                _ => new PlayerView()),
            Card(
                "scenario.video",
                "Video player",
                "A real video surface hosted through NativeControlHost: an embedded H.264/AAC clip with transport, seek and time labels.",
                "<NativeControlHost><ElementHost><MediaElement …",
                _ => new VideoPlayerView()),
            Card(
                "scenario.chat",
                "Chat",
                "A conversation page: an item template for message bubbles, a composer bound two-way and a send command.",
                "<ItemsControl ItemsSource=\"{Binding Messages}\">…",
                _ => new ChatView()),
            Card(
                "scenario.gradients",
                "Gradient studio",
                "The full gradient language: inline and resource linear/radial brushes, gradient text, borders, fills and an opacity mask.",
                "<LinearGradientBrush StartPoint=\"0,0\" EndPoint=\"1,1\">…",
                _ => new GradientStudioView(),
                "Visual studies"),
            Card(
                "scenario.composition",
                "Composition lab",
                "A live Visual tree: overlapping ShapeVisuals, group opacity, an opacity mask, a drop shadow and a cached layer.",
                "<VisualPresenter.Content><ContainerVisual>…",
                _ => new CompositionLabView(),
                "Visual studies"),
            Card(
                "scenario.transforms-3d",
                "3D showcase",
                "Rotate3DTransform in perspective, with sliders driving the angles and camera depth of live cards.",
                "<Rotate3DTransform AngleX=\"-16\" AngleY=\"24\" Depth=\"700\" />",
                _ => new Showcase3DView(),
                "Visual studies"),
        ];
    }

    private static GalleryCard Card(
        string id, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create,
        string group = "Applications")
        => new(new GalleryCardInfo(id, group, title, summary, usage), create);
}
