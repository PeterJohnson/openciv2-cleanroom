# A08 — AI research, government and diplomacy

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. This document owns advance valuation, AI rates and strategic
government scores, government choice, diplomatic stance/security, alliance
willingness, AI advance exchanges, periodic relation reviews, military aid and
requests for a war ally.

[V03](V03-research-and-technology.md) owns prerequisite walks, research
recommendation/selection, knowledge and grants. [V02](V02-governments-and-tax-rates.md)
owns government availability, actual transitions and rate commands.
[V04](V04-diplomacy-and-treaties.md) owns relations, attitude, treaty mutations,
border-intruder scanning, war and turn-diplomacy dispatch.
[V05](V05-diplomatic-negotiation.md) owns emissary admission, interactive
negotiation, request pricing, session reset and the controller using the stance
and willingness supplied here. [V06](V06-espionage-and-bribery.md) owns AI mission
selection; [A07](A07-ai-special-unit-missions.md) owns unit destinations.

[A01](A01-ai-strategy-and-assessment.md) owns civilization/region summaries,
leader parameters and government-choice scheduling. [F04](F04-turn-sequencing.md)
owns the city-turn parent, its collected status and the ordering of rates,
government-score adjustments and diplomacy. [C02](C02-city-tiles-and-worker-assignment.md),
[C04](C04-city-happiness-and-disorder.md) and [C05](C05-city-growth-and-support.md)
produce city contributions to government scores. [C08](C08-buildings-and-wonders.md)
owns effective wonder queries; [V07](V07-spaceship.md) owns race-active,
race-behind and launch queries. [U01](U01-unit-lifecycle-and-stacks.md),
[W01](W01-map-geometry-and-spatial-state.md) and [W03](W03-visibility-and-exploration.md)
own the unit, spatial and revelation children used below. P04/P07 and N01/N02/N05
own real dialogs, reports, message receipt and session authority.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

AI scores, candidate sets, deterministic ties and stateful decisions are unchanged.
Pure candidate work may be reordered only with identical decisions and required
scratch/publication effects. Mutating surveys, retained cursors, live bounds,
child calls and sampled-value reuse prevent assuming an entire chooser is pure.

## 1. State, arithmetic and input boundaries

Use [F01](F01-determinism-and-arithmetic.md) arithmetic and primary random R().
Unless a step specifies another domain, F01 defines reference results for
addition, subtraction, multiplication and left shift. Equivalent evaluation
may omit reductions proven irrelevant to required behavior.
Division truncates toward zero; right shifts are arithmetic. Reached arithmetic
faults retain prior effects rather than becoming a score of zero. A clamp applies
its lower bound then its upper bound, including accepted inverted bounds.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

[F02](F02-game-state-and-invariants.md) supplies saved scalar domains. In
particular: leader attack/expand/civilize and advance AI/civilized modifiers are
signed8; regional city counts, advance counts, active type counts, power ranks,
science/tax rates, government and reputation are unsigned; military/attack
summaries are unsigned16; city count, ship count, subversion count, government
scores and last-contact turns are signed16. Request counts and reputation credit
are signed8. Government-score writes retain signed16 after **each** mutation;
rate writes retain unsigned8 before later reads/clamps. Gold and working scores
retain signed32. Original encodings are in Appendix A, not prescriptions for
native record layout.

In diplomatic stance and willingness, A is V05's actor/target and B is the
counterpart whose AI response is being evaluated: source argument order is
(A,B). Write relation `B→A`, attitude(B,A), rank(B), strength(B), advances(B),
and `distrust(B,A)=reputation(A)-credit(B,A)`. Regional fields are
cities(C,r), military(C,r), attack(C,r), policy(C,r) and presence(C,r), using A01.
A repeated named query/read is live unless explicitly captured. Scan order and
short-circuit order matter; do not snapshot the whole meeting or civilization.

The shared evaluation E is the same storage used by V05: attitude, fear, demand,
aggressive, initiallyAggressive, suppressedAggression, requestMode,
declareWar, peaceInclination, targetIsLeader, outranksTarget, raceThreat,
lastContact, nearbyUnits, capturedCities, warTarget, strongestEnemy and
weakestEnemy. An evaluation updates the fields specified below. It does not
clear gifts, technology selections, selected attitude pair or every meeting flag.
Some fields intentionally retain earlier values when their assignment is skipped.

Nominal inputs have valid identities/rules, finite prerequisite graphs, consistent
map and records and meaningful regional summaries. Accepted unusual inputs retain
scalar wrap, negative scores/treasury, contradictory relations, stale evaluation
state and explicit read inputs. [F03](F03-rules-and-configuration.md) owns accepted
rule ingestion, which does not itself guarantee a later prerequisite walk
terminates. A stricter implementation may declare/reject a narrower domain.

Out-of-bounds writes and corruption are **excluded bugs** under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs).
Do not reproduce corruption through adjacent logical fields. Stop before an
invalid store unless safe omission and continuation are established, keeping
prior valid effects. Unknown/out-of-bounds reads remain distinct: use actual
backing, a supplied input or a bounded stop. The two private-local cases in
Appendix A distinguish an observable missing value from a proven irrelevant one.

## 2. Advance valuation

`value(C,a)` returns a score; it neither grants knowledge nor sets research.
V03 owns callers' randomized selection, including repeated valuation calls.
This valuation itself consumes no RNG.

1. Capture civilize from C's leader. If C is AI and this value≥0, scan human
   civilizations H=1…7 ascending. Require C→H War and advances(C)<advances(H).
   For regions r=1…62 ascending where both have cities: decrement the working
   civilize only while it≥0, then set it to -1 if reputation(H)>6. Continue the
   remaining regions/civilizations; do not stop when civilize becomes negative.
2. Set score=`advance[a].civilizedModifier*workingCivilize + advance[a].aiValue`.
3. Calculate expansion bonus and apply its regional contribution below.
4. If a is the Port Facility prerequisite, add signed shipCount(C)/4.
5. Apply the AI-only bonuses below.
6. If a is an Aqueduct or Sewer System prerequisite, scan live owned cities
   ascending for maximum signed size, initially zero. Choose Aqueduct's unsigned
   size limit when its prerequisite matches; otherwise Sewer System's. If
   maximum≥limit, add 2. Matching both does not add twice.
7. If the **live original** leader civilize≥0 and C knows University, query
   V03 ancestry(Computers,a). If true add live leader civilize+1. Independently
   within that University gate, if a=Computers add 2.
8. Query Invention knowledge. If known and ancestry(Industrialization,a), query
   Navigation knowledge and add 2 if known, otherwise 1.
9. Apply the tribe table below using the live tribe selector.
10. If no civilization is recorded in a's global discovered-by set, add 1.
11. Scan advances b=0…99. If either direct prerequisite equals a, query C's
    knowledge of b; if known subtract 1. Matching both prerequisites subtracts
    once. No enabled or ancestry filter is added.
12. If score<2 replace it with 1, then return.

### Expansion contribution

Use V03's ordered ancestry query against the **rule prerequisites of fixed unit
identities**. The first true branch wins:

| Ancestry query | expansionBonus |
| --- | --- |
| Trireme prerequisite descends from a | 3 |
| Otherwise Caravel prerequisite descends from a | 2 |
| Otherwise Frigate prerequisite descends from a | 2 |
| Otherwise Transport prerequisite descends from a | 1 |
| Otherwise | 0 |

Double expansionBonus on a LargeMap game. When it is nonzero, initialize
freeRegion=false and humanContact=false. For r=1…62 where C has cities,
initialize contested=false and scan k=0…7. For k≠C, test **C's own** city count
or military count in r again. If either is nonzero, set contested=true; if C is
human set humanContact=true; break the k scan. If not contested, set freeRegion
and break the region scan. The inner test does not read k's regional presence.

If freeRegion, add expansionBonus. Otherwise add 1 when !humanContact or
LargeMap. On stable ordinary inputs, any visited owned region is contested by
this own-record test; this is not a corrected survey of foreign civilizations.
When expansionBonus=0 this whole regional phase is skipped, with no score change.
The tribe-11 branch below can safely test expansionBonus first; Appendix A proves
why the otherwise unset freeRegion value cannot affect its zero-bonus result.

### AI-only bonuses

Skip this subsection when C is human. Otherwise:

1. If any human belongs to Space Flight's global discovered-by set: add 2 if
   ancestry(Fusion Power,a), then visit Structural, Component and Module
   improvements in order and add 3 for **each** matching prerequisite.
2. Read the fixed Bomber's rule prerequisite. If nonnegative, known globally
   by a human, and ancestry(prerequisite,a), add 5 if it equals a, otherwise 2.
3. For each wonder in ascending identity whose expiry advance equals a:
   query C's active ownership; if true and a≠Industrialization subtract 2.
   Then query the active wonder city; if present and its live owner is human,
   add 2. These are separate queries and may both contribute.

### Tribe contributions

Tribe identities refer to F03's fixed leader slots, even with custom names.
Each listed adjustment applies independently; unlisted advances/tribes add zero.

| Tribe | Contribution |
| --- | --- |
| 0 | Iron Working, Bronze Working, Warrior Code +2 each; Monotheism -1. |
| 1 | Code of Laws +1. |
| 2 | Banking, Theology, Philosophy +1 each. |
| 3 | Masonry +2. |
| 4 | Democracy +2; Communism -1; Rocketry, Computers, Leadership +1 each. |
| 5 | Polytheism, Bronze Working, Alphabet, Map Making +1 each; Monotheism -1; Philosophy +2. |
| 6 | Polytheism +2; Horseback Riding, Mysticism, Ceremonial Burial +1 each; Monotheism -1. |
| 7 | Communism +2; Philosophy, Guerrilla Warfare +1 each. |
| 8 | Polytheism +2; Horseback Riding, Mysticism, Ceremonial Burial +1 each; Bronze Working and Iron Working -1 each. |
| 9 | Leadership, Tactics, Conscription +1 each. |
| 10 | Gunpowder -2; Monotheism -1. |
| 11 | Subtract expansionBonus when nonzero and freeRegion; Gunpowder +1. |
| 12 | Monotheism +1; separately +1 for nonzero expansionBonus. |
| 15 | Steel and Miniaturization +1 each. |
| 16 | Map Making +1. |
| 17 | +1 for expansionBonus>1; separately Monotheism +1. |
| 18, 19 | Polytheism +2; Horseback Riding, Mysticism, Ceremonial Burial +1 each; Monotheism -1. |
| 20 | Horseback Riding +2. |

## 3. Rates and government policy

### Rates after the city pass

F04 finishes the descending city pass, closes its notice batch, and adds twice
Despotism's score to Anarchy's retained score. It then calls this AI-only rate
phase with the collected largeBalancedCity and disorder predicates. A human
civilization skips rates here, but still receives the strategic score phase.

For AI C:

1. Capture L=`10-(unsigned science+unsigned tax)`. If disorder and L<4,
   increment L. If turn modulo4=0, neither collected predicate is true and
   L>2, decrement L. Clamp L to 0…4.
2. Store science=`live leader civilize + ((10-L)>>1)`, retaining unsigned8.
   Increment that stored value when gold>turn+100, then independently when
   gold>2000. If gold>8000, replace science with 10-L. Each write retains its
   value domain before a later test.
3. Start needsExpansion=false. Scan r=1…63 with nonzero C city count. Set it
   for policy Attack or Defend, or for policy Expand with nonzero presence.
   Continue the whole scan. If false and live science<7, increment science.
4. Start cap=6. Live government above Despotism replaces it with 7; above
   Fundamentalism with 8; above Republic with 10, in that order. Clamp the
   **unsigned stored** science to 0…min(cap,10-L), then store it.
5. In short-circuit order query knowledge of Fusion Power, Laser, and the
   current Structural, Component and Module prerequisites. If all succeed,
   set science=0. No enabled, race-active or Bloodlust test is added here.
6. Store tax=`10-(live science+L)`, retaining unsigned8. Luxury is the implicit
   remainder, not a separately updated saved rate.

A negative intermediate science can wrap to a high unsigned value and then hit
the upper clamp; do not clamp the earlier signed expression to zero.

### Strategic adjustments to government scores

Run for all C after rates. Define K as the **live stored best-human-rank number
used directly as a civilization index**. Do not translate it through civByRank.
Reload this selector and the named state at each access, rather than binding
one civilization across child queries. All score writes below retain signed16.

1. Start bonus=0. Query C's Democracy knowledge, or if absent its active
   Statue of Liberty. If either succeeds and government(K)=Fundamentalism,
   set bonus=subversion(K)+1.
2. Add `(activeDiplomats(K)+activeSpies(K)+subversion(K)) *
   (subversion(K)+bonus)` to C's Democracy score. Each quantity is its named
   scalar, not an espionage-capable unit census. Add 10 more if gold(C)<gold(K).
3. If bonus=0 and C knows Communism or Fundamentalism or has active Statue of
   Liberty, capture leader attack, then query raceActive. If
   `rank(C) <= attack + (raceActive ? 4 : 3)` and advances(C)<advances(K),
   set Democracy then Republic scores to zero.
4. If live leader attack≥0, perform both of these blocks in order:

   - If the civilization at rank 7 is AI, and attack=1 or (attack=0 and
     civilize<0), multiply Democracy by 3 then divide by 4 and store; repeat
     for Republic. Division truncates toward zero.
   - If attack≠0 or (civilize<0 and bonus=0), set a=`2*attack-civilize`.
     If rank(K)-1≤rank(C) or advances(K)≤advances(C), compare technology counts:
     when advances(K)≤advances(C)+4*a, set both scores to zero; otherwise, if
     advances(K)≤advances(C)+8*a, halve both. Outside that outer rank/knowledge
     gate, neither comparison changes the scores.
5. Query C's launched-ship flag. If true and difficulty>King, halve Democracy
   then Republic.
6. If Bloodlust and live science=0, zero Democracy first; if live leader
   attack≥0, zero Republic too.
7. Clear C's 28 wonder-notice entries in ascending order, as F04's closing
   boundary. This is not a clear of the wonder ownership state.

The product in step 2 may overflow working arithmetic and the accumulated score
may wrap negative. The primary source narrows bonus within that product; adding
or subtracting a multiple of 65536 before the final signed16 store does not change
its result. Appendix A records the original full-width local and store boundary.

### Choosing a government

`chooseGovernment(C,restricted)` uses already accumulated scores:

1. If Scenario, its government-lock option is set, and live government≥Despotism,
   return before all scoring and random activity.
2. Start maximum=Democracy. If restricted, set maximum=Republic, draw one R(),
   and reduce maximum to Fundamentalism when R()%3≠0.
3. With AIGangUp, capture the unsigned advance-count difference
   `advances(playerCivilization)-advances(C)` as signed32. If rank(C)<6,
   difference>6 writes Democracy score=-2 and difference>8 writes Republic=-1.
4. If Anarchy score>0, replace maximum with Communism; if current government
   ≤Republic, replace it with Despotism. The earlier restricted draw is retained.
5. Start best=-999 and selected=Despotism. Visit governments Despotism through
   maximum ascending, querying V02 availability. For an available government,
   score≥best replaces both. **Later ties win**; a child can change the score
   before its comparison/reload.
6. Call V02 ChangeGovernment(C,selected), even if no available candidate replaced
   the default or selected equals current government. That child owns actual
   resets, notices and transition restrictions.

## 4. Diplomatic stance

`evaluate(A,B)` publishes an ordered, mutable evaluation; it does not declare
war merely by setting E.declareWar. V05/V04 own the consuming operation.

### Initial state and regional pressure

1. Set E.raceThreat=V07.raceBehind(B). Start warCount=0. Scan k=1…7: count A→k
   War, then for each human k query launched(k); if true and difficulty>King,
   set raceThreat=1. No alive or k≠A/B filter applies.
2. Set outranksTarget=(rank(A)<rank(B)), suppressedAggression=0. Only when
   openingEvaluationActive and !initialEvaluationDone, capture A's last-contact
   turn toward B into E.lastContact. Otherwise retain it.
3. Set fear=0, demand=0, aggressive=false, requestMode=1.
4. For r=1…63, in order:

   - If cities(B,r) is greater than the Boolean `(B.totalCities>1)`, and
     military(B,r)<attack(A,r), add
     `attack(A,r)*(difficulty=Chieftain ? 4 : 2)/(military(B,r)+1)` to fear.
     This contribution uses unsigned division and retains the full result.
   - Otherwise, if both regional military counts are nonzero set aggressive.
     Then update demand: if A has no cities there and B also has none, add
     `(attack(B,r)-attack(A,r))/4`; if A has none but B has cities, add
     `(military(B,r)-attack(A,r))/2`. If A has cities, add attack(B,r) when
     B has fewer than two there and (turn≤200-50*warCount or rank(A)>6 or
     rank(B)>6); otherwise add attack(B,r)-attack(A,r).

5. Query B's active Marco Polo. If present, add demand/4 to demand.
6. Set targetIsLeader when all hold: rank(A)=7, A.totalCities>4,
   B.totalCities>1, A has no fixed Nuclear Missile units, difficulty≠Chieftain
   and turn>200; otherwise clear it. Copy raw attitude(B,A) into E.attitude.

### Nuclear, reputation and attitude modifiers

Apply these steps in order; an earlier aggression assignment can be reversed:

1. If (targetIsLeader or (B→A Vendetta and strength(A)<3*strength(B))) and
   A has no Nuclear Missiles: clamp demand to 2*rank(B)…9999. If aggressive
   is still false, clamp it again to 0…`(gold(A)/50)/(8-rank(B))`. Then set
   aggressive=true and fear=0. If attitude<76 replace it with 75.
2. If raceThreat and outranksTarget, set aggressive=true.
3. If B has Nuclear Missiles, directly add B→A NuclearThreat. If A has none,
   set aggressive and clamp demand to 100…9999.
4. If A has Nuclear Missiles and B→A lacks Alliance: when B has none, set
   fear=max(4,(reputation(A)>>1)+1); otherwise replace demand with
   `activeNuclear(B)*(demand/2)/activeNuclear(A)`.
5. Divide signed fear by `(reputation(A)-1)/2+1`. This uses signed truncation
   for the inner division too; reputation zero gives denominator 1.
6. Subtract 25 from attitude for B→A Alliance; independently add 25 for War.
   If reputation(A)≠0, add `5*(reputation(A)-1-credit(B,A))`.
7. If attitude<26, halve demand and clear aggressive. Otherwise query V04's
   attitude category: if category<4, set demand=2*demand/3; else if attitude>74,
   set demand=3*demand/2 and aggressive=true.
8. B→A Aggressor sets aggressive. Bloodlust with B.science=0 also sets it.
   Nonzero fear then clears aggressive and sets requestMode=0.
9. Query A's Great Wall, or if absent United Nations. If present: clear
   targetIsLeader, copy current aggressive into suppressedAggression, clear
   aggressive, subtract 10 from attitude, and invoke V04 bilateral clearing of
   Incident between B and A.
10. If reputation(A)≠0, add `reputation(A)*demand/2` to demand. If B→A Vendetta,
    double demand; otherwise, if B→A Incident is present, divide demand by 4.
11. Clear nearbyUnits. Scan live A-owned cities ascending. For an unoccupied
    city query the nearest B unit of any type; if present and its returned
    distance<3, double demand, increment nearbyUnits and break. No further
    city is examined after that success.
12. Set demand=`50*clamp(((difficulty+1)*demand)/32,0,20)`. If
    gold(A)<demand<2*gold(A) and gold(A)>49, replace it with `(gold(A)/50)*50`.
13. If demand=0 or 3*strength(B)<strength(A), clear aggressive. If B's unsigned
    aggression counter toward A>9, reset that counter to zero and clear
    aggressive. When aggressive, set requestMode=2; otherwise only an existing
    B→A Alliance sets requestMode=0. Copy aggressive into initiallyAggressive.

### Other civilizations and peace inclination

Start peaceInclination=0; B→A Vendetta makes it -2. Bloodlust with B.science=0
subtracts 1. Nonzero reputation(A) subtracts `(reputation(A)-1)/2`.
Initialize weakestEnemy, strongestEnemy and warTarget to Absent, maximumStrength=0,
minimumStrength=9999 and aggressorCount=0. Scan k=1…7 excluding A and B:

1. If B→k Alliance and rank(B)<rank(A), subtract 1 from peaceInclination.
   Independently within the Alliance gate, require rank(A)=7, A.totalCities>4,
   turn>200, difficulty≠Chieftain and k→A Contact. If fear=0, k→A War and A has
   neither Great Wall nor United Nations, set aggressive. If fear≠0 instead,
   replace fear with clamp(fear-4,0,99). This does not restore requestMode.
2. Count B→k Aggressor in aggressorCount, whether hostile or not.
3. Query hostile(B,k). If true: add 1 to peaceInclination when
   4*strength(k)>strength(B), then add another when strength(k)>strength(B).
   If A→k Contact and not War, replace warTarget with k: **last eligible wins**.
   If B→k has both War and Fighting, compare strength(k): strict greater than
   maximum replaces strongestEnemy; strict less than minimum replaces
   weakestEnemy. Initial zero/9999 thresholds can leave either Absent.
4. After the scan, if aggressorCount=0 invoke V04 bilateral clearing of
   Mercenary between B and A.

Subtract live leader attack(B) from peaceInclination. If strength(A)<strength(B),
subtract 1. Query hostile(B,A). If peaceInclination exceeds that Boolean,
targetIsLeader is false and B→A has neither Alliance nor Aggressor, clear
aggressive; additionally zero demand when bit 5 of turn+B+A is set. Do not
recompute initiallyAggressive or requestMode after this change.

### War intent, scenario overrides and final attitude

1. Count live cities whose founder is B and current owner is A.
2. **Only if** !initialEvaluationDone or distrust(B,A)>6, reset declareWar=false.
   Within that gate, set it true if all hold: B→A lacks War or capturedCities>4;
   B→A lacks Alliance; one of aggressive, (raceThreat and difficulty>Prince),
   or (suppressedAggression and distrust>6 and Vendetta); and one of
   strength(B)<strength(A), Vendetta, or distrust>6. When the outer gate fails,
   retain the incoming declareWar rather than clearing it.
3. If declareWar is nonzero, clear aggressive, set requestMode=0, and replace
   attitude with 30 when it>29.
4. With Scenario and its historical-behavior option: for B=6, A=7 sets
   aggressive only if 6→7 Alliance (otherwise retains its value); A=1 clears
   it; A=3 sets it; other A clears it. For B=3 and A=6 or 1, set aggressive.
   These overrides do not recompute earlier demand, mode or declaration fields.
5. If openingEvaluationActive, capture B's signed request count then query
   V05 patience(A,B). Require captured count<patience, live B.government above
   Fundamentalism, aggressive, no B→A Alliance or CityCaptured, and no Scenario
   revolution prohibition. If 2*strength(B)<3*strength(A), !targetIsLeader and
   no Vendetta, clear aggressive; otherwise chooseGovernment(B,restricted=true).
6. If fear=0 and signed attitude<2, set attitude=1. Then, in order: if A→B
   lacks War and attitude>98, set 99; if (A→B Alliance or requestMode=0) and
   attitude>73, set 74; if A→B War and attitude<27, set 26.

The final caps use **A→B**, not B→A. No direct random draw occurs in the stance
body; restricted government choice can consume one and its transition child
retains its own effects. The captured initiallyAggressive field remains a record
of its earlier assignment, not necessarily the final aggression state.

## 5. Alliance willingness

`allianceWilling(A,B,reasons)` consumes the live E, returns a Boolean and may
change E.warTarget and the caller's reason set. V05 initializes that set to
empty before this phase; the phase itself does not reset it. Reasons are ExistingAlliedTarget,
DangerousEnemy and NewCommonTarget. Preserve reasons even if a later gate
changes willingness back to false.

Count A's Alliance relations toward k=1…7, without excluding A/B or dead slots.
Start score=1 if E.warTarget is Absent, otherwise zero, then:

1. Difficulty<Prince subtracts 1.
2. If both ranks<5: difficulty>Warlord and allyCount=0 subtracts 1; subtract
   another 1 unconditionally within the both-ranks gate.
3. If rank(A)<rank(B): add 1 for rank(B)>5, another for rank(B)>6; subtract 1
   when rank(A)<6 and strength(B)<strength(A).
4. If targetIsLeader add 1; otherwise rank(A)-rank(B)>2 subtracts 1.
5. Nonzero fear subtracts 1. Set agree=(score<peaceInclination), then query
   attitude category and replace agree with
   `difficulty=Chieftain OR (category<4 AND agree)`.
6. Rank(A)=7 with any ally forces false. Distrust above 4 on Chieftain or 2
   otherwise forces false. Then rank(A)<4 with turn>20*rank(A) forces true.

Next apply strategic overrides in order:

1. If warTarget>0, hostile(A,warTarget)=false and A→warTarget Contact, set
   strengthLimit to 3*strength(B)/2 when A→warTarget Peace, otherwise
   2*strength(B). If B→warTarget Fighting and strengthLimit≤strength(warTarget),
   set agree=true and **replace** reasons with DangerousEnemy.
2. If warTarget<1 or reasons is empty, and rank(A)<7, rank(B)<7, turn>99,
   allyCount<7-rank(A), and attitude category<5, search a common target.
   Initialize bestRank=8 and target=Absent. For k=1…7 excluding A/B, require
   rank(k)≥both ranks, A→k or B→k Contact, no A→k War and no B→k Alliance.
   Score=`rank(k)-hasContact(A,k)`; strict smaller replaces target, so first
   ties win. After this search, an existing positive E.warTarget whose rank≠7
   replaces target **without reapplying those candidate gates**. A positive
   target becomes E.warTarget, sets agree=true and adds NewCommonTarget.
3. B→A Vendetta or distrust>4 forces false.
4. If E.warTarget is nonnegative and A→warTarget Alliance, set agree=true and
   add ExistingAlliedTarget. This can override step 3.
5. Signed E.attitude>75 forces false. B's signed request count>4 with
   difficulty≠Chieftain also forces false.

No direct RNG is used. V05 owns the later price/request/response and effects;
a true willingness result alone does not sign a treaty.

## 6. Security assessment and AI technology exchange

### Security against another civilization

`feelsSecure(C,D)` uses first-argument C as the judging civilization. Capture
C→D's initial Alliance membership. C→D Aggressor returns true immediately;
otherwise C→D Vendetta returns true immediately. Without those shortcuts:

1. Scan human H=1…7. If hostile(C,H), strength(D)<strength(H) and H→D lacks
   Alliance, return false. Then independently test hostile(D,H),
   strength(C)<strength(H) and H→C lacks Alliance; return false if all true.
   No exclusion of C/D or alive filter is added.
2. If activeNuclear(C)<activeNuclear(D), return false.
3. Start security=0. For k=1…7 excluding C/D: hostile(C,k) adds 1; a human
   allied from both C and D adds 1, plus another if k outranks both. A human
   allied from C but at War from D subtracts 1. Independently rank(k)=7,
   turn>199 and D→k War subtracts 1.
4. If hostile(C,D)=false add 1. If the **entry-captured** Alliance is present
   add 1. Read and subtract C's live leader attack **before the region scan**.
5. Start ownAttack=0, theirStrength=1 and sharedCities=0. For r=1…62:
   if C has cities and attack(C,r)+military(C,r)<attack(D,r), return false.
   If attack(C,r) and military(D,r) are both nonzero, add attack(C,r) to
   ownAttack, `(attack(D,r)>>1)+military(D,r)` to theirStrength and cities(C,r)
   to sharedCities.
6. Return false if `(ownAttack<<2)/theirStrength < security+4` and
   sharedCities≠0; otherwise return true. The division is reached before
   testing sharedCities. Preserve arithmetic faults for accepted wrapped totals.

This query consumes no RNG and does not change treaties. It is not a comparison
of global strength alone. Appendix A records the original trait-read order,
which differs from the primary source's placement in the final expression.

### Ordered AI advance exchange

`exchange(C,D)` returns whether it invokes a grant. With Scenario and its
no-technology-exchange option, return false immediately. Otherwise capture
leaderFarAhead: the civilization at rank 7 is human, difficulty≠Chieftain,
its city count≥5, turn≥201, and its advance count exceeds both C's and D's.
These gates do not require a live civilization or a particular relation.

Initialize forward/reverse selections absent and both best scores zero. Scan
advances a=0…99 with at least one prerequisite unequal to -2:

1. If C does not know a and D does: draw R(), then call value(C,a). Candidate
   score=R()%3+value. Select the first admitted forward candidate regardless
   of score; thereafter replace only on strict greater.
2. Otherwise requery D's knowledge. If D does not know a and C does: draw R(),
   then value(D,a), and apply the same first/strict-better reverse selection.

Do not cache knowledge across the else branch or valuation across callers.
After the complete scan:

- If both selections exist, V03 grant(C,forward,source=D,quiet=false), then
  grant(D,reverse,source=C,quiet=false), both through the ordinary local grant
  entry. Return true after the second child. The second selection is retained
  across the first grant's callbacks and side effects.
- If only forward exists, require C→D Alliance,
  `advances(C)+2*(6-difficulty)<advances(D)` or leaderFarAhead, and no C→D
  Tribute. Set Tribute bilaterally through V04, then grant the forward advance
  to C from D and return true.
- If only reverse exists, apply the corresponding D→C Alliance/count/Tribute
  conditions. Set Tribute bilaterally using the same (C,D) argument order,
  grant reverse to D from C and return true.
- Otherwise return false.

No grant is rolled back when a later grant fails. Selecting a technology does
not bypass the real grant's knowledge, announcement, research and wonder effects.

## 7. Periodic relation review

`review(C,A)` is V04's AI review of another civilization. Its incoming signed32
attitude delta is **not initialized by the original entry**. Border processing
can assign it; otherwise the original reads retained private state. Supply that
value or use the explicit missing-input handling below. The primary replay
constant and a zero-initialization repair are not universal classic inputs.

Call the border phase first. If it returns KeepMarkers=false, scan all live
units ascending and clear BorderIntruder, regardless of owner. KeepMarkers=true
skips that scan; it does not skip attitude review. Apply the adjustment sequence
below, then V04 AdjustAttitude(C,A,delta). For network mode, send the civilization
state difference to all peers and flush with the normal 5000-unit service timeout.

### Border incident phase

Call V04's intruder scan(C,A), retaining its intruded-city count, selected city,
intruder weight, unit count, repeat count and marker effects. Capture the selected
city identity. Unless count≠0 and C→A has neither Alliance nor War, return
KeepMarkers=false without assigning delta. Otherwise capture the city's x/y:

**Without C→A Peace:**

1. If V04's recent-peace snapshot for A is nonzero, return KeepMarkers=true
   without assigning delta.
2. Assign delta=intruderWeight. Draw R() and set complaintChoice=R()%2.
   With C→A Vendetta increment the shared repeat count.
3. If reputation(A)+repeatCount>2, let bound be that live sum. If bound=1 or
   signed(bound-1)<0 use roll=0; otherwise draw R() and take its remainder by
   a fresh sum. If roll>1, set complaintChoice=0 and add Intrusion bilaterally
   through V04. The draw is skipped on the stated guard, not on a generic
   bound≤1 replacement at signed minimum.
4. If (turn+C) modulo4=0, ask V05 to receive the emissary(A,C,x,y). If accepted,
   reveal the city to A when its **signed knowledge membership** lacks A and
   its owner differs from A; redraw the tile. Format the city name and show
   NearCity for complaintChoice=0 or AdmireCity otherwise. Reset the diplomatic
   session after the ask whether accepted or declined.
5. Return KeepMarkers=false.

**With C→A Peace:**

1. If recent-peace snapshot(A) is nonzero, format the city and C's names,
   show Terms, then return KeepMarkers=true without assigning delta.
2. If A's last-contact turn toward C is earlier than turn but less than three
   turns earlier, return KeepMarkers=true without assigning delta.
3. If repeatCount=0, evaluate(A,C), then format the attitude. If the selected
   city is not known to A under the signed membership test and is not A-owned,
   reveal it, set tile visibility for A with both update flags, and redraw.
   Set delta=intruderWeight-1. Show Intruder for fewer than two intruded cities,
   otherwise Intruders, with the city name. Ignore the reply and return
   KeepMarkers=true; there is no session reset on this first-warning return.
4. Otherwise run the repeated-violation controller below, then return
   KeepMarkers=false. That controller does not assign delta.

### Repeated violation: withdraw or fight

Evaluate(A,C), format attitude, and reveal/set visibility/redraw the selected
city under the same signed knowledge/owner gate. Format its name and show
Violator for fewer than two intruded cities, otherwise Violators.

For reply=0, scan live units ascending with BorderIntruder. Clear the marker
**before** testing owner. For each A-owned unit, find its nearest owned city
with C01's ordinary declared filters. If present, U01 relocate with revelation
argument 1, clear visibility and orders, and reset its counter only when the
live type has nonzero range. After the scan redraw all views for the local human,
format the intruder-unit count, and show Withdrawn with its singular variant
only for count=1.

For any nonzero reply: Democracy qualifies for a senate scandal without a draw.
Otherwise draw one R() **before** testing Republic; qualify when R()%2≠0 and
A is Republic. If qualified and no Scenario revolution prohibition, show Senate
Scandal and V02 ChangeGovernment(A,Anarchy). Then AdjustAttitude(C,A,+50),
V04 DeclareWar(A,C,Absent), format C's name and show Violate. Finally reset the
diplomatic session for either reply path. Marker cleanup in the outer caller
still follows this controller's return.

### Attitude-delta sequence

Start from the assigned or supplied delta. Scan k=1…7, without excluding C/A:
count ahead when advances(k)>advances(A); count hostile(C,k); and accumulate
warPressure by 1 for k→A War, plus another within that War gate when
strength(k)>strength(A).
Apply these steps in order, retaining each signed32 result:

1. With C→A Alliance, when hostileCount=0: gold(C)<gold(A) adds 1;
   advances(C)<advances(A) with difficulty≠Chieftain adds 1;
   advances(A)<advances(C) subtracts 1; strength(C)<strength(A) adds 1;
   strength(A)<strength(C) subtracts 1. Otherwise, if leader attack<1 or
   hostileCount>1, subtract max(1,hostileCount-attack-1). Independently within
   Alliance, advances(C)+8<advances(A) adds 1.
2. C→A Peace subtracts 2. Absence of C→A War subtracts 1.
3. If rank(A)<7 and turn>200, set adjustment=7-rank(A); query raceActive and
   replace adjustment with 1 if true. When turn<400 replace it with
   `(adjustment+1)>>1`. Subtract it.
4. If rank(A)=7, A.totalCities>3, turn>200 and difficulty≠Chieftain, set
   adjustment=difficulty/3+1. If distrust(C,A)≤warPressure and no Vendetta,
   halve by arithmetic shift. Add it.
5. If A has Nuclear Missiles, subtract 1. If C has none and the resulting
   delta>0, subtract one more.
6. Set personality=`3*leaderAttack + 2*leaderExpand`; replace values below -1
   with -2. Set rankDifference=rank(A)-rank(C); halve it if negative, or if
   nonnegative and C→A lacks Alliance. Add personality then rankDifference.
7. If !AIGangUp, or (distrust≤warPressure and no Vendetta): subtract 1 for
   each true comparison, in order, `4*strength(C)<strength(A)`,
   `2*strength(C)<strength(A)`, `3*strength(C)/2<strength(A)`.
8. Without Alliance, add 1 if strength(A)<strength(C), then another if
   2*strength(A)<strength(C).
9. Query A's Great Wall, or if absent United Nations. If present, subtract 1
   when delta<1; otherwise arithmetic-shift delta right by one.
10. If aheadCount=0 add 1. If advances(C)<advances(A), add 1-leaderCivilize.
    With C→A War and delta<1, replace it with zero.
11. Query C's Eiffel Tower; if present add 1. Query A's Eiffel Tower; if
    present, halve delta only when positive, then subtract 1, and subtract
    another if that new result is nonnegative.
12. If C→A lacks Contact, replace delta with zero. Return it to the outer
    AdjustAttitude call. This final overwrite is not an early return.

If delta was never assigned and no actual incoming value is available, its
private arithmetic may be tracked as unknown while preserving all independent
queries/effects. The no-Contact overwrite establishes a known zero for every
incoming value. Otherwise stop before publishing the unknown result through
AdjustAttitude; do not use the replay preset. No earlier delta-dependent branch
here performs a gameplay mutation or variable child call. A stricter bounded
implementation can stop at its first unknown read, documenting that earlier
stop; neither policy initializes the original value to zero. Appendix A records
the independent reconstruction's separately declared zero-start repair.

## 8. Military aid and its contradictory admission gate

`militaryAid(C)` first counts C→k War for k=1…7 excluding C. It then scans allies
A=1…7 excluding C, requiring C→A Alliance and either strength(A)≤strength(C)
or the captured war count is zero. For each admitted ally, scan enemies E=1…7
excluding C/A. Require A→E both War and Fighting, strength(A)≤strength(E) and
rank(A)≤rank(E).

For each pair, initialize selectedUnit=Absent and best=0. Scan live C-owned
units ascending with signed actual rule role<2 and Land domain. Capture x/y,
then require **mapInBounds(x,y)=false**. Next query cityOwner at the same x/y
and require it to be nonnegative. W01's cityOwner repeats the bounds check and
returns Absent on those coordinates. With stable map dimensions this second
requirement always fails, so **no unit is selected or transferred**. The scan
still performs its reached reads/queries. Do not reverse the first test to
implement the apparently intended aid feature.

This is an original logic bug, not an out-of-bounds-write compatibility
exception. The unreachable transfer continuation is recorded in Appendix A for
validation against supplied child outcomes. Native input/state races that change
map dimensions between these pure queries are not established gameplay producers.
V04 still invokes this routine in its declared turn order; a working diplomatic
military-gift feature would be a separately declared behavior change.

## 9. Seeking a war ally

`seekWarAlly(S,P)` is a request from sender S to prospective partner P. Require
`turn modulo32 = 4*S` and distrust(S,P)<6. Scan E=1…7 excluding S/P and requiring
Alive(E). For each E, enter the request tests only if S→E Fighting, S→P has
neither Alliance nor War, hostile(P,E)=false, and P→S and P→E Contact.

### Paid request

If strength(E)≥2*strength(S), ask V05 to receive emissary(P,S,Absent,Absent).
If declined, replace P's last contact toward S with max(old,turn-15), retaining
signed16. Otherwise:

1. Capture amountUnits=clamp(gold(S)/100,1,10). Start advance=Absent,
   bestValue=-99. Scan technologies a=0…99 with a prerequisite unequal to -2
   and not known by P. Admit when S knows a; otherwise require E knows it,
   localHuman→S lacks Embassy and the local human has neither United Nations
   nor Marco Polo. Query value(P,a); strict greater replaces the selection.
   The **local human**, not necessarily P, supplies those intelligence gates.
2. Format E's name and amountUnits*50. Show HelpMe, adding its Instead variant
   if P→E Alliance and its technology variant if a was selected.
3. Reply=0 replaces P's contact toward S with max(old,turn-14). Any nonzero
   reply accepts: if an advance was selected, requery S's knowledge; if absent,
   first V03 grant it to S from E, then grant it to P from S in all cases.
4. For acceptance, add Peace+Alliance between S/P through V04, call
   DeclareWar(P,E,source=S), add War between P/E through V04, then directly
   add P→E Aggressor and AggressorLong. Store P's contact toward E=turn,
   then toward S=turn+16, retaining signed16. Subtract amountUnits*50 from
   S's gold and add it to P's gold, then redraw status. There is no affordability
   recheck: the minimum offer can make S's treasury negative.

After either emissary outcome and any reply, reset the session and return from
the routine. Do not continue to another enemy or execute that enemy's cleanup.

### Unpaid request

Otherwise, if strength(E)>strength(S) and either rank(P)<6 or rank(S)<6, ask
the same emissary. Decline uses max(old,turn-15). If accepted, format E's name
and the live Alliance-dependent label; show Crusade, or Jihad when P→E Alliance.
Reply=0 uses max(old,turn-14). Any nonzero reply:

1. Add Peace+Alliance between S/P, then DeclareWar(P,E,source=S).
2. Store P's contact toward S=turn+16 **before** the following War mutation.
3. Add War between P/E, directly add P→E Aggressor and AggressorLong, then
   store P's contact toward E=turn.

This branch has no gold/technology transfer or status redraw. Reset the session
and return after any emissary/reply outcome.

### Per-enemy cleanup

When neither request branch returns, directly clear S→E Fighting. This cleanup
also runs when the initial Fighting/relation/hostility/contact admission failed,
provided E was alive and distinct. In network mode send the civilization state
difference to all peers and flush with the normal 5000-unit timeout after each
such cleanup. No direct RNG is used here; emissary and grant children keep their
own ordered draws, changes and possible failures.

## 10. Conformance cases

These cases distinguish behavior at declared boundaries; native children must
still be joined for original-program conformance.

| Case | Required distinction |
| --- | --- |
| Advance score 10, no other modifiers, tribe 0 researching Iron Working | Result 12. A known direct child with both prerequisites equal subtracts only once. |
| Trireme prerequisite matches; AI with an owned region | Expansion adds 1, not 3, because the inner presence test reads the actor again. |
| Same expansion case for a human on a normal-size map | No increment; LargeMap restores +1. |
| Tribe 11 and expansionBonus=0 | Unset freeRegion cannot change the returned value; no fabricated stack input is needed. |
| Port Facility prerequisite, ship count -7 | Add -1 by truncating -7/4 toward zero. |
| Human Space Flight knowledge; all three spaceship improvement prerequisites equal a | Three independent +3 contributions. |
| Human knowledge of the fixed Bomber's exact prerequisite | Add 5, not 2+5. |
| All government scores equal and all available | Unrestricted selects Democracy; later ties win. |
| Restricted chooser draw remainder nonzero | Maximum Fundamentalism. A later Anarchy-score gate can restrict further without undoing the draw. |
| Scenario government lock while current government≥Despotism | Return before the restricted draw. |
| No government candidate available, or none scores at least -999 | Still pass default Despotism to the transition child. |
| AI science 6, tax 2, civilize 0, expansion policy blocks extra science | Science 4, tax 4, implied luxury 2. |
| Fourth turn, implied luxury 3, no collected city status | Reduce luxury to 2; a large balanced city prevents this reduction. |
| AI science expression -1 before storage | It wraps to 255 and then hits the later upper cap; it does not clamp to zero first. |
| Completed late-research knowledge chain | Science becomes zero, tax gets the remaining non-luxury share. |
| bestHumanRank=2 but civByRank[2]=7 | Government postprocessing reads civilization 2, not civilization 7. |
| Democracy score 32767 plus strategic contribution 10 | Retained score -32759. |
| Initial evaluation already done, distrust≤6, incoming declareWar=true | Preserve that flag, then execute its aggressive/mode/attitude consequences. |
| Civilization has multiple cities but only one in a region | Regional fear threshold is not passed by that one city. Two pass it. |
| AI nuclear count nonzero, target nuclear count zero | Publish only AI→target NuclearThreat at this step. |
| No Aggressor relations to third civilizations | Stance clears Mercenary bilaterally. |
| Historical scenario sets aggression at the final override | Earlier demand/requestMode are retained rather than recalculated. |
| Alliance willingness refused for Vendetta, but target is already allied from A | The later allied-target override can agree and set its reason; final attitude>75 still refuses, retaining the reason. |
| C→D Aggressor in security query | Immediate true, without regional/security comparison. |
| AI exchange with one technology each way | Draw before each valuation; first strict tie wins; grant forward then reverse with selections retained. |
| One-way allied catch-up qualifies | Set Tribute bilaterally before invoking the grant. |
| Review without a border-produced delta | Use supplied original input or an unknown-input stop. With final no-Contact, the result is zero for every incoming value. |
| First peace intrusion warning | Set delta from the scan, keep markers, and still run outer attitude adjustment. |
| Military aid candidate inside or outside bounds | Both fail the combined original bounds/city-owner gates; no transfer. |
| Accepted paid war request with sender gold 0 | Minimum payment is 50, so sender becomes -50 and partner gains 50. |
| War request returns after emissary refusal or any reply | Reset session and return before per-enemy Fighting cleanup. |

## Appendix A. Original representation and evidence distinctions

### Encodings and scalar backing

Fixed Nuclear Missile, Diplomat, Spy and Bomber identities are 45, 46, 47 and 28.
Trireme, Caravel, Frigate and Transport are 32, 33, 35 and 43. Government order
is Anarchy 0 through Democracy 6. Scenario options used here are revolution
prohibition `0001`, government lock `0010`, no technology exchange `0020`, and
historical behavior `8000` hex. AIGangUp and LargeMap are game masks `0001` and
`0004`; Bloodlust is rule mask `0080`.

Alliance reasons are ExistingAlliedTarget=1, DangerousEnemy=2, NewCommonTarget=4.
V04 owns treaty encodings, including Intrusion `20`, NuclearThreat `100`,
Fighting `200`, Aggressor `800`, Tribute `40000`, AggressorLong `80000` and
Mercenary `100000` hex. RequestMode values 0/1/2 remain independent from the
aggression/declaration flags; do not compress them into a single Boolean.

The government's selected best-human value is the rank field itself, repeatedly
used as a civilization index. The original Democracy product at `00488223`…
`004882b8` uses unsigned active-count bytes, signed subversion words, a full-width
bonus local and a final low-word store. No adjacent government score is an
extension of a wrapped destination. [F06](F06-save-load-and-file-formats.md)
owns persisted record encodings and retained payload; [F02](F02-game-state-and-invariants.md)
owns logical identity and scalar invariants.

### Irrelevant versus observable private inputs

In original advance valuation, `004bdd97` tests expansionBonus and `004bdd9b`
skips the free-region initialization at `004bdda1` when that bonus is zero.
Tribe 11 reads the unset private flag at `004be4b9`; either branch subtracts
zero or does nothing. The read is within the function's allocated private frame,
not an externally visible access/fault boundary. The result and all persistent
effects are identical. It is valid to guard that branch by nonzero expansionBonus,
without claiming the original flag was initialized. The independent reconstruction
records this exact distinction; neither a supplied random flag nor a zero-start
policy is needed for this value-independent case.

By contrast, relation review's EBP-30 delta can affect AdjustAttitude. The
primary classic preset `6715d7a0` hexadecimal is sampled replay state, not a
default. Its B3 fix and the independent reconstruction's BF-002 policy explicitly
initialize zero; that is a separate repair. The independent implementation also
tracks an unknown delta until publication: its final no-Contact store at
`00561fb6` establishes zero regardless of earlier private arithmetic, while
Contact can require supplied state or a stop before `00561fbd`. Keep prior border,
marker, query and presentation effects; do not recreate arbitrary stack bytes.

The original security query reads signed leader attack at `0055cff3` and
subtracts it from its working security score at `0055cfff`, before initializing
the region aggregates. The primary source places that read in the final
comparison. For stable valid data these are numerically equivalent, but §6
retains the original read order and earlier missing-backing boundary.

V03 documents the primary ancestry helper's always-on visited-set repair
BF-173. It is not original cycle behavior. The finite A08 harness below uses
that helper only on acyclic graphs; its success does not qualify cyclic-rule
termination or unrecovered adjacent prerequisite reads.

### Military-aid continuation is not an established gameplay path

Original `0055faab` calls the bounds thunk; the zero branch at `0055fab5`
admits the city-owner call at `0055fac8`. That query's own bounds call at
`005b8cb7` returns -1 for the same invalid coordinates. The source's reversed
admission is therefore corroborated by the original and independent reconstruction.
It is not a missing assumption that an implementation should repair silently.

For a fixture deliberately supplying inconsistent child answers, the dormant
tail additionally requires stack count>1 and source-region policy CapacityPressure,
or Expand with the captured war count zero. It scores `(signedDefense+
2*signedAttack)*maxHP`, selecting the first strict positive maximum. It scans
live ally cities ascending, requiring a present unit owner, Coastal flag,
Defend policy, no adjacent same-domain nonallied unit, and an exact city at the
selected unit's freshly read source coordinates.

The first passing destination unlinks the unit with cargo handling, decrements
C's actual-type active count, changes owner/home and clears visibility, increments
the ally's live-type count, then links at captured destination coordinates.
Counts and home retain their unsigned8 domains. Refresh/redraw destination, then
source city; send the enemy-human notice, then ally-human notice where applicable
and autoplay is off. Local observers use unit dialogs; network observers receive
the corresponding text message. In network mode send the state difference and
flush, then return. These effects are dormant-tail evidence, **not required
transfer behavior for an ordinary input**. Supplying inconsistent map results
does not prove a real original caller reaches them. Invalid record/count stores
would still be excluded bugs, never corruption to reproduce.

## Appendix B. Source traceability and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is OpenCiv2 at
`a71e8aefffe04e6f719e0e213b027b128a747711`; the selected bodies are unchanged
in OpenCiv2 comparison revision `fc2155ccdb2123c1137babab6fa39e38152278c9`.

| Behavior | Primary source and original entry |
| --- | --- |
| Advance value and expansion/AI/tribe contributions | [ai.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/ai.cpp), `ai_advance_value`, `ai_advance_expansion`, `ai_advance_ai_bonus`, `ai_advance_tribe_bonus`; original `004bdb2c`. |
| Rate/strategic score phase | [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp), tail of `civ_process_cities_turn`, original `00487a41`; F04 owns the full parent. |
| Government choice | [bigframes.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/bigframes.cpp), `ai_choose_government`, `0055f5a3`. |
| Stance and three component phases | [diplomacy.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/diplomacy.cpp), `diplo_eval_ai_stance`, `diplo_stance_continents`, `diplo_stance_other_civs`, `diplo_stance_scenario_overrides`; original `0045705e`. |
| Alliance willingness | Same source, `diplo_alliance_willing`, part of proposal controller `0045b4da`; V05 owns its other phases. |
| Security and AI exchange | Same source, `diplo_ai_feels_secure` at `0055cbd5`, `diplo_ai_swap_advances` at `0055d1e2`. |
| Military aid | Same source, `diplo_ai_military_aid` at `0055f7d1`; [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp), bounds `004087c0`, city owner `005b8ca6`. |
| Review parent/border/violation/delta | `diplo_ai_review_relations`, `diplo_review_border_incident`, `diplo_review_violators`, `diplo_review_attitude_delta`, original `00560d95`. |
| War-ally requests | `diplo_ai_seek_war_ally`, original `00562021`. |

Independent read-only evidence is `civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`: `src/contact_advance_score.cpp`,
`src/government.cpp`, `src/civ_turn.cpp`, `src/diplomacy.cpp`,
`src/diplomacy_commands.cpp`, `src/unit_exploration.cpp` and
`src/civ_diplomacy_turn.cpp`. Its accepted reconstruction campaign is not resumed
or modified here. Original MGE `Civ2/civ2.exe` SHA-256 is
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Bounded instruction checks resolve the irrelevant advance temporary, security
trait-read order, military-aid contradiction and government choice. No fresh
original executable gameplay or saved Ghidra annotation cycle is claimed.

The historical bounded Linux harness compiled **31 complete source
bodies**: all 18 A08-owned bodies, the F04 city-turn parent, V03's two primary
ancestry bodies, V04's seven attitude/treaty/hostility/category bodies, V05's
patience body and W01's bounds/city-owner bodies. **47 assertions pass** in
optimized and AddressSanitizer/UndefinedBehaviorSanitizer builds, using wrapping
arithmetic and disabled strict-alias optimization; leak detection is disabled
under sandbox tracing. Source checks verify extracted bodies, thirteen record
definitions, shared state definitions, enums, clean reference checkouts and the
original executable hash.

The fixture joins actual valuation within exchange, actual rate/score tails in
the city parent, actual stance children, actual border/review/delta children,
actual treaty/attitude primitives and the contradictory aid query pair. It
supplies map/stack/nearest-unit, knowledge/wonder/race, government availability
and transition, city-turn results, grant, emissary/reply, movement/revelation,
text/presentation and network adapters. Those child answers are explicit inputs;
observing an invocation does not prove the real child effects or normal native
lifetime. No aid transfer is forced through fabricated bounds answers.

Declared harness adaptations guard the irrelevant zero-bonus private read,
move the security trait read to its original position, supply review's incoming
delta in place of a replay constant, and use host character arrays for message
keys. The primary acyclic ancestry helper remains the documented reconstruction
repair; cyclic paths are not sampled. The fixture does not certify every accepted
input, arbitrary reentrancy, absent original backing or excluded-write paths.

Remaining integration is actual V02/V03/V04/V05/V07/C08/U01/W01/W03 children,
complete A01/F04 consecutive turns, original runtime comparison, populated
P04/P07 callbacks and N01/N02/N05 receipt/authority. X02 must preserve full
state, random and message traces across these joins and failures. Link/anchor,
index and whitespace checks accompany this document. These checks do not
establish complete original-program conformance.
