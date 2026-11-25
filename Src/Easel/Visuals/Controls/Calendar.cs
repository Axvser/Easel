using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Easel;
using XamlDrawing = Easel.Controls.XamlDrawing;
using XamlBrush = Easel.Brush;
using XamlTextAlignment = Easel.Controls.TextAlignment;
using XamlThickness = Easel.Controls.Thickness;
using VisualButton = Easel.Visuals.Controls.Button;
using VisualTextBox = Easel.Visuals.Controls.TextBox;

namespace Easel.Visuals.Controls;

/// <summary>The month grid hosted by a templated <see cref="Calendar"/>.</summary>
/// <remarks>
/// <para>
/// The item reads and writes the owning calendar's state. With a template it presents the month through named parts:
/// <c>PART_Header</c> (a <see cref="TextBlock"/> for the month title), <c>PART_Previous</c> and <c>PART_Next</c>
/// (decorative navigation hit regions), <c>PART_WeekDays</c> (a one-row <see cref="UniformGrid"/> for the weekday
/// labels) and <c>PART_Days</c> (a six-row <see cref="UniformGrid"/> whose cells are created by this item).
/// </para>
/// <para>
/// Without a template it falls back to the original self-drawn month grid, so an explicit empty style still shows a
/// calendar. A template that omits the header or the two grids throws <see cref="InvalidOperationException"/>.
/// </para>
/// </remarks>
public class CalendarItem : TemplatedControl
{
    internal const float HeaderHeight = 34f;
    internal const float WeekHeaderHeight = 22f;
    internal const float CellHeight = 28f;
    internal const float CellWidth = 34f;
    internal const float GridWidth = CellWidth * 7f;
    internal const float GridHeight = CellHeight * 6f;
    internal const int DayCount = 42;

    /// <summary>The month-title and day-number colour as a brush value; an empty colour uses the active theme.</summary>
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(XamlBrush), typeof(CalendarItem),
        new PropertyMetadata(null) { AffectsRender = true });

    private readonly List<(RectangleF Bounds, DateTime Date)> _cells = [];
    private readonly CalendarDayCell[] _dayCells = new CalendarDayCell[DayCount];
    private readonly TextBlock[] _weekDayLabels = new TextBlock[7];
    private RectangleF _previous;
    private RectangleF _next;
    private DateTime? _hoverDate;
    private Calendar? _owner;
    private TextBlock? _headerPart;
    private VisualElement? _previousPart;
    private VisualElement? _nextPart;
    private UniformGrid? _weekPart;
    private UniformGrid? _daysPart;
    private bool _themeSubscribed;

    /// <summary>Creates a hit-testable month grid with theme padding.</summary>
    public CalendarItem()
    {
        IsHitTestVisible = true;
        Padding = new XamlThickness(6f);
        CornerRadius = Theme.Current.CornerRadius;
    }

    /// <summary>The month-title and day-number colour as a brush value; an empty colour uses the active theme.</summary>
    public Brush? Foreground
    {
        get => (XamlBrush?)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>The calendar whose month, selection and colours this item presents, or <see langword="null"/>.</summary>
    public Calendar? Owner
    {
        get => _owner;
        internal set
        {
            if (ReferenceEquals(_owner, value))
            {
                return;
            }

            _owner = value;
            RefreshFromOwner();
        }
    }

    // 指针下的日期；月格绘制与日格样式读取它。
    internal DateTime? HoverDate => _hoverDate;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
        => IsTemplated
            ? MeasureTemplate(availableSize)
            : new Size(
                (int)Math.Ceiling(GridWidth + Padding.Horizontal),
                (int)Math.Ceiling(HeaderHeight + WeekHeaderHeight + GridHeight + Padding.Vertical));

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        if (IsTemplated)
        {
            return;
        }

        if (Owner is not { } owner)
        {
            return;
        }

        var theme = Theme.Current;
        var font = ResolveFont(DefaultFont);
        var foreground = owner.Foreground.IsEmpty ? theme.Foreground : owner.Foreground;
        var left = Padding.Left;
        var top = Padding.Top;
        var gridWidth = CellWidth * 7f;

        _previous = new RectangleF(left + 4f, top + 5f, 24f, 24f);
        _next = new RectangleF(left + gridWidth - 28f, top + 5f, 24f, 24f);
        using (var chevron = new SolidBrush(theme.MutedForeground))
        {
            graphics.FillPolygon(chevron, new[]
            {
                new PointF(_previous.Right - 8f, _previous.Top + 6f),
                new PointF(_previous.Left + 6f, _previous.Top + 12f),
                new PointF(_previous.Right - 8f, _previous.Bottom - 6f),
            });
            graphics.FillPolygon(chevron, new[]
            {
                new PointF(_next.Left + 8f, _next.Top + 6f),
                new PointF(_next.Right - 6f, _next.Top + 12f),
                new PointF(_next.Left + 8f, _next.Bottom - 6f),
            });
        }

        var monthTitle = owner.DisplayMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        VisualText.Draw(this,
            graphics,
            monthTitle,
            font,
            foreground,
            new Rectangle((int)left, (int)top, (int)gridWidth, (int)HeaderHeight),
            wrap: false,
            StringAlignment.Center,
            StringAlignment.Center);

        var weekTop = top + HeaderHeight;
        var firstWeekDay = (int)owner.FirstDayOfWeek;
        for (var column = 0; column < 7; column++)
        {
            var day = (DayOfWeek)((firstWeekDay + column) % 7);
            var label = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(day);
            label = label.Length > 2 ? label.Substring(0, 2) : label;
            VisualText.Draw(this,
                graphics,
                label,
                font,
                theme.MutedForeground,
                new Rectangle(
                    (int)(left + (column * CellWidth)),
                    (int)weekTop,
                    (int)CellWidth,
                    (int)WeekHeaderHeight),
                wrap: false,
                StringAlignment.Center,
                StringAlignment.Center);
        }

        _cells.Clear();
        var firstOfMonth = owner.DisplayMonth;
        var offset = ((int)firstOfMonth.DayOfWeek - firstWeekDay + 7) % 7;
        var days = DateTime.DaysInMonth(firstOfMonth.Year, firstOfMonth.Month);
        var gridTop = weekTop + WeekHeaderHeight;
        for (var index = 0; index < 42; index++)
        {
            var cell = new RectangleF(
                left + (((index % 7) * CellWidth)),
                gridTop + ((index / 7) * CellHeight),
                CellWidth,
                CellHeight);
            var dayNumber = index - offset + 1;
            if (dayNumber < 1 || dayNumber > days)
            {
                continue;
            }

            var date = new DateTime(firstOfMonth.Year, firstOfMonth.Month, dayNumber);
            _cells.Add((cell, date));
            var selected = owner.SelectedDate is { } selectedDate && selectedDate.Date == date;
            var hovered = !selected && _hoverDate is { } hover && hover.Date == date;
            if (hovered || selected)
            {
                using var fill = new SolidBrush(selected ? theme.Accent : Color.FromArgb(36, theme.Accent));
                using var path = XamlDrawing.RoundedRectangle(
                    new RectangleF(cell.X + 3f, cell.Y + 2f, cell.Width - 6f, cell.Height - 4f),
                    4f);
                graphics.FillPath(fill, path);
            }

            VisualText.Draw(this,
                graphics,
                dayNumber.ToString(CultureInfo.CurrentCulture),
                font,
                selected ? theme.AccentForeground : foreground,
                new Rectangle((int)cell.X, (int)cell.Y, (int)cell.Width, (int)cell.Height),
                wrap: false,
                StringAlignment.Center,
                StringAlignment.Center);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(VisualPointerEventArgs args)
    {
        base.OnPointerMoved(args);
        SetHover(IsEnabled ? DateAt(args) : null);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(VisualPointerEventArgs args)
    {
        base.OnPointerExited(args);
        SetHover(null);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        base.OnPointerPressed(args);
        if (!IsEnabled || args.Button != MouseButtons.Left || Owner is not { } owner)
        {
            return;
        }

        if (IsTemplated)
        {
            if (Hits(_previousPart, args))
            {
                owner.DisplayMonth = owner.DisplayMonth.AddMonths(-1);
                args.Handled = true;
                return;
            }

            if (Hits(_nextPart, args))
            {
                owner.DisplayMonth = owner.DisplayMonth.AddMonths(1);
                args.Handled = true;
                return;
            }

            if (DateAt(args) is { } date)
            {
                owner.Focus();
                owner.SelectedDate = date;
                args.Handled = true;
            }

            return;
        }

        var point = args.GetPosition(this);
        if (_previous.Contains(point))
        {
            owner.DisplayMonth = owner.DisplayMonth.AddMonths(-1);
            args.Handled = true;
            return;
        }

        if (_next.Contains(point))
        {
            owner.DisplayMonth = owner.DisplayMonth.AddMonths(1);
            args.Handled = true;
            return;
        }

        foreach (var (bounds, date) in _cells)
        {
            if (bounds.Contains(point))
            {
                owner.Focus();
                owner.SelectedDate = date;
                args.Handled = true;
                return;
            }
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanging()
    {
        DetachTemplateParts();
        base.OnTemplateChanging();
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        base.OnTemplateChanged();
        if (!IsTemplated)
        {
            RefreshFromOwner();
            return;
        }

        ResolveTemplateParts();
        SubscribeTheme();
        RefreshFromOwner();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UnsubscribeTheme();
        }

        base.Dispose(disposing);
    }

    // 月、选中、周起始日或前景变化时由所属 Calendar 调用，模板部件随之对齐。
    internal void RefreshFromOwner()
    {
        if (_owner is { } owner)
        {
            if (_headerPart is { } header)
            {
                header.Text = owner.DisplayMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
            }

            if (IsTemplated && _weekPart is not null)
            {
                ApplyWeekDayLabels(owner);
            }

            if (IsTemplated && _daysPart is not null)
            {
                ApplyDayCells(owner);
            }

            SetValue(
                ForegroundProperty,
                owner.Foreground.IsEmpty ? null : new SolidColorBrush(owner.Foreground));
        }

        InvalidateVisual();
    }

    private void ResolveTemplateParts()
    {
        _headerPart = GetTemplateChild<TextBlock>("PART_Header");
        _previousPart = GetTemplateChild<VisualElement>("PART_Previous");
        _nextPart = GetTemplateChild<VisualElement>("PART_Next");
        _weekPart = GetTemplateChild<UniformGrid>("PART_WeekDays");
        _daysPart = GetTemplateChild<UniformGrid>("PART_Days");
        if (_headerPart is null || _weekPart is null || _daysPart is null)
        {
            throw new InvalidOperationException(
                "The CalendarItem ControlTemplate must declare 'PART_Header' (a TextBlock), 'PART_WeekDays' and "
                + "'PART_Days' (UniformGrid parts).");
        }

        PopulateWeekDays();
        PopulateDays();
    }

    private void PopulateWeekDays()
    {
        var part = _weekPart!;
        while (part.Children.Count > 0)
        {
            part.RemoveChild((VisualElement)part.Children[0]);
        }

        for (var column = 0; column < _weekDayLabels.Length; column++)
        {
            var label = new TextBlock { TextAlignment = XamlTextAlignment.Center };
            _weekDayLabels[column] = part.Add(label);
        }

        ApplyWeekDayColor();
    }

    private void PopulateDays()
    {
        var part = _daysPart!;
        while (part.Children.Count > 0)
        {
            part.RemoveChild((VisualElement)part.Children[0]);
        }

        for (var index = 0; index < _dayCells.Length; index++)
        {
            _dayCells[index] = part.Add(new CalendarDayCell { Item = this });
        }
    }

    private void ApplyWeekDayLabels(Calendar owner)
    {
        var firstWeekDay = (int)owner.FirstDayOfWeek;
        for (var column = 0; column < _weekDayLabels.Length; column++)
        {
            var day = (DayOfWeek)((firstWeekDay + column) % 7);
            var label = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(day);
            if (label.Length > 2)
            {
                label = label.Substring(0, 2);
            }

            _weekDayLabels[column].Text = label;
        }
    }

    private void ApplyDayCells(Calendar owner)
    {
        var firstOfMonth = owner.DisplayMonth;
        var offset = ((int)firstOfMonth.DayOfWeek - (int)owner.FirstDayOfWeek + 7) % 7;
        var days = DateTime.DaysInMonth(firstOfMonth.Year, firstOfMonth.Month);
        for (var index = 0; index < _dayCells.Length; index++)
        {
            var dayNumber = index - offset + 1;
            var cell = _dayCells[index];
            cell.Date = dayNumber >= 1 && dayNumber <= days
                ? new DateTime(firstOfMonth.Year, firstOfMonth.Month, dayNumber)
                : null;
            cell.IsHitTestVisible = cell.Date.HasValue;
        }

        _daysPart?.InvalidateVisual();
    }

    private void ApplyWeekDayColor()
    {
        foreach (var label in _weekDayLabels)
        {
            if (label is not null)
            {
                label.Foreground = Theme.Current.MutedForeground;
            }
        }
    }

    private void SubscribeTheme()
    {
        if (_themeSubscribed)
        {
            return;
        }

        Theme.Changed += OnThemeChanged;
        _themeSubscribed = true;
        ApplyWeekDayColor();
    }

    private void UnsubscribeTheme()
    {
        if (!_themeSubscribed)
        {
            return;
        }

        Theme.Changed -= OnThemeChanged;
        _themeSubscribed = false;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        ApplyWeekDayColor();
        InvalidateVisual();
    }

    private void DetachTemplateParts()
    {
        _headerPart = null;
        _previousPart = null;
        _nextPart = null;
        _weekPart = null;
        _daysPart = null;
        Array.Clear(_dayCells, 0, _dayCells.Length);
        Array.Clear(_weekDayLabels, 0, _weekDayLabels.Length);
        UnsubscribeTheme();
    }

    private DateTime? DateAt(VisualPointerEventArgs args)
    {
        if (!IsTemplated)
        {
            return DateAt(args.GetPosition(this));
        }

        if (_daysPart is not { } days || Owner is null)
        {
            return null;
        }

        var point = args.GetPosition(days);
        var width = days.Bounds.Width;
        var height = days.Bounds.Height;
        if (width <= 0f || height <= 0f)
        {
            return null;
        }

        var column = (int)Math.Floor(point.X / (width / 7f));
        var row = (int)Math.Floor(point.Y / (height / 6f));
        if (column < 0 || column >= 7 || row < 0 || row >= 6)
        {
            return null;
        }

        var index = (row * 7) + column;
        return index < _dayCells.Length ? _dayCells[index]?.Date : null;
    }

    private DateTime? DateAt(PointF point)
    {
        foreach (var (bounds, date) in _cells)
        {
            if (bounds.Contains(point))
            {
                return date;
            }
        }

        return null;
    }

    private static bool Hits(VisualElement? part, VisualPointerEventArgs args)
        => part is not null && part.AbsoluteBounds.Contains(args.Position);

    private void SetHover(DateTime? date)
    {
        if (_hoverDate == date)
        {
            return;
        }

        _hoverDate = date;
        InvalidateVisual();
    }
}

/// <summary>One day slot in a templated <see cref="CalendarItem"/> month grid.</summary>
/// <remarks>The cell paints the hover wash, the selected accent and the day number; an empty slot has no date and
/// passes pointer hits through to the grid.</remarks>
public sealed class CalendarDayCell : VisualElement
{
    /// <summary>Creates a hit-testable day cell.</summary>
    public CalendarDayCell()
    {
        IsHitTestVisible = true;
    }

    // 所属月格；日格从它读取日历状态。
    internal CalendarItem? Item { get; set; }

    // 该槽位显示的日期；开头/结尾的空槽为 null。
    internal DateTime? Date { get; set; }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        if (Item?.Owner is not { } owner || Date is not { } date)
        {
            return;
        }

        var theme = Theme.Current;
        var selected = owner.SelectedDate is { } selectedDate && selectedDate.Date == date;
        var hovered = !selected && Item.HoverDate is { } hover && hover.Date == date;
        var foreground = owner.Foreground.IsEmpty ? theme.Foreground : owner.Foreground;
        if (hovered || selected)
        {
            using var fill = new SolidBrush(selected ? theme.Accent : Color.FromArgb(36, theme.Accent));
            using var path = XamlDrawing.RoundedRectangle(
                new RectangleF(3f, 2f, Math.Max(0f, Bounds.Width - 6f), Math.Max(0f, Bounds.Height - 4f)),
                4f);
            graphics.FillPath(fill, path);
        }

        VisualText.Draw(
            this,
            graphics,
            date.Day.ToString(CultureInfo.CurrentCulture),
            ResolveFont(DefaultFont),
            selected ? theme.AccentForeground : foreground,
            new Rectangle(0, 0, Math.Max(0, (int)Bounds.Width), Math.Max(0, (int)Bounds.Height)),
            wrap: false,
            StringAlignment.Center,
            StringAlignment.Center);
    }
}

/// <summary>A month grid for picking a date, with optional control-template chrome.</summary>
/// <remarks>
/// <para>
/// Without a template the calendar draws a header with month navigation, a weekday row and up to six week rows.
/// Pointer and keyboard move the selection; <see cref="SelectedDate"/> can be <see langword="null"/> until the user
/// picks a day.
/// </para>
/// <para>
/// With a template the self-drawn grid is suppressed and a <see cref="CalendarItem"/> part named
/// <c>PART_CalendarItem</c> presents the month instead. A missing part throws
/// <see cref="InvalidOperationException"/>.
/// </para>
/// </remarks>
public class Calendar : TemplatedControl
{
    /// <summary>The selected day, or <see langword="null"/>.</summary>
    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(
        nameof(SelectedDate), typeof(DateTime?), typeof(Calendar),
        new PropertyMetadata(null) { AffectsRender = true });

    /// <summary>The first month shown in the grid.</summary>
    public static readonly DependencyProperty DisplayMonthProperty = DependencyProperty.Register(
        nameof(DisplayMonth), typeof(DateTime), typeof(Calendar),
        new PropertyMetadata(new DateTime(2000, 1, 1)) { AffectsRender = true });

    /// <summary>The first day of the week column.</summary>
    public static readonly DependencyProperty FirstDayOfWeekProperty = DependencyProperty.Register(
        nameof(FirstDayOfWeek), typeof(DayOfWeek), typeof(Calendar),
        new PropertyMetadata(DayOfWeek.Monday) { AffectsRender = true });

    /// <summary>The month title and day-number colour as a brush value; an empty colour uses the active theme.</summary>
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(XamlBrush), typeof(Calendar),
        new PropertyMetadata(null, null, static (_, value) => ColorBrushCoercion.Coerce(value))
        {
            AffectsRender = true,
        });

    private CalendarItem? _calendarItem;
    private DateTime? _hoverDate;

    /// <summary>Creates a focusable calendar showing the current month.</summary>
    public Calendar()
    {
        Focusable = true;
        IsHitTestVisible = true;
        Padding = new XamlThickness(6f);
        Background = Theme.Current.Surface;
        BorderBrush = Theme.Current.ControlBorder;
        BorderThickness = new XamlThickness(1f);
        CornerRadius = Theme.Current.CornerRadius;
        DisplayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    }

    /// <summary>Raised after <see cref="SelectedDate"/> changes.</summary>
    public event EventHandler? SelectedDateChanged;

    /// <summary>The selected day, or <see langword="null"/>.</summary>
    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    /// <summary>The month shown in the grid; only the year and month are used.</summary>
    public DateTime DisplayMonth
    {
        get => (DateTime)GetValue(DisplayMonthProperty)!;
        set => SetValue(DisplayMonthProperty, new DateTime(value.Year, value.Month, 1));
    }

    /// <summary>The first day of the week column.</summary>
    public DayOfWeek FirstDayOfWeek
    {
        get => (DayOfWeek)GetValue(FirstDayOfWeekProperty)!;
        set => SetValue(FirstDayOfWeekProperty, value);
    }

    /// <summary>The month title and day-number colour; an empty colour uses the active theme.</summary>
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

    /// <summary>The template's month-grid part, or <see langword="null"/> while the calendar draws itself.</summary>
    public CalendarItem? MonthGrid => _calendarItem;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsTemplated)
        {
            return MeasureTemplate(availableSize);
        }

        return new Size(
            (int)Math.Ceiling((CalendarItem.CellWidth * 7f) + Padding.Horizontal),
            (int)Math.Ceiling(
                CalendarItem.HeaderHeight + CalendarItem.WeekHeaderHeight
                + (CalendarItem.CellHeight * 6f) + Padding.Vertical));
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        if (IsTemplated)
        {
            return;
        }

        var theme = Theme.Current;
        var font = ResolveFont(DefaultFont);
        var foreground = Foreground.IsEmpty ? theme.Foreground : Foreground;
        var left = Padding.Left;
        var top = Padding.Top;
        var gridWidth = CalendarItem.CellWidth * 7f;

        var previous = new RectangleF(left + 4f, top + 5f, 24f, 24f);
        var next = new RectangleF(left + gridWidth - 28f, top + 5f, 24f, 24f);
        using (var chevron = new SolidBrush(theme.MutedForeground))
        {
            graphics.FillPolygon(chevron, new[]
            {
                new PointF(previous.Right - 8f, previous.Top + 6f),
                new PointF(previous.Left + 6f, previous.Top + 12f),
                new PointF(previous.Right - 8f, previous.Bottom - 6f),
            });
            graphics.FillPolygon(chevron, new[]
            {
                new PointF(next.Left + 8f, next.Top + 6f),
                new PointF(next.Right - 6f, next.Top + 12f),
                new PointF(next.Left + 8f, next.Bottom - 6f),
            });
        }

        var monthTitle = DisplayMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        VisualText.Draw(this,
            graphics,
            monthTitle,
            font,
            foreground,
            new Rectangle((int)left, (int)top, (int)gridWidth, (int)CalendarItem.HeaderHeight),
            wrap: false,
            StringAlignment.Center,
            StringAlignment.Center);

        var weekTop = top + CalendarItem.HeaderHeight;
        var firstWeekDay = (int)FirstDayOfWeek;
        for (var column = 0; column < 7; column++)
        {
            var day = (DayOfWeek)((firstWeekDay + column) % 7);
            var label = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(day);
            label = label.Length > 2 ? label.Substring(0, 2) : label;
            VisualText.Draw(this,
                graphics,
                label,
                font,
                theme.MutedForeground,
                new Rectangle(
                    (int)(left + (column * CalendarItem.CellWidth)),
                    (int)weekTop,
                    (int)CalendarItem.CellWidth,
                    (int)CalendarItem.WeekHeaderHeight),
                wrap: false,
                StringAlignment.Center,
                StringAlignment.Center);
        }

        var firstOfMonth = DisplayMonth;
        var offset = ((int)firstOfMonth.DayOfWeek - firstWeekDay + 7) % 7;
        var days = DateTime.DaysInMonth(firstOfMonth.Year, firstOfMonth.Month);
        var gridTop = weekTop + CalendarItem.WeekHeaderHeight;
        for (var index = 0; index < 42; index++)
        {
            var cell = new RectangleF(
                left + (((index % 7) * CalendarItem.CellWidth)),
                gridTop + ((index / 7) * CalendarItem.CellHeight),
                CalendarItem.CellWidth,
                CalendarItem.CellHeight);
            var dayNumber = index - offset + 1;
            if (dayNumber < 1 || dayNumber > days)
            {
                continue;
            }

            var date = new DateTime(firstOfMonth.Year, firstOfMonth.Month, dayNumber);
            var selected = SelectedDate is { } selectedDate && selectedDate.Date == date;
            var hovered = !selected && _hoverDate is { } hover && hover.Date == date;
            if (hovered || selected)
            {
                using var fill = new SolidBrush(selected ? theme.Accent : Color.FromArgb(36, theme.Accent));
                using var path = XamlDrawing.RoundedRectangle(
                    new RectangleF(cell.X + 3f, cell.Y + 2f, cell.Width - 6f, cell.Height - 4f),
                    4f);
                graphics.FillPath(fill, path);
            }

            VisualText.Draw(this,
                graphics,
                dayNumber.ToString(CultureInfo.CurrentCulture),
                font,
                selected ? theme.AccentForeground : foreground,
                new Rectangle((int)cell.X, (int)cell.Y, (int)cell.Width, (int)cell.Height),
                wrap: false,
                StringAlignment.Center,
                StringAlignment.Center);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(VisualPointerEventArgs args)
    {
        base.OnPointerMoved(args);
        if (IsTemplated)
        {
            return;
        }

        SetHover(IsEnabled ? DateAt(args.GetPosition(this)) : null);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(VisualPointerEventArgs args)
    {
        base.OnPointerExited(args);
        SetHover(null);
    }

    private DateTime? DateAt(PointF point)
    {
        var left = Padding.Left;
        var top = Padding.Top;
        var firstWeekDay = (int)FirstDayOfWeek;
        var firstOfMonth = DisplayMonth;
        var offset = ((int)firstOfMonth.DayOfWeek - firstWeekDay + 7) % 7;
        var days = DateTime.DaysInMonth(firstOfMonth.Year, firstOfMonth.Month);
        var gridTop = top + CalendarItem.HeaderHeight + CalendarItem.WeekHeaderHeight;
        for (var index = 0; index < 42; index++)
        {
            var cell = new RectangleF(
                left + (((index % 7) * CalendarItem.CellWidth)),
                gridTop + ((index / 7) * CalendarItem.CellHeight),
                CalendarItem.CellWidth,
                CalendarItem.CellHeight);
            var dayNumber = index - offset + 1;
            if (dayNumber >= 1 && dayNumber <= days && cell.Contains(point))
            {
                return new DateTime(firstOfMonth.Year, firstOfMonth.Month, dayNumber);
            }
        }

        return null;
    }

    private void SetHover(DateTime? date)
    {
        if (_hoverDate == date)
        {
            return;
        }

        _hoverDate = date;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        base.OnPointerPressed(args);
        if (IsTemplated || !IsEnabled || args.Button != MouseButtons.Left)
        {
            return;
        }

        var left = Padding.Left;
        var top = Padding.Top;
        var gridWidth = CalendarItem.CellWidth * 7f;
        var previous = new RectangleF(left + 4f, top + 5f, 24f, 24f);
        var next = new RectangleF(left + gridWidth - 28f, top + 5f, 24f, 24f);

        var point = args.GetPosition(this);
        if (previous.Contains(point))
        {
            DisplayMonth = DisplayMonth.AddMonths(-1);
            args.Handled = true;
            return;
        }

        if (next.Contains(point))
        {
            DisplayMonth = DisplayMonth.AddMonths(1);
            args.Handled = true;
            return;
        }

        var firstWeekDay = (int)FirstDayOfWeek;
        var firstOfMonth = DisplayMonth;
        var offset = ((int)firstOfMonth.DayOfWeek - firstWeekDay + 7) % 7;
        var days = DateTime.DaysInMonth(firstOfMonth.Year, firstOfMonth.Month);
        var gridTop = top + CalendarItem.HeaderHeight + CalendarItem.WeekHeaderHeight;
        for (var index = 0; index < 42; index++)
        {
            var cell = new RectangleF(
                left + (((index % 7) * CalendarItem.CellWidth)),
                gridTop + ((index / 7) * CalendarItem.CellHeight),
                CalendarItem.CellWidth,
                CalendarItem.CellHeight);
            var dayNumber = index - offset + 1;
            if (dayNumber < 1 || dayNumber > days || !cell.Contains(point))
            {
                continue;
            }

            Focus();
            SelectedDate = new DateTime(firstOfMonth.Year, firstOfMonth.Month, dayNumber);
            args.Handled = true;
            return;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(VisualKeyEventArgs args)
    {
        base.OnKeyDown(args);
        var current = SelectedDate ?? DateTime.Today;
        DateTime next;
        switch (args.KeyCode)
        {
            case Keys.Left:
                next = current.AddDays(-1);
                break;
            case Keys.Right:
                next = current.AddDays(1);
                break;
            case Keys.Up:
                next = current.AddDays(-7);
                break;
            case Keys.Down:
                next = current.AddDays(7);
                break;
            case Keys.PageUp:
                next = current.AddMonths(-1);
                break;
            case Keys.PageDown:
                next = current.AddMonths(1);
                break;
            default:
                return;
        }

        DisplayMonth = new DateTime(next.Year, next.Month, 1);
        SelectedDate = next;
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, SelectedDateProperty))
        {
            if (e.NewValue is DateTime selected)
            {
                DisplayMonth = new DateTime(selected.Year, selected.Month, 1);
            }

            SelectedDateChanged?.Invoke(this, EventArgs.Empty);
        }

        if (ReferenceEquals(e.Property, SelectedDateProperty)
            || ReferenceEquals(e.Property, DisplayMonthProperty)
            || ReferenceEquals(e.Property, FirstDayOfWeekProperty)
            || ReferenceEquals(e.Property, ForegroundProperty))
        {
            _calendarItem?.RefreshFromOwner();
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanging()
    {
        _calendarItem = null;
        base.OnTemplateChanging();
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        base.OnTemplateChanged();
        if (!IsTemplated)
        {
            return;
        }

        var part = GetTemplateChild<VisualElement>("PART_CalendarItem") ?? (TemplateRoot as CalendarItem);
        if (part is null)
        {
            throw new InvalidOperationException(
                "The Calendar ControlTemplate must declare a part named 'PART_CalendarItem' (a CalendarItem).");
        }

        var item = part switch
        {
            CalendarItem calendarItem => calendarItem,
            Panel host => host.Add(new CalendarItem()),
            _ => throw new InvalidOperationException(
                $"The Calendar ControlTemplate part 'PART_CalendarItem' must be a CalendarItem or a Panel, "
                + $"but was '{part.GetType().Name}'."),
        };

        item.Owner = this;
        _calendarItem = item;
        item.InvalidateVisual();
    }

}

/// <summary>A date field whose calendar opens in a <see cref="VisualPopup"/>.</summary>
/// <remarks>
/// <para>
/// Without a template the field and its calendar are self-drawn. A template must declare <c>PART_TextBox</c> (a
/// <see cref="Easel.Visuals.Controls.TextBox"/>), <c>PART_Button</c> (a
/// <see cref="Easel.Visuals.Controls.Button"/> or <see cref="ToggleButton"/>) and
/// <c>PART_Popup</c> (a <see cref="VisualPopup"/>) whose content hosts this control's calendar. Typing a date and
/// pressing <c>Enter</c> commits it. A missing required part throws <see cref="InvalidOperationException"/>.
/// </para>
/// </remarks>
public class DatePicker : TemplatedControl
{
    /// <summary>The selected day, or <see langword="null"/>.</summary>
    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(
        nameof(SelectedDate), typeof(DateTime?), typeof(DatePicker),
        new PropertyMetadata(null) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The display format passed to <see cref="DateTime.ToString(string, IFormatProvider)"/>.</summary>
    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(
        nameof(Format), typeof(string), typeof(DatePicker),
        new PropertyMetadata("yyyy-MM-dd") { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The text shown while nothing is selected.</summary>
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(DatePicker),
        new PropertyMetadata(string.Empty) { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The selected-date text colour as a brush value; an empty colour uses the active theme.</summary>
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(XamlBrush), typeof(DatePicker),
        new PropertyMetadata(null, null, static (_, value) => ColorBrushCoercion.Coerce(value))
        {
            AffectsRender = true,
        });

    private readonly Calendar _calendar = new();
    private readonly VisualPopup _popup = new();
    private VisualPopup? _templatePopup;
    private VisualTextBox? _partTextBox;
    private VisualButton? _partButton;
    private ToggleButton? _partToggle;
    private bool _hover;
    private bool _opening;
    private bool _syncingText;
    private bool _syncingToggle;
    private bool _themeSubscribed;

    /// <summary>Creates a focusable date field with theme metrics.</summary>
    public DatePicker()
    {
        Focusable = true;
        IsHitTestVisible = true;
        Cursor = Cursors.Hand;
        Padding = new XamlThickness(10f, 6f);
        MinHeight = Theme.Current.ControlHeight;
        CornerRadius = Theme.Current.CornerRadius;

        _calendar.Width = 250f;
        _calendar.Height = 226f;
        _popup.Child = _calendar;
        _popup.PlacementTarget = this;
        _popup.Placement = VisualPlacement.Bottom;
        _popup.StaysOpen = false;
        _popup.Closed += OnPopupClosed;
        _calendar.SelectedDateChanged += OnCalendarSelected;
    }

    /// <summary>Raised after <see cref="SelectedDate"/> changes.</summary>
    public event EventHandler? SelectedDateChanged;

    /// <summary>The selected day, or <see langword="null"/>.</summary>
    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    /// <summary>The display format passed to <see cref="DateTime.ToString(string, IFormatProvider)"/>.</summary>
    public string Format
    {
        get => (string)GetValue(FormatProperty)!;
        set => SetValue(FormatProperty, value ?? string.Empty);
    }

    /// <summary>The text shown while nothing is selected.</summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty)!;
        set => SetValue(PlaceholderProperty, value ?? string.Empty);
    }

    /// <summary>The selected-date text colour; an empty colour uses the active theme.</summary>
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

    /// <summary>The calendar shown by the drop-down; exposed for tests and hosts.</summary>
    public Calendar DropDownCalendar => _calendar;

    /// <summary>Whether the calendar is open.</summary>
    public bool IsDropDownOpen
    {
        get => ActivePopup.IsOpen;
        set
        {
            var popup = ActivePopup;
            if (value)
            {
                _opening = true;
                try
                {
                    _calendar.SelectedDate = SelectedDate ?? DateTime.Today;
                }
                finally
                {
                    _opening = false;
                }

                popup.IsOpen = true;
                _calendar.Focus();
            }
            else
            {
                popup.IsOpen = false;
            }

            SyncPartToggle();
        }
    }

    private VisualPopup ActivePopup => _templatePopup ?? _popup;

    private string DisplayText
        => SelectedDate is { } date
            ? date.ToString(string.IsNullOrEmpty(Format) ? "yyyy-MM-dd" : Format, CultureInfo.CurrentCulture)
            : Placeholder;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsTemplated)
        {
            return MeasureTemplate(availableSize);
        }

        var sample = string.IsNullOrEmpty(DisplayText) ? "yyyy-MM-dd" : DisplayText;
        var measured = VisualText.Measure(sample, ResolveFont(DefaultFont), 0f, wrap: false, RenderDpi);
        return new Size(
            (int)Math.Ceiling(measured.Width + Padding.Horizontal + 30),
            Math.Max(Theme.Current.ControlHeight, (int)Math.Ceiling(measured.Height + Padding.Vertical + 2)));
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        if (IsTemplated)
        {
            return;
        }

        var theme = Theme.Current;
        var background = Background?.ApproximateColor
            ?? (_hover || _popup.IsOpen ? theme.ControlHover : theme.ControlBackground);
        var bounds = new Rectangle(0, 0, Math.Max(1, (int)Math.Ceiling(Bounds.Width)), Math.Max(1, (int)Math.Ceiling(Bounds.Height)));
        var radius = CornerRadius > 0 ? CornerRadius : theme.CornerRadius;
        XamlDrawing.PaintChrome(
            graphics,
            bounds,
            background,
            BorderBrush?.ApproximateColor ?? theme.ControlBorder,
            BorderThickness.IsEmpty ? theme.ControlBorderThickness : BorderThickness,
            radius);

        if (IsFocusVisible)
        {
            using var focusPath = XamlDrawing.RoundedRectangle(
                new RectangleF(1.5f, 1.5f, Math.Max(0f, bounds.Width - 3f), Math.Max(0f, bounds.Height - 3f)),
                Math.Max(0f, radius - 1f));
            using var focusPen = new Pen(theme.FocusRing, theme.FocusThickness);
            graphics.DrawPath(focusPen, focusPath);
        }

        var glyph = new RectangleF(bounds.Width - 24f, (bounds.Height - 14f) / 2f, 14f, 14f);
        using (var pen = new Pen(theme.MutedForeground, 1.3f))
        {
            graphics.DrawRectangle(pen, glyph.X, glyph.Y, glyph.Width, glyph.Height);
            graphics.DrawLine(pen, glyph.X, glyph.Y + 4f, glyph.Right, glyph.Y + 4f);
            graphics.DrawLine(pen, glyph.X + 4.5f, glyph.Y, glyph.X + 4.5f, glyph.Bottom);
            graphics.DrawLine(pen, glyph.Right - 4.5f, glyph.Y, glyph.Right - 4.5f, glyph.Bottom);
        }

        var text = DisplayText;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        VisualText.Draw(this,
            graphics,
            text,
            ResolveFont(DefaultFont),
            SelectedDate is null
                ? theme.MutedForeground
                : Foreground.IsEmpty ? theme.Foreground : Foreground,
            new Rectangle(
                (int)Math.Ceiling(Padding.Left),
                (int)Math.Ceiling(Padding.Top),
                Math.Max(0, bounds.Width - (int)Math.Ceiling(Padding.Horizontal) - 20),
                Math.Max(0, bounds.Height - (int)Math.Ceiling(Padding.Vertical))),
            wrap: false,
            StringAlignment.Near,
            StringAlignment.Center);
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(VisualPointerEventArgs args)
    {
        base.OnPointerEntered(args);
        _hover = true;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(VisualPointerEventArgs args)
    {
        base.OnPointerExited(args);
        _hover = false;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(VisualPointerEventArgs args)
    {
        base.OnPointerPressed(args);
        if (!IsEnabled || args.Button != MouseButtons.Left)
        {
            return;
        }

        Focus();
        IsDropDownOpen = !IsDropDownOpen;
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(VisualKeyEventArgs args)
    {
        base.OnKeyDown(args);

        if (IsTemplated && _partTextBox is not null
            && args.KeyCode == Keys.Enter && TryCommitTypedDate())
        {
            args.Handled = true;
            return;
        }

        switch (args.KeyCode)
        {
            case Keys.Down when (args.Modifiers & Keys.Alt) != 0:
            case Keys.Enter:
            case Keys.Space:
                IsDropDownOpen = true;
                args.Handled = true;
                break;
            case Keys.Escape when ActivePopup.IsOpen:
                ActivePopup.IsOpen = false;
                args.Handled = true;
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, SelectedDateProperty))
        {
            if (e.NewValue is null && ActivePopup.IsOpen)
            {
                _calendar.SelectedDate = null;
            }

            SyncPartText();
            SelectedDateChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (ReferenceEquals(e.Property, FormatProperty)
                 || ReferenceEquals(e.Property, PlaceholderProperty)
                 || ReferenceEquals(e.Property, ForegroundProperty))
        {
            SyncPartText();
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanging()
    {
        if (ActivePopup.IsOpen)
        {
            ActivePopup.IsOpen = false;
        }

        DetachTemplateParts();
        base.OnTemplateChanging();
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        base.OnTemplateChanged();
        if (IsTemplated)
        {
            ResolveTemplateParts();
            return;
        }

        RestoreUntemplatedCalendar();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _popup.Closed -= OnPopupClosed;
            _calendar.SelectedDateChanged -= OnCalendarSelected;
        }

        base.Dispose(disposing);
        if (disposing)
        {
            DetachTemplateParts();
            _popup.Dispose();
        }
    }

    private void ResolveTemplateParts()
    {
        var textPart = GetTemplateChild<VisualElement>("PART_TextBox");
        if (textPart is null)
        {
            throw MissingPart("PART_TextBox", "TextBox");
        }

        if (textPart is not VisualTextBox textBox)
        {
            throw WrongPart("PART_TextBox", textPart, "TextBox");
        }

        _partTextBox = textBox;
        _partTextBox.PropertyChanged -= OnPartTextChanged;
        _partTextBox.PropertyChanged += OnPartTextChanged;
        _partTextBox.PointerPressed -= OnPartTextPointerPressed;
        _partTextBox.PointerPressed += OnPartTextPointerPressed;
        SyncPartText();

        var buttonPart = GetTemplateChild<VisualElement>("PART_Button");
        if (buttonPart is null)
        {
            throw MissingPart("PART_Button", "Button or ToggleButton");
        }

        switch (buttonPart)
        {
            case VisualButton button:
                _partButton = button;
                button.Click -= OnPartButtonClick;
                button.Click += OnPartButtonClick;
                break;
            case ToggleButton toggle:
                _partToggle = toggle;
                toggle.PropertyChanged -= OnPartToggleChanged;
                toggle.PropertyChanged += OnPartToggleChanged;
                SyncPartToggle();
                break;
            default:
                throw WrongPart("PART_Button", buttonPart, "Button or ToggleButton");
        }

        RestoreNestedTemplate(buttonPart);

        var popupPart = GetTemplateChild<VisualElement>("PART_Popup");
        if (popupPart is null)
        {
            throw MissingPart("PART_Popup", "VisualPopup");
        }

        if (popupPart is not VisualPopup popup)
        {
            throw WrongPart("PART_Popup", popupPart, "VisualPopup");
        }

        _templatePopup = popup;
        popup.ParentElement?.RemoveChild(popup);
        popup.PlacementTarget = this;
        popup.Placement = VisualPlacement.Bottom;
        popup.StaysOpen = false;
        popup.Closed -= OnPopupClosed;
        popup.Closed += OnPopupClosed;

        // 日历实例归 DatePicker 所有；弹层只提供宿主。
        _popup.Child = null;
        _calendar.ParentElement?.RemoveChild(_calendar);
        switch (popup.Child)
        {
            case null:
                popup.Child = _calendar;
                break;
            case Panel host:
                host.Add(_calendar);
                break;
            default:
                throw new InvalidOperationException(
                    "DatePicker requires the 'PART_Popup' Child to be empty or a Panel; the control hosts its own "
                    + "Calendar inside it.");
        }

        SyncPartToggle();
        SubscribeTheme();
    }

    private void DetachTemplateParts()
    {
        if (_partTextBox is { } textBox)
        {
            textBox.PropertyChanged -= OnPartTextChanged;
            textBox.PointerPressed -= OnPartTextPointerPressed;
            _partTextBox = null;
        }

        UnsubscribeTheme();

        if (_partButton is { } button)
        {
            button.Click -= OnPartButtonClick;
            _partButton = null;
        }

        if (_partToggle is { } toggle)
        {
            toggle.PropertyChanged -= OnPartToggleChanged;
            _partToggle = null;
        }

        if (_templatePopup is { } popup)
        {
            popup.Closed -= OnPopupClosed;
            if (popup.IsOpen)
            {
                popup.IsOpen = false;
            }

            // 日历是控件自己的，不随模板 disposing。
            if (ReferenceEquals(popup.Child, _calendar))
            {
                popup.Child = null;
            }
            else if (_calendar.ParentElement is { } parent)
            {
                parent.RemoveChild(_calendar);
            }

            popup.ParentElement?.RemoveChild(popup);
            popup.Dispose();
            _templatePopup = null;
        }

        RestoreUntemplatedCalendar();
    }

    private void RestoreUntemplatedCalendar()
    {
        if (!ReferenceEquals(_popup.Child, _calendar))
        {
            _calendar.ParentElement?.RemoveChild(_calendar);
            _popup.Child = _calendar;
        }
    }

    private void SyncPartText()
    {
        if (_partTextBox is null)
        {
            return;
        }

        _syncingText = true;
        try
        {
            _partTextBox.Text = DisplayText;
            // 未选日期时用弱化色；模板里的 TextBox 只认自己的 Foreground 本地值。
            _partTextBox.Foreground = SelectedDate is null
                ? Theme.Current.MutedForeground
                : Foreground.IsEmpty ? Theme.Current.Foreground : Foreground;
        }
        finally
        {
            _syncingText = false;
        }
    }

    private void SubscribeTheme()
    {
        if (_themeSubscribed)
        {
            return;
        }

        Theme.Changed += OnThemeChanged;
        _themeSubscribed = true;
    }

    private void UnsubscribeTheme()
    {
        if (!_themeSubscribed)
        {
            return;
        }

        Theme.Changed -= OnThemeChanged;
        _themeSubscribed = false;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => SyncPartText();

    private void SyncPartToggle()
    {
        if (_partToggle is null)
        {
            return;
        }

        _syncingToggle = true;
        try
        {
            _partToggle.IsChecked = ActivePopup.IsOpen;
        }
        finally
        {
            _syncingToggle = false;
        }
    }

    private void OnPartTextChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (_syncingText || !ReferenceEquals(e.Property, VisualTextBox.TextProperty))
        {
            return;
        }

        // TextBox 把 Enter 标记为已处理，输入框不会把回车冒泡给 DatePicker；能解析的完整日期直接提交。
        if (TryParseDate(_partTextBox?.Text, out var date) && SelectedDate?.Date != date)
        {
            SelectedDate = date;
        }

        InvalidateVisual();
    }

    private void OnPartButtonClick(object? sender, EventArgs e)
    {
        Focus();
        IsDropDownOpen = !IsDropDownOpen;
        SyncPartToggle();
    }

    private void OnPartTextPointerPressed(object? sender, VisualPointerEventArgs e)
    {
        if (!IsEnabled || e.Button != MouseButtons.Left)
        {
            return;
        }

        // TextBox 把按下标记为已处理，冒泡到不了 DatePicker；模板里的输入框也要能开合日历。
        Focus();
        IsDropDownOpen = !IsDropDownOpen;
        SyncPartToggle();
    }

    private void OnPartToggleChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (_syncingToggle || !ReferenceEquals(e.Property, ToggleButton.IsCheckedProperty))
        {
            return;
        }

        IsDropDownOpen = _partToggle?.IsChecked == true;
        SyncPartToggle();
    }

    private bool TryCommitTypedDate()
    {
        if (!TryParseDate(_partTextBox?.Text, out var date))
        {
            return false;
        }

        SelectedDate = date;
        if (ActivePopup.IsOpen)
        {
            ActivePopup.IsOpen = false;
        }

        SyncPartToggle();
        return true;
    }

    private bool TryParseDate(string? text, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var format = string.IsNullOrEmpty(Format) ? "yyyy-MM-dd" : Format;
        if (DateTime.TryParseExact(
                text,
                format,
                CultureInfo.CurrentCulture,
                DateTimeStyles.None,
                out var exact))
        {
            date = exact.Date;
            return true;
        }

        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out exact))
        {
            date = exact.Date;
            return true;
        }

        return false;
    }

    private void OnCalendarSelected(object? sender, EventArgs e)
    {
        if (_opening)
        {
            return;
        }

        SelectedDate = _calendar.SelectedDate;
        ActivePopup.IsOpen = false;
        SyncPartToggle();
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        Focus();
        SyncPartToggle();
        InvalidateVisual();
    }

    private static InvalidOperationException MissingPart(string name, string expected)
        => new($"The DatePicker ControlTemplate must declare a part named '{name}' ({expected}).");

    // 外层模板的 SetTemplatedParentRecursive 会一路盖到内嵌模板的后代，让内层部件的 TemplateBinding 源错位；
    // 内层模板根要重新认回自己的控件。
    private static void RestoreNestedTemplate(VisualElement nested)
    {
        if (nested is TemplatedControl { TemplateRoot: { } root } templated)
        {
            root.SetTemplatedParentRecursive(templated);
        }
    }

    private static InvalidOperationException WrongPart(string name, object part, string expected)
        => new(
            $"The DatePicker ControlTemplate part '{name}' must be a {expected}, but was '{part.GetType().Name}'.");
}
