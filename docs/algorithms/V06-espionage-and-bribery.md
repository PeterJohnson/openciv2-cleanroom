# V06 — Espionage and bribery

**Required profile:** [X01 fresh-game isolation and bug repairs](X01-classic-compatibility-contract.md#fresh-game-isolation-and-supported-bug-repairs)
and [behaviorally significant ordering](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
override conflicting procedures and examples below. Retention applies within a
game or to explicit imported inputs, not across new games. Confirmed original-bug
stack/backing/stop cases are optional diagnostics; use documented safe repairs.
Only real effects and callbacks impose ordering; test adapters cannot invent them.
Unspecified safe continuations remain explicit gaps, not required arbitrary bytes.

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch3**. This specifies city/unit diplomatic missions, menus and
fallbacks, technology theft, sabotage, poisoning, planted nuclear devices,
incitement, bribery, chat interception, capture/escape and espionage incidents.

[U02](U02-movement-and-zones-of-control.md) admits the movement interaction and
interprets the unit-mission return; this document does not add a second movement
or legality check. [U01](U01-unit-lifecycle-and-stacks.md) owns stacks, destruction,
relocation, exhaustion and unit visibility. [C01](C01-city-lifecycle-and-ownership.md)
owns weighted city searches. [C08](C08-buildings-and-wonders.md) owns building
presence/mutation and active wonders; [V03](V03-research-and-technology.md) owns
knowledge, grants and captured-technology selection. [C09](C09-city-capture-and-revolt.md)
owns the accepted incited transfer, [U07](U07-nuclear-attacks.md) the nuclear action,
[V04](V04-diplomacy-and-treaties.md) treaty/attitude/war primitives and
[V02](V02-governments-and-tax-rates.md) forced government change.
[A07](A07-ai-special-unit-missions.md#3-diplomat-and-spy-city-targets) owns outer
AI city-target choice, while this document owns mission selection and fallbacks.
A08 owns stance evaluation, and
[V07](V07-spaceship.md#8-race-arrival-reset-and-loss) spaceship-race assessment.
Their actual child effects are required; they are not success-shaped no-ops.
P04/P06/P07 and N02 own presentation, city/intelligence windows and network services.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## 1. State and input domains

Use A for the actor's owner and B for target owner. A directed relation A→B is
separate from B→A. Human status is membership in the human set, not equality
with local human. Autoplay and multiplayer mode independently gate branches.
The city controller captures A, B and the initial unit type before setting
Contact, then captures whether the unit's **then-live** type is the fixed Spy.
Other diplomatic-role units are not automatically spies. Its shared last-mission
value changes on each dispatch, including fallback dispatches.

Unit state includes type/owner, live token, position, damage, movement spent,
Veteran, SpyOnMission, auxiliary timer, target counter and home. City state includes
owner, founder, size, production/stockpiles, TechStolen, Investigated, Disorder,
buildings and knowledge. Civilization state includes treasury, government,
subversion counter, advances, attitudes, treaties and last contact. Rule inputs
include unit role/cost/prerequisite, ordinary-building names and prerequisites,
spaceship-part prerequisites, and movement multiplier. Local/native services
supply dialog results, network answers, elapsed ticks and callbacks.

[F01](F01-determinism-and-arithmetic.md) supplies reference expression results,
truncating division, signed remainder and primary draw R(). These mission paths
use the primary stream, including calls spelled differently in the reference;
there is no secondary range draw here. Preserve the conditional decision sites,
arguments and reuse; independent raw sample generation need not follow source order.
Movement, damage and timer additions retain their byte values; the counter is
read as signed where specified. City size and unit rule cost use signed-byte
interpretation here. Subversion and contact counters retain their F02 widths.
Prices are not universally clamped positive or limited to the current treasury.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

Nominal inputs follow [F02](F02-game-state-and-invariants.md), valid collection
indices/rules and stock dialog replies. Menu gating is not universal helper
validation: direct/event/custom-resource calls can reach different paths. A
stricter importer may declare a narrower domain. Retain in-bounds arithmetic
wrap and caller effects on accepted inputs. Invalid writes and their corruption
are **excluded bugs** under [X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
even when an adjacent named field exists. Reject or stop before such a write,
retaining an explicit valid prefix. Uninitialized/out-of-bounds reads instead
require supplied evidence/backing or a bounded stop; a reconstruction's chosen
zero is not recovered original behavior.

Prompt names identify message/choice roles; sprites and numeric resource slots
are validation details in Appendix A. A notice remains a callback boundary even
when its answer is ignored. Unless explicitly captured, later fields/indices are
re-read after child operations. No mission promises rollback on failure.

## 2. Shared escape, capture and treaty confirmation

`tryEscape(unit,mode,victim)` captures owner as both its byte membership identity
and signed owner index. For mode>=0, first add the road movement multiplier to
the unit's movement-spent value. Only the fixed Spy type attempts escape:

1. Set odds=`2 + (mode<0)`. Double for Veteran. If mode>0 halve it.
2. If odds<2, draw R(); an even remainder modulo2 increments odds.
3. Roll0 without another draw if `odds==1 || signed32(odds-1)<0`; otherwise
   draw R() and take remainder modulo odds. A **nonzero** roll escapes.
4. On escape capture the unit's position and find the nearest city owned by the
   captured owner. If one exists, relocate there, exhaust moves, redraw old
   position then the city's live position. In network modes publish state and
   send those two redraws. Publish the destination city's name. If no city
   exists, survival still succeeds; do not invent a destination or exhaustion.
5. For mode>=0, show a human non-autoplay survivor notice: Bond007 for a currently
   nonveteran unit, Bond for a veteran. Local actor gets a popup; remote human
   in a network mode gets the corresponding text message. Then set Veteran,
   including AI/autoplay. Mode<0 does not promote or show this notice.
   Return0 after successful survival.

A failed Spy escape shows BondGlory only for a human owner, non-autoplay and
mode>=0. Local owner receives it; remote owner receives the notice only in
multiplayer mode **exactly3**, unlike the successful escape's mode>2 condition.
Then, for both a failed Spy and every non-Spy unit, invoke U01 destruction with
redraw/synchronization. If victim>0 and mode>=0, invoke the espionage incident
below for (captured owner,victim). Return1. Do not invoke that incident on
negative-mode capture attempts or on victim0/-1.

The ordinary victim domain is civilization0…7 plus the sentinel-1. Other
nonpositive signed counters also skip the incident and are not used to index a
relation here. A positive counter outside1…7 can pass through a successful escape
without use; after a loss it would reach an invalid incident relation read. Stop
before that read, retaining the movement/loss prefix, or reject such inputs at a
declared outer boundary. Do not mask the counter into a civilization slot.

For nominal inputs the resulting success tests are:

| Unit | mode<0 | mode0 | mode>0 |
| --- | --- | --- | --- |
| Ordinary Spy |R()%3 !=0 |R()%2 !=0 |First R()%2==0, then second R()%2 !=0 |
| Veteran Spy |R()%6 !=0 |R()%4 !=0 |R()%2 !=0 |
| Other unit |Failure, no draw |Failure, no draw |Failure, no draw |

Use F01's actual finite generator; do not replace these remainder tests with
an idealized probability distribution. A surviving negative-mode
attempt can already relocate/exhaust the Spy, while the parent continues the
mission against its retained target city.

`checkCaught(unit,B)` captures owner/type, calls tryEscape(unit,-1,B), and
returns0 on survival. On loss, show Nailed with the captured type whenever the
captured owner is human and autoplay is off, without a local-owner test; return1.
There is no incident or movement-multiplier addition in the negative escape child.

`confirmTreatyBreach(A,B)` returns0 immediately for an AI actor or when A→B
has none of Ceasefire/Peace/Alliance. Otherwise show Incident and return
`reply != 1`. A nonzero result means cancel the mission. No autoplay gate or
treaty mutation is added by this helper.

## 3. City mission admission, menu and fallback dispatch

Construct one dialog, stop active-unit blinking, capture A/B/initial type, set
Contact bilaterally, then capture isSpy from the live type. Use the human menu
only if autoplay is off, A is human and A equals local human; otherwise call the
fallback selector. The menu publishes actor adjective, live unit type/name and
city name, loads SpyMenu and reads SpyOptions. A failed section open exits the
controller. Read the seven ordinary option lines sequentially, hidden or shown:

| Choice | Availability in the ordinary menu |
| --- | --- |
| Embassy | Local→B Embassy absent and local lacks UN. Marco Polo does not hide it. |
| Investigate | Always. |
| Steal technology | TechStolen absent or isSpy. |
| Sabotage | Always. |
| Poison water | isSpy and city size>1. |
| Plant nuclear device | isSpy, A knows Rocketry then Nuclear Fission, and active Manhattan location !=Unbuilt. |
| Incite revolt | City lacks Palace. Democracy does not hide it. |

Embassy and Investigate append the special Spy annotation when isSpy. Additionally
offer Chat interception when mode>2, B is human, city has Palace and A→B SpyChat
is absent. This additional option is not restricted to Spy. Close the reader,
run modal and exit on a negative reply. The menu gates are not repeated for every
helper; accepted custom replies need the domains below.

The nonlocal/AI/autoplay selector starts Steal. With A→B Vendetta draw R(); if
`R()%3==0` and isSpy choose Poison. The draw occurs **before** testing isSpy.
A→B Peace resets Steal. On difficulty above King, or King with B.subversion>5,
when the choice is Steal, A→B Vendetta or War, and B is not Democracy, choose
Incite. Alliance resets Steal. Finally, if the city is TechStolen, not isSpy and
choice is Steal, choose Sabotage; if A→B has Ceasefire/Peace/Alliance choose
Embassy instead. A07's destination choice does not preselect this mission or
replace this controller's fallback when movement invokes the city-mission entry.

Publish last-mission=choice before each dispatch. Embassy can return a fallback
to Investigate; Steal with no available technology can fall back to Sabotage or
Embassy for an AI; unaffordable/near-capital AI Incite can fall back to Steal.
On fallback repeat dispatch with the same captured A/B/type/isSpy and dialog;
do not rebuild the initial menu. Other outcomes end and destroy the dialog.
Unknown choices have no mission body but still publish last-mission.

## 4. Embassy and city investigation

Embassy checks A→B Embassy. If already present set choice=Investigate and repeat
dispatch. Otherwise directly set only A→B Embassy, then play the spy sound if
local human is A or B. An AI actor destroys its unit with redraw, including an
AI Spy. A human opens intelligence(A,B), then plain-deletes a captured non-Spy
or adds **one** movement-spent point for a Spy. Do not call the escape helper.

For non-autoplay human B, publish A plural and show EnemyEmbassy locally when
mode<3 or local=B; otherwise send its text notification. This victim notice runs
after the actor's unit effect, using the captured initial type for its sprite.
Return without a generic treaty incident.

Investigation plays the spy sound if local=A/B. An AI actor destroys its unit
with redraw and does not set Investigated. A human opens the target city window;
a captured non-Spy then calls tryEscape(unit,0,-1), which normally destroys it.
A Spy instead adds one movement point only if Investigated is still absent.
Then set Investigated, re-reading the city after the window/child.
Notify non-autoplay human B locally or remotely under the same mode/local gate
as Embassy, with A plural and the live city name. This is a city-window visit,
not a universal W03 map exchange or technology grant.

## 5. Technology theft

First confirm treaty breach; cancellation stops without a draw. Draw R()%100
for a starting advance and visit100 indices cyclically. An index qualifies when
its prerequisites are not both Disabled, A lacks it and B knows it, in that order.
Record each qualifier. For each, query V07's spaceship-race predicate; stop at
this qualifier if no race, or difficulty below Prince and A is AI, or its identity
is a prerequisite of Structural/Component/Module, or it is Fusion Power. Otherwise
continue. Thus an active race can retain the **last** ordinary qualifier while
preferring the first qualifying space technology in cyclic order. There is no
general enabled/can-research filter.

If none qualifies: a human adds the road multiplier to movement spent and, if
not autoplay, shows NoSteal; end. An AI chooses Sabotage if A→B lacks all three
peaceful treaty flags, otherwise Embassy, and repeats dispatch.

With a candidate, initialize escape mode0. If TechStolen is absent and autoplay
is off and the actor is a human Spy, show StealSpecific. Negative reply stops.
Reply1 calls checkCaught; loss stops. Survival plays the spy sound only if local=A,
invokes V03's captured-technology chooser(A,B), calls tryEscape(unit,1,B), then
sets TechStolen **after** that escape/incident and ends. This targeted branch
does not use the earlier cyclic candidate or the defender detection scan.
Other replies continue ordinary theft.

If TechStolen was present, a non-autoplay human must reply1 to StealHard.
Then call checkCaught regardless of human/autoplay; loss ends, survival sets
escape mode1. Continue ordinary theft against the retained city even if the
capture check relocated the Spy.

Scan the city's current physical unit stack in U01 order for diplomatic-role
units, without an owner filter. Each such unit gets an independent R()%100;
detection threshold is20, or40 for fixed Spy, or60 for veteran fixed Spy.
Stop on the first roll below its threshold, retaining the detecting type.
No detection: publish A plural and the candidate's name; notify remote human B
only in network mode, non-autoplay and local!=B. For non-autoplay human A,
play sound and show Steal with the advance's current category/epoch icon. Grant
the captured candidate to A from B with ordinary V03 flags; set TechStolen;
then tryEscape(original unit,escape mode,B).

Detection instead exhausts the actor's moves and redraws the city, publishes
detector type and A adjective, shows Curses/sound for a non-autoplay human A,
and sends the remote-human-B detection notice under the same network gate.
Do not grant technology, set TechStolen, destroy the actor, call final escape,
or invoke an incident merely because a defender detected it.

## 6. Sabotage and poisoning

### Production or building sabotage

Confirm treaty breach. Build a captured production name from the city's current
unit or improvement production; its original fixed temporary can overflow with
accepted custom names, an excluded write bug. Draw R()%39, scan39 cyclic building
indices, excluding Nothing, Palace and SDI. Select the first installed building;
if none, target0 means production. C08's presence query is false outside the34
ordinary buildings, even though this loop visits0…38.

If non-autoplay human Spy and selected building>0, show SabotageSpecific.
Negative reply stops. Reply1 builds the target menu: installed identities1…38
except Palace, ascending, then production target0 with its captured name.
A negative target reply stops. Original nonnegative custom replies have no upper
guard; Appendix A distinguishes the reconstruction's admission repairs.
SDI target shows SabotageNo and stops. For any other admitted target, if the city
has Palace or target is City Walls, require reply1 to SabotageHard. Then perform
ordered capture checks: one always, another if a **fresh** Palace query succeeds,
then another if City Walls. Loss at any check ends immediately. The original
later SDI-specific extra checks are unreachable after the earlier SDI return
for an unchanged local target. Surviving this chain sets escape mode1.

With escape mode0 and nonzero city shield stock, choose odds2, or3 for a Spy,
then add1 for the unit's live Veteran flag. Draw R()%odds (the original skips
when odds-1<1, unreachable for these nominal assignments). Roll0 changes the
target to production; other rolls keep the chosen building. There is no roll
when stock is0 or after the targeted hard path.

Apply the following damage/presentation block **only if A or B is human**.
An AI-versus-AI sabotage still reaches final escape but performs neither of
these damage mutations:

- If local=A/B, play spy sound unless A knows Gunpowder and local=A, which
  selects the medium-explosion sound. The knowledge query precedes that local=A
  test inside this branch.
- Production target: zero shields, refresh that city window and publish the
  captured production name. Notify remote human B in non-autoplay network mode;
  show SabotageTwo for non-autoplay human A.
- Building target: clear that building through C08, redraw the city, publish
  its rule name and publish state. Notify remote human B with tile redraw,
  city-window refresh and SabotageOne text in that order. For non-autoplay human
  A refresh the window and show SabotageOne with the improvement sprite.

Finally call tryEscape(unit,escape mode,B), even after the AI-versus-AI no-damage
block. Production/building mutations are not rolled back by failed escape.

### Poison water

After treaty confirmation, city size<2 zeros its food stock; otherwise decrement
size. This helper does not recheck the menu's Spy/size eligibility. Reveal/remember
the city to A through W03, redraw it, publish city name and A adjective, send the
non-autoplay remote-human-B notice, then sound/WaterSupply for non-autoplay human A.
Capture B for the next call, draw R()%2 and invoke tryEscape(unit,that mode,B).
No ordinary city-turn recalculation or population-loss helper is substituted for
these direct size/food changes.

## 7. Planted nuclear device

Autoplay prevents this mission body entirely. Otherwise show MajorIncident and
require reply1; there is no additional human gate. Run three checkCaught calls
in sequence and, if the city has Palace, a fourth. Any loss ends. Each surviving
check can move/exhaust the Spy, but the city argument remains the mission target.

Call U07 nuclear action(A,target's live position,checkSDI=0), ignoring its return.
Then publish city name, A adjective/plural and begin PlantedNuke text. Set a local
identified flag0. Unless A's **live** government is Fundamentalism, choose odds2,
or4 when the unit's live flags have Veteran, draw R()%odds and identify the actor
on roll0. That draw occurs after the nuclear action and its possible unit/city
mutations; a backed inactive unit record is not silently replaced with entry
state. Identification appends variant2, sets the flag1 and scans civilizations1…7
excluding A, without alive/human/contact gates: adjust each other's attitude to A
by+100, then set War bilaterally(A,other) through V04. These are treaty setters,
not full declare-war/reputation operations.

Show the PlantedNuke notice with Manhattan's improvement sprite unconditionally
on the admitted branch. If B is human and mode>2, send its identified/unidentified
notice even when B is local. There is **no final tryEscape call**, movement charge
or veteran promotion after the device. The preceding negative-mode capture checks
and U07 action account for the actor's changes; do not add the normal mission
escape pattern by analogy.

## 8. Inciting a revolt

B's Democracy immediately prevents incitement; show NoRevolt only for non-autoplay
human A. Measure distance to B's capital using the actor unit's **current**
coordinates, not the city's. Use the shared capital-distance provider described
below. Distance<2 silently ends for a human; for an AI change choice to Steal and
repeat dispatch.

Otherwise cap distance at10 when B's government is Communism and distance>9.
If target has Courthouse, halve distance after that cap. Compute
`cost = signedCitySize * ((B.gold+1000)/(distance+3))`, with signed32 retention at
each operation. Negative cost becomes30000 **before** discounts. Halve cost
separately for Disorder, no unit owner on the target tile, and founder=A, in
that order. A captured Spy gets a discount of cost/6, or cost/3 when its live
Veteran flag is set. There is no minimum price or final positive cap.
Capture has-treaty from A→B Ceasefire/Peace/Alliance.

A human is admitted to the offer regardless of balance. An AI requires
`doubleCost=s32(cost*2)` nonnegative and <=A.gold, and no captured treaty;
otherwise change choice to Steal and repeat dispatch. This reserves an eligibility
margin but does not make the AI pay double.

For a non-autoplay human, load Dissidents with city name and cached cost. Only
when cost<=A.gold open DissidentOptions; failure ends. Read all four lines:
append cancel0, ordinary payment1 when no treaty, incident payment1 when treaty,
and double payment2 when treaty, cost*2<=A.gold and cost<15001. Close the reader
and show modal. If cost is unaffordable the loaded dialog still runs without
these appended options. AI/autoplay instead sets choice1.

For choice<=0 end. Every positive result, including an admitted custom value,
subtracts `cost*choice` from live A.gold and adds2 to A.subversion. If local=A,
redraw status. Call tryEscape(unit,0,0), ignoring whether the unit survives.
Choice1 then invokes espionageIncident(A,B) explicitly; choice2 instead directly
adds B→A Vendetta and changes the city's founder to A. Other positive choices
perform neither special action. Play spy sound if local=A/B, then invoke C09
incitedTransfer(city,A,choice). This complete transfer occurs after payment,
spy fate and any incident/government changes. No failed-escape refund occurs.

### Shared capital distance

This document owns `distanceToCapital(owner,x,y)`, shared by the two price paths.
Start result16. Scan live city slots ascending to the live upper bound; require
nonzero city token and matching owner, then C08 Palace presence. For each match
compute [W01](W01-map-geometry-and-spatial-state.md)'s `stepDistance` from
its live coordinates to(x,y) and replace result only when smaller. Return result.
Thus no Palace or every Palace at distance>=16 returns16; multiple Palaces use
the closest within that cap. It does not use C01's weighted-nearest-city query,
C09's central-city choice or a cached capital hint. Do not clamp a supplied
negative distance upward inside this helper.

## 9. Unit bribery and sabotage

### Bribery admission and price

`bribe(targetUnit,A)` captures its owner B and B's original membership byte.
If whole-stack count>1, return. Democracy of B prevents bribery. In that case
show Incorruptible only when autoplay is off and A is human; a remote human in
mode>2 receives a text message, otherwise show locally. An AI actor must know
the fixed Diplomat type's prerequisite. Human actors skip that knowledge gate.

Compute distance from target unit's live coordinates to **B's** capital.
Cap at10 when **A's** government is Communism and distance>9; this differs
from incitement's B-government test. Price is
`signedUnitCost * ((B.gold+750)/(distance+2))`. Negative result becomes30000.
Halve unless the target's role is Settle. An AI actor refuses a Trade-role unit
at this point. Publish B adjective and target's live type name.

Acceptance for a non-autoplay human publishes the cached price. If A is local,
show Desert, adding its affordable variant only when price<=A.gold, and require
reply1. A forced/custom reply1 is not followed by another affordability check.
For a remote A in network mode, send affordable/unaffordable request, set shared
bribe answer=-1 **after sending**, capture timer, then poll messages(1,0) while
elapsed<timeoutSeconds*60 and answer remains-1. Require the last read answer1.
A nonlocal human in a nonnetwork mode falls through without a prompt here.
AI/autoplay instead accepts only if A.gold/2>=price. No lower-bound check is added.

### Transfer and post-transfer continuation

In nonnetwork mode or on the host, perform these steps:

1. Subtract cached price from A.gold. Decrement B's live unit-type count,
   increment A's type count and increment A.subversion.
2. Set target owner=A, home=None, movement spent=0 and orders=None.
3. Find the nearest city **of any civilization** to the unit. Set its home only
   if that selected city is valid and currently A-owned; do not search again
   for a farther A city.
4. Mark the unit seen by **old owner B**, set its tile owner=A with synchronization,
   then publish a state diff with the final flag1. This path does not itself
   adjust civilization unit-total or military-total counters, relocate, reveal
   surroundings, pay B or consume the bribing unit.

A network client instead sends BribedUnit(target,A,cached price) to host, then
polls messages(1,1) until target's live owner becomes A or elapsed reaches3600
ticks. On timeout show ServerConnectTime and clear active-turn-loop. The helper
returns to its caller even after timeout; the following notices are not gated
on a newly confirmed ownership change.

After either transfer branch, play spy sound if local=A or captured B. If A is
human, redraw status when local=A; otherwise in network modes publish state,
send sound then status to A. If autoplay is off and captured B is human, publish
A plural. Local B gets ensure-visible and Deserted for the live target unit;
remote B in network mode gets state publication, possible-center and Deserted
text, in order. Then redraw target's live tile; network mode publishes state
and sends that tile redraw. Finally delay30 ticks if captured B is human and
local=B, independently of autoplay. No generic espionage incident is invoked.

### Unit-mission dispatcher

`unitMission(actor,target,A)` returns0 when target stack count>=2. With fewer
than2 units, directly call bribe and return0 if A is AI, actor's live type is
not fixed Spy, or the Normal Combat rule is off. Autoplay does not independently
suppress the following human-Spy menu.

Show SabotageOptions. Negative reply returns0; reply0 calls bribe and returns0.
Any other nonnegative reply performs unit sabotage: play small explosion sound
if local is target's live owner or A; publish target adjective/type and actor
adjective/type; show BlewItUp using the actor. In network mode notify a human
target owner even when local. Capture target position and animate explosion;
network mode broadcasts it. Query U06's remaining hit points and add
`remainingHP/2` to target damage with byte retention. Redraw, then in network
mode publish with final flag1 and broadcast redraw. Call tryEscape(actor,0,-1)
and return1 regardless of its fate. There is no treaty confirmation or generic
incident, and no automatic target deletion when damage changes. U02 uses this
return to decide whether the movement attempt stops.

## 10. Chat interception and mission expiry

On chat initiation, play spy sound when local=A/B. Initialize the local penalty
values for indices0…14 to
`[0,0,0,0,1,2,2,3,4,5,4,3,2,1,0]`.
Set actor timer to10 for a captured Spy, else5; add table[difficulty+9], then2
if its live Veteran flag is present. Read **B's stored attitude toward A** as
the next table index and subtract that table value, retaining the timer byte.
Capture this pre-random timer, draw R()%6 and assign captured timer+draw; do not
reuse a timer altered inside the random service. Set unit.counter=B, add
SpyOnMission, directly add A→B SpyChat and show ChatSpyStart unconditionally.
There is no immediate escape, movement charge or actor deletion.

For stock difficulty0…5, the difficulty addition is5,4,3,2,1,0. Attitude0…14 has
a defined table value. **Attitude15…100 is also an ordinary produced attitude,
but the original indexes outside the initialized table.** Require an explicitly
supplied original lookup value/backing, or stop after the preceding timer updates
and before that read. BF-289's zero penalty for these attitudes is a bounded
repair; it is not a recovered universal classic duration. This is a read issue,
distinct from the excluded out-of-bounds-write category. An alternative may adopt
and declare the repair or reject this unsupported continuation.

The unit reset owner decrements a SpyOnMission timer with byte retention and
acts only when it becomes exactly0. It clears SpyOnMission, clears SpyChat from
**reset-selector→signed live counter**, and invokes tryEscape(unit,0,counter).
It does not first reset movement or the timer as for an ordinary unit. A starting
zero timer wraps to255 instead of immediately expiring.
[U04 owns the reset scan and its specified safe handling](U04-unit-orders-and-activation.md#mission-expiry-bookkeeping-and-excluded-overwrite):
when the selector/counter is outside the relation collection, omit that write
and continue to tryEscape with the live signed counter. Do not remap the write
into another civilization. The escape child uses the counter domain in section2.

[V01's civilization removal](V01-civilization-lifecycle-and-civil-war.md#62-interrupted-spy-mission) uses its own scan and clearing direction: it clears
SpyOnMission and the eliminated-civilization→signed counter SpyChat before calling
tryEscape(unit,0,counter). Nominal interrupted missions can therefore clear the
eliminated civilization's self-relation. Do not replace the caller-selected
relation by actor→target merely because that is what chat initiation set.
N02/P07 own the message visibility/disclosure consumers of SpyChat and the
ongoing native chat UI; this flag is not map visibility or an embassy.

## 11. Espionage incidents

`espionageIncident(A,B)` first checks **B→A War**; if present, do nothing.
If B→A lacks Ceasefire/Peace/Alliance, adjust B→A attitude+20 and return.
Otherwise publish both plural names and execute these stages in order:

1. If B is human, return immediately for autoplay or A Fundamentalism. Otherwise
   local B or a nonnetwork game shows Pretext (no B→A Alliance) or PretextAllied.
   Reply1 to Pretext sets War bilaterally and B.lastContact[A]=turn. Reply1 to
   PretextAllied only clears Alliance bilaterally and sets that contact turn;
   it does not invoke alliance expulsion. A remote human B in network mode gets
   state publication then a Pretext request; the caller continues without waiting
   for B's decision. N02 owns its receipt/response.
2. If A is human and B is AI, test **A→B Alliance**, independently of the earlier
   B→A gate. Without Alliance: A Fundamentalism directly sets B→A Vendetta,
   adjusts B→A +10 and, unless autoplay, shows IncidentTerror. Other governments
   show IncidentWar unless autoplay, then declareWar(A,B,mediator None).
3. In that same human-A/AI-B branch with Alliance, last-mission=Steal and
   A.advanceCount<B.advanceCount gets special leniency: autoplay returns;
   otherwise evaluate stance, format attitude, draw R()%15+5, adjust B→A by
   that amount, show WimpOut and **return before the senate stage**. Other
   allied incidents show IncidentAllied unless autoplay, then declareWar(A,B,None).
   V04's alliance branch cancels alliance; do not assume it also sets War.
4. If A is human: Democracy reaches the senate stage without a draw. Any other
   government draws R(); even remainder modulo2 returns, and an odd draw still
   returns unless A's now-live government is Republic. Thus non-Republic
   governments can consume a draw despite never reaching a scandal.
5. Unless a scenario suppresses senate intervention, show SenateScandal only
   when autoplay is off, then call V02 changeGovernment(A,Anarchy). The change
   still occurs in autoplay when admitted; the scenario gate occurs after the
   Republic/non-Democracy draw. No separate treaty confirmation occurs.

The shared last-mission is read live. An escape incident after an incitement,
unit reset or civilization-removal callback need not see an invented fresh
mission value. Earlier war/cancel/attitude effects survive a later scandal or
an explicit return. AI-versus-AI treaty incidents have neither human response
stage nor senate stage.


## 12. Conformance cases

Compare state and ordered calls, including draws consumed before a later cancel,
unit destruction, notices and the exact argument passed to each owning child.
The following are independent implementation targets, not a claim that all have
been executed against the original program.

| Case | Required distinction |
| --- | --- |
| Non-Spy, spent254, multiplier3, mode0, victim2 | Spent becomes1, no escape draw, unit destroyed, then incident. Mode-1 skips spent/incident. |
| Ordinary Spy, hard escape with first draw odd / even | Odd fails without a second escape draw. Even consumes another draw; only nonzero modulo2 survives. |
| Surviving Spy with no own city | Return success and promote for mode>=0; retain position and do not invent exhaustion. Negative-mode success never promotes. |
| Remote failed Spy in mode3 / mode4 | BondGlory network notice only in mode3. Successful notice permits both. |
| Cancel city menu | Bilateral Contact remains; negative reply does not publish a new last-mission. An unknown nonnegative choice does. |
| Non-Spy AI with Vendetta | Consume the Poison selection draw before rejecting that choice by type. Peace/Alliance can later replace the choice. |
| Existing Embassy, AI actor | Fallback dispatch publishes Investigate; investigation destroys its unit without setting Investigated. |
| Race with cyclic ordinary candidates2 and5; start0 | Last qualifying ordinary candidate5 survives the survey; a qualifying space prerequisite2 instead stops at2. |
| Targeted theft, successful capture check, failed hard final escape | Invoke captured chooser, then final escape/incident, then set TechStolen even after loss. Ordinary theft sets it before escape. |
| Veteran Spy defender, roll59 | Detection even if defender belongs to A; actor remains alive, exhausted, with no grant, TechStolen or final escape. |
| AI-versus-AI sabotage with shields/building | Selection and ordinary diversion draws still occur, but no city damage; final escape still executes. |
| Custom sabotage reply35 | Original building mutation is a no-op; backed name/sprite reads can continue. Independent BF-231 rejects it. This is not itself proof of a memory overwrite. |
| Direct poison at size1 | Zero food, leave size1, then draw for escape mode. Ordinary menu would hide this choice. |
| Nuclear planting, no Palace, Fundamentalist actor | Three capture checks, U07 action with SDI disabled, no identification draw, no final escape/promotion. |
| Identified nuclear planting | Include inactive civilization slots in attitude/War effects; do not substitute declareWar or gate on contact. |
| Incite size8, B.gold1000, distance16, no discounts except ordinary Spy | Base840, Spy price700. AI eligibility reserves twice the price but ordinary acceptance charges once. Failed escape does not prevent transfer. |
| Incite double-price choice | Payment and escape first, then B→A Vendetta and founder=A, then C09 with reason2; no separate V06 incident. |
| Bribe rule cost10, B.gold1050, no nearby Palace, non-Settle | Price500. A Communism changes distance to10 and price750; B Communism alone does not. |
| Bribe nearest city belongs to third party | Leave home=None even when a farther A city exists; old B gets visibility, B gets no payment, only type counts change here. |
| Unit sabotage with one remaining hit point | Damage addition truncates to0; actor still escapes/dies and dispatcher returns1. No incident. |
| Chat, ordinary Spy, difficulty0, stored attitude9, draw5 | Timer15. The timer captured before R survives a callback changing the stored timer. Attitude15 requires supplied lookup backing or the declared repair/stop. |
| Mission timer0 / timer1 during All-units reset | Zero wraps255. One expires; omit the invalid relationship write, then execute escape using the live signed counter. |
| Human B declines Pretext; human A Republic, odd senate draw | Refusal does not itself bypass the senate stage. Scenario suppression is tested after the draw. |
| Allied human A behind in advances, last mission Steal | Stance/format, one R()%15+5 attitude adjustment, WimpOut, return before even a Democracy's senate stage. |

Also exercise captured versus live fields across UI, network, random and lifecycle
callbacks; host/client bribery success and timeout; Palace/Wall capture sequences;
all nominal menu gates; wrap/negative price paths; and explicit safe text and
index stops. A child supplied by a fixture does not establish its own integration.

## Appendix A. Reference representations and bug boundaries

A cleanroom implementation may use named mission kinds, owned strings and other
record layouts. The following identify original traces and imported values.
F02 owns field meanings; F06/N02 own persistent/wire layouts.

- City mission numbers0…7 are Embassy, Investigate, Steal, Sabotage, Poison,
  PlantNuke, Incite and Chat. Negative initial/menu replies cancel. Incite choices
  1/2 are ordinary/double payment; unit-sabotage choice0 is bribery. These separate
  dialog result domains must not be combined into a universal action enum.
- Fixed unit identities are Diplomat46 and Spy47; diplomatic role6, Settle5 and
  Trade7. The Normal Combat rule is `0x10`. Palace1, Courthouse7, City Walls8,
  SDI17 and spaceship parts35/36/37 are improvement identities. Manhattan's
  improvement is62; its active-wonder query is distinct from a city building bit.
  Relevant advances are Fusion Power32, Gunpowder35, Nuclear Fission58 and
  Rocketry73. Disabled prerequisite is-2.
- Unit SpyOnMission is `0x0002`, Veteran `0x2000`; city TechStolen `0x8`,
  Investigated `0x400000`. SpyChat is directed treaty `0x01000000`; other treaty
  encodings are in V04. The incident's scenario exception is Scenario plus mask `0x1`
  of the first scenario-flags field. Membership uses the original masked shift
  count; an invalid owner is not thereby a valid civilization-record index.
- The original arithmetic is signed32 with truncating division. Unit rule cost
  and city size are interpreted as signed bytes. Movement spent, damage, timer,
  counter, per-type unit counts and home retain eight bits; None home is255.
  Escape reads owner/counter as signed where stated. Subversion and contact-turn
  stores retain16 bits; treasury and price retain32. Negative prices and ordinary
  byte wrap are separate from memory overwrites and remain specified.
- Original sprites occupy60-byte records. Unit notices use the captured original
  type where the mission parent supplied it; escape notices use the live type.
  Technology notices select category/epoch icons; planted-device notice uses
  Manhattan's improvement sprite. P04/P07 own resource rendering and palettes.
- The sabotage production-name temporary is80 bytes. The original unbounded
  name append can overwrite it when a custom rule name is too long. That write
  and resulting stack damage are excluded bugs. Use safe owned text for supported
  names or reject/stop explicitly before the invalid append; do not reproduce
  corrupted odds, selectors or return state.
- Stock Sabotage resources contribute no target rows. Caller-added rows are
  production0 and installed buildings2…34, excluding Palace; SDI17 is then
  explicitly rejected. The original accepts any nonnegative custom modal result.
  C08's building setter ignores identities outside1…34, but subsequent rule-name
  and sprite accesses still occur. Values35…38 are within the original name/sprite
  families and are not automatically memory-write bugs; larger values can require
  unsupported reads. Supply evidence/backing or stop before those reads. Any actual
  out-of-bounds write remains excluded regardless of backing.
- The primary reconstruction adds a reply>38 return; independent BF-231 instead
  rejects reply>34. These are distinct stricter admission policies, not an original
  bounds check. A declared implementation may adopt either finite policy. Do not
  infer that a repaired source guard changes the original accepted-input contract.
- The original chat penalty table initializes15 signed32 entries. Its difficulty
  index is difficulty+9; its attitude index uses the stored unsigned attitude.
  Primary and independent BF-289 use zero outside0…14, replacing unknown original
  frame reads. The primary parent reserves16 local words, using word15 for nuclear
  identification on a different branch; that does not initialize a chat penalty.
  No classic zero penalty is inferred from the repair or that unrelated writer.
- U04's All-units expiry can write outside the relation array. Its specified safe
  continuation omits that write; neither adjacent naming corruption nor another
  guessed relationship is required. Invalid victim indices passed onward are
  handled separately at the escape/incident read boundary in section2.

## Appendix B. Evidence and validation

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary source pin: `a71e8aefffe04e6f719e0e213b027b128a747711` in OpenCiv2.
The bodies were read in checkout `fc2155ccdb2123c1137babab6fa39e38152278c9`;
**20 complete function bodies** were compared with the pin. The independent
reconstruction at `civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, was consulted for sabotage ingress,
chat backing and capture/loss details. No reference source was copied into this
repository. Both source trees remained clean.

Original executable: Civilization II MGE **5.4.0f, patch3**, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`, verified locally.
No original-game replay, native Windows build, new Ghidra investigation or
annotation update was performed. Archived runtime evidence was not newly read
or credited as a fresh test of this specification.

| Evidence | Scope |
| --- | --- |
| [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp), escape `004c5fae`, capture `004c64aa`, confirmation `004c654d` | Draw sequence, spent/promotion/fate and incident conditions. |
| Same file, city controller `004c6bf5` and its private menu/mission helpers | Captured inputs, dispatch/fallback, all eight mission bodies, read/mutation/callback order. The private helpers split the original body; they are not separate original entry addresses. |
| Same file, bribe `004c9528` and accept/transfer helpers; unit mission `004c9ebd` | Price, human/AI acceptance, host/client transfer, counts/visibility and unit damage. |
| [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp), capital distance `004c65d2` | Live Palace scan, step distance and cap. |
| [diplomacy.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/diplomacy.cpp), incident `004c59f0` | Directed relationship gates, human/AI response and senate ordering. |
| [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h), state declarations and the owning documents | Fixed identities, flags, field widths and child contracts. |
| Independent `src/diplomat_action.cpp`, `src/units.cpp`, `analysis/reference/bugfix-log.md` BF-231/BF-289 | Original custom reply ingress, bounded repair differences, unknown chat lookup and cached pre-random duration. |

### Checks performed

A standalone source-derived C++ harness compiled all **20 complete bodies**:
escape/capture/confirmation; city menu, fallback and dispatch; eight city mission
helpers; bribe acceptance, transfer and parent; unit mission; capital distance;
and espionage incident. Optimized and AddressSanitizer/UndefinedBehaviorSanitizer
builds each passed **74 assertions**; leak detection was disabled. Named relation
and type-count fields replaced original packed pointer arithmetic on the64-bit
host, retaining arithmetic wrap. The fixtures did not replay invalid writes as
required outcomes or substitute invented values for the original unknown chat
lookup. The chat case uses a defined table index.

U01 lifecycle/stack/movement, C01 nearest-city, W01 distance, C08 building/wonder,
V03 knowledge/grant/chooser, V02 government, V04 treaty/attitude/declare-war, U07
blast, C09 incited transfer, A08 stance, V07 race, UI and network services were
explicit adapters. Tests cover finite draw traces, cancellation and dispatch,
price/discount/government differences, post-loss transfer, counts and visibility,
theft selection/detection, AI sabotage, poison, planted-device ordering, cached
chat duration and incident returns. They do not demonstrate actual city transfer,
blast casualties, research effects, treaty cascades, native dialog cleanup or
network authority. The shell harness's scope-exit dialog guard is inert because
its dialog adapter owns no native resource. Source-pin, Markdown link/anchor,
ID/count and whitespace checks accompany this document.

### Remaining integration

Join the actual U01, W01/W03, C01/C08/C09, V02/V03/V04 and U07 children, then the
U02 movement, U04 expiry and V01 removal parents. Join A07 destinations to this
mission controller, A08 stance providers and V07's actual race predicate.
P04/P06/P07 and N01/N02/N05 must supply resource-defined replies, city/intelligence windows, host decisions,
message receipt, timer waits, chat disclosure and complete native lifetime effects.
X02 needs independent state/draw/message traces, including failed escape after
payment, reentrant target changes, accepted wrapped values and the declared
read-stop/bug-exclusion policies. No adapter result establishes that integration.
