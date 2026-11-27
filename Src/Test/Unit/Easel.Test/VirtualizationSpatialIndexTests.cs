using System.Drawing;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Coverage for <see cref="VC.VirtualizationSpatialIndex"/>: cross-cell registration, negative coordinates, the
/// right/bottom edge epsilon, empty cells, de-duplication and a randomized comparison against a cell-level brute force.
/// </summary>
[STATestClass]
public sealed class VirtualizationSpatialIndexTests
{
    [TestMethod]
    public void ASpanningItemIsReturnedOnceForAnyCellItCovers()
    {
        var index = new VC.VirtualizationSpatialIndex(50f, 50f);
        index.Add(7, new RectangleF(10f, 10f, 120f, 30f)); // 跨第 0、1、2 列
        index.Add(9, new RectangleF(400f, 400f, 20f, 20f));

        var results = new List<int>();

        index.Query(new RectangleF(0f, 0f, 50f, 50f), results);
        CollectionAssert.AreEqual(new[] { 7 }, results);

        results.Clear();
        index.Query(new RectangleF(60f, 0f, 50f, 50f), results);
        CollectionAssert.AreEqual(new[] { 7 }, results);

        results.Clear();
        index.Query(new RectangleF(0f, 0f, 500f, 500f), results);
        CollectionAssert.AreEqual(new[] { 7, 9 }, results, "a wide query crossing both items keeps insertion order once");
        Assert.AreEqual(results.Count, results.Distinct().Count(), "no index is reported twice");
    }

    [TestMethod]
    public void NegativeCoordinatesAreIndexed()
    {
        var index = new VC.VirtualizationSpatialIndex(40f, 40f);
        index.Add(1, new RectangleF(-35f, -35f, 20f, 20f));
        index.Add(2, new RectangleF(-80f, 10f, 30f, 30f));

        var results = new List<int>();

        index.Query(new RectangleF(-40f, -40f, 10f, 10f), results);
        CollectionAssert.AreEqual(new[] { 1 }, results, "an item left of and above the origin is found");

        results.Clear();
        index.Query(new RectangleF(-100f, -100f, 200f, 200f), results);
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, results,
            "cell scan order is not item insertion order, so compare as sets");
    }

    [TestMethod]
    public void RightAndBottomEdgesBelongToTheLastCellOnly()
    {
        var index = new VC.VirtualizationSpatialIndex(50f, 50f);
        index.Add(0, new RectangleF(0f, 0f, 50f, 50f)); // 右/下边缘恰好落在格线上

        var results = new List<int>();
        index.Query(new RectangleF(50f, 0f, 10f, 50f), results);
        Assert.IsEmpty(results, "an edge exactly on the next cell line does not leak into that cell");

        results.Clear();
        index.Query(new RectangleF(49f, 49f, 1f, 1f), results);
        CollectionAssert.AreEqual(new[] { 0 }, results, "the closing edge still belongs to its own cell");

        var startingAtBoundary = new VC.VirtualizationSpatialIndex(50f, 50f);
        startingAtBoundary.Add(3, new RectangleF(50f, 50f, 10f, 10f));
        results.Clear();
        startingAtBoundary.Query(new RectangleF(50f, 50f, 10f, 10f), results);
        CollectionAssert.AreEqual(new[] { 3 }, results);
    }

    [TestMethod]
    public void EmptyCellsAndClearedIndexesReturnNothing()
    {
        var index = new VC.VirtualizationSpatialIndex(25f, 25f);
        index.Add(1, new RectangleF(0f, 0f, 10f, 10f));

        var results = new List<int>();
        index.Query(new RectangleF(500f, 500f, 50f, 50f), results);
        Assert.IsEmpty(results, "a query over untouched cells finds nothing");

        index.Clear();
        results.Clear();
        index.Query(new RectangleF(-500f, -500f, 2000f, 2000f), results);
        Assert.IsEmpty(results, "Clear drops every registration");
    }

    [TestMethod]
    public void QueryReturnsScatteredIndicesNotAStride()
    {
        var index = new VC.VirtualizationSpatialIndex(100f, 100f);
        // 只把 1、4、9 放在同一行、不相邻的格里，其余格放别的项。
        index.Add(1, new RectangleF(0f, 0f, 10f, 10f));
        index.Add(4, new RectangleF(200f, 0f, 10f, 10f));
        index.Add(9, new RectangleF(400f, 0f, 10f, 10f));
        index.Add(2, new RectangleF(0f, 100f, 10f, 10f));

        var results = new List<int>();
        index.Query(new RectangleF(0f, 0f, 500f, 50f), results);

        CollectionAssert.AreEquivalent(new[] { 1, 4, 9 }, results,
            "only the items on the queried row are returned, with non-consecutive indices");
    }

    [TestMethod]
    public void RandomQueriesMatchACellLevelBruteForce()
    {
        const float cell = 30f;
        var index = new VC.VirtualizationSpatialIndex(cell, cell);
        var items = new List<(int Index, RectangleF Bounds)>();
        var random = new Random(20261127);

        for (var i = 0; i < 400; i++)
        {
            var bounds = new RectangleF(
                random.Next(-120, 900),
                random.Next(-120, 900),
                random.Next(1, 160),
                random.Next(1, 160));
            items.Add((i, bounds));
            index.Add(i, bounds);
        }

        var results = new List<int>();
        for (var query = 0; query < 200; query++)
        {
            var viewport = new RectangleF(
                random.Next(-150, 950),
                random.Next(-150, 950),
                random.Next(0, 250),
                random.Next(0, 250));

            results.Clear();
            index.Query(viewport, results);

            Assert.AreEqual(results.Count, results.Distinct().Count(), "a query never repeats an index");

            var expected = BruteCellQuery(items, viewport, cell);
            CollectionAssert.AreEquivalent(expected, results,
                $"query {viewport} must match the brute force cell scan");

            // 真正相交的项一定在其中；索引允许把同格但未相交的项作为候选返回。
            foreach (var (indexValue, bounds) in items)
            {
                if (bounds.IntersectsWith(viewport))
                {
                    Assert.IsTrue(results.Contains(indexValue),
                        $"index {indexValue} intersects the query and must be a candidate");
                }
            }
        }
    }

    private static List<int> BruteCellQuery(
        List<(int Index, RectangleF Bounds)> items, RectangleF viewport, float cell)
    {
        var expected = new List<int>();
        var query = CellRange(viewport, cell);
        if (query.Right < query.Left || query.Bottom < query.Top)
        {
            // 零尺寸查询正好落在格线上时右/下边界退到左/上之前，等同于不覆盖任何格。
            return expected;
        }
        foreach (var (index, bounds) in items)
        {
            var item = CellRange(bounds, cell);
            if (item.Left <= query.Right && query.Left <= item.Right
                && item.Top <= query.Bottom && query.Top <= item.Bottom)
            {
                expected.Add(index);
            }
        }

        return expected;
    }

    private static (int Left, int Top, int Right, int Bottom) CellRange(RectangleF bounds, float cell)
        => ((int)Math.Floor(bounds.Left / cell),
            (int)Math.Floor(bounds.Top / cell),
            (int)Math.Floor((bounds.Right - 0.001f) / cell),
            (int)Math.Floor((bounds.Bottom - 0.001f) / cell));
}
