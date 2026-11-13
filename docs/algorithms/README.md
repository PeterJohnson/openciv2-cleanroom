# Algorithm specifications

This collection describes classic Civilization II Multiplayer Gold Edition in
sufficient detail for a cleanroom implementation. Its 65 specifications cover
simulation, AI, scenarios, persistence, multiplayer and player interaction.
Implementers should work from the behavioral contracts and declared inputs;
external source references are confined to historical validation/provenance
sections and are not implementation inputs. Do not open reconstruction source
or reuse its harnesses to write code or tests. Report missing behavior as a
specification gap and resolve it through a self-contained contract or independent
original-game observations.

Start with [X01's compatibility contract](X01-classic-compatibility-contract.md),
then F01–F03 for arithmetic, shared state and rules, and W01 for map geometry.
Use the [catalog](#specification-catalog) to find each algorithm and
[X02](X02-whole-game-conformance.md) for fixtures, coverage and validation gaps.
A specification's presence does not establish implementation or conformance.

IDs are stable references. Keep uppercase ID-prefixed filenames such as
`F01-determinism-and-arithmetic.md` and update this index when ownership or scope
changes. [Source provenance](#source-provenance) records the collection's research
history separately from implementation guidance. Source links and filenames
there do not authorize reading or deriving implementation from those sources.

## Classic compatibility target

The target is **Civilization II Multiplayer Gold Edition 5.4.0f, patch 3**, with
the original game rules and supported custom rules and scenarios.
[X01: Classic compatibility contract](X01-classic-compatibility-contract.md) defines
the target, domains, observations and evidence requirements. Preserve its
observable arithmetic, state-dependent order of operations, random decision laws,
selection tie breaks, state transitions, file formats and required semantic
interaction. Original presentation and optional input/provider behaviors follow
the explicit policies below.
Difficulty, government, human versus AI control, topology, and game mode are
inputs wherever they affect behavior.

Randomness follows [X01's policy](X01-classic-compatibility-contract.md#randomness-policy)
and [F01's sampling laws](F01-determinism-and-arithmetic.md#2-random-decision-and-state-boundaries).
Keep classic probabilities, biases, retry rules and reused outcomes; require
reproducibility within the new implementation. Matching original generators,
seed-to-sequence mappings, global draw counts, discarded samples and shared
presentation/gameplay streams is **not required**. Saved-map coordinate patterns
remain exact. Original RNG traces in evidence appendices describe historical
checks, not mandatory cleanroom trajectories. X02 separates controlled-choice
rule tests, implementation replay and optional legacy-reference validation.

[Original-game interoperability](X01-classic-compatibility-contract.md#file-and-original-game-multiplayer-interoperability)
remains required: bidirectional saves/maps/scenarios and mixed TCP/IP multiplayer
with either engine hosting, including UDP discovery. Different future random
outcomes are allowed. The classic file and wire formats, authority assignments
and synchronization points remain required. Original files and peers must work without new RNG metadata or mandatory
protocol extensions. X02 requires round trips through the original and actual
mixed-peer sessions; these tests remain open work.

The [X01 policies](X01-classic-compatibility-contract.md) distinguish
required compatibility from retained original implementation evidence:

| Area | Required implementation | Optional reference or extension |
| --- | --- | --- |
| Presentation | Information, authored scenario content, privacy, semantic choices, accepted/cancelled effects and gameplay/network timing obligations | Original art/layout/pixels/fonts, screen projection, controls/shortcuts, cosmetic media and native callback/paint traces |
| Harmless UI failures | Clear diagnostics/fallback, safe cleanup, no fabricated success or loss of committed effects/required replies | Native error numbers, allocation/lock sequences, stale UI buffers and partial drawing |
| Input admission | Nominal supported formats and custom content, documented defaults, normal network fragmentation, normal-play arithmetic/wrap; safe declared rejection outside the domain | Reproducing malformed/unsupported original inputs and their partial traces; exact behavior applies when an extra domain is explicitly supported |
| Multiplayer transports | Original TCP/IP streams and UDP discovery, both host roles, sequential/simultaneous play, join/resume/host transfer; hotseat separately | IPX/SPX, modem/dial-up, serial and legacy DirectPlay/launch-lobby integration |

AI heuristics, world/map generation, simulation rules and file/application wire
formats are unchanged. Purely cosmetic city-scene generation is presentation;
W02 terrain generation remains required. New UI geometry does not change W01
coordinates, topology, distance or resource predicates.

## Native content and classic adapters

[Versioned JSON content](../content/data-format.md) supplies standalone rules,
names, semantic text, encyclopedia and event definitions. Original text files
remain optional user inputs through compatibility adapters. Both paths preserve
the same gameplay model; F03/C01/P04/P08/P09/S02 own classic parsing and required
consumer behavior. Native formats do not change AI, map generation, classic
file/wire encodings or scenario override obligations. Complete distributable
content and runtime readers remain separate from format examples and conformance
claims; X02 includes paired native/classic content cases.

## Numeric specification convention

All 65 specifications follow [F01's behavioral-equivalence rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges).
Describe logical values, admitted ranges and sentinels before any reference to
original integer widths. A bounded identity/category or part count requires
sufficient range, not a native byte/word. Original storage-only annotations belong
in reference appendices; file/wire/asset widths remain normative at their external
boundaries. An encoding appendix is not automatically optional.

Keep explicit modulo, signed-view and rounding operations where they affect a
required result, branch, failure, publication or later consumer. Wider or
arbitrary-precision arithmetic and omission of redundant casts are allowed when
equivalence is established over the complete admitted domain. Preserve callbacks,
live-read and random-decision dependencies; algebra alone does not establish
independence under the ordering convention below.
A range declaration does not add rejection, saturation or a new gameplay cap.

The equivalence proof must cover admitted custom content, imported saves/scenarios,
remote updates and reachable subsequent states. If equivalence is not established,
retain the specified conversion effects. This convention does not relax AI
heuristics, world generation or classic arithmetic outcomes.

Out-of-bounds memory writes and the resulting corruption are **bugs excluded
from compatibility**, even when deterministic or reachable during ordinary play.
[X01's exclusion](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
applies throughout this collection. Keep their evidence in appendices and specify
safe handling separately; do not make the corruption a required conformance
result. Defined overflow/wrap and deliberate sentinel storage remain in scope.

## Ordering specification convention

All specifications use [X01's behaviorally significant ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering).
Ordered pseudocode is reference evaluation. Keep dependencies that affect values,
selected identities, live/captured inputs, callbacks, partial failure, file bytes
or network effects. Independent work may be reordered, combined, cached,
short-circuited or parallelized only with an equivalence argument covering the
complete admitted domain; otherwise retain reference order.

State a tie rule or publication boundary directly when that is the requirement.
Move incidental source query/traversal sequences into reference appendices when
the main contract can describe the result and its dependencies. Purity must be
established: city recalculation, AI surveys, in-place map passes and event actions
can mutate later inputs. Do not infer independence from different output objects
or matching return values alone. Real callback/failure prefixes remain observable;
artificial internal checkpoints do not create extra compatibility requirements.

[X02](X02-whole-game-conformance.md#dependency-comparison) compares stable semantic
event identities and declared dependencies. Every unordered event pair needs an
owner-backed independence declaration; missing edges are not implicit permission
to reorder. Record chronological traces for diagnosis and same-policy replay,
while keeping original native/helper traces within their reference scope.
Required protocol/file ordering, AI heuristics and world-generation rules remain
unchanged. Version 3 fixture metadata makes this comparison explicit.

## Specification catalog

### Foundations and game orchestration

| ID | Document | Scope |
| --- | --- | --- |
| F01 | [Determinism and arithmetic](F01-determinism-and-arithmetic.md) | Sampling laws and biases, implementation reproducibility, clock inputs, range helpers, integer and floating-point conventions. Original generators are optional appendix evidence. Consumers own decisions and state-dependent ordering. |
| F02 | [Game state and invariants](F02-game-state-and-invariants.md) | Game, civilization, city, unit, map, and scenario state; IDs, lifetimes, flags, sentinels, ownership, list invariants, cached versus authoritative values, and mutation contracts. File encoding belongs to F06. |
| F03 | [Rules and configuration](F03-rules-and-configuration.md) | RULES.TXT parsing, section order, defaults, conversions, validation, cross-references, technology prerequisites, unit and terrain definitions, cosmic parameters, and classic game-option effects. Includes custom-rule input limits and reconstruction differences. |
| F04 | [Turn sequencing](F04-turn-sequencing.md) | Single-player session loop; global, civilization, city, and unit phase order; year progression, turn counters, resume gates, expiry/reset timing, event hooks, autosave, and end checks. Defines child-operation boundaries; their formulas and integration review remain with the mechanics below. |
| F05 | [Game setup and starting positions](F05-game-setup-and-starting-positions.md) | New and premade worlds, tribe/leader selection, world parameter resolution, civilization allocation, starting advances and units, start-site search and relaxation, balancing, accelerated starts, and new-game resets. W02 owns terrain generation; child mechanics and the advanced-start missing-value boundary remain explicit. |
| F06 | [Save, load and file formats](F06-save-load-and-file-formats.md) | Save, scenario, and map layouts; versions, sizes, byte order, record validation, strings, event storage, state restoration, mode adaptation, derived-state rebuilds, saved settings/window state, and partial-load failure behavior. |

### World and environment

| ID | Document | Scope |
| --- | --- | --- |
| W01 | [Map geometry and spatial state](W01-map-geometry-and-spatial-state.md) | Doubled coordinates, parity aliasing, wrapping, distances, neighborhood tables, tile access, ownership and region queries, land/water labeling, border bits, resource/shield/hut predicates, and spatial cache invalidation. |
| W02 | [Map generation](W02-map-generation.md) | Land creation, terrain, rainfall, age operations, rivers, polar edits, region labeling, initial coarse connectivity, fertility, resources, and huts; random decision rules and phase checks. |
| W03 | [Visibility and exploration](W03-visibility-and-exploration.md) | Per-civilization known terrain, tile and unit visibility, revelation radii and exceptions, contact discovery hooks, map sharing, city investigation/reveal, and what movement or ownership changes refresh. |
| W04 | [Terrain work and improvements](W04-terrain-work-and-improvements.md) | Settler/engineer orders, eligibility, work accumulation and completion, irrigation sources, mining, transformation, roads, railways, fortresses, airbases, pollution cleanup, and pillaging. AI chooses jobs in A03. |
| W05 | [Pollution and global warming](W05-pollution-and-global-warming.md) | Pollution generation from city state, probability and tile selection, global accumulation and warming thresholds, terrain changes, recalculation/revelation, and cleanup interaction. Owns shared pollution placement and the AI scan tally; distinguishes meltdown's blast-only call from a nuclear action. |
| W06 | [Huts and barbarians](W06-huts-and-barbarians.md) | Hut outcome selection and consumption, gold/advances/mercenaries/nomads/cities, uprising and sea-raid generation, placement constraints, barbarian leaders and ransom, and barbarian-specific behavior. Ordinary combat is U06. |

### Units and combat

| ID | Document | Scope |
| --- | --- | --- |
| U01 | [Unit lifecycle and stacks](U01-unit-lifecycle-and-stacks.md) | Allocation, creation/deletion, ordered traversal and repair, relocation, cargo selection/grouping, home links, off-map units, unit visibility and bookkeeping; includes cascading removal and rehoming primitives, with explicit child and network contracts. |
| U02 | [Movement and zones of control](U02-movement-and-zones-of-control.md) | Domain and terrain legality, movement allowance and costs, roads/railways/rivers, zones of control, occupied tiles, treaty restrictions and expulsion, entry dispatch, failed moves, and post-move effects. |
| U03 | [Pathfinding and goto](U03-pathfinding-and-goto.md) | Local flood search, cost grids, caching, coarse connectivity and waypoints, directional tie breaks, fallback movement, route continuation/replanning, and differences between route estimates and actual move execution. |
| U04 | [Unit orders and activation](U04-unit-orders-and-activation.md) | Active-unit selection and cycling, wait/sleep/sentry/fortify/disband/home/goto/automation commands, order persistence, wake-up rules, turn resets, healing, and interrupted or exhausted orders. Delegates job and move execution. |
| U05 | [Transport and special movement](U05-transport-and-special-movement.md) | Boarding, unloading and landing, transport loss, air fuel and basing, trireme loss, airlift, paradrop, and exceptional movement checks and costs. Uses U01's shared cargo grouping; combat and diplomacy consequences are cross-references. |
| U06 | [Combat and veterancy](U06-combat-and-veterancy.md) | Attack eligibility, best-defender selection, strength modifiers, hit probability, hit points/firepower, damage loops, special unit interactions, retreat or survival where supported, stack casualties, promotion, and victory aftermath dispatch. |
| U07 | [Nuclear attacks](U07-nuclear-attacks.md) | Delivery and interception checks, blast footprint, unit and city damage, pollution, diplomatic effects, and the exact ordering of cascading changes. Spy delivery enters through V06; ordinary battles remain U06. |

### Cities and trade

| ID | Document | Scope |
| --- | --- | --- |
| C01 | [City lifecycle and ownership](C01-city-lifecycle-and-ownership.md) | Founding eligibility, city allocation and names, initial state, radius claims, lookup, deletion, ownership-transfer primitives, and trade/unit/map bookkeeping. Capture policy belongs to C09. |
| C02 | [City tiles and worker assignment](C02-city-tiles-and-worker-assignment.md) | Tile yields, specials and improvements, government modifiers, usable and blocked squares, shared radii, manual/default worker assignment, specialist allocation, and deterministic assignment tie breaks. |
| C03 | [City economy and corruption](C03-city-economy-and-corruption.md) | Food/shield/trade totals, capital links, corruption and waste, tax/science/luxury splits, rounding and modifier order, route contributions, and the complete city-stat recalculation pipeline. |
| C04 | [City happiness and disorder](C04-city-happiness-and-disorder.md) | Initial citizen moods, difficulty and empire-size effects, specialists, martial law, luxury and building/wonder effects, military unhappiness, tax collection, celebration growth and disorder transitions, and stage-by-stage clamps. |
| C05 | [City growth and support](C05-city-growth-and-support.md) | Food/shield box factors, granaries, growth limits, starvation, the shared growth-facility query used by C04, settler food costs, government-dependent unit support and military counts, food accumulation, shortage removal and disorder retirement. Specifies ordered side effects and the original famine-redraw input boundary. |
| C06 | [City production and purchasing](C06-city-production-and-purchasing.md) | Build eligibility, saved-stock switching, player purchases/sales/change commands, shield accumulation, unit/building completion, AI spending, repeat/default orders, wonder acquisition/races, placement failures and ordered partial effects. Uses C03 Capitalization and C05 box factors. |
| C07 | [Trade and commodities](C07-trade-and-commodities.md) | Commodity score/ranking formulas, refresh and suppression, cargo selection, route mutation, caravan entry/donation, delivery revenue/research/food, reciprocal cargo, reports and ordered effects. Distinguishes supplied original locals and excluded writes; references C03 recurring yields and U03 connections. |
| C08 | [Buildings and wonders](C08-buildings-and-wonders.md) | Improvement/wonder storage, acquisition effects, obsolescence, maintenance, uniqueness and effect scope. References C06 build eligibility and production acquisition; provides an effect-to-consumer catalog linking each bonus to its formula owner. |
| C09 | [City capture and revolt](C09-city-capture-and-revolt.md) | Combat capture and incited transfer, population/building losses, plunder, palace relocation, wonder control, rehoming, partisans, destruction, civilization-elimination/civil-war checks, and event/notification order. |

### Civilizations and game outcomes

| ID | Document | Scope |
| --- | --- | --- |
| V01 | [Civilization lifecycle and civil war](V01-civilization-lifecycle-and-civil-war.md) | Civilization creation/reset, elimination and revival where supported, leader assignment, survival checks, civil-war eligibility and partition, transfers, and destroyed-civilization records. F05 owns initial setup orchestration; use C09 central-city selection and C01 elementary transfer. |
| V02 | [Governments and tax rates](V02-governments-and-tax-rates.md) | Government availability, revolution/anarchy and transitions, rate caps and balancing, civilization treasury/upkeep settlement, population-milestone/funds checks, and government effects on other systems. City-level consumption is owned by C03–C05. |
| V03 | [Research and technology](V03-research-and-technology.md) | Advance availability, prerequisite and research-goal traversal, cost calculation, beaker accumulation and completion, technology grants/theft/exchange, first-discovery effects, advance obsolescence, C08 upgrade triggers, and research-selection rules. |
| V04 | [Diplomacy and treaties](V04-diplomacy-and-treaties.md) | Contact, attitude and reputation state, peace/ceasefire/alliance/war transitions, expiry, treaty violations, senate constraints, shared information, expulsion, and alliance obligations. |
| V05 | [Diplomatic negotiation](V05-diplomatic-negotiation.md) | Audience/session flow, offers and demands, gifts, tribute, technology exchange, map sharing, joint wars, acceptance/refusal effects, patience, and human/AI or human/human branches. A08 owns stance, valuation and alliance-willingness providers; V05 owns interactive prices and effects. |
| V06 | [Espionage and bribery](V06-espionage-and-bribery.md) | Diplomat/spy mission eligibility and menus, embassy/investigation, technology theft, sabotage, poisoning, nuclear planting, bribery and incitement prices, success/capture/escape, veteran effects, and incidents. |
| V07 | [Spaceship](V07-spaceship.md) | Part groups and dependencies, construction limits, assembly, mass/thrust/life-support/energy/success calculations, launch, travel/arrival, loss, and race state. Production belongs to C06; victory checks to V08. |
| V08 | [Scoring, victory and retirement](V08-scoring-victory-and-retirement.md) | Score and rating components, scenario objectives, conquest and space outcomes, retirement warnings and year rules, endgame ordering, continuation, and Hall of Fame qualification/storage rules. |

### Artificial intelligence

| ID | Document | Scope |
| --- | --- | --- |
| A01 | [AI strategy and assessment](A01-ai-strategy-and-assessment.md) | Civilization assessment, continent policies, power/threat estimates, role counts and quotas, goals and standing goals, priorities, leader/difficulty parameters, and per-turn orchestration. |
| A02 | [AI city production](A02-ai-city-production.md) | City surveys, defense needs, improvement/wonder/unit scoring, special-unit and spaceship choices, redundant sales, and final production tie breaks and fallbacks. Uses C06 eligibility and AI spending rules. |
| A03 | [AI settlement and terrain work](A03-ai-settlement-and-terrain-work.md) | City-site evaluation, expansion goals, settler/engineer job selection, road links, irrigation/mining/cleanup priorities, fortress placement, travel and population return, including human automation. Distinguishes role-21 threshold choices from the ordinary recommendation helper; W04 owns transformation execution. |
| A04 | [AI ground tactics](A04-ai-ground-tactics.md) | Land-unit target and move scoring, exploration, attack/defense, garrisons, pillage, partisan behavior, healing/retreat, trespass response, persistent goto, and exhausted/no-action outcomes. |
| A05 | [AI naval and transport choices](A05-ai-naval-and-transport.md) | Cargo reservation, port recruitment, landing/unloading choices, war/trade/land destinations and postcombat transport retasking. U01/U05 own binding and execution; A04 owns shared guards and direction scoring. Preserves retained scratch and established read aliases while excluding overwrites. |
| A06 | [AI air and nuclear target selection](A06-ai-air-and-nuclear.md) | Fuel-derived reach and recovery, missile scoring/staging/redistribution, AirSuperiority targets and bases, city raids, nuclear scoring/reach/staging and pre-attack treaty effects. U02/U04/U05/U06/U07 own execution. |
| A07 | [AI special-unit missions](A07-ai-special-unit-missions.md) | Caravan destinations, diplomat/spy city targets, and proactive paratrooper objectives/landing cancellation. Uses A04 routing and A03 retained context; C07 owns delivery, V06 mission selection/execution, and U05 actual paradrop. |
| A08 | [AI research, government and diplomacy](A08-ai-research-government-and-diplomacy.md) | Advance valuation, AI rates/government scoring and choice, stance/security, alliance willingness, technology exchange, border/relation reviews, military-aid admission and war-ally requests. V03 owns research selection/grants, V05 interactive prices/effects and V07 shared race queries. |

### Scenarios and editors

| ID | Document | Scope |
| --- | --- | --- |
| S01 | [Scenario loading and parameters](S01-scenario-loading-and-parameters.md) | Parameter meanings/reset, briefing, single-player/network-host/hotseat preparation and local human selection, options, retry/cancel effects and resume publication. F06 owns loading/variant/resource context, F04 calendar, V08 objectives and S02 event execution. |
| S02 | [Event language and execution](S02-event-language-and-execution.md) | Event-file grammar and parsing, storage and references, all supported triggers and actions, matching order, random-turn checks, one-shot/persistent behavior, action ordering, event hooks, and save/reload continuation. |
| S03 | [World and scenario editors](S03-world-and-scenario-editors.md) | Classic cheat/editor commands that mutate map, cities, units, civilizations, money, visibility, victory, and scenario state; validation, cancellation, derived-state repairs, and scenario/event writing. |
| S04 | [Rules and asset editors](S04-rules-and-asset-editors.md) | Terrain/unit/building/technology/tribe/cosmic rule edits, references and rename propagation, sprite and sound edits, preview/apply/cancel behavior, and text/asset round trips. Parsing semantics remain F03. |

### Multiplayer and local sessions

| ID | Document | Scope |
| --- | --- | --- |
| N01 | [Multiplayer session and lobby](N01-multiplayer-session-and-lobby.md) | Discovery/join/host setup, player slots and civilization assignment, passwords, scenario/world negotiation, startup waits, disconnects, lost-player handling, and host handoff. |
| N02 | [Network commands and state synchronization](N02-network-commands-and-state-synchronization.md) | Game message schema and dispatch, validation, authority, command routing, busy/lock rules, batching, state sections, checksums/diffs, resynchronization, and join/load state transfer. |
| N03 | [Multiplayer turn sequencing](N03-multiplayer-turn-sequencing.md) | Host/client phase state machines, AI versus local/remote human turns, production/movement barriers, simultaneous movement options, timers/timeouts, completion signals, and RNG ownership. Uses F04's simulation phases. |
| N04 | [Hotseat and player handoff](N04-hotseat-and-player-handoff.md) | Local multiplayer player masks, passwords, turn transitions, concealed views and knowledge, per-player cursor/window state, save/load adaptation, and end/session transitions. Record unreachable e-mail paths separately. |
| N05 | [Transport and wire protocol](N05-transport-and-wire-protocol.md) | Required original TCP framing, UDP discovery, IDs/routing, handshake, partial I/O, errors and shutdown. IPX/SPX, modem, serial and legacy lobby providers remain optional reference extensions. |

### Presentation and player interaction

| ID | Document | Scope |
| --- | --- | --- |
| P01 | [Map projection and interaction](P01-map-projection-and-interaction.md) | Semantic tile/object selection, view/move actions and world-coordinate identity; original screen projection, zoom, clipping, scrolling and window geometry are reference presentation. |
| P02 | [Map rendering and minimap](P02-map-rendering-and-minimap.md) | Terrain/city/unit/resource information, visibility/remembered knowledge and selection; original raster composition, colors, decorative layers and redraw traces are optional. |
| P03 | [Graphics and asset processing](P03-graphics-and-asset-processing.md) | Supported custom asset formats, lookup/overrides, sheet identity, palettes/transparency and editor-export interoperability; native surfaces/blitters, scaling and raster fidelity are reference details. |
| P04 | [Text and dialogs](P04-text-and-dialogs.md) | Authored text/substitutions, quantities, semantic choices, input domains and accept/cancel effects; original fonts, wrapping, geometry and native controls are reference details. |
| P05 | [Input and command dispatch](P05-input-and-command-dispatch.md) | Command eligibility, actor/target routing, action gates and required timer/network ordering; menus, shortcuts, native messages and widget traces may differ. |
| P06 | [City interface](P06-city-interface.md) | City values and worker/production/unit commands, publication/cancellation and target/timeout handling; pane layout, hit geometry and decorative city scenes are optional reference presentation. |
| P07 | [Advisors, reports and history](P07-advisors-reports-and-history.md) | Report values/ranks/visibility and tax/ship/diplomacy/chat decisions; new graph/list layouts and display sorting preserve semantic identities and protocol results. |
| P08 | [Civilopedia and technology tree](P08-civilopedia-and-technology-tree.md) | Rule facts, authored descriptions, knowledge and prerequisite/link relationships; navigation controls, sorting, typography and tree geometry may differ. |
| P09 | [Council and throne room](P09-council-and-throne-room.md) | Council opinions/query effects and meaningful advice, throne qualification and persistent choices; original staging, geometry and cosmetic clip sequencing are optional. |
| P10 | [Audio, video and animation](P10-audio-video-and-animation.md) | Supported authored media, meaningful notifications, completion/skip effects and network/turn progress; original mixers/codecs/device traces, cosmetic sampling and animation timings are optional. |

### Compatibility and whole-game conformance

These two supporting documents define how the algorithm collection is used and
checked. They do not replace the subsystem specifications.

| ID | Document | Scope |
| --- | --- | --- |
| X01 | [Classic compatibility contract](X01-classic-compatibility-contract.md) | Exact target build, supported inputs and game modes, observable equivalence, original quirks versus reconstruction assumptions, unavoidable deviations, and a cross-reference to each affected algorithm. Use the owning specification for each resolved difference. |
| X02 | [Whole-game conformance](X02-whole-game-conformance.md) | Fixture/state format, random and external-input capture, phase snapshots, integration scenarios, expected outputs, source/function coverage ownership, and explicit validation gaps across the complete game. |

## Ownership and coverage rules

Each formula or state transition has one primary owner. Other documents specify
its call conditions, inputs, outputs, and ordering, then link to it. In particular:

- F01 owns sampling laws and implementation reproducibility; each consumer owns
  decision conditions, reuse, retries and state-dependent ordering. Original
  generator sequences and discarded-draw counts belong to optional evidence.
- F02 owns shared state meanings, slot/token identity and collection invariants.
  Lifecycle and mechanics documents own complete mutations and reference F02's
  contracts without duplicating its state model.
- F03 owns rule-file interpretation, reference fixups, preference defaults and
  accepted option mutations. Mechanics own the formulas consuming these rules;
  F05 owns setup orchestration and F06 owns serialized preference encoding.
- F04 and N03 own phase ordering; city/civilization/unit documents own the work
  performed in each phase.
- W01 owns shared geometry and coordinate predicates; U03 owns path search and
  coarse movement connectivity; W02 owns generation's orchestration and snapshots.
- C03 owns recalculated city totals and recurring route yields. C04 owns moods,
  disorder, tax collection and celebration growth; C05 owns ordinary growth and
  support; C06 owns production; C07 owns route creation/delivery and commodities.
  C08 owns building storage, active wonder queries, maintenance, Eiffel acquisition
  and Leonardo upgrades; it catalogs other effects and links to their consumers.
  C09 owns capture/incitement consequences, shared treasury share and central-city
  selection, used by W06 and V01 respectively.
- W03 owns revelation, remembered feature updates and the map-sharing mutations;
  callers own when they occur. Plain exploration, full tile reveal and unit
  surroundings have different effects and must not be substituted for each other.
- U02 owns allowance, actual step cost, control queries and directional dispatch;
  U03 owns estimated search costs and U04 the commands that invoke movement.
- U01 owns shared unit placement, stack repair and cargo grouping. U05 owns
  transport decisions and movement effects that call those primitives; C01/C09
  own city phases invoking supported-unit removal/rehoming.
- U06 owns combat resolution; C09 owns capture consequences; V01 owns shared
  civilization creation, elimination and civil war. F05 retains setup orchestration
  and the shared starting-site search. V06 invokes these contracts for espionage outcomes.
- V02 owns government transitions, rate controllers, population/funds checks and
  shared turn-entry treasury normalization. A08 owns AI policy choices; V04 resumes
  diplomacy maintenance after V02's government prefix.
- A01 owns shared goals, power/rank publication, civilization/region assessment
  and the outer AI movement passes. A02 owns production scores and donor
  selection; C06 owns eligibility/spending, F03 CITYPREF loading and U05 airlift
  execution. A03–A08 own other specialized choices; F04 owns session timing
  and U04 active-order execution.
- V08 owns score/objective queries, rating, Hall of Fame operations and
  retirement/outcome/quit decisions. F04 owns their session timing and F06 the
  Hall of Fame byte format; P07/P10 own report/media realization.
- V07 owns spaceship capacities, assembly, performance and shared race/reset/loss
  queries. C06 owns production spending, F04 arrival dispatch and V08 outcomes.
- V06 owns mission transitions and capital-distance pricing. U04 owns mission
  expiry and V01 interrupted-mission scans, both calling V06 escape/incident.
- V04–V06 own diplomatic/mission transitions, including V06 AI mission selection;
  A07 owns unit destinations and A08 stance/valuation policy. S02 owns event
  matching and action ordering,
  while action effects reuse their normal mutation contracts or state an exception.
- P01 owns semantic map selection and original reference transforms; P02 owns
  displayed map information and reference rendering. P05 routes semantic commands
  without duplicating P01 selection or U04 activation. P01–P10 distinguish required
  effects from optional original pixels, geometry and native traces under X01.
- F06 owns persistent byte formats; N02 owns game message/state-diff formats;
  N05 owns transport framing. UI documents own presentation and input translation,
  and link to the mechanics an accepted command invokes.

Maintain [X02's ownership and coverage ledger](X02-whole-game-conformance.md#8-ownership-and-coverage-ledger)
as implementations and independently captured fixtures are added.
For every reachable classic behavior, assign an owner or an explicit disposition
as a platform contract, delegated library operation, optional presentation/input/
transport reference, or unsupported/unreachable path. Omitted optional fidelity
must not hide required gameplay effects in the same source fragment. A source
file appearing in the navigation notes is not evidence that all of its behavior
has been specified.

## Specification structure

Keep required behavior in the main algorithm. Optional original presentation
procedures belong in a labeled reference appendix; original storage, native
services and implementation mechanics belong in implementation-reference
appendices. Retain logical identities, alias effects, ordered records, meaningful
partial failure, callbacks and state publications in the main contract.

The distinction follows effects: source-image pixels used for editor exports,
file/wire formats, privacy, authored content and game/network timing remain
required even when they occur inside presentation code. Original art, geometry,
widgets and cosmetic timing are reference details under X01. Encoding tables
remain normative at their external boundaries even when placed in an appendix.

Every document should contain:

1. The compatibility target, scope, entry conditions, inputs, outputs, and
   observable side effects, including calls to other specified algorithms.
2. Logical state and data definitions, units, coordinate systems, rule-table
   dependencies, and any rounding, overflow or sentinel behavior affecting results.
3. Reference pseudocode with constants, branch conditions, loop and scan order,
   tie breaking, random sampling laws, retained/fresh values and meaningful retries.
   Identify required dependencies and publication boundaries under X01; incidental
   evaluation order and discarded/global-stream draws are reference evidence.
4. Relevant mode and rule variations, boundary conditions, failure or cancellation
   behavior, and original quirks. Keep unresolved behavior explicit and local.
5. For file inputs, distinguish intended nominal structure/value domains from
   the original's off-nominal handling. Describe deterministic overflow/wrap,
   coercions and errors separately from unresolved failure boundaries. A more
   restrictive implementation may reject inputs outside its declared domain;
   identify that as a compatibility limit, not an original validation rule.
6. Small worked examples and conformance cases with complete initial state and
   expected state changes, return values, controlled random choices or versioned
   implementation random contexts, and external effects.
   Separate nominal examples from off-nominal compatibility cases, and both
   from diagnostic cases for excluded out-of-bounds-write bugs.
7. A clearly labeled validation/provenance section for any retained external
   source links, filenames, function/address locators and historical harness notes.
   These are research records, not implementation instructions. Keep them out of
   algorithm prose, catalog entries and implementation examples; implementers
   must not consult those sources. Resolve missing behavior in the specification.
   Keep independently observed original results distinct from source-derived checks.

Describe the main algorithm using logical properties, named values and ordered
operations that allow a new implementation to choose its own representation.
Put original byte layouts, numeric enum encodings, bitmasks and scratch-field
reuse in a validation appendix, with a mapping from logical state to reference
snapshots. Keep consequences that affect behavior in the main text: for example,
a wrapping counter, a terrain test that excludes marked tiles, or a tie-break
order. File and wire formats still require exact encodings at their external
boundaries; those encodings do not dictate an implementation's internal layout.

Include cross-references when they supply a required definition, describe an
invoked operation, or explain a relevant input, state change or scope boundary.
A shared-primitive specification does not need backlinks merely to list its
consumers. Keep examples self-contained when they illustrate the local algorithm,
and put general navigation and document ownership in this README. Retain
references to caller behavior when it affects the specified state, such as
using a repeatable layout key or retaining a sampled diplomacy threshold.
External reconstruction-source and historical harness references belong only in
explicitly marked validation/provenance sections. Keep file/wire/asset definitions
self-contained; a link to a header or source table cannot supply a missing contract.
Do not promote a source-derived harness or its expected outputs to an independently
authored cleanroom test. Required encodings remain normative regardless of appendix
placement.

## Historical validation and provenance

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

These records explain how the existing specifications were researched; they do
not prescribe a workflow for implementing this repository. New code and tests use
the behavioral contracts, declared data and independently authored fixtures.
The original executable can be exercised externally for comparison and file/peer
interoperability. If a contract is incomplete, record the gap and clarify it;
do not fill it by following a reconstruction-source link.

Original observations and source-derived comparisons are different evidence.
Historical classic-mode switches do not prove original behavior in every state;
reconstructions include fixes, preset locals and optional additions. The owning
specification must state the resolved rule and its limitations. Never adopt a
repair or assume a value merely because a linked implementation uses it.

### Source provenance

Source revisions in evidence appendices identify external OpenCiv2 commits, not
commits in this repository. The independent reconstruction is the external `civ2`
repository at `4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. Source filenames and
paths such as `src/...` in those sections are relative to the named reference
repository. They are not locations to read in this checkout or implementation
files to recreate. Original binary hashes identify the inspected artifacts.

Each specification retains its own evidence revision. F04/F05's published source
links use `a71e8aefffe04e6f719e0e213b027b128a747711`; their linked objects were
historically checked against their later evidence pins. W02's enum-name reference
uses naming revision `ce18e5e52e19870cf1598651eee7c1a97d517519`; it does not change
the algorithm target or make the external header an implementation dependency.
Retained citations are provenance, not authorization to consult source code.

Historical research bundles, source-derived harnesses and expected-result files
remain external. Recorded results retain their stated scope and do not imply
those artifacts are available here or reusable as cleanroom implementation/tests.
Included specification-vector fixtures are under [conformance/](conformance/);
X02 distinguishes those from independent original-game observations and from
implementation regression tests. Reading a provenance citation does not close a
behavioral or validation gap.

## Validation plan

[X02](X02-whole-game-conformance.md) owns the concrete fixture and comparison
contract, integration families and [evidence gaps](X02-whole-game-conformance.md#10-current-evidence-limits-and-closure-rules).
The repository includes three portable arithmetic mapping fixtures under
[conformance/](conformance/). Historical source harnesses are external evidence;
they do not demonstrate a complete cleanroom game, original-file interchange or
mixed-peer multiplayer. W02's original-target reconciliation, complete setup and
turn/resume joins, and independent G01–G23 fixtures remain validation obligations.
Keep evidence and closure criteria in X02 and the owning specification rather
than maintaining a parallel document-completion checklist here.

Each algorithm needs focused cases that check outputs and side effects at its
boundary, followed by integration cases checking the order in which systems
interact. Author cases from the stated contracts and independently captured
original-game observations; do not extract or translate a reconstruction harness.
Recorded historical coverage and successful replays have finite scope; do not
promote them to independent cleanroom tests or exhaustive conformance claims.

The whole-game case set should include:

- New and premade worlds, cylindrical and flat topology, odd custom heights,
  starting-position search, accelerated starts, and a second game in one process.
- A complete human and AI turn including growth, production, support, happiness,
  trade, research, diplomacy, pollution, and order execution.
- Land/sea/air movement, cargo, failed orders, combat and stack losses, city
  capture, partisans, bribery/incitement, civilization elimination and civil war.
- Technology and wonder transitions, government changes, global warming,
  hut/barbarian outcomes, spaceship arrival, conquest, and retirement.
- Bidirectional save/map/scenario interchange through the original game; save/
  reload at meaningful phase boundaries, custom rules and scenario events,
  editor exports surviving original reload, missing/stale optional RNG metadata,
  and rejected invalid inputs.
- Hotseat concealment and handoff; original host with cleanroom client and the
  reverse, sequential/simultaneous turns, running join and reconnect, randomized
  commands, synchronization, timeouts, mixed-session resume, and host transfer
  in both directions over TCP/IP/UDP without shared RNG state or new required packets.
- Redesigned map/city/advisor/pedia interactions with equivalent information,
  decisions, privacy, cancellation and gameplay/network effects; custom scenario
  assets/text/media; changed cosmetic timing and harmless UI failure cleanup.
- Declared malformed/unsupported input rejection, with nominal custom content,
  normal partial network delivery and accepted wrap retained. Optional original
  presentation/off-nominal/legacy-provider cases are reported separately.

Within each applicable case family, vary difficulty, government, human/AI control,
game mode, custom-rule inputs, and relevant limits. Compare state mutations,
controlled random decisions and conditional final state, ordered notifications/commands, serialized
bytes, and semantic presentation outputs appropriate to the contract. Original
pixel/sample/native-control output is an optional reference comparison. Use an
independently derived cases and original-program observations for expected behavior;
a check that only repeats the specification's pseudocode is not sufficient
original-game evidence.
