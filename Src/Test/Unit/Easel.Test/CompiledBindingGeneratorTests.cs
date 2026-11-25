using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Easel.Generator;
using Easel.Test.Infrastructure;

namespace Easel.Test;

/// <summary>
/// The XAML compiler's <c>x:DataType</c> / <c>x:CompileBindings</c> path: generated readers, compile-time
/// validation and the reflection fallback.
/// </summary>
[TestClass]
public sealed class CompiledBindingGeneratorTests
{
    private const string Source = """
        using System.Collections;
        using System.Collections.Generic;
        using System.ComponentModel;
        using Easel;

        namespace Probe;

        public sealed class Item : INotifyPropertyChanged
        {
            private string _name = "item";

            public event PropertyChangedEventHandler? PropertyChanged;

            public string Name
            {
                get => _name;
                set
                {
                    _name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }
        }

        public sealed class ClosedSetting
        {
            public string Name { get; set; } = "closed";
        }

        public partial class Setting : INotifyPropertyChanged
        {
            private string _name = "setting";

            public event PropertyChangedEventHandler? PropertyChanged;

            public string Name
            {
                get => _name;
                set
                {
                    _name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }

            public string ReadOnly => _name;

            public Item Child { get; } = new Item();

            public List<Item> Items { get; } = [new Item()];

            public ArrayList Legacy { get; } = new() { new Item() };
        }

        public partial class ProbeView : Easel.Visuals.Controls.StackPanel
        {
        }
        """;

    [TestMethod]
    public void CompiledBindings_EmitTypedReadersAndCompile()
    {
        var (run, output) = Run("""
            <local:ProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.ProbeView"
                x:DataType="local:Setting"
                x:CompileBindings="True">

              <TextBlock Text="{Binding Name}" />
              <TextBox Text="{Binding Name, Mode=TwoWay}" />
              <TextBlock Text="{Binding Child.Name}" />

            </local:ProbeView>
            """);

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));
        AssertNoCompileErrors(output);

        var generated = run.GeneratedText();
        StringAssert.Contains(generated, "new global::Easel.CompiledBinding(\"Name\", __EaselCompiledBinding");
        StringAssert.Contains(generated, "new global::Easel.CompiledBinding(\"Child.Name\", __EaselCompiledBinding");
        StringAssert.Contains(generated, "global::Easel.CompiledBinding.CreateWriter(");
        StringAssert.Contains(generated, "(__o, __v) => __o.Name = global::Easel.CompiledBinding.Coerce(__o.Name, __v)");
        StringAssert.Contains(generated, "global::Easel.CompiledBinding.HasValue(__n1)");
        Assert.IsFalse(generated.Contains("new global::Easel.Binding(\"Name\")", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MissingDataType_ReportsXaml020AndFallsBack()
    {
        var (run, output) = Run("""
            <local:ProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.ProbeView"
                x:CompileBindings="True">

              <TextBlock Text="{Binding Name}" />

            </local:ProbeView>
            """);

        AssertSingle(run, "EASEL_XAML020", DiagnosticSeverity.Error);
        AssertNoCompileErrors(output);
        StringAssert.Contains(run.GeneratedText(), "new global::Easel.Binding(\"Name\")");
    }

    [TestMethod]
    public void UnknownMember_ReportsXaml021AndFallsBack()
    {
        var (run, output) = Run("""
            <local:ProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.ProbeView"
                x:DataType="local:ClosedSetting"
                x:CompileBindings="True">

              <TextBlock Text="{Binding Missing}" />

            </local:ProbeView>
            """);

        var diagnostic = AssertSingle(run, "EASEL_XAML021", DiagnosticSeverity.Error);
        StringAssert.Contains(diagnostic.GetMessage(), "Missing");
        AssertNoCompileErrors(output);
        StringAssert.Contains(run.GeneratedText(), "new global::Easel.Binding(\"Missing\")");
    }

    [TestMethod]
    public void ReadOnlyLeaf_ReportsXaml022ForTwoWay()
    {
        var (run, output) = Run("""
            <local:ProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.ProbeView"
                x:DataType="local:Setting"
                x:CompileBindings="True">

              <TextBox Text="{Binding ReadOnly, Mode=TwoWay}" />

            </local:ProbeView>
            """);

        AssertSingle(run, "EASEL_XAML022", DiagnosticSeverity.Error);
        AssertNoCompileErrors(output);
        StringAssert.Contains(run.GeneratedText(), "new global::Easel.Binding(\"ReadOnly\")");
    }

    [TestMethod]
    public void CompileBindingsFalse_KeepsTheRuntimeBinding()
    {
        var (run, output) = Run("""
            <local:ProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.ProbeView"
                x:DataType="local:Setting"
                x:CompileBindings="True">

              <StackPanel x:CompileBindings="False">
                <TextBlock Text="{Binding Name}" />
              </StackPanel>

            </local:ProbeView>
            """);

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));
        AssertNoCompileErrors(output);
        StringAssert.Contains(run.GeneratedText(), "new global::Easel.Binding(\"Name\")");
    }

    [TestMethod]
    public void DataTemplateDataType_OverridesTheOuterScope()
    {
        var (run, output) = Run("""
            <local:ProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.ProbeView"
                x:DataType="local:Setting"
                x:CompileBindings="True">

              <local:ProbeView.Resources>
                <DataTemplate x:Key="ItemTemplate" x:DataType="local:Item">
                  <TextBlock Text="{Binding Name}" />
                </DataTemplate>
              </local:ProbeView.Resources>

              <TextBlock Text="{Binding Name}" />

            </local:ProbeView>
            """);

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));
        AssertNoCompileErrors(output);

        var generated = run.GeneratedText();
        StringAssert.Contains(generated, "__source is global::Probe.Item");
        StringAssert.Contains(generated, "__source is global::Probe.Setting");
    }

    [TestMethod]
    public void SourceGeneratedMember_IsDeferredToTheCSharpCompiler()
    {
        var (run, output) = GeneratorProbe.RunAndCompile(
            [new XamlFileGenerator(), new MemberAddingProbeGenerator()],
            Source,
            "CompiledBindingGeneratedMemberProbe",
            new InMemoryAdditionalText("ProbeView.xaml", """
                <local:ProbeView
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:local="clr-namespace:Probe"
                    x:Class="Probe.ProbeView"
                    x:DataType="local:Setting"
                    x:CompileBindings="True">

                  <TextBlock Text="{Binding Generated}" />

                </local:ProbeView>
                """));

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));
        AssertNoCompileErrors(output);
        StringAssert.Contains(run.GeneratedText(), "__n0.Generated");
    }

    [TestMethod]
    public void Indexer_CompilesThroughTheRuntimeHelpers()
    {
        var (run, output) = Run("""
            <local:ProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.ProbeView"
                x:DataType="local:Setting"
                x:CompileBindings="True">

              <TextBlock Text="{Binding Items[0].Name}" />
              <TextBox Text="{Binding Items[0].Name, Mode=TwoWay}" />
              <TextBlock Text="{Binding Items[0], Mode=TwoWay}" />

            </local:ProbeView>
            """);

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));
        AssertNoCompileErrors(output);

        var generated = run.GeneratedText();
        StringAssert.Contains(generated, "global::Easel.CompiledBinding.TryGetIndex(__n1, 0, out var __n2)");
        StringAssert.Contains(generated, "global::Easel.CompiledBinding.CreateIndexWriter(__n1, 0)");
    }

    [TestMethod]
    public void NonGenericIndexerBeforeTheLeaf_ReportsXaml024AndFallsBack()
    {
        var (run, output) = Run("""
            <local:ProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.ProbeView"
                x:DataType="local:Setting"
                x:CompileBindings="True">

              <TextBlock Text="{Binding Legacy[0].Name}" />

            </local:ProbeView>
            """);

        AssertSingle(run, "EASEL_XAML024", DiagnosticSeverity.Warning);
        AssertNoCompileErrors(output);
        StringAssert.Contains(run.GeneratedText(), "new global::Easel.Binding(\"Legacy[0].Name\")");
    }

    [TestMethod]
    public void InvalidCompileBindingsValue_ReportsXaml023()
    {
        var (run, output) = Run("""
            <local:ProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.ProbeView"
                x:CompileBindings="Maybe">

              <TextBlock Text="{Binding Name}" />

            </local:ProbeView>
            """);

        AssertSingle(run, "EASEL_XAML023", DiagnosticSeverity.Error);
        AssertNoCompileErrors(output);
        StringAssert.Contains(run.GeneratedText(), "new global::Easel.Binding(\"Name\")");
    }

    private static (Microsoft.CodeAnalysis.GeneratorDriverRunResult Run, Compilation Output) Run(string xaml)
        => GeneratorProbe.RunAndCompile(
            [new XamlFileGenerator()],
            Source,
            "CompiledBindingProbe",
            new InMemoryAdditionalText("ProbeView.xaml", xaml));

    private static Diagnostic AssertSingle(
        Microsoft.CodeAnalysis.GeneratorDriverRunResult result,
        string id,
        DiagnosticSeverity severity)
    {
        var matches = result.Diagnostics.Where(diagnostic => diagnostic.Id == id).ToArray();
        Assert.HasCount(1, matches, GeneratorProbe.Describe(result.Diagnostics));
        Assert.AreEqual(severity, matches[0].Severity);
        return matches[0];
    }

    private static void AssertNoCompileErrors(Compilation output)
    {
        var errors = output.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.IsEmpty(errors, GeneratorProbe.Describe(errors));
    }

    /// <summary>Adds a member to <c>Probe.Setting</c> from a separate generator, like a user MVVM source generator.</summary>
    private sealed class MemberAddingProbeGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static production => production.AddSource(
                "MemberAddingProbe.g.cs",
                SourceText.From(
                    """
                    namespace Probe;

                    public partial class Setting
                    {
                        public string Generated => "generated";
                    }
                    """,
                    Encoding.UTF8)));
        }
    }
}
