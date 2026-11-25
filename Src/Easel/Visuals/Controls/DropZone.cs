using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Easel;
using XamlDrawing = Easel.Controls.XamlDrawing;
using XamlThickness = Easel.Controls.Thickness;

namespace Easel.Visuals.Controls;

/// <summary>Data for <see cref="DropZone.Dropped"/>.</summary>
public sealed class DropZoneEventArgs : EventArgs
{
    internal DropZoneEventArgs(
        IDataObject data,
        IReadOnlyList<string> files,
        string? text,
        DataGridDragPayload? payload,
        DragDropEffects effects)
    {
        Data = data;
        Files = files;
        Text = text;
        Payload = payload;
        Effects = effects;
    }

    /// <summary>The dropped data object.</summary>
    public IDataObject Data { get; }

    /// <summary>The dropped file paths; empty when the drag carries no files.</summary>
    public IReadOnlyList<string> Files { get; }

    /// <summary>The dropped text, or <see langword="null"/>.</summary>
    public string? Text { get; }

    /// <summary>The row payload when the drag came from a <see cref="DataGrid"/>.</summary>
    public DataGridDragPayload? Payload { get; }

    /// <summary>The effect the drop used; a handler may set another value.</summary>
    public DragDropEffects Effects { get; set; }

    /// <summary>Whether a handler consumed the drop.</summary>
    public bool Handled { get; set; }
}

/// <summary>A dashed surface that accepts files, text and rows and raises one <see cref="Dropped"/> event.</summary>
/// <remarks>
/// The zone is deliberately not tied to a data shape: the application decides what a file path or a grid row becomes.
/// It participates in the native OLE drag loop through the same <see cref="VisualElement.AllowDrop"/> route as every
/// other visual element.
/// </remarks>
public class DropZone : TemplatedControl
{
    /// <summary>The headline shown in the zone.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(DropZone),
        new PropertyMetadata("Drop rows or files") { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The supporting line shown under the headline.</summary>
    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail), typeof(string), typeof(DropZone),
        new PropertyMetadata("Move a row from another list, or drop a CSV from Explorer.") { AffectsMeasure = true, AffectsRender = true });

    /// <summary>Whether a drag is currently over the zone; template triggers may observe it.</summary>
    public static readonly DependencyProperty IsHotProperty = DependencyProperty.Register(
        nameof(IsHot), typeof(bool), typeof(DropZone),
        new PropertyMetadata(false) { AffectsRender = true });

    /// <summary>Whether the pointer is over the zone; template triggers may observe it.</summary>
    public static readonly DependencyProperty IsPointerOverProperty = DependencyProperty.Register(
        nameof(IsPointerOver), typeof(bool), typeof(DropZone),
        new PropertyMetadata(false) { AffectsRender = true });

    private Font? _titleFont;
    private Font? _titleFontBase;
    private DropZoneChrome? _chrome;

    /// <summary>Creates a focusable-looking zone that accepts any drag data.</summary>
    public DropZone()
    {
        IsHitTestVisible = true;
        AllowDrop = true;
        SetThemeMetric(CornerRadiusProperty, static palette => palette.CornerRadius);
        MinHeight = 88d;
        Padding = new XamlThickness(18f, 14f);
    }

    /// <summary>Raised when data is dropped on the zone.</summary>
    public event EventHandler<DropZoneEventArgs>? Dropped;

    /// <summary>The headline shown in the zone.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value ?? string.Empty);
    }

    /// <summary>The supporting line shown under the headline.</summary>
    public string Detail
    {
        get => (string)GetValue(DetailProperty)!;
        set => SetValue(DetailProperty, value ?? string.Empty);
    }

    /// <summary>Whether a drag is currently over the zone.</summary>
    public bool IsHot => (bool)GetValue(IsHotProperty)!;

    /// <summary>Whether the pointer is over the zone; template triggers may observe it.</summary>
    public new bool IsPointerOver => (bool)GetValue(IsPointerOverProperty)!;

    /// <inheritdoc />
    protected override bool HitTestContent(PointF point) => IsHitTestVisible;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var fallback = MeasureFallback(availableSize);
        if (!IsTemplated)
        {
            return fallback;
        }

        var template = MeasureTemplate(availableSize);
        return new Size(
            Math.Max(fallback.Width, template.Width),
            Math.Max(fallback.Height, template.Height));
    }

    /// <inheritdoc />
    protected override void ArrangeOverride(Size finalSize)
    {
        if (IsTemplated)
        {
            base.ArrangeOverride(finalSize);
        }
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
        => DropZoneChrome.Paint(
            this,
            graphics,
            new Size((int)Math.Ceiling(Bounds.Width), (int)Math.Ceiling(Bounds.Height)),
            Padding,
            CornerRadius,
            ResolveFont(DefaultFont),
            GetTitleFont(ResolveFont(DefaultFont)),
            Title,
            Detail,
            IsHot,
            IsPointerOver);

    /// <inheritdoc />
    protected override void OnTemplateChanging()
    {
        _chrome = null;
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged()
    {
        _chrome = GetTemplateChild<DropZoneChrome>("Chrome");
        SyncChrome();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, IsHotProperty)
            || ReferenceEquals(e.Property, IsPointerOverProperty)
            || ReferenceEquals(e.Property, TitleProperty)
            || ReferenceEquals(e.Property, DetailProperty)
            || ReferenceEquals(e.Property, CornerRadiusProperty)
            || ReferenceEquals(e.Property, PaddingProperty))
        {
            SyncChrome();
        }
    }

    /// <inheritdoc />
    protected override void OnDragEnter(VisualDragEventArgs args)
    {
        UpdateDrag(args);
    }

    /// <inheritdoc />
    protected override void OnDragOver(VisualDragEventArgs args)
    {
        UpdateDrag(args);
    }

    /// <inheritdoc />
    protected override void OnDragLeave(VisualDragEventArgs args)
    {
        if (IsHot)
        {
            SetHot(false);
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

        var files = VisualDragDrop.GetFiles(args.Data);
        var text = VisualDragDrop.GetText(args.Data);
        VisualDragDrop.TryGetData(args.Data, VisualDragDrop.DataGridRowsFormat, out DataGridDragPayload? payload);
        var effect = ResolveEffect(args.AllowedEffects, payload);
        var dropped = new DropZoneEventArgs(args.Data, files, text, payload, effect);
        Dropped?.Invoke(this, dropped);
        SetHot(false);
        args.Effects = dropped.Effects;
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(VisualPointerEventArgs args)
    {
        base.OnPointerEntered(args);
        if (!IsPointerOver)
        {
            SetValue(IsPointerOverProperty, true);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerExited(VisualPointerEventArgs args)
    {
        base.OnPointerExited(args);
        if (IsPointerOver)
        {
            SetValue(IsPointerOverProperty, false);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont?.Dispose();
            _titleFont = null;
        }

        base.Dispose(disposing);
    }

    private void UpdateDrag(VisualDragEventArgs args)
    {
        if (!IsEnabled)
        {
            args.Effects = DragDropEffects.None;
            return;
        }

        VisualDragDrop.TryGetData(args.Data, VisualDragDrop.DataGridRowsFormat, out DataGridDragPayload? payload);
        var effect = ResolveEffect(args.AllowedEffects, payload);
        if (effect == DragDropEffects.None)
        {
            args.Effects = DragDropEffects.None;
            return;
        }

        args.Effects = effect;
        args.Handled = true;
        if (!IsHot)
        {
            SetHot(true);
        }
    }

    private static DragDropEffects ResolveEffect(DragDropEffects allowed, DataGridDragPayload? payload)
        => payload is not null ? VisualDragDrop.ResolveRowEffect(allowed) : (allowed & DragDropEffects.Copy) != 0 ? DragDropEffects.Copy : (allowed & DragDropEffects.Link) != 0 ? DragDropEffects.Link : DragDropEffects.None;

    private Size MeasureFallback(Size availableSize)
    {
        var width = availableSize.Width >= int.MaxValue / 2 ? 360 : Math.Max(0, availableSize.Width);
        var height = availableSize.Height >= int.MaxValue / 2
            ? Math.Max((int)MinHeight, (int)Math.Ceiling(64d + Padding.Vertical))
            : Math.Max(0, availableSize.Height);
        return new Size(width, height);
    }

    private void SetHot(bool hot) => SetValue(IsHotProperty, hot);

    // 模板触发器只认控件自己的 DP；布尔状态由控件推给铬件，改标题等值也随同一路径刷新。
    private void SyncChrome()
    {
        if (_chrome is not { } chrome)
        {
            return;
        }

        chrome.Title = Title;
        chrome.Detail = Detail;
        chrome.IsHot = IsHot;
        chrome.IsPointerOver = IsPointerOver;
        chrome.CornerRadius = CornerRadius;
        chrome.Padding = Padding;
    }

    private Font GetTitleFont(Font baseFont)
    {
        if (_titleFont is not null && ReferenceEquals(_titleFontBase, baseFont))
        {
            return _titleFont;
        }

        _titleFont?.Dispose();
        _titleFontBase = baseFont;
        _titleFont = new Font(baseFont, FontStyle.Bold);
        return _titleFont;
    }
}

// 模板里的铬件：虚线边框、热态底色、托盘图标和两行文字都在这里画，DropZone 只负责状态与拖放。
internal sealed class DropZoneChrome : VisualElement
{
    internal static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(DropZoneChrome),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    internal static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail), typeof(string), typeof(DropZoneChrome),
        new PropertyMetadata(string.Empty) { AffectsRender = true });

    internal static readonly DependencyProperty IsHotProperty = DependencyProperty.Register(
        nameof(IsHot), typeof(bool), typeof(DropZoneChrome),
        new PropertyMetadata(false) { AffectsRender = true });

    internal static readonly DependencyProperty IsPointerOverProperty = DependencyProperty.Register(
        nameof(IsPointerOver), typeof(bool), typeof(DropZoneChrome),
        new PropertyMetadata(false) { AffectsRender = true });

    private Font? _titleFont;
    private Font? _titleFontBase;

    internal DropZoneChrome()
    {
        IsHitTestVisible = false;
    }

    internal string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value ?? string.Empty);
    }

    internal string Detail
    {
        get => (string)GetValue(DetailProperty)!;
        set => SetValue(DetailProperty, value ?? string.Empty);
    }

    internal bool IsHot
    {
        get => (bool)GetValue(IsHotProperty)!;
        set => SetValue(IsHotProperty, value);
    }

    internal new bool IsPointerOver
    {
        get => (bool)GetValue(IsPointerOverProperty)!;
        set => SetValue(IsPointerOverProperty, value);
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
        => Paint(
            this,
            graphics,
            new Size((int)Math.Ceiling(Bounds.Width), (int)Math.Ceiling(Bounds.Height)),
            Padding,
            CornerRadius,
            ResolveFont(DefaultFont),
            GetTitleFont(ResolveFont(DefaultFont)),
            Title,
            Detail,
            IsHot,
            IsPointerOver);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont?.Dispose();
            _titleFont = null;
        }

        base.Dispose(disposing);
    }

    internal static void Paint(
        VisualElement owner,
        Graphics graphics,
        Size size,
        XamlThickness padding,
        float cornerRadius,
        Font baseFont,
        Font titleFont,
        string title,
        string detail,
        bool hot,
        bool hover)
    {
        var theme = Theme.Current;
        var bounds = new RectangleF(0.5f, 0.5f, Math.Max(0f, size.Width - 1f), Math.Max(0f, size.Height - 1f));
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        var accent = theme.Accent;
        var tint = hot
            ? Color.FromArgb(34, accent.R, accent.G, accent.B)
            : Color.FromArgb(hover ? 18 : 10, theme.Foreground.R, theme.Foreground.G, theme.Foreground.B);
        var border = hot ? accent : Color.FromArgb(120, theme.ControlBorder.R, theme.ControlBorder.G, theme.ControlBorder.B);

        using (var path = XamlDrawing.RoundedRectangle(bounds, cornerRadius > 0f ? cornerRadius : theme.CornerRadius))
        using (var fill = new SolidBrush(tint))
        using (var pen = new System.Drawing.Pen(border, hot ? 1.8f : 1f) { DashStyle = DashStyle.Dash })
        {
            graphics.FillPath(fill, path);
            graphics.DrawPath(pen, path);
        }

        var iconCenter = new PointF(padding.Left + 18f, bounds.Height / 2f);
        DrawTrayIcon(graphics, accent, iconCenter);

        var textLeft = iconCenter.X + 26f;
        var textWidth = Math.Max(0f, bounds.Width - textLeft - padding.Right);
        var titleHeight = Math.Min(22f, bounds.Height / 2f);
        var lineHeight = Math.Max(16f, bounds.Height / 2f);
        var top = (bounds.Height - (titleHeight + lineHeight)) / 2f;

        VisualText.Draw(
            owner,
            graphics,
            title,
            titleFont,
            hot ? accent : theme.Foreground,
            new RectangleF(textLeft, top, textWidth, titleHeight),
            wrap: false,
            StringAlignment.Near,
            StringAlignment.Center);

        VisualText.Draw(
            owner,
            graphics,
            hot ? "Release to drop" : detail,
            baseFont,
            theme.MutedForeground,
            new RectangleF(textLeft, top + titleHeight, textWidth, lineHeight),
            wrap: false,
            StringAlignment.Near,
            StringAlignment.Center);
    }

    private static void DrawTrayIcon(Graphics graphics, Color accent, PointF center)
    {
        var tray = new RectangleF(center.X - 9f, center.Y - 1f, 18f, 9f);
        using (var pen = new System.Drawing.Pen(accent, 1.4f))
        {
            using var path = XamlDrawing.RoundedRectangle(tray, 3f);
            graphics.DrawPath(pen, path);
            graphics.DrawLine(pen, center.X, center.Y - 11f, center.X, center.Y - 3f);
            graphics.DrawLines(pen, new[]
            {
                new PointF(center.X - 3.5f, center.Y - 6.5f),
                new PointF(center.X, center.Y - 3f),
                new PointF(center.X + 3.5f, center.Y - 6.5f),
            });
        }
    }

    private Font GetTitleFont(Font baseFont)
    {
        if (_titleFont is not null && ReferenceEquals(_titleFontBase, baseFont))
        {
            return _titleFont;
        }

        _titleFont?.Dispose();
        _titleFontBase = baseFont;
        _titleFont = new Font(baseFont, FontStyle.Bold);
        return _titleFont;
    }
}
