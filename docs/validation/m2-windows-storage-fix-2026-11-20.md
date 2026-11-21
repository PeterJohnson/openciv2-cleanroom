# Windows storage correction — 2026-11-20

The two failures in the [initial Windows M2 run](m2-windows-2026-11-20.md)
are corrected. The affected native suite now reports **489 total: 488 passed,
zero failed, one skipped**. All **nine storage tests** pass. This closes those
two storage failures, not the remaining M2 platform and performance gates.

## Change

`NativeByteStore` still stages owned bytes in the destination directory, flushes
and closes the staging file, then publishes it off the dispatcher. On Windows,
initial publication uses an exclusive `File.Move`. If another value already
exists, publication uses `File.Replace`. It does not delete the destination first
or copy bytes into a visible file. Unix retains the existing overwrite-rename.

A local probe reproduced overwrite-move failing while a read handle allowed
delete sharing. Replacement succeeded: the existing handle read the original
bytes and a newly opened handle read the new bytes. This matches the documented
sharing behavior of [Windows ReplaceFile](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew).
Trying exclusive creation before replacement also covers a destination created
by another publisher between attempts. Real permission/I/O failures propagate;
staging cleanup and the instance's serialization gate retain their existing scope.

The directory-collision test now accepts `IOException` or
`UnauthorizedAccessException`, then verifies that the directory remains, staging
is gone, invalid keys are rejected and a subsequent valid write succeeds. It no
longer requires one platform's exception class. No production error is suppressed
to make that test pass.

Three regression cases were added under the existing Architecture §§7–8 /
`IByteStore` contract:

- Keep a reader open across replacement; its remaining bytes stay unchanged,
  while a new reader receives the complete replacement.
- Race two first publishers against a missing key, repeated for ten keys; both
  complete, the final payload is one complete input and no staging remains.
- On Windows, hold a reader that denies delete sharing. Replacement must fail,
  retain the old value and clean staging; after closing the reader, the next
  replacement succeeds.

The held-reader case failed against the original implementation before the fix.
An exploratory eight-writer test also found OS replacement contention. The final
creation-race test specifically covers two first publishers; it does not claim
general coordination among independent writers. Stores retain per-instance
serialization, and contention between independent replacement writers can still
be reported as an I/O failure. The exploratory logs are preserved separately from
the final accepted test runs.

## Executed checks

The machine, pinned SDK/runtime, compiler and content environment are unchanged
from the initial report. Tested changes are an uncommitted diff from
`b332fe11ed32947a8bb16dd718ea1af0284ff909`.

| Check | Result |
|---|---|
| Focused `PlatformStorageTests` | 9 passed, no failures or skips |
| Complete native suite | 488 passed, no failures, 1 existing audio-device skip |
| Root and App Release builds | Pass, zero warnings/errors |
| Managed desktop publication | Pass; notices for 28 locked packages / 33 files |
| Windows x64 Native AOT publication | Pass; notices for 31 locked packages / 37 files |
| Root and App formatting | Format applied, diff reviewed, verification passes |
| Identity infrastructure | 7 tests pass |
| Project boundaries and coverage | 42 projects, 56 families and 456 shared groups checked |

Native AOT retains the two previously observed Avalonia DirectComposition
`CreateTargetForHwnd` non-blittable-Boolean diagnostics. No new publication
diagnostic appeared. The normal managed restore graph was restored after AOT.

The skip remains
`NativeAudioTests.Native_output_completion_loop_cancellation_and_replacement`;
this run did not supply an isolated audio output sink. Browser code, Lua,
rendering and media code did not change. Browser/WASM, the raster matrix, audio
graph, actual desktop UI sequences and performance populations were not rerun
for this storage-only correction; their earlier results remain historical.

## Reproduction and evidence

Outputs are in `build/m2-windows-storage-fix/`. Use the pinned environment in
`build/m2-windows/env.ps1` and run from the repository root:

```powershell
. ./build/m2-windows/env.ps1
dotnet format OpenCiv2.slnx --no-restore
dotnet format solutions/OpenCiv2.App.slnx --no-restore
dotnet build OpenCiv2.slnx -c Release --no-restore --disable-build-servers -m:1
dotnet build solutions/OpenCiv2.App.slnx -c Release --no-restore --disable-build-servers -m:1
dotnet tests/OpenCiv2.Base.Tests/bin/Release/net11.0/OpenCiv2.Base.Tests.dll -class OpenCiv2.Base.Tests.PlatformStorageTests
dotnet test --project tests/OpenCiv2.Base.Tests -c Release --no-build --no-restore
dotnet format OpenCiv2.slnx --no-restore --verify-no-changes
dotnet format solutions/OpenCiv2.App.slnx --no-restore --verify-no-changes
python tools/dotnet/test_identity.py
python tools/dotnet/check_graph.py
python tools/dotnet/check_coverage.py
dotnet publish src/OpenCiv2.Desktop -c Release --no-restore --disable-build-servers -m:1
dotnet restore src/OpenCiv2.Desktop -p:Oc2DesktopAot=true --locked-mode --disable-build-servers
dotnet publish src/OpenCiv2.Desktop -c Release -p:Oc2DesktopAot=true --no-restore --disable-build-servers -m:1
dotnet restore solutions/OpenCiv2.App.slnx --locked-mode --disable-build-servers
```

`storage-focused-final.log` and `native-tests.log` contain the accepted results.
`regression-before.log` retains the failing original held-reader case, and
`storage-focused.log` retains the exploratory replacement-contention result.
`before/` preserves 1,180 original source/artifact files, including all files
named by the original receipt's compiled-source and artifact inventories. The
original failure receipt, logs and UI captures in `build/m2-windows/` are unchanged.
Do not validate that historical receipt against the modified checkout or relabel
its UI/raster outputs as this build.

The follow-up `evidence.json` inventories the final source changes, build-time
input manifests, executed/published binaries and logs, and verifies the preserved
original files against their original hashes. `audit.py` reproduces the inventory.
No changes were staged or committed.
