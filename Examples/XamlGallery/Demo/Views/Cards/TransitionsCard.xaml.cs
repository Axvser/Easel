using Easel;
using Easel.Visuals;
using XamlBrush = Easel.Brush;

namespace Demo.Views;

/// <summary>The element-transition card: hovering the border changes Background and CornerRadius through its Transitions.</summary>
public sealed partial class TransitionsCard : ThemedStackPanel
{
    /// <summary>Creates the view and runs the generated initializer.</summary>
    public TransitionsCard() => InitializeComponent();

    private void OnPanelPointerEntered(object? sender, VisualPointerEventArgs e)
    {
        panel.Background = Lookup("Accent");
        panel.CornerRadius = 28f;
    }

    private void OnPanelPointerExited(object? sender, VisualPointerEventArgs e)
    {
        panel.Background = Lookup("SurfaceAlt");
        panel.CornerRadius = 10f;
    }

    private XamlBrush Lookup(string key)
        => Resources.TryGetValue(key, out var value) && value is XamlBrush brush
            ? brush
            : new SolidColorBrush(key == "Accent" ? Theme.Current.Accent : Theme.Current.SurfaceAlt);
}
