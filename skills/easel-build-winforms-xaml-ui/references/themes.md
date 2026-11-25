# Themes and resource dictionaries

Easel controls have a default palette; an application can switch it, follow Windows, or provide its own
`ResourceDictionary`. The same values drive self-drawn chrome and XAML `{StaticResource}` lookups.

## Modes and the active palette

```csharp
Theme.Mode = ThemeMode.Dark;        // Dark+
Theme.Mode = ThemeMode.Light;       // Light+
Theme.Mode = ThemeMode.HighContrast;
Theme.Mode = ThemeMode.System;      // follow Windows
var effective = Theme.EffectiveMode; // Dark / Light / HighContrast (System resolved)
var palette = Theme.Current;        // the palette controls read now
```

- `Theme.Changed` is raised when the mode, palette or followed system theme changes.
- `Theme.Current` can be assigned a `ThemePalette` directly.
- `Theme.ApplyPalette(ThemeMode mode, ThemePalette palette)` activates a host-defined palette and reports the
  given mode. Use it for custom themes.
- `Theme.RefreshSystemTheme()` re-reads Windows when `Mode == System`;
  `Theme.SystemThemeResolver` is internal test seam.
- `Theme.Mode = ThemeMode.System` attaches a `SystemEvents.UserPreferenceChanged` listener once. The event is
  posted to the `SynchronizationContext` captured when the mode was selected, so a UI host rebuilds on its UI
  thread.

`ThemeMode.System` reads
`HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` (with
`SystemInformation.HighContrast` taking precedence). Windows high contrast while following resolves to
`HighContrast`.

## Built-in theme dictionaries

The built-in Dark+ and Light+ palettes are **XAML `ResourceDictionary` files in the library**:

- [`Src/Easel/Themes/DarkTheme.xaml`](../../../Src/Easel/Themes/DarkTheme.xaml)
- [`Src/Easel/Themes/LightTheme.xaml`](../../../Src/Easel/Themes/LightTheme.xaml)

They are the source of truth for the current role names and values. They carry palette brushes, metrics
(`CornerRadius`, `ControlHeight`, typography tokens where present) and keyed typography styles
(`Heading`, `Subheading`, `Body`, `Caption`). `Theme.CreateDark()` / `CreateLight()` read the files into a
`ThemePalette`.

`HighContrast` is a built-in palette mode but it is **not** a library XAML fallback; its resource dictionary is
generated from the palette when an app asks for it. A custom high-contrast theme is a good demo of authoring
your own dictionary.

Get a copy for an application's resources:

```csharp
ResourceDictionary resources = Theme.CreateResourceDictionary(Theme.EffectiveMode);
```

The copy contains the palette brushes, metrics and typography styles. `Theme.CreateResourceDictionary` for
`ThemeMode.System` uses `EffectiveMode`.

## Using theme resources in XAML

A themed base class merges the dictionary into each view before `InitializeComponent` (the gallery's
`Themed*` bases do this through `GalleryTheme.Fill`):

```csharp
protected ThemedStackPanel()
{
    var theme = Theme.CreateResourceDictionary(Theme.EffectiveMode);
    Resources.MergedDictionaries.Add(theme);
}
```

```xml
<Border Background="{StaticResource Surface}"
        BorderBrush="{StaticResource Border}">
  <TextBlock Text="Title" Style="{StaticResource Heading}" />
</Border>
```

- `{StaticResource}` resolves at construction through the view's resources, ancestors and
  `VisualHost.Resources`. Add the theme dictionary before the view's XAML runs.
- `{DynamicResource}` stays live; see [xaml.md](xaml.md#static-vs-dynamic).
- Do not use a theme brush on a property that expects a `Color` CLR value unless the DP is brush-backed. The
  common text properties (`TextBlock.Foreground`, `Button.Foreground`, etc.) are brush-backed and accept a
  theme brush or gradient.

## Authoring a custom theme

A standalone dictionary with `x:Class`:

```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    x:Class="MyApp.Themes.MyTheme">

  <SolidColorBrush x:Key="Window" Color="#FF101418" />
  <SolidColorBrush x:Key="Surface" Color="#FF182028" />
  <SolidColorBrush x:Key="Raised" Color="#FF202A34" />
  <SolidColorBrush x:Key="Input" Color="#FF182028" />
  <SolidColorBrush x:Key="Border" Color="#FF30404C" />
  <SolidColorBrush x:Key="Hover" Color="#FF25313C" />
  <SolidColorBrush x:Key="Pressed" Color="#FF30404C" />
  <SolidColorBrush x:Key="Text" Color="#FFEAF2F6" />
  <SolidColorBrush x:Key="Muted" Color="#FF9FB0BA" />
  <SolidColorBrush x:Key="Accent" Color="#FF29B6F6" />
  <SolidColorBrush x:Key="AccentText" Color="#FF06121A" />
  <SolidColorBrush x:Key="Focus" Color="#FF29B6F6" />
  <SolidColorBrush x:Key="Selection" Color="#FF0D3A50" />
  <SolidColorBrush x:Key="SelectedText" Color="#FFFFFFFF" />
  <SolidColorBrush x:Key="Disabled" Color="#FF141A1F" />
  <SolidColorBrush x:Key="DisabledText" Color="#FF5A6A74" />
  <SolidColorBrush x:Key="ScrollBarThumb" Color="#66A0A0A0" />
  <SolidColorBrush x:Key="ScrollBarThumbHover" Color="#CCE7E7E7" />
  <SolidColorBrush x:Key="ShapeStroke" Color="#885B8CFF" />
  <SolidColorBrush x:Key="ShadowColor" Color="#FF000000" />
  <SolidColorBrush x:Key="TextVisualForeground" Color="#FFFFFFFF" />

  <x:String x:Key="FontFamily">Segoe UI</x:String>
  <x:Double x:Key="FontSize">9.5</x:Double>
  <x:FontWeight x:Key="FontWeight">Normal</x:FontWeight>
  <x:String x:Key="TextVisualFontFamily">Segoe UI</x:String>
  <x:Double x:Key="TextVisualFontSize">9</x:Double>
  <x:FontWeight x:Key="TextVisualFontWeight">Normal</x:FontWeight>
  <x:Double x:Key="CornerRadius">6</x:Double>
  <x:Int32 x:Key="ControlHeight">30</x:Int32>
  <x:Thickness x:Key="ControlBorderThickness">1</x:Thickness>
  <x:Double x:Key="FocusThickness">1.6</x:Double>
  <x:Double x:Key="FocusThicknessMedium">1.4</x:Double>
  <x:Double x:Key="FocusThicknessThin">1</x:Double>
  <x:Double x:Key="ScrollBarThumbRadius">3</x:Double>
  <x:Double x:Key="ShapeStrokeThickness">1.5</x:Double>
  <x:PointF x:Key="ShadowOffset">4,6</x:PointF>
  <x:Double x:Key="ShadowBlur">6</x:Double>
  <x:Double x:Key="ShadowOpacity">0.45</x:Double>

  <Style x:Key="Heading" TargetType="TextBlock">
    <Style.Setters>
      <Setter Property="FontFamily" Value="Segoe UI" />
      <Setter Property="FontSize" Value="19" />
      <Setter Property="FontWeight" Value="SemiBold" />
      <Setter Property="Foreground" Value="{StaticResource Text}" />
    </Style.Setters>
  </Style>
</ResourceDictionary>
```

Apply it to the whole app:

```csharp
var custom = new MyApp.Themes.MyTheme();
Theme.ApplyPalette(ThemeMode.Dark, ThemePalette.FromResourceDictionary(custom));
// views built after this merge/copy the dictionary; rebuild the root if it is already on screen
```

`ThemePalette.FromResourceDictionary(dictionary)` reads the standard theme keys. To use a custom dictionary for
view resources, merge it (as the gallery's high-contrast demo does) or copy it into each view.

**Required keys** are the ones the current built-in dictionaries define. Copy the exact key set from
[`DarkTheme.xaml`](../../../Src/Easel/Themes/DarkTheme.xaml) when starting a custom theme; missing keys resolve to
transparent/fallback metrics in `FromResourceDictionary` and will make some controls look wrong.

## Typography

`ThemePalette` carries font family, size and weight, scroll-bar thumb colors/radius, shape stroke and
shadow defaults, control border/focus thicknesses and the existing layout metrics. The built-in dictionaries
define `Heading`/`Subheading`/`Body`/`Caption` styles. A control's default text reads the palette's font
tokens; a `Style` setter reads explicit values. `x:String`, `x:Double`, `x:FontWeight`, `x:Thickness`,
`x:PointF` and the other scalar resource types can carry those tokens in a custom dictionary. Use styles for
authored headings and body text, and set `FontFamily`/`FontSize`/`FontWeight` locally only when a card
intentionally departs from the theme.

## Switching at runtime

Controls read `Theme.Current` when they paint/construct; a live theme switch should rebuild the visual root:

```csharp
Theme.Changed += (_, _) =>
{
    surface.BackColor = Theme.Current.WindowBackground;
    surface.Root = new MyShellView(); // or rebuild the current view
};
```

- Apply the theme to the host and rebuild views so every control reads the new palette.
- Native islands (focused `TextBox` editor, `NativeElement`/`NativeControlHost`) choose their native color mode when handles
  are created. Call `Application.SetColorMode` for new handles; changing it late does not repaint existing
  islands.
- `Theme.Changed` can arrive on the UI context for system-follow; still marshal if your host mutates controls
  from another thread.
- `Theme.Current` is a shared mutable palette object. Treat it as read-only in views; replace the palette (or
  rebuild) rather than mutating a color in place.

## Diagnostics

- A missing `{StaticResource}` throws at `InitializeComponent` (`No resource named …`). Add the key to the
  theme dictionary or the view's resources before that point.
- `ThemePalette.FromResourceDictionary` with a dictionary missing keys yields transparent colors; copy the
  built-in key set.
- A custom dictionary with an implicit `<Style TargetType="…">` and no `x:Key` is stored by type in the
  dictionary but is not automatically applied; declare implicit styles on a visual element's resources/styles.
- `MergedDictionaries` has no file-URI `Source`; merge a compiled `ResourceDictionary` type or inline dictionary.
