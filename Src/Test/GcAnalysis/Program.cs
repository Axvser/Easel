using System.Globalization;
using System.Text.Json;

namespace Easel.GcAnalysis;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var options = GcOptions.Parse(args, out var error);
        if (error is not null)
        {
            Console.Error.WriteLine(error);
            PrintHelp();
            return 2;
        }

        if (options.Help)
        {
            PrintHelp();
            return 0;
        }

        if (options.List)
        {
            foreach (var scenario in GcScenarios.Create("all"))
            {
                Console.WriteLine($"{scenario.Name,-20} {scenario.Description}");
            }

            return 0;
        }

        try
        {
            var report = new GcRunner(options).Run();
            PrintReport(report);
            if (!string.IsNullOrWhiteSpace(options.Output))
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(options.Output));
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(
                    options.Output,
                    JsonSerializer.Serialize(report, GcRunner.SerializerOptions));
                Console.WriteLine($"report: {Path.GetFullPath(options.Output)}");
            }

            return report.Success ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 2;
        }
    }

    private static void PrintReport(GcReport report)
    {
        Console.WriteLine(
            $"Easel GC analysis · {report.Runtime} · {report.OperatingSystem} · {report.ProcessorCount} CPUs");
        Console.WriteLine(
            $"GC · server={report.ServerGc} concurrent={report.ConcurrentGc} latency={report.LatencyMode} · iterations={report.Iterations} · leak cycles={report.LeakCycles}");
        Console.WriteLine();
        Console.WriteLine(
            string.Format(
                CultureInfo.InvariantCulture,
                "{0,-20} {1,16} {2,18} {3,12} {4,9} {5,10} {6,6}",
                "scenario",
                "alloc/op median",
                "min..max",
                "gen0/1/2",
                "pause",
                "retained",
                "leaks"));
        foreach (var scenario in report.Scenarios)
        {
            var allocation = scenario.Allocation;
            var range = $"{GcRunner.Bytes(allocation.MinBytes)}..{GcRunner.Bytes(allocation.MaxBytes)}";
            var gens = $"{scenario.Gen0Collections}/{scenario.Gen1Collections}/{scenario.Gen2Collections}";
            Console.WriteLine(
                $"{scenario.Name,-20} {GcRunner.Bytes(allocation.MedianBytes),16} {range,18} {gens,12} {scenario.PauseMilliseconds.ToString("F1", CultureInfo.InvariantCulture) + " ms",9} {GcRunner.Bytes(scenario.RetainedBytes),10} {scenario.AliveWeakReferences,6}");
        }

        Console.WriteLine();
        foreach (var scenario in report.Scenarios)
        {
            if (scenario.Error is { } scenarioError)
            {
                Console.WriteLine($"[error] {scenario.Name}: {scenarioError}");
            }

            foreach (var failure in scenario.Failures)
            {
                Console.WriteLine($"[fail] {scenario.Name}: {failure}");
            }

            if (scenario.AliveLabels.Count > 0)
            {
                Console.WriteLine($"[leak] {scenario.Name}: {string.Join(", ", scenario.AliveLabels)}");
            }
        }

        Console.WriteLine(report.Success ? "PASS" : "FAIL");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(
            """
            Easel GC analysis — allocation pressure and leak detection.

            Usage:
              dotnet run -c Release --project Src/Test/GcAnalysis -- [options]

            Options:
              --scenario <name|all>       scenario or comma-separated list (default all)
              --iterations <n>            measured operations per scenario (default 8)
              --cycles <n>                extra create/dispose cycles for leak checks (default 12)
              --output <path>             write the JSON report
              --baseline <path>           compare allocation medians and leaks with a previous report
              --tolerance <percent>       allowed allocation increase over baseline (default 15)
              --max-retained-bytes <n>    fail a scenario whose retained heap growth exceeds n
              --list                      list scenarios
              --help                      show this help

            Exit codes: 0 pass, 1 leak/regression/budget failure, 2 invalid arguments or report error.
            """);
    }
}
