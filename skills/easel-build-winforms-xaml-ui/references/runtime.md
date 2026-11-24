# Runtime, hosting, layout and input

## The one-HWND model

`VisualHost : System.Windows.Forms.Control` owns the only HWND. It measures and arranges the root
`VisualElement`, paints the whole visual tree into its double buffer, and routes pointer/keyboard/text input to
the deepest hit element. No visual element creates a window.

```csharp
var surface = new VisualHost { Dock = DockStyle.Fill, BackColor = Theme.Current.WindowBackground };
form.Controls.Add(surface);
surface.Root = new MyShellView();
```

`VisualHost.Root` accepts a `VisualElement` or a plain `ContainerVisual`. Only a `VisualElement` gets layout and
input; a `ContainerVisual` is painted as lightweight decoration.

`NativeControlHost` is the explicit escape hatch: it owns one child HWND placed at the element's absolute
rectangle. That island floats above the visual tree, cannot be transformed/clipped/blended, and popups cannot
cover it. The focused visual `TextBox` uses a private host for IME/accessibility; set
`UseNativeEditor="False"` to keep the field entirely visual.

## Surface API

| Member | Use |
| --- | --- |
| `Root` | Assign the shell view; replacing it tears down old popups/native islands and focus. |
| `Resources` | Dictionary consulted after the element and its ancestors. |
| `Styles` | Implicit styles for the whole tree (element/ancestor styles win). |
| `DpiScale` / `DpiScaleOverride` | Physical pixels per logical DIP. Layout/input/paint are logical; only painting and native islands scale. `DpiScaleOverride` pins a test scale. |
| `LogicalClientSize` | Client size in DIPs. |
| `InvalidateSurface()` | Request a full repaint. Layout invalidation is automatic from DPs. |
| `RenderTo(Graphics, Size)` | Deterministic offscreen render at logical size; never applies DPI. Use for tests/captures. |
| `FindElementAt(PointF)` | Hit-test a logical surface point to the deepest element. |
| `FocusElement(element)` | Move logical focus. `null` clears it. |
| `ClosePopups()` | Close every popup in the overlay. |
| `ProcessPointerDown/Move/Up/Wheel/HWheel` | Inject pointer input (physical client pixels; converted to logical). |
| `ProcessKeyDown/Up`, `ProcessTextInput` | Inject keyboard/text input. |
| `LayoutUpdated` | Raised after measure/arrange completes. |
| `PaintCount` / `LastPaintTimestamp` / `LastPaintWasClockDriven` | Paint diagnostics for animation drivers. |
| `MarkNextPaintAsClockDriven()` | Mark the next paint as an animation tick. |
| `MotionScale` | Render gesture frames at a fraction of logical resolution (default `1`, disabled). |

A pointer press captures the hit element; moves are delivered to the capture even outside its bounds until
release. `ProcessPointerDown` returns whether the element handled the event.

## VisualElement layout contract

| DP | Meaning |
| --- | --- |
| `Width`, `Height`, `MinWidth`, `MaxWidth`, `MinHeight`, `MaxHeight` | Explicit and bounded size. `NaN` means automatic. |
| `Margin`, `Padding` (`Easel.Controls.Thickness`) | Outer/inner spacing. |
| `HorizontalAlignment`, `VerticalAlignment` | `Left/Center/Right/Stretch`, `Top/Center/Bottom/Stretch`. |
| `Visibility` | `Visible`, `Hidden` (occupies space, not painted), `Collapsed` (not measured). |
| `IsEnabled` | Inherited; disabled subtrees reject pointer input. |
| `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius` | Self-drawn chrome on `VisualElement`. |
| `Opacity`, `Transform`, `Transform3D`, `Clip`, `ClipToBounds`, `OpacityMask`, `OpacityMaskMode`, `Effect`, `CacheAsBitmap` | Visual composition (see [visuals.md](visuals.md)). |
| `FontSize`, `FontFamily`, `FontWeight` | Inherited typography; derived into `Font`. |
| `DataContext` | Inherited binding source. |
| `Focusable`, `IsTabStop`, `TabIndex` | Logical focus chain. |

The layout pass:

1. `Measure(available)` asks a panel to size its content; `MeasureOverride` returns the desired content size.
   The element then applies its own width/height/min/max/margin.
2. `Arrange(finalRect)` positions the element; `ArrangeOverride` places children. `Bounds` is in the parent's
   coordinates, content paints in local `0..Width/Height`.
3. `InvalidateMeasure` / `InvalidateArrange` / `InvalidateVisual` are driven by DP metadata:
   `AffectsMeasure`, `AffectsArrange`, `AffectsRender`. Do not call WinForms `PerformLayout`.

Panels measure their own content; do not set `Width`/`Height` on a root view merely to fill the window. The
host arranges the root into the logical client rectangle.

## Input, focus and keyboard

Pointer events bubble from the deepest element to its ancestors: `PointerPressed` (and
`PreviewPointerPressed` in tunnel order), `PointerMoved`, `PointerReleased`, `PointerEntered`, `PointerExited`,
`PointerWheel`. `VisualPointerEventArgs` carries `Position`, `Buttons`, `Button`, `ClickCount`, `Delta`,
`Modifiers`, `Handled`, and `GetPosition(element)`.

Keyboard: `KeyDown`, `KeyUp`, `TextInput`, plus `GotFocus`/`LostFocus`. Focus is **logical**: the host keeps
the native keyboard focus; `FocusElement` moves the logical `FocusedElement`, and `Tab` walks `Focusable` /
`IsTabStop` / `TabIndex` elements. `IsFocusVisible` is false for pointer focus and true for keyboard focus.

```csharp
myButton.Focus();
if (myTextBox.IsFocused) { /* caret/IME path */ }
```

`TextBox` owns its caret, selection, clipboard and undo. With the native editor it also handles IME, but the
editor is a child HWND clipped to the scroll viewport; it cannot be transformed.

## Scroll and overlays

`ScrollViewer` contains a single `Content` and uses its default template:
`PART_ScrollContentPresenter` plus horizontal/vertical `ScrollBar`s in Auto tracks. Set
`HorizontalScrollBarVisibility` / `VerticalScrollBarVisibility` to `Auto`, `Visible`, `Hidden` or `Disabled`.
With `CanContentScroll=true` and content implementing `IScrollInfo`, extent/offset come from the content.

`VisualPopup` opens in the surface overlay, above the root tree and not clipped by ancestor
`ScrollViewer.Clip`. Set `PlacementTarget`, `Placement`, `HorizontalOffset`, `VerticalOffset`, `StaysOpen`;
`Opened`/`Closed` report lifecycle. The overlay content resolves resources only from its own dictionary and
`VisualHost.Resources`.

## DPI

All layout, bounds, pointer coordinates and fonts are logical DIPs. The host applies `DpiScale` only when
painting and when placing native islands. Authored sizes/font sizes therefore stay identical at 100%, 150% and
per-monitor DPI. Native HWNDs are placed in physical pixels from logical bounds.

Two settings are required:

1. **Per-monitor v2 awareness** — `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` in the csproj,
   read by `ApplicationConfiguration.Initialize()`, or `Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)`
   before the first window. Without it Windows bitmap-stretches the window.
2. **`AutoScaleMode.None` on the hosting form** — otherwise WinForms scales the form by its font factor and
   `VisualHost` then scales the visual tree by `DpiScale` as well, so everything renders at double size.

```csharp
ApplicationConfiguration.Initialize();              // reads ApplicationHighDpiMode
var form = new Form { AutoScaleMode = AutoScaleMode.None };
form.Controls.Add(new VisualHost { Dock = DockStyle.Fill });
```

- `DpiScale` re-reads the device DPI on a resize and on `OnDpiChangedAfterParent`; that override is not compiled
  into the `netframework4.6.1` target, so a monitor change there refreshes on the next resize.
- Tests and captures pin the scale with `DpiScaleOverride`; `RenderTo` never applies DPI.
- `NativeControlHost` children keep their own DPI behaviour; Easel only places their rectangles.

## Lifetime

`VisualHost.ClosePopups()` and replacing `Root` release popups and native islands. `VisualElement.Dispose()`
disposes descendants, bitmap caches, resource subscriptions and its native editor/child. Dispose a preview tree
when you replace it, as the gallery does:

```csharp
var previous = host.Content as VisualElement;
host.Content = newCard;
previous?.Dispose();
```

## Deterministic testing

Prefer `RenderTo` for visual tests and `ProcessPointer*`/`ProcessKey*` for input. A host created without `Show`
still lays out and renders through `RenderTo`; set `Size` so `LogicalClientSize` is non-zero before
`FindElementAt`/pointer injection:

```csharp
using var host = new VisualHost { Size = new Size(400, 200), Root = myRoot, DpiScaleOverride = 1f };
using var bitmap = new Bitmap(400, 200);
using (var graphics = Graphics.FromImage(bitmap))
{
    host.RenderTo(graphics, new Size(400, 200));
}
```

Without `DpiScaleOverride`, `ProcessPointer*` expects **physical** client pixels; logical bounds must be
multiplied by `DpiScale` before injection.
