using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Easel.Controls;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Benchmarks;

internal static class Program
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private static readonly ScenarioInfo[] KnownScenarios =
    [
        new("slider-value", "slider · value change", 5, 200,
            "Moves one slider value and repaints the damaged region."),
        new("splitter-resize", "splitter · column resize", 3, 100,
            "Changes the first grid column width; forces measure/arrange and a full-frame repaint."),
        new("full-frame", "full frame · layout + repaint", 3, 20,
            "Invalidates the surface, then re-measures, re-arranges and repaints the whole tree."),
        new("theme-toggle", "palette toggle · shared brushes", 3, 20,
            "Switches dark/light with the page kept alive."),
        new("build-page", "build page · new tree", 2, 10,
            "Builds, lays out and renders a fresh page."),
        new("theme-rebuild", "palette toggle · full rebuild", 2, 10,
            "Switches palette, then builds, lays out and renders a fresh page."),
    ];

    private static int Main(string[] args)
    {
        BenchOptions options;
        try
        {
            options = BenchOptions.Parse(args);
        }
        catch (OptionException exception)
        {
            Console.Error.WriteLine("error: " + exception.Message);
            return 2;
        }

        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        if (options.ListScenarios)
        {
            PrintScenarios();
            return 0;
        }

        try
        {
            return Run(options);
        }
        catch (BenchmarkException exception)
        {
            Console.Error.WriteLine("error: " + exception.Message);
            return 2;
        }
    }

    private static int Run(BenchOptions options)
    {
        ValidateSelection(options);

#if DEBUG
        Console.Error.WriteLine("warning · Debug build · timings are not comparable; rerun with -c Release.");
#endif

        Theme.Mode = ThemeMode.Dark;
        var probe = new CounterProbe();
        var (host, grid, slider) = BenchmarkPage.Build(probe);
        var canonicalTree = BenchmarkPage.CaptureTree(host.Root);

        var definitions = CreateScenarios(options, probe, host, grid, slider, canonicalTree);
        if (definitions.Count == 0)
        {
            throw new BenchmarkException("No scenario matched --scenario.");
        }

        var results = new List<ScenarioResult>(definitions.Count);
        foreach (var definition in definitions)
        {
            definition.Setup?.Invoke();
            results.Add(RunScenario(definition, options, probe));
        }

        var report = new BenchmarkReport
        {
            GeneratedAtUtc = DateTime.UtcNow.ToString("O", Invariant),
            Run = CreateRunInfo(options),
            Tree = canonicalTree,
            Scenarios = results,
        };

        if (options.BaselinePath is { } baselinePath)
        {
            report.Comparison = Compare(report, LoadBaseline(baselinePath), options);
        }

        if (!options.NoJson)
        {
            WriteJson(report, options.OutputPath);
        }

        PrintSummary(report, options);

        return report.Comparison?.Verdict == "regression" ? 1 : 0;
    }

    private static void ValidateSelection(BenchOptions options)
    {
        var known = new HashSet<string>(KnownScenarios.Select(scenario => scenario.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var name in options.ScenarioFilter)
        {
            if (!known.Contains(name))
            {
                throw new BenchmarkException($"Unknown scenario '{name}'. Run --list to see the names.");
            }
        }

        foreach (var name in options.Budgets.Keys)
        {
            if (!known.Contains(name))
            {
                throw new BenchmarkException($"--budget names unknown scenario '{name}'.");
            }
        }
    }

    private static List<ScenarioDefinition> CreateScenarios(
        BenchOptions options,
        CounterProbe probe,
        VisualHost host,
        VC.Grid grid,
        VC.Slider slider,
        TreeStats canonicalTree)
    {
        var definitions = new List<ScenarioDefinition>
        {
            new(
                Known("slider-value"),
                null,
                () =>
                {
                    slider.Value = (slider.Value + 7) % 100;
                    BenchmarkPage.Render(host);
                },
                host,
                () => canonicalTree,
                BuildsPerSample: 0),
            new(
                Known("splitter-resize"),
                null,
                () =>
                {
                    var first = grid.ColumnDefinitions[0];
                    first.Width = new GridLength(first.Width.Value > 260 ? 240 : 280);
                    // ColumnDefinition is a plain value holder; a real splitter invalidates layout after the write.
                    grid.InvalidateMeasure();
                    BenchmarkPage.Render(host);
                },
                host,
                () => canonicalTree,
                BuildsPerSample: 0),
            new(
                Known("full-frame"),
                null,
                () =>
                {
                    host.InvalidateSurface();
                    BenchmarkPage.Render(host);
                },
                host,
                () => canonicalTree,
                BuildsPerSample: 0),
            new(
                Known("theme-toggle"),
                static () => Theme.Mode = ThemeMode.Dark,
                () =>
                {
                    Theme.Mode = Theme.Mode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
                    BenchmarkPage.Render(host);
                },
                host,
                () => canonicalTree,
                BuildsPerSample: 0),
            new(
                Known("build-page"),
                static () => Theme.Mode = ThemeMode.Dark,
                () =>
                {
                    var page = BenchmarkPage.Build(probe);
                    probe.Flush(page.Host);
                    page.Host.Dispose();
                },
                Host: null,
                BuildAndCaptureTree,
                BuildsPerSample: 1),
            new(
                Known("theme-rebuild"),
                static () => Theme.Mode = ThemeMode.Dark,
                () =>
                {
                    Theme.Mode = Theme.Mode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
                    var page = BenchmarkPage.Build(probe);
                    probe.Flush(page.Host);
                    page.Host.Dispose();
                },
                Host: null,
                BuildAndCaptureTree,
                BuildsPerSample: 1),
        };

        return definitions
            .Where(definition => options.ScenarioFilter.Count == 0
                                 || options.ScenarioFilter.Contains(definition.Info.Name))
            .ToList();
    }

    private static ScenarioInfo Known(string name)
        => KnownScenarios.First(scenario => string.Equals(scenario.Name, name, StringComparison.OrdinalIgnoreCase));

    private static TreeStats BuildAndCaptureTree()
    {
        var page = BenchmarkPage.Build();
        var tree = BenchmarkPage.CaptureTree(page.Host.Root);
        page.Host.Dispose();
        return tree;
    }

    private static ScenarioResult RunScenario(ScenarioDefinition definition, BenchOptions options, CounterProbe probe)
    {
        var warmup = options.Warmup ?? definition.Info.Warmup;
        var iterations = options.Iterations ?? definition.Info.Iterations;

        for (var iteration = 0; iteration < warmup; iteration++)
        {
            definition.Action();
        }

        // Warmup garbage is released before samples start so a collection does not land inside a later sample.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        probe.BeginRun();
        var samples = new double[iterations];
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var start = Stopwatch.GetTimestamp();
            definition.Action();
            samples[iteration] = (Stopwatch.GetTimestamp() - start) * (1000.0 / Stopwatch.Frequency);
        }

        if (definition.Host is { } host)
        {
            probe.Flush(host);
        }

        probe.EndRun(out var layoutPasses, out var paintPasses);

        var statistics = BenchStatistics.Compute(samples);
        var tree = definition.Tree();
        return new ScenarioResult
        {
            Name = definition.Info.Name,
            Title = definition.Info.Title,
            Warmup = warmup,
            Iterations = iterations,
            MedianMs = Round(statistics.Median),
            P95Ms = Round(statistics.P95),
            MinMs = Round(statistics.Min),
            MaxMs = Round(statistics.Max),
            MeanMs = Round(statistics.Mean),
            StdDevMs = Round(statistics.StdDev),
            SamplesMs = samples.Select(Round).ToArray(),
            Counters = new ScenarioCounters
            {
                LayoutPasses = layoutPasses,
                PaintPasses = paintPasses,
                TreesBuilt = definition.BuildsPerSample * iterations,
                Nodes = tree.Nodes,
                TemplateNodes = tree.TemplateNodes,
            },
        };
    }

    private static double Round(double value) => Math.Round(value, 4);

    private static RunInfo CreateRunInfo(BenchOptions options)
    {
        var assembly = typeof(VisualHost).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return new RunInfo
        {
            Configuration =
#if DEBUG
                "Debug",
#else
                "Release",
#endif
            Framework = RuntimeInformation.FrameworkDescription,
            Os = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            MachineName = Environment.MachineName,
            StopwatchFrequency = Stopwatch.Frequency,
            EaselVersion = assembly.GetName().Version?.ToString() ?? "unknown",
            EaselInformationalVersion = informational,
            Arguments = options.Arguments,
        };
    }

    private static BenchmarkReport LoadBaseline(string path)
    {
        if (!File.Exists(path))
        {
            throw new BenchmarkException($"Baseline '{path}' does not exist.");
        }

        try
        {
            var baseline = JsonSerializer.Deserialize<BenchmarkReport>(File.ReadAllText(path), JsonOutput.Options);
            if (baseline is null)
            {
                throw new BenchmarkException($"Baseline '{path}' is empty.");
            }

            if (baseline.SchemaVersion != 1)
            {
                throw new BenchmarkException(
                    $"Baseline '{path}' has schema {baseline.SchemaVersion}; this runner expects 1.");
            }

            return baseline;
        }
        catch (JsonException exception)
        {
            throw new BenchmarkException($"Baseline '{path}' is not a benchmark JSON: {exception.Message}");
        }
    }

    private static ComparisonSummary Compare(BenchmarkReport current, BenchmarkReport baseline, BenchOptions options)
    {
        var summary = new ComparisonSummary
        {
            BaselinePath = options.BaselinePath ?? string.Empty,
            Metric = options.BudgetMetric,
            HadBudget = options.DefaultBudget is not null || options.Budgets.Count > 0,
        };

        foreach (var scenario in current.Scenarios)
        {
            var entry = new ComparisonEntry { Name = scenario.Name, Title = scenario.Title };
            var currentValue = MetricValue(scenario, options.BudgetMetric);
            entry.CurrentMs = currentValue;

            var baselineScenario = baseline.Scenarios.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, scenario.Name, StringComparison.OrdinalIgnoreCase));
            if (baselineScenario is null)
            {
                entry.Verdict = "missing-baseline";
                summary.Scenarios.Add(entry);
                continue;
            }

            var baselineValue = MetricValue(baselineScenario, options.BudgetMetric);
            entry.BaselineMs = baselineValue;
            if (baselineValue > 0)
            {
                entry.DeltaPercent = Math.Round(((currentValue / baselineValue) - 1) * 100, 4);
            }

            var budget = options.Budgets.TryGetValue(scenario.Name, out var namedBudget)
                ? namedBudget
                : options.DefaultBudget;
            if (budget is { } limit)
            {
                entry.BudgetPercent = limit * 100;
                entry.Verdict = entry.DeltaPercent is { } delta && delta > entry.BudgetPercent
                    ? "regression"
                    : "pass";
            }
            else
            {
                entry.Verdict = "not-gated";
            }

            summary.Scenarios.Add(entry);
        }

        summary.Verdict = !summary.HadBudget
            ? "not-gated"
            : summary.Scenarios.Any(entry => entry.Verdict == "regression")
                ? "regression"
                : "pass";
        return summary;
    }

    private static double MetricValue(ScenarioResult scenario, string metric)
        => metric == "p95" ? scenario.P95Ms : scenario.MedianMs;

    private static void WriteJson(BenchmarkReport report, string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOutput.Options));
        Console.WriteLine($"json · {Path.GetFullPath(path)}");
    }

    private static void PrintSummary(BenchmarkReport report, BenchOptions options)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"Easel benchmark · {report.Run.Framework} · {report.Run.Configuration} · "
            + $"{BenchmarkPage.Width}x{BenchmarkPage.Height} @ 1x");
        Console.WriteLine(
            $"tree · nodes {report.Tree.Nodes} · template {report.Tree.TemplateNodes} · "
            + $"roots {report.Tree.TemplateRoots} · controls {report.Tree.TemplatedControls} · "
            + $"arranged {report.Tree.ArrangedNodes}");
        Console.WriteLine();

        Console.WriteLine($"{"scenario",-26} {"n",5} {"median",9} {"p95",9} {"layout",8} {"paint",8} {"trees",6}");
        foreach (var scenario in report.Scenarios)
        {
            Console.WriteLine(
                $"{scenario.Name,-26} {scenario.Iterations,5} "
                + $"{scenario.MedianMs,9:0.000} {scenario.P95Ms,9:0.000} "
                + $"{scenario.Counters.LayoutPasses,8} {scenario.Counters.PaintPasses,8} "
                + $"{scenario.Counters.TreesBuilt,6}");
        }

        Console.WriteLine($"(times in ms; layout/paint/trees are totals over the measured samples)");

        if (report.Comparison is { } comparison)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"baseline · {Path.GetFileName(comparison.BaselinePath)} · metric {comparison.Metric}"
                + (comparison.HadBudget ? " · budget over threshold fails the run" : " · informational (no budget)"));
            Console.WriteLine($"{"scenario",-26} {"current",9} {"baseline",9} {"delta",9} {"budget",8} result");
            foreach (var entry in comparison.Scenarios)
            {
                var current = entry.CurrentMs is { } currentValue ? currentValue.ToString("0.000", Invariant) : "n/a";
                var baseline = entry.BaselineMs is { } baselineValue ? baselineValue.ToString("0.000", Invariant) : "n/a";
                var delta = entry.DeltaPercent is { } deltaValue ? FormatSigned(deltaValue) + "%" : "n/a";
                var budget = entry.BudgetPercent is { } budgetValue ? "+" + budgetValue.ToString("0.0", Invariant) + "%" : "n/a";
                Console.WriteLine(
                    $"{entry.Name,-26} {current,9} {baseline,9} {delta,9} {budget,8} {VerdictLabel(entry.Verdict)}");
            }

            var gated = comparison.Scenarios.Count(entry => entry.Verdict is "pass" or "regression");
            if (comparison.Verdict == "regression")
            {
                var over = comparison.Scenarios.Count(entry => entry.Verdict == "regression");
                Console.WriteLine($"overall · REGRESSION · {over} of {gated} gated scenarios over budget");
            }
            else if (comparison.Verdict == "pass")
            {
                Console.WriteLine($"overall · pass · {gated} gated scenarios within budget");
            }
            else
            {
                Console.WriteLine("overall · not gated · pass --budget 10% (or name=ratio) to enforce thresholds");
            }
        }

        Console.WriteLine();
    }

    private static string FormatSigned(double value)
        => value.ToString("+0.0;-0.0;0.0", Invariant);

    private static string VerdictLabel(string verdict) => verdict switch
    {
        "pass" => "PASS",
        "regression" => "FAIL",
        "missing-baseline" => "n/a",
        _ => "n/a",
    };

    private static void PrintScenarios()
    {
        foreach (var scenario in KnownScenarios)
        {
            Console.WriteLine(
                $"{scenario.Name,-16} warmup {scenario.Warmup,3}  iterations {scenario.Iterations,4}  {scenario.Title}");
            Console.WriteLine($"{"",16} {scenario.Description}");
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            Easel benchmarks

            Usage:
              dotnet run -c Release --project Src/Test/Performance/Easel.Benchmarks -- [options]

            Options:
              -o, --output <path>     JSON result path (default benchmark-results.json)
                  --no-json           do not write a JSON file
                  --baseline <path>   compare against a baseline JSON produced by this runner
                  --scenario <list>   only run these scenario names (comma separated)
                  --warmup <n>        override warmup iterations for every scenario
                  --iterations <n>    override measured iterations for every scenario
                  --budget <spec>     regression budget: 10%, 0.10, or name=25%; repeatable
                  --budget-metric <m> median (default) or p95
                  --list              list scenarios and their default iteration counts
              -h, --help              show this help

            Baseline comparison runs against a different Easel source by rebuilding this
            runner with the Easel project redirected:

              dotnet run -c Release --project Src/Test/Performance/Easel.Benchmarks -p:EaselProject=C:\path\to\easel-baseline\Src\Easel\Easel.csproj -- --output baseline.json

              dotnet run -c Release --project Src/Test/Performance/Easel.Benchmarks -- --baseline baseline.json --budget 10%
            """);
    }

    private sealed record ScenarioInfo(string Name, string Title, int Warmup, int Iterations, string Description);

    private sealed record ScenarioDefinition(
        ScenarioInfo Info,
        Action? Setup,
        Action Action,
        VisualHost? Host,
        Func<TreeStats> Tree,
        int BuildsPerSample);
}

internal sealed class BenchmarkException(string message) : Exception(message);
