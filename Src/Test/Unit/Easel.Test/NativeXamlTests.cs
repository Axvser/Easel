using Microsoft.CodeAnalysis;
using Easel.Generator;
using Easel.Test.Infrastructure;

namespace Easel.Test;

/// <summary>
/// Drives the XAML compiler over <c>native:</c> elements: the wrapper is generated (the author never writes a host),
/// layout members land on the wrapper, native members and events land on the control, and visual content nests.
/// </summary>
[TestClass]
public sealed class NativeXamlTests
{
    private const string Source = """
        using System;
        using Easel;

        namespace Probe;

        public partial class NativeProbeView : Easel.Visuals.Controls.StackPanel
        {
            public int Clicks { get; private set; }

            private void OnNativeClick(object? sender, EventArgs e) => Clicks++;
        }
        """;

    [TestMethod]
    public void NativeXaml_CompilesAndRoutesNativeAndLayoutMembers()
    {
        const string xaml = """
            <local:NativeProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.NativeProbeView"
                Orientation="Vertical">

              <native:Button
                  x:Name="okButton"
                  Text="OK"
                  Margin="4"
                  Width="120"
                  HorizontalAlignment="Left"
                  Grid.Row="1"
                  Click="OnNativeClick" />

              <native:GroupBox x:Name="group" Text="Host" Width="200" Height="100">
                <StackPanel>
                  <TextBlock Text="inside" />
                </StackPanel>
              </native:GroupBox>

            </local:NativeProbeView>
            """;

        var (run, output) = GeneratorProbe.RunAndCompile(
            [new XamlFileGenerator()],
            Source,
            "NativeXamlProbe",
            new InMemoryAdditionalText("NativeProbeView.xaml", xaml));

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));
        Assert.IsEmpty(
            output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            GeneratorProbe.Describe(output.GetDiagnostics()));

        var generated = run.GeneratedText();
        StringAssert.Contains(generated, "NativeElement<global::System.Windows.Forms.Button>");
        StringAssert.Contains(generated, ".Native.Text = \"OK\";");
        StringAssert.Contains(generated, ".Native.Click += this.OnNativeClick;");
        StringAssert.Contains(generated, "NativeElement<global::System.Windows.Forms.GroupBox>");
        StringAssert.Contains(generated, ".Content = ");
    }

    [TestMethod]
    public void ANativeElementRejectsASecondVisualRoot()
    {
        const string xaml = """
            <local:NativeProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.NativeProbeView">
              <native:Panel>
                <TextBlock Text="one" />
                <TextBlock Text="two" />
              </native:Panel>
            </local:NativeProbeView>
            """;

        var run = GeneratorProbe.Run(
            new XamlFileGenerator(),
            Source,
            "NativeTwoRoots",
            new InMemoryAdditionalText("NativeProbeView.xaml", xaml));

        var diagnostic = AssertSingle(run, "EASEL_XAML004");
        StringAssert.Contains(diagnostic.GetMessage(), "single visual content root");
    }

    [TestMethod]
    public void ANativeElementWrapsNativeChildrenInAnImplicitPanel()
    {
        const string xaml = """
            <local:NativeProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.NativeProbeView">
              <native:Panel>
                <native:Button Text="one" />
                <native:Label Text="two" />
              </native:Panel>
            </local:NativeProbeView>
            """;

        var (run, output) = GeneratorProbe.RunAndCompile(
            [new XamlFileGenerator()],
            Source,
            "NativeChildOfNative",
            new InMemoryAdditionalText("NativeProbeView.xaml", xaml));

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));
        Assert.IsEmpty(
            output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            GeneratorProbe.Describe(output.GetDiagnostics()));

        var generated = run.GeneratedText();
        StringAssert.Contains(generated, "new global::Easel.Visuals.Controls.StackPanel()");
        StringAssert.Contains(generated, ".Content = ");
    }

    [TestMethod]
    public void ANativeClrPropertyBinding_IsGeneratedThroughAProxyProperty()
    {
        const string xaml = """
            <local:NativeProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.NativeProbeView">
              <native:Button Text="{Binding Caption}" />
            </local:NativeProbeView>
            """;

        var (run, output) = GeneratorProbe.RunAndCompile(
            [new XamlFileGenerator()],
            Source,
            "NativeBindingProbe",
            new InMemoryAdditionalText("NativeProbeView.xaml", xaml));

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));
        Assert.IsEmpty(
            output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            GeneratorProbe.Describe(output.GetDiagnostics()));

        var generated = run.GeneratedText();
        StringAssert.Contains(
            generated,
            "NativePropertyBinding.For(typeof(global::System.Windows.Forms.Button), \"Text\")");
        StringAssert.Contains(generated, "VisualBindingOperations.Bind");
        Assert.IsFalse(generated.Contains("TrackTwoWay"), "a one-way binding must not track a native change event");
    }

    [TestMethod]
    public void ATwoWayNativeBinding_TracksTheConventionalChangedEvent()
    {
        const string xaml = """
            <local:NativeProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.NativeProbeView">
              <native:Button Text="{Binding Caption, Mode=TwoWay}" />
            </local:NativeProbeView>
            """;

        var (run, output) = GeneratorProbe.RunAndCompile(
            [new XamlFileGenerator()],
            Source,
            "NativeTwoWayProbe",
            new InMemoryAdditionalText("NativeProbeView.xaml", xaml));

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));
        Assert.IsEmpty(
            output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            GeneratorProbe.Describe(output.GetDiagnostics()));

        StringAssert.Contains(run.GeneratedText(), "NativePropertyBinding.TrackTwoWay");
    }

    [TestMethod]
    public void ALostFocusNativeBinding_CompilesWithTheStandardTrigger()
    {
        const string xaml = """
            <local:NativeProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.NativeProbeView">
              <native:Button Text="{Binding Caption, Mode=TwoWay, UpdateSourceTrigger=LostFocus}" />
            </local:NativeProbeView>
            """;

        var (run, output) = GeneratorProbe.RunAndCompile(
            [new XamlFileGenerator()],
            Source,
            "NativeLostFocusProbe",
            new InMemoryAdditionalText("NativeProbeView.xaml", xaml));

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));
        Assert.IsEmpty(
            output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            GeneratorProbe.Describe(output.GetDiagnostics()));

        StringAssert.Contains(run.GeneratedText(), "UpdateSourceTrigger.LostFocus");
        StringAssert.Contains(run.GeneratedText(), "NativePropertyBinding.TrackTwoWay");
    }

    [TestMethod]
    public void ALocalControlDerivedFromWinForms_IsWrappedAsANativeElement()
    {
        const string source = """
            namespace Probe;

            public class ProbeNativeLabel : System.Windows.Forms.Label
            {
            }

            public partial class NativeView : Easel.Visuals.Controls.StackPanel
            {
            }
            """;

        const string xaml = """
            <local:NativeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.NativeView">
              <local:ProbeNativeLabel Text="custom" />
            </local:NativeView>
            """;

        var (run, output) = GeneratorProbe.RunAndCompile(
            [new XamlFileGenerator()],
            source,
            "NativeCustomControlProbe",
            new InMemoryAdditionalText("NativeView.xaml", xaml));

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));
        Assert.IsEmpty(
            output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            GeneratorProbe.Describe(output.GetDiagnostics()));
        StringAssert.Contains(run.GeneratedText(), "NativeElement<global::Probe.ProbeNativeLabel>");
    }

    [TestMethod]
    public void ANativeElementInsideADataTemplateCompiles()
    {
        const string xaml = """
            <local:NativeProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.NativeProbeView">
              <ItemsControl>
                <ItemsControl.ItemTemplate>
                  <DataTemplate>
                    <native:GroupBox Text="Item">
                      <TextBlock Text="{Binding}" />
                    </native:GroupBox>
                  </DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
            </local:NativeProbeView>
            """;

        var (run, output) = GeneratorProbe.RunAndCompile(
            [new XamlFileGenerator()],
            Source,
            "NativeTemplateProbe",
            new InMemoryAdditionalText("NativeProbeView.xaml", xaml));

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));
        Assert.IsEmpty(
            output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            GeneratorProbe.Describe(output.GetDiagnostics()));
        StringAssert.Contains(run.GeneratedText(), "NativeElement<global::System.Windows.Forms.GroupBox>");
    }

    [TestMethod]
    public void AVisualPropertyOnANativeElement_IsRejectedWithTheNativeAlternative()
    {
        const string xaml = """
            <local:NativeProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.NativeProbeView">
              <native:Button Text="OK" Background="Red" />
            </local:NativeProbeView>
            """;

        var run = GeneratorProbe.Run(
            new XamlFileGenerator(),
            Source,
            "NativeVisualPropertyProbe",
            new InMemoryAdditionalText("NativeProbeView.xaml", xaml));

        var diagnostic = AssertSingle(run, "EASEL_XAML004");
        StringAssert.Contains(diagnostic.GetMessage(), "visual property");
    }

    [TestMethod]
    public void DockOnANativeElement_IsRejectedBecauseTheVisualLayoutOwnsPlacement()
    {
        const string xaml = """
            <local:NativeProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.NativeProbeView">
              <native:Button Text="OK" Dock="Fill" />
            </local:NativeProbeView>
            """;

        var run = GeneratorProbe.Run(
            new XamlFileGenerator(),
            Source,
            "NativeDockProbe",
            new InMemoryAdditionalText("NativeProbeView.xaml", xaml));

        var diagnostic = AssertSingle(run, "EASEL_XAML004");
        StringAssert.Contains(diagnostic.GetMessage(), "owned by the visual layout");
    }

    private static Diagnostic AssertSingle(GeneratorDriverRunResult result, string id)
    {
        var matches = result.Diagnostics.Where(diagnostic => diagnostic.Id == id).ToArray();
        Assert.HasCount(1, matches, GeneratorProbe.Describe(result.Diagnostics));
        return matches[0];
    }
}
