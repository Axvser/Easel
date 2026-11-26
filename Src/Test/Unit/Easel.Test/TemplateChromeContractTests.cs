using System.Drawing;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;
using ShapeEllipse = Easel.Visuals.Controls.Shapes.Ellipse;
using ShapeRectangle = Easel.Visuals.Controls.Shapes.Rectangle;
using XamlThickness = Easel.Controls.Thickness;

namespace Easel.Test;

/// <summary>Default templates source their outer chrome from the control's appearance properties.</summary>
[STATestClass]
public sealed class TemplateChromeContractTests
{
    private static readonly Color ChromeBackground = Color.Red;
    private static readonly Color ChromeBorder = Color.Lime;

    [TestMethod]
    public void InputAndContainerTemplatesPaintTheControlChrome()
    {
        AssertChrome(new VC.Button { Text = "Button", Width = 140, Height = 36 });
        AssertChrome(new VC.TextBox { Text = "Text", Width = 180 });
        AssertChrome(new VC.ComboBox { Width = 180, SelectedIndex = 0 }, hasItems: true);
        AssertChrome(new VC.ListBox { Width = 180, Height = 100, ItemsSource = new[] { "One", "Two" } });
        AssertChrome(new VC.TreeView { Width = 180, Height = 100 }, hasTreeItem: true);
        AssertChrome(new VC.GroupBox { Header = "Group", Width = 220, Child = new VC.TextBlock { Text = "Body" } });
        AssertChrome(new VC.TabControl { Width = 240, Height = 120, SelectedIndex = 0 }, hasTab: true);
        AssertChrome(new VC.ScrollViewer
        {
            Width = 180,
            Height = 100,
            Content = new VC.TextBlock { Text = "Content" },
        });
        AssertChrome(new VC.Calendar { Width = 250, Height = 226 });
        AssertChrome(new VC.DatePicker { Width = 200 });
        AssertChrome(new VC.HeaderedContentControl
        {
            Header = "Head",
            Width = 180,
            Content = new VC.TextBlock { Text = "Body" },
        });
    }

    [TestMethod]
    public void ExpanderPaintsTheOuterChromeAroundTheHeader()
    {
        var expander = new VC.Expander
        {
            Header = "Head",
            IsExpanded = true,
            Width = 220,
            Content = new VC.TextBlock { Text = "Body" },
        };
        AssertChrome(expander, bottomBorder: true);
    }

    [TestMethod]
    public void TogglesPaintTheControlChromeOnTheGlyph()
    {
        var toggle = new VC.ToggleButton { Width = 56, Height = 28, CornerRadius = 9f };
        AssertPartChrome<VC.Border>(toggle, "Track", "ChromeBorder", 9f);

        var check = new VC.CheckBox { Width = 22, Height = 22, CornerRadius = 6f };
        AssertPartChrome<ShapeRectangle>(check, "Box", "ChromeBorder", 6f);

        var radio = new VC.RadioButton { Width = 22, Height = 22 };
        var radioSurface = new VisualHost { Size = new Size(120, 80), DpiScaleOverride = 1f };
        var radioRoot = new VC.StackPanel { Padding = new XamlThickness(20f) };
        ApplyChrome(radio);
        radioRoot.Add(radio);
        radioSurface.Root = radioRoot;
        Layout(radioSurface, 120, 80);

        var ring = radio.GetTemplateChild<ShapeEllipse>("Ring");
        Assert.IsNotNull(ring);
        Assert.AreEqual(ChromeBackground.ToArgb(), ring!.Fill!.ApproximateColor.ToArgb());
        Assert.AreEqual(ChromeBorder.ToArgb(), ring.Stroke!.ApproximateColor.ToArgb());
        Assert.AreEqual(2f, ring.StrokeThickness, 0.01f);
        radio.Dispose();
        radioRoot.Dispose();
        radioSurface.Dispose();
    }

    [TestMethod]
    public void HeaderRolesDriveTheHeaderParts()
    {
        var headerBrush = new SolidColorBrush(Color.Teal);
        var foreground = new SolidColorBrush(Color.Gold);
        var borderBrush = new SolidColorBrush(Color.Purple);

        using var surface = new VisualHost { Size = new Size(420, 360), DpiScaleOverride = 1f };
        var root = new VC.StackPanel { Spacing = 8, Padding = new XamlThickness(20f) };

        var expander = new VC.Expander
        {
            Header = "Head",
            IsExpanded = true,
            Width = 220,
            HeaderBackground = headerBrush,
            HeaderForeground = foreground,
            HeaderBorderBrush = borderBrush,
            HeaderBorderThickness = new XamlThickness(2f),
        };
        var group = new VC.GroupBox
        {
            Header = "Group",
            Width = 220,
            HeaderBackground = headerBrush,
            HeaderForeground = foreground,
            HeaderBorderBrush = borderBrush,
            HeaderBorderThickness = new XamlThickness(2f),
        };
        var tab = new VC.TabItem
        {
            Header = "Tab",
            HeaderForeground = foreground,
            SelectionLineBrush = borderBrush,
        };
        var titled = new VC.HeaderedContentControl
        {
            Header = "Head",
            Width = 220,
            HeaderForeground = foreground,
        };

        var tabs = new VC.TabControl { Width = 240, Height = 80, SelectedIndex = 0 };
        tabs.Items.Add(tab);

        root.Add(expander);
        root.Add(group);
        root.Add(titled);
        root.Add(tabs);
        surface.Root = root;
        Layout(surface, 420, 360);

        var expanderHeader = expander.GetTemplateChild<VC.Border>("PART_Header");
        Assert.IsNotNull(expanderHeader);
        Assert.AreEqual(Color.Teal.ToArgb(), expanderHeader!.Background!.ApproximateColor.ToArgb());
        Assert.AreEqual(Color.Purple.ToArgb(), expanderHeader.BorderBrush!.ApproximateColor.ToArgb());
        Assert.AreEqual(2f, expanderHeader.BorderThickness.Left, 0.01f);

        var groupHeader = group.GetTemplateChild<VC.Border>("PART_Header");
        Assert.IsNotNull(groupHeader);
        Assert.AreEqual(Color.Teal.ToArgb(), groupHeader!.Background!.ApproximateColor.ToArgb());

        var titledHeader = titled.GetTemplateChild<VC.TextBlock>("PART_Header");
        Assert.IsNotNull(titledHeader);
        Assert.AreEqual(Color.Gold.ToArgb(), titledHeader!.Foreground.ToArgb());

        Assert.AreEqual(14f, tab.Padding.Left, 0.01f,
            "the TabItem default style supplies the label padding");
        var label = tab.GetTemplateChild<VC.TextBlock>("SelectedLabel");
        Assert.IsNotNull(label);
        Assert.AreEqual(Color.Gold.ToArgb(), label!.Foreground.ToArgb());
        Assert.AreEqual(14f, label.Padding.Left, 0.01f, "the label reads the control Padding");
        var line = tab.GetTemplateChild<VC.Border>("SelectionLine");
        Assert.IsNotNull(line);
        Assert.AreEqual(Color.Purple.ToArgb(), line!.Background!.ApproximateColor.ToArgb());
    }

    [TestMethod]
    public void ComboBoxEditableAndReadOnlyShareOneHeight()
    {
        AssertComboHeight(new XamlThickness(10f, 6f));
        AssertComboHeight(new XamlThickness(12f, 8f));
    }

    [TestMethod]
    public void ComboBoxProjectedRowsFollowTheControlTextStyle()
    {
        using var surface = new VisualHost { Size = new Size(320, 240), DpiScaleOverride = 1f };
        using var combo = new VC.ComboBox
        {
            Width = 180,
            SelectedIndex = 0,
            Foreground = Color.Red,
            FontSize = 13d,
        };
        combo.Items.Add("One");
        combo.Items.Add("Two");
        surface.Root = combo;
        Layout(surface, 320, 240);
        combo.IsDropDownOpen = true;
        Layout(surface, 320, 240);

        var row = (VC.ListBoxItem)combo.DropDownList.Items[0];
        var text = (VC.TextBlock)row.Content!;
        Assert.AreEqual(Color.Red.ToArgb(), text.Foreground.ToArgb(), "the row takes the control Foreground");
        Assert.AreEqual(13d, text.FontSize, 0.01d, "the row takes the control font size");
    }

    private static void AssertComboHeight(XamlThickness padding)
    {
        using var surface = new VisualHost { Size = new Size(320, 200), DpiScaleOverride = 1f };
        var root = new VC.StackPanel { Spacing = 8, Padding = new XamlThickness(20f) };
        using var readOnly = new VC.ComboBox { Width = 200, Padding = padding, SelectedIndex = 0 };
        using var editable = new VC.ComboBox
        {
            Width = 200,
            Padding = padding,
            SelectedIndex = 0,
            IsEditable = true,
        };
        readOnly.Items.Add("One");
        editable.Items.Add("One");
        root.Add(readOnly);
        root.Add(editable);
        surface.Root = root;
        Layout(surface, 320, 200);

        Assert.AreEqual(readOnly.Bounds.Height, editable.Bounds.Height, 0.01f,
            "the editable editor absorbs the TextBox border allowance");
    }

    private static void AssertPartChrome<T>(
        VC.TemplatedControl control,
        string fillPart,
        string borderPart,
        float cornerRadius)
        where T : VisualElement
    {
        using var surface = new VisualHost { Size = new Size(120, 80), DpiScaleOverride = 1f };
        var root = new VC.StackPanel { Padding = new XamlThickness(20f) };
        ApplyChrome(control);
        root.Add(control);
        surface.Root = root;
        Layout(surface, 120, 80);

        var fill = control.GetTemplateChild<T>(fillPart);
        Assert.IsNotNull(fill);
        Assert.AreEqual(cornerRadius, control.CornerRadius, 0.01f);
        if (fill is VC.Border fillBorder)
        {
            Assert.AreEqual(ChromeBackground.ToArgb(), fillBorder.Background!.ApproximateColor.ToArgb());
            Assert.AreEqual(cornerRadius, fillBorder.CornerRadius, 0.01f);
        }
        else if (fill is ShapeRectangle fillRectangle)
        {
            Assert.AreEqual(ChromeBackground.ToArgb(), fillRectangle.Fill!.ApproximateColor.ToArgb());
            Assert.AreEqual(cornerRadius, fillRectangle.CornerRadius, 0.01f);
        }

        var border = control.GetTemplateChild<VisualElement>(borderPart);
        Assert.IsNotNull(border);
        if (border is VC.Border borderEdge)
        {
            Assert.AreEqual(ChromeBorder.ToArgb(), borderEdge.BorderBrush!.ApproximateColor.ToArgb());
            Assert.AreEqual(2f, borderEdge.BorderThickness.Left, 0.01f);
        }
        else if (border is ShapeRectangle strokeEdge)
        {
            Assert.AreEqual(ChromeBorder.ToArgb(), strokeEdge.Stroke!.ApproximateColor.ToArgb());
            Assert.AreEqual(2f, strokeEdge.StrokeThickness, 0.01f);
        }
        control.Dispose();
        root.Dispose();
        surface.Dispose();
    }

    private static void AssertChrome(
        VisualElement control,
        bool bottomBorder = false,
        bool hasItems = false,
        bool hasTreeItem = false,
        bool hasTab = false)
    {
        if (control is VC.ComboBox combo && hasItems)
        {
            combo.Items.Add("One");
            combo.Items.Add("Two");
        }

        if (control is VC.TreeView tree && hasTreeItem)
        {
            tree.Items.Add(new VC.TreeViewItem { Header = "Root", IsExpanded = true });
        }

        if (control is VC.TabControl tabs && hasTab)
        {
            tabs.Items.Add(new VC.TabItem
            {
                Header = "Tab",
                Content = new VC.TextBlock { Text = "Page" },
            });
        }

        ApplyChrome(control);

        using var surface = new VisualHost { Size = new Size(420, 360), DpiScaleOverride = 1f };
        var root = new VC.StackPanel { Padding = new XamlThickness(20f), Spacing = 8 };
        root.Add(control);
        surface.Root = root;
        Layout(surface, 420, 360);

        using var bitmap = new Bitmap(420, 360);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            surface.RenderTo(graphics, new Size(420, 360));
        }

        var bounds = control.AbsoluteBounds;
        var borderX = bottomBorder ? bounds.Left + 8f : bounds.Left + 1f;
        var borderY = bottomBorder ? bounds.Bottom - 2f : bounds.Top + (bounds.Height / 2f);
        var border = bitmap.GetPixel((int)borderX, (int)borderY);
        Assert.AreEqual(ChromeBorder.ToArgb(), border.ToArgb(),
            $"{control.GetType().Name} paints the control border");

        var background = bitmap.GetPixel((int)(bounds.Left + 6f), (int)(bounds.Bottom - 8f));
        Assert.AreEqual(ChromeBackground.ToArgb(), background.ToArgb(),
            $"{control.GetType().Name} paints the control background");

        control.Dispose();
        root.Dispose();
    }

    private static void ApplyChrome(VisualElement control)
    {
        control.Background = new SolidColorBrush(ChromeBackground);
        control.BorderBrush = new SolidColorBrush(ChromeBorder);
        control.BorderThickness = new XamlThickness(2f);
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
