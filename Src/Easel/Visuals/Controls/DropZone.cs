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
public class DropZone : VisualElement
{
    /// <summary>The headline shown in the zone.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(DropZone),
        new PropertyMetadata("Drop rows or files") { AffectsMeasure = true, AffectsRender = true });

    /// <summary>The supporting line shown under the headline.</summary>
    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail), typeof(string), typeof(DropZone),
        new PropertyMetadata("Move a row from another list, or drop a CSV from Explorer.") { AffectsMeasure = true, AffectsRender = true });

    private bool _hot;
    private bool _hover;
    private Font? _titleFont;
    private Font? _titleFontBase;

    /// <summary>Creates a focusable-looking zone that accepts any drag data.</summary>
    public DropZone()
    {
        IsHitTestVisible = true;
        AllowDrop = true;
        CornerRadius = Theme.Current.CornerRadius;
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
    public bool IsHot => _hot;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width >= int.MaxValue / 2 ? 360 : Math.Max(0, availableSize.Width);
        var height = availableSize.Height >= int.MaxValue / 2
            ? Math.Max((int)MinHeight, (int)Math.Ceiling(64d + Padding.Vertical))
            : Math.Max(0, availableSize.Height);
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnRender(Graphics graphics)
    {
        var theme = Theme.Current;
        var bounds = new RectangleF(0.5f, 0.5f, Math.Max(0f, Bounds.Width - 1f), Math.Max(0f, Bounds.Height - 1f));
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        var accent = theme.Accent;
        var tint = _hot
            ? Color.FromArgb(34, accent.R, accent.G, accent.B)
            : Color.FromArgb(_hover ? 18 : 10, theme.Foreground.R, theme.Foreground.G, theme.Foreground.B);
        var border = _hot ? accent : Color.FromArgb(120, theme.ControlBorder.R, theme.ControlBorder.G, theme.ControlBorder.B);

        using (var path = XamlDrawing.RoundedRectangle(bounds, CornerRadius > 0f ? CornerRadius : theme.CornerRadius))
        using (var fill = new SolidBrush(tint))
        using (var pen = new System.Drawing.Pen(border, _hot ? 1.8f : 1f) { DashStyle = DashStyle.Dash })
        {
            graphics.FillPath(fill, path);
            graphics.DrawPath(pen, path);
        }

        var iconCenter = new PointF(Padding.Left + 18f, bounds.Height / 2f);
        DrawTrayIcon(graphics, accent, iconCenter);

        var textLeft = iconCenter.X + 26f;
        var textWidth = Math.Max(0f, bounds.Width - textLeft - Padding.Right);
        var font = ResolveFont(DefaultFont);
        var titleFont = GetTitleFont(font);
        var titleHeight = Math.Min(22f, bounds.Height / 2f);
        var lineHeight = Math.Max(16f, bounds.Height / 2f);
        var top = (bounds.Height - (titleHeight + lineHeight)) / 2f;

        VisualText.Draw(
            this,
            graphics,
            Title,
            titleFont,
            _hot ? accent : theme.Foreground,
            new RectangleF(textLeft, top, textWidth, titleHeight),
            wrap: false,
            StringAlignment.Near,
            StringAlignment.Center);

        VisualText.Draw(
            this,
            graphics,
            _hot ? "Release to drop" : Detail,
            font,
            theme.MutedForeground,
            new RectangleF(textLeft, top + titleHeight, textWidth, lineHeight),
            wrap: false,
            StringAlignment.Near,
            StringAlignment.Center);
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
        if (_hot)
        {
            _hot = false;
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

        var files = VisualDragDrop.GetFiles(args.Data);
        var text = VisualDragDrop.GetText(args.Data);
        VisualDragDrop.TryGetData(args.Data, VisualDragDrop.DataGridRowsFormat, out DataGridDragPayload? payload);
        var effect = ResolveEffect(args.AllowedEffects, payload);
        var dropped = new DropZoneEventArgs(args.Data, files, text, payload, effect);
        Dropped?.Invoke(this, dropped);
        _hot = false;
        InvalidateVisual();
        args.Effects = dropped.Effects;
        args.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(VisualPointerEventArgs args)
    {
        base.OnPointerEntered(args);
        if (!_hover)
        {
            _hover = true;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerExited(VisualPointerEventArgs args)
    {
        base.OnPointerExited(args);
        if (_hover)
        {
            _hover = false;
            InvalidateVisual();
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
        if (!_hot)
        {
            _hot = true;
            InvalidateVisual();
        }
    }

    private static DragDropEffects ResolveEffect(DragDropEffects allowed, DataGridDragPayload? payload)
        => payload is not null ? VisualDragDrop.ResolveRowEffect(allowed) : (allowed & DragDropEffects.Copy) != 0 ? DragDropEffects.Copy : (allowed & DragDropEffects.Link) != 0 ? DragDropEffects.Link : DragDropEffects.None;

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
