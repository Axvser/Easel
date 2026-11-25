using System.Drawing;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>The TabItem container seam and the state triggers of its built-in header template.</summary>
[STATestClass]
public sealed class TabItemDefaultsTests
{
    [TestMethod]
    public void TabItemContainerStyle_IsTheExactTypeDefaultAndExposesStateParts()
    {
        using var surface = new VisualHost { Size = new Size(420, 240), DpiScaleOverride = 1f };
        var first = new VC.TabItem { Header = "One" };
        var second = new VC.TabItem { Header = "Two" };
        var tabs = new VC.TabControl { SelectedIndex = 0 };
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        tabs.Refresh();
        surface.Root = tabs;
        Layout(surface, 420, 240);

        Assert.IsNotNull(first.TemplateRoot, "the exact-type TabItem default style supplies the header template");
        Assert.IsNotNull(first.GetTemplateChild<VC.Border>("HoverBand"), "the hover band is a template part");
        Assert.IsNotNull(first.GetTemplateChild<VC.Border>("SelectionFill"), "the selection fill is a template part");
        Assert.IsNotNull(first.GetTemplateChild<VC.Border>("SelectionLine"), "the accent line is a template part");
        Assert.IsNotNull(first.GetTemplateChild<VC.Border>("FocusRing"), "the focus ring is a template part");

        var derived = new DerivedTabItem { Header = "Derived" };
        surface.Root = derived;
        Layout(surface, 420, 240);

        Assert.IsNull(derived.TemplateRoot,
            "the default matches TabItem exactly; a derived type needs its own style or BasedOn");
    }

    [TestMethod]
    public void TabItemContainerStyle_CanBeReplacedThroughTheHostStylesSeam()
    {
        using var surface = new VisualHost { Size = new Size(420, 240), DpiScaleOverride = 1f };
        var first = new VC.TabItem { Header = "One" };
        var tabs = new VC.TabControl { SelectedIndex = 0 };
        tabs.Items.Add(first);
        tabs.Refresh();
        surface.Root = tabs;
        Layout(surface, 420, 240);

        Assert.IsNotNull(first.GetTemplateChild<VC.Border>("SelectionLine"), "the default template is applied first");

        tabs.Styles.Add(new VisualStyle
        {
            TargetType = typeof(VC.TabItem),
            Template = new VisualControlTemplate(_ => new VC.Border()),
        });
        Layout(surface, 420, 240);

        Assert.IsInstanceOfType<VC.Border>(first.TemplateRoot,
            "a host style for TabItem replaces the header template");
        Assert.IsNull(first.GetTemplateChild<VC.Border>("SelectionLine"),
            "the replaced template no longer exposes the default state parts");
    }

    [TestMethod]
    public void TabItemContainerStyle_TracksPointerOverThroughTheContainer()
    {
        using var surface = new VisualHost { Size = new Size(420, 240), DpiScaleOverride = 1f };
        var first = new VC.TabItem { Header = "One" };
        var second = new VC.TabItem { Header = "Two" };
        var tabs = new VC.TabControl { SelectedIndex = 0 };
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        tabs.Refresh();
        surface.Root = tabs;
        Layout(surface, 420, 240);

        surface.ProcessPointerMove(CenterOf(second));

        Assert.IsTrue(second.IsPointerOver, "the header container receives the hover state");
        Assert.AreEqual(1d, second.GetTemplateChild<VC.Border>("HoverBand")!.Opacity, 0.001d,
            "hovering reveals the hover band");
        Assert.AreEqual(0d, first.GetTemplateChild<VC.Border>("HoverBand")!.Opacity, 0.001d,
            "the other header keeps its hover band hidden");

        surface.ProcessPointerMove(new PointF(4f, 220f));

        Assert.IsFalse(second.IsPointerOver, "leaving the header clears the hover state");
        Assert.AreEqual(0d, second.GetTemplateChild<VC.Border>("HoverBand")!.Opacity, 0.001d);
    }

    [TestMethod]
    public void TabItemContainerStyle_ShowsTheFocusRingOnTheSelectedHeader()
    {
        using var surface = new VisualHost { Size = new Size(420, 240), DpiScaleOverride = 1f };
        var first = new VC.TabItem { Header = "One" };
        var second = new VC.TabItem { Header = "Two" };
        var tabs = new VC.TabControl { SelectedIndex = 0 };
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        tabs.Refresh();
        surface.Root = tabs;
        Layout(surface, 420, 240);

        Assert.IsTrue(surface.FocusElement(tabs), "the tab control is the focusable owner");
        surface.ProcessKeyDown(Keys.Right);

        Assert.AreEqual(1, tabs.SelectedIndex);
        Assert.IsTrue(second.IsFocusVisible, "the selected header receives the keyboard focus state");
        Assert.AreEqual(1d, second.GetTemplateChild<VC.Border>("FocusRing")!.Opacity, 0.001d,
            "the selected header shows the focus ring");
        Assert.AreEqual(0d, first.GetTemplateChild<VC.Border>("FocusRing")!.Opacity, 0.001d);

        surface.ProcessKeyDown(Keys.Left);

        Assert.AreEqual(0, tabs.SelectedIndex);
        Assert.AreEqual(1d, first.GetTemplateChild<VC.Border>("FocusRing")!.Opacity, 0.001d,
            "the focus ring follows the selection");
        Assert.AreEqual(0d, second.GetTemplateChild<VC.Border>("FocusRing")!.Opacity, 0.001d);

        surface.FocusElement(null);

        Assert.AreEqual(0d, first.GetTemplateChild<VC.Border>("FocusRing")!.Opacity, 0.001d,
            "losing keyboard focus clears the ring");
    }

    [TestMethod]
    public void TabItemContainerStyle_DimsWhenTheContainerIsDisabled()
    {
        using var surface = new VisualHost { Size = new Size(420, 240), DpiScaleOverride = 1f };
        var first = new VC.TabItem { Header = "One" };
        var second = new VC.TabItem { Header = "Two" };
        var tabs = new VC.TabControl { SelectedIndex = 0 };
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        tabs.Refresh();
        surface.Root = tabs;
        Layout(surface, 420, 240);

        Assert.AreEqual(1d, second.Opacity, 0.001d, "an enabled header stays fully opaque");

        second.IsEnabled = false;

        Assert.AreEqual(0.55d, second.Opacity, 0.001d,
            "a disabled container dims through its own template trigger");
        Assert.AreEqual(1d, first.Opacity, 0.001d, "the other header is unaffected");
    }

    private sealed class DerivedTabItem : VC.TabItem
    {
    }

    private static PointF CenterOf(VisualElement element)
    {
        var bounds = element.AbsoluteBounds;
        return new PointF(bounds.X + (bounds.Width / 2f), bounds.Y + (bounds.Height / 2f));
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
