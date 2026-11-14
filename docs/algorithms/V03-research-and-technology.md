# V03 — Research and technology

**Required profile:** [X01 fresh-game isolation and bug repairs](X01-classic-compatibility-contract.md#fresh-game-isolation-and-supported-bug-repairs)
and [behaviorally significant ordering](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
override conflicting procedures and examples below. Retention applies within a
game or to explicit imported inputs, not across new games. Confirmed original-bug
stack/backing/stop cases are optional diagnostics; use documented safe repairs.
Only real effects and callbacks impose ordering; test adapters cannot invent them.
Unspecified safe continuations remain explicit gaps, not required arbitrary bytes.

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. This specifies knowledge queries, prerequisite traversal,
research availability and selection, research goals, cost, beaker accumulation,
completion, grants and captured technology, first discoveries, and the immediate
consequences of acquiring an advance. It includes the decisions and state changes
of the research interface. P04/P07 and
[P08](P08-civilopedia-and-technology-tree.md) own common dialog, report and encyclopedia
presentation services; N02 owns packet transport and session authority.

[F03](F03-rules-and-configuration.md) owns rule ingestion and derived prerequisite
fixups; this document consumes the resulting live table. [F06](F06-save-load-and-file-formats.md)
owns persisted knowledge, research and scenario fields. [C03](C03-city-economy-and-corruption.md)
produces science, and [F04](F04-turn-sequencing.md) orders its collection.
[C07](C07-trade-and-commodities.md) owns caravan beaker credit, which does not
invoke this document's ordinary accumulation/completion procedure. [C08](C08-buildings-and-wonders.md)
owns active wonders, building presence and Leonardo upgrades; [V02](V02-governments-and-tax-rates.md)
owns actual government changes.
[A08](A08-ai-research-government-and-diplomacy.md#2-advance-valuation) supplies advance
valuations.
[V05](V05-diplomatic-negotiation.md#3-technology-exchanges) owns negotiation;
[V06](V06-espionage-and-bribery.md#5-technology-theft) owns spy mission selection,
detection and effects around the acquisition operations here.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

Pure knowledge predicates may use equivalent evaluation on stable valid inputs;
prerequisite walks, grants, completion and recursive effects retain their live
reads, short-circuit failure boundaries, callback order and termination behavior.

## 1. State and accepted domains

Each civilization supplies its knowledge membership, acquisition source per
advance, current research target, research progress, ordinary advance counter,
research-cost counter, future-technology counter, FreeAdvance flag, government,
treasury and directed treaties. The game supplies a separate known-by set and
first-discoverer value for each advance, human/alive sets, local human, turn,
difficulty, scenario/map flags, rule-defined advance count and best-human-rank
field. The two knowledge summaries can differ; these procedures do not rebuild
one from the other. Advance count is a retained counter, **not** a derived
knowledge cardinality. The additional research-cost counter is consumed as
stored; acquisition below does not increment it.

Rules provide 100 advance identities, each with two prerequisites, enabled state,
name and presentation category/epoch, plus unit and improvement prerequisites,
wonder expiry identities and technology paradigms. Other inputs include autoplay,
Tutorial, Instant Advice and Pedia Advances options, diplomacy-session state,
scenario government-change prohibition, current city-window selection, random
streams, dialog replies and callbacks, and the completion notice mode.

Nominal inputs have backed civilizations 0…7, advances 0…99, signed prerequisite
sentinels, ordinary current research −1 or a table identity, and rule/scenario
state admitted by F03/F06. Future Technology is a fixed advance identity. A rule
file may contain an enabled prerequisite cycle: ingestion and a later traversal's
termination are separate contracts. In particular, do not reject every authored
cyclic scenario merely because one recursive UI query could fail to terminate.

Use [F01](F01-determinism-and-arithmetic.md) for all arithmetic. Research target
and progress retain signed16; the three civilization advance counters retain
unsigned8, as do acquisition source and first-discoverer values. Prerequisite
references are interpreted signed8. Rule-defined advance count and scenario
technology paradigm are signed16; the cosmic paradigm and difficulty are
unsigned8. Unless stated otherwise, intermediate expressions have F01 reference results,
division truncates toward zero, and remainder has the dividend's sign. These
views/conversions preserve required values; their widths do not prescribe
internal types or redundant casts on bounded identities.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

Nominal positive beakers can wrap research progress into a negative value.
Duplicate grants can wrap advance count. These are in-bounds scalar behaviors.
A stricter importer can reject a documented wider input domain; it must not
silently replace accepted arithmetic with saturating or unlimited arithmetic.

Out-of-bounds writes, including negative-identity grant writes, excess goal-list
writes and text-buffer corruption, are **excluded bugs** under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs).
Reject the invalid input or stop before the invalid write with earlier valid
effects retained. Do not reproduce corruption through named adjacent fields.
Unrecovered or extra reads instead require explicitly supplied evidence/backing
or a bounded stop; neither a zero value nor a successful fallback is implicit.
Nonterminating prerequisite walks may be diagnosed and stopped safely. A
cycle-breaking result is a separately declared repair, not an original result.

## 2. Shared knowledge and availability

`knows(c,a)` evaluates these branches in order:

```text
if a == -2: return false
if a < 0: return true
if a == FutureTechnology: return false
if a >= 100: return false
if c < 1: return false
return c's stored knowledge membership for a
```

All negative identities other than −2 therefore return true even for an invalid
civilization, without a civilization read. Future Technology never becomes true
through this query, regardless of its counter or stored membership. Nonnegative
ordinary knowledge for civilization0 is false. There is no alive, human, enabled
or prerequisite test. An invalid positive civilization reaching the last branch
is a read-domain boundary, not automatically false.

`canResearch(c,a)` reads enabled first. If nonzero, require, in order:
not `knows(c,a)`, `knows(c,prerequisite0)`, then `knows(c,prerequisite1)`.
Short-circuit on failure. It does not first bounds-check a, reject Future
Technology, or filter by difficulty, modulo class, current research, scenario,
or human status. Callers apply their own additional restrictions below.

A signed prerequisite −2 blocks this query; other negative prerequisites satisfy
it. Disabled prerequisites already rewritten by F03 are consumed as rewritten;
this query does not perform a second disabled-chain substitution.

## 3. Prerequisite walks

### Ancestry

`descendsFrom(a,target)` is the original ordered recursion:

```text
if a < 0: return false
if a == target: return true
if descendsFrom(prerequisite0[a], target): return true
return descendsFrom(prerequisite1[a], target)
```

Negative self-equality is false. Nonnegative self-equality succeeds before any
record read, including an out-of-table equal pair. Otherwise each signed
prerequisite is read at its turn. The original has no visited set, depth bound,
deduplication or table-identity guard. The first branch can recurse indefinitely
before a matching second branch is reached. Shared ancestors can be revisited.
A bounded stop is permissible at this failure boundary; silently answering false
there, or continuing to the second branch, is an additional repaired algorithm.

The primary reconstruction unconditionally replaces this with a visited-set
search. That repair is **not** the original contract. Appendix A identifies the
independent original traversal and the authored-cycle evidence.

### Count used by the science report

This is a different operation, despite its historical “depth” name. It mutates
a shared count and counts visited prerequisite edges, not distinct technologies
or maximum graph depth:

```text
countPrerequisites(a):
    if sharedCount < 101:
        for slot in [0,1]:
            p = signed prerequisite[a,slot]
            if p > 0:
                sharedCount += 1
                countPrerequisites(p)
```

Identity0 is ignored; positive repeated ancestors are counted repeatedly.
The bound is tested only on entry, not again before each sibling increment.
Pending siblings can raise the count above101: a node with both prerequisites
pointing to itself produces202 from count0. The science-ranking caller clears
the count, runs this walk, then adds one for the selected advance. [P07’s herald display](P07-advisors-reports-and-history.md#science-ranking-and-cache) owns the
report ranking/cache use. This entry-bound walk must not be substituted for
ancestry or for F05's distinct setup traversal.

## 4. Research recommendation

`pickResearch(c)` begins with best score−1, selected advance−1, admitted count0.
Scan identities0…99 in order. Require `canResearch`; require at least one
prerequisite unequal to −2; then admit if any of these holds:

- c is currently AI;
- this is the first admitted candidate;
- difficulty is Chieftain;
- `s32(a−unsignedAdvanceCount(c)) % 3 != 0`.

Increment admitted count before scoring. Re-read human membership for the
scoring branch; a callback need not preserve the earlier classification.
For AI, obtain A08's value. If value equals1 or `s32(value−1)<0`, score0 without
a primary draw. Otherwise draw one primary random integer R, call A08 again,
and score `R % secondValue`. Do not reuse the first valuation. A zero second
value is an arithmetic failure boundary, not a zero score. For ordinary values
the no-draw test means value<=1; full signed32 minimum is an exception because
subtracting one wraps to maximum and takes the draw branch.

For a human, draw primary R **before** A08 valuation, then score
`s32(R%3 + value−1)`. Replace the current best only for a strictly greater score:
first ties win. All scores<=−1 can leave selection−1 even after admitting
candidates. Return the selected full identity; no research target changes here.

Scenario and forced-dialog flags do not bypass this recommendation's modulo
filter. They do bypass the player's list filter below. Recommendation and
available displayed choices are therefore not the same set.

## 5. Research selection interface

`chooseResearch(c,forced)` constructs a choice dialog, then an advice dialog;
normal cleanup is advice then choice. Call `pickResearch` exactly once before
any interaction gate and retain its result across all Help/goal iterations.
Enter the interface if forced is nonzero, or if turn is nonzero, c is currently
human and autoplay is off. Otherwise store the recommendation as signed16
research target and return its full value.

Each interface iteration proceeds as follows:

1. Start with the retained recommendation. If Instant Advice is enabled and
   forced is zero, show the advice described below.
2. Query Invention knowledge and select the pre-/post-Invention research label.
   Load Research; Instant Advice requests its alternate position even when
   forced suppressed advice creation. Install advance option drawing/indent.
3. Scan advances0…99, requiring enabled, unknown, both prerequisites known and
   at least one prerequisite unequal to −2. The first admitted option is always
   shown. Later options are shown on Chieftain, when forced, in a scenario, or
   when `(a−unsignedAdvanceCount(c))%3 != 0`. Preserve that ordered short circuit.
   Option identity is a; label Future Technology with the current counter+1.
4. If there are no options, finish with the retained recommendation, without
   running the choice modal or the following advice-close step.
5. Run the choice modal with the resource's default selection and delay300.
   If Instant Advice is currently enabled, close the advice receiver and flush
   input, even if forced prevented it from being shown.
6. Extra-result2 invokes `researchGoal(c,0,0)` and rebuilds this interface.
   Otherwise capture the selected identity. Extra-result0 accepts it; any other
   extra result opens that identity's encyclopedia page and rebuilds.
7. On acceptance, store signed16 selected identity in research target and return
   the full identity. No availability recheck occurs after the modal callback.

A negative accepted selection can store “none.” Do not conflate the option result
with the extra-button result. A missing recommendation used by Instant Advice
can attempt an advance-name read before the no-options test; supply the read
input or stop rather than claiming that the original skipped advice safely.

### Advice contents

Set the recommendation name, adding future counter+1 for Future Technology.
Collect at most five unlocks, scanning unit types0…61 first, then improvements
1…66, whose prerequisite equals the recommendation. Continue the fixed scans
but append only while fewer than five items exist. If any unlock was found, use
the units/buildings advice form. Otherwise scan enabled advances0…99 with either
prerequisite equal to the recommendation, again retaining the first five; use
the subsequent-advances form when nonempty, or the no-unlocks form when empty.

Load the corresponding Science0/1/2 advice resource and category/epoch image.
List selected names in order, using a space separator for two items, commas
for longer lists, the conjunction before the last item, and a final period.
Capture the timer after preparing the contents and invoke the advice modal with
that captured time minus300. P04 owns the common modal timing/event semantics;
this is not a new research delay formula or a fresh recommendation draw.

## 6. Research-goal interface

`researchGoal(c,excludeCurrent,otherCiv)` is informational. It neither stores a
persistent goal nor selects research nor grants an advance. Construct the goal
dialog; initial category is Advances. Each iteration loads Research Goal and
rebuilds the selected category in ascending identity order:

| Category | Included options |
| --- | --- |
| Advances0…99 | Enabled and unknown. If excludeCurrent is nonzero, also require identity different from current research and at least one prerequisite unequal to −2. With excludeCurrent zero, those two additional tests are absent. |
| Units0…61 | Signed prerequisite>=0 and unknown. If excludeCurrent is nonzero, prerequisite must differ from current research. |
| Improvements0…66 | Same prerequisite tests as units. |

Unit/improvement options do not test the prerequisite advance's enabled flag
or current research availability. Show their own name with the prerequisite
name in parentheses. Advance options show future counter+1 where applicable.
Use two columns for the advance category. Outside a diplomacy session, install
the category's custom icons and indentation; a session suppresses those hooks.

If no options exist, return before adding category buttons or opening the modal.
Otherwise append buttons for the other two categories in ascending category
order. The resource supplies Help; extra results2 and3 map to the two added
buttons, respectively. Run the modal. A negative selected identity returns
before interpreting the extra result. Extra1 requests Help only outside a
diplomacy session, then rebuilds the same category. Extra2/3 changes category
and rebuilds. Any other extra result accepts the selected option.

Advance Help opens that advance's encyclopedia. Unit/improvement Help first
captures the item and its prerequisite, then shows a two-option Help On dialog:
0 opens the item's page, 1 opens the prerequisite's page; other replies do
neither. The outer goal list is rebuilt afterward. A missing resource Help
button can invalidate this button mapping; it does not imply a new category
convention. Invalid selections and resulting writes remain excluded bugs.

Convert an accepted unit/improvement option to its signed prerequisite identity.
Let that be selectedGoal. Start path count0 and admitted-candidate count0. Scan
advances0…99. First require enabled and unknown. Then:

- If otherCiv is zero, require both prerequisites known and, when excludeCurrent
  is nonzero, identity different from current research. Unless this is the first
  admitted candidate, difficulty is Chieftain or Scenario is set, require
  `s32(a−s32(unsignedAdvanceCount(c)+excludeCurrent)) % 3 != 0`.
- If otherCiv is nonzero, require `knows(otherCiv,a)` instead. Do not apply those
  prerequisite, current-target or modulo tests.

Require at least one prerequisite unequal to −2. Increment admitted count
**before** testing `descendsFrom(selectedGoal,a)`. Append a to the path only
when that ordered ancestry query succeeds. A candidate that is not on the path
still consumes the first-candidate exemption. The full excludeCurrent argument
participates in the arithmetic; it is not normalized to a boolean there.

Set the selected-goal name argument. For an empty path show Research None or
Steal None according to otherCiv. Otherwise show Research These or Steal These,
with path names in scan order, commas for a list longer than two, conjunction
before the final item and a final period; then run its modal. No random draw is
made directly by this operation. Child UI callbacks or encyclopedia operations
remain separate services.

The original local path list has room for99 entries. A hundredth append is an
excluded write, even if all preceding identities were valid. A new implementation
may stop with a diagnostic or support a larger safe list as a declared extension;
it must not reproduce the adjacent local-variable overwrite.

## 7. Research cost

`researchCost(c)` does not depend on the particular current target and makes no
random draw. Define `sum(c)` as the sum of the civilization's unsigned ordinary
and research-cost counters. Capture `N=sum(c)`; if N<2, replace N with1. Keep
this N through the final human floor and multiplication.

```text
M = clamp(unsigned difficulty, 0, 4)
if c is AI: M = 14-M
else:       M = 2*M+6

if not Scenario OR scenarioYearIncrement == 0:
    if N < sum(civilization indexed by bestHumanRank):
        if difficulty != Chieftain: M -= 1
        if difficulty == Deity AND N+4 < fresh sum(that same indexed slot)
                               AND turn > 150:
            M -= 1
    else:
        M += trunc((N - fresh sum(that same indexed slot))/3)
    penalty = 0
    if N > 19: penalty = clamp(N-trunc(turn/8), 0, 6)
    M += penalty

if not Scenario:
    if cosmicParadigm != 10: M = trunc(s32(unsignedCosmicParadigm*M)/10)
else:
    if scenarioParadigm != 10: M = trunc(s32(signedScenarioParadigm*M)/10)

extra = arithmeticShiftRight(s32(M*3), 2)
if live sum(c) < 20:
    extra = trunc(s32(fresh sum(c)*extra)/20)
M += extra

if signedRuleAdvanceCount > 67:
    M = trunc(s32(M*67)/live signedRuleAdvanceCount)
if LargeMap: M = trunc(s32(M*5)/4)
if SmallMap: M = trunc(s32(M*4)/5)
if c is currently human AND M < s32(11-N): M = s32(11-N)
C = s32(M*N)
if C < 1 OR C > 32000: return 32000
return C
```

Apply signed32 retention to additions/subtractions as well as the displayed
products. The best-human **rank itself indexes a civilization slot**: do not
look up that rank in the civilization power-order array. This original quirk
can select a different technology count than the strongest human civilization.
The leader comparison is independent of whether c is human. Scenario with a
nonzero year increment omits both the leader and turn adjustments, but still
uses its scenario paradigm and all later adjustments.

The extra's shift rounds negative values downward; the later divisions truncate
toward zero. The second map flag is tested after the first, so both flags apply
sequentially if both are accepted. The human floor uses captured N, while the
extra scaling reads live counts. A nonpositive computed cost maps to32000,
**not** to1. No global counter or knowledge membership is recomputed here.

Examples with ordinary paradigm10, rule count67 and no map-size flag:

| Conditions | Cost |
| --- | --- |
| Human Chieftain, own/leader count0 | 10 |
| Human Chieftain, own/leader count2 | 18 |
| Human Chieftain, own/leader count10 | 80 |
| Human Chieftain, own/leader count20, turn1 | 420 |
| Same count20, both LargeMap and SmallMap | 400 |
| Same count20, rule advance count100 | 280 |
| Human Deity, own count10, indexed leader20, turn150 then151 | 170 then160 |
| AI Deity, own count10, indexed leader20, turn151 | 110 |
| AI scenario, nonzero year increment and paradigm0 | 32000 |

## 8. Adding research points

`addResearch(c,points)` returns immediately for points<=0, before any
civilization-indexed read. Otherwise:

1. Add the signed16 retention of points to research progress, retaining signed16.
2. If the live research target is nonnegative, capture signed progress, then
   obtain `researchCost(c)`. If cost<=captured progress, call `complete(c,0)`.
3. Still within that initial nonnegative-target branch, test the live FreeAdvance
   flag. If set, clear it **before** calling `complete(c,0)` once more. This test
   runs even if the threshold did not complete anything. It is not a loop.
4. If live progress is nonzero and live target is negative, show First Civilization
   tutorial when Tutorial is enabled, advance count<2 and c is human; then call
   ordinary `chooseResearch(c,0)`. Do not repeat those gates after the tutorial.

An initial absent target accumulates points and can request a choice, but does
not complete that new target immediately. Zero/negative points neither consume
FreeAdvance nor request research. Philosophy's grant can set FreeAdvance during
the first completion, allowing the second completion in the same accumulation.
Overflow that makes progress negative prevents an ordinary positive threshold
from succeeding. C07's direct caravan credit is distinct: it adds progress with
retention but does not enter this procedure.

## 9. Completing research

`complete(c,source)` constructs its notice receiver and captures current target.
If the target is negative, invoke ordinary research choice and reload target.
If it remains negative, return that value with normal cleanup and no grant.
Otherwise retain the completed identity for the whole procedure:

1. Unless completion notice mode equals2, set progress0. Mode2 preserves the
   entire current progress; it is not a subtraction of the research cost.
2. Set target−1; invoke `giveAdvance(c,completed,source,skipPedia=0,fromNetwork=0)`.
3. Re-read turn and human membership. If turn0 or c is AI, capture the city
   window's selected city, then call [C06's civilization production reset](C06-city-production-and-purchasing.md#ai-wonder-changes)
   for all regions. Afterward, if turn is nonzero and the captured city is still
   in range, live and owned by the full c, invoke the C03 mood query/recalculation
   entry `cityRecalc(city,0)`. The production reset can alter that eligibility.
4. Otherwise, only when c is the local human and autoplay is off, execute the
   local completion notices below. Nonlocal humans and autoplay skip this branch.
5. Return the captured completed identity, regardless of the newly chosen target.

The authority-forwarding branch of `giveAdvance` does not cause this caller to
wait for a remote acknowledgment or skip its remaining steps.

### Local completion notices

Set civilization adjective, the Invention-dependent label and completed name.
For Future Technology, append the **current counter after the grant**, without
adding one. Show Civilization Advance with category/epoch image and zoom8.
If the live Pedia Advances option is enabled, open the advance's encyclopedia.
If completed is Philosophy, FreeAdvance is still set, c is still local and
autoplay remains off, run the Golden Age notice described below.

Then, in this order, independently test and show newly enabled abilities:

1. Fixed Engineers type's prerequisite equals completed: New Transform.
2. Completed is Construction: New Fortress.
3. Airport prerequisite equals completed: New Airlift.
4. Completed is Railroad: New Railroad.
5. Supermarket prerequisite equals completed: New Farmland, with both images.
6. Fixed Paratroopers type's prerequisite equals completed: New Paradrop, with
   the cosmic paradrop range interpreted unsigned8.

Finally, if c is still human and turn remains nonzero, choose research and then
run government/tutorial prompts for the **completed** advance. Earlier callback
changes are visible. Do not re-gate every ability notice on local status or
human membership; the enclosing branch was already entered. The final choice
and prompts do not require a successful nonnegative new selection.

## 10. Granting an advance

`giveAdvance(c,a,source,skipPedia,fromNetwork)` constructs its notice receiver
before testing authority. Execute locally if any of these holds, in order:
mode<3, fromNetwork nonzero, c absent from the alive set, c currently AI, or c
equals the local human. Otherwise send the recipient's peer a Set Civilization
advance message containing c,a,source,skipPedia and the remaining zero arguments,
then clean up and return. Do not mutate research, wait, or grant locally as a
fallback. The inactive-slot exception is used by [V01 creation/catch-up](V01-civilization-lifecycle-and-civil-war.md).

For a local grant, start firstDiscovery=false and proceed in order:

1. If current research target equals the **full a**, set it−1.
2. If a is Future Technology or greater than99, increment future counter with
   unsigned8 retention. Otherwise add a to c's stored knowledge, store source
   with unsigned8 retention, capture whether a's global known-by set was empty,
   and add c to that global set. This is not an idempotent grant guard.
3. If turn0, send the state diff when networked, clean up and finish immediately.
   Do not increment ordinary advance count, record first discoverer, set
   Philosophy's free advance, sell Barracks, upgrade units or propagate the
   Great Library in this path.
4. Increment ordinary advance count with unsigned8 retention, even on a duplicate
   grant or a future-technology grant.
5. Perform the announcement/emissary alternatives below.
6. If c is currently human, equals the local human, source!=0 and autoplay is
   off, optionally open the advance's encyclopedia when outside a diplomacy
   session, Pedia Advances is enabled and skipPedia==0. Then always run
   government/tutorial prompts for a within this gate. skipPedia suppresses only
   that encyclopedia call, not the prompts.
7. Starting at fixed Mobile Warfare, repeatedly follow prerequisite0 while the
   current identity is nonnegative and disabled. This is an ordered live walk
   with no original cycle guard. If a is Gunpowder or equals the resulting
   identity, execute the Barracks sale below. The walk occurs even for Gunpowder.
8. If firstDiscovery was captured true, execute first-discovery effects below.
9. Invoke C08's complete Leonardo upgrade for c, regardless of which advance was
   granted and regardless of whether it was already known.
10. Query the active owner of Great Library. If owner>0 and does not know a,
    count `knows(slot,a)` across slots1…7 in order, with **no alive filter**.
    If more than one knows it, recursively call
    `giveAdvance(owner,a,source=-2,skipPedia=0,fromNetwork=0)` and let it finish.
11. If currently networked, send the state diff, then clean up normally.

Granting an already-known ordinary advance overwrites its source, increments
count on a nonzero turn, and runs the later applicable effects. firstDiscovery
comes from the pre-update **global** known-by set, not the recipient's prior
knowledge. Changes during notices or child calls can affect every later live
query. Nested Great Library grants perform their own authority checks and
synchronization before the outer grant's final sync.

Future Technology and identities>=100 take the counter path, and their knowledge
query is always false. They do not set firstDiscovery. A supplied identity>=100
can nevertheless reach later name/rule reads; the counter path does not prove
that every presentation continuation is defined. A negative a enters the
ordinary storage path in the original and can write outside intended knowledge
or source storage: it is an excluded bug, after the valid target-clearing prefix.

### Announcement versus emissary

With autoplay off, announce if any of these conditions holds:

- local human equals c and source<0;
- local human equals c, source equals c, and Pedia Advances is disabled;
- local human differs from c, c is alive, and an observer test succeeds in this
  order: local→c Embassy, local active United Nations, local active Marco Polo,
  local→c Alliance, reveal map. The observer tests precede the alive test.

The announcement says that c discovered a for source0, or acquired it otherwise.
For nonzero source different from c, add a source name: Great Library for any
negative source, otherwise that civilization's plural name. The language-specific
acquisition/from wording follows P04's localized resources. Outside a diplomacy
session include the advance category/epoch image. If c differs from local human
or the local-human slot is not in the human set, append enabled-by-this-advance
names in this order: all improvements0…66, then unit types0…61 whose prerequisite
equals a. These lists do not independently test availability. Show the modal.

Only if the announcement branch was not taken, test the emissary alternative:
autoplay off, c AI, firstDiscovery true, then secondary `randomRange(0,2)==0`.
That secondary draw occurs **before** the following treaty/wonder gates:
c→local Contact present; c→local Alliance absent; local→c Embassy absent;
local United Nations absent; local Marco Polo absent. If all pass, ask whether
to receive c's emissary using the two absent-topic arguments. A nonzero answer
sets c's plural name and advance name and shows Gape. Reset the diplomacy session
after either answer. A refusal still resets it; a failed earlier gate does not.

### Barracks sale

Scan live cities by ascending slot and live upper bound. For cities whose signed
owner equals unsigned8(c), query Barracks and count existing ones, then clear
Barracks **even when it was absent**. If count0, finish. Otherwise compute
`value=s32(unsignedBarracksCost * unsignedShieldRows * count)`.
If c is currently human, show the Barracks notice with advance and value,
including its images outside a diplomacy session. This helper has no autoplay
gate. Then add value to c's live treasury with signed32 retention; if c equals
the local human, refresh status. No SoldThisTurn flag, ordinary sell command,
stock change or per-city refund is implied. The notice occurs before credit.

### First discovery and Golden Age

For Philosophy, set FreeAdvance. With autoplay off, immediately show Golden Age
if local human differs from c or source!=0. The local source0 research case
instead gets its Golden Age from `complete`, after its completion notice.
Then update `firstDiscoverer[a]` by bitwise OR with unsigned8(c), retaining
unsigned8. Do not replace the prior value or infer it from the known-by set.

When autoplay is off, scan wonders0…27. For each with expiry identity a and raw
location>=0, read that city's owner; if the owner is currently human, set advance
and wonder names and prepare End Wonder. Outside a diplomacy session add the
advance and wonder images. If networked, broadcast that notice before the modal.
This tests raw location and owner, **not** active wonder status or city live token.
Invalid city/name reads require their supplied domain or a bounded stop.

Golden Age sets c's adjective, then scans live own cities by ascending slot and
live bound. Start selected city−1 and best score0. Weight is signed city size,
doubled if the city has a Palace. For weight<=1, score1 with no random draw;
otherwise draw primary R and score `R%weight+1`. Replace for score>=best:
**last ties win**. If a city was selected, set its name, broadcast Golden Age
when networked, and show it with Philosophy's advance image. With no eligible
city, no notice is shown. This selection does not itself create happiness,
food, production or a civilization-wide golden-age state.

## 11. Government and tutorial prompts

The caller supplies the human/local/autoplay gates; this helper does not add
universal ones. For Monarchy, only a current Despotism shows Auto Monarchy.
Reply1 shows Start Revolution and invokes V02's direct setter with Anarchy.
Other replies do not change government.

For Republic, Democracy, Communism or Fundamentalism:

1. If Tutorial is enabled, the advance is Republic and current government is
   below Communism, show the Republic tutorial and return immediately.
2. Select the new government's portrait. Return if Republic, Communism or
   Fundamentalism is already the current government.
3. Unless current government is Democracy or Anarchy, show Auto Revolution
   using advance and current-government names. Reply1 shows Start Revolution
   and calls V02's direct Anarchy setter. These reply semantics differ from
   V02's player's Revolution command, whose accepting reply is zero.

After this advance-specific handling, if Tutorial is currently enabled, show
the corresponding tutorial for Railroad, Refrigeration, Trade, Seafaring or
Writing, tested in that order. Early returns above skip this tail. The current
government can change during notices; do not precompute all gates or replace
the direct setter with the announced-change wrapper.

## 12. Captured technology

`takeTechnology(c,victim)` is shared by city capture and applicable diplomatic/
mission callers. It does not own their eligibility, probability, costs or incident
logic. Start best identity−1 and best valuation−999, construct the dialog, then
return if c or victim is zero. Capture interactive as localHuman==c and autoplay
off; human membership is **not** part of that captured flag.

Each selection iteration:

1. If interactive, load Take Civilization. Scan advances0…99, requiring unknown
   to c and known to victim. If Scenario and No Government Change are both set,
   exclude the five government-enabling advances. Require at least one
   prerequisite unequal to −2; do not require enabled or known prerequisites.
2. If interactive, append the option name. Call A08 valuation for each eligible
   advance. Replace for value>=bestValue: **last ties win**. Best identity/value
   persist across Help rebuilds; they are not reset each iteration.
3. If best remains negative, finish. Use best directly if c is currently AI.
   For a human, show the modal even if interactive was false. Extra0 accepts the
   returned identity. Any other extra result runs `researchGoal(c,0,victim)` and
   rebuilds the list.

The human choice is not revalidated against the rebuilt table. A malformed
negative accepted choice can reach invalid name reads and grant writes; treat
those boundaries as described in section1, not as a successful no-op.

If victim is human, set the taker's plural name and advance name. Then recheck
autoplay off and victim human. If victim is local, show Took Civilization with
the advance; otherwise, when networked, send that notice to victim's peer.
Grant the selected advance with source=victim, skipPedia0 and fromNetwork0.
If c is now AI, invoke C06's civilization production reset for all regions.
No direct random draw occurs here beyond the A08/grant/goal children. A grant
forwarded to a remote authority does not defer this caller's final reset.

## 13. Consumer contracts and effect ownership

| Caller/effect | Required join |
| --- | --- |
| F04 ordinary science collection | Add research points at F04's existing phase boundary; zero does not trigger selection or consume a free advance. |
| C07 caravan payment | Query cost for the payment cap; credit progress directly. Do not invoke ordinary accumulation merely because a threshold was crossed. |
| C06 Darwin's Voyage | Two sequential completion calls. The second observes the first call's choice and effects; no generic “grant two arbitrary advances.” |
| V01 catch-up and F05 starting grants | Preserve caller source/network arguments and turn/alive state. Turn0 and inactive-recipient gates matter; creation may later overwrite its counter. |
| C09 capture | Use captured-technology selection at C09's declared point; its city/unit/Palace consequences stay in C09. |
| [V05 negotiation](V05-diplomatic-negotiation.md#3-technology-exchanges) and [V06 theft](V06-espionage-and-bribery.md#5-technology-theft) | Own eligibility/selection and source arguments; use the shared grant or captured-selection operation actually called, not a universal exchange algorithm. |
| C08 expiry and Leonardo | Read this document's knowledge/global discovery state at C08's specified point. Grant invokes real upgrades; grants do not eagerly cache all effects into cities. |
| V02 government availability and rate UI | Consume shared knowledge and cost queries. Prompt acceptance invokes the direct government setter, retaining its own recalculation/rate effects. |
| A08 valuation and research policy | Supply ordered valuations, including the second AI picker call. Do not substitute a single cached valuation for every use. |
| F06 load, [S02 events](S02-event-language-and-execution.md#changemoney-makeaggression-destruction-and-technology), S03 editor and N02 receipt | Preserve their direct stores versus grant calls and published state. A copied knowledge vector does not imply grant side effects. |

Only the effects explicitly ordered above are immediate acquisition work.
Terrain, movement, production eligibility, science modifiers, happiness and
other advance consequences remain owned by their consumers and can become
observable at their next query/recalculation. Learning an obsoleting advance
does not itself delete every affected wonder/building or retarget every unit.

## 14. Discriminating conformance cases

| Inputs | Required observation |
| --- | --- |
| Knowledge query with invalid c and a−1/−2/−3/89/100. | True/false/true/false/false before a civilization record read. |
| Ordinary stored knowledge in an inactive civilization. | True; no alive gate. |
| Ancestry(−1,−1), ancestry(100,100). | False, then true, without rule reads. |
| First prerequisite loops; second equals target. | Original never reaches the second branch; a bounded diagnostic is distinct from a repaired true result. |
| Count walk with both prerequisites pointing to self. |202 from initial0, not101 or a deduplicated count. |
| AI picker values<=1 for two candidates. | No primary draws, first tie selected. |
| AI picker first valuation7, R5, second valuation2. | Score1, with two valuation calls around one draw. |
| Human picker, count0, difficulty above Chieftain, otherwise eligible0…3. |0 is first-admitted;1/2 admitted;3 excluded. |
| Human picker gives every candidate score<=−1. | Recommendation remains−1. |
| Goal's first available candidate is not an ancestor. | It still consumes the first-candidate modulo exemption. |
| Research cost uses bestHumanRank2 while powerOrder[2] names another civ. | Compare against civilization2. |
| Positive10 points added to progress32760. | Progress−32766; ordinary threshold does not complete. |
| No target, positive100 points, choice selects8. | Target8 and progress100; no immediate completion. |
| Target exists, FreeAdvance set, added points below cost. | Clear flag and complete once anyway. Zero points leave the flag untouched. |
| Threshold completes first Philosophy. | Grant sets free flag; accumulation clears it and performs one more completion. |
| Completion mode2, progress100. | Preserve100 rather than clear or subtract cost. |
| Turn0 ordinary grant matching current target. | Clear target, update knowledge/source/global set, sync if networked; skip count and later effects. |
| Duplicate grant at nonzero turn. | Increment count again, replace source, run Leonardo again; firstDiscovery depends on global pre-state. |
| Advance counter255 then grant. | Counter0. |
| Future counter255 then Future Technology then identity100. | Counter0 then1; ordinary knowledge query remains false. |
| Remote alive human recipient, fromNetwork0. | Send once without clearing target or changing knowledge; inactive recipient instead executes locally. |
| AI first discovery with no Contact. | Secondary draw still occurs before the failed Contact gate. |
| Emissary admitted but refused. | Diplomacy session reset still occurs. |
| Pedia option enabled, skipPedia1, local human nonzero source. | No grant encyclopedia call; government/tutorial prompts still run. |
| Barracks present in one of two own cities, autoplay on. | Clear both own Barracks flags; human notice precedes treasury credit. |
| First discoverer value4, new discoverer1. | Store5 by OR, not1. |
| Two size1 own cities without Palace. | Golden Age chooses the later city with no draw. |
| Expiring wonder raw city points to an inactive city owned by a human. | Notice still qualifies; no city live-token test. |
| Two other knowledge holders including an inactive slot. | Great Library count includes both; recursive grant/sync precede outer sync. |
| Capture candidates disabled or with unknown prerequisites. | Still eligible if the explicit capture filters pass; last valuation tie wins. |
| Negative local grant or hundredth goal-path append. | Stop before invalid storage; no required corruption result. |

## Appendix A — Sources, representations and original defects

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary pin: OpenCiv2 `a71e8aefffe04e6f719e0e213b027b128a747711`, compared with
OpenCiv2 revision `fc2155ccdb2123c1137babab6fa39e38152278c9`.
Sources: [civilization algorithms](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp),
[typed retained state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h),
[scenario fields](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/game.h),
[state initialization](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/gamedata.cpp),
[identities and flags](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h),
and [science-report consumer](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/diplomacy.cpp).

| Entry | Original address / boundary |
| --- | --- |
| `advance_count_prereq_depth` | `00494dae` |
| `civ_has_advance` | `004bd9f0` |
| `advance_has_prereq` | `004bdaa5`; primary body has unconditional visited-set repair |
| `civ_advance_gov_prompts` / `civ_golden_age_popup` | `004bea84` / `004bee56` |
| `civ_give_advance` and private announce/sale/first-discovery helpers | `004bf05b` |
| `civ_can_research_advance` / `civ_pick_research_advance` | `004bfdbe` / `004c09b0` |
| Goal parent and private list/help helpers | `004c0cf7` |
| Choice dialog / ordinary wrapper | `004c195e` / `004c21ad` |
| Completion / cost / points | `004c21d5` / `004c2788` / `004c2b73` |
| Captured technology | `0057a27a` |
| Shared production reset, owned by C06 | `00442541` |

Independent source: `civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, selected bodies in
`src/civilization.cpp`, `src/contact_advance_score.cpp`, `src/contact_transfer.cpp`
and `src/research_goal.cpp`, plus `analysis/reference/bugfix-log.md` BF-173/BF-304.
These corroborate the cost's rank-as-slot and signed-shift behavior, the original
ordered ancestry recursion, transfer authority/partial mutations and the points
parent. Original executable `Civ2/civ2.exe` has SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
No new original-process replay or direct disassembly is claimed here.

BF-173's primary visited search changes cycle behavior even with classic mode
selected. The independent project's BF-304 records an authored Bonaparte FW
Mobility Doctrine/Tactics cycle admitted by the original and used through a
populated turn. Its corrected file ingestion permits the cycle; network/editor
prospective-table restrictions are separate repairs. Those records do not prove
that every later ancestry query on the cycle terminates. Do not present blanket
cycle rejection or visited-set results as original behavior.

Representation details for validation, not a prescribed implementation layout:

- Civilization knowledge occupies a membership vector; acquisition source is a
  separate100-entry array. Civilization offsets `0a`, `0c`, `10`, `11`, `12`
  hexadecimal hold target, progress, ordinary count, future count and cost count.
  Global first-discoverer and known-by arrays begin at game offsets `36` and `9a`.
  F06 owns serialized encodings; logical membership operations above need no
  corresponding in-memory byte arrays.
- Fixed advance identities include Communism15, Construction18, Democracy21,
  Fundamentalism31, Gunpowder35, Invention38, Mobile Warfare53, Monarchy54,
  Philosophy60, Railroad67, Refrigeration70, Republic71, Seafaring75, Trade84,
  Writing88 and Future Technology89. Fixed unit types include Engineers1 and
  Paratroopers13. These are rule-slot dependencies, not name matching.
- Ordinary Palace/Barracks/Airport/Supermarket identities are1/2/32/24. Great
  Library is wonder4, improvement43; United Nations and Marco Polo are24/9.
  The main contracts use named identities; validate these against the pinned
  enumeration rather than treating resource text as a key.
- Source−2 is stored as254; the announcement uses Great Library for any negative
  source. A source equal to the recipient can denote a hut/catch-up award without
  adding “from self.” Flags and treaties are the F02/F06 semantic flags; their
  storage masks need not be copied into a new architecture.
- Set Civilization advance message is `9b` hexadecimal; Golden Age, End Wonder
  and Took Civilization notice kinds are `54`, `4d`, `63`. Payload identity,
  recipient and ordering are specified above; N02 owns wire encoding/receipt.
- Completion notice mode is a retained global initialized0 in the primary data;
  its selected source uses mode2 to preserve progress. No additional writer was
  found in the inspected primary source. Treat a nonzero mode as supplied state;
  do not invent a new gameplay action that sets it.
- The99-element goal-path workspace and source-array indexing do not authorize
  writes into adjacent locals or civilization fields. Fixed temporary text
  buffers and icon storage likewise do not expand the safe accepted write
  domain. Evidence of those overwrites belongs to bug tests, not conformance
  assertions requiring corruption. Read boundaries remain separately explicit.

## Appendix B — Validation and remaining integration

An adapted Linux-host source harness extracts12 complete primary bodies:
knowledge, prerequisite count, availability, recommendation, cost, Golden Age,
Barracks sale, first discovery, grant, completion, points and captured technology.
Its points/completion/grant recursion uses the actual extracted children; ordinary
research-choice, government prompt and grant-announcement calls are adapters.
A separate bounded ancestry fixture follows the independent original recursion,
not the primary repaired visited search. Typed fixture state retains the declared
scalar widths; pointer carriers are widened for the host. One negative-shift
expression uses its defined retained-multiplication equivalent, and a negative
grant guard stops before its excluded storage write after target clearing.

The focused run passed **65 assertions** in optimized and
AddressSanitizer/UndefinedBehaviorSanitizer builds, with leak checking disabled
for the host environment. It covers sentinel ordering, disabled/known availability,
count overshoot and cycle boundary, picker calls/draws/ties including signed-minimum
valuation, cost boundaries, direct rank indexing, both map flags, authority,
turn0 and duplicate/future grants, scalar wrap, notice gates, recursive Library
propagation, Barracks clearing/credit, Golden Age selection, raw-location expiry,
completion/points/free-advance ordering, live city eligibility and captured-tech
filters. The excluded negative grant produces a diagnostic with cleanup, not
adjacent-memory state expectations.

A08 valuation, C08 wonder/building queries and Leonardo, C06 production reset,
city mood recalculation, government setter, actual choice/goal UI, localized text,
resource rendering, diplomacy and network services are explicit adapters or
source-review boundaries. The harness does **not** join those full implementations,
exercise every choice/help callback, run a consecutive civilization turn or
compare against an original runtime. Source-backed checks are not independent
cleanroom conformance. The fixtures remain external research material rather
than imported implementation code.

All 22 selected source bodies were checked against the pin; both reference trees
remained clean and the original executable hash matched. Document links/anchors,
source destinations, specification IDs and whitespace were checked. Remaining
integration includes real C03/F04 science collection, C06 Darwin, C07 payment
caps/credit, C08 upgrades and expiry, V01 catch-up, V02 transitions, V05/V06
transactions, A08 policy, S02 event grants, native P04/P07/P08 modal behavior and
N02 authority/receipt. X02 must retain complete traces distinguishing supplied
read/termination boundaries, accepted arithmetic and excluded-write diagnostics.
