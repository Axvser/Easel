using System.Drawing;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>
/// Differential coverage for the abstract engine: a full reference model recomputes the expected window on every pass
/// while a probe layout feeds random sparse, two-dimensional and negative-coordinate records. The pass compares the
/// visible set, the host order, the first index, and whether the pool was actually used instead of the factory.
/// </summary>
[STATestClass]
public sealed class VirtualizingEngineDifferentialTests
{
    [TestMethod]
    public void RandomSparseWindowsMatchTheReferenceModel()
    {
        var random = new Random(20261127);
        using var panel = new ProbeLayoutPanel();
        var source = NewSource(0, "seed");
        panel.ItemsSource = source;
        var realizedIndices = new HashSet<int>();
        var created = 0;

        for (var round = 0; round < 400; round++)
        {
            if (random.Next(12) == 0)
            {
                var replacement = NewSource(random.Next(0, 220), $"r{round}");
                panel.ItemsSource = replacement;
                source = replacement;

                Assert.AreEqual(0, panel.RealizedCount,
                    $"round {round}: replacing the source clears the realized mapping before the next arrange");
                Assert.AreEqual(created, panel.Created, $"round {round}: the rebuild does not create visuals by itself");
                realizedIndices.Clear();
            }

            var records = BuildRandomWindow(random, source.Count);
            var wanted = records.Select(static record => record.Index).ToHashSet();
            var hostOrder = records.OrderBy(static record => record.Order).ToList();

            panel.SetWindow(records.ToArray());
            panel.ReportedExtent = new Size(random.Next(120, 900), random.Next(120, 900));
            Layout(panel);

            // 离开窗口的视觉在本轮进入者之前就已回到池里，所以本轮可用的旧视觉是“已创建 - 保留”。
            // 等价地，本轮最多把已创建的视觉全部填满后才会调用工厂。
            var expectedCreates = Math.Max(0, wanted.Count - created);
            Assert.AreEqual(expectedCreates, panel.Created - created,
                $"round {round}: the pool supplies every entering visual it can before the factory runs " +
                $"(wanted {wanted.Count}, already created {created})");

            Assert.AreEqual(wanted.Count, panel.RealizedCount, $"round {round}: realized set size");
            Assert.AreEqual(wanted.Count, panel.Items.Count, $"round {round}: Items holds exactly the realized window");
            Assert.AreEqual(
                wanted.Count == 0 ? 0 : wanted.Min(),
                panel.FirstRealizedIndex,
                $"round {round}: FirstRealizedIndex is the minimum source index");

            for (var position = 0; position < hostOrder.Count; position++)
            {
                var record = hostOrder[position];
                Assert.AreSame(source[record.Index], panel.Items[position].DataContext,
                    $"round {round}: host position {position} shows source index {record.Index}");
                Assert.AreEqual(record.Bounds, panel.Items[position].Bounds,
                    $"round {round}: host position {position} is arranged at its record bounds");
            }

            var engineIndices = panel.Items.Select(visual => source.IndexOf(visual.DataContext!)).ToHashSet();
            CollectionAssert.AreEquivalent(
                wanted.Order().ToArray(),
                engineIndices.Order().ToArray(),
                $"round {round}: the engine realized exactly the requested indices");

            var itemSet = panel.Items.ToHashSet();
            Assert.HasCount(wanted.Count, itemSet, $"round {round}: no visual is realized twice");
            CollectionAssert.AreEqual(panel.Items.ToArray(), panel.Children.OfType<VisualElement>().ToArray(),
                $"round {round}: the host child order matches Items");
            Assert.IsTrue(panel.Items.All(panel.CreatedVisuals.Contains),
                $"round {round}: every realized visual came from the factory or the pool");

            foreach (var pooled in panel.CreatedVisuals.Where(visual => !itemSet.Contains(visual)))
            {
                Assert.IsNull(pooled.DataContext, $"round {round}: a pooled visual keeps no reference to the item it showed");
            }

            realizedIndices.Clear();
            realizedIndices.UnionWith(wanted);
            created = panel.Created;
        }

        Assert.IsGreaterThan(0, panel.Created, "the run actually realized something");
        Assert.IsGreaterThan(0, realizedIndices.Count, "the run ends with a realized window");
    }

    [TestMethod]
    public void ReferenceModelCoversZeroOneAndTwoItemSources()
    {
        using var panel = new ProbeLayoutPanel();

        foreach (var count in new[] { 0, 1, 2, 3, 5 })
        {
            var source = NewSource(count, $"n{count}");
            panel.ItemsSource = source;
            Assert.AreEqual(0, panel.RealizedCount, $"a {count}-item source starts with no realized window");

            // Reversed orders: tiny windows still place host positions by Order, not by index.
            var records = Enumerable.Range(0, count)
                .Select(index => new VC.VirtualizationItemLayout(
                    index,
                    new RectangleF(index * 50f, -index * 30f, 40f, 25f),
                    count - 1 - index))
                .ToList();
            panel.SetWindow(records);
            Layout(panel);

            Assert.AreEqual(count, panel.RealizedCount, $"a {count}-item source realizes exactly its items");
            Assert.AreEqual(0, panel.FirstRealizedIndex, $"a {count}-item source starts at index zero");
            for (var position = 0; position < count; position++)
            {
                var expectedIndex = count - 1 - position;
                Assert.AreSame(source[expectedIndex], panel.Items[position].DataContext,
                    $"{count} items: host position {position} shows the highest remaining order");
            }
        }
    }

    private static List<VC.VirtualizationItemLayout> BuildRandomWindow(Random random, int sourceCount)
    {
        if (sourceCount == 0)
        {
            return [];
        }

        var size = random.Next(0, Math.Min(sourceCount, 30) + 1);
        var indices = new HashSet<int>();
        while (indices.Count < size)
        {
            indices.Add(random.Next(sourceCount));
        }

        var indexList = indices.ToList();
        var orders = Enumerable.Range(0, size).OrderBy(_ => random.Next()).ToList();
        var records = new List<VC.VirtualizationItemLayout>(size);
        for (var position = 0; position < size; position++)
        {
            // Two-column-ish cells with negative coordinates: the engine must not care where the record sits.
            var column = random.Next(-4, 8);
            var row = random.Next(-4, 8);
            var bounds = new RectangleF((column * 56f) - 37f, (row * 41f) - 29f, 48f, 34f);
            records.Add(new VC.VirtualizationItemLayout(indexList[position], bounds, orders[position]));
        }

        return records;
    }

    private sealed class ProbeLayoutPanel : VC.VirtualizingPanel
    {
        private IReadOnlyList<VC.VirtualizationItemLayout> _window = Array.Empty<VC.VirtualizationItemLayout>();

        public int Created { get; private set; }

        public List<VisualElement> CreatedVisuals { get; } = [];

        public Size ReportedExtent { get; set; } = new(600, 600);

        public void SetWindow(IReadOnlyList<VC.VirtualizationItemLayout> records) => _window = records;

        protected override Size MeasureLayout(Size availableSize) => ReportedExtent;

        protected override VC.VirtualizationLayoutContext BuildLayout(VC.VirtualizationLayoutRequest request)
            => new(
                _window.OrderBy(static record => record.Order).ToList(),
                ReportedExtent,
                new RectangleF(request.OffsetX, request.OffsetY, request.Viewport.Width, request.Viewport.Height),
                request.OffsetX,
                request.OffsetY,
                SourceItems.Count);

        protected override VisualElement CreateItemVisual(object? item)
        {
            Created++;
            var visual = base.CreateItemVisual(item);
            CreatedVisuals.Add(visual);
            return visual;
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

    private static List<object?> NewSource(int count, string tag)
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
