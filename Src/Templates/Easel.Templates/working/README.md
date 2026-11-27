# WinForms.Easel.Templates

`dotnet new` item and project templates for Easel XAML.

## Install

```powershell
dotnet new install WinForms.Easel.Templates
```

## Templates

| Short name | Item |
| --- | --- |
| `e-app` | Complete app project: `App.xaml` startup theme, `MainForm` window, csproj (project template) |
| `e-usercontrol` | XAML view with code-behind, hosted by a `VisualHost` |
| `e-window` | XAML-declared window for `Application.Run` (title, DIP size, content) |
| `e-resource` | Resource dictionary for brushes and shared primitives |
| `e-theme` | Partial theme overlay: override only chosen palette keys; typography styles derive with `BasedOn` |
| `e-style` | Keyed style derived with `BasedOn`, with setters and a trigger |
| `e-controltemplate` | Keyed `ControlTemplate` with a named part and a `ContentPresenter` |
| `e-datatemplate` | Keyed `DataTemplate` for items, list rows and `DataGrid` cells |

```powershell
dotnet new e-app -n MyApp --easel-version 3.0.0
dotnet new e-usercontrol -n SettingsView --namespace MyApp.Views
dotnet new e-window -n MainForm --namespace MyApp
```

`e-app` is a project template: it creates the project directory, the `WinForms.Easel` package references,
`App.xaml`, `MainForm.xaml`, and runs `dotnet restore`. `App.xaml` starts with the `StartupTheme` key
(`Dark`, `Light`, `HighContrast` or `System`; `System` resolves to `HighContrast` when Windows high contrast
is on) and is where application resources go; it merges into `EaselApplication.Resources` before the first
window, so `{StaticResource}` in any window resolves against anything added there. A keyless
`<Style TargetType="T">` added there is an implicit style for exact `T`. The item templates write their files
into the current directory and keep the passed name instead of the directory name.

`e-theme` is an overlay, not a complete palette: merge it after the host's built-in Dark/Light dictionary.
`e-style` and `e-theme` use `Style.BasedOn` with a base style in the same dictionary; a standalone
`ResourceDictionary` resolves `{StaticResource}` inside that dictionary (and its merged dictionaries), not the
host theme, so derive from a built-in style in a view's `Resources` when that base must come from the host.
