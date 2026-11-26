using System.Windows.Forms;
using Easel.Test.Fixtures;

namespace Easel.Test;

[STATestClass]
public sealed class XamlWindowTests
{
    [TestMethod]
    public void AWindowRootBecomesAContentForm()
    {
        using var window = new WindowView();

        Assert.IsTrue(window is Form);
        Assert.AreEqual("Window fixture", window.Title);
        Assert.AreEqual(420d, window.WidthDips);
        Assert.AreEqual(260d, window.HeightDips);
        Assert.IsNotNull(window.Content);
        Assert.AreSame(window.Content, window.Root);
        Assert.AreSame(window.Content, window.Surface.Root);
        Assert.AreEqual(AutoScaleMode.None, window.AutoScaleMode);
    }

    [TestMethod]
    public void XamlWindowSizeIsAppliedInDeviceIndependentPixels()
    {
        using var window = new WindowView();
        window.Surface.DpiScaleOverride = 1.5f;

        _ = window.Handle;

        Assert.AreEqual(630, window.Width);
        Assert.AreEqual(390, window.Height);
    }

    [TestMethod]
    public void ACodeBehindSizeWinsOverTheXamlDips()
    {
        using var window = new WindowView();
        window.Width = 900;
        window.Surface.DpiScaleOverride = 1.5f;

        _ = window.Handle;

        Assert.AreEqual(900, window.Width);
        Assert.AreEqual(390, window.Height);
    }

    [TestMethod]
    public void WindowResourcesLandOnTheHostingSurface()
    {
        using var window = new WindowView();

        Assert.IsTrue(window.Resources.ContainsKey("Accent"));
        Assert.AreEqual(unchecked((int)0xFF0078D4), ((SolidColorBrush)window.Resources["Accent"]!).Color.ToArgb());
    }

    [TestMethod]
    public void WindowDataContextReachesTheContent()
    {
        using var window = new WindowView();
        var context = new WindowViewModel { Greeting = "Updated" };

        window.DataContext = context;

        Assert.AreSame(context, window.Surface.DataContext);
        Assert.AreSame(context, window.Content!.DataContext);
        Assert.AreEqual("Updated", window.GreetingText.Text);
    }
}
