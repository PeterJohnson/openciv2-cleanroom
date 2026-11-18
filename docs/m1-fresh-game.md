# Fresh-game isolation evidence

The compatibility review makes X01 fresh-game isolation an M1 foundation gate.
`SessionRuntime.PrepareNewGame` replaces world/workspace and mutable content
state before selected content is installed. It preserves explicit preferences,
identity generators and the caller's declared random contexts. The F05 entry
parent also resets classic file scratch. This is a separate operation from
client-session replacement, importing a save, and within-game civilization creation.

## Registered observation

The X02 `F02.prepareNewGame` adapter invokes the real runtime operation with no
supplied gameplay child, callback or random draw. Three specification vectors
cover cold construction, previously populated state, and populated backing with
zero scan bounds. They compare these fields exactly:

- Process unit/city token generators and the two live scan bounds.
- Damage in **all 2048 unit slots**, shield storage in **all 256 city slots**,
  future technologies in all eight civilizations, the peace counter, all 512
  current/standing goal roles and all 21 leader city-name cursors.
- Saved resume-event state and all 256 password bytes; map allocation presence,
  fallback region, a shared text line and the mutable double-movement flag.
- Remembered ordinary game options and multiplayer timer, which must survive;
  validity of an incoming client, which must be revoked.

Byte arrays use exact lowercase hexadecimal; city shields retain signed16 values.
The used-state vector exercises full capacities, high bits, signed extrema,
non-ASCII text, and token counters at zero and `uint.MaxValue`. The empty-scan vector
proves that clearing only the scanned prefix would fail. Expectations come from
X01's reviewed contract, not recorded cleanroom outputs or original-game behavior.

These are explicitly **projection** cases. Three registered exclusion records
enumerate the remaining world, process and session fields. The loader rejects
altered exclusions or a claim of complete-boundary coverage. The comparator
checks every selected field and every selected backing slot. These vectors do
not establish full setup, whole-state equivalence, or independent fixture review.
The coverage ledger retains that limitation.

Adapter input contains entry values only. Expected state and trace never enter
the executable. Native and WASM execute the same portable session code. Build
identity now includes session/command/query dependencies and all owned state
declarations/constructors, so stale reset code cannot inherit a passing report.

## Wider integration checks

`tests/OpenCiv2.FoundationCases/FreshGameCases.cs` complements the projection by populating world
records, free-slot payloads, goals, stack/deletion guards, multiplayer values,
events, history, map/knowledge state, layouts, substitutions, and mutable rules.
It compares the fresh world against independently constructed initial records,
including every entity payload and all civilizations. It checks the full saved
preference records, retained RNG checkpoints, token generators, stable rule-object
address, and revoked old client versus newly generated client identities.

A required-interaction case verifies that preparation rejects without losing
already committed effects or the pending interaction. After cancellation it
still rejects while a required remote reply is queued. The reply's original
destination/correlation and cancelled result survive; replacement succeeds only
after responsibility for that reply transfers to the transport owner.

The existing F05 tests separately compare equal selected inputs after a prior
completed setup, cancelled setup and failed premade load. They exercise actual
content parsers and setup helpers with explicit W02/V01/balancing/acceleration
children. Those child outcomes do not establish implemented gameplay. Full
creation and graphical integration remain M3/M4 work under the reviewed plan.

## Verification

Current C# sources and tests are mapped in [implementation coverage](testing/coverage.md).
Run the [native/browser and CLI checks](testing/README.md);
[validation status](validation/README.md) records executed evidence and remaining gates.
