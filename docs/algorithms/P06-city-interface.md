# P06: City interface

**Required profile:** [X01 fresh-game isolation and bug repairs](X01-classic-compatibility-contract.md#fresh-game-isolation-and-supported-bug-repairs)
and [behaviorally significant ordering](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
override conflicting procedures and examples below. Retention applies within a
game or to explicit imported inputs, not across new games. Confirmed original-bug
stack/backing/stop cases are optional diagnostics; use documented safe repairs.
Only real effects and callbacks impose ordering; test adapters cannot invent them.
Unspecified safe continuations remain explicit gaps, not required arbitrary bytes.

This specification describes classic Civilization II Multiplayer Gold Edition
5.4.0f, patch 3 city windows, their displayed state and hit regions, city cycling,
unit controls, production presentation, notices and the illustrated city view.
Different window, widget and scene representations may implement these rules.
Preserve information, semantic actions and required gameplay/network effects;
original geometry, decorative scenes and native repaint traces are reference behavior.

[F01](F01-determinism-and-arithmetic.md) owns arithmetic;
[C02](C02-city-tiles-and-worker-assignment.md) owns tile/citizen assignment;
[C03](C03-city-economy-and-corruption.md) owns recalculation;
[C04](C04-city-happiness-and-disorder.md) owns happiness stages;
[C05](C05-city-growth-and-support.md) owns support and growth;
[C06](C06-city-production-and-purchasing.md) owns buying, selling and changing
production. [C08](C08-buildings-and-wonders.md) supplies building/wonder queries.
[P02](P02-map-rendering-and-minimap.md) supplies terrain/unit/city drawing;
[P03](P03-graphics-and-asset-processing.md) supplies raster, sprites and assets;
[P04](P04-text-and-dialogs.md) supplies text, fonts, prompts and controls;
[P05](P05-input-and-command-dispatch.md) supplies native events, keyboard/character
routing and message pumping. [N02](N02-network-commands-and-state-synchronization.md)
owns deferred-request replay. These owners specify the child algorithms called
below; this document specifies their city-interface callers.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. Gameplay-dependent ordering and callbacks remain required; purely
cosmetic randomness and native traces follow the presentation policy.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Required contract and reading guide

City/citizen/resource values, worker/specialist and unit actions,
production/buy/sell/rename eligibility and effects, cancellation after committed
edits, deleted/changed-owner targets, recalculation side effects and network
timeout/polling obligations.

The numbered main sections specify required behavior under
[X01](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy).
Original rendering, geometry, widgets, cosmetic media and native traces are in
[Appendix D](#appendix-d-original-presentation-reference); they are optional reference material.
Main-section numbers are retained for stable citations. Required calculations
that happen inside a presentation operation remain in the main text, including
state mutations, visibility/choice rules and file/protocol effects. Native event
traces do not replace semantic operation boundaries. Harmless UI failures and
rejected off-nominal inputs follow X01's declared safe policies.

## 1. Inputs, state and domains

Nominal inputs are initialized logical city-interface state, valid live city/unit
records, complete rules/assets and supported text. Preserve selected city,
hidden/suspended/redraw-suppressed states, central display mode, improvement
identities/count, update scope, pending Proceed refresh, Proceed-requested,
unit-dialog busy, production timeout and city-calculation workspace. Central
modes are Units/Trade, Support Map and Happiness. Native rectangles, backgrounds,
fonts, hit regions and widget lifetimes are reference state in Appendix D.

Unless a step captures a value, reread it when used. A callback can change the
selection, owner, controls, names, rules and window state. Many helpers draw the
canonical city window even when another receiver was used for the preceding
calculation. There is no transaction restoring earlier mutations on cancellation
or failed drawing. Ordinary cleanup is not an invented exception guarantee.

Use F01's wrapping integer arithmetic, truncating signed division and arithmetic
shifts. Signed views and publication conversions are required where their results
affect information or state; original field sizes/offsets belong in the appendix.
Unknown reads require established backing, supplied retained state or a stop.
[X01's exclusion](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
applies to every out-of-bounds text, region, scene, array and control write.
Retain preceding valid effects, then reject, omit or stop before the overwrite.
A declared restrictive profile may reject unsupported inputs earlier. Mapped
adjacent storage does not make corruption part of this interface contract.

## 2. Window lifetime, selection and refresh

### Initialization and opening

Prefix initialization clears hit-region count, sets hidden, clears suspended
and redraw-suppressed, selects no city, sets improvement scroll=0/count=1, and
clears only the Proceed control reference. It does not initialize every other
field. Runtime initialization additionally clears update-scope state and
background origins, invalidates the two cached origins, and clears suspended.

Restore saved F06 view preferences when restore-player and the saved main-window
state selector are nonzero. Expose Close, refresh, suspend/restore, city actions
and P05 command delivery. Save relevant view preferences when the user changes
them. Native creation, frame sizes and callback registration are in Appendix D.

`openCity(receiver,c)` begins with a special gate **only while city processing is
active**: query transport flush, then P05's ordered eighteen mutation guards.
A nonzero flush or guard returns without queuing, selection or visibility changes.
Outside city processing this gate is skipped.

Then, in order:

1. Set the saved game's city-open flag; store the full requested city into the
   receiver; publish C06's production snapshot for the selected city.
2. If hidden, set central mode Units/Trade, scroll=0/count=1; if suspended, show
   the native window. After any show callbacks, unconditionally clear hidden
   and redraw-suppressed.
3. If now suspended, request native restore. Otherwise update the native window,
   then show it. Do not move either operation before the state stores.
4. If shared pending-Proceed-refresh is nonzero, rebuild Proceed with present=1,
   then clear that shared flag. Refresh the Autobuild-sensitive Change button
   with present=1. Clear Proceed-requested after it returns.
5. During city processing, test tutorial advice for the **input city c**: either
   Tutorial is enabled and Disorder is set, or the prolonged-disorder flag is
   set and difficulty is below King. Also require signed owner=local observer
   and no autoplay/suppression. Show `DISORDER`; if its shared dialog result is
   nonzero, show `DISORDER2`, then `DISORDER3`. Finally pump the P04 general modal
   service while receiver is not suspended, not hidden and Proceed-requested=0,
   testing in that order after each iteration. There is no independent timeout.
6. Outside city processing, Tutorial plus signed size>=4 plus missing CityStuff
   tutorial flag checks signed owner. **Owner different from local observer**
   shows `CITYSTUFF`, then ORs its flag into the live post-dialog tutorial state.
   Do not reverse that owner comparison based on the intended help text.

Opening does not universally validate the input city before its reached reads.
Its full original return is branch-dependent, not a Boolean success code;
ignored returns need no invented value. Appendix A describes observed returns
and supplied incoming state for callers that consume them.

### Hiding, closing and release

Hide sets hidden, hides the native window, then selects no city, focuses and
raises the main window, and prepares effects through P05. Native hide callbacks
therefore run before the no-city store. It returns zero.

Close first queries the selected city. None returns with no sound or updates.
Otherwise play effect 99. In city-modal mode 2 set Proceed-requested; in every
other mode hide the window. Refresh open auxiliary reports with selectors
1,2,4,5,6 in that order. This is distinct from a direct Hide, which does not add
sound or report updates. When a report selector does not match an open report,
its refresh has no presentation effect.

Hide-if-deleted tests the receiver's live selected city twice, `<=c` then `>=c`.
A match invokes the **canonical** Hide operation. Raise-if-visible returns for
hidden or suspended, otherwise raises the receiver's native window.

Release first sets suspended, then retires interface controls and display
resources. Native deletion order and stale widget references are reference
behavior; safe cleanup must preserve preceding game mutations and pending
semantic completion rather than invent a successful game action.

Begin-update-scope stores Hidden or Visible according to the current hidden
state; then independently retests hidden and sets pending-Proceed-refresh only
when visible. The hidden path leaves its prior shared value. End-update-scope
captures whether the state was Visible, clears the state, rebuilds Proceed(1)
only when that captured condition holds, then clears pending-Proceed-refresh.
The rebuild observes the already cleared scope state.

### Refresh variants

These operations are deliberately different:

| Operation | Guards and ordered effects |
| --- | --- |
| Full refresh | Require selected>=0, selected<live city total, live identity nonzero and city drawing enabled. Suspended or hidden refreshes only title. Otherwise C03 calculate(force=1), prepare layout, title, select receiver drawing target, caption/status, background margins, pane rectangles, draw-all(0), present whole receiver. |
| Partial update(flag) | Return if hidden, suspended, redraw-suppressed or selected<0. Calculate(force=1) for receiver's selected city, then draw **canonical** Top(1,flag), Resources(1), Central(1). No independent upper-bound/identity check here. |
| Refresh matching city(c) | Same visibility/suppression gates, then require selected=c and c>=0; full refresh. |
| Refresh near(x,y) | Same visibility/suppression/nonnegative gates; read selected city's y, then reread selection for x. W01 weighted distance<=2 invokes partial update(0). |
| Unit-change(u,oldX,oldY), u<0 | Same gates; if selected city is at old coordinates, calculate(0), Central(1), Production(1). |
| Unit-change with u>=0 | If selected city is at unit's live coordinates or old coordinates: calculate(0), Central(1), Production(1). Then reread unit home and selected city; if equal, calculate(1), Support(1), partial update(0). |

Draw-all has the partial-update visibility gates, then draws Top(present,0),
Resources, Food, Production, lower-right background, Support, Improvements,
Central in that order. Each child observes its own live state. A failure or
callback suppression does not undo earlier panes or simulation recalculation.

Title construction starts with the city label, space and the receiver's current
city name. Only when the **canonical window's** scale>1 append comma, P04 year,
then comma/population label/space/P04-formatted city population for ordinary
play. The city query sums integers1 through signed city size and takes at least1;
use P04’s digit/grouping rule directly, with no negative-quantity civilization
fallback. In an active scenario, instead append the objective label and optional
`x`/count when objective display is enabled and V08's count is nonzero. Within the same scale>1 branch,
append space and parenthesized Treasury label, colon and live signed owner's
gold. Population/owner queries reread the selected city. Set the bounded caption,
draw status and present the title strip using its live bottom boundary.

## 3. Layout, controls and cached backgrounds

A redesigned city interface may arrange its controls and panes freely. Preserve
semantic Next/Previous, Buy, Change, mode, Rename, City View and Proceed actions,
their guards in §§4–6 and the required refresh/query effects in §2. Source asset
metadata is interpreted by P03; cached backgrounds, scaling and hit geometry are
not gameplay state.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 4. Hit regions and command delivery

An input adapter resolves a gesture to a semantic city action and its identified
tile, citizen, unit or improvement. It must not select a different target because
new hit rectangles or display sorting differ. The original region collection
and its pixel tests are reference implementation details.

Mouse entry queries transport before P05's eighteen mutation guards, omitting
input-busy. Blocked input logs first, then publishes deferred CityMouse with
fresh selected city, a **second selection's** signed owner, x, y and original
button flag. N02 owns delivery. Ready input performs the hit query and routes:

| Region | Action |
| --- | --- |
| Resource tile | C02 tile click with original x/y. |
| Citizen | C02 citizen click with region ID. |
| Present unit | Local-unit dialog in §6. |
| Improvement | C06 selling command with region ID. |
| Supported unit | Supported-unit dialog in §6. |
| Other | No action. |

Left and right callbacks supply flag0/1 respectively, but this ready dispatch
does not test that flag. It does not add a blanket city ownership check; each
child owns its permission checks. P05 independently handles key/character routing.

## 5. City cycling and direct controls

Previous/Next first query selection; None returns. A second selection's signed
owner must equal local observer, unless score-lock is set. During city processing
show P05's city-modal notice and return. Otherwise capture current city, initialize
wrap choice to it and nearest to absent, and copy the comparison sentinels and
current name.

Scan live city slots ascending. Require identity and candidate's signed owner
matching the **freshly read owner of the captured current city**. Compare names
with classic case-insensitive CRT ordering. Next chooses the strict smallest name
above current, Previous the strict greatest name below current. Independently
track the strict smallest/largest name for wrap. Equal names never replace an
earlier winner or qualify as a next/previous name. The initial nearest sentinels
are the literal strings in Appendix A, not mathematical infinities; unusual
custom names can therefore fall through to wrap.

Open nearest if found, otherwise wrap if it differs from the captured current
city. Otherwise leave selection unchanged. No new owner/identity check follows
the comparisons. Oversized comparison-string copies are excluded writes.

Central/Support Map/Happiness buttons require selection not None, then store
mode, calculate(force=0) for a freshly queried selection, draw Central(1), and
play effect99. They have no added ownership or network gate. Improvement scrolling
stores the full supplied signed position, calculates(0), then draws Improvements(1).

Rename requires selection not None and signed selected owner=local observer;
score-lock does not bypass it. Only afterward query transport/mutation guards.
Blocked: log, then defer Rename with fresh selection, a second selection's signed
owner and original command. Ready: clear production-dialog timeout armed,
install the production idle callback, and show `RENAMECITY` with length15 and
current name. Result0 plus a still-selected city plays effect104, copies output
to the **post-sound selection's** name, full-refreshes, then redraws all maps for
the live observer with flag1. Both ordinary outcomes clear timeout armed. No
post-dialog ownership revalidation or old-name rollback is added.

City View requires selection not None, then the transport/guard scan. Blocked:
log and defer View with the same selection/owner/command ordering. Ready: invoke
the illustrated viewer for the freshly selected city. No additional ownership
or game-mode gate exists here. Buy, Change and selling retain C06's different
gates and mutation semantics.

## 6. Present and supported unit dialogs

Both controllers construct a prompt and sprite **before** testing their shared
busy flag. Busy entry destroys sprite then prompt and leaves the flag unchanged.
Otherwise set busy; require the currently selected city's signed owner=local
observer or score-lock; calculate(force=0) for a fresh selection. Normal finishing
clears busy before destroying sprite then prompt. A diagnostic stop does not
invent successful cleanup or undo a unit mutation.

Prepare three text parameters for a captured unit:

1. Civilization adjective, optional Veteran label, and live type name.
2. Capture x/y; append containing city name when present, then coordinates.
   Without a city, append parenthesized Near/nearest-city name when C01 finds one.
3. Reread home city after earlier text callbacks; format its name, with the
   absent-home value passed as absent to P04.

### Present-unit selection

Create a transparent64×32 preview before permission checking. Require central
mode Units/Trade after calculation. Read city y and x through separate selected
city queries, find top unit there, then take U01's stack position using the
**signed low word** of the region ID. A negative result finishes. Prepare text,
load `UNITOPTIONS` with Cancel, ignoring resource status; set shared unit-chooser
zoom8 and row renderer, append the unit preview, then these options in order:

| Choice | Inclusion |
| --- | --- |
| No action | Always. |
| Activate/clear order | Order is not None; also make it initial selection. |
| Sleep | Order is not Sleeping. |
| Fortify | Order is neither Fortifying nor Fortified. |
| Set home here | Live selected city differs from unit home. |
| Open home | Same test, then a fresh home read is not absent. |
| Disband | Always. |
| Activate; Activate and close | Owner's masked human bit set, current civilization nonzero, and movement remaining nonzero. |

Resources may add other choices. The full returned value is decoded through
wrapping subtraction; Appendix A gives values, including the otherwise
resource-supplied stack-reinsert choice.

- Clear order stores None; Sleep calls U04's order helper; Fortify stores
  Fortifying then consumes remaining movement; stack-reinsert calls U01 with1.
- Set home stores low-byte fresh selected city, full-refreshes and finishes.
  Open home passes its unsigned stored value to openCity and finishes, including
  an off-nominal absent value supplied by a custom response.
- Disband captures selected city, credits its signed shield stock with signed
  unit-type cost times unsigned shield-box rows divided by2, retaining signed16.
  Publish C06's snapshot for a **fresh selection**, delete the captured unit
  visibly with1, full-refresh, finish. No separate confirmation is inserted.
- Either Activate stores the captured unit as active, clears its order, sets
  movement mode View, then U04 focusActive(0). Activate-and-close additionally
  hides and finishes.
- All other outcomes, including cancellation and ordinary order changes,
  calculate(0) for a fresh selection and draw Central(1), then finish.

### Supported-unit selection

The region ID is already a full unit slot; do not narrow it to a stack ordinal.
After permission/calculation, set chooser zoom8 **before** creating the preview,
prepare text, load `CHILDCLICK` with Cancel, install the same row renderer, append
the preview and the Disband option. Interpret the modal result as signed;
nonpositive finishes without the present-unit dialog's trailing redraw.

Go Home stores GoTo order, then goto x/y through two independent selected-city
queries; finish without immediate movement. Disband captures the unit's location,
finds a city there, and if present credits that city's stock with the same
formula above. Publish the production snapshot only when the live selection
matches that city, using another fresh selection for the call. Regardless of
city presence, delete visibly with1, full-refresh and finish.

Any other positive choice validates the captured unit's live coordinates. If
invalid, finish. If U04 allows movement, make it active, clear order, set View
mode and focusActive(0). Otherwise enterView(0), select its current coordinates,
then center using **post-selection** unit coordinates. The close choice then
hides the city. Finish; there is no unconditional Central redraw here.

## 7. Pane drawing and resource display

Pane operations return when hidden, suspended, redraw-suppressed or without a
selected city. The central display chooses Units/Trade, Support Map or Happiness;
queries and mutations remain live at the specified operation boundaries.

Required information includes citizen moods/specialists, present/supporting units,
worked tiles and yields, food/shield stocks and production, trade supply/demand/
routes, corruption/waste/support, tax/luxury/science and improvements. C02–C08 own
those quantities; decorative packing must not overwrite their simulation totals.

- Citizen groups are Happy, Ordinary, Unhappy/Very Unhappy, then Specialists.
  Ordinary count is size minus happy, unhappy and specialists; split unhappy at
  unhappy minus excessUnhappy. C02 supplies specialist types and accepted actions.
- Present units follow the U01 stack at the selected city. Supported units are
  live records whose unsigned home equals the selected city, without adding an
  owner test. Preserve full unit IDs for supported-unit actions and stack ordinals
  for present-unit actions. A new viewport must not change those identities.
- Supply/Demand each contain three commodity IDs when the owner has Trade.
  Negative IDs are marked and use absolute magnitude for the name. Routes show
  destination and commodity; nonnegative commodity shows its yield, negative
  commodity is a food route with displayed amount −1.
- Resource panels show C03 totals, support/waste/loss and surpluses. Food need is
  signed size×foodEaten + roleFiveUnits×settlerFood; food surplus is totalFood−need.
  Shield surplus is totalShields−Support. Rates are tax/science×10 and luxury
  `(10−taxRate−scienceRate)×10`; amounts use uncompressed live totals.
- Improvements scan IDs0–66: IDs<39 use C08 presence; later IDs require wonder
  location equal to the live selected city. Preserve the list count/identity used
  by selling; visual scrolling cannot change an offered improvement's ID.
- Support Map terrain requires W01 validity and owner knowledge or unrestricted
  display. Its supporting-unit markers use home-city membership without an extra
  map-visibility test; the selected city's marker remains distinct.

The worked-tile display has a simulation side effect. Traverse the following
relative coordinates in their stated order, recomputing selected-city coordinates,
W01 wrapping and the first matching city-radius offset (fallback0). In its yield
pass, each worked tile invokes C02 accumulation `(city,radius,0)`, then consumes
Food, Shields and Trade shared temporary counters in that order by decrementing
them to zero after each semantic output step. Preserve relevant external events
between reads; do not replace this with an immutable snapshot or an extra C03
recalculation. Negative admitted counters retain F01 wrap and execution limits.
Screen icon spacing is separate from this required traversal/mutation.

Supported-unit information includes settler-role food cost and one Shield for a
support attribute. For an away attribute under captured government>4, start
penalty1 only when neither Women's Suffrage nor Police Station applies; add1
under captured Democracy. Otherwise the first away unit waives a nonzero penalty.
Then increment the away count. Show neutral when zero, unhappy count when nonzero;
do not let icon packing modify these quantities.

Happiness uses C04 stored stages0–4 and previous-stage0. Required stage admission
and previous-stage updates are retained below; icon geometry is optional.

Use this ordered list of relative map coordinates for both complete passes:

```text
(-1,-3),(1,-3),(-2,-2),(0,-2),(2,-2),
(-3,-1),(-1,-1),(1,-1),(3,-1),(-2,0),(0,0),(2,0),
(-3,1),(-1,1),(1,1),(3,1),(-2,2),(0,2),(2,2),(-1,3),(1,3)
```

- Stage0 always displays.
- Stage1 skips when Happy−Unhappy equals the previous stage's balance; otherwise
  set previous=1 and indicate luxury effects.
- Stage2 displays when Temple, Colosseum, Cathedral, Michelangelo's Chapel or
  ((Courthouse or Palace) and Democracy), tested in that order; otherwise skip.
  Set previous=2. Under Communism show the distribution bonus; otherwise read
  excess, unhappy, happy stage bytes in that order for the citizen information.
- Stage3 skips when its balance equals previous's; otherwise show military effects.
  It does **not** update previous.
- Stage4 compares stage3 with stage4, skipping equal balances; otherwise indicate
  wonder effects.

Stage2 improvement indications consider Colosseum, Cathedral (requiring
Monotheism, with Chapel overriding its displayed identity), Temple, then Palace
if present else Courthouse under Democracy. The original shows at most three,
qualifying a fourth before returning. Layout may show additional information
without adding simulation work.

For military effects, Communism includes the government name. Below Republic,
scan live unit identities at the selected city's live coordinates with nonzero
attack, without an owner filter; the original shows at most three, qualifying
a fourth before returning. Republic/Democracy start penalty1 only without
Suffrage and Police Station, add1 for a fresh Democracy read and return if zero.
Capture away count; when nonzero and a fresh government is Republic, subtract1.
Unhappy amount=away*penalty. New icon spacing must not change that amount.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 8. Production presentation and city notices

C06 owns eligibility, option order, turn estimates, purchasing, sales, selection
effects and the Change/Autobuild/Help/Freebie loop. This section owns the text,
art, advice panels and idle processing used by those flows. Do not recalculate
eligibility or production merely to paint a row.

### Production rows and advice

Format an eligible unit option as its name, ` |(`, C06's turn estimate,
`, `, the attack/defense/movement label, colon, signed attack, `/`, signed
defense, optional `*` for double defense against mounted units, `/`, signed movement divided by
unsigned movement multiplier, space, the hit-points/firepower label, colon,
signed hit points/10, `/`, signed firepower, `)`. Building options use
`name |(turn estimate)`; expired wonders prefix `*`. Use P04 formatting and
live rule values at each append, preserving division failures. Unit row value
is its type; building row value is improvement ID+62, wonder value is wonder
ID+101. Mark the captured current production as default when it matches.

Decode unit/building values using the mapping above. Freebie requires the live
cheat flag and game mode0 in addition to C06's parent gates. Clear timeout-armed
before and after the interaction. New row previews and list geometry do not
recalculate eligibility or alter production.

A02's chooser at a fresh selected city supplies military/domestic recommendations;
999 means absent. For Military, set substitution0 to the recommended unit name,
load `ADVICE/MILITARY` with flags8, form section `@`+unit name, append unit art
at zoom0, and load that section with flags32768, falling back to `@DEFAULT` on
failure; use width300. For Domestic load `ADVICE/DOMESTIC` with flags8. A
negative recommendation denotes the absolute improvement ID: set its name as
substitution0, use `@`+name for ordinary improvements and `@WONDER` for wonders,
and append its art at zoom0 if shared popup scale=0, otherwise8. A unit uses
`@`+unit name and zoom0 art. Load the chosen section with the same fallback
and modal width. The original bounded section scratch admits only fitting
names; overlong construction is excluded corruption. A safe profile must
explicitly reject/omit overlong advice or use a declared enlarged-string
extension, rather than claim the original overflow produced a valid section.

### Production idle processing

The same idle callback is installed by production, Rename and Buy dialogs.
If `(quitTimer && statusTimer<=0)` or selected city is absent:

- A nonserver clears the network active prompt's loop immediately.
- An unarmed server sets armed and samples game ticks as start time.
- An armed server samples ticks, then rereads start time; if signed
  `now > wrap(start+1200)`—strictly greater—clear armed and clear that loop.

Clearing first sets the prompt's close-request flag, then reloads the shared
network active prompt and its backend to end the loop. This is distinct from
P04's ordinary active-prompt reference. Regardless of gate or timeout outcome,
call N02 polling `(1,0)` afterward. There is no implicit timer reset on an
ordinary nongated callback, and no unsigned elapsed-time replacement.

### City preview prompt

Load the requested GAME resource, ignoring its load status as a new early-exit
gate. Present the selected city using P02's ordinary disclosure and run the P04
interaction with argument0. Capture its full result and publish its secondary
result to the shared city-dialog result. Native preview buffers, placement,
scaling and Sprite/Prompt destruction order are reference behavior in Appendix D.

### Turn notice and picture notice

The text/sprite turn-notice wrapper constructs Prompt before testing autoplay.
Autoplay, or neither local-notice permission nor force, destroys it and returns0.
Otherwise set substitution0 to city name, copy the section name to shared text,
and append `3` when section is `BUILT` and the city has Autobuild. Load GAME
status, then separately read owner, y, x and ensure that map location is visible.
If open-notice mode is clear, append the two city-response options with values1
and0; choose initial1 when the city-report preference is set, otherwise0.
If a sprite is supplied, append it and set its prompt sprite-scale setting to8.
Run modal0; a **signed positive** result
sets open-notice mode. Destroy Prompt and return the cached full result. Force
does not suppress these choice/publication effects in this wrapper.

The picture wrapper constructs Palette then Sprite before its autoplay gate;
an early return destroys Sprite then Palette. Without force, require local
permission, then ensure visibility using separate owner/y/x reads and set
substitution0 to the city name. Allocate and construct the compound picture
popup. If the supplied owner has Industrialization, increment the requested
resource with wrap. Select its absolute signed value, load the picture with
palette start10/count192 into the popup background (ignore loader result), read
surface height then width, and extract the whole image using transparency7.
Load GAME status and append the Sprite. Only without force and with open-notice
mode clear, append the two options and preference default. After modal0, only
a nonforced signed positive result sets open-notice mode. Destroy popup, then
Sprite, then Palette; return cached full result. A forced picture uses the
supplied owner and skips visibility, city-name substitution, options and mode
publication.

Preserve ordinary cleanup order and already completed effects on failure.
The preview wrapper owns its constructed local Prompt/Sprite for the original
exception cleanup. Picture setup owns raw allocation only during construction
and its outer Sprite/Palette; do not invent a completed-popup rollback on an
unrelated diagnostic stop. None of these wrappers validates arbitrary city or
asset indices by replacing them with a default city/image.

## 9. Illustrated city view

The illustrated city scene is cosmetic. Its art/layout/shuffling, placement,
resource composition, panning and native modal lifecycle may be replaced.
Retain city/owner identity, supported authored content, required information,
input busy gates and network processing; viewing must not change production,
city calculation or W02 world generation. Scene randomness is presentation-only.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 10. Conformance cases and joins

Validate city opening/closing and Proceed behavior, full versus partial C03
refresh effects, live selection and owner changes, city cycling, rename and unit
choices, C06 production/purchase/sale joins and cancellation after committed edits.
Check worked-tile temporary-yield consumption, information/visibility, production
idle expiry/network service and F06 saved fields with different layouts. Packed
icons, cached backgrounds and illustrated-city raster cases are optional.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## Appendix A. Evidence identity and original adapter

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

The behavioral target is classic MGE5.4.0f patch3. Primary source evidence is
OpenCiv2 revision `a71e8aefffe04e6f719e0e213b027b128a747711`:

- [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp):
  city window `00501440..0050dea8`, city-view `00454260..004565b0`, notices,
  production/purchase UI and name cycling. C02/C06 retain ownership of their
  simulation/actions in these ranges.
- [window.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/window.cpp):
  shared frame preparation `00552ed2`, caption control registration/release
  `00553169/005532d7`; P04 owns the common service.
- [controls.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/controls.cpp):
  specialized icon control creation/painting/input, including `005cb6eb`;
  primary’s replacement handler is in
  [fixups.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/fixups.cpp).
- [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h):
  names for rules IDs. In particular Trade is84, Espionage27; the P05 inspection
  gate and this window's supply/demand display query Trade.

Independent reconstruction `civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, supplies these evidence modules:

| Area | Files under src/ |
| --- | --- |
| Window and interactions | city_window.cpp, city_layout.cpp, city_refresh.cpp, city_close.cpp, city_navigation.cpp, ui_list_methods.cpp |
| Pane children | city_food_box.cpp, city_improvements.cpp, city_happiness_view.cpp, city_support_map.cpp |
| Production and notices | city_production_dialog.cpp, city_popup.cpp, city_status_popup.cpp, city_fixed_callback.cpp |
| Illustrated view | city_viewer.cpp, city_viewer_scene.cpp, city_viewer_input.cpp, city_viewer_tables.inc, city_viewer_geometry.inc |
| Shared services/adapters | city_icon_controls.cpp, map_window.cpp, map_input.cpp, window_keys.cpp; include/civ2/city_window.hpp and city_viewer.hpp |

Original `civ2.exe` SHA-256 is
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Appendix B contains complete city-view table values independently compared with
its original initialized data. Geometry starts at0061c360, improvement pools
0061cce0 and wonder pools0061ce58. These addresses identify validation evidence,
not required implementation pointers.

### IDs, resources and representation

Logical state may use different records/collections. For comparison with the
reference, city size/owner/production and route commodities/count are signed
bytes where stated; home-city values are unsigned bytes with255 absent; food
and shield stock are signed words; selected city, regions, geometry, calculated
yields and viewer counters are signed32. Resource seed is unsigned16 in the
viewer seed expression. Other arithmetic is signed32 wrapping unless a specific
unsigned test or narrow store is named.

| Adapter item | Original value or mapping |
| --- | --- |
| Central modes | Units/Trade0, Support Map1, Happiness2. |
| Update-scope state | None0, Hidden1, Visible2. |
| Buttons | Buy1, Change2, Units3, Support4, Rename5, Happiness6, View7, Exit/Proceed8, Next9, Previous10, improvement scrollbar99. |
| Control storage order | Buy0, Change1, Proceed2, Units3, Support4, Happiness5, View6, Rename7, Next8, Previous9, scrollbar10. Construction:0,1,4,6,7,5,3,8,9,10, then Proceed. Release:0,1,4,6,7,5,3,2,8,9,10. Scrollbar reference is not cleared afterward. |
| Hit groups | Worked map1, Citizen2, Present unit3, Improvement4, Production5, Supported unit6. Capacity200; each original record is rectangle, group, ID. |
| Present-unit options |0 no action;1 clear order;2 sleep;3 fortify;4 resource-supplied stack reinsert;5 set home;6 open home;7 disband;8 activate;9 activate and close. |
| Supported-unit response | Signed≤0 cancels;1 ordinary activation/navigation;2 same and close;3 Go Home;4 Disband. Other positive values follow ordinary activation/navigation. |
| Button labels | Buy35, Change36, Exit37, View38, Units39, Happiness40, Support41, Rename42, Proceed241, Automatic324. |
| Headings | Citizens403, Specialists63, Supported447, Improvements448, Resources450, Present451, Support Map452, Happiness453. |
| Display labels | Food box43; turns44/45; Food consumed64/surplus65/shortage74; Waste67; Shields68/shortage75/support204; Trade69/corruption70; Tax71/Luxury72/Science73; Supply86/Demand87/Wonders88/bonus93; food routes169/192; combat statistics170/171. |
| Unit-choice labels |0..3 use81..84; Set Home179, Open Home189, Disband180, Activate190, Activate-close191; supported Disband306. Veteran13, No Home14, Near178. |
| Notice response labels |32/33, values1/0; preference bit0x400. Prompt sprite-scale field0x130 is set0 for city preview,8 for turn/unit notice. |
| Game/city flags | Open sets game word006 bit2. Autobuild city0x10; disorder0x1, prolonged disorder0x100000; coastal0x80. Unit Veteran0x2000, away0x400, shield support0x800. Unit option star tests type flag0x0400 (double defense against mounted units). |
| Resource tile flags | C02 availability CityPresent8, HostileUnit4, legacy village2, OtherCityWorked16; unavailable overlay excludes CityPresent8 and Unusable1. |
| Orders | None255, Fortifying1, Fortified2, Sleeping3, GoTo11; movement mode View0. |
| Native viewer keys | Left a4/c2, Right a6/c3, Enter d0, Space d1, Escape d2 (hex); held native keys left64/25, right66/27. See P05 translation. |
| Viewer sheets | Building300, Wonder305, Houses310, overlay399; background340+5*layout+variant, cv.dll. CITY.GIF uses palette10/192 and transparency7. Viewer sheets/background palette10/236; overlay palette10/32. |
| Full citizens |44 sprites at00642d48: four groups of11; compact11 at00646a20. Kind0/2/4/6 plus parity; specialists type+7. |
| Large resource icons |00644f00: Food shortage0/normal1, Shield shortage2/normal3, Trade loss4/normal5. |
| Small resource icons |00645068: Food0, Shield1, Trade2. |
| Distribution and special art | Diamonds0/Coins1/Beakers2 at00648860; unhappy00646598, neutral support00648058, pollution006466c8, unavailable00640b98, pick terrain0063fcd8. |
| Improvement/wonder art | Improvement-indexed00645160; separate wonder images00645a84 for advice; sprite objects are original60-byte receivers. |

Palette indices and asset coordinates express visible classic output for the
supplied palette; they do not require byte-packed colors or those sprite-object
addresses. P03 owns extraction/scale/raster semantics; P04 owns label and text
formatting. An alternative UI may declare different visual conformance.

### Excluded bugs and unknown reads

The original uses200 hit rows, six caption icons,60 viewer assignments/sprites,
three city routes,31/24 pool entries per layout,80-byte navigation/advice name
scratch, and bounded title/name/message storage. Every excess logical-object
write is excluded, including a write mapped inside another valid record or the
same larger receiver. Raw independent adapters for viewer pools/scene assignments,
production counters and sprites do not override this exclusion. Clamp/reject/omit
policies must be declared; they are not recovered original overflow results.

Independent BF-031 and primary B31 synthesize the shield-waste x coordinate
when its only original producer was skipped (`0050394f..0050397e`, consumer
`00503b39`). This specification instead distinguishes supplied retained x from
a stop after prior effects. BF-022 safely skips mouse pan before any successful
cursor output; the original ignored failure and could read incoming POINT.x
(`00456438/00456544`). BF-143 returns on failed city-icon BeginPaint; the
original continued toward undefined paint data. These repairs are not original
successful-result oracles. BF-051 expands advice scratch to256; primary truncates
names. Neither establishes meaningful classic out-of-bounds string behavior.
C02 records the primary crowded-citizen hit-region repair; this spec retains
the bounded original spacing-width regions.

Unknown geometry, retained names, borrowed sprite state, failed provider outputs
and out-of-range **reads** need established or explicitly supplied backing, or
a stop. Do not infer initialized state from an allocated object, a mapped page
or a previous unrelated local variable. Original ABI residues (including saved
exception-chain return values) matter only to an adapter/caller consuming them;
callers that discard them need no invented zero return. This does not make
use-after-free, invalid native handles or any overwrite a required behavior.

## Appendix B. City-view asset data

These are canonical data inputs for pixel-identical classic composition, not a
required memory layout. Coordinates are source `(x,y,width,height)` or scene
`(x,y)`, not rectangle right/bottom. Coast/River/Inland mean layouts0/1/2.
A replacement asset set may use an explicit equivalent mapping. Preserve table
order and the mutable template copy at construction when comparing optional original RNG
and placement traces. Empty trailing pool entries are −1.

### Initial shuffled pools

Improvement pools, in index order:

```text
Coast: 17,11,28,2,37,18,27,26,12,33,19,25,13,20,52,24,14,6,5,16,15,50,23,22,21,30,29,-1,-1,-1,-1
River: 27,11,26,2,32,28,25,24,12,43,29,23,13,30,22,52,14,6,5,17,15,50,19,18,16,21,20,-1,-1,-1,-1
Inland: 59,58,57,34,24,10,23,1,36,25,22,21,11,32,26,20,12,27,15,49,13,5,4,16,14,47,18,17,19,29,28
```

Wonder pools, in index order:

```text
Coast: 43,0,40,10,36,34,44,3,41,38,9,45,8,49,48,51,42,7,4,39,32,47,46,31
River: 56,38,0,35,31,10,39,44,36,3,33,40,37,9,8,49,48,34,51,7,47,46,42,41
Inland: 9,56,42,39,35,55,2,8,7,43,33,44,40,37,41,6,51,50,48,38,46,45,31,30
```

### Building-sheet rectangles

| Improvement ID | x | y | width | height |
| --- | ---: | ---: | ---: | ---: |
| 1 | 373 | 333 | 123 | 82 |
| 2 | 249 | 250 | 123 | 82 |
| 3 | 373 | 1 | 123 | 82 |
| 4 | 1 | 333 | 123 | 82 |
| 5 | 125 | 84 | 123 | 82 |
| 6 | 497 | 1 | 123 | 82 |
| 7 | 125 | 1 | 123 | 82 |
| 8 | 249 | 416 | 357 | 78 |
| 9 | 1 | 1 | 123 | 82 |
| 10 | 125 | 167 | 123 | 82 |
| 11 | 373 | 250 | 123 | 82 |
| 12 | 373 | 167 | 123 | 82 |
| 13 | 125 | 618 | 123 | 82 |
| 14 | 249 | 167 | 123 | 82 |
| 15 | 249 | 1 | 123 | 82 |
| 16 | 1 | 84 | 123 | 82 |
| 17 | 497 | 167 | 123 | 82 |
| 18 | 1 | 416 | 123 | 82 |
| 19 | 125 | 333 | 123 | 82 |
| 20 | 249 | 333 | 123 | 82 |
| 21 | 249 | 84 | 123 | 82 |
| 22 | 497 | 333 | 123 | 82 |
| 23 | 497 | 250 | 123 | 82 |
| 24 | 497 | 84 | 123 | 82 |
| 25 | 0 | 0 | 0 | 0 |
| 26 | 373 | 84 | 123 | 82 |
| 27 | 125 | 250 | 123 | 82 |
| 28 | 1 | 250 | 123 | 82 |
| 29 | 1 | 618 | 123 | 82 |
| 30 | 1 | 499 | 220 | 100 |
| 31 | 590 | 499 | 105 | 105 |
| 32 | 1 | 167 | 123 | 82 |
| 33 | 125 | 416 | 123 | 82 |
| 34 | 222 | 499 | 367 | 118 |

### Wonder-sheet rectangles

| Wonder ID | x | y | width | height |
| --- | ---: | ---: | ---: | ---: |
| 0 | 1 | 576 | 158 | 114 |
| 1 | 1 | 116 | 158 | 114 |
| 2 | 407 | 852 | 94 | 160 |
| 3 | 253 | 852 | 76 | 133 |
| 4 | 478 | 1 | 158 | 114 |
| 5 | 478 | 116 | 158 | 114 |
| 6 | 1 | 691 | 304 | 160 |
| 7 | 160 | 461 | 158 | 114 |
| 8 | 478 | 461 | 158 | 114 |
| 9 | 319 | 461 | 158 | 114 |
| 10 | 319 | 116 | 158 | 114 |
| 11 | 478 | 231 | 158 | 114 |
| 12 | 478 | 346 | 158 | 113 |
| 13 | 1 | 231 | 158 | 114 |
| 14 | 1 | 461 | 158 | 114 |
| 15 | 319 | 346 | 158 | 114 |
| 16 | 160 | 346 | 158 | 114 |
| 17 | 160 | 576 | 158 | 114 |
| 18 | 1 | 346 | 158 | 114 |
| 19 | 1 | 852 | 125 | 253 |
| 20 | 319 | 576 | 158 | 114 |
| 21 | 160 | 1 | 158 | 114 |
| 22 | 319 | 1 | 158 | 114 |
| 23 | 160 | 116 | 158 | 114 |
| 24 | 160 | 231 | 158 | 114 |
| 25 | 1 | 1 | 158 | 114 |
| 26 | 319 | 231 | 158 | 114 |
| 27 | 478 | 576 | 158 | 114 |

### Scene positions and overlap order

| Slot | Coast (x,y) | River (x,y) | Inland (x,y) |
| --- | --- | --- | --- |
| 0 | 6,0 | 6,0 | 0,0 |
| 1 | 0,0 | 0,0 | 165,47 |
| 2 | 165,47 | 165,47 | 268,10 |
| 3 | 268,10 | 268,10 | 438,1 |
| 4 | 401,10 | 438,1 | 546,3 |
| 5 | 546,3 | 546,3 | 651,0 |
| 6 | 651,0 | 651,0 | 726,8 |
| 7 | 726,8 | 726,8 | 849,7 |
| 8 | 849,7 | 849,7 | 979,3 |
| 9 | 979,3 | 979,3 | 1115,3 |
| 10 | 1115,3 | 1115,3 | 59,107 |
| 11 | 59,107 | 59,107 | 170,98 |
| 12 | 170,98 | 170,98 | 268,96 |
| 13 | 268,96 | 268,96 | 370,97 |
| 14 | 370,97 | 370,97 | 513,63 |
| 15 | 513,63 | 513,63 | 460,120 |
| 16 | 622,50 | 460,120 | 622,50 |
| 17 | 370,97 | 622,50 | 585,103 |
| 18 | 96,155 | 585,103 | 675,113 |
| 19 | 192,154 | 675,113 | 775,104 |
| 20 | 291,151 | 539,150 | 871,102 |
| 21 | 460,120 | 536,197 | 958,115 |
| 22 | 585,103 | 775,104 | 1060,104 |
| 23 | 675,113 | 871,102 | 1156,104 |
| 24 | 775,104 | 958,115 | 370,97 |
| 25 | 871,102 | 1060,104 | 96,155 |
| 26 | 958,115 | 1156,104 | 192,154 |
| 27 | 1060,104 | 370,97 | 291,151 |
| 28 | 1156,104 | 96,155 | 539,150 |
| 29 | 539,150 | 192,154 | 536,197 |
| 30 | 536,197 | 291,151 | 657,156 |
| 31 | 657,156 | 1,213 | 780,156 |
| 32 | 780,156 | 110,260 | 915,168 |
| 33 | 915,168 | 209,226 | 1033,175 |
| 34 | 1033,175 | 332,226 | 1157,213 |
| 35 | 1157,213 | 13,298 | 1,213 |
| 36 | 1,213 | 170,291 | 110,260 |
| 37 | 110,260 | 293,291 | 209,226 |
| 38 | 209,226 | 4,364 | 332,226 |
| 39 | 332,226 | 131,356 | 13,298 |
| 40 | 13,298 | 253,356 | 170,291 |
| 41 | 170,291 | 657,156 | 293,291 |
| 42 | 293,291 | 780,156 | 4,364 |
| 43 | 4,364 | 915,168 | 131,356 |
| 44 | 131,356 | 1033,175 | 253,356 |
| 45 | 253,356 | 1157,213 | 451,257 |
| 46 | 451,257 | 451,257 | 572,257 |
| 47 | 572,257 | 572,257 | 732,251 |
| 48 | 411,324 | 411,324 | 679,284 |
| 49 | 533,324 | 533,324 | 842,260 |
| 50 | 732,251 | 732,251 | 411,324 |
| 51 | 679,284 | 679,284 | 533,324 |
| 52 | 842,260 | 842,260 | 369,390 |
| 53 | 369,390 | 369,390 | 863,333 |
| 54 | 924,276 | 806,317 | 796,317 |
| 55 | 898,296 | 973,242 | 1020,256 |
| 56 | 762,246 | 1117,365 | 1043,328 |
| 57 | 917,365 | 0,0 | 927,273 |
| 58 | 1070,319 | 0,0 | 1156,288 |
| 59 | 1176,304 | 0,0 | 1156,397 |

## Appendix C. Validation record

Historical research scaffolding included pinned source/body hashes, extracted
routines, explicit adapters, asset hashes, original instruction/table probes and
document checks. It is not included in this repository or required for
implementation.

A native 64-bit harness compiles **20 unchanged whole source bodies**, including
two access/arithmetic helpers, and passes **64 assertions** in optimized and
ASan/UBSan builds. Leak checking was disabled because the environment’s ptrace
prevents LeakSanitizer operation; address/undefined-behavior checks passed.
Static low-address sprite/region fixtures accommodate original 32-bit tokens;
the compiler explicitly permits their narrowing. These fixtures establish
neither the original ABI nor native 32-bit execution.

The supplied adapters provide rectangle writes, bounded slot backing, city/rule
state, advance-query logs, RNG range results, building/wonder drawing, storage
release, cursor/key samples and presentation logs. RNG returns the lower bound;
checks establish call/range consumption, not a second implementation of F01’s
generator. Built/empty scenes exercise all three layouts; extraction/raster,
actual resource loading and native modal providers are not executed. First
failed-cursor consumption is not used as an original-output oracle; the tested
keyboard-held and previously produced cursor cases agree with original ordering.

Source checks verify 25 pinned independent files, all 20 unchanged fixture bodies,
five primary files, 13 original instruction probes and 3092 city-view table bytes.
The probes cover signed size/seed operands, biased slot-range arithmetic,
conditional waste-coordinate production, captured shield surplus used by Trade,
high-byte key polling and width-for-height pressed painting. Full pane/resource
bar rendering, city modal actions/notices, native icon painting, setup cleanup
and shared-frame preparation are source-reviewed, not runtime-tested here.
Complete gameplay/UI/reentry conformance remains X02 work; original pixel
fidelity is a separate optional reference claim.

Asset identities used for this research:

| Input | SHA-256 |
| --- | --- |
| CITY.GIF | `96d45d56d41bdac1bd0b88650cd2338c613cf2d237417dbfccbe2c97c8f2dca9` |
| cv.dll | `f3b96918f09ac281b5480af548084264c845747fdc8a7e3b178df72e1a237c80` |
| ADVICE.TXT | `0f6a2220dd1afd5f57e22d442aa3f43cb23b8ed2dddb378e1537ea476f7db8cb` |
| Labels.txt | `b614ab5e34853a10a0d05f1d0b4a1a722936c9b3cd8e1bc75452858fa5fb1fdd` |
| Game.txt | `431132fc67b9f7b917bba0dc6f1f36c546cb55b88d43854bc4e11fc3b74c0ced` |

## Appendix D. Original presentation reference

This appendix retains the original presentation procedures removed from the main
specification. Geometry, pixel/sample output, native controls and cosmetic timing
are optional reference behavior under X01. Required semantic fragments were kept
in the numbered main sections and are linked where omitted here; this appendix
does not relax their simulation, information, file or protocol obligations.
Original section numbers and local § references identify the recovered procedure
family, whether its retained fragment is in the main section or here. Historical
native/pixel tests are reference validation, not baseline acceptance criteria.

### Original §3. Layout, controls and cached backgrounds

Let `L(v)` return v when layout scale is exactly 2; otherwise signed wrapping
`scale*v / 2`, truncating toward zero. The exact-2 shortcut avoids an unnecessary
overflow. Pane rectangle conversion is different: for each dimension/coordinate
use signed `wrap(scale*v+1)/2`. Compute height, width, y, x in that order, reading
scale for each; add background offset/origin at the stated coordinate step.
Do not replace these operations by floating-point scale or one rounding mode.

Default scale is 2, or 3 when screen width>=1000. Desired client width is
`L(636)+16`; height is `L(421)+L(24)+8`. Center using separate arithmetic halves
of main-client size and these desired dimensions. Only afterward add global
frame width/height to the stored outer size. Invalidate cached x origin before
publishing the saved rectangle.

Smaller decrements scale only when >1. Larger increments when <2, or when <3
and screen width>=1000; otherwise it does nothing. An accepted change recalculates
the rectangle, then moves using the live saved rectangle. Close caption icon
uses Close. Other icon commands have no effect.

Layout preparation captures border, surface height, then width. Start scale=2;
choose 3 for height>=border+663 and width>=2*border+950; afterward choose 1 if
height<border+441 or width<2*border+632. At scale<=1 use title font size12 and
caption height18; otherwise use L(16) and L(24). Replace the detail font with
`wrap(titleSize*2)/3`. Prepare the redraw surface through P04’s shared caption/frame service, then retain its content width
and height, reload the background, and center its scaled 636×421 area within
those content dimensions, independently using zero origin when it does not fit.
Create control font category1, size L(12), with the small-scale flag when scale<3.

If either origin differs from its cached counterpart, or cached x is negative,
rebuild controls. After the rebuild copy the **live** origins into the caches.
A scale change alone is not an extra rebuild predicate here; callers invalidate
or change geometry as specified.

Background reload always constructs temporary port, sprite and palette. When
canonical scale differs from the background cache, publish the new cache before
reset/load, load `CITY.GIF`, extract 636×421, reset the temporary port, allocate
scaled dimensions, draw with sprite scale `4*cachedScale−8`, then reset sprite
scale. Ignore the file loader's ordinary result. Destroy temporaries in reverse
order even on a cache hit. A failed load after cache publication does not restore
the old cache. Missing read/graphics outputs follow P03's contracts.

Pane rectangles, before conversion and origin/offset addition:

| Pane | x | y | width | height |
| --- | ---: | ---: | ---: | ---: |
| Top | 0 | 0 | 436 | 61 |
| Resources | 0 | 61 | 436 | 153 |
| Resource header | 7 | 65 | 188 | 137 |
| Food | 436 | 0 | 200 | 167 |
| Production | 436 | 167 | 200 | 189 |
| Lower-right background | 436 | 356 | 200 | 65 |
| Support | 0 | 212 | 192 | 78 |
| Support header | 7 | 216 | 181 | 69 |
| Improvements | 0 | 290 | 192 | 131 |
| Improvement content | 6 | 306 | 166 | 108 |
| Central | 192 | 212 | 244 | 209 |
| Central header | 197 | 216 | 233 | 198 |

Rectangle production follows this table's order. Background margins fill palette
color10: left/right when captured x origin>0, then top/bottom when captured y
origin>0. Capture each unused-side remainder before the first fill in its pair;
later dimensions and background offsets remain live.

Control rebuild disables traversal, releases old controls, constructs/publishes
ordinary buttons, icon controls and scrollbar, then captures base x/y from
background origin plus offset. Rebuild Proceed(0) first. Configure Buy; invalidate
the Autobuild cache and refresh Change(0); configure Next, Previous, Central,
Support Map, Rename, Happiness, City View, then the scrollbar. Setup callbacks
can replace later references. Each control attaches to the canonical city parent.

Button geometry uses L, relative to the captured base. Buy is (442,181,68,24).
Change uses that rectangle shifted by L(115) horizontally. Central is
(459,364,57,24), followed by Support Map and Rename with separate L(58) shifts.
Happiness is the next row, using L(25) vertically and two separately calculated
L(−58) horizontal shifts; City View follows by L(58). Proceed starts from the
Central rectangle but uses two separate L(58) shifts and L(25). Next is
(437,364,21,24); Previous follows by L(25) vertically. Rectangle-offset helpers
retain their P03 narrowing/wrap contract.

Proceed is recreated on every request. It uses Proceed label when update-scope
state is exactly Visible, otherwise Exit; register it as the default button and
bind Close. Change refresh captures the selected city's Autobuild Boolean. Only
when it differs from the shared cache: publish cache, delete old button, construct
and publish replacement, then use Automatic or Change label according to the
captured Boolean. Present only when requested. Cache publication precedes failures.

The improvement scrollbar uses native scrollbar width, height L(131)−2,
y=baseY+L(290)+1, x=baseX+L(192)+L(0)−width−1. Its page size is
`(existingImprovementContentHeight−SAR(L(1),1))/L(12)`, with the original signed
division failure boundary. The content rectangle here can be retained from the
previous layout: full refresh rebuilds controls **before** recreating pane
rectangles. Bind both scrolling callbacks to the same improvement-scroll action.

After setup, scale1/2/3 selects unit zoom −5/−2/1; other supplied scales retain
previous zoom. When zoom differs from its cache, publish it, allocate
`scaleMap(64,zoom) × scaleMap(32,zoom)`, select that sprite scale, draw the terrain
pick sprite at (0,0), reset scale. The cached surface supplies C02's quadrant
pixel reads; do not replace its pixels by corrected hit geometry.

#### Next/Previous icon controls

These two controls use a specialized pressed-image and event path. Configure
clears its private state, captures parent palette, forms the rectangle and
initializes control kind6; clear left/right callbacks. Save current sprite
scale, select1:1, allocate pressed then normal images, and fill both with the
configured background. Capture arithmetic halves of control dimensions. Save
sprite offsets, replace them with negative halves of content dimensions, draw
to pressed then normal at the captured center, restore offsets and scale.

Form local bounds using pressed surface height then width, each minus1.
Use the native full-width rectangle translation (not P03's narrowed engine
offset). Copy bounds, reduce bottom/right by2, preserve that source, and
translate destination(+2,+2). Fill pressed with live text-shadow color, copy
normal into the shifted pressed rectangle, then update pressed and normal
palettes using independently reloaded parents. Create/publish the native child
and install this specialized event procedure. Failed creation cannot justify a
write through a missing control-state object; stop before that invalid write.

City setup passes background23. After configuration, copy normal image bounds,
center the original sprite using truncating halves of bounds and sprite size,
draw normal there and pressed at(+2,+2). Outline both in10, then inset1 and
draw borders normal31/15, pressed15/10; inset1 and repeat. Set pressed palette
then normal palette using fresh parent reads. These additional draws do not
reuse configuration's temporary sprite offsets.

Every native event first obtains control state; missing state returns1. Read
its control reference before selecting the message. Focus gained returns0
when parent traversal is disabled, otherwise uses P04's shared control handler.
Focus lost redraws the control and returns0. All unhandled messages use that
shared handler. Event-specific behavior is:

- Paint begins native painting, copies control bounds in left/top/right/bottom
  order, validates the parent's rectangle, translates to local coordinates,
  then validates the child. In palette mode1 select its parent's palette.
  If live pressed state=0, acquire Normal, update its palette, realize only in
  mode1, copy its full width×height and release storage. Then independently
  test live state=1: acquire Pressed, update palette, realize in **every** mode,
  copy width×**width** (the original uses width for height too), release. A
  callback can therefore cause both branches. End painting; if focused, recurse
  through focus-gained. A failed BeginPaint needs supplied valid provider output
  or a stop before its first unknown consumption; the later repair's early
  successful return is not evidence of an original paint result.
- Space key-down with traversal enabled sets pressed1, redraws, sets pressed0,
  busy-polls while Space's entire high key-state byte is nonzero, redraws and
  notifies left-click. Other keys forward unchanged to the parent.
- Left or right mouse-down sets tracking1, focuses and captures. Right-down
  returns immediately; left-down continues as movement. Movement while tracking
  reads x/y as unsigned halves of native lparam, converts control bounds to local,
  and changes pressed to the half-open point-in-rectangle result; invalidate
  only when it changed. Movement without tracking returns0.
- Either mouse-up without tracking returns0. Otherwise release capture,
  clear tracking, obtain unsigned x/y and local bounds, invalidate, then clear
  pressed **after** invalidation callbacks. If the point is inside, invoke the
  Paint branch directly and notify that button's left/right callback.

Notification publishes the current parent as the shared control parent, obtains
control ID, tests the callback for nonzero and rereads it before calling. The
Next/Previous callbacks ignore the supplied ID and enter §5 navigation. Normal
callback return values do not change the native event's return0. A direct
callback-dispatch helper returns the child result, or the incoming control token
when no callback exists; tokens have no required representation in a new UI.

### Original city hit-region storage and geometry

Hit regions are ordered half-open rectangles with a group and ID. First match
wins; overlap is not resolved by smallest area or most recent insertion.
Clearing changes only the count. Append refuses count>=200; otherwise capture
index, increment count, write rectangle, ID, then group. Removing an index shifts
later rows forward, preserving order, then decrements count **unconditionally**.
Thus even an out-of-range index can change count without a copy. Group removal
walks backward. Remove-first scans forward for the first matching group and ID,
removes that index, then returns. Writes outside the logical collection remain excluded bugs;
scalar count changes within its own field remain observable.

A hit query scans against the live signed count, tests left<=x<right and
top<=y<bottom, then writes ID before independently reading/writing group. Output
arguments may be absent; a miss leaves them unchanged. This ordering matters
when output storage aliases a field. It does not permit an invalid destination.

### Original §7. Pane drawing and resource display

Each public pane renderer returns immediately when hidden, suspended,
redraw-suppressed or selected city is negative. Preparation selects the graphics
target, text target and control font, then restores that pane's background.
The background source is the pane rectangle translated by negative layout
origin and background offset. Read height, width, y and x in that order.
Present only that rectangle when the caller requests it. Internal Central
helpers rely on their parent gate: restore Central, then reread mode and draw
Units/Trade, Support Map or Happiness. An unknown mode only restores/presents.

Here `L` is §3's layout scaling; `M(v,z)` is P02 map scaling at zoom `z`.
`half(v)` means arithmetic shift right by one. `spacing(n,w,e,&a)` is P03's
icon-spacing helper, including its adjusted-count output `a`. Nonspecialist kinds are Happy0, Ordinary2, Unhappy4, Very Unhappy6. All sums,
products, negations, counts and coordinates use F01 wrapping arithmetic;
division truncates toward zero. Asset/color identifiers are mapped in Appendix A.
Read each `L` call separately; a drawing callback can change scale. A named
local is captured until explicitly replaced. Workspace values and selected-city
fields remain live at their stated uses.

#### Citizens and hit regions

Citizen art style queries the supplied owner in this short-circuit order:
Electronics and Automobile =>3; otherwise Industrialization =>2; otherwise
Invention and Philosophy =>1; otherwise0. Full citizens use style-specific
art; compact Happiness citizens use their own common glyph set.

Select sprite scale `4*layoutScale−8`. Compute spacing from signed city size,
glyph width `L(27)+1` (compact: `L(15)+1`), and caller extent. Draw happy
citizens, then `live size−(captured unhappy+captured happy+live specialists)`
ordinary citizens, then unhappy citizens; for the latter use unhappy kind
until index reaches `unhappy−excessUnhappy`, then very-unhappy kind. A parity
counter toggles across those three groups. Finally iterate the live specialist
count and use C02's specialist type. Negative loop bounds perform no iterations.
Full art draws shadow at `(x+1,y+1)` in color10 and then body at `(x,y)`;
each of those calls independently queries owner/style and, for specialists,
type. The glyph index is `style*11+kind+parity` for nonspecialists and
`style*11+type+7` for specialists. Compact art draws once and has no owner
style lookup. Advance x by captured spacing after each citizen. Reset scale;
return spacing, without restoring an arbitrary previous scale.

Top preparation removes group2. Draw the two headings at relative `(L(2),L(46))`
with width `L(198)` and `(L(199),L(46))` with width `L(238)`, style124/18.
Capture happy, unhappy and excess counts before selecting sprite scale; draw
citizens at `(L(5),L(9))` over width `L(422)`. Then iterate a **fresh signed
size** and register group2, ID=index, from that same starting point with
width=returned spacing and height=`L(30)`. The crowded layout's hit width is
spacing, even when it exceeds visible glyph width. The second public argument
does not alter this rendering.

#### Present units, pollution and trade

Capture Central left/top, unit zoom, `M(64,z)` width, `M(48,z)` height,
`M(64,z)` horizontal pitch and `M(52,z)` row pitch. Compute columns as
`L(244)/pitch`, horizontal margin as `(L(244)−pitch*columns)/2` (truncating), padding
`M(4,z)` and nominal slot count `columns*4`. Remove group3. Obtain the top
stack at separately read selected-city y then x and count it using U01 mode2.
If count≤columns, add half row pitch to padding and draw the Units Present
heading in style124/18 at its header top+`L(1)`. Otherwise omit that heading.

Draw stack units with flags4. In the first two rows, draw their home city name's
first three bytes (or the no-home label) in style10/26 at unit y+`L(0)`+height.
The home label is centered within captured unit width. The text terminator after byte3 does not clear later shared text bytes. At row2,
restart at top+padding+half row pitch, shift initial x by half horizontal pitch,
and decrement columns. At row4 stop without querying the next stack member.
After drawing, make a separate grid pass, registering group3 IDs from0 through
nominal slot count−1, subject to the same row4 stop. This pass does not limit
regions to the number of units actually drawn. Hit width is unit width; first
row height is unit height−3 and other row heights are full unit height.

If live pollution count is nonzero, select scale `4*layoutScale−8` and draw its
icon row at `(left+L(7),top+L(100))`, passing separately reread count as both
count and total, width `L(64)`, extent Central width−`L(14)`, flags0; reset.

Trade text begins `(left+L(7),top+L(133))`, row pitch=font line height−2,
style121/18. If the city's owner has Trade, draw Supply then Demand, each with
exactly three comma-separated commodity names after its label and colon.
Test each signed commodity byte for negativity; if negative append `(`,
then reread the byte and use its absolute magnitude for the name, then `)`.
Advance one row after each list. Add `L(2)` before routes. Iterate the **live
signed** route count. Append destination name, space, then commodity name or
the food-route label for a negative commodity, then colon. Reread commodity
sign: nonnegative appends `+` and the live route yield; negative appends `-1`.
Draw text, then the corresponding Trade or Food icon at text end+`L(2)`,
y+`L(3)`, selecting/resetting scale for each icon. Advance the captured row
pitch. Off-nominal counts need supplied readable backing beyond the three
logical routes; there are no invented extra routes or zero values.

#### Worked-tile map and yield icons

Capture Resources left/top, tile width=`M(64,live unitZoom)`, height=
`M(32,fresh unitZoom)` and their arithmetic halves. Start at `(left+L(5),
top+L(11))`; remove group1 and register one region at start x, start y+half
height, width `4*tileWidth`, height `4*tileHeight`. The center tile's draw
origin is start+`(3*halfWidth,4*halfHeight)`.

[Required behavior is retained in the main section.](#7-pane-drawing-and-resource-display)

For each entry, read selected-city coordinates afresh, wrap x with W01, and
derive pixels using the captured half dimensions. Find the first matching W01
city-radius offset; use radius0 if none matches. Draw P02 terrain through the
primary map renderer, targeting this window, with live signed owner, live unit
zoom and that radius. Use C02’s live availability flags at that radius. For city/unit overlays
subtract half tile height from pixel y: city flag => query city there and, if
nonnegative, draw it with flags1; otherwise unit flag => find the first
stack member whose attack byte is nonzero and draw it with flags7; otherwise
village flag => village art at the unshifted tile pixels. Reread availability
flags and draw unavailable art at unshifted pixels when OtherCityWorked is set
and neither CityPresent nor Unusable is set. The historical village-bit test
is retained even though the ordinary C02 availability producer does not set it.

The second pass recomputes coordinates/wrapping/radius even where coordinates
are subsequently unused. For a worked tile, select scale `4*layoutScale−8`
and invoke C02 tile-yield accumulation `(city,radius,0)`. Sum the three shared
yield counters; if zero, remember empty and set count1. Calculate spacing with
extent=`tileWidth−2*L(8)`, icon width=`L(10)+1`; then independently recompute
those two expressions and divide extent by icon width for capacity. If
count≤capacity, replace count by capacity. Start icons at tile x+`L(8)`,
tile y+half height−half(`L(10)`). For Food, Shields and Trade in that order,
while the **live counter is nonzero**, draw an icon, increment column, advance
x by spacing if column<count, otherwise reset x/column and increase y by
`L(11)`. Decrement that live counter **after** the drawing/scaling callbacks.
Do not clamp negative counters: their wrapping decrement and eventual stop
are the specified arithmetic, subject to the input profile's execution limits.
If empty, draw the empty-yield unhappy glyph. Reset scale after each worked
tile. Thus rendering consumes shared temporary yields; it is not a pure read.

Select the control font and scale `4*layoutScale−8`, then draw the Resources
heading at its header bottom−`L(14)`, style124/57. Continue into the four bars
below in order. Their icon counts are presentation packing, not replacements
for C03's simulation totals.

#### Food and shield bar packing

All bars begin at `x=Resources.left+L(205)` and nominal width `B=L(226)`.
After icon drawing, each bar recomputes label left=`left+L(205)` and
right=`labelLeft+L(226)−1`; do not reuse the earlier icon-row x/width after
callbacks. Centered labels obtain fresh `L(226)` extents.
Use `I=L(14)+1` as icon width. Capture `large=(layoutScale==3)` before Food;
publish row height `L(14)+L(2)` at each bar. Drawing helpers use live scale.

Food packing:

```text
needed = signedSize*foodEaten + roleFiveUnits*settlerFood
used = needed; produced = totalFood; S = produced-needed
n = max(needed,produced); common = S<0 ? produced : needed
extent = B-L(2)
if (S<0 && produced!=0) || (S>0 && needed!=0):
    extent -= L(4)+I; n--
pitch = spacing(n,I,extent,&adjusted)
while adjusted<n:
    n--
    if produced<used: used--
    else if produced==used: produced--; used--; common--
    else: produced--; common--
surplusWidth = S==0 ? 0 : produced==0 ? L(226)
                         : L(2)+L(1)+(abs(S)-1)*pitch+I
usedWidth = B-surplusWidth
surplusX = x+usedWidth
if S<0: usedWidth+=surplusWidth; surplusWidth=0
```

Set y=top+`L(14)`, subtract1 when **live** layout scale is3. Fill used width
with45, positive surplus width+`L(6)` with48. Icon y is y+captured large+
`L(1)`. Draw `common` positive Food icons from x+`L(1)`; draw `abs(S)` surplus
icons from captured surplusX+(`common<1 ? L(1) : half(L(4))`), using positive
or shortage Food according to S. Labels are above at top+`L(14)−L(14)`.
Left is label64 and `common+outgoingFoodRoutes`; if incoming differs from
outgoing, append the parenthesized signed difference and route label169.
Right is shortage label74 or surplus65 and `abs(S)`. Styles are42/10 on the
left,11/29 for shortage,46/10 for positive surplus.

Shields use the following distinct reduction; `Waste` and `Support` below are
live workspace reads wherever written with capitals:

```text
support = Support; shields = totalShields+Waste
S = totalShields-Support
n = max(Support,shields); common = S<0 ? shields : Support
extent = B-L(2)
if (S<0 && shields!=0) || (S>0 && Support!=0):
    extent -= L(4)+I; n--
if Waste!=0: extent -= L(4)+I; n--
pitch = spacing(n,I,extent,&adjusted)
while adjusted<n:
    n--
    if shields-Waste<support: support--
    else if shields==Waste: break
    else if shields-Waste==support: shields--; support--; common--
    else: shields--; common--
wasteWidth = 0
if Waste!=0:
    wasteWidth = (Waste-1)*pitch+I
    if S!=0: wasteWidth += half(L(4))
    if shields!=Waste: wasteWidth += half(L(4))
surplusWidth = S==0 ? 0 : shields==0 ? L(226)
                         : L(2)+L(1)+(abs(S)-1)*pitch+I
usedWidth = B-wasteWidth-surplusWidth
if wasteWidth!=0:
    usedWidth = Support==0 ? 0 : L(2)+L(1)+(Support-1)*pitch+I
    wasteWidth = B-surplusWidth-usedWidth
wasteX = x+usedWidth; surplusX = wasteX+wasteWidth
if wasteWidth!=0:
    wasteIconX = wasteX+half(wasteWidth)-half((Waste-1)*pitch+I)
if S<0: usedWidth+=surplusWidth; surplusWidth=0
```

Set y=top+`L(120)`, subtract1 when live scale3. Fill used84, waste11, positive
surplus width+`L(6)` with92. At y+large+`L(1)`, draw `common` Shield icons from
x+`L(1)`, then positive **live** Waste shortage icons from wasteIconX+`L(2)`,
then `abs(S)` surplus/shortage shields from surplusX+the Food rule's inset.
Capitalization substitutes Coins for the latter regardless of S's sign.
Only nonzero widths cause fills; negative widths still reach P03. The original can reach positive Waste with no `wasteIconX` producer, including
ordinary inputs. Require supplied retained coordinate or stop at its first use
after preceding fills/common icons; do not adopt the reference repair as
classic output.

Label y=top+`L(120)+L(14)+L(2)+L(−1)`. Left label204 displays
`min(reduced shields,live Support)` in84/10; center Waste67 displays live Waste
in11/29 when nonzero; right label75 if S<0 else68 displays `abs(S)` in11/29 or
92/10. A positive S also performs the original production read, even though
its result does not change this label. Retain captured **shield S** for Trade.

#### Trade and distribution bars

Trade starts with cached total=live total Trade, n=that total, extent=B−`L(2)`.
If live Loss≠0 subtract `L(4)+I` from extent and decrement n. Compute spacing.
While adjusted<n, decrement cached total and n; later icon loops still use live
totals. Loss width is zero when Loss=0, otherwise
`L(2)+L(1)+(abs(Loss)−1)*pitch+I`. Used width=B−loss width; capture
lossX=x+used width. Set y=top+`L(55)`. **If captured shield S<0**, merge loss
width into used width and clear loss width. This condition is not trade balance.
Fill used118 and loss121. Draw live positive `(totalTrade−Loss)` normal Trade
icons from x+literal1, y+large+`L(1)`, then loop index<live Loss drawing loss
icons from lossX+half(`L(4)`). Negative Loss enters its nonzero branch but
produces no positive-loop iterations. Labels above at top+`L(55)−L(14)`:
left69 with live net Trade, style118/10; under Communism with nonzero
distribution bonus, append label93 and bonus at text end+`L(4)`, style121/10;
right70 with live Loss, style121/10.

Distribution performs the otherwise unused total-Trade read, then captures
tax, luxury and science. Let n be their sum and categories the number positive.
Start extent=B−`L(2)`; while categories>1, subtract `I+L(4)`, decrement n and
categories. Compute spacing and adjusted count. While adjusted<n, reduce tax
if strictly greater than both others; otherwise reduce science if greater than
luxury; otherwise reduce luxury. Increment adjusted each iteration. Thus ties
are reduced in order Luxury, Science, Tax.

For a nonzero packed tax/science count, its width is
`L(1)+(count−1)*pitch+I`, otherwise zero. Science x=x+B−science width.
Luxury extent=B−tax width−science width, less `L(4)` for each nonzero neighbor.
Luxury x=x+(tax width≠0 ? tax width+`L(4)` :0)+half(luxury extent), less
`half((luxury−1)*pitch+I)` when luxury≠0. Normally alignment=Center; if luxury
≠0 and tax=0, align Luxury left and set its x=x; otherwise if luxury≠0 and
science=0, align Luxury right and set its x=x+B−`[L(1)+(luxury−1)*pitch+I]`.

Set y=top+`L(79)` and fill B in121. At y+large+`L(1)`, draw positive packed
tax Coins from x+`L(1)`, Luxury Diamonds from its x and Science Beakers from
its x; nonzero negative luxury/science enter their branches but draw no icons.
Labels below at top+`L(79)+L(14)+L(2)+L(0)` capture signed owner and use live
rates/totals. Tax percentage is rate*10; Science likewise; Luxury percentage
is `(10−taxRate−scienceRate)*10`. Each label is percentage, `% `, its resource
name, colon and **live uncompressed** total. Tax style118/10 is left unless
Luxury aligned left, then centered; Luxury style41/10 uses its chosen alignment;
Science style94/10 is right unless Luxury aligned right, then centered.
Reset sprite scale and optionally present Resources.

#### Food stock and production stock

Food title uses label43, style44/10, centered at pane top. Place a box relative
`(6,15,183,147)`, scaling each component with L and inset by `L(3)` on both
axes. Horizontal pitch=spacing(signed size+1,`L(17)`,width); vertical pitch=
spacing(live food-box factor,`L(14)`,height). Recompute used width with fresh
size+1; if smaller than width, inset horizontally by half difference. Used
height=vertical pitch*live factor; if smaller, lower bottom by the difference.
Inflate by **unscaled**3, border44/57, inset1 then2. Capture signed food stock
and initial coordinates. Select scale `4*layoutScale−8`; while stock>0 and
row<live factor, capture perRow=fresh size+1 and count=min(perRow,stock), draw
Food row with count, **fresh** size+1 total, width `L(17)`, current box width,
flags1. Subtract captured count, increment row and y by captured vertical pitch.
Reset. If Granary, otherwise Pyramids queried with unsigned owner, draw the
storage line at box.top+half(live factor)*pitch−`L(1)`, from left+`L(2)` to
right−1−`L(2)`, color42. Present if requested.

Production caches width `L(183)` and height `L(146)` and adds a group5 region
over its top `L(40)`; the city mouse dispatcher has no group5 action. A
nonnegative signed production reads type afresh, signed unit cost and signed
owner, prepares the preview, and draws P02 unit flags0 to the **canonical city
window** at pane `(L(73),L(1))`, zoom−1 except scale1=>−5, scale3=>1.
A negative production rereads its sign, then independently its value for the
positive or negated magnitude branch; use that captured magnitude and unsigned
improvement cost,
centers the fresh name in84/10 and draws its improvement/wonder icon to the
canonical window at `(L(80),L(16))`, scale `4*layoutScale−8`. Reload production
after these calls; Capitalization skips the stock box.

The production box is `(L(6),L(40),cached width,cached height)`, inset by
`L(3)`. Vertical pitch=spacing(10,`L(14)`,height). Per-row count=cost; if cost<10,
replace it by live shield-box factor and reduce bottom by `(10−cost)*pitch`.
Horizontal spacing uses live factor, `L(17)`, width and adjusted-count output.
Used width=`pitchX*fresh factor+adjustedCount`—the final term is the helper's
count, not a remainder. If used width<width, capture half the difference as
margin, otherwise0. Inflate3, border81/93, inset1 then `(margin+2,2)`.
Capture signed shield stock; select scale; draw at most10 rows while positive,
each with min(stock,perRow), total perRow, icon width `L(17)`, current box width,
flags1. Subtract count, advance y by captured pitch; reset and present.
The lower-right pane only restores background and optionally presents it.

#### Supported units and improvements

Support captures pane left/top; zoom−3, replaced by−6 at scale1 or0 at scale3.
Pitch=`M(69,z)`, row pitch=`M(52,z)`, columns=`L(192)/pitch`, rows=
`L(78)/rowPitch`. Margin=half(`L(192)−pitch*columns+M(5,z)`); padding=
half(`L(78)−rowPitch*rows+M(4,z)`); icon y offset=`M(32,z)`.
First scan every live unit slot with nonzero identity and unsigned home equal
to live selected city; no unit-owner test. If count≤columns replace padding by
half(`L(78)−M(48,z)`) and draw heading447 in124/18 at header top+`L(1)`;
otherwise omit that heading.
Capture icon width=`M(64,z)`, signed selected owner and unsigned government;
set awaySeen0, remove group6, then scan qualifying units again.

Draw each unit flags4. Count Food icons for live Settler-role units using
settler Food, and one Shield for the support attribute. An away attribute under
captured government>4 calculates penalty1 only without Women's Suffrage and
Police Station; add1 for captured Democracy. Otherwise, the first such unit
waives a nonzero penalty. Zero penalty becomes one neutral glyph; nonzero
penalty becomes that many unhappy glyphs. Increment awaySeen. Select scale;
spacing uses combined count, `L(10)`, icon width. Reread role for the Food
loop and support attribute for the Shield; happiness glyph/count remain
captured. Reset scale. Register group6 with full unit ID and a **square**
iconWidth×iconWidth region. Advance the grid; stop when its row capacity is
reached, without continuing the scan.

Improvements restores, removes group4, captures content left/top and draws
heading448 at pane top−`L(1)`, style124/18. Start x=left+`L(2)`, hit y=top+
`L(1)`; rows=`(contentHeight−half(L(1)))/L(12)`, icon zoom=`2*scale−8`.
Register all rows, including empty ones, group4/ID=row, width `L(192)−L(4)`,
height/pitch `L(12)`. Reset draw y to content top+**literal1**. Scan IDs0–66:
ordinary IDs<39 use C08 presence; later IDs use wonder location==live city.
Count every present entry. Draw only ordinal scroll+1 through scroll+rows:
sprite at x/y, select/reset captured zoom; name at x+`L(24)`,y−2, style41/10.
Ordinary entries also draw a Coin at content.right−`L(14)−L(4)`,y−1 with scale
`3*layoutScale−7`; wonders do not. Advance draw y only for displayed rows.
Publish total count in the canonical window. Set native scrollbar range0..count−1,
page rows−1, then position from the live stored scroll. Present if requested.

#### Support Map

Capture signed owner; draw heading452 in124/18. Available width/height are
Central dimensions−literal4. Divide width by live map width, then height by
live map height. Use F01 `limit(horizontal,1,vertical)` without repairing
reversed bounds. Center with fresh map dimensions: start coordinates are
Central origin+2+half(available−dimension*scale). Read owner's map center and
map width; origin=center−half(width), replaced by0 on a flat world.

Iterate y then x against live map dimensions. Wrap x+origin; require W01 valid
location and either owner's visibility or unrestricted display. Fill a square
at `(startX+x*scale,startY+y*scale)` with land48 or ocean93. Then scan all live
units with nonzero identity and home==live selected city; wrap unit.x−origin
and draw its square in29, with no additional map-validity/visibility check.
Finally separately read selected-city x/y, wrap x−origin, and fill in41. This
ordering lets the city marker overwrite unit and terrain markers.

#### Happiness stages

Capture signed owner and draw heading453. Start x=header.left+2,
y=header.top+`L(14)`+2; available width/height are header dimensions−4.
Row height=height/5; citizen y inset=half(rowHeight)−half(`L(30)`). Stages0–4
consume C04's stored unsigned happy/unhappy/excess counts. Set previous stage0.
For every stage>0 draw a separator in124 at current y **before** deciding
whether to skip; skipped stages can draw overlapping separators.

[Required behavior is retained in the main section.](#7-pane-drawing-and-resource-display)

After the right-hand content, subtract `L(2)` and restrict citizens to the
lesser of half available width and remaining extent. For stage2 under Communism,
perform the otherwise discarded font-height query, select41/10 and show the
distribution bonus: Coin at left/middle−half(`L(14)`), text at left+`L(14)+L(3)`;
that Coin has no new explicit scale selection. Otherwise read excess, unhappy,
happy stage bytes in that order and draw compact citizens. Only a displayed
stage advances y by captured row height.

Stage2 icons consider Colosseum, Cathedral (requiring Monotheism, with Chapel
overriding its displayed ID), Temple, then Palace if present else Courthouse
under Democracy. Perform qualification queries even for a fourth qualifying
entry, then return once three have already drawn. Between icons subtract
`L(2)`; subtract `L(36)` for each, select scale, read the current right edge
after that callback, draw at middle−half(`L(20)`), and reset.

Military effects capture owner, government, unit zoom, `M(64,z)`, `M(48,z)`
and middle. Communism right-aligns the government name in41/10. Below Republic,
scan live unit identities at live selected-city coordinates with nonzero attack,
without ownership filtering; draw at most three flags4 unit icons. The fourth
qualifying unit still performs qualification before returning. For each subtract
unit width, then `L(2)` except the first. Republic/Democracy calculate penalty1
only without Suffrage and Police Station, increment for a **fresh** Democracy
read, and return if zero. Capture away count; if nonzero and a fresh government
is Republic, decrement it. Icons=away*penalty; zero returns. Apply spacing to
icons, width `L(10)+1`, extent−`L(2)`, accepting adjusted count. Reserve
`L(10)+(icons−1)*pitch`, select scale, draw the unhappy row with flags0, reset.

### Original production row rendering and dialog layout

The row renderer decodes value<62 as a unit; otherwise improvement=value−62.
If row flags request an inset, add38 to x. For a nonnegative unit, set
top=y−`(M(48,−2)−rowHeight)/2`, select zoom−2, draw its preview and reset.
For a nonnegative improvement set top=y−`(20−rowHeight)/2` and draw its sprite
without a new scale selection. Return0.

Load the production resource, install this renderer and the Help label.
Instant Advice limits the primary list to min(optionCount,12), positions it at
`(−999,−5)`. After that optional clamp, any count<16 uses max(count+1,8)
visible rows. Modal width is300. The Freebie button requires the live cheat flag and game mode0, using the
parent's separate live gates. Clear the timeout-armed state before and after
the modal interaction. C06 owns any resulting world mutations.

### Original §9. Illustrated city view

This view builds a 1280-wide scene from supplied city-view assets and tables,
then displays/pans a viewport. Use a repeatable layout context keyed as below;
its mapping may differ from the original generator and it may be independent of
simulation. Window/surface providers, screen rectangle, cursor samples, key
states and modal events remain explicit external inputs. Original reseeding of
the shared gameplay stream is evidence in F01 Appendix D.

#### Construction, classification and resources

Construct outer window, child window, scene surface, palette, building sheet,
wonder sheet and resource module; then60 scene sprites, four each of small
base, small variant, tall base and tall variant sprites; overlay surface and
font. Font construction clears its handle only. Query the screen rectangle,
set shared center=`wrap(1280−screen.right)/2` with truncating division, copy
the three improvement and wonder slot templates from Appendix B, publish this
viewer as active, and clear all60 scene assignments. Replace font with size36
and flags1; set title rectangle `(0,0,live screen.right,40)` after font callbacks.

Preparation constructs a temporary surface, stores city and resolves `cv.dll`.
Missing resolution destroys the temporary surface and returns0. Open the
resource module, then classify the site. Capture selected-city x and y. A
coastal flag selects Coast immediately; otherwise examine W01 neighbor entries
0–8 in order, wrapping x and requiring valid coordinates. The first river tile
selects River; absence selects Inland. The resulting parameters are:

| Layout | Draw limit | Improvement pool count | Wonder pool count |
| --- | --- | --- | --- |
| Coast | 60 | 27 | 24 |
| River | 57 | 27 | 24 |
| Inland | 60 | 31 | 24 |

Advance variant uses cached owner and ordered short-circuit queries:
Automobile and Electronics =>2; otherwise Philosophy and Invention =>1;
otherwise0. Preparation stores this query’s result in the viewer’s advance-variant state;
background selection later queries independently. These preparation-era owner
queries use the captured input city, not a later changed viewer selection.
Allocate scene surface with width=min(screen.right,1280), height480. Load
building, wonder and house sheets in that order, using palette start10/count236.
Each failed load destroys temporary surface and returns0, retaining earlier
effects. Extract four tall base sprites at x=1+159*i,y=1,size158×114. Tall
variant y is231 if a fresh citizen-style query>1, otherwise116. Four small
base sprites use x=1+124*i,y346,size123×82; small variant y512 if another fresh
citizen-style query>1, otherwise429. Extract with transparency9 and replace
color7 by9 for each sprite. Allocate a32×32 overlay surface and load its art
with the initializer palette start10/count32; failure follows the same return.

Create outer window at screen left/top, width right−left and height
bottom−top+5, using the shared initializer palette and a black background.
Create child x0, y=`half(screen.bottom−480)` (not screen height), width freshly
read right−left, height480, with the viewer palette. Background variant is3
when selected city has Superhighways, otherwise the advance variant of its
fresh signed owner. Load background `layout*5+variant+340` with palette
start10/count236; failure returns0. Install the key-press, left-click,
key-down and mouse-move handlers below, then refresh. Only after successful
window creation does preparation place the scene, draw it, destroy temporary
surface and return1. Ordinary failure still leads the caller to destroy the
constructed viewer. Allocation failure creates/runs nothing.

#### Slot selection and scene placement

The slot selector returns−1 without RNG when remaining count=0. Otherwise
capture `maximum=remaining−1` and `numerator=3*remaining−3`, draw F01's inclusive
random range `(numerator/4,maximum)`, then read the chosen entry from the **live
layout's** pool. Decrement live remaining count, read its last live pool entry,
replace the selected entry with that last value, and return the captured slot.
Wonder and improvement selectors have separate pools/counts. Negative counts,
invalid layouts and returned indices are not silently clamped. Out-of-bounds
pool writes are excluded even if they land inside the viewer's larger object.

Set variant quota=signed city size+2. Capture signed x shifted left8, separately
read signed y and unsigned resource-seed word, and use their wrapping sum as
the layout key. The same key, scene inputs and policy version reproduce the
layout, including key zero; no global gameplay reseed is required. Iterate
improvements1–34. At each iteration clear reserved. For valid
layouts select the slot as follows:

| Improvement | Coast | River | Inland | Reserved |
| --- | --- | --- | --- | --- |
| City Walls | 53 | 53 | 52 | Yes |
| Offshore Platform | 35 | Skip | Skip | No |
| Harbor | 54 | Skip | Skip | Yes |
| Airport | 57 | Skip | Skip | Yes |
| Port Facility | 55 | Skip | Skip | Yes |
| Others | Take improvement slot | Take improvement slot | Take improvement slot | No |

An ordinary selector result−1 skips the rest of that iteration. If the city has
the improvement, place its sprite. The original range(0,0) afterward is an
unused sample and may be omitted. Otherwise invoke small variant when quota≠0, small
base when quota=0. After all improvements, repeatedly take slots until−1 and
fill them using that same quota test. Do not clear reserved before this drain:
it retains the final improvement iteration's value.

Reset quota to a fresh signed city size+2. Iterate wonders0–27, clearing
reserved each time. Ordinary wonders take a wonder slot and skip on−1;
reserved placement is:

| Wonder | Coast | River | Inland |
| --- | --- | --- | --- |
| Colossus | 58 | 54 | 54 |
| Lighthouse | 59 | 4 | 3 |
| Great Wall | 1 | 1 | 0 |
| Eiffel Tower | 56 | 55 | 57 |

All four rows set reserved. If the live wonder location equals live viewer city,
place it (the original range(0,0) afterward is optional); otherwise use tall variant/base according to
quota≠0. Drain remaining wonder slots with the last reserved state. Reset
wonder sheet storage, then building sheet storage; return1. Quota tests are
nonzero, not positive: a negative signed size can produce a negative quota
that continues to decrement for every eligible variant placement.

Each house helper uses range(0,3) to select among the four house sprites. A
reserved flag causes an immediate return; the original sampled even on that
path. Unused reserved/absent-slot samples may be omitted. Variant helpers
decrement quota before testing slot==−1; base helpers do not change quota.
If slot is not−1, assign the selected one of four shared house sprites. Other
negative slots are not treated as absent. Building/wonder placement returns
for **any negative** slot; otherwise extract the Appendix B rectangle from its
sheet using transparency9, replace color7 by9, and publish the scene assignment
after those callbacks. Drawing/extraction must not turn an invalid receiver
into a permitted overwrite of an adjacent object.

The improvement loop's slot has no original incoming producer for an invalid
layout until one is assigned. Supply retained state or stop when consumed.
The completed improvement drain leaves slot−1 before the wonder loop, so an
invalid wonder layout has that established retained value. Geometry reads
outside valid tables require supplied backing or a stop; they do not grant
permission for the corresponding out-of-bounds mutation.

#### Drawing, modal lifetime and panning

Draw scene slots in ascending order while index<live draw limit. A nonempty
assignment causes a position read from live layout/index, then a fresh sprite
assignment read and draw to the scene surface. This preserves overlap order.
Refresh afterward. Refresh captures destination `(0,0,screen.right,480)`;
source right is screen.right+captured center and source left is a separate
center read. Copy scene to child, draw title, update child, flush paints.
The title is city name, P04 colon, an additional space and formatted year.
Select text color10, offset the live title rectangle(+1,+1), draw; select26,
offset its current rectangle(−1,−1), draw again. Use shared text live across
these callbacks, without reconstructing a private title snapshot.

Run shows outer window, flushes paints, assigns its viewer palette, shows
child, flushes, captures child input, and for live game mode≥3 installs the
network poll idle callback. Run child's modal loop. On ordinary completion,
clear idle callback, release capture, hide child, flush, restore outer window
initializer palette, hide outer window and flush. Key/character effects are:

- Escape key-press clears the **outer** window loop with no active-viewer null
  gate. Left-click, when an active viewer exists, clears its **child** loop.
- Left/numpad-left key-down pans left only when screen.right<1280; corresponding
  right keys pan right under the same test. Enter, Space or Escape key-down
  clears the active child loop when present.
- Mouse move tests x>`arithmetic(wrap(screen.right*7)>>3)` for right pan, then
  independently reloads screen width and active viewer and tests
  x<arithmetic(screen.right>>3) for left pan. The first call can change the
  second call's inputs; these are not mutually exclusive branches.

For either pan, capture destination `(0,0,screen.right,480)` and source bounds
as refresh does, then query cursor position. Each iteration first tests live
center: right stops only when center>`1280−live screen.right`; left stops only
when center<0. Equality admits drawing and a four-pixel overshoot. Next query
the corresponding numpad key and, only if its high byte is zero, arrow key.
Any nonzero high byte holds panning; the sign bit alone is insufficient.
Without either key, right requires cursor.x>`wrap(screen.right*7)>>3`; left
requires cursor.x<`screen.right>>3`. The original ignores cursor-query status:
a first failed query needs supplied retained cursor x or a stop only if this
branch reads it. Keyboard-held branches do not require cursor contents; a
later failed query retains previously produced coordinates.

For an admitted iteration copy scene to child using the captured rectangles,
draw title, **present** child (no refresh callback/paint flush), change live
center by+4 or−4, translate source rectangle by the same amount, and query
cursor again. The destination and source width stay captured even if callbacks
change screen width; termination thresholds use live width/center. There is
no implicit delay, event pump or endpoint clamp in this loop.

Normal destruction clears the active viewer before resource close, then
destroys font, overlay surface, tall-variant sprites3..0, tall-base3..0,
small-variant3..0, small-base3..0, scene sprites59..0, resource wrapper,
wonder sheet, building sheet, palette, scene surface, child and outer window.
Later callback changes to active viewer survive. The owning caller deletes
allocation afterward; a setup/run diagnostic does not establish an invented
exception-cleanup path. Continuing or releasing the layout context follows the
implementation's declared policy; no gameplay RNG restoration is prescribed.

### Original §10. Conformance cases and joins

Compare ordered child calls, selected city, flags, calculation workspace,
regions, controls, controlled visual choices or implementation random context,
text, rectangles and final pixels where the
provider/asset inputs establish them. At minimum cover:

- Hidden, suspended and visible opens; flush-before-guard ordering; processing
  Proceed/Exit and foreign-owner tutorial branch; title-only versus full refresh;
  receiver calculation followed by canonical-window drawing; selection changes
  during each modal callback and cleanup.
- All three scales, odd/negative arithmetic, scale2's multiplication bypass,
  scale change with unchanged origin, cache publication before failed loading,
  scrollbar setup before new pane geometry, empty/overlapping regions, right/bottom
  exclusion, remove-first ties, output aliasing and count-only invalid removal.
- City-name cycling ties, wraparound and names outside the finite literal bounds;
  Rename ownership, both unit dialogs, cancellation, home change, recycling at
  selected city versus unit location, navigation after deletion, View entry,
  late owner/selection changes and retained busy-state effects.
- Citizen parity and crowded regions; all four staggered present-unit rows;
  hidden headings when more than one ordinary row is needed; square supported
  regions; empty improvement rows and live scroll; first-free Republican away
  icon; Happiness stage skips/separators and fourth-icon qualification.
- Tile overlays, worked-tile order, live yield-counter consumption, zero-yield
  marker, negative/overflow yields and division boundaries; each stock-box fit,
  Granary/Pyramids line, Capitalization and callback changes of production.
- Food/Shield shortage versus surplus, zero segments, compressed counts, live
  waste, the missing-coordinate case and its supplied-state/stop alternatives;
  **shield** shortage controlling Trade background; distribution ties and live
  rates versus packed counts. Keep these visual packing values separate from
  recalculated economics.
- Production option eligibility/order through C06, exact row text, expired
  wonders, advice section fallback, unavailable art and supported-name limits;
  network prompt versus ordinary active prompt, strict signed timeout equality
  and wrap, and unconditional polling after idle handling.
- Forced/local/autoplay text and picture notices, differing map/name/options
  effects, preview zoom changes, secondary result publication and ordinary
  destruction order.
- All viewer layouts, era queries and backgrounds; shuffled slot permutations,
  exhausted pools, reserved no-art slots, omitted present-art range(0,0) samples,
  negative quotas, repeated keyed layouts, overlap order, source
  rectangle/title refresh, modal close targets and failed setup prefixes.
- Both pan endpoints including equality overshoot; high-byte key states,
  successful then failed cursor query, first failed query reached/not reached,
  callbacks changing center/screen and frozen copy rectangles. Compare safe
  failure handling separately from supplied retained-state compatibility.
- Next/Previous pressed-image shift, width-for-height copy, live Normal then
  Pressed tests, unsigned native coordinates, capture/release, Space polling,
  focus traversal and callback replacement. Exclude every memory overwrite
  before it occurs, including an original path reached during ordinary play.

C02/C03/C04/C05/C06 supply real city calculations and mutations, U01/U04 unit
stack and movement actions, W01/P01/P02 map geometry/drawing, P03/P04 actual
asset/font/control services, P05 input/timers and N02 network/modal delivery.
P07 owns advisor refresh destinations. X02 must test those joins with initialized
gameplay and independent state/draw/event traces. A finite unit harness does not
establish complete native GUI, pixel or multiplayer conformance.

### Original city display state

Nominal inputs are initialized city/window state, backed live city/unit records,
complete rules and assets, fitting names, valid geometry and positive divisors.
Fonts, native control/window responses, ordered callbacks, clock samples and
external input are supplied services. Pixel conformance uses the same asset
content, palette and font metrics. A replacement need not reproduce Win32.

The city window stores a selected city, hidden, suspended and redraw-suppressed
states; central display mode; improvement scroll/count; layout scale and unit
zoom; background offsets/origins; pane rectangles; ordered hit regions; controls;
and an update-scope state. Shared state includes cached background scale/zoom,
the cached Autobuild button state, pending Proceed refresh, Proceed-requested,
unit-dialog busy, production-dialog timeout state, and the city-calculation
workspace. Central modes are Units/Trade, Support Map and Happiness.

### Original city-window construction

Reuse the saved city rectangle only when restore-player is nonzero and the saved
main-window-state selector is nonzero. Otherwise choose the default scale and
rectangle in §3 through the canonical city window. Create the attached window
with the saved rectangle minus the global frame dimensions, the city title,
caption 24 and border 8. Install the shared title/detail fonts and Close, Smaller,
Larger caption icons in that order. Clear ordinary control references. Register:

| Event | City operation |
| --- | --- |
| Close | Close command below. |
| Update | Full refresh. |
| Minimize | Set suspended. |
| Restore | Clear suspended, then raise if visible. |
| Left/right click | City mouse dispatch with respective flag 0/1. |
| Key press / character | P05 map-key / map-character controller. |
| Size / move | Query the current parent-relative rectangle and save it. Callback coordinates are ignored. |

### Original city-window release

Release first sets suspended, then releases controls, then resets graphics/window
storage. Control release is gated solely by the Buy reference: if absent,
leave every other control and the linked list untouched. Otherwise clear the
control list, delete buttons in order Buy, Change, Central, Support Map, Rename,
Happiness, City View, Proceed, then the Next/Previous icon controls, then the
improvement scrollbar. Read each reference at its deletion. Proceed clears its
reference immediately after its own deletion; the final reset clears the ordinary
button/icon references but retains the scrollbar reference. A later use of that
retired object requires a safe failure boundary, not fabricated live storage.

### Original happiness stage presentation

- Stage1 skips when Happy−Unhappy equals the previous stage's balance. Otherwise
  set previous=1 and draw a Diamond at right−`L(14)`, vertically centered.
- Stage2 displays when Temple, Colosseum, Cathedral, Michelangelo's Chapel or
  ((Courthouse or Palace) and Democracy), tested in that order. Otherwise skip.
  Set previous=2 and draw its improvement icons as below.
- Stage3 skips when its balance equals previous's; otherwise draw military
  effects. It does **not** update previous.
- Stage4 compares specifically stage3 with stage4, skipping equal balances.
  Otherwise right-align the Wonders label, style41/10, at row middle−half font
  height, and use its returned left edge.

### Original city preview prompt construction

Construct Prompt then Sprite. Query shared popup scale separately for preview
height (48 or96) and width (64 or128), create a solid preview in color0, load
the requested GAME resource (ignore its status), and install a city renderer.
It draws P02 city flags0 at x+2 with zoom0 or8 from a **fresh** popup-scale read.
Set the prompt sprite-scale setting to0, append the preview, run the modal
with argument0, capture its full result, and publish the prompt's secondary result to the shared city
dialog result. Destroy Sprite then Prompt. The fixed city callback instead
always uses zoom0 and x+2. P04 owns prompt parsing and native controls.
