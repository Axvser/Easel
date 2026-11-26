using System.Drawing;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// The input chrome polish: the input corner-radius palette metric, the ComboBox hover/active/focus ring roles,
/// the animated hover fade, and the two-pixel focus rings on the text inputs.
/// </summary>
[STATestClass]
public sealed class InputChromePolishTests
{
    [TestMethod]
    public void InputDefaults_UseTheInputCornerRadius()
    {
        var originalMode = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Light;

            using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
            using var combo = new VC.ComboBox { Width = 180 };
            using var textBox = new VC.TextBox { Width = 180 };
            using var richTextBox = new VC.RichTextBox { Width = 180 };
            using var datePicker = new VC.DatePicker { Width = 180 };
            combo.Items.Add("One");
            combo.SelectedIndex = 0;

            var panel = new VC.StackPanel();
            panel.Children.Add(combo);
            panel.Children.Add(textBox);
            panel.Children.Add(richTextBox);
            panel.Children.Add(datePicker);
            surface.Root = panel;
            Layout(surface, 320, 240);

            Assert.AreEqual(6f, combo.CornerRadius, "the light input radius seeds the combo box");
            Assert.AreEqual(6f, textBox.CornerRadius, "the light input radius seeds the text box");
            Assert.AreEqual(6f, richTextBox.CornerRadius, "the light input radius seeds the rich text box");
            Assert.AreEqual(6f, datePicker.CornerRadius, "the light input radius seeds the date picker");

            Theme.Mode = ThemeMode.HighContrast;

            Assert.AreEqual(2f, combo.CornerRadius, "an existing combo box takes the high-contrast input radius");
            Assert.AreEqual(2f, textBox.CornerRadius, "an existing text box takes the high-contrast input radius");
            Assert.AreEqual(2f, richTextBox.CornerRadius, "an existing rich text box takes the high-contrast input radius");
            Assert.AreEqual(2f, datePicker.CornerRadius, "an existing date picker takes the high-contrast input radius");

            Assert.AreEqual(6f, Theme.CreateLight().InputCornerRadius,
                "the light palette carries the input corner radius");
            Assert.AreEqual(2f, Theme.CreateHighContrast().InputCornerRadius,
                "the high-contrast palette carries the input corner radius");
        }
        finally
        {
            RestoreMode(originalMode);
        }
    }

    [TestMethod]
    public void ComboBox_RingsUseTheNewRoles()
    {
        var originalMode = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Light;

            using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
            using var combo = new VC.ComboBox { Width = 180 };
            combo.Items.Add("One");
            combo.Items.Add("Two");
            combo.SelectedIndex = 0;
            surface.Root = combo;
            Layout(surface, 320, 240);

            var hoverRing = combo.GetTemplateChild<VC.Border>("HoverRing");
            var activeRing = combo.GetTemplateChild<VC.Border>("ActiveRing");
            var focusRing = combo.GetTemplateChild<VC.Border>("FocusRing");
            Assert.IsNotNull(hoverRing, "the default template carries the hover ring");
            Assert.IsNotNull(activeRing, "the default template carries the active ring");
            Assert.IsNotNull(focusRing, "the default template carries the focus ring");

            var light = Theme.CreateLight();
            Assert.AreEqual(light.ControlBorderHover.ToArgb(), hoverRing!.BorderBrush!.ApproximateColor.ToArgb(),
                "the hover ring uses the hover border token");
            Assert.AreEqual(light.FocusRing.ToArgb(), activeRing!.BorderBrush!.ApproximateColor.ToArgb(),
                "the active ring uses the focus colour");
            Assert.AreEqual(2f, focusRing!.BorderThickness.Left, "the focus ring is two pixels");
            Assert.AreEqual(0d, hoverRing.Opacity, 0.001d, "the hover ring rests hidden");
            Assert.AreEqual(0d, activeRing.Opacity, 0.001d, "the active ring rests hidden");
            Assert.AreEqual(0d, focusRing.Opacity, 0.001d, "the focus ring rests hidden");
        }
        finally
        {
            RestoreMode(originalMode);
        }
    }

    [TestMethod]
    public void ComboBox_HoverRingFadesThroughTheTransition()
    {
        var originalMode = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Light;

            using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
            using var combo = new VC.ComboBox { Width = 180 };
            combo.Items.Add("One");
            combo.SelectedIndex = 0;
            surface.Root = combo;
            Layout(surface, 320, 240);

            var hoverRing = combo.GetTemplateChild<VC.Border>("HoverRing");
            Assert.IsNotNull(hoverRing, "the default template carries the hover ring");

            combo.SetValue(VC.ComboBox.IsPointerOverProperty, true);
            WaitForTransition(hoverRing!);

            Assert.AreEqual(1d, hoverRing!.Opacity, 0.001d, "hovering fades the hover ring in");

            combo.SetValue(VC.ComboBox.IsPointerOverProperty, false);
            WaitForTransition(hoverRing);

            Assert.AreEqual(0d, hoverRing.Opacity, 0.001d, "leaving fades the hover ring out");
        }
        finally
        {
            RestoreMode(originalMode);
        }
    }

    [TestMethod]
    public void ComboBox_HoverRingRetargetsWhenThePointerLeavesMidway()
    {
        var originalMode = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Light;

            using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
            using var combo = new VC.ComboBox { Width = 180 };
            combo.Items.Add("One");
            combo.SelectedIndex = 0;
            surface.Root = combo;
            Layout(surface, 320, 240);

            var hoverRing = combo.GetTemplateChild<VC.Border>("HoverRing");
            Assert.IsNotNull(hoverRing, "the default template carries the hover ring");

            combo.SetValue(VC.ComboBox.IsPointerOverProperty, true);
            Thread.Sleep(40);
            var midway = hoverRing!.Opacity;
            Assert.IsTrue(VisualTransition.HasActive(hoverRing), "the hover fade is in flight");

            combo.SetValue(VC.ComboBox.IsPointerOverProperty, false);
            Thread.Sleep(40);
            Assert.IsTrue(
                VisualTransition.HasActive(hoverRing),
                "the trigger exit retargets the hover ring instead of being masked by the animation layer");
            Assert.IsTrue(hoverRing.Opacity < midway, "the retarget reverses from the current frame");

            WaitForTransition(hoverRing);
            Assert.AreEqual(0d, hoverRing.Opacity, 0.001d, "the hover ring ends at its resting opacity");
        }
        finally
        {
            RestoreMode(originalMode);
        }
    }

    [TestMethod]
    public void InputFocusRings_AreTwoPixels()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var textBox = new VC.TextBox { Width = 180 };
        using var richTextBox = new VC.RichTextBox { Width = 180 };
        using var datePicker = new VC.DatePicker { Width = 180 };

        var panel = new VC.StackPanel();
        panel.Children.Add(textBox);
        panel.Children.Add(richTextBox);
        panel.Children.Add(datePicker);
        surface.Root = panel;
        Layout(surface, 320, 240);

        var textRing = textBox.GetTemplateChild<VC.Border>("FocusRing");
        var richRing = richTextBox.GetTemplateChild<VC.Border>("FocusRing");
        var dateRing = datePicker.GetTemplateChild<VC.Border>("FocusRing");
        Assert.IsNotNull(textRing, "the text box template carries the focus ring");
        Assert.IsNotNull(richRing, "the rich text box template carries the focus ring");
        Assert.IsNotNull(dateRing, "the date picker template carries the focus ring");
        Assert.AreEqual(2f, textRing!.BorderThickness.Left, "the text box focus ring is two pixels");
        Assert.AreEqual(2f, richRing!.BorderThickness.Left, "the rich text box focus ring is two pixels");
        Assert.AreEqual(2f, dateRing!.BorderThickness.Left, "the date picker focus ring is two pixels");
    }

    // 触发器的过渡在属性变更时同步启动；HasActive 变 false 时动画已把终值落定。
    private static void WaitForTransition(VisualElement target)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (VisualTransition.HasActive(target) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }
    }

    // 先切到另一个内置模式，保证回到原模式时 Theme.Mode 的 setter 真的换回内置调色板。
    private static void RestoreMode(ThemeMode mode)
    {
        Theme.Mode = mode == ThemeMode.Light ? ThemeMode.Dark : ThemeMode.Light;
        Theme.Mode = mode;
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        surface.Size = new Size(width, height);
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
