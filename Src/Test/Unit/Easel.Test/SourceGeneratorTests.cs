using Microsoft.CodeAnalysis;
using Easel.Generator;
using Easel.Test.Infrastructure;

namespace Easel.Test;

/// <summary>
/// Drives <see cref="DependencyPropertyGenerator"/> directly, so the emitted members and the refusal diagnostics can
/// be asserted without a compiler round trip. Properties are declared on visual elements, which is what the generator
/// requires now.
/// </summary>
[TestClass]
public sealed class SourceGeneratorTests
{
    [TestMethod]
    public void XamlProperty_EmitsAFieldAndAClrWrapper()
    {
        var result = GeneratorProbe.Run(new DependencyPropertyGenerator(), """
            using Easel;

            namespace Probe;

            public partial class Card : Easel.Visuals.Controls.Border
            {
                [XamlProperty]
                public partial string Title { get; set; }
            }
            """, "XamlPropertyProbe");

        Assert.IsEmpty(result.Diagnostics, GeneratorProbe.Describe(result.Diagnostics));

        var generated = result.GeneratedText();
        StringAssert.Contains(generated, "public static readonly global::Easel.DependencyProperty TitleProperty =");
        StringAssert.Contains(generated, "global::Easel.DependencyProperty.Register(");
        StringAssert.Contains(generated, "\"Title\"");
        StringAssert.Contains(generated, "typeof(string)");
        StringAssert.Contains(generated, "typeof(global::Probe.Card)");
        StringAssert.Contains(generated, "public partial global::System.String Title");
        StringAssert.Contains(generated, "get => (global::System.String)GetValue(TitleProperty)!;");
        StringAssert.Contains(generated, "set => SetValue(TitleProperty, value);");
    }

    [TestMethod]
    public void XamlProperty_ANonNullableReferenceWithoutADefault_IsRefused()
    {
        var result = GeneratorProbe.Run(new DependencyPropertyGenerator(), """
            #nullable enable
            using Easel;

            namespace Probe;

            public partial class Card : Easel.Visuals.Controls.Border
            {
                [XamlProperty]
                public partial string Title { get; set; }
            }
            """, "XamlPropertyNullDefaultProbe");

        var diagnostic = AssertSingle(result, "EASEL_XAMLP005");
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        StringAssert.Contains(diagnostic.GetMessage(), "Title");
    }

    [TestMethod]
    public void XamlProperty_ANullableReferenceWithoutADefault_IsAccepted()
    {
        var result = GeneratorProbe.Run(new DependencyPropertyGenerator(), """
            #nullable enable
            using Easel;

            namespace Probe;

            public partial class Card : Easel.Visuals.Controls.Border
            {
                [XamlProperty]
                public partial string? Title { get; set; }
            }
            """, "XamlPropertyNullableProbe");

        Assert.IsEmpty(result.Diagnostics, GeneratorProbe.Describe(result.Diagnostics));
    }

    [TestMethod]
    public void XamlProperty_ExplicitNameAndDefaultValue_ReachTheField()
    {
        var result = GeneratorProbe.Run(new DependencyPropertyGenerator(), """
            using Easel;

            namespace Probe;

            public partial class Card : Easel.Visuals.Controls.Border
            {
                [XamlProperty("Caption", DefaultValue = "hello")]
                public partial string Text { get; set; }
            }
            """, "XamlPropertyDefaultProbe");

        Assert.IsEmpty(result.Diagnostics, GeneratorProbe.Describe(result.Diagnostics));

        var generated = result.GeneratedText();
        StringAssert.Contains(generated, "DependencyProperty CaptionProperty =");
        StringAssert.Contains(generated, "new global::Easel.PropertyMetadata(\"hello\")");
        StringAssert.Contains(generated, "public partial global::System.String Text");
    }

    [TestMethod]
    public void XamlAttachedProperty_EmitsRegisterAttachedAndBothAccessors()
    {
        var result = GeneratorProbe.Run(new DependencyPropertyGenerator(), """
            using Easel;

            namespace Probe;

            public static partial class Attachments
            {
                [XamlAttachedProperty]
                public static partial string? GetNote(Easel.Visuals.VisualElement target);

                [XamlAttachedProperty]
                public static partial void SetNote(Easel.Visuals.VisualElement target, string? value);
            }
            """, "XamlAttachedProbe");

        Assert.IsEmpty(result.Diagnostics, GeneratorProbe.Describe(result.Diagnostics));

        var generated = result.GeneratedText();
        StringAssert.Contains(generated, "public static readonly global::Easel.DependencyProperty NoteProperty =");
        StringAssert.Contains(generated, "global::Easel.DependencyProperty.RegisterAttached(");
        StringAssert.Contains(generated, "\"Note\"");
        StringAssert.Contains(generated, "typeof(string)");
        StringAssert.Contains(generated, "typeof(global::Probe.Attachments)");
        StringAssert.Contains(
            generated,
            "public static partial global::System.String? GetNote(global::Easel.Visuals.VisualElement target)");
        StringAssert.Contains(generated, "=> (global::System.String?)NoteProperty.GetValue(target)!;");
        StringAssert.Contains(
            generated,
            "public static partial void SetNote(global::Easel.Visuals.VisualElement target, global::System.String? value)");
        StringAssert.Contains(generated, "=> NoteProperty.SetValue(target, value);");
    }

    [TestMethod]
    public void ANonPartialProperty_IsRefusedWithXamlp001()
    {
        var result = GeneratorProbe.Run(new DependencyPropertyGenerator(), """
            using Easel;

            namespace Probe;

            public partial class Card : Easel.Visuals.Controls.Border
            {
                [XamlProperty]
                public string Title { get; set; } = string.Empty;
            }
            """, "XamlPropertyNotPartialProbe");

        var diagnostic = AssertSingle(result, "EASEL_XAMLP001");
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.IsEmpty(result.GeneratedText());
    }

    [TestMethod]
    public void APropertyOnANonVisualType_IsRefusedWithXamlp002()
    {
        var result = GeneratorProbe.Run(new DependencyPropertyGenerator(), """
            using Easel;

            namespace Probe;

            public partial class Plain
            {
                [XamlProperty]
                public partial string Title { get; set; }
            }
            """, "XamlPropertyOwnerProbe");

        var diagnostic = AssertSingle(result, "EASEL_XAMLP002");
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [TestMethod]
    public void ANonPartialAttachedAccessor_IsRefusedWithXamlp003()
    {
        var result = GeneratorProbe.Run(new DependencyPropertyGenerator(), """
            using Easel;

            namespace Probe;

            public static partial class Attachments
            {
                [XamlAttachedProperty]
                public static string GetNote(Easel.Visuals.VisualElement target) => string.Empty;
            }
            """, "XamlAttachedInvalidProbe");

        var diagnostic = AssertSingle(result, "EASEL_XAMLP003");
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [TestMethod]
    public void ABackingFieldAlreadyInTheSource_IsRefusedWithXamlp004()
    {
        var result = GeneratorProbe.Run(new DependencyPropertyGenerator(), """
            using Easel;

            namespace Probe;

            public partial class Card : Easel.Visuals.Controls.Border
            {
                public static readonly DependencyProperty TitleProperty = null!;

                [XamlProperty]
                public partial string Title { get; set; }
            }
            """, "XamlPropertyConflictProbe");

        var diagnostic = AssertSingle(result, "EASEL_XAMLP004");
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        StringAssert.Contains(diagnostic.GetMessage(), "TitleProperty");
    }

    [TestMethod]
    public void XamlAttachedProperty_EmitsTheVisualChangedCallback()
    {
        var (run, output) = GeneratorProbe.RunAndCompile(
            [new DependencyPropertyGenerator()],
            """
            using Easel;

            namespace Probe;

            public static partial class Attachments
            {
                [XamlAttachedProperty(ChangedCallback = nameof(OnNoteChanged))]
                public static partial string? GetNote(Easel.Visuals.VisualElement target);

                [XamlAttachedProperty]
                public static partial void SetNote(Easel.Visuals.VisualElement target, string? value);

                private static void OnNoteChanged(Easel.IWinformsObject target, Easel.DependencyPropertyChangedEventArgs e)
                {
                }
            }
            """,
            "XamlAttachedCallbackProbe");

        Assert.IsEmpty(run.Diagnostics, GeneratorProbe.Describe(run.Diagnostics));
        var errors = output.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.IsEmpty(errors, GeneratorProbe.Describe(errors));

        StringAssert.Contains(
            run.GeneratedText(),
            "VisualPropertyChangedCallback = global::Probe.Attachments.OnNoteChanged");
    }

    [TestMethod]
    public void XamlProperty_EmitsTheVisualChangedCallback()
    {
        // 这段不编译：生成器发射的是 C# 13 的 partial 属性实现，测试用的内存编译不承载这套语法；
        // 附加属性那一路（方法）已经把「生成的引用真的能编译」验过了。
        var result = GeneratorProbe.Run(new DependencyPropertyGenerator(), """
            using Easel;

            namespace Probe;

            public partial class Card : Easel.Visuals.Controls.Border
            {
                [XamlProperty(ChangedCallback = nameof(OnTitleChanged))]
                public partial string? Title { get; set; }

                private static void OnTitleChanged(Easel.IWinformsObject target, Easel.DependencyPropertyChangedEventArgs e)
                {
                }
            }
            """, "XamlPropertyCallbackProbe");

        Assert.IsEmpty(result.Diagnostics, GeneratorProbe.Describe(result.Diagnostics));

        StringAssert.Contains(
            result.GeneratedText(),
            "VisualPropertyChangedCallback = global::Probe.Card.OnTitleChanged");
    }

    [TestMethod]
    public void AChangedCallbackWithTheWrongSignature_IsRefusedWithXamlp006()
    {
        var result = GeneratorProbe.Run(new DependencyPropertyGenerator(), """
            using Easel;

            namespace Probe;

            public static partial class Attachments
            {
                [XamlAttachedProperty(ChangedCallback = nameof(OnNoteChanged))]
                public static partial string? GetNote(Easel.Visuals.VisualElement target);

                // 少一个参数：不是 VisualPropertyChangedCallback 的形状。
                private static void OnNoteChanged(Easel.IWinformsObject target)
                {
                }
            }
            """, "XamlAttachedBadCallbackProbe");

        var diagnostic = AssertSingle(result, "EASEL_XAMLP006");
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    private static Diagnostic AssertSingle(GeneratorDriverRunResult result, string id)
    {
        var matches = result.Diagnostics.Where(diagnostic => diagnostic.Id == id).ToArray();
        Assert.HasCount(1, matches, GeneratorProbe.Describe(result.Diagnostics));
        return matches[0];
    }
}
