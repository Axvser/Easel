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
using XamlThickness = Easel.Controls.Thickness;
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

    private const float CellPadding = 10f;
    private const float HeaderPadding = 10f;
    private const float ScrollBarWidth = 12f;

    private readonly ObservableCollection<DataGridColumn> _columns = [];
    private readonly List<object?> _rows = [];
    private readonly List<int> _view = [];
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
    private float _contentWidth;
    private float[] _widths = [];
    private Font? _headerFont;
    private Font? _headerFontBase;

    /// <summary>Creates an empty table with the theme's surface, border and corner radius.</summary>
    public DataGrid()
    {
        Focusable = true;
        IsHitTestVisible = true;
        AllowDrop = true;
        Background = Theme.Current.Surface;
        BorderBrush = Theme.Current.ControlBorder;
        BorderThickness = new XamlThickness(1f);
        CornerRadius = Theme.Current.CornerRadius;

        _columns.CollectionChanged += OnColumnsChanged;
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

    /// <summary>The sorted column index, or -1 for source order.</summary>
    public int SortColumnIndex => _sortColumn;

    /// <summary>The current sort direction.</summary>
    public ListSortDirection SortDirection => _sortDirection;

    /// <summary>The number of rows in the current view.</summary>
    public int RowCount => _view.Count;

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
            return MeasureTemplate(availableSize);
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
            ArrangeTemplate(finalSize);
            _scrollBar.Visibility = XamlVisibility.Collapsed;
            return;
        }

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

        var rowHeight = (float)Math.Max(1d, RowHeight);
        var extent = _view.Count * rowHeight;
        var viewport = _viewportHeight;
        var needsBar = extent > viewport + 0.5f;
        _scrollBar.Visibility = needsBar ? XamlVisibility.Visible : XamlVisibility.Collapsed;

        var contentWidth = Math.Max(0f, right - _bodyLeft);
        if (needsBar)
        {
            contentWidth = Math.Max(0f, contentWidth - ScrollBarWidth);
        }

        _contentWidth = contentWidth;
        _widths = ResolveWidths(contentWidth);
        UpdateScrollBar(needsBar);

        if (needsBar)
        {
            var barWidth = (int)Math.Ceiling(ScrollBarWidth);
            _scrollBar.Measure(new Size(barWidth, (int)Math.Ceiling(viewport)));
            _scrollBar.Arrange(new RectangleF(
                _bodyRight - ScrollBarWidth,
                _bodyTop,
                ScrollBarWidth,
                viewport));
        }
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        if (IsTemplated)
        {
            return;
        }

        var theme = Theme.Current;
        var bounds = new Rectangle(
            0,
            0,
            Math.Max(1, (int)Math.Ceiling(Bounds.Width)),
            Math.Max(1, (int)Math.Ceiling(Bounds.Height)));
        var radius = CornerRadius > 0 ? CornerRadius : theme.CornerRadius;
        var inner = new RectangleF(
            _bodyLeft,
            _bodyTop - (float)Math.Min(Math.Max(1d, HeaderHeight), Math.Max(0f, _bodyBottom - _bodyTop)),
            Math.Max(0f, _bodyRight - _bodyLeft),
            Math.Max(0f, _bodyBottom - _bodyTop + (float)Math.Min(Math.Max(1d, HeaderHeight), Math.Max(0f, _bodyBottom - _bodyTop))));

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

        DrawHeader(graphics, inner, headerFont, theme, foreground);

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
        if (IsTemplated || !IsEnabled || args.Button != MouseButtons.Left)
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
        if (IsTemplated)
        {
            return;
        }

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
        if (IsTemplated || _view.Count == 0)
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
        InvalidateVisual();
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(VisualKeyEventArgs args)
    {
        if (IsTemplated || !IsEnabled || _view.Count == 0)
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

    private void DrawHeader(Graphics graphics, RectangleF inner, Font headerFont, ThemePalette theme, Color foreground)
    {
        var height = Math.Min(Math.Max(1d, HeaderHeight), inner.Height);
        var header = new RectangleF(inner.X, inner.Y, inner.Width, (float)height);
        using (var fill = new SolidBrush(theme.SurfaceAlt))
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

            using var hover = new SolidBrush(WithAlpha(theme.Foreground, 16));
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
                theme.MutedForeground,
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

            var cell = new RectangleF(xPos + HeaderPadding, header.Y, Math.Max(0f, width - (HeaderPadding * 2f)), header.Height);
            VisualText.Draw(
                this,
                graphics,
                column.Header,
                headerFont,
                foreground,
                cell,
                wrap: false,
                ToStringAlignment(column.TextAlignment),
                StringAlignment.Center);

            if (_sortColumn == index)
            {
                DrawSortChevron(graphics, theme, xPos + width - 12f, header.Y + (header.Height / 2f), _sortDirection);
            }

            xPos += width;
        }

        using var line = new System.Drawing.Pen(theme.ControlBorder, 1f);
        graphics.DrawLine(line, inner.X, header.Bottom - 0.5f, inner.Right, header.Bottom - 0.5f);
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
            DrawEmptyState(graphics, theme, font);
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

            if (selected)
            {
                using var fill = new SolidBrush(theme.Selection);
                graphics.FillRectangle(fill, row);
            }
            else if (hovered)
            {
                using var fill = new SolidBrush(WithAlpha(theme.Foreground, 18));
                graphics.FillRectangle(fill, row);
            }
            else if (AlternateRows && (index % 2) == 1)
            {
                using var fill = new SolidBrush(WithAlpha(theme.Foreground, 7));
                graphics.FillRectangle(fill, row);
            }

            if (ShowRowNumbers && gutter > 0f)
            {
                using (var gutterFill = new SolidBrush(WithAlpha(theme.Foreground, 10)))
                {
                    graphics.FillRectangle(gutterFill, new RectangleF(row.X, row.Y, gutter, row.Height));
                }

                VisualText.Draw(
                    this,
                    graphics,
                    (index + 1).ToString(CultureInfo.CurrentCulture),
                    font,
                    theme.MutedForeground,
                    new RectangleF(row.X, row.Y, gutter - 6f, row.Height),
                    wrap: false,
                    StringAlignment.Far,
                    StringAlignment.Center);
            }

            if (selected)
            {
                using var accent = new SolidBrush(theme.Accent);
                graphics.FillRectangle(accent, new RectangleF(row.X, row.Y, 2f, row.Height));
            }

            var x = row.X + gutter;
            for (var columnIndex = 0; columnIndex < _columns.Count && columnIndex < _widths.Length; columnIndex++)
            {
                var width = _widths[columnIndex];
                if (width <= 0f)
                {
                    continue;
                }

                var cell = new RectangleF(
                    x + CellPadding,
                    row.Y,
                    Math.Max(0f, width - (CellPadding * 2f)),
                    row.Height);
                DrawCell(graphics, _columns[columnIndex], _rows[_view[index]], cell, font, theme, rowForeground, selected);
                x += width;
            }

            if (ShowGridLines)
            {
                using var line = new System.Drawing.Pen(WithAlpha(theme.Foreground, 20), 1f);
                graphics.DrawLine(line, row.X, row.Bottom - 0.5f, row.Right, row.Bottom - 0.5f);
            }
        }

        if (ShowGridLines)
        {
            using var line = new System.Drawing.Pen(WithAlpha(theme.Foreground, 18), 1f);
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

    private void DrawCell(
        Graphics graphics,
        DataGridColumn column,
        object? item,
        RectangleF cell,
        Font font,
        ThemePalette theme,
        Color foreground,
        bool selected)
    {
        var text = column.GetText(item);
        var color = column.Foreground.IsEmpty ? foreground : column.Foreground;
        var previous = graphics.Save();
        graphics.SetClip(cell, CombineMode.Intersect);
        try
        {
            switch (column.Kind)
            {
                case DataGridColumnKind.Check:
                    DrawCheck(graphics, column, item, cell, theme);
                    break;
                case DataGridColumnKind.Badge:
                    DrawBadge(graphics, column, item, text, cell, font, theme);
                    break;
                case DataGridColumnKind.Progress:
                    DrawProgress(graphics, column, item, text, cell, font, theme);
                    break;
                default:
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
                    break;
            }
        }
        finally
        {
            graphics.Restore(previous);
        }
    }

    private void DrawBadge(
        Graphics graphics,
        DataGridColumn column,
        object? item,
        string text,
        RectangleF cell,
        Font font,
        ThemePalette theme)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var color = column.GetColor(item);
        if (color.IsEmpty)
        {
            color = theme.Accent;
        }

        var measured = VisualText.Measure(text, font, 0f, wrap: false, RenderDpi);
        var width = Math.Min(cell.Width, measured.Width + 18f);
        var height = Math.Min(20f, cell.Height - 4f);
        var x = column.TextAlignment switch
        {
            XamlTextAlignment.Right => cell.Right - width,
            XamlTextAlignment.Center => cell.X + ((cell.Width - width) / 2f),
            _ => cell.X,
        };
        var y = cell.Y + ((cell.Height - height) / 2f);
        using var path = XamlDrawing.RoundedRectangle(new RectangleF(x, y, width, height), height / 2f);
        using var fill = new SolidBrush(color);
        graphics.FillPath(fill, path);
        VisualText.Draw(
            this,
            graphics,
            text,
            font,
            ContrastText(color),
            new RectangleF(x + 6f, y, Math.Max(0f, width - 12f), height),
            wrap: false,
            StringAlignment.Center,
            StringAlignment.Center);
    }

    private void DrawProgress(
        Graphics graphics,
        DataGridColumn column,
        object? item,
        string text,
        RectangleF cell,
        Font font,
        ThemePalette theme)
    {
        var value = column.GetProgress(item);
        var color = column.GetColor(item);
        if (color.IsEmpty)
        {
            color = theme.Accent;
        }

        var labelWidth = cell.Width >= 76f ? 36f : 0f;
        var track = new RectangleF(
            cell.X,
            cell.Y + ((cell.Height - 6f) / 2f),
            Math.Max(0f, cell.Width - labelWidth - (labelWidth > 0f ? 8f : 0f)),
            6f);
        if (track.Width > 0f)
        {
            using (var path = XamlDrawing.RoundedRectangle(track, 3f))
            using (var fill = new SolidBrush(WithAlpha(theme.Foreground, 30)))
            {
                graphics.FillPath(fill, path);
            }

            var fillWidth = Math.Max(0f, track.Width * (float)(value / 100d));
            if (fillWidth >= 2f)
            {
                using var path = XamlDrawing.RoundedRectangle(new RectangleF(track.X, track.Y, fillWidth, track.Height), 3f);
                using var fill = new SolidBrush(color);
                graphics.FillPath(fill, path);
            }
        }

        if (labelWidth > 0f)
        {
            var label = string.IsNullOrEmpty(text) ? $"{value:0}%" : text;
            VisualText.Draw(
                this,
                graphics,
                label,
                font,
                theme.MutedForeground,
                new RectangleF(cell.Right - labelWidth, cell.Y, labelWidth, cell.Height),
                wrap: false,
                StringAlignment.Far,
                StringAlignment.Center);
        }
    }

    private void DrawCheck(Graphics graphics, DataGridColumn column, object? item, RectangleF cell, ThemePalette theme)
    {
        var value = column.GetValue(item);
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

        const float size = 16f;
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
        using var path = XamlDrawing.RoundedRectangle(rect, 4f);
        if (isChecked)
        {
            using var fill = new SolidBrush(theme.Accent);
            graphics.FillPath(fill, path);
            using var check = new System.Drawing.Pen(theme.AccentForeground, 1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            graphics.DrawLines(check, new[]
            {
                new PointF(rect.X + 4f, rect.Y + 8f),
                new PointF(rect.X + 7f, rect.Y + 11f),
                new PointF(rect.X + 12f, rect.Y + 5f),
            });
        }
        else
        {
            using var stroke = new System.Drawing.Pen(theme.ControlBorder, 1.2f);
            graphics.DrawPath(stroke, path);
        }
    }

    private void DrawSortChevron(Graphics graphics, ThemePalette theme, float x, float y, ListSortDirection direction)
    {
        using var brush = new SolidBrush(theme.Accent);
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

    private void DrawEmptyState(Graphics graphics, ThemePalette theme, Font font)
    {
        var area = new RectangleF(_bodyLeft + 16f, _bodyTop + 12f, Math.Max(0f, _contentWidth - 32f), Math.Max(0f, _viewportHeight - 24f));
        if (area.Width <= 0f || area.Height <= 0f)
        {
            return;
        }

        using (var path = XamlDrawing.RoundedRectangle(area, 8f))
        using (var pen = new System.Drawing.Pen(WithAlpha(theme.Foreground, 60), 1f) { DashStyle = DashStyle.Dash })
        {
            graphics.DrawPath(pen, path);
        }

        var center = area.Y + (area.Height / 2f) - 10f;
        VisualText.Draw(
            this,
            graphics,
            EmptyText,
            font,
            theme.MutedForeground,
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
                WithAlpha(theme.MutedForeground, 170),
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
        if (_headerFont is not null && ReferenceEquals(_headerFontBase, baseFont))
        {
            return _headerFont;
        }

        _headerFont?.Dispose();
        _headerFontBase = baseFont;
        _headerFont = new Font(baseFont, FontStyle.Bold);
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
            InvalidateVisual();
        }
        else
        {
            InvalidateVisual();
        }
    }

    private static StringAlignment ToStringAlignment(XamlTextAlignment alignment)
        => alignment switch
        {
            XamlTextAlignment.Right => StringAlignment.Far,
            XamlTextAlignment.Center => StringAlignment.Center,
            _ => StringAlignment.Near,
        };

    private static Color WithAlpha(Color color, int alpha)
        => color.A == alpha ? color : Color.FromArgb(XamlMath.Clamp(alpha, 0, 255), color.R, color.G, color.B);

    private static Color ContrastText(Color background)
    {
        var luminance = ((0.2126 * background.R) + (0.7152 * background.G) + (0.0722 * background.B)) / 255d;
        return luminance > 0.58d
            ? Color.FromArgb(230, 16, 18, 22)
            : Color.FromArgb(240, 255, 255, 255);
    }

    private static string Truncate(string text, int length)
        => text.Length <= length ? text : text.Substring(0, length - 1) + "…";
}
