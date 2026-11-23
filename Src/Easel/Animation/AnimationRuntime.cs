using Easel.Visuals;

namespace Easel.Animation;

/// <summary>The timing and easing of one property animation.</summary>
public sealed class AnimationSpec
{
    /// <summary>The duration of the animation; 240 milliseconds by default.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(240);

    /// <summary>The easing curve; <c>Cubic.Out</c> by default.</summary>
    public IEase Ease { get; set; } = Eases.Cubic.Out;

    /// <summary>The frame rate the animator should target; 60 by default.</summary>
    public int FPS { get; set; } = 60;

    /// <summary>Additional full passes; zero runs once and <see cref="int.MaxValue"/> repeats forever.</summary>
    public int LoopTime { get; set; }

    /// <summary>Whether each pass also runs backwards.</summary>
    public bool IsAutoReverse { get; set; }
}

/// <summary>
/// The animation backend Easel delegates property animation to. Implement it in a host package to bridge a specific
/// transition framework; assign it to <see cref="AnimationRuntime.Animator"/> before using trigger animations.
/// </summary>
public interface IVisualAnimator
{
    /// <summary>Whether <paramref name="property"/> on <paramref name="target"/> can be animated by this backend.</summary>
    /// <param name="target">The element whose property would be animated.</param>
    /// <param name="property">The dependency property to animate.</param>
    bool CanAnimate(VisualElement target, DependencyProperty property);

    /// <summary>Animates <paramref name="property"/> on <paramref name="target"/> to <paramref name="to"/>.</summary>
    /// <param name="target">The element whose property is animated.</param>
    /// <param name="property">The dependency property to animate.</param>
    /// <param name="to">The end value, already converted to the property type when possible.</param>
    /// <param name="spec">The timing and easing.</param>
    /// <remarks>
    /// The implementation must write at <see cref="Easel.BindingPriority.Animation"/> while running, supersede any
    /// animation already running on the same property, and hold the final value at local precedence when it completes,
    /// is superseded, or is stopped.
    /// </remarks>
    void Animate(VisualElement target, DependencyProperty property, object? to, AnimationSpec spec);

    /// <summary>Animates <paramref name="property"/> through <paramref name="frames"/>, one segment per adjacent pair.</summary>
    /// <param name="target">The element whose property is animated.</param>
    /// <param name="property">The dependency property to animate.</param>
    /// <param name="frames">The frames, ordered by <see cref="KeyFrame.Offset"/> with values converted to the property type.</param>
    /// <param name="spec">The timing, easing, loop and auto-reverse settings shared by every segment.</param>
    void AnimateKeyFrames(
        VisualElement target,
        DependencyProperty property,
        IReadOnlyList<KeyFrame> frames,
        AnimationSpec spec);

    /// <summary>Stops every animation this backend is running on <paramref name="target"/>, holding the reached value.</summary>
    /// <param name="target">The element whose animations are stopped.</param>
    void Stop(VisualElement target);

    /// <summary>Whether this backend currently has a running animation on <paramref name="target"/>.</summary>
    /// <param name="target">The element to query.</param>
    bool HasActive(VisualElement target);
}

/// <summary>The animation backend used by <see cref="VisualTransition"/> and the trigger animation actions.</summary>
/// <remarks>
/// Easel ships without an animation engine. A host package (for example VeloxDev.WinForms) assigns its
/// <see cref="IVisualAnimator"/> implementation here to bridge the transition framework of its choice. Until then,
/// requesting an animation throws <see cref="InvalidOperationException"/> instead of silently doing nothing.
/// </remarks>
public static class AnimationRuntime
{
    private static IVisualAnimator _animator = new NullVisualAnimator();

    /// <summary>The active animation backend; assigning <see langword="null"/> restores the no-op backend.</summary>
    public static IVisualAnimator Animator
    {
        get => _animator;
        set => _animator = value ?? new NullVisualAnimator();
    }

    /// <summary>Whether a host supplied an animation backend.</summary>
    public static bool HasAnimator => _animator is not NullVisualAnimator;
}

// 未注入后端时的占位：属性没有可用的采样器，启动动画要给出明确指引，而不是静默跑一趟空动画。
internal sealed class NullVisualAnimator : IVisualAnimator
{
    public bool CanAnimate(VisualElement target, DependencyProperty property) => false;

    public void Animate(VisualElement target, DependencyProperty property, object? to, AnimationSpec spec)
        => throw new InvalidOperationException(
            "No IVisualAnimator is registered. Assign Easel.Animation.AnimationRuntime.Animator before using "
            + "trigger animations, or reference a host package that supplies one.");

    public void AnimateKeyFrames(
        VisualElement target,
        DependencyProperty property,
        IReadOnlyList<KeyFrame> frames,
        AnimationSpec spec)
        => throw new InvalidOperationException(
            "No IVisualAnimator is registered. Assign Easel.Animation.AnimationRuntime.Animator before using "
            + "trigger animations, or reference a host package that supplies one.");

    public void Stop(VisualElement target)
    {
    }

    public bool HasActive(VisualElement target) => false;
}
