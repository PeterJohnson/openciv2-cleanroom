# F04 original-game city-analysis observations

Two expressions remain undefined in
[F04 §6](../algorithms/F04-turn-sequencing.md#open-city-analysis-arithmetic):
the additional support/unhappiness government-score terms and the best-shield
summary producer. This procedure prepares independent original-game evidence;
it supplies neither expression and does not close M4.

The first [Windows observation session, 2026-11-24](f04-original-observations-2026-11-24.md)
records a repeated baseline, no-turn controls and one-city shield outputs 1/2/3.
The [follow-up session](f04-original-observations-2026-11-24-followup.md) confirms
the displayed target version and adds twelve two-city/support turn pairs,
including a matched away-garrison control. These constrain the positive
best-shield producer and final government-score differences; both general
formula gaps remain open. The records retain private capture locations,
observed values and the remaining discriminating experiments.

Use original-game interactions, saved files and, when needed, external process
observations under the
[cleanroom boundary](../architecture.md#1-cleanroom-and-compatibility-boundary).
Original game executables and reconstructed game source are excluded from copying
into the repository/build; other repository material, including capture tooling,
drivers, tests and documentation, may be reused. Do not obtain the missing formulas
from reconstructed game implementations. Review reused assumptions and distinguish
original observations from reconstruction-generated results. The existing
`tools/capture_city_analysis.py` hashes the executable as an opaque file; it does
not launch, disassemble or examine its code. It uses the existing F06 stored-save
inspector, not our gameplay simulation, to read observed values. The optional
process-observation path below is separate tooling, not a feature of that packager.

## Windows handoff

Copy the original installation to a writable Windows working directory. Preserve
the supplied installation and saves. Record the version shown by the original
game's Game Options title (`Ctrl+O`), the operating system and any
patches/compatibility settings. The inspected About entry opened credits instead.
The target is MGE **5.4.0f, patch 3**; an executable hash or save format alone does
not establish that version. Record a mismatch rather than relabeling the result.

The supplied Linux installation was `/home/peter/project/civ2/Civ2`.
Its `civ2.exe` SHA-256 was
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Wine was unavailable on that host. Its existing saves have no controlled action
record and are useful only for tooling checks, not formula conclusions.

Keep the same repository revision for the capture and subsequent analysis.
The root CLI build requires the SDK pinned in `global.json`; no desktop build
or cross-platform qualification is required to inspect the files. From the
repository root, build `src/OpenCiv2.Cli` in Release following
[BUILDING](../BUILDING.md). Alternatively, transfer the original saves, rules and
notes back to the existing Linux checkout for inspection there.

### Reusable Windows setup

The 2026-11-23/24 review inspected tooling and setup documentation in these local
checkouts; it did not execute the original game or validate the current Windows
machine:

| Repository | Inspected revision | Operational references relative to that checkout |
| --- | --- | --- |
| `/home/peter/project/civ2` | `4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752` | `analysis/windows/README.md`, `analysis/reconstruction/checkpoint-tooling/README.md`, `README.md` |
| `/home/peter/project/openciv2` | `0629e37fb9252ac5ff3788a6807baf1777aee414` | `docs/DEVELOPMENT.md`, `README.md` |

Locate the corresponding Windows checkouts rather than assuming these Linux
paths transfer. The previously observed original installation was
`C:\Projects\civ2\Civ2`; the [earlier cleanroom launch procedure](../m1-original-text-observations.md#setup-and-reproduction)
successfully launched `civ2.exe` directly from an isolated copy with that copy as
its working directory. Preserve the supplied installation; use distinct writable
experiment directories and record all selected rules/settings. Original assets
are untracked inputs and do not arrive with a Git clone.

The `civ2` Windows guide documents `tools\python.cmd` for finding a real Python
3.10+ interpreter instead of a WindowsApps alias, and `--print-executable` when an
absolute path is needed. The helper, its UTF-8 setup and other non-game tooling
may be reused. Follow its MSVC x86/CMake/Ninja setup guidance if a native observer
becomes necessary; the Python memory reader below needs no reconstructed
application build. Rediscover installed tools on the destination machine rather
than copying historical version-specific paths. Ghidra/annotation work is not a
prerequisite for the save-pair or memory-reader workflows.

### Capture-tool audit and reuse

The two scripts below were read in full at the pinned `civ2` revision. Neither was
executed. Their generic code may be reused; a new implementation of Windows memory
reading is not required merely because the code lives in that repository.

| Tool | Reusable pieces | Experiment-specific assumptions to replace or verify |
| --- | --- | --- |
| `analysis/scripts/capture-process-windows.py` | Standard-library `ctypes` process query/read, exact-length reads, executable hashing, raw block files and JSON metadata. Opens with query/read permissions only; does not launch, inject, write game memory or invoke game routines. | Requires an already paused x32dbg target but cannot prove the pause. Hardcodes `build-runtime/Civ2/civ2.exe`, image identity/base expectations, block addresses/extents and 32-bit map-pointer/geometry decoding. Existing blocks omit F04 scores and most C03 workspace state. |
| `analysis/scripts/compare-runtime-windows.py` | Block length/hash validation and byte-difference reporting. | Imports the partial `inspect_saves` parser; selects one named save, six load/move snapshots, particular ID-change allowances and unit 48. Its EAX results are transcribed constants, not register captures. Its main routine is not an F04/C03 comparator. |

The capture script's SHA-256 is
`7eaaa945df7757c83ea051111f02c96935fa7b84f7499cf80b86f8022de60dfe`;
the comparator's is
`09f7d816c6b9d164c6d87edb30d9ef301375d6b5b2502ba3259ca51601f0ca5d`.
Preserve these source identities when adapting the tools and hash the resulting
driver/profile for each run. Only the imported save parser's docstring/imports
and declaration inventory were inspected; its implementation was not audited.
Use the existing cleanroom F06 inspector for the save-pair path.

Separate the generic process reader from a versioned observation profile of
addresses, widths, dereferences and required fields. Define semantic decoding
from F04/F06 and the owning C02–C05 specifications. C03's
[observation appendix](../algorithms/C03-city-economy-and-corruption.md#appendix-a-original-encoding-and-storage-for-validation)
provides several workspace mappings, but not a complete F04 observation profile.
Other reusable address/layout documentation may guide measurement setup; verify
those mappings against the pinned target and record their provenance. Record
missing measurement definitions explicitly; do not guess addresses or transplant
reconstructed formulas into the driver. Measurement metadata does not establish
the missing gameplay expressions.

Before using an adapted reader for formula evidence:

- Establish and record the debugger stop, thread, instruction location and
  operation/city identity. Keep the process stopped across the entire snapshot.
  Capture required registers from the debugger rather than copying historical
  return values. A free-text action label alone proves neither boundary nor pause.
- Retain executable, rules, driver/profile and relevant module identities, plus
  debugger settings and any hooks/patches. A matching on-disk executable hash does
  not prove unchanged in-memory execution. The existing reader also records entry
  code bytes; keep such raw code captures external to this repository.
- Refuse existing capture destinations, retain incomplete output on failure, and
  reject missing required blocks. The old reader can overwrite output and silently
  omit a surface when its geometry condition fails. Its comparator uses `assert`
  for integrity checks; use explicit failures that remain active under `python -O`.
- Preserve raw bytes alongside decoded values and hashes. Record repeat captures
  separately; a comparator-only rerun is recomparison, not another original run.

The other rigs are available tooling, but their existing comparison contracts do
not answer these gaps. `civ2`'s native oracle relocates an original image and calls
selected routines without initializing the full application; supplied state and
children must be accounted for before attributing a result to a real city turn.
`openciv2` documents an original-game Wine replay via an `XDaemon.dll` proxy, fixed
randomness and virtual time, with pixel/raw-`rand()` comparisons. Its unit corpus
is recorded from the reconstructed build, even when the surrounding replay is
compared with the original. Those snapshots are not independent formula evidence.
Neither rig requires importing reconstructed gameplay into the cleanroom build.
If random choices matter, follow F01's semantic-choice comparison; matching seeds
or raw call counts across the different production RNG policies is not required.

## Establish an observable turn boundary first

1. Use a separate, small ordinary game with one controlled city. Record difficulty,
   government, turn, player identities, rates, advances, wonders, city improvements,
   worked squares/specialists, food and shield stocks, production, happiness and
   every supported unit's type/home/location. Retain screenshots of the city and
   relevant reports alongside the captures. Note scenario/editor use explicitly.
2. At a stable player-input boundary, save `before.sav`. Finish exactly one turn
   through normal original-game input, recording every choice and event. Save
   `after.sav` at the next equivalent input boundary. Do not overwrite the baseline.
3. Reload the baseline and repeat unchanged into a separate capture. Distinguish
   repeatability from variation caused by random events, AI actions or save/load.
4. Check whether the saved summaries actually change at this boundary. Merely
   opening a city window must not be assumed to execute F04 upkeep. If save/load
   or report inspection changes the values, record a separate no-turn pair and
   determine a usable boundary before attributing changes to a city turn.

Both outputs are signed 16-bit stored fields in each civilization record:
`best_shield_surplus` and `government_score[0..6]`. Their layout is defined by
[F06](../algorithms/F06-save-load-and-file-formats.md). The summary includes all
eight slots; select the actual observed player during analysis. Values saved
for other civilizations may belong to different upkeep boundaries.

## Controlled comparisons

Branch each experiment from a recorded baseline. Change one factor through the
original UI, save that branch's own before-state, and capture the same turn
boundary. If other state changes, record it; do not call it an isolated factor.

| Gap | Comparisons to collect | Confounders to record |
| --- | --- | --- |
| Best-shield producer | Several adjacent positive shield outputs, zero and a shortage where attainable; supported-unit changes; disorder versus ordinary production | Gross/net shields, support, corruption/waste, production completion, city size and mood |
| Best-shield comparison/publication | Two cities with unequal and equal outputs; vary which city has each output and their creation order | Actual city slots, removed cities, processing order, government and turn boundary |
| Additional government scores | Each attainable government; unit counts below/at/above support thresholds; military units at home versus away; ordinary/disordered/celebrating cities | Existing C02/C03 contributions, size, worked tiles, trade, rates, happiness buildings, wonders and supported unit identities |
| Accumulation and reset | One city versus two; repeated unchanged turns; then a changed city | Pass reset, Anarchy post-pass contribution, AI rates/strategic adjustments and intervening events |

Start with a human-owned city to reduce AI policy changes. Government changes
may require intervening Anarchy turns; establish a new stable baseline rather
than treating revolution itself as the isolated comparison. Do not assume a
particular multiplier, maximum, rounding rule, government ordering beyond the
specified enum, or a zero residual. Final saved government scores include other
specified contributors and pass-level adjustments; subtract only terms whose
inputs and timing have been established. If final saves cannot distinguish
candidate expressions, record that observability limit and design another
original-game experiment. Do not fill the gap from consumers or reconstructed
game implementations.

## If final saves cannot isolate the expressions

Use the reusable process reader with a narrowly defined profile around the actual
original city turn. Continue to collect normal save pairs so the instrumented
observations retain a gameplay and persistence context. Do not substitute calls
to reconstructed functions or supplied gameplay children for the original turn.

The first instrumentation targets come from F04 §6's existing stage contract:

| Boundary to distinguish | Required observation and purpose |
| --- | --- |
| City-pass entry and exit | Observe the summary/score reset, actual city order and post-pass adjustments. Separate per-city deltas from the Anarchy and A08 contributions. |
| First versus second full recalculation | Identify the city and invocation, processing/reallocation flags and relevant C02–C05 inputs/results. The second follows production and support retirement, so its units and workspace can differ from the first. |
| Stage 5 after the second recalculation, before/after the additional terms | Capture all seven government scores and the support/military/mood inputs used by the experiment. Isolate the missing parent delta from already specified recalculation contributions. |
| Stage 7 before/after best-shield publication | Capture the prior/resulting summary, the city's current shield/support/waste values and relevant mode/state. Distinguish scale/rounding from comparison/accumulation; use unequal/equal two-city cases and recorded processing order. |

These are requested semantic boundaries, not verified breakpoint addresses or
implemented capture commands. Record the actual observation method and any
unresolved boundary. Start with a stable human city and one repeated baseline,
then the discriminating cases above. Avoid production completion, growth,
retirement or random incidents in the first baseline where possible; record them
when they occur rather than silently removing their effects.

The earlier C03 comparison proposal is useful here as a dependency check, not a
replacement for F04 observation. C03 recalculation has transient luxury, support,
corruption, pollution and whole mood values that a save alone does not retain.
It can also change analysis scores; repeated display/recalculation calls are not
automatically harmless. Capture the required workspace at the named boundary.
C03 itself draws no random values directly, but its children's contracts still
apply. Exact conditional comparisons need their relevant inputs as well.

Keep the process snapshots, debugger transcript, action log and decoder/profile
with hashes beside the packaged save evidence. `capture_city_analysis.py` does
not package these extra artifacts automatically: list and hash them separately.
Resolve a formula only when independent cases distinguish the plausible
expressions and confirm timing, rounding and publication. Update the owning
specification before adding implementation and expected-result cases. A successful
capture, a byte difference or one matching city does not close either F04 gap.

## Package a pair

Create a UTF-8 `notes.txt` containing:

- Operator, date, repository commit, displayed original version and environment.
- Exact baseline origin and whether it was authored, edited or naturally played.
- Controlled variable, civilization/city slots, pre/post turn boundary and all
  actions, prompts, choices, unexpected events and screenshots' filenames.
- Effective rules/scenario files and changes; keep any additional content beside
  the package. The tool copies only the explicitly supplied rules file.
- Whether this is an original observed run or a tooling-only smoke check.

Run from the repository root in PowerShell (Python 3 and the .NET runtime required):

```powershell
python tools/capture_city_analysis.py `
  --before C:\captures\trial01\before.sav `
  --after C:\captures\trial01\after.sav `
  --executable C:\Games\Civ2\civ2.exe `
  --rules C:\Games\Civ2\RULES.TXT `
  --notes C:\captures\trial01\notes.txt `
  --output C:\captures\packaged-trial01 `
  --adapter dotnet src/OpenCiv2.Cli/bin/Release/net11.0/oc2.dll
```

Put `--adapter` last. Use a new output directory for every run. The tool copies
the saves/rules/notes, retains full stored civilization/city/unit/global inspection
JSON and diagnostics, and writes a compact `summary.json` plus hash manifest.
Original inputs must be closed/stable while copying. The original executable
is only hashed, never copied or executed. A failed inspection leaves an
`incomplete` manifest and raw output; do not treat that as a valid observation.
An existing destination is refused. The inspector currently admits save version
`0x2c`; unsupported versions require separate format work, not header editing.

`packaged` means collection succeeded, not that the supplied notes establish
original provenance or compatibility. Preserve raw saves, rules, notes and
screenshots privately; do not commit original game assets. Review observations,
resolve each formula in its owning specification, then add independently derived
behavioral cases and integrate the general F04 city turn. Complete generated
human/AI games and the remaining M4 gates still follow that work.
