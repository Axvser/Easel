# Easel performance benchmarks

The runner measures the costs that control templating can move: a slider drag, a splitter resize, and a palette
switch on a page with a 200-row list, 20 buttons, 8 text fields, 5 sliders, 5 progress bars and a combo box.
It uses a fixed 1× DPI surface and reports the median and p95 of N iterations after warmup.

```powershell
dotnet run -c Release
```

## Compare against the pre-templating baseline

Check out the Easel commit **before** the default styles were introduced (`886ab07`) into a separate clone and
point the same runner at it:

```powershell
dotnet run -c Release -p:EaselProject="C:\path\to\easel-baseline\Src\Easel\Easel.csproj"
```

Reference measurements from 2026-11-26 on this machine, 1200×800, 1× DPI, net8.0 Release:

| scenario | pre-templating (`886ab07`) | current |
| --- | --- | --- |
| slider, 200 value changes | 1.86 ms | 1.46 ms |
| splitter, 100 width changes | 1.82 ms | 1.69 ms |
| palette toggle (no rebuild) | 3.92 ms | 7.32 ms |
| palette + full page rebuild | 13.96 ms | 145 ms |

Interaction is at or below the baseline. The palette toggle stays close after the shared-brush mutation
(`VisualHost.RefreshThemeResources` updates `SolidColorBrush.Color` in place). The full page rebuild is still
dominated by template and binding instantiation: 200 rows × the default row template. Virtualization or lazy
template application is the remaining lever; changing markup or margins will not move that number.
