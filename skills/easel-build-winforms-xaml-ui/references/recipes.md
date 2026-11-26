# Recipes

Copy these as starting points. They assume a project referencing `WinForms.Easel` + `WinForms.Easel.Generator`, and a palette
available through `{StaticResource …}` (see [themes.md](themes.md)).

## 1. Application shell

```csharp
[STAThread]
private static void Main()
{
    ApplicationConfiguration.Initialize();
    Theme.Mode = ThemeMode.Dark;

    Application.Run(new VisualForm
    {
        Text = "My app",
        ClientSize = new Size(1280, 800),
        Root = new ShellView(),
    });
}
```

`ShellView` is a XAML `UserControl` with a `DockPanel`: title bar (Top), status bar (Bottom), side panes and a
fill. `VisualForm` owns the single `VisualHost`; for an existing `Form`, `form.Controls.Add(new ShellView())`
creates the same host and `form.Controls.Remove(view)` tears it down.

A `Theme.Mode` switch mutates the host's theme brushes in place and repaints; no root rebuild is needed. Recreate
(or refresh) a view only when it captured `Theme.Current` colors at construction or merged its own snapshot
dictionary.

## 2. Form page

```xml
<DockPanel xmlns="…" xmlns:x="…"
           x:Class="MyApp.Views.SettingsView" LastChildFill="True">
  <Border DockPanel.Dock="Top" Padding="18,12" Background="{StaticResource Surface}">
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
      </Grid.ColumnDefinitions>
      <StackPanel Grid.Column="0" Spacing="3">
        <TextBlock Text="Settings" Style="{StaticResource Heading}" />
        <TextBlock Text="{Binding Status}" Style="{StaticResource Caption}" />
      </StackPanel>
      <Button Grid.Column="1" Text="Save" Height="30" Command="{Binding SaveCommand}" />
    </Grid>
  </Border>

  <ScrollViewer HorizontalScrollBarVisibility="Disabled">
    <StackPanel Spacing="12" Width="560">
      <Border Background="{StaticResource Surface}" BorderBrush="{StaticResource Border}"
              BorderThickness="1" CornerRadius="12" Padding="16">
        <StackPanel Spacing="10">
          <TextBlock Text="Notifications" Style="{StaticResource Subheading}" />
          <Grid>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="*" />
              <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>
            <TextBlock Text="Desktop notifications" Foreground="{StaticResource Text}" />
            <CheckBox Grid.Column="1" IsChecked="{Binding Notifications, Mode=TwoWay}" />
          </Grid>
          <TextBlock Text="Preview volume" Foreground="{StaticResource Muted}" />
          <Slider Minimum="0" Maximum="100" Value="{Binding Volume, Mode=TwoWay}" />
          <TextBox Text="{Binding DisplayName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
        </StackPanel>
      </Border>
    </StackPanel>
  </ScrollViewer>
</DockPanel>
```

Code-behind assigns the view model before `InitializeComponent` (or passes it through the constructor). The theme
keys resolve from `VisualHost.Resources` when the view is parented; the gallery's `Themed*` bases merge a dictionary
only to add view-local palette overrides and decoration brushes.

## 3. List + detail

```xml
<Grid>
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="280" />
    <ColumnDefinition Width="*" />
  </Grid.ColumnDefinitions>

  <ListBox Grid.Column="0"
           ItemsSource="{Binding Items}"
           DisplayMemberPath="Name"
           SelectedItem="{Binding Selected, Mode=TwoWay}" />

  <ContentControl Grid.Column="1" Content="{Binding Selected}" ContentTemplate="{StaticResource DetailTemplate}" />
</Grid>
```

```xml
<DataTemplate x:Key="DetailTemplate">
  <StackPanel Spacing="8" Margin="20">
    <TextBlock Text="{Binding Name}" Style="{StaticResource Heading}" />
    <TextBlock Text="{Binding Summary}" Foreground="{StaticResource Muted}" Wrap="True" />
  </StackPanel>
</DataTemplate>
```

`ListBox` rows are realized from `ItemsSource`; `SelectedItem` is a DP and two-way bindable. For a very long
fixed-height list use `VirtualList` inside a `ScrollViewer` with `ItemHeight` set.

## 4. Menu, popup and context menu

```xml
<StackPanel Spacing="10">
  <VisualMenu x:Name="menu" />
  <Button x:Name="openButton" Text="Show popup" Click="OnShowPopup" />
  <TextBlock x:Name="status" Foreground="{StaticResource Muted}" />
</StackPanel>
```

```csharp
private void OnShowPopup(object? sender, EventArgs e)
{
    var popup = new VisualPopup
    {
        PlacementTarget = openButton,
        Placement = VisualPlacement.Bottom,
        Child = new PopupBodyView(),
        IsOpen = true,
    };
    popup.Closed += (_, _) => status.Text = "Popup closed";
}
```

Menu items are data, not visual children:

```csharp
var file = new VisualMenuItem { Header = "File" };
file.Items.Add(new VisualMenuItem { Header = "New", InputGestureText = "Ctrl+N", Command = () => … });
file.Items.Add(new VisualMenuItem { IsSeparator = true });
menu.Items.Add(file);
```

`VisualContextMenu.OpenAt(target, surfacePoint)` opens a detached menu at a surface point. Popups live in the
overlay and are not clipped by ancestor scroll viewers.

## 5. Custom templated control

```xml
<ControlTemplate x:Key="PillField" TargetType="TextBox">
  <Border Background="{StaticResource Raised}" BorderBrush="{StaticResource Border}"
          BorderThickness="1" CornerRadius="16" Padding="14,8">
    <TextBoxContentHost x:Name="PART_ContentHost" />
  </Border>
</ControlTemplate>

<TextBox Width="360" Template="{StaticResource PillField}" />
```

`PART_ContentHost` (or `PART_EditorHost`) is required for a TextBox template. The visual path is the default;
the caret, selection, clipboard, undo, DPs and bindings stay visual, and only IME/accessibility peers are lost.

For a custom control:

```csharp
public partial class Card : Border
{
    [XamlProperty(DefaultValue = "")]
    public partial string Title { get; set; }

    public Card()
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Add(new TextBlock { Text = Title });
        Child = stack;
    }
}
```

Better: make the control's markup a XAML `UserControl` and expose data through DPs/DataContext; use
`[XamlProperty]` for styleable state.

## 6. Custom theme and dynamic resources

For a few overrides, append a partial dictionary after the host's built-in theme dictionary (`surface` from
recipe 1):

```csharp
var overrides = new ResourceDictionary
{
    ["Accent"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x8A, 0x00)),
    ["Focus"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x8A, 0x00)),
};
surface.Resources.MergedDictionaries.Add(overrides);
```

Omitted keys keep resolving from the active Dark+/Light+ theme; overridden keys stay fixed across a theme switch
until you replace the overlay. Most control metrics and self-drawn chrome read `Theme.Current`, so call
`Theme.ApplyPalette` as well when those should change. For a complete palette, start from `DarkTheme.xaml`:

```csharp
var custom = new MyApp.Themes.MyTheme();
Theme.ApplyPalette(ThemeMode.Dark, ThemePalette.FromResourceDictionary(custom));
```

For a live key:

```xml
<Border Background="{DynamicResource Accent}" />
```

```csharp
surface.Resources["Accent"] = new SolidColorBrush(Color.Magenta); // dynamic targets update
```

A later `{StaticResource}` applied to the same property supersedes the dynamic reference; see
[xaml.md](xaml.md#static-vs-dynamic). A theme switch mutates the host's theme brushes in place, so an ordinary
switch needs no root rebuild.

## 7. Gradient and 3D card

```xml
<Border Width="260" Height="170" CornerRadius="14" CacheAsBitmap="True">
  <Border.Background>
    <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
      <LinearGradientBrush.GradientStops>
        <GradientStop Color="#FF00D2FF" Offset="0" />
        <GradientStop Color="#FF7B2FF7" Offset="1" />
      </LinearGradientBrush.GradientStops>
    </LinearGradientBrush>
  </Border.Background>
  <Border.Transform3D>
    <Rotate3DTransform AngleX="12" AngleY="-18" CenterX="130" CenterY="85" Depth="700" />
  </Border.Transform3D>
  <TextBlock Text="Perspective" Foreground="{StaticResource AccentText}" />
</Border>
```

Wrap a 3D/rotated stage in a parent with `ClipToBounds="True"` so its projection cannot leave stale pixels.
Use `CacheAsBitmap` on the transformed card when only the transform changes.

## 8. Lightweight visual chart

```csharp
var chart = new ContainerVisual { CacheAsBitmap = true };
for (var i = 0; i < points.Count - 1; i++)
{
    chart.Add(new ShapeVisual
    {
        Bounds = new RectangleF(points[i], new SizeF(2, 2)),
        Geometry = new EllipseGeometry(),
        Fill = new SolidColorBrush(Theme.Current.Accent),
    });
}
var host = new VisualPresenter { Content = chart, Height = 160 };
```

For curves use a `CurveBatchVisual` (cubic segments), or a custom `Visual` overriding `RenderContent` for
polylines; call `InvalidateCache()` after changing points.

## 9. Workflow node view (VeloxDev)

Easel XAML is the view layer; VeloxDev's attached behaviors connect it to the workflow model. A node view has:

```xml
<Border
    xmlns:behaviors="clr-namespace:VeloxDev.WorkflowSystem.AttachedBehaviors;assembly=VeloxDev.WinForms"
    behaviors:WorkflowSlotLayoutBehavior.IsEnabled="True"
    behaviors:WorkflowSlotLayoutBehavior.SlotNames="PART_InputSlot"
    behaviors:WorkflowSlotLayoutBehavior.SlotEnumeratorNames="PART_OutputSlots"
    behaviors:WorkflowSlotLayoutBehavior.CoordinateHostName="PART_Canvas">
  <Grid>
    <Grid behaviors:WorkflowNodeDragBehavior.IsEnabled="True"
          behaviors:WorkflowNodeDragBehavior.CoordinateHostName="PART_Canvas" />
    <Border x:Name="PART_InputSlot"
            behaviors:WorkflowSlotConnectionBehavior.IsEnabled="True" />
  </Grid>
</Border>
```

The canvas uses `behaviors:ViewPool.ItemsSource="{Binding Helper.VisibleItems}"` and a template selector on
`PART_Canvas`. The behavior semantics, slot measurement and connection previews belong to the VeloxDev
workflow skill; the XAML/control/runtime rules above still apply.
