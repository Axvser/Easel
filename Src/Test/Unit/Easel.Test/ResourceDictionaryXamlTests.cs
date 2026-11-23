using Easel.Generator;
using Easel.Test.Infrastructure;

namespace Easel.Test;

/// <summary>WPF-style standalone ResourceDictionary XAML roots.</summary>
[TestClass]
public sealed class ResourceDictionaryXamlTests
{
    /// <summary>A dictionary root emits keys, scalar metrics, styles and merged dictionaries into itself.</summary>
    [TestMethod]
    public void ResourceDictionaryRoot_EmitsKeysStylesAndMergedDictionaries()
    {
        const string xaml = """
            <ResourceDictionary
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:local="clr-namespace:Probe"
                x:Class="Probe.ThemeDictionary">
              <SolidColorBrush x:Key="Accent" Color="#FF112233" />
              <x:Double x:Key="CornerRadius">4</x:Double>
              <x:FontWeight x:Key="BodyWeight">Bold</x:FontWeight>
              <x:Thickness x:Key="Border">1</x:Thickness>
              <x:PointF x:Key="ShadowOffset">4,6</x:PointF>
              <Style x:Key="Heading" TargetType="TextBlock">
                <Style.Setters>
                  <Setter Property="FontSize" Value="19" />
                  <Setter Property="Foreground" Value="{StaticResource Accent}" />
                </Style.Setters>
              </Style>
              <ResourceDictionary.MergedDictionaries>
                <local:BaseDictionary />
              </ResourceDictionary.MergedDictionaries>
            </ResourceDictionary>
            """;

        const string source = """
            using Easel;

            namespace Probe;

            public partial class ThemeDictionary : ResourceDictionary
            {
            }

            public partial class BaseDictionary : ResourceDictionary
            {
            }
            """;

        var result = GeneratorProbe.Run(
            new XamlFileGenerator(),
            source,
            "ProbeTheme",
            new InMemoryAdditionalText("ThemeDictionary.xaml", xaml));

        Assert.IsEmpty(
            result.Diagnostics,
            GeneratorProbe.Describe(result.Diagnostics));

        var generated = result.GeneratedText();
        StringAssert.Contains(generated, "this[\"Accent\"]");
        StringAssert.Contains(generated, "this[\"CornerRadius\"]");
        StringAssert.Contains(generated, "Easel.Visuals.FontWeight.Bold");
        StringAssert.Contains(generated, "ParseThickness");
        StringAssert.Contains(generated, "ParsePointF");
        StringAssert.Contains(generated, "this[\"Heading\"]");
        StringAssert.Contains(generated, "MergedDictionaries.Add");
        StringAssert.Contains(generated, "GetFromDictionary<");
    }
}
