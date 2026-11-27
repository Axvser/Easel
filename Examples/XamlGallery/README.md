# XamlGallery

The living catalogue for the Easel WinForms XAML runtime (`Easel.Visuals`).
The whole window is built from one `VisualHost` — a single HWND — and a tree of visual elements;
no card creates a child window. Reactivity is visual: controls derive from `VisualElement`, not from a
WinForms control. `NativeControlHost` is the one explicit native island; the focused visual `TextBox`
uses a private one for IME and accessibility.

The gallery is **XAML-first**: every demo page and every card is a `.xaml` view with a small code-behind
that only carries data and view behaviour. The shell itself is `Views/GalleryVisualShell.xaml`, bound to
`GalleryShellViewModel`; the catalog tree, preview swap and splitter targets are the only code-behind.

## Window

The window is a three-pane workbench:

- **Catalog** (left): a visual `TreeView` over the filter box, layered as **section → group → card**.
  **Scenarios** leads with industrial/HMI pages, then application pages and visual studies; **Easel native**
  holds the component groups (input and data first, since an HMI is built from them); **WinForms native**
  holds the interop cards.
- **Preview** (center): the selected view plus its `Usage` snippet. The snippet is XAML, not C#.
- **Properties** (right): the editable dependency properties of the view root, discovered by reflection;
  strings, numbers, booleans, enums and colours are editable.

The header title is **Modern WinForms Gallery**; the native window title stays `Easel Demos`. The theme
selector is a `ComboBox` with **Dark+ / Light+ / High contrast / Follow system**. *Follow system* listens
to the Windows app theme (and high contrast) through `ThemeMode.System` and rebuilds the shell live.

## Run

```bash
dotnet run --project Examples/XamlGallery/Demo/Demo.csproj
```

Useful switches:

```bash
# open and close the window without interaction
dotnet run --project Examples/XamlGallery/Demo/Demo.csproj -- --smoke

# open one entry, a group, a section, or a starting palette (Dark+, Light+, high contrast, follow system)
dotnet run --project Examples/XamlGallery/Demo/Demo.csproj -- --entry scenario.production-line
dotnet run --project Examples/XamlGallery/Demo/Demo.csproj -- --group Industrial --light
dotnet run --project Examples/XamlGallery/Demo/Demo.csproj -- --group Scenarios
dotnet run --project Examples/XamlGallery/Demo/Demo.csproj -- --group Graphics --contrast
dotnet run --project Examples/XamlGallery/Demo/Demo.csproj -- --system

# scroll the preview by pixels
dotnet run --project Examples/XamlGallery/Demo/Demo.csproj -- --entry scenario.chat --scroll 200

# capture one card to a PNG and dump the visual tree
dotnet run --project Examples/XamlGallery/Demo/Demo.csproj -- --capture ./captures --entry graphics.gradients --dump

# capture every card
dotnet run --project Examples/XamlGallery/Demo/Demo.csproj -- --capture ./captures --all
```

`--capture` places the window on the first non-primary screen when one exists, selects each target in
catalog order, waits for layout, renders the window into a bitmap with `PrintWindow` (falling back to
`VisualSurface.RenderTo`), and closes. `--dump` writes a visual tree with bounds and colours next to
each PNG. `--preview-only` hides the title, side panes and status strip for a content-only capture.
`--light` / `--contrast` start the process in those palettes; `--system` starts in follow-system mode.
`--group` accepts either a group (`Industrial`, `Data`) or a section (`Scenarios`, `Easel native`).

## Themes

Easel's built-in fallback palettes are XAML resource dictionaries in the library itself, under
[`Src/Easel/Themes/`](../../Src/Easel/Themes/): `DarkTheme.xaml` and `LightTheme.xaml`. Each file is a
WPF-style `<ResourceDictionary>`: palette brushes, `CornerRadius` / `ControlHeight` metrics, and the same
`Heading` / `Subheading` / `Body` / `Caption` typography styles, repeated in both themes even though the
font setters are identical. `Theme.CreateDark()` / `CreateLight()` read those resources, and
`Theme.CreateResourceDictionary(mode)` returns a copy for an application. The themed base classes merge the
selected dictionary into each view's `Resources.MergedDictionaries` before `InitializeComponent`.

The gallery's **High contrast** option is a **custom theme demonstration**, not a library fallback:
[`Demo/Themes/HighContrastTheme.xaml`](Demo/Themes/HighContrastTheme.xaml) is a demo-owned
`<ResourceDictionary>`; `ThemeService` merges it into the views and applies
`ThemePalette.FromResourceDictionary(...)` as the active palette. Resource dictionaries support `x:Key`
resources, keyed `Style`/`ControlTemplate`/`DataTemplate`, scalar `x:Double`/`x:Int32` values, and
`MergedDictionaries` with compiled `ResourceDictionary` types or inline dictionaries. File-URI
`Source="..."` is intentionally not supported because Easel has no runtime XAML loader.

`ThemeMode.System` is a runtime-level mode: it resolves to Dark+/Light+ from
`HKCU\...\Themes\Personalize\AppsUseLightTheme`, or to the high-contrast palette while Windows high
contrast is on. `SystemEvents.UserPreferenceChanged` re-reads the setting and raises `Theme.Changed` on
the thread that selected the mode, rebuilding the gallery without a restart.

## Demos

The catalog is a hierarchy: **Scenarios**, **Easel native** and **WinForms native**, each split into groups.
Industrial work leads the scenario section and the Easel-native groups are ordered by how close they are to an
HMI screen (input, data and graphics first; basics and generated visuals last).

### Scenarios · Industrial

| Card | Shows |
| --- | --- |
| Production line | A shop-floor overview: OEE ring, KPI tiles, six station rows with live cycle bars, a throughput sparkline and an alarm feed. |
| Process flow | A tank-and-pipe mimic: vessel levels, pump states, moving flow dots, a recipe step list and reactor values. |
| Alarm center | Severity counts, filter buttons, an item-templated alarm list where each row owns an `ACK` command, and an event log. |
| Machine panel | A CNC operator panel: X/Y/Z/A DROs, spindle-load ring, feed override slider, jog commands and an E-stop latch. |

Each industrial page binds to a view model and advances its values from a `System.Windows.Forms.Timer`; all
shape geometry (`Path`, `Polyline`, `Polygon`, `Line`) is computed in the view model and bound from XAML.

### Scenarios · Applications

| Card | Shows |
| --- | --- |
| Analytics dashboard | Gradient KPI tiles, a chart drawn with `Polyline`/`Polygon`/`Line`, progress goals, an activity feed, a refresh `[VeloxCommand]`. |
| Settings page | Two-way bound `CheckBox`/`Slider`/`TextBox`/`ComboBox`, radio rows, and a save command that reports the snapshot. |
| Music player | Three real embedded MP3 tracks through WPF `MediaPlayer`, a 3D-tilted album card, a masked strip and transport/seek controls. |
| Video player | A real embedded H.264/AAC clip in a WPF `MediaElement`, hosted in the single `NativeControlHost` island with transport, seek and time labels. |
| Chat | An `ItemsControl` with a message-bubble `DataTemplate`, a two-way composer and a send command. |

### Scenarios · Visual studies

| Card | Shows |
| --- | --- |
| Gradient studio | Inline and resource linear/radial brushes on borders, text, shapes and an opacity mask. |
| Composition lab | A live `ContainerVisual` tree with group opacity, a drop shadow, an opacity mask and a bitmap cache; sliders write `Visual.Opacity` and `Visual.Transform`. |
| 3D showcase | `Rotate3DTransform` in perspective; sliders drive the angles and camera depth of a live card. |

### Easel native — component groups

| Group | Shows |
| --- | --- |
| Input | `Button`/`RepeatButton` (XAML `Click` handlers), toggles, `Slider`, `ProgressBar`, `NativeControlHost`. |
| Data | `DataGrid` at 2,048 rows with sortable headers, badge/progress cells, row numbers and drag in/out; a two-grid drag board; `ListBox` with an item template; `ComboBox`; `VirtualList` at 5,000 rows with a realized-range readout; a three-level `TreeView`; a scaled `Calendar`; `DatePicker` — all bound through `DataContext = model`. |
| Graphics | Shapes, `VisualPresenter` with a XAML-declared `ContainerVisual`, opacity and clip, gradient brushes, visual composition, 3D transforms. |
| Animation | `TransitionAction` and `KeyFrameAnimation` in XAML triggers — property, keyframe, brush and `Transform3D` scenes, each shown next to the markup that produces it. |
| Binding | One-way/two-way path bindings, inherited `DataContext`, a converter, a bound `Button.Command`, and a generated `[VeloxProperty]`/`[VeloxCommand]` view model. |
| Styling | Implicit/explicit `VisualStyle`, `VisualTrigger`, `VisualControlTemplate` with `ContentPresenter`, resources, and a `DynamicResource` vs `StaticResource` comparison — all declared in XAML. |
| Layout | `StackPanel`, `DockPanel`, `Grid`, `WrapPanel`, `Canvas`, `UniformGrid`, `Viewbox`, `GridSplitter`, `GroupBox`, `Expander`, `TabControl` — each a XAML view with attached properties. |
| Text | `TextBlock`, `TextBox`, password/multiline editing, a XAML-templated `TextBox` with `UseNativeEditor=false`, `RichTextBox`, `Image`. |
| Overlay | `VisualPopup`, `VisualMenu`, `VisualContextMenu`. |
| Theme | Palette swatches, typography, the three-mode palette rebuild, and a XAML resource dictionary with keyed styles, a trigger, a control template and a data template. |
| Basics | The implicit fallback appearance, authored as visual XAML: representative controls with no style, background or foreground set. |
| Visuals | A generated XAML view with a XAML click handler. |

### WinForms native

The four interop cards: native elements in one layout pass, native property binding, snapshot composition and
`NativeControlHost`.

## New in this revision

- **Industrial-first sections** — the catalog is now **Scenarios → Easel native → WinForms native**, and the
  industrial pages, input and data groups lead their sections. The shell shows the section/group breadcrumb
  above the preview and the status bar names all three levels.
- **Industrial scenario pages** — production line, process flow, alarm center and machine panel, each driven by
  a timer-backed view model and drawn with bound `Path`/`Polyline`/`Polygon` geometry.
- **DataGrid and native drag** — a row-virtualized two-dimensional table with sortable headers, badge/check/
  progress cells, row numbers, in-place column resize and themed selection. Rows drag out through the Windows
  OLE loop and two grids exchange the same row objects through `Easel.DataGrid.Rows`; `DropZone` accepts files,
  text and rows. The Data group is rebuilt around a 2,048-row ledger, a two-grid drag board, a 5,000-row
  virtual list and annotated ListBox/TreeView/Calendar/DatePicker/ComboBox cards.
- **Gradient brushes** — `LinearGradientBrush` and `RadialGradientBrush` work inline
  (`<Border.Background><LinearGradientBrush>…`, `<Ellipse.Fill>…`) and as resources
  (`{StaticResource AuroraGradient}`). A `GradientStop` takes a colour and an offset. `Foreground` accepts a
  gradient too: text is drawn through the native gradient brush, so glyphs keep the full colour ramp.
- **Visual composition** — a `ContainerVisual`/`ShapeVisual`/`TextVisual` tree hosted by `VisualPresenter`
  composes through `Opacity`, `Transform`, `Clip`, `CacheAsBitmap` and `VisualEffect` (the drop shadow).
  `VisualElement.OpacityMask` (and `Visual.OpacityMask`) multiply the composed subtree by a brush's alpha,
  or by its luminance when `OpacityMaskMode="Luminance"`, so one gradient fades a whole group as one layer.
- **3D transforms** — `Transform3D` on any visual element: `Rotate3DTransform` (AngleX/Y/Z, CenterX/Y/Z,
  Depth), `Scale3DTransform`, `Translate3DTransform`, `MatrixTransform3D` and `Transform3DGroup`. The
  subtree is composed once and warped with a perspective-correct inverse homography; hit-testing and damage
  bounds use the same map. `Depth <= 0` is orthographic, and `CacheAsBitmap` reuses the pre-warp layer across
  transform-only frames. XAML:
  `<Border.Transform3D><Rotate3DTransform AngleX="16" AngleY="24" Depth="700" /></Border.Transform3D>`.
- **Animation** — a new Group runs four scenes and prints the markup next to each: `TransitionAction` on
  `Opacity`/`CornerRadius`, a `KeyFrameAnimation` chain, brush interpolation (`{StaticResource}` endpoints,
  structural gradient→gradient and the cross-fade fallback), and a `Transform3D` endpoint. The cards run on
  Easel's own animation engine; nothing has to be registered or injected.
- **MVVM + XAML** — the shell is XAML bound to `GalleryShellViewModel`; scenario pages are XAML bound to
  `DashboardViewModel`, `SettingsViewModel`, `PlayerViewModel` and `ChatViewModel`. Card roots derive from
  `ThemedStackPanel`/`ThemedGrid`/`ThemedDockPanel`/`ThemedBorder`, which seed the palette and gradient
  resource keys before `InitializeComponent`.

## Data and drag

- **DataGrid** is the large table card: 2,048 rows, seven columns, sortable headers, badge and progress cells,
  a row-number gutter, a fixed header over a virtualized body and a scroll bar owned by the grid. Sorting is
  asc → desc → source order; selection follows the item across sorts.
- **Drag board** is two grids over two `ObservableCollection<SampleRow>` instances. Dragging a row across the
  gap moves the same object: the target inserts it and the source removes it. Both grids also accept
  `FileDrop` and Unicode text; the receiving `Dropped` handler turns either into rows.
- **DataGrid card** drags a row out as text and as a CSV `FileDrop` (a temp file), so Explorer and other
  applications are valid targets. The dashed `DropZone` below the grid accepts a row, a file or text.
- The drag itself is the native OLE loop. `AllowDrop` on an element receives `DragEnter`/`DragOver`/
  `DragLeave`/`Drop`; `VisualElement` routes them to the deepest target and bubbles through its ancestors.
  Escape cancels, and the source host paints a small floating label that follows the pointer.

## Notes

- The **Basics** card is a XAML view; the controls set no style, background, foreground or palette
  override, so it stays the reference for what a host gets without authored styling.
- `Themed*` base types (`Views/GalleryTheme.cs`) put palette colours and shared gradients into the view's
  own resource dictionary. `{StaticResource}` resolves at construction, before the view joins the surface.
- `{StaticResource}` resolves once at construction; `{DynamicResource}` stays subscribed and follows
  dictionary adds/replaces/removes on visual and attached properties. When the same target property receives
  a later resource reference, the later one takes over: a static value added after a dynamic reference stops
  the dynamic updates, and a dynamic reference added after a static one resumes tracking. The Dynamic
  resource card demonstrates both behaviours.
- Code-behind is limited to plumbing: `DataContext = model`, `TreeViewItem` construction, `ComboBox` items,
  calendar/date values, `NativeControlHost.Child`, popup lifetime, bitmap/text seeding, and slider →
  transform wiring.
- `TextBlock.Foreground` is a `Color` accessor over a Brush dependency property. A gradient resource works
  on `TextBlock`, `Button`, `TextBox`, `ComboBox` and host `TextVisual`s: the renderer hands the GDI+ native
  brush to `DrawString`, so glyphs carry the full gradient instead of its first stop. Multiline text boxes
  paint each line through the brush; the gradient restarts per line there.
- The 3D projector samples the composed layer through an inverse homography (bilinear, 2×2 edge coverage)
  rather than an N×N affine grid, so perspective edges stay exact. Very large projected areas fall back to
  an unwarped draw rather than allocating huge buffers. With `CacheAsBitmap`, transform-only changes reuse
  the cached pre-warp layer; content changes invalidate it through the normal render invalidation path.
- `OpacityMaskMode` selects `Alpha` (default; the brush alpha) or `Luminance` (premultiplied white/black
  luminance), both demonstrated in the Gradient studio card.
- One `GalleryModel` is shared by the data and binding cards.
- The music and video pages are real media: the three MP3 tracks and the 12-second H.264/AAC clip are
  embedded resources under `Demo/Assets/Media/`, extracted to a temp folder on first use. Audio uses WPF
  `MediaPlayer`; video uses a WPF `MediaElement` inside the single `NativeControlHost` island (the demo
  opts into WPF; the Easel runtime itself does not).
- Theme selection is Dark+ / Light+ / High contrast / Follow system; a switch rebuilds the visual root so
  every view reads the new theme resources and `Theme.Current` values.

## Animation

Easel ships its own animation engine. Trigger actions, keyframe animations and `VisualTransition`
interpolate values, pace frames and hold the end value themselves; the gallery registers nothing at startup.
Animatable types are numbers, geometry, 2D/3D transforms, `Color` and `Brush`; an unsupported property is
rejected with a clear `NotSupportedException`.
