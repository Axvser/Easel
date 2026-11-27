using System.Drawing;
using System.Windows.Forms;
using XamlThickness = Easel.Controls.Thickness;

namespace Easel.Visuals.Controls;

/// <summary>A fixed-extent, single-axis virtualizing stack — the concrete default for <see cref="VirtualizingPanel"/>.</summary>
/// <remarks>
/// Items share <see cref="ItemExtent"/> (plus <see cref="Spacing"/>) along <see cref="Orientation"/>. The visible
/// index range is plain division; the base engine handles realization, recycling and pooling. Use this control
/// directly for a one-dimensional virtual list; inherit it only to change the row chrome or its binding.
/// </remarks>
public class VirtualizingStackPanel : VirtualizingPanel
{
    /// <summary>The fixed size of one item along <see cref="Orientation"/>, in pixels.</summary>
    public static readonly DependencyProperty ItemExtentProperty =
        DependencyProperty.Register(
            nameof(ItemExtent), typeof(double), typeof(VirtualizingStackPanel),
            new PropertyMetadata(24d) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>Whether items stack vertically (default) or horizontally.</summary>
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation), typeof(Orientation), typeof(VirtualizingStackPanel),
            new PropertyMetadata(Orientation.Vertical) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>The gap between adjacent items along the stack axis.</summary>
    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.Register(
            nameof(Spacing), typeof(double), typeof(VirtualizingStackPanel),
            new PropertyMetadata(0d) { AffectsMeasure = true, AffectsRender = false });

    /// <summary>Creates an empty vertical stack.</summary>
    public VirtualizingStackPanel()
    {
    }

    /// <summary>The fixed size of one item along <see cref="Orientation"/>, in pixels.</summary>
    public virtual double ItemExtent
    {
        get => (double)GetValue(ItemExtentProperty)!;
        set => SetValue(ItemExtentProperty, value);
    }

    /// <summary>Whether items stack vertically (default) or horizontally.</summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty)!;
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>The gap between adjacent items along the stack axis.</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty)!;
        set => SetValue(SpacingProperty, value);
    }

    /// <inheritdoc />
    protected override double VerticalLineStep => Step;

    /// <inheritdoc />
    protected override double HorizontalLineStep => Step;

    /// <inheritdoc />
    protected override Size MeasureLayout(Size availableSize)
    {
        var padding = Padding;
        var count = SourceItems.Count;
        var total = TotalExtent(count);
        return Orientation == Orientation.Vertical
            ? new Size(
                availableSize.Width >= int.MaxValue / 2 ? 0 : Math.Max(0, availableSize.Width),
                (int)Math.Ceiling(total + padding.Vertical))
            : new Size(
                (int)Math.Ceiling(total + padding.Horizontal),
                availableSize.Height >= int.MaxValue / 2 ? 0 : Math.Max(0, availableSize.Height));
    }

    /// <inheritdoc />
    protected override VirtualizationLayoutContext BuildLayout(VirtualizationLayoutRequest request)
    {
        var vertical = Orientation == Orientation.Vertical;
        var step = Step;
        var count = SourceItems.Count;
        var host = request.Host;
        var padding = request.Padding;
        var cross = vertical
            ? (host is not null
                ? Math.Max(0f, host.Bounds.Width - host.Padding.Horizontal)
                : Math.Max(0f, request.FinalSize.Width - padding.Horizontal))
            : (host is not null
                ? Math.Max(0f, host.Bounds.Height - host.Padding.Vertical)
                : Math.Max(0f, request.FinalSize.Height - padding.Vertical));
        var crossLead = vertical
            ? (host is not null ? host.Padding.Left : padding.Left)
            : (host is not null ? host.Padding.Top : padding.Top);
        var origin = host is not null
            ? (vertical
                ? ResolveHostLead(host, vertical: true) + host.Padding.Top
                : ResolveHostLead(host, vertical: false) + host.Padding.Left)
            : (vertical ? padding.Top : padding.Left);
        var offset = vertical ? request.OffsetY : request.OffsetX;
        var viewport = vertical ? request.Viewport.Height : request.Viewport.Width;
        var visible = Math.Max((float)step, viewport);

        var first = 0;
        var last = 0;
        if (count > 0)
        {
            var start = Math.Max(0f, offset - origin);
            first = (int)Math.Floor(start / step);
            last = (int)Math.Ceiling((start + visible) / step);
            first = XamlMath.Clamp(first, 0, count);
            last = XamlMath.Clamp(last, first, count);
        }

        var items = new List<VirtualizationItemLayout>(Math.Max(0, last - first));
        var itemSize = (float)Math.Max(1d, ItemExtent);
        for (var index = first; index < last; index++)
        {
            var lead = (host is not null
                ? (vertical ? host.Padding.Top : host.Padding.Left)
                : (vertical ? padding.Top : padding.Left)) + (index * step);
            items.Add(vertical
                ? new VirtualizationItemLayout(index, new RectangleF(crossLead, (float)lead, cross, itemSize))
                : new VirtualizationItemLayout(index, new RectangleF((float)lead, crossLead, itemSize, cross)));
        }

        var total = TotalExtent(count);
        var extent = vertical
            ? new Size((int)Math.Ceiling(cross + padding.Horizontal), (int)Math.Ceiling(total + padding.Vertical))
            : new Size((int)Math.Ceiling(total + padding.Horizontal), (int)Math.Ceiling(cross + padding.Vertical));
        var viewportRect = new RectangleF(request.OffsetX, request.OffsetY, request.Viewport.Width, request.Viewport.Height);
        return new VirtualizationLayoutContext(items, extent, viewportRect, request.OffsetX, request.OffsetY, count);
    }

    private double Step => Math.Max(1d, ItemExtent) + Math.Max(0d, Spacing);

    private double TotalExtent(int count)
        => count <= 0 ? 0d : (Math.Max(1d, ItemExtent) * count) + (Math.Max(0d, Spacing) * (count - 1d));

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (ReferenceEquals(e.Property, ItemExtentProperty)
            || ReferenceEquals(e.Property, OrientationProperty)
            || ReferenceEquals(e.Property, SpacingProperty))
        {
            InvalidateRealizedWindow();
            InvalidateMeasure();
        }
    }
}
