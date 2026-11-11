# Code review and efficiency work — v0.2.0

Reviewed baseline: f97c265 (v0.1.3). Scope: application/core correctness, lifecycle, rendering, storage and CPU/RAM behavior.

## Findings and fixes

| Priority | Finding | Remedy |
|---|---|---|
| P1 | Valid JSON with a non-object root or primitive city result could escape as InvalidOperationException and terminate automatic callbacks. | Validate provider shapes and normalize malformed data into recoverable InvalidDataException; exercise root/item cases. |
| P2 | Identical minute ticks rebuilt views, effects and menus. | Reuse unchanged view trees using snapshot identity and visible-state keys; invalidate for units, themes, ageing, expiry and real hourly boundaries. Reuse frozen legacy shadows. |
| P2 | Drag, resize and preferences wrote settings multiple times per action. | Persist once after final placement; removed duplicate explicit saves. |
| P2 | City body downloads had no caller deadline. | A 25-second deadline covers headers and body; generation-aware cancellation, finally-owned disposal and expected I/O handling. |
| P2 | Native message hook registered the same Windows message on every callback. | Register once per process; guard updates against destroyed card handles. |

Additional cleanup: data-driven five-theme catalog, shared vector weather-family classification, bounded frozen-artwork cache, and only five forecast days requested for the five-day UI. The three-second desktop recovery probe is retained to preserve the verified shell behavior. No production forced-GC or working-set trimming tricks were added.

## Controlled allocation/layout benchmark

Same public weather fixture, 130x67 compact and 330x243 detail views; .NET10.0.15; Windows build26300. Both runs use DOTNET_TieredCompilation=0 to avoid comparing a prewarmed baseline against tiering delays on a skipped-render workload. Each case warms up then measures creation/update plus offscreen layout on the UI thread. Tests collect before measuring only; production does not. These are managed allocated bytes and workload elapsed time, not resident RAM or OS-wide CPU percentages.

| Workload | Calls | Before allocated MiB | After allocated MiB | Before ms | After ms |
|---|---:|---:|---:|---:|---:|
| compact-unchanged | 500 | 166,613 | 0,023 | 2826,011 | 0,169 |
| compact-changed | 200 | 66,070 | 10,874 | 1100,248 | 371,771 |
| forecast-unchanged | 500 | 367,813 | 0,118 | 5661,428 | 0,379 |
| forecast-changed | 200 | 147,158 | 141,892 | 2391,841 | 2197,646 |

Unchanged compact/detail updates allocate over 99.9% less in this synthetic workload. Real data changes still rebuild affected content, preserving correctness; changed-frame costs are reported rather than hidden. Wall-time results are indicative single-machine measurements. Raw JSON is in docs/performance/.

## Runtime memory interpretation

The long-running original process varied from243.54MiB working set/157.13MiB private bytes to186.14/154.81 during1723.5seconds of ordinary use, spending1.0938CPU seconds. This variation occurred before deployment, so it is not an optimization saving. A fresh optimized process is not an apples-to-apples RAM comparison. WPF/.NET baseline memory remains; reduced allocation and resource churn is the measured improvement.

## Themes

Aurora, Paper and Sunset are frozen vector scenes with matching compact/detail artwork and day/night/weather variants. No large new textures or animation timers. Existing Modern/Classic and user preferences are preserved; default compact size remains130x67. Paper uses dark ink, including its light night palette, without legacy text shadows.

## Validation

Core10/10 and application15/15 assertion groups pass. Includes malformed provider shapes, stalled-body cancellation, existing close regression, layout/assets, cache recovery, theme persistence/repainting, frozen artwork reuse and Kathmandu hourly-boundary invalidation. Offscreen fixture renders were inspected for all three themes. Prior owner-verified desktop ownership/Win+D/virtual-desktop behavior is preserved; no claim of new Explorer-restart or mixed-DPI coverage is made.

Reproduce the controlled comparison from the repository root with: .\scripts\BenchmarkRender.ps1. It creates/reuses a detached baseline checkout under artifacts, supplies the identical fixture/layout harness to both versions and restores the caller's JIT environment afterward. No live weather requests are made by the benchmark.


Post-deployment warm observation: 190.88 seconds, 0.1094 CPU seconds (0.0573% of one logical CPU), 164.79MiB working set and 94.24MiB private bytes. No current-run unhandled exception or icon-load failure was recorded. This is an observation, not a long-duration memory-leak guarantee or an aged-vs-fresh reduction claim.


## v0.2.1 memory investigation and tray correction

System.Runtime counters on the existing process showed roughly 5 MiB managed heap after the last collection and 14.2 MiB GC-committed memory. Most of the process footprint is outside the managed heap. Images and weather objects are not the dominant measured managed-memory cost.

Kept: process-level WPF SoftwareOnly rendering, set before Application construction. The widget has small, mostly static layered windows; avoiding the hardware rendering pipeline reduced measured private memory on this machine. Microsoft documents this switch at https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.renderoptions.processrendermode . No forced garbage collection, working-set trimming or global registry changes.

Short fresh-process observations with the existing saved settings (MiB):

| Rendering | Private committed | Private working set | Total working set |
|---|---:|---:|---:|
| Hardware default repeat | 90.14 | 74.73 | 162.82 |
| Software candidate settled | 41.36 | 34.93 | 147.83 |

Private working set is the closest counter here to Task Manager's usual process Memory display. Total working set includes shared resident pages and is a different metric. Startup measurements showed higher software CPU (3.72 CPU seconds in an early 20-second window versus 0.73 for hardware); after settling the software sample used 0 CPU seconds over 30 seconds, and the repeated hardware sample used 0.047 over 20 seconds. These short observations do not establish a fixed memory ceiling, long-term leak freedom, or drag/resize CPU behavior. Saved scale/theme and background workload can affect results; raw observations are in performance/memory-*.json. Broader native interaction acceptance remains with the owner.

Fixed: the initially empty WinForms tray ContextMenuStrip enters Opening with cancellation set. After populating current menu items, explicitly set cancellation based on the final item count. The regression test failed before the fix (15/16), then passed, including rebuilt theme checks on consecutive openings. Final checks: core 10/10 and application 16/16 groups, build/publish zero warnings/errors. This tests first-opening event behavior, not a simulated OS tray click.

Final packaged v0.2.1 observation after 30-second warmup: 22.34 MiB private working set, 28.67 MiB private committed, 104.98 MiB total working set; 0.0156 CPU seconds over 15 seconds. The package remains running. These are point-in-time observations, not a guaranteed steady footprint.

