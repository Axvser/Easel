# Easel performance benchmarks

A standalone console runner that measures the costs template instantiation adds to a fixed page. It writes a
machine-readable JSON result, reports medians and p95 per scenario, counts layout and paint passes, and can gate a
run against a previous JSON baseline with per-scenario regression budgets.

The project is not part of `Easel.slnx` and is never packed; it references the runtime project only to measure it.

## Scenarios

Every scenario runs against the same deterministic page: 1200×800 at 1× DPI, a 200-row `ListBox`, 20 buttons,
8 text boxes, 5 sliders, 5 progress bars, a combo box and 30 property rows. The page is dark and the surface uses
`RenderTo`, the software export path, so results do not depend on a window, monitor or GPU.

| scenario | warmup | iterations | what it does |
| --- | ---: | ---: | --- |
| `slider-value` | 5 | 200 | Moves one slider value and repaints the damaged region. |
| `splitter-resize` | 3 | 100 | Writes the first column width, invalidates measure, then repaints; one layout pass per sample. |
| `full-frame` | 3 | 20 | Invalidates the surface, then re-measures, re-arranges and repaints the whole tree. |
| `theme-toggle` | 3 | 20 | Switches dark/light with the page kept alive; shared brushes are mutated in place. |
| `build-page` | 2 | 10 | Builds, lays out and renders a fresh page, then disposes it. |
| `theme-rebuild` | 2 | 10 | Switches palette, then builds, lays out and renders a fresh page. |

Each scenario reports `medianMs`, `p95Ms`, `minMs`, `maxMs`, `meanMs`, `stdDevMs` and the raw `samplesMs`. It also
records the visual-tree shape (`nodes`, `templateNodes`, `arrangedNodes`) and the work done over the measured
samples: `layoutPasses` (through `VisualHost.LayoutUpdated`), `paintPasses` (through `VisualHost.PaintCount`) and
`treesBuilt`.

## Run

From the repository root:

```powershell
dotnet run -c Release --project Src/Test/Performance/Easel.Benchmarks -- --output current.json
```

`--output` defaults to `benchmark-results.json` in the current directory. The console prints a compact summary; the
JSON is written separately so console noise does not corrupt it.

Offline, restore once from the local feed and then run without restoring:

```powershell
dotnet restore Src/Test/Performance/Easel.Benchmarks/Easel.Benchmarks.csproj --source C:\Users\Axvse\AppData\Local\Temp\easel-feed
dotnet run -c Release --project Src/Test/Performance/Easel.Benchmarks --no-restore -- --output current.json
```

Options:

| option | meaning |
| --- | --- |
| `-o`, `--output <path>` | JSON result path (default `benchmark-results.json`). |
| `--no-json` | Do not write a JSON file. |
| `--baseline <path>` | Compare against a JSON produced by this runner and attach a `comparison` section. |
| `--scenario <list>` | Run only the named scenarios (comma separated). |
| `--warmup <n>` | Override warmup iterations for every scenario. |
| `--iterations <n>` | Override measured iterations for every scenario. |
| `--budget <spec>` | Regression budget: `10%`, `0.10`, or `name=25%`; repeatable. |
| `--budget-metric <m>` | Which statistic the budget checks: `median` (default) or `p95`. |
| `--list` | List scenarios and their default iteration counts. |
| `-h`, `--help` | Show usage. |

Exit codes: `0` success (also an informational comparison with no budget), `1` at least one gated scenario exceeded
its budget, `2` invalid arguments or baseline file.

## Compare against the pre-templating baseline

A baseline is a separate clone of the runtime at the commit before default control templates were introduced
(`886ab07`). Restore and build this same runner against that project, which produces a baseline JSON:

```powershell
dotnet restore Src/Test/Performance/Easel.Benchmarks/Easel.Benchmarks.csproj `
  -p:EaselProject=C:\path\to\easel-baseline\Src\Easel\Easel.csproj `
  --source C:\Users\Axvse\AppData\Local\Temp\easel-feed
dotnet run -c Release --project Src/Test/Performance/Easel.Benchmarks --no-restore `
  -p:EaselProject=C:\path\to\easel-baseline\Src\Easel\Easel.csproj -- --output baseline.json
```

Restore the benchmark against the current runtime again, then compare and gate:

```powershell
dotnet restore Src/Test/Performance/Easel.Benchmarks/Easel.Benchmarks.csproj --source C:\Users\Axvse\AppData\Local\Temp\easel-feed
dotnet run -c Release --project Src/Test/Performance/Easel.Benchmarks --no-restore -- `
  --output current.json --baseline baseline.json --budget 10%
```

`--budget 10%` gates every scenario; `--budget build-page=25%` gates one. `--budget` can be repeated. Without any
budget the comparison is informational and the exit code stays `0`. Each scenario's delta is
`(current / baseline - 1) × 100`, checked against the budget on the chosen metric; a delta exactly at the budget
passes and anything above it fails.

The JSON records `run.easelInformationalVersion`, which includes the source commit the runner was compiled against,
so a baseline file carries its own provenance.

## Result shape

```json
{
  "schemaVersion": 1,
  "generatedAtUtc": "2026-11-26T02:00:00.0000000Z",
  "run": {
    "configuration": "Release",
    "framework": ".NET 8.0.31",
    "os": "Microsoft Windows 10.0.26300",
    "architecture": "X64",
    "processorCount": 16,
    "machineName": "AXVSER",
    "stopwatchFrequency": 10000000,
    "easelVersion": "1.4.0.0",
    "easelInformationalVersion": "1.4.0+88e180d...",
    "arguments": ["--output", "current.json"]
  },
  "tree": { "nodes": 1764, "templateNodes": 1282, "templateRoots": 248, "templatedControls": 247, "arrangedNodes": 1734, "visibleNodes": 1752 },
  "scenarios": [
    {
      "name": "slider-value",
      "title": "slider · value change",
      "unit": "ms",
      "warmup": 5,
      "iterations": 200,
      "medianMs": 1.678,
      "p95Ms": 2.163,
      "minMs": 1.4,
      "maxMs": 4.0,
      "meanMs": 1.8,
      "stdDevMs": 0.3,
      "samplesMs": [1.6, 1.7],
      "counters": { "layoutPasses": 0, "paintPasses": 200, "treesBuilt": 0, "nodes": 1764, "templateNodes": 1282 }
    }
  ],
  "comparison": {
    "baselinePath": "baseline.json",
    "metric": "median",
    "hadBudget": true,
    "verdict": "pass",
    "scenarios": [
      { "name": "slider-value", "currentMs": 1.678, "baselineMs": 1.769, "deltaPercent": -5.1, "budgetPercent": 10, "verdict": "pass" }
    ]
  }
}
```

The sample array is truncated above; a real file contains every measured sample.

## Measurements

Environment: Windows 11 (10.0.26300), .NET 8.0.31, Release, 1200×800 at 1× DPI, 16 logical processors. Baseline is
the clone at `C:\Users\Axvse\AppData\Local\Temp\easel-baseline` (`886ab07`, pre-templating, `1.4.0+886ab07…`);
current is `88e180d` (`1.4.0+88e180d…`). Medians/p95 in milliseconds; one run per column on the same machine, so
treat small deltas as noise.

| scenario | pre-templating median | pre-templating p95 | current median | current p95 | delta |
| --- | ---: | ---: | ---: | ---: | ---: |
| `slider-value` | 1.476 | 1.834 | 1.486 | 2.017 | +0.7% |
| `splitter-resize` | 4.155 | 6.605 | 2.553 | 3.824 | -38.6% |
| `full-frame` | 3.210 | 3.800 | 2.383 | 2.990 | -25.8% |
| `theme-toggle` | 3.253 | 3.694 | 2.498 | 3.181 | -23.2% |
| `build-page` | 7.246 | 7.598 | 19.699 | 21.660 | +171.9% |
| `theme-rebuild` | 7.094 | 7.937 | 20.054 | 26.829 | +182.7% |

The slider number is the median of three consecutive runs (1.461/1.484/1.486 ms); the other rows are one run of the
same binary. Interaction and palette switching are now at or below the pre-templating baseline. Page build remains
2.7–2.8× the baseline: the baseline realizes 200 flat rows, while the decoupled ListBox keeps 200 index-stable
`ListBoxItem` containers and materializes the row template only for the ~26 visible ones. With the list removed
(`ITEMS=0`) the same page builds in ~17 ms, which is the floor for this page with templates; true window
virtualization (detaching offscreen containers and using spacers) is the remaining lever.

Visual-tree shape of the canonical page:

| counter | pre-templating `886ab07` | current |
| --- | ---: | ---: |
| nodes | 286 | 880 |
| template-created nodes | 4 | 398 |
| template roots | 1 | 77 |
| templated controls | 1 | 76 |
| arranged nodes | 276 | 850 |
| visible nodes | 278 | 868 |

Work counters over the measured samples. Layout, paint and trees-built counts are identical on both sides because
the scenario iteration counts match; the node columns describe the current tree (the baseline tree has 286 nodes
and 4 template nodes):

| scenario | layout passes | paint passes | trees built | nodes per tree | template nodes per tree |
| --- | ---: | ---: | ---: | ---: | ---: |
| `slider-value` | 0 | 200 | 0 | 1764 | 1282 |
| `splitter-resize` | 100 | 100 | 0 | 1764 | 1282 |
| `full-frame` | 20 | 20 | 0 | 1764 | 1282 |
| `theme-toggle` | 20 | 20 | 0 | 1764 | 1282 |
| `build-page` | 10 | 10 | 10 | 1764 | 1282 |
| `theme-rebuild` | 10 | 10 | 10 | 1764 | 1282 |

The slider scenario performs zero layout passes: its value change only damages a rectangle, so it measures the
paint path. The splitter, full-frame, theme and build scenarios each run at least one measure/arrange pass per
sample. The page-build cost tracks the 1282 template-created nodes; the interaction paths differ by roughly the
extra nodes they must measure and paint, not by a change in the slider's own paint code.

## Interpretation

- A single slider drag matches the pre-templating baseline: its value change damages a rectangle and never arranges
  the list.
- Resize, full-frame repaint and palette switching are now faster than the pre-templating baseline: the shared-brush
  mutation, full-frame visible-clip culling and per-node state-stack removal more than pay for the extra template
  nodes on the paint path.
- Building a page is ~2.7× slower. The baseline has 200 flat rows; the decoupled ListBox keeps 200 index-stable
  `ListBoxItem` containers and expands the row template only for the ~26 rows inside the viewport (lazy realization).
  With the list removed the same page builds in ~17 ms, which is the floor of the current design; going below needs
  true window virtualization with spacers, not more template trimming.
- The palette toggle is faster than the baseline because shared brushes are mutated in place and the frame is
  painted directly; the synthetic rebuild scenario measures a path the Gallery no longer uses.

## Limitations

- Results are wall-clock timings from one machine and one run per column. Background load moves the medians,
  especially the 10-iteration build scenarios (p95 is visibly noisier than median); compare the same machine,
  configuration and commit range, and prefer repeated runs when a delta is close to a budget.
- `paintPasses` counts completed paint calls, not painted nodes. `RenderTo` reuses its frame cache, so even a
  no-op render increments `PaintCount`; use layout passes and the tree counters to reason about the work behind a
  timing.
- `templateNodes` counts nodes whose `TemplatedParent` is set, i.e. control-template expansion. Item content
  realized by an items control through a data template is not included unless that item control itself uses a
  control template.
- The page is synthetic, the surface is software-rendered, and the window paint path (DPI scaling, motion scale,
  native islands) is not exercised.
- Baseline comparison recompiles the same runner source against a different `Easel.csproj`; the two builds must
  use a comparable runtime and the same benchmark source, otherwise the delta is not meaningful.
