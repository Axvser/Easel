---
name: easel-build-winforms-xaml-ui
description: Build WinForms UI entirely with Easel's XAML runtime — bootstrap one VisualHost, author XAML views and controls, use layouts, resources/styles/templates, bindings/MVVM, themes (built-in Dark+/Light+, system follow, custom dictionaries), visual composition (gradients, opacity masks, effects, 3D transforms) and the built-in animation engine. Use when writing or reviewing Easel XAML, customizing Easel controls or types, theming an Easel app, or replacing hand-built WinForms UI with the visual runtime.
---

# Easel — build WinForms UI with XAML

Easel is a **XAML-first UI runtime for WinForms**. The window is one `VisualHost` (one HWND) and one visual tree.
Controls derive from `VisualElement`, not from WinForms controls. A Roslyn generator compiles `*.xaml`
`AdditionalFiles` into `private void InitializeComponent()` and generates dependency properties from
`[XamlProperty]` / `[XamlAttachedProperty]`.

This skill covers **everything needed to build UI with Easel**. VeloxDev's workflow skills cover the workflow
model and point here for the WinForms XAML surface. Easel itself does not reference VeloxDev.

## Packages and targets

```powershell
dotnet add package WinForms.Easel
dotnet add package WinForms.Easel.Generator
dotnet new install WinForms.Easel.Templates
```

| Package | Contains |
| --- | --- |
| `WinForms.Easel` | `VisualHost`, `VisualElement` controls, visuals, brushes, resources, theme, animation engine |
| `WinForms.Easel.Generator` | XAML compiler (+ `EASEL_XAML*` diagnostics) and dependency-property generator (+ `EASEL_XAMLP*`) |
| `WinForms.Easel.Templates` | `dotnet new` items: `e-usercontrol`, `e-resource`, `e-theme`, `e-style`, `e-controltemplate`, `e-datatemplate` |

```powershell
dotnet new e-usercontrol -n SettingsView --namespace MyApp.Views
dotnet new e-resource -n AppResources --namespace MyApp
dotnet new e-theme -n AppTheme --namespace MyApp
dotnet new e-style -n AppStyles --namespace MyApp
dotnet new e-controltemplate -n ControlTemplates --namespace MyApp
dotnet new e-datatemplate -n ItemTemplates --namespace MyApp
```

`WinForms.Easel` targets `netframework4.6.1`, `net5.0-windows`, `netcoreapp3.0` and `net8.0-windows`;
`WinForms.Easel.Generator` targets `netstandard2.0` (Roslyn 4.8). A host uses `net8.0-windows` or later for
`dotnet run` development; the library is compiled for all four Easel targets.

Reference the generator as an analyzer and mark every `.xaml` as an `AdditionalFile`:

```xml
<ItemGroup>
  <AdditionalFiles Include="**/*.xaml" />
</ItemGroup>
<ItemGroup>
  <PackageReference Include="WinForms.Easel.Generator" Version="…" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

When consuming a checkout instead of NuGet, reference `Src/Easel/Easel.csproj` and
`Src/Generators/Easel.Generator/Easel.Generator.csproj` (as analyzer).

## Bootstrap

The recommended path is `VisualForm`; it hosts the single surface and accepts any visual root:

```csharp
[STAThread]
private static void Main()
{
    ApplicationConfiguration.Initialize();
    Application.Run(new VisualForm { Text = "My app", ClientSize = new Size(1200, 800), Root = new MyShellView() });
}
```

With an existing native form, the `Control.ControlCollection` extensions create and remove the host:

```csharp
var form = new Form { Text = "My app", ClientSize = new Size(1200, 800) };
form.Controls.Add(new MyShellView()); // creates a dock-filled VisualHost
Application.Run(form);
```

Both forms host exactly one `VisualHost`; a second `Add` on the same collection throws. `Remove(view)` removes
and disposes the host created by `Add`. `VisualHost` remains the only WinForms `Control` inside the visual tree,
and `Root` is a `VisualElement` (or a plain `ContainerVisual`). Everything below it is a retained visual.

## First XAML view

`Views/HelloView.xaml`:

```xml
<UserControl
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    x:Class="MyApp.Views.HelloView"
    Background="{StaticResource Surface}"
    Padding="24">
  <StackPanel Spacing="8">
    <TextBlock Text="Hello" FontSize="22" FontWeight="SemiBold" Foreground="{StaticResource Text}" />
    <TextBox Width="320" Placeholder="Type here…" Text="{Binding Name, Mode=TwoWay}" />
    <Button Text="Run" Height="32" Command="{Binding SaveCommand}" />
  </StackPanel>
</UserControl>
```

`Views/HelloView.xaml.cs`:

```csharp
using Easel.Visuals.Controls;

namespace MyApp.Views;

public sealed partial class HelloView : UserControl
{
    public HelloView() => InitializeComponent();
}
```

The generator emits `InitializeComponent` into the partial class. **Do not declare it yourself**; an existing
member is `EASEL_XAML015`. Theme keys resolve through `VisualHost.Resources` after the view is parented; the host
already merges the active built-in dictionary, so a view does not need its own copy. Merge a dictionary into the
view only for view-local overrides (see [references/themes.md](references/themes.md)).

## Hard rules

⚙ **DPI takes two settings.** Per-monitor v2 awareness (`<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>`
or `Application.SetHighDpiMode` before the first window) **and** `AutoScaleMode = AutoScaleMode.None` on the form
that hosts `VisualHost`. Miss the second and WinForms scales the form while the host scales the tree, so everything
renders twice as large. Details in [references/runtime.md](references/runtime.md#dpi).

⚙ **One HWND for the visual tree.** A WinForms control never joins the visual paint pass; it is always an HWND
island. Write `<native:Button>` in XAML (the generator wraps it in `NativeElement<Button>`), or assign
`NativeControlHost.Child` in code-behind. A `TextBox` with `UseNativeEditor=true` adds one private island for
IME/accessibility; the default field is fully visual.

⚙ **Native layout is the same layout.** `Margin`, `Width`/`Height`, `Min`/`Max`, alignment, `Grid.Row`,
`DockPanel.Dock`, `Canvas.Left`, `Panel.ZIndex` and `Visibility` on a `native:` element are routed to the
wrapper and participate in the normal measure/arrange pass. Every other attribute goes to the native control
(`Text`, `BackColor`, `Padding`, events). A native CLR property binds through a generated proxy property; `Mode=TwoWay` reports back through `INotifyPropertyChanged` or `XxxChanged`, and a control with neither calls `NativePropertyBinding.NotifyChanged` (no polling); native `Enter`/`Leave` raise the wrapper's logical focus events, so `LostFocus` triggers behave like a visual target.

⚙ **A native container hosts visual content.** Put a visual panel inside it (`<native:GroupBox>
<StackPanel>…</StackPanel></native:GroupBox>`); the runtime paints that content through a nested `VisualHost`
over the native display rectangle. Several native children are also accepted and wrapped in an implicit vertical
`StackPanel` in document order; nest an explicit panel for another layout.

⚙ **Opacity/transform need snapshot mode.** A live HWND rejects `WS_EX_LAYERED` and affine transforms. Set
`IsSnapshot="True"` to paint the control as a bitmap in the visual pass: `Opacity`, `Transform` and `Clip` then
apply. In a Form, an almost invisible (`Opacity=1/255`) input proxy keeps real mouse/keyboard/IME alive; scale
re-lays the control out at the scaled size and rotation keeps the transformed polygon interactive (whole-control
hits exact, item positions approximate). Call `RefreshSnapshot()` after changing it. `CornerRadius` rounds the
live window's region, and popups cut rectangular holes in that region.

⚙ **A XAML root derives from `VisualElement` or is a standalone `ResourceDictionary`.** The root tag and the
`x:Class` base must agree (owner derives from the root type). `x:Class` types must be `partial`.

⚙ **Static references bind lazily; dynamic references stay live.** A generated `{StaticResource}` on a visual
property applies as soon as the element's resource chain contains the key; the chain grows when the view is
parented, so a key from `VisualHost.Resources` (including the built-in theme) resolves without copying it into the
view. An eager read — a style `BasedOn` or setter value, a non-visual object property, a trigger-action property,
or a dictionary-internal reference — throws `No resource named …` when the key is absent. `{DynamicResource}` stays
subscribed and follows dictionary changes; a later resource reference on the same target property supersedes an
earlier one.

⚙ **A partial theme overlay wins by merge order.** The host merges its built-in theme dictionary first; append a
partial `ResourceDictionary` to `VisualHost.Resources.MergedDictionaries` to override only the keys it defines
(omitted keys fall back to the active theme; keys stored directly on the host or a view win over every merged
dictionary). See [references/themes.md](references/themes.md#custom-themes).

⚙ **Do not write layout in a code callback.** Use `PropertyMetadata.AffectsMeasure` /
`AffectsArrange` / `AffectsRender` on DPs. `VisualElement.Width`/`Height`/`Margin`/`Padding` and panel attached
properties already invalidate correctly.

⚙ **Set `DataContext` before `InitializeComponent` when XAML bindings should see it**, or assign the same view
model after construction; inherited `DataContext` propagates, but a binding created before the view is parented
retries when its chain grows.

⚙ **Commands are DPs on `ButtonBase`.** `Button`, `RepeatButton`, `ToggleButton`, `CheckBox`, `RadioButton`
route activation through `Command`. Bind `Command` from the data context.

⚙ **Styles can only set dependency properties.** `VisualSetter.Property` must be a DP-backed property; a plain
CLR auto-property is rejected. Use `[XamlProperty]` for a custom styleable property.

⚙ **A style is a declaration, not a live object.** Mutating an applied style's `Setters` does not reapply it;
replace the style or change a local DP value.

⚙ **A native island paints above the visual tree.** A live HWND cannot be transformed, alpha-blended or clipped to
a shape; popups cover it only through the window-region holes. Keep live native islands rectangular and axis-aligned;
use `IsSnapshot` when composition is required. A nested content surface is still bounded by the same island.

⚙ **A `UseNativeEditor` TextBox adds one island.** The visual path is the default: templates, DPs, caret,
selection and undo stay visual. Set `TextBox.UseNativeEditor = true` only when IME composition or the native
accessibility peer is required; the focused field then hosts a private native editor island that paints above the
tree and cannot be transformed. The visual path loses only IME composition and that peer.

⚙ **3D is a bitmap warp.** Content is composed once and projected; `CacheAsBitmap` reuses the pre-warp layer
for transform-only frames. Very large projections fall back to a coarse grid. Do not animate 3D content without
accounting for the per-frame warp cost.

⚙ **Gradient text uses the native GDI+ brush path.** A `Brush` on `TextBlock.Foreground` is real; a gradient
on a multiline `TextBox` restarts per line.

⚙ **Easel ships its own animation engine.** Trigger actions and `VisualTransition` interpolate values,
pace frames and hold the end value with no backend to register. An unsupported property type is rejected
with a clear `NotSupportedException` at the call site.

## Reference index

| You are about to… | Read |
| --- | --- |
| Start an app, host the surface, understand layout/input/DPI | [references/runtime.md](references/runtime.md) |
| Write XAML, use resources/styles/templates/bindings/events, read compiler diagnostics | [references/xaml.md](references/xaml.md) |
| Choose a control and its key properties | [references/controls.md](references/controls.md) |
| Build a layout, scroll, focus chain, keyboard/pointer handling | [references/layout-input.md](references/layout-input.md) |
| Use brushes, gradients, Visual trees, masks, effects, 3D, shapes | [references/visuals.md](references/visuals.md) |
| Theme an app, author a custom ResourceDictionary, follow Windows | [references/themes.md](references/themes.md) |
| Bind a view model, notify with `INotifyPropertyChanged`, validate | [references/mvvm.md](references/mvvm.md) |
| Animate a property, trigger actions, keyframes | [references/animation.md](references/animation.md) |
| Copy a complete form/list/settings/menu/popup/custom-theme recipe | [references/recipes.md](references/recipes.md) |
| Diagnose a blank view, wrong bounds, focus/airspace, resource or 3D issue | [references/pitfalls.md](references/pitfalls.md) |

## Living example

[`Examples/XamlGallery`](../../Examples/XamlGallery/) is a complete XAML-first application: the shell, every
card and every scenario page are XAML views with code-behind only for data and view behaviour. Its
[README](../../Examples/XamlGallery/README.md) lists the scenario pages, component groups and run/capture
switches. Use it as the reference composition for a new app.

## Verification checklist

- [ ] The app has exactly one `VisualHost`; no WinForms control is a visual child.
- [ ] Every `x:Class` is `partial` and its base matches the root tag.
- [ ] Static keys resolve through the view → ancestors → host chain; dynamic keys follow later changes.
- [ ] The app starts with `Theme.Mode` set before the first view is built (or uses `Theme.Current` defaults).
- [ ] The primary path works at 150% DPI and after a theme switch.
- [ ] Native islands stay rectangular; a popup covers one only through the window-region hole.
- [ ] Any animated property has a CLR type the engine can interpolate (see `references/animation.md`).
- [ ] The gallery's `--smoke`/`--capture` pattern is used for visual verification when changing the runtime.

## What this skill does not cover

- The VeloxDev workflow model, node/slot/link behaviour, compiling and executing graphs: use the VeloxDev
  workflow skill and its WinForms page.
- VeloxDev's standalone transition engine and its `Transition<T>` API for non-Easel targets: use VeloxDev's
  animation skill. Easel animations use Easel's own engine and never require VeloxDev.
- WinForms designer, `Control` layout, native painting: Easel replaces those; `NativeControlHost` is the seam.
