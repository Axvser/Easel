namespace Easel.Animation;

/// <summary>The timing and easing of one property animation.</summary>
public sealed class AnimationSpec
{
    /// <summary>The duration of the animation; 240 milliseconds by default.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(240);

    /// <summary>The easing curve; <c>Cubic.Out</c> by default.</summary>
    public IEase Ease { get; set; } = Eases.Cubic.Out;

    /// <summary>The frame rate the engine targets; 60 by default.</summary>
    public int FPS { get; set; } = 60;

    /// <summary>Additional full passes; zero runs once and <see cref="int.MaxValue"/> repeats forever.</summary>
    public int LoopTime { get; set; }

    /// <summary>Whether each pass also runs backwards.</summary>
    public bool IsAutoReverse { get; set; }
}
