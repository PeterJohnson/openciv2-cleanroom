# Game setup and starting positions

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in F05 |
| --- | --- |
| Gameplay | Chosen setup content/options, starts, roster, balancing, starting advances/units and fresh-game state; keep setup calculation and RNG laws. |
| Original files/content | F03 rules and F06 selected map/scenario/save interpretation; setup preferences use their declared persistence fields. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Wizard screen order, temporary menu state, portrait buffers, native callback staging and prior-game initialization residue. |

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

**ID:** F05.
**Target:** Civilization II Multiplayer Gold Edition **5.4.0f, patch 3**, under
[X01](X01-classic-compatibility-contract.md).

This specification describes single-player new-game setup: world and player
choices, new-game resets, civilization initialization, starting-site search,
compensation for unequal starts, and accelerated starts. It includes the
ordering and inputs of city, unit, research and visibility operations; their
complete mechanics remain with their owning documents. Loading a saved game or
starting a scenario is a different path: [F06](F06-save-load-and-file-formats.md)
owns loading and [S01](S01-scenario-loading-and-parameters.md) owns scenario entry
and selection. [N01](N01-multiplayer-session-and-lobby.md) owns network setup,
its distinct option/tribe-choice state machines and host/client continuation;
[N04](N04-hotseat-and-player-handoff.md) owns local multiplayer startup/handoff
and the shared multiplayer opponent selector. Those callers reuse the world
generation and initialization algorithms here without adopting the single-player wizard's
cancellation rules.

Appendix B distinguishes original behavior from port fixes, including an
unresolved incoming value that can matter during an ordinary advanced start.
The main algorithm states the required inputs and behavior.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
semantic publications and failure effects. Only dependencies within the binding
scope above constrain evaluation; storage/API choices need no original-trace
equivalence proof. External file/wire definitions remain binding at adapters.

Setup choices, retained reset state, failed placement attempts and child
publications retain their dependencies. Independent sampling does not authorize
moving a start-site test across a map, unit, city or civilization mutation.

## Compatibility policy

X01's [presentation policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy)
permits a redesigned setup flow while preserving resolved world/player options,
setup dependencies, publication/cancellation effects and the original setup
algorithm. Native controls, portrait positioning and cosmetic failures need not
match. Validate unsupported input under X01 without excluding nominal custom
worlds. Starting-site search, compensation, acceleration, AI and W02 terrain
generation are unchanged; new artwork does not authorize different world rules.

## 1. Inputs, outputs and shared operations

Inputs are the current process state, preferences, loaded rules and leader
records, ordered dialog responses, controlled choices or implementation random
contexts, clock samples, and
optional map-file contents. Previous gameplay is excluded: a second game must
be equivalent to a fresh-process game with the same selected inputs and logical
random choices, apart from stale-reference identity generations. Use [F02](F02-game-state-and-invariants.md) for
identity, live slot bounds and retained state, and
[F03](F03-rules-and-configuration.md) for parsing, rule references, preference
loading and option meanings.

The nominal setup uses difficulty 0…5, one human civilization in slots 1…7,
2…6 opponents, barbarian activity 0…3, one of the 21 tribes, binary leader
gender, one of four city styles, and standard or supported custom rules.
Generated-world parameters are −1, 0 or 1. A nominal premade map has a valid
even doubled width, positive height, supported terrain and region values, and
sufficient data/storage. Starting hints ordinarily identify valid land sites
in canonical coordinates; the original checks substantially less than this.
The file layout and loader's partial-failure contract belong to F06.

The successful output is a world with selected civilizations, their initial
units/advances and visibility, optional accelerated cities, and an initialized
turn state ready for [F04](F04-turn-sequencing.md). Setup also changes preference
state/files, mutable leader records, retained workspaces and sampled decisions.
Cancellation is not a rollback. Earlier writes survive unless a later step explicitly overwrites them. Random
context continuation follows the declared implementation policy; cancellation
does not undo already published choices or other game state.

Arithmetic uses [F01](F01-determinism-and-arithmetic.md). Here `R()` is one draw
from its primary sampling law; `s16`, `s32` and `u8` denote the corresponding modular
reductions. Division truncates toward zero; `>>` on signed values is arithmetic.
Original startup reseeding is optional F01 Appendix D evidence. Setup uses the
implementation's declared random context; map-pattern seeding remains the
separate clock operation defined by F01/W02.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

### Spatial vocabulary

Use [W01's coordinates and lookup](W01-map-geometry-and-spatial-state.md#2-coordinates-lookup-and-lifecycle),
including rectangular bounds, parity aliasing and single-correction `wrapX`.
The starting-distance metric `D` is W01's
[weightedDistance](W01-map-geometry-and-spatial-state.md#4-distances-directions-and-geometric-selection);
the accelerated road gate uses `stepDistance`. `B` and `Dir` are W01's
[ordered radius and direction tables](W01-map-geometry-and-spatial-state.md#3-ordered-neighborhoods).
B[20] is the center; section 10 uses the extended B[0…44] range.

Use [U01's nearest-unit query](U01-unit-lifecycle-and-stacks.md#nearest-unit-and-required-city-queries)
and [C01's nearest-city query](C01-city-lifecycle-and-ownership.md#weighted-nearest-city),
including their published distances and last-equal-distance selection. The
unit query in balancing filters by civilization; the city queries here have
no owner, region or port filter.

Terrain, river, resource, hut, occupant-owner and region queries are distinct.
In particular, the river count in section 8 is not a resource count. The search
uses the stored radius-claimant/fertility value and region fertile-tile count
produced by W02, without recomputing them from current terrain.

## 2. From the main menu to a new world

Before each new-game attempt, execute the X01 fresh-game boundary in the
session owner. Replace all world records, pools (including free-slot payloads),
map/knowledge/workspaces, events, passwords, locks, histories, AI goals and turn
continuations. Reset mutable rule/text/name state and all starting hints and
file-load scratch. Preserve application directories/screen configuration, saved
preferences, immutable content caches and identity generators only. Finish or
cancel pending operations and required network effects first, then revoke old
clients. Load the chosen ordinary content into fresh rule/text state.

The main menu presents choices; it does not refresh old AI goals or redirect old
units. The historical menu refresh/standing-clear sequence is not a new-game
dependency. No A01/U04 implementation is needed to establish empty initial goals.
Load/resume is a distinct path with explicit imported state and F06 semantics.
Cancelled or failed setup may leave documented edits within that attempt; the
next new-game attempt always crosses this fresh boundary again.

For Random World, Premade World or Customize World:

1. Load the ordinary rules through F03.
2. Premade World runs the map dialog in section 3; the other two run the size
   dialog. Remember Generated, Premade Cylindrical or Premade Flat as the source.
3. Customize World runs the parameter dialogs. The other choices consume the
   five random parameter draws below, including Premade World.
4. On success, run the player/rules wizard in section 4.
5. After its final acceptance: save preferences; reload advances, units and
   improvements, in that order; stop menu sound; call W02 with `keep=0` for
   Generated and nonzero for either Premade source; initialize the new game
   (sections 5–8); apply acceleration if the retained acceleration choice is
   nonzero; finally show the human civilization introduction.

The source value distinguishes premade topology for initial checkbox defaults;
W02 itself treats both nonzero values as “retain terrain and finalize.” The
current Flat World rule controls its wrapping/polar operations. Do not infer
that every premade map remains byte-for-byte unchanged.

The final civilization introduction collects at most 12 known advances by
scanning identifiers downward, then lists the collected names in reverse
order, each followed by comma and space. It omits advances whose two
prerequisites are both No, never examines advance 0, and begins with an extra
probe at identifier 100 which the knowledge predicate rejects. It displays
the leader and civilization names without granting advances or updating the
calendar. Appendix A records the extra table read behind that probe.

The five parameter decisions use these exact formulas:

```text
landMass = R()%3 - 1
landForm = 0
if R()%3 == 0: landForm = landMass
temperature = R()%3 - 1
climate = R()%3 - 1
age = R()%3 - 1
```

If size/map selection fails, return without parameter choices or the wizard.
The original five discarded choices and overwritten setup scratch are optional
reference behavior under X01/F01; a new attempt starts fresh. Failure inside
Customize World's parameter dialog also returns without the random alternative.
Keep accepted preference edits, not abandoned simulation state.

After successful setup, the application creates the normal map/status/city/
pedia windows, begins the session, marks setup complete and calls F04. In
single player, session-begin sets the session-active state; it does not perform
upkeep, grant contact or share starting visibility. The ordinary entry has no
resume marker. The fresh-game boundary discards continuation fields from every earlier load
attempt; only an explicit load/resume may install such state.

## 3. World size, parameters and premade maps

### Generated sizes

The size dialog first clears the game-status flags, including map-size and
outcome classifications. It accepts row width U and height H:

| Preset | U | H | Tiles |
| --- | --- | --- | --- |
| Small | 40 | 50 | 2000 |
| Normal | 50 | 80 | 4000 |
| Large | 75 | 120 | 9000 |

Custom input uses decimal conversion, stores each result in the signed cyclic
domain −32768…32767, **then** accepts only `20 <= U,H <= 250` and
`1000 <= U*H <= 10000`. Each width/height prompt starts at 50/80, including
an invalid-input retry or reentry after cancellation;
invalid values repeat the custom dialog, cancellation returns to the preset
menu, and failure to load the custom dialog exits with failure.

Accepting an ordinary preset remembers its choice in the map-size preference.
Accepting Custom preserves that remembered preset. The size helper does not
itself save the preference file. The independent Windows UI/file observation
linked below confirms Custom preserving Large and Small being remembered by
the subsequent parameter-completion save.

On leaving the size helper, even on cancellation, add the Large classification
if `U*H>5999` and Small if `U*H<3000`, using the currently stored dimensions.
Then store doubled width `W=s16(2*U)`. It does not skip these mutations merely
because its return is failure. Preset choice 0 selects Small, 2 selects Large,
and any other nonnegative ordinary preset result selects Normal.

### Customized parameters

Ask in this order: land mass, land form, climate, temperature, age. An ordinary
choice 0…2 becomes −1…1 and stores that **normalized value**, not the row index,
in the corresponding preference. The remembered −1/0/1 selects row 0/1/2 on
the next entry. F06 stores these preferences in land mass, land form,
temperature, climate, age order. A negative
choice leaves the already-written `choice−1` value and returns failure.
A Random response consumes one draw at that position and leaves that preference
unchanged. Land form uses `0`, replaced by current land mass when `R()%3==0`;
the other four use `R()%3−1`. Random climate therefore precedes temperature in
this route, unlike the main menu's five-draw route. Save preferences only after
all five succeed.

An [independent Windows observation](../m1-original-setup-observations.md)
records the repeated custom-size defaults, acceptance of 65586×80 and exact
normalized CIV2.DAT values before/after ordinary and Random parameter choices.
It does not observe private random outcomes or cancellation-time world state.

### Premade-map preparation

After F06's map read succeeds:

1. For every tile, clear improvements, the entire city-radius metadata byte
   (both the claimant and the low five retained bits), visibility and region;
   set occupant owner Unowned and fertility zero. Clear remembered maps for
   civilizations 1…7. Retain terrain/river data and the map header.
2. If the pattern seed is below 2, replace it from the clock. Otherwise ask
   Use Seed; a zero response replaces it and a nonzero response retains it.
   This is not a primary random draw.
3. Scan tribe hints 0…20 for any nonnegative x. If one exists, ask Use Start
   Locations. A zero response clears **all** x/y hints to Absent. The prompt's
   existence does not imply the coordinates have been validated.
4. Set the Flat World rule from the loaded flat marker and return success.

The wizard can subsequently replace that rule. This path does not call the
size dialog, does not classify area as Small/Large and does not generally
clear game-status flags itself. The fresh-game boundary has already removed
prior classifications/outcome flags; only current-attempt flags may be retained. W02 later assigns regions/connectivity/
fertility, clears improvements again, and performs its topology-specific
finalization. It does not select civilizations or use the start hints itself.

An unreadable or failed map read reports failure and returns to setup. Reads,
allocation and prior normalization are not rolled back. Port-added header/tile
validation is listed in Appendix B rather than imposed as original behavior.

An [independent original-game observation](../m1-original-premade-observations.md)
resolves the radius-byte scope: an authored map with `0xff` in every radius
byte saved all 4,000 bytes as zero after ordinary premade setup, before the
first human action and with no cities. This is a whole-entry-path observation;
attribution to preparation also relies on W02's retained-map contract, which
preserves radius metadata. It does not change W01's narrower claimant setter.

## 4. Player, tribe and opponent choices

### Wizard state and cancellation

On entry, restore leader default-name references, clear the reserved setup
field, and initialize local `pickOpponents=0, acceleration=0` **once**. The
ordered dialogs and their Back/Cancel destinations are:

| Dialog | Normal result | Negative/cancel result |
| --- | --- | --- |
| Difficulty | Set difficulty and its preference. | Return setup failure. |
| Opponents | Resolve opponent count below. | Repeat from Difficulty. |
| Barbarian activity | Resolve activity below. | Repeat from Difficulty. |
| Rules | Apply standard rules or enter advanced checkboxes. | Return setup failure. |
| Advanced checkboxes | Read their resulting state; the modal return itself is ignored. | Same processing of resulting checks. |
| Acceleration | Store 0, 1 or 2; 0 means no acceleration. | Repeat Rules, retaining this negative local and earlier effects. |
| Gender | Choose the leader-name/title variant. | Repeat from Difficulty. |
| Tribe | Select one of 21 tribes in rule-table order. | Repeat Gender. |
| Leader/custom tribe names | Apply accepted strings. | Repeat Gender; a checked text-load failure returns failure. |
| City style | Store one of four styles. | Repeat Gender. |
| Selected opponent tribe | Store that slot's override, or Random. | Repeat City Style, then redraw the opponent slots. |

Difficulty, opponent count and other accepted choices are written immediately.
Restarting Difficulty or Rules does not reinitialize the two entry-time locals.
In particular, Standard Rules writes the standard rules and skips the advanced
branch; it does **not** clear a previously selected `pickOpponents` or
`acceleration`, even a negative acceleration retained from cancellation. The
usual first-pass Standard Rules route has both zero. Do not replace this state
machine with an atomic form submission and claim identical cancellation behavior.

### Numeric and rule choices

- Ordinary opponent choice k means `6−k` opponents and updates its preference.
  Random means `R()%5+2` and does not replace that preference.
- Ordinary barbarian choice sets activity and preference directly. Random
  consumes one draw: at Chieftain/Warlord, `R()%2+difficulty`; at Prince/King/
  Emperor, `R()%3+1`; at Deity, `R()%2+2`.
- Standard Rules enables the classic base rules and Normal Combat, with Flat
  World, Bloodlust and No Restart Eliminated off. Advanced starts with saved
  rules, initially forces the Flat checkbox off/on for the two premade sources,
  then reads Simplified Combat, Flat World, Pick Opponents, Accelerated Start,
  Bloodlust and No Restart Eliminated in that order. Pick Opponents starts
  unchecked on each entry. The ordinary rule meanings are in F03.
- Save advanced rule preferences. For a premade source, retain the preference's
  previous topology choice while replacing its other rule choices; the active
  game uses the new checkbox state.
- Accepted Rules, Gender and Tribe choices replace their corresponding ordinary
  preferences. Standard Rules retains the saved advanced-rule bits and saved
  acceleration choice. Advanced initializes Accelerated Startup from whether
  that saved choice is nonzero; its year dialog initially selects that choice.
  Accepting a year replaces the saved choice, while canceling leaves it intact.
  Clearing Accelerated Startup sets both the local acceleration and its saved
  choice to zero without displaying the year dialog.
- For acceleration's year labels, set first-launch turn Absent and use F04's
  calendar at turns 0, 21 and 41, displaying magnitudes. This happens before
  the new-game scenario reset, so incoming calendar state is observable.

### Human identity and names

Before each Gender dialog restore all 21 tribes' default leader, plural,
adjective and both sets of government-title references from custom markers.
The selected tribe's preferred color becomes `playerCiv` and `localCiv`;
set the human set to just that slot. Store its tribe, gendered portrait and
chosen gender/name variant. Copy its default leader name into the civilization's
custom name and mark that leader-name reference as custom.

The ordinary Name dialog edits just that name. Custom Tribe additionally copies
and marks the tribe's plural name, adjective and seven titles for the selected
gender. It can revisit name edits, open a seven-title editor, or invoke portrait
selection. Accepting the title editor copies all seven entries; cancellation
leaves them unchanged. Opening Titles retains the current three name edits;
returning from that child resumes the name dialog with those edits. Nominal
name/title edits allow at most 23 bytes plus
termination. The selected city style is a civilization override; it does not
rewrite every tribe's style. P03/P04 own the portrait and dialog interactions.

Do not infer a human civilization's general female-status flag was normalized
here: this path writes the leader variant and portrait. The AI selection path
below separately clears/sets its civilization female flag.

### Opponent slots and overrides

On accepting City Style, set `alive = human union {barbarians}`. Repeatedly draw
`slot=R()%8` until the requested number of distinct, previously absent slots
have been added. Draws hitting slot 0, the human slot, or an earlier opponent
are discarded but consumed. There is no finite retry cap.

With Pick Opponents, visit admitted nonhuman slots 1…7 ascending. Offer Random,
then the tribes whose preferred color equals that slot, in tribe order. A
selected tribe becomes that slot's pending override; Random leaves any earlier
pending override unchanged. Canceling later can leave earlier overrides set.
The ordinary initialized override is Absent. Slots are selected before tribes;
this is not a uniform draw from all possible sets of leaders.

Independent [Windows UI and preference-file observations](../m1-original-player-setup-observations.md)
confirm advanced-modal cancellation, remembered selection/acceleration choices,
name retention across Titles, and discarded canceled title edits. These limited
observations do not replace the remaining state and sampling contracts above.

## 5. New-game reset and initialization order

After W02 completes, execute the following in order. Fields not written here,
in the outer boundary, or in a child operation remain unchanged.

1. Reset scenario parameters: no flags, empty name, technology paradigm 10,
   zero calendar increment/start/limit/objective civilization and four zero
   victory thresholds. Clear destroyed-civilization history's count, turns,
   killer slots and name starts. Clear civilization color-record flags.
2. Clear all 21 tribes' usage counts and city-name cursors, for human and AI
   tribes alike. Selected content and accepted custom names belong to this attempt.
3. Set turn 0, year −4000 and first-launch turn Absent. Copy preference game
   options with Cheat Mode off. Chieftain forces Tutorial and Instant Advice
   on; other difficulties force Tutorial off while retaining Instant Advice.
   Clear transient and shown-event flags; copy message options; select movement
   mode. Set city/unit bounds zero, active unit 0, turn civilization 0, view-as
   civilization Absent, whole-map reveal off, and all four pollution/warming
   counters zero. Clear the reserved new-game counter.
4. Clear first-discoverer and known-by summaries for all 100 advances. Set
   all 28 wonder locations Not Built.
5. For civilization slots 0…7, clear spaceship state/parts/population and
   launch/arrival years, aggregate city/unit/military/ship/population/attack
   counts, and current research. Clear per-region military/attack/city/
   population counts for all 64 regions.
6. Draw once: `nextHistoryTurn=R()%50`. Clear all 150×8 power-history samples.
7. Initialize civilization slots **0 through 7**, even slots absent from the
   alive set, using section 6. Ignore each return value and continue.
8. Copy the resulting alive set to civilizations-present-at-start. Balance
   starts (section 8).
9. If preferred science+tax exceeds 10, or either exceeds 6, replace both
   preferences with science 6/tax 4. There is no lower-bound check here.
   If exactly one human bit is set, copy the preferences to `playerCiv`,
   reducing each rate modulo 256; do not find the human bit's slot instead.
   Otherwise assign science 6/tax 4 to each human slot 1…7. On Chieftain,
   also set each such human treasury to 50.

These inner steps run on the fresh state established by section 2. They preserve
the current attempt's generated/imported map, rules and accepted choices; they
are not a substitute for the fresh boundary. Previous peace counters, power
ranks, future technologies, civilization flags, goals and free-slot damage have
already been discarded. Within-game unit/city reuse still follows U01/C01. Starting
technology allocation precedes the human-rate/Chieftain-gold adjustments.

## 6. Civilization creation at turn zero

Setup invokes [V01 shared civilization creation](V01-civilization-lifecycle-and-civil-war.md#2-shared-civilization-creation)
with turn zero, in section 5's slot order, ignoring each return. V01 owns the
ordered partial reset, attitude and leader draws, pending leader override,
late alive test, admission and unit placement. The caller does not replace a
failed AI or roll back a reset. Absent slots still undergo the shared reset
and, when AI, leader-selection effects before the alive test.

V01 calls the starting-site search below, then applies the supplied turn-zero
hint if it is rectangularly in bounds. The full prior search still consumes
its draws. Catch-up advances and later-turn extra-unit rules do not run while
turn is zero. Do not apply them merely because acceleration will eventually
advance the turn: section 9 sets that value only at its end.

After all creation calls, section 5 continues with the surviving alive set,
start balancing and preferred rates. These caller effects must not be folded
into V01's reset or interpreted as guarantees made by every later recreation.

## 7. Starting-site search and relaxation

Initialize signed attempt counter n=0. Repeat the following; x is drawn before y.
`drawBelow(b)` returns zero without a draw for b<=1, else `R()%b`.

```text
insetX = FlatWorld and W >= 81 and n < 1500
insetY = FlatWorld and H >= 40 and n < 1500
x = (8 + drawBelow(W-16)) if insetX else drawBelow(W)
y = (8 + drawBelow(H-16)) if insetY else drawBelow(H)
x = x - (x modulo 2) + (y modulo 2)

find nearest city to (x,y)
if none: nearestCityDistance = 30
if turn == 0:
    scan all live units ascending, with a live bound:
        nearestCityDistance = min(nearestCityDistance, D(candidate,unit))

f = tile's radius-claimant-or-fertility value
r = tile's region
q = region[r].fertileTileCount, interpreted signed
n = s32(n+1)

if n > 2000 and not (human(c) and turn == 0):
    return candidate,n

reject if any, in this short-circuit order:
    candidate is ocean
    f < 12 - (n >> 5)
    nearestCityDistance < 16 - (n >> 7)
    nearestCityDistance < 2 and n <= 2000
    nearestCityDistance < 1
    q < 32 - (turn >> 4) - (n >> 6)
    turn > 200 and capturedCityCount[r] != 0
    candidate has a hut and n <= 2000
    candidate's occupant-owner query is not Absent
if rejected: repeat
return candidate,n
```

For setup, turn is zero; the later-turn city-count restriction is inactive.
The radius-claimant-or-fertility query first reads the civilization claiming
the tile's city radius. A nonzero claimant is returned directly. Otherwise it
reads the stored fertility value, promoting values 1…8 to 8 and leaving 0 and
9…15 unchanged. This claimant is distinct from occupant ownership. The final
city-or-unit-owner query independently rejects occupied sites using F02's tile
flags/owner contract, without rescanning live records.

The 1500 inset test uses the count **before** the draw. Thus draws 1…1500 use
the inset, draw 1501 does not. Acceptance thresholds use the incremented count:
for example, fertility first drops from 12 to 11 at attempt 32, and required
separation from 16 to 15 at attempt 128.

At attempt 2001, a nonhuman search returns its last candidate without even
calling the ocean/hut/owner rejection predicates, though earlier nearest and
region reads already occurred. The caller then ordinarily rejects that AI;
a valid stored hint can instead reset attempts to zero and rescue it. A human
turn-zero search continues past 2000 until accepted. It then allows a hut and
separation 1, but still rejects ocean, distance zero and a present occupant.
It has no finite failure limit. A human with no acceptable random site never
reaches its stored-hint override, even if the hint would have been usable.

For off-nominal dimensions or extremely long retries, use F01's arithmetic
at each operation, including attempt wrap; do not invent a finite retry cap or
safe map fallback. Section 12 bounds accesses whose backing is not recovered.

## 8. Balancing starting positions

After all eight civilization calls, select one unit for each c=1…7 with the
nearest-unit query from `(W>>1,H>>1)`, owned by c and with no excluded unit.
Capture these seven selected slots once. No human/alive filter is added.
The discarded center-region count described in Appendix A does not determine
bonuses. All scores are calculated before any bonus unit or research is granted.

For each selected unit, read its coordinates. Let r be its land region clamped
to 0…63 and T that region's signed stored tile count. Start score=0 and d=999.
For every other selected civilization unit in ascending civilization order,
if its unclamped land region equals r, minimize d with D between the units.

Add the following components:

| Component | Added score |
| --- | --- |
| d<=9 | 0 |
| 10<=d<20 | 1 |
| 20<=d, d!=999 | 2 |
| d==999 | `clamp(T/50,3,6)` |
| At least four river-marked tiles among valid B[0…20] | 4 |
| Otherwise at least four grassland tiles there | 2 |
| Otherwise neighborhood component | 0 |
| T/50 is 0, 1 or 2 | 0, 1 or 2 respectively |
| Any other T/50 result | 3 |

The center participates in neighborhood counts. River wins over grassland;
resources and grassland shields are not counted. Keep maximum score M over
all selected starts, initially zero.

Then visit c=1…7 ascending, skipping absent selections:

```text
if M-score[c] > 3
   or (human(c) and Deity)
   or (multiplayer mode and Deity):
    create Settlers at selected unit's current coordinates
    if human(c) and Deity: score[c] += 1
    else: score[c] += 3
for i=0 while i < M-score[c]:
    completeResearch(c, sourceCiv=0)
```

Creation failure does not undo the score adjustment. Scores and M are not
recomputed from the additional unit; later children observe earlier changes.
A Deity human receives an extra Settlers even at the maximum score. Otherwise,
a gap of 3 buys three research completions, while a gap of 4 buys a Settlers
plus one completion. Research completion is V03's real operation: it may choose
a research topic, grant it, mutate counts and reset city production. A failed
completion does not stop the remaining calls. It is not a uniform random grant
or a direct increment of the known-advance set.

At turn zero, research selection does not prompt the human. Its eligibility,
AI valuation and per-candidate draws belong to V03/A08; human and AI selection
still use different scoring. These child draws occur immediately in the order
above, before processing the next civilization's compensation.

## 9. Accelerated starts

The accepted wizard choice is normally 0 (none), 1 (early) or 2 (advanced).
A nonzero choice invokes this operation with `e=choice−1`, hence nominal e=0
or 1. It builds a starting world directly; it does not simulate 20/40 turns.
All children initially observe turn 0 and year −4000 unless they themselves
change those values. Ignore the displayed acceleration-year labels as a state
update.

Capture initial coordinates for each civilization by initializing eight pairs
to Absent, then scanning all live units ascending with a live bound and storing
each unit's coordinates in its owner's pair. The **last live unit per owner**
wins. There is no type, human or alive-set filter. Visit c=1…7 ascending and
skip only pairs outside rectangular map bounds.

### First city and military package

For each eligible pair `(x,y)`:

1. Grant Horseback Riding, Ceremonial Burial, Alphabet and Bronze Working in
   that order, with source civilization 0, ordinary grant mode and the
   startup/silent argument set. Set the advance-count to **4**, independently
   of already-owned starting advances. For e!=0, make three research-completion
   calls and increment the current advance-count modulo 256 after **each**
   call. Preserve the child's own counter effects too.
2. Create a city at `(x,y)`. If it returns failure, skip the remaining package
   for c, retaining all preceding grants. [C01](C01-city-lifecycle-and-ownership.md#allocation-and-initialization) owns city initialization,
   naming, build selection and occupancy effects; this call is not the player
   unit's Build City command and does not implicitly consume every starter.
3. Draw once and set `gold=s32((R()%50+25)*(e+1))`. Set city size to 3 when
   e=0, otherwise 5. Get the tile's first unit and count its stack; if count
   exceeds one, set **that first unit's** home to this city, with the home
   representation/reinterpretation described by F02.
4. Capture the city's current production item. The original discarded a primary
   draw here; no sample is required. Create Pikemen if that cached item is the Pikemen rule slot,
   otherwise Archers; fortify it. Create and fortify a Phalanx.
5. Create a Chariot if The Wheel is known, otherwise Horsemen. For e!=0 also
   create an Elephant if Polytheism is known, otherwise a Chariot. These
   direct creations do not run a build-eligibility test.
6. Add Temple. For e!=0 add Granary if Pottery is known; add Marketplace if
   Currency is known, otherwise Barracks. Develop 2 tiles for e=0 or 4 tiles
   otherwise, using section 10.
7. For e!=0 execute the second-city branch below. Finally, if Monarchy is
   known, directly set government to Monarchy, without a revolution dialog.

Only the first city-creation result is guarded here. Subsequent unit results
used for fortification/home writes and second-city creation failure can lead
to invalid record access; do not reinterpret them as a successful empty package.

### Advanced second-city selection

Initialize best score and selected direction to −1 for this civilization.
Capture the first city's original starting region. Scan directions 0…7:

1. Candidate is `(wrapX(x+5*Dir.dx), y+5*Dir.dy)`. Require rectangular bounds,
   non-ocean and the same region as the original start.
2. Call unrestricted nearest-city lookup. Then scan live units ascending
   with a live bound, skipping those on any city tile. For every remaining
   unit, overwrite a retained `lastUnitDistance` with D(unit,candidate), and
   minimize shared `nearestCityDistance` with it.
3. Require `lastUnitDistance>4`. This tests the **last measured unit distance**,
   not the minimum. The local persists across candidates and civilizations.
4. Score Grassland 4, Plains 2, anything else zero. For nonzero scores, add
   one for a river. Replace the selected direction only on strictly greater
   score; the earliest equal-scoring direction wins.

The original does **not initialize `lastUnitDistance`**. When no eligible unit
has yet written it, the comparison requires its incoming stack value. This can
happen on nominal starts where every unit is on a city tile. An original-memory
fixture must supply that value, or stop at the first affected comparison after
preserving earlier effects. A deterministic default such as 5 is a policy
change, not a recovered classic rule; Appendix B records that reconstruction fix.

If no direction was selected:

- Grant Currency, Masonry, Construction, Mysticism, Code of Laws and Monarchy,
  in that order with the startup grant arguments above.
- Make the first city size 7, add Colosseum, add 25 gold with signed wrap,
  and develop two more tiles.
- Create Archers, Legion and Chariot, in that order, at the city's live position.

Otherwise create a second city at the selected coordinates and immediately
set its size to 3. Draw once. On an even result create Warriors then Phalanx;
on an odd result create Phalanx then Archers. Fortify both. Create Horsemen,
then develop two tiles and attempt the road connection below. Do not compare
city spacing again, replace the selection with a nearest-distance test, or
choose the fallback if this city allocation fails.

### Road connection and final turn

If ordinary distance from the second city's current position to the cached
original start is less than 23, set road-planning mode, path owner Absent,
path unit type to the Warriors rule slot, and the goal to the original start.
From the second city, make at most 50 step attempts:

1. Request [U03 local next-direction](U03-pathfinding-and-goto.md#local-next-direction-query)
   with search parameter 99, retaining its cache and workspace effects.
2. A negative direction or the center direction stops the loop. Otherwise
   advance one direction, wrapping x. Reaching the goal stops **before**
   changing that destination tile.
3. Add a Road only if no city, no Road already, no river unless Bridge Building
   is known, no city-or-unit occupant owner, and no peace-restricted owner,
   tested in that order. Remember the changed tile for c. Blocked improvement
   does not stop path progression.
4. Count the step and repeat if fewer than 50.

Clear road-planning mode on normal exit; retain the other path workspace.
There is no universal exception cleanup in the original. After all civilizations,
store `turn=s16(20*(s16(e)+1)+1)`. Nominal values are 21 and 41. **Do not update
year here.** On an ordinary no-resume entry, F04 advances again to turn 22/42
and computes its year. All remaining direct and child mutations are retained.

## 10. Developing accelerated city tiles

Capture the city center once and begin a map-update batch. For each requested
improvement, rescan B[0…19] in order; wrap x and require rectangular bounds and
**no improvement flags at all**. A tile with a unit/city-present flag is not an
empty tile for this test.

| Base terrain | Base score |
| --- | --- |
| Desert | 1 |
| Plains | 4 |
| Grassland without/with shield | 3 / 5 |
| Hills | 2 |
| Other | 0, ineligible |

For a nonzero score add one at indices 0…7 and three if a resource is present.
Choose the first strictly greatest score. With no choice stop improvement
passes. Otherwise reread its terrain, add Road, then Mine for Hills or
Irrigation for the other eligible terrains, and remember the tile for the
city's current owner. No water-source, technology, ownership or worker check
is added. Rescanning observes the new flags, so that tile is no longer eligible.

After improvements, capture the owner for exploration. Scan B[0…44], adding
that owner to the explored set only on in-bounds non-ocean tiles whose region
matches the city's **live** current location. This final scan does not update
remembered improvements or reveal city/unit records. The center used for
offsets remains the originally captured one.
Use the complete B[0…44] order defined by W01; do not move the center ahead of
the first twenty positions or sort the outer extension.

End the map batch. This helper consumes no direct random draws; resource/shield
predicates use W01's coordinate formulas. [W03](W03-visibility-and-exploration.md#9-caller-refresh-boundaries)
distinguishes exploration-only publication from memory and object revelation.
U03 owns road-path selection; adding improvements is not a request to
rebuild all map fertility or starting scores.

## 11. Random ordering and delegated mechanics

| Boundary | Direct primary draws |
| --- | --- |
| Accepted random/premade world parameters | Five after successful size/map selection. Cancelled selection has no parameter draws. |
| Custom parameters | One for each Random choice in dialog order; none for ordinary choices. |
| Random opponents/activity | One each when selected. |
| Opponent slot allocation | One per trial, including collisions. |
| New-game history deadline | One, before all civilization creation. |
| Civilization attitudes | [V01 reset](V01-civilization-lifecycle-and-civil-war.md#3-ordered-partial-reset-and-catch-up-advances), including absent slots and the self relationship. |
| AI tribe selection | [V01 leader selection](V01-civilization-lifecycle-and-civil-war.md#4-ai-leader-selection) owns candidate, reuse and gender draws, including draws before an override. |
| Site search | X then Y on every trial; omit a coordinate draw only when its divisor is at most one. |
| Balancing | No direct draws; the unit and research children can draw. |
| Accelerated first city | One treasury decision after successful city creation, interleaved with children; the original discarded defender draw is optional evidence. |
| Accelerated second city | One for the defender pair after city creation. |
| Tile development | No direct draws. |

Supply semantic choices or versioned implementation random contexts, and the
required clock inputs. Preserve site-search attempts, fresh trials and
initialization for absent civilizations where they write retained state. An
overwritten sample may be omitted only when it has no non-RNG effect; the
original discarded defender roll is one such case. Sampling a value is distinct
from performing the queries, mutations and child calls surrounding it.

The remaining document owners are:

| Operation | Owner and boundary |
| --- | --- |
| Generation/finalization and fertility | W02, called once before game-state initialization; W01 owns resource/hut predicates. W02's separate classic-target review remains open. |
| Shared spatial operations | W01 supplies coordinates, distances, neighborhoods and spatial predicates; U01 and C01 own the complete nearest-unit and nearest-city operations. |
| Shared civilization creation | [V01](V01-civilization-lifecycle-and-civil-war.md), invoked with turn zero; F05 retains the shared site search and surrounding setup phases. |
| Unit/city creation and deletion | U01/C01, with F02 identity and retained-state rules. Calls can fail or consume random draws. |
| Advance grant/completion and AI valuation | V03/A08. Preserve each call's source/mode, count, turn-zero suppression of prompts and draw trace. |
| Reveal/remember and contact | [W03](W03-visibility-and-exploration.md); creation invokes surroundings, while the extra radius loops only publish exploration and improved tiles have explicit memory updates. |
| Initial AI goals | Empty, directly initialized by F02/F05. A01 refresh is only a within-game operation. |
| Road direction and path workspace | U03; exact classic direction choices remain a child obligation. |
| Full string/dialog/portrait/introduction behavior | P03/P04/P10; setup owns the accepted values, order and retry transitions here. |
| Scenario/load/network setup | S01/F06/N01/N04; do not apply this new-world reset to restored state. |

A complete setup case must join these child contracts at the stated boundaries.
The local setup fixtures do not establish every mutation performed by a
completed game launch.

## 12. Nominal inputs, unusual behavior and failure limits

- Nominal custom dimensions use the bounds in the size chooser. If accepting
  extra parsed values, apply `s16` before its limits/area checks. A stricter
  profile may reject such text before conversion; accepted executions retain
  that order. The reference example is in [Appendix C](#appendix-c-original-numeric-annotations). Decimal conversion
  itself follows F03/F01's parsing contract.
- A hint inside rectangular bounds can be wrong-parity, on water, occupied or
  next to another civilization. It bypasses the random search's quality tests.
  AI hints in the polar three-row bands still fail admission; human ones do not.
- Repeated search has a finite abandonment condition for AI, not for a
  turn-zero human. An all-ocean/no-space world can make human setup nonterminating.
  An optional restrictive policy can reject it; do not label that original behavior.
- A retained negative acceleration choice can enter the arithmetic branch
  despite nominal dialog choices being 0…2. Nonzero e selects the advanced
  package; the gold multiplier and final turn still use its actual value.
- Missing tribes of a color can remove a selected civilization. Invalid custom
  colors, menu indexes, region indexes, owners, counts, strings or capacities
  can instead reach adjacent records/storage. Do not invent a safe default at
  an unrecovered access. File validation and bounded backing belong to F03/F06/X01.
- Advanced second-city distance has an uninitialized-input obligation even
  without malformed input. Distinguish that case from deterministic signed
  wrap, retry-state retention and the observed last-unit-versus-minimum test.
- A failed first accelerated city is skipped after its technology grants. A
  failed second city or a unit used immediately afterward has no corresponding
  success guard. Earlier grants, draws, cities, units and improvements survive.
- Map-generation failures, blocked presentation callbacks and file-write errors
  are not converted into transactional setup failure. Apply the child's actual
  return/failure contract and preserve all prior effects.

## 13. Worked cases and conformance obligations


> Compare the gameplay/interchange observations in the
> [production scope](#production-contract-scope). Original memory/helper traces,
> arbitrary native callbacks and unsafe backing-byte cases in historical lists
> are optional reference, not additional production acceptance criteria.

Unless stated otherwise, child operations succeed and make only their documented
changes; presentation callbacks are inert. Fixtures must give controlled choices or implementation random contexts,
clock samples, initial retained state, rules, dialog responses and any supplied
private backing. A scripted draw list is a boundary fixture, not a claim that
all listed draws are consecutive in a real full-game run.

| Nominal case | Expected observation |
| --- | --- |
| Random-world draws 0, 1, 2, 3, 4 | Land mass −1, form 0, temperature 1, climate −1, age 0. Five parameter decisions; raw generator consumption is unspecified. |
| Custom U=75,H=120 | W=150,H=120, 9000 tiles, Large classification. |
| Human slot 1, two opponents, slot draws 0,1,3,3,5 | Alive {0,1,3,5}; five slot draws. |
| A nonhuman slot absent from alive, three candidate tribes, pending override set | Seven attitude draws, candidate/override processing and gender selection still occur; an entirely overwritten candidate sample may be omitted; no site search or unit placement. |
| First trial, good land/owner/hut state, fertility 12, region fertility 32, distance 16 | Accept at n=1. Equality passes all three quality thresholds. |
| Same candidate repeated with fertility 11, other thresholds passing | Reject trials 1…31, accept trial 32. |
| Flat W=100,H=80, repeated failed trials | Initial draws use divisors 84 and 64 with inset 8; trial 1501 uses 100 and 80. |
| Highest starting score 9, other scores 6 and 5, no Deity | Score 6: three research calls. Score 5: extra Settlers, then one research call. |
| Deity human already has the maximum score | Create a second Settlers, increase its local score by one, grant no compensation research. |
| Two eligible Plains development tiles with equal score | Choose the earlier B index; its newly set flags exclude it on the next pass. |
| Early acceleration, treasury draw 49, successful first city | Gold 74, city size 3, Temple, two developed tiles; final turn 21, stored year still −4000. No discarded defender sample is required. |

| Compatibility/failure case | Expected observation |
| --- | --- |
| Decimal custom width 65586, height 80 | Width narrows to 50; accepts W=100,H=80 under the original post-conversion checks. |
| AI search rejects 2000 times; trial 2001 is ocean | It returns that candidate without the rejection predicates; without a hint the caller removes the AI. |
| Same AI with an in-bounds interior hint | Search still consumes its draws; hint replaces the final candidate and resets attempts to zero before admission. |
| Human has a valid hint but no random candidate can ever pass | Search does not reach the hint override. |
| Second game after human tribe name cursor 12 | Fresh setup starts every tribe cursor at zero; prior city names do not alter the new game. |
| Advanced candidate has minimum distance 2, last qualifying unit distance 8 | The distance gate passes, because it tests 8. If the last distance is 3, it fails regardless of the minimum's source. |
| Advanced candidate with no distance writer yet | Outcome is unspecified without supplied incoming local backing; a reconstruction default of 5 is not an original observation. |
| Select acceleration, cancel its submenu, then choose Standard Rules | The negative local remains; do not silently replace it with No Acceleration. |

## Appendix A. Original representation and evidence mapping

An implementation may choose different storage. The following mappings support
original snapshots, not mandatory internal layouts.

| Logical item | Original representation |
| --- | --- |
| Source mode | 0 generated, 1 premade cylindrical, 2 premade flat; both nonzero values reach `map_generate`'s retained-map path. |
| Custom dimensions | Decimal conversion to signed 16-bit width/height before validation; width then doubled. |
| Rules | Standard `003f`; advanced base `002f`; Normal Combat `0010`, Bloodlust `0080`, No Restart `0100`, Flat `8000`. Unnamed base bits remain set as in F03. |
| Game classifications | Large `0004`, Small `0008`; distinct from active rules and `map.flat`. |
| Names | Negative signed label IDs select custom civilization text; default restoration negates nonpositive IDs in their field domain. Fields/names/titles follow F03. |
| Tribe state | 21 records; usage/gender are unsigned byte domains, city-name cursor retained until explicitly reset. Pending overrides are signed bytes at `0062ced0`, Absent `−1`. |
| Start hints | Signed x/y arrays for 21 tribes at `00627fe0` and `00628010`; these arrays lie in shared startup backing. |
| Neighborhoods | Direction x/y at `00628350/00628360`; radius x/y at `00628370/006283a0`. The first 45 pairs used here match the original data image. |
| Search fields | Attempt counter is signed 32-bit; quality accessor uses radius bits 5…7, else the owner byte's low nibble with 1…8 promoted to 8; region fertile/tile counts are signed 16-bit. |
| New-game reserved writes | Game offsets `0e` and `34` hexadecimal are cleared at the wizard and game initialization respectively. They do not justify clearing adjacent state. |
| Initial history | Signed history deadline `R()%50`; 150 samples of eight civilization values are cleared. |
| Starting counter | `Civ.num_advances` is unsigned byte state: admission writes 1, acceleration writes 4 and may separately increment it after research. It is not a count recomputed from known-tech bits. |
| Unit homes | The accelerated stack assignment stores the city slot's low byte, even when its signed interpretation collides with a sentinel; F02 defines later contextual reads. |
| Accelerated missing value | `game_accelerated_start` local `dist`, original EBP−14h; sole scan assignment at `004a9dcf`, threshold test `004a9dee`. It persists across candidates/owners. |
| Introduction's extra probe | `civ_intro_popup` first tests the prerequisite bytes of advance 100, beyond the advance table. These alias terrain 0's irrigation/mining result fields. Its subsequent knowledge test returns false for 100, so this read adds no name under recovered backing. |

Balancing also initializes 64 local region counts and, for each selected unit,
increments the land region of the **map midpoint**, not the selected unit's
position. Those counts are never consumed in the recovered function. Within
valid region backing they do not affect the scores above; an invalid midpoint
region can write outside the local array before scoring. That overwrite is an
excluded bug: omit the invalid count write, whose result is unused, and continue
the scoring procedure. Do not reproduce adjacent-state corruption or turn the
unused counts into an invented crowding penalty.

## Appendix B. Source traceability and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Evidence is pinned to OpenCiv2
`36a4cbeabbfc709b3c3d37e531d948d9e42250d8` and the independent reconstruction in
`civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`.

The external OpenCiv2 links below use published revision
`a71e8aefffe04e6f719e0e213b027b128a747711`. Every linked file or directory was
verified to have the same Git object as at this document's pinned evidence
revision. See [source provenance](README.md#source-provenance) for the
evidence-link conventions.

| Contract | Source / original entry |
| --- | --- |
| Session/menu/wizard | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): `game_main` `0041f8d9`, `game_main_menu_loop` `0041eeeb`; [bigframes.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/bigframes.cpp): `setup_new_game_dialogs` `0041ba52` and extracted helpers; [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp): `civ_intro_popup` `0041b8ff`. |
| Size/parameters/map | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): `setup_choose_map_size` `0041d417`, `setup_custom_world` `0041d7ea`, `setup_clear_start_positions` `0041dcc3`; [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp): `map_load_dialog` `0041dd0e`, `map_read` `005b8783`, `map_generate` `00408d33`. |
| New state and starts | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): `game_init_new` `004aa9c0`, `game_balance_start_positions` `004a7754`; [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp): `civ_create` `004a7ce9` and its reset, leader, search and placement helpers. |
| Acceleration | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): `game_accelerated_start` `004a9785`; [window.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/window.cpp): `accel_start_develop_city` `004a93b3`. |
| Research boundary | [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp): `civ_pick_research_advance` `004c09b0`, `civ_choose_research_dialog` `004c195e`, `civ_research_complete` `004c21d5`; [ai.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/ai.cpp): `ai_advance_value` `004bdb2c`. |
| Geometry/queries | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp), [support.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/support.cpp), [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp), [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp): `map_distance_diag`, `dist_from_deltas`, `unit_find_nearest`, `city_find_nearest`, and tile predicates. |
| State and constants | [civ2types.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h), [state declarations](https://github.com/PeterJohnson/openciv2/tree/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state), [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h), [original image](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/data/image/data.bin) with its [layout](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/data/image/layout.json). |

Independent evidence at the pinned reconstruction revision includes
`src/new_game.cpp`, `src/application_session.cpp`, `src/game_initialization.cpp`,
`src/civilization_init.cpp`, `src/map.cpp` and `src/map_generation.cpp`. It
corroborates retry-local retention, absent-slot draws, search thresholds,
start-hint override order, balancing and acceleration side effects. Its explicit
backing stops and deliberate bug fixes are not original return values.

### Relevant differences

- OpenCiv2 B2 resets every tribe's city-name cursor in fixed mode. Classic
  behavior retains the human cursor as described above
  ([bug ledger](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md#b2-a-new-game-in-the-same-session-keeps-the-human-players-place-in-the-city-name-list)).
- X53 expands the custom map area limit; the classic limit here is 10000 tiles.
- BF-094 validates map dimensions; BF-236/BF-287/BF-288 validate terrain,
  region and occupant encodings. These port loader checks do not establish
  original file rejection rules. F06 owns the complete file boundary.
- BF-113 rejects added Gender rows beyond the two supported variants; BF-102
  retries added city-style rows beyond four. The original could store/index
  those values. Do not apply these checks silently to a full-compatibility claim.
- The independent reconstruction's BF-290 initializes the missing accelerated
  distance to 5. Its `analysis/reference/bugfix-log.md` records why even a normal
  start can read the original uninitialized local. This specification retains
  the original backing obligation and does not adopt that initializer.
- Portrait-dialog cancellation and other presentation primitives have their
  own retained-value/bugfix boundaries. Use their owning contracts rather than
  assuming every Cancel is a pure no-op.

### Validation scope

Source review covers the setup branches, reset/placement ordering, search and
balancing formulas, acceleration and tile-development loops described here.
The 45 neighborhood pairs were checked against the original data image and
independent reconstruction. A temporary harness extracted **12 complete source
functions**: tribe selection, site search, balancing, acceleration, tile
development, nearest-unit selection, the quality accessor and five spatial
helpers. All **104 assertions passed**, covering threshold equality and
relaxation, the 1500/2000 boundaries, coordinate draw suppression, overrides
and usage wrap, last-slot ties, compensation despite failed child returns,
tile selection, and early/advanced package order, counts and final turn/year.

The harness used OpenCiv2's state types and image-derived neighbor tables with
signed-character and wrapping arithmetic settings. Map services, scripted
random returns, allocation, research, visibility and pathfinding children were
adapted/stubbed. The advanced fixture contained an outside-city unit that wrote
the distance before each use; it did not invent a result for the original's
uninitialized branch. Menu cancellation, file parsing, the full reset and real
child mechanics were source-reviewed, not exercised by these assertions. No
complete original-program launch, all-menu-input campaign or whole-game
conformance claim is implied.

Remaining integration cases include real research grants and allocation failures,
custom map input failures, second-game retained state, callback-mutated setup,
road-path results, and the supplied-backing advanced-start case. The existing
[unit-case inventory](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/tests/unit/cases.tsv) and
[replay scripts](https://github.com/PeterJohnson/openciv2/tree/a71e8aefffe04e6f719e0e213b027b128a747711/harness/scripts) are starting evidence, not exhaustive
coverage. These checks do not establish full independent conformance.

The shared creation contract is owned by
[V01](V01-civilization-lifecycle-and-civil-war.md#appendix-b--validation-and-remaining-integration).
Its finite source checks join real unit creation and cover reset/leader order,
hints, later-turn admission and client waits with declared external adapters.
They do not validate this entire wizard/world/setup/acceleration sequence.

## Appendix C. Original numeric annotations

These original annotations retain storage widths and reference-evaluation context.
They do not prescribe internal types or redundant casts. The main contract and
F01 determine logical ranges and behaviorally significant conversions; external
file/wire encodings remain normative in their owning mappings. Where an excerpt
includes an off-nominal conversion, it applies to that explicitly admitted profile,
not as a new requirement to accept unsupported inputs.

### Reference annotation 1

- Custom size limits apply after narrowing to the signed cyclic domain. For
  example, parsed width 65586 becomes 50 before checking area. A stricter
  implementation can reject such text before narrowing, with a declared
  narrower acceptance domain; it must still preserve arithmetic for accepted
  executions. Decimal conversion itself follows F03/F01's parsing contract.
