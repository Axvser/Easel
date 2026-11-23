using Demo.Views.Cards;

namespace Demo;

/// <summary>The Graphics group: vector shapes, hosted visual trees, gradients, composition and 3D transforms.</summary>
internal static class GraphicsCards
{
    /// <summary>Creates the Graphics group in catalog order.</summary>
    public static IReadOnlyList<GalleryCard> Create() =>
    [
        Card(
            "graphics.shapes",
            "Graphics",
            "Shapes",
            "Rectangle, ellipse, line, polygon, polyline and path.",
            "<Polygon Points=\"0,60 30,0 60,60\" Fill=\"{StaticResource Accent}\" />",
            _ => new ShapesCard()),
        Card(
            "graphics.presenter",
            "Graphics",
            "VisualPresenter",
            "Hosts a lightweight Visual tree inside the control tree.",
            "<VisualPresenter.Content><ContainerVisual>…",
            _ => new VisualPresenterCard()),
        Card(
            "graphics.opacity",
            "Graphics",
            "Opacity and clip",
            "A visual subtree is composited through one bitmap.",
            "<Border Opacity=\"0.5\" CornerRadius=\"8\">",
            _ => new OpacityClipCard()),
        Card(
            "graphics.gradients",
            "Graphics",
            "Gradient brushes",
            "Linear and radial gradient brushes declared inline and as resources; the same Brush contract paints borders, text and shapes.",
            "<Border.Background><LinearGradientBrush StartPoint=\"0,0\" EndPoint=\"1,1\">…",
            _ => new GradientsCard()),
        Card(
            "graphics.composition",
            "Graphics",
            "Visual composition",
            "A lightweight Visual tree composed through group opacity, an opacity mask, a drop shadow and a bitmap cache.",
            "<ContainerVisual Opacity=\"0.85\">…",
            _ => new CompositionCard()),
        Card(
            "graphics.transforms-3d",
            "Graphics",
            "3D transforms",
            "Rotate3DTransform projects live 2D cards in perspective; the sliders drive the angles through dependency properties.",
            "<Border.Transform3D><Rotate3DTransform AngleX=\"16\" AngleY=\"24\" />",
            _ => new Transform3DCard()),
    ];

    private static GalleryCard Card(
        string id, string group, string title, string summary, string usage,
        Func<GalleryModel, Easel.Visuals.VisualElement> create)
        => new(new GalleryCardInfo(id, group, title, summary, usage), create);
}
