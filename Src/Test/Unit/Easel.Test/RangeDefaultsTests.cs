using System.Drawing;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;
using XamlHorizontalAlignment = Easel.Controls.HorizontalAlignment;
using XamlVerticalAlignment = Easel.Controls.VerticalAlignment;

namespace Easel.Test;

/// <summary>The built-in Slider and ProgressBar templates keep their parts and interaction contract.</summary>
[STATestClass]
public sealed class RangeDefaultsTests
{
    [TestMethod]
    public void SliderDefaultTemplate_ExposesTrackFillThumbAndFocusParts()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var slider = new VC.Slider { Width = 200, Minimum = 0, Maximum = 100, Value = 50 };
        surface.Root = slider;
        Layout(surface, 320, 240);

        Assert.IsNotNull(slider.TemplateRoot, "the implicit Slider style supplies the built-in template");
        var track = slider.GetTemplateChild<VisualElement>("PART_Track");
        var fill = slider.GetTemplateChild<VisualElement>("PART_Fill");
        var thumb = slider.GetTemplateChild<VisualElement>("PART_Thumb");
        var ring = slider.GetTemplateChild<VisualElement>("PART_FocusRing");
        Assert.IsNotNull(track);
        Assert.IsNotNull(fill);
        Assert.IsNotNull(thumb);
        Assert.IsNotNull(ring);

        Assert.IsFalse(track!.IsHitTestVisible, "the track is decorative");
        Assert.IsFalse(fill!.IsHitTestVisible, "the fill is decorative");
        Assert.IsFalse(thumb!.IsHitTestVisible, "the thumb is decorative");
        Assert.IsFalse(ring!.IsHitTestVisible, "the focus ring is decorative");

        Assert.AreEqual(186f, track.Bounds.Width, 0.75f, "the track is inset by the thumb radius on both ends");
        Assert.AreEqual(track.Bounds.Width * 0.5f, fill.Bounds.Width, 0.75f, "the fill covers half the track");
        Assert.AreEqual(track.Bounds.Height, fill.Bounds.Height, 0.01f, "the fill matches the track height");
        Assert.AreEqual(
            track.Bounds.X + (track.Bounds.Width / 2f),
            thumb.Bounds.X + (thumb.Bounds.Width / 2f),
            0.75f,
            "the thumb sits at the value along the track");
        Assert.AreEqual(0d, ring.Opacity, 0.01d, "the focus ring stays hidden without keyboard focus");
    }

    [TestMethod]
    public void SliderDefaultTemplate_KeepsPointerDragAndKeyboardInteraction()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var slider = new VC.Slider { Width = 200, Minimum = 0, Maximum = 100 };
        surface.Root = slider;
        Layout(surface, 320, 240);

        var bounds = slider.AbsoluteBounds;
        var centerY = bounds.Y + (bounds.Height / 2f);
        var press = new PointF(bounds.X + (bounds.Width * 0.8f), centerY);
        Assert.AreSame(slider, surface.FindElementAt(press), "decorative parts do not steal the press");
        surface.ProcessPointerDown(press, MouseButtons.Left);
        surface.ProcessPointerUp(press, MouseButtons.Left);
        Assert.IsGreaterThan(70d, slider.Value);

        surface.ProcessPointerDown(new PointF(bounds.X + 20f, centerY), MouseButtons.Left);
        surface.ProcessPointerMove(new PointF(bounds.X + 160f, centerY));
        surface.ProcessPointerUp(new PointF(bounds.X + 160f, centerY), MouseButtons.Left);
        Assert.IsGreaterThan(70d, slider.Value, "dragging moves the value with the pointer");

        Assert.IsTrue(slider.Focus());
        surface.ProcessKeyDown(Keys.Home);
        Assert.AreEqual(0d, slider.Value);
        surface.ProcessKeyDown(Keys.Right);
        Assert.IsGreaterThan(0d, slider.Value);
    }

    [TestMethod]
    public void SliderDefaultTemplate_ShowsTheFocusRingOnKeyboardFocus()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var slider = new VC.Slider { Width = 200, Minimum = 0, Maximum = 100, Value = 50 };
        surface.Root = slider;
        Layout(surface, 320, 240);

        var ring = slider.GetTemplateChild<VisualElement>("PART_FocusRing");
        Assert.IsNotNull(ring);

        var bounds = slider.AbsoluteBounds;
        var point = new PointF(bounds.X + 40f, bounds.Y + (bounds.Height / 2f));
        surface.ProcessPointerDown(point, MouseButtons.Left);
        surface.ProcessPointerUp(point, MouseButtons.Left);
        Assert.AreEqual(0d, ring!.Opacity, 0.01d, "pointer focus keeps the ring hidden");

        surface.ProcessKeyDown(Keys.Right);
        Assert.IsTrue(slider.IsFocusVisible, "keyboard input shows the focus-visible state");
        Assert.AreEqual(1d, ring.Opacity, 0.01d, "keyboard focus raises the ring through the template trigger");
    }

    [TestMethod]
    public void SliderCustomTemplate_ReplacesChromeAndStillArrangesTheParts()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        VC.Border? track = null;
        VC.Border? fill = null;
        VC.Border? thumb = null;
        var template = new VisualControlTemplate(scope =>
        {
            var root = new VC.Grid();
            track = new VC.Border
            {
                Height = 6f,
                Padding = default,
                Background = Color.DarkSlateGray,
                VerticalAlignment = XamlVerticalAlignment.Center,
            };
            fill = new VC.Border { Padding = default, Background = Color.Orange };
            thumb = new VC.Border
            {
                Width = 20f,
                Height = 20f,
                Padding = default,
                Background = Color.White,
                HorizontalAlignment = XamlHorizontalAlignment.Center,
                VerticalAlignment = XamlVerticalAlignment.Center,
            };
            root.Add(track);
            root.Add(fill);
            root.Add(thumb);
            scope.Register("PART_Track", track);
            scope.Register("PART_Fill", fill);
            scope.Register("PART_Thumb", thumb);
            return root;
        })
        {
            TargetType = typeof(VC.Slider),
        };

        using var slider = new VC.Slider { Width = 200, Minimum = 0, Maximum = 100, Value = 25, Template = template };
        surface.Root = slider;
        Layout(surface, 320, 240);

        Assert.AreSame(track, slider.GetTemplateChild<VC.Border>("PART_Track"), "the custom chrome replaces the default one");
        Assert.AreSame(fill, slider.GetTemplateChild<VC.Border>("PART_Fill"));
        Assert.AreSame(thumb, slider.GetTemplateChild<VC.Border>("PART_Thumb"));
        Assert.AreEqual(Color.Orange.ToArgb(), fill!.Background!.ApproximateColor.ToArgb(),
            "the fill paints the template's brush");
        Assert.AreEqual(20f, thumb!.Bounds.Width, 0.01f, "the template owns the thumb size");
        Assert.AreEqual(200f * 0.25f, fill.Bounds.Width, 0.75f, "the control still places the fill by value");

        var bounds = slider.AbsoluteBounds;
        var press = new PointF(bounds.X + 150f, bounds.Y + (bounds.Height / 2f));
        surface.ProcessPointerDown(press, MouseButtons.Left);
        surface.ProcessPointerUp(press, MouseButtons.Left);
        Assert.IsGreaterThan(70d, slider.Value, "pointer math still runs against the custom track");
    }

    [TestMethod]
    public void ProgressBarDefaultTemplate_FillsTheFractionOnEveryArrange()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var bar = new VC.ProgressBar { Width = 200, Height = 10, Minimum = 0, Maximum = 100, Value = 25 };
        surface.Root = bar;
        Layout(surface, 320, 240);

        Assert.IsFalse(bar.IsHitTestVisible, "a progress bar never takes pointer input");
        Assert.IsNotNull(bar.TemplateRoot, "the implicit ProgressBar style supplies the built-in template");
        var track = bar.GetTemplateChild<VisualElement>("PART_Track");
        var fill = bar.GetTemplateChild<VisualElement>("PART_Fill");
        Assert.IsNotNull(track);
        Assert.IsNotNull(fill);
        Assert.IsFalse(track!.IsHitTestVisible);
        Assert.IsFalse(fill!.IsHitTestVisible);
        Assert.AreEqual(200f, track.Bounds.Width, 0.75f);
        Assert.AreEqual(50f, fill.Bounds.Width, 0.75f);

        bar.Value = 75;
        Layout(surface, 320, 240);

        Assert.AreEqual(150f, fill.Bounds.Width, 0.75f, "a new value re-arranges the fill");
    }

    [TestMethod]
    public void RangeDefaults_BrushesComeFromTheHostPalette()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        var trackColor = Color.FromArgb(0xFF, 0x12, 0x34, 0x56);
        var accentColor = Color.FromArgb(0xFF, 0x65, 0x43, 0x21);
        surface.Resources["SurfaceAlt"] = new SolidColorBrush(trackColor);
        surface.Resources["Accent"] = new SolidColorBrush(accentColor);

        using var slider = new VC.Slider { Width = 200, Minimum = 0, Maximum = 100, Value = 50 };
        surface.Root = slider;
        Layout(surface, 320, 240);

        var track = slider.GetTemplateChild<VisualElement>("PART_Track");
        var fill = slider.GetTemplateChild<VisualElement>("PART_Fill");
        Assert.IsNotNull(track);
        Assert.IsNotNull(fill);
        Assert.AreEqual(trackColor.ToArgb(), track!.Background!.ApproximateColor.ToArgb());
        Assert.AreEqual(accentColor.ToArgb(), fill!.Background!.ApproximateColor.ToArgb());
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
