# F04 original-game city-analysis observations

Two expressions remain undefined in
[F04 §6](../algorithms/F04-turn-sequencing.md#open-city-analysis-arithmetic):
the additional support/unhappiness government-score terms and the best-shield
summary producer. This procedure prepares independent original-game evidence;
it supplies neither expression and does not close M4.

Use original-game interactions and saved files under the
[cleanroom boundary](../architecture.md#1-cleanroom-and-compatibility-boundary).
Do not consult reconstruction/decompilation code, source-derived drivers or
historical harnesses. The capture tool hashes the executable as an opaque file;
it does not launch, disassemble or examine its code. It uses the existing F06
stored-save inspector, not our gameplay simulation, to read observed values.

## Windows handoff

Copy the original installation to a writable Windows working directory. Preserve
the supplied installation and saves. Record the version shown by the original
game's About screen, the operating system and any patches/compatibility settings.
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
original-game experiment. Do not fill the gap from callers or external code.

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
