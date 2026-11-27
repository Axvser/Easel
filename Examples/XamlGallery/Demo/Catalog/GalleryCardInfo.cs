namespace Demo;

/// <summary>The metadata a catalog row needs; it carries no visual factory so a view model can hold it.</summary>
internal sealed class GalleryCardInfo
{
    /// <summary>Creates a card's metadata.</summary>
    public GalleryCardInfo(string id, string group, string title, string summary, string usage)
    {
        Id = id;
        Group = group;
        Title = title;
        Summary = summary;
        Usage = usage;
    }

    /// <summary>The stable card id.</summary>
    public string Id { get; }

    /// <summary>The catalog group.</summary>
    public string Group { get; }

    /// <summary>The catalog section that owns <see cref="Group"/>.</summary>
    public string Section => GalleryTaxonomy.SectionOf(Group);

    /// <summary>The card title.</summary>
    public string Title { get; }

    /// <summary>The one-line summary.</summary>
    public string Summary { get; }

    /// <summary>The usage snippet.</summary>
    public string Usage { get; }
}

/// <summary>One catalog section under the filter that is currently active.</summary>
internal sealed class GalleryCardSection
{
    /// <summary>Creates a visible section.</summary>
    public GalleryCardSection(string name, IReadOnlyList<GalleryCardGroup> groups)
    {
        Name = name;
        Groups = groups;
    }

    /// <summary>The section name.</summary>
    public string Name { get; }

    /// <summary>The matching groups in catalog order.</summary>
    public IReadOnlyList<GalleryCardGroup> Groups { get; }
}

/// <summary>One catalog group under the filter that is currently active.</summary>
internal sealed class GalleryCardGroup
{
    /// <summary>Creates a visible group.</summary>
    public GalleryCardGroup(string name, IReadOnlyList<GalleryCardInfo> cards)
    {
        Name = name;
        Cards = cards;
    }

    /// <summary>The group name.</summary>
    public string Name { get; }

    /// <summary>The matching cards in catalog order.</summary>
    public IReadOnlyList<GalleryCardInfo> Cards { get; }
}
