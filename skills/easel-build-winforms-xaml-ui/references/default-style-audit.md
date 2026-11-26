# Built-in default style audit (3.0.0)

Rule: a **content-only container** must not get a fallback colour. Its fallback may host content and provide
structure, but it must not paint a background, border or fill of its own. **Control chrome** that is the control's
purpose (a button's surface, an input's frame, a popup card, a scrollbar) keeps its colours. A **selectable
carrying surface** (selection list, tree, table, calendar, menu bar) is `transparent / host-supplied`: its state
rows, headers, text and focus rings stay, but the fill behind them is supplied by whoever hosts the control. A
popup menu card is control chrome, not a host-supplied surface: it floats without a host, and its submenus are
created internally sharing only the template, not a parent `Background`.

A content-only container is a control whose job is to host arbitrary content (`ItemsControl`,
`HeaderedContentControl`, `ContentControl`, `VirtualList`, `ScrollViewer`, `CalendarItem`, `TabControl` content).
Control chrome is appearance that is the control's own purpose or interactive affordance (`Button`, `TextBox`,
`ComboBox`/`DatePicker` field, scrollbars, `GroupBox` frame, `Expander` header). Ambiguous = a hybrid; marked for a
decision instead of silently changed.

| file | target type | template/setters | background | border | foreground/fill | container? | verdict | action |
|---|---|---|---|---|---|---|---|---|
| DefaultStyles.xaml | — | merge hub for 10 dictionaries | — | — | — | — | n/a | no action |
| ButtonDefaults.xaml | Button, RepeatButton (`ButtonChromeStyle`) | template: Chrome + Hover/Pressed + FocusRing | Chrome `Input`; Hover `Hover`; Pressed `Pressed` | `Border` 1 | text `Foreground`; focus `Focus` | N | keep chrome | none |
| ButtonDefaults.xaml | ToggleButton | template | track `Input`; checked `Accent` | `Border` 1 | knobs `Muted`/`AccentText` | N | keep chrome | none |
| ButtonDefaults.xaml | CheckBox | template | box `Input`; checked `Accent` | `Border` stroke 1 | mark `AccentText` | N | keep chrome | none |
| ButtonDefaults.xaml | RadioButton | template | ring `Input`; checked `Accent` dot | `Border` stroke | dot `Accent` | N | keep chrome | none |
| TextInputDefaults.xaml | TextBox | template | `Input` | `Border` 1 | content `Foreground` | N | keep chrome | none |
| TextInputDefaults.xaml | RichTextBox | template | `Input` | `Border` 1 | content `Foreground` | N | keep chrome | none |
| ComboBoxDefaults.xaml | ComboBox | template | Chrome `Input` | `ControlBorderThickness` | chevron `Text`/`Foreground` | ambiguous | keep chrome (input+items) | none |
| ComboBoxDefaults.xaml | ComboBoxItem | setters: Padding/alignment/wrap | none | none | inherited text | N (row) | transparent | none |
| ContainersDefaults.xaml | Expander | template; header Border, content host | header `Raised`; content host none | header `Border` 1 | header text | Y | keep header chrome; content transparent | none |
| ContainersDefaults.xaml | GroupBox | template; frame Border + header chip | header chip `Surface`; frame none | frame `Border` 1 | header text | Y | ambiguous | recommend keep (frame is the control's identity); no content fill |
| ContainersDefaults.xaml | TabItem (keyed + implicit) | template; hover/selection bands | `Hover`/`Raised`; line `Accent` | none | labels | N | keep chrome | none |
| ContainersDefaults.xaml | TabControl | template; header host + 1px separator | none (content); separator `Border` | separator line only | — | Y | keep structural | none |
| RangeDefaults.xaml | Slider | template `SliderChrome` | track `SurfaceAlt`; fill/thumb `Accent` | none | `Accent`, focus `Focus` | N | keep chrome | none |
| RangeDefaults.xaml | ProgressBar | template `ProgressBarChrome` | track `SurfaceAlt`; fill `Accent` | none | `Accent` | N | keep chrome | none |
| ItemsDefaults.xaml | **ItemsControl** | template: bare `ItemsPresenter` (no inner ScrollViewer) | **was `Input` → none** | **was `Border` 1 → none** | — | **Y** | **transparent, unclipped** | no ScrollViewer so content is not clipped at the control bounds; host wraps it to scroll |
| ItemsDefaults.xaml | ListBox | template: Transparent Border + FocusRing + ScrollViewer + presenter | **was `Input` → Transparent** | **was `Border` 1 → none**; focus `Focus` | selection via item | N | **fixed: transparent / host-supplied** | surface Border kept only for empty-area hit-testing/focus |
| ItemsDefaults.xaml | ListBoxItem | template: Hover/Selection bands | `Hover`/`Selection` only | none | content | N (row) | keep (state-driven) | none |
| ItemsDefaults.xaml | VirtualList | template: ItemsPresenter only | none | none | — | Y | transparent | none |
| ItemsDefaults.xaml | TreeView | template: Transparent Border + FocusRing + presenter | **was `Surface` → Transparent** | **was `Border` 1 → none**; focus `Focus` | — | N | **fixed: transparent / host-supplied** | surface Border kept only for empty-area hit-testing |
| ItemsDefaults.xaml | TreeViewItem | template bands + chevrons | `Raised`/`Selection` | none | chevron `Muted` | N | keep (state-driven) | none |
| DataGridDefaults.xaml | DataGridBadgeCell/CheckCell/ProgressCell + `DataGridCellChrome` | setters (hit-test/focus only) | none | none | grid owns drawing | N | transparent | none |
| DataGridDefaults.xaml | DataGrid | template: empty region Borders; ctor self-draws | **was `Surface` (ctor) → none** | **was `ControlBorder` 1 (ctor) → none** | header/rows/gridlines/text stay | N | **fixed: transparent / host-supplied** | removed ctor chrome; `RenderContent` paints only a host-set brush |
| CalendarDefaults.xaml | Calendar | template Border + CalendarItem; ctor clears | **was `Surface` → Transparent/none** | **was `ControlBorder` → none** | header/day content stays | N | **fixed: transparent / host-supplied** | template Border kept structural (`IsHitTestVisible=false`); ctor sets no surface |
| CalendarDefaults.xaml | CalendarItem | template; nav glyphs + grids | none | none | nav `Muted`, header `Foreground` | Y | transparent | none |
| CalendarDefaults.xaml | DatePicker | template | `Input` | `ControlBorderThickness` | text | N | keep chrome | none |
| MenuDefaults.xaml | VisualContextMenu | template; Chrome + HoverRing + presenter | `SurfaceAlt` | `Border` 1 | rows/hover ring stay | N | keep chrome (popup card) | no host backdrop; submenus share only the template, so the surface stays on the default style and ctor |
| MenuDefaults.xaml | VisualMenu | template | **was `Raised` → Transparent** | **was bottom `Border` 1 → none** | top-level item content/state stays | N | **fixed: transparent / host-supplied** | its dropdown is a `VisualContextMenu`, which keeps its popup surface |
| MenuDefaults.xaml | VisualMenuItemContainer | template bands/separator/checkmark | `Selection`/`Pressed` | separator `Border` | `Accent`, `Muted` | N | keep (state-driven) | none |
| MiscDefaults.xaml | ScrollBar | template track only | track `SurfaceAlt` | none | thumb self-drawn | N | keep chrome | none |
| MiscDefaults.xaml | HeaderedContentControl | template: header text + content host | none | none | header 0.65 opacity | Y | transparent | none |
| MiscDefaults.xaml | DropZone | template `DropZoneChrome` | self-drawn (dashed border; hot `Accent`) | self-drawn | text | ambiguous | keep chrome (drop affordance) | none |
| Theme.cs | palette + typography resources | no container styles; `AddTypographyStyles` targets `TextBlock` | — | — | — | — | n/a | no action |
| DarkTheme.xaml / LightTheme.xaml | palette + named `TextBlock` styles | no container styles | — | — | typography only | — | n/a | no action |
| Controls ctor (Calendar.cs, DataGrid.cs, Menu.cs, Popup.cs) | VisualPopup (ComboBox/DatePicker host) | base popup chrome | `Surface` | `ControlBorder` 1 | per control | N | keep (popup cards) | Calendar/DataGrid set no surface; VisualContextMenu keeps `SurfaceAlt`/`ControlBorder` |
| ScrollViewer.cs `DefaultChrome` | ScrollViewer | code template: Grid + presenter + two bars | none | none | — | Y | transparent structural | none |
| LayoutExtras.cs (`GridSplitter.OnRender`) | GridSplitter sash | self-drawn unless `Background` set | `SurfaceAlt` | none | — | N | keep chrome | none |
| InteractiveControls.cs:2038 | visual TextBox native editor | nearest ancestor background else `ControlBackground` | `ControlBackground` fallback | — | — | N | keep chrome | none |
| NativeElement.cs:950 | native island backcolor | native surface | `ControlBackground` | — | — | N (HWND island) | keep | none |
| ContentControls.cs | ContentControl / UserControl | no default style/template | none (Background default null) | none | — | Y | transparent | none |
