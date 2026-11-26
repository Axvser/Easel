using Easel.Animation;

namespace Easel.Visuals;

/// <summary>
/// Animates every change of one dependency property of a <see cref="VisualElement"/> from its old value to its new
/// value.
/// </summary>
/// <remarks>
/// Add an entry to <see cref="VisualElement.Transitions"/> to animate that property without a trigger. While the
/// animation runs it writes at <see cref="BindingPriority.Animation"/>; when it completes the animation layer is
/// cleared, so the layer the change was written at (a trigger, style setter or local value) shows through again.
/// </remarks>
public sealed class Transition
{
    /// <summary>The dependency property whose changes animate, or <see langword="null"/> while unconfigured.</summary>
    public DependencyProperty? Property { get; set; }

    /// <summary>The duration of each animation; 240 milliseconds by default.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(240);

    /// <summary>The easing curve; <see langword="null"/> means <c>Cubic.Out</c>.</summary>
    public IEase? Ease { get; set; }
}
