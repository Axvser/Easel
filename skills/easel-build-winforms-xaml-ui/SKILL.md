---
name: easel-build-winforms-xaml-ui
description: Build WinForms UI entirely with Easel's XAML runtime — bootstrap one VisualHost, author XAML views and controls, use layouts, resources/styles/templates, bindings/MVVM, themes (built-in Dark+/Light+, system follow, custom dictionaries), visual composition (gradients, opacity masks, effects, 3D transforms) and the injectable animation contract. Use when writing or reviewing Easel XAML, customizing Easel controls or types, theming an Easel app, or replacing hand-built WinForms UI with the visual runtime.
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
dotnet add package Easel
dotnet add package Easel.Generator
```

| Package | Contains |
| --- | --- |
| `Easel` | `VisualHost`, `VisualElement` controls, visuals, brushes, resources, theme, animation contract |
| `Easel.Generator` | XAML compiler (+ `EASEL_XAML*` diagnostics) and dependency-property generator (+ `EASEL_XAMLP*`) |

`Easel` targets `netframework4.6.1`, `net5.0-windows`, `netcoreapp3.0` and `net8.0-windows`;
`Easel.Generator` targets `netstandard2.0` (Roslyn 4.8). A host uses `net8.0-windows` or later for
`dotnet run` development; the library is compiled for all four Easel targets.

Reference the generator as an analyzer and mark every `.xaml` as an `AdditionalFile`:

```xml
<ItemGroup>
  <AdditionalFiles Include="**/*.xaml" />
</ItemGroup>
<ItemGroup>
  <PackageReference Include="Easel.Generator" Version="…" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

When consuming a checkout instead of NuGet, reference `Src/Easel/Easel.csproj` and
`Src/Generators/Easel.Generator/Easel.Generator.csproj` (as analyzer).

## Bootstrap

Create the single surface and run the ordinary WinForms message loop:

```csharp
[STAThread]
private static void Main()
{
    ApplicationConfiguration.Initialize();

    var surface = new VisualHost { Dock = DockStyle.Fill, BackColor = Theme.Current.WindowBackground };
    var form = new Form { Text = "My app", ClientSize = new Size(1200, 800) };
    form.Controls.Add(surface);
    surface.Root = new MyShellView();
    Application.Run(form);
}
```

`VisualHost` is the only WinForms `Control`. `Root` is a `VisualElement` (or a plain `ContainerVisual`).
Everything below it is a retained visual.

## First XAML view

`Views/HelloView.xaml`:

```xml
<local:UserControl
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="clr-namespace:MyApp.Views"
    x:Class="MyApp.Views.HelloView"
    Background="{StaticResource Surface}"
    Padding="24">
  <StackPanel Spacing="8">
    <TextBlock Text="Hello" FontSize="22" FontWeight="SemiBold" Foreground="{StaticResource Text}" />
    <TextBox Width="320" Placeholder="Type here…" Text="{Binding Name, Mode=TwoWay}" />
    <Button Text="Run" Height="32" Command="{Binding SaveCommand}" />
  </StackPanel>
</local:UserControl>
```

`Views/HelloView.xaml.cs`:

```csharp
using Easel.Visuals.Controls;

namespace MyApp.Views;

public sealed partial class HelloView : UserControl
{
    public HelloView()
    {
        InitializeComponent();
    }
}
```

The generator emits `InitializeComponent` into the partial class. **Do not declare it yourself**; an existing
member is `EASEL_XAML015`.

## Hard rules

⚙ **One HWND.** Never add a WinForms control to a visual tree. The only native paths are
`NativeControlHost` (an explicit rectangular island) and the focused `TextBox`'s private native editor.

⚙ **A visual parent accepts `VisualElement` children only.** `native:` and `System.Windows.Forms` controls are
rejected. Use `NativeControlHost` for a native widget.

⚙ **A XAML root derives from `VisualElement` or is a standalone `ResourceDictionary`.** The root tag and the
`x:Class` base must agree (owner derives from the root type). `x:Class` types must be `partial`.

⚙ **Every `{StaticResource}` must be declared before the point that uses it.** Static resources resolve during
`InitializeComponent`, before the view joins the surface. `{DynamicResource}` stays subscribed and follows
dictionary changes; a later resource reference on the same target property supersedes an earlier one.

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

⚙ **`NativeControlHost` is above the visual tree.** It cannot be transformed, clipped or blended, and popups
cannot cover it. Keep native islands rectangular and axis-aligned.

⚙ **A focused native TextBox is an island too.** `TextBox.UseNativeEditor = false` keeps the whole field visual
(templates, DPs, caret, selection, undo) and loses only IME composition and the native accessibility peer.

⚙ **3D is a bitmap warp.** Content is composed once and projected; `CacheAsBitmap` reuses the pre-warp layer
for transform-only frames. Very large projections fall back to a coarse grid. Do not animate 3D content without
accounting for the per-frame warp cost.

⚙ **Gradient text uses the native GDI+ brush path.** A `Brush` on `TextBlock.Foreground` is real; a gradient
on a multiline `TextBox` restarts per line.

⚙ **Easel does not ship an animation engine.** Trigger actions and `VisualTransition` call
`AnimationRuntime.Animator`; without an injected `IVisualAnimator` they throw a clear
`InvalidOperationException`. If the app accepts VeloxDev, install **`VeloxDev.WinForms` 12.0.0 or later** — its
module initializer wires `EaselVisualAnimator` automatically, so no separate animation package or manual
registration is needed.

## Reference index

| You are about to… | Read |
| --- | --- |
| Start an app, host the surface, understand layout/input/DPI | [references/runtime.md](references/runtime.md) |
| Write XAML, use resources/styles/templates/bindings/events, read compiler diagnostics | [references/xaml.md](references/xaml.md) |
| Choose a control and its key properties | [references/controls.md](references/controls.md) |
| Build a layout, scroll, focus chain, keyboard/pointer handling | [references/layout-input.md](references/layout-input.md) |
| Use brushes, gradients, Visual trees, masks, effects, 3D, shapes | [references/visuals.md](references/visuals.md) |
| Theme an app, author a custom ResourceDictionary, follow Windows | [references/themes.md](references/themes.md) |
| Bind a view model, use `[VeloxProperty]`/`[VeloxCommand]`, validate | [references/mvvm.md](references/mvvm.md) |
| Animate a property, trigger actions, keyframes, inject an engine | [references/animation.md](references/animation.md) |
| Copy a complete form/list/settings/menu/popup/custom-theme recipe | [references/recipes.md](references/recipes.md) |
| Diagnose a blank view, wrong bounds, focus/airspace, resource or 3D issue | [references/pitfalls.md](references/pitfalls.md) |

## Living example

[`Examples/XamlGallery`](../../Examples/XamlGallery/) is a complete XAML-first application: the shell, every
card and every scenario page are XAML views with code-behind only for data and view behaviour. Its
[README](../../Examples/XamlGallery/README.md) lists every card and the run/capture switches. Use it as the
reference composition for a new app.

## Verification checklist

- [ ] The app has exactly one `VisualHost`; no WinForms control is a visual child.
- [ ] Every `x:Class` is `partial` and its base matches the root tag.
- [ ] Every static resource key is declared before use; dynamic keys resolve in the view's chain.
- [ ] The app starts with `Theme.Mode` set before the first view is built (or uses `Theme.Current` defaults).
- [ ] The primary path works at 150% DPI and after a theme switch.
- [ ] Native islands stay rectangular; no popup is expected to cover one.
- [ ] Any animation call has `AnimationRuntime.Animator` injected, or is guarded by `CanAnimate`.
- [ ] The gallery's `--smoke`/`--capture` pattern is used for visual verification when changing the runtime.

## What this skill does not cover

- The VeloxDev workflow model, node/slot/link behaviour, compiling and executing graphs: use the VeloxDev
  workflow skill and its WinForms page.
- VeloxDev's transition engine and its `Transition<T>` API: use VeloxDev's animation skill; Easel only defines
  the injection contract.
- WinForms designer, `Control` layout, native painting: Easel replaces those; `NativeControlHost` is the seam.
