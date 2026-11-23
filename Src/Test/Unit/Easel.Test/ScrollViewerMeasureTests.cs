using System.Drawing;
using System.Drawing.Imaging;
using Easel;
using Easel.Visuals;
using Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>ScrollViewer measurement rules for the horizontal axis.</summary>
[TestClass]
public sealed class ScrollViewerMeasureTests
{
    /// <summary>A disabled horizontal axis locks the offset and disables horizontal scrolling on the presenter.</summary>
    [TestMethod]
    public void DisabledHorizontalAxis_LocksTheOffsetAndPresenter()
    {
        using var host = new VisualHost();
        var content = new Border { Width = 2000f, Height = 20f };
        var viewer = new ScrollViewer
        {
            Width = 300f,
            Height = 100f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = content,
        };
        host.Root = viewer;

        using var bitmap = new Bitmap(300, 100, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            host.RenderTo(graphics, new Size(300, 100));
        }

        Assert.AreEqual(0f, viewer.HorizontalOffset);
        var presenter = viewer.GetTemplateChild<ScrollContentPresenter>("PART_ScrollContentPresenter");
        Assert.IsNotNull(presenter);
        Assert.IsFalse(presenter.CanHorizontallyScroll, "presenter still allows horizontal scrolling");
    }
}
