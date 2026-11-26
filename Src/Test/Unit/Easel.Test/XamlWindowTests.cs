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
        Assert.AreEqual(420, window.Width);
        Assert.AreEqual(260, window.Height);
        Assert.IsNotNull(window.Content);
        Assert.AreSame(window.Content, window.Root);
        Assert.AreSame(window.Content, window.Surface.Root);
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
