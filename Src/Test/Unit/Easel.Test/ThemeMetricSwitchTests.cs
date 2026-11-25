using System.Drawing;
using System.Windows.Forms;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// A palette switch rewrites the metrics controls seed from the theme, without rebuilding the controls, while local
/// values and style setters keep winning over the theme layer.
/// </summary>
[STATestClass]
public sealed class ThemeMetricSwitchTests
{
    [TestMethod]
    public void ThemeSwitch_RewritesTheMetricsOfExistingControls()
    {
        var originalMode = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;

            using var button = new VC.Button();
            using var textBox = new VC.TextBox();
            using var border = new VC.Border();
            using var expander = new VC.Expander();
            using var comboBox = new VC.ComboBox();
            using var richTextBox = new VC.RichTextBox();
            using var checkBox = new VC.CheckBox();
            using var dropZone = new VC.DropZone();
            using var calendar = new VC.Calendar();
            using var calendarItem = new VC.CalendarItem();
            using var datePicker = new VC.DatePicker();
            using var dataGrid = new VC.DataGrid();
            using var menu = new VC.VisualContextMenu();
            using var popup = new VC.VisualPopup();

            Assert.AreEqual(4f, button.CornerRadius, "the dark palette corner radius");
            Assert.AreEqual(28d, button.MinHeight, "the dark palette control height");
            Assert.AreEqual(4f, border.CornerRadius);
            Assert.AreEqual(4f, calendarItem.CornerRadius);
            Assert.AreEqual(4f, dataGrid.CornerRadius);
            Assert.AreEqual(4f, menu.CornerRadius);
            Assert.AreEqual(4f, popup.CornerRadius);

            Theme.Mode = ThemeMode.HighContrast;

            Assert.AreEqual(2f, button.CornerRadius, "an existing button takes the high-contrast radius");
            Assert.AreEqual(30d, button.MinHeight, "an existing button takes the high-contrast height");
            Assert.AreEqual(2f, textBox.CornerRadius);
            Assert.AreEqual(30d, textBox.MinHeight);
            Assert.AreEqual(2f, border.CornerRadius);
            Assert.AreEqual(2f, expander.CornerRadius);
            Assert.AreEqual(30d, comboBox.MinHeight);
            Assert.AreEqual(2f, comboBox.CornerRadius);
            Assert.AreEqual(30d, richTextBox.MinHeight);
            Assert.AreEqual(2f, richTextBox.CornerRadius);
            Assert.AreEqual(30d, checkBox.MinHeight, "the toggle family follows the control height");
            Assert.AreEqual(30d, checkBox.MinWidth);
            Assert.AreEqual(2f, dropZone.CornerRadius);
            Assert.AreEqual(2f, calendar.CornerRadius);
            Assert.AreEqual(2f, calendarItem.CornerRadius);
            Assert.AreEqual(30d, datePicker.MinHeight);
            Assert.AreEqual(2f, datePicker.CornerRadius);
            Assert.AreEqual(2f, dataGrid.CornerRadius);
            Assert.AreEqual(2f, menu.CornerRadius);
            Assert.AreEqual(2f, popup.CornerRadius);

            Theme.Mode = ThemeMode.Light;

            Assert.AreEqual(4f, button.CornerRadius, "switching back restores the built-in radius");
            Assert.AreEqual(28d, button.MinHeight, "switching back restores the built-in height");
        }
        finally
        {
            RestoreMode(originalMode);
        }
    }

    [TestMethod]
    public void LocalValue_OutranksTheThemeMetric()
    {
        var originalMode = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;

            using var button = new VC.Button { CornerRadius = 9f, MinHeight = 44d };

            Theme.Mode = ThemeMode.HighContrast;

            Assert.AreEqual(9f, button.CornerRadius, "a local corner radius is never overwritten by the palette");
            Assert.AreEqual(44d, button.MinHeight, "a local minimum height is never overwritten by the palette");
        }
        finally
        {
            RestoreMode(originalMode);
        }
    }

    [TestMethod]
    public void StyleSetter_OutranksTheThemeMetric()
    {
        var originalMode = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;

            using var button = new VC.Button();
            var style = new VisualStyle { TargetType = typeof(VC.Button) };
            style.Setters.Add(new VisualSetter(VisualElement.CornerRadiusProperty, 12f));
            style.Setters.Add(new VisualSetter(VisualElement.MinHeightProperty, 40d));
            button.Style = style;

            Theme.Mode = ThemeMode.HighContrast;

            Assert.AreEqual(12f, button.CornerRadius, "a style setter outranks the theme layer");
            Assert.AreEqual(40d, button.MinHeight, "a style setter outranks the theme layer");
        }
        finally
        {
            RestoreMode(originalMode);
        }
    }

    [TestMethod]
    public void ThemeSwitch_ReachesTheTemplateBindingAndTheLayout()
    {
        var originalMode = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;
            using var surface = new VisualHost { Size = new Size(240, 120), DpiScaleOverride = 1f };
            // 零内边距让自然内容低于 MinHeight，量到的就是主题控件高度本身。
            var button = new VC.Button { Padding = new Easel.Controls.Thickness(0) };
            var panel = new VC.StackPanel();
            panel.Children.Add(button);
            surface.Root = panel;
            Layout(surface, 240, 120);

            var chrome = button.GetTemplateChild<VC.Border>("Chrome");
            Assert.IsNotNull(chrome, "the button implicit style supplies the default template");
            Assert.AreEqual(4f, chrome!.CornerRadius, "the template reads the dark corner radius");
            Assert.AreEqual(28d, button.Bounds.Height, "the dark control height reaches layout");

            Theme.Mode = ThemeMode.HighContrast;
            Layout(surface, 240, 120);

            Assert.AreEqual(2f, button.CornerRadius);
            Assert.AreEqual(2f, chrome.CornerRadius, "the template binding follows the rewritten metric");
            Assert.AreEqual(30d, button.Bounds.Height, "the control re-measures to the high-contrast height");
        }
        finally
        {
            RestoreMode(originalMode);
        }
    }

    [TestMethod]
    public void CustomPalette_UpdatesMetricsOnExistingControls()
    {
        var originalMode = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;
            using var button = new VC.Button();
            Assert.AreEqual(4f, button.CornerRadius);

            Theme.ApplyPalette(
                ThemeMode.HighContrast,
                new ThemePalette { CornerRadius = 7f, ControlHeight = 35 });

            Assert.AreEqual(7f, button.CornerRadius, "a host-defined palette reaches existing controls");
            Assert.AreEqual(35d, button.MinHeight, "a host-defined palette reaches existing controls");
        }
        finally
        {
            RestoreMode(originalMode);
        }
    }

    [TestMethod]
    public void DisposedControl_StopsFollowingTheTheme()
    {
        var originalMode = Theme.Mode;
        try
        {
            Theme.Mode = ThemeMode.Dark;
            var button = new CountingButton();
            var disposedChanges = 0;

            button.Dispose();
            disposedChanges = button.Changes;

            Theme.Mode = ThemeMode.HighContrast;

            Assert.AreEqual(disposedChanges, button.Changes, "a disposed control left the static theme event");
        }
        finally
        {
            RestoreMode(originalMode);
        }
    }

    private sealed class CountingButton : VC.Button
    {
        internal int Changes { get; private set; }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            Changes++;
            base.OnPropertyChanged(e);
        }
    }

    // 先切到另一个内置模式，保证回到原模式时 Theme.Mode 的 setter 真的换回内置调色板（自定义调色板不会被保留）。
    private static void RestoreMode(ThemeMode mode)
    {
        Theme.Mode = mode == ThemeMode.Light ? ThemeMode.Dark : ThemeMode.Light;
        Theme.Mode = mode;
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
