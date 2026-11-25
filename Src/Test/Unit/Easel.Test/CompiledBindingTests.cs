using System.ComponentModel;
using Easel;
using Easel.Visuals;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>The runtime side of <see cref="CompiledBinding"/>: reader resolution, change tracking and write-back.</summary>
[STATestClass]
public sealed class CompiledBindingTests
{
    [TestMethod]
    public void OneWayCompiledBinding_FollowsTheSource()
    {
        var model = new ProbeModel { Name = "first" };
        var text = new VC.TextBlock();
        text.SetBinding(
            VC.TextBlock.TextProperty,
            new CompiledBinding(nameof(ProbeModel.Name), ReadName) { Source = model });

        Assert.AreEqual("first", text.Text);

        model.Name = "second";
        Assert.AreEqual("second", text.Text);
    }

    [TestMethod]
    public void TwoWayCompiledBinding_WritesBackToTheSource()
    {
        var model = new ProbeModel { Name = "source" };
        var box = new VC.TextBox();
        box.SetBinding(
            VC.TextBox.TextProperty,
            new CompiledBinding(nameof(ProbeModel.Name), ReadName)
            {
                Source = model,
                Mode = BindingMode.TwoWay,
            });

        box.Text = "target";

        Assert.AreEqual("target", model.Name);
    }

    [TestMethod]
    public void NestedCompiledBinding_TracksEveryNodeAndReattaches()
    {
        var first = new ProbeModel { Name = "first" };
        var root = new RootModel { Child = first };
        var text = new VC.TextBlock();
        text.SetBinding(
            VC.TextBlock.TextProperty,
            new CompiledBinding("Child.Name", ReadChildName) { Source = root });

        Assert.AreEqual("first", text.Text);

        first.Name = "renamed";
        Assert.AreEqual("renamed", text.Text, "the leaf node is tracked");

        root.Child = new ProbeModel { Name = "replacement" };
        Assert.AreEqual("replacement", text.Text, "an intermediate change re-resolves the path");
    }

    [TestMethod]
    public void CompiledBinding_WrongSourceTypeUsesTheFallback()
    {
        var text = new VC.TextBlock();
        text.SetBinding(
            VC.TextBlock.TextProperty,
            new CompiledBinding("Name", ReadName)
            {
                Source = new object(),
                FallbackValue = "fallback",
            });

        Assert.AreEqual("fallback", text.Text);
    }

    [TestMethod]
    public void OneTimeCompiledBinding_DoesNotFollowLaterChanges()
    {
        var model = new ProbeModel { Name = "first" };
        var text = new VC.TextBlock();
        text.SetBinding(
            VC.TextBlock.TextProperty,
            new CompiledBinding("Name", ReadName)
            {
                Source = model,
                Mode = BindingMode.OneTime,
            });

        model.Name = "second";

        Assert.AreEqual("first", text.Text);
    }

    [TestMethod]
    public void CompiledIndexer_ReadsAndWritesThroughTheHelpers()
    {
        var rows = new List<ProbeModel> { new() { Name = "row" } };
        var box = new VC.TextBox();
        box.SetBinding(
            VC.TextBox.TextProperty,
            new CompiledBinding("Rows[0].Name", ReadRowName)
            {
                Source = rows,
                Mode = BindingMode.TwoWay,
            });

        Assert.AreEqual("row", box.Text);

        box.Text = "edited";
        Assert.AreEqual("edited", rows[0].Name);
    }

    [TestMethod]
    public void TryGetIndex_HandlesArraysListsAndDictionaries()
    {
        var array = new[] { "a", "b" };
        Assert.IsTrue(CompiledBinding.TryGetIndex(array, 1, out var fromArray));
        Assert.AreEqual("b", fromArray);
        Assert.IsFalse(CompiledBinding.TryGetIndex(array, 2, out _));

        IList<string> list = ["c"];
        Assert.IsTrue(CompiledBinding.TryGetIndex(list, 0, out var fromList));
        Assert.AreEqual("c", fromList);

        IDictionary<string, int> dictionary = new Dictionary<string, int> { ["k"] = 4 };
        Assert.IsTrue(CompiledBinding.TryGetIndex(dictionary, "k", out var fromDictionary));
        Assert.AreEqual(4, fromDictionary);
        Assert.IsFalse(CompiledBinding.TryGetIndex(dictionary, "missing", out _));
    }

    private static bool ReadName(
        object? source,
        List<object?> nodes,
        out object? value,
        out Type valueType,
        out Action<object?>? write)
    {
        value = null;
        valueType = typeof(object);
        write = null;

        if (source is not ProbeModel model)
        {
            return false;
        }

        nodes.Add(model);
        value = model.Name;
        valueType = CompiledBinding.TypeOf(model.Name);
        write = CompiledBinding.CreateWriter(
            model,
            static (owner, incoming) => owner.Name = CompiledBinding.Coerce(owner.Name, incoming));
        return true;
    }

    private static bool ReadChildName(
        object? source,
        List<object?> nodes,
        out object? value,
        out Type valueType,
        out Action<object?>? write)
    {
        value = null;
        valueType = typeof(object);
        write = null;

        if (source is not RootModel root)
        {
            return false;
        }

        nodes.Add(root);
        var child = root.Child;
        if (child is null)
        {
            return false;
        }

        nodes.Add(child);
        value = child.Name;
        valueType = CompiledBinding.TypeOf(child.Name);
        write = CompiledBinding.CreateWriter(
            child,
            static (owner, incoming) => owner.Name = CompiledBinding.Coerce(owner.Name, incoming));
        return true;
    }

    private static bool ReadRowName(
        object? source,
        List<object?> nodes,
        out object? value,
        out Type valueType,
        out Action<object?>? write)
    {
        value = null;
        valueType = typeof(object);
        write = null;

        if (source is not List<ProbeModel> rows)
        {
            return false;
        }

        nodes.Add(rows);
        if (!CompiledBinding.TryGetIndex(rows, 0, out var row) || row is null)
        {
            return false;
        }

        nodes.Add(row);
        value = row.Name;
        valueType = CompiledBinding.TypeOf(row.Name);
        write = CompiledBinding.CreateWriter(
            row,
            static (owner, incoming) => owner.Name = CompiledBinding.Coerce(owner.Name, incoming));
        return true;
    }

    private sealed class RootModel : INotifyPropertyChanged
    {
        private ProbeModel? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ProbeModel? Child
        {
            get => _child;
            set
            {
                if (ReferenceEquals(_child, value))
                {
                    return;
                }

                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }
    }

    private sealed class ProbeModel : INotifyPropertyChanged
    {
        private string? _name;

        public event PropertyChangedEventHandler? PropertyChanged;

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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }
    }
}
