using Easel.Visuals;

namespace Easel.Animation.Engine;

// Easel 的宿主就是元素所在的 VisualHost：帧写入与收尾都投回它的句柄线程，未挂载时就地执行。
internal static class AnimationHost
{
    internal static bool TryMarshal(VisualElement target, Action action)
    {
        var surface = target.Surface;
        if (surface is { IsHandleCreated: true, IsDisposed: false } && surface.InvokeRequired)
        {
            try
            {
                surface.BeginInvoke(action);
                return true;
            }
            catch (InvalidOperationException)
            {
                // 句柄在检查与投递之间消失：退回就地执行。
            }
        }

        return false;
    }
}
