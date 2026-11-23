using Easel.Visuals.Controls;

namespace Demo.Views;

/// <summary>The popup card; the body is a XAML view, the popup is hosted by the surface overlay.</summary>
public sealed partial class PopupCard : ThemedStackPanel
{
    private VisualPopup? _popup;

    /// <summary>Creates the view and runs the generated initializer.</summary>
    public PopupCard() => InitializeComponent();

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
            PlacementTarget = showButton,
            Placement = VisualPlacement.Bottom,
            Child = new PopupBodyView(),
        };
        _popup.Opened += (_, _) => popupStatus.Text = "Popup open";
        _popup.Closed += (_, _) => popupStatus.Text = "Popup closed";
        _popup.IsOpen = true;
    }
}
