# XAML language, resources, styles and bindings

The XAML compiler is a Roslyn source generator over a deliberate **subset**, not a runtime XAML loader. It reads
each `.xaml` `AdditionalFile`, resolves every element/property against the compilation, and writes ordinary C#
construction into a private `InitializeComponent()`. Unsupported markup is reported and skipped; there is no
fallback parser.

## Root and code-behind

Two roots are supported:

1. A **visual view**: any type deriving `VisualElement`.
2. A **standalone resource dictionary**: `<ResourceDictionary x:Class="…">` deriving `Easel.ResourceDictionary`.

```csharp
public sealed partial class MyView : UserControl
{
    public MyView() => InitializeComponent();
}
```

- `x:Class` must name a `partial` type in the compilation (`EASEL_XAML009`).
- The owner must derive from the root type (`EASEL_XAML011`).
- The root must be a visual type or `ResourceDictionary`; a non-visual root is rejected.
- `InitializeComponent` is generated, private and non-partial. Do not declare it (`EASEL_XAML015`).
- Without `x:Class`, the file is not consumed by this generator (`EASEL_XAML005`).

## Namespaces and type resolution

```xml
xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
xmlns:local="clr-namespace:MyApp.Views"
```

| Written | Resolves to |
| --- | --- |
| `Foo` (no prefix) | `Easel.Visuals.Controls.Foo`, then `.Visuals.Controls.Shapes.Foo`, then `.Visuals.Foo`, then `Easel.Controls.Foo`, `Easel.Controls.Shapes.Foo`, `Easel.Foo` |
| `local:Foo` | the `clr-namespace` type |
| `v:Foo` | legacy alias for the Easel visual namespaces |
| `native:Foo` (built-in prefix; no `xmlns:native` declaration) | a native control wrapped in `NativeElement<Foo>`; layout attributes target the wrapper, the rest target the control |

An unprefixed name that is not an Easel type is `EASEL_XAML019`; write `local:`. An unprefixed local type is
never silently accepted.

## Native controls

Write the built-in `native:` prefix; no `xmlns:native` declaration is needed. The author never writes a host:

```xml
<native:GroupBox Text="Native group" Margin="12" Width="240">
  <StackPanel Spacing="6">
    <TextBlock Text="Visual content inside a native container" />
    <native:Button Text="Native button" Click="OnNativeClick" />
  </StackPanel>
</native:GroupBox>
```

- Layout attributes (`Margin`, `Width`/`Height`, `Min`/`Max`, `HorizontalAlignment`, `VerticalAlignment`,
  `Visibility`, `IsEnabled`, `DataContext`, attached properties) write the wrapper `NativeElement`, so they use
  the normal measure/arrange pass.
- Every other attribute (`Text`, `BackColor`, `Padding`, `AutoSize`, `Tag`, …) and any event writes the native
  control.
- A native element hosts one visual root (`Content`). Several native children are allowed and are wrapped in an implicit vertical `StackPanel` in document order; nest an explicit panel when another layout is needed.
- `x:Name` is the wrapper; `Name.Native` is the typed control.
- A native CLR property binding goes through `NativePropertyBinding.For(type, property)`: the binding writes a proxy property on the wrapper and the control is updated. `Mode=TwoWay`/`OneWayToSource` reports back through `INotifyPropertyChanged` or the conventional `XxxChanged` event; a control with neither calls `NativePropertyBinding.NotifyChanged(control, nameof(...))` (or `wrapper.NotifyNativePropertyChanged(...)`) after changing the value — nothing polls. The native `Enter`/`Leave` raise the wrapper's logical `GotFocus`/`LostFocus`, so `UpdateSourceTrigger=LostFocus` behaves exactly like a visual target.
- `Opacity`, `Transform` and arbitrary visual clip only apply in snapshot mode (`IsSnapshot="True"`): the control is painted as a bitmap in the visual pass and takes part in composition. A live HWND rejects `WS_EX_LAYERED` and affine transforms. In a Form, snapshot mode also creates an almost invisible input proxy (`Opacity = 1/255`, a fully transparent window is click-through) so real mouse/keyboard/IME reach the control: opacity/translation are exact, a scale re-lays the control out at the scaled size (hit areas match), and rotation keeps the transformed polygon interactive (whole-control hits exact, item positions approximate). Call `RefreshSnapshot()` after changing the control.
- `CornerRadius` rounds the live window through its window region (aliased). An open popup cuts its rectangle out of the island's region, so it can cover the island.

## Elements, children and properties

- Attributes set properties; an attribute matching a CLR event subscribes a handler.
- Property elements: `<Grid.RowDefinitions>`, `<Border.Background>`, `<StackPanel.Resources>`,
  `<TextBlock.Text>` etc.
- Attached properties: `Grid.Row="1"`, `Canvas.Left="20"`, `DockPanel.Dock="Top"`,
  `Grid.RowSpan="2"`, or the property-element form `<Grid.Row>1</Grid.Row>`.
- Children follow the parent:
  - A panel/collection container: `.Add(child)` for visual children, or `.Items.Add` when the parent exposes an
    items collection (`Items`).
  - A single-content control: `.Content = child` (e.g. `ContentControl`, `ScrollViewer`, `Expander`,
    `TabItem`); `Border`/`GroupBox` use `.Child`.
  - A visual parent rejects non-visual children.
- Text content: `<TextBlock>text</TextBlock>` projects into `Text`; it is written after attributes, so do not
  combine it with a `Text`/`Content` attribute.
- `x:Name` declares a private field and writes `VisualElement.Name`; names are namescope-local in templates.

```xml
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="*" />
  </Grid.RowDefinitions>
  <TextBlock Grid.Row="0" Text="Header" />
  <Border Grid.Row="1" Background="{StaticResource Surface}" />
</Grid>
```

## Literals

Supported literal parsers include: `string`, all integer/float/double/decimal types, `bool`, enums (name or
number), `System.Drawing.Color` (`#RGB`, `#RRGGBB`, `#AARRGGBB`, known names), `Padding`, `Thickness`,
`GridLength` (`Auto`, `*`, `2*`, pixels), `Size`, `SizeF`, `Point`, `PointF`, `TimeSpan`. `DateTime` has no
XAML literal; use code-behind. `Brush`, `Geometry`, `ImageSource`, `PointF[]`, `RectangleF` (`x,y,w,h`) and
`Rectangle` are runtime-converted from their string forms (`#FF…`, SVG path data, a file path).

Markup extensions: `{StaticResource Key}`, `{DynamicResource Key}`, `{Binding …}`,
`{TemplateBinding …}`, `{x:Null}`, `{x:Static Member}`, `{x:Type TypeName}` where a type is expected.

## Resources

### On a visual element

```xml
<UserControl.Resources>
  <ResourceDictionary>
    <SolidColorBrush x:Key="Accent" Color="#FF0078D4" />
    <Style x:Key="Card" TargetType="Border">
      <Style.Setters>
        <Setter Property="Background" Value="{StaticResource Surface}" />
        <Setter Property="CornerRadius" Value="8" />
      </Style.Setters>
    </Style>
    <ControlTemplate x:Key="Chrome" TargetType="ContentControl">…</ControlTemplate>
    <DataTemplate x:Key="Row">…</DataTemplate>
  </ResourceDictionary>
</UserControl.Resources>
```

The `<ResourceDictionary>` wrapper is optional; direct resources under `X.Resources` work too. Keys must be
declared before use in the same file. Resolution walks the element, its ancestors, then `VisualHost.Resources`.

### Standalone dictionary

```xml
<ResourceDictionary xmlns="…" xmlns:x="…" x:Class="MyApp.Themes.MyTheme">
  <SolidColorBrush x:Key="Window" Color="#FF141414" />
  <x:Double x:Key="CardRadius">8</x:Double>
  <Style x:Key="Heading" TargetType="TextBlock">…</Style>
</ResourceDictionary>
```

The generated class derives `ResourceDictionary`; construct it and read/merge it:

```csharp
var theme = new MyApp.Themes.MyTheme();
resources.MergedDictionaries.Add(theme);
```

`MergedDictionaries` accepts compiled `ResourceDictionary` types or inline dictionaries. **File-URI
`Source="…xaml"` is not supported** — there is no runtime loader. Merge a compiled type or code-built dictionary.

Standalone dictionaries also support `x:String`, `x:Int32`, `x:Int64`, `x:Double`, `x:Single`, `x:Decimal`,
`x:Boolean`, `x:Color`, `x:FontWeight`, `x:Thickness`, `x:PointF`, brushes and styles/templates. An implicit style (no `x:Key`) inside a standalone
dictionary is stored by its `TargetType` key but is **not** auto-applied to the visual tree; declare implicit
styles on a visual element's `Resources` or `Styles`.

### Static vs dynamic

- `{StaticResource Key}` resolves once. A key declared later in the same file is still missing at that point.
- `{DynamicResource Key}` stays subscribed to reachable dictionaries. Add/replace/remove updates the target; a
  removed key clears a DP back to its default (or stops a CLR-only write).
- A later resource reference for the same target property **supersedes** an earlier one, regardless of
  static/dynamic kind: a later static value stops an earlier dynamic subscription; a later dynamic reference
  resumes tracking. This is the "last writer wins" rule for resource references.
- Style/Trigger **setter values** are resolved once when the style is declared; a `DynamicResource` value in a
  setter is reported as unsupported. A dynamic reference belongs on the element's property.

```xml
<Border Background="{DynamicResource Accent}" />
<!-- later, in code -->
Resources["Accent"] = new SolidColorBrush(newColor); // the border updates
```

## Styles, triggers and templates

```xml
<Style x:Key="PrimaryButton" TargetType="Button">
  <Style.Setters>
    <Setter Property="Background" Value="{StaticResource Accent}" />
    <Setter Property="Foreground" Value="{StaticResource AccentText}" />
    <Setter Property="Height" Value="32" />
  </Style.Setters>
  <Style.Triggers>
    <Trigger Property="IsEnabled" Value="False">
      <Trigger.Setters>
        <Setter Property="Background" Value="{StaticResource Disabled}" />
      </Trigger.Setters>
    </Trigger>
  </Style.Triggers>
</Style>
```

- `Style.Setters` accepts wrapped `<Style.Setters><Setter/></Style.Setters>` or direct `<Setter/>` children.
- Setters write the `Style` layer; triggers write `StyleTrigger`. A local value still wins.
- `Setter.Value` accepts a literal, `{StaticResource}`, `{Binding}`, `{x:Static}` or `{x:Null}`.
- A setter targets a dependency property; `Property` names a DP-backed CLR property.
- An implicit style has `TargetType` and no `x:Key`. It applies to matching descendants when declared on a
  visual element's resources/styles (`Style="{StaticResource …}"` for explicit).
- `Style.BasedOn` accepts `{StaticResource Key}` to merge a base style.
- `Trigger` compares one DP of the styled element. `DataTrigger` and `MultiTrigger` are not supported on the
  visual backend (`EASEL_XAML004`); bind or convert in the view model instead.
- `<Trigger.EnterAction>` / `<Trigger.ExitAction>` run a `TransitionAction`, `KeyFrameAnimation`,
  `TriggerActionGroup`, or a custom `VisualTriggerAction` derived type. In a template trigger the action target is
  the same `TargetName` part as the setters.

### ControlTemplate

```xml
<ControlTemplate x:Key="CardChrome" TargetType="ContentControl">
  <Border x:Name="PART_Frame" Background="{StaticResource Surface}" Padding="12">
    <StackPanel Spacing="6">
      <TextBlock Text="Chrome" />
      <ContentPresenter />
    </StackPanel>
  </Border>
</ControlTemplate>
```

- Exactly one visual root; `x:Name` parts are registered in a per-instance `VisualNameScope` and read with
  `GetTemplateChild<T>("PART_…")`.
- `ContentPresenter` shows the templated control's `Content`; `TemplateBinding` reads the `TemplatedParent`.
- Template triggers write the `Template` layer and may target a named part with `TargetName`.

### Data/Items templates

- `ContentTemplate`, `ItemTemplate`, `ItemsPanel` accept property-element templates or keyed resources.
- A keyed `<DataTemplate>` compiles to a `DataTemplate` factory; `VisualTemplateSelector`/`DataTemplateSelector`
  chooses at runtime.
- `ItemsPanelTemplate` must have one `Panel` root.
- `DataGridColumn.CellTemplate` / `CellFactory` turns a visible cell into a visual element (the row item is its
  `DataContext`); columns without one keep the self-drawn `Text`/`Badge`/`Check`/`Progress` rendering.

## Bindings

```xml
<TextBlock Text="{Binding Title}" />
<TextBox Text="{Binding Name, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
<TextBlock Text="{Binding Amount, StringFormat=N2}" />
<Button Command="{Binding SaveCommand}" />
```

`Binding` supports path/dotted paths/simple indexers, `Source`, `ElementName` (namescope-wide), `RelativeSource`
(`Self`, `TemplatedParent`, `FindAncestor` with `AncestorType`/`AncestorLevel`), one-way/two-way/one-time/
one-way-to-source, `Converter`/`ConverterParameter`/`ConverterCulture`, `StringFormat`, `FallbackValue`,
`TargetNullValue`, `UpdateSourceTrigger`, `ValidatesOnExceptions`, `NotifyOnValidationError`,
`ValidatesOnNotifyDataErrors`, `ValidatesOnDataErrors`.

`DataContext` is inherited; a local `DataContext` cuts inheritance for that subtree. Bindings attached before
parenting re-resolve when the element joins a DataContext chain. `VisualBindingOperations.Bind(element, dp,
binding)` is the code path; `element.SetBinding(dp, binding)` is the fluent form.

A failed binding raises the static `VisualBindingOperations.BindingFailed` event (`BindingFailureEventArgs`);
a validation error raises `Validation.Error` (`ValidationErrorEventArgs`) when the binding sets
`NotifyOnValidationError`. Neither is silently swallowed.

### Compiled bindings

Data-context paths can be compiled into direct property access. Enable it at the view root (or any subtree):

```xml
<UserControl
    xmlns:vm="clr-namespace:MyApp.ViewModels"
    x:DataType="vm:SettingsViewModel"
    x:CompileBindings="True">
  <TextBox Text="{Binding DisplayName, Mode=TwoWay}" />
</UserControl>
```

- `x:CompileBindings` is inherited; `x:CompileBindings="False"` opts a subtree out. Setting it without an
  `x:DataType` in scope is `EASEL_XAML020` for each data-context binding with a path.
- `x:DataType` is also inherited. A `DataTemplate` sets its own item type (`x:DataType="local:Row"`), which
  covers every binding in the template body.
- Compiled bindings still use `INotifyPropertyChanged`, converters, `StringFormat`, fallback values and
  validation. The generated reader walks the path through typed accessors, not `TypeDescriptor`.
- The generator checks a path when the member is visible in its input compilation. Members added by another
  source generator (for example `[VeloxProperty]`) defer to the C# compiler, which still fails the build on a typo.
- `Source`, `ElementName`, `RelativeSource` and `TemplateBinding` stay runtime bindings; they are not compiled
  against `x:DataType`.
- Simple indexers compile through runtime helpers: arrays, `IList<T>` and `IDictionary<TKey, TValue>`. A
  non-generic `IList`/`IDictionary` reads as `object`; an intermediate one falls back to reflection with
  `EASEL_XAML024`. Other indexers fall back the same way.
- A two-way compiled binding needs an accessible setter and a non-boxed owner; otherwise `EASEL_XAML022`.

## Events

Any CLR event on a visual type can be set as an attribute:

```xml
<Button Text="Run" Click="OnRunClick" />
```

```csharp
private void OnRunClick(object? sender, EventArgs e) { … }
```

The handler must be unambiguous and match the event's delegate signature. Pointer/key/text events use
`VisualPointerEventArgs` / `VisualKeyEventArgs` / `VisualTextInputEventArgs`.

## Compiler diagnostics

| ID | Meaning |
| --- | --- |
| `EASEL_XAML001` | unknown XAML type |
| `EASEL_XAML002` | unknown property/event member |
| `EASEL_XAML003` | value cannot be converted |
| `EASEL_XAML004` | unsupported construct |
| `EASEL_XAML005` | missing `x:Class` |
| `EASEL_XAML006` | `x:Class` type not found |
| `EASEL_XAML007` | duplicate `x:Name` |
| `EASEL_XAML008` | missing event handler |
| `EASEL_XAML009` | `x:Class` type not partial |
| `EASEL_XAML010` | unsupported resource |
| `EASEL_XAML011` | owner is not a visual type, or the root is not assignable from the owner |
| `EASEL_XAML012` | XML parse error |
| `EASEL_XAML013` | unknown attached property |
| `EASEL_XAML014` | invalid style `TargetType` |
| `EASEL_XAML015` | `InitializeComponent` already declared |
| `EASEL_XAML016` | ambiguous event handler |
| `EASEL_XAML017` | invalid handler signature |
| `EASEL_XAML018` | ambiguous type |
| `EASEL_XAML019` | unprefixed type is not an Easel type |
| `EASEL_XAML020` | compiled binding has no `x:DataType` in scope |
| `EASEL_XAML021` | compiled binding path member is unknown or inaccessible |
| `EASEL_XAML022` | compiled binding leaf cannot be written |
| `EASEL_XAML023` | invalid `x:DataType` / `x:CompileBindings` value |
| `EASEL_XAML024` | compiled binding path is unsupported and falls back to reflection |
| `EASEL_XAMLP001`–`005` | property generator: non-partial, wrong owner, invalid attached accessors, name conflict, missing default value |

`EASEL_XAML003/004/007/008/010/011/013/016/017/024` are warnings: the element/attribute is skipped and the build
continues. Every other `EASEL_XAML*` ID and `EASEL_XAMLP*` is an error. Treat the warning IDs as errors in
practice.

## Not supported

- Runtime XAML loading/`Source` URIs, designer, hot reload.
- `{DynamicResource}` in style/trigger setter values or trigger-action properties.
- `DataTrigger`/`MultiTrigger` (use a `Trigger` on a DP, or convert in the view model).
- Elements from non-Easel assemblies without a `clr-namespace` prefix; unprefixed local types.
- Live native controls inside transformed/blended subtrees (set `IsSnapshot="True"`).
- Anti-aliased arbitrary-shape clipping of a live HWND (`IsSnapshot` + a visual `Clip`, or `CornerRadius` which is aliased).
- `DateTime` literals, arbitrary object constructors without a public parameterless constructor.
- Implicit styles from a standalone dictionary applied automatically (declare them on a visual element instead).
- Animation of a property type the engine cannot interpolate (see [animation.md](animation.md)).
