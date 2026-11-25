using Microsoft.CodeAnalysis;
using Easel.Generator;
using Easel.Test.Infrastructure;

namespace Easel.Test;

/// <summary>
/// The documented <c>EASEL_XAML*</c> diagnostics on a visual root. Every case is driven through the generator, because
/// the malformed files never reach a compiled output.
/// </summary>
[TestClass]
public sealed class XamlDiagnosticsTests
{
    private const string Source = """
        namespace Probe;

        public partial class ProbeView : Easel.Visuals.Controls.StackPanel
        {
        }
        """;

    private const string NonPartialSource = """
        namespace Probe;

        public class ProbeView : Easel.Visuals.Controls.StackPanel
        {
        }
        """;

    private const string OpenRoot = """
        <local:ProbeView
            xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:local="clr-namespace:Probe"
            x:Class="Probe.ProbeView">
        """;

    private const string CloseRoot = "</local:ProbeView>";

    [TestMethod]
    public void AnUnknownPrefixedElement_IsXaml001()
    {
        var diagnostic = AssertSingle(Wrap("<local:MissingControl />"), "EASEL_XAML001");
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        StringAssert.Contains(diagnostic.GetMessage(), "MissingControl");
    }

    [TestMethod]
    public void AnUnknownMember_IsXaml002()
    {
        var diagnostic = AssertSingle(Wrap("<Border Bogus=\"1\" />"), "EASEL_XAML002");
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        StringAssert.Contains(diagnostic.GetMessage(), "Bogus");
    }

    [TestMethod]
    public void AMissingXClass_IsXaml005()
    {
        const string xaml = """
            <local:ProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe">
              <Border />
            </local:ProbeView>
            """;

        var diagnostic = AssertSingle(xaml, "EASEL_XAML005");
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [TestMethod]
    public void AnXClassThatDoesNotExist_IsXaml006()
    {
        const string xaml = """
            <local:ProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.DoesNotExist">
              <Border />
            </local:ProbeView>
            """;

        var diagnostic = AssertSingle(xaml, "EASEL_XAML006");
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        StringAssert.Contains(diagnostic.GetMessage(), "Probe.DoesNotExist");
    }

    [TestMethod]
    public void ADuplicateXName_IsXaml007()
    {
        var diagnostic = AssertSingle(
            Wrap("<Border x:Name=\"duplicate\" /><Border x:Name=\"duplicate\" />"),
            "EASEL_XAML007");
        Assert.AreEqual(DiagnosticSeverity.Warning, diagnostic.Severity);
        StringAssert.Contains(diagnostic.GetMessage(), "duplicate");
    }

    [TestMethod]
    public void AMissingEventHandler_IsXaml008()
    {
        var diagnostic = AssertSingle(Wrap("<Button Click=\"DoesNotExist\" />"), "EASEL_XAML008");
        Assert.AreEqual(DiagnosticSeverity.Warning, diagnostic.Severity);
        StringAssert.Contains(diagnostic.GetMessage(), "DoesNotExist");
    }

    [TestMethod]
    public void ANonPartialXClass_IsXaml009()
    {
        var result = Run(Wrap("<Border />"), NonPartialSource);
        var diagnostic = AssertSingle(result, "EASEL_XAML009");
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.IsEmpty(result.GeneratedText());
    }

    [TestMethod]
    public void MalformedXml_IsXaml012()
    {
        var result = Run("<local:ProbeView", Source);
        var diagnostic = AssertSingle(result, "EASEL_XAML012");
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.IsEmpty(result.GeneratedText());
    }

    [TestMethod]
    public void AnExistingInitializeComponent_IsXaml015()
    {
        const string source = """
            namespace Probe;

            public partial class ProbeView : Easel.Visuals.Controls.StackPanel
            {
                private void InitializeComponent() { }
            }
            """;

        var result = Run(Wrap("<Border />"), source);
        var diagnostic = AssertSingle(result, "EASEL_XAML015");

        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        StringAssert.Contains(diagnostic.GetMessage(), "InitializeComponent");
        Assert.IsEmpty(result.GeneratedText());
    }

    [TestMethod]
    public void APartialInitializeComponentDeclaration_IsXaml015()
    {
        const string source = """
            namespace Probe;

            public partial class ProbeView : Easel.Visuals.Controls.StackPanel
            {
                private partial void InitializeComponent();
            }
            """;

        var result = Run(Wrap("<Border />"), source);
        AssertSingle(result, "EASEL_XAML015");
        Assert.IsEmpty(result.GeneratedText());
    }

    [TestMethod]
    public void ANativePrefixedType_GeneratesANativeElement()
    {
        const string xaml = """
            <local:ProbeView
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.ProbeView">
              <native:Button Text="OK" />
            </local:ProbeView>
            """;

        var result = Run(xaml, Source);
        Assert.IsEmpty(result.Diagnostics, GeneratorProbe.Describe(result.Diagnostics));
        StringAssert.Contains(result.GeneratedText(), "NativeElement<global::System.Windows.Forms.Button>");
    }

    private static string Wrap(string body) => OpenRoot + "\n" + body + "\n" + CloseRoot;

    private static Diagnostic AssertSingle(string xaml, string id) => AssertSingle(Run(xaml, Source), id);

    private static Diagnostic AssertSingle(GeneratorDriverRunResult result, string id)
    {
        var matches = result.Diagnostics
            .Where(diagnostic => diagnostic.Id == id)
            .ToArray();

        Assert.HasCount(1, matches, GeneratorProbe.Describe(result.Diagnostics));
        return matches[0];
    }

    private static GeneratorDriverRunResult Run(string xaml, string source)
        => GeneratorProbe.Run(
            new XamlFileGenerator(),
            source,
            "XamlDiagnosticsProbe",
            new InMemoryAdditionalText("ProbeView.xaml", xaml));
}
