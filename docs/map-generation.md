# Map generation

`Core/MapGenerator.Generate` implements W02's new-map pipeline and retained-map
finalization from the repository specifications. `Session/WorldGeneration`
pins the world and supplies installed rules. F05 `WorldEntry` runs this child
when `WorldEntryServices.GenerateWorld` is null and `Generation` supplies
`WorldGenerationOptions`. Existing explicit generation providers remain usable.
Civilization creation, start placement, balancing and acceleration remain separate
F05/V01 work; the application still opens its authored showcase world.

The caller supplies already-converted game ticks, a gameplay random context and
a unique call identity. Choices use `W02.<phase>.<decision>` sites and a per-site
occurrence ordinal in `ChoiceKey.Attempt`. World entry appends `/generation` to
its call identity. Neither generation nor its queries read a clock or reseed the
context. New flat worlds double effective temperature and climate in setup state;
retained worlds do not. New-map admission accepts F05 presets and its custom-size
domain, including odd heights and both 10,000-tile extremes.

Land coverage/marking and river backups are invocation-local scratch. Allocation
clears new spatial and remembered layers; starts are reset only for new maps.
Rainfall alternates forward and reverse rows in place. Weathering/voting retain
paired selections, running votes and terrain tie order. River retries retain
consumed choices, restore rejected terrain and consult the attempt-start backup
for continuation. There are no invented retry/step caps. The original discarded
forest-neighbor river samples are omitted under F01/W02.

`MapRegions` implements W01's water-first labeling, cylindrical row carry,
small-water aggregation, provisional ID reuse, high-ID remapping, overflow counts
and skipped-direction border scan. Fertility consumes decoded signed rule yields,
first-special scores for either resource variant, and mining-before-irrigation
precedence. Final polar edits and resource suppression deliberately do not refresh
the preceding regions, connectivity or fertility.

`GenerationConnectivity` implements U03's uniform-cost, absent-owner bounded search
and land/sea graph builder. It preserves fixed Warriors/Caravel slot domains,
Coast-bound penalties, the 2,304-removal FIFO limit, unwrapped selection window,
strict result threshold and zero-valued successful cache hits. It is not a general
unit pathfinder: occupancy/peace/AI penalties, ordinary movement costs, goto and
coarse waypoint search remain U03 work. A supplied `GenerationPathWorkspace`
retains its flood between queries and retained-map invocations. New-map generation
resets it. It is not a saved-game/checkpoint workspace or an emulator of private
original memory. With stable inputs there are no synthetic message-pump callbacks.

For retained classic maps, pass the allocation-bound adapter returned by
`MapCompatibility.GenerationMarkers`. Opaque terrain bytes stay in Formats:
the core only asks whether additional markers prevent a plain-ocean match and
clears them on whole-terrain replacement. Radius/visibility/memory/seed/start hints
survive retained finalization. Features and ownership are cleared; unit and city
records are not deleted. Finalization copies the first logical tile and its
adapter metadata to the fallback.

Failures retain completed edits and consumed choices. Callers must not admit a
failed world as a completed game. F05's fresh-game boundary isolates a subsequent
attempt. The routine does not implement UI cancellation or concurrent mutation.

## Evidence boundary

The fifteen shared native/browser groups in `MapGenerationCases` and
`MapGenerationIntegrationCases` include hand-derived phase expectations, 52 full
generation matrix runs, controlled replay, retained metadata and real F05 parent
integration with fresh/reused runtimes. Complete replay comparisons are regression
checks, not independent compatibility goldens. The W02 original-target
reconciliation gap in X02 remains open: original-game phase/full-map observations
under aligned semantic choices have not been supplied by this implementation.
No reconstruction sources or historical source-derived harnesses are inputs.

Implementation-time validation: the Release root build and native suite pass (502 passed,
two platform/device skips); formatting verification and project/coverage checks
pass. Browser execution was blocked by `NU1403` for the pinned
`Microsoft.NET.ILLink.Tasks` 11.0.0 package, including a locked restore into an
empty temporary cache. That run's conformance inventory separately reported a P02
specification hash mismatch. No package pins or unrelated
specification hashes were changed to bypass these failures.

The later [M2 closeout review](m2-closeout.md) verifies the combined checkout's
native tests and passing conformance inventory; the earlier P02 mismatch is not
an outstanding prerequisite for M3. During the M3 handoff check, a locked restore
against the existing verified package cache and browser publication succeeded
without changing pins. The in-flight browser test was stopped when the user
deferred browser work. This does not establish a passing W02 WASM run. The next
[M3 implementation pass](implementation-plan.md#current-handoff-linux-playable-slice)
validates local Linux and retains browser execution as deferred work.
The Linux handoff probe passes all 471 shared foundation groups, including the
generation groups, and verifies 311 compiled inputs; conformance metadata also
passes. Receipts are under `build/m3-handoff-review/`. This supplements the earlier
native suite; it does not claim another desktop UI or cross-platform run.
