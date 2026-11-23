using Easel.Visuals;

namespace Demo;

/// <summary>One demo entry: metadata, its usage snippet and the XAML view factory that builds it.</summary>
/// <remarks>A card factory only constructs a view; every visual inside that view is authored in XAML.</remarks>
internal sealed record GalleryCard(GalleryCardInfo Info, Func<GalleryModel, VisualElement> Create)
{
    /// <summary>The card id.</summary>
    public string Id => Info.Id;

    /// <summary>The catalog group.</summary>
    public string Group => Info.Group;

    /// <summary>The card title.</summary>
    public string Title => Info.Title;

    /// <summary>The card summary.</summary>
    public string Summary => Info.Summary;

    /// <summary>The usage snippet.</summary>
    public string Usage => Info.Usage;
}

/// <summary>Shared builders used by a few cards that still need data-bound child rows.</summary>
internal static class CardVisuals
{
    /// <summary>Builds a tree item for a sample node, recursively.</summary>
    public static Easel.Visuals.Controls.TreeViewItem BuildTreeItem(GalleryNode node)
    {
        var item = new Easel.Visuals.Controls.TreeViewItem { Header = node.Name, IsExpanded = true };
        foreach (var child in node.Children)
        {
            item.Items.Add(BuildTreeItem(child));
        }

        return item;
    }
}
