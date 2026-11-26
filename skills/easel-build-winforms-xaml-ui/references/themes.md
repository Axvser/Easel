# Themes and resource dictionaries

Easel controls have a default palette; an application can switch it, follow Windows, or provide its own
`ResourceDictionary`. Two surfaces read the theme: the host's theme resource dictionary (the built-in styles use
`{DynamicResource}` keys) and `Theme.Current` (self-drawn chrome and control metrics). A complete custom theme
usually sets both.

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
- `Theme.Current` can be assigned a `ThemePalette` directly; like `ApplyPalette`, that changes palette reads, not
  the host's resource dictionary.
- `Theme.ApplyPalette(ThemeMode mode, ThemePalette palette)` activates a host-defined palette and reports the
  given mode. It changes `Theme.Current`, not the host's resource dictionary.
- `Theme.RefreshSystemTheme()` re-reads Windows when `Mode == System`;
  `Theme.SystemThemeResolver` is internal test seam.
- `Theme.Mode = ThemeMode.System` attaches a `SystemEvents.UserPreferenceChanged` listener once. The event is
  posted to the `SynchronizationContext` captured when the mode was selected, so a UI host receives the change on
  its UI thread.

`ThemeMode.System` reads
`HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` (with
`SystemInformation.HighContrast` taking precedence). Windows high contrast while following resolves to
`HighContrast`.

## Built-in theme dictionaries

The built-in Dark+ and Light+ palettes are **XAML `ResourceDictionary` files in the library**:

- [`Src/Easel/Themes/DarkTheme.xaml`](../../../Src/Easel/Themes/DarkTheme.xaml)
- [`Src/Easel/Themes/LightTheme.xaml`](../../../Src/Easel/Themes/LightTheme.xaml)

`Caret` is the default text-field caret colour; a dictionary that omits it falls back to `Focus`.

The host merges the active theme dictionary into `VisualHost.Resources` in its constructor (it is the first entry of
`MergedDictionaries`), so a control template can use `{DynamicResource Accent}` and other theme keys directly. On a
theme switch the host syncs that dictionary's brushes and metrics in place. Built-in fallback control styles live in
`Src/Easel/Themes/DefaultStyles.xaml`; they are template-based defaults keyed by control type and apply when the
control has no explicit style.

They are the source of truth for the current role names and values. They carry palette brushes, metrics
(`CornerRadius`, `ControlHeight`, typography tokens where present) and keyed typography styles
(`Heading`, `Subheading`, `Body`, `Caption`). `Theme.CreateDark()` / `CreateLight()` read the files into a
`ThemePalette`.

`HighContrast` is a built-in palette mode but it is **not** a library XAML fallback; its resource dictionary is
generated from the palette when an app asks for it. A custom high-contrast theme is a good demo of authoring
your own dictionary.

Get a copy when an application wants its own dictionary to edit or merge:

```csharp
ResourceDictionary resources = Theme.CreateResourceDictionary(Theme.EffectiveMode);
```

The copy contains the palette brushes, metrics and typography styles. It is a snapshot with new brush instances: it
does not follow later theme switches. `Theme.CreateResourceDictionary` for `ThemeMode.System` uses `EffectiveMode`.

## Using theme resources in XAML

The host merges the active built-in dictionary into `VisualHost.Resources` in its constructor, and a view resolves
through that dictionary once it is parented. A view does not need to copy the theme into its own `Resources`:

```xml
<Border Background="{StaticResource Surface}"
        BorderBrush="{StaticResource Border}">
  <TextBlock Text="Title" Style="{StaticResource Heading}" />
</Border>
```

- `{StaticResource}` on a visual property resolves once, as soon as the view's resource chain contains the key;
  the generated code retries as the chain grows. Keys on the view or an ancestor win over the host; the host's own
  keys win over its merged dictionaries.
- `{DynamicResource}` stays live; see [xaml.md](xaml.md#static-vs-dynamic).
- Do not use a theme brush on a property that expects a `Color` CLR value unless the DP is brush-backed. The
  common text properties (`TextBlock.Foreground`, `Button.Foreground`, etc.) are brush-backed and accept a
  theme brush or gradient.

## Custom themes

### Partial overlay on the built-in dictionary

The host's theme dictionary is `MergedDictionaries[0]` on `VisualHost.Resources`. A custom theme can be **partial**:
append a dictionary after it and define only the keys you change. Missing keys fall back to the active theme.

```csharp
var overrides = new ResourceDictionary
{
    ["Accent"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x8A, 0x00)),
    ["Focus"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x8A, 0x00)),
    ["Surface"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x1A, 0x1F, 0x24)),
};
surface.Resources.MergedDictionaries.Add(overrides); // appended after the theme dictionary
```

- Lookup checks a dictionary's own keys first, then its `MergedDictionaries` last-added first. Keys stored directly
  on `surface.Resources` (or on a view's `Resources`) win over every merge; an overlay appended after the host's
  theme wins over the theme, while one inserted before it loses.
- A key the overlay omits keeps resolving from the active built-in theme, so a partial dictionary is enough.
- The host syncs its theme dictionary in place on a `Theme.Mode` change. Overridden keys stay at the overlay's
  value; to make them follow the mode, replace or update the overlay's values (or swap the dictionary) from
  `Theme.Changed`.
- `{StaticResource}` on a visual resolves once, so add the overlay before building views that read it, or use
  `{DynamicResource}` for keys that must change later.
- The overlay changes resource lookups. Built-in styles read theme keys with `{DynamicResource}`, so default
  control chrome follows the overlay. Most control metrics and self-drawn chrome read `Theme.Current`, not the
  resource dictionary; call `Theme.ApplyPalette` (or assign `Theme.Current`) as well when those should change.

### Full palette

`ThemePalette.FromResourceDictionary(dictionary)` reads the standard keys directly. Unlike a merged overlay, a key
missing from that dictionary becomes `Color.Empty` or the palette's fallback metric, not the built-in value; start
from [`DarkTheme.xaml`](../../../Src/Easel/Themes/DarkTheme.xaml) when building a complete palette this way.

```csharp
var custom = new MyApp.Themes.MyTheme(); // complete standalone dictionary
Theme.ApplyPalette(ThemeMode.Dark, ThemePalette.FromResourceDictionary(custom));
```

`Theme.ApplyPalette` changes `Theme.Current` immediately, so existing controls re-read its colors and metrics, but
it does not rewrite `VisualHost.Resources`. Merge a dictionary as above when templates and `{DynamicResource}` keys
should use the custom colors too.

### Per-view copies

A view does not need its own theme copy. `Theme.CreateResourceDictionary(...)` returns a snapshot whose brushes are
new instances; a copy merged into a view outranks the host and is not updated by a theme switch. Merge a dictionary
into a view only for view-local overrides or decoration (the gallery's `Themed*` bases do this for its custom
high-contrast theme and gradient brushes), and refresh that copy yourself when the palette changes.

## Typography

`ThemePalette` carries font family, size and weight, scroll-bar thumb colors/radius, shape stroke and
shadow defaults, control border/focus thicknesses and the existing layout metrics. The built-in dictionaries
define `Heading`/`Subheading`/`Body`/`Caption` styles. A control's default text reads the palette's font
tokens; a `Style` setter reads explicit values. `x:String`, `x:Double`, `x:FontWeight`, `x:Thickness`,
`x:PointF` and the other scalar resource types can carry those tokens in a custom dictionary. Use styles for
authored headings and body text, and set `FontFamily`/`FontSize`/`FontWeight` locally only when a card
intentionally departs from the theme.

## Switching at runtime

`Theme.Mode`, assigning `Theme.Current` and `Theme.ApplyPalette(...)` raise `Theme.Changed`. The host syncs its theme
dictionary brushes in place and invalidates the surface, so an ordinary switch needs no root rebuild:

```csharp
Theme.Changed += (_, _) => surface.BackColor = Theme.Current.WindowBackground;
```

- Existing controls re-read `Theme.Current`; templates that bound the host's theme brushes change with them.
- Recreate (or refresh) a view only when it captured `Theme.Current` colors at construction or merged its own
  cloned theme dictionary. The gallery does this for a few previews and its `GalleryTheme` copies.
- Native islands (`NativeElement`/`NativeControlHost`, and a `TextBox` with `UseNativeEditor=true`) choose their
  native color mode when handles are created. Call `Application.SetColorMode` for new handles; changing it late
  does not repaint existing islands.
- `Theme.Changed` can arrive on the UI context for system-follow; still marshal if your host mutates controls
  from another thread.
- `Theme.Current` is a shared mutable palette object. Treat it as read-only in views; replace the palette rather
  than mutating a color in place (an in-place color edit raises no change notification).

## Diagnostics

- A missing `{StaticResource}` on a visual property leaves the value unset and the binding waits for the chain; it
  does not throw. An eager read (style `BasedOn` or setter value, a non-visual object property, a trigger-action
  property, a dictionary-internal reference) throws `No resource named …`.
- `ThemePalette.FromResourceDictionary` with a dictionary missing keys yields transparent colors/fallback metrics;
  a merged partial overlay instead falls back to the active theme.
- A custom dictionary with an implicit `<Style TargetType="…">` and no `x:Key` is stored by type in the
  dictionary but is not automatically applied; declare implicit styles on a visual element's resources/styles.
- `MergedDictionaries` has no file-URI `Source`; merge a compiled `ResourceDictionary` type or inline dictionary.
