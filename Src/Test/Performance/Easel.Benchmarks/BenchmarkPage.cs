using System.Drawing;
using Easel;
using Easel.Controls;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Benchmarks;

/// <summary>The deterministic page every scenario runs against.</summary>
internal static class BenchmarkPage
{
    public const int Width = 1200;
    public const int Height = 800;

    public static (VisualHost Host, VC.Grid Grid, VC.Slider Slider) Build(CounterProbe? probe = null)
    {
        var host = new VisualHost { Size = new Size(Width, Height), DpiScaleOverride = 1f };
        probe?.Attach(host);

        var grid = new VC.Grid { Width = Width, Height = Height };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

        var catalog = new VC.ListBox { Width = 240, Height = Height - 20 };
        catalog.ItemsSource = Enumerable.Range(0, 200).Select(i => "Catalog item " + i).ToList();
        VC.Grid.SetColumn(catalog, 0);
        grid.Add(catalog);

        var content = new VC.ScrollViewer { Width = 1, Height = Height - 20 };
        var stack = new VC.StackPanel { Spacing = 6, Padding = new Easel.Controls.Thickness(12) };
        // Keep one slider inside the initial viewport: the scenario must paint the control it measures,
        // not only the fixed full-frame blit below the scroll offset.
        stack.Add(new VC.Slider { Height = 26, Minimum = 0, Maximum = 100, Value = 42 });
        for (var i = 0; i < 20; i++) stack.Add(new VC.Button { Text = "Action " + i, Height = 28 });
        for (var i = 0; i < 8; i++) stack.Add(new VC.TextBox { Text = "Field " + i, Height = 28, UseNativeEditor = false });
        for (var i = 0; i < 5; i++) stack.Add(new VC.Slider { Height = 26, Minimum = 0, Maximum = 100, Value = 42 });
        for (var i = 0; i < 5; i++) stack.Add(new VC.ProgressBar { Height = 8, Value = 62 });
        var combo = new VC.ComboBox { Width = 200 };
        combo.ItemsSource = new[] { "Dark+", "Light+", "High contrast", "Follow system" };
        combo.SelectedIndex = 0;
        stack.Add(combo);
        content.Content = stack;
        VC.Grid.SetColumn(content, 1);
        grid.Add(content);

        var side = new VC.StackPanel { Spacing = 8, Padding = new Easel.Controls.Thickness(12) };
        for (var i = 0; i < 30; i++) side.Add(new VC.TextBlock { Text = "Property " + i, Width = 180, Height = 20 });
        VC.Grid.SetColumn(side, 2);
        grid.Add(side);

        host.Root = grid;
        Render(host);

        var sliders = new List<VC.Slider>();
        Collect(stack, sliders);
        return (host, grid, sliders[0]);
    }

    public static void Render(VisualHost host)
    {
        using var bitmap = new Bitmap(host.Size.Width, host.Size.Height);
        using var graphics = Graphics.FromImage(bitmap);
        host.RenderTo(graphics, host.Size);
    }

    public static TreeStats CaptureTree(Visual? root)
    {
        var stats = new TreeStats();
        if (root is null)
        {
            return stats;
        }

        var templatedControls = new HashSet<VisualElement>();
        Walk(root, stats, templatedControls);
        stats.TemplatedControls = templatedControls.Count;
        return stats;
    }

    private static void Walk(Visual node, TreeStats stats, HashSet<VisualElement> templatedControls)
    {
        stats.Nodes++;

        if (node is VisualElement element)
        {
            if (element.TemplatedParent is { } templatedParent)
            {
                stats.TemplateNodes++;
                templatedControls.Add(templatedParent);

                // A template root is the first templated node under a parent that belongs to a different template.
                if (element.Parent is not VisualElement parent
                    || !ReferenceEquals(parent.TemplatedParent, templatedParent))
                {
                    stats.TemplateRoots++;
                }
            }

            if (element.Bounds.Width > 0f && element.Bounds.Height > 0f)
            {
                stats.ArrangedNodes++;
            }

            if (element.IsVisible)
            {
                stats.VisibleNodes++;
            }
        }

        if (node is not ContainerVisual container)
        {
            return;
        }

        foreach (var child in container.Children)
        {
            Walk(child, stats, templatedControls);
        }
    }

    private static void Collect(VisualElement element, List<VC.Slider> sliders)
    {
        if (element is VC.Slider slider) sliders.Add(slider);
        foreach (var child in element.Children)
            if (child is VisualElement visual) Collect(visual, sliders);
    }
}

/// <summary>
/// Tracks layout passes through <see cref="VisualHost.LayoutUpdated"/> and paint passes through
/// <see cref="VisualHost.PaintCount"/>. Passes are accumulated over the measured samples only.
/// </summary>
internal sealed class CounterProbe
{
    private sealed class HostCounter(VisualHost host)
    {
        public VisualHost Host { get; } = host;

        public long Layouts { get; set; }

        public long PaintBase { get; set; }
    }

    private readonly List<HostCounter> _hosts = [];
    private long _layoutTotal;
    private long _paintTotal;
    private long _layoutBase;
    private long _paintBase;

    public void Attach(VisualHost host)
    {
        var counter = new HostCounter(host) { PaintBase = host.PaintCount };
        host.LayoutUpdated += (_, _) =>
        {
            counter.Layouts++;
            _layoutTotal++;
        };
        _hosts.Add(counter);
    }

    /// <summary>Adds the paints performed by <paramref name="host"/> since its last flush.</summary>
    /// <remarks>Call it before disposing a host the scenario created; a disposed page cannot be read afterwards.</remarks>
    public void Flush(VisualHost host)
    {
        foreach (var counter in _hosts)
        {
            if (!ReferenceEquals(counter.Host, host))
            {
                continue;
            }

            _paintTotal += host.PaintCount - counter.PaintBase;
            counter.PaintBase = host.PaintCount;
            return;
        }
    }

    public void BeginRun()
    {
        _layoutBase = _layoutTotal;
        _paintBase = _paintTotal;
        foreach (var counter in _hosts)
        {
            counter.PaintBase = counter.Host.PaintCount;
        }
    }

    public void EndRun(out long layoutPasses, out long paintPasses)
    {
        layoutPasses = _layoutTotal - _layoutBase;
        paintPasses = _paintTotal - _paintBase;
    }
}
