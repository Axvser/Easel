using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using Easel;
using Easel.Visuals;
using Easel.Visuals.Controls;

namespace Easel.GcAnalysis;

/// <summary>One reproducible allocation/leak scenario.</summary>
internal interface IGcScenario
{
    string Name { get; }

    string Description { get; }

    void Run(LeakTracker tracker);
}

/// <summary>The built-in scenario catalog.</summary>
internal static class GcScenarios
{
    private static readonly Dictionary<string, Func<IGcScenario>> Factories = new(StringComparer.Ordinal)
    {
        ["host-lifecycle"] = static () => new HostLifecycleScenario(),
        ["page-build"] = static () => new PageBuildScenario(),
        ["template-lifecycle"] = static () => new TemplateLifecycleScenario(),
        ["binding-lifecycle"] = static () => new BindingLifecycleScenario(),
        ["theme-switch"] = static () => new ThemeSwitchScenario(),
        ["list-window"] = static () => new ListWindowScenario(),
    };

    internal static IReadOnlyList<string> Names { get; } = Factories.Keys.ToArray();

    internal static IEnumerable<IGcScenario> Create(string selection)
    {
        var names = string.Equals(selection, "all", StringComparison.OrdinalIgnoreCase)
            ? Names
            : selection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var name in names)
        {
            if (!Factories.TryGetValue(name, out var factory))
            {
                throw new ArgumentException($"unknown scenario '{name}'. Known: {string.Join(", ", Names)}");
            }

            yield return factory();
        }
    }

    internal static void Render(VisualHost host, int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        host.RenderTo(graphics, new Size(width, height));
    }

    internal static VisualHost CreateHost(int width, int height)
        => new() { Size = new Size(width, height), DpiScaleOverride = 1f };
}

internal sealed class HostLifecycleScenario : IGcScenario
{
    public string Name => "host-lifecycle";

    public string Description => "Create a small visual tree, render it, and dispose the host.";

    public void Run(LeakTracker tracker)
    {
        var host = GcScenarios.CreateHost(800, 600);
        var root = new StackPanel { Padding = new Easel.Controls.Thickness(12) };
        var button = new Button { Text = "Action", Width = 120, Height = 32 };
        var text = new TextBlock { Text = "GC analysis", Width = 200, Height = 20 };
        var box = new TextBox { Text = "editable", Width = 200, Height = 30 };
        var list = new ListBox
        {
            Width = 240,
            Height = 260,
            ItemsSource = Enumerable.Range(0, 20).Select(static index => "Row " + index).ToArray(),
        };
        root.Add(button);
        root.Add(text);
        root.Add(box);
        root.Add(list);
        host.Root = root;
        GcScenarios.Render(host, 800, 600);

        tracker.Track("host", host);
        tracker.Track("root", root);
        tracker.Track("button", button);
        tracker.Track("button-template", button.TemplateRoot);
        tracker.Track("text", text);
        tracker.Track("text-box", box);
        tracker.Track("list", list);
        if (list.Items.Count > 0)
        {
            tracker.Track("list-item", list.Items[0]);
        }

        host.Dispose();
    }
}

internal sealed class PageBuildScenario : IGcScenario
{
    public string Name => "page-build";

    public string Description => "Build and dispose a 200-row ListBox page with controls, sliders and a ComboBox.";

    public void Run(LeakTracker tracker)
    {
        var host = GcScenarios.CreateHost(1000, 700);
        var root = new StackPanel { Padding = new Easel.Controls.Thickness(14), Spacing = 6 };
        var list = new ListBox
        {
            Width = 320,
            Height = 520,
            ItemsSource = Enumerable.Range(0, 200).Select(static index => "Item " + index).ToArray(),
        };
        root.Add(list);
        for (var index = 0; index < 10; index++)
        {
            root.Add(new Button { Text = "Button " + index, Width = 120, Height = 28 });
        }

        for (var index = 0; index < 4; index++)
        {
            root.Add(new TextBox { Text = "Field " + index, Width = 200, Height = 28 });
        }

        for (var index = 0; index < 4; index++)
        {
            root.Add(new Slider { Width = 220, Height = 26, Minimum = 0, Maximum = 100, Value = 42 });
        }

        root.Add(new ComboBox
        {
            Width = 180,
            ItemsSource = new[] { "One", "Two", "Three" },
        });

        host.Root = root;
        GcScenarios.Render(host, 1000, 700);

        tracker.Track("host", host);
        tracker.Track("root", root);
        tracker.Track("list", list);
        if (list.Items.Count > 0)
        {
            tracker.Track("first-row", list.Items[0]);
        }

        if (root.Children.Count > 0 && root.Children[0] is VisualElement first)
        {
            tracker.Track("first-control", first);
        }

        if (root.Children.Count > 1 && root.Children[1] is Button firstButton)
        {
            tracker.Track("first-button", firstButton);
            tracker.Track("first-button-template", firstButton.TemplateRoot);
        }

        host.Dispose();
    }
}

internal sealed class TemplateLifecycleScenario : IGcScenario
{
    public string Name => "template-lifecycle";

    public string Description => "Create, template, render and dispose a mixed control batch.";

    public void Run(LeakTracker tracker)
    {
        var host = GcScenarios.CreateHost(900, 800);
        var root = new StackPanel { Padding = new Easel.Controls.Thickness(12), Spacing = 6 };
        var controls = new List<VisualElement>();
        var firstButton = new Button { Text = "Template", Width = 120, Height = 30 };
        controls.Add(firstButton);
        root.Add(firstButton);
        for (var index = 0; index < 12; index++)
        {
            VisualElement control = (index % 4) switch
            {
                0 => new TextBox { Text = "Text " + index, Width = 180, Height = 28 },
                1 => new ComboBox { Width = 160, ItemsSource = new[] { "A", "B", "C" } },
                2 => new CheckBox { IsChecked = index % 2 == 0 },
                _ => new Slider { Width = 180, Height = 26, Value = 30 },
            };
            controls.Add(control);
            root.Add(control);
        }

        host.Root = root;
        GcScenarios.Render(host, 900, 800);

        tracker.Track("host", host);
        tracker.Track("root", root);
        tracker.Track("button", firstButton);
        tracker.Track("button-template", firstButton.TemplateRoot);
        for (var index = 0; index < controls.Count; index++)
        {
            tracker.Track("control-" + index, controls[index]);
        }

        host.Dispose();
    }
}

internal sealed class BindingLifecycleScenario : IGcScenario
{
    public string Name => "binding-lifecycle";

    public string Description => "Bind a TextBlock to an INotifyPropertyChanged source and dispose the host.";

    public void Run(LeakTracker tracker)
    {
        var source = new ProbeSource { Value = "bound value" };
        var host = GcScenarios.CreateHost(400, 120);
        var root = new StackPanel { Padding = new Easel.Controls.Thickness(10) };
        var text = new TextBlock { Width = 200, Height = 24 };
        text.SetBinding(TextBlock.TextProperty, new Binding("Value") { Source = source });
        root.Add(text);
        host.Root = root;
        GcScenarios.Render(host, 400, 120);

        tracker.Track("host", host);
        tracker.Track("root", root);
        tracker.Track("text", text);
        tracker.Track("source", source);

        host.Dispose();
    }

    private sealed class ProbeSource : INotifyPropertyChanged
    {
        private string _value = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Value
        {
            get => _value;
            set
            {
                if (string.Equals(_value, value, StringComparison.Ordinal))
                {
                    return;
                }

                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }
}

internal sealed class ThemeSwitchScenario : IGcScenario
{
    public string Name => "theme-switch";

    public string Description => "Switch the palette across a rendered page and dispose it.";

    public void Run(LeakTracker tracker)
    {
        var original = Theme.Mode;
        var host = GcScenarios.CreateHost(700, 500);
        var root = new StackPanel { Padding = new Easel.Controls.Thickness(12), Spacing = 8 };
        root.Add(new Button { Text = "Themed", Width = 120, Height = 30 });
        root.Add(new TextBox { Text = "Theme", Width = 180, Height = 28 });
        root.Add(new ListBox
        {
            Width = 220,
            Height = 220,
            ItemsSource = Enumerable.Range(0, 20).Select(static index => "Row " + index).ToArray(),
        });
        host.Root = root;

        try
        {
            var modes = new[] { ThemeMode.Dark, ThemeMode.Light, ThemeMode.HighContrast, ThemeMode.Dark };
            for (var index = 0; index < modes.Length; index++)
            {
                Theme.Mode = modes[index];
                GcScenarios.Render(host, 700, 500);
            }
        }
        finally
        {
            Theme.Mode = original;
        }

        tracker.Track("host", host);
        tracker.Track("root", root);
        host.Dispose();
    }
}

internal sealed class ListWindowScenario : IGcScenario
{
    public string Name => "list-window";

    public string Description => "Create a 500-row ListBox and dispose it after rendering the visible window.";

    public void Run(LeakTracker tracker)
    {
        var host = GcScenarios.CreateHost(520, 640);
        var list = new ListBox
        {
            Width = 320,
            Height = 560,
            ItemsSource = Enumerable.Range(0, 500).Select(static index => "Row " + index).ToArray(),
        };
        host.Root = list;
        GcScenarios.Render(host, 520, 640);

        tracker.Track("host", host);
        tracker.Track("list", list);
        if (list.Items.Count > 0)
        {
            tracker.Track("first-row", list.Items[0]);
        }

        if (list.Items.Count > 1)
        {
            tracker.Track("last-row", list.Items[^1]);
        }

        host.Dispose();
    }
}
