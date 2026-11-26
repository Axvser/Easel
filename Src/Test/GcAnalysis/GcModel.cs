using System.Text.Json.Serialization;

namespace Easel.GcAnalysis;

/// <summary>Results of one GC scenario.</summary>
public sealed class ScenarioReport
{
    /// <summary>The scenario key.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>A one-line description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Number of measured operations.</summary>
    public int Iterations { get; init; }

    /// <summary>Number of additional create/dispose cycles used for leak detection.</summary>
    public int LeakCycles { get; init; }

    /// <summary>Per-operation managed allocation statistics in bytes.</summary>
    public AllocationReport Allocation { get; init; } = new();

    /// <summary>Managed bytes allocated during the whole measured window.</summary>
    public long TotalAllocatedBytes { get; init; }

    /// <summary>Gen0 collections during the measured window.</summary>
    public int Gen0Collections { get; init; }

    /// <summary>Gen1 collections during the measured window.</summary>
    public int Gen1Collections { get; init; }

    /// <summary>Gen2 collections during the measured window.</summary>
    public int Gen2Collections { get; init; }

    /// <summary>GC pause time during the measured window, in milliseconds.</summary>
    public double PauseMilliseconds { get; init; }

    /// <summary>Managed heap growth across the leak cycles after a forced full collection.</summary>
    public long RetainedBytes { get; init; }

    /// <summary>Managed heap size after the final forced collection.</summary>
    public long HeapSizeBytes { get; init; }

    /// <summary>Fragmented bytes reported by the final <see cref="GC.GetGCMemoryInfo"/>.</summary>
    public long FragmentedBytes { get; init; }

    /// <summary>Number of weak references registered by the scenario.</summary>
    public int TrackedObjects { get; init; }

    /// <summary>Weak references that survived a forced full collection.</summary>
    public int AliveWeakReferences { get; init; }

    /// <summary>Labels of surviving weak references (first few).</summary>
    public IReadOnlyList<string> AliveLabels { get; init; } = [];

    /// <summary>An error raised while running the scenario, or <see langword="null"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    /// <summary>Whether this scenario met its leak and regression checks.</summary>
    public bool Success { get; init; }

    /// <summary>Human-readable failure reasons.</summary>
    public IReadOnlyList<string> Failures { get; init; } = [];
}

/// <summary>Allocation statistics for a scenario.</summary>
public sealed class AllocationReport
{
    /// <summary>The smallest per-operation allocation.</summary>
    public long MinBytes { get; init; }

    /// <summary>The median per-operation allocation.</summary>
    public long MedianBytes { get; init; }

    /// <summary>The mean per-operation allocation.</summary>
    public long MeanBytes { get; init; }

    /// <summary>The largest per-operation allocation.</summary>
    public long MaxBytes { get; init; }

    /// <summary>The 95th percentile per-operation allocation.</summary>
    public long P95Bytes { get; init; }
}

/// <summary>The complete analysis document.</summary>
public sealed class GcReport
{
    /// <summary>Schema version for baseline readers.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>UTC time the report was produced.</summary>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>The runtime description.</summary>
    public string Runtime { get; init; } = string.Empty;

    /// <summary>The operating system description.</summary>
    public string OperatingSystem { get; init; } = string.Empty;

    /// <summary>Processor count.</summary>
    public int ProcessorCount { get; init; }

    /// <summary>Whether server GC is enabled.</summary>
    public bool ServerGc { get; init; }

    /// <summary>Whether concurrent GC is enabled.</summary>
    public bool ConcurrentGc { get; init; }

    /// <summary>The requested GC latency mode.</summary>
    public string LatencyMode { get; init; } = string.Empty;

    /// <summary>Measured iterations per scenario.</summary>
    public int Iterations { get; init; }

    /// <summary>Leak-stress cycles per scenario.</summary>
    public int LeakCycles { get; init; }

    /// <summary>Per-scenario results.</summary>
    public IReadOnlyList<ScenarioReport> Scenarios { get; init; } = [];

    /// <summary>Whether every scenario passed.</summary>
    public bool Success { get; init; }
}
