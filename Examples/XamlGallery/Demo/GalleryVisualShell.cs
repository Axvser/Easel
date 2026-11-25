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
    private readonly List<VisualElement> _pendingDispose = [];
    private System.Windows.Forms.Timer? _disposeTimer;
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
            if (_disposeTimer is { } timer)
            {
                timer.Stop();
                timer.Tick -= OnDisposeTimerTick;
                timer.Dispose();
                _disposeTimer = null;
            }

            FlushPendingDisposals();
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
            var next = IsolatePreviewDataContext(_catalog.Find(_vm.SelectedCard.Id)?.Create(_model));
            PreviewHost.Content = next;
            DeferDispose(current);
            _inspector.SetTarget(next);
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
        _inspector.SetTarget(next);
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

    // 旧卡片已经离开可视树；下一帧画完后再释放，媒体/原生控件拆解的时间就不落在换页上。
    private void DeferDispose(VisualElement? previous)
    {
        if (previous is null)
        {
            return;
        }

        _pendingDispose.Add(previous);
        if (_disposeTimer is null)
        {
            _disposeTimer = new System.Windows.Forms.Timer { Interval = 30 };
            _disposeTimer.Tick += OnDisposeTimerTick;
        }

        // 重启计时：从最后一次换页起算一帧，连续换页只释放一轮。
        _disposeTimer.Stop();
        _disposeTimer.Start();
    }

    private void OnDisposeTimerTick(object? sender, EventArgs e)
    {
        _disposeTimer?.Stop();
        FlushPendingDisposals();
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
        _inspector.SetTarget(null);

        var previous = PreviewHost.Content as VisualElement;
        PreviewHost.Content = null;
        DeferDispose(previous);
    }
}
