# V05 — Diplomatic negotiation

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in V05 |
| --- | --- |
| Gameplay | Offer eligibility/valuation, AI responses, patience, transfers and treaty changes, preserving accepted exchange order and human negotiation authority. |
| Original files/content | F03 rule identities and F06 retained diplomatic values; native dialog records are not a save format. |
| Original peers | N02 offer/reply encodings, participant/recipient identity, acceptance, cancellation and transfer ordering remain exact at the peer boundary. |
| Executable comparison only | Audience screen progression, prompt result storage, UI retries without game effects and artwork callbacks. |

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
**5.4.0f, patch 3**. This document owns audiences, emissary admission, interactive
proposals and responses, gifts, tribute, technology exchange, joint-war requests,
patience, and the gameplay/session continuations of multiplayer parley.

[V04](V04-diplomacy-and-treaties.md) owns directed relations, treaty primitives,
attitude, senate checks, war declarations, contact dispatch and treaty notices.
[V03](V03-research-and-technology.md) owns knowledge, technology grants and the
captured-technology chooser; an exchange invokes those children in the order
specified here. [W03](W03-visibility-and-exploration.md#7-sharing-maps) owns the
accepted AI map exchange and directional multiplayer map transfer. They are not
interchangeable. [A08](A08-ai-research-government-and-diplomacy.md) owns stance
evaluation, technology valuation and the alliance-willingness provider described
below. Interactive request pricing and
execution described here belong here. P04/P07 own the common dialog/report and
list presentation services; N01/N02 own session admission, message encoding,
receipt, authority and state synchronization. None is an assumed-success no-op.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
semantic publications and failure effects. Only dependencies within the binding
scope above constrain evaluation; storage/API choices need no original-trace
equivalence proof. External file/wire definitions remain binding at adapters.

## 1. Inputs and notation

Throughout single-player negotiation, `A` is the actor/player civilization and
`B` the counterpart whose emissary and AI responses are shown. Write `R(A,B)`
for A's directed relation toward B, `rank(A)` for its stored power rank,
`mil(A)` for its stored military strength, `rep(A)` for its reputation, and
`distrust = rep(A) - credit(B,A)`. Flags use V04's named sets. `category()` is
V04's full signed-attitude category, not the stored attitude alone.

The live evaluation `E` supplies current attitude, selected ordered pair,
aggressive/initially-aggressive/suppressed-aggression flags, declare-war flag,
fear, request mode, peace inclination, target-is-leader, war target,
strongest/weakest enemy, tribute demand, last contact, captured-city count,
AI-units-near-cities, spaceship threat/relative rank and technology choices
`best`, `second`, `wanted`. The meeting has active, asked, emissary response,
initial-evaluation-complete, opening-evaluation-active, AI-meeting-active,
gifts-this-meeting, had-ceasefire and multiplayer-pending state. Some of these
are shared with the evaluator. Keep the same logical storage; do not replace
live reads after children with an entry snapshot.

A08's `evaluate(A,B)` updates this shared evaluation using the live pair and
opening flags; `value(c,advance)` returns its technology valuation. Its
`allianceWilling(A,B)` returns willingness and a reason set and can replace
`E.warTarget`. Reasons are **existing allied target**, **help against a dangerous
enemy**, and **new common target**. V05 specifies exactly when these providers
run and which of their results persist. A08 specifies their algorithms; joining
those actual providers with these controllers remains necessary before claiming
complete AI negotiation conformance.

[F01](F01-determinism-and-arithmetic.md) supplies arithmetic, clamp, primary
random `R()` and secondary inclusive-range `Q(lo,hi)`. Unqualified expressions have
F01 reference results and division truncates toward zero. `mod` means the signed
remainder. Short-circuit gates consume draws/queries only on the reached branch.
Request count and reputation credits retain signed8; reputation retains
unsigned8. Turn/contact fields retain signed16. Unit type/cost/role fields have
the signed interpretations specified by F02/F03. Gifts-this-meeting and
intermediate scores retain signed32. These are value domains, not prescribed
record layouts. See Appendix A for reference encodings.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

Nominal inputs have backed civilizations0…7, valid rules and live records under
[F02](F02-game-state-and-invariants.md), valid dialog replies, and complete
messages admitted by N02. A helper has no implicit alive, distinct-civilization,
knowledge or positive-price check unless stated. Accepted negative treasury,
contradictory flags, wrapped request counts and stale summaries follow the
specified operations. A stricter importer may declare a smaller domain.

Out-of-bounds writes and their corruption are **excluded bugs** under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs).
Reject such inputs or stop before the invalid write, after accounting for prior
valid effects. Do not recreate adjacent-field corruption through named fields.
Uninitialized/out-of-bounds reads are a separate evidence issue: require
supplied backing or a bounded stop instead of inventing zero. No operation here
promises rollback after a child fails or an input leaves the admitted domain.

Dialog names below identify observable message/choice roles. Replies are full
integers: different prompts use different acceptance values. A notice is a
callback boundary even when its answer is ignored. `reaction(n)` calls the
leader animation/presentation service; it does not itself change diplomacy.
`format` refreshes the attitude argument from live E. P04 owns resource loading,
text expansion and display mechanics, including retained arguments not assigned
by a caller. The main algorithms name semantic arguments; original slot numbers
are validation details.

## 2. Patience, emissaries and session reset

`patience(A,B)` starts at2; attitude<25 sets3; attitude>60 subtracts1.
Query A's active Eiffel Tower and add1 if present. Add1 for B→A Peace and2
for B→A Alliance. Finally B→A War resets the result to2. The Eiffel query still
occurs before that override. No clamp or random draw occurs.

`format(A,B)` builds attitude-category text, appends “er” in language2, then a
space and B's adjective. It publishes the attitude string. A is otherwise unused.

`greetEmissary(A,B)` constructs its dialog, sets meeting active=1 and, if the
Diplomacy Screen preference is enabled, opens B's leader window. Its dialog
position is a presentation choice; the original formula is in
[Appendix C](#appendix-c-original-presentation-reference). Format attitude and publish B's leader/title/plural,
gender form and A's plural. Choose Greetings by B→A Vendetta absence/presence,
then draw `Q(0,3)` for its variant. If B's fixed Nuclear Missile active-type
counter is nonzero, append the nuclear-weapons section before the modal.
After it returns, if A's same counter is nonzero and the pair of A→B flags
(Peace,NuclearThreat) is anything except (absent,present), show YourNukes with
variant2 when B has missiles and1 otherwise; then directly set A→B NuclearThreat.
Thus Peace can make this notice repeat even with NuclearThreat already set.
The counter test is nonzero, including accepted negative values.

`askEmissary(A,B,x,y)` constructs a dialog even in autoplay. In autoplay set
response=-1 and return0 without resetting asked. If asked was already set,
return `response != -1`; do not greet again. Otherwise set asked=1 and initialize
a local nuclear-forced flag false. While response=0:

1. If B has missiles and B→A NuclearThreat is absent, set nuclear-forced true;
   it stays true for this invocation. Evaluate stance. If (x,y) is in bounds,
   ensure it is visible to A and redraw it when that helper returns0. Format.
2. If nuclear-forced, show EmissaryForce and break without assigning response.
3. Otherwise build Emissary with B's leader/title/plural/gender. Add Intel reply2
   when local-human→B Embassy exists, or local human has active UN or Marco Polo,
   in that short-circuit order. Store `response = -modalReply`.
4. If response is not -2, break. Otherwise show the intelligence report for
   (A,B), set response=0, and rebuild the prompt.

After the loop, greet if response != -1; return that same predicate. Reply1
refuses; reply0 accepts with response still0. Forced reception usually accepts
with response0. A prior nonzero response can bypass the loop entirely.

`resetSession()` clears active, asked, response and initial-evaluation-complete,
then closes the leader window. It does not clear opening-evaluation-active or
AI-meeting-active. Callers that test asked/response after reset see those zeros
unless a child has republished them.

## 3. Technology exchanges

### Survey and offer composition

An exchange constructs one dialog and captures whether B→A Alliance is set
once, before any repetition. Each survey resets best/second/wanted choices to-1,
their scores to0, both known counts to0, first A-known advance to-1 and a
blocking-wonder result to-1. Scan advance indices0…99, skipping only records
whose two prerequisites are both Disabled. Do not use the general enabled or
can-research predicate.

For each admitted advance read A's knowledge, then B's. Count each known result.
The first A-known advance is retained. If B knows it and A does not:

- Scan wonders0…27 with that prerequisite, Unbuilt raw location, and
  `B.groupCount < A.groupCount`. For each, scan live B cities ascending for
  production of that wonder. Record it and break only the city loop. A later
  qualifying wonder/advance overwrites the earlier blocking result.
- Compute `value(A,advance)` and replace wanted when its score is **at least**
  the retained wanted score. Later ties win.

If A knows it and B does not, compute `value(B,advance)`. A score below best
can replace second when it is at least second. Otherwise move an existing best
and its score to second, then make this advance best. Scores start at0, so a
negative score need not select any option. Later ties become best, with the
previous tied best becoming second.

Composition publishes wanted name, A adjective and best name. It publishes
second name if valid, else first A-known name if valid; otherwise that text
argument retains its prior contents. Use friendly Exchange variant1 only when
mode=0, E.aggressive=0 and category(E.attitude)<5; otherwise variant0. Reset/load
the dialog. If second is valid and differs from best, append the expanded
alternative-tech label as reply2. Oversized original label writes are excluded
bugs, not an obligation to corrupt the dialog's stack.

### Selling a technology

`sell(A,B,mode)` returns0 immediately if wanted<0. Otherwise price starts at
`value(B,wanted)*20`. If attitude>50, replace it by `attitude*price/50`.
Double it when A has rank7, more than4 cities, turn>200 and difficulty is not
Chieftain. Count civilizations in the **low eight bits** of wanted's global
known-by set, subtract1, and use divisor1 if the result is below2.
Let `q=price/divisor`; price=`q*10`. If A.gold>1500 use `q*30/2`; if A.gold>3000
multiply the resulting price by3/2.

If A→B Alliance: when A's stored advance count is below B's, halve price;
otherwise subtract price/4. If A.count+4<B.count, halve again. A negative price
becomes30000, then any price<100 becomes100. Positive values above30000 are not
capped. If A.gold<price, return0.

Show SellTech for mode<2, otherwise SellTech2, with wanted and cached price.
Only reply1 subtracts that price from live A.gold, redraws status, grants A the
**live** wanted technology from B with ordinary grant flags, and formats attitude.
B receives no gold. Return1 whenever this affordable offer was shown, including
refusal. This return is “offer presented,” not “purchase completed.”

### Exchange controller

Modes used by callers are0 (favor),1 (opening exchange),2/3 (allied demand).
Each iteration surveys first. If selection-only is nonzero, return0 now, with
shared choices already published. Otherwise format and publish B adjective.
Execute these gates in order:

1. Without the captured alliance, refuse for B→A Vendetta, A→B SneakAttack,
   nonzero captured-city count, B→A Fighting, aggressive with B-known-count>5,
   or category>5. Mode0 draws `Q(0,2)`: zero shows NoExchangeMedium/reaction3;
   nonzero shows NoExchangeMad/reaction4. Other modes show neither. Return0.
2. A blocking wonder on difficulty above King prevents exchange; mode0 shows
   NoExchangeWonder with its name, then return0.
3. If A-known-count<B-known-count: mode1 returns0. Otherwise, when category>
   `5 - (B→A Peace absent)` and no captured alliance, try selling. If an offer
   was presented, return **mode**. Otherwise mode0 shows NoExchangeMedium/reaction3
   and all modes return0.
4. If A has rank7 and >4 cities, no captured alliance, and category exceeds
   `(turn<201 ? 4 : 3) - (B→A Peace absent)`, refuse ordinary exchange unless
   best equals a prerequisite of Armor, Cavalry, Artillery, Howitzer, Riflemen
   or Musketeers, or equals Guerrilla Warfare. On refusal, modes other than1
   try selling and return mode if presented; otherwise mode0 shows
   NoExchangeNow. Return0.
5. If best<0, wanted>=0, **live** B→A Alliance, and
   `A.count + 6 - B.leader.civilize - clamp(B.count/12,0,5) < B.count`, with
   B.request<2 or mode=3, show ExchangeGift for mode<2, else ExchangeGift2;
   reaction3. Grant A live wanted from B, then set wanted=-1. Mode<2 adds2 to
   B.request and returns0; modes2/3 return1.
6. If wanted<0 or best<0, modes other than1 try selling and return mode if
   presented; otherwise mode0 shows NoExchangeNow2. Return0. Mode>1 also
   returns0 before any use of the supplied coordinates.
7. Ask the emissary; refusal returns0. Compose and run the exchange dialog.
   Reply0 returns0. Default attitude benefit is twice the retained best score.
   For reply2, reject if B already knows live second, B has Marco Polo, A is
   rank7 with >4 cities, or second score<best score and either A-known-count<
   B-known-count or category>4. Rejection shows ExchangePetty/reaction3,
   increments B.request and returns0. Otherwise swap live best/second and use
   the retained second score as the benefit. Other nonzero replies use best.
8. Grant B live best from A; invoke V03's **captured-technology chooser** for
   A taking from B. This is not a direct grant of the displayed wanted choice.
   Set best to live second, second=-1, adjust B→A attitude by minus the cached
   benefit, set TradedTech on both civilizations, and clear E.aggressive.
   Repeat if mode!=0; otherwise return1.

The captured alliance and dialog owner survive repetition; the survey, scores
and choices do not. Grant callbacks can change choices before subsequent reads.
Negative externally supplied modes enter the modal path and repeat; they are
outside nominal caller modes, not silently normalized to0.

## 4. Gifts and favors

### Gold attitude value

`goldValue(g)` starts value=0, divisor=10, step=50. While g>0 add
`clamp(g,0,step)/divisor`, increase divisor by5, subtract step from g, then set
step=100. Return value. Nonpositive amounts score0. The marginal blocks therefore
use50/10, then100/15,100/20, etc.; this is not one percentage of the whole gift.

### Gift menu

Format and show GiftMenu. Reply0 decrements B.request. Other unrecognized
choices have no effect. The following branches do not reset the session.

**Technology (1):** run the exchange survey through selection-only mode0. If
best<0, show KnowNo/reaction4. Otherwise show TechGift if second<0, else
TechGift2. Reply0 shows Wasting/reaction4. Reply1 computes benefit
`value(B,best)*4`, then grants B live best from A. Any other nonzero reply
computes `value(B,best)*2`—still values best—but grants B live second. Adjust
B→A by minus benefit, format, show Accept/reaction2. Subtract
`signed8(giftsThisMeeting)+2` from B.request, then increment giftsThisMeeting.

**Gold (2):** let `v=A.gold/50`. For i=1…4 append amount `(i*v/4)*50` when it
is positive and differs from the preceding appended amount. A zero modal result
shows Wasting/reaction4. Otherwise subtract the returned amount from A and add
it to B, redraw status, adjust B→A by `-(goldValue(amount)*3/2)`, format and show
Accept/reaction2. Apply the same request reduction/gift increment as technology.
Accepted negative off-nominal replies reverse the monetary transfer and score0;
there is no second affordability check in this handler.

**Military unit (3):** list live A cities ascending. No cities, a negative
selection, no unit at the selected city, or cancellation of the later unit
chooser each decrements B.request; no-unit cases show MilitaryNone. Capture
B's nearest city to the selected A city. Reject with MilitaryNo/reaction4 if
none exists, category>4, A.count<B.count, rank(A)<rank(B), or mil(A)<mil(B).
Select a unit from the source stack using the shared unit chooser. If its
obsolete-by advance is nonnegative and B knows it, show MilitaryNo/reaction4.

On acceptance detach the unit, decrement A's active-type count, change owner
to B, clear unit visibility, increment B's active-type count, attach at the
**captured** destination city's live coordinates, and set home=None. This path
does not itself update global unit totals, military totals, movement or orders.
Benefit is signed unit-type cost*3, halved if B knows its prerequisite. Refresh
and redraw destination city, then source city; adjust B→A by minus benefit;
show Accept/reaction2. This branch does not apply the tech/gold gift counter bonus.

Re-query B's knowledge of the unit's live type prerequisite. If absent, scan
advances0…99 for the first unknown ancestor of that prerequisite, using V03's
ancestry predicate. If the selected index is **strictly greater than0**, draw
R(); even signed remainder modulo2 shows Breakthrough and grants that advance
from A. No draw for index0 or no candidate. The grant need not be an advance A
knows; the gift handler makes no donor-knowledge check.

### Favor menu

Repeatedly format and show FavorMenu. Reply0 decrements B.request and returns.
Reply1 runs exchange mode0. If it returned nonzero, capture B.request and test
`captured+1 < patience(A,B)`; on success increment the live counter and reopen
FavorMenu. Otherwise publish a state diff and return. The publication is not
conditional on multiplayer mode.

Reply3 requests maps. Reject with MapNo/reaction3 if any of:
`(!A→B Alliance && attitude>0 && !A.Marco && !A.Magellan)`, attitude>25,
B lacks Map Making, A lacks Map Making, in that order with short-circuit wonder
queries. Otherwise call W03's accepted AI-negotiation map exchange. Return.

Reply2 lists alive civilizations1…7 excluding A/B. Target0 returns to FavorMenu.
Other targets must be backed; a negative reply is not an implicit safe cancel in
the original. B→target without Contact shows NoContact/reaction3. An existing
B→target War shows AtWar/reaction3. Otherwise calculate the war price and distrust
below. Admit the request if distrust<7, or all of: B→target lacks Alliance,
B→A lacks Vendetta, A→target has War, and rank(A)<=rank(target).
Rejected admission shows HellNoWeWontGo/reaction3.

For an admitted request, B→target Alliance shows EternalAllies/reaction4.
Otherwise offer Mercenary when B.gold<1000, B.gold<=A.gold, best<0 or second<0.
The remaining case requests both technologies through Cybercop and records the
price sentinel -1. Reply0 declines. Any other reply accepts: for negative price,
grant B live best then live second from A, then set both choices=-1; for
nonnegative price reject with Unfortunate/reaction3 when A.gold<price, otherwise
transfer the cached price A→B. Then perform mercenary-war execution below.
Return after a nonzero target; this menu does not repeat after the offer.

### War price, distrust and execution

For target T, price starts as
`clamp(((rep(A)+B.gold/100-rep(T)-credit(B,A)) * (mil(T)>>1))/50,2,500)*50`.
Apply these steps sequentially, rounding at each division:

1. If B→T lacks Ceasefire and Peace, subtract price/3. If B.gold<A.gold,
   add price/3. If B→T Alliance, cap at7000 then use `clamp(price*3,500,25000)`.
2. If T has rank7 and >4 cities, halve. If A→T lacks War, add price/3.
3. If B→A Alliance, scan B→slots1…7 for any War. If none, subtract price/4.
   If T→A SneakAttack, subtract another price/4. If rank(A)<rank(B), subtract
   price/3.
4. If T→A Nuked: halve if A→T lacks Nuked; independently halve if rank(A)<rank(T).
5. Return `clamp(price/100,1,500)*50`.

War distrust starts `rep(A)-credit(B,A)-B.leader.attack`; add2 for B→A Vendetta,
1 if A→T lacks War,1 if rank(T)<rank(A),1 if rank(B)<rank(A). Halve for B→A
Alliance. It is signed and unclamped.

Mercenary execution redraws status, bilaterally sets B↔T Contact, RecentContact
and War, directly adds B→T Aggressor and AggressorLong, then B→A Mercenary.
Publish state; show MercDeclare with B/T. In network modes notify remote human
B then remote human T. If A→T Alliance, draw R(); an even draw adjusts T→A +25,
shows MercBetrayAlly/reaction4 and cancels alliance(A,T). Otherwise, only when
A→T Peace draw R(); an even draw adjusts T→A +25, shows MercBetray/reaction4,
sets A↔T War and directly sets T→A Vendetta. Publish state again. This is not
V04's generic declare-war operation and does not inherit its reputation logic.

## 5. Player proposals and AI responses

### Proposal menu and dispatch

Format, load Diplomacy and open its nine-line menu section. Read all nine lines
in order, including hidden ones. Offer:0 always (leave),1 if B→A Peace without
Alliance (alliance request),2 if neither Peace nor Alliance (peace request),3 if
allied (aid),4 if not allied and A's government is below Republic (tribute),5 if
at peace without Alliance and B→A ChangedThisTurn absent (withdraw troops),6 if
allied (cancel alliance),7 favor,8 gift. The at-peace test deliberately becomes
false when Alliance is present. Failed section opening leaves reply0. Close the
text reader even on that failure.

Response dispatch first clears the active-unit blink. Choices1/2 call the
alliance/peace handlers;3/4/5 call demand handling. Choice6 bilaterally clears
Alliance, adjusts B→A +50, evaluates stance, formats, shows CancelAlly/reaction4,
increments A.reputation, then invokes V04 cancelAlliance(A,B), including its
second clear and unit expulsion. Other choices return0. A nonzero response from
a handler ends the audience; a zero result normally returns to its proposal loop.

### Alliance and peace requests

For an alliance request obtain willingness/reasons from A08, then format and
publish both adjectives. For peace compute a reason set as follows, in order:

- Start with ordinary willingness if E.targetIsLeader=0, attitude<51 and
  distrust<=`4 + (Chieftain ? 2 : 0)`.
- If warTarget>0, A is not hostile toward it and A→target has Contact: compare
  target military against unsigned `2*mil(B)` when A→target lacks Peace,
  otherwise unsigned `3*mil(B)/2`. If B→target Fighting and the target meets
  that threshold, replace the set with ordinary willingness plus dangerous-enemy.
- If warTarget>0 and A→target Alliance, add existing-allied-target.
- Clear all reasons if B→A Vendetta and distrust>`2-B.leader.attack`, or
  distrust>6, or attitude>75.
- Query A's Great Wall, then UN if absent. Either adds wonder compulsion even
  after the preceding refusal override.

If unwilling, use the refusal selection below. Otherwise execute the following
bargain stages, preserving all payments even if a later stage fails:

1. **Technology payment.** Alliance wants payment if B.request>1, attitude>25,
   A.reputation!=0 or E.declareWar!=0. It may request technology when neither
   allied-target nor dangerous-enemy reason applies, or declareWar is nonzero,
   and best>0. Peace wants payment if B.request>1, attitude>50 or distrust>1;
   it may request technology only with neither dangerous-enemy nor allied-target
   reason and best>0. Draw Q(0,1), show PerhapsSecret/reaction3. Reply0 makes
   alliance unwilling; peace retains only wonder compulsion. Other replies
   mark paid, grant B live best from A, **then** value live best and adjust B→A
   by minus twice that value, format, set best=live second and second=-1.
2. **Gold payment.** Re-evaluate the payment condition from live state. Alliance
   may request gold if dangerous-enemy is absent or declareWar is nonzero;
   peace may if allied-target is absent. Require still willing and not paid.
   Compute `q = s32((A.gold/50)*mil(B))/(mil(B)+mil(A)+1) + rep(A)*k`, with k=2
   for alliance and1 for peace. Price=q*50, or q*100 when mil(B)<mil(A).
   Repeatedly subtract50 while price>A.gold; do not replace this loop by a clamp
   that changes retained overflow behavior. If price>0, draw Q(0,1) and show
   PerhapsThrowIn/reaction3 (the alliance branch requests reaction3 twice).
   Reply0 rejects as in stage1. Otherwise transfer cached price A→B, mark paid,
   adjust B→A by -goldValue(price), format and redraw status. Alliance redraws
   before format; peace formats before redraw.
3. If no willingness remains, show the appropriate NoPatience/reaction3 and
   return0. Otherwise, if live warTarget>0, show PerhapsSolidarity/reaction3.
   Reply0 abandons the agreement only for: alliance—any reason, rank(A)=7,
   distrust>2 or declareWar; peace—dangerous-enemy/allied-target reason,
   rank(A)=7 or distrust>3. That abandonment increments B.request and shows
   PerhapsThanksAnyway if paid, otherwise PerhapsBye, then returns `!paid`.
   If those gates fail, refusal still permits the treaty.
4. A nonzero solidarity reply declares war(A,live target,mediator B). Alliance
   with new-common-target reason additionally sets B↔target Contact+War and
   A↔target Contact. Alliance directly adds B→target Aggressor+AggressorLong.
   Set warTarget=-1 after those calls. Peace makes only the war declaration and
   target reset. Then call formAlliance(A,B) or signPeace(A,B), and return0.

**Refusal messages:** For alliance, the ordinary branch requires no B→A Vendetta,
distrust<3 and captured-city count=0. If A has <4 cities or
`rank(A)!=7 && (rank(A)-rank(B)<3 || rank(A)<5)`, choose NoSmall/reaction3
when category<4 and rank(A)<rank(B); otherwise NoThanks/reaction3 if category<4
and B.request<3, NoPatience/reaction3 if category<4, or NoDislike/reaction4.
Outside that rank/city condition use NoWinning/reaction4. For peace the ordinary
branch requires no Vendetta, distrust<5 and captured-city count<2. Choose
NoWinning/reaction4 if targetIsLeader or rank(A)>rank(B)+2; otherwise NoPatience/
reaction3 when attitude<51 and (rank(B)<=rank(A) or attitude<26), else NoDislike/
reaction4.

The remaining betrayal branch in either handler increments B.request. It shows
NoBetrayWeak/reaction3 if Vendetta is absent, distrust>2 (alliance) or>4 (peace),
and either fear is nonzero or `category(attitude<2)` is nonzero. The latter
passes the Boolean0/1 to category, which yields1 in both cases; do not substitute
`category(attitude)<2`. Otherwise show the appropriate NoBetray/reaction4.

### Demands, aid and withdrawal

Capture B.request. Proposals3/4 replace it with signed8(patience(A,B)); proposal5
increments it. Format. If E.declareWar or B→A HostileIntent/BrokenTreaty: format,
draw Q(0,2) for Provoke, clear declareWar, reaction4, set B↔A War, set
A.lastContact[B]=turn, show the war notice and return1.

For proposal3, when rank(A)<rank(B), set fear=rank(B)-rank(A). Tribute is
`clamp(fear,0,B.gold/50)*50`. If turn-E.lastContact<8, set tribute=0 and wanted=-1.
If that age<16, halve tribute. If A→B lacks Peace, halve again. B's Great Wall,
or UN if absent, sets tribute=0. If B→A Alliance, wanted>=0,
`A.count+difficulty < B.count`, and (mil(B)<mil(A) or rank(A)<rank(B)), set
tribute=0 and fear=1. A's Great Wall/UN gives a protection Boolean, overridden
false when distrust>6.

If B→A Peace without Alliance, attempt withdrawal unless **all** of:
(initially aggressive or attitude>74), fear=0, unprotected, and after drawing R(),
`B.government < 5 + R()%2`, B→A ChangedThisTurn absent and attitude>25.
The draw is consumed only after the first three gates. Withdrawal scans live
units ascending: live B unit with signed role<6, on land, a nearest A city exists
with retained nearest distance<3, and a nearest B city exists. Relocate to that
B city, clear visibility, set orders=None, redraw old coordinates and increment
moved count. Unit coordinates are not an independent output of this operation.

Adjust B→A +2 for proposal5, otherwise +10. For proposal3 use+5 when rank(A)<
rank(B), then override to0 when (A.count<B.count and rank(A)<rank(B)) or rank(A)<4.
If any moved, show Apologize/reaction3; proposal5 returns0 immediately.

Enter the refusal/negotiation branch when tribute=0 and (wanted<0 or fear=0),
or unconditionally for proposal5:

- If not allied, avoid provoking war if initially nonaggressive and attitude<76,
  or units moved, or any of fear/protection/ChangedThisTurn/attitude<26 is true
  and `(R()%3 != 0 || proposal==5)`. The last draw still occurs for proposal5
  when that expression is reached. Without moved units show NoViolators for5,
  otherwise Feeble, reaction3. If the avoid-war test fails, format, draw Q(0,2),
  clear declareWar, reaction4, set War, show Provoke, then set A's contact turn;
  return1.
- If allied, call exchange mode `2 + (capturedRequest<2)`. These modes never
  consume its coordinate arguments: the original caller's uninitialized x/y
  therefore do not create an input requirement on this path. If exchange returns
  nonzero, return0. Otherwise show Sympathy/reaction3 when A.count<B.count and
  rank(A)<rank(B), or rank(A)<4; else FeebleAlly/reaction3 when initially
  nonaggressive, attitude<51 and targetIsLeader=0, but only if no units moved.
  The remaining case shows TauntAlly/reaction4, cancels alliance and returns1.

Otherwise pay the demand: if wanted<0, or tribute!=0 and B.count<=A.count, show
GiveMore (GiveMoreAlly for proposal3)/reaction2, transfer cached tribute B→A and
redraw status. Else show GiveMoreCiv/reaction2, grant A live wanted from B, and
set wanted=-1. If B→A lacks Peace, first set Tribute when the meeting's
had-ceasefire value is zero, then set Ceasefire. Return0.

### An ally calls for help

Capture local human slot L. Admit only if L is human, ally→L Alliance, and
L→enemy lacks Alliance and War. Ask emissary(L,ally,-1,-1). Refusal adjusts
ally→captured L +100. Acceptance shows DemandHelp; reply1 immediately means war.
Other replies compute threat over regions1…62. A region qualifies when
`(ally.cityCount>1 ? 1 : 0) < ally.regionCities` and ally.regionMilitary is below
enemy.regionAttack. Add `enemyAttack*(Chieftain ? 4 : 2)/(allyMilitary+1)`.
If total threat is nonzero, set gold units=`clamp(threat,0,ally.gold/50)` and
payment=units*50. A nonzero payment shows HelpBonus. Reply1 subtracts units*50
from ally, adds cached payment to captured L, and redraws status.

If the latest reply is1, declare war(captured L,enemy,mediator ally). Otherwise
show DidntHelp; attitude<51 adjusts ally→L +100, else cancelAlliance(L,ally).
Reset the session after the admitted operation, including emissary refusal;
failed initial admission does not reset it.

## 6. AI/player audience controller

### Entry and opening order

Initialize local declared-war, ended, paid, propose, treaty-made, conceded,
hostile, tech-gap, senate-blocked and emissary-sent false. Set AI-meeting-active=1.
If scenario events are loaded and
[S02's negotiation check](S02-event-language-and-execution.md#negotiation)(A,B)
returns0, clear AI-meeting-active and return without the ordinary cleanup. Otherwise select the
evaluation pair (B,A), set gifts-this-meeting=0, and reset B.request=0 only for
AI-initiated meetings. Set initial-evaluation-complete=0, opening-evaluation-active=1,
evaluate(A,B), then set those flags to1 and0 respectively. Capture B→A Ceasefire
in shared had-ceasefire and capture no-contact from B→A.

For AI initiation, set A.lastContact[B]=turn, then bilaterally clear Ceasefire
and Tribute. For player initiation, clamp negative B.request to0; A→B Aggressor
sets it to signed8(patience). If A→B Incident, set B.request=0 and bilaterally
clear WarThisTurn. Otherwise, on difficulty above King, if both ships launched,
A's arrival is earlier and B→A War, bilaterally set WarThisTurn.

Set emissary response to the Boolean player-initiated value. For player initiation,
refuse audience when B→A WarThisTurn, or captured B.request>=fresh patience and
A→B MetThisTurn. Format if not yet asked; use OutaHere/reaction4, except the
patience rejection of an ally uses OutaHereAlly/reaction3. Set ended=true and go
to cleanup. Otherwise askEmissary(A,B,x,y); its result does not terminate this
parent immediately.

Set MetThisTurn bilaterally and emissary-sent=true. Capture hostile from B→A
CityCaptured and clear CityCaptured bilaterally. In the special scenario mode,
unordered pair{3,6} sets warTarget=0, best=-1, tribute=0 and hostile=0;
pair{3,1} sets best=-1, warTarget=0 and hostile=0. Perform exchange mode1.
If now nonaggressive, AI-initiated, B→A Ceasefire or Peace, and fear>2, go to
cleanup. Otherwise format. A positive war target1…7 that is no longer alive
becomes-1. Alliance plus spaceship threat and AI-outranks-target also clears it.

Perform sections below in this order: request war, ease allied demands, measure
technology gap, demand tribute, force propose if allied and category>3, demand
technology, dismiss ally, suppress captured-city aggression, reconsider aggression,
threaten war, offer alliance, ceasefire, peace, main conversation. Any specified
“end” jumps to common cleanup; it is not a session reset at that point.

### Requests and demands during the opening

**Request war:** if warTarget>0 and (requestMode!=0 or B→A Alliance), ask the
emissary. On acceptance show CancelTreaty with relation variant2, changed to1
for A→target Peace and0 for Alliance. Text uses aggressive/nonaggressive forms,
overridden by allied form. Reply0 adjusts B→A +5 when not allied; when allied
adjust +20 only if requestMode!=0, and set propose=true. Increment B.request.
Other replies declare war(A,live target,mediator B), set declared-war=true,
aggressive=0, strongestEnemy=-1, warTarget=-1. If not allied draw R(); only an
odd draw sets tribute=0 and, if had-ceasefire, sets Ceasefire. If allied, set
tribute=0 and paid=true without that draw. Format at the end of an admitted request.

**Ease allied demands:** only for A→B Alliance. If distrust>4 and unpaid, set
propose. If best>=0: when rank(B)<rank(A), zero tribute if B.count<A.count or
A.gold<B.gold; otherwise zero it only if both inequalities hold. Space threat
and AI-outranks-target set propose and zero tribute. Special scenario ordered
pair(B,A)=(6,7) additionally clears best and sets propose.

**Technology gap:** start false. If A knows either Mobile Warfare or Advanced
Flight, set gap if B knows neither. Otherwise, if A knows either Conscription
or Tactics, set gap if B knows neither. Otherwise set gap if A knows Gunpowder
and B does not. Finally A.count<=B.count and B knows Conscription clears gap.
Knowledge calls short-circuit in the written order; names mean fixed classic
advance identities, not renamed rule labels.

**Tribute demand:** require tribute>0, (requestMode=2 or propose), tribute<=A.gold,
and accepted emissary. If allied cap the demand at `max(A.gold/2,100)` when that
cap is <=the current demand. Draw Q(0,3). Variant3 becomes2 when mil(B)<mil(A);
variant2 becomes1 when Invention has no first discoverer; tech-gap overrides to8;
Alliance overrides to9. Show Tribute/reaction4. Only reply1 pays **live** tribute
A→B, clears aggressive, sets paid, redraws status, decrements nonzero B.request,
adjusts B→A by -goldValue(live tribute), formats, and restores Ceasefire if
had-ceasefire. Regardless of reply, set tribute=999 to record that a demand ran.

**Technology demand:** require (requestMode!=0 or propose), best>=0, tribute!=999,
and accepted emissary. Draw Q(0,2) for TakeCiv; tech-gap changes variants0/1 to8,
Alliance changes any to9. Reply1 grants B live best from A, then values live best
and adjusts B→A by `-(value*3/2)`. Move live second into best and clear second.
If AI-units-near-cities=0, aggressive=0, or new best<0, set paid. Otherwise show
OverABarrel/reaction4; reply1 grants the remaining best, adjusts attitude with a
fresh valuation, clears best and sets paid. When paid, clear aggressive,
restore Ceasefire if had-ceasefire and decrement nonzero B.request. A non-1 first
reply while allied adjusts B→A by `rep(A)+2-credit(B,A)` and increments request.
Format and set tribute=999 after any admitted technology demand.

**Dismiss ally:** if B→A Alliance, unpaid, and aggressive or attitude>49 or
(space threat and AI-outranks-target), ask emissary but ignore refusal; format
if asked remains0. Show Begone0 when tribute=999, else Begone1, reaction4.
CancelAlliance(B,A), set A.lastContact[B]=turn, and, with space threat above King,
set declareWar=1. End.

Then set hostile=`hostile && aggressive`. If true, clear aggressive. For first
contact, unpaid, tribute=999, currently nonaggressive, and category>
`(initiallyAggressive ? 2 : 4)`, query A's Great Wall then UN. If neither, draw R();
`B.government < 6-R()%2` sets aggressive=1.

### Threats, alliance and treaties

**Threats:** test these branches in order:

1. Aggressive and tribute>50, and either B→A Peace or (B is not hostile to A,
   E.lastContact>0 and B→A Incident absent): ask emissary ignoring refusal;
   format if asked=0; reaction4; draw Q(0,2) for Provoke.
2. Aggressive and tribute=999: format if needed, reaction4, use Reject0.
3. Aggressive and (E.lastContact<0, B→A Incident or player-initiated): ask
   emissary ignoring refusal; format if needed, reaction4, use Rid0.

Each chosen branch sets bilateral War, raises B's stored attitude toward A to75
only if it is below75, shows the war notice, sets A.lastContact[B]=turn, and ends.
Any still-aggressive audience ends without another threat. If suppressed
aggression and not allied, ask emissary; acceptance shows AccursedWall when A
has Great Wall, otherwise AccursedUN, reaction4.

**Offer alliance:** enter only with B→A Peace without Alliance and targetIsLeader=0.
Count all A→slots1…7 Alliances. Set propose to both ranks<6, replacing its prior
value. If propose, warTarget<1 and turn>200, scan slots1…7 excluding A/B, requiring
rank>=both ranks, A→slot Contact without War, and B→slot neither Alliance nor War.
Choose lowest rank, with later ties winning, into warTarget. Set tech-gap to
whether a positive target has any A→target Ceasefire/Peace/Alliance; this replaces
the earlier technology-gap meaning.

Offer only if all: (fear or propose or tech-gap), (peaceInclination>1 or propose),
A has no allies, (attitude<25 or attitude<50 with tech-gap),
(tech-gap or declared-war or propose), distrust<4, B→A lacks Ceasefire,
capturedCityCount<2, (rank(A)<6 or rank(B)<5), and emissary accepted.
Show ProposeAlliance; **reply0** admits the next stage. Set propose=true. If
warTarget>=0, set propose=false and show PerhapsSolidarity/reaction3. Reply1
declares war(A,target,B), adds B→target Aggressor+AggressorLong, clears target,
and sets propose=true. Other replies adjust B→A +10, format, and may show
PerhapsDidntProve/reaction4 if fear=0, distrust>2 or attitude>25; propose remains
false even when that further message is skipped. If propose, formAlliance(A,B)
and format. Set treaty-made=true whenever the initial proposal reply was0,
even if solidarity failed and no alliance was made.

**Offer ceasefire:** if B→A War and emissary accepted, show GrantCease if paid,
otherwise WallCease if suppressed-aggression or hostile, else ProposeCease.
Reply0 accepts. For a nonzero refusal, query B's Great Wall then UN. If neither,
call V04 senateIntervenes(A,B). Zero invokes the hawks notice below; nonzero
shows OverruleCease, forces choice0 and sets senate-blocked. If B has either
wonder, re-query Great Wall to choose WallOverCease versus UNOverCease, force
choice0 and set senate-blocked.

Let d=distrust+1, doubled for B→A Vendetta. For a still-nonzero choice, compute
roll0 without a draw when `d==1 || s32(d-1)<0`; otherwise use `R()%d`. This exact
gate matters for an accepted minimum signed value. On roll0 try concessions in
sequence, each only while choice remains nonzero:

- Fear>3 and wanted>=0: GiveCiv/reaction4; any nonzero answer forces choice0,
  grants A live wanted from B and sets conceded. It does not clear wanted.
- Cash=`clamp(fear*2-4,0,B.gold/50)*50`, halved if B has Great Wall or UN.
  If nonzero, GiveCash/reaction4; a nonzero answer transfers cached cash B→A,
  forces choice0, redraws status and sets conceded.
- Fear!=0, B has <2 cities, and neither B wonder: Grovel/reaction4. A nonzero
  answer transfers all **live** B.gold to A, zeros B.gold, forces choice0,
  redraws, sets conceded and scans advances0…99 ascending. Grant each one B
  knows, A does not, and whose prerequisites are not both Disabled.

If a concession forced choice0, set E.lastContact=turn. Still-nonzero choice
shows ContinueWar/reaction4, sets A.lastContact[B]=turn and ends. Otherwise set
treaty-made, agreeCeasefire(A,B), format, and show Worthless/reaction4 if distrust>6.

**Offer peace:** only while B→A lacks Peace. Threshold starts3; if
unsigned2*mil(A)<mil(B), set2; if unsigned3*mil(A)/2<mil(B), decrement;
if mil(B)<mil(A), increment; if unsigned2*mil(B)<mil(A), increment again.
Admit when suppressed-aggression, or ((paid or no-contact) and no Ceasefire),
or all of (fear or category<=threshold), targetIsLeader=0 and
`distrust+capturedCityCount < fear+4`. Require accepted emissary and show
ProposePeace; reply0 accepts.

For refusal, evaluate `B has neither Great Wall nor UN`, then, only if false,
`senateBlocked && R()%2==0`. If either true call senateIntervenes(A,B).
Its zero result, or (senateBlocked and a further even R() draw), keeps refusal;
show hawks notice only when B→A ChangedThisTurn absent. Otherwise show
OverrulePeace and force choice0. When the earlier condition was false, choose
WallOverPeace/UNOverPeace by a fresh B Great Wall query and force choice0.

If still refused, calculate d and the roll exactly as for ceasefire. On roll0
compute the same wonder-adjusted cash, and show CashForPeace if nonzero. Any
nonzero answer transfers it B→A, forces choice0, sets conceded and redraws.
Set treaty-made=true after the admitted peace proposal, including refusal.
Choice0 signs peace(A,B) and formats; otherwise show NoPeace/reaction4.

**Hawks notice:** only A.government>Fundamentalism, not a scenario with senate
suppression, and not autoplay. Show ContinueUN if A has active UN, otherwise
ContinueHawks, with B's plural. This helper does not change treaty state.

### Main conversation and cleanup

Enter the main conversation if asked, player-initiated, or (B→A lacks Peace
and at least one of fear, B→A War, B→A Contact absent). If distrust>6, count B's
alliances across slots1…7; B→A Vendetta plus (any ally, targetIsLeader or
mil(A)<mil(B)) sets E.declareWar=1. If no-contact, ask emissary.

For an AI-initiated meeting with asked=0 or response=-1, use the forced-treaty
continuation: when declareWar=0, query B Great Wall/UN. Without either, invoke
senateIntervenes; zero shows hawks notice, nonzero can set Peace when absent
and fear!=0, then sets A.lastContact[B]=turn+16. Re-read War; if still present,
show SenateCease, set Ceasefire and contact turn=turn. With B's wonder, the
same peace-then-war tests occur only inside a no-Peace gate, using fresh
Great Wall queries for the Wall/UN variants. These use bare treaty setters,
not signPeace/agreeCeasefire wrappers.

Otherwise format if asked=0. If treaty-made, denounce a notorious third party:
unless player-initiated with B→A Vendetta or Aggressor, scan slots1…7 excluding
A/B and requiring **B→slot War absent**. Do not filter alive status or A→slot War.
Among A's credits
toward these slots choose the smallest <=-1, later ties winning. Increment that
credit, then show NotoriousBig when mil(A)<mil(B), else NotoriousSmall, reaction3.
A callback can have changed B→target War after selection, in which case show
NotoriousHate/reaction4. If no candidate, no notice.

If no treaty-made, greet: tribute=999 and unpaid gives AttitudeAlly,
AttitudePeace (Ceasefire or Peace), or Attitude, reaction4. Otherwise an
AI-initiated meeting uses WelcomeAlly/WelcomePeace/Welcome, reaction2, with
address form initially stern; absent Vendetta changes it to mild for
mil(B)<mil(A) or (category<4 and distrust<3), and to fearful when fear!=0.
Player initiation uses HowdyAlly/HowdyPeace/Howdy at request<2, reaction2;
at request>=2 use DoodyAlly/reaction2 or Doody/reaction3 (after reaction2).

Without B→A Peace/Alliance, invoke V04's border-intruder count(B,A). A nonzero
weighted count shows PleaseCity when the selected **city index** is<2, otherwise
PleaseCities, with selected city and B adjective. Do not test the number of cities.
For A→B Alliance, inspect regions1…62 for strongestEnemy>0. Count qualifying
regions using `(B.cityCount>1)<B.regionCities` and B.regionMilitary below enemy
regionAttack. Any shows AllyPlea/reaction2. Otherwise inspect weakestEnemy>0
using its city/military summaries against B's attack; any shows AllyBrag/reaction3.

Then loop: clamp negative B.request to0; show proposal menu. Reply0 sets ended
and goes to cleanup. Reply7 runs FavorMenu;8 GiftMenu; others run response
dispatch, whose nonzero result ends. Increment B.request, capture the signed
result, calculate fresh patience, and repeat while captured<limit. Otherwise
format and show PatienceAlly or Patience/reaction3.

Common cleanup:

1. Clear Incident bilaterally. Clear B→A HostileIntent if declareWar=0 or A→B
   has Alliance/War; otherwise directly set it. If not ended, increment B.request.
2. If conceded or hostile, repeatedly recompute patience and increment the signed
   request count while `request < s32(hostile+patience)`. Off-nominal wrap can
   prevent termination; a declared bounded stop may retain the completed prefix.
3. Reset session. If had-ceasefire, B→A now lacks Ceasefire and A→B lacks
   Peace/Alliance/War, show CeaseExpire.
4. On Chieftain, without B→A War, local-human→B Embassy, local UN or local Marco,
   and if B knows Writing: show Ambassadors with the Diplomat unit, directly
   set A→B Embassy, then open intelligence(A,B).
5. If Tutorial, emissary-sent, (asked or player-initiated), response!=-1,
   B.cityCount>4, rank(A)+1<rank(B), and B→A War, show Schism tutorial. These
   asked/response reads occur **after reset**, not from the earlier reception.
6. Clear AI-meeting-active.

## 7. Two-human audiences and parley state

### Request and wait

V04's two-human contact calls this request continuation. Set meeting reply=-1
and multiplayer-pending=1; send the audience request to B with A as requester.
Classic **does not clear** a stale cancel count here. Show ParleyWaiting with
B's leader/plural, a fresh timer and the meeting-wait idle callback.

The idle callback processes network messages, then reads time. End the current
modal when elapsed ticks>=timeoutSeconds*60, reply!=-1, or cancel count!=0.
On exit it sets reply=0 only if reply was still-1. It does not clear cancels.

After the waiting dialog, test in order:

1. Nonzero host-handoff stage: show Busy, clear pending.
2. Nonzero cancel count: clear it, show Cancel with B, set other civilization=None
   and clear pending.
3. Modal result=-1: send audience cancel to B, set other=None and clear pending.
4. Reply<1: show GoAway and clear pending.
5. Reply=1: set reply=-1, other=B, restart the timer/wait callback and show
   ParleyOK with A. Afterwards, handoff takes priority over cancel; either
   clears other and pending, with Busy or Cancel (also clearing cancel count).
   Otherwise open parley with B in composing state.
6. Other reply: show Busy and clear pending.

Busy/Cancel/GoAway notices use the ordinary timeout idle service. The Foreign
Minister request first sets Contact and RecentContact bilaterally, then uses the
same wait/accept continuation but lacks the first
handoff check immediately after ParleyWaiting; it checks handoff after ParleyOK.
P07 owns report selection and the prerequisites for invoking that request. Do not
silently unify the two callers in a way that changes this priority.

On the receiver, first set both local/requester contact turns to current turn,
even if already busy. If other civilization is not None, send busy reply2 and
return. Otherwise mark other=RequestPending, compose the request text and show
ParleyRequest with its network autoclose idle callback. Stored requester→local
attitude0 uses display category0; other values use V04's category function.
After the dialog: handoff sends busy2, clears other/pending. Otherwise send the
full modal reply only if cancel count is zero, then re-read that count. With
no cancel and reply1, release the queued popup, clear popup-in-progress, open
parley in waiting-for-offer state and finish the caller's dialog. On cancellation,
clear its count and show Cancel. Other nonacceptance clears other and pending.
N02 supplies receipt/counter increments, queuing and the release ownership.

### Parley modes and local selection

Parley states are ReviewIncoming, AwaitResponse, AwaitOffer, ComposeAction,
ChooseBarterKinds and the concrete offer modes below. Opening a negotiation
selects the parley surface, resets selected action to None and clears both local
and remote unit/city-detail disclosure flags. Opening in ComposeAction derives
its panes from the no-action selection. An already-open surface is shown without
replacing its existing other-civilization/mode fields. A closed one receives
other and (unless opening in ComposeAction) the requested mode, rebuilds panes,
resets list offsets to21, shows and restores edit carets. Sound is selected by
`((map.seed+other)&7)+83`; this uses no random draw. Chat-only opening and its
contact filters remain [P07](P07-advisors-reports-and-history.md#21-chat-visibility-messages-and-history)/N01, not gameplay offer acceptance.

Select an action to choose Treaty, JointWar (or MapExchange for the alternative
proposal selection), Gift, Barter or Demand. Gift kinds are technologies, gold,
units, cities, maps in that order. Barter first chooses left/right kinds, then
enters concrete barter. The barter/demand kinds are gold, technologies, units, cities, maps, war targets
and treaty, in that order. Choosing Gold disables Gold on the opposite side and
moves an opposite Gold selection to Technologies; it enables Treaty on both.
Choosing Treaty disables Treaty on the opposite side and moves an opposite Treaty
selection to Gold; it enables Gold on both. The other kinds enable both Gold and
Treaty on both sides. [P07](P07-advisors-reports-and-history.md#22-chat-and-parley-windows-and-panes) owns these pane identities and composition, using P04’s shared controls; N02 must still
admit the actual packed kinds.

A standalone treaty pane offers Ceasefire, Peace, Alliance and Cancel; barter
and demand omit Cancel. In order, disable each of Ceasefire/Peace/Alliance that
already exists in local→other. If the current selection is the disabled
Ceasefire or Peace, advance to the next; disabled Alliance becomes Cancel in a
standalone pane and Ceasefire otherwise. Standalone Cancel is disabled when no
such treaty exists and a Cancel selection then becomes Ceasefire. These are
ordered adjustments, not a search that guarantees a final enabled choice.
The pane marks itself selected even with accepted contradictory treaty flags.

Technology rows scan0…99 ascending. Permission to see the other's technology
comes from local UN, local Marco, local→other Embassy, local→other Alliance or
reveal-map, in short-circuit order. A giving pane lists local-known technologies,
excluding other-known ones only when that information is available. A receiving
pane with permission lists other-known/local-unknown technologies; without
permission it instead lists local-unknown, enabled==1 advances with index<89.
It does not prove the other civilization owns those choices. A demand's pane
and the right barter pane are receiving panes. Initial selections are zero.


War-target rows scan civilizations1…7 ascending, excluding local and counterpart.
They require local→target Contact unless the raw UN location is anything other
than Unbuilt, local has active Marco, or reveal-map is enabled. There is **no
alive or existing-war filter** here; this differs from the AI FavorMenu target
list. The UN check is raw global location, not local active-wonder ownership.
Initial selections are zero.

City rows scan ascending live slots owned by the selected side; the other side's
cities additionally require local knowledge of the city. Each row retains index,
unique ID and the live count of **all** units at its coordinates, regardless of
owner. Unit rows similarly require selected-side ownership and, for the other
side, local visibility; they also require a neighboring land tile or exactly one
unit in the stack. Selection preservation saves nonzero selected IDs, rebuilds
and reselects matches (allocation failure discards preservation). Cities preserve
for any nonzero request; units reselect only when that request equals1. [P07](P07-advisors-reports-and-history.md#sorting-and-rebuilds) owns
the subsequent city/unit sorting and its tie order; the transaction consumes that
resulting order, not a new ascending-slot scan. Detail disclosure and list
refresh can change these rows before an offer is packed.

NeverMind changes concrete offers and ChooseBarterKinds to ComposeAction;
concrete Barter goes back to ChooseBarterKinds. ComposeAction stays unchanged.
It does not universally clear selected action or sent-offer storage. Send is
available in concrete one-sided offers when the right side has a selection;
Barter requires both sides. AwaitOffer and AwaitResponse disable send. Detail
reveal/conceal is offered while AwaitOffer according to local disclosure flags.

When sending, discard the preceding retained offer, construct the new offer
from local human, other civilization, current mode and selected rows. Pack the
right pane first; Barter additionally packs the left pane second. Only rows
whose selection equals1 are packed, in displayed row order. Technology/target
rows carry indices; unit rows carry unique IDs; city rows carry unique IDs and
an accompanying unit count. Gold uses the signed decimal parser's result, maps
the current map choice, and treaties the current treaty kind. N02 owns lengths,
framing, validation and allocation/packing failure; an allocation failure returns
no offer, rather than a completed transaction.

For cancellation-treaty mode, after attempting to send, clear just the first
present relation in priority Alliance, Peace, Ceasefire via V04; refresh panes
and remain in the current mode. This happens even when offer allocation failed.
Other successfully constructed offers enter AwaitResponse and disable NeverMind.
No ordinary asset transfer occurs merely from clicking Send or Accept.

Accept sends the accept message, shows ParleyAccept2 with timeout idle, then
selects ComposeAction through the retained selected action. CounterOffer sends
counter, then also selects through the retained action. NoThanks sends decline
and enters AwaitOffer. These callbacks operate on the active parley surface;
an absent/invalid receiver is outside nominal inputs, not an invented no-op.

### Ordered reply processing

First process network messages and select the parley surface. Handle **one**
applicable branch in this priority order, clearing the selected counter to0:

1. Transaction counter while AwaitOffer: a cancellation treaty directly clears
   the first present Alliance/Peace/Ceasefire flag with its notice and leaves
   the current mode. Otherwise enter ReviewIncoming and rebuild panes.
2. Accept counter while AwaitResponse: if a retained offer exists, change its
   message role to Execute; the host executes locally, clients send Execute to
   host. Release the retained offer after that call and clear its reference.
   Enter AwaitOffer, rebuild panes, then show ParleyAccept with this same reply
   processor as idle callback.
3. Decline counter while AwaitResponse: enter ComposeAction, set selected
   action=None, derive/rebuild panes and show ParleyNoThanks with this processor
   as idle callback.
4. Counteroffer counter while AwaitResponse: enter AwaitOffer, rebuild panes,
   show ParleyCounterOffer with this processor as idle callback.

A counter with the wrong current mode remains pending. Counters are cleared,
not decremented one message at a time. The callback can re-enter and nest another
response dialog while the earlier one remains open; B7 records this behavior.
Do not replace it with an assumed atomic request/response round trip. N02 owns
the received-offer replacement and authority checks preceding these callbacks.

Closing an open parley releases any retained offer first. Send cancel only when
other is valid and both cancel and accept counters are zero. If other is valid,
cancel is nonzero and accept zero, instead clear cancel and show ParleyCancel.
Clear multiplayer-pending. Then mark closed, set other=None, hide the surface,
raise/focus the main frame and flush input. An accept counter can suppress the
outgoing cancel without being consumed by close.

[P07](P07-advisors-reports-and-history.md#gold-edit-and-disclosure-controls)
owns the disclosure and peer-intelligence callbacks, including ordered sends,
control updates and report admission. Those presentation choices neither
transfer assets nor constitute a W03 map exchange.

## 8. Executing multiplayer offers

The admitted offer has proposer A, counterpart B, a concrete mode, a first
(right-pane) item and, for barter, a second (left-pane) item. A null offer does
nothing. A nonnull unknown mode still executes the publication/elimination tail.
There is no implicit all-or-nothing transaction, balance reservation or rollback.

| Offer mode | Ordered operation |
| --- | --- |
| Treaty | Apply first treaty to(A,B). |
| Technology gift | Transfer listed advances A→B. |
| Gold gift | Transfer first amount A→B. |
| Unit gift | Transfer listed units to B. |
| City gift | Transfer listed cities to B. |
| Map gift | W03 directional maps A→B, first map kind. |
| Joint war | Apply listed targets for A, then the same list for B. |
| Map exchange | W03 directional maps B→A, then A→B, same map kind. |
| Barter | Execute **second** item as A's giving side, then **first** as B's giving side. |
| Demand | Execute first item as B's giving side. |

A giving-side item dispatches Treaty to(A,B), advances/gold from giver to other,
units/cities to other, war pact for giver, maps from giver to other. Unknown item
kinds have no gameplay operation; valid surrounding operations and tail still
run. The two treaty item kinds use the same treaty helper.

After dispatch, publish state. Refresh status for B locally or send B an info
update, then do the same for A. If B's live unit-total and city count are both0,
invoke V01 destruction check(B,A). Re-read A's counts and, if both0, check(A,B).
Callbacks and the first half of barter can change what the second half sees.

### Treaty, gold and technology items

Treaty kinds set Ceasefire, Peace or Alliance using V04. Cancellation checks
A→B Ceasefire then Peace then Alliance, clearing each present flag in that order.
This differs from the UI cancellation's highest-treaty-only branch. There is no
implicit alliance expulsion or declare-war penalty. A war-pact item sets War
between the giving civilization and each target in list order, without generic
war-declaration notices or reputation charges.

Gold transfer caps amount only at the donor's **live upper balance**:
if donor.gold<amount, amount=donor.gold. Subtract from donor, add to recipient,
then adjust recipient→donor by `-(goldValue(amount)*3/2)`. No lower clamp exists;
a negative supplied amount or negative donor balance can reverse the transfer.
Technology transfer walks the captured list count in order; for each index,
query donor knowledge then, only if known, recipient knowledge. Grant only when
the donor knows and recipient does not, using V03's suppress-dialog flag1 and
ordinary final flag0. Each grant completes before testing the next item.

### Unit lookup, displacement and transfer

Lookup by unique ID scans live slots descending and picks the first matching
live unit. Check its eight neighbors in W01 direction order for in-bounds land.
Without any land neighbor, require a whole-stack count of exactly1 or return
“Ineligible” (distinct from missing). Do not silently pick an earlier duplicate
ID when the last matching unit is ineligible. City ID lookup also scans descending
but has no unit-coast test. No donor-owner authorization is added by these
helpers; N02 owns message admission, while the transfer acts on live records.

`dropTile(unit,recipient,mustMove)` initially outputs the unit's current position
and succeeds immediately if mustMove=0, no city there and stack count<=1. That
fast path performs no terrain/recipient check. Otherwise examine W01's ordered
B[0…44] neighborhood around that captured position, wrapping x. Require a valid
tile, and when mustMove a different coordinate. It is admissible if its city
owner is recipient **or** unit owner is recipient **or** both are None. Require
land for a land unit, ocean for a sea unit, any terrain for an air unit. Return
the first match. Failure returns0 with the final candidate coordinates retained.

For each unique unit ID in offer order, look it up and capture its current owner.
Skip missing/ineligible units and units already owned by recipient. A sea unit
with stack count>1 first repeatedly removes a companion: choose first stack unit,
or its successor if first is the offered ship. Find a must-move drop for the
companion's own owner. Failure cargo-deletes it; success relocates it. Re-query
the ship stack and continue until count<=1. This can remove other ships and
units, not just a reconstructed passenger list. These effects remain even if
the offered ship later cannot be transferred.

Find a non-must-move drop for the offered unit and recipient. Failure skips
ownership transfer. Success, in order:

1. Increment recipient active-type count, decrement captured old owner's count.
   Set owner, home=None, movement spent=0, orders=None.
2. Find nearest recipient city from the unit's **old** position, before relocation;
   if valid and still owned by recipient, set home to its retained city value.
3. Mark the unit seen by recipient. For signed role<5 increment recipient
   military count. Increment recipient unit-total. If old military count is
   nonzero and the live type role<5, decrement it; decrement nonzero old total.
4. Relocate to the chosen drop; invoke W03 surroundings with allowContact=0.

After all items flush the outgoing send buffer with timeout5000. Unit/stack
operations are [U01](U01-unit-lifecycle-and-stacks.md), not plain coordinate stores.

### City transfer

For each offered city ID, find the descending live match; if found and still
live, cede it. The accompanying displayed unit count is ignored by execution.
Duplicate IDs can therefore invoke cession repeatedly. No same-owner skip occurs.

Cession retains an original position and old owner, but uses a **mutable current
city index** initially equal to the offered city:

1. Remove Palace, Temple, Cathedral, Courthouse, in that order. Set recipient's
   last-city turn to current turn; increment its city count; decrement old
   owner's city count only if nonzero. Begin map batch.
2. For valid B[0…20] around the captured position, set radius owner to recipient
   and merge its visibility, in order. Set current city's owner=recipient;
   reveal/remember that city to **old owner**. Invoke the scenario city-taken
   event if loaded. Set captured center tile's owner to recipient.
3. Capture unit upper bound once and scan descending. For a live old-owner unit,
   if its unsigned home value equals **current city index** and its position
   differs from that city's live coordinates, cargo-delete it. Otherwise, if
   positioned at that city's coordinates, increment recipient type count,
   decrement old-owner type count, change owner, clear home/movement/orders,
   then replace **current city index** with the nearest recipient city to the
   unit. Set the unit's home if that result is valid and still recipient-owned.
   Mark seen and update military/unit totals as in unit transfer, with the same
   nonzero old-count guards. Later units and the following stages consume the
   replacement index, not necessarily the initially offered city.
4. Check current city's production with C06 unit/improvement availability for
   recipient. If unavailable set Settlers, then scan unit types62…0 descending
   for the first available Defend-role type. Score=`(signedDefense*8)/signedCost`,
   plus1 for double-versus-horse. Use that type only if score>0; break after
   the first eligible type even when its score is nonpositive. A zero cost
   requires the F01 divide-fault boundary, not an invented score.
5. Redraw neighbors of current city's live coordinates; publish state; send
   draw-area there. For the **original** center, merge recipient visibility and
   redraw if previously absent. Do the same for its eight valid neighbors in
   direction order. Flush the map batch and send draw-area at original position.
6. Set current city's founder=old owner and captured turn=retained current turn.
   If Leonardo's active wonder city equals the current index, invoke C08's
   recipient upgrade pass.

The index replacement is behaviorally significant when it names another valid
city. If it yields -1 or another invalid record, reads require explicit backed
input or a stop; writes outside the city collection are **excluded bugs**.
The independent reconstruction's mapping of city[-1] writes into civilization7
storage is evidence of the bug, not a cleanroom implementation requirement.
Do not implement this cession through C01's elementary owner setter: removal,
unit processing, visibility, events, production and publication differ.

## 9. Worked cases and failure boundaries

| Input or continuation | Required result |
| --- | --- |
| Gold gifts49,50,150,250,350. | Gold attitude values4,5,11,16,20. Block divisions round separately. |
| Attitude61, A has Eiffel, B→A Peace+Alliance. | Patience5; adding War changes it to2 after the Eiffel query. |
| Sale value1, one known civilization, neutral attitude, no alliance; buyer balances1500,1501,3000,3001. | Prices200,300,300,450. An affordable refusal returns1 without moving gold. |
| A pays200 for a sale, and the status callback replaces wanted. | Grant the replacement wanted; seller receives no money. |
| Three A-only technologies tie in ascending scan. | Last becomes best, preceding becomes second. |
| An exchange shows technology X wanted by A. | After A's grant to B, A runs the captured-technology chooser; the displayed X is not itself the grant instruction. |
| An alliance negotiation pays, then rejects a required joint war. | Keep the payment, increment request and return0 because paid; the audience need not end. |
| Aid invokes exchange mode2/3 with caller x/y never initialized. | Those modes return before coordinate reads. No invented coordinate or stack preset is needed. |
| ProposeAlliance reply0, then solidarity reply0. | No alliance; treaty-made still becomes true for subsequent conversation selection. |
| A offers50 gold with50 available, B offers100 with0 available. | Barter first credits B with A's50, then caps B's payment at50; both return to their starting balances. |
| A parley advance list repeats an already-granted advance. | Re-query live knowledge; do not grant it a second time. A prior grant can also change eligibility of a later entry. |
| A ship's companions are displaced, then its own destination search fails. | Keep the companion effects; skip only the offered ship's ownership transfer. |
| A cession's nearest-city result changes to another valid city. | Later unit tests, production and final city fields use that current index; original-position revelation still uses the captured center. |
| That result is-1 and a later operation would write a city field. | Excluded out-of-bounds-write bug. Safe rejection/stop; no civilization7 corruption requirement. |
| A stale cancel exists when an audience begins. | Classic can cancel the new audience; do not silently apply B6's fresh-request clearing. |
| Reply counters are queued for multiple kinds. | Current mode and transaction/accept/decline/counter priority decide which one is consumed; reentrant notice callbacks can consume another. |

The standalone source checks below exercise the gold, patience, sale, survey,
war-distrust and admitted transaction examples, including live-selector and
barter-order cases. The audience, military gift, displacement, cession and native
session cases are specifications/source reviews with integration obligations,
not claims of newly executed original-program traces.

Accepted input handling must distinguish these boundaries:

- Scalar overflow and retained signed counters can change prices, willingness,
  loop termination or prompt variants without accessing invalid memory. Preserve
  those operations on the declared domain. Zero-divisor/quotient faults and
  nontermination require a declared stop or a narrower accepted domain.
- Oversized labels, gold-edit output, lists or packets that would overwrite an
  owner are excluded bugs. A growing text/list representation or bounded reject
  is acceptable safe behavior, declared separately from original conformance.
  In particular a gold-edit caret underflow must not write before its buffer.
- Callback mutation can invalidate a live index after an earlier valid choice.
  Do not turn a failed lookup into an adjacent-record write. Preserve/reject the
  valid prefix under X01, with the stopping point observable in a test.
- A missing/invalid received-offer reference, unsupported kind-table index or
  truncated list is not defined to produce an empty valid offer. N02 must define
  safe admission/failure before executing the gameplay continuation.
- A destroyed receiver or unknown read backing is not recovered by zeroing all
  session state. Ownership cleanup and recursive callbacks need their P04/N02
  contracts; an unsupported continuation may stop explicitly.

## Appendix A. Reference encodings for validation

These details identify original inputs and traces. A new implementation can use
named states, dynamically owned text, stable object IDs and different records.
F06/N02 own persistent/wire layouts; no packed struct is required for gameplay.

- The selected executable uses 32-bit arithmetic and pointers. Request count and
  reputation credit are signed bytes; attitude/reputation unsigned bytes;
  gold and meeting scratch are full signed32. Game/contact turns and city counts
  are retained signed16. Per-type unit counts retain bytes, military counts
  signed16, civilization unit totals signed32, unit home a byte whose None is255.
  A home comparison during cession zero-extends that byte before comparing it
  with the full current city index. Captured-city turn stores the low eight bits.
- Technology identities include Advanced Flight0, Alphabet1, Conscription17,
  Guerrilla Warfare34, Gunpowder35, Invention38, Map Making46, Mobile Warfare53,
  Tactics81 and Writing88. The exchange branch comparing best against fixed
  unit prerequisites uses the six unit identities in section3. Technology0 is
  excluded by explicit `best>0` payment tests even though it is a valid technology.
- Trait reason bits in alliance willingness are1 allied target,2 dangerous enemy,
  4 new common target. Peace reason bits are1 ordinary willingness,2 dangerous
  enemy,4 allied target,8 wonder compulsion. These sets are distinct.
- Treaty codes are in V04 Appendix A. The mercenary initial set is `0x2401`
  (Contact|RecentContact|War); its direct aggressor extension is `0x80800`;
  Mercenary is `0x100000`. The initial scenario overrides require Scenario and
  the special scenario flag `0x8000`. These bit patterns are not required internal
  representations.
- Parley modes0/1/2/3 are ReviewIncoming/AwaitResponse/AwaitOffer/ComposeAction;
  4 is chat-only;5 chooses barter kinds;6 Treaty;7 Technologies;8 Gold;9 Units;
  10 Cities;11 Maps;12 JointWar;13 MapExchange;14 Barter;15 Demand. Treaty kinds
  0/1/2/3 are Ceasefire/Peace/Alliance/Cancel. Item kinds5/6 are treaty,8 advances,
  9 gold,10 units,12 war targets,13 maps,17 cities. None=-1 and RequestPending=-2
  are distinct other-civilization sentinels.
- Original offer header is32 bytes and each item header12 bytes. A list value is
  a signed32 count; advance/target/unit elements are signed32 words, city elements
  are pairs of signed32 ID and displayed unit count. Right item precedes the
  left item in the payload even though barter executes left first. N02 owns
  framing, sizes, validation and authority, including rejected malformed packets.
- Exchange alternative label ordinal107 originally used an80-byte temporary.
  Independent BF-200 documents both an oversized admitted raw label and
  substitution expansion beyond a fixed caller output. Its growing substitution
  result and the primary bounded-copy/expansion path are repairs, not evidence
  that the original accepted arbitrary text without corruption. Neither requires
  preserving the overwritten stack.
- Original cession reassigns its incoming city argument at `004de4b0`. The
  independent reconstruction can map a resulting city[-1] byte access into
  adjacent civilization storage. Under this collection's X01 rule, all such
  writes are excluded, even when that storage exists and the result is repeatable.
  Reads from that path require separate evidence/backing or a stop.

## Appendix B. Evidence and validation

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary source revision:
`a71e8aefffe04e6f719e0e213b027b128a747711` in OpenCiv2. The selected bodies were
read in checkout `fc2155ccdb2123c1137babab6fa39e38152278c9` and **76 scoped
function bodies** were compared with that pin. The independently reconstructed
source at `civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, was consulted for the exchange's
coordinate-read gate, post-callback selector reads, text bounds and cession's
mutable city index. It was not used to import source into this repository.

Original executable: Civilization II MGE **5.4.0f, patch3**, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
The file hash and clean reference worktrees were verified. No new original-game
replay, native Windows build, Ghidra investigation or annotation update was
performed for this document. Previously archived runtime evidence was not
re-read and is not claimed as a newly executed conformance test.

| Evidence | Scope |
| --- | --- |
| [diplomacy.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/diplomacy.cpp), patience `00456f8b`, format `00458a3b`, greeting `00458ab1`, reception `00458df9`, reset `0045918e` | Admission, counter tests, secondary greeting draws and reset order. |
| Same file, sell `004591cb`, exchange `0045950b`, help `0045b0d6`, gold value `0045b472` | Survey ties, offers, price rounding, live selections and allied help. |
| Same file, proposal response `0045b4da`, favor `0045dd7f`, gifts `0045f0b1`, hawks `0045fd67`, menu `0045fe19`, audience `00460129` and their private helpers | Complete ordered player/AI negotiation, calls, replies and cleanup. |
| Same file, two-human contact continuation of `0055d8d8`, close helper | Wait/cancel order and retained session state. |
| [network.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/network.cpp), reply processor `004b81dd`, offer packing `004db690`, execute `004dd285` | Stateful protocol continuation, packing versus execution order, publication tail. |
| Same file, cession `004de0e2`, units `004de990`, drop tile `004ded07`, ID lookups and item helpers | Distinct asset transfers, current-city replacement, partial effects, selection. |
| [window.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/window.cpp), meeting wait `00430822`; [reports.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/reports.cpp), Foreign Minister audience request | Timer/counter exit and report-caller difference. |
| [BUGS.md](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md), B6/B7 | Stale cancellation repair versus classic behavior; reentrant response notices. |
| Independent `src/diplomacy_negotiation.cpp`, `src/multiplayer_request.cpp`, and `analysis/reference/bugfix-log.md` BF-200 | Coordinate arguments unused by modes2/3, original selected-index timing, cession overwrite evidence and bounded text ownership. |

### Checks performed

A standalone source-derived C++ harness compiled **11 complete V05 function
bodies**: patience, gold value, exchange survey, sale, war price, war distrust,
parley treaty, war targets, gold, advances and complete transaction dispatch.
It joined the real V04 set/clear treaty bodies. Both optimized and
AddressSanitizer/UndefinedBehaviorSanitizer builds passed **70 assertions**;
leak detection was disabled. Pointer arithmetic for packed signed32 item words
and V04 relation fields was adapted to the 64-bit host; arithmetic used signed
wrap retention. No invalid write was replayed as a required result.

The fixtures supplied knowledge, wonder/AI value queries, technology-grant
callbacks, attitude adjustment, city/unit/map transfers, destruction and
presentation/network services. They checked price boundaries, uncapped positive
prices, refused/accepted sale results, the live selector after redraw, survey ties
and disabled entries, war distrust, gold upper-cap/negative behavior, ordered
barter/maps, repeated advance knowledge tests, publication/elimination order and
null versus unknown-mode dispatch. These are finite source checks, not native
protocol validation or complete V03 grant/C08 effect integration. The audience,
response and asset-transfer bodies outside that set were source-reviewed, not
executed by this harness. Source-pin, Markdown links/anchors, specification IDs,
filename IDs and whitespace checks accompany the document.

### Remaining integration

Join A08's actual stance/valuation/willingness, V03 grants and captured selection,
[C06](C06-city-production-and-purchasing.md)/[C08](C08-buildings-and-wonders.md) eligibility/effects,
U01/W03 real movement/revelation, [V01](V01-civilization-lifecycle-and-civil-war.md) elimination and
S02 events with these parents. P04/P07 must supply actual menu resources, replies,
list ordering/filters, disclosure, leader reactions, lifetime and callback effects;
N01/N02/N05 must supply authority, request queues, message/counter receipt,
transport and synchronization. X02 needs independent full-state/random/message
traces for accepted exchanges, failed solidarity after payment, partial ship/city
transfers, reentrant session closure, admitted scalar wrap and safe stops before
excluded writes. A supplied valuation, grant, packet or UI callback is not evidence
that its actual producer has been integrated.

## Appendix C. Original presentation reference

The following original input geometry and drawing procedures are optional under
X01's presentation policy. The main text retains semantic selections, calculations,
file/wire effects and required ordering. This appendix is for original-adapter
validation, not a required UI design.

### Original emissary dialog position

position derives from the screen rectangle: each offset is
`clamp(trunc((dimension-640 or 480)/3),1,9999)` and the default-dialog offset
uses the negative values.
