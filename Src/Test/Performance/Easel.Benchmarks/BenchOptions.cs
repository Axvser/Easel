using System.Globalization;

namespace Easel.Benchmarks;

internal sealed class OptionException(string message) : Exception(message);

/// <summary>Parsed command line for the benchmark runner.</summary>
internal sealed class BenchOptions
{
    public const string DefaultOutputPath = "benchmark-results.json";

    public bool ShowHelp { get; private set; }

    public bool ListScenarios { get; private set; }

    public bool NoJson { get; private set; }

    public string OutputPath { get; private set; } = DefaultOutputPath;

    public string? BaselinePath { get; private set; }

    public int? Warmup { get; private set; }

    public int? Iterations { get; private set; }

    public HashSet<string> ScenarioFilter { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, double> Budgets { get; } = new(StringComparer.OrdinalIgnoreCase);

    public double? DefaultBudget { get; private set; }

    public string BudgetMetric { get; private set; } = "median";

    public string[] Arguments { get; private set; } = [];

    public static BenchOptions Parse(string[] args)
    {
        var options = new BenchOptions { Arguments = args };

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "-h":
                case "--help":
                case "-?":
                    options.ShowHelp = true;
                    break;
                case "--list":
                    options.ListScenarios = true;
                    break;
                case "--no-json":
                    options.NoJson = true;
                    break;
                case "-o":
                case "--output":
                    options.OutputPath = RequiredValue(args, ref index, argument);
                    break;
                case "--baseline":
                    options.BaselinePath = RequiredValue(args, ref index, argument);
                    break;
                case "--warmup":
                    options.Warmup = ParseNonNegativeInt(RequiredValue(args, ref index, argument), argument);
                    break;
                case "--iterations":
                    options.Iterations = ParsePositiveInt(RequiredValue(args, ref index, argument), argument);
                    break;
                case "--scenario":
                    foreach (var name in RequiredValue(args, ref index, argument)
                                 .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        options.ScenarioFilter.Add(name);
                    }

                    break;
                case "--budget":
                    AddBudget(options, RequiredValue(args, ref index, argument));
                    break;
                case "--budget-metric":
                    options.BudgetMetric = RequiredValue(args, ref index, argument).ToLowerInvariant();
                    if (options.BudgetMetric is not ("median" or "p95"))
                    {
                        throw new OptionException("--budget-metric must be 'median' or 'p95'.");
                    }

                    break;
                default:
                    throw new OptionException($"Unknown option '{argument}'. Run --help for usage.");
            }
        }

        return options;
    }

    private static string RequiredValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new OptionException($"{option} requires a value.");
        }

        index++;
        return args[index];
    }

    private static int ParsePositiveInt(string value, string option)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 1)
        {
            throw new OptionException($"{option} must be a positive integer.");
        }

        return parsed;
    }

    private static int ParseNonNegativeInt(string value, string option)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            throw new OptionException($"{option} must be zero or a positive integer.");
        }

        return parsed;
    }

    // --budget 10%       default budget for every scenario
    // --budget build=25% budget for one named scenario
    private static void AddBudget(BenchOptions options, string value)
    {
        var separator = value.IndexOf('=');
        var name = separator < 0 ? null : value[..separator].Trim();
        var raw = separator < 0 ? value : value[(separator + 1)..].Trim();
        var ratio = ParseRatio(raw);

        if (string.IsNullOrEmpty(name))
        {
            options.DefaultBudget = ratio;
        }
        else
        {
            options.Budgets[name] = ratio;
        }
    }

    // Accepts "10%", "0.10", or a bare number above 1 interpreted as a percentage.
    private static double ParseRatio(string value)
    {
        var percent = value.EndsWith('%');
        if (percent)
        {
            value = value[..^1].Trim();
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || double.IsNaN(parsed)
            || double.IsInfinity(parsed)
            || parsed < 0)
        {
            throw new OptionException($"'{value}' is not a valid budget; use e.g. 10% or 0.10.");
        }

        return percent || parsed > 1 ? parsed / 100 : parsed;
    }
}
