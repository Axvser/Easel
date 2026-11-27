# Controls catalogue

Every control derives from `VisualElement`; its properties are dependency properties, so styles, triggers,
bindings, resources and animations apply uniformly. No control creates a child window (the two explicit native
islands are described in [runtime.md](runtime.md)).

Shared visual-element properties (`Width`, `Height`, `Margin`, `Padding`, `Background`, `BorderBrush`,
`BorderThickness`, `CornerRadius`, `Visibility`, `IsEnabled`, alignment, `Opacity`, `Transform`, `Clip`,
`Effect`, typography, `DataContext`) are listed in [runtime.md](runtime.md#visualelement-layout-contract).
Every element also owns a `Transitions` collection: each `Transition` names a DP and animates every change of
it on that element, from style, trigger, local or binding writes alike (see [animation.md](animation.md#element-transitions)).
This page lists each control's own properties and behaviour.

## Layout and containers

| Control | Key members | Behaviour |
| --- | --- | --- |
| `Border` | `Child`, `Padding`, `CornerRadius`, `BorderBrush`, `BorderThickness`, `Background` | Single child, arranged inside padding/border. `Add(child)` sets `Child`. Border chrome is self-drawn. |
| `ContentControl` | `Content` (`object?`), `ContentTemplate`, `ContentTemplateSelector`, `ContentStringFormat`, `Template` | Content may be a visual or data; a `ContentTemplate` turns data into a visual. |
| `ContentPresenter` | `Content`, `ContentTemplate`, `ContentTemplateSelector`, `ContentStringFormat` | Template part that shows the templated parent's content. |
| `UserControl` | `Content` | A `ContentControl` intended as a view root; `Add(child)` sets `Content`. |
| `StackPanel` | `Orientation`, `Spacing` | Horizontal/vertical stack with an explicit gap. |
| `DockPanel` | `LastChildFill`, attached `Dock` (`Left/Right/Top/Bottom/None`) | Docks in child order; the last visible child fills when `LastChildFill`. |
| `WrapPanel` | `Orientation`, `ItemWidth`, `ItemHeight`, `Spacing` | Wraps items into lines; fixed item width/height when set. |
| `Grid` | `RowDefinitions`, `ColumnDefinitions`, attached `Row`, `Column`, `RowSpan`, `ColumnSpan` | Pixel/`Auto`/star tracks. A grid with no definitions is one star row/column. |
| `Canvas` | attached `Left`, `Top`, `Right`, `Bottom` | Absolute placement; does not measure content. |
| `UniformGrid` | `Rows`, `Columns` | Equal cells; columns define the wrap. |
| `Viewbox` | `Child`, `Stretch` | Scales one child with a visual transform. |
| `Decorator` | `Child` | Single-child non-panel base. |
| `ScrollViewer` | `Content`, `HorizontalScrollBarVisibility`, `VerticalScrollBarVisibility`, `CanContentScroll`, `IsScrollInputEnabled`, `HorizontalOffset`, `VerticalOffset`, `ScrollChanged`, `ScrollOffsetRequested` | Scrolls one content; default template uses a `ScrollContentPresenter` plus Auto-track scroll bars. |
| `ScrollBar` | `Orientation`, `Minimum`, `Maximum`, `Value`, `ViewportSize`, `SmallChange`, `LargeChange`, `Template`, `ReserveCornerSize`, `TrackBrush`, `ThumbBrush`, `ThumbHoverBrush`, `ThumbRadius`, `ValueChanged`, `Scroll` | Template parts `PART_Track`, `PART_Thumb`, repeat buttons; supports `ScrollBarScrollEventArgs`. Usually used inside a `ScrollViewer`. |
| `ScrollContentPresenter` | `HorizontalOffset`, `VerticalOffset`, `ExtentWidth`, `ExtentHeight`, `ViewportWidth`, `ViewportHeight`, `IsExternalScroll` | Hosts scroll content; implements `IScrollInfo`; external-camera mode reports the range without moving content. |
| `GridSplitter` | `Target`, `Orientation` | Drags the target's `Width`/`Height`. Direction follows the target's side of the sash; auto-sized panes start from their arranged size. Cursor is `SizeWE`/`SizeNS`. |
| `GroupBox` | `Header`, `Child`, `HeaderBackground`, `HeaderForeground`, `HeaderBorderBrush`, `HeaderBorderThickness` | Titled frame with one child; the frame follows `Background`/`BorderBrush`/`BorderThickness`/`Padding`. |
| `Expander` | `Header`, `IsExpanded`, `Content`, `HeaderBackground`, `HeaderForeground`, `HeaderBorderBrush`, `HeaderBorderThickness` | Header toggles one content element; collapsed content is not measured. |
| `HeaderedContentControl` | `Header`, `Content`, `HeaderForeground` | Two-row shell; `Background`/`BorderBrush`/`Padding` are honored. |
| `TabControl` | `Items` (`IList<TabItem>`), `SelectedIndex`, `SelectionChanged` | One page at a time, pointer/arrow switching. Call `Refresh()` after mutating `Items` if the selected page must be rebuilt immediately. |
| `TabItem` | `Header`, `Content`, `HeaderForeground`, `SelectionLineBrush`, `Padding` | One tab page; the header labels read `Padding` and `HeaderForeground`. |
| `VisualPresenter` | `Content` (`Visual?`) | Gives a lightweight `ContainerVisual`/`ShapeVisual`/`TextVisual` tree a slot; no layout pass inside. |
| `NativeElement<T>` | `Native` (`T`), `Content` (`VisualElement?`), `ContentSurface`, `CornerRadius`, `IsSnapshot`, `RefreshSnapshot()` | The XAML wrapper for a native control. Measures with `GetPreferredSize`, arranges into the native window, paints visual `Content` through a nested `VisualHost`, rounds the live window's region, and can paint a bitmap snapshot (`IsSnapshot`). In a Form the snapshot keeps real input through an almost invisible owned proxy window. |
| `NativeControlHost` | `Child` (`System.Windows.Forms.Control?`) | The code-behind form of `NativeElement`; it owns the child. Same island limits. |
| `DropZone` | `Title`, `Detail`, `IsHot`, `Dropped` | Dashed drop surface; accepts files, text and Easel row payloads and raises one `Dropped` event. `Background`/`BorderBrush`/`CornerRadius`/`Padding` are honored; the dashed hot state stays theme-driven. |

## Text and images

| Control | Key members | Notes |
| --- | --- | --- |
| `TextBlock` | `Text`, `Font`, `Foreground` (Brush-backed), `Wrap`, `TextAlignment` | Single line by default; `Wrap` measures wrapped height. `Foreground` accepts a Brush, including gradients. |
| `TextBox` | `Text`, `Placeholder`, `Font`, `Foreground`, `IsReadOnly`, `UseNativeEditor`, `IsPassword`, `AcceptsReturn`, `Wrap`, `SelectionStart`, `SelectionLength`, `CaretIndex`, `SelectionBrush`, `SelectionForeground`, `CaretBrush`, `CaretThickness`, `CaretBlinkInterval`, `IsOverwriteMode`, `CaretBlockOpacity` | Self-drawn editing on the visual path by default. Set `UseNativeEditor=true` to fall back to the private native editor for IME/accessibility; the island draws the OS selection and caret, so the selection/caret properties apply only to the visual path. An unset `CaretBrush` uses the theme's `Caret` colour. Insert toggles `IsOverwriteMode`, which draws a translucent outlined block caret (`CaretBlockOpacity`, 0.35 by default) and replaces the next character. Defaults to `InputCornerRadius` (6 Light/Dark, 2 HighContrast) with a 2px `Focus` ring that fades in. |
| `TextBoxContentHost` | — | The `PART_ContentHost` (or `PART_EditorHost`) for a custom `ControlTemplate` on `TextBox`. |
| `RichTextBox` | `Text`, `Font`, `Foreground`, `IsReadOnly`, `Wrap`, `SelectionStart`, `SelectionLength`, `SetPlainText`, `Select`, `ToggleBold/Italic/Underline`, `DocumentChanged`, `SelectionBrush`, `SelectionForeground`, `CaretBrush`, `CaretThickness`, `CaretBlinkInterval` | Styled runs in a flat character buffer; draws via the visual text pipeline. `RichTextContentHost` is the template content part. Defaults to `InputCornerRadius`; a 2px `Focus` ring fades in. |
| `Image` | `Source` (`System.Drawing.Image?`), `Stretch` | Draws a bitmap in the tree. `BitmapImage`/`DrawingImage` also implement `ImageSource` for brushes/hosts. |

## Control templates

`TemplatedControl` applies `Template` first, then the style's template, then `DefaultTemplate` (a built-in fallback a control sets in code). A theme style can replace the whole appearance; self-drawn chrome stays inactive while a template is applied. Template triggers can read `ButtonBase.IsPointerOver`/`IsPressed`, `ToggleButtonBase.IsChecked` and `TemplatedControl.IsFocusVisible`.

Decorative template parts must set `IsHitTestVisible="False"`; that skips the part's own hit but keeps its children hittable, so an interactive child still receives input. The built-in default styles live in `Src/Easel/Themes/DefaultStyles.xaml`, match the exact control type (a derived type needs its own style or `BasedOn`), and apply when no explicit style exists. `TemplateBinding` clears the target for a null value or for `Color.Empty` reported by a Color-typed CLR property, so an unset brush does not paint transparent text.

## Selection and range

| Control | Key members | Notes |
| --- | --- | --- |
| `Button` | `Text`, `Font`, `Foreground`, `Command`, `CommandParameter`, `Click` | Activates by pointer, Space/Enter; executes `Command`. |
| `RepeatButton` | as `Button`, repeats while held | Delay/interval is built into the control. |
| `ToggleButton` | `IsChecked` (`bool`), `Command`, `Checked` | Checked pipe shared by all toggles; `Background`/`BorderBrush`/`BorderThickness`/`CornerRadius` drive the track and outline. |
| `CheckBox` | `IsChecked` (`bool?`), `Command`, `Checked` | Writes `true`/`false`; a `null` state is not produced by input. Background/Border/CornerRadius drive the box. |
| `RadioButton` | `IsChecked` (`bool?`), `GroupName`, `Command`, `Checked` | Group is scoped by `GroupName`; activating one clears its group siblings, and clicking the checked one again clears it. Background/Border drive the ring. |
| `Slider` | `Minimum`, `Maximum`, `Value`, `TrackBrush`, `FillBrush`, `ThumbBrush`, `FocusRingBrush`, `ValueChanged` | Pointer drag and arrow keys; bounded. |
| `ProgressBar` | `Minimum`, `Maximum`, `Value`, `TrackBrush`, `FillBrush` | Track and fill; bind `Value` from a slider/model. |
| `ScrollBar` | see layout table | Range control with a template and scroll events. |

## Items and data

| Control | Key members | Notes |
| --- | --- | --- |
| `ItemsControl` | `ItemsSource`, `ItemFactory`, `DisplayMemberPath`, `ItemTemplate`, `ItemTemplateSelector`, `ItemsPanel`, attached `IsItemsHost`, `ItemsRebuilt` | Base for list-like controls. `Items` are visual children unless a template/factory provides them. `RefreshItems()` rebuilds. |
| `ListBox` | all `ItemsControl` members + `SelectedIndex`, `SelectedItem`, `SelectionChanged` | Selected row uses the theme selection brush. The surface is transparent by default; `Background`/`BorderBrush`/`BorderThickness`/`Padding` are honored when set. |
| `VirtualizingPanel` | `ItemsSource`, `ItemExtent`, `Orientation`, `ItemTemplate`, `FirstRealizedIndex`, `RealizedCount`, `IScrollInfo` | Base for uniform-extent virtualized hosts: realizes the visible window and recycles items on scroll. Override `RealizeItem` / `RecycleItem` / `BindRecycledItem`; works with a pixel-scrolling `ScrollViewer` or its own `CanContentScroll` scrolling. The Gallery's `data.virtualizing-panel` is a custom subclass at 100k/1M rows; `VirtualizingPanelScenarioTests` covers bounded realization, recycled indices, source replacement and logical-offset clamping. |
| `VirtualList` | `ItemsSource`, `ItemHeight`, `ItemExtent`, `DisplayMemberPath`, `ItemTemplate`, `SelectedIndex`, `SelectedItem`, `SelectionChanged` | A `VirtualizingPanel` with selection; only visible rows are realized and scrolled rows are recycled. `ItemHeight` is an alias of `ItemExtent`; the extent is fixed. |
| `TreeView` | `Items` (`ObservableCollection<TreeViewItem>`), `SelectedItem`, `Indent`, `Font`, `SelectionChanged` | **No `ItemsSource`/hierarchical template**: build `TreeViewItem`s in code or XAML children. `Reveal(item)` expands ancestors and selects. `Background`/`BorderBrush`/`Padding` are honored; the default surface is transparent. |
| `TreeViewItem` | `Header`, `IsExpanded`, `IsSelected`, `Items`, `ParentItem`, `Depth`, `Font`, `Foreground` | Rows are real visual items; the tree owns their realization. |
| `ComboBox` | `Items`/`ItemsSource`, `SelectedIndex`, `SelectedItem`, `DisplayMemberPath`, `IsEditable`, `Text`, `IsDropDownOpen`, `MaxDropDownHeight`, `Placeholder`, `Foreground`, `SelectionChanged` | Drop-down opens in the surface overlay (not clipped by ancestors). `IsEditable` uses the text field; `Placeholder` is drawn by that field. `Foreground` and typography reach the projected drop-down rows; inline `ComboBoxItem`s keep their own. Hover paints a neutral `BorderHover` ring; open uses a 1.5px `Focus` ring, keyboard focus 2px, and the ring opacities fade in. |
| `Calendar` | `SelectedDate` (`DateTime?`), `DisplayMonth`, `FirstDayOfWeek`, `Foreground`, `SelectedDateChanged` | Month grid; only the selected day gets the accent fill. `Background`/`BorderBrush`/`Padding` are honored; day colours stay theme-driven. |
| `DatePicker` | `SelectedDate`, `Format`, `Placeholder`, `Foreground`, `IsDropDownOpen`, `SelectedDateChanged` | A date field whose `Calendar` opens in the overlay; `Placeholder` is drawn by its embedded TextBox. The field uses `InputCornerRadius` and a 2px `Focus` ring. |
| `CollectionView` | sort/filter/group, current item | `ItemsSource` may be an `IEnumerable`; a `CollectionView` or `CollectionViewSource` drives sorting/filtering/grouping. |
| `DataGrid` | `ItemsSource`, `Columns`, `RowHeight`, `HeaderHeight`, `RowNumberWidth`, `ShowRowNumbers`, `ShowGridLines`, `AlternateRows`, `SelectedIndex`/`SelectedItem`, `SortColumnIndex`/`SortDirection`, `SortBy`, `DragStarting`, `Dropped`, `GetRow`, `IndexOf`, `RealizedCellCount`, `RealizedCells` | Row-virtualized 2-D table. Columns with a cell template/factory realize visible cells as elements; the rest stay self-drawn. Header clicks sort asc → desc → source order; the header edge resizes a column; `AllowDrop` is on by default. Columns wider than the body scroll horizontally; the horizontal bar takes the bottom edge and the vertical bar ends above it. |
| `DataGridColumn` | `Header`, `BindingPath`, `Width`, `MinWidth`, `MaxWidth`, `StringFormat`, `TextAlignment`, `Kind`, `Foreground`, `IsSortable`, `CellTemplate`, `CellFactory`, `TextSelector`, `ColorSelector`, `ProgressSelector` | Plain column object, not a visual. `Width <= 0` or `NaN` shares the remaining room; `Kind` is `Text`, `Badge`, `Check` or `Progress`. `CellFactory` wins over `CellTemplate`; the row item is the cell's `DataContext`. |

## Menus, popups and overlays

| Control | Key members | Notes |
| --- | --- | --- |
| `VisualMenu` | `Items` (`IList<VisualMenuItem>`), `Foreground`, `OpenedMenu`, `Refresh()` | Menu bar; top-level items open drop-downs in the overlay. Call `Refresh()` after mutating `Items` while a template is applied. `Background`/`BorderBrush`/`CornerRadius`/`Padding` are honored. |
| `VisualMenuItem` | `Header`, `InputGestureText`, `IsEnabled`, `IsChecked`, `IsSeparator`, `Command` (`Action?`), `CommandPath`, `Click`, `Items` | Data item, not a visual child. Use `IsSeparator` for rules. |
| `VisualMenuItemContainer` | `Item`, `IsHighlighted`, `IsPressed`, `IsTopLevel`, `Foreground` | Realized row/template part. |
| `VisualContextMenu` | inherits `VisualPopup`; `OpenAt(target, surfacePoint)`, `Items` | Detached menu opened at a surface point. `Background`/`BorderBrush`/`CornerRadius`/`Padding` are honored; `Foreground` is pushed to the rows. |
| `VisualPopup` | `IsOpen`, `Child`, `PlacementTarget`, `Placement` (`VisualPlacement`), `HorizontalOffset`, `VerticalOffset`, `StaysOpen`, `MatchTargetWidth`, `Opened`, `Closed` | Hosted in the overlay above the root tree; not clipped by ancestor scroll viewers. `PlacementTarget` is set in code (no `ElementName` requirement). |

## Dialogs

| Control | Key members | Notes |
| --- | --- | --- |
| `VisualDialog` | `Options`, `ShowAsync(host)`, `Close(DialogResult)`, `IsOpen`, `Result`, `PromptText`, `Opened`/`Closed`, template parts | In-surface modal: 40% scrim, centered card (Surface, 1px ControlBorder, radius 12, shadow), 180 ms fade/slide, focus trap, native islands hidden while open. Template parts `PART_Icon`, `PART_Title`, `PART_Content`, `PART_Primary`, `PART_Secondary`, `PART_Cancel`, `PART_Close`. |
| `DialogService` | `ShowAsync(host, options)`, `MessageAsync`, `ConfirmAsync`, `PromptAsync` | Async results: `DialogResult.Primary/Secondary/Cancel`; prompt returns the text or `null`. `DialogResult` collides with `System.Windows.Forms.DialogResult` when both namespaces are imported. |

```csharp
var result = await DialogService.ConfirmAsync(
    surface, "Delete row?", "This cannot be undone.", "Delete", "Keep", DialogKind.Warning);
var name = await DialogService.PromptAsync(surface, "Rename", "Name", current);
```

`DialogOptions.Content` may be a `VisualElement` (default card path) or a string; with a template, string content fills
`PART_Content`. Dialogs stack; Enter activates the primary, Esc cancels when `DismissOnEscape`, outside clicks close
only when `DismissOnOutsideClick`.

## Shapes

`Rectangle`, `Ellipse`, `Line`, `Polygon`, `Polyline`, `Path` (`ShapeElement`):

| Property | Meaning |
| --- | --- |
| `Fill` | `Brush?`; gradient/image brushes allowed. |
| `Stroke` | `Brush?`; stroke brush. |
| `StrokeThickness` | `float`. |
| `X1`, `Y1`, `X2`, `Y2` | `Line` endpoints. |
| `Points` | `PointF[]` for `Polygon`/`Polyline` (`"0,0 10,0 10,10"`). |
| `Data` | SVG path data for `Path`. |

Shapes are controls here (not the lightweight `ShapeVisual`); use them for a handful of interactive/visible
shapes, and `VisualPresenter`/`Visual` for large decorative batches.

## Control defaults and state

The built-in appearance is the template set under `Src/Easel/Themes/Defaults/` (merged by
`Src/Easel/Themes/DefaultStyles.xaml`). The outer chrome reads the
control's appearance properties — `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding`, and
`Foreground`/typography where the control draws text — with the active palette as the fallback. Templates bind the
effective `ChromeBackground`/`ChromeBorderBrush`/`ChromeBorderThickness` values; roles that are not one outer chrome
have their own DPs (slider/progress track-fill-thumb, scrollbar track/thumb, expander/group-box/tab header, tab
selection line). State rows, focus rings, separators and the calendar day palette stay theme-driven. The property
contract table is in [default-style-audit.md](default-style-audit.md).

A content-only container (`ItemsControl`, `VirtualList`, `HeaderedContentControl`, `ContentControl`, `ScrollViewer`,
`CalendarItem`, `TabControl` content) and the carrying surface of a selectable container (`ListBox`, `TreeView`,
`Calendar`, `VisualMenu`) are transparent by default. `ListBox`/`TreeView`/`Calendar`/`CalendarItem`/
`HeaderedContentControl`/`DropZone`/`VisualContextMenu`/`VisualMenu` honor a host-set
`Background`/`BorderBrush`/`Padding`; `ItemsControl` and `VirtualList` are layout-only and stay transparent.
State rows, headers, text and focus rings keep their colours. A bare `ItemsControl` has no inner `ScrollViewer`,
so it never clips content at its own bounds; wrap it to scroll. Input chrome (`Button`, `TextBox`,
`ComboBox`/`DatePicker`, scrollbars) keeps its default look but yields to the same control properties; the input
controls read `InputCornerRadius` and fade their hover/focus rings. A control without a template falls back to
self-drawn appearance that reads `Theme.Current` (see [themes.md](themes.md)).

A surface `ResourceDictionary` overlay does not recolour built-in chrome; set the control property, the role DP,
or `Theme.Current` instead. Custom templates that read `{DynamicResource}` do follow the overlay. Do not hard-code
colors in an app that should follow the palette. Controls expose hover/pressed/selected/focused state as DPs or
template triggers; `ButtonBase` exposes `IsPointerOver`/`IsPressed`, `ToggleButtonBase` `IsChecked`, list/tree
items selected state.

## Code-behind notes

- `TreeView`, `Calendar`/`DatePicker` initial dates, `ComboBox` items, `DataGridColumn` selectors
  (`TextSelector`/`ColorSelector`/`ProgressSelector`) and `NativeControlHost.Child` need code-behind after
  `InitializeComponent`. A `<native:…>` element is declarative: `x:Name.Native` gives the typed control.
  `DataGrid` columns themselves are declarative (`<DataGrid.Columns>…`).
- `VisualPopup` placement target is a code reference; set it in the click handler.
- `TabControl.Refresh()` rebuilds the selected page after mutating `Items`.
- `RichTextBox` content is set with `SetPlainText`/`Select`/formatting methods.
- Do not add WinForms controls except through a `native:` element (XAML) or `NativeControlHost` (code-behind).
