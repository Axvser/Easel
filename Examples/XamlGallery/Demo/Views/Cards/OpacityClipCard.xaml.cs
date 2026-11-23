using System.Drawing;

namespace Demo.Views.Cards;

/// <summary>The opacity-and-clip card; the clip rectangle is assigned because it has no XAML literal.</summary>
public sealed partial class OpacityClipCard : ThemedStackPanel
{
    /// <summary>Creates the view and clips the 240-pixel border to 180 pixels.</summary>
    public OpacityClipCard()
    {
        InitializeComponent();
        clipBorder.Clip = new RectangleF(0f, 0f, 180f, 80f);
    }
}
