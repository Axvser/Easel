using System.Text.Json;
using System.Text.Json.Serialization;

namespace Easel.Benchmarks;

/// <summary>The machine-readable result written to disk and read back for baseline comparison.</summary>
internal sealed class BenchmarkReport
{
    public int SchemaVersion { get; set; } = 1;

    public string GeneratedAtUtc { get; set; } = string.Empty;

    public RunInfo Run { get; set; } = new();

    public TreeStats Tree { get; set; } = new();

    public List<ScenarioResult> Scenarios { get; set; } = [];

    public ComparisonSummary? Comparison { get; set; }
}

internal sealed class RunInfo
{
    public string Configuration { get; set; } = string.Empty;

    public string Framework { get; set; } = string.Empty;

    public string Os { get; set; } = string.Empty;

    public string Architecture { get; set; } = string.Empty;

    public int ProcessorCount { get; set; }

    public string MachineName { get; set; } = string.Empty;

    public long StopwatchFrequency { get; set; }

    public string EaselVersion { get; set; } = string.Empty;

    public string? EaselInformationalVersion { get; set; }

    public string[] Arguments { get; set; } = [];
}

/// <summary>Shape of the visual tree after the canonical page is built and laid out.</summary>
internal sealed class TreeStats
{
    public int Nodes { get; set; }

    /// <summary>Nodes created by a control template (<see cref="Easel.Visuals.VisualElement.TemplatedParent"/> is set).</summary>
    public int TemplateNodes { get; set; }

    /// <summary>Top-most node of each instantiated control template.</summary>
    public int TemplateRoots { get; set; }

    /// <summary>Distinct controls that owned an instantiated template.</summary>
    public int TemplatedControls { get; set; }

    /// <summary>Nodes with a non-empty arranged bounds.</summary>
    public int ArrangedNodes { get; set; }

    public int VisibleNodes { get; set; }
}

internal sealed class ScenarioResult
{
    public string Name { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Unit { get; set; } = "ms";

    public int Warmup { get; set; }

    public int Iterations { get; set; }

    public double MedianMs { get; set; }

    public double P95Ms { get; set; }

    public double MinMs { get; set; }

    public double MaxMs { get; set; }

    public double MeanMs { get; set; }

    public double StdDevMs { get; set; }

    public double[] SamplesMs { get; set; } = [];

    public ScenarioCounters Counters { get; set; } = new();
}

/// <summary>Work counters accumulated over the measured (post-warmup) samples.</summary>
internal sealed class ScenarioCounters
{
    public long LayoutPasses { get; set; }

    public long PaintPasses { get; set; }

    /// <summary>Visual trees built by the scenario; zero for scenarios that reuse the canonical page.</summary>
    public int TreesBuilt { get; set; }

    public int Nodes { get; set; }

    public int TemplateNodes { get; set; }
}

internal sealed class ComparisonSummary
{
    public string BaselinePath { get; set; } = string.Empty;

    public string Metric { get; set; } = "median";

    public bool HadBudget { get; set; }

    /// <summary><c>pass</c>, <c>regression</c>, or <c>not-gated</c>.</summary>
    public string Verdict { get; set; } = "not-gated";

    public List<ComparisonEntry> Scenarios { get; set; } = [];
}

internal sealed class ComparisonEntry
{
    public string Name { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public double? CurrentMs { get; set; }

    public double? BaselineMs { get; set; }

    public double? DeltaPercent { get; set; }

    public double? BudgetPercent { get; set; }

    /// <summary><c>pass</c>, <c>regression</c>, <c>not-gated</c>, or <c>missing-baseline</c>.</summary>
    public string Verdict { get; set; } = "not-gated";
}

internal static class JsonOutput
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

internal readonly record struct BenchStatistics(
    double Median,
    double P95,
    double Min,
    double Max,
    double Mean,
    double StdDev)
{
    public static BenchStatistics Compute(double[] samples)
    {
        if (samples.Length == 0)
        {
            return new BenchStatistics(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        }

        var sorted = (double[])samples.Clone();
        Array.Sort(sorted);

        var sum = 0d;
        foreach (var sample in samples)
        {
            sum += sample;
        }

        var mean = sum / samples.Length;
        var variance = 0d;
        if (samples.Length > 1)
        {
            foreach (var sample in samples)
            {
                var difference = sample - mean;
                variance += difference * difference;
            }

            variance /= samples.Length - 1;
        }

        return new BenchStatistics(
            Percentile(sorted, 0.50),
            Percentile(sorted, 0.95),
            sorted[0],
            sorted[^1],
            mean,
            Math.Sqrt(variance));
    }

    // Linear interpolation between the two closest ranks, matching common benchmark tooling.
    private static double Percentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 1)
        {
            return sorted[0];
        }

        var rank = (sorted.Length - 1) * percentile;
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        return sorted[lower] + ((sorted[upper] - sorted[lower]) * (rank - lower));
    }
}
