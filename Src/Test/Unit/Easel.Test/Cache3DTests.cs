using System.Drawing;
using System.Drawing.Imaging;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary><see cref="Visual.CacheAsBitmap"/> on a 3D visual: identical output and reusable content.</summary>
[STATestClass]
public sealed class Cache3DTests
{
    /// <summary>The cached path paints the same pixels as the uncached one.</summary>
    [TestMethod]
    public void Cached3D_MatchesUncachedOutput()
    {
        using var cached = RenderScene(cache: true);
        using var plain = RenderScene(cache: false);

        for (var y = 0; y < cached.Height; y += 3)
        {
            for (var x = 0; x < cached.Width; x += 3)
            {
                Assert.AreEqual(plain.GetPixel(x, y).ToArgb(), cached.GetPixel(x, y).ToArgb(), $"pixel {x},{y}");
            }
        }
    }

    /// <summary>Transform and opacity changes reuse the cached content; a content change invalidates it.</summary>
    [TestMethod]
    public void Cached3D_ReusesContentAcrossTransformChanges()
    {
        using var surface = new VisualHost { Size = new Size(200, 160) };
        var child = new CountingElement();
        using var root = new VC.StackPanel
        {
            Width = 120f,
            Height = 80f,
            CacheAsBitmap = true,
            Transform3D = new Rotate3DTransform
            {
                AngleY = 30d,
                CenterX = 60d,
                CenterY = 40d,
                Depth = 400d,
            },
        };
        root.Add(child);
        surface.Root = root;

        _ = Render(surface, 200, 160);
        Assert.AreEqual(1, child.Renders, "the first frame builds the cache");

        _ = Render(surface, 200, 160);
        Assert.AreEqual(1, child.Renders, "an unchanged frame reuses the cache");

        root.Transform3D = new Rotate3DTransform
        {
            AngleY = 55d,
            CenterX = 60d,
            CenterY = 40d,
            Depth = 400d,
        };
        _ = Render(surface, 200, 160);
        Assert.AreEqual(1, child.Renders, "a transform change keeps the content bitmap");

        root.Opacity = 0.6d;
        _ = Render(surface, 200, 160);
        Assert.AreEqual(1, child.Renders, "an opacity change keeps the content bitmap");

        child.Marker = 1;
        _ = Render(surface, 200, 160);
        Assert.AreEqual(2, child.Renders, "a content change rebuilds the cache");
    }

    private static Bitmap RenderScene(bool cache)
    {
        using var surface = new VisualHost { Size = new Size(200, 160) };
        using var root = new VC.StackPanel
        {
            Width = 120f,
            Height = 80f,
            Background = Color.DarkSlateBlue,
            CacheAsBitmap = cache,
            Transform3D = new Rotate3DTransform
            {
                AngleX = 18d,
                AngleY = 42d,
                CenterX = 60d,
                CenterY = 40d,
                Depth = 180d,
            },
        };
        root.Add(new CountingElement { FillColor = Color.Orange });
        surface.Root = root;

        return Render(surface, 200, 160);
    }

    private static Bitmap Render(VisualHost surface, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            surface.RenderTo(graphics, new Size(width, height));
        }

        return bitmap;
    }

    private sealed class CountingElement : VisualElement
    {
        public static readonly DependencyProperty MarkerProperty = DependencyProperty.Register(
            nameof(Marker),
            typeof(int),
            typeof(CountingElement),
            new PropertyMetadata(0) { AffectsRender = true });

        public int Renders { get; private set; }

        public Color FillColor { get; set; } = Color.Orange;

        public int Marker
        {
            get => (int)GetValue(MarkerProperty)!;
            set => SetValue(MarkerProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize) => new(52, 34);

        protected override void RenderContent(Graphics graphics)
        {
            Renders++;
            using var brush = new SolidBrush(FillColor);
            graphics.FillRectangle(
                brush,
                new RectangleF(0f, 0f, Math.Max(1f, Bounds.Width), Math.Max(1f, Bounds.Height)));
        }
    }
}
