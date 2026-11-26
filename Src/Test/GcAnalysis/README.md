# Easel GC analysis

A standalone harness for GC pressure and managed-memory leaks. It is intentionally not a unit test and not the
render benchmark: scenarios build and dispose real Easel trees, then report allocation, collection and weak-reference
survival data.

## What it measures

Every scenario runs without a tracker for its measured operations, then repeats create/dispose cycles with weak
references enabled.

| Metric | Meaning |
| --- | --- |
| `alloc/op` | Managed bytes allocated on the current thread for one build+render+dispose operation; min, median, mean, p95 and max are recorded. |
| `total allocated` | `GC.GetTotalAllocatedBytes(precise: true)` delta across measured operations and leak cycles. |
| `gen0/1/2` | Collection counts for each generation during the measured window. |
| `pause` | `GC.GetTotalPauseDuration()` delta. |
| `retained` | Managed heap growth after three blocking full collections, measured from the post-warmup heap. |
| `leaks` | Weak references that survive those forced collections. |

Leak detection tracks the host, its root, representative controls, template roots, list containers and a binding
source. A surviving reference is a leak: the scenario detached and disposed the object, so only a retained
subscription/cache/static can keep it alive.

Trackers are disabled for measured allocation runs; their `WeakReference` allocations therefore do not inflate
`alloc/op`. Leak-stress cycles are enabled and are excluded from the allocation median.

## Scenarios

| Name | What it builds |
| --- | --- |
| `host-lifecycle` | A small tree with a Button, TextBox, TextBlock and 20-row ListBox. |
| `page-build` | A 200-row ListBox plus Buttons, TextBoxes, Sliders and a ComboBox. |
| `template-lifecycle` | A mixed batch of templated Buttons, TextBoxes, ComboBoxes, CheckBoxes and Sliders. |
| `binding-lifecycle` | A TextBlock bound to an `INotifyPropertyChanged` source. |
| `theme-switch` | A rendered page switched Dark → Light → High contrast → Dark. |
| `list-window` | A 500-row ListBox that only attaches the viewport window. |

## Usage

```powershell
# all scenarios, report to a file
dotnet run -c Release --project Src/Test/GcAnalysis -- --output gc-analysis.json

# one scenario with more cycles
dotnet run -c Release --project Src/Test/GcAnalysis -- --scenario page-build --iterations 12 --cycles 20

# scenario list or help
dotnet run -c Release --project Src/Test/GcAnalysis -- --list
dotnet run -c Release --project Src/Test/GcAnalysis -- --help
```

Options: `--scenario`, `--iterations`, `--cycles`, `--output`, `--baseline`, `--tolerance`,
`--max-retained-bytes`, `--list`, `--help`.

Exit codes:

- `0` — all scenarios passed;
- `1` — a weak-reference leak, allocation regression against the baseline, or configured retained-byte budget
  failed;
- `2` — invalid arguments, unreadable baseline, or an unexpected harness error.

### Baseline comparison

Write a report on the reference machine, then compare a later run:

```powershell
dotnet run -c Release --project Src/Test/GcAnalysis -- --output baseline.json
dotnet run -c Release --project Src/Test/GcAnalysis -- --baseline baseline.json --tolerance 15
```

The comparison checks two things only: the allocation median may not grow beyond `--tolerance` percent, and no
scenario may gain surviving weak references. `retained` is reported but not compared by default because a
single heap-growth delta is noisy; use `--max-retained-bytes` when a scenario has a known ceiling.

## Interpretation

- Run `Release` on one machine; compare against a saved baseline rather than treating absolute byte counts as a
  portable budget. The project disables server GC and concurrent GC so the numbers are deterministic.
- A nonzero `leaks` is always actionable. The printed labels name the retained host, control, template root or
  source.
- `retained` can be a few hundred bytes lower than zero due to heap bookkeeping; only large growth across many
  cycles indicates a leak. The weak-reference check is the reliable leak signal.
- Allocation numbers include render-time `Bitmap`/`Graphics` objects created by the harness's `RenderTo` call; they
  are part of the scenario. Compare like-for-like scenarios, not absolute values across different page sizes.
- Native HWND, GDI and WPF media resources are outside the managed weak-reference graph. This harness reports the
  managed side only; use a native-resource diff/user-handle count alongside it for native leaks.
