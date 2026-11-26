using System.Diagnostics;
using Easel.Animation.Binding;
using Easel.Animation.Effects;
using Easel.Animation.Events;
using Easel.Animation.Sampling;
using Easel.Visuals;

namespace Easel.Animation.Runtime;

// 一个属性上的一趟动画：按段推进，每段自己按 FPS 采样并处理 LoopTime / IsAutoReverse；
// 帧写入、被取代判定和收尾都交给 AnimationScheduler，保证与同一属性上的新动画串行。
internal sealed class AnimationRun
{
    private readonly object _gate = new();
    private readonly VisualElement _target;
    private readonly DependencyProperty _property;
    private readonly AnimationProperty _binding;
    private readonly AnimationEffect _effect;
    private readonly object? _singleTo;
    private readonly IReadOnlyList<KeyFrame>? _frames;
    private readonly object? _startFrom;
    private readonly bool _holdOnComplete;

    private Segment[] _segments = [];
    private System.Threading.Timer? _timer;
    private bool _ended;
    private int _segmentIndex;
    private int _iteration;
    private bool _forward = true;
    private long _passStart;
    private double _durationMs;
    private object? _from;
    private object? _to;
    private object? _lastWritten;
    private int _cycle;

    internal AnimationRun(
        VisualElement target,
        DependencyProperty property,
        AnimationEffect effect,
        object? to,
        IReadOnlyList<KeyFrame>? frames,
        object? from = null,
        bool holdOnComplete = true)
    {
        _target = target;
        _property = property;
        _binding = new AnimationProperty(target, property);
        _effect = effect;
        _singleTo = to;
        _frames = frames;
        _startFrom = from;
        _holdOnComplete = holdOnComplete;
    }

    internal VisualElement Target => _target;

    internal DependencyProperty Property => _property;

    internal AnimationProperty Binding => _binding;

    // 隐式过渡收尾只摘动画层，让触发/样式/本地层的终值显出；显式动画仍把终值落到 Local 层。
    internal bool HoldOnComplete => _holdOnComplete;

    // 最后一个算出来的帧值；收尾时直接落它，避免托管元素的排队写入在登记被摘后跳过而导致落值不精确。
    internal object? LastWritten
    {
        get
        {
            lock (_gate)
            {
                return _lastWritten;
            }
        }
    }

    internal void Begin()
    {
        lock (_gate)
        {
            if (_ended)
            {
                return;
            }

            // 元素级过渡显式给出旧值；其余动画仍从当前值起步。
            var start = _startFrom ?? _binding.Read();
            _segments = BuildSegments();
            _lastWritten = start;
            EnterSegment(0, start, Stopwatch.GetTimestamp());

            try
            {
                // 在调用线程上先落一帧，动画层立即生效；零时长的段也在这里一次走完。
                Pump(Stopwatch.GetTimestamp());
                if (!_ended)
                {
                    Arm();
                }
            }
            catch
            {
                // 启动失败：撤掉登记，不把半趟动画留在活动表里。
                TryEnd();
                AnimationScheduler.Abandon(this);
                throw;
            }
        }
    }

    // 停掉计时器；返回 false 表示这一趟已经结束了，调用方不必再收尾。
    internal bool TryEnd()
    {
        lock (_gate)
        {
            if (_ended)
            {
                return false;
            }

            _ended = true;
            _timer?.Dispose();
            _timer = null;
            return true;
        }
    }

    private Segment[] BuildSegments()
    {
        if (_frames is null)
        {
            return [new Segment(_singleTo, 1d)];
        }

        var segments = new Segment[_frames.Count];
        var previousOffset = 0d;
        for (var index = 0; index < _frames.Count; index++)
        {
            var frame = _frames[index];
            var offset = Math.Max(0d, Math.Min(1d, frame.Offset));
            segments[index] = new Segment(frame.Value, Math.Max(0d, offset - previousOffset));
            previousOffset = offset;
        }

        return segments;
    }

    private void EnterSegment(int index, object? from, long now)
    {
        _segmentIndex = index;
        _iteration = 0;
        _forward = true;
        _from = from;
        _to = _segments[index].To;
        _durationMs = Math.Max(0d, _effect.Duration.TotalMilliseconds * _segments[index].Fraction);
        _passStart = now;
    }

    private void Arm()
    {
        var fps = _effect.FPS > 0 ? _effect.FPS : 60;
        var interval = Math.Max(1, (int)Math.Round(1000d / fps));
        _timer = new System.Threading.Timer(_ => Tick(), null, interval, interval);
    }

    private void Tick()
    {
        lock (_gate)
        {
            if (_ended)
            {
                return;
            }

            try
            {
                Pump(Stopwatch.GetTimestamp());
            }
            catch
            {
                // 采样或写入抛出的异常不能逃出计时器线程；停在当前值上结束这一趟。
                try
                {
                    Finish();
                }
                catch
                {
                    TryEnd();
                    AnimationScheduler.Abandon(this);
                }
            }
        }
    }

    private void Pump(long now)
    {
        while (!_ended)
        {
            var duration = _durationMs;
            double rawT;
            if (duration <= 0d)
            {
                rawT = 1d;
            }
            else
            {
                rawT = ((now - _passStart) * 1000d / Stopwatch.Frequency) / duration;
                if (rawT > 1d)
                {
                    rawT = 1d;
                }
            }

            var eased = AnimationInterpreter.Ease(_effect.Ease, rawT, _forward);
            _lastWritten = ValueInterpolator.Interpolate(_property.PropertyType, _from, _to, eased);
            AnimationScheduler.Write(this, _lastWritten);

            if (rawT < 1d)
            {
                return;
            }

            // 正向程走完先反着走一遍；反向程走完才算这一轮结束。
            if (_forward && _effect.IsAutoReverse)
            {
                _forward = false;
                _passStart = Stopwatch.GetTimestamp();
                now = _passStart;
                continue;
            }

            _cycle++;
            _iteration++;
            _forward = true;

            if (_iteration > _effect.LoopTime)
            {
                if (_segmentIndex + 1 >= _segments.Length)
                {
                    Finish();
                    return;
                }

                EnterSegment(_segmentIndex + 1, _lastWritten, Stopwatch.GetTimestamp());
            }
            else
            {
                _passStart = Stopwatch.GetTimestamp();
            }

            // 零时长的段不占时间：直接在同一帧里走下一段，避免空等一个计时器周期。
            if (_durationMs > 0d)
            {
                return;
            }

            if (_effect.LoopTime == int.MaxValue)
            {
                // 永远循环一个不占时间的段没有可看的帧，停在终点结束。
                Finish();
                return;
            }

            now = Stopwatch.GetTimestamp();
        }
    }

    private void Finish()
    {
        if (!TryEnd())
        {
            return;
        }

        _effect.InvokeFinally(_target, new AnimationEventArgs { Loop = _iteration, Cycle = _cycle });
        AnimationScheduler.Complete(this);
    }

    private readonly struct Segment
    {
        internal Segment(object? to, double fraction)
        {
            To = to;
            Fraction = fraction;
        }

        internal object? To { get; }

        internal double Fraction { get; }
    }
}
