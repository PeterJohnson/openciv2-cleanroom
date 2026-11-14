# Classic compatibility contract

**ID:** X01. **Target:** Civilization II Multiplayer Gold
Edition **5.4.0f, patch 3**, the 32-bit Windows game.

This contract defines what the [algorithm specifications](README.md) mean by
classic behavior. An implementation should be possible using the specifications
and their declared inputs, without consulting the reconstructed source.

The inspected OpenCiv2 revision is
`ce18e5e52e19870cf1598651eee7c1a97d517519`. Supplementary evidence comes from
`civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. These are evidence revisions, not
alternative game versions. Earlier specifications retain their own explicitly
pinned contracts until reviewed; in particular, [W02](W02-map-generation.md) pins
OpenCiv2 `1ff87e33fbec8b7be8c4bf98c77709e16aad4f31`.
The policies below apply to every specification, including earlier evidence pins
and imperative wording in recovered procedures. Required behavior is classic
simulation and semantic interaction, bidirectional original-file interchange,
and original-peer TCP/IP multiplayer. Exact original presentation, harmless
native UI failures, rejected off-nominal executions and legacy transports have
the dispositions below. Historical algorithms remain evidence; these policies
control which observations a cleanroom implementation must reproduce.
AI heuristics and map-generation algorithms are unchanged by these relaxations.

## 1. The behavior being reproduced

For a declared operation, supply the same entry state and ordered external
inputs and controlled random choices, execute to the same boundary, and compare
the specified observations. With independently generated choices, compare the
selection laws and reproducibility policy rather than original trajectories.
Equivalence includes intermediate effects that can influence another operation,
even if the final visible screen happens to match.

| Observation | Required agreement |
| --- | --- |
| Simulation state | Exact field values, flags, IDs, ownership, list/stack order, counters, caches that affect later decisions, and mutations of shared scratch state where specified. |
| Decisions | Exact eligibility, scores, deterministic tie breaks, state-dependent scan consequences and branches; exact outcomes conditional on the same logical random choices. |
| Randomness | F01 sampling laws, thresholds, retained samples, candidate/retry rules and reproducibility within the new implementation. Original generators, seed-to-sequence mappings, stream sharing and raw/discarded-draw counts are not required. |
| Interaction | Equivalent information and available decisions; command eligibility/targets, acceptance/cancellation effects, active player/unit and visibility. Preserve interaction ordering that affects gameplay or the protocol; native widgets and navigation may differ. |
| Persistence | Bidirectional interoperability with original save/map/scenario files in the declared domain; specified bytes and load effects. Equality of a few displayed statistics is insufficient. F06 identifies padding and retained fields; any exclusion needs its stated justification. |
| Multiplayer | Mixed TCP/IP sessions with the original target game, with either implementation hosting; original operation authority, ordered messages, payload/state differences, turn barriers and synchronization effects. Legacy transports are optional. |
| Presentation | Same meaningful information, scenario-authored content, decisions and gameplay effects. Art, layout, wording of stock interface chrome, rendering, cosmetic media/timing and native control behavior may differ under the presentation policy. |
| Failure | Required gameplay/file/TCP-IP failure effects within the supported domain; documented safe rejection outside it. Harmless native UI failures use the simplified policy below. Successful-path cases do not establish failure handling. |

Matching return values alone does not establish equivalent ordering under the
rule below. A
message pump can dispatch a callback; a failed attempt can change a cache or
affect the next candidate. Each owning document must state these effects or
constrain its boundary so they cannot occur. Purely discarded random samples, presentation/gameplay stream coupling and
presentation-only callback traces are exceptions under the policies below.
F01 gives the shared
[determinism contract](F01-determinism-and-arithmetic.md).

No universal numeric tolerance applies to required observations. Simulation
integers and file/protocol fields are exact. Floating operations that affect
those observations follow the specified precision and rounding. Original pixel,
font-metric and decoded sample equality are optional reference tests, not
acceptance conditions for a redesigned interface.

### Fresh-game isolation and supported bug repairs

The required product reproduces classic user gameplay, with the declared RNG
policy and documented bug repairs. Original function boundaries, private memory
layout and incidental evaluation order are not product requirements. This policy
overrides conflicting imperative prose, worked cases and historical evidence in
every specification. Required file/wire/data definitions remain inputs.

**Fresh-game isolation:** with identical chosen content (including an explicitly
selected map/scenario), setup inputs, saved user preferences and controlled logical
random choices, a new game has equivalent gameplay state whether started in a fresh
process or after any earlier game, cancelled setup or failed load. Compare logical
identities; session generations and lifetime tokens may differ for stale-reference
protection. Prior-game simulation state is not an input.

Start each new-game attempt at a named session operation before content loading,
map preparation and the setup wizard. Replace world records and workspaces;
initialize empty AI goals, pools, counters, knowledge, histories, event/turn state,
map fallback, mutable rule/name cursors, text substitutions and load scratch.
Load chosen content into fresh mutable rule/text state. Preserve only explicit
application configuration, user preferences, immutable content caches and identity
generators. Revoke old clients/continuations; finish or cancel admitted work and
drain required network effects before crossing the boundary. No menu-rendering,
AI refresh/redirection, or discarded RNG call is needed to clear an old game.

Accepted setup edits/preferences can persist during an attempt as specified;
the next attempt starts with fresh simulation state. New-world preparation retains
only the map, rules and choices produced by that attempt. Loading/resuming a saved
game, importing selected scenario state, replacing a controller, and creating or
reviving a civilization *inside* a game are different operations. Preserve their
defined retained state; do not turn every allocation or load into a new game.

**Bug repairs:** demonstrated uninitialized stack/local reads, out-of-bounds
reads/writes, dangling accesses and nonterminating invalid operations are not
compatibility obligations. Use a narrow, documented deterministic initializer,
range check, no-result/skip, safe rejection or bounded termination as appropriate.
Preserve valid preceding effects and unrelated gameplay. Defined scalar wrap,
signed interpretation, deterministic ties and intentional within-game retained
state are not automatically bugs. A missing specification is not proof of a bug.

Each repair records its trigger, replacement behavior, downstream differences and
validation limits in its owner. A repair can change outcomes downstream of the bug;
record that difference rather than requiring equality to arbitrary original bytes.
Never demand original stack contents as production input. Original supplied-byte
and missing-input-stop cases may remain diagnostic evidence, not baseline gates.
If a safe continuation is not specified, record a finite owning-specification gap
and bounded rejection; do not silently invent an algorithm or claim that nominal
gameplay is complete. Implementations use this contract, not external source code.

The following repairs are already specified by this profile:

| Owner / trigger | Required safe behavior |
| --- | --- |
| F02/F05, a new-game attempt | Fresh state and fresh mutable content; no previous goals, damage, future technologies, peace counters, name cursors or resume markers. |
| C07, commodity road accumulator without an initializer | Start the accumulator at zero, then apply the specified tile increments and scoring. This deliberately changes stack-dependent demand outcomes. |
| C07, full routes with no selected replacement | Keep existing routes; do not replace through an unset slot. Preserve valid preceding delivery effects. |
| A02, rivalry lookup for non-wonder production | Rivalry contribution is zero outside the 28 wonder entries; do not read adjacent locals. |
| C05/P06, undefined famine redraw coordinates | Redraw the affected city/view and perform the following window update; no original coordinate input is required. |
| F05/P04, cancelled portrait selection with no result | Keep the current portrait. |
| C08, invalid remote notification recipient | Suppress the undeliverable notice and continue valid work; do not access another record as backing. |

All other confirmed bug sites use the same policy and must state their chosen
repair before claiming implementation. Historical repair IDs are provenance, not
instructions to import a reconstruction's complete behavior.

### Behaviorally significant ordering

Written sequences, scan directions and ordered pseudocode define **reference
evaluation**. The required contract is its behaviorally significant dependencies
and observations. An implementation may reorder, combine, cache, short-circuit
or parallelize internal work when it establishes equivalent required behavior
over the complete admitted execution domain. This rule applies throughout the
collection, including earlier source pins and wording such as “in order” or
“do not reorder”; it does not remove any dependency that those instructions
protect. Without such an argument, follow the reference sequence.

A dependency requires one action before another when changing their order can
change a required value, selected identity, decision, external effect, failure
or state observed by a later consumer. In particular, preserve:

- Read-after-write, overlapping/aliased writes, captured versus live inputs,
  retained scratch values and publications across real callbacks or reentry.
- Candidate eligibility, meaningful attempts/retries, first/last-match ties,
  scan bounds and retained cursors; allocation/lifetime, stack and event order
  wherever they determine later behavior. A tie rule can be represented explicitly
  without physically scanning in that order only if other scan effects agree.
- F01's significant conversions, rounding and exceptional results. Reassociation
  or speculation must not raise a fault on a path that reference evaluation never
  reaches, suppress a required fault, or change its preceding observable effects.
- Turn phases, scenario actions, partial acceptance/cancellation, information
  disclosure, meaningful notice/choice order and game-clock/deadline behavior.
- File bytes and supported partial-I/O effects; multiplayer authority, message
  and queue order, replies, state synchronization and barriers. Independent local
  calculations may be rescheduled without moving these external boundaries.

Purity must be established, not inferred from a name such as query, preview or
recalculate. A pure internal query on stable, fully backed inputs may be cached,
combined or skipped after its result becomes irrelevant. Keep an ordered fallback
when admitted missing reads, failures or actual callbacks make its execution
observable. Do not turn a supplied pure-query test adapter into an arbitrary
mutating provider solely to require the original call sequence.

Independent field/tile updates may be grouped before a publication boundary if
no admitted observer can distinguish their intermediate states. A callback,
remote command or later phase that reads those states defeats that argument.
Preserve the logical publication even when no player sees it immediately.
Reordered computation does not license replacing live input with an entry snapshot
across a boundary where it can change, or making a partial operation transactional.

Random choices retain F01's laws, arguments, reuse, decision/candidate/attempt
identity and dependence on state. Independent samples may be generated in another
order or prefetched; extra raw draws are not semantic events. Do not exchange
samples between decisions, change an adaptive retry distribution, or replace
correlated uses of one choice by fresh choices. Version the implementation's
scheduling/key policy so its own runs remain reproducible; nondeterministic
worker scheduling must not silently select which decision receives a sample.

An equivalence argument covers supported custom rules, imported saves/scenarios,
remote input, reachable later states and the claimed failure/callback domain.
A stock-input example alone is insufficient. An explicitly supported fast path
may use proved bounds and fall back to reference evaluation elsewhere; do not
reject nominal inputs merely to avoid ordering consequences.

[X02](X02-whole-game-conformance.md#dependency-comparison) compares semantic events
and their required dependencies. Tests must not create artificial checkpoints,
helper calls or native-store observations solely to mandate incidental order.
Historical source/native traces can retain a total sequence for their declared
reference purpose; they do not expand baseline requirements. This convention
preserves AI heuristics, map-generation algorithms and gameplay outcomes; it
relaxes implementation scheduling only where the behavior is equivalent.

### Randomness policy

This is a deliberate relaxation of exact original execution, adopted for the
cleanroom implementation. Preserve classic probability formulas and decision
rules, including F01's modulo and range-mapping biases, deterministic ties,
meaningful retries, reused sampled values and significant state/callback dependencies. Keep
saved-map coordinate patterns and non-RNG arithmetic unchanged.

The implementation may choose and version its own generators, seed mapping,
stream partition and presentation randomness. It must reproduce its own runs
from complete declared random contexts and external inputs. It need not match
the original generators, discarded draws, global draw sequence, PRNG-specific
seed exchanges or presentation-to-simulation random-state sharing. Old “primary”
and “secondary” names identify sampling laws under F01, not mandatory globals.

Exact required state and file comparisons involving random outcomes are conditional
on controlled logical choices. A matching numeric seed in two implementations
no longer implies the same map, combat result, AI decision or city layout.
Original sequence traces remain optional historical evidence, clearly separated
from required rule tests and implementation replay tests in X02. The file and
original-peer interoperability requirements below remain mandatory; they do not
require identical PRNG state or matching future random outcomes across engines.

This relaxation does not license changing gameplay sampling distributions,
eliminating stateful attempts, randomizing deterministic decisions, bypassing
required gameplay/network callbacks or ignoring their nondeterministic inputs.
Cosmetic choices follow the separate presentation policy.

### Standalone content and classic text adapters

Original text files are optional runtime inputs. A standalone implementation
uses the project's [versioned JSON content formats](../content/data-format.md)
and independently supplied complete defaults. The native and classic paths feed
the same logical rules, naming, semantic interaction and event execution; content
format does not select different mechanics, AI heuristics or map generation.

Classic RULES/CITY/CITYPREF, GAME/LABELS/menu/advice/encyclopedia/council and
scenario event/text inputs remain supported through their owning adapters.
Their nominal custom domains, lookup/fallback, significant conversions, retained
state, publication and failure effects remain required when that path is selected.
Native validation may use its own explicit authoring rules; it cannot silently
narrow classic support or replace scenario-authored content with stock JSON.

Standalone does not mean data-free: full rule values, ordered names and semantic
content must be supplied. Defining formats or providing small examples is not a
playable-content or conformance claim. Native IDs/hashes remain outside classic
save/scenario/wire bytes; an original peer uses equivalent classic content and
requires no new content-format negotiation. Explicit classic export reports
unrepresentable data rather than silently changing names, identities or rules.

### Presentation and semantic interaction policy

A replacement may use new stock art, layouts, fonts, rendering and playback
libraries, scaling, camera/zoom controls, menus, widgets, shortcuts, navigation
and cosmetic timing. It need not reproduce original pixels, audio samples,
palette cycling, line wrapping, window geometry, native control IDs, widget
allocation/cleanup order, redraw counts or cosmetic animation/media choices.
Purely visual sampling rules are optional reference behavior as well; gameplay
probabilities, AI choices and map generation are unchanged.

The required contract is what the player can learn and do, and what each action
changes. Preserve:

- Information values and units, meaningful notices, eligible choices, selected
  object/actor and the command represented by each choice. Presentation sorting
  may differ only when choice identity and automatic gameplay selections do not.
- Fog of war, remembered knowledge, private diplomacy and hotseat concealment.
  A new layout must not expose information unavailable to the current observer.
- Gameplay/file effects of acceptance, cancellation, editing, completion and
  skipping. Preserve already published edits; cancellation is not automatically
  rollback. Keep gameplay-dependent choice/notification order and defaults used
  by timeouts or automatic decisions.
- Original scenario text, names, substitutions, supported graphics/media and
  resource override rules. New stock artwork does not replace scenario-authored
  content silently. Decode source palettes/transparency/sheet identities so the
  intended custom content survives new rendering; exact screen rasterization is
  optional. Classic editor exports retain their external formats.
- Original command authority, response/result mapping, publication, processing
  of incoming network events, phase barriers and turn deadlines. A new dialog
  sends the existing protocol's values; no new UI or RNG negotiation is required.

Separate the semantic command and its effects from its visual realization.
Native mouse coordinates may become semantic tile/object selections; widget
returns may become named choices with a protocol adapter where needed. This
freedom changes screen projection, not W01 topology, map coordinates, distance
rules or stored map patterns. Reordered screens or nonmodal alternatives must
still prevent actions during the same gameplay/network busy states and handle
changed ownership, deleted targets, remote results and timeout closure.

A painting, text or media call in a recovered algorithm may recalculate cities,
mutate shared simulation state or admit a network operation. Such effects remain
required at the owning semantic boundary even if the original renderer is gone.
When geometry selects simulation work, keep that original logical admission
calculation and its declared classic view inputs independently of new rendering.
For example, V08/P07 score-report layout limits which cities are recalculated;
a new viewport must not silently change that prefix or refresh every city. The
required fixture declares those inputs; do not invent a canonical replacement
viewport or infer them from new font metrics.

The implementation may use a different event loop; it need not recreate each
native message pump, allocation callback or paint interleaving. It must preserve
required operation order and state observed across relevant external events.
Shorter/skippable animations must not skip damage, duplicate commands, freeze
network service or change turn-timeout policy. Different real user/network timing
is an external input, not an obligation to match one native execution trace.

UI-only retained buffers, caches and stale values can be eliminated or initialized
safely. Shared values read by simulation, AI, map generation, persistence or the
protocol remain required under their owners. Saved UI fields retain their F06
encoding: retain original values or use an explicitly documented valid translation
for changed local preferences. Do not drop fields, repurpose reserved bytes or
store new layout metadata in classic payloads.

Put detailed original geometry, raster, native-control and cosmetic media
procedures in clearly labeled **Original presentation reference** appendices.
P01–P10 and affected callers keep required semantic/gameplay/file/network behavior
in the main specification, including any calculation performed by a display
routine. Source-image and export pixels remain required where they encode file
content. Reference procedures retain original context but do not create a second
normative algorithm or require new UI geometry to match the old layout.
X02 tests semantic interaction and labels original visual/native traces separately.
Historical validation counts are evidence, not a demand to reproduce the original
UI architecture.

### Harmless native UI failures

An implementation may replace original native UI error numbers/messages,
allocation/lock failures, missing font/device errors and cosmetic partial updates
with clear diagnostics, fallback presentation and safe cleanup. It may abandon
an uncommitted local interaction or retry without reproducing stale UI buffers,
allocation order or partial drawing. Document the chosen behavior and distinguish
failure/cancellation from an accepted command; do not fabricate success.

This applies only to presentation-local effects. Preserve committed game edits,
required file-write outcomes, peer-visible results, locks, deadlines and session
cleanup. If a failed dialog requires a protocol reply, use the existing allowed
failure/cancellation path; do not silently lose the request or invent a new code.
Do not treat scenario-content loss as a harmless font/layout failure. Read/write
errors on supported saves, gameplay allocation failures and network disconnects
retain their owning contracts unless an explicit separate policy applies.

### Required and optional transports

The required network path is original-compatible TCP/IP, including N05's TCP
stream framing and UDP discovery, with unmodified original peers and either
implementation hosting. Preserve N01–N03 admission, setup, sequential and
simultaneous turns, save/resume, join/reconnect, synchronization and host transfer.
Hotseat remains required separately. Replacing the original wire protocol with a
new protocol, or requiring a peer-installed adapter, does not satisfy this path.

IPX/SPX, dial-up/modem, serial and legacy DirectPlay/launch-lobby integrations are
optional extensions. Their recovered algorithms remain reference evidence, not
unfinished mandatory implementation work. Hide or clearly mark unavailable
options; do not advertise an unsupported provider or silently reinterpret a saved
session's mode/provider. F06's supported file formats and mode adaptation remain
required even when a legacy connection cannot be resumed. A claim for an optional
provider needs its own tests; omission does not establish that the original lacked
it. State TCP/IP as the supported mixed-peer transport in compatibility claims.

### File and original-game multiplayer interoperability

The adopted policies retain these requirements for the target MGE build,
nominal supported content, declared additional inputs and required TCP/IP path:

- **Saves and maps:** import original files, export classic files that the original
  can load, and continue exchanging them in either direction. Preserve serialized
  game state and F06's specified load-time transformations. Existing map-pattern
  values and W01 coordinate predicates remain exact. Matching the next random
  outcome after crossing between implementations is not required.
- **Scenarios:** load and run original scenario files, rules, events and supported
  assets; scenarios exported through the classic editors must work in the
  original game. Preserve event conditions, ordering, persistent state and
  probability formulas. Random events need not occur on the same turns in
  independent runs with different generators.
- **Multiplayer:** support TCP/IP sessions containing unmodified original-game peers,
  both with a cleanroom host and with an original host. Preserve N01–N05's
  operation authority, command/result handling, publication points, barriers,
  wire formats and host-transfer behavior. Peers apply the received effects
  according to the original protocol; they must not reroll an already resolved
  outcome merely because their local generator differs.

The original multiplayer design exchanges state and operation results, with
simulation work assigned to particular peers. It does not require every peer
to maintain one identical random stream. RNG selection therefore does not grant
freedom to replace that authority model with a new lockstep protocol, require
original peers to share new RNG contexts, or add a mandatory protocol extension.
New replay/checkpoint metadata stays outside classic file and wire payloads;
original clients and original files must work without it. An optional extended
mode must not replace or be required by this compatibility path.

These are required outcomes, not claims of completed validation. X02 requires
bidirectional file tests and actual mixed-peer sessions, including simultaneous
turns, resume/join and host migration. Protocol parsing or cleanroom-only peer
tests cannot alone establish original-game interoperability. The input-admission
and optional-transport policies above/below bound those claims. Required TCP/IP evidence gaps remain open; omitted optional providers
do not block the baseline compatibility claim.

## 2. Inputs and supported domains

A case must declare the game mode, difficulty, active and human civilizations,
government, topology, rule tables/options, scenario settings, and relevant
session history. Only list an input as irrelevant when the scoped operation
cannot read it or be affected by it through a callback.

Original rule and scenario customization is part of classic behavior. Optional
OpenCiv2 additions are outside this contract. Each specification must give the
accepted custom-input domain; defaults do not establish the behavior of all
custom tables. Distinguish caller validation from a helper's actual behavior.

For file inputs, distinguish three things:

1. **Nominal inputs:** the intended file structure, field meanings and documented
   value domains, including supported customization and ordinary normalization.
2. **Off-nominal behavior:** what the original does with missing, malformed or
   out-of-domain values. Separate deterministic handling, such as prefix parsing,
   clamping, overflow/wrap and defined errors, from behavior requiring additional
   backing state or unrecovered evidence.
3. **Implementation policy:** the domain an alternative implementation chooses
   to accept and how it rejects inputs outside that domain.

### Input admission and rejection policy

The required implementation profile supports nominal original saves, maps,
scenarios, rules, resources and TCP/IP messages within each owner's declared
formats/versions/domains, including ordinary customization and documented
normalizations/defaults. Nominal does not mean stock values only. It may reject
malformed, unsupported or off-nominal inputs that the original happened to accept;
reproducing those executions is optional, not part of the baseline requirement.
Declare admission rules and rejection behavior for each input family. Do not
claim unrestricted original-input compatibility for a narrower domain.

Validate before executing an unsupported operation. Clear errors, bounded
termination and safe cleanup are required. Staged/transactional rejection may
leave the current game unchanged instead of reproducing the original parser's
partial mutations. If validation follows valid committed operations, state which
effects remain and do not undo externally published work silently. Do not clamp,
repair or report success for rejected content without identifying an additional
conversion policy. Routine defaults/fallbacks specified for nominal content are
still required, not a license to reject every omitted optional section.

If an implementation explicitly supports an off-nominal domain, preserve its
recovered interpretation, defined errors, scalar wrap and relevant partial
mutations, subject to the presentation and excluded-write policies. Original
malformed-input traces remain reference evidence for that optional domain.
Missing legitimate inputs still need an owning contract. Confirmed original
uninitialized/invalid-memory reads use the bug-repair policy above; historical
stack inputs and diagnostic stops are not required nominal-gameplay behavior.

Input validation does not change arithmetic for accepted executions, including
wrap reached during normal play or with nominal custom rules. High-bit encoded
fields, signed sentinels, retained/padding fields, documented legacy versions and
normal TCP fragmentation/coalescing are not malformed merely because they are
inconvenient. Preserve accepted file conversion, event semantics, simulation
limits and network parsing; distinguish incomplete delivery from an invalid
complete message. No broader range, AI, map-generation or bug-fix change is
implied. Out-of-bounds writes remain excluded under the following policy.

### Out-of-bounds writes are excluded bugs

An original write outside the intended destination object or array is a bug,
not required compatibility behavior. Do not reproduce its corruption of other
state, even when the affected bytes are mapped, the result is deterministic,
or ordinary nominal play reaches the write. Recreating the same corruption
through logical field assignments does not make it part of the target.

Keep recovered address calculations and observed damage in a bug/evidence
appendix, separate from required conformance cases. Such cases may diagnose the
original bug; they must not require a cleanroom implementation to corrupt state.
The owning algorithm should state its safe handling: omit the invalid write
when the remaining valid continuation is established, or document rejection or
a bounded stop when continuation is not specified. Do not invent an intended
replacement target, such as a different civilization's record, without labeling
that as an additional behavior change. Preventing the overwrite itself is an
accepted exception to original equivalence, not a compatibility shortfall.

This exclusion does not remove defined in-bounds overflow, modular arithmetic,
signed narrowing, partial updates, deliberate shared/sentinel storage, or the
safe-repair contracts for confirmed stale/out-of-bounds reads. A wrapped value can be
required even when using it later as an array index would trigger an excluded
write. Supplying extra backing for research does not expand a logical array's
valid index domain.

| Input or mode | Disposition |
| --- | --- |
| Single player; new, custom and premade worlds | In scope, including setup and a second game in the same process. New-game simulation is isolated from all prior games; only the explicit persistence allowlist survives. |
| Saved games, scenarios and classic editors | Bidirectional interoperability with the original is required for F03/F06/S01–S04 formats and domains, including load-time mode adaptation. |
| Hotseat | In scope. It is a local multiplayer mode, with its own concealment, active-player and handoff behavior; do not treat it as a network game. |
| Network and Internet | Mixed original-peer TCP/IP sessions are required, with either implementation hosting, including UDP discovery. N01–N05 retain application and transport semantics for this path. |
| Dial-up, serial, IPX/SPX and legacy DirectPlay/lobby integration | Optional extensions, recorded by N05. Unimplemented providers are explicitly unavailable; their absence does not narrow required TCP/IP, hotseat or nominal file support. |
| Play by e-mail (PBEM) | Not a reachable playable mode in this MGE release. Do not add it to the classic feature set merely because mode-2 branches remain in the executable. |
| Other Civilization II releases and unofficial patches | Separate targets; do not merge their rules or fixes into MGE 5.4.0f patch 3 without an explicit variant contract. |
| Malformed/unsupported inputs and failures | Safe rejection outside the admitted domain is permitted. Harmless native UI failures use the simplified policy; gameplay, supported-file I/O and TCP/IP failures retain their contracts. |

The PBEM exclusion is specific: the original Multiplayer Game menu offers Hot
Seat, Network, Internet and Dial-Up. The recovered PBEM setup entry at
`0x005ae580` has only an unused incremental-link thunk as a reference. Loading
an `.eml` through normal Load a Game clears multiplayer mode and proceeds as
single player. That reachable load behavior belongs to F06; the unreachable
email prompts do not become a required session mode.

State at an operation boundary is often a better input than a complete process
history. For example, W02 accepts resolved world parameters, controlled choices
or the implementation's random context, and a clock-derived resource seed input.
A test that starts at the main menu also includes the earlier setup decisions,
clock queries, UI actions and callbacks. These are different test boundaries.

## 3. Original behavior and reconstruction choices

Use the following evidence categories explicitly:

1. **Original operation:** recovered instructions/data establish the operation,
   widths, branch conditions or constants. Record the address and executable
   identity; decompiler types and variable names alone are not definitive.
2. **Original observation:** a recorded execution establishes an outcome for
   its captured inputs, environment and history. Record that bounded domain.
3. **Reconstruction agreement:** source and/or tests from OpenCiv2 and the
   independent reconstruction agree. This supports a contract but does not
   replace missing original evidence.
4. **Assumption or deliberate deviation:** a chosen value, missing platform
   behavior, safety repair or unresolved interpretation. State it at the affected
   procedure and describe the observable consequence or unknown.

When evidence conflicts, first compare builds, modes, entry state and external
inputs. Prefer original instructions for a recovered primitive and controlled
original observations for its exercised behavior. If they still disagree,
record an unresolved contract instead of silently choosing a convenient result.
An observed stale stack value is evidence of that execution, not a universal
constant. Lack of a replay mismatch does not establish an unexercised branch.

### Classic switches in OpenCiv2

At the pinned revision, a **nonempty** `CIV2_CLASSIC` disables selectable bug
fixes and extras; the string `0` is also nonempty. The usual invocation is
`CIV2_CLASSIC=1`. Unit tests disable both. Replays disable both unless their
respective test override is enabled, and an explicit classic setting still
takes precedence over those overrides.

The precise conditions, using `present(x)` to mean a nonempty environment value,
are:

```text
classic = present(CIV2_CLASSIC)
replay = not unitTest and present(CIV2_SCRIPT)
fixes = not classic and not unitTest
        and (not replay or present(CIV2_TEST_FIXES))
extras = not classic and not unitTest
         and (not replay or present(CIV2_TEST_EXTRAS))
UI_fix_enabled = fixes
state_fix_enabled = fixes and game.multiplayer < 3
```

Consequently, disabling network state fixes does not by itself select the whole
classic presentation. Nor does selecting classic undo repairs present in every
mode. Capture the effective switches in each fixture rather than relying on a
label such as “default run.”

### Known differences that later documents must account for

The following table routes known reconstruction differences to their owning
behavioral contracts. Section 6 records historical provenance separately; external
source code is not an implementation oracle. This table is not exhaustive; each
owner's contract states the applicable behavior and unresolved inputs.

| Evidence | Difference or unresolved input | Owning specifications |
| --- | --- | --- |
| B1 | Commodity road count starts at zero in every OpenCiv2 mode. The original starts from a stale local; zero was common in Wine recordings, but other values affected demand. | [C07 original-input boundary](C07-trade-and-commodities.md#original-inputs-not-initialized-locally); F02 for captured backing state. |
| B3, B13, B19 and other preset locals | Classic uses recorded/preset values for otherwise uninitialized reads. Do not generalize those values to all original histories or hosts. | A08 for B3's attitude change; A05 for B13/B19's transport choices; F02 for entry-state requirements. |
| B21 | Wonder scoring bounds an index that originally could read adjacent locals. This can change AI production scores. | A02. |
| B22 | Ordinary hotseat movement retains a reconstructed layout of the original adjacent locals, but paradrop visibility with no active human uses an always-on repair. | U02/U05, N04, P02. |
| B26 | Cancelling a custom portrait preserves the existing portrait, replacing an original stale result. | F05, P04. |
| B27 | Disband with no active unit returns, replacing a possible stale-coordinate city-disband prompt in cheat mode. | U04, S03, P05. |
| B29–B31 | Council speech clearing, famine redraw location and production-waste drawing use initialized values instead of stale locals. | [P09 retained chapter input](P09-council-and-throne-room.md#bugs-and-reconstruction-policies); [C05 famine input boundary](C05-city-growth-and-support.md#famine-consequences-and-redraw-input)/P06; P06, respectively. |
| B8 and memory-error ledger | Event string lifetimes, buffer bounds, file/packet validation and similar repairs are always on. Equivalent successful output does not establish equivalent corrupt-input or failure behavior. | S02/S03, F06, N02/N05, and the relevant presentation/file owner. |
| C07 route replacement / BF-026 | When all route scores are at least9999 and the new threshold passes, the original uses an unset replacement slot. Keep the existing routes when no replacement was selected; supplied-slot diagnostics remain historical evidence. | [C07 route insertion](C07-trade-and-commodities.md#insertion-and-replacement). |
| C09 capture notices | Original jumps skip remote ally/general notice sends after the local popup; the recorded independent reconstruction instead uses separate network tests. The original also ignores a received Palace reply when selecting its local escape decision. | [C09 original evidence](C09-city-capture-and-revolt.md#appendix-a--source-and-representation-evidence). |
| C08 Leonardo notification / BF-194 | The original remote notice reads a recipient through the mapped player slot without a range guard. Suppress an undeliverable notice and continue valid work under the supported repair policy. Invalid notification/record writes remain excluded bugs. | [C08](C08-buildings-and-wonders.md#missing-input-and-excluded-write-boundaries). |
| C06 invalid production writes | Failed AI diplomat creation can attempt a Veteran write through a negative unit result; a malformed window selection can overrun per-type build counters. These are excluded writes, distinct from spent stock and scalar wrap. | [C06](C06-city-production-and-purchasing.md#appendix-a--representation-messages-and-original-bugs). |
| C04 mood-counter widths | The primary decompilation uses byte accesses where original instructions use whole signed32 mood counters; only history/city snapshots narrow to bytes. C04 resolves this against the original executable and corrects C03/F02. | [C04](C04-city-happiness-and-disorder.md#appendix-b-sources-corrected-evidence-and-checks), C03, F02. |
| F01 range arithmetic | Original endpoint subtraction wraps at 32 bits. The hand-written C++ expression does not make signed-overflow semantics portable for extreme endpoints. | F01; any consumer accepting those endpoints. |

For a stale or out-of-bounds original read, a specification may describe exact
behavior conditional on explicitly supplied backing bytes. Alternatively, it may
limit its equivalence claim to recorded cases, or specify a deliberate safe
replacement. It must say which. Do not require arbitrary memory corruption as a
portable implementation technique or claim a replacement is exact original
behavior. A snapshot of valid game records need not contain those extra inputs.

Defined gameplay quirks within a game remain part of the target unless a named
repair applies. Historical second-game city-name retention (B2) is excluded:
every new-game attempt resets simulation state under the fresh-game contract.

## 4. Platform boundary

An implementation may use different storage layouts, classes, rendering APIs,
allocators, networking libraries and codecs if the required observations agree.
Original presentation-only observations are optional reference behavior.
Original addresses identify evidence; they are not required runtime addresses.
Little-endian file fields, arithmetic widths, ordering, timing inputs and in-scope
aliases that affect required behavior must still be represented explicitly. Aliases
created by excluded out-of-bounds writes are bug evidence only.

Numeric requirements follow [F01's behavioral-equivalence convention](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges).
State logical values, ranges and sentinels in the main algorithm. Retain an
explicit width-dependent conversion where its effect matters; put storage-only
widths in reference appendices. Any representation with sufficient range is
permitted, including wider or arbitrary-precision integers. Redundant operations
may be omitted when required behavior is equivalent over the complete admitted
domain, including reachable custom-content and imported/network state.

This does not remove observable wrap, signed views, rounding, faults or partial
effects. File/wire widths remain exact at their boundaries. Off-nominal-only
examples belong with their declared optional profile; nominal executions that
reach a conversion remain supported. No additional gameplay relaxation follows
from replacing a type annotation by a logical range.

Specifications should express logical behavior in the main procedure and map it
to the original storage in a validation appendix. Compare logical state directly,
or encode it through that mapping when using original snapshots. A byte-for-byte
reference comparison does not require identical internal records or enums.

Model platform calls by their inputs, results and side effects. At minimum,
specify relevant clock returns, message/timer callbacks, file contents and
errors, input events, transport deliveries, and media completion. Record ordering
where callbacks can affect simulation, persistence or protocol behavior; original
native widget/paint interleavings are optional reference observations. Do not
infer real-time scheduling from an ordinary sequential procedure.

The deterministic Wine/OpenCiv2 replay harness is a **test environment**. Its
virtual clock and injected seed are not intrinsic rules of Civilization II.
F01 describes their effect on random and arithmetic inputs. P05 and N01–N05
own the event and transport scheduling contracts. Runtime
packaging, compiler scaffolding and third-party codec internals are outside the
algorithm collection; their externally visible effects are not automatically
outside it.

## 5. Evidence required for a compatibility claim

Every case accompanying a specification must identify:

1. **Target and evidence:** game version, original binary hash when used, source
   revisions, fixture revision, and how expected results were obtained.
2. **Boundary and domain:** called operation, start/stop points, applicable modes
   and input limits, plus any excluded callbacks or failures.
3. **Entry state:** all read state and relevant backing bytes; controlled random
   choices or the implementation's versioned random contexts,
   resource-pattern seed where applicable, process/session versus new-game
   initialization, rules and asset identities.
4. **External inputs:** ordered commands, clock returns or named clock model,
   file/packet inputs, callback schedule, environmental options and failures.
5. **Expected observations:** return value, mutations, ordered effects, random
   decisions and appropriate serialized or presentation output. Internal RNG
   states are compared only within the same declared implementation policy.
6. **Comparison and limits:** normalization/masks with reasons, what was actually
   compared, disagreements, assumptions and unexercised branches.

These information categories are encoded by
[X02's fixture format](X02-whole-game-conformance.md#2-version-3-fixture-format).
X02 also defines comparison, coverage and integration requirements, with sampling-law
vectors and explicit limits on the evidence currently available.

For example, a W02 boundary case can provide resolved dimensions/parameters,
named random choices, the one resource-seed clock value, terrain rules, required
map/cache state, and a no-mutating-callback condition. It can then compare tile
bytes and derived metadata conditional on those choices. It does not establish setup,
starting positions, an AI turn or network generation equivalence. A main-menu
replay has a wider boundary and correspondingly more inputs.

State each specification's scope, unresolved requirements and evidence limits
explicitly. Record independent conformance cases and their results separately
from source review and documentation checks. Document existence or an isolated
passing case does not establish complete game equivalence. Missing behavior
must remain visible; any narrower supported domain must be declared under X01's
input policy.

## 6. Traceability and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

| Evidence at the pinned OpenCiv2 revision | Use here |
| --- | --- |
| [Classic and test configuration](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/platform/main.c), [fix predicates](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/include/auto_common.h) | Exact mode-switch conditions. |
| [Original bug ledger](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/docs/BUGS.md), [extras](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/docs/EXTRAS.md) | Selectable changes, always-on repairs, stale-state assumptions and optional behavior. |
| [Status and coverage](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/docs/STATUS.md) | PBEM reachability, provider gaps and finite replay coverage. |
| [Replay comparison](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/harness/compare.py), [determinism hook](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/hook/determinism.c), [native replay](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/platform/replay.c) | Scope of screenshot, primary-random and media-request observations. |
| [W02](W02-map-generation.md), [F01](F01-determinism-and-arithmetic.md) | Existing scoped algorithm and shared arithmetic contracts. |

This document was checked against those source conditions and evidence notes.
It introduces no claim that an original executable or whole-game conformance
suite was run for this documentation change. The known-difference table is a
starting audit list; each later owner must record its own exact behavior and
evidence before claiming compatibility.
