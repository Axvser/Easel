using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Easel.Generator;
using Easel.Test.Infrastructure;

namespace Easel.Test;

/// <summary>
/// The generators follow the project's C# language version: a 7.3 project gets output without nullable syntax,
/// and a C# 8+ project keeps the nullable-aware output.
/// </summary>
[TestClass]
public sealed class LegacyLanguageVersionTests
{
    private const string Source = """
        using Easel.Visuals;

        namespace Probe
        {
            public sealed class Model
            {
                public string Title { get; set; }
            }

            public partial class ProbeView : Easel.Visuals.Controls.StackPanel
            {
                public ProbeView()
                {
                    InitializeComponent();
                }
            }

            public partial class Card : Easel.Visuals.Controls.StackPanel
            {
                [Easel.XamlAttachedProperty(DefaultValue = "0")]
                static partial void SetGap(VisualElement element, int value);
            }
        }
        """;

    private const string Xaml = """
        <StackPanel
            xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:local="clr-namespace:Probe"
            x:Class="Probe.ProbeView"
            x:DataType="local:Model"
            x:CompileBindings="True">

          <TextBlock x:Name="title" Text="{Binding Title}" />

        </StackPanel>
        """;

    [TestMethod]
    public void CSharp73_Gets_No_Nullable_Syntax()
    {
        var (run, output) = Run(LanguageVersion.CSharp7_3);
        var text = run.GeneratedText();

        Assert.IsFalse(text.Contains("#nullable"), "the 7.3 output still contains #nullable");
        Assert.IsFalse(text.Contains("object?"), "the 7.3 output still contains object?");
        Assert.IsFalse(text.Contains("null!"), "the 7.3 output still contains null!");
        AssertNoCompileErrors(output);
    }

    [TestMethod]
    public void CSharp8_And_Later_Keeps_Nullable_Syntax()
    {
        var (run, output) = Run(LanguageVersion.Preview);
        var text = run.GeneratedText();

        StringAssert.Contains(text, "#nullable enable");
        StringAssert.Contains(text, "object?");
        StringAssert.Contains(text, "null!");
        AssertNoCompileErrors(output);
    }

    private static (Microsoft.CodeAnalysis.GeneratorDriverRunResult Run, Compilation Output) Run(
        LanguageVersion languageVersion)
        => GeneratorProbe.RunAndCompile(
            [new XamlFileGenerator(), new DependencyPropertyGenerator()],
            Source,
            "LegacyLanguageVersionProbe",
            new CSharpParseOptions(languageVersion),
            new InMemoryAdditionalText("ProbeView.xaml", Xaml));

    private static void AssertNoCompileErrors(Compilation output)
    {
        var errors = output.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.IsEmpty(errors, string.Join(" | ", errors.Select(static diagnostic => diagnostic.ToString())));
    }
}
