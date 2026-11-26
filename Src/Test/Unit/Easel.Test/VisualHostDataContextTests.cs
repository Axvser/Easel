using Easel.Visuals;
using Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// The host is the root of the DataContext inheritance chain: a WPF-style Window/UserControl context that every
/// element without a local value picks up, and that keeps updating as the host context or the root changes.
/// </summary>
[STATestClass]
public sealed class VisualHostDataContextTests
{
    [TestMethod]
    public void RootAndChildWithoutLocalValues_InheritTheHostDataContext()
    {
        using var host = new VisualHost();
        var root = new Grid();
        var child = new Grid();
        root.Add(child);
        var context = new object();

        host.Root = root;
        host.DataContext = context;

        Assert.AreSame(context, root.DataContext, "the root inherits the host context");
        Assert.AreSame(context, child.DataContext, "a child inherits through the root");
    }

    [TestMethod]
    public void HostDataContextSetBeforeTheRoot_ReachesTheNewRoot()
    {
        using var host = new VisualHost();
        var context = new object();
        host.DataContext = context;

        var root = new Grid();
        host.Root = root;

        Assert.AreSame(context, root.DataContext, "assigning the root re-raises the current host context");
    }

    [TestMethod]
    public void LocalRootDataContext_OverridesTheHostAndClearingItFallsBack()
    {
        using var host = new VisualHost();
        var root = new Grid();
        var child = new Grid();
        root.Add(child);
        var hostContext = new object();
        var localContext = new object();
        host.DataContext = hostContext;
        host.Root = root;

        root.DataContext = localContext;

        Assert.AreSame(localContext, root.DataContext, "a local value wins over the host");
        Assert.AreSame(localContext, child.DataContext);

        root.ClearValue(VisualElement.DataContextProperty);

        Assert.AreSame(hostContext, root.DataContext, "clearing the local value falls back to the host");
        Assert.AreSame(hostContext, child.DataContext);
    }

    [TestMethod]
    public void ChangingTheHostDataContext_UpdatesTheRootAndInheritingDescendants()
    {
        using var host = new VisualHost();
        var root = new Grid();
        var child = new Grid();
        var localChild = new Grid { DataContext = "local" };
        root.Add(child);
        root.Add(localChild);
        host.Root = root;

        var first = new object();
        var second = new object();
        host.DataContext = first;

        Assert.AreSame(first, root.DataContext);
        Assert.AreSame(first, child.DataContext);

        host.DataContext = second;

        Assert.AreSame(second, root.DataContext);
        Assert.AreSame(second, child.DataContext);
        Assert.AreEqual("local", localChild.DataContext, "a local descendant value is never overwritten");
    }

    [TestMethod]
    public void ReplacingTheRoot_MakesTheNewRootInheritTheCurrentContext()
    {
        using var host = new VisualHost();
        var context = new object();
        host.DataContext = context;
        var firstRoot = new Grid();
        host.Root = firstRoot;

        var secondRoot = new Grid();
        var secondChild = new Grid();
        secondRoot.Add(secondChild);
        host.Root = secondRoot;

        Assert.AreSame(context, secondRoot.DataContext);
        Assert.AreSame(context, secondChild.DataContext);
    }

    [TestMethod]
    public void ClearingTheHostDataContext_FallsBackToNull()
    {
        using var host = new VisualHost();
        var root = new Grid();
        var child = new Grid();
        root.Add(child);
        host.DataContext = new object();
        host.Root = root;

        host.DataContext = null;

        Assert.IsNull(root.DataContext);
        Assert.IsNull(child.DataContext);
    }

    [TestMethod]
    public void PostWithoutAHandle_RunsTheActionSynchronously()
    {
        using var host = new VisualHost();
        var ran = false;

        host.Post(() => ran = true);

        Assert.IsTrue(ran, "a host without a handle runs the action inline");
    }

    [TestMethod]
    public void PostNull_Throws()
    {
        using var host = new VisualHost();

        Assert.ThrowsExactly<ArgumentNullException>(() => host.Post(null!));
    }
}
