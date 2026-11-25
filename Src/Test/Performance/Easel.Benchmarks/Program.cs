using System.Diagnostics;
using System.Drawing;
using Easel;
using Easel.Controls;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

static class Program
{
    private const int W = 1200, H = 800;

    private static void Main()
    {
        Theme.Mode = ThemeMode.Dark;
        var (host, grid, slider) = BuildPage();
        Console.WriteLine($"nodes={CountNodes(grid)}");
        Render(host);
        Measure("slider 200 values", 5, 200, () => { slider.Value = (slider.Value + 7) % 100; Render(host); });
        Measure("splitter 100 widths", 3, 100, () =>
        {
            grid.ColumnDefinitions[0].Width = new GridLength(260 + (grid.ColumnDefinitions[0].Width.Value > 260 ? -20 : 20));
            Render(host);
        });
        Measure("theme toggle 20", 3, 20, () =>
        {
            Theme.Mode = Theme.Mode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
            Render(host);
        });
        Measure("build page 10", 2, 10, () => { var (p, g, s) = BuildPage(); Render(p); GC.KeepAlive(g); GC.KeepAlive(s); p.Dispose(); });
        Measure("theme rebuild 10", 2, 10, () =>
        {
            Theme.Mode = Theme.Mode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
            var (next, nextGrid, nextSlider) = BuildPage();
            Render(next);
            GC.KeepAlive(nextGrid);
            GC.KeepAlive(nextSlider);
            next.Dispose();
        });
    }

    private static (VisualHost Host, VC.Grid Grid, VC.Slider Slider) BuildPage()
    {
        var host = new VisualHost { Size = new Size(W, H), DpiScaleOverride = 1f };
        var grid = new VC.Grid { Width = W, Height = H };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

        var catalog = new VC.ListBox { Width = 240, Height = H - 20 };
        catalog.ItemsSource = Enumerable.Range(0, 200).Select(i => "Catalog item " + i).ToList();
        VC.Grid.SetColumn(catalog, 0);
        grid.Add(catalog);

        var content = new VC.ScrollViewer { Width = 1, Height = H - 20 };
        var stack = new VC.StackPanel { Spacing = 6, Padding = new Easel.Controls.Thickness(12) };
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

    private static void Collect(VisualElement element, List<VC.Slider> sliders)
    {
        if (element is VC.Slider slider) sliders.Add(slider);
        foreach (var child in element.Children)
            if (child is VisualElement visual) Collect(visual, sliders);
    }

    private static int CountNodes(VisualElement? node)
    {
        if (node is null) return 0;
        var count = 1;
        foreach (var child in node.Children)
            if (child is VisualElement element) count += CountNodes(element);
        return count;
    }

    private static void Render(VisualHost host)
    {
        using var bitmap = new Bitmap(host.Size.Width, host.Size.Height);
        using var graphics = Graphics.FromImage(bitmap);
        host.RenderTo(graphics, host.Size);
    }

    private static void Measure(string name, int warmup, int iterations, Action action)
    {
        for (var i = 0; i < warmup; i++) action();
        var samples = new List<double>(iterations);
        var sw = new Stopwatch();
        for (var i = 0; i < iterations; i++)
        {
            sw.Restart();
            action();
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }
        samples.Sort();
        var median = samples[samples.Count / 2];
        var p95 = samples[Math.Min(samples.Count - 1, (int)(samples.Count * 0.95))];
        Console.WriteLine($"{name,-24} median={median:F2} ms p95={p95:F2} ms");
    }
}
