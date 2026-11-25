using Easel.Animation.Engine;
using Easel.Visuals;

namespace Easel.Animation.Binding;

// 把一个 (元素, DP) 适配成引擎的可写目标：帧写 Animation 层，收尾把当前值落到 Local 层再摘掉动画层。
internal sealed class AnimationProperty
{
    private readonly VisualElement _target;
    private readonly DependencyProperty _property;

    internal AnimationProperty(VisualElement target, DependencyProperty property)
    {
        _target = target;
        _property = property;
    }

    internal object? Read() => _property.GetValue(_target);

    internal bool TryMarshal(Action action) => AnimationHost.TryMarshal(_target, action);

    internal void SetAnimation(object? value) => _property.SetValue(_target, value, BindingPriority.Animation);

    // 值留住（HoldEnd）但不再压过后续写入：先摘动画层再写 Local 层——反之，SetValue 触发的
    // PropertyChanged 若启动了新动画，收尾的 ClearValue 会把新动画的层位一起清掉。
    // value 由调用方给出：托管元素上最后一帧可能还在消息队列里，等回读时它已因登记被摘而跳过。
    internal void Hold(object? value)
    {
        _property.ClearValue(_target, BindingPriority.Animation);
        _property.SetValue(_target, value, BindingPriority.LocalValue);
    }
}
