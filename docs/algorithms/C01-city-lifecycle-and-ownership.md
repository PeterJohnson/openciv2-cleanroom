# C01: City lifecycle and ownership

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in C01 |
| --- | --- |
| Gameplay | Founding/joining/deletion/transfer, naming choices/cursors, spatial claims and city/unit/civilization bookkeeping. |
| Original files/content | CITY and related text grammar plus F03 names; F06 city records and references. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Name pool allocation/order, file-service callbacks, city-window scopes and inactive storage not consumed by a game rule. |

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

Independent constructor assignments may be grouped only between required
publication points. Slot selection, later-equal production ties, naming callbacks,
map synchronization and retained fields preserve their specified consequences.

## Scope, inputs and shared definitions

This specifies classic Civilization II Multiplayer Gold Edition **5.4.0f,
patch 3** city allocation, initialization, naming, founding/joining, removal,
spatial queries and the elementary civil-war ownership transfer. Appendix B
records the evidence and validation limits.

[F01](F01-determinism-and-arithmetic.md) supplies arithmetic/random semantics;
[F02](F02-game-state-and-invariants.md) owns identities, retained slot state and
logical fields; [F03](F03-rules-and-configuration.md) supplies rules and text
configuration. [W01](W01-map-geometry-and-spatial-state.md) owns geometry,
neighborhoods, region data and tile setters; [W03](W03-visibility-and-exploration.md)
owns exploration, remembered features and city knowledge. [U01](U01-unit-lifecycle-and-stacks.md)
owns unit/stack lifetime effects; [U04](U04-unit-orders-and-activation.md) owns
the invoking commands/orders and civilization recovery. [F05](F05-game-setup-and-starting-positions.md)
and [W06](W06-huts-and-barbarians.md) invoke creation from setup and huts.

C02–C08 own calculation, production eligibility, commodities and facilities;
[C09](C09-city-capture-and-revolt.md) owns capture/incitement policy and their larger
transfer sequences; [V01](V01-civilization-lifecycle-and-civil-war.md) owns
civilization creation/elimination/civil war. P04/P06 own text resources,
dialogs and city-window behavior; N02/N05 own network dispatch, transport and
receipt. Preserve the ordered calls below with their real effects. A child
boundary is not an assumed-success result or a reason to skip prior mutations.

Nominal state has valid city/unit/civilization/leader slots, initialized rule
and map data, consistent finite stack links, valid file/provider state and
bounded strings. Civilizations are 0..7; ordinarily city slots 0..254 can be
allocated. A slot and its lifetime token are different. These routines do not
all validate their inputs or require positive population; their actual guards
are stated individually. Stricter accepted domains must be declared separately.

Use F01's behavioral arithmetic and ordered live reads. Nominal civilizations
are 0…7 and city slots 0…254, with a separate NoCity value. With consistent
summaries, a civilization's city count is 0…255; no native integer width is
implied. Preserve independently maintained counts rather than recounting them.

Population has C05's signed interpretation and cyclic updates: changing 127 by
+1 yields −128 before its consumers run. Unit-rule defense/cost use the signed
view specified below, including values whose unsigned rule code is above 127.
F03 owns ingestion; consumers must not silently interchange signed and unsigned
views. The score numerator defense×8 fits −1024…1016, so its multiplication needs
sufficient range, not a mandatory signed32 operation. Division still truncates
toward zero and retains its zero-divisor boundary.

At specified mutations retain modulo-256 production counts, signed16 stock/
coordinate/summary conversions and the modulo-2³² next-city token, including zero
on wrap. These are value transitions, not layout requirements. Callbacks can
change map, owner, active city, counters and slot identity; do not implicitly
revalidate or replace a captured slot. Original width shorthand is in [Appendix C](#appendix-c-original-numeric-annotations).

[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
excludes out-of-bounds writes and their corruption. Preserve safe earlier
effects, then reject/stop before an unsupported write unless a safe continuation
is specified. Supplied backing can explain reads, not authorize adjacent-object
damage. Valid counter wrap, retained inactive fields and intentional shared
storage remain compatibility behavior.

## City lookup and spatial queries

### Exact city lookup

`cityAt(x,y)` first requires W01 in-bounds, then a nonnegative **tile city owner**.
An Airbase alone does not pass. Only then scan live city slots ascending through
the live city upper bound. Return the first live record whose coordinates equal
the supplied x/y exactly; otherwise NoCity. Do not normalize x, use tile-index
alias equality, require record owner to equal the tile owner, or search records
when the tile gate fails. Duplicate-coordinate records select the lowest slot.

### Weighted nearest city

Inputs are origin, owner filter, requested region and alternate owner. Set
selected=NoCity and shared nearest distance=9999. If requested region>=0,
query the origin's land-only region. If absent, disable the region gate;
otherwise retain that origin region. A negative requested region disables
this gate; special -2 additionally requires a Port Facility.

Scan live slots ascending through the live bound. For each live city capture
its x/y, then require:

1. Region gate disabled, or **origin** region equals requested region. Candidate
   city region is not queried; this unusual filter must not become a usual
   same-continent city search.
2. Owner filter negative, or live city owner equals that filter reduced to its
   unsigned8 value, or equals a nonnegative alternate owner.
3. For mode -2 only, the candidate has Port Facility through C08's query.

Compute W01 weighted diagonal distance to the captured candidate coordinates.
If it is at most the **live shared nearest distance**, replace both selected
slot and shared distance. Later equal candidates win. Return the slot or
NoCity; leave the shared distance published. No draw or final conversion to
ordinary map distance occurs. Recovery's conversion remains U04 caller work.

### Coastal predicates

`cityTouchesBody(city,region)` returns false without scanning if region>=63.
Otherwise scan D[0..7] around the city's **live** coordinates, wrapping x once
per visit. Return true at the first in-bounds Ocean coordinate whose raw region
equals the argument. Negative region is not explicitly rejected before scanning.

`citiesShareOcean(a,b)` starts previousRegion=NoRegion. Scan D[0..7] around a's
live coordinates. For each in-bounds Ocean neighbor whose region differs from
previousRegion, call `cityTouchesBody(b,region)` and retain that region as
previousRegion; return true on success. Otherwise false after all eight.
This suppresses repeated consecutive tested regions, not all previously seen
regions. Region 63 cannot satisfy b. These queries do not relabel map regions.

### Adjacent sea summary

`adjacentSeaInfo(city,optionalMaximum,optionalTraversalIndex)` captures city x/y,
initializes maximum=0, lastRegion=0 and direction=0. While direction<8:

1. Form wrapped D[direction] neighbor. If in bounds and Ocean, read its region
   into lastRegion and its regional water-body size. The size read occurs even
   for region>=63; afterward replace size by 1 for such a region.
2. If maximum<=size, replace maximum. This does not make lastRegion the region
   having the maximum: it always retains the latest visited Ocean region.
3. On an Ocean visit, additionally advance direction by `2-(direction AND 1)`.
   Then apply the loop's ordinary +1. Non-Ocean/out-of-bounds visits only add 1.

Store maximum to the first optional output, then the **final traversal index**
to the second optional output; if the outputs alias, the latter wins. Return
lastRegion. No Ocean returns 0/maximum0/index8. This is not an eight-neighbor
maximum-region selection; Ocean visits skip later directions and the traversal
output can exceed 8. Missing regional read backing stops before the size override.

## Allocation and initialization

`createCity(x,y,c)` does not itself check settler role, Ocean, nearby cities,
ownership or an existing city at the coordinate. Callers supply those policies.
Set city-creation activity=1 before testing authority. In mode<3 or on the host,
use the following local path. Otherwise use the client path below.

### Slot and record prefix

Set slot=0. While slot<live upper bound and that slot's token is nonzero,
increment slot. Only if slot equals the live upper bound, extend by one when
bound<255. At bound>=255 in that extension branch, show TOOMANYCITIES through
the debug notice wrapper only for a human c in mode<3, then clear creation
activity and return NoCity. Notice callbacks' other effects remain. Reusing a
hole does not perform the extension limit test. Nominal bound is 0..255; a
negative bound skips the scan and equality branch, leaving slot0 selected
without repairing the bound. A stricter input domain may reject such state.

After selection, in order:

1. Set c's city count to `s16(c.cityCount+1)` and store the current turn as its last-city
   turn. Do not recompute either summary.
2. Set tile City component with synchronization, then clear Fortress component
   with synchronization. This can turn an Airbase into a City feature.
3. Store x/y, owner c, current next-city token; increment next-city token with
   full32 wrap; store founder c, capture age 0, flags exactly CommodityDirty,
   and size 1. A wrapped zero token is not silently skipped.
4. If Apollo's global location is negative, clear city-known set and all eight
   remembered sizes. Otherwise set city-known to all eight civilizations and
   each remembered size to 1; add all eight exploration bits at the center,
   then remember center features for civilizations 1..7 in ascending order.
5. Clear all explicitly stored specialist kinds and set production to fixed Settlers slot.

The constructor does not directly set the tile occupant owner here. Its feature
and radius operations do not substitute for that owner. Normal founding/setup
callers and U01 placement supply their own ownership effects.

### Initial production and stocks

Set bestScore=0. Scan fixed unit slots 2..61 ascending. For a Defend-role type,
call [C06 unit-build eligibility](C06-city-production-and-purchasing.md#unit-availability)
`(c,city,type)`. If accepted, read the type's
then-current signed defense/cost and compute `trunc((defense*8)/cost)`.
Add 1 for DoubleVersusHorse. A score>=bestScore replaces both bestScore and
city production; **later equal scores win**, including zero. All-negative
scores leave Settlers selected. Zero cost reaches F01's division boundary;
there is no special free-unit rule in this selection.

Increment c's selected unit-production counter with byte retention, using the
city's live production after the scan. Clear food box, shield box and base trade.
Clear **only the first character** of the existing name, then invoke default
naming below. On return clear worked-square/specialist-count storage and all
installed improvements. These later clears overwrite changes made by naming
callbacks to those fields; other retained state is not globally reset.

### First-city and AI catch-up work

If c's **live** city count equals 1, set Palace through C08 and store x as its
capital-x cache. If c is now nonhuman, run this sequence:

1. Begin tile-update batching. Visit B[0..19] around original input `(x,y)`;
   center B[20] is excluded. For each in-bounds tile first add c's exploration
   bit with synchronization. If live turn>=40 and index<8, and the tile's live
   exploration mask intersects no current human player, query terrain. If its
   irrigation-conversion rule is KeepTerrain, set Irrigation with synchronization.
   This has no separate non-Ocean or existing-improvement exclusion.
2. End/flush batching. If live turn>40 and the center is not explored by any
   live human player, set size to `clamp(trunc((liveTurn-20)/20),2,10)`, then set
   Temple, Marketplace and Library in order.
3. Independently, if live turn>40 and live production is not Settlers, create
   one unit of that production type for input c at original `(x,y)`. Ignore its
   return. This does not require the center to remain hidden from humans.

Turn 40 can irrigate the first eight neighbors but does not get the larger
city/facilities or extra unit. Apollo publication earlier can defeat the
unexplored-by-humans condition. The first-city test is not repeated within
this entered branch; individual turn/visibility/production reads stay live.

### Route state, environmental flags and final children

Set route count to zero, partially clear inactive partner payloads as defined
in Appendix A, then clear all three route commodities. Retain those logical
residual partner values rather than treating the record as fully zeroed.
Supply/demand outputs are subsequently produced by C07, not initialized here
to an invented default.

Visit D[0..8], including center last, around original input coordinates. For
each in-bounds tile:

1. If Ocean, read region and its water-body size. If size>1 and either region<63
   or the city's **current Coastal flag is already set**, set OceanAccess.
   Then set Coastal unconditionally. Two visits to region 63 can therefore
   qualify differently: the first can enable the second. Do not deduplicate.
2. Read raw terrain River flag; if present, or otherwise terrain is Mountains,
   set HydroPossible. No recomputation removes flags set by an earlier visit.

Call `refreshCityArea(city)` below, then [C07 commodity refresh](C07-trade-and-commodities.md#commodity-refresh-gate-and-terrain-survey).
C07 initializes the road accumulator to zero under the supported safety repair;
no original stack input is needed. This is an intentional correction. After that child,
clear science, tax, net trade, total food, total shields, happy and unhappy
outputs. These clears occur after the child and can overwrite its changes.

If live mode>=3, send state synchronization `(all,2,0,0,0)`, then flush sends
with timeout 5000. Do not retest mode between those calls. Finally clear
creation activity and return retained city slot. There is no full city-output
recalculation, default worked-tile assignment, global population recount,
neighbor redraw or city-window open added by this constructor itself.

### Client creation

Clear creation activity, set shared creation result Pending, send CreateCity to
host with x,y,c, then sample start ticks even if the send already supplied a reply.
While result is Pending, sample ticks; if signed32(now-start)>=3600 stop waiting,
otherwise poll messages `(1,1)`. Pending after that loop logs a timeout, shows
SERVERCONNECTTIME, then clears the turn-loop-active flag after the dialog returns.

Drain priority/normal received queues with polling `(1,0)` until both are zero.
Return the shared result read at the completed drain boundary. A late reply can
change the returned value even after the timeout notice. There is no second
creation-activity clear after client callbacks and no local fallback allocation.
Pending and NoCity are different results; no timeout rollback is implied.

## Default city naming

Production decodes selected sources into owned name catalogs before invoking
naming. The classic file procedure below specifies source precedence, ordered
selection and outcomes; opening, scanning and closing are not gameplay reentry
points. Scenario and home catalogs remain separate: a matching scenario list
uses only its own EXTRA pool, even when exhausted. An unterminated imported
civilization list can supply its known prefix; selecting beyond that prefix
reports unsupported content instead of reusing a closed scanner. An EXTRA pool
ending at EOF remains a finite fallback pool. Native lists have explicit ends.
The counter, retry and published-name effects below remain shared behavior.

The classic provider reads CITY text as below. The [native JSON city-name
provider](../content/data-format.md#5-city-names-and-production-preferences)
supplies ordered name lists for explicitly named civilizations and a shared
fallback pool, without exposing file sections or requiring that file.
Retain the live civilization-name lookup, cursor/wrap, retry and published-name
behavior; native storage does not add unique names or an ID-based fallback.

Capture the city's owner c. Form section text from the pooled civilization
plural-name label at the absolute value of the current leader's signed label
index. The leader slot is read through c's **live tribe reference** whenever
needed below; do not snapshot the entire leader record across file callbacks.
The section text itself is retained after construction.

Nominal CITY.TXT contains a matching civilization section and ordered names,
optionally followed by EXTRA and `@STOP` terminators. A name is published at
most once using this exact procedure, with at most two attempts:

1. Increment the live leader's next-name counter modulo 256, then capture that
   unsigned counter as remaining. The increment precedes opening the file.
2. Change to scenario directory and open CITY's retained section. On failure,
   change to home directory and retry. If that also fails, leave the attempt
   loop without resetting the counter. On success change to home directory.
3. While remaining>0, read a line in mode 1. If the live line is empty or begins
   with `@STOP` case-insensitively over its first five characters, break without
   decrementing remaining. Otherwise decrement remaining and repeat.
4. If remaining>0, open EXTRA in the **current file**, with no new filename.
   If missing, reset the live leader's counter to zero and begin the next attempt.
   Otherwise scan EXTRA lines with the same remaining count and stop rules.
5. If remaining<=0, clear the entire name destination and copy at most 15
   characters from the current shared line, retaining a terminal NUL. Finish.
   Otherwise reset the leader counter to zero and retry.

Always close the text file on normal exit, including missing sections and
two exhausted attempts. Failed naming leaves the old name payload as it was
on entry; creation had already cleared its first character only. No generated
fallback, uniqueness search, random suffix or civilization count-based index
is added. EOF return is not independently tested: the supplied reader's line
buffer behavior determines the next check.

Counter 255 increments to zero. No name line is read in that attempt; the
current shared line after section lookup is published, which can be the section
header itself. EXTRA does not restart remaining at the original index: it
continues the count left when the civilization list ended. Cancellation of
player founding decrements the live counter once afterward, not an undo log
for this algorithm's possible reset/retry/publication effects.

The original section scratch can overflow for an otherwise accepted long
civilization name. This is an excluded overwrite. A safe implementation can
provide sufficient storage for the complete supported section text and continue
the specified lookup; it need not reproduce adjacent corruption or truncate
the name at the original scratch capacity. Unsupported unterminated/unbacked
strings require a declared rejection or stop. Appendix A records the distinction.

## Radius claims and fertility suppression

`refreshCityArea(city)` captures city x/y and signed owner before beginning
tile-update batching. Visit B[0..44] in order, wrapping x once each time. For
each in-bounds coordinate:

1. Replace its fertility value by `liveFertility modulo 8`, with synchronization.
2. For B[0..20] only, replace its radius claimant with captured owner, with
   synchronization.

End/flush batching. There is no Ocean, exploration, owner, worked-assignment,
city-distance or overlap check. All 45 entries get fertility suppression;
the inner 21 get claims. Later calls overwrite earlier overlapping claims.
This changes neither occupant owner nor city identity. Repeated aliases from
wrapping remain visits, and callbacks do not replace the captured center/owner.

## Founding from a unit

`foundFromUnit(u,city,optionalName)` captures u's x/y. If map reveal is enabled
or its live owner equals the local observer, ensure that coordinate visible
using the actor's live owner. If supplied city<0, call `createCity` at captured
coordinates using u's owner **reread after centering**; return on a negative
result. A supplied nonnegative city bypasses creation without an identity,
location or owner check.

For the resulting slot, copy optionalName when non-null; nominal names fit the
15-character plus terminator destination. An oversized direct argument must
not overwrite adjacent city state. Then call U01 **basic unit deletion** with
synchronization. Clear center Irrigation, Mine, Road, Railroad and Fortress
components with synchronization; City, Unit and Pollution components are not
cleared by this operation. Remember current center features for civilizations
1..7 unconditionally, then redraw its area with mode 1, live local observer and
update=1. It does not mark every civilization's exploration.

This helper adds no settler-role, adjacency or Ocean gate. AI founding policy
can invoke it with no name, whereas player founding supplies an already-created
city and modal text. Basic deletion differs from cargo-aware deletion; do not
substitute a general disband command or delete all units at the coordinate.

## Player Build City / Join City command

Clear unit blink activity. Capture active unit, x/y and owner c. If its type's
role is not Settle, show ONLYSETTLERS with fixed Settlers sprite and the display
scale determined by large-screen state, then return. If Ocean, show CITYATSEA
and return. Query exact city at the captured coordinate.

### Joining an existing city

If a city is found and its signed size is less than the live **unsigned
Aqueduct-size rule**, set size to `s8(size+1)`, basic-delete u
with synchronization, refresh that city window if displayed, and redraw captured
area with mode 1 and live local observer. Otherwise set numeric argument 0 to
the Aqueduct-size rule's unsigned value and show ONLY10 for that city.

There is no city-owner comparison here, no Aqueduct/Sewer facility query, no
special distinction between Settlers and Engineers beyond Settle role, and no
full city recalculation added. Eligibility enforced by a UI/menu caller must
not be mistaken for this helper's guard. An accepted size 127 increment can
wrap to -128 if a custom threshold permits it.

### Creating, naming and committing a new city

With no exact city, scan D[0..7] ascending around captured coordinates. At the
first in-bounds tile with a city owner, query its exact city, show ADJACENTCITY
for that result, and return. This includes foreign city owners; an Airbase
does not satisfy the city-owner predicate. There is no larger radius search.

Call `createCity(x,y,c)`; on negative result return. On success show NAMECITY
text input, maximum length 15, with the new city's name as initial text. **Zero
means acceptance**; a nonzero result cancels.

On cancellation, delete the new city with from-network=false, then decrement
the name counter modulo 256 through captured c's **then-live tribe reference**.
Do not restore the entire pre-creation game state: token allocation, summary,
map, naming and descendant effects have already occurred. The actor normally
survives because founding's basic deletion has not run, but city deletion's
support policy can still affect any unit that now references that city.

On acceptance:

1. Call `foundFromUnit` with retained actor and created slot, plus returned name.
2. Set string argument 0 to that slot's live name. Clear shared text, append the
   current year, then copy it as string argument 1.
3. Play BuildCity sound `(1,0,0)` and show FOUNDED city-picture notice with
   picture 76, zoom flag 1 and captured c.
4. If tutorial is currently enabled and c's live city count is 1, show
   FIRSTPRODUCT from the tutorial file with its tutorial sprite, mode 0.
5. Set city-processing mode=1; begin city-window notice scope, open the retained
   city, end notice scope; then set city-processing mode=0. This is an explicit
   final zero, not restoration of an incoming value.

Any changes from notice/window callbacks remain. There is no new city identity
or actor revalidation between these steps and no transactional cancellation
after accepted naming. P06 owns later Rename City UI and its different busy/
idle/autoclose behavior; it must not reuse this founding rollback sequence.

## City deletion

`deleteCity(city,fromNetwork)` first returns if city is negative or its token is
zero. It does not test the current city upper bound before reading a nonnegative
slot; nominal callers supply valid backing. These no-op guards leave the
incoming deletion-activity flag unchanged. Otherwise set deletion activity=1
and choose local/host versus client authority as for creation.

### Supported-unit processing

Locally save the city-window refresh lock, then set it to 1. Capture city's x/y
and owner c. Decrement c's signed16 city count immediately. Scan live unit slots
**descending**, initially from the current unit upper bound minus one. A unit
qualifies when live and its home, interpreted as unsigned8, equals city:

1. Fixed Partisans type: set home NoCity and continue downward, regardless of
   owner, role or location.
2. Otherwise, for an AI-owned unit only, query exact city at its live location.
   If another city is found, and the unit's role is Defend and the number of
   Defend-role units in its stack is exactly one, set home to that city's slot
   with byte retention and continue. No same-owner check on the new city occurs.
3. For an AI unit in another city that fails that defender condition, set the
   other city's DefenderLost flag before destruction.
4. In every other qualifying case, invoke U01 destroy/redraw `(u,sync=1)`.
   After that child, **restart the descending scan from the live upper bound**.

The original city stays live during this entire phase. Do not free it before
unit children, iterate a snapshot of supported units, or simply make all units
homeless. Destroy/redraw has its own stack/cargo and window effects. A callback
can add/rehome/delete units and affect the restarted scan. Partisan/defender
rehoming itself does not restart the scan.

Ordinary city IDs stop at 254, so unsigned home 255 is NoCity. An explicitly
backed off-nominal live slot 255 can nevertheless match such homes; the helper
does not add a sentinel exception. Invalid destination-array writes remain
excluded even if malformed records make the scan reach them.

### Freeing, routes and wonders

When no further supported unit needs processing:

1. Set the retained city's token to zero. If city equals the **live** city upper
   bound minus one, decrement that bound once. Do not trim all trailing holes.
   Other city payload remains until later named mutations; it is not cleared.
2. Scan other live cities ascending through the live bound. Capture each one's
   route count with signed8 interpretation; scan its route indices descending.
   For each partner equal to the deleted slot, call ordered route removal below.
3. Scan all 28 wonder slots ascending. Any location equal to the deleted city
   becomes Destroyed, not Unbuilt. No wonder eligibility/rebuild policy is added.

There is no independent decrement of total civilization population, unit-build
summary counters or regional-city summaries here. Their owning callers/phases
must retain the actual updates they perform; do not invent a universal recount.

### Ordered route removal

Call [C07's shared removal primitive](C07-trade-and-commodities.md#ordered-route-removal).
It owns shift order, trailing payload, scalar count wrap, dirty publication and
excluded-write boundaries. Deletion's descending caller scan remains above.

### Map, visibility and windows

Initialize nearbyOwners empty. Begin batching and clear the City component at
captured center with synchronization. Visit B[0..44] around captured x/y, wrapping
x once per visit. Only in-bounds **non-Ocean** coordinates continue:

1. Set fertility to `8 + (liveFertility modulo 8)`, with synchronization.
2. For B[0..20] only, if current radius claimant equals captured c, clear that
   claimant to 0. Then query city-or-unit owner. If it is a normal civilization
   other than c, include it in nearbyOwners.

End/flush batching. If nearbyOwners is nonempty, iterate the actual stack at
captured center and OR each unit's visibility with those owners. There is no
exploration test or replacement of the existing visibility sets.

Next scan all live cities ascending through the live bound and call
`refreshCityArea` on each. This rebuilds overlapping radius/fertility claims in
**slot order**, including Ocean cells that the removal pass skipped. A later
city can overwrite an earlier claimant; it is not nearest-city ownership.

Close the deleted city window if displayed. Restore the saved refresh lock
after that child; if the saved value was zero, refresh the city window. If
fromNetwork's low byte is zero and live mode>=3, synchronize state `(all,2,0,0,0)`,
broadcast CityWindowDeleted(city), then CityWindowRedraw, then flush sends with
timeout 5000. Once entered, do not recheck mode between these calls. Finally
clear deletion activity and return.

Deletion does not itself clear every known-city/known-feature layer, reset the
tile occupant to Unowned, remove Pollution, relabel regions, choose a new
capital, eliminate the civilization, transfer a city to an attacker or redraw
the entire map. Callers such as U04 disband, U06 combat, C05/C06 city turns,
C09 capture and S02 events supply their own later work. No rollback is added
when a child stops after earlier mutations; lock/busy cleanup occurs only at
the specified normal continuation.

### Client deletion

After the live-city guard and authority decision, clear deletion activity, set
deletion acknowledgement Pending, send DeleteCity(city) to host, and sample
start ticks. Poll `(1,1)` while acknowledgement is Pending and signed elapsed
ticks<3600. Pending at timeout logs, shows SERVERCONNECTTIME, then clears
turn-loop activity after the notice. Drain both queues with `(1,0)`, then return.
There is no second activity clear, acknowledgement interpretation as a city ID,
or local deletion fallback. fromNetwork does not bypass this authority branch;
it only gates the local path's final notifications.

## Elementary ownership transfer

`transferCityForCivilWar(city,oldC,newC)` performs these operations in order:

1. Decrement oldC's signed16 city count; increment newC's signed16 count.
2. Subtract the city's live signed size from oldC's total-city-size summary,
   retaining signed16. Add its live signed size to newC's summary, same retention.
3. Store newC as the city's owner with byte retention.
4. Set tile occupant owner at that city's **then-live** coordinate to newC,
   with synchronization requested.

It does not verify oldC equals the record's owner, and equal oldC/newC still
executes the ordered decrement/increment and population changes. It does not
change founder, token, capture age, facilities, wonders, production, name,
worked assignments, unit ownership/homes, radius claims or revealed knowledge.
V01's civil-war parent selects cities, reveals them and later handles units.
C09's capture/incited transfers have additional policies and cannot be replaced
by this small primitive alone. [V05 parley cession](V05-diplomatic-negotiation.md#city-transfer)
also has its own ordered building/unit/production and visibility changes, including
a mutable city index and excluded out-of-bounds-write boundary. There is no single
generic owner setter that silently performs all those different sequences.

## Caller joins and conformance cases


> Compare the gameplay/interchange observations in the
> [production scope](#production-contract-scope). Original memory/helper traces,
> arbitrary native callbacks and unsafe backing-byte cases in historical lists
> are optional reference, not additional production acceptance criteria.

| Caller | Required boundary |
| --- | --- |
| F05 accelerated starts | Calls full creation amid its grants/site search. Preserve naming, production eligibility, first-city catches, shared counters and commodity child before continuing setup. |
| W06 tribe hut | Calls full creation at the captured hut coordinate, then may apply its separate year-based size/facility awards. Negative-result late writes remain W06's excluded bug. |
| A03/A04 settler founding | Calls `foundFromUnit(u,NoCity,noName)` after AI-specific eligibility. The player adjacency/prompt checks are not implicit in the helper. |
| Player Build City | Creates before the name dialog; acceptance deletes the actor through founding, cancellation invokes full city deletion and decrements one live name counter. |
| U04 cheat disband / U06 battle / C05–C06 depletion | Invoke full deletion at their stated phase, then retain their own redraw, elimination, population/production or combat continuation. |
| V01 civil war / C09 capture | V01 uses the elementary transfer with explicit parent knowledge/unit steps; C09 specifies the different capture/incitement consequences. |
| Network requests | N02 decodes authority/messages and calls these same children. Client Pending waits and local completion ordering are not proof of peer receipt or wire compatibility. |

Useful examples assume valid fixed state and inert callbacks unless specified:

- With live slots 0 and 2, empty 1 and bound 3, creation uses 1 without extending.
  Deleting slot 2 afterward reduces bound to 2 once; it does not also trim empty 1.
- Two eligible defenders with equal zero scores replace default production in
  turn; the later type wins. Two negative scores leave Settlers. Cost zero is
  a division boundary after earlier constructor effects.
- First AI city at turn 40 may gain irrigation, but extra population/facilities
  and the free selected defender begin at turn 41. Visibility to any human can
  suppress the first two bonuses independently of the defender grant.
- A city naming counter 255 becomes 0, performs no name-line reads and copies
  the section lookup's live line; exhausted names can reset and retry once.
- Deleting a city with Partisans, a human-supported military unit, and a lone
  AI Defend unit inside another city respectively clears the Partisans' home,
  destroys the human unit and rehomes the AI defender. Descending order and
  post-destruction restart can change which records later qualify.
- Removing the middle of routes [A,B,C] yields count2 and active [A,C], retaining
  C in the inactive final payload. Removing at index0 with count0 wraps count
  to255 but performs no shifted-array write.
- Ocean at D[0] makes the sea summary skip D[1] and D[2]. Its return identifies
  the last Ocean region actually visited, not necessarily the largest body;
  optional traversal output is the final loop counter.

Full conformance must also cover client replies/timeouts/queue-drain mutations,
window/name callbacks, Apollo and knowledge timing, repeated wrapped radius
visits, stale unused fields, scalar wrap, custom rules and safe rejection before
invalid writes. Named child integration remains separate from testing a supplied
child result.

The following original representation/provenance sections retain diagnostic
supplied-byte and stop cases. X01's repaired profile controls implementation and
required tests, including fresh-game isolation and the named C07/A02/C05 repairs.

## Appendix A. Original representation and excluded bugs

These encodings support original snapshots and F06 persistence; they do not
require a matching private data layout.

| Item | Original mapping |
| --- | --- |
| Entries | Exact lookup `0043cf76`; nearest `0043d07a`; naming `0043f493`; area refresh `0043f7a7`; creation `0043f8b0`; route removal `00440325`; deletion `004413d1`; body query `0044263f`; sea summary `0044272d`; shared-ocean query `004429af`; founding `004c4d1e`; player build `0058be56`; civil-war transfer `0057a7e9`. |
| City fields | 88-byte record; signed size byte9, owner8, founder10; name16 bytes at32; worked word48; improvement bytes52..56; production57; route count58; commodities65..67; signed partner words68..73; lifetime token84. F02/F06 own the complete record. |
| Inactive partner initialization | Creation clears only three bytes starting at partner[0]. Logically partner0 becomes0; partner1 retains its previous upper byte and gets low byte0, interpreted signed16; partner2 remains unchanged. Route count0 makes them inactive, but saved bytes/explicit later reads can observe retention. C07's later child may perform further changes. |
| Name retention | Creation clears name[0] only before naming. Successful naming clears all16 bytes then copies up to15; failure can retain the old15-character tail behind the initial NUL. Neither allocation nor deletion memsets the whole city. |
| Flags | CommodityDirty `00020000`, Coastal `00000080`, HydroPossible `00000800`, OceanAccess `00200000`, DefenderLost `00000020`; DoubleVersusHorse unit-type flag `0400`; River terrain flag `80`. |
| Feature operations | Creation sets `02`, clears `40`; founding clears `7c`; deletion clears `02`. Radius owner is tile-byte2 high three bits; fertility low nibble is in the packed owner/fertility byte. Area refresh uses fertility `&7`; removal uses `|8`. |
| Fixed slots | Settle role5, Defend role1; Settlers type0, Partisans type9; Palace1, Temple4, Marketplace5, Library6, Port Facility34; Apollo wonder25. |
| Protocol markers | Pending `fffffffe`, NoCity `ffffffff`; CreateCity `3b`, DeleteCity `39`, CityWindowDeleted `89`, CityWindowRedraw `8a` hex. The protocol owner supplies exact packets/receipt. |

The original naming section buffer is 24 bytes. Accepted long pooled civilization
names can overrun it at `0043f54f`. Both reconstructions provide larger storage
(the primary uses 256 bytes; the independent uses 255 for its supported text
domain). Their buffer capacities are not required cleanroom limits. Preserve
the whole supported section string and subsequent lookup; exclude overwritten
stack state. Independent bug entry BF-054 records the original capacity and
accepted longer-name producer.

The direct optional-name copy in founding likewise assumes a fitting destination.
Oversized direct names do not authorize writes into worked tiles or facilities;
reject or stop before that write, or document an intentional bounded-copy policy.
The normal player text wrapper limits payload to15.

Malformed route indices/counts can cause writes outside the three-element
partner/commodity arrays. Historical negative-index diagnostics in the
independent fixtures observe adjacent-field changes; these are excluded bug
evidence, **not** required conformance. Keep safe scalar count wrap distinct.

C07's commodity counter is a different issue: the original reads a local it did
not initialize. OpenCiv2 B1 and independent BF-001 choose zero in production.
This is a reconstruction correction to an unknown input, not an out-of-bounds
write. C01 calls the real child boundary and does not infer original zero from
that correction. A bounded original comparison must supply that incoming value
or stop at the child's first unsupported read.

## Appendix B. Evidence, checks and remaining work

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`, inspected at reference checkout
`fc2155ccdb2123c1137babab6fa39e38152278c9`. Relevant city/unit/civ/game/handwritten
register-helper sources do not differ between those pins. Independent evidence
is `civ2` revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. Neither reference tree was modified.

| Family | Primary evidence |
| --- | --- |
| City queries, naming, creation, area, deletion, route removal, transfer | [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp): named entries in Appendix A. |
| Founding/player entry | [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp): `unit_found_city`, `unit_cmd_build_city`, basic/destroy-redraw unit children. |
| Other callers | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): setup/editor callers; [retvals.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/retvals.cpp): tribe-hut child; [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp): civil-war transfer callers. |
| Map and encodings | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp), [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h), [game types](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h). |
| Reconstruction corrections | [BUGS.md](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md): B1 commodity counter, B2 persistent naming index; primary naming source cites independent BF-054. |

Independent `src/city_creation.cpp`, `src/city_deletion.cpp`, `src/map_input.cpp`,
`src/city.cpp`, `src/unit_exploration.cpp` and `src/city_production_rules.cpp`
corroborate ordered creation/deletion, name-counter wrap, retained inactive
payload, first-city branches, query quirks, caller effects and client waits.

A fresh temporary source harness extracted **17 additional routines**: ten city
lifecycle/query routines, the two founding/player routines, and five primitive
map access/setters. It reused 92 previously extracted unit, combat and map
routines, including the weighted nearest-city query. The city-at and city-delete
adapters were replaced by the actual routines. The fixtures exercised real
creation/naming/area/deletion, basic unit deletion and destroy/redraw, stack
traversal, unit creation, tile mutation/remembering and batching where invoked.

Both optimized and address/undefined-sanitized builds passed **322 assertions**.
The finite cases cover:

- Exact lookup's map gate and first-record tie, and nearest-city later ties.
- Hole reuse versus the 255-slot extension limit, production zero/negative
  scores, selected-production counter wrap, lifetime-token wrap, inactive
  partner/name/summary retention and the post-commodity output clears.
- First AI city turns 39/40/41, center-only Apollo publication, actual defender
  creation, repeated region-63 coastal visits and all 45 radius coordinates.
- CITY/EXTRA residual skip, 255-to-0 name-counter publication, reset/retry and
  missing-file retention, plus a safely stored 100-character section string.
- Real supported-unit removal, Partisans' home clearing, AI lone-defender
  rehoming, descending route deletion, destroyed wonders, remaining-city claims,
  no-op activity retention and single-step bound shrinkage.
- Zero-route scalar underflow, explicit-old-owner transfer and signed16 summary
  wrap; sea-summary skipped directions, last-region return and final index 9.
- Player acceptance/cancellation with actual founding and unit deletion,
  retained constructor effects, cross-owner joining and size 127-to-minus128.
- Client creation reply/drain replacement and timeout followed by a late reply,
  client deletion's request/drain path, callback activity changes, and host
  synchronization followed by flush even after the mode changes.

Map access/feature/region predicates, production eligibility, improvement
queries/setters, commodities, text/label/
directory providers, dialogs, windows, clock and network transport were declared
adapters. In particular, supplied commodity output is not a validation of C07's
unknown incoming local, nor is a transport trace evidence of peer receipt.
Nominal map access used a fixed 20-by-12 backing grid and the source's 45-entry
radius table; it did not establish every geometry/alias case. The host harness
retained packed scalar records but used host-sized pointer carriers. It did not
exercise unsupported-address behavior, 32-bit address aliasing, timer wrap,
all incoming fields or full saved-state equality. Long-section storage checks
validate the safe reconstruction, not original overwrite consequences. No
out-of-bounds-write diagnostic was made a passing compatibility result.

The independent repository's `tests/ai_garrison_intent.py` describes original/
source capacity returns, four bounded naming returns, eight first-city prefix
captures and two final creation returns with explicit supplied children.
`tests/city_deletion_fixture.inc` and `tests/city_deletion_tail_fixture.inc`
contain bounded deletion/route/area comparisons. These fixture definitions and
their declared adapters were inspected, not rerun for C01. Their malformed
negative-route alias diagnostics are excluded bug evidence. Original-executable
evidence in that repository targets SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Do not combine those bounded captures with the fresh source-only checks into
a claim that the entire original city operation was compared end to end.

No fresh original-executable run, instruction export or saved annotation change
was made. [C07](C07-trade-and-commodities.md#appendix-b--evidence-and-validation)
owns route removal and adds actual supplied-input commodity bodies to finite
city-turn cases; this does not close C01 creation/transfer/UI integration.
C08 facility/effect children still require their full caller joins.
[C09](C09-city-capture-and-revolt.md#appendix-b--validation-and-remaining-joins)
adds actual capture/incitement and deletion/production joins with explicit
V01/S02/effect adapters. [V01](V01-civilization-lifecycle-and-civil-war.md#appendix-b--validation-and-remaining-integration)
now joins actual lifecycle/civil-war bodies with C01 transfer and explicit
external adapters; complete capture/elimination/event integration still requires
its actual children and S02 dispatch.
Full F05/W06/U04 caller joins, P04/P06 resources/dialog/window continuations and N02/N05 receipt
remain separate obligations. X02 must retain independent full-state/random/
external-trace fixtures, including partial failure, callback identity changes,
client queue timing and the excluded overwrite boundaries. These checks do
not establish complete original-program or whole-game conformance.

## Appendix C. Original numeric annotations

These original annotations retain storage widths and reference-evaluation context.
They do not prescribe internal types or redundant casts. The main contract and
F01 determine logical ranges and behaviorally significant conversions; external
file/wire encodings remain normative in their owning mappings. Where an excerpt
includes an off-nominal conversion, it applies to that explicitly admitted profile,
not as a new requirement to accept unsupported inputs.

### Reference annotation 1

Use signed32 arithmetic with F01 retention, signed16 city/count/stock/coordinate
stores, signed8 size and stated rule fields, unsigned8 sequence/count wrap,
and ordered live reads. Keep next city identity's full32 wrap. Callbacks can
change the map, owner, active city, counters and slot identity; a retained slot
is not implicitly revalidated or replaced after a child.
