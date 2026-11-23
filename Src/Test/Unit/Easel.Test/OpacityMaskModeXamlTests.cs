using Microsoft.CodeAnalysis;
using Easel.Generator;
using Easel.Test.Infrastructure;

namespace Easel.Test;

/// <summary>The luminance mask mode is declarable in visual XAML.</summary>
[STATestClass]
public sealed class OpacityMaskModeXamlTests
{
    private const string Source = """
        namespace Probe;

        public partial class MaskView : Easel.Visuals.Controls.Border
        {
        }
        """;

    [TestMethod]
    public void LuminanceMode_ParsesInVisualXaml()
    {
        const string xaml = """
            <local:MaskView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.MaskView">

              <Border x:Name="card" OpacityMaskMode="Luminance" />

            </local:MaskView>
            """;

        var (run, output) = GeneratorProbe.RunAndCompile(
            [new XamlFileGenerator()],
            Source,
            "OpacityMaskModeProbe",
            new InMemoryAdditionalText("MaskView.xaml", xaml));

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));
        Assert.IsEmpty(
            output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            GeneratorProbe.Describe(output.GetDiagnostics()));

        StringAssert.Contains(
            run.GeneratedText(),
            "card.OpacityMaskMode = global::Easel.OpacityMaskMode.Luminance;");
    }
}
