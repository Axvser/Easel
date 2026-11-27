using System.Drawing;
using XamlThickness = Easel.Controls.Thickness;

namespace Easel.Visuals.Controls;

/// <summary>A pluggable layout for <see cref="VirtualizingLayoutPanel"/>.</summary>
/// <remarks>
/// Implement this instead of deriving from <see cref="VirtualizingPanel"/> when the only difference is how
/// items are placed. The engine still owns realization, recycling, pooling and host ordering.
/// </remarks>
public interface IVirtualizingLayout
{
    /// <summary>Measures the full document extent.</summary>
    /// <param name="availableSize">The available panel size.</param>
    /// <param name="itemCount">The source item count.</param>
    /// <param name="padding">The panel padding.</param>
    Size Measure(Size availableSize, int itemCount, XamlThickness padding);

    /// <summary>Builds the realized window and its layout records.</summary>
    /// <param name="request">The viewport, offsets and host.</param>
    VirtualizationLayoutContext Build(VirtualizationLayoutRequest request);
}

/// <summary>Adapts two delegates to <see cref="IVirtualizingLayout"/>, so a one-off layout needs no new class.</summary>
public sealed class DelegateVirtualizingLayout : IVirtualizingLayout
{
    private readonly Func<Size, int, XamlThickness, Size> _measure;
    private readonly Func<VirtualizationLayoutRequest, VirtualizationLayoutContext> _build;

    /// <summary>Creates a layout from its measure and build delegates.</summary>
    /// <param name="measure">Measures the full document extent.</param>
    /// <param name="build">Builds the realized window for one arrange pass.</param>
    public DelegateVirtualizingLayout(
        Func<Size, int, XamlThickness, Size> measure,
        Func<VirtualizationLayoutRequest, VirtualizationLayoutContext> build)
    {
        _measure = measure ?? throw new ArgumentNullException(nameof(measure));
        _build = build ?? throw new ArgumentNullException(nameof(build));
    }

    /// <inheritdoc />
    public Size Measure(Size availableSize, int itemCount, XamlThickness padding)
        => _measure(availableSize, itemCount, padding);

    /// <inheritdoc />
    public VirtualizationLayoutContext Build(VirtualizationLayoutRequest request)
        => _build(request);
}

/// <summary>A concrete <see cref="VirtualizingPanel"/> whose layout is supplied as an <see cref="IVirtualizingLayout"/>.</summary>
/// <remarks>
/// Use the shipped concrete panels for stack, wrap and uniform-grid layouts. Use this panel when the layout is
/// application-specific: assign a <see cref="DelegateVirtualizingLayout"/> or an <see cref="IVirtualizingLayout"/>
/// implementation instead of overriding <c>BuildLayout</c> / <c>MeasureLayout</c>.
/// </remarks>
public class VirtualizingLayoutPanel : VirtualizingPanel
{
    private IVirtualizingLayout? _layout;

    /// <summary>The layout used by this panel; it must be set before the first measure.</summary>
    public IVirtualizingLayout? Layout
    {
        get => _layout;
        set
        {
            if (ReferenceEquals(_layout, value))
            {
                return;
            }

            _layout = value;
            InvalidateRealizedWindow();
            InvalidateMeasure();
            InvalidateArrange();
        }
    }

    /// <inheritdoc />
    protected override Size MeasureLayout(Size availableSize)
    {
        if (_layout is not { } layout)
        {
            throw new InvalidOperationException(
                "VirtualizingLayoutPanel.Layout must be set before the panel is measured.");
        }

        return layout.Measure(availableSize, SourceItems.Count, Padding);
    }

    /// <inheritdoc />
    protected override VirtualizationLayoutContext BuildLayout(VirtualizationLayoutRequest request)
    {
        if (_layout is not { } layout)
        {
            throw new InvalidOperationException(
                "VirtualizingLayoutPanel.Layout must be set before the panel is arranged.");
        }

        return layout.Build(request);
    }
}
