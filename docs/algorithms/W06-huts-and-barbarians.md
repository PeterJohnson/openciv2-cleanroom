# W06: Huts and barbarians

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

## Target, scope and shared contracts

Target classic Civilization II Multiplayer Gold Edition **5.4.0f, patch 3**.
Appendix B records the evidence and validation limits.
This document owns hut outcomes, periodic barbarian creation, barbarian order
selection and city ransom demands. [U06](U06-combat-and-veterancy.md#final-effects-and-returned-movement-result)
owns the separate reward for defeating a barbarian leader, including its payment
and notification sequence; do not substitute that reward for the demand below.

[F01](F01-determinism-and-arithmetic.md) owns arithmetic and sampling laws;
[F02](F02-game-state-and-invariants.md) owns identity/state;
[F03](F03-rules-and-configuration.md) supplies rules and parsed configuration.
[W01](W01-map-geometry-and-spatial-state.md) owns geometry, D/B neighborhoods,
regions and the coordinate hut predicate. [U01](U01-unit-lifecycle-and-stacks.md)
owns unit creation, deletion, stack queries and relocation.
[U02](U02-movement-and-zones-of-control.md) owns movement, exhaustion and control queries;
[U03](U03-pathfinding-and-goto.md) owns goto direction selection;
[U04](U04-unit-orders-and-activation.md) owns order execution and activation. [C01](C01-city-lifecycle-and-ownership.md) owns city creation
and the weighted nearest-city query. [W04](W04-terrain-work-and-improvements.md)
owns pillage, [W03](W03-visibility-and-exploration.md) knowledge/visibility, and
[F04](F04-turn-sequencing.md) the phases that invoke this work.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

C08 owns facilities; V03 owns research eligibility and
advance grants. A04/A05 own surrounding AI dispatch and coastal target routing.
P01/P04/P06 own centering, resource parsing, dialogs and city windows; N02/N05
own synchronization, notifications and transport. Calls below are ordered
boundaries with their real effects, not assumed-success or inert queries.

Nominal inputs have valid live objects, initialized rules/neighborhoods,
canonical map coordinates, barbarian activity 0..3, difficulty 0..5, valid
region backing and initialized civilization summaries. Keep per-city counts,
global advance-known-by sets and rank summaries as supplied; do not replace
them with fresh recounts. Named unit/facility types mean the **fixed rules slots**
listed in Appendix A, even when custom rules rename or redefine them.

Use F01 reference results for ordinary arithmetic, signed division/remainder
truncating toward zero, signed16 turn/year and city-count fields, signed8 size,
role, movement-cost, attack/defense and counter interpretations where stated.
Owner masks use the original low-five-bit shift selection. Preserve narrowing
at stores. Children and callbacks can change live state; retain only explicitly
captured values. Unsupported reads require declared backing or a stop after
prior valid effects. [X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
excludes out-of-bounds writes and resulting corruption, including logical
recreation of adjacent-object damage.

All hut and periodic-spawning draws below use F01's **primary sampling law**.
Barbarian neighbor scoring uses its **secondary inclusive range** helper.
These sampling laws are not interchangeable; their generator contexts may be
independent. Descendant decisions retain their call-site state effects. Tests
supply controlled choices or versioned implementation contexts and external inputs.

## Hut entry and consumption

U02 retains destination hut presence **before relocation**. Relocation changes
the tile's occupant owner through U01. W01's hut predicate requires Unowned,
so that ownership change consumes the coordinate hut; there is no independent
stored hut flag to clear. Later, after movement revelation, city-window and
special-movement continuations, U02 calls this operation if its retained hut
result is nonzero and the live mover has Land domain.

`enterHut(u)` captures signed owner `c` and coordinate `(x,y)` from the unit.
If `c=0`, return. Otherwise query its live type's domain; return unless Land.
This helper does **not** retest hut presence, mark consumption, or validate
movement eligibility. A direct repeated call can award again. Its incidental
integer return is ignored by the known movement caller; no action-success
meaning is assigned to it.

Query nearest city with all owner/region/filter selectors unrestricted, publishing
the shared weighted distance `near`. Draw once: `outcome = draw remainder 5`.
Then evaluate this override in short-circuit order:

1. The captured owner is absent from the **live human mask**, or one additional
   draw has remainder 0 modulo 3. A human therefore consumes this draw even if
   its later city-count check fails; an AI consumes none here.
2. Its current city count is nonzero.
3. Read the hut region; that owner's recorded regional city count is zero.
4. Its regional policy/flags low seven components are zero, or `near>12`.
5. Recheck that recorded regional city count is zero.

If all pass, force outcome 0. There is no new outcome draw. Dispatch through
the redirects below until a terminal action finishes. A redirect is not a
recursive entry: it neither repeats the initial nearest query nor the initial
draw/override. Read shared `near` at the stated gates rather than retaining a
separate distance snapshot across callbacks.

### Outcome 0: tribe or redirected nomads

If `near<4`, redirect to 1. Otherwise, when the civilization has zero cities
and turn<50, scan live unit slots ascending through the live upper bound. Count
every live unit with role Settle and home NoCity, **regardless of owner**. If
more than one exists, redirect to 1.

Next query W01's owner-or-fertility value. If it is at most 11, redirect to 5.
If greater than 11:

1. If the live local-human-move flag is set, show SURPRISETRIBE with the
   information sprite, mode 0.
2. Create a city at captured `(x,y)` for captured `c`; retain result `city`.
3. If the **live year is at least 1000**, draw once and store
   `size=draw remainder 4 + 1` in that city. Set Temple. Then draw independently,
   in order: set Marketplace on remainder 0 modulo 2, Granary on remainder 0
   modulo 3, and Library on remainder 0 modulo 4. All four draws occur regardless
   of which optional facilities qualify.
4. If local-human-move is now set, redraw the coordinate's neighbors, broadcast
   DrawArea(x,y), then open that city window if the retained result is nonnegative.

The post-1000 size/facility writes precede the later nonnegative-city check.
If creation fails, the original can write outside the city array. This is an
**excluded bug**: after the already-consumed size draw, stop before that invalid
write unless a separately documented safe continuation exists. Do not corrupt
another object or invent a successful city. Before year 1000, failure does not
enter the award writes; the redraw/send still occurs if locally requested, and
the negative result suppresses opening the window.

### Outcome 1: mercenaries

Show SURPRISEMERCS if local-human-move is currently set. Select two candidate
fixed unit slots using world advance-known-by sets, not just the recipient's
advances:

| Candidate | Ordered construction |
| --- | --- |
| Mounted `M` | Start Knights. If nobody knows Chivalry, draw: remainder 0 modulo 3 selects Chariot, otherwise Horsemen. Within that branch only, if Polytheism is known, draw once more; odd selects Elephant. Then, independently and in order, known Monotheism replaces `M` with Crusaders, Leadership with Dragoons, Conscription with Riflemen. |
| Foot `I` | Start Fanatics. If Guerrilla Warfare is unknown use Musketeers; if Gunpowder is unknown use Legion; if Iron Working is unknown use Archers. These are successive independent tests, so the last replacement wins. |

Draw once: even selects `M`, odd `I`. Create that type for captured `c` at `(x,y)`.
If local-human-move is clear **and** live local observer differs from `c`, set
the new unit's home to NoCity on successful creation. Otherwise redraw neighbors
and broadcast DrawArea, even if creation failed. Thus human/local-observer and
other recipients have different home-city handling. Do not force all mercenaries
to be unsupported or infer the type from a strongest-unit search.

### Outcome 2: precious metals

Start amount 50. Draw once. Only on remainder 0 modulo 3, draw again and choose
25 if `draw remainder 10 - liveDifficulty + 2 < 5`, otherwise 100. If the live
year is **greater than 1000**, double the amount. This year boundary differs
from the tribe-city size/facility boundary at exactly 1000.

Add amount to captured `c`'s treasury with signed32 wrap. Set numeric text
argument 0. If local-human-move is set, show SURPRISEMETALS. After that callback,
if the live local observer equals `c`, redraw status information. No autoplay
check is added here; the local-human-move flag was supplied by the caller.

### Outcome 3: barbarians or nothing

If `near<4`, or `c` has no cities and turn<50, redirect to 1. Otherwise, for an
AI owner only, compare `powerRank[c]` with `powerRank[weakestHumanRank]`. The
second index is the stored **rank value used as a civilization index**; do not
resolve it through the inverse rank table. If strictly smaller, draw once;
remainder modulo 8 less than unsigned difficulty redirects to 0.

Next, if activity<3 and either turn<30 or (turn<50 and a new draw is even),
query nearest city **restricted to c**, then read the hut region. If `c` has
a nonzero recorded city count on that region and the now-published `near<24`,
show SURPRISENOTHING only when locally requested and return. Activity 0 does
not globally disable hut barbarians; it only participates in this early gate.

Choose `single = turn<50 OR (turn<75 AND new draw even)`, with short circuit.
Then show SURPRISEBARB if locally requested. Iterate `step=0` while `step<8`:

1. Direction is signed remainder `(liveTurn+step) remainder 8`. Use D[direction]
   about captured `(x,y)`, wrapping x once. Negative off-nominal indices need
   declared read backing or a stop; do not silently normalize modulo 8.
2. Require, in order, in bounds, no unit owner, no city owner, and non-Ocean.
3. For an eligible coordinate read terrain. Start rough type Legion, flat type
   Horsemen. Known Metallurgy changes them to Musketeers/Knights; known advance
   at Armor's **signed prerequisite** changes them to Riflemen/Cavalry; known
   Guerrilla Warfare changes them to Fanatics/Partisans. Apply in that order.
4. Choose flat if signed terrain movement cost<3, otherwise rough. Create for
   civilization 0. On success OR its visibility with the **hut tile's** live
   exploration mask, redraw the created tile, and in network mode broadcast
   DrawSquare for it.
5. If `single`, return after this eligible creation attempt, including failure.
   Otherwise, or after an ineligible coordinate, add
   `clamp(4-liveCityCount(c),1,4)` to step and continue.

The stride is recomputed after each attempt, no retry draw is made, and no
leader is specially added by the hut outcome. Children can change the turn,
stride, technology or knowledge before later visits.

### Outcome 4: scrolls

If turn is zero, redirect to 2 without an Invention query. Otherwise query
Invention for captured `c`; if held, redirect to 2. Otherwise draw once and
start at `advance=draw remainder 100`.

Make at most **999 eligibility calls**, testing the current advance for `c`.
On failure increment modulo 100 and continue. The search can revisit IDs; it
is not a one-pass set of 100. On success show SURPRISESCROLLS if locally
requested, then grant `(recipient=c, advance, source=c, flags=0,0)` through
V03 and return. Preserve those captured arguments across the prompt. After
999 failures redirect to 0 without a new initial outcome roll.

The 0→5→4→0 redirect cycle can repeat when no advance is researchable, nomads
are disallowed and the value stays low. Do not turn the 999-call limit into a
global termination limit or invent gold as an eventual fallback. A stricter
implementation may reject a demonstrated nonterminating input domain explicitly.

### Outcome 5: nomads

This is reached by redirection, not the initial remainder-5 draw. If Explosives
is known by anyone, redirect to 2. Otherwise scan live unit slots ascending and
count this owner's live Settle-role units with home NoCity. If count is greater
than `signedCityCount(c) arithmetic-right-shifted 3`, redirect to 4.

Otherwise show SURPRISENOMADS if locally requested, create the fixed Settlers
slot for `c` at `(x,y)`, and on success set home NoCity. If local-human-move is
now set, redraw neighbors and broadcast DrawArea regardless of creation success.

## City treasury share

[C09 owns the shared treasury-share query](C09-city-capture-and-revolt.md#shared-treasury-share),
including its two rounding orders, signed arithmetic and division boundaries.
The uprising and ship-target callers below retain their stated query positions;
this read-only operation consumes no random draw. Do not duplicate its formula
or add a probability clamp at the shared-query boundary.

## Periodic barbarian spawning

F04 invokes this before warming and before advancing turn/year, only when the
incoming turn is nonzero. The routine itself first returns if activity=0.
For difficulty<5, return while
`turn < ((3-activity)*3 + 30)*(5-difficulty)`.
For difficulty>=5, return while `turn < (3-activity)*3 + 15`.
Equality admits spawning. No draws occur on these early returns.

Set cadence mask `m=15` for activity 1, `m=7` for activity 2 or 3. If
`((turn+1) AND m)==0` and no civilization knows the advance at Armor's signed
prerequisite, attempt the sea raid below. Exhausting that search returns from
the **whole spawning operation**, skipping the city attempt.

After no raid or a completed raid, choose a city slot: zero without drawing
if the live city upper bound is at most 1; otherwise draw once and take its
remainder modulo the bound reloaded after the draw.
Only if the slot is below the live bound, live, and its owner is in the live
human mask, run the uprising below. There is no retry for a dead/AI slot.

Off nominal, entry activity>3 leaves the original cadence local uninitialized.
Require its actual incoming value or stop at its first use after earlier effects;
do not choose a default mask. A restrictive loader may reject such activity.
The same local remains captured if callbacks later change activity outside 1..3.
Signed prerequisite indices outside the declared advance array require supplied
adjacent read backing; never treat them automatically as known/unknown.

### Sea raid search

Start attempts=0. Each iteration:

1. If live width<=1 set x=0 with no draw; otherwise draw and take remainder
   modulo the width reloaded after the draw.
2. If live height<=7 set offset=0 with no draw; otherwise draw modulo
   `(reloadedHeight-6)`. Set y=offset+3.
3. Round x down to even, then add 1 if y is odd.
4. Increment attempts. If greater than 1000, return failure to the parent
   **before any tile query**. Reference evaluation generates the 1001st
   candidate's unused samples. They may be omitted under F01/X01 when doing so
   preserves significant surrounding reads/failures; this does not admit another
   candidate or perform the tile test.
5. Require Ocean, no unit owner, then regional water-body size at least 16.
   Retry on failure. No explicit in-bounds or city-owner test is added here;
   W01 map access supplies its actual bounds behavior.

For the accepted coordinate set
`b=clamp(trunc(turn/(250-50*activity))+1,1,5)` and
`n=b+(activity==3 ? 2 : 1)`. Select ship Trireme, replace by Caravel if Navigation
is globally known, then Frigate if Magnetism is globally known.

Select escort Warriors, or Archers when the base-rules feature is enabled.
If **a human** knows Iron Working use Legion. If Metallurgy is globally known,
or activity=3 and Chivalry is globally known, use Knights unless x is odd and
base-rules enabled, in which case use Crusaders. Finally, human-known Tactics
plus base-rules replaces it with Dragoons. Global and human-known tests differ.

For remaining=n-1 down through 0, create one unit for civilization 0. The first
is Diplomat/leader when remaining is nonzero; the others use escort. On each
successful creation call U04 sleep. Failed creations do not terminate the loop.
Then create the ship at the same coordinate:

- Failure: query first unit at that coordinate; if any exists, delete its whole
  stack with synchronization. Do not assume this is only the newly created units.
- Success: draw once; even makes the ship Veteran. Then U01 reinsert/relocate it
  in place with synchronization.

Return normal completion, including ship failure, so the parent can still
attempt an uprising. No separate explicit raid notice occurs in this producer.

### Uprising selection and moving search center

Start with the selected city. Draw once, then query its treasury share. Continue
only if `draw remainder 100 < share`; values above 99 make this gate certain.
Start attempts=0 and search as follows:

1. Draw x offset: `wrap(liveCityX + 2*(draw remainder 13) - 6)`.
2. Capture that city's current y, draw y offset, then use
   `capturedY + draw remainder 13 - 6`. Round x down to even and add y parity.
3. If in bounds, query nearest unrestricted city and **replace the city local**
   by that result. If negative, set attempts=200 and leave the loop.
4. Increment attempts. If `attempts>=liveActivity*50`, leave the loop before
   testing this candidate's eligibility.
5. Require in bounds, shared nearest distance>=5, no adjacent nonallied unit under
   U02's same-medium control query for civilization 0, non-Ocean, no unit owner,
   and region equal to the now-selected nearest city's region. Retry otherwise.

Retries use the **new nearest city as their center**. The resulting city need
not be the original human city or even belong to a human. The final allowed
attempt number is never tested for eligibility. In stock activity 1..3, no-city
attempts=200 guarantees rejection at the next limit test.

After the loop return unless attempts<liveActivity*50. For current activity
1..3 replace the retained cadence local by the nearest city's signed size `s`.
All three use the full size; apparent activity-1 and activity-2 divisions in
the original fall through to the same size assignment. For a live changed
activity outside this set, retain the earlier local value.

If s<=1 use count=0 without drawing; else draw modulo s. If activity=3 and
turn>=150, select surge result zero without drawing when difficulty>=9;
otherwise draw modulo `(10-liveDifficulty)`. On zero, let `h=trunc(s/2)` and
bonus=0 without drawing when h<=1, otherwise draw modulo h. Add bonus+h to
count. Clamp the resulting count to 2..99; call it `n`.

### Uprising types, creation and notice

Start main Chariot, alternate Horsemen. Draw once; remainder 0 modulo 3 swaps
them. The original sampled even if later advances replace both; omit the sample
only when the initial types have no remaining effect. Preserve the later advance
queries and their order. Set text string
argument 0 to uprising label 98; if n>10 and n>trunc(liveCitySize/2), use 99.
Apply these successive branches:

| Condition | Types and label |
| --- | --- |
| Globally known Gunpowder | Label 100; with base-rules enabled set main Archers, alternate Elephant. Otherwise retain types. |
| Globally known Industrialization | Label 101; main Cannon, alternate Musketeers. Within this branch only, if Armor's prerequisite is globally known, label 102 and alternate Riflemen. With base-rules enabled set main Artillery; additionally, if Guerrilla Warfare is globally known, use the next row. |
| Nested Guerrilla Warfare branch | Read government from civilization indexed directly by **strongestHumanRank**, without inverse-rank conversion. If government<Republic or the selected city's owner differs from founder, use Cavalry/Partisans. Otherwise retain main Artillery, set alternate Fanatics and label 103. |

Loop remaining=n down through 0, making **n+1 creation attempts**. The first is
Diplomat/leader. Later attempts use main when `(remaining AND 4)!=0`, alternate
otherwise: groups of four, not alternating individuals. Create for civilization
0 at the retained candidate coordinate. On success make Fanatics Veteran and
OR visibility with that candidate tile's live exploration mask. Keep each
creation result, including failure, as the latest result; do not retain the
last successful unit instead.

If the **final** result is nonnegative and autoplay is now disabled, copy the
selected city's name to string argument 1. If its owner is the live observer
and the candidate is explored by that observer, show BARBARIANS with the final
unit. Otherwise, if owner is nonlocal, network mode is active, owner is human
and that owner explores the candidate, send DrawSquare to that owner's recipient,
then notification kind 2 with two strings, no numbers, final unit and zero.
Resolve live owner/recipient at each send. Finally, if the selected city's
live owner is human, redraw the candidate locally, even if every creation failed
or autoplay suppressed the notice. Do not restore the original selected city.

## Barbarian order selection

[A04's ordinary chooser](A04-ai-ground-tactics.md#2-shared-entry-and-recovery-dispatch)
delegates immediately when its actor owner is 0. This
entry itself reads the live active-unit slot `u`, captures owner `c`, clears
orders to None unless they are Goto, then captures coordinates `(x,y)`.
Return value 1 bypasses its ordinary finish tail; 0 means that tail ran. It is
not a universal moved/survived flag: one deletion path still returns 0.

If y<2, y>=height-2, or the coordinate is out of bounds, call cargo-aware unit
deletion with synchronization and return 1. Polar tests short-circuit before
the bounds query. Otherwise query and retain origin enemy-control result `z`,
then capture type `t`, then query nearest unrestricted city `city`. Its shared
distance `near` remains live; later nearest-city queries can replace it.
Capture no independent permanent copy of `near`.

If t has nonzero hold capacity, take the transport branch below, regardless of
its domain or role. Otherwise take the land/nontransport branch. Read properties
from the captured type unless a step explicitly requests the actor's live type.

### Shared finish and selected-direction tails

**Finish:** if live order is Goto and both live target coordinates equal captured
entry `(x,y)`, clear orders to None. Return 0. This reads the retained slot even
after a deletion child; do not silently substitute a new identity or bypass the
documented slot access. Missing backing is an explicit stop.

**Select direction d, 0..7:** set order AiGoto, compute wrapped target x using
the live D[d].x and captured x, store signed16, then compute target y from live
D[d].y and captured y, store signed16. Run Finish. This does not itself move
the unit or exhaust movement; U04 executes the order later.

**Hold, direction 8:** store last direction NoDirection, exhaust movement,
then test whether captured type's live role is Defend, or the origin's current
features mean Fortress without City (not an Airbase). If so, set Fortifying;
if the live AI-garrison flag is also set, replace with Fortified. Run Finish.
Other roles can retain earlier order writes; Hold is not an unconditional
Fortifying assignment.

### Transport lifetime and adjacent landing

Query stack count using U01 mode 2, then Diplomacy-role count. If their retained
difference<2, destroy/redraw with synchronization and return 1. Increment the
unit's work/lifetime counter with byte retention; interpret it as signed8 and
if >30 destroy/redraw and return 1. A stored 127 increments to -128 and avoids
that comparison; do not replace it with an unbounded age check.

Scan directions D[0..7] in ascending order. For each wrapped in-bounds neighbor:

1. Try landing if retained city is nonnegative, shared near<9, neighbor is
   non-Ocean, has neither city nor unit owner, y>1 and y<liveHeight-2, and its
   region equals the retained city's region. These queries short-circuit.
2. On a landing candidate, if stack count<3, Attack-role count=0, Defend-role
   count=0, and near<3, destroy/redraw the ship and run **Finish**, returning 0.
   Otherwise perform the landing announcement/work below, then Finish. Either
   result stops scanning; it does not search for a better landing coordinate.
3. If landing did not qualify, query Ocean again. If Ocean and unit owner>0,
   immediately Select this direction to attack. The first qualifying neighbor
   wins. It can precede another direction's available landing.

For landing announcements visit civilizations v=1..7, checking live gates in
order: alive, human, explores neighbor, retained city's live owner equals v,
autoplay disabled. For each admitted v, query nearest unrestricted city from
the neighbor and **replace city**, then copy its name to string argument 0.
If v is local observer, only in mode 0 first publish stack visibility to v,
ensure the neighbor visible using captured actor owner c, and redraw it;
then show BARBARIANSLAND with u. A nonlocal v in network mode instead receives
notification 78 with one string, no numbers, u and zero. Continue the v loop
with the possibly changed city, mask and other live state.

After that loop OR u's visibility with the neighbor's live exploration mask,
set its AI task to UnloadRaid, invoke the shared cargo wake operation with
all 16 role bits and wake argument 0, then exhaust u's movement. There is no
direct relocation to the neighbor here. U04/U05/A05 own later cargo movement;
calling this an immediate disembark would omit that continuation.

### Transport target search

If no adjacent landing/attack was selected and live orders already equal Goto,
retain them and Finish. Otherwise start bestScore=0, selectedCity=None.
If origin is Ocean, capture its region as sea body; otherwise use no body filter.
Scan live city slots ascending through the live bound, admitting live cities
whose owner is nonzero. With a body filter, require the city's region borders
that captured water body through W01's region-border query.

For each admitted city query treasury share, then weighted diagonal distance
from captured origin to its **then-current** coordinates. Score is
`trunc(signed32(share+50) / signed32(distance+1))`. Retain only a **strictly
greater** score; first equal score wins. Scores<=0 cannot select a city.
If none selected, destroy/redraw u and return 1. Otherwise call A05's coastal
goto helper with u, RaidTransport task and the selected city's current coordinate,
then Finish. Do not add a human-owner or military-feasibility filter.

### Nontransport garrison and periodic removal

If shared near=0 or origin is Fortress without City, query stack count `n`,
then Diplomacy-role count `l`. Continue to roaming if `(n-l>1 OR z!=0)` and
captured type's live role is not Defend. Otherwise compute
`threshold = 2 + (liveNear==0 ? 1 : 0) + (z==0 ? 1 : 0)`.
Query stack count again; if it is at least threshold and signed attack>=signed
defense for t, continue roaming. Otherwise set Fortifying and Hold.
Without that initial city/fortress condition go directly to roaming.

At the start of roaming, if city>=0 and `(liveTurn+u) AND 3 == 0`, and origin
is non-Ocean or t now has a hold, query the city's region then origin's region.
If they differ or shared near>8, destroy/redraw u and return 1. This check does
not run every activation. A callback change to hold does not redo initial
transport dispatch.

### Pursuit and nonmilitary behavior

Initialize pursuitX=pursuitY=-3 (no bias). If city>=0, its region equals
origin's region and its live owner is human:

- Compute `dx=liveCityX-capturedX`. In a round world, negate dx when
  `abs(dx)>=arithmeticShiftRight(liveWidth,1)`; the half-world tie also negates.
  Set pursuitX to sign(dx).
- Set pursuitY to **sign(liveCityY+3)**. This is the recovered subtraction
  from the initialized -3 local, not from the actor's y. Preserve the ordinary
  southward bias; it is unrelated to the excluded overwrite policy.

With a nonnegative city, independently clear both biases back to -3 when its
owner equals c, when that owner's city count<2, or when its treasury<100.
If pursuitX remains set, write u's target x then y from the city's live
coordinates, call U03 goto-next-direction, and narrow its result into last
direction. The order is not changed to Goto by these direct target writes.

For signed role>4, before scoring neighbors:

1. Capture origin terrain and query stack count. On non-Ocean with count>1,
   query number of units matching t's live role; if <2, Hold without a preceding
   explicit Fortifying assignment.
2. On Ocean, if shared near<3, destroy/redraw and return 1.
3. Otherwise on land, query nearest unit of owner c, excluding u. If its shared
   distance<4, read that returned unit's type; if signed role<2, call U04's
   targeted goto with LeaderFollow task and that unit's live coordinate, then
   Finish. A missing returned slot with a qualifying distance needs backing or
   a stop, not a fabricated escort.
4. If no follower route, and `(liveTurn AND 7)+4 <= liveNear`, destroy/redraw
   and return 1. Otherwise continue to neighbor scoring.

### Neighbor scoring

Start bestScore=-999 and bestDirection=8. Scan D[0..7] ascending. Admit a
wrapped in-bounds neighbor only if `(t.domain==Sea)==(terrain==Ocean)`.
Thus a non-Sea custom type uses non-Ocean here, not a universal Land check.
Capture neighbor unit owner, then first unit, and initialize score=0.

For signed role<5:

1. Query neighbor city owner. For a foreign city, skip if origin is Ocean.
   Requery unit owner; if none, run the city-ransom sequence below. Its paid
   return or missing-resource early finish stops this chooser. Otherwise, if
   t is not Partisans, or a fresh unit-owner query finds no defenders, immediately
   Select this direction. It takes priority over all prior scores.
2. For defended foreign city and Partisans, skip this direction if
   `(liveTurn+u) AND 7 == 0`. Otherwise query own stack count; if <2, set
   Fortifying **before** testing the score. If bestScore<99 set bestScore=99,
   bestDirection=8. Continue scoring this direction; the hold is provisional.
3. If the neighbor's raw occupant owner differs from c, set score=4. If its
   Fortress component is set add 8; otherwise requery features and add 6 if
   either Irrigation or Mine component is set. This includes single improvements,
   not just combined Farmland. Draw secondary range [0,5] and add it.
4. If a first unit was captured, use its captured owner: an own unit is rejected
   when origin is land, otherwise subtract 20. A foreign unit is rejected when
   origin is Ocean. On land, Partisans reject foreign units with signed role<5,
   otherwise add 99; other types add 99 unless that unit's role is Diplomacy.
   These rejections occur **after** the range draw.
5. If retained origin z!=0, t's live role is Attack, and captured first unit is
   absent, query target enemy control. If nonzero and the actor's **live type**
   lacks IgnoreZoc, reject. Do not replace z by a fresh origin query.
6. Add 6 when u's live signed last direction equals this direction. Add 2 for
   each set pursuit axis whose value equals the sign of that live D offset.

For signed role>=5, set score=99 for a captured own unit, -99 for a captured
foreign unit, otherwise zero. Query nearest unrestricted city from the neighbor
(ignore its returned city but retain published shared distance); capture signed
terrain move cost; draw secondary [0,3]; then add moveCost + draw + 4*liveNear.
No military rejection/control tests are added to this branch.

Retain the candidate when signed score>=bestScore: **later equal scores win**.
Arithmetic wraps before comparison. After the full scan, if origin has a city
owner and its live stack count<=1 and bestScore<99, force Hold.

Otherwise, only if bestScore<99, query movement remaining. If it is at most
the live road movement multiplier, city>=0 and that city's current owner is
human, read origin features. If any Irrigation, Mine or Fortress component is
present, or t is Partisans and Road is present, call W04 pillage(u,choice=0),
then exhaust movement and Finish. Pillage's own continuation owns what is
removed; do not assume it succeeded. If that path does not run, use Hold for
bestDirection=8 or Select for the chosen direction.

### City ransom demand

This occurs during the foreign-city neighbor test, only after an empty-unit
query. Capture that neighbor's city owner `v`, but initially use the **retained
nearest city's size**, not yet the neighbor city's size. Demand is the larger
of 50, trunc(v's treasury/2), and 25 times that signed size. If powerRank[v]=7,
year>200 and v has more than four cities, replace demand by
`trunc(liveGold(v)/100)*100`; if that result<1000 use liveGold(v)+1 instead.
This can make the strongest civilization unable to afford its demand.

Set numeric argument 0. Only if mode<3, v is human, and its live treasury>=demand
attempt the dialog path. There is no network demand/acceptance substitute here.
Otherwise continue the neighbor's attack/Partisans logic.

For an eligible dialog, replace retained city by `cityAt(neighbor)`, copy its
name to string argument 1, change to scenario directory and try CITY's
BARBARIANS section. On failure change to home directory and retry. If both fail,
run Finish immediately: no attack selection, count-line read or dialog. On
success change to home directory, read one line with mode 1 and parse its signed
decimal count. Return/EOF from that line read is not checked; parsing uses the
live shared line buffer. P04 supplies text/stream semantics.

If count<=0, continue the neighbor logic. Otherwise capture city's y then x,
compute signed remainder `r=(3*y+5*x) remainder count`, and read lines for
i=0 through r inclusive. Negative r performs no message-line reads. Copy the
live line to string argument 0, ensure the city's **then-current** coordinate
visible using v, and if autoplay is now disabled show RANSOMCITY with u.

On a nonzero dialog result, subtract retained demand from v's **live** treasury
with signed32 wrap, redraw status, then scan B[0..19] in order around the
retained city's live coordinates, wrapping x each time. For each in-bounds
coordinate, query first unit; if nonnegative and its live owner is 0, delete
that **entire stack** with synchronization, then redraw the tile. The center
B[20] is excluded. Return 1 directly, without Finish, even if the actor was
not among those removed. No affordability recheck, barbarian treasury credit,
city transfer or universal delete-all-barbarians operation is added.

Autoplay, cancellation or zero answer resumes ordinary neighbor processing.
The resulting attack selection can still target that city. The text/resource
calls, directory changes, nearest-city replacement and prior effects are not
rolled back.

## Examples and conformance obligations

For the examples below use nominal rules, valid backing, no callback mutation,
and inert presentation boundaries unless specified. Listed primary values are
controlled F01 sampling-law codes, not seeds. Their list positions name decisions
in reference evaluation; they do not impose a global raw draw sequence. Include
descendants' semantic choices separately. Raw generation and unused samples are
outside required comparison; preserve meaningful attempts and dependencies.

| Entry state / supplied draws | Required observations |
| --- | --- |
| Hut gold, human owner, initial selection 2, override draw 1, amount draw 1, year 1000 | Three named decisions, amount 50. At year 1001 the same choices give 100. |
| Hut tribe, AI with an existing city on the hut region, nearest distance>=4, value>11, year 1000; draws 0,3,0,0,0; successful city creation | Outcome stays 0; city size 4, Temple, Marketplace, Granary, Library; five named decisions. Year 999 uses only the initial choice and no late-city award. |
| Hut scrolls, AI without initial override, nonzero turn, no Invention; draws 4,99; only advance 2 researchable | Test IDs 99,0,1,2 in order, grant 2, two named decisions. With all tests failing, make 999 calls then redirect to 0. |
| Hut barbarians, activity 3, turn 80, four cities, nearest distance>=4, no AI redirect and no type-upgrade advances; draw 3; all eight neighbors eligible | Eight creation attempts, D[0] first, stride 1, no extra semantic choices; copy hut exploration into each successfully created unit. |
| Periodic generation, activity 2, difficulty 5, turn 23, undiscovered Armor prerequisite, width 20, height 12, every candidate land | The first 1000 attempts test coordinate choices; attempt 1001 makes no tile query and returns failure. Reference evaluation generates 2002 coordinate samples, but the final unused pair may be omitted; no uprising city-selection/chance decision follows. |
| Transport chooser, usable nonpolar Ocean origin, stack containing ship+combat escort+leader, Goto elsewhere, no adjacent landing/enemy, counter 127 | Counter becomes stored 128/read -128; retains unit and Goto; returns 0. Counter 30 instead becomes 31 and deletes with return 1. |
| Nontransport, no city/fortress/periodic removal/pursuit, eight legal neighbors with identical base score and equal secondary draws, no last-direction bonus or pillage | Eight candidate-specific range [0,5] choices, choose D[7] on the final tie and set AiGoto. |
| Empty adjacent city, retained nearest size 4, owner treasury 150, ordinary power state, human nonnetwork owner; one text variant; accepted RANSOMCITY | Demand 100, treasury 50, remove owner-0 stacks within B[0..19], status then deletion/redraw, return 1. Decline keeps 150 and continues attack direction selection. |

Additional conformance must cover callback changes, human/global technology
distinctions, full custom type tables, generation capacity failures and all
partial-result paths. In particular preserve:

- Hut initial override order, global versus owner-only unsupported-settler
  counts, exact year comparisons, candidate failure without reroll, and the
  possibly repeating scroll/nomad redirect cycle.
- Nominal creation failure separately from the excluded late-city overwrite;
  supplied adjacent prerequisite reads separately from malformed unbacked input.
- Uprising center changes across retries, last-attempt exclusion, all activity
  and technology type sequences, final-result notice gating, count+leader,
  and direct rank-index use. Do not silently correct rank or pursuit formulas.
- Transport lifetime wrap, first adjacent landing/attack priority, first-score
  target ties versus later-score neighbor ties, live announced-city replacement,
  deletion returning 0 versus 1, and deferred cargo movement.
- Ransom directory fallback, EOF/line-buffer behavior, nonpositive variant count,
  signed negative variant, dialog callbacks changing gold/owner/map state,
  removal of whole stacks and center exclusion.

## Appendix A. Original encodings and bug evidence

These mappings assist validation; the cleanroom need not use these layouts.

| Item | Reference mapping |
| --- | --- |
| Main entries | Hut `0058f040`, periodic spawning `00485c15`, barbarian chooser `005351aa`, treasury share `00579dbb`. |
| Supporting entries | Hut predicate `005b8ffa`; targeted goto `00531607`; cargo role wake `00531567`; combat leader reward is U06's `unit_resolve_attack_ransom`. |
| Rules/state | Base-rules/expanded barbarian types flag `RULE_BASE_1=0001`, normally set. Flat world `8000`. Global signed16 turn at `010`, year `012`; unsigned difficulty `020`, activity `021`, human mask `023`; global discovery sets `09a`; strongest/weakest human rank bytes `138/139`, rank-by-civilization table `13a`. Offsets hex. |
| Fixed unit slots | Settlers 0, Warriors 2, Archers 4, Legion 5, Musketeers 7, Fanatics 8, Partisans 9, Riflemen 11, Horsemen 15, Chariot 16, Elephant 17, Crusaders 18, Knights 19, Dragoons 20, Cavalry 21, Armor 22, Cannon 24, Artillery 25, Trireme 32, Caravel 33, Frigate 35, Diplomat/leader 46. |
| Advance slots | Chivalry 11, Conscription 17, Explosives 28, Guerrilla Warfare 34, Gunpowder 35, Industrialization 37, Invention 38, Iron Working 39, Leadership 42, Magnetism 45, Metallurgy 51, Monotheism 55, Navigation 57, Polytheism 64, Tactics 81. Armor's signed prerequisite is read from rules slot 22, not fixed to Mobile Warfare 53. |
| Facilities | Granary 3, Temple 4, Marketplace 5, Library 6. |
| Orders/tasks | None `ff`, Fortifying 1, Fortified 2, Sleep 3, Goto 11, AiGoto 27; UnloadRaid task `55`, LeaderFollow `58`, RaidTransport `70` hex. |
| Flags/features | Veteran `2000`, AI-garrison `0100`; City `02`, Irrigation `04`, Mine `08`, Road `10`, Fortress `40` hex. Fortress-without-City tests `(features & 42)==40`; scoring's `0c` test accepts either Irrigation or Mine. |
| Region summaries | Civilization-relative city-count bytes `292+region`, policy bytes `352+region`; world water-body signed word at region-record offset 4. Ordinary region bounds/backing remain W01/F02's contract. |
| Read alias inputs | A signed Armor prerequisite outside 0..99 addresses the corresponding byte relative to game offset `09a`. Independent fixtures include negative indices still within the saved record. Supply the actual logical backing value; do not perform an unsafe host-language array read. |
| Notices | DrawSquare message `72`, DrawArea `75` hex; uprising text kind 2 with two strings; landing text kind `4e` with one string; no numbers in these two network text messages. Local information notices use the information sprite and mode 0. Labels 98..103 select uprising descriptions. |

The failed late hut-city award reaches the size store at `0058f34d` even for a
negative creation result. Original city records are 88 bytes and the size field
is at offset 9. This address arithmetic identifies an **excluded overwrite
bug**, not a required alias into preceding state. Record rejection/stop before
the invalid store; retain earlier prompt, creation effects and size draw. The
subsequent facility calls are not automatically safe merely because the invalid
size store was omitted.

No nominal behavioral return is inferred from hut helper residual registers.
Some paths retain a network return, arithmetic temporary or uninitialized
local; the known movement caller ignores it. ABI diagnostic work must state its
separate boundary and inputs instead of making those values outcome IDs.

## Appendix B. Sources, validation and remaining joins

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`, inspected at reference checkout
`fc2155ccdb2123c1137babab6fa39e38152278c9`. The relevant game, AI, city and
return-value sources have no changes between those revisions. Independent
evidence is `civ2` revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. Neither tree was modified.

| Family | Primary source |
| --- | --- |
| Hut dispatcher and outcomes | [retvals.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/retvals.cpp): `unit_enter_hut`, five `hut_*` helpers. |
| Spawning and global caller | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): `game_spawn_barbarians`, sea-raid/uprising helpers, `game_advance_turn`. |
| Barbarian policy and dispatch | [ai.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/ai.cpp): `ai_barbarian_unit_move`, six `ai_barb_*` helpers, `ai_unit_choose_orders`. |
| Treasury share | [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp): `city_treasury_share`. |
| Movement/cargo/leader reward | [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp): movement relocation/finish, targeted goto, role wake, combat ransom and U01 primitives. |
| Hut/owner and encodings | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp), [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h), [game types](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h). |

Independent `src/village.cpp`, `src/barbarian_generation.cpp` and
`src/ai_barbarian.cpp` corroborate the ordered branches, signed prerequisite
reads, full-size uprising fallthrough, rank-index quirk, pursuit-y producer,
selection ties, ransom text flow and distinct deletion returns. Its stricter
activity/backing checks are implementation limits, not inferred original
validation. Its ability to represent a wrapped failed-city store does not
override X01's exclusion of that bug.

A temporary native harness extracts **20 additional routines**: the complete
hut dispatcher/helpers, spawning parent/helpers, barbarian chooser/helpers,
treasury query, targeted goto, role wake and hut predicate. It retains **92
inherited routines**, including actual U01 creation, stack queries/deletion,
relocation, W01 tile mutation and the nearest-city operation now owned by C01.
Optimized and unoptimized AddressSanitizer/UndefinedBehaviorSanitizer builds both pass **125
guarded assertions**. Signed-char/wrap semantics are enabled, strict-alias
optimization disabled, and leak detection disabled for the ptrace environment.
Counts identify extracted bodies, not coverage of every body or branch.

Finite checks cover treasury rounding, human extra hut draw, year 999/1000/1001,
mercenary replacements/home, nomads, scroll wrapping/999 failures, hut barbarian
turn gates/stride/visibility, all stock periodic start thresholds, exhausted sea
search, successful raid with real unit creation/sleep/reinsertion, a successful
uprising, polar deletion, garrison Hold, later-score tie, transport counter wrap,
and accepted/declined city ransom with real stack deletion. City/facility/advance
production, bounded map/region predicates, control queries, goto/shore routing,
UI/text/directory services, network messages and their returned values are
declared adapters. Secondary range results and selected primary results are
scripted; unscripted CRT draws use F01's recurrence. One source local that
holds a tile pointer is widened for the native host. The fixtures use nominal
coordinates and retain the specified stored counter widths. These are source checks, not original-executable runs or a
complete movement/turn/AI campaign. Invalid city-award writes are not executed
or claimed as successful conformance cases.

Independent `tests/village.py` defines original/source comparisons for outcome
and caller boundaries, signed input/read cases, mercenary type precedence,
late callback reads, scroll scans and limited saved-state outcomes. Its report
explicitly distinguishes captured prompt/creation/grant/message boundaries
from complete runtime behavior. `tests/barbarian_generation_fixture.inc`
defines nine generation-prefix cases and four uprising-tail cases, including
capacity exhaustion, draw counts, full-size fallthrough and nonstock difficulty.
`tests/ai_garrison_intent.py` describes scoped original/source returns including
nine barbarian garrison, three periodic-removal and six ransom cases. Those
fixtures and their stated limits were inspected, **not rerun** here. Fixture
definitions are not new evidence of a passing original session.

No fresh original-executable run, instruction export or saved annotation change
was made. X02 must retain independently reproducible fixtures in this repository;
the temporary source harness is external. Complete joins remain with U02 hut
arrival/consumption, F04 global/civilization phases, C01/C08 creation/facilities,
V03 grants, A04/A05 chooser execution/coastal routing, actual text/dialog/window
services and N02/N05 authority/transport/receipt. [C09](C09-city-capture-and-revolt.md#appendix-b--validation-and-remaining-joins)
owns and exercises the shared treasury query; W06 caller timing remains as
specified here. Include the excluded-overwrite stop, uninitialized cadence input, altered read backing,
callback lifetimes and nonterminating redirect case as separate limits.
These checks do not close whole-game or original-program conformance.
