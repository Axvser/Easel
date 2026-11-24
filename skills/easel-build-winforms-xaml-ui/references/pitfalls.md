# Pitfalls and diagnostics

Each entry is a failure that Easel's own runtime, tests or gallery has hit. Read the symptom first.

## XAML and code-behind

**`EASEL_XAML006` / view does nothing.** The `x:Class` type must exist, be `partial`, and derive from the root
tag's type. A misspelled namespace still compiles the C# class but produces no `InitializeComponent` call in it.

**`EASEL_XAML019` on a local type.** Unprefixed names resolve only to Easel namespaces. Write
`xmlns:local="clr-namespace:MyApp.Views"` and `local:MyView`.

**A `{StaticResource}` throws at construction.** Static references resolve while `InitializeComponent` runs,
before the view is parented. The key must be in the view/ancestor dictionary at that moment, and declared
before use in the same file. Merge the theme dictionary in the base constructor, before `InitializeComponent`.

**`EASEL_XAML015`.** The generator emits `private void InitializeComponent()`; do not declare your own.

**A style setter does nothing.** `VisualSetter.Property` must be a dependency property. A plain CLR property is
rejected. Use `[XamlProperty]` for a styleable custom property.

**A style change does not reapply.** A `VisualStyle` is a declaration. Replacing its `Setters` after it is
applied does not re-evaluate it. Replace the style or write the local DP.

**Implicit style in a standalone dictionary does not apply.** Standalone `<ResourceDictionary>` implicit styles
are stored by type but not automatically applied. Declare the implicit style under a visual element's
`Resources`/`Styles`, or assign it explicitly.

**Merged dictionary `Source="…"` is rejected.** Easel has no runtime XAML loader. Merge a compiled
`ResourceDictionary` type (`<local:BaseTheme/>`) or a code-built dictionary.

**`ElementName` does not find a name.** Names are namescope-local. A name inside a different
`DataTemplate`/`ControlTemplate` is not visible; use `RelativeSource FindAncestor` or bind through the item's
DataContext.

**DynamicResource in a setter/trigger action is rejected.** Setter values are resolved once. Put the dynamic
reference on the element's property, or use a static resource.

**A later static value gets overwritten by an older dynamic reference.** Resource references follow the
last writer per target property; if you build your own binding via `ApplyStatic`/`BindDynamic`, pass a stable
`targetToken` (normally the DP reference) so the newer one supersedes the older.

## Layout and input

**A `DockPanel` top bar eats the whole window.** A top/bottom child with no height is measured against the
remaining height; give chrome an explicit height (`Height="48"`) or use a `Grid` `Auto` row.

**A `Grid` with no definitions is one star cell.** Add `RowDefinitions`/`ColumnDefinitions` when you expect
tracks; a star cell fills available space.

**A `Canvas` collapses to zero height in a `StackPanel`.** Canvas does not size to children; give it a size or
put it in a star track.

**A horizontal `ScrollViewer` makes long text unwrapped.** The horizontal axis is unbounded while it can scroll.
Set `HorizontalScrollBarVisibility="Disabled"` when text should wrap at the viewport.

**A `ScrollViewer` inside an Auto-sized popup stretches to the whole available width.** Its desired size comes
from the presenter content (+ Padding); do not put a star-sized Grid directly as inline content in an Auto
slot.

**A rotated/transformed visual leaves stale pixels outside its presenter.** The damage rect only covers the new
projected bounds. Set `ClipToBounds="True"` on the presenter/stage; for a `Transform3D` change the runtime
invalidates the parent. Keep the stage clipped even so.

**`GridSplitter` moves the wrong pane.** Direction follows the target's side: left/above targets grow away from
the sash; right/below targets grow toward it (delta inverted). Auto-sized targets use their arranged size, not
`Width`/`Height` (`NaN`).

**Focus looks stuck on a click.** Pointer focus sets `IsFocusVisible=false`; only keyboard focus paints the ring.
`Focusable=true` + `IsTabStop` control Tab participation.

## Native interop

**A WinForms control disappears or paints over everything.** It must be inside `NativeControlHost`, not a visual
child. A native island is an airspace rectangle above the tree: it cannot be transformed, clipped, blended or
covered by a popup.

**A focused TextBox editor floats over a scrollbar.** The native editor is clipped to the scroll viewport by the
host; if you place it in a transformed/clipped subtree, it cannot follow the transform. Use
`UseNativeEditor=false` for a fully visual field.

**The native editor disappears after a layout pass.** A host that skips `ArrangeOverride` for an unchanged size
still re-places visible islands by absolute bounds; if a custom host re-implements this, keep the
`NativeControlHost` placement scan.

**Theme/native color mode.** Native islands choose their dark/light mode when their handles are created. Call
`Application.SetColorMode` before creating them; changing it late does not repaint existing HWNDs.

## Composition and 3D

**Text does not use the gradient.** Set the gradient on `TextBlock.Foreground` (Brush-backed) or
`TextVisual.Foreground`; a multiline `TextBox` restarts the gradient per line. A `SolidColorBrush` is exact;
an image brush on text falls back to its representative color.

**Opacity on a container fades children individually.** That only happens if you set opacity on each child. Put
`Opacity`/`OpacityMask`/`Effect` on the group; the group composes through one bitmap.

**An opacity mask does nothing with a black opaque brush.** `OpacityMaskMode.Alpha` uses alpha; black opaque is
fully covering. Use `Luminance` for a black→white mask.

**A `CacheAsBitmap` subtree does not update.** A DP change inside marks the nearest cache dirty automatically; a
custom `Visual` must call `InvalidateCache()` after changing pixels. Effects disable the cache.

**3D content is soft/blurry.** The 3D warp composes at logical resolution and resamples through the projected
quad. Keep the source small, avoid very strong perspective, and cache the pre-warp layer. Very large
destinations fall back to a coarse affine grid.

**A 3D card ghosts while its angles change.** The old projection must be invalidated with the new one; the
runtime invalidates the parent on a `Transform3D` change. If you bypass the DP and mutate a transform object in
place, call the owning element's `InvalidateVisual()` yourself.

## Data and resources

**Bindings show empty until a re-layout.** A binding created before its element is parented retries when the
chain grows. Set `DataContext` before `InitializeComponent` for immediate resolution, or accept one retry frame.

**A `TreeView` shows nothing for `ItemsSource`.** `TreeView` has no hierarchical `ItemsSource`; build
`TreeViewItem` rows in code (or as XAML children). `ListBox`/`ItemsControl`/`VirtualList` do support
`ItemsSource`.

**A `ComboBox` drop-down is empty until `Items`/`ItemsSource` resolves.** `Items` is observable, so items added
after `InitializeComponent` appear immediately, and a `SelectedIndex` set earlier resolves once the items reach
it. The gallery fills combo items in code-behind.

**A resource dictionary key is missing in a custom theme.** Copy the exact key set from
[`Src/Easel/Themes/DarkTheme.xaml`](../../../Src/Easel/Themes/DarkTheme.xaml); missing keys become transparent in
`ThemePalette.FromResourceDictionary`, producing invisible or unthemed controls.

**Theme switch leaves old colors.** Controls read `Theme.Current`/their merged dictionary at construction.
Rebuild the visual root after changing the mode; update the host `BackColor` too.

## Verification

When a change fails visually, use the gallery's deterministic path: `RenderTo` for a static frame,
`ProcessPointerDown/Move/Up` for gestures, and the capture switches:

```bash
dotnet run --project Examples/XamlGallery/Demo/Demo.csproj -- --entry scenario.dashboard --capture ./captures
dotnet run --project Examples/XamlGallery/Demo/Demo.csproj -- --capture ./captures --all --dump
```

`--dump` writes the visual tree with bounds/colors, which quickly distinguishes "not laid out" from "not
painted" and "wrong resource" from "wrong position".
