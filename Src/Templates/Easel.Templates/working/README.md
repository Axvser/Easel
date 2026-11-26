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
| `e-theme` | Theme dictionary: palette, typography, metrics and keyed text styles |
| `e-style` | Keyed style with setters and a trigger |
| `e-controltemplate` | Keyed `ControlTemplate` with a named part and a `ContentPresenter` |
| `e-datatemplate` | Keyed `DataTemplate` for items, list rows and `DataGrid` cells |

```powershell
dotnet new e-usercontrol -n SettingsView --namespace MyApp.Views
```

`-n` sets the file and class name and `--namespace` sets the namespace. Each item writes its files into the
current directory; the template keeps the passed name instead of the directory name.
