using System.Drawing;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Realized item visuals survive a list template application or replacement. The shell template only decides where the
/// rows live; rebuilding them would create and dispose the whole row tree twice while a page is being assembled.
/// </summary>
[STATestClass]
public sealed class ItemsRealizationReuseTests
{
    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }

    [TestMethod]
    public void ItemsSource_SetBeforeHosting_KeepsTheSameContainersWhenTheShellApplies()
    {
        using var surface = new VisualHost { Size = new Size(220, 160), DpiScaleOverride = 1f };
        using var list = new VC.ListBox
        {
            Width = 200,
            Height = 140,
            ItemsSource = new[] { "alpha", "beta", "gamma" },
        };

        var realizedBeforeHosting = list.Items.ToArray();
        Assert.AreEqual(3, realizedBeforeHosting.Length, "the source is realized on the bare list");

        surface.Root = list;
        Layout(surface, 220, 160);

        Assert.AreEqual(3, list.Items.Count);
        for (var index = 0; index < realizedBeforeHosting.Length; index++)
        {
            Assert.AreSame(
                realizedBeforeHosting[index],
                list.Items[index],
                $"row {index} must survive the shell template application instead of being rebuilt");
        }

        var presenter = list.GetTemplateChild<VC.ItemsPresenter>("PART_ItemsPresenter");
        Assert.IsNotNull(presenter, "the shell applied");
        Assert.IsTrue(presenter!.Children.Contains(list.Items[0]), "the persisted row moved into the shell presenter");
        Assert.AreEqual(
            "beta",
            ((VC.TextBlock)((VC.ListBoxItem)list.Items[1]).Content!).Text,
            "the persisted row still shows its source item");
    }

    [TestMethod]
    public void ReplacingTheTemplate_RehostsTheSameRowsAndKeepsTheirContent()
    {
        using var surface = new VisualHost { Size = new Size(220, 160), DpiScaleOverride = 1f };
        using var list = new VC.ListBox { Width = 200, Height = 140 };
        surface.Root = list;
        list.ItemsSource = new[] { "alpha", "beta", "gamma" };
        Layout(surface, 220, 160);

        var rows = list.Items.ToArray();

        list.Template = new VisualControlTemplate(_ => new VC.Grid());
        Layout(surface, 220, 160);

        Assert.AreEqual(3, list.Items.Count);
        for (var index = 0; index < rows.Length; index++)
        {
            Assert.AreSame(rows[index], list.Items[index], $"row {index} must move to the new shell, not be rebuilt");
        }

        Assert.IsInstanceOfType<VC.Grid>(list.TemplateRoot, "the direct template replaced the shell");
        Assert.IsTrue(
            ((VC.Grid)list.TemplateRoot!).Children.Contains(list.Items[0]),
            "the rows were re-parented into the replacement template root");
    }

    [TestMethod]
    public void Dispose_StillDisposesTheRealizedRowsAndTheirContent()
    {
        var probes = new List<DisposeProbe>();
        var surface = new VisualHost { Size = new Size(220, 160), DpiScaleOverride = 1f };
        var disposed = false;
        try
        {
            var list = new VC.ListBox
            {
                Width = 200,
                Height = 140,
                ItemFactory = _ =>
                {
                    var probe = new DisposeProbe();
                    probes.Add(probe);
                    return probe;
                },
            };
            surface.Root = list;
            list.ItemsSource = new[] { "alpha", "beta" };
            Layout(surface, 220, 160);

            Assert.AreEqual(2, probes.Count, "one content visual per source item");

            surface.Dispose();
            disposed = true;

            Assert.IsTrue(
                probes.All(static probe => probe.Disposed),
                "detaching for a template change must not orphan rows");
        }
        finally
        {
            if (!disposed)
            {
                surface.Dispose();
            }
        }
    }

    public sealed class DisposeProbe : VC.TextBlock
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
