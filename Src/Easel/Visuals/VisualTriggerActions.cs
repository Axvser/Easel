using Easel.Animation;
using Easel.Animation.Runtime;
using Easel.Animation.Sampling;

namespace Easel.Visuals;

/// <summary>An action run by a <see cref="VisualTrigger"/> when it enters or exits its active state.</summary>
public abstract class VisualTriggerAction
{
    /// <summary>Runs the action against the element the trigger applies to.</summary>
    /// <param name="target">The styled element, or the named template part the trigger targets.</param>
    public abstract void Invoke(VisualElement target);
}

/// <summary>Runs several actions in declaration order when a trigger enters or exits.</summary>
public sealed class TriggerActionGroup : VisualTriggerAction
{
    /// <summary>The actions to run, in order.</summary>
    public IList<VisualTriggerAction> Actions { get; } = new List<VisualTriggerAction>();

    /// <inheritdoc />
    public override void Invoke(VisualElement target)
    {
        foreach (var action in Actions)
        {
            action?.Invoke(target);
        }
    }
}

/// <summary>One value at a normalized point in a <see cref="KeyFrameAnimation"/>.</summary>
public sealed class KeyFrame
{
    /// <summary>The position in the animation, from 0 (start) to 1 (end).</summary>
    public double Offset { get; set; }

    /// <summary>The value the animated property reaches at <see cref="Offset"/>.</summary>
    public object? Value { get; set; }
}

/// <summary>
/// A trigger action that animates one dependency property to <see cref="To"/> with Easel's animation engine.
/// </summary>
/// <remarks>
/// The animation writes at <see cref="BindingPriority.Animation"/>, so it outranks a local value, style setter or
/// trigger setter while it runs. A new animation started on the same property supersedes the running one.
/// <para>
/// When it completes or is stopped, the value it reached is written at <see cref="BindingPriority.LocalValue"/> and
/// the animation layer is cleared. The end value therefore holds at local precedence (HoldEnd) instead of shadowing
/// every later write at animation precedence: a later local write overrides it, and clearing the local value lets
/// the style, trigger or default value show through again.
/// </para>
/// </remarks>
public sealed class TransitionAction : VisualTriggerAction
{
    /// <summary>The dependency property to animate, or <see langword="null"/> while unconfigured.</summary>
    public DependencyProperty? Property { get; set; }

    /// <summary>The value the animation ends at.</summary>
    public object? To { get; set; }

    /// <summary>The timing and easing of the animation; omitted means a 240 ms ease-out.</summary>
    public AnimationSpec? Effect { get; set; }

    /// <inheritdoc />
    public override void Invoke(VisualElement target)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (Property is null)
        {
            throw new InvalidOperationException("A TransitionAction needs a Property.");
        }

        VisualTransition.Animate(target, Property, To, Effect);
    }
}

/// <summary>
/// A trigger action that animates one dependency property through a sequence of keyframes.
/// </summary>
/// <remarks>
/// Each adjacent pair of keyframes becomes one segment whose duration is proportional to its slice of
/// <see cref="Duration"/>. The chain is one animation: a later animation on the same property supersedes all
/// of it, and <see cref="VisualTransition.Stop"/> ends it where it stands. As with
/// <see cref="TransitionAction"/>, the value reached when the chain ends is held at
/// <see cref="BindingPriority.LocalValue"/> after the animation layer is cleared.
/// </remarks>
public sealed class KeyFrameAnimation : VisualTriggerAction
{
    /// <summary>The dependency property to animate, or <see langword="null"/> while unconfigured.</summary>
    public DependencyProperty? Property { get; set; }

    /// <summary>The total duration of all segments; 240 milliseconds by default.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(240);

    /// <summary>The easing applied to every segment; <c>Cubic.Out</c> by default.</summary>
    public IEase Ease { get; set; } = Eases.Cubic.Out;

    /// <summary>Additional full passes of each segment; zero runs once and <see cref="int.MaxValue"/> repeats forever.</summary>
    public int LoopTime { get; set; }

    /// <summary>Whether each segment also runs backwards.</summary>
    public bool IsAutoReverse { get; set; }

    /// <summary>The keyframes, in any order; they are sorted by <see cref="KeyFrame.Offset"/> when invoked.</summary>
    public IList<KeyFrame> KeyFrames { get; } = new List<KeyFrame>();

    /// <inheritdoc />
    public override void Invoke(VisualElement target)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (Property is null)
        {
            throw new InvalidOperationException("A KeyFrameAnimation needs a Property.");
        }

        // 集合是公开可写的；null 元素排到比较器里会先炸成 NRE，而它本来就没有可用的 Offset。
        var frames = KeyFrames
            .Where(static frame => frame is not null)
            .OrderBy(static frame => frame.Offset)
            .ToArray();
        if (frames.Length == 0)
        {
            return;
        }

        VisualTransition.AnimateKeyFrames(target, Property, frames, Duration, Ease, LoopTime, IsAutoReverse);
    }
}

/// <summary>
/// Runs a dependency-property animation on a <see cref="VisualElement"/> with Easel's built-in animation engine.
/// </summary>
/// <remarks>
/// Easel normalizes the end value and provides interpolation, frame pacing and the hold-end bookkeeping itself, so a
/// host never registers a backend.
/// <para>
/// A completed or stopped animation holds its last value by writing it at <see cref="BindingPriority.LocalValue"/>
/// and clearing the animation layer, so the value never keeps outranking later writes at animation precedence.
/// </para>
/// </remarks>
public static class VisualTransition
{
    /// <summary>Animates <paramref name="property"/> on <paramref name="target"/> to <paramref name="to"/>.</summary>
    /// <param name="target">The element whose property is animated.</param>
    /// <param name="property">The dependency property to animate.</param>
    /// <param name="to">The value the animation ends at.</param>
    /// <param name="effect">The effect to run; omitted means a 240 ms ease-out.</param>
    /// <remarks>A new animation supersedes whatever is already running on the same property.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="property"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="property"/> has no interpolation in the engine (for example a
    /// <see cref="VisualEffect"/> or a string property). The animation is rejected instead of being accepted and
    /// holding the current value for its duration.
    /// </exception>
    public static void Animate(
        VisualElement target,
        DependencyProperty property,
        object? to,
        AnimationSpec? effect = null)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (property is null) throw new ArgumentNullException(nameof(property));

        EnsureAnimatable(property);

        // 调用方常把 1 / 1f 交给一个 Thickness 属性；在这一处统一成属性类型，插值器就不必猜。
        if (to is not null && !property.PropertyType.IsInstanceOfType(to))
        {
            to = XamlRuntime.ConvertValue(to, property.PropertyType);
        }

        AnimationScheduler.Animate(target, property, to, effect ?? new AnimationSpec());
    }

    /// <summary>Animates <paramref name="property"/> through <paramref name="frames"/>, one segment per adjacent pair.</summary>
    /// <param name="target">The element whose property is animated.</param>
    /// <param name="property">The dependency property to animate.</param>
    /// <param name="frames">The keyframes; they are sorted by <see cref="KeyFrame.Offset"/>.</param>
    /// <param name="duration">The total duration of the whole chain.</param>
    /// <param name="ease">The easing applied to every segment; omitted means <c>Cubic.Out</c>.</param>
    /// <param name="loopTime">Additional full passes; zero runs once and <see cref="int.MaxValue"/> repeats forever.</param>
    /// <param name="isAutoReverse">Whether each pass also runs backwards.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/>, <paramref name="property"/> or <paramref name="frames"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException"><paramref name="property"/> has no interpolation in the engine.</exception>
    public static void AnimateKeyFrames(
        VisualElement target,
        DependencyProperty property,
        IReadOnlyList<KeyFrame> frames,
        TimeSpan duration,
        IEase? ease = null,
        int loopTime = 0,
        bool isAutoReverse = false)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (property is null) throw new ArgumentNullException(nameof(property));
        if (frames is null) throw new ArgumentNullException(nameof(frames));
        if (frames.Count == 0)
        {
            return;
        }

        var ordered = new List<KeyFrame>(frames.Count);
        foreach (var frame in frames)
        {
            if (frame is null)
            {
                continue;
            }

            var value = frame.Value;
            if (value is not null && !property.PropertyType.IsInstanceOfType(value))
            {
                value = XamlRuntime.ConvertValue(value, property.PropertyType);
            }

            ordered.Add(new KeyFrame
            {
                Offset = XamlMath.Clamp(frame.Offset, 0d, 1d),
                Value = value,
            });
        }

        if (ordered.Count == 0)
        {
            return;
        }

        EnsureAnimatable(property);

        ordered.Sort(static (left, right) => left.Offset.CompareTo(right.Offset));
        var spec = new AnimationSpec
        {
            Duration = duration,
            Ease = ease ?? Eases.Cubic.Out,
            LoopTime = loopTime,
            IsAutoReverse = isAutoReverse,
        };
        AnimationScheduler.AnimateKeyFrames(target, property, ordered, spec);
    }

    /// <summary>Stops every animation currently running on <paramref name="target"/>.</summary>
    /// <param name="target">The element whose animations are stopped.</param>
    /// <remarks>
    /// The value each running animation had reached is held at <see cref="BindingPriority.LocalValue"/> and the
    /// animation layer is cleared, so the stop freezes where it stands without leaving a value that outranks every
    /// later write. Use it when a trigger exits or an element is torn down.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public static void Stop(VisualElement target)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        AnimationScheduler.Stop(target);
    }

    internal static bool HasActive(VisualElement target) => AnimationScheduler.HasActive(target);

    // 没有该类型的插值器时整段停住当前值；在启动前直接拒绝，而不是开一趟什么也不写的动画。
    private static void EnsureAnimatable(DependencyProperty property)
    {
        if (ValueInterpolator.CanInterpolate(property.PropertyType))
        {
            return;
        }

        throw new NotSupportedException(
            $"'{property.Name}' of type '{property.PropertyType.Name}' has no interpolator in Easel's animation "
            + "engine, so it cannot be animated. Animate a supported property; a VisualEffect cannot be interpolated "
            + "at all.");
    }
}
