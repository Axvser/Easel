using System.Drawing;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;
using XamlVisibility = Easel.Controls.Visibility;

namespace Easel.Test;

/// <summary>The container family's built-in templates and their part contracts.</summary>
[STATestClass]
public sealed class ContainerDefaultsTests
{
    [TestMethod]
    public void ExpanderDefaultTemplate_HostsContentAndTogglesFromTheHeader()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        var content = new VC.TextBlock { Text = "Body", Height = 60d };
        var expander = new VC.Expander { Width = 240d, Header = "More", Content = content };
        surface.Root = expander;
        Layout(surface, 320, 240);

        var header = expander.GetTemplateChild<VC.Border>("PART_Header");
        var host = expander.GetTemplateChild<VC.Grid>("PART_ContentHost");
        var toggle = expander.GetTemplateChild<VC.TextBlock>("PART_Toggle");
        Assert.IsNotNull(expander.TemplateRoot, "the Expander implicit style supplies the default template");
        Assert.IsNotNull(header, "PART_Header resolves through the default template");
        Assert.IsNotNull(host, "PART_ContentHost resolves through the default template");
        Assert.IsNotNull(toggle, "PART_Toggle resolves through the default template");
        Assert.AreSame(host, content.ParentElement, "content is parented by the template part");
        Assert.AreEqual("▾", toggle!.Text, "the toggle part shows the expanded glyph");
        Assert.IsFalse(header!.IsHitTestVisible, "the decorative header does not steal the hit");
        Assert.IsFalse(host!.IsHitTestVisible, "the content host itself is decorative");
        Assert.AreEqual(XamlVisibility.Visible, content.Visibility);

        var expandedHeight = expander.DesiredSize.Height;
        var bounds = expander.AbsoluteBounds;
        Click(surface, new PointF(bounds.X + 20f, bounds.Y + 10f));

        Assert.IsFalse(expander.IsExpanded, "a header click collapses the expander");
        Assert.IsFalse(content.IsVisible, "the collapsed content is hidden");
        Assert.AreEqual("▸", toggle.Text, "the toggle part follows the collapsed state");
        Layout(surface, 320, 240);
        Assert.IsLessThan(expandedHeight, expander.DesiredSize.Height, "the collapsed expander measures without the page");

        surface.FocusElement(expander);
        surface.ProcessKeyDown(Keys.Space);

        Assert.IsTrue(expander.IsExpanded, "the keyboard toggles the expander back open");
        Assert.IsTrue(content.IsVisible);
    }

    [TestMethod]
    public void GroupBoxDefaultTemplate_HostsTheChildAndLeavesItInteractive()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        var button = new VC.Button { Text = "Act", Width = 90d, Height = 28d };
        var box = new VC.GroupBox { Width = 240d, Header = "Settings", Child = button };
        surface.Root = box;
        Layout(surface, 320, 240);

        var header = box.GetTemplateChild<VC.Border>("PART_Header");
        var host = box.GetTemplateChild<VC.StackPanel>("PART_ContentHost");
        Assert.IsNotNull(box.TemplateRoot, "the GroupBox implicit style supplies the default template");
        Assert.IsNotNull(header, "PART_Header resolves through the default template");
        Assert.IsNotNull(host, "PART_ContentHost resolves through the default template");
        Assert.AreSame(host, button.ParentElement, "the child is parented by the content part");
        Assert.IsFalse(box.IsHitTestVisible, "the frame itself stays pass-through");
        Assert.IsGreaterThanOrEqualTo(10f, button.AbsoluteBounds.X - box.AbsoluteBounds.X);
        Assert.IsGreaterThanOrEqualTo(24f, button.AbsoluteBounds.Y - box.AbsoluteBounds.Y);

        var center = new PointF(
            button.AbsoluteBounds.X + (button.AbsoluteBounds.Width / 2f),
            button.AbsoluteBounds.Y + (button.AbsoluteBounds.Height / 2f));
        surface.ProcessPointerDown(center, MouseButtons.Left);

        Assert.IsTrue(button.IsPressed, "an interactive child inside the decorative frame still receives the press");
        surface.ProcessPointerUp(center, MouseButtons.Left);

        var replacement = new VC.TextBlock { Text = "Replaced" };
        box.Child = replacement;
        Layout(surface, 320, 240);

        Assert.AreSame(host, replacement.ParentElement, "a later child moves into the same content part");
        Assert.IsNull(button.ParentElement, "the previous child leaves the tree");
    }

    [TestMethod]
    public void TabControlDefaultTemplate_RendersHeadersFromItemsAndSwitchesPages()
    {
        using var surface = new VisualHost { Size = new Size(420, 240), DpiScaleOverride = 1f };
        var firstContent = new VC.TextBlock { Text = "First" };
        var secondContent = new VC.TextBlock { Text = "Second" };
        var first = new VC.TabItem { Header = "One", Content = firstContent };
        var second = new VC.TabItem { Header = "Two", Content = secondContent };
        var tabs = new VC.TabControl { SelectedIndex = 0 };
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        tabs.Refresh();
        surface.Root = tabs;
        Layout(surface, 420, 240);

        var headerHost = tabs.GetTemplateChild<VC.StackPanel>("PART_HeaderHost");
        Assert.IsNotNull(tabs.TemplateRoot, "the TabControl implicit style supplies the default template");
        Assert.IsNotNull(headerHost, "PART_HeaderHost resolves through the default template");
        CollectionAssert.Contains(headerHost!.Children.ToArray(), first, "the first tab item is hosted as a header");
        CollectionAssert.Contains(headerHost.Children.ToArray(), second, "the second tab item is hosted as a header");
        Assert.IsNotNull(first.TemplateRoot, "each TabItem gets its own default template");
        Assert.IsTrue(first.IsSelected, "the selected index marks the first item");
        Assert.IsFalse(second.IsSelected);
        Assert.AreSame(tabs, firstContent.ParentElement, "the selected page stays a direct child of the tab control");
        Assert.IsNull(secondContent.ParentElement);
        Assert.IsGreaterThanOrEqualTo(32f, firstContent.AbsoluteBounds.Y - tabs.AbsoluteBounds.Y,
            "the page is arranged below the tab strip");

        var secondBounds = second.AbsoluteBounds;
        Click(surface, new PointF(
            secondBounds.X + (secondBounds.Width / 2f),
            secondBounds.Y + (secondBounds.Height / 2f)));

        Assert.AreEqual(1, tabs.SelectedIndex, "clicking a tab header selects that page");
        Assert.IsTrue(second.IsSelected);
        Assert.IsFalse(first.IsSelected);
        Assert.AreSame(tabs, secondContent.ParentElement);
        Assert.IsNull(firstContent.ParentElement);

        surface.FocusElement(tabs);
        surface.ProcessKeyDown(Keys.Left);

        Assert.AreEqual(0, tabs.SelectedIndex, "the keyboard moves the selection back");
        Assert.AreSame(tabs, firstContent.ParentElement);
        Assert.IsNull(secondContent.ParentElement);
    }

    [TestMethod]
    public void TabItemDefaultTemplate_ExposesSelectionStateThroughPartOpacity()
    {
        using var surface = new VisualHost { Size = new Size(420, 240), DpiScaleOverride = 1f };
        var first = new VC.TabItem { Header = "One" };
        var second = new VC.TabItem { Header = "Two" };
        var tabs = new VC.TabControl { SelectedIndex = 0 };
        tabs.Items.Add(first);
        tabs.Items.Add(second);
        surface.Root = tabs;
        Layout(surface, 420, 240);

        Assert.AreEqual(1d, first.GetTemplateChild<VC.Border>("SelectionLine")!.Opacity, 0.001d,
            "the selected tab shows the accent line");
        Assert.AreEqual(1d, first.GetTemplateChild<VC.Border>("SelectionFill")!.Opacity, 0.001d,
            "the selected tab shows the raised fill");
        Assert.AreEqual(1d, first.GetTemplateChild<VC.TextBlock>("SelectedLabel")!.Opacity, 0.001d);
        Assert.AreEqual(0d, first.GetTemplateChild<VC.TextBlock>("MutedLabel")!.Opacity, 0.001d);

        Assert.AreEqual(0d, second.GetTemplateChild<VC.Border>("SelectionLine")!.Opacity, 0.001d,
            "the unselected tab hides the accent line");
        Assert.AreEqual(0d, second.GetTemplateChild<VC.Border>("SelectionFill")!.Opacity, 0.001d);
        Assert.AreEqual(0d, second.GetTemplateChild<VC.TextBlock>("SelectedLabel")!.Opacity, 0.001d);
        Assert.AreEqual(0.65d, second.GetTemplateChild<VC.TextBlock>("MutedLabel")!.Opacity, 0.001d);

        tabs.SelectedIndex = 1;

        Assert.AreEqual(0d, first.GetTemplateChild<VC.Border>("SelectionLine")!.Opacity, 0.001d,
            "changing the selection clears the previous tab");
        Assert.AreEqual(1d, second.GetTemplateChild<VC.Border>("SelectionLine")!.Opacity, 0.001d);
    }

    [TestMethod]
    public void ContainerDefaults_ReadPaletteRolesThroughDynamicResources()
    {
        var originalMode = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;
            var palette = Theme.CreateDark();

            using var surface = new VisualHost { Size = new Size(420, 240), DpiScaleOverride = 1f };
            var expander = new VC.Expander { Width = 240d, Header = "More" };
            surface.Root = expander;
            Layout(surface, 420, 240);

            var header = expander.GetTemplateChild<VC.Border>("PART_Header")!;
            Assert.AreEqual(palette.SurfaceAlt.ToArgb(), header.Background!.ApproximateColor.ToArgb(),
                "the header background reads the Raised palette key");
            Assert.AreEqual(palette.ControlBorder.ToArgb(), header.BorderBrush!.ApproximateColor.ToArgb(),
                "the header border reads the Border palette key");

            var selected = new VC.TabItem { Header = "One" };
            var tabs = new VC.TabControl { SelectedIndex = 0 };
            tabs.Items.Add(selected);
            surface.Root = tabs;
            Layout(surface, 420, 240);

            var line = selected.GetTemplateChild<VC.Border>("SelectionLine")!;
            Assert.AreEqual(palette.Accent.ToArgb(), line.Background!.ApproximateColor.ToArgb(),
                "the selected tab accent reads the Accent palette key");
        }
        finally
        {
            Theme.Mode = originalMode;
        }
    }

    private static void Click(VisualHost surface, PointF point)
    {
        surface.ProcessPointerDown(point, MouseButtons.Left);
        surface.ProcessPointerUp(point, MouseButtons.Left);
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
