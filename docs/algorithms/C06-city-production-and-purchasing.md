# C06 — City production and purchasing

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
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Target and ownership

The target is **Civilization II Multiplayer Gold Edition 5.4.0f, patch 3**.
This document owns build eligibility, production categories and snapshots,
changing orders, shield accumulation and completion, player purchasing/selling,
AI production spending, wonder acquisition and completion races. The validation
and integration limits are stated below.

[F01](F01-determinism-and-arithmetic.md) defines arithmetic, clamps and random sampling
laws; [F02](F02-game-state-and-invariants.md) defines persistent state and
shared workspaces; [F03](F03-rules-and-configuration.md) supplies parsed rules.
[C05](C05-city-growth-and-support.md#box-factors-and-settler-food) owns shieldRows,
including its AI adjustment. **Do not duplicate that factor calculation here.**
[C03](C03-city-economy-and-corruption.md) owns recalculation and Capitalization's
conversion into tax; C06 suppresses stock accumulation/completion for that item.
[F04](F04-turn-sequencing.md#inside-one-city-turn) calls production after food
accumulation and before support losses and the second full recalculation.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

[U01](U01-unit-lifecycle-and-stacks.md) supplies actual unit creation/deletion;
[C01](C01-city-lifecycle-and-ownership.md) supplies city deletion.
[A02](A02-ai-city-production.md) owns AI production scoring and final choice,
including redundant-facility sales inside
that chooser. C06 owns the spending formula executed by the city-production
turn and the state changes after a choice. [C07](C07-trade-and-commodities.md) owns
commodity selection/delivery;
[C08](C08-buildings-and-wonders.md) owns facility/wonder storage and effects and
references C06's shared build eligibility. V03 owns research;
[V07](V07-spaceship.md) owns spaceship queries, group selection, addition and launch. P04/P06/P10 and
N02/N05 supply presentation and network receipt; their callbacks are observable
boundaries, not assumed-success operations.

## Domain and numerical contracts

Nominal inputs have live cities, owners, valid unit/improvement identities,
initialized rules, backed map/region/rank state, and coherent window and dialog
state. Production is a logical tagged choice: a unit, ordinary improvement,
spaceship group, Capitalization or wonder. There is no arbitrary additional
unit type meaning for the chooser's sentinel99.

Accepted off-nominal input preserves signed/narrowing behavior. Use F01's
reference expression results and truncation. Stock publications apply `s16`,
production choices preserve their logical identity/sentinel (`s8` for admitted
extra codes), and unit-build counter updates apply modulo256.
Unit cost, attack, defense, movement and role comparisons used below read
signed8 rule values unless explicitly stated otherwise. Improvement costs and
cosmic rules are unsigned. Treasury arithmetic is signed32.

The original's representations and identity numbers are mapped in Appendix A.
Widths in the main algorithm describe results; an implementation may use other
storage. Missing read backing requires explicit inputs or a bounded stop, not
invented zeros. Division by zero stops at the described operation after earlier
mutations. Restricting input domains is permissible when declared.

[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
excludes writes beyond intended records/arrays and their corruption. In
particular, a failed AI diplomat creation does not authorize writing Veteran
into a nonexistent unit, and a malformed production selection does not authorize
writing beyond the civilization's per-type build counters. Omit an invalid write
where the remaining continuation is established; otherwise stop before it.
Neither physical backing nor logical emulation makes the damage required.

`human(owner)` reads the live control mask using F01 selector semantics. Unless
explicitly captured, city fields, rules and shared workspace are live across
child calls. In player commands, repeated selected-city queries can return
different cities after callbacks; do not collapse the whole command to one
captured record.

## Build eligibility

These predicates answer availability; they do not validate every subsequent
completion call. A saved production item can reach completion without rerunning
all eligibility checks. The sequence below defines reference queries and early
exits; preserve their result, admitted failure boundaries and significant effects.

### Unit availability

`canBuildUnit(owner, cityOrNone, type)` returns false at the first failing gate:

1. Prerequisite must be greater than Disabled(-2). Prerequisite None(-1) passes
   without an advance query; otherwise owner must know it.
2. If obsoleteBy is nonnegative and owner knows it, reject.
3. For Defend role, reject if this type's signed defense is below the Musketeers
   type's signed defense and owner knows Gunpowder.
4. For Attack role with signed movement exactly twice the unsigned road movement
   multiplier, reject if its signed attack<=Knights attack, defense<Knights
   defense, and owner knows the Knights type's prerequisite.
5. Signed attack>=99 requires Manhattan's raw location not Unbuilt and knowledge
   of Nuclear Fission. Destroyed Manhattan passes the location test.
6. For a supplied city and Sea domain, require both OceanAccess and Coastal flags.
   A negative city argument skips this city gate.
7. For AI owners only, if this is a Land unit with signed movement greater than
   the road multiplier, scan all62 type slots ascending. Another type excludes
   this one if it has the same role/domain, movement greater than the multiplier,
   an advance query passing for its prerequisite, attack>=this attack and
   defense>=this defense, with at least one strictly greater. Do **not** recurse
   through availability for the alternative or test its obsoleteBy, costs,
   special government requirement or city access. Equal attack/defense does not
   exclude this type. Read rule values again after the advance query.
8. The Fanatics identity requires Fundamentalism. All other surviving types pass.

Role/domain equality and ordering above use the recorded rule values, not names
of stock units with similar capabilities. Availability does not compare costs,
hit points or firepower when eliminating an inferior AI type.

### Wonder availability

`canBuildWonder(owner, wonder)` first handles the scenario restriction: if this
is a scenario, its NoNewWonders flag is set, and reveal-map/editor mode is off,
reject unless its AllowManhattan exception is set and this is Manhattan.
Then require raw wonder location=Unbuilt and a successful advance query for the
corresponding improvement's prerequisite. Destroyed wonders cannot be rebuilt.
Expiration does not itself forbid building a wonder.

### Improvement availability

Wonder choices delegate to canBuildWonder. For a nonwonder improvement:

1. Require its prerequisite advance query to pass. If cityOrNone is negative,
   return true now; the remaining city-specific tests are skipped.
2. Reject an existing improvement. Harbor and Offshore Platform require Coastal;
   Port Facility and Coastal Fortress require both OceanAccess and Coastal.
3. Apply these ordered, identity-specific gates:

| Improvement | Additional requirements |
| --- | --- |
| Granary | Owner lacks Pyramids effect. |
| Cathedral | Owner lacks Michelangelo effect. |
| Courthouse | City lacks Palace. |
| Bank | Marketplace exists, then its prerequisite advance is known. |
| Stock Exchange | Bank exists, then its prerequisite advance is known. |
| University | Library exists, then its prerequisite advance is known. |
| Research Lab | Owner lacks SETI effect, University exists, then University's prerequisite advance is known. |
| Sewer System | Aqueduct exists. |
| Police Station | Owner lacks Women's Suffrage effect. |
| Manufacturing Plant | Factory exists, then Factory's prerequisite advance is known. |
| Hydro Plant | City has HydroPossible. |

4. For Power, Hydro, Solar or Nuclear Plant, require Factory or, only if absent,
   Manufacturing Plant. Solar then passes this gate. Other plant choices also
   require no Hoover effect, no Hydro Plant, no Nuclear Plant unless the choice
   is Hydro, and no Solar Plant, in that order. Existing ordinary Power Plant
   does not prevent upgrading to a different plant.
5. Spaceship Structural/Component/Module choices require Bloodlust off, raw
   Apollo location not Unbuilt, positive year **or** scenario mode, and spaceship
   not launched. This predicate does not ask whether that part group is maxed.
6. Return true. The child facility query owns which identities can be present;
   spaceship groups and Capitalization are not persistent ordinary buildings.

## Category, saved stock and switching penalty

The three switch categories are Units; ordinary improvements through the34th
identity; and all higher nonunit identities together, including spaceship
groups, Capitalization and wonders. Classification uses the requested full
signed value before any production-field narrowing. Appendix A maps the encoding.

`saveBuildState(city)` publishes the current category first and the current
signed shield stock second into shared savedCategory/savedStock. No city identity
is stored with that snapshot. Opening a city, a successful purchase, relevant
unit-disband actions and the displayed-city turn tail refresh it. Merely
previewing or committing a production switch does not refresh it.

`shieldsAfterChange(unusedCity, requestedItem)` starts from the full signed32
savedStock. If difficulty is not Chieftain and the requested category differs
from savedCategory, return:

```text
s32(savedStock - floor(changePenaltyPercent * unsignedLow16(savedStock) / 100))
```

Otherwise return savedStock unchanged. The rule is unsigned, and the product
with the low16 stock is nonnegative and bounded. The snapshot's upper bits
remain relevant to subtraction. Do not recompute from the city's current stock
or apply a penalty repeatedly to successive previews. Do not clamp a negative
result, or suppress a penalty solely because the percentage exceeds100.

For savedStock101 and50%, a category change yields51. A later switch back to the
saved category yields101. For savedStock=-1 and50%, the result is -32768; this
comes from the unsigned low16 operand, not taking half of a signed deficit.

Displayed build turns use current shared shieldRows and net Shields-support:
`rate=clampC(s32(Shields-shieldSupport),1,99)`;
`turns=clampC(s32(trunc(s32(cost*shieldRows-1-stock)/rate)+1),1,999)`.
Append singular/plural turn text based on the captured result. Unit cost is
signed, improvement cost unsigned. This is a forecast, not a completion loop.

## Setting a production order

`setProduction(city, request)` creates its notice-dialog scope, captures the
current signed production as oldItem and the signed owner as owner, then:

1. If the live current item is a valid unit type0..61, decrement captured owner's
   count of cities building that type, with modulo256 retention.
2. If request>=99, call A02's production chooser with both recommendation-output
   pointers absent and use its result. The chooser may mutate game state.
3. If the resulting request<99, and captured owner is currently human and the
   civilization-turn-in-progress flag is zero, calculate shieldsAfterChange and
   store signed16 stock. Then store request with signed8 production semantics.
   A result>=99 makes no assignment here; it does not store sentinel99.
4. If the live item differs from oldItem and captured owner is currently AI,
   run the wonder-change continuation below.
5. If the final live item is a valid unit type0..61, increment captured owner's
   corresponding building count modulo256. Destroy the dialog scope and return.

The primitive does not validate eligibility, reset the snapshot, recalculate,
or inherently refresh a window. It can still decrement and increment counters
when the chooser leaves the item unchanged. Callers define their own subsequent
refresh and retry behavior.

### AI wonder changes

When either oldItem or currentItem is a wonder, scan live cities ascending,
excluding the input city and requiring owner equal to the input city's **live**
owner. Count matches of oldItem and, separately, the input city's live item.
Retain these counts through subsequent notices.

If the current item is a wonder:

1. If oldItem was not a wonder, replace stock with trunc(stock/2), retaining
   signed16. This AI penalty is independent of the saved-category rule and
   difficulty. Retain the new wonder/improvement identities from the live item.
2. Supply captured owner's civilization name as text argument1. If no other
   matching new-wonder city was counted:
   - If oldItem was a wonder and either no other old-item city was counted or
     that old wonder is no longer Unbuilt, use the switch-wonder path. If the
     old count was nonzero, scan all live cities ascending, requiring captured
     owner and oldItem, and directly set their production to the input city's
     live production. Do not call setProduction for these changes. Outside
     autoplay supply old/new names as arguments2/3, issue SwitchWonder with the
     new improvement sprite and zoom8; network modes also broadcast its text.
   - Otherwise, if the captured owner/new-wonder Started notice flag is clear,
     set it **before** testing autoplay. Outside autoplay supply the new name
     as argument2 and issue/broadcast StartWonder with that sprite and zoom8.
3. If live stock=0 and captured owner's stored count in this wonder's seven-item
   group is zero, test human civilizations1..7. Require each human's count in
   this group to be nonzero; no human civilizations makes this test vacuously
   true. If it passes, set `bonus=max(difficulty-2*ownerGroupCount,0)` and add
   `bonus*unsigned improvementCost` to stock, retaining signed16. Use the raw
   cost, not shieldRows. Group and improvement refer to the selected wonder
   retained before the notice, while stock/counts/difficulty are read afterward.

If the current item is not a wonder, oldItem was a wonder and the other-old
count is zero, require the old wonder's location not equal to city, captured
owner lacking its active effect, and its Abandoned notice flag clear. Set that
flag before testing autoplay. Outside autoplay issue/broadcast AbandonWonder
with owner and old-wonder names, sprite and zoom8. Suppressing a notice does not
undo its recorded Started/Abandoned flag.

`resetCivilizationProduction(owner, regionOrAny)` skips everything in hot-seat
mode if owner belongs to the hot-seat-human mask. Otherwise scan live cities
ascending, matching signed city owner against the unsigned low-byte owner
argument and, when region>=0, requiring its tile region to match. Call
setProduction(city,99) for each match, rereading the live city bound.

## Player purchase command

Construct the dialog scope, query the selected city, and return if None. Query
the flush/network busy gate below. If busy, record deferred Buy work with the
current selected city, a fresh selection's owner, and original control argument;
return without calculation or a purchase dialog.

Otherwise request cached-mode C03 recalculation for a fresh selected city.
Capture owner from a fresh selection, then unitItem and buildItem from two
successive selected-city reads. Require local human=owner or reveal-map mode.
If the captured buildItem is Capitalization, return. Read live selected
production to choose the price branch, while using the corresponding **captured**
identity for cost/name:

```text
cost = signed unit cost, or unsigned improvement cost
remaining = clampAB(s32(rawShieldBoxRule*cost-liveSelectedStock),0,999)
ordinary improvement price = 2*remaining
spaceship/wonder price = 4*remaining
unit price = trunc(remaining*remaining/20) + 2*remaining
if a fresh selected city's stock equals0: price *= 2
```

The higher nonunit price covers identities35 and above, including spaceship
groups. Supply the captured item name as text argument0, price as numeric
argument0, and captured owner's live treasury as numeric argument1. Set
affordable=(price<=owner's live treasury) and choose Complete1 if affordable,
Complete0 otherwise. If a fresh selected item is a unit and a fresh selected
city has Disorder, choose Complete2 and force affordable=false. Disorder does
not block the nonunit purchase branch.

Load the selected section, choose the sprite family from fresh selected
production but its identity from the captured corresponding item, set zoom8,
and set default choice1 only when affordable. Show the modal. If retained
affordable is true, result=0, and the city window's live closed value is not1:

1. Subtract captured price from captured owner's live treasury, with signed32 wrap.
   Do not retest affordability after the modal.
2. Query the selected city; set its stock to
   `s16(live rawShieldBoxRule * captured cost)`. The rule is read after the getter.
3. Save build state for a fresh selection. Play the positive purchase sound.
4. Fully recalculate a fresh selection; draw production, update window title and
   main status, in that order. Destroy the dialog scope.

Purchase fills the stock only; it does not create the unit/building or perform
ordinary completion immediately. The target, stock, rules, and treasury may
have changed during the dialog; the captured price/cost/owner remain in force.
Cancellation keeps prior calculation/dialog effects and skips those four steps.

## Player selling command

The input is an improvement-list row. Query selected owner and require local
human or reveal-map mode. A sale already made this turn blocks the command,
except when both reveal-map and scenario mode are active. A blocked sale plays
the negative sound and shows AlreadySold for a fresh selected city.

For an allowed sale, request cached recalculation, capture the signed16 list-top
position, then scan ordinary/nonwonder improvement identities1..38 ascending.
Query each on a fresh selected city. Increment ownedCount only for present
improvements; select the first whose count equals
`s16(listTop+s16(row)+1)`. If none, return. The facility provider normally reports
only identities1..34 as present, so parts and Capitalization are not sale rows.

Palace cannot be sold unless reveal-map **and** scenario mode are active; its
denial plays the negative sound and shows CantHockThis with the Palace sprite.
Otherwise capture `price=unsigned improvementCost*rawShieldBoxRule`, supply name
and price as argument0, and show HockThis with the selected improvement sprite
and zoom8. On modal result0:

1. Play the sell sound, then clear the improvement in a fresh selected city.
2. Query selection again and add captured price to **that city's live owner's**
   treasury with signed32 wrap.
3. Query selection again, set its ImprovementSold flag, refresh the city window,
   update main status, then refresh open Trade, Attitude and Science advisors,
   in that order.

Do not revalidate ownership, the once-per-turn flag, price or facility after
the modal. F04 clears ImprovementSold at city-turn entry. This user command is
separate from A02's automatic redundant-facility sales and [C08 upkeep losses](C08-buildings-and-wonders.md#city-maintenance-payment-and-forced-loss).

## Player production-change command

The busy gate is the same as Buy, but defers Change work. After passing it,
request cached calculation, capture owner, require local-human ownership or
reveal-map mode, and capture the current production for initial highlighting.
Until selected city becomes None:

1. Run the optional advice-panel child; it uses A02's chooser with recommendation
   outputs and can have its specified side effects. Supply the live selected
   city name and load Production. Classic behavior has no extra Cancel-button
   extension. Install option rendering and list layout through P06.
2. Append available unit types ascending0..61, improvements1..38, then wonders
   ascending0..27. Eligibility uses captured owner and fresh selected cities.
   Each row's estimate uses shieldsAfterChange and the displayed-turn formula.
   Wonder expiration adds its display marker but does not remove an otherwise
   available choice. Use the retained highlight item when setting defaults.
3. Add the cheat action only when CheatMode and single-player mode are active.
   Instant Advice caps its displayed row-count consideration at12; then
   for fewer than16 options use max(8,optionCount+1) rows. [P06](P06-city-interface.md) owns pixel layout.
4. Clear pending-close, install the idle auto-close child and run the dialog;
   clear pending-close again afterward. Negative selection or no remaining
   selected city recreates the Change button and returns. If Instant Advice
   is on, close its panels and flush input.
5. Clear Autobuild, AutoUnitsOnly and AutoImprovementsOnly in a fresh selected
   city **before interpreting the dialog action**, including encyclopedia/help.

For the ordinary Choose action, decode the option to a production item, compute
newStock from the saved snapshot, and retain it. If newStock<live selected stock
and ProductionPenalty messages are enabled, supply unsigned penalty percentage,
current item name and chosen item name, then show ProdChange. Result0 rejects
the change. A rejection still leaves the auto flags cleared. On confirmation:

- If a fresh selected production is nonnegative, decrement captured owner's
  corresponding unit-build count; store chosen item in a fresh selected city;
  if its fresh item is nonnegative, increment captured owner's corresponding
  count; store retained newStock into a fresh selection. Valid indices are the
  logical unit-type domain; unbounded positive original counter indices are
  excluded writes, not adjacent civilization-field effects to reproduce.
- Refresh the open Cities and Units advisors in order1,2. This direct command
  does not call setProduction, perform AI wonder-change notices, or refresh the
  saved snapshot.

Other dialog actions:

| Action | Ordered continuation |
| --- | --- |
| Automatic | For captured human owner, load CITYPREF through F03 and show AutoMode. Negative result recreates Change and returns with auto flags already cleared. Otherwise set Autobuild, plus UnitsOnly for choice1 or ImprovementsOnly for choice2. Call setProduction on fresh selection with99, draw production, recreate Change and return without the ordinary trailing recalculation. AI owners skip the preference/mode dialog but still request99. |
| Encyclopedia | Show selected unit or improvement through Pedia, update retained highlight to that item, and repeat the list loop. Auto flags have already cleared. |
| Cheat free item | Create the unit at fresh selected owner/coordinates; or set an ordinary improvement; or add the corresponding spaceship part; or directly assign the wonder location to fresh selection. Capitalization does nothing. Refresh window, show Freebie and continue to the trailing refresh. No shield cost is charged here; S03 owns availability of editor/cheat mode. |
| Other returned action | No production assignment; continue to trailing refresh. |

After Choose (accepted or rejected), Cheat or Other, fully recalculate a fresh
selection, draw production, draw the resource map, recreate Change and return.
If the loop ends because the city vanished, close all three dialogs. Retain the
order of independent selected-city getters, especially across modal, draw and
calculation children; no transaction rolls earlier writes back.

### Busy gate and deferred work boundary

First query whether the network send buffer is flushing. If not, test busy
states in order: unit create, unit delete, city create, city delete, map lock,
map unlock, stack unlink, stack link, stack bottom, unit relocate, wonder
acquire, civilization eliminate, civilization create, civilization-destroyed
check, cargo gather, lift off map, cargo delete, visible destroy. Stop on the
first busy state. A blocked command logs, publishes deferred kind Buy or Change,
then stores the two selection-derived arguments and the original control ID.
N02 owns later receipt/execution and network synchronization. Selling has no
corresponding gate in its own routine; its caller may impose one.

## Production during a city turn

At entry, set local improvement=None and acquired=false; set shared
completedImprovement=None. Capture signed city owner. Compute
`net=s32(Shields-shieldSupport)`; if Disorder is set, replace net with0; then
replace any net<1 with0. Retain net for the later AI purchase phase.
Unless current production is Capitalization, add s16(net) to stock, retaining
signed16. This can wrap a stock negative.

For barbarian owner0, query the city tile's region. If that civilization's
regional military count>16 and the tile-unit-owner query is nonnegative, set
stock=0. This follows accumulation and does not require the occupying unit's
owner to be barbarian.

Dispatch from the live production sign to the nonunit or unit path below.
Explicit early returns skip the common tail. Normal continuations run:

1. If captured owner is currently human and Autobuild is set, and either stock=0
   or live production is nonunit with the parent's retained improvement=None,
   call setProduction(city,99). If the resulting **stored item** equals99,
   clear all three auto flags. The setter normally does not write99, so do not
   reinterpret its sentinel return as this stored-field test.
2. If captured owner is currently AI, run the AI purchase phase below with
   retained net. Its buying can fill the next item but does not run another
   completion pass.
3. Clear DefenderLost in the city's live flags and return.

### Nonunit readiness and wonder races

Retain improvement=absolute value of the current signed item. Set wonder=None
unless this is a wonder identity; calculate retained
`cost=s32(unsigned improvementCost*shieldRows)`.

Outside autoplay, if the raw wonder location is no longer Unbuilt, or the
nonwonder facility is already present, supply its name as argument1. Unless
InvalidBuild messages are disabled, invoke BadBuild with its corresponding
sprite. This warning alone does not switch orders, clear stock or return.

For a spaceship-group item built by a human, require spaceship not launched
and group not maxed. Failure supplies the name and calls BadSpace, then proceeds
to the common tail; no extra autoplay/message-preference gate is added here.
AI part builds bypass this initial gate. Other items pass it directly.

If cost>live stock or the retained item is Capitalization, skip completion and
run the common tail. Otherwise clear WonderBuilt and set acquired=false:

- Nonwonder: query whether the facility is already present. If so, discard the
  retained improvement (None). No cost is deducted in that case; the later
  common tail can reconsider a human auto order.
- Wonder: if its raw location is Unbuilt and captured owner is currently AI,
  scan live cities ascending. If any human city has the **same live production
  item as the input city** and stock>=unsigned improvementCost*rawShieldBoxRule,
  return from the entire production routine immediately. This preserves the
  accumulated stock and cleared WonderBuilt, skips spending/reselection and
  leaves DefenderLost unchanged. The human threshold uses the raw rule, not
  the AI factor.
- Then acquire the wonder. In nonnetwork modes, succeed only if still Unbuilt,
  publish location=city and set WonderBuilt. In network modes call the acquisition
  operation below, retaining its full result as acquired. If acquisition fails,
  discard both wonder and improvement; if owner is AI, call setProduction(city,99).

For a retained AI spaceship-group item, query launched again. If launched,
request production99. Otherwise call V07 choosePartGroup(owner, currentGroup).
A negative result requests99. A nonnegative result directly changes the city's
production to that returned group, recomputes cost from that group's unsigned
rule and live shieldRows, and completes only if current stock meets that new
cost. Do not create the original group as a fallback. Human parts and all other
retained items go directly to completion effects.

### Wonder acquisition operation

Set the WonderAcquire busy flag. In nonnetwork mode or on the host, succeed
only if the raw location is Unbuilt. On success publish location, set the city's
WonderBuilt flag, call setImprovement(city, improvement, present=true), and set
stock=0 when captured owner is currently human or improvement>34. In particular,
every nominal wonder acquisition resets stock. The cost argument is not used
for this host-side stock change. On network success broadcast a state diff and
flush, then clear the busy flag. An already claimed wonder returns false after
clearing busy without those success effects.

On a client, clear busy **before** sending the request; set acknowledgement to
Pending, send owner/city/wonder/cost/improvement, and poll until acknowledgement
changes or the F01 elapsed-time check reaches3600 ticks. Timeout logs and returns
false, with no invented local acquisition. On acknowledgement, drain the normal
and priority message queues, returning the live acknowledged result after the
drain. Network child mutations remain committed. N02/N05 own message identity,
authority and receipt; this operation does not fabricate a host result.

### Completed improvement or wonder

If retained improvement=None, do nothing here. Otherwise:

1. If windowShown, draw production. For Manhattan, visit civilizations1..7
   ascending and set reputation=clampAB(trunc((reputation+1)/2),0,6).
   Outside autoplay, visit them again: each alive human who knows this city or
   currently owns it receives the Manhattan notice. The local human gets the
   explosion sound and sprite notice; nonlocal humans receive network text only
   in network modes. Use live masks/known-to/owner as tested at each iteration.
2. If acquired=false, call setImprovement(city, retainedImprovement, true), then
   subtract s16(retainedCost) from stock with signed16 retention. Successful
   network acquisition already performed its storage operation and skips this.
3. For a currently AI owner and ordinary improvement<35, if this is Palace
   decrement the live stored production first, then call setProduction(city,99).
   This route keeps the remainder stock. All other cases run the completion
   announcement operation below.
4. For a spaceship group, call V07 addPart(owner, group). If owner is currently
   human, spaceship is not launched and addPart returned negative, supply the
   part name and issue BadSpace. Earlier cost/announcement effects are retained.
5. For Palace, scan all live cities ascending belonging to captured owner and
   clear their Palace, including this city. Set this city's Palace again and
   publish its x coordinate as the civilization's capital-x hint. Outside
   autoplay supply city and civilization-adjective text, broadcast in network
   modes, and show MoveCapital. This code does not publish a capital-y hint.
6. Execute the selected wonder's completion child: Darwin completes research
   twice, sequentially; Leonardo upgrades units; Eiffel applies its diplomacy
   effect; Apollo invokes [W03's publication](W03-visibility-and-exploration.md).
   [C08](C08-buildings-and-wonders.md#leonardo-upgrade-operation) owns Leonardo
   and [Eiffel acquisition](C08-buildings-and-wonders.md#eiffel-acquisition-operation);
   [V03](V03-research-and-technology.md#9-completing-research) owns Darwin's research
   children. W03 owns Apollo's publication.
7. If live windowShown, draw production, then improvements without regating
   between the draws.
8. If a wonder completed, scan live cities ascending, excluding the completed
   city, selecting currently AI owners still building that improvement. Request
   production99 for each. Then request cached-mode recalculation for the
   completed city. The caller does not perform a new full production pass.

### Completion announcement and next item

Capture announce=(localHuman=owner or retainedImprovement>34), then force false
in autoplay. If owner is currently AI, publish city known-to=all civilizations.
Supply item name as argument1, civilization adjective as argument2, and the
completion wording selector as argument3. Default wording is Built; Cure for
Cancer, Women's Suffrage and the named special-wonder group use the mapped
alternate labels in Appendix A.

When retained announce is true, play the relevant completion sound: wonder
sound for a wonder, spaceship-construction sound for a part, otherwise the
building-specific sound or default building sound. These precede the dialog.

If captured owner equals live localHuman and autoplay is off, broadcast built
text in network mode only for improvement>34, then invoke a **forced** Built
city notice unless ImprovementBuilt messages are disabled. Otherwise, if
announce is true, supply city name, broadcast built text in network mode, and
show a standalone Built2 dialog with the selected sprite and zoom8. The latter
does not use the ImprovementBuilt preference gate. When announce is true,
there is a wonder, owner is local human, autoplay is off and WonderMovies is
enabled, invoke its movie afterward.

If owner is currently human, publish completedImprovement=retained improvement.
If acquired=false, set stock=0, discarding excess. Finally, if Autobuild is set
or owner is currently AI, request production99. Otherwise retain the current
production field; completion does not automatically choose a new manual human
building. The caller then performs the later effects above.

### Unit readiness, upgrading and creation

Capture unitType from the signed production field. If its obsoleteBy is
nonnegative and owner knows it, scan type slots0..61 ascending for the first
type whose prerequisite equals the original type's obsoleteBy and whose role
matches. For a currently human owner, supply old/new names and invoke Upgraded
with the replacement sprite. Retain the selected type through that callback,
store it as the production item, and stop scanning. There is no replacement
availability, domain, cost, strength or government check, and no upgrade chain
loop in this call.

Compute `cost=s32(signed typeCost*shieldRows)`. If cost>live stock, return to the
common tail. Otherwise clear WonderBuilt, draw production when windowShown,
and test the Chieftain exception: Settler role, current size=1 and Chieftain
returns from the **entire production routine**, before spending or creation.

Subtract s16(cost) from stock. For human owners or any role except AI Diplomacy,
call U01 create(type, capturedOwner, live city coordinates). A negative result
returns to the common tail: **do not refund stock** or execute the population,
commodity, human stock-reset or per-unit notice steps. Thus ordinary human
failure can retain an excess remainder that successful human completion would
discard. Creation owns its own failure notices and partial effects.

For a successfully created unit, apply Veteran rules in this order:

1. Diplomacy role under live Communism becomes Veteran, bypassing subsequent
   branches and their disposable-unit tests.
2. Otherwise, for Land domain **or** classic combat rules (NormalCombat off),
   require Barracks or Sun Tzu effect, signed role<6, and no
   DestroyedAfterAttack flag.
3. Otherwise, Sea domain requires Port Facility or Lighthouse effect and no
   DestroyedAfterAttack flag.
4. Otherwise, Air domain requires Airport and no DestroyedAfterAttack flag.

For Sea Transport role, set its counter to `255-(cityIdentifier mod64)`.
AI Diplomacy skips this generic creation block and is created later, after the
window support/info draws, as specified below.

### Settler population consequences

After successful generic creation, for Settler role:

1. If live city size=1 and captured owner's city count=1, return from the entire
   production routine. The settler exists, stock has been spent, and size remains1.
   This differs from the earlier Chieftain refusal, which created nothing.
2. For size1 with a currently human owner, supply city/unit names. If autoplay
   is active **or** GhostTown popup returns0, delete the created unit with bare
   U01 deletion, refresh=true, and return from the entire routine. No refund.
3. Decrement live city size using signed8 storage. If resulting size<1, capture
   coordinates, prepare city/unit/Built text, delete the city and check captured
   owner for elimination. If not eliminated, call U01 create again at captured
   coordinates. Ignore that creation result; if UnitBuilt messages are enabled
   and notifyEnabled is nonzero, show Built. Set windowShown=0 and
   notifyEnabled=0, redraw the captured tile, and return from the entire routine.

The second creation is a real ordered call after C01 deletion and its unit
cleanup; do not collapse the two creations into an abstract relocation. A
failed second creation does not restore the city. Population wrap uses the
same signed8 contract as C05 and is not an excluded memory overwrite.

### Successful unit continuation

If live windowShown, draw supported units and the information panel, with no
second gate between them. Then split on captured owner's live human status:

- AI: for Trade role call [C07 cargo selection](C07-trade-and-commodities.md#ai-trade-unit); for Diplomacy role run the
  creation operation below. If signed attack>=99 and the owner's signed active
  count for this type equals1, set civilizations1..7's last-contact-with-owner
  turns to the Never sentinel. Request production99. The AI route preserves
  the remainder stock for subsequent spending.
- Human: for Settler or signed role>5, supply the type name. Trade role runs the
  commodity dialog below. Other such roles issue the UnitBuilt sprite notice
  unless disabled, with the role-specific wording. Ordinary military types
  do not get that notice here. Set stock=0; if Autobuild, request99. If live
  windowShown, draw production. Continue to the common tail.

The source contains a second Trade-role counter branch inside the non-Trade
arm. With stable rules and no intervening child, that branch is unreachable;
do not add its random draw to the ordinary non-Trade path.

### AI diplomat placement

Initialize bestDistance=32767, tieCount=0 and targetCity=None. Visit live cities
ascending whose owner differs from captured owner and whose treaty with it
has neither Ceasefire nor Peace. For each, call U01 nearest-unit search for
captured owner, excluding no unit. Transform its shared distance to the wrapped
absolute value of distance-3 **even if the search returned None**. If no unit,
continue. If target city has TechStolen and transformed distance<4, set it to3.

Require transformed distance<=bestDistance. On equality, increment tieCount;
if its previous value was positive, increment again and consume one primary
primary-law sample modulo the resulting count. Continue only for remainder0. Query the
nearest unit's terrain and require it not to be Ocean, then require target-city region equal
to the nearest unit's current region. Reread shared distance after those queries.
**Only when bestDistance<that live distance**, set tieCount=1 and replace
bestDistance. Retain target city/unit after successful region comparison.

That last comparison is the original's unusual direction, confirmed in its
instructions. With ordinary unmutated query state the earlier <= gate and
later > update cannot both hold, leaving bestDistance32767. Do not repair it
to a normal minimum search. Even if a query raises the shared distance and
permits an update, bestDistance increases; no lower best value is established
by this operation's normal sequential inputs.

The retained continuation tests bestDistance<4, then, only if true, consumes a
primary-law sample and accepts draw mod10<=difficulty to create at target unit's
live coordinates. Otherwise it creates at the city's live coordinates. Thus
the normal route creates at home and consumes no placement-chance draw; tied
32767 search values can still consume the earlier tie draws. Under Communism,
the original then sets Veteran without checking creation success. For a
negative creation result, omit that invalid unit write under X01; the parent's
remaining valid AI completion/spending continuation is unchanged. No sentinel
unit or adjacent-memory damage is required.

### Human caravan commodity dialog

Call [C07's shared human cargo selector](C07-trade-and-commodities.md#human-trade-unit)
with this city, captured owner and created unit. C07 owns dialog/retry/report
order, cargo and supply writes, HelpPressed and failure boundaries. Resume the
human completion path above only after that operation returns.

## AI purchase phase

Before planning, if DefenderLost is set, or (only if not set) the tile-unit-owner
query is negative, request production99. Capture magnitude of the new current
nonunit item, or0 for a unit. Read **unsigned** rule cost in this phase, including
unit cost, and retain `required=s32(cost*shieldRows)`. This differs from signed
unit completion and human purchase cost reads. Query land-region; a negative
answer selects regional policy0, otherwise use owner's stored regional policy.
[A01](A01-ai-strategy-and-assessment.md#5-regional-presence-and-policy) owns its
assessment-time producer; this spending operation does not refresh the census.
Initialize budget=0 and apply these rules in order; later assignments replace
earlier values:

1. For policy0,1 or4, retained net!=0, live unit production and signed role equal
   to policy, set budget=arithmeticShiftRight(live gold,6).
2. For a wonder magnitude, identify its seven-wonder group. If owner's count is
   zero or below the supplied human-group minimum, set budget=gold>>6. Otherwise
   use gold>>8 if owner's count>human maximum+1, else gold>>7. If live gold>=2500,
   double budget with signed32 wrap. Human min/max group summaries are inputs;
   do not recompute them here.
3. For magnitude35..38, scan human civilizations1..7 ascending. If any has a
   spaceship, set budget=gold>>7 and stop the scan. Then call the AI space-race
   threat query; a true result doubles the budget. This includes Capitalization.
4. For Disorder, nonunit magnitude and nonzero stock, set budget=gold>>3.
5. Query tile-unit owner again. If negative or DefenderLost, live item is a unit
   and stock!=0, set budget=gold>>3.
6. For Palace with nonzero stock, set budget=gold>>3.
7. For the Nuclear Missile **identity**, active type count=0 and stock!=0,
   set budget=gold>>4. This test does not generalize to every attack>=99 type.
8. If live gold>2000, add gold>>9 to budget with signed32 wrap.

Set `budget=clampAB(0,budget,s32(required-live stock))`: **0 is the value,
budget is the lower bound**, not the other way around. Do not reorder the
arguments or reverse negative bounds. Add s16(budget) to stock, retaining
signed16, before calculating the treasury charge:

```text
refund = trunc(s32(unsignedDifficulty*budget)/10)
refund = trunc(refund / s32(8-powerRank[bestHumanRank]))
if gold[worstHumanRank] < live ownerGold:
    if ownerGold >=1000: refund = trunc(refund/2)
    if ownerGold >=2000: refund = 0
ownerGold = s32(ownerGold - s32(budget-refund))
```

The best/worst selectors are used directly as civilization indices; keep that
distinct from C05's rank entry7. A zero divisor stops after the stock update,
before the treasury charge or almost-wonder notice. There is no affordability
clamp or prohibition on a negative budget for accepted off-nominal stocks.
For example, a remaining cost of -5 makes the reversed-bound clamp produce -5,
reducing stock and potentially increasing treasury. This is defined scalar
behavior, not an out-of-bounds overwrite to exclude.

Finally, if live production is a wonder, calculate its requirement from current
unsigned cost and shieldRows. If requirement<=stock+Shields-shieldSupport, raw
location remains Unbuilt, and autoplay is off, supply owner/wonder names and
issue AlmostWonder with the improvement sprite and zoom8; broadcast in network
modes. There is no one-time notice flag here and no Disorder override of the
Shields-support expression. The production parent's final DefenderLost clear
occurs only after this operation returns normally.

## Discriminating conformance cases

Unless varied, use a valid flat map, city0 at (8,6), owner1 human, King difficulty,
raw shield rule10 and shieldRows10, no relevant improvements/wonders, a single
valid unit type0 with signed cost4 and ordinary attack role, no busy network
operations, and stable child-query state. Observe full state and significant call dependencies;
the selected result alone does not establish equivalence.

| Case | Required result |
| --- | --- |
| Unit prerequisite Disabled versus None | Disabled rejects without querying an advance; None passes that gate without querying an advance. |
| Defend unit weaker than Musketeers, Gunpowder known | Rejected even if its own obsoleteBy is None. Equal defense does not trigger this rejection. |
| AI fast Land unit versus an obsolete stronger same-role/domain alternative | Alternative can exclude it: the dominance scan does not test the alternative's obsoleteBy or full build eligibility. Exact attack/defense ties do not exclude. |
| Sea unit, city with only Coastal | Rejected; require OceanAccess too. A negative city argument bypasses both flag requirements. |
| Destroyed Manhattan, Nuclear Fission known, attack99 | Pass the nuclear gate; Manhattan need only be not Unbuilt. Destroyed wonder itself cannot be rebuilt. |
| Bank, Marketplace present but its prerequisite unknown | Rejected. Negative city argument passes after Bank's own prerequisite, without checking Marketplace. |
| HydroPossible, Factory and Nuclear Plant, Hydro choice | Allowed unless another gate prevents it; Hydro is the Nuclear-plant exception. Solar ignores Hoover/other-plant exclusions after its own prerequisites and industrial-facility gate. |
| Snapshot category Units, stock101,50% penalty | Nonunit preview51; changing current stock to51 does not change a later unit preview101. Chieftain skips the penalty. Snapshot stock-1 instead produces-32768 on a category change. |
| AI changes nonwonder to wonder with stock9 | Stock becomes4 through truncating halving. Human snapshot penalties are not used. |
| AI zero-stock first wonder in group; all human group counts nonzero, King, raw cost3 | Grant9 shields, not90. Started flag is set even if autoplay suppresses its notice. |
| Unit purchase, stock10 and treasury1000 | Remaining30, price105; confirmed purchase leaves treasury895 and stock40, republishes the snapshot, and creates nothing yet. Stock0 instead gives doubled price320. |
| Purchase modal changes treasury to1, raw rule to12 and selection to another city | Deduct retained105 from captured owner, producing-104; set new selected stock48 from captured cost4 and fresh rule12. No second affordability test. |
| Disorder, unit versus ordinary building purchase | Unit gets Complete2 and no purchase. Building remains purchasable. Capitalization returns without the purchase dialog. |
| Busy map lock on Buy/Change | Publish corresponding deferred work and fresh selection arguments; no recalculation or modal purchase/change action. |
| Sell one Granary with raw cost3 | Credit30, remove facility, set sold flag. Next sale blocks unless reveal-map and scenario mode are both active. Palace has the same two-mode exception to its separate prohibition. |
| Rejected category-change confirmation while Autobuild was set | Production/stock unchanged, auto flags already cleared; full trailing recalculation and draws still occur. Snapshot unchanged. |
| Human unit, stock38, Shields5, support1 | Accumulate to42, spend40, create one unit, then reset stock0. Ordinary military completion has no UnitBuilt notice. |
| Same ready human unit but actual creation refuses at capacity, stock45 | Stock becomes5; no unit, no refund, no successful-human reset to0. Continue to common tail. |
| Size1 Settler on Chieftain | Clear WonderBuilt and optional production draw, then early return before spending or creation; DefenderLost not cleared. |
| Size1 Settler on King, owner's only city, stock45 | Create unit, spend40, retain size1 and stock5; early return skips common tail. |
| Size1 Settler, another owner city exists, GhostTown result0 | Create then delete unit; retain city/size and spent stock. Do not refund. |
| Human Trade unit, supplies[0,negative,3], choose third slot | Append options0,2,Food; set unit commodity3 and third supply-3. Commodity0 remains0 if chosen. HelpPressed sets zoomRequested after acceptance. |
| AI Communist diplomat creation fails | No valid unit; spent stock remains. Omit invalid Veteran write, preserve sentinel/adjacent state, and continue the valid parent tail. |
| AI wonder ready, human city building same wonder with full raw-cost stock | AI returns without acquisition, spending or DefenderLost clear. A human short of the raw threshold does not win this gate merely because the AI-adjusted threshold is smaller. |
| Manual human ordinary building, stock35 and cost30 | Acquire facility, announce, publish completedImprovement, reset stock0; production stays that item. AI ordinary completion retains5 and requests next production. |
| Already present ordinary building, stock35 | Eligible BadBuild warning; no new acquisition, deduction or completedImprovement publication. Manual stock remains35. |
| Darwin completion | After acquisition/announcement, invoke research completion twice in order. No combined “two technologies” shortcut before the first child's effects. |
| Host network wonder acquisition | Publish location, flag, storage child and stock reset before diff/flush; duplicate attempt returns false. Client timeout does not publish a local wonder. |
| AI spaceship group's chosen replacement costs more than current stock | Store the replacement production, skip part construction, and continue the common tail; do not build the originally affordable group. |
| AI gold640, policy matching role0, nonzero net, stock10, cost40 | Budget10, stock20; with rank divisor8 and King refund0, treasury630. This is unrelated to human quadratic pricing. |
| Same AI input but stock45>required40 | Reversed-bound clamp yields budget-5, stock40 and treasury645. |
| Same ordinary AI spending, powerRank[bestHumanRank]=8 | Stop at division after stock increases, leaving treasury unchanged; no AlmostWonder or final DefenderLost clear. |

Additional fixtures should cover sale/selection callbacks, complete property
read ordering, snapshots refreshed by disband/window callers, every special
building gate, unit stock/population overflow, failed secondary Settler creation,
caravan report/cancel loops, AI tied-distance draws, part failures, client receipt
and queue drain, all spending overrides, and consecutive city/civilization turns.
These are finite conformance obligations, not a requirement to corrupt memory
on rejected indices or failed allocations.

The following original representation/provenance sections retain diagnostic
supplied-byte and stop cases. X01's repaired profile controls implementation and
required tests, including fresh-game isolation and the named C07/A02/C05 repairs.

## Appendix A — Representation, messages and original bugs

| Original encoding/detail | Logical meaning or validation use |
| --- | --- |
| Production signed BYTE: nonnegative unit, negative improvement | Nominal units0..61; ordinary buildings1..34; spaceship groups35..37; Capitalization38; wonders39..66. Requests>=99 invoke the chooser; recommendation999 means no advisory recommendation. These sentinels serve different callers. |
| Production categories0/1/2 | Units; improvements1..34; all nonunit identities>=35. The classifier uses full signed arguments, not just the production byte. |
| Snapshot DWORD category/stock | Category at `006a65a4`, signed stock at `006a6528`; snapshot published in that order. Only low16 stock participates in the percentage product. |
| City stock WORD; type-building count BYTE | Signed16 stock stores and modulo256 counters. Per-type construction counts begin at civilization offset340 and contain62 entries. Off-array stores are excluded bugs. |
| Improvement/rule costs BYTE | Improvement cost unsigned everywhere here; unit cost signed for completion, display and player purchase, unsigned in AI spending. |
| Dialog option IDs | Unit type directly; nonwonder improvement+62; wonder+101 (equivalently improvement+62). Action extra0 Choose,1 Automatic,2 Encyclopedia,3 Freebie. AutoMode1 UnitsOnly,2 ImprovementsOnly. |
| Deferred kinds7/8 | Buy/Change; original control ID and two live selection-derived arguments retained. |
| Treaty mask6 | Ceasefire or Peace excludes an AI diplomat target; a generic allied/war predicate is not substituted. |
| Wonder notice flags1/2 | Started/Abandoned per civilization and wonder; the switch path does not set these flags. |
| Completion wording label IDs | Default187; Cure for Cancer184; Women's Suffrage185; Marco Polo, Sun Tzu, King Richard, Leonardo, Darwin and Magellan186. Diplomacy-unit wording182; ordinary Settler wording187; caravan uses its own dialog. P04 owns localized text lookup. |
| Building completion sounds | Barracks5, Aqueduct4, Bank47, Cathedral11, University55, Stock Exchange69; other ordinary building2; spaceship/wonder use their named construction/new-wonder sounds. Purchase uses positive sound104. P10 owns playback policy. |
| Advisor report IDs | Change refreshes Cities1 then Units2. Sale refreshes Trade5, Attitude4, Science6. P07 owns report content and open-window gates. |
| AI diplomat Veteran write at `004ed03e` | A failed creation can return-1, yet the Communist path writes its flags. Exclude the invalid read/modify/write; do not reproduce changes to preceding game-state bytes. |
| Production advice's original80-byte section scratch | Long names can overflow it. This is excluded corruption. Primary OpenCiv2 truncates; independent BF-051 expands its buffer. P06 must state its safe supported-name/section policy without treating either repair as original successful overflow behavior. |

All masks, offsets, byte layouts and ABI return residue belong to validation
and F06 persistence, not the cleanroom storage design. The direct stock/counter
wraps are different: their numeric results are specified above. A known scalar
result does not authorize a later invalid index.

## Appendix B — Sources and validation

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`. The following referenced bodies were
verified unchanged in the read-only reference checkout used for this document:

| Pinned source | Relevant entries/continuations |
| --- | --- |
| [City routines](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp) | Set production `00441b11`; improvement eligibility `004c03ae`; acquire wonder `004e7270`; category/snapshot/switch `004e7458/004e7492/004e74df`; production parent `004ec3fe` and its extracted private continuations; sale `00505d3d`; estimate `00509ac0`; Buy `00509b48`; Change `0050a473`; snapshot callers and city-turn join. |
| [Civilization routines](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp) | Production reset `00442541`; unit eligibility `004bfe5a`; wonder eligibility `004c02d8`; report identity corroboration. |
| [Unit routines](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp) | U01 creation/deletion and the disband stock/snapshot caller. |
| [Enums](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h) | Identity, flag, sound and message-preference mappings. |
| [Reports](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/reports.cpp) | Open-report refresh boundary and report identities. |

Independent reconstruction was read at `civ2` revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, including `src/city.cpp`,
`city_production_rules.cpp`, `city_production_update.cpp`,
`city_production_dialog.cpp` and `city_production_turn.cpp`. It corroborates
signed/unsigned cost distinctions, saved-input versus live state, the retained
AI diplomat comparison, creation-failure continuation, the AI clamp's argument
order, refund division timing and player dialog retargeting. Its adjacent-byte
counter/record adapters are evidence only; X01 excludes their invalid writes.

Read-only original disassembly was inspected for production, switching,
purchasing and eligibility in `Civ2/civ2.exe`, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`:

| Instructions | Decisive observation |
| --- | --- |
| `004ecf34`–`004ecf4e` | AI diplomat best-distance update is skipped when best>=current; it is not a normal minimum update. |
| `004ed026`–`004ed03e` | Communist Veteran read/write uses the creation result without a negative-result guard. |
| `004ed8a7`–`004ed8af` | A fully funded human competitor makes the AI wonder producer jump to the parent's early exit. |
| `004eec0d`–`004eec1b` | AI clamp passes value0, lower bound budget and upper bound remaining; the reversed-argument behavior is intentional compatibility evidence. |

No original executable was run for this document, and no saved analysis,
source or original assets were changed. The disassembly establishes the listed
local decisions, not complete runtime conformance.

The finite host fixture adds **22 source bodies**—14 original routine entries
and eight extracted private continuations—to the previous146 bodies. It replaces
the production, choice-application, build-snapshot and eligibility adapters with
the actual scoped source. It retains C04's corrected whole mood counters and
C05's explicit supplied-coordinate/bounded-stop adapter for famine redraw.

**156 C06 assertions passed** in optimized builds and under AddressSanitizer
and UndefinedBehaviorSanitizer. Cases exercise eligibility, snapshots/counters,
AI wonder switches, purchase/sale modal changes, production-change confirmation,
ordinary unit and building completion, Settler exceptions, commodity selection,
failed creations, network acquisition/timeout, wonder races, AI spending and a
real city-turn parent joining C02/C03/C04/C05. No invalid output write is required
or executed. The failed AI diplomat Veteran write is explicitly omitted; the
refund's zero-divisor path uses a stop adapter after its original prior stock
effect. Host pointer carriers and typed copies are not a native process model.

Declared child adapters remain for A02 scoring/recommendations, C07 AI commodity
choice/reports, C08/V03 effect and advance providers, V07 spaceship logic and
special-wonder research/upgrade/diplomacy/publication children. P04/P06/P07/P10
dialogs, advice rendering, sprites, audio, selected-window state and network
transport/receipt are controlled fixtures. U01 creation/deletion, C01 city
deletion and the city calculation/turn bodies are actual source. Cases do not
claim arbitrary callback mutations are produced by the actual native UI.
The tests and reference source remain external research material, not cleanroom
implementation source imported into this repository.

[C07](C07-trade-and-commodities.md#appendix-b--evidence-and-validation) owns
the shared human cargo dialog and adds actual AI selection, report, commodity
refresh and delivery bodies to finite source cases, including C06 regressions.
[C08](C08-buildings-and-wonders.md#appendix-b--validation-and-remaining-joins)
specifies and exercises actual storage, active effects, upkeep and acquisition
children in focused cases; the production-parent join remains to be exercised.
[A02](A02-ai-city-production.md) specifies the full chooser, including
recommendation outputs, mutating surveys, retained read boundaries and final
choice. Its finite checks use eligibility/recalculation adapters; the joined
C06 spending/eligibility and production-choice path remains to be exercised.
V03/V07 must also join research and spaceship outcomes. S03/P04/P06/P07/P10/N02/N05
own real cheat, window, message, presentation and receipt behavior. F04 needs consecutive human
and AI turns with the real children; X02 must retain full-state and call-trace
cases, scalar wrap, supplied-read stops and safe excluded-write behavior.
These finite checks do not establish complete original-program or whole-game
conformance.
