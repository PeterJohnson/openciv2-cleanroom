# U06: Combat and veterancy

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

Rounds, damage, defender selection, casualties and aftermath consume earlier
results. Retain those dependencies and sampled-value reuse; independent arithmetic
cannot move a combat effect across a treaty, callback or network boundary.

## Target, ownership and inputs

This specification describes classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3** attack/defense strength, defender selection, combat preview,
ordered execution, damage rounds, promotion, casualties and immediate aftermath.
Appendix B records the source checks and limits.

[F01](F01-determinism-and-arithmetic.md) owns arithmetic and sampling laws;
[F02](F02-game-state-and-invariants.md) owns shared state and lifetime identities;
[F03](F03-rules-and-configuration.md) supplies interpreted rules.
[W01](W01-map-geometry-and-spatial-state.md) owns tile predicates, wrapping,
regions and direction table D. [U01](U01-unit-lifecycle-and-stacks.md) owns stacks,
lift/relocation, visibility membership, basic deletion.
[U02](U02-movement-and-zones-of-control.md) owns maximum/remaining hit points, movement
allowance/left/exhaustion and the attack-entry movement gates. A hit-point query
can clear damage when normal combat is disabled; it is not a pure accessor.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

[U07](U07-nuclear-attacks.md) owns the nuclear strike child.
[C09](C09-city-capture-and-revolt.md) owns city destruction/capture consequences;
V04 owns treaty mutation, war, ally obligations and diplomatic confirmation;
[A01](A01-ai-strategy-and-assessment.md) owns shared goals, A02 production choices,
and A04–A08 strategic responses and government policy.
[S02](S02-event-language-and-execution.md#11-caller-hooks-and-persistence) owns
unit-killed event execution, W03 city knowledge, and P02/P04/P07/P10 presentation
and reports. This document specifies their call conditions, arguments and order.
A called algorithm is not replaced by an assumed success or an inert callback.

Nominal inputs supply valid live attacker/defender and rule records, canonical
map coordinates, direction 0..7, positive movement divisor, positive maximum hit
points and usable firepower for normal repeated combat. Supported custom rules
can change type properties and values; fixed type-slot special cases still use
the original slots, regardless of renamed display strings. The original has
fewer validation gates than these nominal requirements. Off-nominal scalar wrap,
negative signed rules and original reads are separately described below.

[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
excludes writes outside intended objects and resulting corruption. Stop before
an invalid write, or reject the input, unless an established safe continuation
permits omission; do not reproduce damage through adjacent logical assignments.
Valid scalar wrap and in-bounds ring replacement remain required. Missing read
backing, stale/deleted slots, invalid division and nontermination are distinct
boundaries; supply their declared inputs or stop at the unsupported operation,
retaining earlier valid effects. A dead allocated slot is not automatically an
out-of-bounds object, nor does its retained storage prove a live identity.

## State, arithmetic and returned results

Use `a` for attacker, `d` for selected defender, `c/e` for captured attacker/
defender owners, `origin/target` for captured coordinates, and `A/B` for attack/
defense strength. `FA/FD` are firepowers. F01 defines reference results for
intermediate sums, products, left shifts and differences. Equivalent evaluation
is permitted; significant conversions before comparisons/divisions remain.
Right shifts of these values are arithmetic. Signed division truncates toward
zero and retains F01's divide-by-zero/overflow boundary. Byte-valued rule attack,
defense, firepower, movement, hit points, role and range use signed interpretation
where stated; movement multiplier is unsigned. Damage/spent counters wrap modulo
256. Compare ordinary positive strengths as integers; do not use floating odds.

Define the shared primary-roll operation:

```text
rollBelow(S):
    if signed32(S - 1) <= 0: return 0 without a draw
    return primaryCRT() remainder S, using signed division
```

This matches the signed decrement gate, including the most-negative signed32
input: its decrement wraps positive and therefore reaches a draw. It is not an
unbounded mathematical `S<=1` clamp. Every direct random call in U06 uses F01's
**primary sampling law**. A separate combat context is permitted; the sampling
law and round/decision rules remain unchanged.

Shared combat state contains cached terrain and city, current defense factor
(in halves), kill count, per-civilization treaty notices and ally warnings,
selected animation defender/coordinates, last sound selection, active-unit-lost
flag and the shared moving unit. Cached city/terrain can be changed by child
operations. Do not globally freeze them for the duration of a fight.

| Entry/result | Meaning |
| --- | --- |
| Preview, execute=0 | Returns a capped heuristic score after the preparation described below. It is not a win probability or a pure read. |
| No target unit | Returns 999 after entry work and target lookup, in either mode. |
| Execution returns 1 | Initial result, retained on diplomatic cancellation/alliance and many surviving-attacker paths. It does not promise a completed or won attack. |
| Execution returns 0 | Attacker loss, nuclear delegation, consumed attacker, or the specified AI response continuation. |
| Round helper nonzero | Attacker victory; repeated normal-combat termination can return its remaining hit points rather than Boolean 1. |

U02 publishes the moving unit and clears AI-garrison before calling execution.
Both movement branches call execute=1, even when movement's own execute option
is clear. U02 interprets the result according to its attack branch and can finish
at origin afterward. Direct callers must supply the appropriate incoming moving
unit; combat does not initialize it at entry. U06 does not repeat U02's Amphibious,
Submarine, concealed-target, Diplomacy-role, zero-attack, long-range-air and fatigue
gates. No generic retreat or escape step is added to ordinary combat.

## Strength primitives

### Attack strength

Start `A = signed rule attack * 8`. If Veteran, add `A >> 1`. Then if Paradropped,
add the **updated** `A >> 1`. These are sequential bonuses; both give 18 times
ordinary rule attack for positive integral stock values. No damage or movement
scaling happens in this primitive.

### Defense strength

Inputs are defender, refresh-tile flag and attacker slot, which may be absent.
Capture defender coordinates. When refresh is nonzero, publish terrain from
that tile and city-at-tile into shared combat cache. Otherwise retain incoming
cache, even when inconsistent with the defender's coordinates.

Capture signed defender rule defense and multiply by 4 before obtaining the
tile surface. Read river presence from that surface, then set:

```text
B = signed32(4 * captured rule defense)
B = signed32(B * (signed terrain defense of cached terrain + riverPresent))
factor = 2
```

Publish factor in shared combat state. Apply these rules in order:

1. Full Fortified order and live Land domain set factor=3.
2. Fortress component present **without** the City component qualifies as a
   fortress. With absent attacker, set factor=4. With present attacker, do so
   only when it is not Air and lacks Ignore-walls. This replaces fortification;
   an airbase does not qualify for this fortress branch.
3. If cached city is present, check the fighter interception branch: defender
   is Air with Attack-air, attacker present and Air, and attacker lacks
   Destroyed-after-attack. Multiply factor by 2 against an Attack-air attacker,
   otherwise by 4. Skip the remaining city-wall and non-Land reset rules.
4. Otherwise, for a present cached city, query City Walls then Great Wall only
   if Walls was absent. If either effective protection exists and defender is
   Land, set factor=6 when attacker absent, or when attacker is Land and lacks
   Ignore-walls. Finally reset factor=2 for a non-Land defender.
5. With no cached city, also reset factor=2 for non-Land defender.
6. If factor differs from 2, set `B = signed32(B*factor) >> 1`. If Veteran,
   add `B >> 1` afterward. Return B with shared factor/cache effects retained.

Terrain and river multiplication precede the domain-specific factor reset;
they are not restricted to Land here. A city's eligible fighter branch retains
its multiplied factor rather than being reset by the later non-Land rule.

### Selecting the best defender

Initialize best score zero and selected unit to the **supplied** unit. Negative
input returns it unchanged. Otherwise capture its coordinates, publish terrain
and city there, then traverse its validated U01 stack head to tail.

Skip a candidate only when cached terrain is Ocean and its live domain is Land.
For every other candidate:

1. Compute defense strength without refreshing tile, using the supplied attacker.
2. If normal combat is enabled, query remaining hit points, then maximum hit
   points, and set score=`signed32(remaining*B)/maximum`. Preserve child effects
   and faults; do not reuse one HP result as the other.
3. Anti-mounted property adds 1, independent of the attacker's actual type.
4. Anti-air property adds 1 when attacker is exactly the absent sentinel −1.
   Otherwise an Air attacker multiplies score by 3, or by 5 when that attacker
   has Destroyed-after-attack.
5. Candidate Attack-air and attacker other than −1, with attacker Air and
   Attack-air, doubles score.
6. Sea candidate in a cached city: absent attacker halves score. Present Air
   attacker doubles it when the city lacks SAM Battery; all other cases halve
   it. Halving here is signed division, not arithmetic right shift.
7. A score **at least** best replaces both best and selected unit.

Ties select the last qualifying stack member. If all candidates are skipped or
score below zero, the original supplied unit remains selected, even if it would
not pass a candidate test. The absent tests are not uniform: defense strength
accepts any negative attacker, while several selection modifiers test exactly −1;
other negative values need separately backed reads. Selection consumes no random
draws. Its health weighting and small preference bonuses are not the later
combat strength formula.

## Preparation common to preview and execution

Initialize message flags, city-improvement notice, reroll flag, ransom flag,
shore-bombard flag, explosion kind and related local flags to zero; result=1.
For civilizations 1..7 clear shared ally-warning and treaty-notice entries.
Retain entry zero. Capture attacker owner and origin, compute attack strength,
and capture signed FA from the attacker's current rule.

Query movement left. If less than live unsigned r, query movement left **again**
and retain that second result; otherwise retain live r. In execution only,
scale A by `signed32(A*retained movement)/live r`. Preview does not apply this
scaling, though its movement queries still run.

Compute target using captured origin plus D[direction], wrapping x once. Retain
these target coordinates also for casualty history. Query first unit at target;
if absent return 999. Select best defender as above. Capture its owner, compute
its defense strength without refresh, and capture signed FD. Apply in order:

| Step | Condition and mutation |
| --- | --- |
| Air-superiority versus range-zero Air | Attacker role Air-superiority, defender Air with range zero: `B -= B>>1`, FD=1. |
| Anti-mounted | Defender property: query attacker allowance. If double-movement mode, subtract signed live rule movement divided by 2. If result equals `2*r`, attacker Land, and freshly queried maximum HP is 10, add `B>>1`. |
| Anti-air | Defender property and attacker Air: B×3 against ordinary Air; B×5 against Destroyed-after-attack. |
| City defenses | Apply the ordered city-defense operation below. |
| Partisans | Fixed Partisans slot attacking a unit with rule attack zero: A×8. |
| Sea attacking Land | Set FA=1, then FD=1. |
| Submarine defender | Set FD=1. |
| Submarine attacker, AI defender owner | Clear defender owner's standing goals of kind 18 within radius 3 of captured target; add goal kind 18 there with priority 2. These calls also occur in preview. |
| Sea defender on non-Ocean, attacker not Sea | Set FD=1, shore-bombard=true, FA×2. Both original branches after rereading attacker domain make these same writes. |
| Barbarian adjustments | Apply below. |

All references to unit type/properties in successive steps are live, except FA,
FD, A/B and named captures. A callback can change a later test. There is no final
reselection of the defender after these modifiers.

### City-defense adjustments

These adjustments require cached city slot **strictly greater than zero**;
city slot zero is excluded here, although selection, walls and other city logic
accept it. Preserve that distinction.

- Attacker Sea, defender not Sea, Coastal Fortress present: double B in either
  mode. Only in execution set improvement notice to Coastal Fortress and mark
  the city's Coastal-fort-used flag.
- Attacker Air: if defender lacks Attack-air or attacker is Destroyed-after-attack,
  query SDI. With SDI, Destroyed-after-attack and signed attacker rule attack
  below 99, double B and set the SDI-bonus flag; in execution set improvement
  notice to SDI. Then query SAM Battery; if present double B again, and in
  execution overwrite improvement notice with SAM.
- Otherwise, in the Air branch, execution sets the scramble notice. There is
  no additional strength multiplication here; the fighter factor is already
  in defense strength. Preview leaves scramble notice unset.

### Barbarian adjustments

If captured attacker owner is Barbarian:

1. Against a nonhuman captured defender owner, divide A by 2. Against human,
   set A=`signed32((difficulty+1)*A)/4`, truncating toward zero.
2. With a cached city, defender civilization fewer than two cities sets A=0.
   Query its Palace; presence enables defender reroll and arithmetic-halves A.
   Fewer than eight cities enables reroll regardless of Palace.
3. Effective Great Wall for defender owner divides A by 2 again.

If captured defender owner is Barbarian, fixed Archers halve B; fixed Legion
also halves B when no currently human civilization knows Iron Working. After
this halving, B<2 becomes 1. Effective Great Wall for attacker owner doubles A.
Both owner-zero branches can run for off-nominal same-owner combat.

### Preview return

For execute=0, if normal combat is enabled, query attacker remaining HP and set
`A = signed32((HPa + 8*FA)*A)`; then query defender remaining HP and similarly
set `B = signed32((HPd + 8*FD)*B)`. While A>3999, arithmetic-shift A and B right
once, in that order of resulting values. Compute `score=signed32(A*8)/(B+1)`.
Return 1024 if score exceeds 1024, otherwise score; do not clamp a negative score.

Preview returns before movement charge, confirmation, treaty changes, rounds,
promotion and casualties. It has already changed shared caches/notice arrays
and can have invoked AI goal updates, stack repair and HP/movement query effects.
It is a heuristic, not the exact probability of the subsequent round process.

## Execution before combat rounds

After preparation, execute these operations in order:

1. If U02's shared movement-execute option is enabled, add live r to attacker
   movement spent modulo 256. This is not the partial-movement value used to
   scale attack strength.
2. If captured attacker owner is currently human, call diplomatic confirmation
   with Contact, Peace and Alliance as its requested relations. Nonzero result
   returns 1. After that callback, an attacker→defender Alliance also returns 1.
   The prior movement charge is not refunded.
3. If both captured owners are non-Barbarian, clear global peace-turn count.
   Publish Fighting in attacker→defender relation through V04's treaty operation.
4. Run treaty-break handling below. Call V04 force-war `(c,e)` and retain its
   result for later ally-joining; intervening callbacks do not replace it.
5. For non-Barbarian attacker, difficulty below Prince with human defender
   arithmetic-halves A. On Chieftain with human attacker, double A afterward.
   These adjustments run **only in execution** after treaty handling.
6. Build viewer entries, then when any viewer exists run start presentation,
   precombat announcements, sound selection and attack animation, in that order.
7. Read live signed attacker rule attack. At least 99 delegates to U07 nuclear
   handling as described below, then returns 0 before normal combat aftermath.
8. Otherwise set explosion-shown=false. If both FA and FD are zero, set attacker
   victory false without entering rounds. All later promotion/casualty stages
   still run. Otherwise run damage rounds.

### Treaty-break handling

Let the directional relation tests be read live using captured owner indices.
The sneak-attack branch applies when defender owner is human and either:

- defender→attacker has Ceasefire or Peace; or
- defender→attacker has Contact or attacker→defender has Broken-treaty, and
  defender→attacker does not have War.

For that branch:

1. If attacker government is above Fundamentalism, consume one primary draw
   modulo 100. Republic subtracts 50; otherwise query United Nations and
   subtract 50 if effective. If signed32(result−10*defender reputation) is
   nonnegative and attacker owner is AI, call choose-government `(c,1)`.
   The original also sampled for a human attacker. That unused sample may be
   omitted, preserving the government/United Nations queries and their effects.
2. If autoplay disabled and defender owner human, notify the defender: SNEAK
   with attacker adjective unless defender→attacker has Ceasefire without
   Peace; that case uses BREAKCEASE with attacker plural name. Local defender
   receives a unit dialog; remote network defender receives the text notice.
3. Double A and store current turn in defender's last-contact entry for attacker.

Otherwise, only a Peace relation in **either** direction prepares later treaty
and ally notices. Nonnetwork: if at least one owner human, set general notice.
For two AI owners, local observer allied to defender sets ally warning 1; otherwise
allied to attacker sets warning 2; otherwise full revelation sets general notice.
An absent/invalid observer on this relation read needs original backing or a stop.

Network: scan human civilizations 1..7, marking treaty notice for either
participant or full revelation; any marked entry sets general notice. Then
scan human civilizations 1..7 again: a civilization other than defender, allied
to defender, gets ally warning 1; otherwise one other than attacker, allied to
attacker, gets warning 2. Set the corresponding aggregate warning as each entry
is assigned. These scans do not require alive membership. Entries remain shared
through later callbacks and city-destruction suppression.

## Viewer publication and start presentation

Scan v=1..7 and clear each seen entry, including ineligible entries. Entry zero
is retained, but subsequent combat viewer loops iterate 1..7 rather than indexing
by a possibly negative observer. Eligibility requires `(network OR observer==v)`
and current alive and human membership.

In network mode only the two captured participants can become seen. In
nonnetwork mode set seen for either captured participant, full revelation,
either unit's live seen-by membership or live ownership, or the scenario option
that shows combat. Return whether any entry became true. Full revelation does
not add network spectators to this producer.

When any viewer exists, start presentation performs:

1. Mark attacker seen by captured defender owner through U01; send state
   difference `(all,2,0,0,0)`.
2. For each seen v in ascending order: local observer ensures target visible
   with captured attacker owner as argument; if that returns zero redraw origin.
   Remote v gets PossiblyCenter(target,c if v==c else e), then DrawSquare(origin).
3. U01 lift attacker with sync enabled. For view 0 and each open view 1..7, draw
   selected defender and blit target. If attacker has Submarine or
   Destroyed-after-attack, delay 10 ticks.

This viewer-dependent lift can mutate stacks/cargo. Do not assume presentation
is always a state-free decoration. Each display/send can run its own callbacks.

### Announcements

Perform the following families in table order. Prepare text when its condition
holds; autoplay normally suppresses the notices, not preceding text preparation.
Each notification scans seen v=1..7, using a local dialog for live observer and
a text-argument message to the current remote connection otherwise.

| Family | Condition, ordered text and payload |
| --- | --- |
| MISSILEATTACK | Attacker Destroyed-after-attack, signed attack<99, defender human, and nearest all-owner/all-region city exists: argument 0=city name, payload attacker. |
| PEARLHARBOR | Shore-bombard: arguments 0=attacker adjective, 1=live attacker type name, 2=defender adjective; payload attacker. |
| BATTERY / BATTERY2 | Improvement notice nonempty: argument 1=improvement name, 2=cached-city name. With SDI bonus and notice not SDI, also argument 3=SDI name and use BATTERY2; otherwise BATTERY. Improvement presentation uses mode 8. Argument 0 retains earlier text state. |
| SCRAMBLE | Scramble notice: 0=defender adjective, 1=live defender type name, 2=cached-city name; payload defender. |
| AMPHIBMOTIZE | Live attacker Land and captured origin Ocean: 1=attacker adjective, 2=attacker type name; payload attacker. Argument 0 remains incoming. |

Nearest-city lookup uses [C01's weighted query](C01-city-lifecycle-and-ownership.md#weighted-nearest-city),
not a local substitute. Unlike paradrop, the missile announcement checks absent
city before copying its name. Message encodings are in Appendix A.

### Sound selection and attack animation

Capture attacker type and clear last sound ID and last sound argument to absent.
Explosion kind starts at default zero. First matching sound branch wins:

| Ordered condition | Sound or deferred explosion selection |
| --- | --- |
| Test slots 1..8 | Corresponding EXTRA1..EXTRA8, mode 1. |
| Extra-land/ship/air slots | CUSTOM1/2/3, mode 1. |
| Destroyed-after-attack | MISSILE mode 1 only when signed attack<99; otherwise no sound. |
| Air attacker, Air defender | AIRCOMBT below type slot 30, JETCOMBT otherwise, mode 0. |
| Other Air attacker | Range zero HELISHOT; otherwise below slot 30 DIVEBOMB, else JETBOMB; mode 1. |
| Sea Submarine | TORPEDOS mode 1. |
| Other Sea | Explosion kind 6, replaced with 46 for Battleship, Cruiser, AEGIS Cruiser or Destroyer; no immediate effect call. |
| Elephant | ELEPHANT mode 1. |
| Horsemen/Chariot/Knights/Crusaders | SWRDHORS mode 1. |
| Dragoons/Cavalry | CAVALRY mode 1. |
| Musketeers/Riflemen/Alpine Troops/Partisans | INFANTRY mode 1. |
| Fanatics/Paratroopers/Marines/Mechanized Infantry | MCHNGUNS mode 1. |
| Other non-Air/non-Sea type outside slots 22..26 | SWORDFGT mode 1. |
| Catapult slot 23 | CATAPULT mode 1. |
| Remaining slots 22..26 | Explosion kind 40. Slots above 23 also play FIRE mode 1 and delay 20 ticks. |

Fixed slots and sound IDs are representation mappings in Appendix A; current
customized names do not choose these cases. [P10](P10-audio-video-and-animation.md#2-effect-selection-and-dispatch)
owns playback and the ordered last-sound updates; its
[animation timelines](P10-audio-video-and-animation.md#13-map-combat-and-nuclear-animation-timelines)
own frame/wait/input behavior. Preserve callback effects.

Then publish selected animation defender and target coordinates. When networked,
for each nonlocal seen v send SetSlideSound with live last sound ID/argument,
then SlidePiece(attacker,origin,direction,live selected defender,0). Animate the
attacker from origin with ordinary optional arguments absent. U01 relocates its
whole lifted group back to captured origin with sync disabled; redraw both
combat tiles. In network mode, sync state and send DrawTwo(target,origin) to
each nonlocal seen v. Actual victory does not automatically enter target here.

## Damage rounds

Set animation interval to 5 when both units' **signed rule maximum HP** are below
30, otherwise 10. This uses rule values, not remaining HP. If attacker is
Destroyed-after-attack, set interval zero and explosion-shown=false; scan seen
viewers and show/send the selected explosion at target before any round. This
scan is not separately gated by any-viewer.

Repeat:

1. Query attacker remaining HP. If zero, or a subsequent defender remaining
   HP query is zero, query attacker HP again and return that value as the
   attacker-victory result. Short-circuit the defender query when attacker is zero.
2. Take `ra=rollBelow(A)`, then `rd=rollBelow(B)`. Attacker wins this round only
   if `ra>rd`; equality favors defender.
3. Only when defender-reroll is enabled **and** attacker won the first pair,
   draw a second attack roll then a second defense roll. Reverse the result
   only if second attack roll is **strictly less** than second defense roll.
   Equality on this second pair preserves the attacker win.
4. Attacker round win hits defender with captured FA at target; otherwise hit
   attacker with captured FD at origin. Execute the hit operation below.
5. Read the current normal-combat rule. If disabled, stop immediately and
   return that round's Boolean outcome; otherwise repeat HP checks.

There is no strength recomputation between rounds. Direct primary draws are
skipped independently for small strengths by rollBelow. This is not a single
`A/(A+B)` chance. Negative or zero custom firepower can wrap or fail to advance
a health counter, and some configurations need not terminate. Do not silently
clamp them into positive firepower; a restrictive implementation must state its
input limit. The both-zero shortcut belongs to the parent, before this helper.

### Applying one hit

Capture old unsigned damage; store `(old + signed firepower) mod 256`.
If any-viewer, interval nonzero, and `newDamage/interval != oldDamage/interval`,
set explosion-shown=true and send state difference `(all,2,0,0,0)`.
Then for each seen v:

- Local observer: show the selected explosion at the hit location; query victim
  HP afterward and redraw both tiles if nonzero.
- Remote: send Kaboom at hit location with selected kind; query victim HP
  afterward and send DrawTwo if nonzero.

The comparisons use retained unsigned damage values, so wrapped reductions can
also cross an animation interval. The HP queries happen after animation/send
callbacks and may themselves clear damage in single-round mode. No direct
random draw occurs in hit presentation.

## Result presentation, promotion and casualty processing

On attacker loss set the active-unit-lost flag to 1; a win does not clear an
incoming value. Run result presentation, clear selected animation defender to
absent, reset shared kill count to zero, run promotion bookkeeping, then remove
the loser and process defender-city consequences. These operations precede the
final result notifications and surviving-attacker continuation.

### Result presentation

If any viewer, clear last sound ID, leaving its argument retained. A victorious
attacker against Air defender outside a cached city uses the defender's plane
crash sound. A losing Air attacker without Destroyed-after-attack uses its own
crash sound. Below type slot 30 select DIVCRASH, otherwise JETCRASH, mode 1.
Other outcomes play LARGEXPL mode 1 only for nonzero explosion kind.

If networked and a sound ID was produced, send SoundPlay with live ID/argument
to nonlocal seen viewers. Then, regardless of any-viewer, sync state. With viewers,
redraw both tiles for each local viewer or send DrawTwo remotely. If no hit
explosion was recorded, follow each redraw with a **default-kind zero** explosion
at the loser's tile. The earlier deferred explosion kind is reset for this final
fallback in the original. There is no casualty deletion before this stage.

### Promotion bookkeeping

Use captured A/B, not updated rules or remaining HP. Let total=`signed32(A+B)`.

- Attacker loss: increment attacker civilization's aggression counter toward
  defender modulo 256. Take `rollBelow(total)`; promote defender if roll≤A,
  otherwise query defender owner's Sun Tzu and promote if effective.
- Attacker win: set attacker aggression toward defender to zero. Take
  `rollBelow(total)`; promote attacker if roll≤B, otherwise query attacker
  owner's Sun Tzu and promote if effective.

The random test is performed even for an already veteran or missile winner.
Short-circuit success skips the wonder query. Promotion itself first rejects
already Veteran, then Destroyed-after-attack. Otherwise set Veteran, and if live
owner is human and autoplay disabled, prepare live unit-type name argument zero;
show PROMOTED locally or send a remote network notice to its owner. No extra
Land-domain gate restricts Sun Tzu's combat promotion call here.

### Recording one casualty

The casualty operation takes losing unit, opposing unit and captured battle
coordinates. It reads the current game year at the history/send call. Execute
in this order:

1. Increment loser's civilization/type loss total unless already 255; this
   counter saturates, unlike the aggression counter.
2. If loser's live owner equals local observer **or** mode is nonnetwork, append
   local casualty history. Otherwise, for a human owner in network mode, send
   the casualty record request to that owner's live connection, with losing
   unit, opposing unit, current signed year and battle coordinates.
3. Increment signed32 shared combat kill count.
4. If scenario events are loaded, invoke UnitKilled with loser's **live** type,
   opponent's live owner and loser's live owner. The losing record is still
   present at this call; event actions can mutate/delete/reuse records.
5. Invoke U01 basic deletion with synchronization enabled on the retained
   losing slot. Do not add a new identity recheck after events.

A whole-stack casualty operation resets kill count to zero, gets validated head,
then captures each member's **raw next link before** recording its casualty.
Continue with that captured next slot after callbacks/deletion, rather than
following a newly repaired link. It can therefore visit retained/reused slots;
unsupported state requires the declared boundary, not invented liveness.

### Casualty history ring

Each civilization has a logical ring of 300 entries, next/first indices and a
count. Write the next entry's unit type, x, y and year in that order. Query the
opponent civilization's secondary color, then write it. Query opponent plural
name and copy a maximum of 24 bytes, padding on an early terminator; force final
byte to zero, leaving at most 23 name bytes. Re-read losing owner and next index
for each field write, including after those child queries. Do not cache a single
entry through callbacks that change them.

Increment the live owner's next index. If it equals 300 set zero. If next now
equals first, increment first and wrap it only on equality with 300; otherwise
increment signed32 count. Thus ordinary initialized operation retains at most
299 counted entries with first==next representing empty, and replaces oldest
entries within its allocated ring. Repaint the casualty report if open.
Malformed indices that address outside the logical ring are excluded-write bugs,
not additional history slots. Original compact field/string encodings belong
in Appendix A; P07 owns display and navigation of recorded entries.

### Removing the loser and defender-city consequences

For attacker loss, set winner owner=e, U01 lift attacker with sync enabled,
then kill its lifted stack using defender as opponent and captured target as
history location. Set loser owner=c. U01's domain/cargo rules determine the
lifted group: this does not indiscriminately kill the origin tile's entire stack.

For attacker victory:

1. If defender owner Barbarian and its live signed role>4, enable ransom.
   Set winner owner=c.
2. If cached city absent, target not a fortress (Fortress without City component),
   and no target airbase owner, kill defender's entire validated stack. Otherwise
   record/delete only the selected defender. Opponent is attacker, history
   location captured target. Set loser owner=e and capture current cached city.
3. If cached city present, mark Defender-lost. Test **captured attacker origin**:
   when non-Ocean, city lacks City Walls, defender owner lacks effective Great
   Wall, and not `(Chieftain AND human captured defender owner)`, decrement
   city population modulo 256.
4. If the retained population becomes zero, call C09 city deletion `(city,0)`;
   update target tile knowledge for civilizations 1..7, redraw neighbors, then
   check defender civilization elimination `(e,c)`. A nonzero elimination
   result clears aggregate treaty notice and all shared notice entries 1..7.
5. If decremented population remains nonzero, reveal city to attacker owner.
   If target has no unit owner and captured defender owner is AI, reset that
   owner's city production for target's live raw region.
6. After the city-population branch, independently test target unit owner.
   If absent and captured defender owner is human, invoke
   [A05's transport retasking](A05-ai-naval-and-transport.md#10-retasking-transports-after-a-city-becomes-exposed)
   with the captured city slot, even after a city-deletion
   child. This stale-slot boundary is not silently repaired here.

The ordinary population loss is not city capture. C09 capture can occur through
later movement/AI response. Fortress and airbase protect a losing defender's
other stack members; ordinary open-tile stack loss is intentional classic behavior,
not an out-of-bounds-write bug.

## Final effects and returned movement result

After loser processing, unconditionally sync state. If any viewer, in network
mode send DrawTwo(target,origin) to each nonlocal seen viewer, then redraw both
tiles locally. Perform these remaining steps in order:

1. If autoplay disabled and kill count>1, set count as numeric argument zero.
   Show MULTIPLELOSE if observer is loser, else MULTIPLEWIN if observer winner.
   In network mode, notify a nonlocal human loser when possible; only otherwise
   notify a nonlocal human winner. Do not send both remote notices from this
   branch.
2. If ransom enabled, compute unsigned `barbarianActivity*100/2`, add it to
   captured attacker owner's treasury with its scalar retention, and set text
   amount. If observer==attacker and autoplay disabled, show RANSOM with fixed
   ransom sprite and redraw info. Otherwise network with autoplay disabled
   syncs state, then sends RANSOM and InfoStatus to each nonlocal seen viewer.
   The RANSOM send carries one string substitution and no numeric substitutions,
   despite the local numeric preparation; N02 owns the receiving continuation.
3. If retained force-war result is nonzero, call allies-join-war `(e,c)`.
4. Run final treaty/ally notices below.
5. Refresh advisor kind 2 if open; in network mode broadcast ReportsUpdate(2).
6. Attacker loss sets result=0. Attacker win invokes surviving-attacker handling.
7. In network mode sync again, then send AbortKaboom followed by DrawArea(origin)
   to each nonlocal seen viewer. Return result.

The leader reward above is distinct from [W06's city ransom demand](W06-huts-and-barbarians.md#city-ransom-demand),
which is made by the barbarian order chooser before an attack on an empty city.

### Final treaty and ally notices

Only autoplay disabled and a nonzero aggregate treaty/ally flag enters. Prepare
defender plural name as argument zero, attacker plural as argument one.
Nonnetwork: general notice takes priority and shows CANCELPEACE; otherwise ally
warning 1 shows ALLYUNDERATTACK then calls ally-demand-help `(e,c)`, warning 2
shows ALLYATTACKING.

Network: scan v=1..7. A shared treaty-notice entry shows/sends CANCELPEACE first.
Then, if shared ally warning is nonzero and autoplay still disabled, handle it:
warning 1 shows local ALLYUNDERATTACK then calls ally-demand-help; remotely send
ALLYUNDERATTACK and additionally AskAlly(e,c) only when captured defender owner
is AI. Warning 2 shows/sends ALLYATTACKING. Re-read shared entries and observer
at their uses. City elimination may have cleared notices without clearing ally
warnings. These calls can change later shared moving-unit or owner state.

### Surviving-attacker handling

Capture actor from **current shared moving-unit**, not the original attacker.
If its live domain is Air and range is not 1, exhaust it.

The special AI-city continuation requires captured attacker owner currently AI,
cached defending city present, and no first unit at target. If it does not apply,
Destroyed-after-attack deletes this actor through basic deletion with sync enabled
and sets result=0; otherwise retain result=1.

If the AI-city continuation applies:

- Destroyed-after-attack: basic-delete actor with sync, redraw origin neighbors,
  call AI respond-to-attack `(c,target)` and set result=0.
- Other actor: if live Air with nonzero range, increment fuel counter modulo
  256. If signed counter is at least signed live range, stop this continuation,
  retaining result=1 and the counter; it does **not** directly crash/delete here.
  Otherwise call AI respond-to-attack `(c,target)` and set result=0.

The callback can invoke movement/paradrop and replace shared state.
[A04](A04-ai-ground-tactics.md#9-immediate-response-to-an-attack) owns
its search/selection. U02/U05 retain any later movement finish and fuel processing.

### Nuclear delegation

After precombat presentation, live signed attack≥99 clears selected defender
and calls [U07 nuclear action](U07-nuclear-attacks.md#nuclear-action)
`(c,target,checkSDI=1)`. Child result zero basic-deletes the original attacker
with sync, redraws origin, and in network mode
syncs then sends DrawSquare(origin) to nonlocal seen viewers. Nonzero result
resets attacker aggression toward defender to zero; if attacker is AI and cached
city present, call AI respond-to-attack `(c,target)`. Return 0 in all cases,
without rounds, normal promotion/casualties, normal treaty notices or final loop.
Do not reinterpret the nuclear child's result as ordinary combat victory.

## Worked cases and conformance requirements

Unless varied, use canonical flat tiles, valid live records/rules, r=3, terrain
defense 2, no river/city/fortress, allowance 6, maximum HP 10, firepower 1, normal
combat, Prince difficulty, no human viewers, no callbacks changing state and
no wonders. Supply current moving-unit as attacker for execution. Scripted draws
are explicit boundary inputs; F01 owns raw generator vectors.

| Case | Expected result |
| --- | --- |
| Rule attack 4, Veteran and Paradropped | Attack 32→48→72. |
| Rule defense 3 | Plain 24; Fortified 36; Fortress replaces it with 48; Walls replace it with 72; Veteran after Walls yields 108. |
| City Air fighter defense 3 against Air bomber / fighter | Primitive defense 96 / 48; selection's additional fighter-versus-fighter preference can change its score again. |
| Stack equal scores `[A,B,C]` | C selected. Ocean stack containing only Land candidates returns supplied unit, not necessarily head. |
| Air attack against SAM in city slot 0 / slot 1, B=8 | Parent city-defense modifier leaves 8 / doubles to 16. Walls and city protection have different presence tests. |
| Preview A=80, B=24, both HP10 and firepower1 | `(18*80*8)/(18*24+1)=26`, even with only one movement unit remaining. |
| Submarine preview versus AI | Standing goal kind 18 cleared/added before return, no combat draw. |
| Round A=B=8, rolls 7 and 7 | Defender wins tie. With first pair 7/0 and protected-defender reroll 3/3, attacker keeps its win. |
| A=0, B=1 | Neither roll draws; defender wins round. |
| Hit old damage 250, firepower 10 / −1 | Retained damage 4 / 249. This is scalar wrap. |
| Both firepowers zero | Parent skips rounds and loses attacker; promotion stage can still consume its own draw. |
| Human cancels diplomatic confirmation | Prior movement charge remains; return 1, no rounds or promotion. |
| Already-veteran or missile winner | Promotion selection still draws; promotion helper itself refuses the redundant/ineligible change. |
| Aggression 255 on attacker loss; type loss total 254 | Aggression wraps to 0; loss total becomes 255 and saturates on later losses. |
| Attacker loses from a Land stack with another ordinary Land member | Lift singles out attacker; other origin member survives. Sea cargo grouping can give a different lifted group. |
| Defending city size 2, no Walls/Great Wall, attack from land | Defender dies and population becomes 1. Attack from Ocean leaves size 2. |
| Network observer is a third civilization with full revelation | Viewer producer still selects only human participants. |
| Casualty event changes loser's next link | Whole-stack casualty scan follows its pre-event cached next, not the changed link. |

Fixtures must include incoming shared cache/arrays, actual callback outcomes,
controlled choices or a versioned implementation replay trace, ordered messages and queried state. Test both normal
repeated combat and one-round mode, preview side effects, partial movement,
special properties, city slot zero, promotion short-circuit, complete casualty
chains and substituted moving-unit continuations. An unchanged state prefix
is not proof of the unexecuted city/event/network tail.

## Appendix A. Original encodings and snapshot mapping

The following aids comparison with reference state. It does not require the
same private layout in a cleanroom implementation.

| Item | Reference mapping |
| --- | --- |
| Arithmetic | Signed32 strength intermediates; signed rule bytes; unsigned stored damage/spent; full order Fortified=2. Normal-combat rule `0010` hex. |
| Type properties | Attack-air `0010`, Coast-bound `0020`, Ignore-walls `0040`, Carry-air `0080`, Anti-mounted `0400`, Destroyed-after-attack `1000`, Anti-air `2000`, Submarine `0008`. |
| Unit flags | Veteran `2000`, Paradropped `0010`, AI-garrison `0100`; U01/U04 own other lifecycle flags. |
| City flags/facilities | Defender-lost `00000020`, Coastal-fort-used `08000000`; Palace 1, Walls 8, SDI 17, SAM 27, Coastal Fortress 28. |
| Shared combat cache | City `006acb08`, kill count `006acb0c`, treaty-notice words from `006acb10`, terrain `006acb30`, factor `006acb34`. Ally-warning words from `006acae8`. Entry loops use indices 1..7. |
| Shared sound/ally storage | Ally-warning entries overlap the final in-bounds words of the original sound/control scratch array. This intentional shared storage is not an expanded writable domain. |
| Opponent bookkeeping | Civilization aggression byte offset `50+opponent` hex; type-loss bytes begin `116` hex. Former wraps; latter saturates at 255. |
| Confirmation relations | Requested mask `0d` = Contact/Peace/Alliance. Fighting `0200`, War `2000`; Broken-treaty `40` low byte. Government Republic=5, Democracy=6; Prince=2, Chieftain=0. |
| Fixed unit slots | Archers 4, Legion 5, Partisans 9, Catapult 23; Ransom sprite 62. Extra slots 51..53; test slots 54..61. Sound's 30 split compares numeric type. |
| Other sound slots | Elephant 17; Horsemen/Chariot/Crusaders/Knights 15/16/18/19; Dragoons/Cavalry 20/21; Musketeers/Riflemen/Alpine 7/11/10; Fanatics/Paratroopers/Marines/Mechanized Infantry 8/13/12/14; Destroyer/Cruiser/AEGIS/Battleship 37/38/39/40. |
| Named sound IDs | EXTRA1..8=125..132; CUSTOM1..3=101..103; AIRCOMBT=0, JETCOMBT=82, HELISHOT=33, DIVEBOMB=24, JETBOMB=80, TORPEDOS=77, MISSILE=41, ELEPHANT=25, SWRDHORS=74, CAVALRY=12, INFANTRY=34, MCHNGUNS=38, SWORDFGT=73, CATAPULT=10, FIRE=28, LARGEXPL=35, DIVCRASH=23, JETCRASH=79. |
| Text message IDs | PROMOTED `20`; BREAKCEASE/SNEAK `2d/2e`; MISSILEATTACK/PEARLHARBOR `2f/30`; BATTERY2/BATTERY `31/32`; SCRAMBLE/AMPHIBMOTIZE `33/34`; MULTIPLELOSE/WIN `35/36`; RANSOM `37`; CANCELPEACE/ALLYUNDERATTACK/ALLYATTACKING `3a/3b/3c` (hex). |
| Text argument counts | PROMOTED, BREAKCEASE, SNEAK, MISSILEATTACK, RANSOM: 1 string and 0 numbers; PEARLHARBOR/BATTERY/SCRAMBLE/AMPHIBMOTIZE: 3 strings; BATTERY2: 4; final treaty notices: 2; multiple-kill notices: 0 strings and 1 number. |
| Network messages | DeathStack `64`, SlidePiece `70`, DrawSquare `72`, DrawTwo `73`, SoundPlay `7a`, Kaboom `7c`, ReportsUpdate `7f`, SetSlideSound `9a`, AbortKaboom `a3` hex. N02/N05 own packet encoding and receipt. |
| Casualty history | 300 records per civilization; type/x/y/year/killer-color use retained16, name 24 bytes including forced terminator. First/next/count are signed32. Local record entry `0059c575`. |

Rule parsing acceptance does not guarantee safe downstream division or a finite
round loop. A wrapped invalid array index must stop before a write; do not make
original corruption a conformance requirement. Original read aliases need separate
backing contracts. Intentional scalar and ring wrap are not excluded by that rule.

## Appendix B. Evidence, checks and remaining integration

Primary evidence is OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`, inspected in reference checkout
`fc2155ccdb2123c1137babab6fa39e38152278c9`. Independent evidence is
`civ2` at `4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`,
principally `src/combat_entry.cpp` and its combat defender, preview, rounds,
execution, casualty/history and presentation fixture sources. Neither reference
tree was changed. Archived test reports are historical scope evidence, not runs
performed for this document.

| Family | Source / original entry |
| --- | --- |
| Attack/defense | [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp), `unit_attack_strength` / `unit_defense_strength`, `0057e2c3` / `0057e33a`. |
| Defender selection | Same file, `unit_stack_best_defender`, `0057e6e2`. |
| Casualty / stack / promotion | Same file, `unit_kill_in_combat` `0057e9f9`, `unit_kill_stack_in_combat` `0057eb94`, `unit_promote_veteran` `0057ebfd`. |
| Preview/execution and extracted children | Same file, `unit_resolve_attack`, `00580341`. Named static helpers split the original body. |
| Local casualty ring | [reports.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/reports.cpp), `casualty_log_add`, `0059c575`. |
| Data / enums | [unit state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/unit.h), [network state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/network.h), [named constants](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h). |

Independent BF-169/BF-265 add bounds to malformed incoming casualty packets.
Those receiver changes do not turn invalid history writes into required behavior;
N02 owns supported packet validation and X01 excludes object-overwrite bugs.
The source bug ledger explicitly distinguishes classic stack casualties from
memory corruption. No rule-improving combat extension is adopted here.

A temporary native harness extracted **25 U06 routines**, including the complete
preview/execution parent and its static children, alongside **38 existing U01
and allowance/selection routines**. Both optimized and unoptimized AddressSanitizer/
UndefinedBehaviorSanitizer builds passed **67,982 assertions**, with signed-wrap,
signed-char semantics and strict-alias optimization disabled. Leak detection
was disabled for the ptrace environment. Extraction counts are not a claim that
every routine, branch or state combination was independently exercised.

The bulk of assertions cover every attack-rule byte under four Veteran/Paradropped
combinations and all 65,536 stored-damage/firepower-byte pairs. Additional cases
check independent terrain/fortress/wall/fighter expectations; defender ties and
Ocean exclusion; city-zero SAM omission and combined SAM/SDI; barbarian last-city
protection; preview's score/shared clears and submarine goal effects; all 64
pairs of equal-strength rolls and all 64 protected-defender second-roll pairs;
skipped small-strength draws; real promotion/casualty/stack continuations;
precharged cancellation, already-veteran/missile promotion attempts, casualty
next-link retention, city population loss and network viewer restrictions.

Complete parent calls cover an ordinary win/loss, the both-zero-firepower path,
a viewer path with real lift/relocation, and delegated nuclear return. A seed-1
case using attack 5, defense 3, firepowers 2/3 and HP10 matches the independent
round fixture's eight rounds plus one promotion draw: attacker damage 12,
defender damage 8, attacker loss, 17 draws. Scripted primary results exercise
specific boundaries elsewhere; unscripted calls advance F01's CRT recurrence.
There is no proof of a new random generator in these tests.

The harness uses actual extracted U01 stack/cargo/relocation/basic-deletion,
HP/movement and combat code on a bounded 20×12 logical map. Tile/facility/wonder
queries, casualty-history storage, scenario actions, AI, treaty/war, city
removal/revelation, nuclear strike, UI/audio and network services are declared
adapters. Selected event callbacks mutate a next link to test cached traversal;
they do not implement S02. A nuclear child returning zero is an explicit supplied
boundary, not a U07 strike test. Sound adapters record calls without reproducing
playback or all last-sound-global behavior. No complete history-ring, runtime UI,
network receiver, real city destruction or whole-turn claim follows.

No original executable run, fresh instruction export or saved annotation change
was made during U06. The independent source and historical fixture definitions
corroborate formulas, city-slot tests, draw order and return phases; their native
comparison scripts were read, not rerun here. Temporary harnesses are outside
this repository. X02 must retain independent fixtures and real integrations
before claiming reproducibility or complete original-program conformance.


Remaining joins:

- U02/U05 must execute real attack-entry/finish/cleanup around this parent,
  including cancellation after charge, moving-unit replacement and AI empty-city
  responses. U07 now joins this document's nuclear child to its real action and
  U01 deletion; full-parent nuclear delivery and live presentation remain open.
- V04/A01/A04–A08 must join actual war, senate, standing-goal and retaliation
  children. [C09](C09-city-capture-and-revolt.md#appendix-b--validation-and-remaining-joins)
  now exercises the capture parent with actual city deletion/revelation; the
  U06-to-C09 and real S02 event joins, casualty links and lifetime reuse remain.
- N02/N03/N04/N05 must join live participants, transport/receipt and synchronization;
  P02/P04/P07/P10 must join information/visibility, semantic dialogs, casualties
  and required completion/gameplay callbacks under X01; native sound/raster traces
  are optional. Full semantic integration is broader than adapter-backed source
  checks.
- X02 must retain independently reproducible seed/state fixtures, arithmetic and
  write-boundary cases, city-slot distinctions and complete execution traces.
  These checks do not close whole-game conformance obligations.
