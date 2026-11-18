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
remains historical evidence. The updated hosted workflows require a fresh run
after commit/push; actual Windows/macOS desktop checks remain open.

## M2 map-rendering acceptance

Complete-renderer acceptance is separately tracked for
[P02 MV01–MV30](map-visual-coverage.md) against the
[M2 matrix](../testing/map-rendering.md) and
[interactive performance contract](../performance/map-rendering.md). All complete
native/browser feature gates remain pending; expanding the specification does
not extend the scope of earlier test reports.

## Executed cross-platform CI

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
