# Built-in gameplay JSON components

Status: unreleased version 1 data formats. Complete first-wave classic defaults,
JSON Schemas, provenance checks and an offline developer loader are implemented.
C++ gameplay consumers, runtime profile selection, full rule-reference resolution
and multiplayer/save integration remain implementation-plan work. This does not
provide the missing text-backed stock rules or a playable pack.

The six components implement the initial scope of the
[extraction assessment](built-in-gameplay-data.md). They supply operands to the
existing algorithms; they contain no formulas, scripts, dispatch programs or
replacement AI/map-generation policy. The default target remains classic MGE
5.4.0f, patch 3. Changing a built-in value requires explicit custom mode.

## 1. Files, selection and common rules

The shipped bundle is [data/gameplay/classic-mge-5.4.0f](../../data/gameplay/classic-mge-5.4.0f/README.md):

| Component | Format | Default file | Schema |
| --- | --- | --- | --- |
| Bundle manifest | `openciv2-gameplay/1` | [gameplay.json](../../data/gameplay/classic-mge-5.4.0f/gameplay.json) | [gameplay.schema.json](schemas/gameplay.schema.json) |
| Calendar | `openciv2-calendar/1` | [calendar.json](../../data/gameplay/classic-mge-5.4.0f/calendar.json) | [calendar.schema.json](schemas/calendar.schema.json) |
| Spaceship | `openciv2-spaceship/1` | [spaceship.json](../../data/gameplay/classic-mge-5.4.0f/spaceship.json) | [spaceship.schema.json](schemas/spaceship.schema.json) |
| Governments | `openciv2-governments/1` | [governments.json](../../data/gameplay/classic-mge-5.4.0f/governments.json) | [governments.schema.json](schemas/governments.schema.json) |
| Difficulty | `openciv2-difficulty/1` | [difficulty.json](../../data/gameplay/classic-mge-5.4.0f/difficulty.json) | [difficulty.schema.json](schemas/difficulty.schema.json) |
| Combat | `openciv2-combat/1` | [combat.json](../../data/gameplay/classic-mge-5.4.0f/combat.json) | [combat.schema.json](schemas/combat.schema.json) |
| Scoring | `openciv2-scoring/1` | [scoring.json](../../data/gameplay/classic-mge-5.4.0f/scoring.json) | [scoring.schema.json](schemas/scoring.schema.json) |

The manifest requires `format`, `id`, positive `revision`, `target` equal to
`classic-mge-5.4.0f`, `mode` equal to `classic` or `custom`, `components` and
`provenance`. Provenance is `{authors,license,notes}` as in content packs.
Components contains exactly the six named descriptors `{path,sha256}`. Each path
is relative to the gameplay manifest's directory, obeying the
[content path rules](data-format.md#3-pack-manifest). Validate file hashes against
the exact bytes decoded. There is no network retrieval or nearby-file discovery.

Each component is a complete document. No field-level merge, hidden fallback or
separate component inheritance occurs in this version. To customize one value,
copy the bundle, edit the value, update its descriptor hash, change mode to
`custom`, and assign a distinct ID/revision. The structural schemas deliberately
allow custom values; baseline equality is a separate semantic check.

A root content manifest now requires `gameplay`, selecting either:

```json
{"builtin":"classic-mge-5.4.0f","revision":1}
```

or `{path,sha256}` for an explicit gameplay manifest within that content root.
An overlay inherits `gameplay` when omitted, otherwise replaces the whole
selection. A classic-text session likewise selects the shipped bundle by default
or an explicit custom bundle; these values have no classic TXT adapter.
Content's `profile` and gameplay's `target` name the same classic algorithm and
identity family. `mode` separately declares whether the supplied operands claim
the classic baseline. Custom mode is an explicit extension outside X01's required
classic-equivalence target, not a new promise about unmodified original peers.

Common JSON admission, exact numbers, unknown-field rejection and metadata-only
root `extensions` follow [the content conventions](data-format.md#2-common-json-conventions).
Gameplay integer operands use the signed 32-bit authoring domain unless narrowed
below. This bounds input values, not implementation storage. Positive denominators
and other bounds are enforced by the schemas. Retain F01's significant arithmetic
at the consuming operation; validation does not authorize removing scalar wrap.

All keyed collections use semantic identities, not localized names or implicit
array slots. Government/difficulty keys bind to the fixed identities listed in
V02/F03; spaceship part/group keys bind to V07. Rule display names and custom
native IDs still resolve through the selected identity profile. Only calendar
period arrays carry execution-relevant order.

## 2. Calendar

`profiles` has four required keys: `chieftain-warlord`, `prince`, `king`,
`emperor-deity`. These name F04's profile results 0, 1, 2, 3 respectively; they do
not bypass its difficulty/map-size adjustment. Each contains `periods`, exactly
six `{startYear,turnCount,yearStep}` records. Counts and steps are positive.
Adjacent periods must be continuous: next start equals previous start plus
previous count times step. Endpoints must fit the authoring integer domain.

[F04 §5](../algorithms/F04-turn-sequencing.md#5-turn-to-calendar-conversion) uses
these fields at its existing period reads. Preserve turns 0/1, the strict `>`
period boundary, ordinary year-zero substitution, post-final-period progression,
scenario override and first-launch supplement. A different year step does not
change those algorithms or the separate calendar display convention.

`retirement` contains `warningYear`, `endYear`, and nonnegative
`scenarioWarningLeadTurns`. Native warning year must precede end year.
[V08 §5](../algorithms/V08-scoring-victory-and-retirement.md#5-outcome-producers-and-scheduled-retirement)
uses the two years for the ordinary equality tests and the lead in
`turn == scenarioLimit - lead`. V08 §6 must also use **the same endYear** for its
post-report Retirement continuation gate, including scenario outcomes. Do not
change equality into a reached-year test or rewrite scenario turn limits.
Unrelated authored briefing text is not automatically regenerated from these values.

## 3. Spaceship

`parts` has `structural`, `propulsion`, `fuel`, `habitation`, `life-support` and
`solar-energy`. Each record supplies fixed `group`, `maximumCount` in 0…32767,
and positive `massTons` divisible by 100. The maximum bound ensures a nominal
configured count is representable in the classic signed saved-count field.
It does not clamp incoming stored counts, including negative/off-nominal ones.
Group membership is fixed by the schema: one Structural, two Component and three
Module kinds. Adding kinds or regrouping them is outside this format.

[V07](../algorithms/V07-spaceship.md) uses maximumCount consistently in partLimit,
groupMax/groupFull, partMaxed, and addPart's group/subtype maximum checks. Preserve
the differences between sums and per-kind maximum predicates. Structural-support
formulas, AI's supported-module cap, empty-group exception and assembly selection
remain unchanged. Do not impose a second clamp on stored counts or omit raw mass.

Normalize massTons by exact division by 100 before V07's mass/performance arithmetic.
Use that result for both stored-part mass and the missing-kind supplement.
`peoplePerHabitation` is the positive number of people represented by each effective
habitation unit in population descriptions. Stored ship population remains an
effective habitation count; V08's score still uses that count and success, not a
new people total. This field does not change a classic state field's unit.

Production costs, prerequisites and the mass/thrust paradigm remain in `rules.json`.
Thrust progression, support coefficients, Fusion arithmetic and flight penalties
are not newly configurable in this first-wave format.

## 4. Governments and difficulty

`governments` contains seven records keyed `anarchy`, `despotism`, `monarchy`,
`communism`, `fundamentalism`, `republic`, `democracy`. Each supplies:

- `maximumRateTenths`, 0…10, for V02's individual rate cap. This does not change
  the ten-tenths total or Fundamentalism's separate text-backed science cap.
- `requiredAdvance`, null for Anarchy/Despotism and a native advance ID for the
  other five. The shipped canonical IDs `advance.monarchy`, `advance.communism`,
  `advance.fundamentalism`, `advance.republic`, `advance.democracy` mean those
  fixed classic advances. A full content resolver must map their identities
  even when rules use custom IDs or localized names. Custom references require
  equivalent explicit rule/profile resolution; the bundle checker cannot resolve
  arbitrary references without the selected rules.
- Positive `corruptionDivisor`, read by C03 for the **effective** government after
  its celebration increment. This affects both actual losses and candidate
  analysis calculations at their existing reads. It does not change immunity,
  distance caps, palace/courthouse effects or captured Communism identity.

V02 retains the Statue query, the advance query even when the wonder is present,
and Fundamentalism's enabled-advance gate. Changing its requiredAdvance changes
both that query and its enabled check. Anarchy/Despotism still perform no advance
query. V02's announced-change tax assignment is outside this component.

`difficulties` contains six records keyed `chieftain`, `warlord`, `prince`, `king`,
`emperor`, `deity`. Each has positive `aiBoxFactor`, `humanResearchMultiplier` and
`aiResearchMultiplier`. C05 reads aiBoxFactor as its starting rows for **both** food
and shields; raw human box factors remain text-backed. Preserve the food `>200`
versus shield `>=200` late gate, raw wonder/knowledge tests, subtraction, scaling
and odd-row adjustment. V03 selects the applicable initial research multiplier
before leader comparisons, turn penalty, paradigm scaling, extra, map modifiers,
human floor and the final cost check. It still reads the original difficulty at
those later gates. Other difficulty effects and AI decisions are unchanged.

For nominal identities these fields replace only the stated initial constants.
Classic mode retains the original formulas/branches for admitted off-nominal
selectors: V02 government ≥6 returns the classic Democracy rate; C03's helper
retains its ≤0 and ≥5 branches, including an effective value of 7; V03 clamps its
initial difficulty to 0…4; C05 and V08 retain their stated arithmetic formulas.
These are compatibility fallbacks, not extra configurable identities.

Custom sessions admit ordinary government/difficulty identities only at state
input boundaries. C03's internally derived effective government 7 remains valid
and uses the configured Democracy divisor; no new government is created. Reject
unsupported off-nominal custom state explicitly before installation rather than
inventing a policy entry. The classic path's separately admitted domain remains
unchanged; runtime admission is still implementation work.

## 5. Combat

`strengthBonuses` supplies rational `{numerator,denominator}` values for `veteran`
and `paradrop`. Reduce fractions before use. At each existing U06 bonus operation:

```text
bonus = floorDivide(s32(currentStrength * numerator), denominator)
currentStrength = s32(currentStrength + bonus)
```

The denominator is positive and floorDivide rounds toward negative infinity.
At the default 1/2 this is exactly the specified arithmetic shift, including
negative values. Apply veteran to both attack/defense at its existing locations;
paradrop follows attack's veteran operation and uses the updated value. Do not
combine the two into one multiplier.

`defenseMultipliers` contains `fortification`, `fortress`, `cityWalls`. Each is a
positive rational whose doubled value is an integral authoring-domain value.
Convert to that doubled factor **exactly**, then feed U06's existing factor
replacement and final `s32(B*factor) >> 1` operation, including its factor==2
shortcut. Other factor consumers see the resulting factor too. Fighter
interception, non-Land reset, terrain/river normalization and applicability gates
remain unchanged; these bonuses do not automatically stack.

`antiAirMultipliers` has positive integers `ordinaryAircraft` and
`destroyedAfterAttack`. `partisansAgainstZeroAttackMultiplier` is another positive
integer. They replace the multipliers at U06's named preparation rows, retaining
all identity/domain predicates and significant arithmetic. No combat round law,
firepower, damage handling, promotion rule or AI preview formula is replaced.

## 6. Scoring

All fields bind to [V08 §§2–3](../algorithms/V08-scoring-victory-and-retirement.md#2-city-objectives-and-score-components).
Apply F01's retention after each significant product/sum and preserve the
specified order of queries, scratch publication and display-triggered effects.

| Field | Consumer binding |
| --- | --- |
| `citizens.perCitizen`, `perHappyCitizen`, `perUnhappyCitizen` | Multiply the three retained signed city counts and sum in size/happy/unhappy order. Defaults 1/1/−1 reproduce size + happy − unhappy. No separate specialist/angry term. |
| `wonderPoints` | Add once for each raw qualifying wonder location; do not replace V08's ownership test with active-wonder eligibility. |
| `spaceshipPointsPerPopulationSuccess` | Multiply V08's population-count × success result by this coefficient. Retain the pending/arrived split and Won gate. |
| `pollutionPointsPerTile` | Multiply pollutedTiles − warmingPollution, preserving a negative difference. |
| `peace` | Admit when turn ≥ firstEligibleTurn; clamp peaceTurns × pointsPerTurn to 0…maximumPoints. Default threshold 200 is equivalent to the original >199. |
| `futureTechnologyPoints` | Multiply unsigned future-tech count. |
| `barbarianActivity` | activity × pointsPerLevel + offsetPoints. |
| `conquest` | With NoRestartEliminated, use noRestartMinimumTurn when turn ≤ that value. Then pointsPerEnemy × numEnemies + pointsPerRemainingTurn × (turnReference − effectiveTurn) + basePoints. Apply largeMapMultiplier and then smallMapMultiplier at their separate existing flag gates, truncating each divided product toward zero. |
| `objectiveValue.city`, `.wonder` | Starting value for Objective city flag, and increment for each raw located wonder when WondersCount is set. This shared helper also serves scenario/editor/AI callers without changing their selection logic. |
| `scenario.outcomePoints` | Named decisive-victory/marginal-victory/draw/marginal-defeat/decisive-defeat bases, selected after the existing perspective reversal. Scenario-owned thresholds and test order remain unchanged. |
| `scenario.pointsPerObjective`, `.conquestMultiplier` | Add points per objective, then multiply only when the previously computed conquest bonus is positive. Preserve the later −1 sentinel and total publication. |
| `rating.difficultyFactors` | Initial rating coefficient for each named difficulty. Multiply max(shared total, shared conquestBonus), then divide by the unchanged percentage scale 100. Do not recalculate score here. |

Score coefficients can be signed; objective-value weights and scenario conquest
multiplier are nonnegative. Peace cap is nonnegative. Map multipliers are exact
nonnegative rationals. Fractions reduce before reference products are evaluated;
5/4 and 10/8 therefore have the same operands and fingerprint. Title thresholds,
Hall of Fame encoding, total zero floor, objective sentinels and victory-state
transitions remain algorithms/contracts, not new numeric settings.

## 7. Provenance, fingerprints and validation

[gameplay-defaults.json](gameplay-defaults.json) is a repository ledger with its
own [schema](schemas/gameplay-defaults.schema.json). It pins the shipped manifest,
a semantic SHA-256, eight source specification hashes and 59 field/subtree
bindings to exact sections. Every gameplay leaf must have a binding. Source or
data changes require reviewing values and updating the relevant pins together.
The ledger is trusted repository metadata; user manifests cannot replace it to
self-certify classic values. It is not an original execution capture.

The versioned fingerprint serializes this envelope as compact ASCII JSON with
lexically sorted object keys, no insignificant whitespace and decimal integer
values: `{format:"openciv2-gameplay-values/1",target,components}`. Include each
component's `format`, remove its root `extensions`, and reduce all rational
numerator/denominator pairs by their greatest common divisor (zero becomes 0/1).
Preserve array order. Manifest ID/revision/mode, descriptor paths/hashes and
provenance are outside this effective-value digest. Hash the resulting bytes
with SHA-256. The developer loader returns the same normalized operands it hashes;
a future runtime must use them, not unnormalized fractions from the original file.

Byte hashes still independently protect every selected file. Whitespace, object
member order, equivalent integer tokens, metadata and equal rational spellings
can have different byte hashes while retaining one gameplay fingerprint.
Classic mode **requires** the trusted baseline fingerprint. Custom mode may
contain the same values, but any changed first-wave operand fails the
`--require-classic` gate. This gate proves only baseline equality for this data
subset, not full game/content/original-peer compatibility.

From the repository root, after installing the
[schema developer dependencies](schemas/README.md#local-validation):

```sh
python tools/check_content_schemas.py
python tools/check_gameplay.py
python tools/check_gameplay.py --content-pack docs/content/examples/content.json
python tools/check_gameplay.py --manifest path/to/gameplay.json --require-classic
python -m unittest discover -s tests -p 'test_gameplay_data.py' -v
```

The checker validates schemas, exact input bytes/hashes, path/symlink containment,
component kinds, period continuity, rational admission, source pins and complete
provenance coverage. It resolves a complete gameplay bundle offline. The content-
pack option validates **only its gameplay selection** and requires a root pack;
full content inheritance, rule references and text completeness remain open.
Thus the deliberately incomplete content example passes this limited check.

Tests include independently specified numeric vectors and isolated custom changes,
classic-value rejection, malformed data, hash/source drift and canonicalization.
Their arithmetic probes are transcription checks, not engine implementations or
original captures. CI runs these beside the existing content checks.

## 8. Runtime and compatibility work still required

A future session must resolve one immutable gameplay bundle for both native and
classic text paths and bind each field to the consumers above. Other built-in
rules remain fixed by their specifications; these six files are not an exhaustive
externalization of the executable's data. Keep AI heuristics and map generation
unchanged. There is no original TXT export for the new fields.

Default values retain the original interoperability target. Changed runtime values
create a custom variant that an unmodified original peer generally cannot apply.
Classic saves/scenarios cannot carry this bundle or its digest in reserved bytes;
writing valid file structure does not preserve custom continuation semantics.
Original-peer admission must require baseline built-in values plus all other
existing content/protocol checks. The host cannot impose custom values on original
clients that execute local mechanics.

New-engine checkpoints must bind the resolved digest, and custom multiplayer must
verify matching values through its separately designed session mechanism, including
resume and host transfer. Missing custom metadata cannot imply baseline silently.
Those runtime gates, actual consumer execution, bidirectional original exchanges
and replay tests remain open; the developer tool does not implement them.
