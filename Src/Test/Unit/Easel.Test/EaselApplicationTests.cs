using System.Drawing;
using System.Windows.Forms;
using Easel.Visuals;
using Easel.Visuals.Controls;

namespace Easel.Test;

[STATestClass]
public sealed class EaselApplicationTests
{
    [TestMethod]
    public void TheDpiProbeMatchesTheProcessMode()
    {
        var mode = Application.HighDpiMode;
        var perMonitorV2 = EaselApplication.IsPerMonitorV2Active();

        if (mode == HighDpiMode.PerMonitorV2)
        {
            Assert.IsTrue(perMonitorV2);
        }
        else if (mode != HighDpiMode.PerMonitor)
        {
            Assert.IsFalse(perMonitorV2, "a process that is not per-monitor aware cannot report PMv2");
        }
    }

    [TestMethod]
    public void ApplicationResourcesCompleteTheElementChain()
    {
        var brush = new SolidColorBrush(Color.Red);
        EaselApplication.Resources["AppProbeBrush"] = brush;
        try
        {
            var element = new TextBlock();

            Assert.AreSame(brush, VisualResourceLookup.FindResource(element, "AppProbeBrush"));
        }
        finally
        {
            EaselApplication.Resources.Remove("AppProbeBrush");
        }
    }

    [TestMethod]
    public void ApplicationStylesAreTheLastImplicitStyleCollection()
    {
        var style = new VisualStyle { TargetType = typeof(TextBlock) };
        style.Setters.Add(new VisualSetter(TextBlock.FontSizeProperty, 33d));
        EaselApplication.Styles.Add(style);
        try
        {
            var block = new TextBlock();
            block.RefreshStyle();

            Assert.AreEqual(33d, block.FontSize);
        }
        finally
        {
            EaselApplication.Styles.Remove(style);
        }
    }

    [TestMethod]
    public void AKeylessStyleInApplicationResourcesIsImplicit()
    {
        var style = new VisualStyle { TargetType = typeof(TextBlock) };
        style.Setters.Add(new VisualSetter(TextBlock.FontSizeProperty, 33d));
        EaselApplication.Resources[typeof(TextBlock)] = style;
        try
        {
            var block = new TextBlock();
            block.RefreshStyle();

            Assert.AreEqual(33d, block.FontSize);
        }
        finally
        {
            EaselApplication.Resources.Remove(typeof(TextBlock));
        }
    }
}

