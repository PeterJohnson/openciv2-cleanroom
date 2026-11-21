# Native platform acceptance

Use [the root build guide](../BUILDING.md), [test matrix](README.md) and
[packaging](../packaging.md) on each target with the pinned SDK/runtime and native
compiler. Record OS version/architecture, hardware, display scale, exact commit,
working-tree status, commands, outputs and skips. Stop at a failure; later passes
do not erase it. Build both optional runtimes and exercise both extracted CLI
archives with installed-runtime roots empty.

[Executed CI](../validation/README.md) already covers Linux x64, macOS ARM64 and
Windows x64 core/CLI, Lua/SQLite, desktop compilation/publication and packages.
Actual Windows/macOS desktop and OS delivery checks below remain pending. The
[Riverbend slice](../validation/m2-map-slice.md) adds Linux X11 validation with
pinned content and software raster results for simulated 4K/5K surfaces.

## Actual desktop input

The existing automatic native input driver is X11-specific. On macOS/Windows run
the real desktop host, without routing input through the Linux driver:

```sh
dotnet src/OpenCiv2.Desktop/bin/Release/net11.0/OpenCiv2.Desktop.dll --observe build/test-results/desktop-observation.json
```

The observation option is read-only and writes the latest permitted view plus
`desktop-observation.json.identity.json`. Save a copy of the observation after
each step, screenshots, the actual input method, display scale and exit status:

1. Ready state shows only unit slots 0, 1 and 2 for observer 1; foreign slot 3 is
   absent. Select unit 0 and press **Move selected unit to bottom**. The operation
   reaches `AwaitingInteraction` with zero committed effects.
2. Press **Cancel command**. It reaches `Cancelled`, with zero effects.
3. Submit unit 0 again and press **Confirm**. It reaches `Completed`, `changed`
   is true and the effect count is one.
4. Repeat the same submission and confirmation. It completes with `changed`
   false and zero effects. Submit again and **Decline**; it cancels with no effect.
5. Restart the host for a fresh world. Type `unit.stack-bottom unit=0:100` in the
   console and press Enter. Reach the confirmation using normal keyboard focus
   and activate it. The semantic result matches step 3. Try
   `unit.stack-bottom unit=3:103`; it must not reveal the foreign unit or commit
   effects. Record the actual rejection text/status.
6. Close the window through the OS close action and verify a successful process
   exit without a retained background session.

After closing the host, validate its identity against the checkout and record
output-file hashes from the repository root:

```sh
python -c 'import json,sys; sys.path.insert(0,"tools/dotnet"); from run_application import validate; print("Validated compiled inputs:",len(validate(json.load(open("build/test-results/desktop-observation.json.identity.json")))))'
python -c 'import hashlib,json,pathlib; p=pathlib.Path("src/OpenCiv2.Desktop/bin/Release/net11.0"); d={str(f):hashlib.sha256(f.read_bytes()).hexdigest() for f in sorted(p.rglob("*")) if f.is_file()}; pathlib.Path("build/test-results/desktop-artifacts.json").write_text(json.dumps(d,indent=2))'
```

A manual observation record must identify its exact
binary and input sequence. An identity file by itself proves neither input
delivery nor a passing scenario. The ordinary host does not write observations
unless `--observe` is explicitly supplied.

## Gates still requiring native evidence

| Gate | Required evidence on each target |
| --- | --- |
| OS cancellation/output delivery | Demonstrate actual target-OS interrupt/cancel behavior, no unintended partial publication and closed output handling. The Linux SIGINT/SIGTERM driver is not Windows console-event evidence. |
| Avalonia seam | Real native pointer/keyboard sequence above, matching results/effects, normal shutdown and artifact-bound observations. An application build alone is insufficient. |
| Performance | Actual target hardware/startup/resource results if claiming those platform budgets; the accepted reference measurements are Linux/Chromium only. |
| Other native architectures | Repeat the automated matrix and applicable input/OS gates on macOS x64, Linux ARM64 and Windows ARM64 before claiming those RIDs. macOS ARM64 execution does not establish another OS's ARM64 result. |

Full native menus (macOS NativeMenu; Windows/Linux in-window menus), screen readers,
IME, fullscreen/multi-monitor DPI and complete rendering remain M2. Native/
IndexedDB storage has [Linux/browser evidence](../validation/m2-storage-services.md);
Windows/macOS file pickers and remaining browser engines still need execution.
Original-peer networking, full gameplay and release signing/installers retain
their owning milestones. They are not silently satisfied by the
bounded foundation result. Browser execution remains its own required evidence
and uses the separate commands in the build guide and CI.
