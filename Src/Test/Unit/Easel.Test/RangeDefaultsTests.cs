using System.Drawing;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;
using XamlHorizontalAlignment = Easel.Controls.HorizontalAlignment;
using XamlVerticalAlignment = Easel.Controls.VerticalAlignment;

namespace Easel.Test;

/// <summary>The built-in Slider and ProgressBar templates keep their chrome and interaction contract.</summary>
[STATestClass]
public sealed class RangeDefaultsTests
{
    [TestMethod]
    public void SliderDefaultTemplate_UsesOneChromeInsteadOfSeparateParts()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var slider = new VC.Slider { Width = 200, Minimum = 0, Maximum = 100, Value = 50 };
        surface.Root = slider;
        Layout(surface, 320, 240);

        var chrome = slider.TemplateRoot as VC.SliderChrome;
        Assert.IsNotNull(chrome, "the implicit Slider style supplies the built-in chrome");
        Assert.AreEqual(0, chrome!.Children.Count, "the chrome paints track, fill, thumb and ring itself");
        Assert.IsNull(slider.GetTemplateChild<VisualElement>("PART_Track"), "no separate part visuals are created");
        Assert.IsNull(slider.GetTemplateChild<VisualElement>("PART_Fill"));
        Assert.IsNull(slider.GetTemplateChild<VisualElement>("PART_Thumb"));
        Assert.IsNull(slider.GetTemplateChild<VisualElement>("PART_FocusRing"));
        Assert.IsFalse(chrome.IsHitTestVisible, "the chrome is decorative");
        Assert.AreEqual(0.5d, chrome.Fraction, 0.001d, "the chrome is told the current fraction");
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
        Assert.AreSame(slider, surface.FindElementAt(press), "the chrome does not steal the press");
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

        var chrome = slider.GetTemplateChild<VC.SliderChrome>("PART_Chrome");
        Assert.IsNotNull(chrome);
        Assert.IsFalse(chrome!.ShowFocusRing, "the ring stays hidden without keyboard focus");

        var bounds = slider.AbsoluteBounds;
        var point = new PointF(bounds.X + 40f, bounds.Y + (bounds.Height / 2f));
        surface.ProcessPointerDown(point, MouseButtons.Left);
        surface.ProcessPointerUp(point, MouseButtons.Left);
        Assert.IsFalse(chrome.ShowFocusRing, "pointer focus keeps the ring hidden");

        surface.ProcessKeyDown(Keys.Right);
        Assert.IsTrue(slider.IsFocusVisible, "keyboard input shows the focus-visible state");
        Assert.IsTrue(chrome.ShowFocusRing, "keyboard focus raises the ring through the template binding");
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
        var chrome = bar.TemplateRoot as VC.ProgressBarChrome;
        Assert.IsNotNull(chrome, "the implicit ProgressBar style supplies the built-in chrome");
        Assert.AreEqual(0, chrome!.Children.Count);
        Assert.AreEqual(0.25d, chrome.Fraction, 0.001d, "the chrome is told the current fraction");
        Assert.IsNull(bar.GetTemplateChild<VisualElement>("PART_Track"), "no separate part visuals are created");
        Assert.IsNull(bar.GetTemplateChild<VisualElement>("PART_Fill"));

        bar.Value = 75;
        Assert.AreEqual(0.75d, chrome.Fraction, 0.001d, "a new value updates the chrome without another layout");

        Layout(surface, 320, 240);
        Assert.AreEqual(0.75d, chrome.Fraction, 0.001d, "layout keeps the same fraction");
    }

    [TestMethod]
    public void RangeDefaults_BrushesComeFromControlProperties()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        var trackColor = Color.FromArgb(0xFF, 0x12, 0x34, 0x56);
        var accentColor = Color.FromArgb(0xFF, 0x65, 0x43, 0x21);

        using var slider = new VC.Slider
        {
            Width = 200,
            Minimum = 0,
            Maximum = 100,
            Value = 50,
            TrackBrush = new SolidColorBrush(trackColor),
            FillBrush = new SolidColorBrush(accentColor),
            ThumbBrush = new SolidColorBrush(accentColor),
        };
        surface.Root = slider;
        Layout(surface, 320, 240);

        var chrome = slider.GetTemplateChild<VC.SliderChrome>("PART_Chrome");
        Assert.IsNotNull(chrome);
        Assert.AreEqual(trackColor.ToArgb(), chrome!.TrackBrush!.ApproximateColor.ToArgb());
        Assert.AreEqual(accentColor.ToArgb(), chrome.FillBrush!.ApproximateColor.ToArgb());
        Assert.AreEqual(accentColor.ToArgb(), chrome.ThumbBrush!.ApproximateColor.ToArgb());
    }

    [TestMethod]
    public void SliderValueChange_MovesTheChromeWithoutAnotherLayoutPass()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var slider = new VC.Slider { Width = 200, Minimum = 0, Maximum = 100, Value = 20 };
        surface.Root = slider;
        Layout(surface, 320, 240);

        var chrome = slider.GetTemplateChild<VC.SliderChrome>("PART_Chrome")!;
        var layouts = 0;
        surface.LayoutUpdated += (_, _) => layouts++;
        var paints = surface.PaintCount;
        var fraction = chrome.Fraction;

        slider.Value = 80;

        Assert.AreEqual(0, layouts, "the value change only updates the chrome, it does not run layout");
        Assert.IsGreaterThan(fraction, chrome.Fraction, "the chrome follows the value immediately");
        Assert.AreEqual(paints, surface.PaintCount, "nothing paints until the surface is rendered");

        Layout(surface, 320, 240);
        Assert.AreEqual(paints + 1, surface.PaintCount, "the value change still damages the surface");
        Assert.AreEqual(0, layouts, "rendering the value change still does not run layout");
    }

    [TestMethod]
    public void SliderFocusRing_IsDrawnAtTheCurrentValueWhenFocusBecomesVisible()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var slider = new VC.Slider { Width = 200, Minimum = 0, Maximum = 100, Value = 20 };
        surface.Root = slider;
        Layout(surface, 320, 240);

        var chrome = slider.GetTemplateChild<VC.SliderChrome>("PART_Chrome")!;
        Assert.IsFalse(chrome.ShowFocusRing);

        slider.Value = 75;
        Assert.IsTrue(slider.Focus());
        surface.ProcessKeyDown(Keys.Right);

        Assert.IsTrue(chrome.ShowFocusRing, "keyboard focus shows the ring");
        Assert.AreEqual(0.76d, chrome.Fraction, 0.01d, "the ring is drawn at the updated value, not a stale one");
    }

    [TestMethod]
    public void SliderTemplateReplacement_RearrangesAgainstTheNewParts()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        var track = new VC.Border { Height = 6f, Padding = default, Background = Color.Green, VerticalAlignment = XamlVerticalAlignment.Center };
        var fill = new VC.Border { Padding = default, Background = Color.Orange };
        var template = CreateTemplate(track, fill, thumb: null);

        using var slider = new VC.Slider { Width = 200, Minimum = 0, Maximum = 100, Value = 25 };
        surface.Root = slider;
        Layout(surface, 320, 240);
        Assert.IsNull(slider.GetTemplateChild<VC.Border>("PART_Fill"), "the built-in chrome is still active");

        slider.Template = template;
        Layout(surface, 320, 240);
        Assert.AreSame(fill, slider.GetTemplateChild<VC.Border>("PART_Fill"), "the replacement exposes its own fill");

        slider.Value = 60;
        Assert.AreEqual(track.Bounds.Width * 0.6f, fill.Bounds.Width, 0.75f,
            "the cached parts were discarded and the new fill is arranged by value");
    }

    [TestMethod]
    public void ProgressBarValueChange_UpdatesTheChromeWithoutAnotherLayoutPass()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var bar = new VC.ProgressBar { Width = 200, Height = 10, Minimum = 0, Maximum = 100, Value = 25 };
        surface.Root = bar;
        Layout(surface, 320, 240);

        var chrome = bar.GetTemplateChild<VC.ProgressBarChrome>("PART_Chrome")!;
        var layouts = 0;
        surface.LayoutUpdated += (_, _) => layouts++;

        bar.Value = 75;

        Assert.AreEqual(0, layouts, "the bar moves its fill without a layout pass");
        Assert.AreEqual(0.75d, chrome.Fraction, 0.001d, "the chrome follows the value immediately");
    }

    [TestMethod]
    public void SliderCustomTemplateWithoutATrackPart_KeepsTheFullWidthTravel()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        var fill = new VC.Border { Padding = default, Background = Color.Orange };
        using var slider = new VC.Slider { Width = 200, Minimum = 0, Maximum = 100, Value = 50 };
        slider.Template = CreateTemplate(track: null, fill, thumb: null);
        surface.Root = slider;
        Layout(surface, 320, 240);

        slider.Value = 80;

        Assert.AreEqual(186f * 0.8f, fill.Bounds.Width, 0.75f,
            "without PART_Track the control falls back to its own inset track as the travel");
    }

    private static VisualControlTemplate CreateTemplate(VC.Border? track, VC.Border fill, VC.Border? thumb)
    {
        return new VisualControlTemplate(scope =>
        {
            var root = new VC.Grid();
            if (track is not null)
            {
                root.Add(track);
                scope.Register("PART_Track", track);
            }

            root.Add(fill);
            scope.Register("PART_Fill", fill);

            if (thumb is not null)
            {
                root.Add(thumb);
                scope.Register("PART_Thumb", thumb);
            }

            return root;
        })
        {
            TargetType = typeof(VC.Slider),
        };
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
