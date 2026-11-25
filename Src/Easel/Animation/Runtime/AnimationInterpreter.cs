namespace Easel.Animation.Runtime;

// 把归一化时间折成缓动进度：反向程从 1 走到 0，端帧直接取端点而不依赖 Ease(1) 的精度。
internal static class AnimationInterpreter
{
    internal static double Ease(IEase ease, double rawT, bool forward)
    {
        if (rawT >= 1d)
        {
            return forward ? 1d : 0d;
        }

        return ease.Ease(forward ? rawT : 1d - rawT);
    }
}
