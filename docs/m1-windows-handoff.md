# M1 record/codec handoff to Windows

M1 remains **in progress**. This is a stable stopping point for continuing the
[full completion audit](m1-validation.md), not a milestone completion claim.
The user requested pausing after this commit to resume on a Windows machine.

The Windows continuation is recorded in [M1 stored-save records](m1-stored-saves.md).
The remainder of this handoff preserves the earlier stopping-point evidence.

## Implemented record boundary

`state/records.hpp` defines complete logical unit/city payloads and heap-backed
stable pools: 2048 unit slots and 256 backed city slots, with the ordinary city
creation limit separately named as 255. Scan bounds, holes, retained payload,
slot references and lifetime tokens remain independent. Deletion finalization
clears only the token and decreases a last-slot bound once, after the owning
operation's cleanup. `at` can access backed retained records beyond the scan;
`live` requires both a scanned slot and a nonzero token. Unsupported positive
deletion slots reject before access.

The outer F05 entity reset invalidates all records and unit stack links while
retaining other payload; the inner reset clears bounds and selects unit slot 0.
Neither resets process tokens. These are portions of the larger lifecycle.
Default C++ record values are construction conveniences, not a claim about all
original process-global initializers. Load callers supply retained records.
The shared city-mood dispatch preserves cached results from a different city;
full dispatch publishes its selected city before calling the owning pipeline.

`oc2_formats` encodes fields explicitly, without struct dumps or host I/O:

- Unit, city, tile, region and map-header codecs preserve every stored bit,
  fixed-text tails, signed values and positive home-city slots 128..254.
- Raw-map decoding keeps stored header sizes, separate X/Y start arrays, tiles
  and trailing bytes. Dimension-derived sizes determine the read window even
  when stored size fields disagree. No live state is changed by decoding.
- Raw/embedded map item loading preserves partial-header/start/layer/tile effects,
  W01 allocation order, fallback, summaries and civilization-0 memory. A live
  map export uses the allocated geometry's recomputed sizes.
- Entity item loading implements the F06 helper's 26/30/32-byte unit and
  84/88-byte city layouts. Omitted suffixes and failed-item prefixes survive.
  **No lifetime renewal occurs until every unit and city input succeeds.**
  Modern holes remain zero; old records all receive tokens. Zero-size blocks
  make no I/O request. The complete game's version gate is separate from the
  helper's additional intermediate layouts.
- Record writes include holes, retain slot order and stop at a failed item.
  The supplied service owns transferred bytes/cursors and failure outcomes.

These are codecs and item helpers, not the complete F06 core loader, new-game
initialization or semantic file validator. Civilization/global/scenario/event
records, rules/names restoration, session continuation and full save export are
still required. `oc2 inspect`, `validate`, `diff` and SQLite export do not yet exist.
Malformed dimensions and unbacked accesses reject at the declared boundary;
lossless record decoding deliberately preserves values that a later nominal-domain
validator may flag. It does not normalize or repair them.

## Validation available before the handoff

Linux Clang 20.1.8/CMake 3.31.10: 50 CTests pass, including 47 Catch2 cases and
20 X02 vectors. ASan/UBSan passes the same suite with leak detection enabled.
Emscripten 4.0.23/Node passes all four CTests, running the same 47 Catch2 cases
and 20 vectors. All 99 Python checks pass. The library-only build still passes
without GUI, assets, Python discovery or dependency fetching. Windows/macOS have
not yet executed these new changes. The Catch2 macros emit their existing WASM
`__COUNTER__` warnings.

The supplied installation is `/home/peter/project/civ2/Civ2`. Only original game
files in that explicit directory were inspected. No reconstruction source or
historical harness was used. The SHA-256 of `civ2.exe` is:

```text
edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c
```

The new native test driver `oc2_map_roundtrip INPUT.MP NEW_OUTPUT.MP` decoded and
re-encoded EUROPE, GREECE, MEDITERR, PACIFIC, WORLD, WORLD_M and WORLD_S maps.
Each output equals its input byte-for-byte; input hashes remained unchanged.
An existing-output refusal also passed. See the
[external comparison report](conformance/reports/original-maps-native.json).
Original files and exports remain external; the report carries metadata/hashes.
This is a manually retained check, distinct from X02 execution reports.

No original program was executed here: Wine, wine64, Xvfb and xvfb-run were absent
from PATH. The original accepting an export, resuming a save and communicating
with another peer are separate pending observations.

## Windows checks

Use a Visual Studio 2022 developer PowerShell with its SDK, Ninja, Clang 20.1.8
and CMake 3.31.10 available, following [BUILDING.md](BUILDING.md). The explicit
fetch preset below permits the pinned Catch2 download; an offline source override
is documented there. Run from this checkout:

```powershell
cmake --preset headless-fetch -DCMAKE_C_COMPILER=clang-cl -DCMAKE_CXX_COMPILER=clang-cl -DOC2_VERIFY_TOOLCHAIN=ON
cmake --build --preset headless-fetch --parallel 4
ctest --preset headless-fetch --output-on-failure
python tools/check_conformance.py
```

Supply the Windows original-installation path and a fresh scratch directory.
Verify the executable against the hash above, then run the codec comparisons:

```powershell
$oc2Original = 'D:\Games\Civ2'
$oc2Scratch = Join-Path $env:TEMP ('oc2-m1-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $oc2Scratch | Out-Null
Get-FileHash (Join-Path $oc2Original 'civ2.exe') -Algorithm SHA256
Get-ChildItem $oc2Original -Filter '*.MP' | ForEach-Object {
    $oc2Output = Join-Path $oc2Scratch $_.Name
    & .\build\headless-fetch\oc2_map_roundtrip.exe $_.FullName $oc2Output
    if ($LASTEXITCODE -ne 0) { throw "Map comparison failed: $($_.Name)" }
    Get-FileHash $_.FullName, $oc2Output -Algorithm SHA256
}
```

Then run the original program/map editor against scratch copies. Open a generated
export and record acceptance, dimensions and selected tile/start values. Save a
separate copy with the original editor, pass that copy through the C++ driver,
and reopen its output in the original. Include a deliberately edited ordinary
tile and tribe starting position to exercise an original-authored variation.
Keep input files untouched and retain separate outputs. Record executable/editor
hashes, OS, compatibility settings/extensions, exact actions, file hashes,
observations and failures. A header identity or codec round trip alone is not an
original-execution result. Do not publish proprietary files as repository fixtures.

## Resume implementation

This handoff's implementation sequence is historical and has been superseded.
Resume only from the seven [finite closeout gates](m1-closeout.md) and their
[current audit status](m1-validation.md#current-closeout-status). Do not reimplement
completed record/content/query work or expand M1 into full gameplay, generalized
runner coverage or live networking. Stored and loaded-state claims remain distinct.
