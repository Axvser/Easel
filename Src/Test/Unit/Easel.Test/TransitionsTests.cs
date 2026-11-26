using System.Drawing;
using Easel.Generator;
using Easel.Test.Infrastructure;
using Easel.Visuals;
using Microsoft.CodeAnalysis;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Element-level transitions: a listed dependency property animates from its old value to its new value, while
/// non-interpolatable, null-ended and unchanged values are skipped; the markup form compiles into the collection.
/// </summary>
[STATestClass]
public sealed class TransitionsTests
{
    [TestMethod]
    public void Transition_AnimatesTheNextOpacityChange()
    {
        var border = new VC.Border { Opacity = 0d };
        border.Transitions.Add(new Transition
        {
            Property = VisualElement.OpacityProperty,
            Duration = TimeSpan.FromMilliseconds(180),
        });

        border.Opacity = 1d;

        Assert.IsTrue(
            VisualTransition.HasActive(border),
            "changing a listed property starts a transition");
        Assert.IsTrue(
            border.Opacity < 1d,
            "the first frame restores the old value instead of jumping to the new one");

        WaitForTransition(border);

        Assert.IsFalse(VisualTransition.HasActive(border), "the transition reaches its end");
        Assert.AreEqual(1d, border.Opacity, 0.001d, "the transition lands on the new value");

        // 收尾摘掉动画层也会发一跳变更，必须被抑制，否则会自激出下一趟过渡。
        Thread.Sleep(100);
        Assert.IsFalse(VisualTransition.HasActive(border), "releasing the animation layer does not start another transition");
    }

    [TestMethod]
    public void Transition_AnimatesBrushes()
    {
        var border = new VC.Border { Background = Color.Red };
        border.Transitions.Add(new Transition
        {
            Property = VisualElement.BackgroundProperty,
            Duration = TimeSpan.FromMilliseconds(180),
        });

        border.Background = Color.Blue;

        var first = border.Background!.ApproximateColor;
        Assert.IsTrue(
            first.R > 200 && first.B < 100,
            $"the first frame restores the old red brush, but was {first}");

        WaitForTransition(border);

        Assert.AreEqual(
            Color.Blue.ToArgb(),
            border.Background!.ApproximateColor.ToArgb(),
            "the transition lands on the new brush");
    }

    [TestMethod]
    public void Transition_AnimatesThicknessLikeValues()
    {
        var border = new VC.Border { CornerRadius = 2f };
        border.Transitions.Add(new Transition
        {
            Property = VisualElement.CornerRadiusProperty,
            Duration = TimeSpan.FromMilliseconds(120),
        });

        border.CornerRadius = 8f;

        Assert.IsTrue(
            VisualTransition.HasActive(border),
            "a float dependency property participates in transitions");

        WaitForTransition(border);

        Assert.AreEqual(8f, border.CornerRadius, 0.001f, "the transition lands on the new corner radius");
    }

    [TestMethod]
    public void Transition_IgnoresNonAnimatableProperties()
    {
        var border = new VC.Border();
        border.Transitions.Add(new Transition
        {
            Property = VisualElement.IsEnabledProperty,
            Duration = TimeSpan.FromMilliseconds(120),
        });

        border.IsEnabled = false;

        Assert.IsFalse(border.IsEnabled, "a property without an interpolator takes the new value directly");
        Assert.IsFalse(
            VisualTransition.HasActive(border),
            "a property without an interpolator starts no transition");
    }

    [TestMethod]
    public void Transition_DoesNotAnimateWhenTheOldValueIsNull()
    {
        var border = new VC.Border();
        border.Transitions.Add(new Transition
        {
            Property = VisualElement.BackgroundProperty,
            Duration = TimeSpan.FromMilliseconds(120),
        });

        border.Background = Color.Blue;

        Assert.IsFalse(
            VisualTransition.HasActive(border),
            "a null old value has no start state, so the change is skipped");
        Assert.AreEqual(
            Color.Blue.ToArgb(),
            border.Background!.ApproximateColor.ToArgb(),
            "a skipped change applies the new value immediately");
    }

    [TestMethod]
    public void Transition_RetargetsWhenTheValueChangesMidway()
    {
        var border = new VC.Border { Opacity = 0d };
        border.Transitions.Add(new Transition
        {
            Property = VisualElement.OpacityProperty,
            Duration = TimeSpan.FromMilliseconds(400),
        });

        border.Opacity = 1d;
        Thread.Sleep(100);
        var midway = border.Opacity;
        Assert.IsTrue(midway > 0d && midway < 1d, $"the first run is in flight (opacity={midway})");

        border.Opacity = 0d;
        Thread.Sleep(60);
        Assert.IsTrue(
            VisualTransition.HasActive(border),
            "a lower-layer change retargets the running transition instead of being masked");
        Assert.IsTrue(
            border.Opacity < midway,
            "the retarget moves from the current frame toward the new target");

        WaitForTransition(border);
        Assert.AreEqual(0d, border.Opacity, 0.001d, "the retarget lands on the latest target");
    }

    [TestMethod]
    public void Xaml_TransitionsCollectionCompiles()
    {
        const string source = """
            namespace Probe;

            public partial class ProbeVisualView : Easel.Visuals.Controls.Border
            {
            }
            """;

        const string xaml = """
            <v:Border
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:v="clr-namespace:Easel.Visuals.Controls;assembly=Easel"
                x:Class="Probe.ProbeVisualView">
              <v:Border.Transitions>
                <Transition Property="Opacity" Duration="0:0:0.1" Ease="CubicOut" />
              </v:Border.Transitions>
            </v:Border>
            """;

        var (run, output) = GeneratorProbe.RunAndCompile(
            [new XamlFileGenerator()],
            source,
            "VisualTransitionsXamlProbe",
            new InMemoryAdditionalText("ProbeVisualView.xaml", xaml));

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));

        var generated = run.GeneratedText();
        StringAssert.Contains(
            generated,
            "new global::Easel.Visuals.Transition",
            "the transition descriptor resolves to the visual runtime");
        StringAssert.Contains(
            generated,
            "OpacityProperty",
            "the property attribute resolves to the owner's dependency property");
        StringAssert.Contains(
            generated,
            ".Transitions.Add(",
            "the descriptor goes into the element's transitions collection");

        var errors = output.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.IsEmpty(errors, GeneratorProbe.Describe(errors));
    }

    // 动画在计时器线程推进；按截止时间轮询比固定睡眠更稳。
    private static void WaitForTransition(VisualElement target)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (VisualTransition.HasActive(target) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }
    }
}
