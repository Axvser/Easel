using Easel.Animation.Events;

namespace Easel.Animation.Effects;

// 一个属性动画的时序配置；公开入口只提供 AnimationSpec，引擎内部在启动时把它抄成这个可变副本。
internal sealed class AnimationEffect
{
    internal TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(240);

    internal IEase Ease { get; set; } = Eases.Cubic.Out;

    internal int FPS { get; set; } = 60;

    internal int LoopTime { get; set; }

    internal bool IsAutoReverse { get; set; }

    internal event EventHandler<AnimationEventArgs>? Finally;

    internal static AnimationEffect FromSpec(AnimationSpec spec) => new()
    {
        Duration = spec.Duration,
        Ease = spec.Ease ?? Eases.Cubic.Out,
        FPS = spec.FPS,
        LoopTime = spec.LoopTime,
        IsAutoReverse = spec.IsAutoReverse,
    };

    internal void InvokeFinally(object sender, AnimationEventArgs args) => Finally?.Invoke(sender, args);
}
