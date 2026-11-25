using System.ComponentModel;
using System.Drawing;
using Easel.Visuals.Spatial;

namespace Easel.Test;

/// <summary>The generic spatial grid: insertion, bounds changes, union bounds and the visible-set query.</summary>
[STATestClass]
public sealed class SpatialIndexTests
{
    [TestMethod]
    public void QueryReturnsOnlyTheItemsIntersectingTheViewport()
    {
        var map = new SpatialGridHashMap<TestItem>(50d);
        var a = new TestItem(new RectangleF(0f, 0f, 40f, 40f));
        var b = new TestItem(new RectangleF(200f, 200f, 40f, 40f));
        var c = new TestItem(new RectangleF(0f, 120f, 40f, 40f));
        map.Insert(a);
        map.Insert(b);
        map.Insert(c);

        var hits = map.Query(new RectangleF(-10f, -10f, 80f, 80f)).ToList();

        CollectionAssert.AreEqual(new[] { a }, hits, "only the item touching the query rectangle is returned");
        Assert.IsEmpty(map.Query(RectangleF.Empty).ToList(), "an empty viewport has no hits");
    }

    [TestMethod]
    public void MovingAnItemRefilesItFromTheOldCells()
    {
        var map = new SpatialGridHashMap<TestItem>(50d);
        var item = new TestItem(new RectangleF(0f, 0f, 40f, 40f));
        map.Insert(item);

        Assert.HasCount(1, map.Query(new RectangleF(0f, 0f, 50f, 50f)).ToList());
        item.MoveTo(new RectangleF(300f, 300f, 40f, 40f));

        Assert.IsEmpty(map.Query(new RectangleF(0f, 0f, 50f, 50f)).ToList(), "the old cells no longer hold the item");
        Assert.HasCount(1, map.Query(new RectangleF(290f, 290f, 80f, 80f)).ToList(), "the new cells hold it");
    }

    [TestMethod]
    public void BoundsUnionTracksInsertionRemovalAndClear()
    {
        var map = new SpatialGridHashMap<TestItem>(25d);
        var a = new TestItem(new RectangleF(0f, 0f, 10f, 10f));
        var b = new TestItem(new RectangleF(100f, 60f, 20f, 20f));
        map.Insert(a);
        map.Insert(b);

        var union = map.Bounds;
        Assert.AreEqual(0f, union.Left, 0.1f);
        Assert.AreEqual(0f, union.Top, 0.1f);
        Assert.AreEqual(120f, union.Right, 0.1f);
        Assert.AreEqual(80f, union.Bottom, 0.1f);

        map.Remove(a);
        union = map.Bounds;
        Assert.AreEqual(100f, union.Left, 0.1f);
        Assert.AreEqual(60f, union.Top, 0.1f);

        map.Clear();
        Assert.AreEqual(RectangleF.Empty, map.Bounds);
        Assert.IsEmpty(map.Query(new RectangleF(-1000f, -1000f, 4000f, 4000f)).ToList());
    }

    [TestMethod]
    public void CollectIntoFillsACallerBuffer()
    {
        var map = new SpatialGridHashMap<TestItem>(50d);
        var a = new TestItem(new RectangleF(0f, 0f, 20f, 20f));
        var b = new TestItem(new RectangleF(30f, 0f, 20f, 20f));
        map.Insert(a);
        map.Insert(b);

        var buffer = new List<TestItem>();
        map.CollectInto(new RectangleF(0f, 0f, 100f, 100f), buffer);

        Assert.HasCount(2, buffer);
    }

    [TestMethod]
    public void VirtualizationExpandsThroughAdjacencyByDepth()
    {
        var map = new SpatialGridHashMap<TestItem>(50d);
        var a = new TestItem(new RectangleF(0f, 0f, 20f, 20f));
        var b = new TestItem(new RectangleF(500f, 0f, 20f, 20f));
        var c = new TestItem(new RectangleF(900f, 0f, 20f, 20f));
        a.Neighbors.Add(b);
        b.Neighbors.Add(a);
        b.Neighbors.Add(c);
        c.Neighbors.Add(b);
        map.Insert(a);
        map.Insert(b);
        map.Insert(c);

        var query = new SpatialVirtualization<TestItem>(map, item => item.Neighbors);
        var viewport = new RectangleF(0f, 0f, 50f, 50f);

        CollectionAssert.AreEqual(new[] { a }, query.CollectVisible(viewport, 0).ToArray());
        CollectionAssert.AreEqual(new[] { a, b }, query.CollectVisible(viewport, 1).ToArray());
        CollectionAssert.AreEqual(new[] { a, b, c }, query.CollectVisible(viewport, 2).ToArray());
    }

    private sealed class TestItem : ISpatialBoundsProvider
    {
        private RectangleF _bounds;

        public TestItem(RectangleF bounds) => _bounds = bounds;

        public event PropertyChangedEventHandler? PropertyChanged;

        public RectangleF Bounds => _bounds;

        public List<TestItem> Neighbors { get; } = new();

        public void MoveTo(RectangleF bounds)
        {
            if (_bounds == bounds)
            {
                return;
            }

            _bounds = bounds;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Bounds)));
        }
    }
}
