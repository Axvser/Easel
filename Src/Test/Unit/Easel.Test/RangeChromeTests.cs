using System.Drawing;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>The built-in range chrome paints every state in one pass and reports only the changed span.</summary>
[STATestClass]
public sealed class RangeChromeTests
{
    private static readonly Color TrackColor = Color.FromArgb(0xFF, 0x12, 0x34, 0x56);
    private static readonly Color AccentColor = Color.FromArgb(0xFF, 0x65, 0x43, 0x21);
    private static readonly Color FocusColor = Color.FromArgb(0xFF, 0xF0, 0x10, 0x10);

    [TestMethod]
    public void SliderChrome_PaintsTrackFillAndThumbFromTemplateBrushes()
    {
        using var surface = CreateSurface();
        using var slider = new VC.Slider { Width = 200, Minimum = 0, Maximum = 100, Value = 50 };
        ApplyBrushes(slider);
        surface.Root = slider;
        Layout(surface);

        var bounds = slider.AbsoluteBounds;
        var thumbX = 7f + ((bounds.Width - 14f) * 0.5f);
        using var bitmap = Render(surface);

        Assert.AreEqual(TrackColor.ToArgb(), Sample(bitmap, bounds, bounds.Width - 25f, bounds.Height / 2f).ToArgb(),
            "the track outside the fill uses the track brush");
        Assert.AreEqual(AccentColor.ToArgb(), Sample(bitmap, bounds, 35f, bounds.Height / 2f).ToArgb(),
            "the filled part uses the fill brush");
        Assert.AreEqual(AccentColor.ToArgb(), Sample(bitmap, bounds, thumbX, (bounds.Height / 2f) - 5f).ToArgb(),
            "the thumb is painted above the track band");
    }

    [TestMethod]
    public void SliderChrome_PaintsTheFocusRingOnlyAfterKeyboardFocus()
    {
        using var surface = CreateSurface();
        using var slider = new VC.Slider { Width = 200, Minimum = 0, Maximum = 100, Value = 50 };
        ApplyBrushes(slider);
        surface.Root = slider;
        Layout(surface);

        var bounds = slider.AbsoluteBounds;
        var thumbX = 7f + ((bounds.Width - 14f) * 0.5f);
        var ringY = (bounds.Height / 2f) - 9f;
        using var before = Render(surface);
        var previous = Sample(before, bounds, thumbX, ringY);

        Assert.IsTrue(slider.Focus());
        surface.ProcessKeyDown(Keys.Right);
        using var after = Render(surface);
        var current = Sample(after, bounds, thumbX, ringY);

        Assert.AreNotEqual(previous.ToArgb(), current.ToArgb(), "the ring draws over the empty band above the track");
        Assert.IsGreaterThan(current.G + 30, current.R, "the ring uses the focus brush");
        Assert.IsGreaterThan(current.B + 30, current.R, "the ring uses the focus brush");
    }

    [TestMethod]
    public void SliderChrome_ValueChangeRepaintsTheOldAndNewFillSpans()
    {
        using var surface = CreateSurface();
        using var slider = new VC.Slider { Width = 200, Minimum = 0, Maximum = 100, Value = 80 };
        ApplyBrushes(slider);
        surface.Root = slider;
        Layout(surface);
        Layout(surface);

        var bounds = slider.AbsoluteBounds;
        using (var before = Render(surface))
        {
            Assert.AreEqual(AccentColor.ToArgb(), Sample(before, bounds, 120f, bounds.Height / 2f).ToArgb(),
                "the old value covers the span that is about to become empty");
        }

        slider.Value = 20;
        using var after = Render(surface);

        Assert.AreEqual(TrackColor.ToArgb(), Sample(after, bounds, 120f, bounds.Height / 2f).ToArgb(),
            "the narrowed damage clears the part the fill no longer covers");
        Assert.AreEqual(AccentColor.ToArgb(), Sample(after, bounds, 30f, bounds.Height / 2f).ToArgb(),
            "the narrowed damage still paints the newly covered part");
    }

    [TestMethod]
    public void ProgressBarChrome_PaintsTrackAndFillFromTemplateBrushes()
    {
        using var surface = CreateSurface();
        using var bar = new VC.ProgressBar { Width = 200, Height = 10, Minimum = 0, Maximum = 100, Value = 25 };
        ApplyBrushes(bar);
        surface.Root = bar;
        Layout(surface);

        var bounds = bar.AbsoluteBounds;
        using var bitmap = Render(surface);

        Assert.AreEqual(TrackColor.ToArgb(), Sample(bitmap, bounds, 150f, bounds.Height / 2f).ToArgb(),
            "the uncovered part uses the track brush");
        Assert.AreEqual(AccentColor.ToArgb(), Sample(bitmap, bounds, 25f, bounds.Height / 2f).ToArgb(),
            "the covered part uses the fill brush");
    }

    [TestMethod]
    public void ProgressBarChrome_ValueChangeRepaintsTheOldFillSpan()
    {
        using var surface = CreateSurface();
        using var bar = new VC.ProgressBar { Width = 200, Height = 10, Minimum = 0, Maximum = 100, Value = 80 };
        ApplyBrushes(bar);
        surface.Root = bar;
        Layout(surface);
        Layout(surface);

        var bounds = bar.AbsoluteBounds;
        using (var before = Render(surface))
        {
            Assert.AreEqual(AccentColor.ToArgb(), Sample(before, bounds, 120f, bounds.Height / 2f).ToArgb());
        }

        bar.Value = 20;
        using var after = Render(surface);

        Assert.AreEqual(TrackColor.ToArgb(), Sample(after, bounds, 120f, bounds.Height / 2f).ToArgb(),
            "the old fill span is cleared");
        Assert.AreEqual(AccentColor.ToArgb(), Sample(after, bounds, 30f, bounds.Height / 2f).ToArgb(),
            "the new fill span is painted");
    }

    [TestMethod]
    public void SliderChrome_ReplacingAFullAreaTemplateClearsTheOldPixels()
    {
        using var surface = CreateSurface();
        var fullRed = new VisualControlTemplate(_ => new VC.Border
        {
            Padding = default,
            Background = Color.Red,
        })
        {
            TargetType = typeof(VC.Slider),
        };

        using var slider = new VC.Slider { Width = 200, Height = 26, Minimum = 0, Maximum = 100, Value = 50 };
        slider.Template = fullRed;
        surface.Root = slider;
        Layout(surface);
        Layout(surface);
        var bounds = slider.AbsoluteBounds;
        using (var before = Render(surface))
        {
            Assert.IsGreaterThan(200, Sample(before, bounds, 100f, 1f).R, "the custom template paints the full height");
        }

        slider.Template = null;
        Layout(surface);
        using var after = Render(surface);
        Assert.IsLessThan(100, Sample(after, bounds, 100f, 1f).R,
            "switching to the built-in chrome clears the full-area pixels");
    }

    private static VisualHost CreateSurface()
        => new() { Size = new Size(320, 240), DpiScaleOverride = 1f };

    // 角色刷现在来自控件 DP，不再是 SurfaceAlt/Accent/Focus 资源键。
    private static void ApplyBrushes(VC.Slider slider)
    {
        slider.TrackBrush = new SolidColorBrush(TrackColor);
        slider.FillBrush = new SolidColorBrush(AccentColor);
        slider.ThumbBrush = new SolidColorBrush(AccentColor);
        slider.FocusRingBrush = new SolidColorBrush(FocusColor);
    }

    private static void ApplyBrushes(VC.ProgressBar bar)
    {
        bar.TrackBrush = new SolidColorBrush(TrackColor);
        bar.FillBrush = new SolidColorBrush(AccentColor);
    }

    private static void Layout(VisualHost surface)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, surface.Size);
    }

    private static Bitmap Render(VisualHost surface)
    {
        var bitmap = new Bitmap(surface.Size.Width, surface.Size.Height);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, surface.Size);
        return bitmap;
    }

    private static Color Sample(Bitmap bitmap, RectangleF bounds, float localX, float localY)
        => bitmap.GetPixel(
            Math.Clamp((int)Math.Round(bounds.X + localX), 0, bitmap.Width - 1),
            Math.Clamp((int)Math.Round(bounds.Y + localY), 0, bitmap.Height - 1));
}
