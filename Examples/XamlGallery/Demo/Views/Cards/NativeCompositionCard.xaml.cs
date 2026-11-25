using System.Drawing;
using System.Drawing.Drawing2D;
using Easel.Visuals.Controls;

namespace Demo.Views;

/// <summary>Corner radius, snapshot opacity/rotation and a popup that cuts through a native island.</summary>
public sealed partial class NativeCompositionCard : ThemedStackPanel
{
    private VisualPopup? _popup;

    /// <summary>Creates the view, runs the generated initializer and tilts the snapshot button.</summary>
    public NativeCompositionCard()
    {
        InitializeComponent();

        // 快照模式把控件画成位图，Transform 才真正生效；绕自身中心转 16°。
        var matrix = new Matrix();
        matrix.RotateAt(16f, new PointF(100f, 16f));
        rotated.Transform = matrix;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _popup?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnShowPopupClick(object? sender, EventArgs e)
    {
        if (_popup is { IsOpen: true })
        {
            _popup.Close();
            return;
        }

        _popup?.Dispose();
        _popup = new VisualPopup
        {
            PlacementTarget = island,
            Placement = VisualPlacement.Top,
            // 从岛上方向下偏移，让弹层压在原生岛的内容区上；岛的窗口区域会被挖掉。
            VerticalOffset = 110,
            Child = new NativePopupBody(),
        };
        _popup.IsOpen = true;
    }
}
