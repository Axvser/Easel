using System.Runtime.CompilerServices;
using Easel.Animation.Effects;
using Easel.Visuals;

namespace Easel.Animation.Runtime;

// 每个元素的动画登记表：同一 DP 只保留一趟动画，新的一趟取代旧的；Stop 清空该元素的全部登记。
internal static class AnimationScheduler
{
    private static readonly ConditionalWeakTable<VisualElement, TargetState> Targets = new();

    internal static void Animate(VisualElement target, DependencyProperty property, object? to, AnimationSpec spec)
        => Animate(target, property, null, to, spec);

    internal static void Animate(
        VisualElement target,
        DependencyProperty property,
        object? from,
        object? to,
        AnimationSpec spec)
        => Animate(target, property, from, to, spec, holdOnComplete: true);

    // holdOnComplete=false 是隐式过渡：收尾摘掉 Animation 层，不把终值写进 Local 层，
    // 否则会压过触发器的 StyleTrigger 层，下一次状态切换就不会再变色。
    internal static void Animate(
        VisualElement target,
        DependencyProperty property,
        object? from,
        object? to,
        AnimationSpec spec,
        bool holdOnComplete)
        => Start(new AnimationRun(target, property, AnimationEffect.FromSpec(spec), to, null, from, holdOnComplete));

    internal static void AnimateKeyFrames(
        VisualElement target,
        DependencyProperty property,
        IReadOnlyList<KeyFrame> frames,
        AnimationSpec spec)
        => Start(new AnimationRun(target, property, AnimationEffect.FromSpec(spec), null, frames));

    internal static void Stop(VisualElement target)
    {
        if (!Targets.TryGetValue(target, out var state))
        {
            return;
        }

        List<AnimationRun> runs;
        lock (state.Gate)
        {
            runs = [.. state.Active.Values];
            state.Active.Clear();
        }

        foreach (var run in runs)
        {
            if (run.TryEnd())
            {
                HoldIfInactive(run);
            }
        }
    }

    internal static bool HasActive(VisualElement target)
    {
        if (!Targets.TryGetValue(target, out var state))
        {
            return false;
        }

        lock (state.Gate)
        {
            return state.Active.Count > 0;
        }
    }

    // 该 DP 上是否还有动画（显式或隐式）；动画层压着底层时，底层写入不该另起一趟过渡。
    internal static bool IsActive(VisualElement target, DependencyProperty property)
    {
        if (!Targets.TryGetValue(target, out var state))
        {
            return false;
        }

        lock (state.Gate)
        {
            return state.Active.ContainsKey(property);
        }
    }

    // 只认元素级过渡（不落 Local 的那一类）；显式动画仍按自己的终值收尾。
    internal static bool IsImplicitActive(VisualElement target, DependencyProperty property)
    {
        if (!Targets.TryGetValue(target, out var state))
        {
            return false;
        }

        lock (state.Gate)
        {
            return state.Active.TryGetValue(property, out var run) && !run.HoldOnComplete;
        }
    }

    // 只有仍登记在册的那一趟才写帧；被取代的旧动画即使还有一个排队中的写入也不能盖掉新动画。
    internal static void Write(AnimationRun run, object? value)
    {
        var state = GetState(run.Target);
        void Core()
        {
            lock (state.Gate)
            {
                if (state.Active.TryGetValue(run.Property, out var current) && ReferenceEquals(current, run))
                {
                    run.Binding.SetAnimation(value);
                }
            }
        }

        if (run.Binding.TryMarshal(Core))
        {
            return;
        }

        Core();
    }

    internal static void Complete(AnimationRun run)
    {
        var state = GetState(run.Target);
        lock (state.Gate)
        {
            if (state.Active.TryGetValue(run.Property, out var current) && ReferenceEquals(current, run))
            {
                state.Active.Remove(run.Property);
            }
        }

        HoldIfInactive(run);
    }

    // 只有该 DP 上已经没有任何动画时才落值，避免旧动画的收尾清掉新动画正在写的层位。
    internal static void HoldIfInactive(AnimationRun run)
    {
        var state = GetState(run.Target);
        var value = run.LastWritten;
        void Core()
        {
            lock (state.Gate)
            {
                if (!state.Active.ContainsKey(run.Property))
                {
                    if (run.HoldOnComplete)
                    {
                        run.Binding.Hold(value);
                    }
                    else
                    {
                        run.Binding.Release();
                    }
                }
            }
        }

        if (run.Binding.TryMarshal(Core))
        {
            return;
        }

        Core();
    }

    // 启动失败时只摘登记、不落值：这一趟从未真正开始，调用方会收到异常。
    internal static void Abandon(AnimationRun run)
    {
        var state = GetState(run.Target);
        lock (state.Gate)
        {
            if (state.Active.TryGetValue(run.Property, out var current) && ReferenceEquals(current, run))
            {
                state.Active.Remove(run.Property);
            }
        }
    }

    private static void Start(AnimationRun run)
    {
        var state = GetState(run.Target);
        AnimationRun? superseded;
        lock (state.Gate)
        {
            state.Active.TryGetValue(run.Property, out superseded);
            state.Active[run.Property] = run;
        }

        superseded?.TryEnd();
        run.Begin();
    }

    private static TargetState GetState(VisualElement target)
        => Targets.GetValue(target, static _ => new TargetState());

    private sealed class TargetState
    {
        internal readonly object Gate = new();

        internal readonly Dictionary<DependencyProperty, AnimationRun> Active = new();
    }
}
