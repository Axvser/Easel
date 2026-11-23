using System.Drawing;
using System.Drawing.Drawing2D;
using Easel;
using Easel.Visuals;

namespace Demo.Views.Cards;

/// <summary>
/// Hosts a lightweight <see cref="ContainerVisual"/> tree and exposes its group opacity and rotation to the sliders.
/// </summary>
/// <remarks>
/// The visual tree is not a control tree: it has no measure/arrange, no HWND and no input. It is composed once and
/// painted by the presenter in the surface pass, which is exactly what the card demonstrates.
/// </remarks>
public sealed partial class CompositionLabView : ThemedStackPanel
{
    private ContainerVisual? _layer;

    /// <summary>Creates the view, builds the visual tree and applies the initial slider values.</summary>
    public CompositionLabView()
    {
        InitializeComponent();
        _layer = BuildLayer();
        visualHost.Content = _layer;
        ApplySliders();
    }

    private static ContainerVisual BuildLayer()
    {
        var theme = Theme.Current;
        var layer = new ContainerVisual { Opacity = 0.88, CacheAsBitmap = true };

        var glow = new ShapeVisual
        {
            Bounds = new RectangleF(18f, 18f, 140f, 140f),
            Geometry = new EllipseGeometry(),
            Fill = new SolidColorBrush(theme.Accent),
            Opacity = 0.9d,
            Effect = new DropShadowEffect { Offset = new PointF(8f, 10f), Blur = 12f, Opacity = 0.55d },
        };
        layer.Add(glow);

        var card = new ShapeVisual
        {
            Bounds = new RectangleF(108f, 58f, 150f, 108f),
            Geometry = new RectangleGeometry(),
            Fill = GalleryTheme.Linear(
                (Color.FromArgb(0xFF, 0x00, 0xD2, 0xFF), 0d),
                (Color.FromArgb(0xFF, 0x7B, 0x2F, 0xF7), 1d)),
            Opacity = 0.78d,
        };
        layer.Add(card);

        var accent = new ShapeVisual
        {
            Bounds = new RectangleF(188f, 120f, 64f, 64f),
            Geometry = new EllipseGeometry(),
            Fill = new SolidColorBrush(theme.Selection),
            Opacity = 0.65d,
        };
        layer.Add(accent);

        layer.Add(new TextVisual
        {
            Bounds = new RectangleF(18f, 194f, 250f, 28f),
            Text = "ContainerVisual · ShapeVisual · TextVisual",
            Foreground = new SolidColorBrush(theme.Foreground),
            Font = new Font("Segoe UI Semibold", 9.5f),
        });

        return layer;
    }

    private void ApplySliders()
    {
        if (_layer is null || opacitySlider is null || angleSlider is null)
        {
            return;
        }

        _layer.Opacity = Math.Clamp(opacitySlider.Value / 100d, 0.05d, 1d);

        var matrix = new Matrix();
        matrix.RotateAt((float)angleSlider.Value, new PointF(135f, 124f));
        _layer.Transform = matrix;
        _layer.InvalidateCache();

        if (opacityReadout is not null)
        {
            opacityReadout.Text = $"{(int)Math.Round(opacitySlider.Value)}%";
        }

        visualHost?.InvalidateVisual();
    }

    private void OnOpacityChanged(object? sender, EventArgs e) => ApplySliders();

    private void OnAngleChanged(object? sender, EventArgs e) => ApplySliders();
}
