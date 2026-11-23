using System.Drawing;
using Easel.Visuals;
using Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>Static/dynamic resource references on the same target property follow the last writer.</summary>
[TestClass]
public sealed class ResourceBindingPriorityTests
{
    private static Color BackgroundColor(Border border)
        => border.Background?.ApproximateColor ?? Color.Empty;

    [TestMethod]
    public void LaterStaticReference_SupersedesAnEarlierDynamicReference()
    {
        using var root = new Border();
        using var child = new Border();
        root.Resources["A"] = Color.Red;
        root.Resources["B"] = Color.Blue;
        root.Add(child);

        var dynamicBinding = VisualResourceLookup.BindDynamic<Easel.Brush>(
            child,
            "A",
            value => child.Background = value,
            targetToken: VisualElement.BackgroundProperty);
        Assert.AreEqual(Color.Red.ToArgb(), BackgroundColor(child).ToArgb());

        // A later static reference wins; the earlier dynamic reference must stop writing.
        VisualResourceLookup.ApplyStatic<Easel.Brush>(
            child,
            "B",
            value => child.Background = value,
            VisualElement.BackgroundProperty);
        Assert.AreEqual(Color.Blue.ToArgb(), BackgroundColor(child).ToArgb());

        root.Resources["A"] = Color.Green;
        Assert.AreEqual(
            Color.Blue.ToArgb(),
            BackgroundColor(child).ToArgb(),
            "the superseded dynamic reference must not overwrite the later static value");

        dynamicBinding.Dispose();
    }

    [TestMethod]
    public void LaterDynamicReference_SupersedesAnEarlierStaticReference()
    {
        using var root = new Border();
        using var child = new Border();
        root.Resources["A"] = Color.Red;
        root.Resources["C"] = Color.Yellow;
        root.Add(child);

        VisualResourceLookup.ApplyStatic<Easel.Brush>(
            child,
            "A",
            value => child.Background = value,
            VisualElement.BackgroundProperty);
        Assert.AreEqual(Color.Red.ToArgb(), BackgroundColor(child).ToArgb());

        var dynamicBinding = VisualResourceLookup.BindDynamic<Easel.Brush>(
            child,
            "C",
            value => child.Background = value,
            targetToken: VisualElement.BackgroundProperty);
        Assert.AreEqual(Color.Yellow.ToArgb(), BackgroundColor(child).ToArgb());

        root.Resources["C"] = Color.Magenta;
        Assert.AreEqual(
            Color.Magenta.ToArgb(),
            BackgroundColor(child).ToArgb(),
            "the later dynamic reference tracks dictionary changes");

        dynamicBinding.Dispose();
    }
}
