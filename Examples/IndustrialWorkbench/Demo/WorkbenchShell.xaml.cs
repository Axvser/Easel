using Easel;
using Easel.Visuals;
using Easel.Visuals.Controls;
using IndustrialWorkbench.ViewModels;
using IndustrialWorkbench.Views;

namespace IndustrialWorkbench;

/// <summary>The workbench shell: factory status bar, the code-built catalog tree and the swapped page host.</summary>
public sealed partial class WorkbenchShell : UserControl
{
    private readonly Dictionary<TreeViewItem, string> _entryIds = [];
    private Dictionary<string, ControlInfo> _infos = [];
    private WorkbenchViewModel? _model;
    private ControlDetailView? _detail;
    private bool _syncing;

    /// <summary>Creates the shell and its generated initializer.</summary>
    public WorkbenchShell() => InitializeComponent();

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, DataContextProperty) && _model is null && DataContext is WorkbenchViewModel model)
        {
            Attach(model);
        }
    }

    private void Attach(WorkbenchViewModel model)
    {
        _model = model;
        _infos = ControlCatalog.Create().ToDictionary(static info => info.Id);
        BuildCatalog();
        ShowOverview();
    }

    // TreeView 没有 ItemsSource：目录在代码里构建，Overview 固定在第一项。
    private void BuildCatalog()
    {
        _entryIds.Clear();
        CatalogTree.Items.Clear();

        var overview = new TreeViewItem { Header = "Overview · 控件墙" };
        _entryIds[overview] = ControlCatalog.OverviewId;
        CatalogTree.Items.Add(overview);

        foreach (var group in ControlCatalog.Groups)
        {
            var groupItem = new TreeViewItem { Header = group, IsExpanded = true };
            foreach (var info in _infos.Values.Where(info => info.Group == group))
            {
                var item = new TreeViewItem { Header = info.Title };
                _entryIds[item] = info.Id;
                groupItem.Items.Add(item);
            }

            CatalogTree.Items.Add(groupItem);
        }

        _syncing = true;
        try
        {
            CatalogTree.SelectedItem = overview;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnCatalogSelectionChanged(object? sender, EventArgs e)
    {
        if (_syncing || _model is null)
        {
            return;
        }

        if (CatalogTree.SelectedItem is not { } item || !_entryIds.TryGetValue(item, out var id))
        {
            return;
        }

        if (id == ControlCatalog.OverviewId)
        {
            ShowOverview();
        }
        else if (_infos.TryGetValue(id, out var info))
        {
            ShowDetail(info);
        }
    }

    private void ShowOverview()
        => SetPage(new OverviewView { DataContext = _model });

    private void ShowDetail(ControlInfo info)
    {
        _detail ??= new ControlDetailView();
        _detail.DataContext = _model;
        _detail.ShowControl(info, _model!);
        SetPage(_detail);
    }

    private void SetPage(VisualElement next)
    {
        var previous = PageHost.Content as VisualElement;
        PageHost.Content = next;
        if (previous is not null && !ReferenceEquals(previous, next) && !ReferenceEquals(previous, _detail))
        {
            previous.Dispose();
        }
    }

    private void OnEmergencyClick(object? sender, EventArgs e) => _model?.EmergencyStop();

    private void OnResetClick(object? sender, EventArgs e) => _model?.Reset();

    /// <summary>Selects a catalog entry by id (used by the capture runner).</summary>
    /// <param name="id">The entry id, or <see cref="ControlCatalog.OverviewId"/>.</param>
    /// <returns><see langword="false"/> when the id is unknown.</returns>
    public bool SelectEntry(string id)
    {
        var item = _entryIds.FirstOrDefault(pair => pair.Value == id).Key;
        if (item is null)
        {
            return false;
        }

        CatalogTree.SelectedItem = item;
        return true;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && _detail is not null && !ReferenceEquals(PageHost.Content, _detail))
        {
            _detail.Dispose();
        }

        base.Dispose(disposing);
    }
}
