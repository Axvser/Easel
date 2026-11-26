namespace Easel.GcAnalysis;

/// <summary>Collects weak references so a scenario can prove that disposed objects become collectible.</summary>
internal sealed class LeakTracker
{
    private readonly List<(string Label, WeakReference Reference)> _references = [];
    private readonly bool _enabled;

    /// <summary>Creates a tracker; disabled trackers pay almost nothing and are used for allocation measurements.</summary>
    public LeakTracker(bool enabled = true) => _enabled = enabled;

    /// <summary>Registers a weak reference without keeping the target alive.</summary>
    public void Track(string label, object? target)
    {
        if (!_enabled || target is null || target is string)
        {
            return;
        }

        _references.Add((label, new WeakReference(target)));
    }

    /// <summary>Number of registered weak references.</summary>
    public int Count => _references.Count;

    /// <summary>Forces full collections and returns the labels that are still alive.</summary>
    public IReadOnlyList<string> CollectAlive()
    {
        ForceFullCollection();
        var alive = new List<string>();
        foreach (var (label, reference) in _references)
        {
            if (reference.IsAlive)
            {
                alive.Add(label);
            }
        }

        return alive;
    }

    /// <summary>Runs three blocking full collections, including finalizers.</summary>
    public static void ForceFullCollection()
    {
        for (var pass = 0; pass < 3; pass++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
    }
}
