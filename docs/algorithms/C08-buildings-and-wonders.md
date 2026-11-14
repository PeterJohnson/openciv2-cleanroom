# C08 — Buildings and wonders

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in C08 |
| --- | --- |
| Gameplay | Possession/wonder ownership/expiry, upkeep and forced loss, acquisition/upgrade effects and their real data dependencies. |
| Original files/content | F03 building/wonder/unit definitions and F06 possession/location/treasury fields. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Bitset storage choice, upgrade notice buffers, artificial notice mutations and native redraw order. |

The algorithms below specify game values/decisions and their real dependencies,
plus explicitly identified external data definitions. They do not prescribe
function boundaries, object/global layout, allocation strategy or incidental
helper/store order. Preserve retained data where an admitted game/file/peer
consumer uses it; represent that data flow explicitly rather than emulating
original memory. Native or synthetic callbacks do not create new game boundaries.
Historical backing-byte/fault/trace cases are reference unless an actual external
format or supported semantic operation consumes the value. Missing gameplay or
format meaning is an owner gap, not permission to invent it or require arbitrary
original bytes. This scope controls stronger historical wording below.

**Required profile:** [X01 fresh-game isolation and bug repairs](X01-classic-compatibility-contract.md#fresh-game-isolation-and-supported-bug-repairs)
and [behaviorally significant ordering](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
override conflicting procedures and examples below. Retention applies within a
game or to explicit imported inputs, not across new games. Confirmed original-bug
stack/backing/stop cases are optional diagnostics; use documented safe repairs.
Only real effects and callbacks impose ordering; test adapters cannot invent them.
Unspecified safe continuations remain explicit gaps, not required arbitrary bytes.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
semantic publications and failure effects. Only dependencies within the binding
scope above constrain evaluation; storage/API choices need no original-trace
equivalence proof. External file/wire definitions remain binding at adapters.

## Target and ownership

The target is **Civilization II Multiplayer Gold Edition 5.4.0f, patch 3**.
The evidence and validation limits are stated below.
This document owns ordinary-building presence/mutation, active wonder queries,
obsolescence, building maintenance and forced losses, Leonardo's upgrades and
Eiffel's acquisition operation. The effect catalogs identify each effect's
formula owner; they are not a second set of formulas.

[C06](C06-city-production-and-purchasing.md) owns eligibility, build categories,
production completion, acquisition authority, wonder uniqueness/races, manual
sale and buying. An effect is not automatically a facility: an empire-wide
wonder can satisfy one consumer without setting any building-presence entry.
[C01](C01-city-lifecycle-and-ownership.md) owns city allocation, elementary
ownership changes and deletion. [C09](C09-city-capture-and-revolt.md) owns
capture/incitement losses and ordering; S03 owns editor changes. These callers must explicitly perform their own side
effects. Calling the elementary setter does not perform acquisition.

[F01](F01-determinism-and-arithmetic.md) supplies arithmetic and masked shifts;
[F02](F02-game-state-and-invariants.md) supplies identity, state and workspaces;
[F03](F03-rules-and-configuration.md) supplies the parsed rules. There are no
random draws in the operations owned here. A child operation can have effects
specified by its owner. Preserve callback order and subsequent live reads.

## Inputs, state and domains

Nominal input has valid city/civilization/wonder identities, initialized rule
records, ordinary unit types and valid network recipients. The civilization's
building-maintenance input is the city's captured owner; the shield-box factor
is the **incoming shared calculation value**, not a factor recomputed for that
city by the payer. [C05](C05-city-growth-and-support.md#box-factors-and-settler-food)
owns calculation of that value.

Logical state required here:

| State | Meaning |
| --- | --- |
| City's installed buildings | Independently stored presence of each of the 34 ordinary buildings. |
| Wonder's recorded location | Unbuilt, destroyed, or a city slot. Distinct from its queried active location. |
| Wonder's expiry advance | A signed rule reference; negative means never expires in this query. |
| Civilization | Government, gold, reputation, Eiffel-applied latch, knowledge, directed treaties and human-control membership. |
| Building rules | Name, unsigned cost in shield rows, unsigned base maintenance. |
| Advance rules | Enabled indicator and signed first prerequisite; knowledge is a separate input. |
| Unit rules | Prerequisite, obsolete-by advance, role, defense, transport capacity and name. |
| Unit slots | Live token, owner, type, Veteran status, coordinates and retained other fields; current upper bound. |
| Presentation/network | Local civilization, mode, autoplay, screen-size value, player-slot mapping, text arguments and callbacks. |

Evaluate gold updates using F01's specified cyclic results. Rule costs/upkeep
and reputation are nonnegative values 0…255. A Barracks subtotal can reach 257;
do not reduce it modulo 256. Leonardo's defense/capacity comparisons use signed
rule views, so high rule codes can compare as negative. Owners and expiry
references have their logical identities/sentinels; admitted raw values retain
the stated signed views. Wonder locations identify city slots or their named
sentinels. Original storage annotations are in
[Appendix C](#appendix-c-original-numeric-annotations). None prescribes a native
field type or additional clamp.

A stricter implementation may reject off-nominal inputs at a declared boundary.
For compatibility within the specified domain, retain the ordered scalar
wraparound, negative sentinels and live-state behavior below. A stale city slot
with available record contents is not automatically an active city: the queries
below deliberately omit a live-token check. Unavailable read inputs require
supplied evidence or a bounded stop after preceding effects. Invalid writes,
including notification-array or record overflows, are **excluded bugs** under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs).
Mapped adjacent memory does not make those writes required behavior.

### Advance knowledge provider

[V03 owns the shared knowledge query](V03-research-and-technology.md#2-shared-knowledge-and-availability),
including negative prerequisite sentinels, Future Technology, civilization0,
and supplied-read boundaries. Use that query for the Barracks prerequisite walk
and other knowledge tests here. It does not override the earlier negative-expiry
or negative-obsolete-by gates below.

## Ordinary building storage

`hasBuilding(city, building)` returns false for anything outside the 34 ordinary
identities, without reading the city. Otherwise it returns that stored presence.
`setBuilding(city, building, on)` ignores the same out-of-domain identities;
otherwise it sets that presence when `on` is nonzero and clears it when zero.
Negative `on` also sets it. Neither operation tests whether the city is live.

The setter does not check prerequisite knowledge, government, coastline,
uniqueness, redundant wonders, existing plants or Palace location. It does not
change gold, shields, SoldThisTurn, production choice or cached calculations;
it does not show a notice or send synchronization. The caller owns those steps.

Spaceship parts and Capitalization have production identities but no installed
ordinary-building presence. Wonders have separate locations. A city with an
active Pyramids effect but no Granary still answers false for `hasBuilding(Granary)`.
C05's food operation separately combines those two conditions. The same principle
applies to Sun Tzu, Michelangelo, Women's Suffrage, Hoover and SETI consumers.

## Wonder location, owner and obsolescence

Use four distinct operations. Do not replace raw-location tests with active
ownership tests, or cache a result across state changes.

1. `expired(wonder)`: capture its signed expiry-advance reference. If negative,
   return false without querying knowledge. Otherwise return whether **any** of
   civilizations 1 through 7 knows that captured reference, using V03's query.
   There is no human/alive filter and no civilization-0 query. With stable, fully
   backed inputs and pure knowledge queries, evaluation may short-circuit or
   reorder. If an admitted read/failure or actual callback distinguishes query
   execution, retain the reference scan: ascending 1…7, all seven queries even
   after success. [Appendix D](#appendix-d-original-query-sequencing) records that sequence.
2. `activeCity(wonder)`: first call `expired`. If true return Unbuilt; otherwise
   read and return the **current recorded location**, including Destroyed.
3. `activeOwner(wonder)`: call `activeCity`; if negative return no owner.
   Otherwise read that city slot's signed owner, with no live-city-token test.
4. `hasWonder(civ, wonder)`: for Eiffel only, return false immediately if both
   scenario mode and the scenario Eiffel-suppression flag are set. Otherwise
   call `activeCity`; a negative result means false. Compare its city owner's
   signed value with the **signed low byte** of the civ argument.

`activeOwner` does not apply the Eiffel scenario suppression. Expiration does
not clear a recorded location, remove a building, reset the Eiffel latch or
undo upgrades. If knowledge or expiry rules change, subsequent queries can
reactivate the effect. A destroyed, unexpired wonder returns Destroyed from
`activeCity`, whereas an expired destroyed wonder returns Unbuilt. All negative
results fail ownership tests, but a caller testing only “not Unbuilt” can still
observe the difference.

The raw location is a separate input. C06's unique-wonder eligibility uses the
recorded built state; losing an effect does not make that wonder buildable again.
Its nuclear-unit eligibility uses Manhattan's raw “not Unbuilt” state. V06's
nuclear-planting gate instead uses `activeCity(Manhattan) != Unbuilt`. A destroyed
wonder therefore can pass those gates; expiry affects only the latter query.
The numeric sentinels are mapped in Appendix A.

### Acquisition and removal boundaries

C06 owns recording a newly acquired wonder, competing completion, authority and
notifications. Its special completion continuations invoke Darwin research
completion **twice in sequence** (V03), Leonardo's operation below, Eiffel's
operation below, or [Apollo publication](W03-visibility-and-exploration.md#apollo-revelation).
Ordinary storage calls do not synthesize these effects.

An ownership change at a recorded wonder city changes subsequent owner queries
without moving that location. C01 deletion changes matching recorded locations
to Destroyed. C09 capture has separate wonder-notice and later Leonardo hooks;
N02's negotiated city transfer has its own later Leonardo hook. V03's advance
grant calls Leonardo after the grant/first-discovery work and before Great Library
propagation. Do not invoke an upgrade every time an arbitrary unit is created,
or reapply a one-time effect whenever a presence query succeeds. Those caller
joins require their owning documents for the complete surrounding sequence.

## Building maintenance quote

`upkeep(civ, building)` returns a full-width nonnegative cost. The helper reads
the supplied rule entry directly; it does not first require a stored building.

1. Start with unsigned base maintenance.
2. For Barracks only:
   - If difficulty is below Prince and cost is nonzero, subtract one.
   - Query Gunpowder. If known, add one.
   - Start at Mobile Warfare. While the advance reference is nonnegative and
     that rule is disabled, replace it with its **signed first prerequisite**.
     Only that prerequisite is followed; do not search both prerequisite branches.
   - Query knowledge of the final reference, **including a negative sentinel**.
     If known, add one.
3. If the resulting cost equals **exactly one**, query Adam Smith ownership.
   If active, replace cost with zero. Do not test it for other costs.
4. If cost is still nonzero, read the civilization's live government. Under
   Fundamentalism, Temple, Colosseum and Cathedral cost zero.
5. Return cost. There is no further clamping or narrowing.

For example, a base-255 Barracks at Prince with both queried advances known
costs 257. A base-1 Barracks at Chieftain with neither known costs zero; with
Gunpowder known it costs one and can then be paid by Adam Smith. A disabled
Mobile Warfare chain ending at −1 adds one because that sentinel's knowledge
query is true; a chain ending at −2 does not.

The walk has no visited set or cycle rejection. A disabled cycle does not
terminate normally; a finite observer may stop and report that boundary rather
than invent a returned cost. An invalid positive prerequisite needs supplied
rule input or a bounded stop. Rejecting cyclic/out-of-range rules at load time
is an optional input restriction, not an original validation step. These are
read/termination boundaries, not required out-of-bounds overwrites.

## City maintenance payment and forced loss

[F04's city turn](F04-turn-sequencing.md#inside-one-city-turn) invokes this payer
after pollution on its human-city path. The helper itself does not test human
control, disorder or city liveness.

1. Capture the city's signed owner once. If that owner's entry government is
   Anarchy, return immediately. Do not retest this outer government gate later.
2. Scan production improvement identities from Palace through Capitalization,
   ascending. For each, query ordinary presence. Spaceship parts and
   Capitalization therefore fail the presence query.
3. For a present building, compute `upkeep(capturedOwner, building)`, then subtract
   it from that owner's **live gold**, with signed 32-bit wrap.
4. If the resulting gold is nonnegative, proceed to the next identity.
5. Otherwise set that owner's gold to zero and remove the building with the
   elementary setter.
6. If autoplay is currently off, set text argument 1 to the current building
   name and numeric argument 0 to `unsignedCost * sharedShieldRows`, interpreted
   as a wrapping signed 32-bit value. Show the city's InHock notice with the
   building's sprite.
7. After the notice, read the building cost and shared shield factor **again**.
   Add that fresh wrapping product to the captured owner's current gold, with
   signed 32-bit wrap. Continue the scan using live presence and rule values.

There is no one-sale limit and no invocation of C06's manual-sale policy. Palace
can be removed; SoldThisTurn is not set; there is no immediate recalculation,
capital-hint repair or shield-stock refund. A notice can change owner, government,
rule values, factor or treasury: payment continues for the captured owner, the
outer Anarchy test is not repeated, and the credit uses fresh inputs. Subsequent
quotes can nevertheless observe a new government in their Fundamentalism check.

A zero-maintenance building can be lost if incoming gold is already negative:
subtracting zero leaves it negative and enters the forced-loss branch. Conversely,
subtracting a positive charge from the most negative signed gold can wrap to a
positive value and avoid loss. These are in-bounds scalar behaviors. A safe
implementation using arbitrary-precision arithmetic must still reproduce those
conversion effects when claiming this off-nominal compatibility domain.

## Eiffel acquisition operation

`applyEiffel(civ)` does not check wonder ownership, expiry, scenario suppression,
human control, alive status, autoplay or network mode. Its callers decide when
to invoke it.

1. If the civilization's EiffelApplied latch is already set, return.
2. Replace its unsigned reputation by `floor(reputation / 2)`.
3. Set EiffelApplied **before** any attitude calls.
4. Visit civilizations 1…7 ascending, excluding the **full passed civ identity**.
   For each other civilization, invoke the shared attitude adjustment
   `(other, civ, −25)`. Then clear **Vendetta in other → civ**, reading the
   directed treaty after that call. Do not clear the reverse relationship.

[V04 owns the shared attitude primitive](V04-diplomacy-and-treaties.md#2-shared-attitude-and-hostility).
Its guards and selected-pair accumulation still apply;
Eiffel clears Vendetta after the call even when that adjustment itself does
nothing. Do not replace the call with unconditional subtraction.

The latch survives loss or reacquisition until an explicit reset/load/editor
operation changes it. A subsequent invocation therefore does not halve reputation
again. Ongoing Eiffel reputation decay is a separate
[V04 operation](V04-diplomacy-and-treaties.md#global-reputation-maintenance)
called at F04's global-turn phase; diplomacy policy is V04/V05/A08. This
one-time operation has no rollback if an external child stops after prior effects.

## Leonardo upgrade operation

`upgradeForLeonardo(civ)` performs a single ascending pass. It does not rebuild
unit counters or recursively upgrade a replacement during the same visit.

1. Query `hasWonder(civ, Leonardo)`. If false, return. Initialize one
   `notified[oldType] = false` entry for each of the 62 nominal unit types.
2. Scan unit slots from zero while the index is below the **live unit upper
   bound**. Continue past a slot unless its token is nonzero and its signed
   owner equals the **full civ argument**. Unlike `hasWonder`, this owner test
   does not first narrow the argument.
3. Capture its current type and that rule's signed obsolete-by advance.
   If its role is Defend, its signed defense is lower than Musketeers' signed
   defense, and Gunpowder is known, substitute Gunpowder as the upgrade advance.
4. If the retained advance is negative, or its knowledge query is false, skip
   this unit. The defensive substitution's knowledge query and this final
   knowledge query are separate calls when both branches are reached.
5. Scan candidate types 0…61 ascending, retaining the **last** candidate that:
   - has prerequisite equal to the retained upgrade advance;
   - has the same role as the unit's **current live type**;
   - has signed capacity at least the signed capacity of that live type.
   There is no candidate-domain, strength, movement, cost, enabled/availability,
   obsolete-by or best-capacity test. If no candidate qualifies, skip the unit.
6. Read notification state for the unit's current live type, then test current
   human membership using F01's masked civilization shift. If not yet notified
   and human, mark that old type notified **before** callbacks. Set text args
   0/1/2 to the current old-type name, retained replacement-type name and Leonardo
   name. If mode is below network mode 3 or the local human civ equals the full
   civ argument, show Upgrade with the replacement unit and large-screen
   presentation flag. Otherwise send the corresponding remote text request with
   three string args, no numeric args, replacement type and the raw screen value.
   The recipient comes from the current civ-to-player-slot mapping.
7. After that boundary, store the retained replacement type in the selected
   unit slot. Clear Veteran in its **live** flags. Do not revalidate token,
   owner or old type after the callback.
8. Redraw its live coordinates. If the live mode is at least 3, broadcast a state
   difference, then a DrawSquare message using coordinates read **after** the
   difference callback. There is no second mode test between those two sends.
9. Advance to the next slot, testing the live bound again.

Notifications are once per old type **per invocation**, not once per replacement
or civilization lifetime. No autoplay gate exists here. AI units still change
type and redraw. A candidate can be the original type; it is still assigned and
loses Veteran. Retain hit-point damage, movement spent, orders, home, auxiliary
counter and all other fields. Later consumers use the new rules; this routine
does not rescale damage or remaining movement, nor repair type/role/region counts.

### Missing input and excluded-write boundaries

A remote human with no valid mapped player slot cannot receive the notice.
The required repaired profile suppresses that undeliverable notice and continues
valid replacement work, retaining the notification mark/text effects. Never read
another record as recipient backing. This is a deliberate correction; supplied
recipient reads and diagnostic stops describe the original bug only. Appendix A
retains its historical evidence.

Nominal notification entries cover types 0…61. The original local allocation
has two extra uninitialized bytes; their existence does not initialize those
entries. An extended-input experiment must supply every actual rule and
notification read or stop where unavailable. A notification write outside the
valid local object, or a unit-record write outside its valid object, is an
excluded bug. Do not emulate adjacent-memory corruption to continue this pass.
A narrower implementation may reject invalid types before the pass, declaring
that input limit separately from the original's lack of validation.

## Ordinary building effect catalog

All ordinary buildings additionally participate in storage, C06 eligibility and
production, this document's maintenance, C09 losses and A02 production valuation.
Those generic operations do not need repeating in every row. A rule can change
cost/prerequisites without changing these identity-specific consumers. The
owning specifications below define the complete consuming algorithms.

| Building | Effect consumers and scope |
| --- | --- |
| Palace | [C03](C03-city-economy-and-corruption.md) capital/overlap and corruption; [C04](C04-city-happiness-and-disorder.md) happiness; C06 relocation on completion; C09 capture/relocation; V06 bribery/incitement constraints and distances. Presence does not itself enforce uniqueness. |
| Barracks | [C06](C06-city-production-and-purchasing.md) ground-unit Veteran creation; [U04](U04-unit-orders-and-activation.md) recovery; upkeep special case above. |
| Granary | [C05](C05-city-growth-and-support.md) food retention after population change. |
| Temple | [C03](C03-city-economy-and-corruption.md) content/tithe subtotal; [C04](C04-city-happiness-and-disorder.md) mood stages; Fundamentalist upkeep exemption above. |
| Marketplace | C03 tax/luxury bonuses and C06 Bank prerequisite. |
| Library | C03 science bonus and C06 University prerequisite. |
| Courthouse | C03 corruption/waste; C04 Democracy mood contribution; V06 incitement pricing. |
| City Walls | [U06](U06-combat-and-veterancy.md) defense and combat aftermath; C09 capture; P02 rendering. |
| Aqueduct | C05 ordinary growth and celebration-growth facility query. |
| Bank | C03 tax/luxury bonus and C06 Stock Exchange prerequisite. |
| Cathedral | C03 content/tithes and C04 mood; Fundamentalist upkeep exemption above. |
| University | C03 science bonus and C06 Research Lab prerequisite. |
| Mass Transit | [W05](W05-pollution-and-global-warming.md) population-pollution control. |
| Colosseum | C03 content/tithes and C04 mood; Fundamentalist upkeep exemption above. |
| Factory | C03 industrial production and C06 dependent industrial eligibility. |
| Manufacturing Plant | C03 industrial production. |
| SDI Defense | [U07](U07-nuclear-attacks.md) nuclear interception; V06 spy-nuclear eligibility; [A06](A06-ai-air-and-nuclear.md#7-nuclear-city-targeting) target rejection. |
| Recycling Center | C03 industrial-pollution divisor consumed by W05. |
| Power Plant | C03 industrial production and plant classification consumed by W05; C06 replacement/eligibility. |
| Hydro Plant | C03 power effect and cleaner-plant divisor consumed by W05; C06 replacement/river eligibility. |
| Nuclear Plant | C03 power effect and cleaner-plant divisor; W05 meltdown and removal; C06 replacement. |
| Stock Exchange | C03 tax/luxury bonus. |
| Sewer System | C05 second population-growth facility requirement. |
| Supermarket | [C02](C02-city-tiles-and-worker-assignment.md) farmland food modifier. |
| Superhighways | C02 tile trade; C03 recurring-route trade; [C07](C07-trade-and-commodities.md) delivery payment. |
| Research Lab | C03 science; its consumer also accepts SETI. |
| SAM Battery | U06 air-attack defense. |
| Coastal Fortress | U06 sea-attack defense. |
| Solar Plant | C03 power and industrial-pollution divisor; W05 intensity and warming-pressure effects; C06 replacement. |
| Harbor | C02 ocean food. |
| Offshore Platform | C02 ocean shields. |
| Airport | C06 air-unit Veteran creation; U04 recovery; [U05](U05-transport-and-special-movement.md) airlift; C03 route trade and C07 delivery modifiers. |
| Police Station | C04 away-unit unhappiness, also available through Women's Suffrage. |
| Port Facility | C06 sea-unit Veteran creation and U04 recovery. |

[C07](C07-trade-and-commodities.md#demand-scores) also consumes Marketplace,
Bank, Cathedral/Michelangelo, Factory, Power/Hydro/Nuclear/Solar Plant,
Mass Transit, Recycling Center, SDI Defense and Superhighways for commodity
demand. Its formulas retain ordinary-presence versus effective-wonder distinctions.
C01/C05 use Palace and growth facilities in the lifecycle/food operations
referenced above; their identity checks are not general building effects.

Spaceship Structural, Component and Module production delegates to V07; they
are not city buildings. Capitalization delegates to C03's tax conversion and
C06's no-stock-completion gate. Other uses in AI evaluation or presentation do
not grant additional simulation effects implicitly.

## Wonder effect catalog

“Owner-wide” means an explicit `hasWonder` query in the consumer. “Local” means
an active location comparison. Neither means setting ordinary building entries.
Expiry uses the parsed rule reference, not a hard-coded date or technology list.
C06 owns each wonder's unique-build eligibility and completion behavior.

| Wonder | Scope and formula owner |
| --- | --- |
| Pyramids | Owner-wide Granary alternative in C05 food retention; C06 redundant-build gate. |
| Hanging Gardens | Owner-wide happiness with a distinct host-city contribution, C04. |
| Colossus | Local tile-trade modifier, C02. |
| Lighthouse | Owner-wide sea movement, [U02](U02-movement-and-zones-of-control.md); trireme coast-loss exemption, U05; qualifying sea-unit Veteran creation, C06. |
| Great Library | Active owner's technology propagation following advance grants, V03. No building presence is synthesized. |
| Oracle | Owner-wide Temple contribution multiplier, C03 content/tithe subtotal and C04 mood. |
| Great Wall | Owner-wide city-defense alternative, U06; negotiation/war policy V04/V05/A08. It does not create stored Walls. |
| Sun Tzu's War Academy | Owner-wide ground-unit Veteran creation, C06, and combat promotion, U06. Recovery uses its own U04 facility tests. |
| King Richard's Crusade | Local tile-shield modifier, C02. |
| Marco Polo's Embassy | Owner-wide diplomatic information/access predicates in V04/V05/V06 and P07/N02; actual contact and treaty storage remain separate. |
| Michelangelo's Chapel | Owner-wide Cathedral alternative in C03/C04 and C07 commodity demand; C06 redundant-build gate. |
| Copernicus' Observatory | Local science multiplier, C03. |
| Magellan's Expedition | Owner-wide sea movement allowance, U02. Diplomatic/AI valuation belongs to V05/A08. |
| Shakespeare's Theatre | Local mood/disorder gates, C04 and the F04 city-turn join. |
| Leonardo's Workshop | Owner-wide upgrade pass above; triggers in C06, V03, C09 and N02. |
| J. S. Bach's Cathedral | Owner-wide content contribution in C03/C04; no continent filter is added by the shared wonder query. |
| Isaac Newton's College | Local science modifier, C03. |
| Adam Smith's Trading Co. | Owner-wide exact-one maintenance exemption above. |
| Darwin's Voyage | Two sequential research-completion calls on C06 completion, V03. No recurrent science bonus. |
| Statue of Liberty | Owner-wide government availability, anarchy/transition and related per-turn policy, V02/V04/A08. |
| Eiffel Tower | One-time reputation/attitude/Vendetta operation above; ongoing F04 reputation decay and V04/V05/A08 policy. Only `hasWonder` has the scenario suppression. |
| Women's Suffrage | Owner-wide Police Station alternative for C04 unhappiness; C06 redundant-build gate. |
| Hoover Dam | Owner-wide power alternative, C03/W05; C06 plant eligibility. Shared query adds no same-continent or same-region requirement. |
| Manhattan Project | Global raw built-state gate for C06 nuclear construction; active-location gate for V06 spy planting; C06 completion effects. Destruction and expiry are deliberately distinguished above. |
| United Nations | Owner-wide diplomacy/senate/access predicates, V04/V05/V06/A08; U06/U07 treaty-violation children. |
| Apollo Program | C06 completion invokes W03 publication; raw global built-state and civilization/space gates consumed by C06, C05 and V07. Publication is not continuously rerun by ownership queries. |
| SETI Program | Owner-wide Research Lab alternative, C03; C06 redundant-build gate. |
| Cure for Cancer | Owner-wide mood contribution, C04; P07 demographics presentation uses its own query. |

C08 defines query semantics and the named acquisition operations. The diplomacy,
research and spaceship specifications own their consuming algorithms. Consumers
must retain their actual query choice, short-circuit order and local/owner/global scope. A blanket
“all cities gain this building” representation would change maintenance,
redundancy, sale and existence tests.

## Discriminating cases


> Compare the gameplay/interchange observations in the
> [production scope](#production-contract-scope). Original memory/helper traces,
> arbitrary native callbacks and unsafe backing-byte cases in historical lists
> are optional reference, not additional production acceptance criteria.

Unless specified otherwise: records/rules are initialized; owner 1 has a live
city; expiry references are negative; callbacks return without mutation;
mode is single-player, autoplay off, gold nonnegative, no relevant advances or
wonders owned. No case below makes a random decision or changes a random context.

| Setup | Required observation |
| --- | --- |
| Set an ordinary building with `on = −1`, then query it; clear with zero. | Present then absent; no quote, gold, calculation or notice side effects. Invalid identities perform no city access. |
| Pyramids located at city 0; that city has no Granary entry. | Owner has Pyramids; ordinary Granary presence remains false. |
| Expiry advance known only by dead civilization 7. | Expired; queries still run 1…7; active city is Unbuilt, raw location unchanged. |
| Nonexpired Destroyed location. Then give one civilization the expiry advance. | Active location changes from Destroyed to Unbuilt without changing recorded location. |
| Scenario Eiffel suppression set; Eiffel located at owner-1 city. | `hasWonder(1,Eiffel)` false without expiry query; active owner still 1. |
| Wonder located in a backed city slot whose live token is zero and owner is 1. | Active owner remains 1. `hasWonder(257,w)` compares low byte and succeeds. |
| Prince Barracks base 255, both relevant advances known. | Cost 257, not 1; Adam Smith is not queried. |
| Disabled Mobile Warfare follows first prerequisite to −1 or −2. | Respectively add one or zero after the Gunpowder adjustment. |
| Temple base 3 under Fundamentalism. | Cost zero without an Adam Smith query. Anarchy itself does not change the quote. |
| Gold 2; buildings Palace then Temple; Palace upkeep 3, cost 4, factor 10; Temple upkeep 1. | Palace removed, first balance becomes 40, Temple retained, final gold 39. No SoldThisTurn mutation. |
| Gold −1; present zero-upkeep building, cost 2, factor 10. | Building lost, final gold 20. |
| Gold −2147483648; present upkeep-1 building. | Subtraction wraps to 2147483647; building retained. |
| Executable diagnostic: a notice callback changes cost to 5, factor to 6, captured owner's gold to 7 and city's owner to 2. | Original trace credits 30 to captured owner, final 37. A baseline case needs an actual admitted game/peer producer for these changes; displaying a notice does not supply one. |
| Eiffel reputation 9, latch clear; every other → 1 relationship has Vendetta. | Reputation 4, latch set before six ascending attitude calls; directional Vendettas cleared. Second call does nothing. |
| Leonardo candidates 4 and 9 both match; 4 has greater capacity. | Type 9 wins; greatest capacity is not the selection rule. |
| Two human units of same old type upgraded in one call. | One notice; both assignments and redraws; both lose Veteran, other fields retained. |
| Upgrade notice changes selected slot owner/type/token, then returns. | Retained replacement still assigned to that same slot; no eligibility retest. |
| Executable diagnostic: redraw enters network mode; injected difference callback changes coordinates and leaves network mode. | Original trace sends Difference then DrawSquare with new coordinates. Redraw does not change session mode in the production contract; actual protocol transitions are tested through N01–N03. |
| Remote notification requires unavailable recipient input. | Stop after mark/text setup and before unit assignment, or declare the notice-suppression repair. Do not invent an original recipient. |

## Appendix A — Representation and source evidence

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is OpenCiv2 commit
`a71e8aefffe04e6f719e0e213b027b128a747711`, checked against the same selected
bodies in OpenCiv2 revision `fc2155ccdb2123c1137babab6fa39e38152278c9`.
Source navigation: [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp),
[civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp),
[regargs.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/regargs.cpp),
[enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h),
[network.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/network.cpp)
and [BUGS.md](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md).

| Entry | Original address |
| --- | --- |
| `city_has_improvement`, `city_set_improvement` | `0043d20a`, `0043d289` |
| `wonder_is_expired`, `wonder_get_city`, `wonder_get_owner` | `00453da0`, `00453e18`, `00453edf` |
| `civ_has_wonder` | `00453e51` |
| `civ_has_advance` | `004bd9f0` |
| `civ_leonardo_upgrade` | `004be6ba` |
| `civ_apply_eiffel_tower` | `004ec312` |
| `civ_improvement_upkeep`, `city_pay_improvement_upkeep` | `004f00f0`, `004f0221` |

Independent corroboration: `civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, selected bodies in
`src/civilization.cpp`, `src/unit_upgrade.cpp`, `src/city_distribution.cpp`,
`src/city_turn.cpp` and `src/city_capture.cpp`. This corroborates ordered fields, full-width payment,
negative-query gates and live callback continuations, with explicit mapped-input
boundaries. Its adjacent-memory adapters do not expand X01's write domain.

Original executable: MGE 5.4.0f patch 3, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Read-only disassembly at `004be995…004be9dc` confirms Leonardo captures screen
value, reads civ-to-slot at `004be9a6`, reads recipient at `004be9b5`, calls text
send at `004be9bf`, then assigns type at `004be9d0`. There is **no slot-range
guard** in that branch. BF-194's guarded skip is a reconstruction repair.
This is instruction inspection, not an original runtime replay.

| Representation | Validation mapping |
| --- | --- |
| Ordinary improvement identities | 1…34 in catalog order. Presence bit is `id mod 8` in byte `floor(id/8)` of five stored bytes. Bit 0 and unused high bits are not ordinary facilities. |
| Nonbuilding production | Nothing 0; spaceship 35/36/37; Capitalization 38. |
| Wonders | 0…27 in catalog order; improvement identity is 39+wonder. Raw location is signed 16-bit: Unbuilt −1, Destroyed −2. |
| Scenario suppression | Game Scenario flag and low bit 1 of the first scenario flags word. Its effect here is specifically the Eiffel `hasWonder` early return. |
| Eiffel | Civilization flag `0x10`; reputation byte shifted right logically; directional treaty Vendetta `0x10` cleared after each attitude call. |
| Leonardo | Original local array 64 bytes, first 62 cleared. Veteran unit flag `0x2000` cleared; type byte replaced. Remote text kind `0x3e`, three strings/no numbers; state-difference arguments `(0xff,2,0,0,0)`, DrawSquare kind `0x72`, recipient `0xff`, current x/y then zero fields. Local large-screen flag is 8 iff stored screen value nonzero; remote send uses raw value. |
| Key rule references | Gunpowder 35, Mobile Warfare 53, Musketeers unit 7, Defend role 1, Prince difficulty 2; government Anarchy 0 and Fundamentalism 4. |
| Presence setter ABI | Decompilation returns city×11 for accepted IDs and zero otherwise. This is register residue of a void operation, not a success/failure contract. |

## Appendix B — Validation and remaining joins

The 11 selected primary bodies listed in Appendix A were compared with the
pinned revision and are unchanged. A focused external harness extracted all
11, including actual building storage, advance membership, the four wonder
queries, maintenance quote/payment, Eiffel and Leonardo. **79 assertions passed**
in an optimized build and an AddressSanitizer/UndefinedBehaviorSanitizer build.
Leak detection was disabled because the sandbox's tracing environment prevents
LeakSanitizer execution; address and undefined-behavior checks remained enabled.

Cases cover valid/ignored storage identities; low-byte versus full-argument owner
tests; stale city slots; expiry scan order, captured expiry and fresh location;
scenario suppression; knowledge sentinels; Barracks width and prerequisite walk;
Adam Smith and Fundamentalism; Anarchy payment gate; sequential forced loss,
negative-gold wrap and live refund inputs; Eiffel latch/ordering/directed clears;
Leonardo last-match/signed-capacity/defensive substitution, notice coalescing,
retained fields, live bounds and post-callback writes; network-mode and coordinate
changes; and missing/supplied remote recipient input. No random provider is used.

The harness adapts host pointer carriers and logical structure layout, instruments
knowledge-call boundaries, and maps the directed treaty access to typed storage.
It replaces BF-194's guard with an explicit original recipient read that accepts
supplied input or stops; it never performs an excluded overwrite. Gold builds
use defined wrapping arithmetic. Attitude, presentation, redraw and network
transport/receipt remain declared adapters. Notification callback injections
exercise live-read order, not claims that the native UI generates those mutations.
Invalid-rule cycles, uninitialized extended-type notification reads and arbitrary
missing backing were not exhaustively exercised; their boundaries are specified
above. C06/F04/C09/V03 parent integration was not executed in this focused harness.

These are adapted source-body checks on Linux, not native Windows conformance,
original runtime replay, or independent cleanroom implementation tests. Harnesses
and reference sources remain external research material and are not imported
into this repository. Earlier consumer fixtures that stub wonder/building effects
retain their stated limits; these checks do not retroactively validate them.

[C09](C09-city-capture-and-revolt.md#appendix-b--validation-and-remaining-joins)
specifies and exercises its full capture parent with declared C08 children,
including the human/autoplay gate on Eiffel and late Leonardo trigger.
Remaining joins: actual C08 children in C09 and N02 city transfer; actual V03 knowledge
and research propagation, V04 attitude children in Eiffel acquisition, and actual C08
providers in [V02's government/rate consumers](V02-governments-and-tax-rates.md).
V02's finite source checks use declared query/upkeep adapters; they do not close
these effect-provider joins.
[A02](A02-ai-city-production.md#3-defense-needs-sales-and-donor-airlift) owns
the redundant-building sales policy; its source checks use declared facility/
wonder adapters, leaving the actual provider join open. P04/P06/P07/P10 and N02/N05
own real notices, sprites, player receipt and transport. F04 needs consecutive
human/AI turns with real children. The effect catalog does not establish those
integration results by implication. X02 should retain state/call traces,
no-RNG assertions,
scalar-wrap cases, missing-read stops and excluded-write diagnostics separately.

## Appendix C. Original numeric annotations

These original annotations retain storage widths and reference-evaluation context.
They do not prescribe internal types or redundant casts. The main contract and
F01 determine logical ranges and behaviorally significant conversions; external
file/wire encodings remain normative in their owning mappings. Where an excerpt
includes an off-nominal conversion, it applies to that explicitly admitted profile,
not as a new requirement to accept unsupported inputs.

### Reference annotation 1

These algorithms use signed 32-bit gold arithmetic, wrapping at each add or
subtract. Rule costs/upkeep are unsigned values 0…255; reputation is 0…255.
Barracks additions are full-width, **not** reduced modulo 256. Unit defense
and capacity comparisons in Leonardo interpret their stored values as signed
8-bit integers. Owner and expiry references are signed 8-bit values; recorded
wonder locations are signed 16-bit values. These interpretation rules affect
results; the storage layout itself is optional.

## Appendix D. Original query sequencing

The following sequence records original reference evaluation. X01's ordering
convention governs cleanroom conformance: retain the dependencies and failure
effects stated in the main contract; unobserved query/store order alone is not
an additional requirement. Historical source traces retain their evidence scope.

1. `expired(wonder)`: capture its signed expiry-advance reference. If negative,
   return false without querying knowledge. Otherwise query civilizations
   **1 through 7, ascending**, for that same captured reference. Start false and
   retain true if **any** query succeeds. Finish all seven queries even after
   a success. There is no human/alive filter and no civilization-0 query.
