using Demo.Views.Cards;

namespace Demo;

/// <summary>The animation card group: each card runs a scene and shows the markup that produces it.</summary>
internal static class AnimationCards
{
    /// <summary>Creates the animation group.</summary>
    public static IReadOnlyList<GalleryCard> Create() =>
    [
        Card(
            "animation.transition",
            "Animation",
            "TransitionAction",
            "A trigger starts one transition per property; the exit action animates the values back.",
            "<TransitionAction Property=\"Opacity\" To=\"0.3\" Ease=\"CubicOut\" />",
            _ => new AnimationTransitionCard()),
        Card(
            "animation.keyframes",
            "Animation",
            "KeyFrameAnimation",
            "One chain splits a duration into segments, each running the same easing.",
            "<KeyFrameAnimation Property=\"Width\" …><KeyFrame Offset=\"0.5\" Value=\"120\" />",
            _ => new AnimationKeyFrameCard()),
        Card(
            "animation.brush",
            "Animation",
            "Brush interpolation",
            "Gradients lerp their stops and geometry; a linear-to-radial pair composes a cross-fade.",
            "<TransitionAction Property=\"Background\" To=\"{StaticResource SunsetGradient}\" />",
            _ => new AnimationBrushCard()),
        Card(
            "animation.transform3d",
            "Animation",
            "Transform3D",
            "A 3D endpoint interpolates its own angles and keeps the pivot and camera depth.",
            "<TransitionAction Property=\"Transform3D\" To=\"{StaticResource Tilted}\" />",
            _ => new AnimationTransform3DCard()),
    ];

    private static GalleryCard Card(
        string id, string group, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create)
        => new(new GalleryCardInfo(id, group, title, summary, usage), create);
}
