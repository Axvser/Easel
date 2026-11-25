using System.Collections;
using System.Collections.Specialized;
using System.Drawing;
using System.Windows.Forms;
using XamlThickness = Easel.Controls.Thickness;
using XamlVisibility = Easel.Controls.Visibility;

namespace Easel.Visuals.Controls;

/// <summary>A panel that turns an <see cref="ItemsSource"/> into child visuals through a factory and stacks them vertically.</summary>
/// <remarks>
/// <para>
/// When the source also implements <see cref="INotifyCollectionChanged"/> every collection change rebuilds the
/// children; the first version does not diff individual add/remove operations. Each generated visual receives the
/// source item as its <see cref="VisualElement.DataContext"/> unless it already set one.
/// </para>
/// <para>
/// <see cref="Items"/> exposes the realized visuals. The factory must return a new, unparented element; this panel
/// owns the lifetime of whatever it returns.
/// </para>
/// <para>
/// A control template hosts the realized items in the first <see cref="ItemsPresenter"/> or panel marked with
/// <see cref="SetIsItemsHost"/> found inside the template root; a template root that is itself a panel acts as the
/// host when it declares none. Applying or clearing a template re-parents the realized items into the new host.
/// </para>
/// </remarks>
public class ItemsControl : TemplatedControl
{
    /// <summary>The sequence whose items become child visuals.</summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource), typeof(IEnumerable), typeof(ItemsControl),
            new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>Creates a visual from a source item; <see langword="null"/> uses a text projection.</summary>
    public static readonly DependencyProperty ItemFactoryProperty =
        DependencyProperty.Register(
            nameof(ItemFactory), typeof(Func<object, VisualElement>), typeof(ItemsControl),
            new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The property read from each item for its text; empty uses <see cref="object.ToString"/>.</summary>
    public static readonly DependencyProperty DisplayMemberPathProperty =
        DependencyProperty.Register(
            nameof(DisplayMemberPath), typeof(string), typeof(ItemsControl),
            new PropertyMetadata(string.Empty) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The template that creates each item visual; takes precedence over <see cref="ItemFactory"/>.</summary>
    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(
            nameof(ItemTemplate), typeof(DataTemplate), typeof(ItemsControl),
            new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>Chooses the item template per source item; a non-null result wins over <see cref="ItemTemplate"/>.</summary>
    public static readonly DependencyProperty ItemTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(ItemTemplateSelector), typeof(DataTemplateSelector), typeof(ItemsControl),
            new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The panel that hosts the realized items, or <see langword="null"/> to stack them vertically.</summary>
    public static readonly DependencyProperty ItemsPanelProperty =
        DependencyProperty.Register(
            nameof(ItemsPanel), typeof(ItemsPanelTemplate), typeof(ItemsControl),
            new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>Marks a panel as the host of an <see cref="ItemsControl"/>'s realized items.</summary>
    public static readonly DependencyProperty IsItemsHostProperty =
        DependencyProperty.RegisterAttached(
            "IsItemsHost", typeof(bool), typeof(ItemsControl),
            new PropertyMetadata(false) { AffectsMeasure = true, AffectsRender = false });

    private readonly List<object?> _sourceItems = [];
    private readonly List<VisualElement> _items = [];
    private INotifyCollectionChanged? _observed;
    private ItemsPanelTemplate? _appliedPanelTemplate;
    private Panel? _itemsHost;
    private VisualElement? _hostTemplateRoot;
    private bool _rebuilding;
    private bool _disposing;
    private bool _realizesAllItems;

    /// <summary>Creates an empty items panel.</summary>
    public ItemsControl()
    {
        // Panel 的默认是不拦空白处的指针；模板根自己决定可命中区域。
        IsHitTestVisible = false;
    }

    /// <summary>Reads whether <paramref name="element"/> is marked as an items host panel.</summary>
    /// <param name="element">The element to read.</param>
    public static bool GetIsItemsHost(VisualElement element)
        => element is null ? throw new ArgumentNullException(nameof(element)) : (bool)element.GetValue(IsItemsHostProperty)!;

    /// <summary>Marks <paramref name="element"/> as the host of an items control's realized items.</summary>
    /// <param name="element">The panel to mark.</param>
    /// <param name="value">Whether the panel hosts items.</param>
    public static void SetIsItemsHost(VisualElement element, bool value)
        => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(IsItemsHostProperty, value);

    /// <summary>The sequence whose items become child visuals.</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Creates a visual from a source item; <see langword="null"/> uses a text projection.</summary>
    public Func<object, VisualElement>? ItemFactory
    {
        get => (Func<object, VisualElement>?)GetValue(ItemFactoryProperty);
        set => SetValue(ItemFactoryProperty, value);
    }

    /// <summary>The property read from each item for its text; empty uses <see cref="object.ToString"/>.</summary>
    public string DisplayMemberPath
    {
        get => (string)GetValue(DisplayMemberPathProperty)!;
        set => SetValue(DisplayMemberPathProperty, value ?? string.Empty);
    }

    /// <summary>The template that creates each item visual; takes precedence over <see cref="ItemFactory"/>.</summary>
    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>Chooses the item template per source item; a non-null result wins over <see cref="ItemTemplate"/>.</summary>
    public DataTemplateSelector? ItemTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(ItemTemplateSelectorProperty);
        set => SetValue(ItemTemplateSelectorProperty, value);
    }

    /// <summary>The panel that hosts the realized items, or <see langword="null"/> to stack them vertically.</summary>
    public ItemsPanelTemplate? ItemsPanel
    {
        get => (ItemsPanelTemplate?)GetValue(ItemsPanelProperty);
        set => SetValue(ItemsPanelProperty, value);
    }

    /// <summary>The realized item visuals, in source order.</summary>
    public IReadOnlyList<VisualElement> Items => _items;

    /// <summary>The source items snapshotted from <see cref="ItemsSource"/>.</summary>
    protected IReadOnlyList<object?> SourceItems => _sourceItems;

    /// <summary>The number of source items, independent of how many are realized.</summary>
    protected int ItemCount => _sourceItems.Count;

    /// <summary>Rebuilds the realized item visuals from the current source and factory.</summary>
    public void RefreshItems() => RebuildItems();

    /// <summary>Raised after the realized item visuals were rebuilt.</summary>
    public event EventHandler? ItemsRebuilt;

    /// <summary>Returns the source item at <paramref name="index"/>, or <see langword="null"/> when out of range.</summary>
    /// <param name="index">The zero-based source index.</param>
    protected object? GetItemAt(int index)
        => index >= 0 && index < _sourceItems.Count ? _sourceItems[index] : null;

    /// <summary>Creates the visual for one source item.</summary>
    /// <param name="item">The source item; may be <see langword="null"/>.</param>
    /// <returns>The new, unparented item visual.</returns>
    /// <remarks>A subclass may wrap the projection <see cref="CreateItemContent"/> returns, for example in a row
    /// container that paints selection; the content stays reachable for recycling.</remarks>
    /// <exception cref="InvalidOperationException">The configured <see cref="ItemFactory"/> returned <see langword="null"/>.</exception>
    protected virtual VisualElement CreateItemVisual(object? item) => CreateItemContent(item);

    /// <summary>Creates the data-bound content visual for one source item, before any container wraps it.</summary>
    /// <param name="item">The source item; may be <see langword="null"/>.</param>
    /// <returns>The new, unparented content visual.</returns>
    /// <exception cref="InvalidOperationException">The configured <see cref="ItemFactory"/> returned <see langword="null"/>.</exception>
    protected virtual VisualElement CreateItemContent(object? item)
    {
        var selector = ItemTemplateSelector;
        var template = selector?.SelectTemplate(item, null) ?? ItemTemplate;
        var factory = ItemFactory;
        var visual = template is not null
            ? template.Create()
            : factory is not null
                ? factory(item!)
                : new TextBlock
                {
                    Text = FormatItem(item),
                    Padding = new XamlThickness(8f, 4f),
                };

        if (visual is null)
        {
            throw new InvalidOperationException("ItemFactory returned null.");
        }

        if (item is not null && !visual.IsSet(VisualElement.DataContextProperty))
        {
            visual.DataContext = item;
        }

        return visual;
    }

    /// <summary>Wraps one item's content in the shared list row container.</summary>
    /// <param name="item">The source item, used for the container's <see cref="VisualElement.DataContext"/>.</param>
    /// <param name="content">The data-bound content visual.</param>
    /// <returns>A new, unparented <see cref="ListBoxItem"/>.</returns>
    protected static ListBoxItem CreateItemContainer(object? item, VisualElement content)
    {
        if (content is null) throw new ArgumentNullException(nameof(content));

        var container = new ListBoxItem
        {
            Content = content,
            CornerRadius = Theme.Current.CornerRadius,
        };
        if (item is not null)
        {
            container.DataContext = item;
        }

        return container;
    }

    /// <summary>Realizes the current source items as children; the base implementation realizes every item.</summary>
    protected virtual void RealizeItems()
    {
        // 宿主只解析一次：自绘控件没有模板宿主时，ResolveItemsHost 会扫描子项，逐行扫描会退化成 O(n²)。
        var host = ResolveItemsHost();
        foreach (var item in _sourceItems)
        {
            var visual = CreateItemVisual(item);
            _items.Add(visual);
            AttachToHost(host, visual);
        }

        // 只有走“全部实现”的这条路径，重挂模板时才能确定 _items 与源一一对应；虚拟化子类自行
        // 覆盖本方法，保持原来的重建路径。
        _realizesAllItems = true;
    }

    /// <summary>Removes every realized item visual from its host.</summary>
    protected void ClearRealizedItems()
    {
        for (var index = _items.Count - 1; index >= 0; index--)
        {
            // items 可能挂在 ItemsPresenter/ItemsPanel 里，而不是控件自身。
            _items[index].ParentElement?.RemoveChild(_items[index]);
        }

        _items.Clear();
    }

    /// <summary>Adds a realized item visual to <see cref="Items"/> and to the active items host.</summary>
    /// <param name="visual">The item visual to add.</param>
    protected void AddRealizedItem(VisualElement visual)
    {
        if (visual is null) throw new ArgumentNullException(nameof(visual));
        _items.Add(visual);
        AttachToItemsHost(visual);
    }

    // item 挂在模板里的宿主或控件自身；宿主解析只在模板根变化时重查，不为每行遍历模板树。
    private void AttachToItemsHost(VisualElement visual)
    {
        var host = ResolveItemsHost();
        AttachToHost(host, visual);
    }

    private void AttachToHost(Panel? host, VisualElement visual)
    {
        if (host is not null && !ReferenceEquals(host, visual))
        {
            host.Add(visual);
            return;
        }

        Add(visual);
    }

    /// <summary>Inserts a realized item visual at <paramref name="index"/> and attaches it to <paramref name="host"/>.</summary>
    /// <param name="index">The position in <see cref="Items"/>; the host always appends.</param>
    /// <param name="visual">The item visual to insert.</param>
    /// <param name="host">The items host, or <see langword="null"/> to attach to this control.</param>
    protected void InsertRealizedItem(int index, VisualElement visual, Panel? host)
    {
        if (visual is null) throw new ArgumentNullException(nameof(visual));
        if (index < 0 || index > _items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        _items.Insert(index, visual);
        if (host is not null && !ReferenceEquals(host, visual))
        {
            host.Add(visual);
            return;
        }

        Add(visual);
    }

    /// <summary>Removes a realized item visual from <see cref="Items"/> and from its host.</summary>
    /// <param name="index">The position in <see cref="Items"/>.</param>
    /// <returns>The detached visual.</returns>
    protected VisualElement RemoveRealizedItemAt(int index)
    {
        if (index < 0 || index >= _items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var visual = _items[index];
        _items.RemoveAt(index);
        visual.ParentElement?.RemoveChild(visual);
        return visual;
    }

    // ItemsPanel 优先；否则使用模板里的 ItemsPresenter/IsItemsHost 面板；都没有就由控件自己竖排。
    /// <summary>Resolves the panel that hosts realized items, creating the <see cref="ItemsPanel"/> host when needed.</summary>
    /// <returns>The host panel, or <see langword="null"/> when the control arranges items itself.</returns>
    protected Panel? ResolveItemsHost()
    {
        var template = ItemsPanel;
        if (template is not null)
        {
            if (_itemsHost is not null && ReferenceEquals(_appliedPanelTemplate, template))
            {
                return _itemsHost;
            }

            if (_itemsHost is not null && Children.Contains(_itemsHost))
            {
                RemoveChild(_itemsHost);
            }

            _appliedPanelTemplate = template;
            _hostTemplateRoot = null;
            _itemsHost = template.Create();
            Add(_itemsHost);
            return _itemsHost;
        }

        var hostFromTemplate = _appliedPanelTemplate is not null && _itemsHost is not null;
        _appliedPanelTemplate = null;
        if (template is null && hostFromTemplate && Children.Contains(_itemsHost!))
        {
            // 上一轮由 ItemsPanel 创建的宿主在模板被移除后退出。
            RemoveChild(_itemsHost!);
            _itemsHost = null;
        }

        // 控件模板的宿主在模板根内部；先按显式标记找，找不到时把模板根本身当宿主。
        // 模板根在两次模板变更之间不变，找到一次就缓存，避免每实现一行都递归扫描模板树。
        if (TemplateRoot is { } templateRoot)
        {
            if (ReferenceEquals(_hostTemplateRoot, templateRoot) && _itemsHost is not null)
            {
                return _itemsHost;
            }

            _hostTemplateRoot = templateRoot;
            _itemsHost = FindTemplateItemsHost(templateRoot)
                ?? (templateRoot as Panel);
            return _itemsHost;
        }

        _hostTemplateRoot = null;

        foreach (var child in Children)
        {
            if (child is ItemsPresenter presenter)
            {
                _itemsHost = presenter;
                return presenter;
            }
        }

        foreach (var child in Children)
        {
            if (child is Panel panel && GetIsItemsHost(panel))
            {
                _itemsHost = panel;
                return panel;
            }
        }

        _itemsHost = null;
        return null;
    }

    private static Panel? FindTemplateItemsHost(VisualElement root)
    {
        if (root is ItemsPresenter presenter)
        {
            return presenter;
        }

        if (root is Panel panel && GetIsItemsHost(panel))
        {
            return panel;
        }

        foreach (var child in root.Children)
        {
            if (child is not VisualElement element)
            {
                continue;
            }

            if (FindTemplateItemsHost(element) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Called after <see cref="ItemsSource"/> or <see cref="ItemFactory"/> rebuilt the realized items.</summary>
    protected virtual void OnItemsRebuilt()
    {
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsTemplated)
        {
            return MeasureTemplate(availableSize);
        }

        var padding = Padding;
        var contentWidth = Math.Max(0, availableSize.Width - (int)Math.Ceiling(padding.Horizontal));
        var contentHeight = Math.Max(0, availableSize.Height - (int)Math.Ceiling(padding.Vertical));
        if (_itemsHost is not null)
        {
            var hosted = _itemsHost.Measure(new Size(contentWidth, contentHeight));
            return new Size(
                hosted.Width + (int)Math.Ceiling(padding.Horizontal),
                hosted.Height + (int)Math.Ceiling(padding.Vertical));
        }

        var width = 0;
        var height = 0;
        foreach (var child in _items)
        {
            if (child.Visibility == XamlVisibility.Collapsed)
            {
                continue;
            }

            var measured = child.Measure(new Size(contentWidth, 0));
            width = Math.Max(width, measured.Width);
            height += measured.Height;
        }

        return new Size(
            width + (int)Math.Ceiling(padding.Horizontal),
            height + (int)Math.Ceiling(padding.Vertical));
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        if (IsTemplated)
        {
            ArrangeTemplate(finalSize);
            return;
        }

        var padding = Padding;
        var content = new RectangleF(
            (int)Math.Ceiling(padding.Left),
            (int)Math.Ceiling(padding.Top),
            Math.Max(0f, finalSize.Width - padding.Horizontal),
            Math.Max(0f, finalSize.Height - padding.Vertical));
        if (_itemsHost is not null)
        {
            _itemsHost.Arrange(content);
            return;
        }

        var position = content.Top;
        foreach (var child in _items)
        {
            if (child.Visibility == XamlVisibility.Collapsed)
            {
                continue;
            }

            var desired = child.DesiredSize;
            child.Arrange(new RectangleF(content.Left, position, content.Width, desired.Height));
            position += desired.Height;
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanging()
    {
        base.OnTemplateChanging();

        // 模板根销毁前把已实现的 item 从旧宿主摘出来，但**不释放**：item 视觉只依赖 ItemTemplate/ItemFactory，
        // 与列表自身的模板无关。保留同一批实例，OnTemplateChanged 直接挂到新宿主，避免“先建一遍、
        // 施模板时再全毁重建一遍”的双份创建（200 行页面里这是最大的单项成本）。
        // 控件本身在释放时已经逐行释放并摘掉，这里不再重复。
        if (!_disposing)
        {
            DetachRealizedItems();
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        base.OnTemplateChanged();
        ReattachRealizedItems();
    }

    // 摘下旧宿主但保留 _items，等下一次模板应用重新挂载。
    private void DetachRealizedItems()
    {
        for (var index = _items.Count - 1; index >= 0; index--)
        {
            // 直接摘父级，不走 RemoveChild：那条路会为每行重解隐式样式并逐次 InvalidateMeasure，
            // 而这里要么马上挂进新宿主（Add 会重挂表面通知），要么整行即将释放。
            _items[index].Parent?.Children.Remove(_items[index]);
        }
    }

    // 模板应用后把保留的 item 挂到新宿主；只有源或工厂已变化（数量对不上）时才真的重建。
    private void ReattachRealizedItems()
    {
        if (_disposing)
        {
            return;
        }

        if (_items.Count != _sourceItems.Count || !_realizesAllItems)
        {
            RefreshItems();
            return;
        }

        foreach (var item in _items)
        {
            AttachToItemsHost(item);
        }

        InvalidateMeasure();
        OnItemsRebuilt();
        ItemsRebuilt?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, ItemsSourceProperty))
        {
            Observe(e.OldValue as INotifyCollectionChanged, e.NewValue as INotifyCollectionChanged);
            RebuildItems();
        }
        else if (ReferenceEquals(e.Property, ItemFactoryProperty)
                 || ReferenceEquals(e.Property, ItemTemplateProperty)
                 || ReferenceEquals(e.Property, ItemTemplateSelectorProperty)
                 || ReferenceEquals(e.Property, ItemsPanelProperty)
                 || ReferenceEquals(e.Property, DisplayMemberPathProperty))
        {
            RebuildItems();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposing = true;
            Observe(_observed, null);
            // 行先释放再摘父级：先摘会为每行触发一轮资源/父链重解析，而释放会摘掉这些订阅。
            // 摘干净后 RemoveTemplate 的 RemoveChild 只需重扫外壳，不必再走一遍 200 行的样式树。
            for (var index = _items.Count - 1; index >= 0; index--)
            {
                var item = _items[index];
                item.Dispose();
                item.Parent?.Children.Remove(item);
            }

            _items.Clear();
        }

        base.Dispose(disposing);
    }

    private string FormatItem(object? item)
    {
        if (item is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(DisplayMemberPath))
        {
            var property = item.GetType().GetProperty(DisplayMemberPath);
            if (property?.GetValue(item) is { } value)
            {
                return value.ToString() ?? string.Empty;
            }
        }

        return item.ToString() ?? string.Empty;
    }

    private void RebuildItems()
    {
        if (_rebuilding)
        {
            return;
        }

        _rebuilding = true;
        try
        {
            ClearRealizedItems();
            _sourceItems.Clear();
            if (ItemsSource is { } source)
            {
                foreach (var item in source)
                {
                    _sourceItems.Add(item);
                }
            }

            // 先确定宿主：空 ItemsSource 也要让 ItemsPanel 里的空面板参与布局。
            ResolveItemsHost();
            _realizesAllItems = false;
            RealizeItems();
        }
        finally
        {
            _rebuilding = false;
        }

        InvalidateMeasure();
        OnItemsRebuilt();
        ItemsRebuilt?.Invoke(this, EventArgs.Empty);
    }

    private void Observe(INotifyCollectionChanged? previous, INotifyCollectionChanged? next)
    {
        if (ReferenceEquals(previous, next))
        {
            return;
        }

        if (previous is not null)
        {
            previous.CollectionChanged -= OnSourceCollectionChanged;
        }

        _observed = next;
        if (next is not null)
        {
            next.CollectionChanged += OnSourceCollectionChanged;
        }
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildItems();
}

/// <summary>The container a <see cref="ListBox"/> or <see cref="VirtualList"/> puts around one realized item.</summary>
/// <remarks>
/// <para>
/// The container paints a row's hover and selection bands from <see cref="IsPointerOver"/> and
/// <see cref="IsSelected"/>; the owning list only pushes those states. An implicit or host style therefore replaces
/// the row appearance without retemplating the list. The built-in style lives in <c>ItemsDefaults.xaml</c>.
/// </para>
/// <para>
/// The container stays out of hit testing, so the list keeps pointer selection and maps a press by row bounds. A
/// custom style may opt into input when a scenario needs per-row interaction.
/// </para>
/// </remarks>
public class ListBoxItem : ContentControl
{
    /// <summary>Whether the owning list has this row selected.</summary>
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(ListBoxItem),
        new PropertyMetadata(false) { AffectsRender = true });

    /// <summary>Whether the pointer is over this row.</summary>
    public static readonly DependencyProperty IsPointerOverProperty = DependencyProperty.Register(
        nameof(IsPointerOver), typeof(bool), typeof(ListBoxItem),
        new PropertyMetadata(false) { AffectsRender = true });

    private bool _templateDeferred;
    private VisualControlTemplate? _deferredTemplate;

    /// <summary>Creates a row container that leaves pointer hits to its list.</summary>
    public ListBoxItem()
    {
        IsHitTestVisible = false;
    }

    // 列表把行模板的创建推迟到行进入视口；这里只记下样式给的模板，不建模板根。
    internal bool IsTemplateDeferred => _templateDeferred;

    internal void DeferTemplateRealization() => _templateDeferred = true;

    internal void RealizeTemplate()
    {
        if (!_templateDeferred)
        {
            return;
        }

        _templateDeferred = false;
        base.ApplyStyleTemplate(_deferredTemplate);
    }

    internal void ReleaseTemplate()
    {
        if (_templateDeferred)
        {
            return;
        }

        _templateDeferred = true;
        base.ApplyStyleTemplate(null);
    }

    internal override void ApplyStyleTemplate(VisualControlTemplate? template)
    {
        _deferredTemplate = template;
        if (_templateDeferred)
        {
            return;
        }

        base.ApplyStyleTemplate(template);
    }

    /// <summary>Whether the owning list has this row selected.</summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty)!;
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Whether the pointer is over this row.</summary>
    /// <remarks>This dependency property shadows <see cref="VisualElement.IsPointerOver"/> so a style or template
    /// trigger can observe the row's hover state.</remarks>
    public new bool IsPointerOver => (bool)GetValue(IsPointerOverProperty)!;

    internal void SetIsPointerOver(bool value) => SetValue(IsPointerOverProperty, value);
}

/// <summary>A vertical list whose items can be selected by pointer or keyboard.</summary>
/// <remarks>
/// This list realizes every source item, so its selection and pointer mapping stay index-for-index. For a large
/// fixed-row source use <see cref="VirtualList"/>; a virtualized mode here would have to route selection and input
/// through the realized window, so it is not enabled on <see cref="ListBox"/> itself.
/// </remarks>
public class ListBox : ItemsControl
{
    /// <summary>The zero-based index of the selected item, or -1 when nothing is selected.</summary>
    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(
            nameof(SelectedIndex), typeof(int), typeof(ListBox),
            new PropertyMetadata(-1) { AffectsRender = true });

    /// <summary>The selected source item, or <see langword="null"/> when nothing is selected.</summary>
    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem), typeof(object), typeof(ListBox),
            new PropertyMetadata(null) { AffectsRender = true });

    private bool _syncingSelection;
    private int _hoverIndex = -1;

    /// <summary>Creates a focusable list with theme metrics.</summary>
    public ListBox()
    {
        Focusable = true;
        IsHitTestVisible = true;
        MinHeight = Theme.Current.ControlHeight;
    }

    /// <summary>Raised after <see cref="SelectedIndex"/> or <see cref="SelectedItem"/> changes.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>The zero-based index of the selected item, or -1 when nothing is selected.</summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty)!;
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>The selected source item, or <see langword="null"/> when nothing is selected.</summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>Raises <see cref="SelectionChanged"/>.</summary>
    protected virtual void OnSelectionChanged() => SelectionChanged?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    protected override VisualElement CreateItemVisual(object? item)
    {
        var container = CreateItemContainer(item, CreateItemContent(item));
        // 源先全部建行（Items 保持逐项对应），行模板等进入视口再建。
        container.DeferTemplateRealization();
        return container;
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(finalSize);
        RealizeVisibleRows();
    }

    // 行的文档位置由 ItemsPresenter 排好后才知道，因此在 Arrange 之后按视口实现模板。
    private void RealizeVisibleRows()
    {
        var count = Items.Count;
        if (count == 0)
        {
            return;
        }

        // 没有自己的滚动视口时不裁行，必须全部实现，否则行会不显示。
        if (GetTemplateChild<ScrollViewer>("PART_ScrollViewer") is not { ContentPresenter: { } presenter })
        {
            for (var index = 0; index < count; index++)
            {
                if (Items[index] is ListBoxItem deferred)
                {
                    deferred.RealizeTemplate();
                }
            }

            return;
        }

        // 行是 presenter 的直接子项，Bounds 已是 presenter 坐标；用偏移和视口比较，避免每行走一遍父链求绝对框。
        var host = ResolveItemsHost();
        var offset = presenter.VerticalOffset;
        var viewportHeight = presenter.ViewportHeight;
        if (viewportHeight <= 0f)
        {
            return;
        }

        var changed = false;
        for (var index = 0; index < count; index++)
        {
            if (Items[index] is not ListBoxItem row)
            {
                continue;
            }

            var bounds = row.Bounds;
            if (bounds.Height <= 0f)
            {
                continue;
            }

            // 不走模板 presenter 的行（自定义 ItemsPanel）无法用滚动偏移判断，直接实现，保持原有可见性。
            if (!ReferenceEquals(row.ParentElement, host))
            {
                if (row.IsTemplateDeferred)
                {
                    row.RealizeTemplate();
                    changed = true;
                }

                continue;
            }

            var top = bounds.Y;
            var bottom = bounds.Y + bounds.Height;
            if (row.IsTemplateDeferred)
            {
                if (bottom > offset && top < offset + viewportHeight)
                {
                    row.RealizeTemplate();
                    changed = true;
                }
            }
            else if (bottom <= offset - RealizationMargin || top >= offset + viewportHeight + RealizationMargin)
            {
                row.ReleaseTemplate();
                changed = true;
            }
        }

        if (changed)
        {
            SyncContainerStates();
            InvalidateMeasure();
        }
    }

    // 释放边界比实现边界宽出一小段，滚动到边界附近不会抖。
    private const float RealizationMargin = 48f;

    /// <inheritdoc />
    protected override void OnPointerMoved(VisualPointerEventArgs args)
    {
        base.OnPointerMoved(args);
        SetHover(IndexAt(args.GetPosition(this)));
    }

    /// <inheritdoc />
    protected override void OnPointerExited(VisualPointerEventArgs args)
    {
        base.OnPointerExited(args);
        SetHover(-1);
    }

    private void SetHover(int index)
    {
        if (!IsEnabled)
        {
            index = -1;
        }

        if (_hoverIndex == index)
        {
            return;
        }

        SetContainerPointerOver(_hoverIndex, false);
        _hoverIndex = index;
        SetContainerPointerOver(index, true);
        InvalidateVisual();
    }

    private void SetContainerPointerOver(int index, bool value)
    {
        if (index >= 0 && index < Items.Count && Items[index] is ListBoxItem container)
        {
            container.SetIsPointerOver(value);
        }
    }

    private void SyncContainerStates()
    {
        for (var index = 0; index < Items.Count; index++)
        {
            if (Items[index] is ListBoxItem container)
            {
                container.IsSelected = index == SelectedIndex;
                container.SetIsPointerOver(IsEnabled && index == _hoverIndex);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        if (!IsEnabled || args.Button != MouseButtons.Left)
        {
            return;
        }

        var index = IndexAt(args.GetPosition(this));
        if (index < 0)
        {
            return;
        }

        Focus();
        SelectedIndex = index;
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(VisualKeyEventArgs args)
    {
        if (!IsEnabled || ItemCount == 0)
        {
            return;
        }

        var index = SelectedIndex;
        switch (args.KeyCode)
        {
            case Keys.Up:
                index = index <= 0 ? 0 : index - 1;
                break;
            case Keys.Down:
                index = index < 0 ? 0 : index + 1;
                break;
            case Keys.Home:
                index = 0;
                break;
            case Keys.End:
                index = ItemCount - 1;
                break;
            default:
                return;
        }

        SelectedIndex = XamlMath.Clamp(index, 0, ItemCount - 1);
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_syncingSelection)
        {
            return;
        }

        if (ReferenceEquals(e.Property, SelectedIndexProperty))
        {
            SyncSelectedItemFromIndex();
            SyncContainerStates();
            InvalidateVisual();
            OnSelectionChanged();
        }
        else if (ReferenceEquals(e.Property, SelectedItemProperty))
        {
            SyncSelectedIndexFromItem();
            SyncContainerStates();
            InvalidateVisual();
            OnSelectionChanged();
        }
    }

    /// <inheritdoc />
    protected override void OnItemsRebuilt()
    {
        base.OnItemsRebuilt();
        if (SelectedIndex >= ItemCount)
        {
            SelectedIndex = -1;
        }
        else if (SelectedIndex >= 0)
        {
            SyncSelectedItemFromIndex();
        }
        else if (SelectedItem is not null)
        {
            SyncSelectedIndexFromItem();
        }

        SyncContainerStates();
        InvalidateVisual();
    }

    private int IndexAt(PointF local)
    {
        // 模板里的 item 挂在 ItemsHost 下，Bounds 不是列表坐标；统一换到表面坐标再比。
        var surfacePoint = PointToSurface(local);
        for (var index = 0; index < Items.Count; index++)
        {
            if (Items[index].AbsoluteBounds.Contains(surfacePoint))
            {
                return index;
            }
        }

        return -1;
    }

    private void SyncSelectedItemFromIndex()
    {
        _syncingSelection = true;
        try
        {
            var index = SelectedIndex;
            SelectedItem = index >= 0 && index < ItemCount ? GetItemAt(index) : null;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void SyncSelectedIndexFromItem()
    {
        _syncingSelection = true;
        try
        {
            var index = -1;
            if (SelectedItem is { } item)
            {
                for (var candidate = 0; candidate < ItemCount; candidate++)
                {
                    if (Equals(GetItemAt(candidate), item))
                    {
                        index = candidate;
                        break;
                    }
                }
            }

            SelectedIndex = index;
        }
        finally
        {
            _syncingSelection = false;
        }
    }
}

/// <summary>A fixed-row virtualizing list that realizes only the rows visible in an ancestor <see cref="ScrollViewer"/>.</summary>
/// <remarks>
/// <para>
/// Rows share <see cref="ItemHeight"/> and stack vertically. <see cref="VirtualizingPanel"/> reports the full item
/// count as the desired height and recycles the realized window as the viewport moves; selection by pointer or
/// keyboard works against that window.
/// </para>
/// </remarks>
public class VirtualList : VirtualizingPanel
{
    /// <summary>The fixed height of one row, in pixels.</summary>
    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.Register(
            nameof(ItemHeight), typeof(double), typeof(VirtualList),
            new PropertyMetadata(24d) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The zero-based index of the selected row, or -1 when nothing is selected.</summary>
    public static readonly DependencyProperty SelectedIndexProperty =
        DependencyProperty.Register(
            nameof(SelectedIndex), typeof(int), typeof(VirtualList),
            new PropertyMetadata(-1) { AffectsRender = true });

    /// <summary>The selected source item, or <see langword="null"/> when nothing is selected.</summary>
    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem), typeof(object), typeof(VirtualList),
            new PropertyMetadata(null) { AffectsRender = true });

    private bool _syncingSelection;
    private bool _syncingExtent;
    private int _hoverIndex = -1;

    /// <summary>Creates an empty virtualizing list.</summary>
    public VirtualList()
    {
        IsHitTestVisible = true;
        Focusable = true;
    }

    /// <summary>The fixed height of one row, in pixels.</summary>
    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty)!;
        set => SetValue(ItemHeightProperty, value);
    }

    /// <summary>The fixed row extent, kept in sync with <see cref="ItemHeight"/>.</summary>
    public override double ItemExtent
    {
        get => ItemHeight;
        set => ItemHeight = value;
    }

    /// <summary>The zero-based index of the selected row, or -1 when nothing is selected.</summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty)!;
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>The selected source item, or <see langword="null"/> when nothing is selected.</summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>Raised after <see cref="SelectedIndex"/> or <see cref="SelectedItem"/> changes.</summary>
    public event EventHandler? SelectionChanged;

    /// <inheritdoc />
    protected override VisualElement CreateItemVisual(object? item)
        => CreateItemContainer(item, CreateItemContent(item));

    /// <inheritdoc />
    protected override void BindRecycledItem(VisualElement element, int index)
    {
        if (element is ListBoxItem container)
        {
            // 池里的容器只换了 DataContext；内容视觉仍绑着旧项，必须按新索引重建。
            var previous = container.Content as VisualElement;
            container.Content = CreateItemContent(GetItemAt(index));
            previous?.Dispose();
        }

        base.BindRecycledItem(element, index);
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        base.ArrangeOverride(finalSize);
        SyncContainerStates();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, ItemHeightProperty))
        {
            InvalidateRealizedWindow();
            InvalidateMeasure();
            return;
        }

        // 基类 ItemExtent 这个依赖属性若被直接 SetValue（XAML 写 ItemExtent），转存到行高。
        if (ReferenceEquals(e.Property, ItemExtentProperty) && !_syncingExtent)
        {
            _syncingExtent = true;
            try
            {
                ItemHeight = (double)e.NewValue!;
            }
            finally
            {
                _syncingExtent = false;
            }

            return;
        }

        if (_syncingSelection)
        {
            return;
        }

        if (ReferenceEquals(e.Property, SelectedIndexProperty))
        {
            SyncSelectedItemFromIndex();
            SyncContainerStates();
            InvalidateVisual();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (ReferenceEquals(e.Property, SelectedItemProperty))
        {
            SyncSelectedIndexFromItem();
            SyncContainerStates();
            InvalidateVisual();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    protected override void OnItemsRebuilt()
    {
        base.OnItemsRebuilt();
        if (SelectedIndex >= ItemCount)
        {
            SelectedIndex = -1;
        }
        else if (SelectedIndex >= 0)
        {
            SyncSelectedItemFromIndex();
        }
        else if (SelectedItem is not null)
        {
            SyncSelectedIndexFromItem();
        }

        SyncContainerStates();
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        if (!IsEnabled || args.Button != MouseButtons.Left)
        {
            return;
        }

        var index = IndexAt(args.Position);
        if (index < 0)
        {
            return;
        }

        Focus();
        SelectedIndex = index;
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(VisualKeyEventArgs args)
    {
        if (!IsEnabled || ItemCount == 0)
        {
            return;
        }

        var index = SelectedIndex;
        switch (args.KeyCode)
        {
            case Keys.Up:
                index = index <= 0 ? 0 : index - 1;
                break;
            case Keys.Down:
                index = index < 0 ? 0 : index + 1;
                break;
            case Keys.Home:
                index = 0;
                break;
            case Keys.End:
                index = ItemCount - 1;
                break;
            default:
                return;
        }

        SelectedIndex = XamlMath.Clamp(index, 0, ItemCount - 1);
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(VisualPointerEventArgs args)
    {
        base.OnPointerMoved(args);
        SetHover(IndexAt(args.Position));
    }

    /// <inheritdoc />
    protected override void OnPointerExited(VisualPointerEventArgs args)
    {
        base.OnPointerExited(args);
        SetHover(-1);
    }

    private void SetHover(int index)
    {
        if (!IsEnabled)
        {
            index = -1;
        }

        if (_hoverIndex == index)
        {
            return;
        }

        _hoverIndex = index;
        SyncContainerStates();
        InvalidateVisual();
    }

    private void SyncContainerStates()
    {
        for (var offset = 0; offset < Items.Count; offset++)
        {
            if (Items[offset] is not ListBoxItem container)
            {
                continue;
            }

            var index = FirstRealizedIndex + offset;
            container.IsSelected = index == SelectedIndex;
            container.SetIsPointerOver(IsEnabled && index == _hoverIndex);
        }
    }

    private int IndexAt(PointF surfacePoint)
    {
        if (FirstRealizedIndex < 0)
        {
            return -1;
        }

        for (var index = 0; index < Items.Count; index++)
        {
            if (Items[index].AbsoluteBounds.Contains(surfacePoint))
            {
                return FirstRealizedIndex + index;
            }
        }

        return -1;
    }

    private void SyncSelectedItemFromIndex()
    {
        _syncingSelection = true;
        try
        {
            var index = SelectedIndex;
            SelectedItem = index >= 0 && index < ItemCount ? GetItemAt(index) : null;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void SyncSelectedIndexFromItem()
    {
        _syncingSelection = true;
        try
        {
            var index = -1;
            if (SelectedItem is { } item)
            {
                for (var candidate = 0; candidate < ItemCount; candidate++)
                {
                    if (Equals(GetItemAt(candidate), item))
                    {
                        index = candidate;
                        break;
                    }
                }
            }

            SelectedIndex = index;
        }
        finally
        {
            _syncingSelection = false;
        }
    }
}
