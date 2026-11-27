using Easel;
using Easel.Visuals.Controls;
using Easel.Visuals;
using XamlBrush = Easel.Brush;

namespace IndustrialWorkbench.Controls;

/// <summary>A composite status card authored in XAML: a lamp, a title, a value and a caption.</summary>
/// <remarks>
/// This is the XAML + code-behind path: the visuals live in <c>StatusTile.xaml</c>, the dependency properties here.
/// The base visual object already raises <see cref="System.ComponentModel.INotifyPropertyChanged"/> when a dependency
/// property changes, so the bindings inside the XAML follow the tile without extra plumbing.
/// </remarks>
public sealed partial class StatusTile : UserControl
{
    /// <summary>The card title.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(StatusTile),
        new PropertyMetadata("STATUS") { AffectsRender = false });

    /// <summary>The large value string.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(StatusTile),
        new PropertyMetadata("--") { AffectsRender = false });

    /// <summary>The small caption under the value.</summary>
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption), typeof(string), typeof(StatusTile),
        new PropertyMetadata(string.Empty) { AffectsRender = false });

    /// <summary>Whether the indicator lamp is lit.</summary>
    public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
        nameof(IsOn), typeof(bool), typeof(StatusTile),
        new PropertyMetadata(true) { AffectsRender = false });

    /// <summary>The indicator lamp colour.</summary>
    public static readonly DependencyProperty LampBrushProperty = DependencyProperty.Register(
        nameof(LampBrush), typeof(XamlBrush), typeof(StatusTile),
        new PropertyMetadata(null) { AffectsRender = false });

    /// <summary>Creates the card and binds its inner content to this instance.</summary>
    public StatusTile()
    {
        InitializeComponent();
        // 内层子树的上下文指向控件自身，外部给 StatusTile 的绑定仍走继承的 ViewModel 上下文。
        SelfHost.DataContext = this;
    }

    /// <summary>The card title.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value ?? string.Empty);
    }

    /// <summary>The large value string.</summary>
    public string Value
    {
        get => (string)GetValue(ValueProperty)!;
        set => SetValue(ValueProperty, value ?? string.Empty);
    }

    /// <summary>The small caption under the value.</summary>
    public string Caption
    {
        get => (string)GetValue(CaptionProperty)!;
        set => SetValue(CaptionProperty, value ?? string.Empty);
    }

    /// <summary>Whether the indicator lamp is lit.</summary>
    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty)!;
        set => SetValue(IsOnProperty, value);
    }

    /// <summary>The indicator lamp colour.</summary>
    public XamlBrush? LampBrush
    {
        get => (XamlBrush?)GetValue(LampBrushProperty);
        set => SetValue(LampBrushProperty, value);
    }
}
