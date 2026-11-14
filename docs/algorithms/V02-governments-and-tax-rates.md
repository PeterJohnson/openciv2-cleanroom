# V02 — Governments and tax rates

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in V02 |
| --- | --- |
| Gameplay | Government/revolution, cap enforcement, rate normalization/balancing, meaningful accepted rate changes, treasury/population results and previews. |
| Original files/content | F03 government rules and F06 rate/preference fields. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Rate-dialog receiver, scrollbar callbacks, palette lifetime and original edit/close choreography; drafts are not automatic game commits. |

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

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. This specifies government availability, direct and announced
changes, player selection/revolution, the government portion of turn maintenance,
rate limits/balancing/dialog state, population queries and milestone/funds checks,
and the civilization treasury normalization used by upkeep. It includes the
simulation effects of the rate interface; native drawing/layout and event-loop
services remain P03/P04/P07 and N01/N02 responsibilities.

[F04](F04-turn-sequencing.md) owns phase order; [V01](V01-civilization-lifecycle-and-civil-war.md)
owns civilization creation/reset, retirement and civil war.
[A08](A08-ai-research-government-and-diplomacy.md#3-rates-and-government-policy) owns
AI government valuation/selection and civilization rate policy; this document owns the shared
queries and transitions those choices invoke. [V04](V04-diplomacy-and-treaties.md#11-civilization-diplomacy-maintenance) owns
the remainder of the combined diplomacy-turn operation after the government
prefix below. V03 owns
[advance knowledge and research cost](V03-research-and-technology.md).
[C08](C08-buildings-and-wonders.md) owns
active wonder queries and maintenance costs. City calculation, collection and
upkeep remain with C03–C08; these operations must not be replaced by a global
rate-times-income approximation.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
semantic publications and failure effects. Only dependencies within the binding
scope above constrain evaluation; storage/API choices need no original-trace
equivalence proof. External file/wire definitions remain binding at adapters.

## 1. State, accepted inputs and arithmetic

Use [F02](F02-game-state-and-invariants.md)'s live slots, upper scan bounds,
directed treaties, human set, retained state and mutation rules. Government
identities and nominal scalar values are:

| Identity | Name | Maximum individual rate, tenths | Availability dependency |
| --- | --- | --- | --- |
| 0 | Anarchy | 6 | Always accepted by the query |
| 1 | Despotism | 6 | Always accepted by the query |
| 2 | Monarchy | 7 | Monarchy knowledge or Statue of Liberty |
| 3 | Communism | 8 | Communism knowledge or Statue of Liberty |
| 4 | Fundamentalism | 8 | Fundamentalism knowledge or Statue of Liberty, and its advance enabled |
| 5 | Republic | 8 | Republic knowledge or Statue of Liberty |
| 6 | Democracy | 10 | Democracy knowledge or Statue of Liberty |

Nominal files/configuration satisfy F03/F06's domains, civilizations 0…7 and
backed city/leader/wonder/advance state. Government is stored modulo 256; tax and
science likewise. Luxury is derived as `10−science−tax`, rather than stored in
the civilization. Ordinary accepted rate choices lie in 0…10 and total ten,
with each respecting the current cap. The dialog has full-width signed selections,
initial preferred values, three hold values, a cached cap, preview totals and a
rate-limit warning. Persistent hold values survive between dialog invocations.

Additional inputs include local-human slot, game mode, scenario and tutorial
flags, autoplay, government names/titles/portraits, city production, current
research, turn, treasury, prior normalized treasury, population milestone, live
callbacks, receiver state and network/UI results. Hold choices ordinarily have
at most one selected member. Imported or directly supplied receiver state may
contain multiple/nonboolean holds; their nonzero tests below still apply.

Use [F01](F01-determinism-and-arithmetic.md) for reference expression results,
truncating division, arithmetic shifts and specified membership/publication
conversions. Nominal rates are logical integers in 0…10; that bound does not
remove conversions reached by admitted custom-rule or remote updates.
Government comparisons that use a full argument are distinct from comparisons
of its stored byte. Population milestone, turn, city population summaries and
retained city yields use their signed interpretations; city size is signed8.
No direct algorithm here draws random numbers. Named children and A08 choices
can draw, and V04's continuation begins its own random/diplomatic work afterward.

An accepted off-nominal scalar is not automatically clamped to its nominal
domain. Examples include government256 storing Anarchy while still passing a
full-positive-argument test, and excessive incoming tax/science producing a
negative derived luxury. A stricter importer may reject them, declaring its
smaller domain. Invalid record/hold-array writes are **excluded bugs** under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
even if adjacent original storage is mapped. Stop before such a write, preserving
the valid prefix, or reject the input earlier. Unrecovered extra reads require
supplied evidence/backing or a bounded stop; do not invent a value or require
arbitrary corruption. Resource/window failure does not imply rollback of already
changed rates or governments.

## 2. Shared government and rate queries

`maximumRate(c)` reads c's unsigned retained government:

```text
if government < Monarchy: return 6
if government == Monarchy: return 7
if government < Democracy: return 8
return 10
```

Thus other byte values >=6 also return 10; the operation does not validate a
seven-government domain. Fundamentalism's separate science-production cap is
applied by [C03 trade distribution](C03-city-economy-and-corruption.md), not by
lowering this dialog's maximum from 8.

`available(c,g)` first queries c's active Statue of Liberty, even for the switch's
default cases. Then, for g=2…6, query the fixed advance from the table above,
**even if the cached wonder result is already true**. Return false only when
both are absent, except Fundamentalism additionally returns false if that
advance's enabled field is zero. That enabled read occurs after knowledge/wonder
results; Statue does not bypass it. Other g, including 0,1 and out-of-range full
arguments, return true after the wonder query. No other government checks an
advance-enabled flag here or walks disabled prerequisites. V03 supplies knowledge;
C08 supplies active wonder ownership/expiry.

This is a menu/AI eligibility query, not a guard automatically applied by every
setter. Direct government changes and the unrestricted selector below can bypass
it. Scenario prohibition is enforced by particular callers, not by this query.

## 3. Direct government setter

`setGovernment(c,newGovernment)`:

1. Capture the current government byte, then store `u8(newGovernment)`.
2. If the stored government differs from the captured byte:
   - If the live government is Anarchy **or** c is currently AI, scan other=0…7
     and clear Vendetta in **other→c**, including the diagonal. Do not clear
     c's outgoing row or other treaty flags by implication.
   - If c is currently human, clear the shared AttitudeDirty transient flag.
     Scan live city slots ascending through the live bound; for each current
     owner equal to full c, call C03 full `recalculate(city,1)`. After it, if
     that slot's current production is the fixed Fanatics unit and c's current
     government is not Fundamentalism, directly replace production with the
     fixed Riflemen unit. This is a direct production field store, without
     C06 production-switch stock/counter/prompt semantics. Do not recheck city
     ownership/token between recalculation and that production test.
3. Independently of whether the government changed: if c is currently human,
   the **full** newGovernment argument is positive and autoplay is off, call
   the tax-rate dialog wrapper (section 7).

The setter does not set Revolution/GovernmentChanged flags, test availability,
ask for revolution confirmation, or impose scenario restrictions itself. Setting
the same positive government can still reopen rates. A full argument256 stores
Anarchy and can open rates; a negative argument with the same low byte does not.

## 4. Announced government change

`changeGovernment(c,newGovernment)` captures the incoming government as old.
If the full new argument is nonzero, set GovernmentChanged immediately.

For a currently human c, a nonzero argument invokes the restricted government
selector, then returns without a direct setter call. The argument requests
selection; it does not force that particular government. Cancellation leaves
the earlier GovernmentChanged flag. For human newGovernment==0, continue directly
to the setter after skipping the AI notification path.

For AI, only run notifications when old differs from the full new argument or
the current Revolution flag is set:

1. Set text arguments in order: c's adjective at0, requested government name
   at1, c's current leader name at2 and current leader title at3. This happens
   before the government setter, so the title is still based on current state.
2. Test local observer eligibility in this short-circuit order: localHuman→c
   Embassy; localHuman's United Nations; its Marco Polo; revealed-map state.
   This test does not require localHuman's human/alive bit.
3. If eligible, inspect c's **live** Revolution and autoplay:
   - If Revolution is clear or autoplay is active, then only when autoplay is
     off show Overthrown with old government's portrait and afterward set
     Revolution. Under autoplay no local popup/flag mutation occurs here.
   - Otherwise show Changed with the requested government's portrait and
     afterward clear Revolution.
   Then assign c's tax rate with the formula below, even if autoplay suppressed
   the popup.
4. Initialize notification count 0. If current mode is networked, scan observers
   1…7 ascending. Require their current human bit and observer!=localHuman.
   Test observer eligibility in this order: revealed-map state; observer→c
   Embassy; observer United Nations; observer Marco Polo. No alive test.
   For each eligible observer, re-read c's Revolution/autoplay:
   - If Revolution is clear or autoplay is active, send Overthrown using one
     text argument and old-government portrait, then set Revolution.
   - Otherwise send Changed using four text arguments and new-government
     portrait, then clear Revolution.
   Increment count. The flag mutation from one observer affects the next;
   autoplay does not suppress these remote sends.
5. If at least one remote notification was sent, assign tax with the same
   formula again. Science is unchanged by this assignment.

For the assignment, let `rank=newGovernment` when its signed full value<4,
otherwise `rank=s32(newGovernment−1)`. Set tax to
`u8(4−arithmeticShiftRight(s32(rank),1))`. For nominal governments0…6 this gives
`[4,4,3,3,3,2,2]`. If neither local nor remote observer qualified, this wrapper
leaves the old tax rate. Do not apply this assignment unconditionally.

After the AI branch or human zero branch, call the direct setter with the
original newGovernment argument. There is no network-host forwarding wrapper
around the complete change. Portrait zoom follows the current interface scale;
N02/P04 supply the actual sends/notices and preserve the specified callbacks.

## 5. Player government selection and revolution command

### Government selector

Construct a temporary dialog before the human test and destroy it on normal exit.
If c is not human, perform no selection. If c is a nonlocal human in network
mode, send PickGovernment to its current peer, with allGovernments as the request
argument, then exit without waiting. Otherwise:

1. Load PickGovernment text. Enumerate g ascending from0 when allGovernments is
   nonzero, otherwise from1, through6 inclusive. In unrestricted mode include
   all without querying availability; otherwise include exactly those for
   which available(c,g) succeeds. Option values are government identities.
2. Show the modal dialog. A negative result exits without government effects.
   For a nonnegative result call setGovernment(c,result) without a second
   eligibility check. The setter's city and rate-dialog children finish first.
3. Play NewGovernment sound; set text arguments to the **live** leader name,
   live leader title, adjective and selected government label in order. Show
   NewGovernment with that selected portrait.
4. If result>4, Tutorial is currently enabled and the democratic-government
   tutorial has not been shown, set its government label, show Democrats,
   then set the shown flag after the callback returns.

Normal UI results are members of the offered list. Accepting an arbitrary
nonnegative callback result does not make missing government-name/portrait
backing available; off-nominal accesses follow section 1. The unrestricted
cheat caller passes nonzero allGovernments, but the selector still requires a
human c. It is not an unrestricted setter for AI records.

### Revolution command

Capture c=localHuman. If this is a scenario with NoGovernmentChange set, return.
Otherwise, if localHuman's GovernmentChanged is clear and its government is not
Anarchy, set text to the adjective and current government label, then ask
Revolution with the current portrait. **Exactly result0** confirms: play Revolt,
set text to the captured c's plural name, show StartRevolution with the information
sprite, setGovernment(c,Anarchy), then refresh menu state. Other results do nothing.
This command does not set Revolution by itself.

If GovernmentChanged was already set, call changeGovernment(liveLocalHuman,1)
instead of asking Revolution, even if its government is Anarchy. If the flag
was clear and government already Anarchy, do nothing. The menu-availability
controller ordinarily disables the command during Anarchy or scenario prohibition;
it is not an extra gate inserted into every direct invocation.

C04's repeated Democracy disorder is a separate caller: its notice and scenario
gate precede clearing Revolution, changeGovernment(c,0), and setting Revolution
again. Do not replace that sequence with the player command.

## 6. Government portion of turn maintenance

F04 invokes the combined government/diplomacy operation after its city pass.
This document owns the following prefix; V04 must resume immediately afterward:

1. Capture current government; clear GovernmentChanged and Reserved40 flags.
2. If the captured government is Anarchy:
   - When c is AI **or** `(turn AND 3)!=0`: if `(liveTurn AND 3)==0` or active
     Statue of Liberty is present, call changeGovernment(c,1). After it, if
     c is now human, clear Revolution.
   - Otherwise this is a human at a turn divisible by4. If its live Revolution
     is clear, call changeGovernment(c,1). If set, only clear Revolution and
     remain in the current government for this prefix. Do not query Statue
     in this second branch.
3. If c is currently AI and Revolution is now set, call changeGovernment with
   its current government. This can run after step2 and can change notification
   flags again.

These are calendar/flag tests, not a sampled revolution duration. A human forced
into Anarchy with Revolution set can spend an additional four-turn cycle if the
first divisible-by-four check only clears the flag. A menu revolution without
that flag can select on the next such check. Statue can permit the other-turn
branch; AI exits on a divisible-by-four turn regardless of Statue.

The prefix applies to slot0 too. V04's continuation then returns for slot0;
for other slots it generates diplomatic random state and updates treaties and
other relations. Do not move those draws before this prefix or treat it as a
standalone complete diplomacy turn.

## 7. Rate entry and cap-enforcement wrappers

`showRates(c)` constructs a temporary rate dialog. If c is human and either the
mode is local (<3) or c==localHuman, run section8, then refresh the city window.
For a nonlocal human in network mode, send KingdomRates to its current peer.
For AI do neither. Destroy the temporary dialog on normal exit in all cases.
There is no autoplay check in this wrapper; particular callers supply theirs.
There is no acknowledgment wait or local fallback for the remote request.

`enforceMaximum(c)` does nothing for AI. For a nonlocal network human send TaxCheck
to its peer. Otherwise capture cap=maximumRate(c) once and test in this order:

1. If live unsigned tax>cap, call showRates(c).
2. Re-read live science/tax; if `s32(10−science−tax)>cap`, call showRates(c).
3. Re-read live unsigned science; if science>cap, call showRates(c).

The cached cap survives dialogs and any government change. All three calls can
occur if callbacks leave each successive test true. Negative derived luxury
alone does not trigger a call. N02 owns receipt, where TaxCheck and KingdomRates
invoke the corresponding operation for the receiving local-human slot.

## Rate operations versus original controls

Government caps, normalization/balancing formulas, preview values and accepted
rate changes are game semantics. The following dialog/scroll recipes describe
one UI realization. Prepare required cap normalization explicitly and publish
any real game effect once; window construction and painting do not perform it.
Use named rate-adjustment/acceptance operations. A new UI can stage adjustments
until Apply; an immediate-apply UI treats each accepted adjustment as a command.
Cancel discards only unsubmitted drafts. Retain accepted normalization/rate
effects and remote TaxCheck/KingdomRates obligations. Shared receivers, hold
widgets, scroll callbacks, palette lifetime and native close order are reference.
Real government/authority changes while a decision is pending still require the
owner's game validation; synthetic widget callbacks are not additional inputs.

## 8. Rate-dialog lifecycle

The following retains original interaction and service ordering as reference.
Adopt rate arithmetic and actual game/peer effects under the operation contract
above; P03/P04/P07 permit different controls and presentation. Native creation,
show or paint callbacks do not create required game publications.

### Opening and normalization

1. Initialize a local changed flag0 and palette resources. Publish this active
   receiver, store c in it and set rate-dialog active. Allocate/construct the
   background surface and publish its result before the following queries.
2. Cache maximumRate(c); clear the rate-limit warning. Modify the **live
   civilization rates**, in this order:
   - While tax>cap, decrement tax modulo256; if science<cap increment science
     modulo256. Set warning1 on each iteration.
   - While science>cap, decrement science; if tax<cap increment tax. Set warning1.
   - While `s32(10−science−tax)>cap`, increment tax if tax<cap, otherwise increment
     science; set warning1. Re-read the rates at each test.
3. Capture preferredTax=unsigned tax, preferredScience=unsigned science,
   preferredLuxury=`s32(10−science−tax)`. Run section9's balancer with adjusted=
   preferredTax, first=preferredScience, second=preferredLuxury,
   firstPreferred referencing preferredScience itself, and **both hold arguments
   referencing the same zero changed flag**. Copy these results into selections.
4. Create the window and controls. Scrollbar callback order is Tax, Science,
   Luxury, with ranges0…10 and their then-current selections. The single OK
   button is configured as both default and cancel action. Load persistent holds
   in Tax, Science, Luxury order, set checkboxes and register callbacks.
5. Calculate preview (section10), register redraw, repaint and show. In current
   network mode install modal-idle network polling. Run the modal loop, remove
   its idle callback, then pump messages while the shared active flag remains set.

Initial normalization does not set changed: it only sets warning. In normal
state the preferred rates already sum to10, so the balancer need not alter them.
It is not a comprehensive malformed-input repair: incoming tax255/science255
under cap6 becomes6/6 with preferredLuxury−2, still summing to10. Do not clamp
that negative value unless declaring a stricter policy.

### Closing and commit

The OK/default/cancel callback first clears the shared active flag, then ends
the active receiver's modal loop. After the loop and final pumps:

1. Delete the current background surface if present, then clear its global
   reference. Copy all three receiver hold values back to persistent holds.
2. If either live civilization tax/science differs from its **full-width**
   selection, set changed1. Store tax selection's low byte, then science's low
   byte into the civilization. This happens even when changed remains0.
3. If live turn<11 and live government<Monarchy, copy current science and tax
   into preferred startup rates and save preferences through F06. This is not
   conditional on changed. Negative wrapped turns also pass the comparison.
4. If changed is nonzero, clear AttitudeDirty and fully recalculate live owned
   cities ascending through the live bound. Use full c in the owner comparison.
5. Request city-window recalculation/redraw with argument0, status redraw1,
   then refresh advisor windows4,5,6 in order. Complete palette cleanup and
   return to showRates, whose subsequent city-window refresh is separate.

There is no cancellation rollback; the cancel action reaches the same commit.
Opening normalization alone can leave changed0, so it does not force the
step4 full-city pass. The initial and callback previews nevertheless have their
own persistent city-field effects. Closing preserves other global/receiver
state unless one of the listed children changes it.

## 9. Balancing, scroll callbacks and holds

The shared balancer accepts references A (adjusted), B (first), C (second),
preferredB, holdB and holdC. Capture cap from the **current active receiver**
once. Each sum is `s32(s32(C+B)+A)`; the references can alias.

While sum<10, take the first applicable action:

1. If B<cap and B<live preferredB and holdB==0, increment B.
2. Else if C<cap and holdC==0, increment C.
3. Else if B<cap and holdB==0, increment B.
4. Else increment A, even if that exceeds cap.

Then, while sum>10:

1. If B!=0 and holdB==0, decrement B.
2. Else if C!=0 and holdC==0, decrement C.
3. Else decrement A.

All mutations retain signed32 results. A negative B is nonzero and is decremented
by the second loop if unlocked; do not substitute B>0. No fairness/rotation,
minimum-zero normalization or final cap pass is added. Malformed extremes can
cause very long/wrapped loops; an input-restrictive implementation may reject
them before entry instead of claiming general termination.

A scroll callback receives signed requested value q. If q>the active cap, set
warning1; q<0 alone does not set it. Clamp q to 0…cap for local A and capture
the other two selections into local B/C. Use this table:

| Changed control / A | First B | Second C | preferredB | holdB / holdC |
| --- | --- | --- | --- | --- |
| Tax | Science | Luxury | Initial preferred science | Science / Luxury |
| Science | Luxury | Tax | Initial preferred luxury | Luxury / Tax |
| Luxury | Tax | Science | Initial preferred tax | Tax / Science |

Run the balancer, then store A into the active receiver. If A differs from
**requested q**, set that scrollbar's position and show/redraw it. If the live
first selection differs from local B, store it, set that scrollbar and show it;
then do the same for C. Calculate preview, then redraw the active dialog.
Each active-receiver access is live across control callbacks; do not capture
one receiver across arbitrary reentrant UI operations. Initial preferred rates
are not updated after each scroll. The changed control's own hold is not passed
into balancing: holding Tax protects it while another rate changes, not while
Tax itself is directly manipulated.

For a hold callback, nominal control identity selects Tax, Science or Luxury.
Store the supplied full checked value in that hold. If nonzero, visit all three
holds in order; for each other one write0, uncheck its control and show/redraw
it. Do not normalize the selected nonzero value or call preview/redraw for the
whole dialog here. Unchecking one hold changes no other hold. Invalid control
identities must not write outside the intended three-hold array; the original
receiver-relative overwrite is an excluded bug, even when it lands in another
mapped dialog field.

## 10. Preview and displayed rate information

Preview is not a pure query:

1. Clear receiver totalTax, totalUpkeep and totalScience; capture its c. Initialize
   counts for all 39 production/improvement identities to zero. Capture c's
   unsigned science, then tax; temporarily store selected tax's low byte, then
   selected science's low byte into the live civilization.
2. Scan live city slots ascending through the live bound. For each live owner
   equal to full c, call [C03 distribution](C03-city-economy-and-corruption.md)
   with `(city, signedCurrentTrade, mode1, extraTax0)`.
   Then query ordinary-building presence for identities0…38 ascending and
   increment each present count. C08's query rejects nonbuilding identities;
   do not infer installed spaceship parts, Capitalization or wonders here.
3. Only after distribution and counting, if the city's live Disorder flag is
   clear, add its current signed tax/science yields to the receiver totals.
   Disordered cities still underwent distribution and contribute building counts.
4. Scan identities0…38 ascending. For a nonzero count query C08 upkeep; only
   if that result is nonzero add `s32(count*upkeep)` to totalUpkeep. This estimates
   maintenance, without deducting treasury or invoking building-sales policy.
5. Restore captured civilization tax, then captured science. Restore occurs
   only on this normal completion path. Do not restore the cities' distribution
   fields, shared calculation scratch, or other child effects.

Thus preview can leave city tax/science fields calculated with temporary rates
while the civilization rates have already reverted. Full C03 recalculation is
a separate operation owned by its caller. No global city-count/total-population
rebuild is implied.

Rendering displays the live government's label and cached cap, selections as
percentages of10, preview tax income/upkeep and discovery-turn estimate. It
consumes the warning by using the warning color for the heading and resetting
warning0; other rate text uses its ordinary color. For discovery turns, query
V03 research cost and replace a result<1 with1. If signed previewScience<1,
**store previewScience=1**. Display
`trunc(s32(previewScience−1+cost)/previewScience)`. Do not subtract accumulated
research progress: this estimate uses full cost. P07's [tax window](P07-advisors-reports-and-history.md#18-tax-rate-window-layout)
and P04 retain original layout/labels/palettes as reference presentation under
X01. The displayed information and these read/mutation/formula effects remain
required when a renderer is replaced.

## 11. Treasury normalization and population/funds notices

### Treasury and settlement boundary

F04 invokes this ordered treasury operation before capturing startGold and
before unit recovery/city upkeep:

```text
if liveGold > 30000: liveGold = 30000
if liveGold < -16384: liveGold = 30000
if liveGold < 0: liveGold = 0
startGold = liveGold
```

This is not a single clamp: −16385 becomes30000, whereas −16384 becomes0.
Only the classic limit applies; OpenCiv2's optional larger-gold limit is outside
the target. City tax collection is C04, capitalization/distribution C03,
production spending C06, facility upkeep/forced loss C08, and deliveries C07.
Their individual order and live treasury effects remain with those owners.
F04 runs the city pass, government/diplomacy maintenance and AI assessment
before the following combined population/funds check. No blanket end-of-pass
normalization or independent “total upkeep settlement” is added here.

### Population queries

`cityPopulation(city)` starts total0 and, for i=1 through the city's signed size,
adds i. Return1 when total<2, otherwise total. It does not test the city's token.
With a stable valid record this is `max(1,n*(n+1)/2)` for n>=1 and1 for n<=0;
using the triangular formula on a negative n without that guard changes behavior.
Units are ten thousand people and are distinct from F02's sum-of-city-sizes summaries.

`civilizationPopulation(c)` scans live cities ascending through the live bound,
adding cityPopulation for each signed owner equal to full c, retaining signed32.
After the scan, if total<0 or total>32000 set total32000. Then if total<2 return1;
otherwise return total. A civilization with no live city therefore reports1
unit here. OpenCiv2's optional population-limit expansion is outside this target.
[P04](P04-text-and-dialogs.md#3-string-and-quantity-formatting) owns textual formatting and separators; these quantities feed the numeric
milestone algorithm independently of localization.

### Combined population and funds check

Inputs are c and previously captured startGold. Initialize noticePopulation=−1,
calculate current civilizationPopulation, and capture old signed milestone.

1. If population<100 and `liveMilestone*10 < population`, set noticePopulation
   to `trunc(population/10)*10` and milestone to `s16(trunc(population/10))`.
2. Otherwise, for population>=100, if `(liveMilestone−9)*100 < population`,
   set noticePopulation to `trunc(population/100)*100` and milestone to
   `s16(trunc(population/100)+9)`.
3. If oldMilestone<liveMilestone, c is currently human and autoplay is off:
   set text argument1 to its adjective; clear the shared text buffer, format
   noticePopulation, set argument2 from that buffer, then show Fertile with
   c's portrait. Milestone updates still occur for AI/autoplay, without the popup.
4. Re-read the following gates after that popup: c is human; autoplay is off;
   turn loop is active; signed city-count summary>1; live gold<100. If all pass,
   compute `forecast=s32(s32(liveGold−startGold)*10+liveGold)` using live gold
   at its two read points. If forecast<0, show Taxes with c's portrait. Any
   nonzero result invokes showRates(c); exactly zero does not.

There is no rounding up of milestone thresholds: a population between multiples
can rewrite the same milestone without a popup. Falling population does not
ordinarily reduce an already reached milestone. A malformed negative milestone
can cause a notice at zero rounded population; preserve the stated arithmetic
if accepting it. The funds notice uses the retained city-count summary and
wrapped forecast, not a reconstruction of actual city count or a floating-point
runway calculation.

## 12. Effects owned by consumers

| Government-dependent mechanic | Formula/order owner |
| --- | --- |
| Tile yields, city work and government-score side effects | [C02](C02-city-tiles-and-worker-assignment.md), [C03](C03-city-economy-and-corruption.md) |
| Corruption/waste, Fundamentalism science cap/loss and tithes, trade allocation | C03; rates here are inputs, not a replacement formula |
| Martial law, military unhappiness, tax collection, disorder and celebration | [C04](C04-city-happiness-and-disorder.md) |
| Unit/settler support, growth and food shortages | [C05](C05-city-growth-and-support.md) |
| Production eligibility and purchase costs | [C06](C06-city-production-and-purchasing.md) |
| Building upkeep and active wonder effects | [C08](C08-buildings-and-wonders.md) |
| Senate, treaties and diplomatic maintenance after section6 | V04; preserve the prefix's flags and government before its draws/queries |
| AI selected rates, government scores and choices | A08, called at F04's declared city/postprocessing/AI boundaries |
| Civilization creation or copied civil-war government/rates | V01's direct field mutations; they do not imply invoking this document's announced-change wrapper |

Each consumer may query government at different times or use a captured owner.
A cached universal “government bonus vector” must not change those observations.
Shared callbacks require actual child effects or a declared boundary; they are
not assumed-success no-ops.

## 13. Conformance cases


> Compare the gameplay/interchange observations in the
> [production scope](#production-contract-scope). Original memory/helper traces,
> arbitrary native callbacks and unsafe backing-byte cases in historical lists
> are optional reference, not additional production acceptance criteria.

| Inputs | Required observation |
| --- | --- |
| Government bytes0…6, then255. | Caps6,6,7,8,8,8,10, then10. |
| Available query for Anarchy or full99. | Query Statue first, return true; no advance lookup. |
| Fundamentalism disabled, knowledge and Statue present. | Query still returns false after both dependency checks. |
| Human setter256 from Despotism, outgoing/incoming Vendetta. | Store Anarchy, clear incoming Vendetta only, recalculate own cities and open rates. |
| Human sets its already-current positive government. | No changed-government work; rates still open if autoplay off. |
| Human leaves Fundamentalism while a city builds Fanatics. | Full city calculation precedes direct switch to Riflemen; no stock-switch wrapper. |
| AI adopts Democracy, no observer qualifies. | Government changes; tax remains its prior value. |
| Local observer then two remote observers qualify, Revolution initially clear. | Local Overthrown sets Revolution; first remote Changed clears it; second remote Overthrown sets it. Tax becomes2. |
| Autoplay local observer only. | No local notice/flag change, but observer-qualified tax assignment still occurs. |
| Human change request6, selector chooses3 or cancels. | Choose3 if accepted; cancellation retains government but GovernmentChanged stays set. |
| Revolution confirmation0 versus1. | Zero starts Anarchy; one declines. Player command itself does not set Revolution. |
| Human Anarchy, turn4, Revolution set. | Clear Revolution, no selector. Same turn with flag clear invokes selection. |
| Human Anarchy, turn5, without/with active Statue. | No selection without it; selection and post-call Revolution clearing with it. |
| Tax6/Science4/Luxury2 during Tax balancing, Science held. | Reduce Luxury to0; with both other rates held, instead reduce Tax to4. |
| Tax requested6 from4/4/2 while Tax itself held. | Tax6, Science2, Luxury2: the adjusted rate's own hold is not consulted. |
| Tax255/Science255, Despotism dialog entry. | Live6/6, derived/selected Luxury−2; initial normalization alone does not set changed. |
| First cap-enforcement dialog changes government and rates. | Later checks use newly read rates but the originally captured cap; up to three dialogs. |
| Preview selected6/4 at a trade20 city, original civilization3/3. | City distribution reflects6/4; civilization restores3/3; income/upkeep totals are preview only. |
| Same city in Disorder. | Distribution and building count still run, but income/science are excluded; upkeep remains. |
| PreviewScience0 at paint. | Stored previewScience becomes1; display uses full research cost. |
| City size0, −1,4; four size127 cities. | City quantities1,1,10; civilization quantity capped32000. |
| Treasury −16385,−16384,−1,30001 at turn entry. | startGold30000,0,0,30000 respectively. |
| Population10 then105 then91. | Milestones1 then10, then retain10; notices at100,000 and1,000,000 people on the increases. |
| Gold50/startGold56 versus55, city summary2. | Forecast−10 requests Taxes; forecast0 does not. |
| Fertile callback removes c from human set. | Subsequent funds gate observes the new set and skips Taxes. |
| Hold callback identifies a fourth slot. | Stop before the invalid array write; no required corruption of adjacent receiver fields. |

## Appendix A — Source and representation evidence

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary source pin: OpenCiv2
`a71e8aefffe04e6f719e0e213b027b128a747711`, compared with OpenCiv2 revision
`fc2155ccdb2123c1137babab6fa39e38152278c9`.
Sources: [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp),
[city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp),
[window.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/window.cpp),
[unwind.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/unwind.cpp),
[network.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/network.cpp),
[enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h).

| Entry | Original address / boundary |
| --- | --- |
| `civ_max_tax_rate` | `0040bd10` |
| `taxrate_balance` | `0040bdac` |
| Redraw; Tax/Science/Luxury callbacks | `0040bed1`; `0040beec`, `0040c07f`, `0040c212` |
| OK / hold callbacks | `0040c3a5` / `0040c3cd` |
| `TaxRateDialog::calc_totals` / `paint` / `run` | `0040c480` / `0040c7d0` / `0040cd64` |
| `taxrate_dialog_show` / `taxrate_enforce_max_rate` | `0040ddc6` / `0040decc` |
| `menu_revolution` | `0040e3b1` |
| City / civilization population | `0043cc7e` / `0043cce5` |
| Population/funds check; enclosing civilization turn | `00489292`; `00489553`, F04 owns the whole parent |
| Direct setter / availability | `0055c066` / `0055c277` |
| Government selector / announced change | `0055c3d3` / `0055c69d` |
| Government prefix of diplomacy turn | `00560084`, ending before the nonzero-civilization random/treaty continuation |

Independent source: `civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, selected bodies in
`src/government.cpp`, `src/tax_rate.cpp`, `src/civ_diplomacy_turn.cpp`,
`src/civ_turn.cpp` and `src/label_strings.cpp`. These corroborate full-argument
setter gating, incoming Vendetta clearing, observer-dependent notifications/tax,
ordered rate normalization, cyclic balancing/holds, partial preview effects,
population units/cap and the wrapped funds forecast. Receiver-relative backing
adapters there are research evidence; they do not expand valid hold-array writes.

Original executable: local `Civ2/civ2.exe`, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Addresses and independent annotations are evidence navigation. This work does
not claim a fresh original UI replay or direct disassembly of all listed entries.

Representation notes for validation, not required architecture:

- Stored government, science and tax are civilization offsets15,13,14 hexadecimal;
  rates are bytes. Dialog selections/preferences/totals/holds are full32 fields.
- Tax, Luxury, Science selections occupy dialog offsets `2e0`, `2e4`, `2e8`;
  initial preferred Tax, Luxury, Science occupy `2ec`, `2f0`, `2f4`. Hold fields
  at `34c`, `350`, `354` are instead Tax, Science, Luxury. This distinction matters
  when validating callbacks against receiver bytes; a new implementation can
  use named fields. Valid hold control IDs are 300,301,302. Other receiver-relative
  writes are excluded even when they target another existing dialog field.
- Revolution/GovernmentChanged/Reserved40 are `0001`/`0008`/`0040`; incoming
  Vendetta is treaty bit `00000010`; AttitudeDirty is transient bit `0004`.
  Tutorial-government shown is event bit `0020`; scenario NoGovernmentChange is
  `0010`. These encodings belong to F06/N02 where serialized.
- Fixed availability advances are Monarchy54, Communism15, Fundamentalism31,
  Republic71, Democracy21. Statue/United Nations/Marco Polo wonder identities
  are 19/24/9. Fixed Fanatics/Riflemen slots are 8/11.
- Remote notifications are Changed `2b` and Overthrown `2c` hexadecimal, with
  text-count4/1 and portrait argument new/old respectively. NewGovernment sound
  is 20; semantic service calls in the main body are not prescribed internal APIs.
- Original dialog/control/palette resource construction and cleanup are not
  gameplay data models. Preserve state/presentation effects and explicit
  failure boundaries rather than emulating compiler unwind frames or invalid
  receiver writes.

## Appendix B — Validation and remaining integration

Validation uses an adapted Linux-host source harness with 18 selected complete
bodies and three exact behavioral regions: government turn prefix, treasury
normalization, and dialog rate-initialization prefix. The complete bodies include
government queries/setter/wrapper/selector/menu, cap enforcement, population and
funds, balancing/scroll/hold/OK callbacks and preview. It joins real inherited
C02/C03/C04 city calculation/distribution and U01/C01 primitives where those
children execute. Active-wonder/advance/building/upkeep queries, tax-dialog
invocation, text/presentation, control callbacks and networking use declared
adapters. It does not run the full native tax-dialog parent or renderer.

The focused run passed **72 assertions** in optimized and
AddressSanitizer/UndefinedBehaviorSanitizer builds, with leak checking disabled
for the host environment. It exercised caps/availability including defaults,
full-argument versus stored-government gates, real city recalculation before
Fanatics switching, directed Vendetta, observer flags/rates, selector and command
outcomes, anarchy timing, cyclic balancing/holds, initial normalization, live
cap-enforcement callbacks, real trade-preview effects, population boundaries,
treasury normalization and post-notice funds gates. The invalid hold-ID case
stops before the excluded write. Expected native receiver offsets were replaced
with named backed fields; pointer carriers were widened for the host while the
arithmetic/retained fields kept their stated widths.

All 23 selected source bodies, including the full parents of the extracted
regions and the separately reviewed renderer/wrapper, matched the pinned source.
Both reference checkouts remained clean and the executable hash matched.
Internal links/anchors, pinned source destinations, specification IDs and whitespace
were checked. These checks are finite source validation, not independent
cleanroom or original-runtime conformance. Complete native control/modal/paint
behavior, preference writes, real C08/V03 consumers, AI policies, event/network
receipt and consecutive F04 turns remain to be joined. In particular, neither
paint's science-floor mutation nor the full finish/commit sequence was exercised
by this focused harness; those contracts are reviewed against source here. X02 must
retain full-state/call traces and distinguish these limits, accepted scalar
wrap, supplied-read boundaries and excluded-write diagnostics.
