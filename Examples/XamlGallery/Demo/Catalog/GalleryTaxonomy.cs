namespace Demo;

/// <summary>The catalog's three-level order: section, then group, then card.</summary>
/// <remarks>
/// Industrial-facing material leads every section: HMI pages first, then the controls and visuals an
/// HMI page is built from. A card names its group; the section is derived here so a group is declared once.
/// </remarks>
internal static class GalleryTaxonomy
{
    /// <summary>The scenario pages section.</summary>
    public const string Scenarios = "Scenarios";

    /// <summary>The Easel visual-tree component section.</summary>
    public const string EaselNative = "Easel native";

    /// <summary>The WinForms interop section.</summary>
    public const string WinFormsNative = "WinForms native";

    private static readonly (string Section, string[] Groups)[] Order =
    [
        (Scenarios, ["Industrial", "Applications", "Visual studies"]),
        (EaselNative, ["Input", "Data", "Graphics", "Animation", "Binding", "Styling", "Layout", "Text", "Overlay", "Theme", "Basics", "Visuals"]),
        (WinFormsNative, ["Native"]),
    ];

    private static readonly Dictionary<string, (string Section, int Rank)> Groups = BuildGroups();

    /// <summary>The sections in catalog order.</summary>
    public static IReadOnlyList<string> Sections { get; } =
        Order.Select(static entry => entry.Section).ToArray();

    /// <summary>The section that owns a group.</summary>
    /// <param name="group">The group name.</param>
    /// <returns>The owning section; an unknown group falls under <see cref="EaselNative"/>.</returns>
    public static string SectionOf(string group)
        => Groups.TryGetValue(group, out var entry) ? entry.Section : EaselNative;

    /// <summary>The section's position in the catalog.</summary>
    /// <param name="section">The section name.</param>
    public static int SectionRank(string section)
    {
        for (var index = 0; index < Order.Length; index++)
        {
            if (string.Equals(Order[index].Section, section, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    /// <summary>The group's position inside its section.</summary>
    /// <param name="group">The group name.</param>
    /// <returns>The rank; an unknown group sorts after the declared groups.</returns>
    public static int GroupRank(string group)
        => Groups.TryGetValue(group, out var entry) ? entry.Rank : int.MaxValue;

    private static Dictionary<string, (string Section, int Rank)> BuildGroups()
    {
        var groups = new Dictionary<string, (string Section, int Rank)>(StringComparer.Ordinal);
        foreach (var (section, names) in Order)
        {
            for (var index = 0; index < names.Length; index++)
            {
                groups[names[index]] = (section, index);
            }
        }

        return groups;
    }
}
