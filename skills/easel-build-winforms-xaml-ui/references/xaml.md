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
| `Foo` (no prefix) | `Easel.Visuals.Controls.Foo`, then `.Visuals.Controls.Shapes.Foo`, then `.Visuals.Foo`, then shared primitives (`Easel.Controls`) |
| `local:Foo` | the `clr-namespace` type |
| `v:Foo` | legacy alias for the Easel visual namespaces |
| `native:Foo` / `System.Windows.Forms` | rejected; visual parents accept `VisualElement` only |

An unprefixed name that is not an Easel type is `EASEL_XAML019`; write `local:`. An unprefixed local type is
never silently accepted.

## Elements, children and properties

- Attributes set properties; an attribute matching a CLR event subscribes a handler.
- Property elements: `<Grid.RowDefinitions>`, `<Border.Background>`, `<StackPanel.Resources>`,
  `<TextBlock.Text>` etc.
- Attached properties: `Grid.Row="1"`, `Canvas.Left="20"`, `DockPanel.Dock="Top"`,
  `Grid.RowSpan="2"`, or the property-element form `<Grid.Row>1</Grid.Row>`.
- Children follow the parent:
  - A panel/collection container: `.Add(child)` for visual children, or `.Items.Add` when the parent exposes an
    items collection (`Items`, `DropDownItems`).
  - A single-content control: `.Content = child` (e.g. `ContentControl`, `ScrollViewer`, `GroupBox`,
    `Expander`, `TabItem`); `Border` uses `.Child`.
  - A visual parent rejects non-visual children.
- Text content: `<TextBlock>text</TextBlock>` projects into `Text`; an explicit `Text="…"` overrides it.
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
`GridLength` (`Auto`, `*`, `2*`, pixels), `Size`, `SizeF`, `Point`, `PointF`, `PointF[]`,
`RectangleF` (`x,y,w,h`), `Rectangle`, `TimeSpan`. `DateTime` has no XAML literal; use code-behind.
`Brush`, `Geometry`, `ImageSource` and `PointF[]` are runtime-converted from their string forms
(`#FF…`, SVG path data, a file path).

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
- `Trigger` watches one DP; `DataTrigger` watches a binding path; `MultiTrigger` requires all conditions.
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
binding)` is the code path; `element.SetBinding(...)` and `element.Bind(...)` are the fluent forms.

Validation failures are surfaced through `BindingFailureEventArgs`/`ValidationErrorEventArgs` on the target and
are not silently swallowed.

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
| `EASEL_XAML002` | unknown property/event member (Error) |
| `EASEL_XAML003` | value cannot be converted |
| `EASEL_XAML004` | unsupported construct |
| `EASEL_XAML005` | missing `x:Class` |
| `EASEL_XAML006` | `x:Class` type not found |
| `EASEL_XAML007` | duplicate `x:Name` |
| `EASEL_XAML008` | missing event handler |
| `EASEL_XAML009` | `x:Class` type not partial |
| `EASEL_XAML010` | unsupported resource |
| `EASEL_XAML011` | root not assignable from owner |
| `EASEL_XAML012` | XML parse error |
| `EASEL_XAML013` | unknown attached property |
| `EASEL_XAML014` | invalid style `TargetType` |
| `EASEL_XAML015` | `InitializeComponent` already declared |
| `EASEL_XAML016` | ambiguous event handler |
| `EASEL_XAML017` | invalid handler signature |
| `EASEL_XAML018` | ambiguous type |
| `EASEL_XAML019` | unprefixed type is not an Easel type |
| `EASEL_XAMLP001`–`005` | property generator: non-partial, wrong owner, invalid attached accessors, name conflict, missing default value |

Warnings do not stop the build; the element/attribute is skipped where the compiler says so. Treat
`EASEL_XAML001/002/005/006/009/012/014/015/018/019` and `EASEL_XAMLP*` as errors in practice.

## Not supported

- Runtime XAML loading/`Source` URIs, designer, hot reload.
- `{DynamicResource}` in style/trigger setter values or trigger-action properties.
- Elements from non-Easel assemblies without a `clr-namespace` prefix; unprefixed local types.
- WinForms controls as visual children (`NativeControlHost` only).
- Native controls inside transformed/clipped/blended subtrees.
- `DateTime` literals, arbitrary object constructors without a public parameterless constructor.
- Implicit styles from a standalone dictionary applied automatically (declare them on a visual element instead).
- Animation without an injected `IVisualAnimator`.
