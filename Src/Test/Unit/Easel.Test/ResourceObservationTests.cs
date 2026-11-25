using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Regression coverage for the resource and property layers that were reworked for allocation cost: cycle-safe
/// merged lookups, shared-dictionary change fan-out and multi-layer priority promotion.
/// </summary>
[STATestClass]
public sealed class ResourceObservationTests
{
    /// <summary>A shared merged dictionary resolves through several paths, and a miss does not poison later lookups.</summary>
    [TestMethod]
    public void MergedDictionaries_SharedByTwoPaths_ResolveAndResetBetweenLookups()
    {
        var leaf = new ResourceDictionary { ["Gap"] = 42d };
        var left = new ResourceDictionary();
        var right = new ResourceDictionary();
        left.MergedDictionaries.Add(leaf);
        right.MergedDictionaries.Add(leaf);

        var root = new ResourceDictionary();
        root.MergedDictionaries.Add(right);
        root.MergedDictionaries.Add(left);

        Assert.IsTrue(root.TryGetValue("Gap", out var value));
        Assert.AreEqual(42d, value);

        Assert.IsFalse(root.TryGetValue("Missing", out _), "the walk visits each dictionary once");
        Assert.IsTrue(root.TryGetValue("Gap", out value), "a later lookup starts from a clean visited set");
        Assert.AreEqual(42d, value);
    }

    /// <summary>Several dynamic resources anchored on one dictionary must all receive its changes.</summary>
    [TestMethod]
    public void DynamicResource_SharedDictionary_ReachesEveryBinding()
    {
        var root = new VC.StackPanel();
        var first = new VC.Border();
        var second = new VC.Border();
        root.Add(first);
        root.Add(second);

        using var firstBinding = VisualResourceLookup.BindDynamic<double>(
            first, "Gap", value => first.Width = value, () => first.ClearValue(VisualElement.WidthProperty));
        using var secondBinding = VisualResourceLookup.BindDynamic<double>(
            second, "Gap", value => second.Width = value, () => second.ClearValue(VisualElement.WidthProperty));

        root.Resources["Gap"] = 10d;
        Assert.AreEqual(10d, first.Width);
        Assert.AreEqual(10d, second.Width);

        root.Resources["Gap"] = 20d;
        Assert.AreEqual(20d, first.Width);
        Assert.AreEqual(20d, second.Width);

        root.Resources.Remove("Gap");
        Assert.IsTrue(double.IsNaN(first.Width), "removing the key clears the first target");
        Assert.IsTrue(double.IsNaN(second.Width), "removing the key clears the second target");
    }

    /// <summary>Clearing layers in priority order promotes the next-lowest layer instead of dropping it.</summary>
    [TestMethod]
    public void PriorityLayers_PromoteTheLowestExtraLayerWhenThePrimaryIsCleared()
    {
        var model = new PriorityModel();
        // 先写最高（数值最大）的 Style 当主层，再依次叠上更低的 Template / StyleTrigger / Animation。
        model.SetValue(PriorityModel.NameProperty, "style", BindingPriority.Style);
        model.SetValue(PriorityModel.NameProperty, "template", BindingPriority.Template);
        model.SetValue(PriorityModel.NameProperty, "trigger", BindingPriority.StyleTrigger);
        model.SetValue(PriorityModel.NameProperty, "animation", BindingPriority.Animation);

        Assert.AreEqual("animation", model.Name);
        model.ClearValue(PriorityModel.NameProperty, BindingPriority.Animation);
        Assert.AreEqual("trigger", model.Name);

        model.ClearValue(PriorityModel.NameProperty, BindingPriority.Animation);
        Assert.AreEqual("trigger", model.Name, "clearing a missing layer leaves the effective value alone");

        model.ClearValue(PriorityModel.NameProperty, BindingPriority.Style);
        Assert.AreEqual("trigger", model.Name, "clearing the primary promotes the lowest remaining extra layer");

        model.ClearValue(PriorityModel.NameProperty, BindingPriority.StyleTrigger);
        Assert.AreEqual("template", model.Name, "the next extra layer is promoted in turn");

        model.ClearValue(PriorityModel.NameProperty, BindingPriority.Template);
        Assert.AreEqual("default", model.Name);
        Assert.IsFalse(model.IsSet(PriorityModel.NameProperty));
    }

    private sealed class PriorityModel : WinformsObject
    {
        public static readonly DependencyProperty NameProperty =
            DependencyProperty.Register(
                nameof(Name), typeof(string), typeof(PriorityModel), new PropertyMetadata("default"));

        public string Name
        {
            get => (string)GetValue(NameProperty)!;
            set => SetValue(NameProperty, value);
        }
    }
}
