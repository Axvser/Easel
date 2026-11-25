using Easel.Visuals;
using Easel.Visuals.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Easel.Test;

[TestClass]
public sealed class InheritedPropertyPropagationTests
{
    [TestMethod]
    public void DataContextReachesADeepChainOncePerChange()
    {
        var root = new StackPanel();
        var current = root;
        var leaves = new List<TextBlock>();
        for (var i = 0; i < 10; i++)
        {
            var child = new TextBlock { Text = i.ToString() };
            current.Add(child);
            leaves.Add(child);
            var next = new StackPanel();
            current.Add(next);
            current = next;
        }

        var leaf = leaves[^1];
        var notifications = 0;
        leaf.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, VisualElement.DataContextProperty))
            {
                notifications++;
            }
        };

        var first = new object();
        root.DataContext = first;
        Assert.AreEqual(1, notifications, "one inherited DataContext change must notify a leaf exactly once");
        Assert.AreSame(first, leaf.DataContext);

        var second = new object();
        root.DataContext = second;
        Assert.AreEqual(2, notifications, "each inherited DataContext change must notify a leaf exactly once");
        Assert.AreSame(second, leaf.DataContext);
    }

    [TestMethod]
    public void DataContextStopsAtALocalValue()
    {
        var root = new StackPanel();
        var middle = new StackPanel();
        var leaf = new TextBlock();
        middle.Add(leaf);
        root.Add(middle);
        var local = new object();
        middle.DataContext = local;

        var notifications = 0;
        leaf.PropertyChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Property, VisualElement.DataContextProperty))
            {
                notifications++;
            }
        };

        root.DataContext = new object();
        Assert.AreEqual(0, notifications);
        Assert.AreSame(local, leaf.DataContext);
    }
}
