using System.Drawing;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;
using WinFormsForm = System.Windows.Forms.Form;
using WinFormsPanel = System.Windows.Forms.Panel;

namespace Easel.Test;

[STATestClass]
public sealed class VisualBootTests
{
    [TestMethod]
    public void AddingAndRemovingAVisualRootManagesItsHost()
    {
        using var panel = new WinFormsPanel();
        var view = new VC.StackPanel();
        var host = panel.Controls.Add(view);

        Assert.AreEqual(1, panel.Controls.Count);
        Assert.AreSame(host, panel.Controls[0]);
        Assert.AreSame(view, host.Root is VC.DockPanel dock ? dock.Children[0] : host.Root);

        Assert.IsTrue(panel.Controls.Remove(view));
        Assert.AreEqual(0, panel.Controls.Count);
    }

    [TestMethod]
    public void VisualFormHostsAnyVisualRoot()
    {
        using var form = new VisualForm();
        var view = new VC.TextBlock { Text = "Root", Width = 120, Height = 24 };
        form.Root = view;

        Assert.AreSame(view, form.Root);
        Assert.IsNotNull(form.Surface.Root);

        form.Root = null;
        Assert.IsNull(form.Surface.Root);
    }

    [TestMethod]
    public void ASecondRootIsRejectedWhileTheContainerHasAHost()
    {
        using var panel = new WinFormsPanel();
        panel.Controls.Add(new VC.StackPanel());

        Assert.ThrowsExactly<InvalidOperationException>(() => panel.Controls.Add(new VC.StackPanel()));
    }
}
