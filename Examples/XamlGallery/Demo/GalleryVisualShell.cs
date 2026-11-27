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
/// The window declares the shell; the shell binds itself to the <see cref="GallerySession"/> it inherits.
/// </remarks>
internal sealed partial class GalleryVisualShell : ThemedDockPanel
{
    private GalleryModel _model = null!;
    private GalleryShellViewModel _vm = null!;
    private GalleryCardCatalog _catalog = null!;
    private GallerySession? _session;
    private readonly Dictionary<VC.TreeViewItem, string> _entryItems = [];
    private GalleryVisualInspector? _inspector;
    private readonly List<VisualElement> _pendingDispose = [];
    private bool _syncingSelection;

    /// <summary>Creates the view. <see cref="Attach"/> runs when the inherited data context is a session.</summary>
    public GalleryVisualShell() => InitializeComponent();

    /// <summary>Every card in catalog order.</summary>
    public IReadOnlyList<GalleryCard> Cards => _catalog.Cards;

    /// <summary>The visual currently hosted by the preview pane; the capture runner inspects it.</summary>
    internal VisualElement? PreviewContent => PreviewHost.Content as VisualElement;

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
            // Reveal 会把 SelectedItem 设成同一项；抑制回传，避免再走一次 ShowCard（页面构建两次）。
            var previous = _syncingSelection;
            _syncingSelection = true;
            try
            {
                CatalogTree.Reveal(item);
            }
            finally
            {
                _syncingSelection = previous;
            }
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

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, DataContextProperty) && _session is null && DataContext is GallerySession session)
        {
            Attach(session);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_session is not null)
            {
                _vm.CatalogChanged -= OnCatalogChanged;
                _vm.PreviewChanged -= OnPreviewChanged;
            }

            FlushPendingDisposals();
        }

        base.Dispose(disposing);
    }

    // 本地 DataContext 换成 VM：窗口的上下文仍是 session，捕获入口从窗口取；shell 子树只认 VM。
    private void Attach(GallerySession session)
    {
        _session = session;
        _model = session.Model;
        _vm = session.ViewModel;
        _catalog = session.Catalog;

        DataContext = _vm;

        _inspector = new GalleryVisualInspector(Resources);
        // 预览卡片内部的尺寸变化（Slider/GridSplitter）只重排预览视口，不牵动左目录与右属性面板。
        PreviewScroll.IsLayoutBoundary = true;
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

        session.AttachShell(this);
    }

    private void OnCatalogSelectionChanged(object? sender, EventArgs e)
    {
        if (_session is null
            || _syncingSelection
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

        foreach (var section in _vm.VisibleSections)
        {
            var sectionItem = new VC.TreeViewItem { Header = section.Name, IsExpanded = true };
            foreach (var group in section.Groups)
            {
                var groupItem = new VC.TreeViewItem { Header = group.Name, IsExpanded = true };
                foreach (var info in group.Cards)
                {
                    var item = new VC.TreeViewItem { Header = info.Title };
                    _entryItems[item] = info.Id;
                    groupItem.Items.Add(item);
                }

                sectionItem.Items.Add(groupItem);
            }

            CatalogTree.Items.Add(sectionItem);
        }

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
            var next = IsolatePreviewDataContext(_catalog.Find(_vm.SelectedCard.Id)?.Create(_model));
            PreviewHost.Content = next;
            DeferDispose(current);
            _inspector?.SetTarget(next);
        }
    }

    private void ShowCard(GalleryCardInfo info)
    {
        Surface?.ClosePopups();
        UsageExpander.IsExpanded = false;
        PreviewScroll.VerticalOffset = 0;

        var previous = PreviewHost.Content as VisualElement;
        var next = IsolatePreviewDataContext(_catalog.Find(info.Id)?.Create(_model));
        PreviewHost.Content = next;
        DeferDispose(previous);
        _inspector?.SetTarget(next);
    }

    // 预览卡片各有自己的 DataContext；挂载前把未设置的值钉成本地空层，继承通知就不会在整棵预览子树上重复下传。
    internal static VisualElement? IsolatePreviewDataContext(VisualElement? preview)
    {
        if (preview is not null && !preview.IsSet(VisualElement.DataContextProperty))
        {
            preview.DataContext = null;
        }

        return preview;
    }

    // 旧卡片已经离开可视树；换页这一手返回后再释放，媒体/原生控件拆解的时间就不落在换页上。
    private void DeferDispose(VisualElement? previous)
    {
        if (previous is null)
        {
            return;
        }

        _pendingDispose.Add(previous);
        if (Surface is { } surface)
        {
            surface.Post(FlushPendingDisposals);
        }
        else
        {
            FlushPendingDisposals();
        }
    }

    private void FlushPendingDisposals()
    {
        if (_pendingDispose.Count == 0)
        {
            return;
        }

        var pending = _pendingDispose.ToArray();
        _pendingDispose.Clear();
        foreach (var element in pending)
        {
            element.Dispose();
        }
    }

    private void ClearPreview()
    {
        Surface?.ClosePopups();
        UsageExpander.IsExpanded = false;
        PreviewScroll.VerticalOffset = 0;
        _inspector?.SetTarget(null);

        var previous = PreviewHost.Content as VisualElement;
        PreviewHost.Content = null;
        DeferDispose(previous);
    }
}
