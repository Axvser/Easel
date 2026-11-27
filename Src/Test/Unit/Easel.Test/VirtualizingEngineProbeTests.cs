using System.Drawing;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Backs the abstract <see cref="VC.VirtualizingPanel"/> engine with a layout that is deliberately not a
/// one-dimensional contiguous stack: sparse indices, two-column bounds, negative document coordinates and an
/// <see cref="VC.VirtualizationItemLayout.Order"/> that differs from the source index.
/// </summary>
[STATestClass]
public sealed class VirtualizingEngineProbeTests
{
    [TestMethod]
    public void ProbeWindowRealizesExactlyTheRequestedIndices()
    {
        var source = Rows(100, "A");
        using var panel = new ProbeLayoutPanel { ItemsSource = source };
        panel.SetWindow(
            (5, new RectangleF(0f, 0f, 40f, 30f), 1),
            (2, new RectangleF(40f, 0f, 40f, 30f), 3),
            (42, new RectangleF(0f, 30f, 40f, 30f), 0),
            (43, new RectangleF(40f, 30f, 40f, 30f), 2));

        Layout(panel);

        Assert.AreEqual(4, panel.RealizedCount, "only the requested records are realized");
        Assert.AreEqual(4, panel.Items.Count);
        Assert.AreEqual(2, panel.FirstRealizedIndex, "the first realized index is the minimum source index, not the host position");

        var expected = new[] { source[42], source[5], source[43], source[2] };
        for (var position = 0; position < expected.Length; position++)
        {
            Assert.AreSame(expected[position], panel.Items[position].DataContext,
                $"host position {position} must hold the record with order {position}");
        }

        var released = panel.Children.OfType<VisualElement>().ToArray();
        CollectionAssert.AreEqual(panel.Items.ToArray(), released, "the host children follow the layout order, not the index order");
    }

    [TestMethod]
    public void LeavingVisualsEnterThePoolAndAreReusedInsteadOfCreated()
    {
        var source = Rows(300, "A");
        using var panel = new ProbeLayoutPanel { ItemsSource = source };
        panel.SetWindow(
            (0, new RectangleF(0f, 0f, 40f, 30f), 0),
            (3, new RectangleF(40f, 0f, 40f, 30f), 1),
            (7, new RectangleF(0f, 30f, 40f, 30f), 2),
            (9, new RectangleF(40f, 30f, 40f, 30f), 3));
        Layout(panel);

        var firstPass = panel.Items.ToArray();
        Assert.AreEqual(4, panel.Created);

        panel.SetWindow(
            (100, new RectangleF(-80f, -60f, 40f, 30f), 0),
            (101, new RectangleF(-40f, -60f, 40f, 30f), 1),
            (150, new RectangleF(-80f, -30f, 40f, 30f), 2),
            (199, new RectangleF(-40f, -30f, 40f, 30f), 3));
        Layout(panel);

        Assert.AreEqual(4, panel.Created, "a disjoint window is filled from the pool, never by calling the factory again");
        Assert.AreEqual(4, panel.Recycled.Count, "every leaving visual was reported");
        Assert.AreEqual(4, panel.Rebound.Count, "every entering index rebound a pooled visual");
        Assert.HasCount(4, panel.Items);

        CollectionAssert.AreEquivalent(firstPass, panel.Items.ToArray(),
            "the second window is exactly the first window's visual instances");

        foreach (var (index, item, element) in panel.Recycled)
        {
            Assert.AreSame(source[index], item, "the recycled visual reports the source index it actually showed");
            Assert.IsTrue(firstPass.Contains(element), "the recycled element came from the previous window");
        }

        foreach (var (index, element) in panel.Rebound)
        {
            Assert.AreSame(source[index], element.DataContext, "a rebound visual now shows the item at its new index");
        }
    }

    [TestMethod]
    public void ChangingOnlyOrderReordersTheHostWithoutRecycling()
    {
        var source = Rows(50, "A");
        using var panel = new ProbeLayoutPanel { ItemsSource = source };
        panel.SetWindow(
            (9, new RectangleF(0f, 0f, 40f, 30f), 2),
            (1, new RectangleF(40f, 0f, 40f, 30f), 0),
            (4, new RectangleF(0f, 30f, 40f, 30f), 1));
        Layout(panel);

        Assert.AreEqual(1, panel.FirstRealizedIndex);
        CollectionAssert.AreEqual(
            new[] { source[1], source[4], source[9] },
            panel.Items.Select(static visual => visual.DataContext).ToArray(),
            "host order follows Order, not the source index");

        var created = panel.Created;
        panel.SetWindow(
            (9, new RectangleF(0f, 0f, 40f, 30f), 0),
            (1, new RectangleF(40f, 0f, 40f, 30f), 1),
            (4, new RectangleF(0f, 30f, 40f, 30f), 2));
        Layout(panel);

        CollectionAssert.AreEqual(
            new[] { source[9], source[1], source[4] },
            panel.Items.Select(static visual => visual.DataContext).ToArray(),
            "a pure order change must rewrite the host child order");
        Assert.AreEqual(created, panel.Created, "reordering keeps the same visuals");
        Assert.AreEqual(0, panel.Recycled.Count, "reordering must not recycle anything");
    }

    [TestMethod]
    public void SparseNegativeBoundsReachTheItemVisualsUnchanged()
    {
        var source = Rows(80, "A");
        using var panel = new ProbeLayoutPanel { ItemsSource = source };
        var records = new (int Index, RectangleF Bounds, int Order)[]
        {
            (70, new RectangleF(-140f, -90f, 40f, 30f), 0),
            (6, new RectangleF(320f, -90f, 40f, 30f), 1),
            (13, new RectangleF(-140f, 410f, 40f, 30f), 2),
            (55, new RectangleF(320f, 410f, 40f, 30f), 3),
        };
        panel.SetWindow(records);
        Layout(panel);

        Assert.AreEqual(4, panel.RealizedCount);
        Assert.AreEqual(6, panel.FirstRealizedIndex);
        for (var position = 0; position < records.Length; position++)
        {
            Assert.AreEqual(records[position].Bounds, panel.Items[position].Bounds,
                "negative and far-flung document coordinates are arranged verbatim");
            Assert.AreEqual(records[position].Bounds, panel.Items[position].AbsoluteBounds,
                "a standalone panel arranges in surface coordinates");
        }
    }

    [TestMethod]
    public void EmptyWindowsAndEmptySourcesRealizeNothing()
    {
        using var panel = new ProbeLayoutPanel();
        Assert.AreEqual(-1, panel.FirstRealizedIndex, "no arrange has happened yet");

        panel.ItemsSource = Rows(20, "A");
        panel.SetWindow();
        Layout(panel);

        Assert.AreEqual(0, panel.RealizedCount);
        Assert.IsEmpty(panel.Items);
        Assert.IsEmpty(panel.Children.OfType<VisualElement>());
        Assert.AreEqual(0, panel.FirstRealizedIndex, "an empty realized window reports index zero");

        panel.SetWindow(
            (2, new RectangleF(0f, 0f, 40f, 30f), 0),
            (4, new RectangleF(40f, 0f, 40f, 30f), 1));
        Layout(panel);
        Assert.AreEqual(2, panel.RealizedCount);

        panel.ItemsSource = Array.Empty<object?>();
        panel.SetWindow();
        Layout(panel);

        Assert.AreEqual(0, panel.RealizedCount, "an empty source clears the window");
        Assert.AreEqual(0, panel.FirstRealizedIndex);
        Assert.IsEmpty(panel.Items);
    }

    [TestMethod]
    public void ReplacingTheSourceRebindsKeptIndicesToTheNewItems()
    {
        var firstSource = Rows(50, "A");
        using var panel = new ProbeLayoutPanel { ItemsSource = firstSource };
        panel.SetWindow(
            (0, new RectangleF(0f, 0f, 40f, 30f), 0),
            (1, new RectangleF(40f, 0f, 40f, 30f), 1),
            (2, new RectangleF(0f, 30f, 40f, 30f), 2));
        Layout(panel);

        var firstVisuals = panel.Items.ToArray();
        var created = panel.Created;

        var secondSource = Rows(50, "B");
        panel.ItemsSource = secondSource;

        Assert.AreEqual(0, panel.RealizedCount,
            "replacing the source resets the realized mapping; the next arrange rebuilds the window");

        Layout(panel);

        Assert.AreEqual(created, panel.Created, "source replacement reuses the pooled visuals");
        Assert.AreEqual(3, panel.RealizedCount);
        CollectionAssert.AreEquivalent(firstVisuals, panel.Items.ToArray(),
            "the same visuals show the replacement source");
        for (var position = 0; position < panel.Items.Count; position++)
        {
            Assert.AreSame(secondSource[position], panel.Items[position].DataContext,
                "a kept source index must be rebound to the new item, never left on the old one");
        }

        Assert.AreEqual(3, panel.Recycled.Count, "the old mappings were recycled before the rebuild");
        foreach (var (index, item, _) in panel.Recycled)
        {
            Assert.AreSame(firstSource[index], item, "recycling reports the old item the visual showed");
        }
    }

    [TestMethod]
    public void ReplacingTheFactoryFreesTheOldVisualsAndBuildsFreshOnes()
    {
        var source = Rows(30, "A");
        using var panel = new ProbeLayoutPanel { ItemsSource = source };
        panel.ItemFactory = _ => new TaggedVisual("first");
        panel.SetWindow(
            (0, new RectangleF(0f, 0f, 40f, 30f), 0),
            (1, new RectangleF(40f, 0f, 40f, 30f), 1),
            (2, new RectangleF(0f, 30f, 40f, 30f), 2));
        Layout(panel);

        var oldVisuals = panel.CreatedVisuals.OfType<TaggedVisual>().ToArray();
        Assert.HasCount(3, oldVisuals);
        var createdWithOldFactory = panel.Created;

        panel.ItemFactory = _ => new TaggedVisual("second");
        Layout(panel);

        Assert.IsTrue(oldVisuals.All(static visual => visual.Disposed),
            "a pooled visual built by the old factory must be disposed before it can leak");
        Assert.HasCount(3, panel.Items);
        Assert.IsTrue(panel.Items.All(static visual => visual is TaggedVisual { Tag: "second" }),
            "the new factory built every visible visual");
        Assert.AreEqual(createdWithOldFactory + 3, panel.Created,
            "the factory change builds exactly one new visual per realized index");
    }

    [TestMethod]
    public void ReplacingTheItemTemplateFreesTheOldVisualsAndBuildsFreshOnes()
    {
        var source = Rows(30, "A");
        using var panel = new ProbeLayoutPanel { ItemsSource = source };
        panel.ItemTemplate = new DataTemplate(() => new TaggedVisual("first"));
        panel.SetWindow(
            (0, new RectangleF(0f, 0f, 40f, 30f), 0),
            (1, new RectangleF(40f, 0f, 40f, 30f), 1));
        Layout(panel);

        var oldVisuals = panel.CreatedVisuals.OfType<TaggedVisual>().ToArray();
        Assert.HasCount(2, oldVisuals);
        var createdWithOldTemplate = panel.Created;

        panel.ItemTemplate = new DataTemplate(() => new TaggedVisual("second"));
        Layout(panel);

        Assert.IsTrue(oldVisuals.All(static visual => visual.Disposed),
            "a template change must dispose the pooled visuals built from the old template");
        Assert.IsTrue(panel.Items.All(static visual => visual is TaggedVisual { Tag: "second" }),
            "the replacement template rebuilt every visible visual");
        Assert.AreEqual(createdWithOldTemplate + 2, panel.Created);
    }

    [TestMethod]
    public void PooledVisualsDoNotHoldTheOldItem()
    {
        var source = Rows(20, "A");
        using var panel = new ProbeLayoutPanel { ItemsSource = source };
        panel.SetWindow(
            (0, new RectangleF(0f, 0f, 40f, 30f), 0),
            (1, new RectangleF(40f, 0f, 40f, 30f), 1),
            (2, new RectangleF(0f, 30f, 40f, 30f), 2),
            (3, new RectangleF(40f, 30f, 40f, 30f), 3));
        Layout(panel);

        panel.SetWindow();
        Layout(panel);

        Assert.HasCount(4, panel.Recycled);
        foreach (var (_, _, element) in panel.Recycled)
        {
            Assert.IsNull(element.DataContext, "a pooled visual must not keep the item it last showed");
        }
    }

    [TestMethod]
    public void DisposeReleasesBothThePoolAndTheRealizedVisuals()
    {
        var source = Rows(60, "A");
        var panel = new ProbeLayoutPanel { ItemsSource = source };
        panel.ItemFactory = _ => new TaggedVisual("row");
        panel.SetWindow(
            (0, new RectangleF(0f, 0f, 40f, 30f), 0),
            (1, new RectangleF(40f, 0f, 40f, 30f), 1));
        Layout(panel);

        panel.SetWindow(
            (50, new RectangleF(0f, 0f, 40f, 30f), 0),
            (51, new RectangleF(40f, 0f, 40f, 30f), 1));
        Layout(panel);

        var created = panel.CreatedVisuals.OfType<TaggedVisual>().ToArray();
        Assert.HasCount(2, created, "the pool prevented a second pair from being created");

        panel.Dispose();

        Assert.IsTrue(created.All(static visual => visual.Disposed), "disposal frees every created visual, pooled or realized");
        Assert.AreEqual(0, panel.RealizedCount);
    }

    [TestMethod]
    public void SpatialIndexReturnsEachIndexOnceAcrossNegativeAndSpanningBounds()
    {
        var index = new VC.VirtualizationSpatialIndex(50f, 50f);
        index.Add(1, new RectangleF(-60f, -60f, 20f, 20f));
        index.Add(2, new RectangleF(-10f, -10f, 120f, 120f));
        index.Add(3, new RectangleF(500f, 500f, 20f, 20f));

        var hits = new List<int>();
        index.Query(new RectangleF(-100f, -100f, 200f, 200f), hits);

        CollectionAssert.AreEquivalent(new[] { 1, 2 }, hits, "an item registered in several cells is returned once");

        hits.Clear();
        index.Query(new RectangleF(-60f, -60f, 10f, 10f), hits);
        CollectionAssert.AreEquivalent(new[] { 1 }, hits, "negative cell coordinates resolve correctly");

        hits.Clear();
        index.Query(new RectangleF(490f, 490f, 40f, 40f), hits);
        CollectionAssert.AreEquivalent(new[] { 3 }, hits);

        index.Clear();
        hits.Clear();
        index.Query(new RectangleF(-1000f, -1000f, 4000f, 4000f), hits);
        Assert.IsEmpty(hits, "Clear drops every registration");
    }

    private sealed class ProbeLayoutPanel : VC.VirtualizingPanel
    {
        private IReadOnlyList<VC.VirtualizationItemLayout> _window = Array.Empty<VC.VirtualizationItemLayout>();

        public int Created { get; private set; }

        public List<VisualElement> CreatedVisuals { get; } = [];

        public List<(int Index, object? Item, VisualElement Element)> Recycled { get; } = [];

        public List<(int Index, VisualElement Element)> Rebound { get; } = [];

        public Size ReportedExtent { get; set; } = new(600, 600);

        public void SetWindow(params (int Index, RectangleF Bounds, int Order)[] records)
            => _window = records
                .Select(static record => new VC.VirtualizationItemLayout(record.Index, record.Bounds, record.Order))
                .ToList();

        protected override Size MeasureLayout(Size availableSize) => ReportedExtent;

        protected override VC.VirtualizationLayoutContext BuildLayout(VC.VirtualizationLayoutRequest request)
        {
            var ordered = _window.OrderBy(static record => record.Order).ToList();
            return new VC.VirtualizationLayoutContext(
                ordered,
                ReportedExtent,
                new RectangleF(request.OffsetX, request.OffsetY, request.Viewport.Width, request.Viewport.Height),
                request.OffsetX,
                request.OffsetY,
                SourceItems.Count);
        }

        protected override VisualElement CreateItemVisual(object? item)
        {
            Created++;
            var visual = base.CreateItemVisual(item);
            CreatedVisuals.Add(visual);
            return visual;
        }

        protected override void RecycleItem(int index, VisualElement element)
        {
            Recycled.Add((index, element.DataContext, element));
            base.RecycleItem(index, element);
        }

        protected override void BindRecycledItem(VisualElement element, int index)
        {
            Rebound.Add((index, element));
            base.BindRecycledItem(element, index);
        }
    }

    private sealed class TaggedVisual : VC.TextBlock
    {
        public TaggedVisual(string tag)
        {
            Tag = tag;
        }

        public string Tag { get; }

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

    private sealed class Row
    {
        public Row(int index, string tag)
        {
            Index = index;
            Tag = tag;
        }

        public int Index { get; }

        public string Tag { get; }

        public override string ToString() => $"{Tag}:{Index}";
    }

    private static List<object?> Rows(int count, string tag)
    {
        var rows = new List<object?>(count);
        for (var index = 0; index < count; index++)
        {
            rows.Add(new Row(index, tag));
        }

        return rows;
    }

    private static void Layout(VC.VirtualizingPanel panel)
    {
        panel.InvalidateMeasure();
        panel.Measure(new Size(320, 200));
        panel.Arrange(new RectangleF(0f, 0f, 320f, 200f));
    }
}
