using Microsoft.CodeAnalysis;
using Easel.Generator;
using Easel.Test.Infrastructure;

namespace Easel.Test;

[TestClass]
public sealed class XamlWindowGeneratorTests
{
    private const string Source = """
        using Easel.Visuals;

        namespace Probe;

        public partial class ProbeWindow : Window
        {
        }
        """;

    [TestMethod]
    public void AWindowContentPropertyCompiles()
    {
        const string xaml = """
            <Window
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                x:Class="Probe.ProbeWindow"
                Title="Probe"
                Width="320"
                Height="200">
              <Window.Content>
                <StackPanel>
                  <TextBlock Text="Hi" />
                </StackPanel>
              </Window.Content>
            </Window>
            """;

        var (run, output) = GeneratorProbe.RunAndCompile(
            [new XamlFileGenerator()],
            Source,
            "XamlWindowProbe",
            new InMemoryAdditionalText("ProbeWindow.xaml", xaml));

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));
        var errors = output.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.IsEmpty(errors, GeneratorProbe.Describe(errors));

        var generated = run.GeneratedText();
        StringAssert.Contains(generated, "this.Title = \"Probe\";");
        StringAssert.Contains(generated, "this.Width = 320;");
        StringAssert.Contains(generated, "this.Content =");
    }

    [TestMethod]
    public void ASecondWindowContentRootIsRefused()
    {
        const string xaml = """
            <Window
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                x:Class="Probe.ProbeWindow"
                Title="Probe">
              <StackPanel />
              <Border />
            </Window>
            """;

        var run = GeneratorProbe.Run(
            new XamlFileGenerator(),
            Source,
            "XamlWindowDuplicateContentProbe",
            new InMemoryAdditionalText("ProbeWindow.xaml", xaml));

        var diagnostic = AssertSingle(run, "EASEL_XAML004");
        StringAssert.Contains(diagnostic.GetMessage(), "one content root");
    }

    private static Diagnostic AssertSingle(GeneratorDriverRunResult result, string id)
    {
        var matches = result.Diagnostics.Where(diagnostic => diagnostic.Id == id).ToArray();
        Assert.AreEqual(1, matches.Length, GeneratorProbe.Describe(result.Diagnostics));
        return matches[0];
    }
}
