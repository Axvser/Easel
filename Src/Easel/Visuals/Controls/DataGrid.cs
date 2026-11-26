using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Easel;
using XamlBrush = Easel.Brush;
using XamlDrawing = Easel.Controls.XamlDrawing;
using XamlTextAlignment = Easel.Controls.TextAlignment;
using XamlVisibility = Easel.Controls.Visibility;

namespace Easel.Visuals.Controls;

/// <summary>How a <see cref="DataGridColumn"/> paints its cell text.</summary>
public enum DataGridColumnKind
{
    /// <summary>Plain text.</summary>
    Text = 0,

    /// <summary>A rounded status pill behind the text.</summary>
    Badge = 1,

    /// <summary>A boolean check box.</summary>
    Check = 2,

    /// <summary>A 0–100 progress bar with the value at the right.</summary>
    Progress = 3,
}

/// <summary>One column of a <see cref="DataGrid"/>.</summary>
/// <remarks>
/// The column reads its value with a binding path, so nested paths such as <c>Owner.Name</c> work. A width of zero
/// or <see cref="double.NaN"/> shares the room left after the fixed columns; <see cref="MinWidth"/> keeps the share
/// usable when the grid is narrow.
/// </remarks>
public sealed class DataGridColumn : INotifyPropertyChanged
{
    private string _header = string.Empty;
    private string _bindingPath = string.Empty;
    private double _width = 120d;
    private double _minWidth = 48d;
    private double _maxWidth = double.PositiveInfinity;
    private string _stringFormat = string.Empty;
    private DataGridColumnKind _kind = DataGridColumnKind.Text;
    private XamlTextAlignment _textAlignment = XamlTextAlignment.Left;
    private Color _foreground = Color.Empty;
    private bool _isSortable = true;
    private Func<object?, string?>? _textSelector;
    private Func<object?, Color>? _colorSelector;
    private Func<object?, double>? _progressSelector;
    private DataTemplate? _cellTemplate;
    private Func<object?, VisualElement>? _cellFactory;
    private BindingPath? _path;

    /// <summary>Raised when a property that affects layout or painting changed.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The header label.</summary>
    public string Header
    {
        get => _header;
        set => Set(ref _header, value ?? string.Empty);
    }

    /// <summary>The dot-separated property path read from each row; empty uses the row itself.</summary>
    public string BindingPath
    {
        get => _bindingPath;
        set => Set(ref _bindingPath, value ?? string.Empty);
    }

    /// <summary>The fixed width in pixels, or zero/<see cref="double.NaN"/> to share the remaining room.</summary>
    public double Width
    {
        get => _width;
        set => Set(ref _width, value);
    }

    /// <summary>The smallest width a resize or share may assign.</summary>
    public double MinWidth
    {
        get => _minWidth;
        set => Set(ref _minWidth, Math.Max(0d, value));
    }

    /// <summary>The largest width a resize or share may assign.</summary>
    public double MaxWidth
    {
        get => _maxWidth;
        set => Set(ref _maxWidth, Math.Max(0d, value));
    }

    /// <summary>A format string for <see cref="string.Format(IFormatProvider, string, object?)"/>; empty uses <c>ToString</c>.</summary>
    public string StringFormat
    {
        get => _stringFormat;
        set => Set(ref _stringFormat, value ?? string.Empty);
    }

    /// <summary>How the cell is painted.</summary>
    public DataGridColumnKind Kind
    {
        get => _kind;
        set => Set(ref _kind, value);
    }

    /// <summary>The horizontal alignment of the cell text.</summary>
    public XamlTextAlignment TextAlignment
    {
        get => _textAlignment;
        set => Set(ref _textAlignment, value);
    }

    /// <summary>The text colour, or <see cref="Color.Empty"/> for the theme foreground.</summary>
    public Color Foreground
    {
        get => _foreground;
        set => Set(ref _foreground, value);
    }

    /// <summary>Whether clicking the header sorts by this column.</summary>
    public bool IsSortable
    {
        get => _isSortable;
        set => Set(ref _isSortable, value);
    }

    /// <summary>Replaces the formatted text; the row object is the argument.</summary>
    public Func<object?, string?>? TextSelector
    {
        get => _textSelector;
        set => Set(ref _textSelector, value);
    }

    /// <summary>Chooses a badge/progress colour; the row object is the argument.</summary>
    public Func<object?, Color>? ColorSelector
    {
        get => _colorSelector;
        set => Set(ref _colorSelector, value);
    }

    /// <summary>Chooses a 0–100 progress value; the row object is the argument.</summary>
    public Func<object?, double>? ProgressSelector
    {
        get => _progressSelector;
        set => Set(ref _progressSelector, value);
    }

    /// <summary>Creates a cell element from a template; <see cref="CellFactory"/> wins when both are set.</summary>
    public DataTemplate? CellTemplate
    {
        get => _cellTemplate;
        set => Set(ref _cellTemplate, value);
    }

    /// <summary>Creates a cell element for a row; takes precedence over <see cref="CellTemplate"/>.</summary>
    public Func<object?, VisualElement>? CellFactory
    {
        get => _cellFactory;
        set => Set(ref _cellFactory, value);
    }

    /// <summary>Whether this column itself supplies a cell element through <see cref="CellFactory"/> or <see cref="CellTemplate"/>.</summary>
    /// <remarks>
    /// The grid may also realize an element for a non-text <see cref="Kind"/> from its built-in cell template.
    /// Per-column factory or template still wins over that built-in appearance.
    /// </remarks>
    public bool HasCellElement => _cellFactory is not null || _cellTemplate is not null;

    /// <summary>Creates the element for one row's cell, or <see langword="null"/> when the column paints itself.</summary>
    /// <param name="item">The row object; the element receives it as its <see cref="VisualElement.DataContext"/>.</param>
    public VisualElement? CreateCellElement(object? item)
    {
        var visual = _cellFactory is { } factory
            ? factory(item)
            : _cellTemplate?.Create();
        if (visual is null)
        {
            return null;
        }

        if (item is not null && !visual.IsSet(VisualElement.DataContextProperty))
        {
            visual.DataContext = item;
        }

        return visual;
    }

    /// <summary>The text shown for <paramref name="item"/>.</summary>
    /// <param name="item">The row object.</param>
    public string GetText(object? item)
    {
        if (TextSelector is { } selector)
        {
            return selector(item) ?? string.Empty;
        }

        var value = GetValue(item);
        if (value is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(StringFormat))
        {
            if (StringFormat.IndexOf("{0", StringComparison.Ordinal) >= 0)
            {
                try
                {
                    return string.Format(CultureInfo.CurrentCulture, StringFormat, value);
                }
                catch (FormatException)
                {
                    return value.ToString() ?? string.Empty;
                }
            }

            if (value is IFormattable formattable)
            {
                try
                {
                    return formattable.ToString(StringFormat, CultureInfo.CurrentCulture);
                }
                catch (FormatException)
                {
                    return value.ToString() ?? string.Empty;
                }
            }
        }

        return Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
    }

    /// <summary>Reads the raw cell value.</summary>
    /// <param name="item">The row object.</param>
    public object? GetValue(object? item)
    {
        if (item is null)
        {
            return null;
        }

        _path ??= new BindingPath(BindingPath);
        return _path.TryResolve(item, out var value, out _, out _) ? value : null;
    }

    /// <summary>Reads the badge/progress colour for <paramref name="item"/>, or <see cref="Color.Empty"/>.</summary>
    /// <param name="item">The row object.</param>
    public Color GetColor(object? item) => ColorSelector?.Invoke(item) ?? Color.Empty;

    /// <summary>Reads the clamped 0–100 progress value for <paramref name="item"/>.</summary>
    /// <param name="item">The row object.</param>
    public double GetProgress(object? item)
    {
        var value = ProgressSelector?.Invoke(item);
        if (value is null)
        {
            var raw = GetValue(item);
            try
            {
                value = raw is null ? 0d : Convert.ToDouble(raw, CultureInfo.CurrentCulture);
            }
            catch (FormatException)
            {
                value = 0d;
            }
            catch (InvalidCastException)
            {
                value = 0d;
            }
        }

        return Math.Max(0d, Math.Min(100d, value.Value));
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        if (propertyName == nameof(BindingPath))
        {
            _path = null;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>The objects carried by a row drag between two data grids.</summary>
public sealed class DataGridDragPayload
{
    internal DataGridDragPayload(DataGrid source, IReadOnlyList<object?> items, int sourceIndex)
    {
        Source = source;
        Items = items;
        SourceIndex = sourceIndex;
    }

    /// <summary>The grid that started the drag.</summary>
    public DataGrid Source { get; }

    /// <summary>The dragged rows, in source order.</summary>
    public IReadOnlyList<object?> Items { get; }

    /// <summary>The source index of the first dragged row, or -1 for an unordered drag.</summary>
    public int SourceIndex { get; }
}

/// <summary>Data for <see cref="DataGrid.DragStarting"/>; a handler may add file data or change the effects.</summary>
public sealed class DataGridDragStartingEventArgs : EventArgs
{
    internal DataGridDragStartingEventArgs(IDataObject data, DragDropEffects allowedEffects, string ghostText)
    {
        Data = data;
        AllowedEffects = allowedEffects;
        GhostText = ghostText;
    }

    /// <summary>The data object the drag will use; add or replace formats here.</summary>
    public IDataObject Data { get; }

    /// <summary>The permitted effects; set to <see cref="DragDropEffects.None"/> to cancel the drag.</summary>
    public DragDropEffects AllowedEffects { get; set; }

    /// <summary>The floating label text.</summary>
    public string GhostText { get; set; }
}

/// <summary>Data for <see cref="DataGrid.Dropped"/>.</summary>
public sealed class DataGridDropEventArgs : EventArgs
{
    internal DataGridDropEventArgs(
        IDataObject data,
        IReadOnlyList<string> files,
        string? text,
        int insertIndex,
        DragDropEffects effects,
        DataGridDragPayload? payload)
    {
        Data = data;
        Files = files;
        Text = text;
        InsertIndex = insertIndex;
        Effects = effects;
        Payload = payload;
    }

    /// <summary>The dropped data object.</summary>
    public IDataObject Data { get; }

    /// <summary>The dropped file paths; empty when the drag carries no files.</summary>
    public IReadOnlyList<string> Files { get; }

    /// <summary>The dropped text, or <see langword="null"/>.</summary>
    public string? Text { get; }

    /// <summary>The row insertion index in the current view.</summary>
    public int InsertIndex { get; }

    /// <summary>The effect the drop used; a handler may set another value.</summary>
    public DragDropEffects Effects { get; set; }

    /// <summary>The payload when the drag came from a data grid.</summary>
    public DataGridDragPayload? Payload { get; }

    /// <summary>Whether the data came from outside this grid.</summary>
    public bool IsExternal { get; internal set; }

    /// <summary>Whether a handler consumed the drop.</summary>
    public bool Handled { get; set; }
}

/// <summary>A 2-D, row-virtualized data table with sortable headers, themed rows and native drag in/out.</summary>
/// <remarks>
/// <para>
/// The grid snapshots its source into a view list and paints only the visible rows, so a few thousand rows cost the
/// same per frame as ten. Header clicks sort ascending, then descending, then back to source order. The selected row
/// follows the item across sorts.
/// </para>
/// <para>
/// Rows can be dragged out as text and <c>Easel.DataGrid.Rows</c>; dropping that payload on another grid moves the
/// same objects. Dropping files or text raises <see cref="Dropped"/> and leaves the row shape to the application.
/// The drag itself is the native OLE loop, so Explorer and other applications are first-class targets.
/// </para>
/// </remarks>
public class DataGrid : TemplatedControl
{
    /// <summary>The sequence whose items become rows.</summary>
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(DataGrid),
        new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The fixed height of one row, in pixels.</summary>
    public static readonly DependencyProperty RowHeightProperty = DependencyProperty.Register(
        nameof(RowHeight), typeof(double), typeof(DataGrid),
        new PropertyMetadata(30d) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The height of the column header, in pixels.</summary>
    public static readonly DependencyProperty HeaderHeightProperty = DependencyProperty.Register(
        nameof(HeaderHeight), typeof(double), typeof(DataGrid),
        new PropertyMetadata(32d) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The width of the row-number gutter when <see cref="ShowRowNumbers"/> is set.</summary>
    public static readonly DependencyProperty RowNumberWidthProperty = DependencyProperty.Register(
        nameof(RowNumberWidth), typeof(double), typeof(DataGrid),
        new PropertyMetadata(44d) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>Whether the leading row-number gutter is drawn.</summary>
    public static readonly DependencyProperty ShowRowNumbersProperty = DependencyProperty.Register(
        nameof(ShowRowNumbers), typeof(bool), typeof(DataGrid),
        new PropertyMetadata(false) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>Whether one-pixel cell separators are drawn.</summary>
    public static readonly DependencyProperty ShowGridLinesProperty = DependencyProperty.Register(
        nameof(ShowGridLines), typeof(bool), typeof(DataGrid),
        new PropertyMetadata(true) { AffectsRender = true });

    /// <summary>Whether alternate rows get a subtle tint.</summary>
    public static readonly DependencyProperty AlternateRowsProperty = DependencyProperty.Register(
        nameof(AlternateRows), typeof(bool), typeof(DataGrid),
        new PropertyMetadata(true) { AffectsRender = true });

    /// <summary>The text shown when the source has no rows.</summary>
    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText), typeof(string), typeof(DataGrid),
        new PropertyMetadata("No rows") { AffectsRender = true });

    /// <summary>The zero-based selected row in the current view, or -1.</summary>
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(DataGrid),
        new PropertyMetadata(-1) { AffectsRender = true });

    /// <summary>The selected source item, or <see langword="null"/>.</summary>
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(object), typeof(DataGrid),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The row text colour as a brush; an empty value uses the theme foreground.</summary>
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(XamlBrush), typeof(DataGrid),
        new PropertyMetadata(null, null, static (_, value) => ColorBrushCoercion.Coerce(value))
        {
            AffectsRender = true,
        });

    /// <summary>Whether dragging a row inside this grid reorders it.</summary>
    public static readonly DependencyProperty AllowRowReorderProperty = DependencyProperty.Register(
        nameof(AllowRowReorder), typeof(bool), typeof(DataGrid),
        new PropertyMetadata(true));

    /// <summary>The normal row fill; an empty value paints no base fill.</summary>
    public static readonly DependencyProperty RowBackgroundProperty = RegisterBrush(nameof(RowBackground));

    /// <summary>The tint alternate rows receive; <see langword="null"/> uses a translucent foreground tint.</summary>
    public static readonly DependencyProperty AlternateRowBackgroundProperty = RegisterBrush(nameof(AlternateRowBackground));

    /// <summary>The tint the row under the pointer receives; <see langword="null"/> uses a translucent foreground tint.</summary>
    public static readonly DependencyProperty HoveredRowBackgroundProperty = RegisterBrush(nameof(HoveredRowBackground));

    /// <summary>The selected row fill; <see langword="null"/> uses the theme selection role.</summary>
    public static readonly DependencyProperty SelectedRowBackgroundProperty = RegisterBrush(nameof(SelectedRowBackground));

    /// <summary>The two-pixel accent bar on the selected row; <see langword="null"/> uses the theme accent.</summary>
    public static readonly DependencyProperty SelectedRowAccentProperty = RegisterBrush(nameof(SelectedRowAccent));

    /// <summary>The cell text colour; <see langword="null"/> uses the grid foreground, and a column's own colour still wins.</summary>
    public static readonly DependencyProperty CellForegroundProperty = RegisterBrush(nameof(CellForeground));

    /// <summary>The horizontal inset of one cell's content, in pixels.</summary>
    public static readonly DependencyProperty CellPaddingProperty = RegisterDouble(nameof(CellPadding), 10d);

    /// <summary>The one-pixel grid line colour; <see langword="null"/> uses a translucent foreground tint.</summary>
    public static readonly DependencyProperty GridLineBrushProperty = RegisterBrush(nameof(GridLineBrush));

    /// <summary>The row-number text colour; <see langword="null"/> uses the theme muted foreground.</summary>
    public static readonly DependencyProperty RowNumberForegroundProperty = RegisterBrush(nameof(RowNumberForeground));

    /// <summary>The row-number gutter fill; <see langword="null"/> uses a translucent foreground tint.</summary>
    public static readonly DependencyProperty RowNumberBackgroundProperty = RegisterBrush(nameof(RowNumberBackground));

    /// <summary>The column header fill; <see langword="null"/> uses the theme surface-alt role.</summary>
    public static readonly DependencyProperty ColumnHeaderBackgroundProperty = RegisterBrush(nameof(ColumnHeaderBackground));

    /// <summary>The column header text colour; <see langword="null"/> uses the grid foreground.</summary>
    public static readonly DependencyProperty ColumnHeaderForegroundProperty = RegisterBrush(nameof(ColumnHeaderForeground));

    /// <summary>The header tint under the pointer; <see langword="null"/> uses a translucent foreground tint.</summary>
    public static readonly DependencyProperty ColumnHeaderHoverBackgroundProperty = RegisterBrush(nameof(ColumnHeaderHoverBackground));

    /// <summary>The header's bottom rule colour; <see langword="null"/> uses the theme control border.</summary>
    public static readonly DependencyProperty ColumnHeaderBorderBrushProperty = RegisterBrush(nameof(ColumnHeaderBorderBrush));

    /// <summary>The font style used for column headers.</summary>
    public static readonly DependencyProperty ColumnHeaderFontStyleProperty = DependencyProperty.Register(
        nameof(ColumnHeaderFontStyle), typeof(FontStyle), typeof(DataGrid),
        new PropertyMetadata(FontStyle.Bold) { AffectsRender = true });

    /// <summary>The horizontal inset of one header label, in pixels.</summary>
    public static readonly DependencyProperty HeaderPaddingProperty = RegisterDouble(nameof(HeaderPadding), 10d);

    /// <summary>The sort chevron colour; <see langword="null"/> uses the theme accent.</summary>
    public static readonly DependencyProperty SortIndicatorBrushProperty = RegisterBrush(nameof(SortIndicatorBrush));

    /// <summary>The dashed border around the empty state; <see langword="null"/> uses a translucent foreground tint.</summary>
    public static readonly DependencyProperty EmptyStateBorderBrushProperty = RegisterBrush(nameof(EmptyStateBorderBrush));

    /// <summary>The empty-state label colour; <see langword="null"/> uses the theme muted foreground.</summary>
    public static readonly DependencyProperty EmptyStateForegroundProperty = RegisterBrush(nameof(EmptyStateForeground));

    /// <summary>The badge pill fill when the column supplies no colour; <see langword="null"/> uses the theme accent.</summary>
    public static readonly DependencyProperty BadgeBackgroundProperty = RegisterBrush(nameof(BadgeBackground));

    /// <summary>The badge text colour; <see langword="null"/> picks black or white from the pill fill.</summary>
    public static readonly DependencyProperty BadgeForegroundProperty = RegisterBrush(nameof(BadgeForeground));

    /// <summary>The badge pill height, in pixels.</summary>
    public static readonly DependencyProperty BadgeHeightProperty = RegisterDouble(nameof(BadgeHeight), 20d);

    /// <summary>The horizontal inset of the badge text, in pixels.</summary>
    public static readonly DependencyProperty BadgePaddingProperty = RegisterDouble(nameof(BadgePadding), 9d);

    /// <summary>The badge corner radius; <see cref="double.NaN"/> makes the pill fully rounded.</summary>
    public static readonly DependencyProperty BadgeCornerRadiusProperty = RegisterDouble(nameof(BadgeCornerRadius), double.NaN);

    /// <summary>The check box fill when checked; <see langword="null"/> uses the theme accent.</summary>
    public static readonly DependencyProperty CheckFillProperty = RegisterBrush(nameof(CheckFill));

    /// <summary>The check box outline when unchecked; <see langword="null"/> uses the theme control border.</summary>
    public static readonly DependencyProperty CheckBorderBrushProperty = RegisterBrush(nameof(CheckBorderBrush));

    /// <summary>The check mark stroke; <see langword="null"/> uses the theme accent foreground.</summary>
    public static readonly DependencyProperty CheckMarkBrushProperty = RegisterBrush(nameof(CheckMarkBrush));

    /// <summary>The check box edge length, in pixels.</summary>
    public static readonly DependencyProperty CheckSizeProperty = RegisterDouble(nameof(CheckSize), 16d);

    /// <summary>The check box corner radius, in pixels.</summary>
    public static readonly DependencyProperty CheckCornerRadiusProperty = RegisterDouble(nameof(CheckCornerRadius), 4d);

    /// <summary>The progress track fill; <see langword="null"/> uses a translucent foreground tint.</summary>
    public static readonly DependencyProperty ProgressTrackBrushProperty = RegisterBrush(nameof(ProgressTrackBrush));

    /// <summary>The progress fill when the column supplies no colour; <see langword="null"/> uses the theme accent.</summary>
    public static readonly DependencyProperty ProgressFillBrushProperty = RegisterBrush(nameof(ProgressFillBrush));

    /// <summary>The progress label colour; <see langword="null"/> uses the theme muted foreground.</summary>
    public static readonly DependencyProperty ProgressLabelForegroundProperty = RegisterBrush(nameof(ProgressLabelForeground));

    /// <summary>The progress track height, in pixels.</summary>
    public static readonly DependencyProperty ProgressTrackHeightProperty = RegisterDouble(nameof(ProgressTrackHeight), 6d);

    /// <summary>The progress track and fill corner radius, in pixels.</summary>
    public static readonly DependencyProperty ProgressCornerRadiusProperty = RegisterDouble(nameof(ProgressCornerRadius), 3d);

    /// <summary>The template part that bounds the column header strip.</summary>
    /// <remarks>
    /// Shell parts are a geometry contract: a part only supplies a rectangle, the grid paints headers, rows and cells
    /// inside it, and a missing part falls back to the self-drawn layout. Parts therefore stay transparent and out of
    /// hit testing; row, header and cell appearance comes from the public style properties, never from the part.
    /// </remarks>
    public const string ColumnHeadersPartName = "PART_ColumnHeaders";

    /// <summary>The template part that bounds the data row area.</summary>
    public const string RowsPartName = "PART_Rows";

    /// <summary>The template part that bounds the realized cell elements; falls back to <see cref="RowsPartName"/>.</summary>
    public const string CellHostPartName = "PART_CellHost";

    /// <summary>The template part that bounds the vertical scroll bar.</summary>
    public const string ScrollBarPartName = "PART_ScrollBar";

    /// <summary>The decorative template part above the scroll bar; the grid paints it but never arranges content there.</summary>
    public const string CornerPartName = "PART_Corner";

    /// <summary>The resource key of the built-in text-cell template; no default template is registered, so text columns stay self-drawn.</summary>
    public const string TextCellTemplateKey = "DataGrid.TextCell";

    /// <summary>The resource key of the built-in badge-cell template.</summary>
    public const string BadgeCellTemplateKey = "DataGrid.BadgeCell";

    /// <summary>The resource key of the built-in check-cell template.</summary>
    public const string CheckCellTemplateKey = "DataGrid.CheckCell";

    /// <summary>The resource key of the built-in progress-cell template.</summary>
    public const string ProgressCellTemplateKey = "DataGrid.ProgressCell";

    // 种类到资源键、以及资源缺失时的兜底工厂都集中在这两张表里；绘制路径不再按 Kind 分支。
    private static readonly Dictionary<DataGridColumnKind, string> CellTemplateKeys = new()
    {
        [DataGridColumnKind.Text] = TextCellTemplateKey,
        [DataGridColumnKind.Badge] = BadgeCellTemplateKey,
        [DataGridColumnKind.Check] = CheckCellTemplateKey,
        [DataGridColumnKind.Progress] = ProgressCellTemplateKey,
    };

    private static readonly Dictionary<DataGridColumnKind, Func<DataGridCell>> FallbackCells = new()
    {
        [DataGridColumnKind.Badge] = static () => new DataGridBadgeCell(),
        [DataGridColumnKind.Check] = static () => new DataGridCheckCell(),
        [DataGridColumnKind.Progress] = static () => new DataGridProgressCell(),
    };

    private const float ScrollBarWidth = 12f;

    private readonly ObservableCollection<DataGridColumn> _columns = [];
    private readonly List<object?> _rows = [];
    private readonly List<int> _view = [];
    private readonly DataGridCellHost _cellHost;
    private readonly ScrollBar _scrollBar;
    private readonly HashSet<DataGridColumn> _observedColumns = [];
    private INotifyCollectionChanged? _observedSource;
    private bool _syncingSelection;
    private bool _syncingScroll;
    private int _sortColumn = -1;
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;
    private int _hoverRow = -1;
    private int _hoverHeader = -1;
    private int _dragRow = -1;
    private PointF _dragOrigin;
    private bool _dragArmed;
    private DataGridColumn? _resizeColumn;
    private float _resizeStartX;
    private float _resizeStartWidth;
    private int _dropIndex = -1;
    private float _verticalOffset;
    private float _bodyLeft;
    private float _bodyTop;
    private float _bodyRight;
    private float _bodyBottom;
    private float _headerLeft;
    private float _headerTop;
    private float _headerRight;
    private float _headerBottom;
    private float _contentWidth;
    private float[] _widths = [];
    private readonly Dictionary<DataGridColumnKind, DataTemplate?> _cellTemplates = [];
    private bool[] _cellVisual = [];
    private Font? _headerFont;
    private Font? _headerFontBase;
    private FontStyle _headerFontStyle;

    /// <summary>Creates an empty table with theme metrics; the grid paints no surface of its own.</summary>
    public DataGrid()
    {
        Focusable = true;
        IsHitTestVisible = true;
        AllowDrop = true;
        SetThemeMetric(CornerRadiusProperty, static palette => palette.CornerRadius);

        _columns.CollectionChanged += OnColumnsChanged;
        // 单元格元素在自绘内容之上、滚动条之下；先加宿主再把滚动条加在它后面。
        _cellHost = new DataGridCellHost();
        Add(_cellHost);
        _scrollBar = new ScrollBar
        {
            Orientation = Orientation.Vertical,
            Width = ScrollBarWidth,
            Visibility = XamlVisibility.Collapsed,
            Focusable = false,
            IsTabStop = false,
        };
        _scrollBar.ValueChanged += OnScrollBarValueChanged;
        Add(_scrollBar);
    }

    /// <summary>The columns, in painting order.</summary>
    public ObservableCollection<DataGridColumn> Columns => _columns;

    /// <summary>Raised after <see cref="SelectedIndex"/> or <see cref="SelectedItem"/> changes.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised after the sort column or direction changes.</summary>
    public event EventHandler? SortChanged;

    /// <summary>Raised before a row drag starts; add file data or cancel here.</summary>
    public event EventHandler<DataGridDragStartingEventArgs>? DragStarting;

    /// <summary>Raised after a drop was processed; external rows/files are left to the handler.</summary>
    public event EventHandler<DataGridDropEventArgs>? Dropped;

    /// <summary>The sequence whose items become rows.</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>The fixed height of one row, in pixels.</summary>
    public double RowHeight
    {
        get => (double)GetValue(RowHeightProperty)!;
        set => SetValue(RowHeightProperty, value);
    }

    /// <summary>The height of the column header, in pixels.</summary>
    public double HeaderHeight
    {
        get => (double)GetValue(HeaderHeightProperty)!;
        set => SetValue(HeaderHeightProperty, value);
    }

    /// <summary>The width of the row-number gutter when <see cref="ShowRowNumbers"/> is set.</summary>
    public double RowNumberWidth
    {
        get => (double)GetValue(RowNumberWidthProperty)!;
        set => SetValue(RowNumberWidthProperty, value);
    }

    /// <summary>Whether the leading row-number gutter is drawn.</summary>
    public bool ShowRowNumbers
    {
        get => (bool)GetValue(ShowRowNumbersProperty)!;
        set => SetValue(ShowRowNumbersProperty, value);
    }

    /// <summary>Whether one-pixel cell separators are drawn.</summary>
    public bool ShowGridLines
    {
        get => (bool)GetValue(ShowGridLinesProperty)!;
        set => SetValue(ShowGridLinesProperty, value);
    }

    /// <summary>Whether alternate rows get a subtle tint.</summary>
    public bool AlternateRows
    {
        get => (bool)GetValue(AlternateRowsProperty)!;
        set => SetValue(AlternateRowsProperty, value);
    }

    /// <summary>The text shown when the source has no rows.</summary>
    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty)!;
        set => SetValue(EmptyTextProperty, value ?? string.Empty);
    }

    /// <summary>The zero-based selected row in the current view, or -1.</summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty)!;
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>The selected source item, or <see langword="null"/>.</summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>The row text colour; an empty colour uses the active theme.</summary>
    public Color Foreground
    {
        get => GetValue(ForegroundProperty) is XamlBrush brush ? brush.ApproximateColor : Color.Empty;
        set
        {
            if (value.IsEmpty)
            {
                ClearValue(ForegroundProperty);
            }
            else
            {
                SetValue(ForegroundProperty, new SolidColorBrush(value));
            }
        }
    }

    /// <summary>Whether dragging a row inside this grid reorders it.</summary>
    public bool AllowRowReorder
    {
        get => (bool)GetValue(AllowRowReorderProperty)!;
        set => SetValue(AllowRowReorderProperty, value);
    }

    /// <summary>The normal row fill; <see langword="null"/> paints no base fill.</summary>
    public XamlBrush? RowBackground
    {
        get => (XamlBrush?)GetValue(RowBackgroundProperty);
        set => SetValue(RowBackgroundProperty, value);
    }

    /// <summary>The tint alternate rows receive; <see langword="null"/> uses a translucent foreground tint.</summary>
    public XamlBrush? AlternateRowBackground
    {
        get => (XamlBrush?)GetValue(AlternateRowBackgroundProperty);
        set => SetValue(AlternateRowBackgroundProperty, value);
    }

    /// <summary>The tint the row under the pointer receives; <see langword="null"/> uses a translucent foreground tint.</summary>
    public XamlBrush? HoveredRowBackground
    {
        get => (XamlBrush?)GetValue(HoveredRowBackgroundProperty);
        set => SetValue(HoveredRowBackgroundProperty, value);
    }

    /// <summary>The selected row fill; <see langword="null"/> uses the theme selection role.</summary>
    public XamlBrush? SelectedRowBackground
    {
        get => (XamlBrush?)GetValue(SelectedRowBackgroundProperty);
        set => SetValue(SelectedRowBackgroundProperty, value);
    }

    /// <summary>The accent bar on the selected row; <see langword="null"/> uses the theme accent.</summary>
    public XamlBrush? SelectedRowAccent
    {
        get => (XamlBrush?)GetValue(SelectedRowAccentProperty);
        set => SetValue(SelectedRowAccentProperty, value);
    }

    /// <summary>The cell text colour; <see langword="null"/> uses the grid foreground, and a column's own colour still wins.</summary>
    public XamlBrush? CellForeground
    {
        get => (XamlBrush?)GetValue(CellForegroundProperty);
        set => SetValue(CellForegroundProperty, value);
    }

    /// <summary>The horizontal inset of one cell's content, in pixels.</summary>
    public double CellPadding
    {
        get => (double)GetValue(CellPaddingProperty)!;
        set => SetValue(CellPaddingProperty, value);
    }

    /// <summary>The one-pixel grid line colour; <see langword="null"/> uses a translucent foreground tint.</summary>
    public XamlBrush? GridLineBrush
    {
        get => (XamlBrush?)GetValue(GridLineBrushProperty);
        set => SetValue(GridLineBrushProperty, value);
    }

    /// <summary>The row-number text colour; <see langword="null"/> uses the theme muted foreground.</summary>
    public XamlBrush? RowNumberForeground
    {
        get => (XamlBrush?)GetValue(RowNumberForegroundProperty);
        set => SetValue(RowNumberForegroundProperty, value);
    }

    /// <summary>The row-number gutter fill; <see langword="null"/> uses a translucent foreground tint.</summary>
    public XamlBrush? RowNumberBackground
    {
        get => (XamlBrush?)GetValue(RowNumberBackgroundProperty);
        set => SetValue(RowNumberBackgroundProperty, value);
    }

    /// <summary>The column header fill; <see langword="null"/> uses the theme surface-alt role.</summary>
    public XamlBrush? ColumnHeaderBackground
    {
        get => (XamlBrush?)GetValue(ColumnHeaderBackgroundProperty);
        set => SetValue(ColumnHeaderBackgroundProperty, value);
    }

    /// <summary>The column header text colour; <see langword="null"/> uses the grid foreground.</summary>
    public XamlBrush? ColumnHeaderForeground
    {
        get => (XamlBrush?)GetValue(ColumnHeaderForegroundProperty);
        set => SetValue(ColumnHeaderForegroundProperty, value);
    }

    /// <summary>The header tint under the pointer; <see langword="null"/> uses a translucent foreground tint.</summary>
    public XamlBrush? ColumnHeaderHoverBackground
    {
        get => (XamlBrush?)GetValue(ColumnHeaderHoverBackgroundProperty);
        set => SetValue(ColumnHeaderHoverBackgroundProperty, value);
    }

    /// <summary>The header's bottom rule colour; <see langword="null"/> uses the theme control border.</summary>
    public XamlBrush? ColumnHeaderBorderBrush
    {
        get => (XamlBrush?)GetValue(ColumnHeaderBorderBrushProperty);
        set => SetValue(ColumnHeaderBorderBrushProperty, value);
    }

    /// <summary>The font style used for column headers.</summary>
    public FontStyle ColumnHeaderFontStyle
    {
        get => (FontStyle)GetValue(ColumnHeaderFontStyleProperty)!;
        set => SetValue(ColumnHeaderFontStyleProperty, value);
    }

    /// <summary>The horizontal inset of one header label, in pixels.</summary>
    public double HeaderPadding
    {
        get => (double)GetValue(HeaderPaddingProperty)!;
        set => SetValue(HeaderPaddingProperty, value);
    }

    /// <summary>The sort chevron colour; <see langword="null"/> uses the theme accent.</summary>
    public XamlBrush? SortIndicatorBrush
    {
        get => (XamlBrush?)GetValue(SortIndicatorBrushProperty);
        set => SetValue(SortIndicatorBrushProperty, value);
    }

    /// <summary>The dashed border around the empty state; <see langword="null"/> uses a translucent foreground tint.</summary>
    public XamlBrush? EmptyStateBorderBrush
    {
        get => (XamlBrush?)GetValue(EmptyStateBorderBrushProperty);
        set => SetValue(EmptyStateBorderBrushProperty, value);
    }

    /// <summary>The empty-state label colour; <see langword="null"/> uses the theme muted foreground.</summary>
    public XamlBrush? EmptyStateForeground
    {
        get => (XamlBrush?)GetValue(EmptyStateForegroundProperty);
        set => SetValue(EmptyStateForegroundProperty, value);
    }

    /// <summary>The badge pill fill when the column supplies no colour; <see langword="null"/> uses the theme accent.</summary>
    public XamlBrush? BadgeBackground
    {
        get => (XamlBrush?)GetValue(BadgeBackgroundProperty);
        set => SetValue(BadgeBackgroundProperty, value);
    }

    /// <summary>The badge text colour; <see langword="null"/> picks black or white from the pill fill.</summary>
    public XamlBrush? BadgeForeground
    {
        get => (XamlBrush?)GetValue(BadgeForegroundProperty);
        set => SetValue(BadgeForegroundProperty, value);
    }

    /// <summary>The badge pill height, in pixels.</summary>
    public double BadgeHeight
    {
        get => (double)GetValue(BadgeHeightProperty)!;
        set => SetValue(BadgeHeightProperty, value);
    }

    /// <summary>The horizontal inset of the badge text, in pixels.</summary>
    public double BadgePadding
    {
        get => (double)GetValue(BadgePaddingProperty)!;
        set => SetValue(BadgePaddingProperty, value);
    }

    /// <summary>The badge corner radius; <see cref="double.NaN"/> makes the pill fully rounded.</summary>
    public double BadgeCornerRadius
    {
        get => (double)GetValue(BadgeCornerRadiusProperty)!;
        set => SetValue(BadgeCornerRadiusProperty, value);
    }

    /// <summary>The check box fill when checked; <see langword="null"/> uses the theme accent.</summary>
    public XamlBrush? CheckFill
    {
        get => (XamlBrush?)GetValue(CheckFillProperty);
        set => SetValue(CheckFillProperty, value);
    }

    /// <summary>The check box outline when unchecked; <see langword="null"/> uses the theme control border.</summary>
    public XamlBrush? CheckBorderBrush
    {
        get => (XamlBrush?)GetValue(CheckBorderBrushProperty);
        set => SetValue(CheckBorderBrushProperty, value);
    }

    /// <summary>The check mark stroke; <see langword="null"/> uses the theme accent foreground.</summary>
    public XamlBrush? CheckMarkBrush
    {
        get => (XamlBrush?)GetValue(CheckMarkBrushProperty);
        set => SetValue(CheckMarkBrushProperty, value);
    }

    /// <summary>The check box edge length, in pixels.</summary>
    public double CheckSize
    {
        get => (double)GetValue(CheckSizeProperty)!;
        set => SetValue(CheckSizeProperty, value);
    }

    /// <summary>The check box corner radius, in pixels.</summary>
    public double CheckCornerRadius
    {
        get => (double)GetValue(CheckCornerRadiusProperty)!;
        set => SetValue(CheckCornerRadiusProperty, value);
    }

    /// <summary>The progress track fill; <see langword="null"/> uses a translucent foreground tint.</summary>
    public XamlBrush? ProgressTrackBrush
    {
        get => (XamlBrush?)GetValue(ProgressTrackBrushProperty);
        set => SetValue(ProgressTrackBrushProperty, value);
    }

    /// <summary>The progress fill when the column supplies no colour; <see langword="null"/> uses the theme accent.</summary>
    public XamlBrush? ProgressFillBrush
    {
        get => (XamlBrush?)GetValue(ProgressFillBrushProperty);
        set => SetValue(ProgressFillBrushProperty, value);
    }

    /// <summary>The progress label colour; <see langword="null"/> uses the theme muted foreground.</summary>
    public XamlBrush? ProgressLabelForeground
    {
        get => (XamlBrush?)GetValue(ProgressLabelForegroundProperty);
        set => SetValue(ProgressLabelForegroundProperty, value);
    }

    /// <summary>The progress track height, in pixels.</summary>
    public double ProgressTrackHeight
    {
        get => (double)GetValue(ProgressTrackHeightProperty)!;
        set => SetValue(ProgressTrackHeightProperty, value);
    }

    /// <summary>The progress track and fill corner radius, in pixels.</summary>
    public double ProgressCornerRadius
    {
        get => (double)GetValue(ProgressCornerRadiusProperty)!;
        set => SetValue(ProgressCornerRadiusProperty, value);
    }

    /// <summary>The sorted column index, or -1 for source order.</summary>
    public int SortColumnIndex => _sortColumn;

    /// <summary>The current sort direction.</summary>
    public ListSortDirection SortDirection => _sortDirection;

    /// <summary>The number of rows in the current view.</summary>
    public int RowCount => _view.Count;

    /// <summary>The number of cell elements currently realized for the visible window.</summary>
    public int RealizedCellCount => _cellHost.RealizedCount;

    /// <summary>The realized cell elements, in realization order.</summary>
    public IReadOnlyList<VisualElement> RealizedCells => _cellHost.RealizedCells;

    /// <summary>Returns the row object at a view index, or <see langword="null"/> when out of range.</summary>
    /// <param name="viewIndex">The zero-based index in the current sort order.</param>
    public object? GetRow(int viewIndex)
        => viewIndex >= 0 && viewIndex < _view.Count ? _rows[_view[viewIndex]] : null;

    /// <summary>Returns the view index of <paramref name="item"/>, or -1 when it is not visible.</summary>
    /// <param name="item">The source object to find.</param>
    public int IndexOf(object? item)
    {
        for (var viewIndex = 0; viewIndex < _view.Count; viewIndex++)
        {
            if (Equals(_rows[_view[viewIndex]], item))
            {
                return viewIndex;
            }
        }

        return -1;
    }

    /// <summary>The source index of the first realized row, or -1 when there are no rows.</summary>
    public int FirstVisibleRow
    {
        get
        {
            if (_view.Count == 0)
            {
                return -1;
            }

            var rowHeight = Math.Max(1d, RowHeight);
            var first = (int)Math.Floor(_verticalOffset / rowHeight);
            return XamlMath.Clamp(first, 0, _view.Count - 1);
        }
    }

    /// <summary>The number of rows that intersect the viewport.</summary>
    public int VisibleRowCount
    {
        get
        {
            if (_view.Count == 0 || _viewportHeight <= 0f)
            {
                return 0;
            }

            var rowHeight = Math.Max(1d, RowHeight);
            var first = FirstVisibleRow;
            var visible = (int)Math.Ceiling((_viewportHeight + (_verticalOffset - (first * rowHeight))) / rowHeight);
            return XamlMath.Clamp(visible, 0, _view.Count - first);
        }
    }

    private float _viewportHeight => Math.Max(0f, _bodyBottom - _bodyTop);

    /// <summary>Returns the row index under a point in the grid's local coordinates, or -1.</summary>
    /// <param name="point">The local point.</param>
    public int GetRowIndexAt(PointF point)
    {
        if (point.Y < _bodyTop || point.Y >= _bodyBottom || _view.Count == 0)
        {
            return -1;
        }

        var rowHeight = (float)Math.Max(1d, RowHeight);
        var index = (int)Math.Floor((point.Y - _bodyTop + _verticalOffset) / rowHeight);
        return index >= 0 && index < _view.Count ? index : -1;
    }

    /// <summary>Returns the column index under a point in the grid's local coordinates, or -1.</summary>
    /// <param name="point">The local point.</param>
    public int GetColumnIndexAt(PointF point)
    {
        if (point.X < _bodyLeft || point.X >= _bodyRight)
        {
            return -1;
        }

        var x = _bodyLeft + (ShowRowNumbers ? (float)RowNumberWidth : 0f);
        if (point.X < x)
        {
            return -1;
        }

        for (var index = 0; index < _widths.Length; index++)
        {
            if (point.X < x + _widths[index])
            {
                return index;
            }

            x += _widths[index];
        }

        return _widths.Length > 0 ? _widths.Length - 1 : -1;
    }

    /// <summary>Sorts by a column, cycling ascending, descending and source order.</summary>
    /// <param name="columnIndex">The column to sort by.</param>
    public void SortBy(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= _columns.Count || !_columns[columnIndex].IsSortable)
        {
            return;
        }

        if (_sortColumn != columnIndex)
        {
            _sortColumn = columnIndex;
            _sortDirection = ListSortDirection.Ascending;
        }
        else if (_sortDirection == ListSortDirection.Ascending)
        {
            _sortDirection = ListSortDirection.Descending;
        }
        else
        {
            _sortColumn = -1;
            _sortDirection = ListSortDirection.Ascending;
        }

        RebuildView();
        InvalidateVisual();
        SortChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsTemplated)
        {
            MeasureTemplate(availableSize);
        }

        var width = availableSize.Width >= int.MaxValue / 2 ? 560 : Math.Max(0, availableSize.Width);
        var height = availableSize.Height >= int.MaxValue / 2
            ? (int)Math.Ceiling(HeaderHeight + (Math.Max(1, Math.Min(_view.Count, 10)) * Math.Max(1d, RowHeight)) + 4d)
            : Math.Max(0, availableSize.Height);
        var barHeight = Math.Max(0, height - (int)Math.Ceiling(HeaderHeight) - (int)Math.Ceiling(Padding.Vertical));
        _scrollBar.Measure(new Size((int)Math.Ceiling(ScrollBarWidth), barHeight));
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        if (IsTemplated)
        {
            ArrangeTemplatedLayout(finalSize);
        }
        else
        {
            ArrangeSelfDrawnLayout(finalSize);
        }

        ArrangeCellHost(finalSize);
    }

    private void ArrangeSelfDrawnLayout(Size finalSize)
    {
        var padding = Padding;
        var border = BorderThickness;
        _bodyLeft = padding.Left + border.Left;
        var top = padding.Top + border.Top;
        var right = finalSize.Width - padding.Right - border.Right;
        var bottom = finalSize.Height - padding.Bottom - border.Bottom;
        var innerHeight = Math.Max(0f, bottom - top);
        var header = (float)Math.Min(Math.Max(1d, HeaderHeight), innerHeight);
        _bodyTop = top + header;
        _bodyRight = right;
        _bodyBottom = bottom;
        _headerLeft = _bodyLeft;
        _headerTop = top;
        _headerRight = right;
        _headerBottom = _bodyTop;

        ArrangeBody(finalSize, null);
    }

    // 模板只提供几何分区：表格仍自绘内容，因此模板部件必须是透明的、只定义位置。
    private void ArrangeTemplatedLayout(Size finalSize)
    {
        var padding = Padding;
        var border = BorderThickness;
        var content = new RectangleF(
            padding.Left + border.Left,
            padding.Top + border.Top,
            Math.Max(0f, finalSize.Width - padding.Horizontal - border.Horizontal),
            Math.Max(0f, finalSize.Height - padding.Vertical - border.Vertical));

        TemplateRoot?.Arrange(content);

        var header = PartBounds(ColumnHeadersPartName);
        var rows = PartBounds(RowsPartName) ?? PartBounds(CellHostPartName);
        var bar = PartBounds(ScrollBarPartName);

        var headerHeight = (float)Math.Min(Math.Max(1d, HeaderHeight), content.Height);
        var headerRect = header ?? new RectangleF(content.X, content.Y, content.Width, headerHeight);
        var bodyTop = Math.Min(content.Bottom, headerRect.Bottom);
        var rowsRect = rows ?? new RectangleF(content.X, bodyTop, content.Width, Math.Max(0f, content.Bottom - bodyTop));

        _headerLeft = headerRect.Left;
        _headerTop = headerRect.Top;
        _headerRight = headerRect.Right;
        _headerBottom = headerRect.Bottom;
        _bodyLeft = rowsRect.Left;
        _bodyTop = rowsRect.Top;
        _bodyRight = rowsRect.Right;
        _bodyBottom = rowsRect.Bottom;

        ArrangeBody(finalSize, bar);
    }

    private void ArrangeBody(Size finalSize, RectangleF? scrollBarPart)
    {
        var rowHeight = (float)Math.Max(1d, RowHeight);
        var extent = _view.Count * rowHeight;
        var viewport = _viewportHeight;
        var needsBar = extent > viewport + 0.5f;
        _scrollBar.Visibility = needsBar ? XamlVisibility.Visible : XamlVisibility.Collapsed;

        // 模板把滚动条放在单独的列里时，行区宽度已经让出了滚动条，不能再减一次。
        var barOutsideBody = scrollBarPart is { } outside && outside.X >= _bodyRight - 0.5f;
        var contentWidth = Math.Max(0f, _bodyRight - _bodyLeft);
        if (needsBar && !barOutsideBody)
        {
            contentWidth = Math.Max(0f, contentWidth - ScrollBarWidth);
        }

        _contentWidth = contentWidth;
        _widths = ResolveWidths(contentWidth);
        UpdateScrollBar(needsBar);

        if (!needsBar)
        {
            return;
        }

        var barRect = scrollBarPart ?? new RectangleF(_bodyRight - ScrollBarWidth, _bodyTop, ScrollBarWidth, viewport);
        _scrollBar.Measure(new Size((int)Math.Ceiling(barRect.Width), (int)Math.Ceiling(barRect.Height)));
        _scrollBar.Arrange(barRect);
    }

    // 模板部件坐标换回本控件局部坐标；部件缺失时返回 null，布局回退到自绘路径。
    private RectangleF? PartBounds(string name)
    {
        if (GetTemplateChild<VisualElement>(name) is not { } part)
        {
            return null;
        }

        var origin = AbsoluteBounds.Location;
        var bounds = part.AbsoluteBounds;
        return new RectangleF(bounds.X - origin.X, bounds.Y - origin.Y, bounds.Width, bounds.Height);
    }

    // 单元格宿主铺满控件，靠 Clip 裁到表头下方；元素坐标因此与自绘行是同一套。
    private void ArrangeCellHost(Size finalSize)
    {
        _cellHost.Visibility = XamlVisibility.Visible;
        _cellHost.Clip = new RectangleF(
            _bodyLeft,
            _bodyTop,
            Math.Max(0f, _bodyRight - _bodyLeft),
            Math.Max(0f, _bodyBottom - _bodyTop));
        _cellHost.Measure(finalSize);
        _cellHost.Arrange(new RectangleF(0f, 0f, finalSize.Width, finalSize.Height));
        UpdateCellElements();
    }

    private void UpdateCellElements()
    {
        RefreshCellVisuals();
        var rowHeight = (float)Math.Max(1d, RowHeight);
        if (_view.Count == 0 || _viewportHeight <= 0f)
        {
            _cellHost.Reset();
            return;
        }

        var first = Math.Max(0, (int)Math.Floor(_verticalOffset / rowHeight));
        var last = Math.Min(_view.Count - 1, (int)Math.Ceiling((_verticalOffset + _viewportHeight) / rowHeight));
        if (last < first)
        {
            _cellHost.Reset();
            return;
        }

        var gutter = ShowRowNumbers ? (float)Math.Max(0d, RowNumberWidth) : 0f;
        _cellHost.Update(
            _view,
            _rows,
            first,
            last,
            rowHeight,
            _verticalOffset,
            _bodyTop,
            _bodyLeft,
            gutter,
            _columns,
            _widths,
            _cellVisual,
            this);
    }

    // 每次布局重解一次：列模板优先，其次是按 Kind 取的资源模板，资源缺失时用内置兜底单元。
    private void RefreshCellVisuals()
    {
        var count = _columns.Count;
        if (_cellVisual.Length != count)
        {
            _cellVisual = new bool[count];
        }

        _cellTemplates.Clear();
        for (var index = 0; index < count; index++)
        {
            var column = _columns[index];
            var hasElement = column.HasCellElement;
            if (!hasElement && CellTemplateKeys.TryGetValue(column.Kind, out var key))
            {
                if (!_cellTemplates.TryGetValue(column.Kind, out var template))
                {
                    template = ResolveCellTemplate(key);
                    _cellTemplates[column.Kind] = template;
                }

                hasElement = template is not null || FallbackCells.ContainsKey(column.Kind);
            }

            _cellVisual[index] = hasElement;
        }
    }

    // 元素链上的同名资源先赢，宿主因此可以替换某个种类的外观；没有才回退到默认字典。
    private DataTemplate? ResolveCellTemplate(string key)
    {
        if (VisualResourceLookup.TryFindResource(this, key, out var local) && local is DataTemplate localTemplate)
        {
            return localTemplate;
        }

        return Theme.DefaultStyles.TryGetValue(key, out var fallback) && fallback is DataTemplate defaultTemplate
            ? defaultTemplate
            : null;
    }

    // 单元格宿主只认这一处创建逻辑：列的工厂/模板优先，之后才是 Kind 的内置模板与兜底单元。
    internal VisualElement? CreateCellElement(DataGridColumn column, object? item)
    {
        if (column.HasCellElement)
        {
            return column.CreateCellElement(item);
        }

        VisualElement? visual = null;
        var kind = column.Kind;
        if (_cellTemplates.TryGetValue(kind, out var template) && template is not null)
        {
            visual = template.Create();
        }

        if (visual is null && FallbackCells.TryGetValue(kind, out var factory))
        {
            visual = factory();
        }

        if (visual is null)
        {
            return null;
        }

        if (visual is DataGridCell cell)
        {
            cell.Owner = this;
            cell.Column = column;
        }

        if (item is not null && !visual.IsSet(VisualElement.DataContextProperty))
        {
            visual.DataContext = item;
        }

        return visual;
    }

    /// <inheritdoc />
    protected override void RenderContent(Graphics graphics)
    {
        if (!IsTemplated)
        {
            base.RenderContent(graphics);
            return;
        }

        // 模板只负责分区；只有宿主显式设置了底色/边框才补一层，默认表格不带表面。
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(
            0,
            0,
            Math.Max(0, (int)Math.Ceiling(Bounds.Width)),
            Math.Max(0, (int)Math.Ceiling(Bounds.Height)));
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (Background is not null || (BorderBrush is not null && !BorderThickness.IsEmpty))
        {
            XamlDrawing.PaintChromeWithBrushes(graphics, bounds, Background, BorderBrush, BorderThickness, CornerRadius);
        }

        OnRender(graphics);
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var theme = Theme.Current;
        var bounds = new Rectangle(
            0,
            0,
            Math.Max(1, (int)Math.Ceiling(Bounds.Width)),
            Math.Max(1, (int)Math.Ceiling(Bounds.Height)));
        var radius = CornerRadius > 0 ? CornerRadius : theme.CornerRadius;
        var left = Math.Min(_headerLeft, _bodyLeft);
        var right = Math.Max(_headerRight, _bodyRight);
        var inner = new RectangleF(
            left,
            _headerTop,
            Math.Max(0f, right - left),
            Math.Max(0f, _bodyBottom - _headerTop));

        var foreground = Foreground.IsEmpty ? theme.Foreground : Foreground;
        var font = ResolveFont(DefaultFont);
        var headerFont = GetHeaderFont(font);

        if (inner.Width <= 0f || inner.Height <= 0f)
        {
            return;
        }

        var previous = graphics.Save();
        using (var clip = XamlDrawing.RoundedRectangle(
                   new RectangleF(inner.X - 0.5f, inner.Y - 0.5f, inner.Width, inner.Height),
                   Math.Max(0f, radius - 1f)))
        {
            graphics.SetClip(clip, CombineMode.Intersect);
        }

        DrawHeader(graphics, headerFont, theme, foreground);

        // 行必须裁到表头下方：滚动偏移不是行高整数倍时，首行会越过 _bodyTop，画到表头上面。
        var bodyState = graphics.Save();
        graphics.SetClip(
            new RectangleF(
                _bodyLeft,
                _bodyTop,
                Math.Max(0f, _bodyRight - _bodyLeft),
                Math.Max(0f, _bodyBottom - _bodyTop)),
            CombineMode.Intersect);
        DrawRows(graphics, font, theme, foreground);
        DrawDropIndicator(graphics, theme);
        graphics.Restore(bodyState);

        graphics.Restore(previous);

        if (IsFocusVisible)
        {
            using var focusPath = XamlDrawing.RoundedRectangle(
                new RectangleF(1.5f, 1.5f, Math.Max(0f, bounds.Width - 3f), Math.Max(0f, bounds.Height - 3f)),
                Math.Max(0f, radius - 1f));
            using var focusPen = new System.Drawing.Pen(theme.FocusRing, theme.FocusThickness);
            graphics.DrawPath(focusPen, focusPath);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (_syncingSelection)
        {
            return;
        }

        if (ReferenceEquals(e.Property, ItemsSourceProperty))
        {
            BindSource();
            return;
        }

        if (ReferenceEquals(e.Property, RowHeightProperty)
            || ReferenceEquals(e.Property, HeaderHeightProperty)
            || ReferenceEquals(e.Property, RowNumberWidthProperty)
            || ReferenceEquals(e.Property, ShowRowNumbersProperty))
        {
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        if (ReferenceEquals(e.Property, SelectedIndexProperty))
        {
            SyncSelectedItemFromIndex();
            EnsureSelectedVisible();
            InvalidateVisual();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (ReferenceEquals(e.Property, SelectedItemProperty))
        {
            SyncSelectedIndexFromItem();
            EnsureSelectedVisible();
            InvalidateVisual();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        if (!IsEnabled || args.Button != MouseButtons.Left)
        {
            return;
        }

        var point = args.GetPosition(this);
        Focus();

        if (point.Y < _bodyTop)
        {
            var edge = ColumnEdgeAt(point);
            if (edge >= 0)
            {
                _resizeColumn = _columns[edge];
                _resizeStartX = point.X;
                _resizeStartWidth = _widths.Length > edge ? _widths[edge] : (float)Math.Max(0d, _columns[edge].Width);
                Cursor = Cursors.SizeWE;
                args.Handled = true;
                return;
            }

            var column = GetColumnIndexAt(point);
            if (column >= 0)
            {
                SortBy(column);
            }

            args.Handled = true;
            return;
        }

        var row = GetRowIndexAt(point);
        if (row >= 0)
        {
            SelectedIndex = row;
            _dragRow = row;
            _dragOrigin = point;
            _dragArmed = true;
            args.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(VisualPointerEventArgs args)
    {
        var point = args.GetPosition(this);
        if (_resizeColumn is { } resize && (args.Buttons & MouseButtons.Left) != 0)
        {
            var width = Math.Max(
                (float)resize.MinWidth,
                Math.Min((float)resize.MaxWidth, _resizeStartWidth + (point.X - _resizeStartX)));
            resize.Width = Math.Round(width);
            InvalidateVisual();
            args.Handled = true;
            return;
        }

        if (_dragArmed && _dragRow >= 0 && (args.Buttons & MouseButtons.Left) != 0)
        {
            var dx = Math.Abs(point.X - _dragOrigin.X);
            var dy = Math.Abs(point.Y - _dragOrigin.Y);
            if (dx >= 4f || dy >= 4f)
            {
                BeginRowDrag();
            }

            args.Handled = true;
            return;
        }

        var hover = GetRowIndexAt(point);
        if (hover != _hoverRow)
        {
            _hoverRow = hover;
            InvalidateVisual();
        }

        var header = point.Y < _bodyTop ? GetColumnIndexAt(point) : -1;
        var edge = point.Y < _bodyTop ? ColumnEdgeAt(point) : -1;
        if (header != _hoverHeader || edge >= 0)
        {
            _hoverHeader = header;
            InvalidateVisual();
        }

        Cursor = edge >= 0 ? Cursors.SizeWE : null;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(VisualPointerEventArgs args)
    {
        _dragArmed = false;
        _dragRow = -1;
        _resizeColumn = null;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(VisualPointerEventArgs args)
    {
        base.OnPointerExited(args);
        if (_hoverRow != -1 || _hoverHeader != -1)
        {
            _hoverRow = -1;
            _hoverHeader = -1;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerWheel(VisualPointerEventArgs args)
    {
        if (_view.Count == 0)
        {
            return;
        }

        var rowHeight = (float)Math.Max(1d, RowHeight);
        var max = Math.Max(0f, (_view.Count * rowHeight) - _viewportHeight);
        if (max <= 0f)
        {
            return;
        }

        _verticalOffset = XamlMath.Clamp(
            _verticalOffset - ((args.Delta / 120f) * rowHeight * 3f),
            0f,
            max);
        SyncScrollBar();
        InvalidateArrange();
        InvalidateVisual();
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(VisualKeyEventArgs args)
    {
        if (!IsEnabled || _view.Count == 0)
        {
            return;
        }

        var index = SelectedIndex;
        var page = Math.Max(1, VisibleRowCount - 1);
        switch (args.KeyCode)
        {
            case Keys.Up:
                index = index <= 0 ? 0 : index - 1;
                break;
            case Keys.Down:
                index = index < 0 ? 0 : index + 1;
                break;
            case Keys.PageUp:
                index = Math.Max(0, index - page);
                break;
            case Keys.PageDown:
                index = Math.Min(_view.Count - 1, index + page);
                break;
            case Keys.Home:
                index = 0;
                break;
            case Keys.End:
                index = _view.Count - 1;
                break;
            case Keys.C:
                if ((args.Modifiers & Keys.Control) != 0)
                {
                    CopySelection();
                    args.Handled = true;
                }

                return;
            default:
                return;
        }

        SelectedIndex = XamlMath.Clamp(index, 0, _view.Count - 1);
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnDragEnter(VisualDragEventArgs args)
    {
        UpdateDropTarget(args, args.GetPosition(this));
    }

    /// <inheritdoc />
    protected override void OnDragOver(VisualDragEventArgs args)
    {
        UpdateDropTarget(args, args.GetPosition(this));
    }

    /// <inheritdoc />
    protected override void OnDragLeave(VisualDragEventArgs args)
    {
        if (_dropIndex != -1)
        {
            _dropIndex = -1;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnDrop(VisualDragEventArgs args)
    {
        if (!IsEnabled)
        {
            args.Effects = DragDropEffects.None;
            return;
        }

        var point = args.GetPosition(this);
        var insert = InsertionIndexAt(point);
        var files = VisualDragDrop.GetFiles(args.Data);
        var text = VisualDragDrop.GetText(args.Data);
        VisualDragDrop.TryGetData(args.Data, VisualDragDrop.DataGridRowsFormat, out DataGridDragPayload? payload);

        var effect = ResolveDropEffect(args.Data, args.AllowedEffects, payload);
        var dropArgs = new DataGridDropEventArgs(args.Data, files, text, insert, effect, payload)
        {
            IsExternal = payload is null || !ReferenceEquals(payload.Source, this),
        };

        if (payload is not null)
        {
            effect = ProcessGridPayload(payload, insert, effect);
            dropArgs.Effects = effect;
        }

        _dropIndex = -1;
        InvalidateVisual();
        Dropped?.Invoke(this, dropArgs);
        if (dropArgs.Effects != DragDropEffects.None)
        {
            effect = dropArgs.Effects;
        }

        args.Effects = effect;
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _columns.CollectionChanged -= OnColumnsChanged;
            UnobserveColumns();
            if (_observedSource is not null)
            {
                _observedSource.CollectionChanged -= OnSourceCollectionChanged;
                _observedSource = null;
            }

            _scrollBar.ValueChanged -= OnScrollBarValueChanged;
            _headerFont?.Dispose();
            _headerFont = null;
        }

        base.Dispose(disposing);
    }

    private void BindSource()
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceCollectionChanged;
            _observedSource = null;
        }

        _rows.Clear();
        if (ItemsSource is { } source)
        {
            foreach (var item in source)
            {
                _rows.Add(item);
            }

            if (source is INotifyCollectionChanged notifier)
            {
                _observedSource = notifier;
                notifier.CollectionChanged += OnSourceCollectionChanged;
            }
        }

        RebuildView();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var selected = SelectedItem;
        _rows.Clear();
        if (ItemsSource is { } source)
        {
            foreach (var item in source)
            {
                _rows.Add(item);
            }
        }

        RebuildView(selected);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void RebuildView(object? keepSelected = null)
    {
        keepSelected ??= SelectedItem;
        _view.Clear();
        for (var index = 0; index < _rows.Count; index++)
        {
            _view.Add(index);
        }

        if (_sortColumn >= 0 && _sortColumn < _columns.Count)
        {
            var column = _columns[_sortColumn];
            var direction = _sortDirection;
            _view.Sort((left, right) =>
            {
                var result = CompareValues(column.GetValue(_rows[left]), column.GetValue(_rows[right]));
                return direction == ListSortDirection.Descending ? -result : result;
            });
        }

        if (_view.Count == 0)
        {
            SetSelectionSilently(-1, null);
        }
        else if (keepSelected is not null)
        {
            var index = -1;
            for (var viewIndex = 0; viewIndex < _view.Count; viewIndex++)
            {
                if (Equals(_rows[_view[viewIndex]], keepSelected))
                {
                    index = viewIndex;
                    break;
                }
            }

            SetSelectionSilently(index, index >= 0 ? keepSelected : null);
        }
        else if (SelectedIndex >= _view.Count)
        {
            SetSelectionSilently(-1, null);
        }

        // 排序或换源后同一格对应的行对象已经不同，元素一律回池重绑。
        _cellHost.Reset();
        SyncScrollBar();
    }

    private static int CompareValues(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return 1;
        }

        if (right is null)
        {
            return -1;
        }

        if (left is IComparable comparable)
        {
            try
            {
                return comparable.CompareTo(right);
            }
            catch (ArgumentException)
            {
                // 不同类型退回文本序，排序仍要给出稳定结果。
            }
        }

        return string.Compare(
            Convert.ToString(left, CultureInfo.CurrentCulture),
            Convert.ToString(right, CultureInfo.CurrentCulture),
            StringComparison.CurrentCulture);
    }

    private void SetSelectionSilently(int index, object? item)
    {
        _syncingSelection = true;
        try
        {
            SelectedIndex = index;
            SelectedItem = item;
        }
        finally
        {
            _syncingSelection = false;
        }

        InvalidateVisual();
    }

    private void SyncSelectedItemFromIndex()
    {
        _syncingSelection = true;
        try
        {
            var index = SelectedIndex;
            SelectedItem = index >= 0 && index < _view.Count ? _rows[_view[index]] : null;
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
                for (var viewIndex = 0; viewIndex < _view.Count; viewIndex++)
                {
                    if (Equals(_rows[_view[viewIndex]], item))
                    {
                        index = viewIndex;
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

    private void EnsureSelectedVisible()
    {
        var index = SelectedIndex;
        if (index < 0 || index >= _view.Count || _viewportHeight <= 0f)
        {
            return;
        }

        var rowHeight = (float)Math.Max(1d, RowHeight);
        var top = index * rowHeight;
        var bottom = top + rowHeight;
        var max = Math.Max(0f, (_view.Count * rowHeight) - _viewportHeight);
        if (top < _verticalOffset)
        {
            _verticalOffset = top;
        }
        else if (bottom > _verticalOffset + _viewportHeight)
        {
            _verticalOffset = bottom - _viewportHeight;
        }

        _verticalOffset = XamlMath.Clamp(_verticalOffset, 0f, max);
        SyncScrollBar();
        InvalidateArrange();
    }

    private void SyncScrollBar()
    {
        var rowHeight = (float)Math.Max(1d, RowHeight);
        var extent = _view.Count * rowHeight;
        var max = Math.Max(0f, extent - _viewportHeight);
        _verticalOffset = XamlMath.Clamp(_verticalOffset, 0f, max);

        _syncingScroll = true;
        try
        {
            _scrollBar.Minimum = 0d;
            _scrollBar.Maximum = extent;
            _scrollBar.ViewportSize = _viewportHeight;
            _scrollBar.SmallChange = rowHeight;
            _scrollBar.LargeChange = Math.Max(rowHeight, _viewportHeight);
            _scrollBar.Value = _verticalOffset;
        }
        finally
        {
            _syncingScroll = false;
        }
    }

    private void OnScrollBarValueChanged(object? sender, EventArgs e)
    {
        if (_syncingScroll)
        {
            return;
        }

        var rowHeight = (float)Math.Max(1d, RowHeight);
        var max = Math.Max(0f, (_view.Count * rowHeight) - _viewportHeight);
        _verticalOffset = XamlMath.Clamp((float)_scrollBar.Value, 0f, max);
        InvalidateArrange();
        InvalidateVisual();
    }

    private void UpdateScrollBar(bool visible)
    {
        SyncScrollBar();
        if (visible)
        {
            _scrollBar.Visibility = XamlVisibility.Visible;
        }
    }

    private float[] ResolveWidths(float available)
    {
        var widths = new float[_columns.Count];
        var used = 0f;
        var fill = 0;
        for (var index = 0; index < _columns.Count; index++)
        {
            var column = _columns[index];
            var width = column.Width;
            if (double.IsNaN(width) || width <= 0d)
            {
                fill++;
                continue;
            }

            var fixedWidth = (float)Math.Min(column.MaxWidth, Math.Max(column.MinWidth, width));
            widths[index] = fixedWidth;
            used += fixedWidth;
        }

        var gutter = ShowRowNumbers ? (float)Math.Max(0d, RowNumberWidth) : 0f;
        var remaining = Math.Max(0f, available - gutter - used);
        var share = fill > 0 ? remaining / fill : 0f;
        for (var index = 0; index < _columns.Count; index++)
        {
            var column = _columns[index];
            if (double.IsNaN(column.Width) || column.Width <= 0d)
            {
                widths[index] = (float)Math.Min(column.MaxWidth, Math.Max(column.MinWidth, share));
            }
        }

        return widths;
    }

    private int ColumnEdgeAt(PointF point)
    {
        if (point.Y >= _bodyTop || point.X < _bodyLeft || point.X > _bodyRight)
        {
            return -1;
        }

        var x = _bodyLeft + (ShowRowNumbers ? (float)RowNumberWidth : 0f);
        for (var index = 0; index < _widths.Length; index++)
        {
            x += _widths[index];
            if (Math.Abs(point.X - x) <= 4f)
            {
                return index;
            }
        }

        return -1;
    }

    private void DrawHeader(Graphics graphics, Font headerFont, ThemePalette theme, Color foreground)
    {
        var header = new RectangleF(
            _headerLeft,
            _headerTop,
            Math.Max(0f, _headerRight - _headerLeft),
            Math.Max(0f, _headerBottom - _headerTop));
        if (header.Width <= 0f || header.Height <= 0f)
        {
            return;
        }

        var headerBackground = ResolveColor(ColumnHeaderBackground, theme.SurfaceAlt);
        var headerForeground = ResolveColor(ColumnHeaderForeground, foreground);
        var padding = (float)Math.Max(0d, HeaderPadding);

        using (var fill = new SolidBrush(headerBackground))
        {
            graphics.FillRectangle(fill, header);
        }

        if (_hoverHeader >= 0 && _hoverHeader < _widths.Length)
        {
            var x = _bodyLeft + (ShowRowNumbers ? (float)RowNumberWidth : 0f);
            for (var index = 0; index < _hoverHeader; index++)
            {
                x += _widths[index];
            }

            using var hover = new SolidBrush(ResolveColor(ColumnHeaderHoverBackground, WithAlpha(foreground, 16)));
            graphics.FillRectangle(hover, new RectangleF(x, header.Y, _widths[_hoverHeader], header.Height));
        }

        var xPos = _bodyLeft;
        if (ShowRowNumbers)
        {
            VisualText.Draw(
                this,
                graphics,
                "#",
                headerFont,
                ResolveColor(RowNumberForeground, theme.MutedForeground),
                new RectangleF(xPos, header.Y, (float)RowNumberWidth, header.Height),
                wrap: false,
                StringAlignment.Center,
                StringAlignment.Center);
            xPos += (float)RowNumberWidth;
        }

        for (var index = 0; index < _columns.Count && index < _widths.Length; index++)
        {
            var column = _columns[index];
            var width = _widths[index];
            if (width <= 0f)
            {
                continue;
            }

            var cell = new RectangleF(xPos + padding, header.Y, Math.Max(0f, width - (padding * 2f)), header.Height);
            VisualText.Draw(
                this,
                graphics,
                column.Header,
                headerFont,
                headerForeground,
                cell,
                wrap: false,
                ToStringAlignment(column.TextAlignment),
                StringAlignment.Center);

            if (_sortColumn == index)
            {
                DrawSortChevron(
                    graphics,
                    ResolveColor(SortIndicatorBrush, theme.Accent),
                    xPos + width - 12f,
                    header.Y + (header.Height / 2f),
                    _sortDirection);
            }

            xPos += width;
        }

        using var line = new System.Drawing.Pen(ResolveColor(ColumnHeaderBorderBrush, theme.ControlBorder), 1f);
        graphics.DrawLine(line, header.X, header.Bottom - 0.5f, header.Right, header.Bottom - 0.5f);
    }

    private void DrawRows(Graphics graphics, Font font, ThemePalette theme, Color foreground)
    {
        var rowHeight = (float)Math.Max(1d, RowHeight);
        var bodyTop = _bodyTop;
        var bodyBottom = _bodyBottom;
        if (bodyBottom <= bodyTop)
        {
            return;
        }

        if (_view.Count == 0)
        {
            DrawEmptyState(graphics, theme, font, foreground);
            return;
        }

        var first = Math.Max(0, (int)Math.Floor(_verticalOffset / rowHeight));
        var last = Math.Min(_view.Count - 1, (int)Math.Ceiling((_verticalOffset + (bodyBottom - bodyTop)) / rowHeight));
        var gutter = ShowRowNumbers ? (float)Math.Max(0d, RowNumberWidth) : 0f;
        var rowForeground = foreground;

        for (var index = first; index <= last; index++)
        {
            var top = bodyTop + (index * rowHeight) - _verticalOffset;
            var row = new RectangleF(_bodyLeft, top, Math.Max(0f, _contentWidth), rowHeight);
            var selected = index == SelectedIndex;
            var hovered = index == _hoverRow;

            var baseRow = ResolveColor(RowBackground, Color.Transparent);
            if (baseRow.A > 0)
            {
                using var fill = new SolidBrush(baseRow);
                graphics.FillRectangle(fill, row);
            }

            if (selected)
            {
                using var fill = new SolidBrush(ResolveColor(SelectedRowBackground, theme.Selection));
                graphics.FillRectangle(fill, row);
            }
            else if (hovered)
            {
                using var fill = new SolidBrush(ResolveColor(HoveredRowBackground, WithAlpha(foreground, 18)));
                graphics.FillRectangle(fill, row);
            }
            else if (AlternateRows && (index % 2) == 1)
            {
                using var fill = new SolidBrush(ResolveColor(AlternateRowBackground, WithAlpha(foreground, 7)));
                graphics.FillRectangle(fill, row);
            }

            if (ShowRowNumbers && gutter > 0f)
            {
                using (var gutterFill = new SolidBrush(ResolveColor(RowNumberBackground, WithAlpha(foreground, 10))))
                {
                    graphics.FillRectangle(gutterFill, new RectangleF(row.X, row.Y, gutter, row.Height));
                }

                VisualText.Draw(
                    this,
                    graphics,
                    (index + 1).ToString(CultureInfo.CurrentCulture),
                    font,
                    ResolveColor(RowNumberForeground, theme.MutedForeground),
                    new RectangleF(row.X, row.Y, gutter - 6f, row.Height),
                    wrap: false,
                    StringAlignment.Far,
                    StringAlignment.Center);
            }

            if (selected)
            {
                using var accent = new SolidBrush(ResolveColor(SelectedRowAccent, theme.Accent));
                graphics.FillRectangle(accent, new RectangleF(row.X, row.Y, 2f, row.Height));
            }

            var cellPadding = (float)Math.Max(0d, CellPadding);
            var x = row.X + gutter;
            for (var columnIndex = 0; columnIndex < _columns.Count && columnIndex < _widths.Length; columnIndex++)
            {
                var width = _widths[columnIndex];
                if (width <= 0f)
                {
                    continue;
                }

                var cell = new RectangleF(
                    x + cellPadding,
                    row.Y,
                    Math.Max(0f, width - (cellPadding * 2f)),
                    row.Height);
                if (columnIndex >= _cellVisual.Length || !_cellVisual[columnIndex])
                {
                    DrawCell(graphics, _columns[columnIndex], _rows[_view[index]], cell, font, rowForeground);
                }

                x += width;
            }

            if (ShowGridLines)
            {
                using var line = new System.Drawing.Pen(ResolveColor(GridLineBrush, WithAlpha(foreground, 20)), 1f);
                graphics.DrawLine(line, row.X, row.Bottom - 0.5f, row.Right, row.Bottom - 0.5f);
            }
        }

        if (ShowGridLines)
        {
            using var line = new System.Drawing.Pen(ResolveColor(GridLineBrush, WithAlpha(foreground, 18)), 1f);
            var x = _bodyLeft + gutter;
            for (var index = 0; index < _widths.Length; index++)
            {
                x += _widths[index];
                if (x < _bodyLeft + _contentWidth - 1f)
                {
                    graphics.DrawLine(line, x - 0.5f, _bodyTop, x - 0.5f, _bodyBottom);
                }
            }
        }
    }

    // 自绘路径只剩文本；Badge/Check/Progress 由 Kind 模板创建的元素单元负责。
    private void DrawCell(
        Graphics graphics,
        DataGridColumn column,
        object? item,
        RectangleF cell,
        Font font,
        Color foreground)
    {
        var text = column.GetText(item);
        var color = column.Foreground.IsEmpty ? ResolveColor(CellForeground, foreground) : column.Foreground;
        var previous = graphics.Save();
        graphics.SetClip(cell, CombineMode.Intersect);
        try
        {
            VisualText.Draw(
                this,
                graphics,
                text,
                font,
                color,
                cell,
                wrap: false,
                ToStringAlignment(column.TextAlignment),
                StringAlignment.Center);
        }
        finally
        {
            graphics.Restore(previous);
        }
    }

    private void DrawSortChevron(Graphics graphics, Color color, float x, float y, ListSortDirection direction)
    {
        using var brush = new SolidBrush(color);
        var points = direction == ListSortDirection.Ascending
            ? new[]
            {
                new PointF(x - 4f, y + 2f),
                new PointF(x + 4f, y + 2f),
                new PointF(x, y - 3f),
            }
            : new[]
            {
                new PointF(x - 4f, y - 2f),
                new PointF(x + 4f, y - 2f),
                new PointF(x, y + 3f),
            };
        graphics.FillPolygon(brush, points);
    }

    private void DrawEmptyState(Graphics graphics, ThemePalette theme, Font font, Color foreground)
    {
        var area = new RectangleF(_bodyLeft + 16f, _bodyTop + 12f, Math.Max(0f, _contentWidth - 32f), Math.Max(0f, _viewportHeight - 24f));
        if (area.Width <= 0f || area.Height <= 0f)
        {
            return;
        }

        var emptyForeground = ResolveColor(EmptyStateForeground, theme.MutedForeground);
        using (var path = XamlDrawing.RoundedRectangle(area, 8f))
        using (var pen = new System.Drawing.Pen(ResolveColor(EmptyStateBorderBrush, WithAlpha(foreground, 60)), 1f) { DashStyle = DashStyle.Dash })
        {
            graphics.DrawPath(pen, path);
        }

        var center = area.Y + (area.Height / 2f) - 10f;
        VisualText.Draw(
            this,
            graphics,
            EmptyText,
            font,
            emptyForeground,
            new RectangleF(area.X, center, area.Width, 20f),
            wrap: false,
            StringAlignment.Center,
            StringAlignment.Center);

        if (AllowDrop)
        {
            VisualText.Draw(
                this,
                graphics,
                "Drop files, text or rows here",
                font,
                WithAlpha(emptyForeground, 170),
                new RectangleF(area.X, center + 20f, area.Width, 20f),
                wrap: false,
                StringAlignment.Center,
                StringAlignment.Center);
        }
    }

    private void DrawDropIndicator(Graphics graphics, ThemePalette theme)
    {
        if (_dropIndex < 0 || _view.Count == 0)
        {
            return;
        }

        var rowHeight = (float)Math.Max(1d, RowHeight);
        var y = _bodyTop + (_dropIndex * rowHeight) - _verticalOffset;
        y = XamlMath.Clamp(y, _bodyTop, _bodyBottom);
        using var pen = new System.Drawing.Pen(theme.Accent, 2f);
        graphics.DrawLine(pen, _bodyLeft, y, _bodyLeft + _contentWidth, y);
        using var head = new SolidBrush(theme.Accent);
        graphics.FillEllipse(head, _bodyLeft - 1f, y - 3f, 7f, 7f);
    }

    private void BeginRowDrag()
    {
        if (_dragRow < 0 || _dragRow >= _view.Count)
        {
            return;
        }

        var sourceIndex = _view[_dragRow];
        var item = _rows[sourceIndex];
        var payload = new DataGridDragPayload(this, new[] { item }, sourceIndex);
        var data = new DataObject();
        data.SetData(VisualDragDrop.DataGridRowsFormat, payload);
        var text = BuildRowText(sourceIndex);
        if (!string.IsNullOrEmpty(text))
        {
            data.SetData(DataFormats.UnicodeText, text);
        }

        var textCell = _columns.FirstOrDefault(static column => column.Kind == DataGridColumnKind.Text);
        var label = textCell?.GetText(item);
        var ghost = string.IsNullOrEmpty(label)
            ? $"Row {_dragRow + 1}"
            : $"Row {_dragRow + 1} · {Truncate(label!, 28)}";
        var start = new DataGridDragStartingEventArgs(
            data,
            DragDropEffects.Move | DragDropEffects.Copy,
            ghost);
        DragStarting?.Invoke(this, start);

        _dragArmed = false;
        _dragRow = -1;
        _hoverRow = -1;
        InvalidateVisual();

        if (start.AllowedEffects == DragDropEffects.None)
        {
            return;
        }

        VisualDragDrop.StartDrag(this, data, start.AllowedEffects, start.GhostText);
    }

    private string BuildRowText(int sourceIndex)
    {
        var values = new List<string>(_columns.Count);
        foreach (var column in _columns)
        {
            values.Add(column.GetText(_rows[sourceIndex]));
        }

        return string.Join("\t", values);
    }

    private void CopySelection()
    {
        var index = SelectedIndex;
        if (index < 0 || index >= _view.Count)
        {
            return;
        }

        try
        {
            var text = BuildRowText(_view[index]);
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
            }
        }
        catch (ExternalException)
        {
            // 剪贴板可能被另一个进程占用；复制失败不应打断导航。
        }
    }

    private void UpdateDropTarget(VisualDragEventArgs args, PointF point)
    {
        if (!IsEnabled || !AcceptsDrop(args.Data))
        {
            args.Effects = DragDropEffects.None;
            if (_dropIndex != -1)
            {
                _dropIndex = -1;
                InvalidateVisual();
            }

            return;
        }

        var effect = ResolveDropEffect(args.Data, args.AllowedEffects, null);
        if (effect == DragDropEffects.None)
        {
            args.Effects = DragDropEffects.None;
            return;
        }

        var insert = InsertionIndexAt(point);
        if (insert != _dropIndex)
        {
            _dropIndex = insert;
            InvalidateVisual();
        }

        args.Effects = effect;
        args.Handled = true;
    }

    private bool AcceptsDrop(IDataObject data)
    {
        if (data is null)
        {
            return false;
        }

        return data.GetDataPresent(VisualDragDrop.DataGridRowsFormat)
            || data.GetDataPresent(DataFormats.FileDrop)
            || data.GetDataPresent(DataFormats.UnicodeText)
            || data.GetDataPresent(DataFormats.Text);
    }

    private DragDropEffects ResolveDropEffect(
        IDataObject data,
        DragDropEffects allowed,
        DataGridDragPayload? payload)
    {
        if (payload is null)
        {
            VisualDragDrop.TryGetData(data, VisualDragDrop.DataGridRowsFormat, out payload);
        }

        if (payload is not null)
        {
            if (ReferenceEquals(payload.Source, this) && (_sortColumn >= 0 || !AllowRowReorder))
            {
                return DragDropEffects.None;
            }

            return VisualDragDrop.ResolveRowEffect(allowed);
        }

        return (allowed & DragDropEffects.Copy) != 0
            ? DragDropEffects.Copy
            : (allowed & DragDropEffects.Link) != 0
                ? DragDropEffects.Link
                : DragDropEffects.None;
    }

    private int InsertionIndexAt(PointF point)
    {
        if (_view.Count == 0)
        {
            return 0;
        }

        if (point.Y <= _bodyTop)
        {
            return 0;
        }

        if (point.Y >= _bodyBottom)
        {
            return _view.Count;
        }

        var rowHeight = (float)Math.Max(1d, RowHeight);
        var relative = point.Y - _bodyTop + _verticalOffset;
        var index = (int)Math.Floor(relative / rowHeight);
        var fraction = relative - (index * rowHeight);
        if (fraction > rowHeight / 2f)
        {
            index++;
        }

        return XamlMath.Clamp(index, 0, _view.Count);
    }

    private DragDropEffects ProcessGridPayload(DataGridDragPayload payload, int insertViewIndex, DragDropEffects effect)
    {
        if (ReferenceEquals(payload.Source, this))
        {
            if (!AllowRowReorder || _sortColumn >= 0 || effect != DragDropEffects.Move)
            {
                return DragDropEffects.None;
            }

            if (ItemsSource is not IList list || list.IsReadOnly || list.IsFixedSize || payload.SourceIndex < 0)
            {
                return DragDropEffects.None;
            }

            var from = payload.SourceIndex;
            var to = insertViewIndex >= _view.Count ? list.Count : _view[insertViewIndex];
            if (from < to)
            {
                to--;
            }

            to = XamlMath.Clamp(to, 0, Math.Max(0, list.Count - 1));
            if (from == to)
            {
                return DragDropEffects.None;
            }

            var item = list[from];
            list.RemoveAt(from);
            list.Insert(to, item);
            SelectedItem = item;
            return DragDropEffects.Move;
        }

        if (ItemsSource is not IList target || target.IsReadOnly || target.IsFixedSize)
        {
            return effect;
        }

        var insert = _sortColumn >= 0 || insertViewIndex >= _view.Count
            ? target.Count
            : _view[insertViewIndex];
        insert = XamlMath.Clamp(insert, 0, target.Count);
        var moved = 0;
        foreach (var item in payload.Items)
        {
            try
            {
                target.Insert(insert + moved, item);
                moved++;
            }
            catch (ArgumentException)
            {
                // 目标集合的元素类型不兼容；交给 Dropped 处理器处理。
                break;
            }
        }

        if (moved == 0)
        {
            return effect;
        }

        if (effect == DragDropEffects.Move
            && payload.Source.ItemsSource is IList source
            && !ReferenceEquals(source, target)
            && !source.IsReadOnly
            && !source.IsFixedSize)
        {
            foreach (var item in payload.Items)
            {
                source.Remove(item);
            }
        }

        return effect;
    }

    private Font GetHeaderFont(Font baseFont)
    {
        var style = ColumnHeaderFontStyle;
        if (_headerFont is not null && ReferenceEquals(_headerFontBase, baseFont) && _headerFontStyle == style)
        {
            return _headerFont;
        }

        _headerFont?.Dispose();
        _headerFontBase = baseFont;
        _headerFontStyle = style;
        _headerFont = new Font(baseFont, style);
        return _headerFont;
    }

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is DataGridColumn column && _observedColumns.Remove(column))
                {
                    column.PropertyChanged -= OnColumnPropertyChanged;
                }
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is DataGridColumn column)
                {
                    ObserveColumn(column);
                }
            }
        }

        if (_columns.Count == 0)
        {
            _sortColumn = -1;
        }
        else if (_sortColumn >= _columns.Count)
        {
            _sortColumn = _columns.Count - 1;
        }

        _cellHost.Reset();
        _cellHost.ClearPools();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void ObserveColumn(DataGridColumn column)
    {
        if (_observedColumns.Add(column))
        {
            column.PropertyChanged += OnColumnPropertyChanged;
        }
    }

    private void UnobserveColumns()
    {
        foreach (var column in _observedColumns)
        {
            column.PropertyChanged -= OnColumnPropertyChanged;
        }

        _observedColumns.Clear();
    }

    private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DataGridColumn.BindingPath)
            || e.PropertyName == nameof(DataGridColumn.IsSortable))
        {
            RebuildView();
        }
        else if (e.PropertyName == nameof(DataGridColumn.Width)
                 || e.PropertyName == nameof(DataGridColumn.MinWidth)
                 || e.PropertyName == nameof(DataGridColumn.MaxWidth))
        {
            InvalidateArrange();
            InvalidateVisual();
        }
        else if (e.PropertyName == nameof(DataGridColumn.CellTemplate)
                 || e.PropertyName == nameof(DataGridColumn.CellFactory)
                 || e.PropertyName == nameof(DataGridColumn.Kind))
        {
            _cellHost.Reset();
            _cellHost.ClearPools();
            InvalidateMeasure();
            InvalidateVisual();
        }
        else
        {
            InvalidateVisual();
        }
    }

    internal static StringAlignment ToStringAlignment(XamlTextAlignment alignment)
        => alignment switch
        {
            XamlTextAlignment.Right => StringAlignment.Far,
            XamlTextAlignment.Center => StringAlignment.Center,
            _ => StringAlignment.Near,
        };

    internal static Color WithAlpha(Color color, int alpha)
        => color.A == alpha ? color : Color.FromArgb(XamlMath.Clamp(alpha, 0, 255), color.R, color.G, color.B);

    private static DependencyProperty RegisterBrush(string name)
        => DependencyProperty.Register(
            name, typeof(XamlBrush), typeof(DataGrid),
            new PropertyMetadata(null) { AffectsRender = true });

    private static DependencyProperty RegisterDouble(string name, double defaultValue)
        => DependencyProperty.Register(
            name, typeof(double), typeof(DataGrid),
            new PropertyMetadata(defaultValue) { AffectsRender = true });

    // 自绘路径只认单色；渐变笔刷取代表色，和 Foreground 的取值方式一致。
    internal static Color ResolveColor(XamlBrush? brush, Color fallback)
    {
        if (brush is null)
        {
            return fallback;
        }

        var color = brush.ApproximateColor;
        return color.IsEmpty ? fallback : color;
    }

    internal static Color ContrastText(Color background)
    {
        var luminance = ((0.2126 * background.R) + (0.7152 * background.G) + (0.0722 * background.B)) / 255d;
        return luminance > 0.58d
            ? Color.FromArgb(230, 16, 18, 22)
            : Color.FromArgb(240, 255, 255, 255);
    }

    private static string Truncate(string text, int length)
        => text.Length <= length ? text : text.Substring(0, length - 1) + "…";
}

/// <summary>The base of the element-based cells realized by the DataGrid kind templates.</summary>
/// <remarks>
/// A built-in template such as <c>DataGrid.BadgeCell</c> creates one of these for each visible cell. The grid
/// attaches the owning grid and the column before arranging it, and the row item arrives as the
/// <see cref="VisualElement.DataContext"/>. A cell paints inside its own bounds and reads every colour and metric
/// from the owning grid's public style properties.
/// </remarks>
public abstract class DataGridCell : VisualElement
{
    /// <summary>Creates a decorative cell; the grid supplies its geometry, column and item.</summary>
    protected DataGridCell()
    {
        IsHitTestVisible = false;
        IsTabStop = false;
        Focusable = false;
    }

    /// <summary>The grid that realized this cell, or <see langword="null"/> outside a grid.</summary>
    public DataGrid? Owner { get; internal set; }

    /// <summary>The column this cell presents, or <see langword="null"/> outside a grid.</summary>
    public DataGridColumn? Column { get; internal set; }

    /// <summary>The row item this cell presents.</summary>
    protected object? Item => DataContext;

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        if (Owner is not { } owner || Column is not { } column)
        {
            return;
        }

        var padding = (float)Math.Max(0d, owner.CellPadding);
        var cell = new RectangleF(
            padding,
            0f,
            Math.Max(0f, Bounds.Width - (padding * 2f)),
            Bounds.Height);
        if (cell.Width <= 0f || cell.Height <= 0f)
        {
            return;
        }

        var previous = graphics.Save();
        graphics.SetClip(cell, CombineMode.Intersect);
        try
        {
            Paint(graphics, owner, column, cell);
        }
        finally
        {
            graphics.Restore(previous);
        }
    }

    /// <summary>Paints the cell inside <paramref name="cell"/>, which already excludes <see cref="DataGrid.CellPadding"/>.</summary>
    /// <param name="graphics">The target surface in the cell's local coordinates.</param>
    /// <param name="owner">The grid that supplies colours and metrics.</param>
    /// <param name="column">The column that supplies the value, text and optional per-row colour.</param>
    /// <param name="cell">The local rectangle available to the appearance.</param>
    protected abstract void Paint(Graphics graphics, DataGrid owner, DataGridColumn column, RectangleF cell);
}

/// <summary>A status pill painted from <see cref="DataGridColumnKind.Badge"/>.</summary>
public sealed class DataGridBadgeCell : DataGridCell
{
    /// <summary>Creates an empty badge cell; the grid attaches its column and item.</summary>
    public DataGridBadgeCell()
    {
    }

    /// <inheritdoc />
    protected override void Paint(Graphics graphics, DataGrid owner, DataGridColumn column, RectangleF cell)
    {
        var text = column.GetText(Item);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var theme = Theme.Current;
        var color = column.GetColor(Item);
        if (color.IsEmpty)
        {
            color = DataGrid.ResolveColor(owner.BadgeBackground, theme.Accent);
        }

        var padding = (float)Math.Max(0d, owner.BadgePadding);
        var font = owner.ResolveFont(DefaultFont);
        var measured = VisualText.Measure(text, font, 0f, wrap: false, RenderDpi);
        var width = Math.Min(cell.Width, measured.Width + (padding * 2f));
        var height = Math.Min((float)Math.Max(0d, owner.BadgeHeight), cell.Height - 4f);
        if (width <= 0f || height <= 0f)
        {
            return;
        }

        var corner = double.IsNaN(owner.BadgeCornerRadius)
            ? height / 2f
            : (float)Math.Max(0d, owner.BadgeCornerRadius);
        var x = column.TextAlignment switch
        {
            XamlTextAlignment.Right => cell.Right - width,
            XamlTextAlignment.Center => cell.X + ((cell.Width - width) / 2f),
            _ => cell.X,
        };
        var y = cell.Y + ((cell.Height - height) / 2f);
        using var path = XamlDrawing.RoundedRectangle(new RectangleF(x, y, width, height), corner);
        using var fill = new SolidBrush(color);
        graphics.FillPath(fill, path);
        VisualText.Draw(
            owner,
            graphics,
            text,
            font,
            DataGrid.ResolveColor(owner.BadgeForeground, DataGrid.ContrastText(color)),
            new RectangleF(x + padding, y, Math.Max(0f, width - (padding * 2f)), height),
            wrap: false,
            StringAlignment.Center,
            StringAlignment.Center);
    }
}

/// <summary>A boolean box painted from <see cref="DataGridColumnKind.Check"/>.</summary>
public sealed class DataGridCheckCell : DataGridCell
{
    /// <summary>Creates an empty check cell; the grid attaches its column and item.</summary>
    public DataGridCheckCell()
    {
    }

    /// <inheritdoc />
    protected override void Paint(Graphics graphics, DataGrid owner, DataGridColumn column, RectangleF cell)
    {
        var value = column.GetValue(Item);
        bool isChecked;
        try
        {
            isChecked = value switch
            {
                bool flag => flag,
                null => false,
                _ => Convert.ToBoolean(value, CultureInfo.CurrentCulture),
            };
        }
        catch (FormatException)
        {
            isChecked = false;
        }
        catch (InvalidCastException)
        {
            isChecked = false;
        }

        var theme = Theme.Current;
        var size = (float)Math.Max(1d, owner.CheckSize);
        var rect = new RectangleF(
            column.TextAlignment switch
            {
                XamlTextAlignment.Right => cell.Right - size,
                XamlTextAlignment.Center => cell.X + ((cell.Width - size) / 2f),
                _ => cell.X,
            },
            cell.Y + ((cell.Height - size) / 2f),
            size,
            size);
        using var path = XamlDrawing.RoundedRectangle(rect, (float)Math.Max(0d, owner.CheckCornerRadius));
        if (isChecked)
        {
            using var fill = new SolidBrush(DataGrid.ResolveColor(owner.CheckFill, theme.Accent));
            graphics.FillPath(fill, path);
            using var check = new System.Drawing.Pen(
                DataGrid.ResolveColor(owner.CheckMarkBrush, theme.AccentForeground),
                1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            var scale = size / 16f;
            graphics.DrawLines(check, new[]
            {
                new PointF(rect.X + (4f * scale), rect.Y + (8f * scale)),
                new PointF(rect.X + (7f * scale), rect.Y + (11f * scale)),
                new PointF(rect.X + (12f * scale), rect.Y + (5f * scale)),
            });
        }
        else
        {
            using var stroke = new System.Drawing.Pen(
                DataGrid.ResolveColor(owner.CheckBorderBrush, theme.ControlBorder),
                1.2f);
            graphics.DrawPath(stroke, path);
        }
    }
}

/// <summary>A 0–100 progress track painted from <see cref="DataGridColumnKind.Progress"/>.</summary>
public sealed class DataGridProgressCell : DataGridCell
{
    /// <summary>Creates an empty progress cell; the grid attaches its column and item.</summary>
    public DataGridProgressCell()
    {
    }

    /// <inheritdoc />
    protected override void Paint(Graphics graphics, DataGrid owner, DataGridColumn column, RectangleF cell)
    {
        var theme = Theme.Current;
        var foreground = owner.Foreground.IsEmpty ? theme.Foreground : owner.Foreground;
        var value = column.GetProgress(Item);
        var color = column.GetColor(Item);
        if (color.IsEmpty)
        {
            color = DataGrid.ResolveColor(owner.ProgressFillBrush, theme.Accent);
        }

        var trackHeight = (float)Math.Max(1d, owner.ProgressTrackHeight);
        var corner = (float)Math.Max(0d, owner.ProgressCornerRadius);
        var labelWidth = cell.Width >= 76f ? 36f : 0f;
        var track = new RectangleF(
            cell.X,
            cell.Y + ((cell.Height - trackHeight) / 2f),
            Math.Max(0f, cell.Width - labelWidth - (labelWidth > 0f ? 8f : 0f)),
            trackHeight);
        if (track.Width > 0f)
        {
            using (var path = XamlDrawing.RoundedRectangle(track, corner))
            using (var fill = new SolidBrush(DataGrid.ResolveColor(owner.ProgressTrackBrush, DataGrid.WithAlpha(foreground, 30))))
            {
                graphics.FillPath(fill, path);
            }

            var fillWidth = Math.Max(0f, track.Width * (float)(value / 100d));
            if (fillWidth >= 2f)
            {
                using var path = XamlDrawing.RoundedRectangle(new RectangleF(track.X, track.Y, fillWidth, track.Height), corner);
                using var fill = new SolidBrush(color);
                graphics.FillPath(fill, path);
            }
        }

        if (labelWidth > 0f)
        {
            var text = column.GetText(Item);
            var label = string.IsNullOrEmpty(text) ? $"{value:0}%" : text;
            VisualText.Draw(
                owner,
                graphics,
                label,
                owner.ResolveFont(DefaultFont),
                DataGrid.ResolveColor(owner.ProgressLabelForeground, theme.MutedForeground),
                new RectangleF(cell.Right - labelWidth, cell.Y, labelWidth, cell.Height),
                wrap: false,
                StringAlignment.Far,
                StringAlignment.Center);
        }
    }
}

// 只实现可见单元格窗口的元素宿主：键是（可见行, 列），移出窗口的元素按列回池。
internal sealed class DataGridCellHost : Panel
{
    private readonly Dictionary<long, VisualElement> _realized = new();
    private readonly List<VisualElement> _order = new();
    private readonly Dictionary<int, Stack<VisualElement>> _pools = new();
    private readonly HashSet<long> _desired = new();
    private readonly List<long> _stale = new();

    internal DataGridCellHost()
    {
        IsHitTestVisible = false;
    }

    internal int RealizedCount => _realized.Count;

    internal IReadOnlyList<VisualElement> RealizedCells => _order;

    internal void Update(
        IReadOnlyList<int> view,
        IReadOnlyList<object?> rows,
        int firstRow,
        int lastRow,
        float rowHeight,
        float verticalOffset,
        float bodyTop,
        float bodyLeft,
        float gutter,
        IReadOnlyList<DataGridColumn> columns,
        float[] widths,
        bool[] cellVisual,
        DataGrid owner)
    {
        _desired.Clear();
        var y = bodyTop + (firstRow * rowHeight) - verticalOffset;
        for (var row = firstRow; row <= lastRow; row++)
        {
            var item = rows[view[row]];
            var x = bodyLeft + gutter;
            for (var column = 0; column < columns.Count && column < widths.Length; column++)
            {
                var width = widths[column];
                if (width <= 0f || column >= cellVisual.Length || !cellVisual[column])
                {
                    x += width;
                    continue;
                }

                var key = Key(row, column);
                _desired.Add(key);
                if (!_realized.TryGetValue(key, out var element))
                {
                    element = Take(column) ?? owner.CreateCellElement(columns[column], item);
                    if (element is null)
                    {
                        x += width;
                        continue;
                    }

                    Bind(element, item);
                    _realized[key] = element;
                    _order.Add(element);
                    Add(element);
                }
                else if (!ReferenceEquals(element.DataContext, item))
                {
                    Bind(element, item);
                }

                element.Measure(new Size((int)Math.Ceiling(width), (int)Math.Ceiling(rowHeight)));
                element.Arrange(new RectangleF(x, y, width, rowHeight));
                x += width;
            }

            y += rowHeight;
        }

        _stale.Clear();
        foreach (var key in _realized.Keys)
        {
            if (!_desired.Contains(key))
            {
                _stale.Add(key);
            }
        }

        foreach (var key in _stale)
        {
            Recycle(key);
        }
    }

    internal void Reset()
    {
        if (_realized.Count == 0)
        {
            return;
        }

        _stale.Clear();
        _stale.AddRange(_realized.Keys);
        foreach (var key in _stale)
        {
            Recycle(key);
        }

        _stale.Clear();
    }

    internal void ClearPools()
    {
        foreach (var pool in _pools.Values)
        {
            foreach (var element in pool)
            {
                element.Dispose();
            }
        }

        _pools.Clear();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Reset();
            ClearPools();
        }

        base.Dispose(disposing);
    }

    private static void Bind(VisualElement element, object? item)
    {
        if (item is not null)
        {
            element.DataContext = item;
        }
        else
        {
            element.ClearValue(VisualElement.DataContextProperty);
        }
    }

    private void Recycle(long key)
    {
        var element = _realized[key];
        _realized.Remove(key);
        _order.Remove(element);
        if (element.ParentElement is { } parent)
        {
            parent.RemoveChild(element);
        }

        var column = (int)(key & 0xFFFFFFFF);
        if (!_pools.TryGetValue(column, out var pool))
        {
            pool = new Stack<VisualElement>();
            _pools[column] = pool;
        }

        pool.Push(element);
    }

    private VisualElement? Take(int column)
        => _pools.TryGetValue(column, out var pool) && pool.Count > 0 ? pool.Pop() : null;

    private static long Key(int row, int column) => ((long)row << 32) | (uint)column;
}
