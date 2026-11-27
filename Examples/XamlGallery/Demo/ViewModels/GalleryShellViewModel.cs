using Demo.Mvvm;

namespace Demo;

/// <summary>The gallery shell state: filter text, visible groups, selected card and the derived chrome text.</summary>
/// <remarks>
/// The view model owns only catalog metadata and strings. The visual factory, tree-item construction, preview
/// disposal and inspector rows stay in the view; switching the palette rebuilds the view with this same instance.
/// </remarks>
internal sealed class GalleryShellViewModel : ObservableObject, IDisposable
{
    private readonly IReadOnlyList<GalleryCardInfo> _cards;
    private readonly IThemeService _theme;
    private readonly List<GalleryCardInfo> _visible = [];
    private readonly List<GalleryCardGroup> _groups = [];
    private bool _hadMatches;

    private string _filterText = string.Empty;
    private GalleryCardInfo? _selectedCard;
    private int _themeIndex;

    /// <summary>Text used to filter the catalog.</summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
            {
                OnFilterTextChanged();
            }
        }
    }

    /// <summary>The card currently shown in the preview.</summary>
    public GalleryCardInfo? SelectedCard
    {
        get => _selectedCard;
        set
        {
            if (SetProperty(ref _selectedCard, value))
            {
                OnSelectedCardChanged(value);
            }
        }
    }

    /// <summary>The selected palette index.</summary>
    public int ThemeIndex
    {
        get => _themeIndex;
        set
        {
            if (SetProperty(ref _themeIndex, value))
            {
                OnThemeIndexChanged(value);
            }
        }
    }

    /// <summary>Creates the shell state and selects the first visible card.</summary>
    /// <param name="cards">The catalog metadata, in catalog order.</param>
    /// <param name="theme">The palette service.</param>
    public GalleryShellViewModel(IReadOnlyList<GalleryCardInfo> cards, IThemeService theme)
    {
        _cards = cards;
        _theme = theme;
        _themeIndex = theme.SelectedIndex;
        ApplyFilterCore();
        _selectedCard = _visible.FirstOrDefault() ?? _cards.FirstOrDefault();
        _hadMatches = HasMatches;
        _theme.Changed += OnThemeChanged;
    }

    /// <summary>Raised after the filter changed the visible groups.</summary>
    public event EventHandler? CatalogChanged;

    /// <summary>Raised when the preview should show a card, or clear itself for <see langword="null"/>.</summary>
    public event EventHandler<GalleryCardInfo?>? PreviewChanged;

    /// <summary>The groups that match the current filter, in first-appearance order.</summary>
    public IReadOnlyList<GalleryCardGroup> VisibleGroups => _groups;

    /// <summary>Whether anything matches the filter.</summary>
    public bool HasMatches => _visible.Count > 0;

    /// <summary>The preview title.</summary>
    public string Title => !HasMatches ? "No matches" : SelectedCard?.Title ?? string.Empty;

    /// <summary>The preview summary.</summary>
    public string Summary => !HasMatches
        ? "Clear the filter to show the catalog again."
        : SelectedCard?.Summary ?? string.Empty;

    /// <summary>The usage snippet of the selected card.</summary>
    public string Usage => HasMatches ? SelectedCard?.Usage ?? string.Empty : string.Empty;

    /// <summary>The status-bar text.</summary>
    public string StatusText => !HasMatches
        ? "No matches"
        : SelectedCard is { } card ? $"{card.Group} · {card.Id}" : "Ready";

    /// <summary>The four theme choices shown by the selector.</summary>
    public IReadOnlyList<string> ThemeOptions => _theme.Options;

    /// <summary>Selects a card by id; a card hidden by the filter clears the filter first.</summary>
    /// <param name="id">The card id.</param>
    /// <returns><see langword="false"/> when the id is unknown.</returns>
    public bool SelectCard(string id)
    {
        var card = _cards.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        if (card is null)
        {
            return false;
        }

        if (!_visible.Any(candidate => candidate.Id == card.Id))
        {
            FilterText = string.Empty;
        }

        var alreadySelected = ReferenceEquals(SelectedCard, card);
        SelectedCard = card;
        if (alreadySelected)
        {
            // 生成属性在值相同时不发通知；捕捉/重复选择仍要重建预览。
            PreviewChanged?.Invoke(this, card);
        }

        return true;
    }

    /// <inheritdoc />
    public void Dispose() => _theme.Changed -= OnThemeChanged;

    private void OnThemeIndexChanged(int value)
        => _theme.SelectIndex(value);

    private void OnFilterTextChanged()
    {
        ApplyFilterCore();
        RaiseComputed();
        if (HasMatches)
        {
            if (SelectedCard is null || !_visible.Any(card => card.Id == SelectedCard.Id))
            {
                SelectedCard = _visible[0];
            }
            else if (!_hadMatches)
            {
                // 筛选从“无匹配”回到有匹配时，选中的卡还是同一张，必须再请视图重建预览。
                PreviewChanged?.Invoke(this, SelectedCard);
            }
        }
        else
        {
            PreviewChanged?.Invoke(this, null);
        }

        _hadMatches = HasMatches;
        CatalogChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectedCardChanged(GalleryCardInfo? newValue)
    {
        RaiseComputed();
        PreviewChanged?.Invoke(this, newValue);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        var index = _theme.SelectedIndex;
        if (_themeIndex != index)
        {
            ThemeIndex = index;
        }
    }

    private void RaiseComputed()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(Usage));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasMatches));
    }

    private void ApplyFilterCore()
    {
        var filter = _filterText.Trim();
        _visible.Clear();
        _groups.Clear();
        foreach (var group in _cards.GroupBy(static card => card.Group, StringComparer.Ordinal))
        {
            var matches = group.Where(card => Matches(card, filter)).ToList();
            if (matches.Count == 0)
            {
                continue;
            }

            _visible.AddRange(matches);
            _groups.Add(new GalleryCardGroup(group.Key, matches));
        }

        OnPropertyChanged(nameof(VisibleGroups));
        OnPropertyChanged(nameof(HasMatches));
    }

    private static bool Matches(GalleryCardInfo card, string filter)
        => filter.Length == 0
           || card.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)
           || card.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
           || card.Summary.Contains(filter, StringComparison.OrdinalIgnoreCase)
           || card.Group.Contains(filter, StringComparison.OrdinalIgnoreCase);
}
