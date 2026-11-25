using System.Drawing;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Dynamic resource references follow the ancestor chain as it grows, are re-hosted when their nearest source moves,
/// and fall back to their default when the key disappears. The binding updates its subscriptions incrementally, so
/// these cases must keep working without a full unsubscribe/resubscribe cycle.
/// </summary>
[STATestClass]
public sealed class ResourceBindingChainTests
{
    private static Color BackgroundColor(VC.Border border)
        => border.Background?.ApproximateColor ?? Color.Empty;

    [TestMethod]
    public void DynamicReference_CreatedBeforeParenting_ResolvesWhenTheChainGrows()
    {
        using var root = new VC.Border();
        root.Resources["Accent"] = Color.Red;

        using var child = new VC.Border();
        var binding = VisualResourceLookup.BindDynamic<Easel.Brush>(
            child,
            "Accent",
            value => child.Background = value,
            targetToken: VisualElement.BackgroundProperty);

        Assert.AreEqual(Color.Empty.ToArgb(), BackgroundColor(child).ToArgb(), "nothing is reachable yet");

        root.Add(child);

        Assert.AreEqual(Color.Red.ToArgb(), BackgroundColor(child).ToArgb(), "the binding followed the new parent");

        binding.Dispose();
    }

    [TestMethod]
    public void DynamicReference_FollowsTheNearestSourceWhenTheChainIsRerouted()
    {
        using var root = new VC.Border();
        root.Resources["Accent"] = Color.Red;

        using var child = new VC.Border();
        root.Add(child);

        var binding = VisualResourceLookup.BindDynamic<Easel.Brush>(
            child,
            "Accent",
            value => child.Background = value,
            targetToken: VisualElement.BackgroundProperty);
        Assert.AreEqual(Color.Red.ToArgb(), BackgroundColor(child).ToArgb());

        root.RemoveChild(child);

        using var middle = new VC.Border();
        middle.Resources["Accent"] = Color.Blue;
        middle.Add(child);
        root.Add(middle);

        Assert.AreEqual(
            Color.Blue.ToArgb(),
            BackgroundColor(child).ToArgb(),
            "the nearer dictionary now wins over the previous source");

        binding.Dispose();
    }

    [TestMethod]
    public void DynamicReference_ClearsWhenTheKeyIsRemovedAndAppliesAgainWhenItReturns()
    {
        using var root = new VC.Border();
        root.Resources["Accent"] = Color.Red;

        using var child = new VC.Border();
        root.Add(child);

        var missing = 0;
        var binding = VisualResourceLookup.BindDynamic<Easel.Brush>(
            child,
            "Accent",
            value => child.Background = value,
            onMissing: () =>
            {
                missing++;
                child.ClearValue(VisualElement.BackgroundProperty);
            },
            targetToken: VisualElement.BackgroundProperty);
        Assert.AreEqual(Color.Red.ToArgb(), BackgroundColor(child).ToArgb());

        root.Resources.Remove("Accent");

        Assert.AreEqual(1, missing, "the previously resolved key disappeared");
        Assert.AreEqual(Color.Empty.ToArgb(), BackgroundColor(child).ToArgb(), "the target fell back to its default");

        root.Resources["Accent"] = Color.Green;

        Assert.AreEqual(Color.Green.ToArgb(), BackgroundColor(child).ToArgb(), "a later declaration still applies");
        Assert.AreEqual(1, missing, "the missing callback only runs once per disappearance");

        binding.Dispose();
    }

    [TestMethod]
    public void DynamicReference_ClearingTheWholeDictionaryFallsBackToADefault()
    {
        using var root = new VC.Border();
        root.Resources["Accent"] = Color.Red;

        using var child = new VC.Border();
        root.Add(child);

        var missing = 0;
        var binding = VisualResourceLookup.BindDynamic<Easel.Brush>(
            child,
            "Accent",
            value => child.Background = value,
            onMissing: () =>
            {
                missing++;
                child.ClearValue(VisualElement.BackgroundProperty);
            },
            targetToken: VisualElement.BackgroundProperty);
        Assert.AreEqual(Color.Red.ToArgb(), BackgroundColor(child).ToArgb());

        root.Resources.Clear();

        Assert.AreEqual(1, missing, "clearing the dictionary reports one disappearance");
        Assert.AreEqual(Color.Empty.ToArgb(), BackgroundColor(child).ToArgb());

        binding.Dispose();
    }

    [TestMethod]
    public void DynamicReference_ResolvesASurfaceResourceAfterTheRootIsHosted()
    {
        using var surface = new VisualHost { Size = new Size(120, 80), DpiScaleOverride = 1f };
        surface.Resources["Accent"] = Color.Teal;

        var root = new VC.StackPanel();
        using var child = new VC.Border();
        var binding = VisualResourceLookup.BindDynamic<Easel.Brush>(
            child,
            "Accent",
            value => child.Background = value,
            targetToken: VisualElement.BackgroundProperty);
        root.Add(child);

        Assert.AreEqual(Color.Empty.ToArgb(), BackgroundColor(child).ToArgb(), "the surface is not reachable yet");

        surface.Root = root;

        Assert.AreEqual(Color.Teal.ToArgb(), BackgroundColor(child).ToArgb(), "the surface resource resolved on attach");

        binding.Dispose();
    }
}
