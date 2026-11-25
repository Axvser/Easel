using System.Drawing;
using Easel;
using Easel.Visuals;
using Demo.Views;
using VC = Easel.Visuals.Controls;
using XamlVisibility = Easel.Controls.Visibility;

namespace Demo;

/// <summary>The three-pane demo workbench: catalog, preview and dependency-property inspector.</summary>
/// <remarks>
/// The layout, chrome, bindings and commands are authored in <c>Views/GalleryVisualShell.xaml</c>. The code below
/// keeps only view behaviour: building catalog rows, swapping the preview and inspector, and splitter targets.
/// </remarks>
internal sealed partial class GalleryVisualShell : ThemedDockPanel
{
    private readonly GalleryModel _model;
    private readonly GalleryShellViewModel _vm;
    private readonly GalleryCardCatalog _catalog;
    private readonly Dictionary<VC.TreeViewItem, string> _entryItems = [];
    private GalleryVisualInspector _inspector;
    private bool _syncingSelection;

    /// <summary>Builds the shell for an existing view model and selection.</summary>
    /// <param name="model">The shared sample data used by the card views.</param>
    /// <param name="vm">The shell state; it survives palette rebuilds.</param>
    /// <param name="catalog">The card metadata/factory pairs.</param>
    public GalleryVisualShell(GalleryModel model, GalleryShellViewModel vm, GalleryCardCatalog catalog)
    {
        _model = model;
        _vm = vm;
        _catalog = catalog;

        // Bindings resolve against the inherited DataContext while InitializeComponent builds the tree.
        DataContext = vm;
        InitializeComponent();

        _inspector = new GalleryVisualInspector(Resources);
        InspectorHost.Content = _inspector.Root;
        LeftSplitter.Target = CatalogPane;
        RightSplitter.Target = InspectorPane;

        _vm.CatalogChanged += OnCatalogChanged;
        _vm.PreviewChanged += OnPreviewChanged;
        RebuildCatalog();
        if (_vm.SelectedCard is { } initial)
        {
            ShowCard(initial);
        }
    }

    /// <summary>The visual root of the demo shell.</summary>
    public VisualElement Root => this;

    /// <summary>Every card in catalog order.</summary>
    public IReadOnlyList<GalleryCard> Cards => _catalog.Cards;

    /// <summary>The selected card id, or <see langword="null"/>.</summary>
    public string? SelectedId => _vm.SelectedCard?.Id;

    /// <summary>The current catalog filter text.</summary>
    public string FilterText => _vm.FilterText;

    /// <summary>Selects a card and updates the preview and inspector.</summary>
    /// <param name="id">The card id.</param>
    /// <returns><see langword="false"/> when the id is unknown.</returns>
    public bool SelectEntry(string id)
    {
        if (!_vm.SelectCard(id))
        {
            return false;
        }

        var item = _vm.SelectedCard is { } card
            ? _entryItems.FirstOrDefault(pair => pair.Value == card.Id).Key
            : null;
        if (item is null)
        {
            RebuildCatalog();
            item = _vm.SelectedCard is { } retry
                ? _entryItems.FirstOrDefault(pair => pair.Value == retry.Id).Key
                : null;
        }

        if (item is not null)
        {
            CatalogTree.Reveal(item);
        }

        return true;
    }

    /// <summary>Scrolls the preview viewport.</summary>
    /// <param name="offset">The vertical offset in pixels.</param>
    public void SetPreviewScrollOffset(float offset) => PreviewScroll.VerticalOffset = offset;

    /// <summary>Hides the title, catalog, inspector and status bar so only the preview remains.</summary>
    /// <param name="visible">Whether the chrome is shown.</param>
    public void SetChromeVisible(bool visible)
    {
        var visibility = visible ? XamlVisibility.Visible : XamlVisibility.Collapsed;
        TitleBar.Visibility = visibility;
        CaptionBar.Visibility = visibility;
        StatusBar.Visibility = visibility;
        CatalogPane.Visibility = visibility;
        InspectorPane.Visibility = visibility;
        LeftSplitter.Visibility = visibility;
        RightSplitter.Visibility = visibility;
        InvalidateMeasure();
    }

    /// <summary>Finds a card by id.</summary>
    /// <param name="id">The card id.</param>
    public GalleryCard? FindCard(string id) => _catalog.Find(id);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _vm.CatalogChanged -= OnCatalogChanged;
            _vm.PreviewChanged -= OnPreviewChanged;
        }

        base.Dispose(disposing);
    }

    private void OnCatalogSelectionChanged(object? sender, EventArgs e)
    {
        if (_syncingSelection
            || CatalogTree.SelectedItem is not { } item
            || !_entryItems.TryGetValue(item, out var id))
        {
            return;
        }

        _vm.SelectCard(id);
    }

    private void OnCatalogChanged(object? sender, EventArgs e) => RebuildCatalog();

    private void OnPreviewChanged(object? sender, GalleryCardInfo? info)
    {
        if (info is null)
        {
            ClearPreview();
        }
        else
        {
            ShowCard(info);
        }
    }

    private void RebuildCatalog()
    {
        _entryItems.Clear();
        CatalogTree.Items.Clear();

        var demos = new VC.TreeViewItem { Header = "Demos", IsExpanded = true };
        foreach (var group in _vm.VisibleGroups)
        {
            var groupItem = new VC.TreeViewItem { Header = group.Name, IsExpanded = true };
            foreach (var info in group.Cards)
            {
                var item = new VC.TreeViewItem { Header = info.Title };
                _entryItems[item] = info.Id;
                groupItem.Items.Add(item);
            }

            demos.Items.Add(groupItem);
        }

        CatalogTree.Items.Add(demos);

        var selected = _vm.SelectedCard is { } card
            ? _entryItems.FirstOrDefault(pair => pair.Value == card.Id).Key
            : null;
        _syncingSelection = true;
        try
        {
            CatalogTree.SelectedItem = selected ?? _entryItems.Keys.FirstOrDefault();
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    /// <summary>Updates theme-driven code-built parts without rebuilding the shell.</summary>
    public void RefreshTheme()
    {
        // Inspector labels hold the shell's theme brushes, which mutate in place; only a preview that
        // captured Theme.Current colours at construction needs a new instance.
        if (_vm.SelectedCard is not null
            && PreviewHost.Content is VisualElement current
            && current is Demo.Views.DynamicResourceCard or Demo.Views.Cards.CompositionLabView)
        {
            var next = _catalog.Find(_vm.SelectedCard.Id)?.Create(_model);
            PreviewHost.Content = next;
            current.Dispose();
            _inspector.SetTarget(next);
        }
    }

    private void ShowCard(GalleryCardInfo info)
    {
        Surface?.ClosePopups();
        UsageExpander.IsExpanded = false;
        PreviewScroll.VerticalOffset = 0;

        var previous = PreviewHost.Content as VisualElement;
        var next = _catalog.Find(info.Id)?.Create(_model);
        PreviewHost.Content = next;
        previous?.Dispose();
        _inspector.SetTarget(next);
    }

    private void ClearPreview()
    {
        Surface?.ClosePopups();
        UsageExpander.IsExpanded = false;
        PreviewScroll.VerticalOffset = 0;
        _inspector.SetTarget(null);

        var previous = PreviewHost.Content as VisualElement;
        PreviewHost.Content = null;
        previous?.Dispose();
    }
}
