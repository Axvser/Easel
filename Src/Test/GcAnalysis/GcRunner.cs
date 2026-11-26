using System.Globalization;
using System.Runtime;
using System.Text.Json;

namespace Easel.GcAnalysis;

/// <summary>Runs scenarios and builds allocation, collection and leak reports.</summary>
internal sealed class GcRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly GcOptions _options;
    private readonly GcReport? _baseline;

    public GcRunner(GcOptions options)
    {
        _options = options;
        _baseline = LoadBaseline(options.Baseline);
    }

    /// <summary>Runs the selected scenarios.</summary>
    public GcReport Run()
    {
        var scenarios = new List<ScenarioReport>();
        foreach (var scenario in GcScenarios.Create(_options.Scenario))
        {
            scenarios.Add(RunScenario(scenario));
        }

        return new GcReport
        {
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ProcessorCount = Environment.ProcessorCount,
            ServerGc = GCSettings.IsServerGC,
            ConcurrentGc = GC.GetGCMemoryInfo().Concurrent,
            LatencyMode = GCSettings.LatencyMode.ToString(),
            Iterations = _options.Iterations,
            LeakCycles = _options.LeakCycles,
            Scenarios = scenarios,
            Success = scenarios.All(static scenario => scenario.Success),
        };
    }

    /// <summary>The JSON serializer used for report and baseline files.</summary>
    public static JsonSerializerOptions SerializerOptions => JsonOptions;

    private ScenarioReport RunScenario(IGcScenario scenario)
    {
        var failures = new List<string>();
        var allocations = new List<long>(_options.Iterations);
        try
        {
            // Warm the templates, JIT and static caches; the first operation is not a steady-state measurement.
            for (var index = 0; index < 2; index++)
            {
                scenario.Run(new LeakTracker(enabled: false));
            }

            LeakTracker.ForceFullCollection();
            var baselineAllocated = GC.GetTotalAllocatedBytes(precise: true);
            var baselineHeap = GC.GetTotalMemory(forceFullCollection: true);
            var baselineGen0 = GC.CollectionCount(0);
            var baselineGen1 = GC.CollectionCount(1);
            var baselineGen2 = GC.CollectionCount(2);
            var baselinePause = GC.GetTotalPauseDuration();

            for (var index = 0; index < _options.Iterations; index++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                scenario.Run(new LeakTracker(enabled: false));
                allocations.Add(GC.GetAllocatedBytesForCurrentThread() - before);
            }

            var leakTracker = new LeakTracker();
            for (var index = 0; index < _options.LeakCycles; index++)
            {
                scenario.Run(leakTracker);
            }

            var alive = leakTracker.CollectAlive();
            var heapAfter = GC.GetTotalMemory(forceFullCollection: true);
            var totalAllocated = GC.GetTotalAllocatedBytes(precise: true) - baselineAllocated;
            var pause = GC.GetTotalPauseDuration() - baselinePause;
            var memory = GC.GetGCMemoryInfo();

            var allocation = BuildAllocationReport(allocations);
            if (alive.Count > 0)
            {
                failures.Add($"leak: {alive.Count} of {leakTracker.Count} tracked objects survived a full collection");
            }

            var retained = heapAfter - baselineHeap;
            if (_options.MaxRetainedBytes is { } maxRetained && retained > maxRetained)
            {
                failures.Add($"retained {retained} bytes exceeds {maxRetained} bytes");
            }

            CompareToBaseline(scenario.Name, allocation, alive.Count, failures);

            return new ScenarioReport
            {
                Name = scenario.Name,
                Description = scenario.Description,
                Iterations = _options.Iterations,
                LeakCycles = _options.LeakCycles,
                Allocation = allocation,
                TotalAllocatedBytes = totalAllocated,
                Gen0Collections = GC.CollectionCount(0) - baselineGen0,
                Gen1Collections = GC.CollectionCount(1) - baselineGen1,
                Gen2Collections = GC.CollectionCount(2) - baselineGen2,
                PauseMilliseconds = pause.TotalMilliseconds,
                RetainedBytes = retained,
                HeapSizeBytes = memory.HeapSizeBytes,
                FragmentedBytes = memory.FragmentedBytes,
                TrackedObjects = leakTracker.Count,
                AliveWeakReferences = alive.Count,
                AliveLabels = alive.Take(32).ToArray(),
                Success = failures.Count == 0,
                Failures = failures,
            };
        }
        catch (Exception exception)
        {
            return new ScenarioReport
            {
                Name = scenario.Name,
                Description = scenario.Description,
                Iterations = _options.Iterations,
                LeakCycles = _options.LeakCycles,
                Allocation = BuildAllocationReport(allocations),
                Error = exception.ToString(),
                Failures = ["scenario threw: " + exception.Message],
                Success = false,
            };
        }
    }

    private void CompareToBaseline(
        string scenarioName,
        AllocationReport allocation,
        int aliveWeakReferences,
        List<string> failures)
    {
        if (_baseline is null)
        {
            return;
        }

        var previous = _baseline.Scenarios.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, scenarioName, StringComparison.Ordinal));
        if (previous is null)
        {
            failures.Add("baseline has no result for this scenario");
            return;
        }

        var allowed = previous.Allocation.MedianBytes * (1d + (_options.TolerancePercent / 100d));
        if (allocation.MedianBytes > allowed)
        {
            failures.Add(
                $"allocation median {allocation.MedianBytes} exceeds baseline {previous.Allocation.MedianBytes} by more than {_options.TolerancePercent:F1}%");
        }

        if (aliveWeakReferences > previous.AliveWeakReferences)
        {
            failures.Add(
                $"alive weak references {aliveWeakReferences} exceeds baseline {previous.AliveWeakReferences}");
        }
    }

    private static AllocationReport BuildAllocationReport(List<long> allocations)
    {
        if (allocations.Count == 0)
        {
            return new AllocationReport();
        }

        var sorted = allocations.Order().ToArray();
        var median = sorted[sorted.Length / 2];
        if (sorted.Length % 2 == 0)
        {
            median = (sorted[(sorted.Length / 2) - 1] + sorted[sorted.Length / 2]) / 2;
        }

        return new AllocationReport
        {
            MinBytes = sorted[0],
            MedianBytes = median,
            MeanBytes = (long)sorted.Average(),
            MaxBytes = sorted[^1],
            P95Bytes = sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * 0.95) - 1)],
        };
    }

    private static GcReport? LoadBaseline(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<GcReport>(stream, JsonOptions)
               ?? throw new InvalidOperationException($"baseline '{path}' is empty");
    }

    /// <summary>Formats a byte count with an invariant unit suffix.</summary>
    public static string Bytes(long bytes)
    {
        var value = (double)bytes;
        string[] units = ["B", "KB", "MB", "GB"];
        var unit = 0;
        while (Math.Abs(value) >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return value.ToString("F1", CultureInfo.InvariantCulture) + " " + units[unit];
    }
}
