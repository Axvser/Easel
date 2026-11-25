# MVVM, bindings and generated properties

Easel has no MVVM framework of its own. It provides:
`DataContext` inheritance, `{Binding}` / `{TemplateBinding}`, `ICommand` on buttons, dependency properties, and
the `[XamlProperty]` / `[XamlAttachedProperty]` generators. You may write view models by hand, or use
VeloxDev.Core's `[VeloxProperty]` / `[VeloxCommand]` generators as the gallery does.

## DataContext

`DataContext` is an inherited DP. A binding with no explicit `Source`/`ElementName`/`RelativeSource` reads the
nearest `DataContext` up the visual parent chain.

```csharp
public sealed partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel vm)
    {
        DataContext = vm;          // before InitializeComponent: bindings see the context on their first read
        InitializeComponent();
    }
}
```

- Set `DataContext` **before** `InitializeComponent` when the XAML has bindings. A binding created before
  parenting still retries when the chain is complete, but setting it first avoids an empty first frame.
  Setting it afterwards also reaches `OneWay`/`TwoWay` bindings through the inherited-context change;
  `OneTime` bindings are the exception and never re-read a context that arrives later.
- A local `DataContext` cuts inheritance for that subtree.
- A binding's source is a property path into the data context; `Source` can override it with an object,
  `ElementName` with a named element, `RelativeSource` with `Self`/`TemplatedParent`/`FindAncestor`.
- Use `RelativeSource={RelativeSource FindAncestor, AncestorType=local:MyView, AncestorLevel=1}` from an item
  template to reach the view model instead of copying it into every item.

## Compiled bindings

Bindings resolve through `TypeDescriptor` by default. Add `x:DataType` and `x:CompileBindings` to compile the
data-context path into direct property access:

```xml
<UserControl
    xmlns:vm="clr-namespace:MyApp.ViewModels"
    x:DataType="vm:SettingsViewModel"
    x:CompileBindings="True">
  <TextBox Text="{Binding DisplayName, Mode=TwoWay}" />
  <Button Command="{Binding SaveCommand}" />
</UserControl>
```

- Both directives are inherited by the subtree; `x:CompileBindings="False"` opts a subtree out.
- A `DataTemplate` sets the item type for its body: `<DataTemplate x:DataType="local:Row">`.
- Paths get checked at compile time; source-generated properties (`[VeloxProperty]`) are checked by the C#
  compiler at the same point. Converters, `StringFormat`, fallback values, validation and
  `INotifyPropertyChanged` behave exactly like runtime bindings.
- `Source`, `ElementName`, `RelativeSource` and `TemplateBinding` remain runtime bindings.
- `OneWay`/`OneTime` need only a getter. `TwoWay`/`OneWayToSource` need an accessible setter; a read-only leaf
  is `EASEL_XAML022`.
- Indexers compile for arrays, `IList<T>` and `IDictionary<TKey, TValue>`; a non-generic `IList`/`IDictionary`
  yields `object`, so a later segment falls back to reflection with `EASEL_XAML024`.

## Bindable view models

A hand-written view model must implement `INotifyPropertyChanged`; a view can also bind an `IObservable`
through `element.Bind(dp, observable)`. The generator path from VeloxDev.Core is the one the gallery uses:

```csharp
using VeloxDev.MVVM;

public sealed partial class SettingsViewModel
{
    [VeloxProperty] private bool? _notifications = true;
    [VeloxProperty] private double _volume = 62;
    [VeloxProperty] private string _status = "No changes saved";

    [VeloxCommand]
    private void Save()
        => Status = $"Saved · notifications {(Notifications == true ? "on" : "off")} · volume {Volume:F0}%";
}
```

- `[VeloxProperty]` on a private field generates a public notifying property (`Notifications`, `Volume`, …).
  The field name loses the leading underscore.
- `[VeloxCommand]` on a method generates `XxxCommand` (`SaveCommand`). `canValidate: true` +
  `CanExecuteXxxCommand` controls enablement.
- The class must be `partial`. Call `AddCommand.Notify()` (or the generated command's `Notify`) when a
  `CanExecute` dependency changes.
- The XAML binds by the generated property name; path resolution goes through `TypeDescriptor`, so a renamed
  property breaks the binding.

Easel itself never takes a dependency on VeloxDev.Core. A project that does not use that generator can write
`INotifyPropertyChanged` and `ICommand` manually; the binding and button paths are identical.

## Bindings in XAML

```xml
<TextBox Text="{Binding DisplayName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
<TextBlock Text="{Binding Amount, StringFormat=N2}" />
<TextBlock Text="{Binding OptionalName, StringFormat='Source: {0}'}" />
<CheckBox IsChecked="{Binding Notifications, Mode=TwoWay}" />
<Slider Minimum="0" Maximum="100" Value="{Binding Volume, Mode=TwoWay}" />
<Button Text="Save" Command="{Binding SaveCommand}" />
```

- `Mode`: `OneWay` (default), `TwoWay`, `OneTime`, `OneWayToSource`.
- `UpdateSourceTrigger`: `PropertyChanged`, `LostFocus`, `Explicit`.
- `Converter` (`IValueConverter`), `ConverterParameter`, `ConverterCulture`, `StringFormat`, `FallbackValue`,
  `TargetNullValue`.
- Validation: `ValidatesOnExceptions`, `ValidatesOnNotifyDataErrors`, `ValidatesOnDataErrors`,
  `NotifyOnValidationError`; a failed binding raises `VisualBindingOperations.BindingFailed`, and a validation
  error raises `Validation.Error` when `NotifyOnValidationError` is set.
- `Command` is a DP on `ButtonBase`; `CheckBox`/`ToggleButton`/`RadioButton` also execute it. `CommandParameter`
  is passed through.

## Control-to-control binding

Use `ElementName` for a named element in the same namescope:

```xml
<CheckBox x:Name="enableSwitch" IsChecked="True" />
<Button IsEnabled="{Binding IsChecked, ElementName=enableSwitch}" />
```

For a template part, `TemplateBinding` binds the `TemplatedParent`:

```xml
<ControlTemplate TargetType="ContentControl">
  <Border Background="{TemplateBinding Background}">
    <ContentPresenter />
  </Border>
</ControlTemplate>
```

`ElementName` is namescope-wide, not ancestor-only; a name in a different template/namescope is not visible.
`FindAncestor` reaches an ancestor type across template boundaries when the type is legal.

## Views and code-behind

Keep code-behind to view behaviour:

```csharp
public sealed partial class PopupCard : StackPanel
{
    private VisualPopup? _popup;
    public PopupCard() => InitializeComponent();

    private void OnShow(object? sender, EventArgs e)
    {
        _popup?.Dispose();
        _popup = new VisualPopup
        {
            PlacementTarget = showButton,
            Placement = VisualPlacement.Bottom,
            Child = new PopupBodyView(),
            IsOpen = true,
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _popup?.Dispose();
        base.Dispose(disposing);
    }
}
```

Use code-behind for `DataContext` assignment, tree/list item construction, combo items, date values, native
hosts, popup lifetime and one-off wiring. Use the view model for state, derived values and commands.

## Generated dependency properties

```csharp
using Easel;

public sealed partial class GlowCard : Easel.Visuals.Controls.Border
{
    [XamlProperty(AffectsRender = true)]
    public partial double Glow { get; set; }

    [XamlAttachedProperty]
    public static partial string? GetTag(VisualElement element);
    public static partial void SetTag(VisualElement element, string? value);
}
```

- `[XamlProperty]` requires a `partial` property with no accessor bodies and a `VisualElement`-derived owner.
- A non-nullable reference type needs `DefaultValue` or a nullable declaration (`EASEL_XAMLP005`).
- Generated members are usable in XAML styles, triggers, bindings and animations.
- A DP-backed property is needed for a style setter; plain CLR properties cannot be styled.

### Change callbacks

Both attributes accept `ChangedCallback = nameof(OnXChanged)`, naming a **static void** method on the declaring
type that takes `(IWinformsObject target, DependencyPropertyChangedEventArgs e)`:

```csharp
[XamlAttachedProperty(ChangedCallback = nameof(OnIsEnabledChanged))]
public static partial bool GetIsEnabled(VisualElement element);
public static partial void SetIsEnabled(VisualElement element, bool value);

private static void OnIsEnabledChanged(IWinformsObject target, DependencyPropertyChangedEventArgs e)
    => ((VisualElement)target).InvalidateMeasure();
```

- The callback runs after the effective value changes — on the first write, on a later different value, and on
  `ClearValue` falling back to the default. Writing the same effective value again does not notify.
- `VisualPropertyChangedCallback` is the visual-element path; a property also used on a native `Control` needs the
  control-shaped `PropertyChangedCallback` in its metadata as well.
- A callback that writes the same property again does not recurse (the second write is the same effective value).

## Selecting a view model

`DataContext` can be any object. For design/test separation, pass a view model into the view constructor
(`new SettingsView(vm)`); for a page created by a template, set `DataContext` on the template result. Avoid a
static/global service locator; the gallery shares one `GalleryModel` only as sample data, not as a service.

## Threading and lifecycle

- UI changes happen on the WinForms UI thread; `VisualHost` paints and routes input there.
- A background result should marshal to the UI thread before touching view models that raise property changes
  bound to the tree (or use a `SynchronizationContext` captured at construction).
- Dispose a replaced preview/tree; dispose native popups; dispose resource subscriptions through the element's
  own `Dispose` (it disposes children, caches and bindings).
- A view model with a `Theme.Changed`, `Tickable` or command subscription must unsubscribe in `Dispose`.
