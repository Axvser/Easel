# WinForms.Easel.Templates

`dotnet new` item templates for Easel XAML.

## Install

```powershell
dotnet new install WinForms.Easel.Templates
```

## Templates

| Short name | Item |
| --- | --- |
| `e-usercontrol` | XAML view with code-behind, hosted by a `VisualHost` |
| `e-resource` | Resource dictionary for brushes and shared primitives |
| `e-theme` | Partial theme overlay: override only chosen palette keys; typography styles derive with `BasedOn` |
| `e-style` | Keyed style derived with `BasedOn`, with setters and a trigger |
| `e-controltemplate` | Keyed `ControlTemplate` with a named part and a `ContentPresenter` |
| `e-datatemplate` | Keyed `DataTemplate` for items, list rows and `DataGrid` cells |

```powershell
dotnet new e-usercontrol -n SettingsView --namespace MyApp.Views
```

`-n` sets the file and class name and `--namespace` sets the namespace. Each item writes its files into the
current directory; the template keeps the passed name instead of the directory name.

`e-theme` is an overlay, not a complete palette: merge it after the host's built-in Dark/Light dictionary.
`e-style` and `e-theme` use `Style.BasedOn` with a base style in the same dictionary; a standalone
`ResourceDictionary` resolves `{StaticResource}` inside that dictionary (and its merged dictionaries), not the
host theme, so derive from a built-in style in a view's `Resources` when that base must come from the host.
