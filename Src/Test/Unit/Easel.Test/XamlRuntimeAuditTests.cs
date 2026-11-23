using System.ComponentModel;
using System.Drawing;
using Easel.Animation;
using Easel;
using Easel.Test.Infrastructure;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Regressions for the runtime audit: animation-layer release, bindings established before parenting, ZIndex
/// ordering outside the view pool, zero-size containers, GroupBox content, theme repaint and horizontal scrolling.
/// </summary>
[STATestClass]
public sealed class XamlRuntimeAuditTests
{
    [TestMethod]
    public void BindingsResolveTheirSourceAfterTheChildIsParented()
    {
        var model = new ContextModel { Name = "ctx" };
        var root = new VC.Grid { DataContext = model };
        root.Add(new VC.TextBlock { Name = "probe", Text = "named" });

        // An inherited-DataContext path binding created before parenting.
        var inherited = new VC.TextBlock();
        inherited.SetBinding(VC.TextBlock.TextProperty, new Binding(nameof(ContextModel.Name)));
        Assert.AreEqual(string.Empty, inherited.Text, "unresolved before the child has a parent");

        root.Add(inherited);
        Assert.AreEqual("ctx", inherited.Text, "parenting re-resolves the inherited context");

        // An ElementName binding created before the child is in the tree.
        var named = new VC.TextBlock();
        named.SetBinding(
            VC.TextBlock.TextProperty,
            new Binding(nameof(VC.TextBlock.Text)) { ElementName = "probe" });
        Assert.AreEqual(string.Empty, named.Text, "ElementName cannot resolve outside the tree");

        root.Add(named);
        Assert.AreEqual("named", named.Text, "parenting re-resolves the named element");
    }

    [TestMethod]
    public void ZIndex_DrivesPaintAndHitOrderOutsideTheViewPool()
    {
        var paintOrder = new List<string>();
        var container = new ContainerVisual { Bounds = new RectangleF(0f, 0f, 100f, 100f) };
        var high = new RecordingElement(paintOrder) { Name = "high", Bounds = new RectangleF(0f, 0f, 50f, 50f) };
        var low = new RecordingElement(paintOrder) { Name = "low", Bounds = new RectangleF(0f, 0f, 50f, 50f) };
        VC.Panel.SetZIndex(high, 0);
        VC.Panel.SetZIndex(low, -1);

        container.Add(high);
        container.Add(low);

        using var bitmap = new Bitmap(100, 100);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            container.Render(graphics);
        }

        CollectionAssert.AreEqual(new[] { "low", "high" }, paintOrder.ToArray());
        Assert.AreSame(high, container.HitTestVisual(new PointF(10f, 10f)));
    }

    [TestMethod]
    public void ZeroSizedUnclippedContainer_StillRendersAndHitsItsChildren()
    {
        var paintOrder = new List<string>();
        var container = new ContainerVisual { Bounds = RectangleF.Empty, ClipToBounds = false };
        container.Add(new RecordingElement(paintOrder)
        {
            Name = "child",
            Bounds = new RectangleF(5f, 5f, 20f, 20f),
        });

        using var bitmap = new Bitmap(60, 60);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            container.Render(graphics);
        }

        CollectionAssert.Contains(paintOrder, "child");
        Assert.AreSame(container.Children[0], container.HitTestVisual(new PointF(10f, 10f)));
    }

    [TestMethod]
    public void GroupBox_AddMapsContentToChild()
    {
        using var box = new VC.GroupBox();
        using var inner = new VC.Border();

        box.Add(inner);

        Assert.AreSame(inner, box.Child);
        CollectionAssert.Contains(box.Children.ToArray(), inner, "the child must also join the visual children");
    }

    [TestMethod]
    public void SwitchingThemeMode_InvalidatesTheSurface()
    {
        var original = Theme.Mode;
        try
        {
            using var surface = new VisualHost { Size = new Size(80, 60), DpiScaleOverride = 1f };
            var layoutCount = 0;
            surface.LayoutUpdated += (_, _) => layoutCount++;

            using var bitmap = new Bitmap(1, 1);
            using var graphics = Graphics.FromImage(bitmap);
            surface.RenderTo(graphics, new Size(80, 60));
            var before = layoutCount;

            Theme.Mode = original == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
            surface.RenderTo(graphics, new Size(80, 60));

            Assert.IsTrue(layoutCount > before, "a theme switch must invalidate the surface");
        }
        finally
        {
            Theme.Mode = original;
        }
    }

    [TestMethod]
    public void HorizontalScroll_MeasuresWrappingContentWiderThanTheViewport()
    {
        var presenter = new VC.ScrollContentPresenter
        {
            Width = 100,
            Height = 60,
            CanHorizontallyScroll = true,
        };
        presenter.Content = new VC.TextBlock
        {
            Wrap = true,
            Text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore",
        };

        presenter.Measure(new Size(100, 60));
        presenter.Arrange(new RectangleF(0f, 0f, 100f, 60f));

        Assert.IsTrue(
            presenter.ExtentWidth > presenter.ViewportWidth,
            "wrapping content must be allowed to extend horizontally when horizontal scrolling is on");
    }

    private sealed class RecordingElement(List<string> order) : VisualElement
    {
        protected override void OnRender(Graphics graphics) => order.Add(Name ?? string.Empty);
    }

    private sealed class ContextModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
