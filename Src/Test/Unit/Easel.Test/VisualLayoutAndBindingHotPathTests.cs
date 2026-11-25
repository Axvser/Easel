using System.ComponentModel;
using System.Drawing;
using Easel;
using Easel.Controls;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>Behaviour locks for the template-walk and layout-cache hot paths.</summary>
/// <remarks>
/// The render benchmark builds the same page many times, so these tests assert the observable behaviour of the
/// allocation fixes: a Grid arranges from the measure pass it just ran, a binding keeps one subscription while its
/// source object is unchanged, and template initialisation still assigns ownership to nested template roots.
/// </remarks>
[STATestClass]
public sealed class VisualLayoutAndBindingHotPathTests
{
    [TestMethod]
    public void Grid_ArrangeReusesTheTracksItJustResolved()
    {
        var grid = new VC.Grid { Width = 200, Height = 200 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        var child = new ProbeElement();
        grid.Add(child);

        grid.Measure(new Size(200, 200));
        Assert.AreEqual(1, child.MeasureCount, "the pixel column measures the child once");
        Assert.AreEqual(new Size(120, 200), child.LastAvailable, "the pixel column measures the slot, not the grid");

        grid.Arrange(new RectangleF(0, 0, 200, 200));
        Assert.AreEqual(1, child.MeasureCount, "arranging at the measured size must not measure again");
        Assert.AreEqual(new RectangleF(0, 0, 120, 200), child.Bounds, "the arrangement still uses the resolved tracks");
    }

    [TestMethod]
    public void Grid_StarTracksStillReflowOnceInsideTheMeasurePass()
    {
        var grid = new VC.Grid { Width = 200, Height = 200 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var child = new ProbeElement();
        grid.Add(child);

        grid.Measure(new Size(200, 200));
        Assert.AreEqual(2, child.MeasureCount, "a star child is measured against the grid and then against its slot");
        Assert.AreEqual(new Size(100, 200), child.LastAvailable, "the second pass uses the assigned slot");

        grid.Arrange(new RectangleF(0, 0, 200, 200));
        Assert.AreEqual(2, child.MeasureCount, "arranging does not repeat the star reflow");
    }

    [TestMethod]
    public void Grid_RemeasuresAfterInvalidationBeforeTheNextArrange()
    {
        var grid = new VC.Grid { Width = 200, Height = 200 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var child = new ProbeElement();
        grid.Add(child);

        grid.Measure(new Size(200, 200));
        grid.Arrange(new RectangleF(0, 0, 200, 200));
        Assert.AreEqual(2, child.MeasureCount);

        grid.Width = 240;
        grid.Arrange(new RectangleF(0, 0, 240, 200));
        Assert.AreEqual(4, child.MeasureCount, "an invalidated measure still re-runs before arranging");
        Assert.AreEqual(120f, child.Bounds.Width, "the star tracks divide the new width");
    }

    [TestMethod]
    public void Binding_KeepsOneSubscriptionWhileTheSourceObjectIsUnchanged()
    {
        var model = new CountingModel { Name = "first" };
        var text = new VC.TextBlock();
        text.SetBinding(
            VC.TextBlock.TextProperty,
            new Binding(nameof(CountingModel.Name)) { Source = model });

        Assert.AreEqual("first", text.Text);
        Assert.AreEqual(1, model.SubscribeCount, "the first resolution subscribes once");
        Assert.AreEqual(0, model.UnsubscribeCount);

        model.Name = "second";

        Assert.AreEqual("second", text.Text);
        Assert.AreEqual(1, model.SubscribeCount, "re-resolving the same node must not subscribe again");
        Assert.AreEqual(0, model.UnsubscribeCount, "re-resolving the same node must not unsubscribe either");
    }

    [TestMethod]
    public void TemplateInitialisationMarksTheWholeTreeAndKeepsNestedOwners()
    {
        var inner = new VC.ContentControl
        {
            Template = new VisualControlTemplate(_ =>
            {
                var innerRoot = new VC.StackPanel { Name = "InnerRoot" };
                innerRoot.Add(new VC.TextBlock());
                return innerRoot;
            }),
        };

        var outer = new VC.ContentControl
        {
            Template = new VisualControlTemplate(_ =>
            {
                var outerRoot = new VC.StackPanel { Name = "OuterRoot" };
                outerRoot.Add(inner);
                return outerRoot;
            }),
        };

        Assert.IsNotNull(outer.TemplateRoot);
        Assert.IsNotNull(inner.TemplateRoot);
        Assert.AreSame(outer, outer.TemplateRoot!.TemplatedParent, "the outer root belongs to the outer control");
        Assert.AreSame(outer, inner.TemplatedParent, "the nested control itself is part of the outer template");
        Assert.AreSame(inner, inner.TemplateRoot!.TemplatedParent, "the nested template keeps its own owner");

        AssertTemplateContent(outer.TemplateRoot);
    }

    private static void AssertTemplateContent(VisualElement element)
    {
        Assert.IsTrue(element.IsTemplateContent, $"{element.GetType().Name} is part of a template");
        var children = element.Children;
        for (var index = 0; index < children.Count; index++)
        {
            if (children[index] is VisualElement child)
            {
                AssertTemplateContent(child);
            }
        }
    }

    private sealed class ProbeElement : VisualElement
    {
        public int MeasureCount { get; private set; }

        public Size LastAvailable { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            MeasureCount++;
            LastAvailable = availableSize;
            return new Size(40, 30);
        }
    }

    private sealed class CountingModel : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _handlers;
        private string? _name;

        public int SubscribeCount { get; private set; }

        public int UnsubscribeCount { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                SubscribeCount++;
                _handlers += value;
            }

            remove
            {
                UnsubscribeCount++;
                _handlers -= value;
            }
        }

        public string? Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                _handlers?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }
    }
}
