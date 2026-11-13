# V04 — Diplomacy and treaties

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. This specifies directed relations and bilateral treaty
mutations, attitude, senate/attack checks, reputation consequences, alliances,
expulsion, contact dispatch and AI-to-AI contact transitions, border-intrusion
measurement and the diplomacy portion of civilization turn maintenance.

[V02](V02-governments-and-tax-rates.md) owns the preceding government-turn
prefix. [F04](F04-turn-sequencing.md) orders that combined operation in a turn.
[V05](V05-diplomatic-negotiation.md) owns audience negotiation, player proposals
and multiplayer parley sessions;
[V06](V06-espionage-and-bribery.md) owns mission eligibility/incidents and A08 owns AI security, valuation,
relation-review, aid and war-ally policies called below. These are real children,
not assumed-success no-ops. P04/P07 and N01/N02 own common presentation and
session/message services. This document owns the exact admission and call order
at those boundaries, including decisions that continue after callbacks.

[C08](C08-buildings-and-wonders.md) supplies active wonder queries,
[W01](W01-map-geometry-and-spatial-state.md) geometry/regions,
[C01](C01-city-lifecycle-and-ownership.md) city searches and
[U01](U01-unit-lifecycle-and-stacks.md) stack operations. [U02](U02-movement-and-zones-of-control.md)
and [U06](U06-combat-and-veterancy.md) own their movement/combat callers and direct
incident mutations. A nuclear attack's direct relation effects remain in
[U07](U07-nuclear-attacks.md), which now uses the shared attitude primitive here.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

Treaty cascades, reputation/contact updates and callbacks retain their
required dependencies. Pure predicates may be combined only without changing
which state is read or which admitted failure is reached.

## 1. State, domains and notation

Write `R(a,b)` for a's directed relation toward b. Each direction separately
stores Contact, Ceasefire, Peace, Alliance, Vendetta, HostileIntent, BrokenTreaty,
Embassy, NuclearThreat, Fighting, RecentContact, Aggressor, SneakAttack, War,
ChangedThisTurn, Incident, Nuked, Tribute, AggressorLong, Mercenary, WarThisTurn,
CityCaptured, MetThisTurn and SpyChat. These flags can coexist. In particular,
relations are not a single enumeration progressing from unknown to peace to war.
A direct one-direction mutation is distinct from the bilateral primitives below.
Appendix A gives original encodings for validation; named sets are sufficient
for a new implementation.

Civilizations supply directional attitudes, last-contact turns and reputation
credits, reputation, government, SenateActive, HasAllied and other flags,
diplomacy random value, request count, city count, per-region city summaries and
spaceship state. Global inputs include human/alive sets, local human, player
civilization, power ranks, turn, difficulty, reveal map, autoplay, Scenario and
AI Gang Up, scenario flags, the hot-seat-human set, current movement/cursor state,
world/unit/city data and live upper scan bounds. The active diplomacy evaluation
has a selected ordered pair, current attitude and AI-declare-war scratch.

Nominal inputs have backed civilizations0…7, live collections consistent with
[F02](F02-game-state-and-invariants.md), and F03/F06 rule/scenario state.
Civilization0 is rejected only where specified; many helpers have no general
alive, human, distinct-pair or barbarian gate. Each city's region summaries and
tile owner query are consumed as stored, not rebuilt for these operations.

[F01](F01-determinism-and-arithmetic.md) governs reference arithmetic, shifts,
clamps and primary random draws. Stored attitude, reputation and diplomacy random
are unsigned8; request count and per-civilization reputation credit are signed8;
last-contact turns and game turn are signed16. Counter updates preserve their specified cyclic results. Positive reputation is not automatically capped at7.
Diplomacy-evaluation attitude is signed32 and is not clamped along with the
stored attitude. Record/array indexing remains distinct from scalar retention.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

Accepted contradictory flags, wrapped counters and stale but backed summaries
follow the operations below. A stricter importer may declare a smaller domain.
Out-of-bounds writes and their corruption are **excluded bugs** under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
including invalid civilization, region, unit and text writes. Reject earlier or
stop before the invalid write; do not emulate corruption through adjacent fields.
Unrecovered reads require supplied backing/evidence or a bounded stop after the
valid prefix. In particular, an uninitialized AI attitude input is not recovered
by inventing zero or by requiring one replay's stack preset.

## 2. Shared attitude and hostility

`attitude(a,b)` returns a's unsigned stored attitude toward b without clamping
or validating a treaty. `setAttitude(a,b,value)` skips its store only when mode
is hot-seat, a belongs to the hot-seat-human set and a differs from local human.
Otherwise clamp the signed full value to0…100 and store it. A blocked store does
not imply that a caller's other changes are blocked.

`adjustAttitude(a,b,delta)` reads stored attitude, requests
`setAttitude(a,b,s32(old+delta))`, then, if the evaluation's selected ordered pair
is exactly(a,b), adds delta to its current attitude with signed32 retention and
**without** the0…100 clamp. Finally re-read stored attitude. The independent
original reconstruction retains that final value as the return; the primary
source exposes a void wrapper, and the scoped callers here ignore the result.
Both the read and scratch update still occur when hot-seat suppressed the store.

The display category of a signed attitude is:

| Value | Category |
| --- | --- |
| <0 |0 |
|0…10 |1 |
|11…25 |2 |
|26…38 |3 |
|39…61 |4 |
|62…74 |5 |
|75…89 |6 |
|90…99 |7 |
|>=100 |8 |

`hostile(a,b)` first returns true if R(a,b) has War. Otherwise return false if
Alliance is present. Otherwise, only Contact present **and Peace absent** can
qualify: return whether stored attitude>49. Ceasefire does not prevent this
last case. There is no reciprocity or alive test.
[A05](A05-ai-naval-and-transport.md#hostility-toward-an-absent-unit-owner) specifies
the established read aliases for its unloading call with an absent unit owner;
that caller does not create a dummy civilization or authorize an invalid write.

## 3. Bilateral flag primitives

`clearRelation(a,b,F)` performs these recursive steps in order, testing the
original requested set F:

1. If F contains Peace, recursively clear Alliance.
2. If F contains War, recursively clear Aggressor and SneakAttack.
3. If F contains Contact, recursively clear War.
4. Remove F from R(a,b), then remove F from R(b,a).

Clearing Contact consequently clears War/Aggressor/SneakAttack, but does not
clear Peace, Alliance, RecentContact or Embassy. Clearing Alliance alone does
not clear Peace. Even for a==b, preserve the two ordered stores and recursive
calls. The original performs an additional discarded relation read before
Peace's Alliance recursion; it has no effect with backed canonical inputs but
is still a read boundary for an invalid pair.

`setRelation(a,b,F)` performs these ordered steps:

1. If F contains Alliance, recursively set Peace.
2. If F contains any Ceasefire/Peace/Alliance, bilaterally clear War, Aggressor,
   Fighting, BrokenTreaty and HostileIntent. The War clear also clears
   SneakAttack through the rule above. If F contains Peace, the original has
   a discarded relation read before this child.
3. If F contains War, bilaterally clear Ceasefire/Peace/Alliance, then add
   WarThisTurn to F.
4. Union the resulting F into R(a,b), then into R(b,a).

Do not simplify this to normalization of a final flag set. For example, setting
Alliance and War in one request first sets Peace, then clears it, and finally
stores both Alliance and War without Peace. Existing flags outside the specified
clear set survive. Neither primitive sets Contact automatically, updates
last-contact turns, adjusts attitude/reputation, expels units or sends a message.
Callers supply those effects. Raw stores elsewhere intentionally bypass these
recursive consequences.

## 4. Senate and attack confirmation

`senateBlocks(a,b)` returns false when a's stored government<Republic. It also
returns false when Scenario and No Senate Intervention are both set. Otherwise:

1. bonus=25 if R(b,a) has SneakAttack, else0.
2. Query a's active United Nations. If present, replace bonus with50.
3. threshold=`clamp(s32(bonus+15*unsignedReputation(b)),0,75)`.
4. If a's stored diplomacy random<threshold, return false.
5. If a's government<Democracy, return whether SenateActive is set. Otherwise
   return true.

This query makes no random draw: it consumes the turn's retained random value.
Government values above Democracy follow its final branch. The United Nations
query is for a, while some notices below query b; do not merge them.

`confirmAttack(a,b,protectedFlags)` returns **nonzero to block**, zero to allow.
It returns zero immediately if R(a,b) has War, if none of protectedFlags is in
R(a,b), or if b is not alive, in that order. Otherwise set b's plural name.
If Alliance is present, show Annoy Allied and return1 regardless of its reply.
The alliance portrait depends on whether Communism has any global discoverer.

For a non-alliance, choose the warning from live flags in this priority order:
Peace; otherwise Ceasefire with Tribute; otherwise Ceasefire; otherwise Annoy.
Only reply1 continues; any other reply returns1. Invoke `senateBlocks(a,b)`.
If true, show Overrule with a's current government portrait and return1.
If false, permit the attack. Before returning0, when a's government is above
Fundamentalism, show one additional notice:

- If b has active United Nations, Allow UN.
- Otherwise, if R(b,a) lacks SneakAttack and b's reputation is zero, Allow Hawks.
- Otherwise set b's adjective and show Allow Aggressor.

Do not re-gate the outer branch after each popup. This operation itself does not
set War, break a treaty, change reputation or move units; U02/U06 own the later
execution. Its protected set comes from the caller and can differ by action.

## 5. Negotiated alliance, peace and ceasefire

The following operations receive `(actor,other)`: actor is the audience's player
side in ordinary use; other is the counterpart whose attitude is updated. They
also have direct callers, so do not add universal human/alive/autoplay tests.
[A08](A08-ai-research-government-and-diplomacy.md#4-diplomatic-stance) owns
`evaluateStance(actor,other)` and V05/P04 own formatting/reaction services.

`formAlliance(actor,other)`:

1. Adjust other→actor attitude by−25.
2. Bilaterally set Alliance, including its Peace cascade.
3. Evaluate stance. Set other's request count0 and HasAllied; set evaluation
   AI-declare-war0. Set actor's last contact with other to current turn and
   actor's HasAllied.
4. Format attitude; set other's adjective argument1 and actor's adjective2.
   Play leader reaction2 and show Alliance, using the global Communism-dependent
   alliance portrait. No autoplay suppression is added here.

`signPeace(actor,other)` bilaterally sets Peace+ChangedThisTurn; reads other→actor
attitude, clamps it to0…50, and calls the shared setter (including its hot-seat
store gate). Then evaluate stance, clear other's request count, set actor's
last-contact turn, format attitude, set adjectives in the same order, play
reaction2 and show Treaty.

`agreeCeasefire(actor,other)` bilaterally sets Ceasefire+ChangedThisTurn, then
bilaterally clears Tribute. Clamp/set other→actor attitude to0…50 and evaluate
stance. Set actor's last contact, format attitude, set adjectives, play reaction2
and show Ceasefire. After the modal, directly clear Aggressor from every
R(slot,actor), slots1…7 in order, without an alive or distinct-slot filter.
AggressorLong remains. This procedure does **not** clear other's request count.

## 6. Expulsion and alliance cancellation

`expelUnits(intruder,territoryOwner)` scans live units by ascending slot with a
live upper bound, initially matching owner==intruder. For each:

1. Capture its coordinates. Query C01's weighted nearest city with owner
   preference intruder, excluded city−1 and extra owner territoryOwner. Continue
   only when the returned city exists and is owned by territoryOwner.
2. Read the tile's region and whether it is ocean. On land, require the territory
   owner's stored city count for that region nonzero and the unit's role unequal
   to Trade. Ocean bypasses both tests; this is not a simple land-unit filter.
3. For land, query weighted nearest city for intruder, excluded−1 and no extra
   owner. For ocean, scan live intruder cities ascending, requiring adjacency to
   the captured body; choose minimum W01 diagonal distance with initial9999 and
   strict replacement, so first ties win.
4. If a destination exists and the unit's **current** owner matches its owner,
   invoke U01 lift-off-map(unit,0), then whole-stack relocation to that city's
   current coordinates with synchronization requested.
5. If moving-units mode equals1 and active unit is this unit, update the map
   cursor from the destination's live coordinates. Set this unit's order None.

No successful destination means no movement or order clearing. Lift/relocation
use their real stack semantics; do not replace them with coordinate stores or
assume that only the initially selected member moves. The outer slot scan can
later see already relocated stack members. Nearest-city and region reads must
not be replaced by a new territorial polygon model.

`cancelAlliance(a,b)` bilaterally clears Alliance only, expels a from b then b
from a, and redraws all views for the current local human. Then prepare Cancel
Alliance. If local human is a or b and autoplay is off, show the participant
form using the other party's plural/adjective. Otherwise evaluate the observer
predicate below; return if it fails, and show the third-party form only when
autoplay is off. The portrait uses global Communism discovery. No network notice
is sent by this wrapper and Peace survives unless a child changes it.

The common ordered observer predicate for an ordered pair(a,b), where used
below, is local→a Embassy, local United Nations, local Marco Polo, local→b Embassy,
local United Nations again, local Marco Polo again, then reveal map. Evaluate
left to right with short circuit and live queries. Repeated wonder queries are
intentional; callers may have different autoplay gates around this predicate.

## 7. Declaration, reputation and forced war

`declareWar(a,b,mediator)` first increments mediator's signed8 reputation credit
toward a if mediator>=0, before any alliance test. Then inspect R(a,b).

If Alliance is absent:

- If neither Ceasefire nor Peace is present, adjust mediator→a attitude by−5
  when mediator>=0.
- Otherwise run a treaty-break penalty block once, **plus one additional earlier
  identical block if both Ceasefire and Peace are present**. Each block:
  (1) increments a's unsigned8 reputation if a has power rank7, stored city
  count>4 and mediator<0; (2) increments reputation if difficulty is not
  Chieftain and a lacks active Eiffel Tower; (3) decrements a's signed8 credit
  toward b if mediator<0 and a is currently human. Do not combine the repeated
  wonder queries or full-width gates. After those blocks, if mediator>=0,
  adjust mediator→a attitude by−15 once.
- If a is currently human, directly add Vendetta to R(b,a).
- Bilaterally set War.
- If a is currently human, directly add Aggressor+AggressorLong to R(a,b), set
  a's last contact with b to current turn, then call `activateAllies(b,a)`.

If Alliance is present:

1. Increment a's reputation when non-Chieftain and a lacks Eiffel.
2. If mediator<0, increment reputation once more; if a is human, decrement its
   reputation credit toward b. This extra reputation increment is not human-only.
3. If mediator>=0, adjust mediator→a attitude by−25.
4. Call `cancelAlliance(a,b)`, including expulsion/redraw/notices.
5. If a is now human, directly add Vendetta to R(b,a).

The alliance branch **does not set War**. It can end with Peace still present.
Do not silently complete it into a war declaration. Reputation and credits wrap
in their stated ranges rather than saturating at displayed reputation levels.

`forceWar(a,b)` starts result0 and bilaterally clears HostileIntent+BrokenTreaty.
If the reverse R(b,a) has Ceasefire or Peace and either participant is human,
call `declareWar(a,b,-1)`. Then bilaterally clear Peace+Alliance. If the live
R(a,b) has Contact, bilaterally set War and request stored attitudes100 in both
directions, a→b first. If neither direction currently has SneakAttack, directly
set it on a→b and return1; otherwise return0. No Contact means no final War or
attitude operation: even Ceasefire can survive that path. The returned boolean
reports a newly marked sneak attack, not whether all relations changed.

## 8. Alliance obligations

`activateAllies(a,enemy)` returns without scanning unless R(a,enemy) has Contact.
Scan slots1…7, excluding a and enemy, requiring a→candidate Alliance,
candidate→enemy Contact, and neither Alliance nor War on candidate→enemy.
No alive check is added. Set names for a, candidate and enemy before these branches:

- If enemy is AI, act only if a is human. Show Ally Helps locally when a is
  local, or send its remote notice when networked; then bilaterally set
  candidate/enemy Contact+RecentContact+War, and directly add
  Aggressor+AggressorLong on candidate→enemy. No attitude adjustment is made.
- If enemy is human, first show Activate Ally to a local enemy or notify the
  remote enemy **only when a is AI**. Then, regardless of a's human status,
  adjust candidate→enemy by+100, bilaterally set Contact+RecentContact+War,
  directly add Aggressor+AggressorLong on candidate→enemy and a→enemy, and
  set enemy's last contact with candidate to current turn.

These notices are not universally gated by autoplay. Query human/local/mode
state at the displayed points; a participant classification need not remain
unchanged across text or modal callbacks.

`joinAllyWar(a,enemy,ally)` first returns0 if a→enemy has Alliance or War.
If ally→enemy lacks War, set HostileIntent directly on both a→enemy and
ally→enemy only when both already have Vendetta; return0.
Otherwise, when enemy is human and a→enemy lacks Vendetta:

1. Return0 if `signedLastContact(enemy,a)−signedTurn < 6`.
2. If enemy's rank<7, draw primary R and return0 unless R%3==0.

The subtraction is **last contact minus turn**, not elapsed turns. A usual past
contact therefore fails this gate; do not reverse it. If enemy is human and the
procedure continues, set its last contact with a and ally to current turn.
Set the three names, show Join War, bilaterally set a/enemy War and return1.
No ordinary reputation declaration or automatic ally activation is substituted.

## 9. Border-intrusion measurement

`countIntruders(owner,intruder)` resets selected city to−1, intruded-city count0,
unit count0, weighted count0 and repeat offenses0. If owner→intruder has Alliance,
return0. Otherwise scan live owner cities ascending with the live upper bound.
For each city, set local unit count0 and scan the first20 positions of W01's
ordered city neighborhood, wrapping x and retaining the unwrapped y.

Skip positions that are outside the map, ocean, have any city owner, or whose
tile-unit owner differs from intruder. At the first position passing these tests,
capture its improvements and walk its physical stack in U01 order. For each
member with signed role<5 and BorderIntruder clear:

1. If this city differs from the last selected intruder city, increment the
   intruded-city count; set selected city to this city.
2. If BorderWarned is clear, set it; otherwise increment repeat offenses.
3. Set BorderIntruder; increment the local and total unit counts.
4. Increase weighted count by1, plus1 for Road,1 for Railroad,1 for Mine,
   1 for Irrigation and2 for Fortress. Road and Railroad can both contribute.

The original exits this city's tile loop after that stack **even if no member
qualified**. It does not examine other occupied tiles around the city. Members
are not independently filtered by owner after the tile-owner test. BorderIntruder
prevents counting a member again in overlapping city neighborhoods or a later
call until another algorithm clears it. This function resets only its summaries,
not those unit flags. If this city's local counted units>3, add one repeat
offense after the tile loop. Return the weighted count.

B4's reconstruction fix removes the early exit and changes these results.
Classic compatibility retains it. The local coordinate temporary reused as the
old weighted count is not another city coordinate; the classic early exit
prevents a subsequent tile continuation from consuming it as y.

## 10. Contact dispatch

`meet(a,b,x,y)` returns before side effects for a==0, b==0 or turn0, in that
order. If scenario events are loaded, invoke
[S02's negotiation check](S02-event-language-and-execution.md#negotiation)(a,b) and
return if it returns0. No generic alive or distinct-pair check follows.

Define `needsAudience(a,b)` as, in order: a→b Contact absent, Incident present,
a's last contact with b negative, or `turn−lastContact(a,b)>15`. Use current
signed turns and preserve that short circuit. Dispatch as follows:

- If mode0 or either participant is AI, test a remote human a needing an audience,
  then remote human b needing one. For the first match in network mode, send
  Do AI Parley to that participant with its identity first, the other identity
  second and the supplied x/y; **return from meet before common contact writes**.
  Otherwise test local human a needing an audience, then local human b needing
  one, invoking V05's AI/player audience with playerInitiated0 for each. The
  source rechecks local identity before each networked local call; a changed
  identity there also causes early return. Later tests observe prior callbacks.
- Otherwise both participants were human. In modes1/2, test local a then local b
  with needsAudience and invoke the same V05 audience entry for each match.
- In network mode, recheck both are human. If a→b lacks Contact, a's last contact
  with b is negative, or `turn−lastContact(a,b)>15`, invoke V05's two-human parley
  request. Incident alone does not trigger this route. Its busy/refusal/cancel
  result does not itself bypass the common contact writes below.

At the common continuation, if a→b lacks Contact, bilaterally set ChangedThisTurn.
Then capture firstContact as whether a→b **still** lacks Contact, and bilaterally
set Contact+RecentContact. Do not mark firstContact from the state before the
entire audience operation: a child can already have published Contact.

Enter the AI-to-AI continuation only when both are now AI and either
`(s32(turn+a+b) & 3)==0` or captured firstContact. Skip that continuation when
Scenario, the special scenario diplomacy flag, unordered pair{1,3}, and turn<=4
all hold. The common contact publication still occurs in that special case.

Evaluate each side's willingness to remain peaceful, first(a,b) then(b,a).
A side is peaceful if any of these short-circuit tests succeeds:
[A08 `feelsSecure(side,other)`](A08-ai-research-government-and-diplomacy.md#security-against-another-civilization) returns0; other has active Great Wall; other has
active United Nations; or side's government>Fundamentalism and side→other lacks
Aggressor. Evaluate the second side only when the first was peaceful.

If both are peaceful, invoke [A08's ordered AI advance exchange](A08-ai-research-government-and-diplomacy.md#ordered-ai-advance-exchange)(a,b). Then test
live a→b Peace: absent invokes the peace transition below; present invokes the
alliance review below. If either side was not peaceful, invoke the war transition
when a→b lacks War **or captured firstContact is true**. An existing War with
firstContact false does nothing further. Do not substitute `declareWar`: these
transitions have their own notices, raw flags and reputation behavior.

### AI peace transition

With autoplay off, if a→b has Aggressor, set names and query b's United Nations;
show UN Force if present, else query Great Wall and show Wall Force if present.
Then separately do the mirrored b→a Aggressor/a-wonder test with live state.
These forcing notices do not require the local player to have an embassy.

Next, with autoplay off, prefer an Ally Makes Peace notice if local→a has Alliance;
otherwise try local→b Alliance with swapped names; otherwise use the ordered
observer predicate from section6 to show Sign Peace. Finally bilaterally set
Peace regardless of which notices ran. It does not set ChangedThisTurn or
clamp attitudes as the negotiated peace wrapper does.

### AI alliance review

Capture wasAllied from a→b. Start useExistingAlliance=wasAllied and leaderGap=false.
If a has rank7 and b rank>3, or b has rank7 and a rank>3, set useExistingAlliance
false and leaderGap=true, with the second test reading live ranks.
Start alliedThirdCount0, target−1 and targetLeader=false. Scan third slots1…7,
excluding a/b, without an alive filter:

1. If either a→third or b→third has Alliance, increment alliedThirdCount.
2. Otherwise set targetLeader to whether third is human, has rank7, difficulty
   is not Chieftain, stored city count>=5 and turn>=201. If true, choose third
   and stop the scan.
3. Otherwise on non-Chieftain, when useExistingAlliance is true, choose the first
   third for which both a→third and b→third lack Peace. When it is false, require
   no leaderGap, `hostile(a,third)`, rank(a)<=rank(third), `hostile(b,third)` and
   rank(b)<=rank(third), in that order. Choose and stop on success.

After the scan, Scenario plus the special scenario diplomacy flag, unordered
pair{6,7} and turn>1 forces target0, regardless of the prior choice.
Reject an alliance target if target<1, or if all of the following hold:
alliedThirdCount!=0; useExistingAlliance is false; and either targetLeader is
false or the **game player civilization's** unsigned reputation<alliedThirdCount.
The reputation owner is not necessarily a, b or local human.

On rejection, if wasAllied, autoplay off and the ordered observer predicate
succeeds, show Cancel Allied. Then bilaterally clear Alliance. This transition
does **not** call the expelling `cancelAlliance` wrapper.

On acceptance, when wasAllied is false, start joinedWar0. Evaluate the ordered
observer predicate, additionally accepting a human target when local has Contact
with a or b. Only inside that observer-qualified branch:

- Set a/b names. If autoplay off and targetLeader, use C01's nearest city query
  at supplied x/y with owner preference a, excluded−1, extra owner b. Set city
  name only if found, leaving the existing text argument otherwise; set target
  adjective and show Sign NATO. If autoplay off and not targetLeader, set target
  plural name and show Sign Allied.
- Call `joinAllyWar(a,target,b)`, then `joinAllyWar(b,target,a)`, and OR their
  results. These calls still run under autoplay; only the preceding notices
  were suppressed. An unqualified observer skips both joins.

Still in the newly-allied path, if target is human, V07's launched query is false
and joinedWar==0, test a→target War. If absent, bilaterally set a/target Incident.
Test **a→target War again**, and if absent bilaterally set b/target Incident.
The second test is not b→target. Finally bilaterally set a/b Alliance, including
Peace. Existing accepted alliances skip the joined-war/incident block but still
invoke that final setter.

### AI war transition

With autoplay off, prefer a local alliance with a, otherwise with b. For the
chosen allied side, call A08's security query against the other; if it returns0,
invoke V05's ally-help demand before showing Ally Makes War with that side first.
If neither local alliance qualifies, use the ordered observer predicate to show
Declare War unless a→b has Peace without Alliance, in which case show Cancel Peace.

Then bilaterally clear Peace, bilaterally set War, and inspect live a→b Vendetta.
If present, bilaterally clear Vendetta and directly set Aggressor on a→b only.
No reputation penalty, expulsion, +100 attitude adjustment or automatic ally scan
is implied by this path.

## 11. Civilization diplomacy maintenance

The complete turn parent first executes [V02's government prefix](V02-governments-and-tax-rates.md),
including its flag clearing, Anarchy selection and revolution announcements.
After that, if the full civilization argument is zero, return. Otherwise:

1. Draw primary R; store unsigned8(R%100) as diplomacy random.
2. Draw primary R; if R%3==0, toggle SenateActive.
3. If signed turn%3==0 and signed request count!=0, decrement it with signed8
   retention. A negative count is not clamped toward zero: −128 becomes127.
4. Run the first relation scan below.
5. If c is currently AI, run the later policy/notification scans below.

The two initial draws occur for every nonzero c, even a human, regardless of
whether there are live counterpart civilizations. All subsequent random draws
belong to admitted branches or their children. Current human/government state
can differ from the state at parent entry.

### First relation scan

For other=1…7, including c and inactive slots:

1. When currently networked, process messages(1,0). Then snapshot c→other
   ChangedThisTurn into that slot's shared recent-peace indicator as0/1.
2. Directly clear ChangedThisTurn, WarThisTurn and MetThisTurn from c→other.
3. Only if other is alive, differs from c, and c is currently AI, run the
   following relationship work. The snapshot/clear already happened otherwise.

For an admitted pair, if c→other has HostileIntent, let
`n=clamp(3−floor(unsignedReputation(other)/4),1,3)`. If n<=1, use roll0 without a
draw; otherwise draw primary R and use R%n. On roll0:

- Without Alliance on c→other, directly clear Ceasefire, Peace and HostileIntent
  there; directly set BrokenTreaty+Aggressor+AggressorLong. Set other's last
  contact with c to signed16(max(oldLastContact,turn−8)). This moves an old date
  forward, rather than forcing every date to exactly turn−8.
- With Alliance, adjust c→other attitude by+100, store other's last contact with
  c as−1 and directly clear HostileIntent on c→other. Leave Alliance intact.

Then, only if other is currently AI, continue:

1. Directly clear Tribute on c→other.
2. On turns divisible by32: if AggressorLong is absent, directly clear Aggressor;
   then clear AggressorLong unconditionally. This permits a two-boundary lifetime.
3. On turns divisible by16, if RecentContact is absent, perform the stale-contact
   continuation below. After it, directly clear RecentContact on c→other whether
   or not that continuation ran.
4. On other turns divisible by8, if Alliance is present and RecentContact absent,
   invoke A08's AI advance exchange(c,other). This is the else branch of the
   divisible-by16 test; it does not run a second time on those turns.

### Stale-contact continuation

When c→other lacks Alliance:

- If Peace is absent **or** AI Gang Up is off, test for Contact+War on c→other
  with Aggressor absent in both directions. On success perform War Ends notices
  as described below. The notice operation itself does not set Ceasefire.
- Otherwise, Peace and AI Gang Up are present: call `meet(c,other,0,0)`.

When Alliance is present, invoke A08's AI advance exchange(c,other) instead.
After either branch and its callbacks, if c→other still has Contact, both
directions lack Aggressor, and either c or other is currently AI, bilaterally
clear Contact through section3. This can leave an Alliance without Contact;
do not add an implicit alliance exception or rebuild normalized relations.

For War Ends, the local notice requires both currently AI, then the ordered
observer predicate, then autoplay off. Set names and show War Ends. Separately,
if network mode>=3 at entry to the remote block, scan observers1…7, requiring
current human membership and observer!=local. Their observer predicate is ordered
differently: reveal map; observer→c Embassy; observer→other Embassy; observer
United Nations; observer Marco Polo. For each success set both names and send
War Ends to its peer. Remote notices do not require autoplay off, and the mode
is not re-tested for every observer once this block was entered.

### Later AI policy scans

If c is AI after the first scan, enter the whole following block:

1. For other1…7, process messages(1,0) when currently networked. If other is human,
   autoplay off and c→other Contact present, invoke A08 relation review locally
   when other is local human; otherwise, when networked, send Political
   Violations(c,other) to that peer. There is no alive test here.
2. Invoke A08 military aid(c), regardless of how many earlier recipients qualified.
3. For other1…7, again process messages(1,0) when currently networked. If other
   is human, invoke A08 seek-war-ally(c,other) locally or send Political
   Attitude(c,other) when networked. This second scan has neither Contact nor
   autoplay gate and no alive test.

Do not re-test whether c is AI before every loop/body after entering this block.
Child policy decisions are not supplied by this document. In particular, the
original relation review's uninitialized attitude-delta path needs its incoming
value or a bounded stop; a fixture adapter does not establish deterministic
original AI behavior or complete turn conformance.


### Global reputation maintenance

This is a separate F04 global-turn-advance child, after the new turn/calendar
and unit reset and before history/arrival processing. Capture
`period=12*(unsignedDifficulty+1)`. Only when signed turn is divisible by that
period, scan every civilization slot0…7, including inactive slots:

```text
if (turn divisible by 2*period OR c has active Eiffel Tower)
   AND (unsignedReputation(c)>1 OR c has active Eiffel Tower)
   AND unsignedReputation(c)!=0:
    decrement reputation with unsigned8 retention
    if c is currently human:
        for observer = 0..7:
            observer.credit[c] = signed8(clamp(signed8(oldCredit),0,live reputation(c)))
```

Reference evaluation short-circuits: the first Eiffel query is skipped at the
longer period; the second is skipped above reputation1. Pure queries on stable,
fully backed inputs can be combined under X01; retain the reference sequence
where admitted reads or failures distinguish it. A nonzero reputation
therefore decreases at the longer interval, but reducing1 to0 requires Eiffel.
A qualifying Eiffel owner can decay at the shorter interval. Credit changes
use the reduced reputation, affect all observers regardless of activity, and
occur only for a human recipient. The ordinary no-wonder endpoint is1, not0.
C08 separately owns Eiffel's one-time acquisition halving and attitude effects.

## 12. Conformance cases

| Input | Required observation |
| --- | --- |
| Stored attitude255 queried, then set101. | Query255, store100. |
| Stored100, matching evaluation95, adjust+100. | Stored100, evaluation195, final stored re-read. |
| Remote hot-seat human, adjust−50. | Stored attitude unchanged; matching evaluation still decreases50. |
| Contact+Ceasefire, attitude49 then50. | Hostility false then true; adding Peace suppresses it; War overrides Alliance. |
| Clear Contact from a relation containing all flags. | Remove Contact/War/Aggressor/SneakAttack bilaterally; Peace/Alliance remain. |
| Set Alliance+War together. | Final Alliance+War+WarThisTurn, without Peace. |
| Sign Peace from attitude90. | Other→actor stored50; ChangedThisTurn bilateral; actor last-contact updated. |
| Agree Ceasefire, third parties marked Aggressor+AggressorLong toward actor. | Clear their Aggressor after the notice; retain AggressorLong and counterpart request count. |
| Human rank7 with five cities breaks both Peace and Ceasefire, non-Chieftain, no Eiffel. | Reputation+4 and own credit toward victim−2, then War and aggression flags. |
| Same actor breaks Alliance. | Cancellation/expulsion; Peace can remain, no War set by this branch. |
| Mediator credit127, declaration without prior treaty. | Credit−128 and mediator's attitude toward actor−5. |
| Force War from Contact+Alliance+Peace. | Finish at War, request both stored attitudes100, mark a new SneakAttack only if neither direction had one. |
| No Contact, AI pair with only Ceasefire. | Force War can return0 with Ceasefire retained. |
| Candidate ally inactive but relation filters pass. | Activation still acts; no alive filter. |
| Human enemy lastContact15 at turn10, then lastContact16. | First join-war attempt rejects before drawing; second passes time gate. |
| Republic active senate, retained random50, victim reputation0 then4. | Block, then permit; no fresh random draw. |
| No Senate Intervention scenario. | No senate block. |
| Attack confirmation reply1, Democracy blocks. | Overrule and return1; no treaty mutation. |
| Turn3 request count−128; primary123 then0. | Random23, senate toggled, request count127. |
| Human turn, stale treaty flags. | Snapshot/clear all seven outgoing slots; no AI relation aging. |
| HostileIntent against reputation8. | No branch random draw beyond the two turn-entry draws. |
| HostileIntent+Alliance. | Attitude+100, reverse lastContact−1, keep Alliance. |
| Aggressor+AggressorLong at turn32. | Keep Aggressor, clear long flag; later eligible boundary can clear Aggressor. |
| AI Alliance without RecentContact at turn16. | Exchange advances, then possibly clear Contact while retaining Alliance. |
| Networked AI with one contacted local human, autoplay off. |21 message pumps across three scans; review, military aid and seek-ally in order. |
| Turn0 or rejecting negotiation event. | No common Contact publication. |
| Remote human requires AI audience. | Forward and return before common contact writes. |
| Two-human parley callback returns. | Parent continues common Contact/RecentContact/first-contact publication. |
| AI pair{1,3}, special scenario, turn4. | Publish Contact but skip AI peace/war continuation. |
| Two occupied neighboring tiles of one city. | Count only first qualifying tile; second stays unmarked. |
| First qualifying stack already marked BorderIntruder. | Count0, still skip later tiles. |
| Four eligible units on Road+Railroad+Mine+Irrigation+Fortress. | Weight28; extra repeat offense for local count>3. |
| Expelled unit has a valid own destination and is active. | Actual lift/stack relocation, cursor update and None order; no direct coordinate-only shortcut. |
| Global maintenance at twice the difficulty period, no Eiffel, reputation2 then1. | Decrease2 to1; retain1. Inactive slots are included. |
| Human reputation decreases, incoming credits−5 and7 above the new value1. | Store0 and1 in those observers; AI recipient credits are unchanged. |
| Invalid relation/region/record write. | Safe rejection/stop, never required adjacent-field corruption. |

## Appendix A — Evidence and representation

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary pin: OpenCiv2 `a71e8aefffe04e6f719e0e213b027b128a747711`, compared with
OpenCiv2 revision `fc2155ccdb2123c1137babab6fa39e38152278c9`.
Sources: [diplomacy](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/diplomacy.cpp),
[global turn advance](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp),
[civilization maintenance and attitude](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp),
[state layouts](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h),
[flags](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h),
[shared border/turn workspace](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/game.h),
and [bug ledger](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md).

| Entry | Original address |
| --- | --- |
| Attitude adjustment/get/set | `00456f20`, `00467904`, `00467933` |
| Bilateral clear/set; attitude category; hostility | `00467750`, `00467825`; `004679ab`; `00467af0` |
| Form alliance / sign peace / agree ceasefire | `0045a535`, `0045a6ab`, `0045a7a8` |
| Activate allies / declaration | `0045a8e3`, `0045ac71` |
| Expulsion / alliance cancellation | `00467baf`, `00467ef2` |
| Intrusion count / senate query | `0055bbc0`, `0055bef9` |
| Join ally war / contact parent and private transition helpers | `0055d685`, `0055d8d8` |
| Combined government/diplomacy turn parent | `00560084` |
| Global reputation region of F04 turn advance | `00487371`, after unit reset and before history |
| Force war / confirm attack | `00579c40`, `00579ed0` |

Independent source at `civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, includes selected
`src/diplomacy.cpp`, `src/unit_exploration.cpp`, `src/diplomacy_negotiation.cpp`
and `src/civ_diplomacy_turn.cpp` bodies. These corroborate stored versus evaluation
attitude, the final attitude return, recursive treaty order and discarded reads,
negotiated transitions, contact/turn publication and live observer gates.
The original executable `Civ2/civ2.exe` SHA-256 is
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
No new original runtime or direct disassembly is claimed for this document.

Representation details belong to validation and F06/N02 serialization:

| Relation name | Original hexadecimal mask |
| --- | --- |
| Contact / Ceasefire / Peace / Alliance | `1` / `2` / `4` / `8` |
| Vendetta / HostileIntent / BrokenTreaty / Embassy | `10` / `20` / `40` / `80` |
| NuclearThreat / Fighting / RecentContact / Aggressor | `100` / `200` / `400` / `800` |
| SneakAttack / War / ChangedThisTurn | `1000` / `2000` / `4000` |
| Incident / Nuked / Tribute / AggressorLong | `10000` / `20000` / `40000` / `80000` |
| Mercenary / WarThisTurn / CityCaptured / MetThisTurn / SpyChat | `100000` / `200000` / `400000` / `800000` / `1000000` |

- Civilization relation records start at offset `20` hexadecimal; attitude,
  reputation credit and aggression counters start at `40`, `48`, `50`.
  A counterpart's record is not an alias for the actor's directional state.
  Stored reputation is at `1e`, request count at `1f`, diplomacy random at `16`.
- No Senate Intervention is scenario flag `0001`; the special scenario diplomacy
  flag is `8000`. Its explicit pair/turn exceptions above are retained behavior,
  not a new user-selectable diplomatic system.
- Border workspace has full signed counters and a selected city. BorderIntruder
  and BorderWarned are unit flags, not entries in that workspace. The original
  coordinate temporary's reuse does not require the same variable arrangement.
- Political Violations/Attitude and Do AI Parley use N02 message identities from
  the pinned enumeration. Activate Ally, Ally Helps and War Ends remote notice
  kinds are `38`, `39`, `50` hexadecimal, with text counts4,4,2. V05 owns the
  two-human parley continuation, including its waiting/cancel state.
- B4 removes the classic first-tile intrusion exit when fixes are enabled. B6
  clears a stale parley-cancel count before a new audience; classic leaves it
  for V05's waiting logic. Neither improvement is silently folded into contact.
- B3's AI relation review can read an uninitialized attitude-delta local when
  no border incident initializes it. The primary classic preset `6715d7a0`
  hexadecimal describes one replay; the independent notes record a different
  prior-stack value. Neither is a universal original default.
  [A08](A08-ai-research-government-and-diplomacy.md#7-periodic-relation-review)
  specifies supplied input or a bounded stop, and the final no-Contact overwrite
  that establishes zero independently of the incoming value. Initializing zero
  instead is a separate repair. This read issue is distinct from excluded writes.
- Native sprite alternatives, pointer/record arithmetic and compiler scaffolding
  do not prescribe cleanroom representation. Arbitrary adjacent memory is not
  valid relation, region or notification-array storage.

## Appendix B — Validation and remaining joins

An adapted Linux-host harness adds 22 complete selected source bodies to the
existing finite source harness: bilateral mutations, category/hostility,
expulsion/cancellation, negotiated transitions, ally activation/declaration/join,
intrusion/senate, forced war/confirmation, contact parent with four human/AI
transition helpers, and the combined diplomacy-turn parent, plus the exact
global reputation maintenance region from F04's turn-advance parent. It inherits actual
attitude operations, V02 government children, C01 city queries and U01
lift/stack relocation, among other previously extracted bodies. Native record
arithmetic is replaced by the same named retained fields; pointer carriers are
widened for the host and sprite choices are presentation adapters.

The focused optimized and AddressSanitizer/UndefinedBehaviorSanitizer runs each
passed **85 assertions**, with leak checking disabled for the host environment.
They exercise attitude clamp/scratch/wrap/hot-seat gates, category boundaries,
hostility, recursive/contradictory flags, negotiated effects, double treaty-break
penalties, alliance cancellation versus war, credit wrap, forced war, inactive
allies, reversed contact-date gate, senate/confirmation, turn draws and retained
counter wrap, directional aging, ordered policy scans, event/remote/contact gates,
special scenario admission, classic intrusion early exit/weights/markers and
actual unit expulsion with cursor/order changes, and global reputation
periods/endpoints, inactive slots, credit normalization and retained unsigned values.

A08 stance/security/exchange/review/aid/seeking, V05 audiences/help demands/parley,
scenario-event decisions, active wonders, text/modal/portrait services and network
send/pump behavior use declared adapters. These are not full native UI/session or
complete AI joins. The selected peaceful/war contact paths execute actual state
transitions; exhaustive alliance-target/observer permutations, coastal expulsion,
post-callback classification changes and malformed supplied backing remain beyond
this focused run.

All 27 selected source bodies, including the enclosing global-turn parent and
reviewed parley helper, were compared with the pin. Reference trees and executable
identity were checked, along with document links/anchors, specification IDs and
whitespace.
These are finite source checks, not an independent cleanroom implementation or
original-program conformance. Remaining joins include full W03/U02/U06/U07
callers, C07/C08 attitude effects, consecutive F04 turns, A08's original-input
relation review, V05/V06 transactions, S02 negotiation events, and native N02/P04
message/modal behavior. X02 must distinguish supplied-read boundaries, retained
scalar wrap, declared repairs and excluded-write diagnostics in its traces.
