# C04: City happiness and disorder

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Target, ownership and inputs

This specifies classic Civilization II Multiplayer Gold Edition **5.4.0f,
patch 3** initial citizen moods, five normalization stages, luxury/building/
government/wonder effects, disorder and its duration flags, city tax collection,
celebration transitions and celebration growth. Appendix B records the
evidence and integration limits.

[F01](F01-determinism-and-arithmetic.md) supplies arithmetic and clamps.
[F02](F02-game-state-and-invariants.md#the-shared-city-workspace) supplies city
identity and the shared calculation workspace. [F03](F03-rules-and-configuration.md)
owns interpreted cosmic/rule values. [C02](C02-city-tiles-and-worker-assignment.md)
owns assignment, unassigned citizens and specialists. [C03](C03-city-economy-and-corruption.md)
owns the calculation parent, luxury/production/tax inputs and final city-record
publication. [U01](U01-unit-lifecycle-and-stacks.md) owns ordered stack access.
[F04](F04-turn-sequencing.md#inside-one-city-turn) owns city-turn ordering.

[C05](C05-city-growth-and-support.md) supplies support, aggressive/home-attack
unit counts and food requirements; [C06](C06-city-production-and-purchasing.md)
owns production changes; [C08](C08-buildings-and-wonders.md) and V03 own
facility-presence, active-wonder and advance queries.
[V02](V02-governments-and-tax-rates.md) owns government change and its downstream effects.
P04/P06/P10 own notices, city-window interaction, sound and music. Those child
operations are required contracts, not assumed-success placeholders.

C05 owns the shared growth-facility query, ordinary food-box growth, starvation
and support consequences. C04 retains celebration qualification and its size
increment; the two growth paths have different continuation rules.

Nominal inputs are a valid city and civilization, initialized rules and shared
workspace for the intended city, ordinary positive signed city size, a valid
unassigned-worker count, and finite backed unit stacks. Rule values, government,
player classifications, facilities and active wonders must be available.
The stored city happy/unhappy bytes are output snapshots, not substitutes for
the live shared counters used here.

Off-nominal accepted inputs preserve defined scalar wrap, signed size, unusual
rules, negative/shared values and significant query dependencies. A replacement may restrict
its accepted domain. Unsupported reads require supplied backing or a bounded
stop. [X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
excludes out-of-bounds writes and their damage, including logically emulated
adjacent-field corruption. Supplying memory backing does not extend a logical
array's valid range. Scalar size overflow is a separate, retained behavior.

## State and numerical contract

Use F01 reference expression results, with equivalent evaluation permitted. `div` truncates
toward zero, `rem` is the corresponding signed remainder, `asr` is arithmetic
right shift, and `clamp` means F01's ordered `clampAB` without swapping reversed
bounds. Population has C05's signed value; city count is an independently maintained
summary. Owners, governments and difficulty are logical identities/categories.
For admitted raw values, retain `s8` population/owner, `s16` city-count and
`u8` government/difficulty/cosmic-rule views before their consumers.

| State | Role |
| --- | --- |
| H, U, A | Shared signed32 happy, unhappy and excess-unhappy counters. A distinguishes excess unhappiness carried through the stages; it is not an extra population added to size. |
| unassigned | C02's shared count excluded from ordinary workers during normalization; this is not replaced by a recount of explicit specialist kinds. |
| moodHistory[0..4] | Happy, unhappy and excess-unhappy snapshots after each stage. Only these snapshots narrow modulo256. |
| luxury, tax, Food, Shields | C03's current shared totals; tax is not reread from the city record. |
| aggressiveUnits, homeAttackUnits | C05-produced counters; their names do not imply an extra recount by C04. |
| martialLaw | Shared count produced only by the low-government military stage; other branches can leave it unchanged. |
| shieldSupport, supportedSettlers, settlerFood | C05 inputs used by OnEdge and celebration growth. |
| foodSurplus | The turn's previously computed shared surplus, used by OnEdge. Celebration growth instead computes consumption from current size/support and compares current Food. |
| Disorder, DisorderSecond, DisorderLong, OnEdge, Celebrating | Independent city flags, changed only at the stated sites. |
| Revolution | Civilization flag surrounding a forced government change. |
| notifyEnabled, zoomRequested | Shared notice/window state produced by callers and notice children. |

**H, U and A are whole signed32 counters throughout calculation and return.**
They are not signed bytes with retained upper bits. Original instructions resolve
an erroneous byte interpretation in the primary decompilation; Appendix B gives
the decisive addresses. City-record mood bytes and stage-history bytes narrow
only at publication. Full and cached C03/F02 returns both subtract whole counters,
although cached dispatch can return a different city's stale workspace.

No C04 routine directly draws a random value. Children retain their own effects,
including callbacks, government/production changes and possible recalculation.
Only explicitly captured locals survive a child; later reads use live state.
These routines do not add a blanket city-identity revalidation after a callback.

## Normalization used after every mood stage

`normalizeMoods(city, stage)` uses current size s and shared H, U, A:

```text
H = clamp(H, 0, s)
while A != 0 and U < A:
    A = signed32(A - 1)
    U = signed32(U + 1)
U = clamp(U, 0, s)

while signed32(U + H) > clamp(signed32(s - unassigned), 0, 99):
    if A == 0:
        H = clamp(signed32(H - 1), 0, s)
    else:
        A = signed32(A - 1)
    U = clamp(signed32(U - 1), 0, s)

while A != 0 and signed32(U + H) < signed32(s - unassigned):
    A = signed32(A - 1)
    U = signed32(U + 1)

if stage >= 0:
    historyHappy[stage] = H modulo256
    historyUnhappy[stage] = U modulo256
    historyExcess[stage] = A modulo256
```

Read size/unassigned at their uses; the original does not freeze them for later
invocations. The first loop precedes U's clamp. The middle loop uses a worker
bound clamped to 0..99, whereas the last loop uses the **raw signed difference**.
Do not replace the loops with a common final clamp or assume A becomes zero.
Only the assignments shown clamp their operands. A is not independently limited
to population. H+U can finish below the worker bound because one middle-loop
iteration can reduce two counters.

Normal callers pass stages0..4. A negative stage suppresses history stores but
still performs all normalization. A nonnegative index outside0..4 would write
outside the intended history arrays: stop before the invalid store, preserving
the scalar work already performed, or reject it earlier under a declared input
restriction. Do not alter adjacent state to reproduce the original bug.

For off-nominal inputs, retain wrap and reversed-bound behavior. Do not invent a
fixed iteration cap as a successful equivalent result; an implementation that
restricts pathological inputs must state that domain. A negative unassigned
count can expose the difference between the clamped and raw worker bounds.

## Ordered calculation stages

The mood sequence is the tail of C03's trade/happiness operation, immediately
after ordinary trade distribution. Use the owner c captured by that parent
**before its first corruption call**; do not recapture city owner here. Fields
of captured c, current city records and shared totals remain live at later reads.
The sequence does not itself set Disorder, Celebrating or OnEdge.

### Stage 0: baseline and excess unhappiness

Set H=0. Test whether captured c belongs to the live human-player set.

For an AI civilization:

```text
U = signed32((signedCitySize - 1) - (unhappySizeRule - 5))
```

Difficulty, number of cities and government do not alter this AI baseline.
They can affect later stages and the children that produced its inputs.

For a human civilization:

1. Set f=`signed32(riotFactorRule - 2*difficulty)`. If the LargeMap game flag
   is present, add2; do not infer this flag from current map dimensions.
2. Set d=`signed32(((government >> 1) + 2) * f) div 2`. If d<2, replace it
   with1. This guarantees a nonzero divisor for the following calculation.
3. Set contentLimit=`signed32(unhappySizeRule - difficulty)`. If contentLimit<3,
   query Palace. When present and both aggressiveUnits and homeAttackUnits are
   zero, replace contentLimit with2. Preserve that order and short-circuiting.
4. Set U=`signed32((signedCitySize - 1) - (contentLimit - 2))`.
5. Unless captured c's current government is Communism, add:

```text
signed32((signedCivilizationCityCount - d) + (citySlot rem d)) div d
```

This uses the actual city slot, not its ordinal among this civilization's cities.
Do not replace truncation toward zero by floor division: a negative numerator
between -d and zero contributes zero, not -1. Communism skips this empire term,
not the preceding human baseline or Palace query.

Then set A=0. If signed city size<U, set A=`U - size` and U=size. Normalize stage0.
A whole-counter assignment initializes A; a large empire's excess does not wrap
to a byte before normalization.

### Stage 1: luxury

Set H=`asr(liveLuxury,1)`, replacing the old H, then normalize stage1. This uses
C03's post-specialist and post-building luxury total. Do not round odd luxury
upward or apply an additional per-specialist subtraction here; unassigned already
participates in normalization.

### Stage 2: buildings and advances

Perform these steps in order, without normalizing between them:

1. Query Colosseum. If present, subtract3 from U; then query Electronics and
   subtract1 more if known by captured c.
2. Query Monotheism. Only if known, query Cathedral; only if absent query
   Michelangelo. If either qualifies, query Communism and then Theology.
   Subtract `(Communism absent ? 1 : 0) + (Theology present ? 3 : 2)` from U.
3. Query Temple. If present, let b=1 if Mysticism is known, otherwise0; then
   query Ceremonial Burial and increment b if known. Query Oracle and double b
   if present. Subtract b from U.
4. Query Courthouse and only if absent Palace. If either exists and captured
   c's current government is Democracy, increment H by1.
5. Normalize stage2.

Advance/wonder queries use captured c; facility queries use the live city slot.
All subtotal arithmetic is signed32. These are separate queries from C03's
buildingContent/tithes calculation, with a different order. Neither its cached
subtotal nor its earlier facility results replace this sequence.

### Stage 3: government and military effects

Choose by captured c's live government:

- **Fundamentalism:** set A=0 and U=0. Do not reset martialLaw in this branch.
- **Below Republic:** set martialLaw=0; get the first unit at the city's live
  coordinate and traverse through U01's stack successor. For each unit whose
  rule attack value is nonzero, increment martialLaw; if captured c's government
  is Communism at that point, increment it again. There is no city-owner,
  domain, home-city, fortification or activity filter. A signed negative custom
  attack byte also counts because the predicate is nonzero. After the whole
  scan, cap martialLaw at6 under then-current Communism, otherwise3. Subtract
  `clamp(martialLaw,0,U)` from U. Do not stop scanning when the cap is reached.
- **Republic or higher, excluding Fundamentalism above:** query Women's Suffrage;
  only if absent query Police Station. Set multiplier m=0 if either qualifies,
  otherwise1. If current government is Democracy, increment m. If m is nonzero,
  set n=aggressiveUnits; when n is nonzero and current government is Republic,
  subtract1 from n. Add `signed32(m*n)` to U. Do not clamp n or insert a separate
  first-unit exemption for Democracy. This branch leaves martialLaw unchanged.

Then normalize stage3. Suffrage/Police therefore removes the ordinary Republic
penalty, but Democracy still has a one-per-aggressive-unit penalty. The exact
aggressive-unit membership belongs to C05's preceding support pass, not a new
C04 scan. The low-government martial-law scan is separate and does not replace
those support counters.

### Stage 4: wonders

1. Query Hanging Gardens for captured c. If present, increment H. Only then
   query its active city; if it equals the input city, add2 more.
2. Query Cure for Cancer for captured c; if present increment H.
3. Query Shakespeare's active city; if it equals the input city set U=0.
   This does not explicitly clear A.
4. Query Bach for captured c; if present subtract2 from U.
5. Normalize stage4.

These wonder calls are not replaced with a check of stored wonder locations;
[C08](C08-buildings-and-wonders.md#wonder-location-owner-and-obsolescence) owns effective scope/obsolescence. Normalization follows the whole group.
C03 then publishes its economy and low-byte mood snapshots and returns the
whole signed32 difference H−U. No additional mood or state-transition pass is
inserted before publication.

## City-turn disorder, tax and celebration operation

The turn handler captures the city's signed owner c at entry. If c is barbarian,
return immediately, without reading moods, sending notices, collecting tax or
changing any of these flags. Otherwise capture `margin=signed32(H-U)` from the
shared whole counters. Do not refresh the workspace or read the city's saved
happy/unhappy bytes instead.

[F04](F04-turn-sequencing.md#inside-one-city-turn) invokes this handler after
production, unsupported-unit removal, the second full city recalculation and
government-analysis adjustments. Research follows it, then eligible pollution
and upkeep. Production may therefore have used the preceding disorder state.
The handler itself makes no direct recalculation call, and does not reset the
workspace identity after growth or a flag change. Called children may recalculate
or change shared state; preserve their actual continuations.

### Negative entry margin: disorder and duration

If captured margin<0:

1. If localHuman==c, autoplay is off and disorder messages are enabled, play
   the disorder sound, then invoke the nonsilent disorder city-picture popup.
   The sound precedes the popup's own notification gate.
2. Test the city's **current** Disorder flag after the popup.
3. If Disorder is clear, set Disorder and OnEdge. If captured c is currently AI,
   call [C06's production setter](C06-city-production-and-purchasing.md#setting-a-production-order) with the special reconsideration request99.
   Redraw radius0 around the city's live coordinate for the current local human,
   requesting refresh. Do not set DisorderSecond or clear stale duration flags.
4. If Disorder was already set, first issue the same AI production request when
   captured c is currently AI. If DisorderSecond is now set, set DisorderLong;
   then set DisorderSecond. This branch has no direct map redraw.
5. Within that already-disordered branch, if captured c is currently human and
   its government is Democracy, check the scenario gate. Outside a scenario,
   or when the scenario's RevolutionForbidden rule is absent, invoke the Revolt
   city notice with the Democracy sprite. Then clear c's Revolution flag, call
   V02 government change with Anarchy, and set Revolution in the **then-current**
   flags. Retain captured c even if the notice changes city ownership.

RevolutionForbidden denotes the scenario gate identified in Appendix A.
A fresh first disorder does not collapse Democracy. An already-disordered qualifying call can collapse it regardless of
whether DisorderSecond was previously set. Suppressing the disorder message
does not suppress the revolt call or state changes. Human-player membership
and local-human equality are distinct tests.

There is **no tax collection** in this negative-entry-margin branch, even if
a popup changes H/U or clears Disorder. The captured branch decision remains.

### Nonnegative entry margin: OnEdge, restoration and tax

If captured margin>=0:

1. Set OnEdge exactly when current H==U, signed32(Shields−shieldSupport)>0,
   and the previously computed foodSurplus>=0. Otherwise clear OnEdge. Test in
   that order, without recomputing foodSurplus from current yields.
2. If Disorder is currently set, clear Disorder, DisorderSecond and DisorderLong
   together. If captured c is currently AI, issue production reconsideration99.
   Otherwise, when order-restored messages are enabled, invoke the Restored
   city-notice helper with force=false and no sprite. Then redraw radius0 around
   the live city coordinate for the current local human, requesting refresh.
3. If captured c's then-current government is not Anarchy, add **live workspace
   tax** to c's treasury with signed32 wrap. No nonnegative-tax clamp is applied.

Restoration and redraw precede the government and tax reads; their callbacks can
change those inputs. Tax collection occurs on every qualifying call, not merely
when order was restored. Duration flags are cleared only in the restoration
branch, not just because margin is nonnegative. Neither caller nor helper
retests human membership to decide tax eligibility: AI and human both collect.

### Celebration qualification and first qualifying call

After either margin branch, evaluate live state in this order:

```text
U == 0
and signedCitySize > 2
and asr(signedCitySize + 1, 1) <= H
and currentGovernmentOfCapturedOwner != Anarchy
```

The threshold is the ceiling of half the population for ordinary positive
sizes. It uses full population, including specialists, while normalization
excluded unassigned citizens from ordinary moods. Qualification uses live
workspace counters; it is not the captured margin or stored city mood bytes.
There is no separate Disorder-clear test in this qualification expression.

When qualification passes and Celebrating is currently clear:

1. If captured c is currently human, query its ruler title and copy that title
   into text argument1. This occurs even when celebration messages are disabled.
2. For that human branch, if celebration messages are enabled, play music track3
   with force=false only when notifyEnabled is nonzero; then invoke the
   nonsilent WeLoveKing city-picture popup, regardless of notifyEnabled.
   The popup owns its own early notification/autoplay gates.
3. Set Celebrating in the live city flags after the children.

There is **no growth on this first qualifying call**. AI cities set the same
flag without title, music or celebration popup calls. Autoplay is not an extra
C04 gate on title/music in this branch; the picture helper applies its own gate.

### Continuing celebration and growth

When qualification passes and Celebrating was already set, test the following
in order:

1. Captured c's live government must be greater than Fundamentalism: ordinary
   Republic or Democracy. Other governments retain celebration without this
   size increment.
2. Compute `consumption=signed32(signedCitySize * foodPerCitizen +
   supportedSettlers * settlerFood)` and require consumption<live Food.
   Equality is insufficient. Use this expression, not cached foodSurplus.
3. Call [C05's growth-facility query](C05-city-growth-and-support.md#shared-growth-facility-query) and require no missing facility.
4. Increment the city's **live** size by one with signed8 retention.

Do not add a second qualification check after the facility query, advance the
food box, rerun assignment, clear celebration or refresh totals in this branch.
Size127 becomes -128 in the original. OpenCiv2's selectable B16 cap is disabled
for this classic target. This is an in-bounds scalar increment, not an excluded
memory overwrite. A stricter input domain can prevent such states, but must not
claim equivalent overflow behavior for executions it accepts and changes.

### Ending celebration

When qualification fails and Celebrating is currently set:

1. If captured c is currently human, its government is not Anarchy and Disorder
   is currently clear, query the ruler title and copy it to text argument1.
2. In that branch, if celebration messages are enabled, invoke the
   WeDontLoveKing city notice with force=false and no sprite.
3. Clear Celebrating in the live flags after the children, regardless of notice
   eligibility or whether a child changed the flag.

No extra action occurs if qualification fails while Celebrating is already
clear. A no-food-growth or missing-facility result does **not** end celebration;
those tests occur only after celebration qualification has passed.

### Shared growth-facility query

Use [C05's shared growth-facility query](C05-city-growth-and-support.md#shared-growth-facility-query).
It returns the missing facility or None. Celebration continues to the size
increment only for None; it does not execute ordinary food-box refill or the
growth-halt notice path.

## Notification and callback boundaries

A C04 notice invocation does not guarantee a visible dialog:

| Child | C04 use and child boundary |
| --- | --- |
| Nonsilent city-picture popup | Disorder or WeLoveKing section, city argument, corresponding picture, silent=false. The child suppresses itself in autoplay or when notifyEnabled is zero; otherwise it selects assets, supplies city text, exposes zoom choices and can set zoomRequested. |
| Ordinary city notice | Restored or WeDontLoveKing, force=false, no sprite. The helper applies autoplay/notify gates and can set zoomRequested after a positive modal result. |
| Sprite notice wrapper | Revolt with Democracy sprite; calls the ordinary helper with force=false and that sprite. The government transition follows even if the notice is suppressed. |
| Ruler-title/text | Queries captured c's current title and writes argument1 before the celebration message test. Later UI children own other text arguments and resource lookup. |
| Production reconsideration99 | C06 dispatch for an AI city entering, remaining in, or leaving disorder; not an assignment of literal production item99. |
| Government change to Anarchy | Full V02 operation, surrounded by Revolution clear/set. It is not reduced to writing the government field. |

Picture/notice/modal, production, government and redraw children can alter
state. Capture only entry owner and margin at the stated sites; read membership,
government, flags, yields and coordinates at their subsequent uses. The turn
handler ignores notice return values and has no general rollback. C04 does not
own full drawing, allocation failures, transport, asset processing or modal
input loops; those remain explicit P04/P06/P10/N02 child integration obligations.

## Examples and conformance obligations

- Size6, human Monarchy, difficulty3, unhappy-size rule7, riot-factor rule14,
  one city and slot0 gives d12 and baseline U3. With24 cities it gives U4;
  the LargeMap flag changes d to15 and U back to3. Communism skips this empire
  term, whereas the AI baseline is U3 independent of these difficulty/counts.
- Normalize size6, unassigned2, H5/U4/A0: three middle-loop iterations yield
  H2/U1/A0. Replacing this with independent caps gives a different result.
- Normalize size3, H256/U−256/A0: H becomes3 and U0. Treating the inputs as bytes
  would incorrectly produce H0/U0. Only the stage snapshot narrows.
- With size4 and baseline excess4, stage0 can be H0/U4/A4. Luxury4 gives H2/U2/A2
  after stage1; excess unhappiness persists through later stages.
- Republic with three aggressive units and no Police/Suffrage adds2 unhappy.
  Democracy adds6; Police/Suffrage reduces it to3 rather than zero.
- New disorder sets Disorder and OnEdge. A second disordered call sets
  DisorderSecond; a third sets DisorderLong. Restoration clears all three
  disorder flags, but computes OnEdge independently.
- A size5 city with H3/U0 and a non-Anarchy government qualifies for celebration.
  It first sets Celebrating without growth. On a later qualifying Republic/
  Democracy call, food strictly above consumption and no missing facility grows
  it once. The food box and cached totals are not rebuilt here.

Conformance must distinguish humans from local-human UI routing, original whole
counters from byte snapshots, city slot from city ordinal, stage clamps from
final clamps, current inputs from captured margin/owner, and scalar wrap from
invalid-array writes. Include supplied-child traces, specialist limits,
negative/large counters, no-growth qualifications, scenario collapse gates,
restoration tax, stale flags and callbacks. Finite examples do not replace
whole-turn or independent executable comparisons.

## Appendix A. Original representations for validation

These locations and masks are evidence aids, not required implementation layout.

| Observation | Original representation |
| --- | --- |
| Shared counters | H `006a6550`, U `006a65a8`, A `006a659c`, each a whole signed32 value. |
| History | Five byte entries each at happy `006a6620`, unhappy `006a65f0`, excess `006a6628`. The normalizer stores only the low byte at the requested stage. |
| Size and city moods | Size signed byte at city+9; happy/unhappy output bytes at city+`0x52/0x53`. |
| City flags | Disorder `0x1`, Celebrating `0x2`, DisorderSecond `0x2000`, OnEdge `0x4000`, DisorderLong `0x100000`. |
| Civilization flags/treasury | Revolution mask `0x1` at civilization+0; signed32 treasury at+2. |
| Scenario/option gates | Scenario game bit `0x80`; scenario special-rule `0x10` prohibits revolution; disabled-message bits Disorder `0x10`, Restored `0x20`, Celebration `0x40`. LargeMap bit `0x4` is a game flag. |
| Growth query returns | None0, Aqueduct9, Sewer System23. |
| UI arguments | Disorder sound14; picture base Disorder `0x48`, WeLoveKing `0x4a`; both silent0/civ argument0; celebration music `(3,0)`; notice wrapper uses force0. |

The original normalizer lacks an upper bound on the history index. Only0..4
are supported logical stages; spills into neighboring state are excluded bugs.
Do not enlarge the history contract merely because a source declaration gives
padding or a neighboring region a larger array type.

## Appendix B. Sources, corrected evidence and checks

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

The primary reference files are pinned to OpenCiv2
`a71e8aefffe04e6f719e0e213b027b128a747711` and were checked unchanged at local
HEAD `fc2155ccdb2123c1137babab6fa39e38152278c9` for this scope.

| Evidence | Scope |
| --- | --- |
| [City routines](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp) | Mood clamp `004ea031`; trade/mood sequence `004ea8e4`; disorder/tax/celebration `004ef578`; growth query `00441a79`; ordinary notice `004eb571`; sprite wrapper `004f3f30`; city-turn caller `004f0a9c`. Mood-counter widths in this decompilation require the original-instruction correction below. |
| [Return-preserving parent](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/retvals.cpp), [workspace](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/city.h) | Shared state and cached/full dispatch; all mood returns use whole counters in the original. |
| [Window picture helper](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/window.cpp) | `popup_city_picture_zoom` `004eb80a` supplies child autoplay/notify/zoom gates. Full presentation is [P06](P06-city-interface.md). |
| [Government change](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp) | `civ_change_government` `0055c69d` is a full V02 child, not only a field store. |
| [B16 ledger](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md) | Selectable city-size cap differs from classic scalar growth. C04 preserves the original signed-byte increment. |

Independent source was inspected at `civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, especially `src/city_trade.cpp` and
`src/city_turn.cpp`. It uses whole mood counters and exposed the conflict with
the primary decompilation. The archive tag named by that repository's archive
index was unavailable in this checkout; its absent archived instruction files
were not treated as inspected evidence.

A read-only disassembly of the actual original executable resolved the conflict.
The inspected file was `Civ2/civ2.exe`, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Bounded instruction ranges were `004ea031..004eb326`, `004eb4a1..004eb570` and
`004ef578..004efbc5`. Decisive observations include:

| Original instruction sites | Finding |
| --- | --- |
| `004ea04d/004ea05b`, `004ea060..004ea084`, `004ea0a5/004ea0b3` | Clamp, compare, decrement and increment use entire mood DWORDs. |
| `004ea0de..004ea1ad` | Both normalization worker-limit loops use DWORD mood arithmetic; the last limit is not clamped. |
| `004eabf4`, `004eacf5`, `004ead47`, `004eadee` | Baseline H/U and luxury H replace/update whole counters. |
| `004ea1c2..004ea1e6`, `004eb2e4..004eb30b` | Stage-history and final city mood stores copy low bytes; they do not make preceding arithmetic byte-sized. |
| `004eb312/004eb317`, `004eb51f/004eb524` | Full trade/mood and cached-entry returns subtract whole H/U counters. |
| `004ef5a6/004ef5ab`, `004ef963`, `004efaf4` | Turn margin and treasury use DWORD arithmetic; celebration growth increments the actual size BYTE without a127 cap. |

C03's former byte-counter/retained-upper-bits interpretation is corrected to
these original instructions. That was a reconstruction error, not an original
quirk or a new cleanroom behavior choice. F02's cached/full explanation is
updated accordingly. The primary reference checkout remains untouched.

A temporary external harness reused C03's 137 extracted routines, corrected the
two mood bodies' counter accesses to the verified original widths, and added
three actual bodies: disorder/tax/celebration, growth-facility query and sprite
notice wrapper. **142 C04 assertions passed** in optimized and Address/Undefined
Behavior Sanitizer builds. The **140 C03 regression assertions also passed**
in both builds after replacing the erroneous byte-retention expectations.

The C04 checks cover whole-counter normalization, history, negative stage and
unassigned inputs, baseline human/AI/slot/empire cases, Palace fallback, luxury,
building and wonder effects, mixed-stack martial law, Republic/Democracy
military penalties, disorder aging/restoration, scenario collapse, live versus
captured callback state, tax wrap, celebration start/end/growth, growth-facility
ordering and signed size127→−128. Actual C03 trade/distribution and U01 stack
bodies participate; the luxury input for dedicated mood cases is supplied at a
declared distribution-child boundary. This is a finite source harness, not an
original-executable runtime comparison.

Remaining adapters include C05 support production, C07 commodity refresh,
C08/V03 facility/advance/wonder providers, C06 production reconsideration, V02
government transition, and UI/audio/network/map environment. Callback tests
establish the parent continuation under supplied mutations; they do not prove
that an actual UI child makes each tested mutation. No invalid history write
was required or executed. The statically inspected original executable was not
run; no saved analysis annotations, source or assets in either reference
repository were changed. No original code or harness was imported into this
cleanroom repository.

[C05](C05-city-growth-and-support.md#appendix-b--validation-and-remaining-joins)
owns the shared growth query and adds finite actual food/support/turn-parent
joins, including rerunning this document's cases.
[C06](C06-city-production-and-purchasing.md#appendix-b--sources-and-validation)
adds actual production/setter bodies with a declared A02 choice adapter. Real
A02 choices and C08/V03 effects remain necessary.
[V02](V02-governments-and-tax-rates.md#appendix-b--validation-and-remaining-integration)
specifies government transitions and adds finite actual city-recalculation
joins. The complete repeated-disorder-to-government/dialog continuation still
requires joined validation with its real external children.
[C07](C07-trade-and-commodities.md#appendix-b--evidence-and-validation) adds actual
commodity refresh to finite parent cases with explicit incoming road values.
F04 needs
whole consecutive city/civilization turns, and P04/P06/P10/N02 need real notice,
zoom, audio, callback and receipt behavior. X02 must retain reproducible
full-state and child-trace cases, including scalar wrap and safe excluded-write
boundaries. These finite checks do not establish complete original-program or whole-game
conformance.
