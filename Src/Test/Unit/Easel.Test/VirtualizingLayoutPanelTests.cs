using System.Drawing;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>The layout-strategy API: a custom layout needs no <see cref="VC.VirtualizingPanel"/> subclass.</summary>
[STATestClass]
public sealed class VirtualizingLayoutPanelTests
{
    [TestMethod]
    public void DelegateLayoutRealizesTheVisibleWindow()
    {
        using var surface = new VisualHost { Size = new Size(220, 100) };
        using var viewer = new VC.ScrollViewer { Width = 220, Height = 100 };
        using var panel = new VC.VirtualizingLayoutPanel
        {
            ItemsSource = Enumerable.Range(0, 1000).Select(static index => (object)index).ToList(),
            Layout = TwoColumnLayout(1000),
        };
        viewer.Content = panel;
        surface.Root = viewer;

        Layout(surface, 220, 100);

        Assert.IsGreaterThan(0, panel.RealizedCount);
        Assert.IsLessThan(50, panel.RealizedCount, "the window stays bounded");
        Assert.AreEqual(0, panel.FirstRealizedIndex);
        Assert.AreEqual(0f, panel.Items[0].Bounds.Y, 0.1f);
        Assert.AreEqual(100f, panel.Items[1].Bounds.X, 0.1f);

        viewer.VerticalOffset = 300f;
        Layout(surface, 220, 100);

        Assert.AreEqual(10, panel.FirstRealizedIndex, "300 px / 60 px rows = row 5, first index is 10");
        Assert.IsLessThan(50, panel.RealizedCount);
    }

    [TestMethod]
    public void ALayoutObjectNeedsNoPanelSubclass()
    {
        using var surface = new VisualHost { Size = new Size(200, 80) };
        using var viewer = new VC.ScrollViewer { Width = 200, Height = 80 };
        using var panel = new VC.VirtualizingLayoutPanel
        {
            ItemsSource = Enumerable.Range(0, 100).Select(static index => (object)index).ToList(),
            Layout = new FixedLayout(100, rowHeight: 20, columns: 1),
        };
        viewer.Content = panel;
        surface.Root = viewer;

        Layout(surface, 200, 80);

        Assert.AreEqual(4, panel.RealizedCount);
        Assert.AreEqual(0, panel.FirstRealizedIndex);
    }

    [TestMethod]
    public void MissingLayoutThrowsBeforeMeasure()
    {
        using var panel = new VC.VirtualizingLayoutPanel
        {
            ItemsSource = new[] { (object)1, 2, 3 },
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => panel.Measure(new Size(200, 100)));
    }

    [TestMethod]
    public void ReplacingTheLayoutResetsTheRealizedWindow()
    {
        using var surface = new VisualHost { Size = new Size(200, 80) };
        using var viewer = new VC.ScrollViewer { Width = 200, Height = 80 };
        using var panel = new VC.VirtualizingLayoutPanel
        {
            ItemsSource = Enumerable.Range(0, 100).Select(static index => (object)index).ToList(),
            Layout = new FixedLayout(100, rowHeight: 20, columns: 1),
        };
        viewer.Content = panel;
        surface.Root = viewer;
        Layout(surface, 200, 80);
        var before = panel.RealizedCount;

        panel.Layout = new FixedLayout(100, rowHeight: 40, columns: 1);
        Layout(surface, 200, 80);

        Assert.AreEqual(2, panel.RealizedCount);
        Assert.AreNotEqual(before, panel.RealizedCount);
    }

    private static VC.DelegateVirtualizingLayout TwoColumnLayout(int count)
        => new(
            measure: (available, itemCount, _) =>
            {
                var rows = (int)Math.Ceiling(itemCount / 2d);
                return new Size(available.Width, rows * 60);
            },
            build: request =>
            {
                var firstRow = Math.Max(0, (int)Math.Floor((request.OffsetY - request.Padding.Top) / 60f));
                var lastRow = (int)Math.Ceiling((request.OffsetY + request.Viewport.Height) / 60f);
                var items = new List<VC.VirtualizationItemLayout>();
                for (var row = firstRow; row < lastRow; row++)
                {
                    for (var column = 0; column < 2; column++)
                    {
                        var index = (row * 2) + column;
                        if (index >= count)
                        {
                            continue;
                        }

                        items.Add(new VC.VirtualizationItemLayout(
                            index,
                            new RectangleF(column * 100f, row * 60f, 100f, 60f)));
                    }
                }

                return new VC.VirtualizationLayoutContext(
                    items,
                    new Size(200, (int)Math.Ceiling(count / 2d) * 60),
                    new RectangleF(request.OffsetX, request.OffsetY, request.Viewport.Width, request.Viewport.Height),
                    request.OffsetX,
                    request.OffsetY,
                    count);
            });

    private sealed class FixedLayout : VC.IVirtualizingLayout
    {
        private readonly int _count;
        private readonly int _rowHeight;
        private readonly int _columns;

        public FixedLayout(int count, int rowHeight, int columns)
        {
            _count = count;
            _rowHeight = rowHeight;
            _columns = columns;
        }

        public Size Measure(Size availableSize, int itemCount, Easel.Controls.Thickness padding)
            => new(availableSize.Width, (int)Math.Ceiling(itemCount / (double)_columns) * _rowHeight);

        public VC.VirtualizationLayoutContext Build(VC.VirtualizationLayoutRequest request)
        {
            var rows = (int)Math.Ceiling(_count / (double)_columns);
            var firstRow = Math.Max(0, (int)Math.Floor((request.OffsetY - request.Padding.Top) / _rowHeight));
            var lastRow = (int)Math.Ceiling((request.OffsetY + request.Viewport.Height) / _rowHeight);
            var items = new List<VC.VirtualizationItemLayout>();
            for (var row = firstRow; row < lastRow; row++)
            {
                for (var column = 0; column < _columns; column++)
                {
                    var index = (row * _columns) + column;
                    if (index >= _count)
                    {
                        continue;
                    }

                    items.Add(new VC.VirtualizationItemLayout(
                        index,
                        new RectangleF(column * (400f / _columns), row * _rowHeight, 400f / _columns, _rowHeight)));
                }
            }

            return new VC.VirtualizationLayoutContext(
                items,
                new Size(400, rows * _rowHeight),
                new RectangleF(request.OffsetX, request.OffsetY, request.Viewport.Width, request.Viewport.Height),
                request.OffsetX,
                request.OffsetY,
                _count);
        }
    }

    private static void Layout(VisualHost surface, int width, int height)
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        surface.RenderTo(graphics, new Size(width, height));
    }
}
