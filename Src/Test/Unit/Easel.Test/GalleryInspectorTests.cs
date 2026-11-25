using Demo;
using Easel;
using Easel.Controls;
using Easel.Visuals;
using Easel.Visuals.Controls;
using VC = Easel.Visuals.Controls;

namespace Easel.Test;

/// <summary>The gallery inspector reuses its rows and rebinds them to the selected card.</summary>
[TestClass]
public sealed class GalleryInspectorTests
{
    private static StackPanel RowFor(VisualElement root, string label)
    {
        foreach (var child in root.Children)
        {
            if (child is StackPanel row
                && row.Children.FirstOrDefault(candidate => candidate is TextBlock) is TextBlock text
                && text.Text == label)
            {
                return row;
            }
        }

        throw new AssertFailedException($"inspector row '{label}' not found");
    }

    [TestMethod]
    public void TextRowWritesBackToTheTarget()
    {
        var inspector = new GalleryVisualInspector(new ResourceDictionary());
        var target = new Border { Opacity = 1d };
        inspector.SetTarget(target);

        var editor = (VC.TextBox)RowFor(inspector.Root, "Opacity").Children.First(child => child is VC.TextBox);
        editor.Text = "0.5";

        Assert.AreEqual(0.5d, target.Opacity, 0.0001d);
    }

    [TestMethod]
    public void RowsAreReusedAndRetargeted()
    {
        var inspector = new GalleryVisualInspector(new ResourceDictionary());
        var first = new Border { Opacity = 0.5d };
        var second = new Border { Opacity = 1d };

        inspector.SetTarget(first);
        var editor = (VC.TextBox)RowFor(inspector.Root, "Opacity").Children.First(child => child is VC.TextBox);
        editor.Text = "0.25";

        inspector.SetTarget(second);
        var reused = (VC.TextBox)RowFor(inspector.Root, "Opacity").Children.First(child => child is VC.TextBox);
        reused.Text = "0.75";

        Assert.AreSame(editor, reused, "the same property row should keep its editor");
        Assert.AreEqual(0.25d, first.Opacity, 0.0001d);
        Assert.AreEqual(0.75d, second.Opacity, 0.0001d);
    }

    [TestMethod]
    public void BooleanAndEnumRowsWriteBackToTheTarget()
    {
        var inspector = new GalleryVisualInspector(new ResourceDictionary());
        var target = new Border();
        inspector.SetTarget(target);

        var check = (VC.CheckBox)RowFor(inspector.Root, "IsEnabled").Children.First(child => child is VC.CheckBox);
        check.IsChecked = false;
        Assert.IsFalse(target.IsEnabled);

        var combo = (VC.ComboBox)RowFor(inspector.Root, "Visibility").Children.First(child => child is VC.ComboBox);
        combo.SelectedItem = nameof(Visibility.Collapsed);
        Assert.AreEqual(Visibility.Collapsed, target.Visibility);
    }

    [TestMethod]
    public void NullTargetHidesEveryRow()
    {
        var inspector = new GalleryVisualInspector(new ResourceDictionary());
        inspector.SetTarget(new Border());
        inspector.SetTarget(null);

        foreach (var child in inspector.Root.Children)
        {
            if (child is StackPanel row)
            {
                Assert.AreEqual(Visibility.Collapsed, row.Visibility);
            }
        }
    }

    [TestMethod]
    public void PreviewDataContextIsPinnedLocally()
    {
        var preview = new StackPanel();
        preview.Add(new TextBlock());

        Assert.IsFalse(preview.IsSet(VisualElement.DataContextProperty));
        Assert.AreSame(preview, GalleryVisualShell.IsolatePreviewDataContext(preview));
        Assert.IsTrue(preview.IsSet(VisualElement.DataContextProperty));
        Assert.IsNull(preview.DataContext);
    }

    [TestMethod]
    public void PreviewKeepsItsOwnDataContext()
    {
        var preview = new StackPanel { DataContext = "card" };

        GalleryVisualShell.IsolatePreviewDataContext(preview);

        Assert.AreEqual("card", preview.DataContext);
    }
}
