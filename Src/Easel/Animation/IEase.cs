namespace Easel.Animation;

/// <summary>An easing curve that maps normalized time to progress.</summary>
public interface IEase
{
    /// <summary>Maps normalized time <paramref name="t"/> in [0,1] to the eased progress.</summary>
    /// <param name="t">Normalized time.</param>
    /// <returns>The eased progress.</returns>
    double Ease(double t);
}
