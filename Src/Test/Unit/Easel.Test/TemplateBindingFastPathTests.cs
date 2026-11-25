using System.ComponentModel;
using System.Drawing;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Covers the single-segment <see cref="TemplateBinding"/> fast path and the lazy resource binding bookkeeping that
/// sit on the template-instantiation hot path: both must fall back to the general machinery for anything unusual.
/// </summary>
[STATestClass]
public sealed class TemplateBindingFastPathTests
{
    [TestMethod]
    public void TemplateBinding_FollowsTheTemplatedParentAfterApplication()
    {
        using var control = new VC.Button { Text = "First" };
        var template = new VisualControlTemplate(scope =>
        {
            var text = new VC.TextBlock();
            VisualBindingOperations.Bind(text, VC.TextBlock.TextProperty, new TemplateBinding("Text"));
            scope.Register("PART_Text", text);
            return text;
        })
        {
            TargetType = typeof(VC.Button),
        };

        control.Template = template;
        var part = control.GetTemplateChild<VC.TextBlock>("PART_Text")!;

        Assert.AreEqual("First", part.Text, "the binding resolves once the templated parent is assigned");

        control.Text = "Second";

        Assert.AreEqual("Second", part.Text, "the fast path keeps listening to the templated parent");

        control.Width = 200;

        Assert.AreEqual("Second", part.Text, "an unrelated source change keeps the bound value");
    }

    [TestMethod]
    public void TemplateBinding_TwoWayFallsBackToTheGeneralExpression()
    {
        using var control = new VC.Button { Text = "Start" };
        var template = new VisualControlTemplate(scope =>
        {
            var text = new VC.TextBlock();
            VisualBindingOperations.Bind(
                text,
                VC.TextBlock.TextProperty,
                new TemplateBinding("Text", BindingMode.TwoWay));
            scope.Register("PART_Text", text);
            return text;
        })
        {
            TargetType = typeof(VC.Button),
        };

        control.Template = template;
        var part = control.GetTemplateChild<VC.TextBlock>("PART_Text")!;

        Assert.AreEqual("Start", part.Text);

        part.Text = "Edited";

        Assert.AreEqual("Edited", control.Text, "a two-way template binding writes the target value back");
    }

    [TestMethod]
    public void TemplateBinding_WithAValidatingParentUsesTheGeneralExpression()
    {
        var parent = new ValidatingElement { Value = "Validated" };
        var template = new VisualControlTemplate(scope =>
        {
            var text = new VC.TextBlock();
            VisualBindingOperations.Bind(text, VC.TextBlock.TextProperty, new TemplateBinding("Value"));
            scope.Register("PART_Text", text);
            return text;
        })
        {
            TargetType = typeof(ValidatingElement),
        };

        var instance = template.CreateContent(parent);
        var part = instance.Scope.Find<VC.TextBlock>("PART_Text")!;

        Assert.AreEqual("Validated", part.Text, "a source implementing a validation interface still binds");
    }

    [TestMethod]
    public void DynamicResource_AppliesWhenTheElementsOwnDictionaryAppearsLater()
    {
        var border = new VC.Border();
        using var binding = VisualResourceLookup.BindDynamic<Easel.Brush>(
            border,
            "Later",
            value => border.Background = value,
            () => border.Background = null,
            VC.Border.BackgroundProperty);

        Assert.IsNull(border.Background);

        border.Resources["Later"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x12, 0x34, 0x56));

        Assert.IsNotNull(border.Background, "creating the element dictionary after the binding must resolve it");
    }

    [TestMethod]
    public void DynamicResource_SeesKeysAddedToAnAncestorAfterTheChildWasBound()
    {
        var root = new VC.StackPanel();
        var border = new VC.Border();
        root.Add(border);

        using var binding = VisualResourceLookup.BindDynamic<Easel.Brush>(
            border,
            "Shared",
            value => border.Background = value,
            () => border.Background = null,
            VC.Border.BackgroundProperty);

        root.Resources["Shared"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x65, 0x43, 0x21));

        Assert.IsNotNull(border.Background, "an ancestor dictionary added later must still reach the child");
    }

    [TestMethod]
    public void DynamicResource_ClearsItsValueWhenTheElementLeavesTheResourceChain()
    {
        using var surface = new VisualHost();
        surface.Resources["Detachable"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x0A, 0x0B, 0x0C));
        var root = new VC.StackPanel();
        var border = new VC.Border();
        root.Add(border);
        surface.Root = root;

        using var binding = VisualResourceLookup.BindDynamic<Easel.Brush>(
            border,
            "Detachable",
            value => border.Background = value,
            () => border.Background = null,
            VC.Border.BackgroundProperty);

        Assert.IsNotNull(border.Background, "the surface dictionary supplies the value");

        root.RemoveChild(border);

        Assert.IsNull(border.Background, "leaving the chain must run the missing callback and clear the old value");
    }

    [TestMethod]
    public void Style_BasedOnChangesInvalidateTheCachedChainForNewElements()
    {
        var firstBase = new VisualStyle { TargetType = typeof(VC.Button) };
        firstBase.Setters.Add(new VisualSetter(VC.Button.OpacityProperty, 0.25));
        var secondBase = new VisualStyle { TargetType = typeof(VC.Button) };
        secondBase.Setters.Add(new VisualSetter(VC.Button.OpacityProperty, 0.75));

        var style = new VisualStyle { TargetType = typeof(VC.Button), BasedOn = firstBase };

        using (var before = new VC.Button { Style = style })
        {
            Assert.AreEqual(0.25, before.Opacity);
        }

        style.BasedOn = secondBase;

        using var after = new VC.Button { Style = style };
        Assert.AreEqual(0.75, after.Opacity, "the cached base chain is discarded when BasedOn changes");
    }

    private sealed class ValidatingElement : VisualElement, IDataErrorInfo
    {
        public string Value { get; set; } = string.Empty;

        public string Error => string.Empty;

        public string this[string columnName] => string.Empty;
    }
}
