namespace Easel.GcAnalysis;

/// <summary>Command-line options for the GC analysis harness.</summary>
internal sealed class GcOptions
{
    /// <summary>Scenario name or <c>all</c>.</summary>
    public string Scenario { get; private set; } = "all";

    /// <summary>Measured iterations per scenario.</summary>
    public int Iterations { get; private set; } = 8;

    /// <summary>Additional leak-stress create/dispose cycles.</summary>
    public int LeakCycles { get; private set; } = 12;

    /// <summary>Optional JSON report path.</summary>
    public string? Output { get; private set; }

    /// <summary>Optional previous report used as a regression baseline.</summary>
    public string? Baseline { get; private set; }

    /// <summary>Allowed allocation increase over the baseline, in percent.</summary>
    public double TolerancePercent { get; private set; } = 15d;

    /// <summary>Optional per-scenario retained-byte ceiling.</summary>
    public long? MaxRetainedBytes { get; private set; }

    /// <summary>Print the scenario list and exit.</summary>
    public bool List { get; private set; }

    /// <summary>Print help and exit.</summary>
    public bool Help { get; private set; }

    /// <summary>Parses arguments; returns an error message for bad input.</summary>
    public static GcOptions Parse(string[] args, out string? error)
    {
        var options = new GcOptions();
        error = null;
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--scenario":
                    if (!TryTakeValue(args, ref index, out var scenario))
                    {
                        error = "--scenario requires a name";
                        return options;
                    }

                    options.Scenario = scenario;
                    break;
                case "--iterations":
                    if (!TryTakeInt(args, ref index, out var iterations, 1))
                    {
                        error = "--iterations requires a positive integer";
                        return options;
                    }

                    options.Iterations = iterations;
                    break;
                case "--cycles":
                    if (!TryTakeInt(args, ref index, out var cycles, 0))
                    {
                        error = "--cycles requires a non-negative integer";
                        return options;
                    }

                    options.LeakCycles = cycles;
                    break;
                case "--output":
                    if (!TryTakeValue(args, ref index, out var output))
                    {
                        error = "--output requires a path";
                        return options;
                    }

                    options.Output = output;
                    break;
                case "--baseline":
                    if (!TryTakeValue(args, ref index, out var baseline))
                    {
                        error = "--baseline requires a path";
                        return options;
                    }

                    options.Baseline = baseline;
                    break;
                case "--tolerance":
                    if (!TryTakeDouble(args, ref index, out var tolerance, 0d))
                    {
                        error = "--tolerance requires a non-negative percentage";
                        return options;
                    }

                    options.TolerancePercent = tolerance;
                    break;
                case "--max-retained-bytes":
                    if (!TryTakeLong(args, ref index, out var retained, 0))
                    {
                        error = "--max-retained-bytes requires a non-negative integer";
                        return options;
                    }

                    options.MaxRetainedBytes = retained;
                    break;
                case "--list":
                    options.List = true;
                    break;
                case "--help":
                case "-h":
                    options.Help = true;
                    break;
                default:
                    error = $"unknown argument: {argument}";
                    return options;
            }
        }

        return options;
    }

    private static bool TryTakeValue(string[] args, ref int index, out string value)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool TryTakeInt(string[] args, ref int index, out int value, int minimum)
    {
        value = 0;
        return TryTakeValue(args, ref index, out var text)
               && int.TryParse(text, out value)
               && value >= minimum;
    }

    private static bool TryTakeLong(string[] args, ref int index, out long value, long minimum)
    {
        value = 0;
        return TryTakeValue(args, ref index, out var text)
               && long.TryParse(text, out value)
               && value >= minimum;
    }

    private static bool TryTakeDouble(string[] args, ref int index, out double value, double minimum)
    {
        value = 0d;
        return TryTakeValue(args, ref index, out var text)
               && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value)
               && value >= minimum;
    }
}
