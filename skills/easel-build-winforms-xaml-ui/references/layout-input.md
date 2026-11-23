# Layout, scrolling and input

## Choose the container

| Need | Use |
| --- | --- |
| One child with chrome/padding | `Border`, `ContentControl` |
| A vertical/horizontal sequence | `StackPanel` (`Spacing`) |
| Edge-docked chrome around a fill | `DockPanel` (`DockPanel.Dock`) |
| Tracks that share remaining space | `Grid` (`Auto`, `*`, `2*`, pixels) |
| Fixed-size wrapping tiles | `WrapPanel` (`ItemWidth`, `ItemHeight`, `Spacing`) |
| Equal cells | `UniformGrid` (`Columns`) |
| Absolute positioning | `Canvas` (`Canvas.Left/Top/Right/Bottom`) |
| Scale one fixed design | `Viewbox` (`Stretch`) |
| A scrollable region | `ScrollViewer` |
| A resizable split | `GridSplitter` |
| A page/flip surface | `TabControl` |

## StackPanel

```xml
<StackPanel Spacing="10">
  <TextBlock Text="Title" />
  <TextBox Placeholder="Name" />
</StackPanel>
```

`Spacing` is the gap between children; the panel sums child desired sizes on its axis and the maximum on the
cross axis. A horizontal stack does not wrap; use `WrapPanel` for that.

## DockPanel

```xml
<DockPanel LastChildFill="True">
  <Border DockPanel.Dock="Top" Height="48" Background="{StaticResource Surface}" />
  <Border DockPanel.Dock="Left" Width="220" Background="{StaticResource SurfaceAlt}" />
  <Border />
</DockPanel>
```

Child order is dock order. The first Left/Right child takes from the left/right of the remaining rectangle; the
last visible child fills when `LastChildFill`. A top/bottom child with no explicit height is measured with the
remaining height; give it a size when it is chrome, or use a `Grid` with `Auto` rows.

## Grid

```xml
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="*" />
    <RowDefinition Height="2*" />
  </Grid.RowDefinitions>
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="240" />
    <ColumnDefinition Width="*" />
  </Grid.ColumnDefinitions>

  <TextBlock Grid.Row="0" Grid.ColumnSpan="2" Text="Header" />
  <Border Grid.Row="1" Grid.Column="0" />
  <Border Grid.Row="1" Grid.Column="1" />
</Grid>
```

- `Auto` measures the largest child on the axis; `*` shares remaining space after Auto/pixel tracks and
  `Spacing`/padding; pixel tracks are fixed.
- A child may span with `Grid.RowSpan`/`Grid.ColumnSpan`.
- Grid measures Auto tracks first, then re-measures children inside their resolved cells. Do not depend on
  `*` tracks having their final size during `Measure`.
- A Grid with no row/column definitions behaves as one star row/column.

## Canvas and Viewbox

```xml
<Canvas Height="180">
  <Border Canvas.Left="20" Canvas.Top="20" Width="100" Height="60" />
  <Ellipse Canvas.Right="20" Canvas.Bottom="20" Width="80" Height="80" />
</Canvas>

<Viewbox Width="320" Height="180" Stretch="Uniform">
  <Grid Width="640" Height="360">…</Grid>
</Viewbox>
```

`Canvas` does not size itself to its children; give it a size or put it in a star track. `Viewbox` scales one
child; the child should have a natural/fixed design size.

## ScrollViewer and scroll bars

```xml
<ScrollViewer Width="360" Height="240"
              HorizontalScrollBarVisibility="Disabled"
              VerticalScrollBarVisibility="Auto">
  <StackPanel Spacing="8">…</StackPanel>
</ScrollViewer>
```

- Default template: `PART_ScrollContentPresenter` in the star cell, `PART_HorizontalScrollBar`/`PART_VerticalScrollBar`
  in Auto tracks. Bars take their own space; they do not overlay content.
- `Auto` shows a bar only when content exceeds the viewport; `Hidden` allows offset changes by wheel/keyboard
  without showing a bar; `Disabled` fixes the axis at offset 0 and constrains measurement where the content
  allows it.
- Horizontal axis is **not constrained** while it can scroll (long text wraps at the viewport only when the
  horizontal axis is disabled).
- `CanContentScroll=true` with an `IScrollInfo` content delegates extent/offset/page/wheel to the content.
- `IsExternalScroll`/`SetExternalOffset` is for camera hosts; the presenter reports the range but does not
  translate the content.
- When changing a child's size or visibility, the presenter re-measures and updates extent; you do not need to
  set the bar maximums.

## GridSplitter

```xml
<DockPanel>
  <Border x:Name="leftPane" DockPanel.Dock="Left" Width="220" />
  <GridSplitter x:Name="splitter" DockPanel.Dock="Left" Width="6" Orientation="Vertical" />
  <Border />
</DockPanel>
```

```csharp
splitter.Target = leftPane;
```

- `Orientation.Vertical` resizes width with a `SizeWE` cursor; `Horizontal` resizes height with `SizeNS`.
- Direction is computed from the target's side of the sash: a target left/above grows when dragged away; a
  target right/below grows when dragged toward the sash (the delta is inverted).
- The starting size is the target's **arranged** width/height, so auto-sized panes work; the first move writes
  an explicit size.
- Minimums are 80 px wide / 40 px high.

## Alignment and margins

`HorizontalAlignment`/`VerticalAlignment` are `Left/Center/Right/Stretch` and `Top/Center/Bottom/Stretch`.
`Stretch` with no explicit width/height fills the slot; an explicit `Width`/`Height` combined with `Stretch`
centers. `Margin` is outside the element; `Padding` is inside its chrome.

## Focus and Tab

- `Focusable=true` puts an element in the focus candidate set. `IsTabStop=false` skips it; `TabIndex` orders it
  (lower first; ties use tree order).
- `Focus()` calls `VisualHost.FocusElement`. `IsFocused` is logical focus; `IsFocusVisible` is true only after
  keyboard focus, not pointer focus.
- `Tab`/`Shift+Tab` walk the chain; arrow keys inside a `TabControl`/list/tree move within the control.
- A focused native TextBox editor keeps the native focus on the island; the host still reports the logical
  `FocusedElement`. Do not call `FocusElement(null)` on every `LostFocus` — check `ContainsFocus`.
- `TextBox` with `UseNativeEditor=false` gets all focus behavior on the visual path.

## Pointer

- A press hit-tests the deepest element, captures it, and bubbles `PreviewPointerPressed` down then
  `PointerPressed` up.
- While captured, `PointerMoved` goes to the captured element even outside its bounds; `PointerReleased`
  releases capture.
- `PointerEntered`/`PointerExited` follow hover, not capture.
- `PointerWheel`/`PointerHWheel` bubble from the element under the pointer; a `ScrollViewer` consumes the wheel
  on its visible axis.
- `args.Handled = true` stops the bubble.
- `args.GetPosition(element)` converts the surface point to an element's local coordinates.
- Cursor: set `VisualElement.Cursor`; the host picks the deepest hovered element's cursor.

## Drag and drop

- Set `AllowDrop="True"` on the element that should receive a drop. The host enables the native OLE drop target,
  hit-tests the deepest `AllowDrop` element and bubbles `DragEnter`, `DragOver`, `DragLeave` and `Drop` through
  its ancestors. Set `args.Effects` and `args.Handled` in the handler; `Handled` stops the bubble.
- `VisualDragEventArgs` carries `Data` (`IDataObject`), `AllowedEffects`, `Position`, `Modifiers` and
  `GetPosition(element)`.
- Start a drag with `VisualDragDrop.StartDrag(source, data, allowedEffects, ghostText)`. It is the blocking native
  loop; call it from a pointer handler. `VisualDragDrop.CreateData(payload, text, files)` builds a data object and
  `GetFiles`/`GetText`/`TryGetData` read one back.
- `DropZone` is a ready dashed target: it accepts files, text and `Easel.DataGrid.Rows`, raises one `Dropped`
  event and paints its own hot state.
- A drag started inside the app paints a floating label through `GiveFeedback`; Escape cancels it through
  `QueryContinueDrag`. `DataGrid` uses the same path for row moves and accepts a CSV/text drop on its body.
- Use `args.GetPosition(target)` after a hit test; the host converts `DragEventArgs.X/Y` (screen coordinates)
  before routing.

## Keyboard

`KeyDown`, `KeyUp`, `TextInput` are delivered to the focused element and bubble. `TextInput` is where printable
characters and IME composition reach a `TextBox`/`RichTextBox`. `VisualKeyEventArgs` carries `KeyCode`,
`Modifiers`, `IsRepeat`. Handle `Tab`/arrows only when the control's own behavior should not consume them.

## DPI and coordinates

- all layout, `Bounds`, `AbsoluteBounds`, pointer points and fonts are logical DIPs;
- `DpiScale` affects only the paint transform and the placement of native islands;
- `VisualHost` multiplies logical points by `DpiScale` in painting; `ProcessPointer*` accepts physical client
  pixels and converts internally;
- in tests set `DpiScaleOverride = 1f` to make bounds and pointer points identical.

## Damage, caches, and composition cost

- A DP with `AffectsRender` invalidates the element's bounds; the host clears/paints only the dirty rectangle.
- `Visual.CacheAsBitmap` caches the local subtree; moving the cached node does not re-render content. A change
  inside an element invalidates the nearest cache; a custom `Visual` must call `InvalidateCache()`.
- `Effect`, `Opacity < 1`, `OpacityMask`, and `Transform3D` route through a temporary bitmap. Keep those groups
  small and avoid per-frame transforms over huge subtrees.
- `Visual.Clip`/`ClipToBounds` clip painting and hit-testing; use `ClipToBounds` on a stage that hosts a rotated
  visual so old frames cannot remain outside its bounds.
- `Visual.RenderDamage` culls subtrees outside the dirty rectangle; items that draw outside their bounds should
  override `GetInvalidationBounds` or accept conservative repaint.
