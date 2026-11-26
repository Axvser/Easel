using Easel.Visuals;
using Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>The host-behavior contract: the visual element supplies the host, the behavior never searches for it.</summary>
[STATestClass]
public sealed class HostBehaviorTests
{
    [TestMethod]
    public void HostBehavior_AddedBeforeTheRootIsHosted_AttachesWhenTheSurfaceArrives()
    {
        using var surface = new VisualHost();
        var element = new Grid();
        var behavior = new RecordingHostBehavior();

        element.AddHostBehavior(behavior);
        Assert.AreEqual(0, behavior.AttachCount, "no host exists yet");

        surface.Root = element;

        Assert.AreEqual(1, behavior.AttachCount);
        Assert.AreSame(surface, behavior.Host);
    }

    [TestMethod]
    public void HostBehavior_AddedToAHostedElement_AttachesImmediately()
    {
        using var surface = new VisualHost();
        var element = new Grid();
        surface.Root = element;

        var behavior = new RecordingHostBehavior();
        element.AddHostBehavior(behavior);

        Assert.AreEqual(1, behavior.AttachCount);
        Assert.AreSame(surface, behavior.Host);
    }

    [TestMethod]
    public void HostBehavior_Removed_Detaches()
    {
        using var surface = new VisualHost();
        var element = new Grid();
        surface.Root = element;
        var behavior = new RecordingHostBehavior();
        element.AddHostBehavior(behavior);

        element.RemoveHostBehavior(behavior);

        Assert.AreEqual(1, behavior.DetachCount);
        Assert.IsNull(behavior.Host);
    }

    [TestMethod]
    public void HostBehavior_RootReplaced_DetachesFromTheOldHost()
    {
        using var surface = new VisualHost();
        var element = new Grid();
        var other = new Grid();
        surface.Root = element;
        var behavior = new RecordingHostBehavior();
        element.AddHostBehavior(behavior);

        surface.Root = other;

        Assert.AreEqual(1, behavior.DetachCount);
        Assert.IsNull(behavior.Host);
    }

    [TestMethod]
    public void HostBehavior_MovedToAnotherHost_DetachesThenAttaches()
    {
        using var first = new VisualHost();
        using var second = new VisualHost();
        var element = new Grid();
        var behavior = new RecordingHostBehavior();
        element.AddHostBehavior(behavior);

        first.Root = element;
        second.Root = element;

        Assert.AreEqual(2, behavior.AttachCount);
        Assert.AreEqual(1, behavior.DetachCount);
        Assert.AreSame(second, behavior.Host);
    }

    [TestMethod]
    public void HostBehavior_AddedTwice_AttachesOnce()
    {
        using var surface = new VisualHost();
        var element = new Grid();
        var behavior = new RecordingHostBehavior();
        element.AddHostBehavior(behavior);
        element.AddHostBehavior(behavior);

        surface.Root = element;

        Assert.AreEqual(1, behavior.AttachCount);
    }

    [TestMethod]
    public void HostBehavior_OnADescendant_SeesTheRootSurface()
    {
        using var surface = new VisualHost();
        var root = new Grid();
        var child = new Grid();
        root.Add(child);
        var behavior = new RecordingHostBehavior();
        child.AddHostBehavior(behavior);

        surface.Root = root;

        Assert.AreEqual(1, behavior.AttachCount);
        Assert.AreSame(surface, behavior.Host);
    }

    [TestMethod]
    public void HostBehavior_DetachesWhenTheElementIsDisposed()
    {
        using var surface = new VisualHost();
        var element = new Grid();
        surface.Root = element;
        var behavior = new RecordingHostBehavior();
        element.AddHostBehavior(behavior);

        element.Dispose();

        Assert.AreEqual(1, behavior.DetachCount);
        Assert.IsNull(behavior.Host);
    }

    private sealed class RecordingHostBehavior : IVisualHostBehavior
    {
        public VisualHost? Host { get; private set; }

        public int AttachCount { get; private set; }

        public int DetachCount { get; private set; }

        public void Attach(VisualHost host)
        {
            AttachCount++;
            Host = host;
        }

        public void Detach()
        {
            DetachCount++;
            Host = null;
        }
    }
}
