# V01 — Civilization lifecycle and civil war

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. This specifies shared civilization reset/creation, leader
assignment, restart admission, last-city destruction, retirement/forced removal,
and civil-war partitioning. The validation below is finite source validation,
not an original-program or complete-game conformance claim.

[F05](F05-game-setup-and-starting-positions.md) owns the new-game wizard, global
resets, starting-site search, compensation and accelerated starts. Its turn-zero
caller uses this document's creation algorithm. [C09](C09-city-capture-and-revolt.md)
owns capture consequences and central-city selection; [C01](C01-city-lifecycle-and-ownership.md)
owns elementary civil-war city transfer. These are distinct operations.
[U01](U01-unit-lifecycle-and-stacks.md) owns ordinary unit creation/deletion and
stack traversal. [W01](W01-map-geometry-and-spatial-state.md) owns spatial
queries, neighborhood B and map mutation; [W03](W03-visibility-and-exploration.md)
owns revelation. [V02](V02-governments-and-tax-rates.md) owns government/rate
maintenance and the combined
population-milestone/funds turn check; [V03](V03-research-and-technology.md#10-granting-an-advance)
owns advance-grant effects; [V06](V06-espionage-and-bribery.md#2-shared-escape-capture-and-treaty-confirmation)
owns spy escape; [V07](V07-spaceship.md) owns the complete spaceship lifecycle. Their required call
boundaries are identified here. P04/P07 and N01/N02 supply real presentation and
session/message services.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## 1. Inputs, domains and ordered execution

Use [F02](F02-game-state-and-invariants.md)'s retained slots, live record tokens,
upper scan bounds, directed relationships and distinct human/alive sets. A
civilization's zero city/military summaries, its alive bit and its actual live
assets are independent inputs. Do not derive one from another at entry.

Nominal inputs have civilization slots 0…7, normal players 1…7, leader slots
0…20, advance slots 0…99, unit types 0…61, regions 0…63, backed city/unit records
and valid stack/home references. Rules and persisted state arrive under F03/F06.
A restart takes the current live map and rules, not the turn-zero defaults.
Ordinary setup initializes leader usage and overrides as F05 specifies;
standalone/repeated calls can legitimately retain different values.

Inputs include turn, difficulty, restart rule, game-won state, local-human slot,
autoplay, mode, authority, retirement policy, AI goal lists, civilization/leader
state, global advance ownership/first discoverers, remembered world knowledge,
password state, destroyed-civilization history, scenario schism predicate,
implementation random context or controlled choices, callbacks, incoming messages and timer observations.

Use [F01](F01-determinism-and-arithmetic.md) for arithmetic and primary `R()`.
No direct operation here uses the secondary sampling law. Child operations
may consume their own draws. Division truncates toward zero. Unless another
conversion is stated, signed computations have F01 reference results;
bit operations use its corresponding unsigned value. Civilizations/units/cities
are compared with the full argument where stated, even when a mask uses its
low-byte shift count. Do not normalize every input to a civilization byte.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

The following widths affect results and belong in the behavioral contract:

- Turn, city/military/population summaries, last-contact/last-city values and
  saved coordinates retain signed 16-bit values; military strength is unsigned
  16-bit. City population and unit role/counter reads explicitly described as
  signed use the signed low byte.
- Treasury and region population sums retain 32 bits. Leader usage, portrait,
  technology counters, rates and unit-type counters retain low bytes. Leader
  identity/color and pending starting coordinates retain their declared signed
  values; a leader's gender is an unsigned selector, not a normalized Boolean.
- The technology knowledge set contains the entire retained 104-bit vector;
  only the separate acquisition-source/global-summary loops cover 100 entries,
  and catch-up tests only entries 0…98. Do not collapse these domains.

Scans use their live upper bound on every test unless explicitly captured.
Read callback-sensitive fields at the specified step. There is no transaction,
rollback, automatic aggregate rebuild, or general cleanup pass on return.

Out-of-bounds writes are **excluded bugs**, under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs).
Reject an invalid operation before its first invalid write, retaining earlier
specified effects, or impose a declared stricter admission rule. Do not emulate
adjacent-record corruption. Unrecovered out-of-bounds reads instead require
supplied evidence/backing or a bounded stop at the read; no zero/default is
inferred. A validated implementation may reject such inputs earlier, declaring
the smaller accepted domain. Within backed records, scalar wrap, intentionally
retained payload and unusual ordered mutations below remain in scope.

## 2. Shared civilization creation

`create(c)` returns an integer, not a Boolean proof that new assets exist.
A return of 1 includes inactive slots and failure to find an AI leader; 0 can
mean refused human recreation or rejected placement; −1 can mean too few
civilizations remain. A remote result is returned as received.

1. If c is currently human and turn is nonzero, return 0 **before setting busy**.
2. Set creation busy. If mode is networked (mode >=3) and this machine is not
   host, use section 7's creation request and return its result.
3. Capture c's old tribe. Apply section 3's reset, retaining its 64 captured
   region city counts for the later site search.
4. For c=0, clear creation busy and return 1. No leader/site/unit operation follows.
5. Initialize retained `blocked=0`. If c is currently AI, run section 4's leader
   selection. Its no-candidate exit clears alive/busy and returns 1 immediately.
6. Test c's **live** alive bit. If absent, clear creation busy and return 1.
   Reset, catch-up and AI leader effects have already occurred.
7. Run [F05 section 7](F05-game-setup-and-starting-positions.md#7-starting-site-search-and-relaxation),
   passing c and the captured region counts. Retain candidate x/y and attempts.
   At turn zero only, a rectangularly in-bounds hint for the **current** tribe
   replaces x/y and resets attempts to zero. The prior search and its draws
   still occurred; do not validate hint parity, terrain, occupancy or distance.
8. Set blocked=1 if attempts>2000, or if `y<3` or `y>=mapHeight−3`.
   This does not clear an earlier nonzero reuse result from leader selection.
9. If c is currently AI, set science to `u8(signedCivilizeTrait+3)`, then tax
   to `u8(9−liveScience)`. There is no rate-cap normalization here.
10. Admit if c is currently human, or if all of: blocked==0; turn==0 or
    NoRestartEliminated is clear; `signedTurn <= s32(350−35*unsignedDifficulty)`.
    Nominal deadlines for difficulties 0…5 are 350, 315, 280, 245, 210, 175.
    Site search, hints and rates happen **before** this test.
11. On admission run section 5, clear creation busy, return 1.
12. On rejection clear c's alive bit and restore **only** its captured old tribe.
    Count alive normal slots 1…7. With at least two, clear busy and return 0.
    Otherwise, if GameWon is clear, set reason Conquest (3) and GameWon; then
    clear busy and return −1. Already-won state preserves its existing reason.
    Usage, gender, rates, knowledge and other prior effects are not undone.

The admission rule alone does not guarantee successful unit allocation. Nor
does clearing alive in a caller ensure that create sets it again: an inactive
slot exits in step 6. Civil war deliberately uses this reset-only behavior.

## 3. Ordered partial reset and catch-up advances

Reset c in this order:

1. Treasury, research progress, advance count and research-cost count become
   zero. Last-city turn becomes turn when turn>=11, otherwise 10. Government
   becomes Despotism; tax/science become 4/4. Clear subversion, reputation,
   diplomatic-request count and best-shield summary.
2. Set throne levels to `[1,1,0,1,0,0,1,0,0,0]`; upgrades become zero.
3. For other=0…7, clear reputation-credit and aggression entries in **both**
   directions, including the diagonal.
4. Set attitude c→0 to 100. For other=1…7: if c=0 set c→other to 100 without
   a draw. Otherwise draw once, `a=R()%80+10`. If other is currently human,
   replace a with `clamp(a+5*difficulty,10,75)`. Write other→c, then copy that
   live value to c→other. Include other==c. Later creation of another slot can
   overwrite both directions with another draw.
5. Refresh c's active AI goals, then clear its standing goals, using A01's
   ordered children. Goal refresh occurs before standing-goal clearing; do
   not replace the pair by clearing both goal lists.
6. For each region 0…63 ascending, set c's policy to Expansion, initialize a
   retained count to zero, and add each normal slot's unsigned city-count
   summary for this region, slots 1…7 ascending. This captures c's own stale
   region counts too. The region summaries themselves are **not** cleared.
7. Clear c's city count, global civilization unit total, military-unit count,
   military strength, total attack, total city size, ship count and population
   milestone. Clear its four wonder-group counts.
8. For other=0…7 set lastContact[other][c] to Absent (−1). This is the incoming
   column only; do not clear the complete outgoing row.
9. For advance=0…99, set c's acquisition source to Absent and clear c's bit
   from the global known-by set for that advance.
10. For other=0…7 clear treaties both c→other and other→c. Clear c's entire
    104-bit knowledge vector.
11. If the **live** turn is nonzero, perform catch-up below.
12. Clear active, in-production and lost counts for all 62 unit types. This
    happens after any grant callbacks and overwrites their changes to these
    counters.

Fields not listed retain their contents. In particular, this is not a complete
civilization constructor: future technologies, selected research, spaceship,
region aggregates, arbitrary flags and many other cached values survive.
AI leader assignment later changes the female-leader flag; human creation does
not. The reset itself does not delete cities or units still owned by c.

For catch-up:

1. Capture whether c's alive bit is set, then clear it.
2. For advance a=0…98 ascending, qualify when both:
   `(firstDiscoverer[a] is not currently human OR popcount8(knownBy[a])>2)`
   and `(knownBy[a] intersects currentHumans)`.
   Test first-discoverer membership before conditionally counting; count all
   eight low bits, including AI/barbarian bits. Re-read live sets at each test.
3. Only for qualifying advances: on difficulty 0 use roll=0 without a draw;
   otherwise draw once and take `R()%(difficulty+1)`. A nonzero result calls
   V03 `giveAdvance(c,a,c,0,0)`. Preserve the child's complete effects before
   the next advance test. No grant occurs on roll zero.
4. If c was alive on entry, set its alive bit. Otherwise perform no final
   clearing: a callback's newly set bit can survive.

The parent later setting advance count to 1 does not undo actual knowledge or
V03's global acquisition effects. Source-grant count and knowledge cardinality
are deliberately not interchangeable.

## 4. AI leader selection

Use the live list of 21 leaders and current human set:

1. Scan leaders ascending for preferred color equal to full c, that color not
   human, and usage zero. If no candidate, repeat allowing used leaders. A
   third scan is permitted under the same rules as the second if still empty.
   There is no fallback to another color.
2. No candidates: clear c's alive bit and creation busy; report the parent's
   immediate-success exit. Otherwise select the sole candidate without a draw,
   or `candidates[R()%count]` when count>1.
3. If turn==0 and c's pending leader override is nonnegative, replace the
   selected index with it and clear the override to Absent. The selection
   draw, if any, has already been consumed. Do not require the override to
   match the candidate's color; invalid record access has section 1's boundary.
4. If chosen leader usage is zero, set blocked=0. Otherwise draw once and
   set blocked=`R()%(usage+1)`. Set c's tribe, increment leader usage modulo
   256, and clear that leader's city-name cursor.
5. Always draw once. If `R()%3==0`, change zero gender to 1, or any nonzero
   gender to 0. Otherwise retain it. Select that unsigned gender's name entry
   and store it as the leader's active name. Noncanonical gender may require
   additional read evidence; do not silently normalize it.
6. Set c's portrait to the leader index retained modulo 256 and clear its
   FemaleLeader flag. If live leader gender is nonzero, add 21 to the portrait
   modulo 256 and set the flag.

A prior usage of 255 gives a reuse divisor of 256, then wraps usage to zero;
it does not avoid the reuse draw. An inactive AI slot still performs this
entire selection because create tests alive afterward.

## 5. Placement after admission

At retained candidate P=(x,y), with retained blocked value:

1. Set c's advance count to 1.
2. Capture the current unit upper bound and scan downward from bound−1 to 0.
   Delete each live Barbarian unit with W01 weighted diagonal distance
   `D(P,liveUnitPosition)<9`, using U01 deletion with synchronization requested.
   Do not restart the scan or extend its captured bound after children.
3. If c equals the local-human slot, set the first map view center to P.
4. Call U01 creation of fixed Settlers rule slot 0, owner c, at P. Ignore its
   result, including allocation failure. Set c's capital-x to x **after** it.
5. If live turn>20, blocked==0 and P's current explored-by set has no current
   human bit, create another Settlers at P, ignoring failure. Only afterward,
   if live turn>40 and C06 `canBuildUnit(c,NoCity,fixedPhalanxSlot3)` succeeds,
   create that Phalanx at P, also ignoring failure.
6. Begin a map-update batch. Visit B[0…20] around P in W01 order, wrap x,
   check rectangular bounds, and add c to explored-by with synchronization.
   End the batch.

Use fixed rule slots even with customized roles/names. The first unit's W03
surroundings child can change the human-exploration predicate before step 5.
The explicit step 6 changes exploration only: no direct remembered-improvement,
city-size, unit-visibility or contact operation is implied.

## 6. Last-city destruction check

`checkDestroyed(c,killer)` asks whether c still owns a live city. It is not
interchangeable with forced removal and is not a unit-survival test.

1. If c==0, return 0 before changing its busy flag.
2. Set destruction-check busy. On a network nonhost, use section 7's destruction
   request and return its result; no local city scan precedes that request.
3. Scan live city slots ascending through the live bound. Any live city whose
   signed owner equals full c clears busy and returns 0.
4. If killer==Absent (−1), reset c's spaceship without destruction-history or
   destruction/lost-ship notice. Otherwise perform section 6.1.
5. Capture the current unit upper bound; scan downward. For each live own unit,
   call U01 deletion `(unit,1)`. For each live foreign unit whose SpyOnMission
   is set and signed counter equals full c, perform section 6.2. Do not restart.
6. If c is **currently human**: if c equals localHuman, set game-over reason
   LastCityLost (4); otherwise, in network mode send EndSequence to c's current
   peer with current game flags and reason 4. Unscramble passwords, clear only
   the first plaintext character of c's password, then scramble again (F06).
   If the current human set has no bit other than c, clear busy and return 0.
   This early return follows unit/ship/history/password effects. It leaves
   human/alive bits and exploration unchanged; it is not “nothing happened.”
7. In a map-update batch, for x=0…width−1 outer and y=0…height−1 inner, remove
   c from explored-by with synchronization. Visit **every rectangular coordinate**,
   including aliases under W01's map addressing. Do not skip parity or deduplicate.
8. In current network mode clear c's human bit, then send UnlockNation to host
   with c as its nation argument. Also clear the bit in hotseat mode (mode 1).
   Other local modes do not clear it here.
9. Call create(c), ignoring its result. In current network mode synchronize
   state, then request DrawMap without retesting mode between those two children.
10. Redraw all local-human map views unless mode==1 and localHuman is not alive.
    Clear destruction-check busy and return 1, regardless of create's result.

The destroyed check does not clear alive before recreation. Thus an AI can
restart if allowed, or remain reset but inactive after rejected placement.
A surviving human bit can instead make nonzero-turn create return immediately.

### 6.1. History and spaceship loss

For a non-Absent killer, in this order:

1. If autoplay is off, set text arguments to c's adjective and killer's plural
   name. In network mode broadcast Destroyed (two strings, no numbers, c in
   its civilization argument), then select music track 2 in mode 1 and show
   the Destroyed portrait popup. There is no human-only restriction.
2. If live destroyed-history count<12, capture that count as index, increment
   count, then record signed turn, killer's low byte, c's current portrait and
   c's plural name in order. Do not replace an existing entry after capacity.
   Invalid index/name destination writes are excluded bugs, with prior effects
   retained. Appendix A identifies the original field limits for validation.
3. Call [V07 spaceship-lost notification/reset](V07-spaceship.md#8-race-arrival-reset-and-loss)
   even during autoplay. V07 owns the Launched-before-Exists notice choice and
   reset order. The removal parent adds no human/local/autoplay suppression.

The separate reset-only removal path uses V07 resetShip directly; it does not
invoke this history/notice sequence. Both child contracts preserve the global
first-launch calendar marker and shared calculated scratch.

### 6.2. Interrupted spy mission

Clear SpyOnMission, then clear SpyChat from treaty **c→signedLiveCounter**, then
call V06 `spyTryEscape(unit,0,signedLiveCounter)`. At an ordinary admitted entry
the counter equals c, so this clears **c→c**, not c→foreignOwner. Preserve this
in-bounds behavior. Do not clear other treaties, normalize the counter, or
substitute a guessed intended relationship.

## 7. Nonhost request and wait paths

These apply only after each parent's entry gate. They do not execute the local
mutation path as a fallback.

| Parent | Busy flag cleared before send | Pending acknowledgment | Request to host |
| --- | --- | --- | --- |
| create(c) | Creation | −27 | NewCiv(c), other arguments zero |
| checkDestroyed(c,killer) | Destruction-check | −27 | KillCiv(c,killer), other arguments zero |
| eliminate(c,kill,recreate) | Elimination | −2 | KillOrQuit(c), other arguments zero |

Set the pending acknowledgment, send, and sample the start tick **even if the
send's callback already acknowledged**. While acknowledgment equals pending,
read current ticks and, if `s32(now−start)<3600`, process messages `(1,1)` and
repeat. Test acknowledgment before reading time. Negative wrapped elapsed
values continue waiting under that signed comparison.

If still pending, log timeout, show ServerConnectTime with the information
sprite, then clear turn-loop active after the popup returns. Otherwise log
receipt. Next, while either priority or normal receive queue is nonempty,
process `(1,0)`. This drain has no additional timeout. Create/destroyed-check
return the live acknowledgment after the drain; eliminate returns no value.
A late message can change the returned result after timeout. There is no final
busy-flag clear after the drain to erase a callback's update.

KillOrQuit does **not** send the kill/recreate arguments. The host dispatch
calls eliminate with its default false/false arguments; its local retirement
policy still determines destruction. Do not serialize the client's flags as
an unstated compatibility improvement. N02 owns packet encoding and actual
peer dispatch; these waits require its ordered callbacks and queue observations.

## 8. Retirement and forced removal

`eliminate(c,kill,recreate)` returns no value. Interpret kill/recreate by their
low byte. It is a separate path with inline asset mutations; replacing it by
C01/U01 ordinary deletion changes behavior.

Initialize retained `relinked=false` **once for the entire call**. Set elimination
busy. On a network nonhost, use section 7 and return. Otherwise:

1. Clear c's human bit. In current network mode send UnlockNation to host.
2. If KillCivilizationOnRetire is nonzero or lowByte(kill) is nonzero, perform
   the destructive sequence below inside one map-update batch.
3. Unscramble passwords, clear only the first plaintext character of the current
   working c's password, and scramble, whether or not destruction ran.
4. In current network mode stop CD playback, synchronize state and flush the
   send buffer with bound 5000, without rechecking mode within this group.
5. If the live human set has no bit other than c, clear elimination busy and
   return. There is no recreation or final map redraw on this branch.
6. If lowByte(recreate) is nonzero call create(c), ignoring the result. In
   current network mode synchronize state then request DrawMap. Redraw all
   local-human map views, clear elimination busy and return.

With kill=false and policy disabled, cities/units/alive remain and only human
control is removed before the common tail. A recreate request on that path can
reset the still-populated civilization and place additional units; there is no
implicit cleanup of its retained assets.

### 8.1. Inline unit pass

Within the destructive batch scan live units ascending through the live bound:

- A foreign SpyOnMission unit whose signed counter equals working c uses
  section 6.2 and no own-unit deletion.
- For an own unit, when c>=0 and slot<2048: decrement c's military count if
  nonzero and the signed type role<5; decrement its active count for that type
  if nonzero; decrement the global civilization unit total if nonzero. Retain
  the respective scalar wraps. The source predicates are not general bounds
  validation; writes outside these fields are still excluded.
- If previous stack link is nonnegative, write previous.next=current.next and
  set retained relinked=true. If next link is nonnegative, write next.previous=
  current.previous and set relinked=true. Only when relinked is **still false**
  and current coordinates are in bounds, clear the tile's Unit feature with
  synchronization. A link found in an earlier unit suppresses this clearing
  for later isolated units too.
- Set this unit's previous/next to Absent, x to `s16(−100*(liveOwner+1))`, y to
  the same formula using its separately read live owner, and token to zero.
  If this slot is exactly live upperBound−1, decrement the bound once.
  Do not trim all preceding dead slots.
- Scan slots 0…liveUpperBound−1 without a live-token check. For Sleep orders
  whose goto-x equals the deleted unit slot, set orders to None. Retain goto-x/y
  and all other fields.

No generic owner/cargo/home/visibility cleanup is added. Invalid stack writes
stop safely; valid stale/dead record accesses retain their ordered effects.

### 8.2. Inline city pass and radius repair

Next scan city slots ascending through the live bound. For each live city owned
by working c:

1. Capture its x/y, then set working c from its live signed owner. Decrement
   c's city count, clear this city's token, and decrement the upper bound once
   if it was exactly the last slot. Other retained city fields survive.
2. For every remaining live city ascending, scan routes downward from signed
   routeCount−1 and remove each partner equal to the deleted slot using
   [C07 route removal](C07-trade-and-commodities.md). Do not restart.
3. For every one of 28 raw wonder locations equal to this city, write Destroyed
   (−2). This does not use the active/expired wonder query.
4. Initialize seen=0; clear the City feature at the captured center with sync.
   Visit B[0…44] around it, wrapping x and checking bounds. For non-Ocean tiles,
   set fertility's high eligibility flag (logical `fertility OR 8`). Within
   B[0…20], clear radius claimant to 0 only if it equals working c, and query
   city-or-unit owner. For each owner>0 different from c add its bit to seen.
5. If seen is nonzero, traverse the center's U01 stack and add seen to each
   unit's visibility.

After all city removals, visit **every city slot below the live bound**, including
zero-token slots and foreign cities. Capture its x/y and visit B[0…44], wrapping
x and checking bounds, with **no Ocean exclusion**. Set fertility to its low
three-bit value (`fertility AND 7`). Within B[0…20], set radius claimant to the
**working eliminated c**, not to this city's owner. Later slot visits can replace
earlier radius assignments. This is an in-bounds compatibility quirk, distinct
from excluded array overflows.

Finally reset the working c's spaceship, clear its alive bit, and remove its
exploration at all rectangular coordinates in x-outer/y-inner order as in
section 6. End the batch and continue the common password/network tail.

## 9. Civil-war eligibility and rebel initialization

`civilWar(parent)` returns 0 on its explicit refusal branches, otherwise 1.
C09 decides when to ask; there is no additional random eligibility roll here.

1. Always call [S02's NoSchism predicate](S02-event-language-and-execution.md#noschism)
   for parent, even when the separate events-loaded flag is clear. A false result returns 0.
2. Scan normal slots 1…7 ascending. Each slot with military count==0 and
   city count==0 replaces the retained candidate, so the highest qualifies.
   Do not require it to be inactive, AI, asset-free, or different from parent.
   No candidate returns 0.
3. Initialize regionPopulation[0…63]=0 and capitalRegion=Absent. Scan live
   parent cities ascending. Add each city's signed size to its tile's region
   sum with signed32 retention. If the city has Palace and either no capital
   was selected or parent.capital-x equals this city's x, capture its region,
   x/y and city slot. A later matching x wins; no y comparison is made.
   No selected Palace returns 0. Invalid region writes are excluded bugs.
4. Clear candidate's alive bit; call create(candidate). Only **exactly zero**
   returns 0 here, leaving the earlier clear and child effects intact. Every
   nonzero return, including negative values, continues. Normally the inactive
   AI is reset and assigned a leader, then exits create without site/units.
5. Set rebel's alive bit. Set text arguments to parent's and rebel's adjectives,
   show Schism, then in current network mode broadcast Schism with two strings
   and no numbers. There is no human/autoplay suppression on this path.
6. Set rebel.gold=`trunc(liveParent.gold/2)`, then subtract **live rebel.gold**
   from live parent.gold. Set rebel military strength to unsigned parent
   strength divided by 2; do not debit parent's strength. Copy parent government,
   research progress, advance count and then its entire knowledge vector to
   rebel, in order. Do not copy selected research, future technologies,
   acquisition sources, or reconcile the global known-by summaries here.
7. For other=1…7 set lastContact[other][rebel]=s16(turn−8).
   Replace rebel→parent treaty with Contact|War. Replace parent→rebel with
   Contact|War|Aggressor|AggressorLong. Other reset/grant treaties retain their
   current values.
8. Capture parent and rebel exploration bits; begin a map batch and scan the
   map for index=0…liveArea−1, generating staggered-row coordinates starting
   at (0,0). After each visit add 2 to x; if x>=liveWidth, increment y and set
   x to y parity. Nominally this visits physical entries in row-major order.
   For each entry
   containing parent's bit, add rebel's bit with synchronization. End batch.
   Do not copy remembered features or city/unit knowledge by implication.

The candidate can coincide with parent in an inconsistent-but-backed state;
ordered stores and the subsequent live reads still apply. It can also be a
nonzero-turn human, whose create refusal is the exact-zero abort above. Do not
silently select a different candidate after either case.

## 10. Population partition and city transfer

Using the captured region population sums and original capital position:

1. Set loyalPopulation=regionPopulation[capitalRegion], rebelPopulation=0.
2. Scan regions 0…63 ascending. For each nonzero sum p:
   - If loyalPopulation<rebelPopulation or this is capitalRegion, add p to loyal
     only when it is not capitalRegion, then replace this entry with class 1.
   - Otherwise add p to rebelPopulation and replace the entry with class 2.
   Zero entries remain 0. All sums/comparisons use signed32 retention.
3. If `loyalPopulation < s32(2*rebelPopulation)` and
   `rebelPopulation <= loyalPopulation`, scan live parent cities ascending.
   For a city whose live region has class 2, reveal it to parent using W03,
   then call C01 elementary civil-war transfer `(city,parent,rebel)`.
   Otherwise reveal it to rebel. Re-read owner/position at the stated steps.
4. Otherwise set transferredPopulation=0 and capture parent's current signed
   total-city-size summary as target. While `s32(3*transferredPopulation)<target`:
   initialize bestDistance=1, selected=Absent; scan live parent cities ascending.
   Reveal each to rebel **then parent on every iteration**, then compute D from
   the captured capital position to its current position. Strictly greater
   distance replaces selected. No selection ends the loop. Otherwise add the
   selected city's current signed size to transferredPopulation and call C01
   transfer. Repeat with live ownership and bounds but the same captured target.

Distance 0 or 1 cannot win the fallback search; ties keep the first city slot.
The fallback's target is the retained civilization summary, not a freshly summed
population. Negative sizes/wrapped sums follow the arithmetic domain rather than
an invented positive-population normalization. C01 supplies city counts, visible
size, founder/flag and other elementary transfer effects; do not substitute C09
capture, plunder, building loss or partisan creation.

## 11. Civil-war units and capitals

After city partition, scan live units ascending through the live upper bound.
Only units whose current owner equals parent enter:

1. Query exact city at the unit's live coordinates. If none and home is not
   Absent, use its unsigned retained home slot. If no city is selected, continue.
2. Read that city's signed owner. Continue unless it equals parent or rebel.
   Capture this owner, update the unit tile's occupant owner to it with sync,
   then save the outer unit index.
3. Traverse the whole U01 stack containing this unit. Assign **every** member
   the captured owner, including members previously owned by another civilization.
   If a member has a home and that home city's owner differs from the captured
   owner, replace home with the selected city's low byte. Homeless units stay
   homeless. Do not repair active/military counts or other fields here.
4. Restore the saved outer unit index and continue its ascending scan. A stack
   still owned by parent can be visited repeatedly.

Find rebel's central city using [C09's shared query](C09-city-capture-and-revolt.md#shared-central-city-selection).
If nonnegative, add Palace and set rebel.capital-x from that city. Temporarily
set the **original captured capital city's** owner to Absent, find parent's
central city, and if nonnegative add Palace and set parent.capital-x. Restore
the original capital city's owner to parent. No Palace is cleared by these
steps: the original remains, and standalone civil war can leave multiple
Palaces. C09's enclosing capture later removes its captured Palace.

In current network mode synchronize state then request DrawMap, without a
mode recheck between them. Redraw all local-human map views and return 1.
These are not the destruction busy flags; civil war has no corresponding busy
flag or host-forwarding wrapper of its own.

## 12. Caller boundaries and conformance cases

| Caller/child | Required boundary |
| --- | --- |
| F05 setup | Calls create at turn zero in its defined slot order; all later setup balancing and rate changes happen after these creation effects. |
| F05 starting-site search | Receives captured region counts even for later-game restart; later turn changes its distance/region/attempt gates. |
| C09 capture | Calls civil war at its Palace/power branch; uses central-city fallback only on zero result. Later destruction checks use this document's return semantics. |
| C05/C06 city loss, U06 combat, other city/cheat/network callers | Their own deletion/loss consequences precede checkDestroyed. Do not invoke it inside every elementary C01 transfer. |
| Retirement / forced-kill callers | Ordinary retirement passes false/false; the explicit kill-and-recreate caller passes true/true. N02 KillOrQuit dispatch uses false/false. [V08](V08-scoring-victory-and-retirement.md#7-explicit-quit-and-retire-commands) owns the enclosing retirement/endgame flow. |
| V03 catch-up | Each grant sees c temporarily inactive and can affect the next advance test. Parent later clears unit-type counters and may overwrite advance count. |
| V06 interrupted spy | Receives `(unit,0,signedLiveCounter)` after flag/self-treaty clearing, before the lifecycle scan continues. |
| [V07 spaceship](V07-spaceship.md#8-race-arrival-reset-and-loss) | Owns the reset/lost-notice children invoked at section 6.1 and the reset-only removal path; caller history/order remains here. |

At minimum exercise these independently implementable cases. Tables assume
nominal valid backing unless explicitly stated; draws from unspecified children
are separate from the parent draws listed.

| Inputs | Required observation |
| --- | --- |
| Human c, nonzero turn, creation busy already set. | Return 0, preserve busy and all state, no parent draw. |
| Create Barbarian slot 0. | Reset/possible catch-up, then return 1; no attitude draws, leader or placement. |
| Inactive AI with one unused leader. | Seven attitude draws plus one gender draw, reset/leader mutations, no placement. |
| No same-color AI leader in any pass. | Seven attitude draws, clear alive, return 1. |
| Chosen leader usage255, reuse draw255. | blocked255; usage wraps0; gender draw still occurs. |
| Two candidates and a turn-zero override. | Candidate/override processing still runs; an entirely overwritten candidate sample may be omitted. Clear override before selected-record access. |
| Catch-up human-discovered technology known by only two slots versus three. | Two-slot case fails the first condition; three qualifies, including nonhuman bits. Difficulty0 draws/grants nothing. Advance99 is never considered. |
| Reset has stale region city counts, grant changes type counts. | Captured region sums include stale values; unit-type counts are cleared after grants. |
| Valid turn-zero hint after successful random search. | Search draws retained, hint replaces candidate, attempts0; no terrain/parity recheck. |
| Prince difficulty3, turn245 versus246, AI, acceptable candidate. | 245 admitted;246 rejected after search/rates. With only one other living normal slot return−1 and mark Conquest if not already won. |
| Placement at turn21, unseen by humans, unblocked. | Two Settlers; at turn41 eligible Phalanx adds a third unit. Human exploration after first creation suppresses both extras. |
| No cities, last human loses its final units. | Destruction check can return0 after deleting units/history/ship/password, without clearing human/alive/exploration. |
| Foreign mission spy whose counter==eliminated c. | Clear c→c SpyChat, not c→foreignOwner; call escape after clearing mission flag. |
| Retire with kill=false, policy off, last human. | Human bit/password cleared; assets/alive remain; no final redraw/recreate. |
| Forced removal sees a linked unit before an isolated one. | Sticky relinked suppresses the later isolated tile's Unit-feature clear. |
| Deleted and foreign city slots remain below bound. | Radius repair visits both, sets claimant to eliminated c, including Ocean squares. |
| Civil-war candidates2 and7 both have zero city/military counts. | Choose7, regardless of alive status; no second-candidate fallback. |
| Capital-region population4, later rebel region3. | Balanced branch transfers the latter; parent gold101 becomes51 and rebel50; parent strength9 remains9, rebel4. |
| Single-region cities at equal maximum distance from capital. | Fallback transfers the first tied city; stops after captured target's one-third threshold. |
| A parent unit shares a rebel-city stack with a third-party unit. | Both change owner; mismatched non-Absent homes become the selected city; no global unit-count rebuild. |
| Copied knowledge versus global known-by. | Rebel knowledge copy does not update global known-by; parent's copied advance count is not recomputed. |
| Client timeout followed by acknowledgment during queue drain. | Turn loop already disabled; create/check return the later acknowledgment. |
| Invalid region64 during population accumulation. | Stop before the scratch-array write; no compatibility requirement to corrupt the neighboring local. |

## Appendix A — Source and representation evidence

Primary source pin: OpenCiv2
`a71e8aefffe04e6f719e0e213b027b128a747711`; selected bodies compared with checkout
`fc2155ccdb2123c1137babab6fa39e38152278c9`.
Sources: [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp),
[map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp),
[network.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/network.cpp),
[game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp),
[enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h).

| Entry | Original address / evidence |
| --- | --- |
| `civ_create` and six private fragments | `004a7ce9`; reset, catch-up, leader, search, placement and client request are source extraction boundaries, not separate original entries. |
| `civ_check_destroyed` | `004aa378`; distinct last-city gate and return values. |
| `civ_eliminate` | `004e1763`; inline mutations, retained relink flag, deleted-slot radius pass, client flags omitted. |
| `civ_civil_war` | `0057a904`; scenario gate, highest free summary slot, partition, whole-stack owner pass. |
| `civ_spaceship_launched` / `civ_spaceship_exists` / `civ_spaceship_lost_notify` | `004a7577` / `004a75a6` / `004a762d`; reset is `004a74bc`. |
| `city_transfer_for_civil_war` | `0057a7e9`; C01 owns the elementary transfer. |
| `civ_find_central_city` | `0057a685`; C09 owns the query. |
| `city_reveal_to_civ` | `0043cc00`; W03 owns the ordered reveal. |

Independent source: `civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, selected bodies in
`src/civilization_init.cpp`, `src/civilization_destruction.cpp` and
`src/city_capture.cpp`. They independently corroborate retained leader-usage
blocking, late alive checks, catch-up ordering, sticky relinking, dead-slot
radius repair, capital-x selection, directed schism treaties, live owner/home
reads and the captured fallback population target. Their adjacent-record
adapters are evidence for access dependencies, not permission to emulate
excluded writes. No reconstruction source or assets were changed for this work.

Original executable: local `Civ2/civ2.exe`, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Original addresses are navigation evidence; this document does not claim a new
original-runtime lifecycle replay or direct disassembly of every listed entry.

Storage notes are for saved-state/message fixtures, not required architecture:

- Civilization records are 1428 bytes, units 32 and cities 88. The original
  knowledge vector occupies 13 bytes; acquisition sources 100 and each unit-type
  count array 62. Throne levels 10 and region counts/policies 64 are separate arrays.
- Civilization passwords occupy 32 bytes each. This path clears one plaintext
  byte after F06 decoding; it does not overwrite the whole password payload.
- Destroyed history holds 12 entries; name storage is 24 bytes per entry including
  terminator. A negative count or overlong name can cause excluded writes after
  earlier count/history/notification effects. These must not become expected
  corrupted fixture outputs.
- FemaleLeader is `0x0200`; NoRestartEliminated is `0x0100`; SpyChat is
  `0x01000000`. Schism replacement treaties are rebel→parent `0x00002001`
  and parent→rebel `0x00082801`. Semantic flags in the main algorithm are the
  contract; their encoding belongs to F06/N02 integration.
- Region scratch entries in civil war are 64 signed 32-bit values. Invalid-region
  writes, invalid leader/type/civilization destinations, and invalid stack-link
  stores are excluded regardless of whether adjacent original storage is mapped.
  A malformed gender name selection is instead an extra **read** dependency;
  supplied backing or a stop is required if that path is accepted.

## Appendix B — Validation and remaining integration

The focused adapted-host harness extracts the lifecycle parents and their
private creation fragments, spaceship reset/predicate/loss bodies, and the two
shared map queries used by creation. It joins the inherited actual U01 lifecycle
and stack operations, C01 elementary transfer, C07 route removal, C09 central-city
selection and W03 city revelation. Its fixture map and explicitly declared
geometry, building/wonder/advance-query, presentation, network, AI-goal,
advance-grant, spy and password adapters supply the external boundaries. Scripted primary draw returns test consumption
and branching; F01 owns actual generator numeric vectors. All 16 extracted
bodies were unchanged from the pinned source revision; both reference checkouts
remained clean and the original executable hash matched. Local links/anchors,
pinned source destinations, specification IDs and whitespace were checked.

The focused run passed **65 assertions** in both an optimized build and an
AddressSanitizer/UndefinedBehaviorSanitizer build (leak checking disabled for
the host environment). Cases cover early human/Barbarian returns, inactive
leader selection and reuse wrap, catch-up gates/draws/counter order, hint and
restart deadlines, extra-unit gates, client acknowledgment/timeout, last-human
partial effects, self-treaty spy clearing, retirement versus forced removal,
sticky relinking, dead/foreign city radius repair, both civil-war partitions,
knowledge-summary divergence, third-party stack ownership, Palace retention,
and rejection before an invalid region write. This is source validation on an
adapted Linux host,
not native Win32 execution or independent original-program conformance. The
harness guards excluded scratch/history writes instead of reproducing corruption.
It is research material outside this repository, not implementation code required
by a reader of the specification.

Remaining integration includes real A01 goal refresh, V03 grant consequences,
V06 escape, V07 complete ship consumers, F06 password transforms, S02 scenario
predicate, native P04/P07 dialogs and N01/N02 queue/session/host behavior. The
finite fixtures do not establish arbitrary reentrant mutation behavior, all
malformed inputs, indefinite wait/search termination, complete sequential turns
or original UI/network timing. C09's enclosing Palace/capture/elimination chain
and the other complete callers still require joined validation with those real
children. The operation sequences and explicit boundaries above remain the
implementation contract; no missing child result is silently invented.
