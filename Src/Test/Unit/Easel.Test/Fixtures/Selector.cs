namespace Easel.Test.Fixtures;

/// <summary>
/// A named resource that carries a <see cref="Easel.Visuals.DataTemplate"/>. The generator assigns
/// the template from a <c>{StaticResource}</c> attribute, so the fixture proves a selector-style object resource can
/// be declared beside the template it refers to.
/// </summary>
public sealed class Selector
{
    /// <summary>The template this selector was configured with.</summary>
    public Easel.Visuals.DataTemplate? Template { get; set; }
}
