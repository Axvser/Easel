using System.Drawing;
using System.Windows.Forms;
using XamlDrawing = Easel.Controls.XamlDrawing;

namespace Easel.Visuals;

/// <summary>Drag-and-drop data routed to a <see cref="VisualElement"/> by its <see cref="VisualHost"/>.</summary>
public sealed class VisualDragEventArgs : EventArgs
{
    internal VisualDragEventArgs(
        IDataObject data,
        DragDropEffects allowedEffects,
        DragDropEffects effects,
        PointF position,
        Keys modifiers)
    {
        Data = data;
        AllowedEffects = allowedEffects;
        Effects = effects;
        Position = position;
        Modifiers = modifiers;
    }

    /// <summary>The object carried by the drag; it may hold text, files or an in-process payload.</summary>
    public IDataObject Data { get; }

    /// <summary>The effects the drag source permits.</summary>
    public DragDropEffects AllowedEffects { get; }

    /// <summary>The effect the target wants; set it in <see cref="VisualElement.DragOver"/> or <see cref="VisualElement.Drop"/>.</summary>
    public DragDropEffects Effects { get; set; }

    /// <summary>The pointer position in the surface's client coordinates.</summary>
    public PointF Position { get; }

    /// <summary>The modifier keys held during the drag.</summary>
    public Keys Modifiers { get; }

    /// <summary>Whether an element handled the event; a handled event stops bubbling.</summary>
    public bool Handled { get; set; }

    /// <summary>Converts the surface position into an element's local coordinates.</summary>
    /// <param name="element">The element whose local space is used.</param>
    public PointF GetPosition(VisualElement element)
        => element is null ? Position : element.PointFromSurface(Position);
}

/// <summary>Starts drags and builds the data objects shared by Easel drop targets.</summary>
/// <remarks>
/// <para>
/// The drag itself is the native Windows OLE drag loop: dragging out of the app and dropping from Explorer use the
/// same code path as an in-process row move. <see cref="StartDrag"/> blocks until the user drops or cancels, exactly
/// like <see cref="Control.DoDragDrop(IDataObject, DragDropEffects)"/>.
/// </para>
/// <para>
/// <c>Easel.DataGrid.Rows</c> carries a <see cref="DataGridDragPayload"/> so two grids in the same process move the
/// same objects instead of re-parsing text. Text and <see cref="DataFormats.FileDrop"/> are included for other apps.
/// </para>
/// </remarks>
public static class VisualDragDrop
{
    /// <summary>The format that carries <see cref="DataGridDragPayload"/> between two data grids.</summary>
    public const string DataGridRowsFormat = "Easel.DataGrid.Rows";

    /// <summary>Whether a drag started from this process is currently running.</summary>
    public static bool IsDragging { get; internal set; }

    /// <summary>The element that started the current drag, or <see langword="null"/>.</summary>
    public static VisualElement? DragSource { get; internal set; }

    /// <summary>The object carried by the current drag, or <see langword="null"/>.</summary>
    public static IDataObject? DragData { get; internal set; }

    /// <summary>The effect the current drag target wants.</summary>
    public static DragDropEffects CurrentEffects { get; internal set; }

    /// <summary>Starts a native drag for <paramref name="source"/> and returns the effect the drop reported.</summary>
    /// <param name="source">The element that owns the drag; it must be attached to a <see cref="VisualHost"/>.</param>
    /// <param name="data">The data carried by the drag.</param>
    /// <param name="allowedEffects">The effects the source permits.</param>
    /// <param name="ghostText">Text for the floating drag label, or <see langword="null"/> for no label.</param>
    /// <returns>The effect the drop target reported, or <see cref="DragDropEffects.None"/>.</returns>
    public static DragDropEffects StartDrag(
        VisualElement source,
        IDataObject data,
        DragDropEffects allowedEffects,
        string? ghostText = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (data is null) throw new ArgumentNullException(nameof(data));
        return source.Surface?.StartVisualDrag(source, data, allowedEffects, ghostText)
            ?? DragDropEffects.None;
    }

    /// <summary>Creates a data object with text, files and an optional in-process payload.</summary>
    /// <param name="payload">An optional object for the <c>Easel.DataGrid.Rows</c> format.</param>
    /// <param name="text">Plain text for other applications.</param>
    /// <param name="files">File paths for Explorer and other file drop targets.</param>
    public static DataObject CreateData(object? payload = null, string? text = null, string[]? files = null)
    {
        var data = new DataObject();
        if (payload is not null)
        {
            data.SetData(DataGridRowsFormat, payload);
        }

        if (!string.IsNullOrEmpty(text))
        {
            data.SetData(DataFormats.UnicodeText, text);
        }

        if (files is { Length: > 0 })
        {
            data.SetData(DataFormats.FileDrop, files);
        }

        return data;
    }

    /// <summary>Reads an in-process payload of <typeparamref name="T"/> from a data object.</summary>
    /// <param name="data">The drag data.</param>
    /// <param name="format">The custom format name.</param>
    /// <param name="value">The payload when present and of the right type.</param>
    public static bool TryGetData<T>(IDataObject data, string format, out T? value)
    {
        value = default;
        if (data is null || string.IsNullOrEmpty(format) || !data.GetDataPresent(format))
        {
            return false;
        }

        if (data.GetData(format) is T typed)
        {
            value = typed;
            return true;
        }

        return false;
    }

    /// <summary>Reads the dropped file paths; empty when the drag carries no files.</summary>
    /// <param name="data">The drag data.</param>
    public static string[] GetFiles(IDataObject? data)
    {
        if (data is null || !data.GetDataPresent(DataFormats.FileDrop))
        {
            return Array.Empty<string>();
        }

        return data.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>();
    }

    /// <summary>Reads the dropped text; empty when the drag carries no text.</summary>
    /// <param name="data">The drag data.</param>
    public static string? GetText(IDataObject? data)
    {
        if (data is null)
        {
            return null;
        }

        if (data.GetDataPresent(DataFormats.UnicodeText))
        {
            return data.GetData(DataFormats.UnicodeText) as string;
        }

        return data.GetDataPresent(DataFormats.Text)
            ? data.GetData(DataFormats.Text) as string
            : null;
    }

    /// <summary>The standard <c>Move</c>-or-<c>Copy</c> choice for a row drag.</summary>
    /// <param name="allowed">The effects the source permits.</param>
    /// <param name="copy">Whether a copy is wanted when both effects are allowed.</param>
    public static DragDropEffects ResolveRowEffect(DragDropEffects allowed, bool copy = false)
    {
        if (copy && (allowed & DragDropEffects.Copy) != 0)
        {
            return DragDropEffects.Copy;
        }

        if ((allowed & DragDropEffects.Move) != 0)
        {
            return DragDropEffects.Move;
        }

        if ((allowed & DragDropEffects.Copy) != 0)
        {
            return DragDropEffects.Copy;
        }

        return DragDropEffects.None;
    }
}

/// <summary>The floating label that follows the pointer while a drag started inside the app is running.</summary>
internal sealed class VisualDragGhost : VisualElement
{
    private const float PadX = 10f;
    private const float PadY = 6f;

    internal VisualDragGhost(string text)
    {
        Text = text ?? string.Empty;
        IsHitTestVisible = false;
        Opacity = 0.96d;
        FontSize = 9.5d;
    }

    internal string Text { get; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var measured = VisualText.Measure(Text, ResolveFont(DefaultFont), 0f, wrap: false, RenderDpi);
        return new Size(
            (int)Math.Ceiling(measured.Width + (PadX * 2f) + 4f),
            (int)Math.Ceiling(measured.Height + (PadY * 2f)));
    }

    protected override void OnRender(Graphics graphics)
    {
        var theme = Theme.Current;
        var bounds = new RectangleF(0f, 0f, Bounds.Width, Bounds.Height);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
        {
            return;
        }

        using (var shadow = XamlDrawing.RoundedRectangle(
                   new RectangleF(bounds.X + 1f, bounds.Y + 3f, bounds.Width, bounds.Height),
                   7f))
        using (var shadowBrush = new SolidBrush(Color.FromArgb(70, theme.ShadowColor)))
        {
            graphics.FillPath(shadowBrush, shadow);
        }

        using (var path = XamlDrawing.RoundedRectangle(bounds, 6f))
        using (var fill = new SolidBrush(theme.Surface))
        using (var stroke = new Pen(theme.Accent, 1.2f))
        {
            graphics.FillPath(fill, path);
            graphics.DrawPath(stroke, path);
        }

        VisualText.Draw(
            this,
            graphics,
            Text,
            ResolveFont(DefaultFont),
            theme.Foreground,
            new RectangleF(bounds.X + PadX, bounds.Y, bounds.Width - (PadX * 2f), bounds.Height),
            wrap: false,
            StringAlignment.Near,
            StringAlignment.Center);
    }
}
