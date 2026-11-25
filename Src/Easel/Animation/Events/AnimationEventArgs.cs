namespace Easel.Animation.Events;

// 一次动画结束时交给引擎内部回调的位置信息；Loop 是本段的第几轮，Cycle 是整条动画走过的程数。
internal sealed class AnimationEventArgs : EventArgs
{
    internal int Loop { get; set; }

    internal long Cycle { get; set; }
}
