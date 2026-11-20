# Validation status

The C# core/CLI and bounded application/automation hosts have executed native and
browser evidence. This page distinguishes those checks from desktop acceptance
and whole-game compatibility. Build and test commands are in the
[build guide](../BUILDING.md) and [test matrix](../testing/README.md).

## Current local validation

The opt-in desktop Native AOT build passed
[Linux validation](desktop-aot-2026-11-17.md) on 2026-11-17 Pacific: publication,
six actual pointer checks and normal shutdown, plus the same checks for the
managed default and all 454 core tests. AOT locks cover Linux x64, macOS ARM64 and
Windows x64; only Linux AOT compilation/execution was exercised locally.

The .NET 11 and shared SkiaSharp 4 upgrade passed
[local Linux/Chromium validation](dotnet11-2026-11-18.md) on 2026-11-18 UTC,
including native/browser behavior, actual application input, extracted CLI
packages and collector smoke checks. That record identifies the uncommitted
tested tree and report hashes. The [previous .NET 10 run](local-2026-11-17.md)
remains historical evidence. The upgraded hosted workflows have now passed for
the signed Native AOT lock correction, as recorded below; actual Windows/macOS
desktop checks remain open.

## M2 map slice

The [staged GPU optimization record](m2-gpu-optimization.md) evaluates command
reuse, shared tint masks, stable placement, sparse damage and retained map
regions against the preserved renderer. Its experimental visual failures and
target-hardware measurements remain separate from complete M2 acceptance.

The [browser performance record](m2-browser-performance.md) compares Software2D
and WebGL with CPU/GPU composition at 1080p, 4K and 5K. Ordered foreground
commands improve sustained 1080p GPU pan with exact paired endpoints; terrain
rebuilds, world updates and dense overviews retain failed performance gates.
The [zoom stall investigation](m2-browser-stalls.md) follows those terrain rebuilds
through raster/detail transitions and evaluates serial browser atlas batching.

The [GPU composition experiments](m2-gpu-experiments.md) compare batching,
preparation offload, retained content, cache budgets, pipelining and native input
latency at 1080p, 4K and 5K. They repair a stale-upload error in the CPU baseline
and supersede the [first prototype's](m2-gpu-composition-prototype.md) performance
conclusion and earlier warm hardware-Xwayland timings using the affected path.
GPU composition remains experimental; the measurements and pixel comparisons
are separate from full M2 acceptance.

The [Riverbend validation record](m2-map-slice.md) covers pinned graphics and
fonts, the Linux interaction and the CPU raster workloads. The later
[browser map record](m2-browser-map.md) covers pinned HTTP content admission,
the full shared MV01–MV30 matrix in WASM and the production WebGL gallery.
Unavailable platform checks remain open.

Complete-renderer acceptance is separately tracked for
[P02 MV01–MV30](map-visual-coverage.md) against the
[M2 matrix](../testing/map-rendering.md) and
[interactive performance contract](../performance/map-rendering.md). Backend
functional evidence, hardware performance and complete platform gates are
separate; expanding the specification does not extend earlier test reports.

## Executed cross-platform CI

Commit `9bc6e0176872d63504b2a206ecadbe3a3c9624b4` passed the
[build and test workflow](https://github.com/PeterJohnson/openciv2-cleanroom/actions/runs/44235017302)
and [repository checks](https://github.com/PeterJohnson/openciv2-cleanroom/actions/runs/44235017244).
All five build/test jobs succeeded: implementation coverage, browser/Lua/application
input, and the Linux x64, macOS ARM64 and Windows x64 core/CLI/package jobs.
The native jobs include successful managed desktop builds and Native AOT
publication with the corrected signed-package locks. Linux additionally passed
managed and AOT X11 input; Windows/macOS desktop interaction remains unverified.
This is a workflow/job-status check of the .NET 11 revision, not a new downloaded
artifact receipt or target-hardware performance measurement.

### Earlier artifact receipt

Commit `7a3a3199f90ab0bb98bb425590e04ab17b311c4f` passed the
[application/core CI run](https://github.com/PeterJohnson/openciv2-cleanroom/actions/runs/44027604903)
and [repository checks](https://github.com/PeterJohnson/openciv2-cleanroom/actions/runs/44027604889)
on 2026-11-17 UTC. The immutable [receipt](ci-2026-11-17.json) records original
run/job IDs, architectures, report hashes and artifact/package digests. Its paths
and branch names identify that executed revision; they are not current build
instructions. Repository layout changes require fresh validation before claiming
that CI has executed the new revision.

| Surface | Executed result |
| --- | --- |
| Linux x64, macOS ARM64, Windows x64 | 454 core tests per target: 432 shared groups, twenty native I/O and two API-boundary checks; zero skips |
| X02 | 57 selected cases on all three native targets and Chromium, with equal observations and consumed prefixes |
| Independent CLI/helper drivers | Eight suites, 56 tests per native target, including five SQLite CLI tests |
| Optional runtimes | Thirteen Lua groups and four SQLite tests on each native target; thirteen Lua groups also on WASM |
| Self-contained CLI archives | 36 base and 41 SQLite extracted-package cases per target, zero skips; bundled CoreCLR observed with installed-runtime roots empty; two manifest rejection checks per archive |
| Browser application | Sixteen actual pointer/keyboard checks; 432 shared groups; versioned conformance and Lua parity |
| Linux application/runtime | Six actual X11 pointer checks and normal window close; Lua sanitizer harness; SIGINT/SIGTERM/closed-output checks |
| Tooling/publication | Locked restores, formatting, dependency graph, source identities, desktop build/publication and dependency/font notices |

The native targets used Ubuntu 24.04 x64, macOS 15 ARM64 and Windows Server 2022
x64, with SDK 10.0.404/runtime 10.0.15. Browser CI used Chromium 145.0.7632.6 and
Playwright 1.58.2. Reports preserve the actual runtime/compiler and binary hashes.
CI artifacts are evidence, not signed releases or original-game interoperability.

## Remaining acceptance

- Actual Windows/macOS desktop input and normal shutdown.
- Native OS interruption/output delivery and target-hardware performance on those
  platforms; Linux results do not establish them.
- Native execution of additional RIDs (`osx-x64`, `linux-arm64`, `win-arm64`).
- M2 map rendering, menus, fullscreen/mixed-DPI, IME/accessibility, content loading
  and native/browser storage.
- Later gameplay, complete automation, original-peer networking and signed releases.

Use [platform acceptance](../testing/platforms.md) to record results and failures.
Headless tests, emulated density and restore-only checks cannot close native
interaction or hardware-performance gates. Authored fixtures and C# regression
cases cannot replace independently captured original-game observations.

The [two-renderer integration](m2-renderer-options.md) adds desktop preferences,
GPU fallback/retry and deterministic atlas placement. Its scope is Linux only;
other-platform execution is explicitly deferred.
