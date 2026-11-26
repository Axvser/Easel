# Default style property contract

Default templates paint their outer chrome from the control's own appearance properties. `ChromeBackground`,
`ChromeBorderBrush`, `ChromeBorderThickness` and `ChromeBorderWidth` are the effective values a template binds:
the local `Background`/`BorderBrush`/`BorderThickness` when set, otherwise the palette role the control declares
through `SetChromeTheme`. `Theme.Changed` refreshes them; local values and style setters win. Role DPs cover
internal parts that are not one outer chrome. State rows, focus rings, separators and the calendar day palette
stay theme-driven.

A `ResourceDictionary` overlay on a surface does not recolour the outer chrome, which is computed from
`Theme.Current`; set the control property or the role DP, or assign `Theme.Current`, instead. Template parts that
read `{DynamicResource}` — hover/pressed bands, focus rings, separators — still follow the overlay.

| control | outer chrome | role DPs / notes |
| --- | --- | --- |
| `Button`, `RepeatButton` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding`, `Foreground`, `Text`, `Font` | Background paints below Hover/Pressed; the border paints above them. |
| `ToggleButton`, `CheckBox`, `RadioButton` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius` drive the track/box/ring | Checked fill, check mark, knob and dot stay `Accent`/`AccentText`. |
| `TextBox`, `RichTextBox` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding` | `Foreground` and font are read by the content host. Radius defaults to `InputCornerRadius` (6 Light/Dark, 2 HighContrast); a 2px `Focus` ring fades in. |
| `ComboBox` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding` | `Foreground` and typography reach the projected drop-down rows; inline `ComboBoxItem`s keep their own. The editable part is a chrome-less TextBox (`Placeholder` supplied). Radius defaults to `InputCornerRadius`; the hover ring is neutral `BorderHover`, the open ring 1.5px `Focus`, keyboard focus 2px `Focus`; ring opacities use each ring's element `Transitions` collection (150 ms, CubicOut). |
| `DatePicker` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding` | The embedded `PART_TextBox` is chrome-less; `Placeholder` is drawn by it and `Foreground` is pushed. Radius defaults to `InputCornerRadius`; the focus ring is 2px `Focus`. |
| `ListBox`, `TreeView` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding` | Transparent by default; a host-set chrome paints. `ListBoxItem` also inherits the ContentControl base chrome; row state bands stay theme. |
| `ItemsControl`, `VirtualList` | none | Layout-only realization hosts; `Background`/`BorderBrush`/`Padding` are not applied. Wrap to add a surface or scrolling. |
| `Expander` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding` | `HeaderBackground`, `HeaderForeground`, `HeaderBorderBrush`, `HeaderBorderThickness` cover the header. |
| `GroupBox` | same as Expander | Same header DPs; the frame is the control chrome. |
| `TabControl` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding` | The content host is inset by `Padding`; the header row reuses `TabItem`. |
| `TabItem` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding` | `HeaderForeground` and `SelectionLineBrush`; hover/selection bands stay theme. |
| `HeaderedContentControl` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding` | `HeaderForeground`; the content host is inset by `Padding`. |
| `Slider` | none (role chrome) | `TrackBrush`, `FillBrush`, `ThumbBrush`, `FocusRingBrush`. |
| `ProgressBar` | `CornerRadius` | `TrackBrush`, `FillBrush`. |
| `ScrollBar` | none (role chrome) | `TrackBrush`, `ThumbBrush`, `ThumbHoverBrush`, `ThumbRadius`. |
| `Calendar` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding` | `Foreground` is pushed to the month item; day/weekend/today colours stay theme. |
| `CalendarItem` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding` | `Foreground` covers the month title and day numbers. |
| `DataGrid` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding` | Code-painted only when set. Rows, headers, cells, badge/check/progress colours have their own DPs. |
| `VisualContextMenu` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius` | `Padding` insets the presenter; `Foreground` is pushed to rows; row state bands stay theme. |
| `VisualMenu` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding` | `Foreground` is pushed to rows. |
| `VisualMenuItemContainer` | none | Row bands, check mark, submenu arrow and separator stay theme. |
| `DropZone` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding` | The dashed/hot affordance stays theme-driven. |
| `ScrollViewer` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding` | ContentControl base paints the chrome; `Padding` insets the template root. |
| `VisualPopup` | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius`, `Padding` | Base chrome seeded from the palette; local values win. |

`Border`, `TextBlock`, `ContentControl` and other template-less elements paint `Background`/`BorderBrush` directly
through the base renderer.
