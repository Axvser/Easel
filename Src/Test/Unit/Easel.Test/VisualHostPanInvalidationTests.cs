using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Easel.Visuals;

namespace Easel.Test;

/// <summary>
/// The <see cref="VisualHost.RenderTo"/> pan-only fast path must not replay a cached frame when a root that paints its
/// own content repaints in the same frame as a root translation.
/// </summary>
[STATestClass]
public sealed class VisualHostPanInvalidationTests
{
    /// <summary>
    /// A custom root with no Background/Border changes what it draws and then translates; the shifted frame must
    /// carry the new pixels instead of the cached ones.
    /// </summary>
    [TestMethod]
    public void CustomRoot_ContentRepaintDuringRootTranslation_RedrawsInsteadOfShiftingStaleFrame()
    {
        using var surface = new VisualHost { Size = new Size(120, 80), DpiScaleOverride = 1f };
        var root = new PaintingRoot { Fill = Color.Red };
        surface.Root = root;

        using var first = Render(surface, 120, 80);
        Assert.AreEqual(
            Color.Red.ToArgb(),
            first.GetPixel(60, 40).ToArgb(),
            "the first frame paints the root's own fill");

        // 宿主在设置 Transform 之后才调用 InvalidateVisual（WorkflowSurfaceBehavior 的相机路径就是这个顺序）；
        // 这里的内容真的变了，而不只是变换带来的附带失效。
        root.Fill = Color.Blue;
        root.Transform = new Matrix(1f, 0f, 0f, 1f, 20f, 0f);
        root.InvalidateVisual();

        using var second = Render(surface, 120, 80);
        Assert.AreEqual(
            Color.Blue.ToArgb(),
            second.GetPixel(60, 40).ToArgb(),
            "the translated frame contains the newly painted content, not the cached red pixels");
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

    // 根自己绘制内容且没有 Background/Border，内容变化只能靠显式 InvalidateVisual 通知宿主。
    private sealed class PaintingRoot : VisualElement
    {
        public Color Fill { get; set; } = Color.Red;

        protected override void OnRender(Graphics graphics)
        {
            using var brush = new SolidBrush(Fill);
            graphics.FillRectangle(brush, 0f, 0f, Math.Max(1f, Bounds.Width), Math.Max(1f, Bounds.Height));
        }
    }
}
