# Built-in gameplay data: extraction assessment

Status: proposal and implementation backlog. No new runtime formats, configurable
mechanics or compatibility exceptions are introduced by this assessment.

The specifications contain useful gameplay data that the original executable
supplied rather than reading from text files. Extracting selected values into
semantic JSON files is worthwhile: it makes the classic defaults inspectable,
lets the implementation load one explicit rules profile, and provides future
customization points without executable patching. Start with small, well-defined
tables and coefficients. Keep their algorithms in code.

The bundled values should reproduce **Civilization II Multiplayer Gold Edition
5.4.0f, patch 3** as specified here. They are specification-derived defaults,
not new balance choices or independently captured original-game results. Work
from this repository's contracts under the [cleanroom boundary](../architecture.md#1-cleanroom-and-compatibility-boundary).
Do not fill gaps from reconstruction code or silently adopt its repairs.

## 1. Selection criteria

Extract a value when it has a stable gameplay meaning, an identified consumer,
and a useful customization purpose: a bonus, cost coefficient, threshold,
capacity, named association or progression table. Prefer one owner and an explicit
unit over a global collection of otherwise unrelated numbers.

Do not make every numeric literal configurable. Record counts, external identity
ranges, sentinels, wire layouts, arithmetic conventions, scan/tie rules and
callback boundaries are contracts. They are not balance settings. An expression
printed in a Markdown table is still an algorithm when it contains conditions,
queries, ordered rounding or mutation.

Keep existing text-backed data in [the current formats](data-format.md): unit and
terrain statistics, advance prerequisites/AI values, improvement costs/upkeep,
wonder expiry, tribe personality, names and all 22 global rules already have
homes. For example, spaceship production costs and the mass/thrust paradigm stay
in `rules.json`; only the previously built-in spaceship data belongs in the new
file. Scenario state/settings already represented by F06/S01 also remain distinct
from shared gameplay defaults.

The names below are proposed component filenames, not added manifest keys or
released `format` identifiers. They describe game concepts rather than executable
addresses or legacy table layouts.

## 2. Recommended first extraction

These six components have bounded initial scopes and concrete original values
in the current specifications. “Ready” means the listed initial subset can be
transcribed from the contract; it does not mean a loader or conformance evidence
already exists.

| Proposed file | Initial data and original-value examples | Owner and readiness |
| --- | --- | --- |
| `calendar.json` | Four ordinary calendar profiles, each with six ordered periods containing start year, turn count and year step. Profile 0 begins `(-4000,250,20)`; profiles 1–3 begin `(-4000,60,50)`. Ordinary retirement warning/end years are 2000/2020; scenario warning lead is five turns. | [F04 §5](../algorithms/F04-turn-sequencing.md#5-turn-to-calendar-conversion), [V08 §5](../algorithms/V08-scoring-victory-and-retirement.md#5-outcome-producers-and-scheduled-retirement). Complete period table and retirement values are ready. Coordinate retirement consumers with V08's continuation rules. |
| `spaceship.json` | Named part definitions with group, maximum count and mass per part: Structural 39/100 tons; Propulsion and Fuel 8/400 tons each; Habitation, Life support and Solar energy 4/1600 tons each. Habitation capacity is 10,000 people per part. Candidate follow-on data: structural-support coefficients, thrust progression and flight penalty constants. | [V07 §§1–3](../algorithms/V07-spaceship.md#1-state-arithmetic-and-accepted-inputs). Six part rows and group membership are ready. The 100-ton conversion and tenths-of-year arithmetic remain exact normalization rules; do not replace every occurrence of a scale factor with an independent option. |
| `governments.json` | Policy keyed by government identity, separate from names/titles: maximum rate in tenths `6,6,7,8,8,8,10`; technology availability associations; corruption divisors corresponding to nominal government order `4,5,6,7,7,8,8`. | [V02 §§1–2](../algorithms/V02-governments-and-tax-rates.md#1-state-accepted-inputs-and-arithmetic), [C03 corruption](../algorithms/C03-city-economy-and-corruption.md#corruption-helper-and-analysis-scores). Ready for these fields. Celebration's effective-government shift, Statue of Liberty and Fundamentalism's enabled-advance exception remain algorithmic. |
| `difficulty.json` | Named difficulty modifiers: baseline AI food/shield factors `15,13,12,10,9,8`; initial research multipliers, human `6,8,10,12,14,14` and AI `14,13,12,11,10,10`. Later extend with explicitly inventoried difficulty adjustments. | [C05 box factors](../algorithms/C05-city-growth-and-support.md#box-factors-and-settler-food), [V03 §7](../algorithms/V03-research-and-technology.md#7-research-cost). These nominal values are derived directly from specified formulas. Preserve late-game adjustment, turn-200 food/shield distinction, paradigm scaling and subsequent research arithmetic. This is not a replacement AI heuristic. |
| `combat.json` | Named strength bonuses: veteran and paradrop each add half the current strength, sequentially; fortification ×1.5, fortress ×2, walls ×3; anti-air ×3 or ×5 for destroyed-after-attack attackers; Partisans ×8 against zero-attack units. Begin with these magnitudes, not configurable combat stages. | [U06 strength](../algorithms/U06-combat-and-veterancy.md#strength-primitives), [U06 preparation](../algorithms/U06-combat-and-veterancy.md#preparation-common-to-preview-and-execution). Values and gates are specified. Preserve replacement versus stacking, domain/identity gates and the owner's exact arithmetic for every bonus. Unit stats, abilities and terrain defense stay in `rules.json`. |
| `scoring.json` | Wonder points 20; future technology points 5; pollution coefficient −10; peace begins after turn 199, awards 3 per peace turn and caps at 100. Scenario outcome bases are 1000/750/500/250/0; rating factors by difficulty are `4,5,6,8,10,13`. Include the remaining V08 score/conquest coefficients as one reviewed set. | [V08 §§2–3](../algorithms/V08-scoring-victory-and-retirement.md#2-city-objectives-and-score-components). The score formulas and nominal rating values are available. Keep scenario objective thresholds in scenario state and retirement values in `calendar.json`, with one authoritative location per value. |

The arrays above are compact summaries in the specifications' identity order.
Actual files should identify governments, difficulties, parts and outcomes by
semantic ID. JSON array position must not become a new classic slot assignment.
Calendar periods retain meaningful order within their profile.

Calendar and spaceship are particularly good first implementation slices: their
principal tables are small and fully enumerated. Government, difficulty and
combat extraction needs more consumer tracing, but offers especially useful
customization. Scoring supplies another bounded domain with existing worked
examples.

Some nominal tables are generated by reference formulas rather than stored as
literal original tables. A new table lookup must preserve the declared behavior
for admitted off-nominal inputs too. In particular, V02 accepts some out-of-range
government arguments, C03 uses effective governments, and difficulty consumers
apply different clamps. Specify the retained fallback or explicit input rejection;
do not silently turn an originally defined branch into a missing-key error.

## 3. Further useful components

These are worthwhile follow-on candidates, but should be extracted in small
consumer-owned groups rather than as generic expressions or arbitrary effect
programs. Examples identify available values, not a claim to have enumerated
every constant in the family.

| Proposed file | Worth extracting | Required separation / source |
| --- | --- | --- |
| `city-economy.json` | Specialist yields: Entertainer luxury 2, Tax Collector tax 3, Scientist science 3; facility bonus magnitudes; city growth/retention coefficients; purchase price coefficients: ordinary improvement 2, spaceship/wonder 4, unit quadratic divisor 20 plus linear factor 2, empty-stock multiplier 2. | [C02 yields](../algorithms/C02-city-tiles-and-worker-assignment.md#tile-yield-evaluation), [C03 distribution](../algorithms/C03-city-economy-and-corruption.md#tax-science-and-luxury-distribution), [C05](../algorithms/C05-city-growth-and-support.md), [C06 purchase](../algorithms/C06-city-production-and-purchasing.md#player-purchase-command). Do not duplicate base yields/box rules or merge human pricing with AI spending. Keep rounding, live reads, food retention and completion gates in their owners. |
| `happiness.json` | Building content amounts, advance-dependent increments, martial-law magnitudes/caps, away-unit penalties and wonder happiness magnitudes. Colosseum contributes 3 plus 1 with Electronics; ordinary martial law caps at 3, Communism doubles each qualifying unit and caps at 6. | [C04 mood stages](../algorithms/C04-city-happiness-and-disorder.md#ordered-calculation-stages), [C03 tithes](../algorithms/C03-city-economy-and-corruption.md#building-content-subtotal-and-tithes). Share genuinely common authored magnitudes, but retain separate calculations, queries and normalization stages. C03's subtotal is not a cache for C04. |
| `unit-recovery.json` | Healing factors by condition and facility associations by domain. Base factor 1, fortress factor 2, nearby-city increment 1 or 2 with Barracks, distance threshold 4; retain full-repair conditions separately. | [U04 recovery](../algorithms/U04-unit-orders-and-activation.md#civilization-recovery). HP scaling, repeated distance reads, airbase admission, wake-up effects and retained-selector behavior stay in code. Full repair is not an enormous numeric bonus. |
| `terrain-work.json` | Built-in job requirements: fortress movement-cost divisor 2 plus 3; road/railroad multipliers 2/4 with river surcharge 2; pollution cleanup and airbase work 4 each. | [W04 work requirements](../algorithms/W04-terrain-work-and-improvements.md#capture-and-required-work). Terrain irrigation/mining times and the transformation rule already belong in `rules.json`. Preserve work accumulation, donor transfer, engineer effects and completion algorithms. |
| `trade.json` | Delivery coefficients and commodity premiums: Cloth/Coal/Wine/Silver add 1/2 of the captured base value, Silk/Spice/Gems/Gold add 1, Oil 3/2, Uranium 2, the others zero; route and delivery bonus magnitudes. | [C07 delivery](../algorithms/C07-trade-and-commodities.md#delivery-preparation-and-payment-formula), [C03 recurring trade](../algorithms/C03-city-economy-and-corruption.md#recurring-trade-refresh-and-second-corruption). Premium is captured once and only used at demand matches; repeated matches and the live-owner branch remain. Delivery, route-replacement and recurring trade use different formulas. |
| `environment.json` | Pollution threshold/coefficient data, industrial advance contributions, meltdown parameters, warming thresholds and named coastal/dry terrain transitions. Examples: industrial pollution subtracts 20; coastal warming changes Forest to Jungle and other eligible terrain to Swamp. | [W05 intensity](../algorithms/W05-pollution-and-global-warming.md#city-pollution-intensity), [W05 warming](../algorithms/W05-pollution-and-global-warming.md#warming-terrain-transformation). Retain population gates, duplicated neighborhood visits, severity coordinate pattern, cleanup/memory effects and sampling laws. Terrain transitions alone do not define the algorithm. |
| `huts-and-barbarians.json` | Hut award amounts and era cutoffs, mercenary/raid unit associations, spawning/ransom coefficients. Precious-metals outcomes use 25/50/100 and double only after year 1000; tribe-city development starts at year 1000. | [W06 hut entry](../algorithms/W06-huts-and-barbarians.md#hut-entry-and-consumption) and its periodic spawning/order sections. Keep the initial five-outcome draw, redirects, human/AI overrides, world-knowledge tests and placement attempts. A flat six-outcome weight table would change the classic behavior. |
| `espionage.json` | Spy escape coefficients, bribe/incitement price constants and mission-specific discounts. Incitement uses treasury offset 1000 and distance offset 3; unit bribery uses 750 and 2. | [V06 escape](../algorithms/V06-espionage-and-bribery.md#2-shared-escape-capture-and-treaty-confirmation), [V06 incitement](../algorithms/V06-espionage-and-bribery.md#8-inciting-a-revolt), [V06 bribery](../algorithms/V06-espionage-and-bribery.md#9-unit-bribery-and-sabotage). Distinct owner/government checks, staged odds, payment, capture and transfer ordering remain executable behavior. |
| `world-setup.json` | Map-size presets (40×50, 50×80, 75×120 logical tiles), initial/accelerated-start package values and selected start-quality thresholds. | [F05 setup](../algorithms/F05-game-setup-and-starting-positions.md#3-world-size-parameters-and-premade-maps), [F05 balancing](../algorithms/F05-game-setup-and-starting-positions.md#8-balancing-starting-positions). Keep original defaults and size classification. Start-site search, relaxation, compensation and development algorithms are unchanged. More than preset extraction needs a separate consumer audit. |

Research can later receive a `research.json` component for non-difficulty cost
coefficients and thresholds from [V03 §7](../algorithms/V03-research-and-technology.md#7-research-cost).
It should reference difficulty policy rather than copy its initial multipliers.
Likewise movement, visibility, nuclear effects, capture and civil-war parameters
may merit focused components when their consumers are implemented. Avoid adding
mostly empty files now merely to mirror every algorithm document.

## 4. Defer broad extraction from these families

**Commodity supply/demand.** [C07's score tables](../algorithms/C07-trade-and-commodities.md#supply-scores)
combine terrain counts, location, region, city size, technology, tribe and
facility queries. They also have cross-commodity dependencies, ordered integer
operations and an explicit incoming road-counter boundary. Terrain weights and
tribe bonuses can eventually become named data, but porting each row as a JSON
formula would create a second programming language and obscure those obligations.
Extract delivery premiums first; keep supply/demand programs in code.

**AI.** [A08's tribe/advance contributions](../algorithms/A08-ai-research-government-and-diplomacy.md#tribe-contributions)
are an attractive future data set: for example tribe 0 gives +2 to Iron Working,
Bronze Working and Warrior Code, and −1 to Monotheism. Other entries depend on
computed expansion state. [A02's production scores](../algorithms/A02-ai-city-production.md#5-improvement-scores)
and the wider A01–A08 policies are more deeply conditional. Keep the existing
heuristics, candidate admission, ties and live-state effects. Do not introduce
configurable AI policies in the first extraction. Any later data-only move should
first reproduce the same defaults and evaluate each value at its existing site.

**Map generation.** [W02](../algorithms/W02-map-generation.md) contains rainfall,
weathering and river transitions, climate/temperature vote adjustments and phase
coefficients that could eventually live in `map-generation.json`. Extracting
these is a larger audit because in-place edits, sampling laws, retries and later
passes depend on earlier results. The required generator remains unchanged.
Do not substitute generic biome rules or a new noise generator. W01 geometry,
coordinate-based resource/hut patterns and meaningful neighbor/tie order remain
contracts even if some tables are represented as internal data.

**Building and wonder effects.** [C08's catalogs](../algorithms/C08-buildings-and-wonders.md#ordinary-building-effect-catalog)
index consumers; they are not a declarative effect system. Costs, prerequisites,
upkeep and expiry are already text-backed. Bonus magnitudes belong with their
owning mechanic. Keep availability, active/local/global scope, replacement,
acquisition and triggered operations in code. For example, granting every city
an actual building to model a wonder changes maintenance, sale and presence tests.

**Diplomacy and lifecycle.** V01/V04/V05/C01/C09 contain candidate scalar values,
but their transitions, reset prefixes, treaties, payment/transfer ordering and
reentry behavior are not tables to externalize wholesale. Inventory a proposed
coefficient at its actual consumer before adding a general `diplomacy.json` or
`civilization.json` component. This assessment does not supply such schemas.

## 5. Whole-game boundary check

This grouping accounts for all 65 specification families. It records where
extraction is promising, where the existing formats already cover authored data,
and where numeric details should remain contracts.

| Families | Disposition |
| --- | --- |
| F01, F02, F06 | Arithmetic/random policy, state invariants and save encoding remain implementation/compatibility contracts. No general customization file for widths, counts, sentinels or record layouts. |
| F03 | Existing `rules.json` and classic adapters cover the text-backed data. New built-in components supplement this model without duplicating its fields. |
| F04, F05 | Calendar and selected setup values are candidates; sequencing, resume and setup algorithms stay in code. |
| W01, W03 | Geometry, coordinate features, visibility/privacy and refresh semantics stay in code. Specific reveal-radius tuning is later work, not part of changing map geometry. |
| W02 | Keep map-generation algorithm/defaults unchanged; defer parameter extraction beyond ordinary setup presets. |
| W04–W06 | Work, environment and hut/barbarian numeric data are useful follow-on groups. |
| U01–U05 | Lifecycle, stack, movement, pathfinding and transport contracts stay in code; selected recovery/movement coefficients can be extracted separately. |
| U06, U07 | Combat bonus magnitudes are an early candidate; nuclear parameters later, with interception/blast/publication logic preserved. |
| C01–C09 | City economic/happiness/trade coefficients are useful. Existing text data and state remain separate; worker selection, lifecycle/capture and effect algorithms are not generic JSON programs. |
| V01–V08 | Government, research/difficulty, spaceship and scoring data have clear candidates. Diplomacy, espionage and lifecycle need narrower consumer audits. |
| A01–A08 | Preserve heuristics; defer configurable AI coefficients until individually inventoried and tested. |
| P01–P10 | Text, council dialogue and encyclopedia content already have native formats. Rendering/layout/media choices belong to presentation/assets, not this gameplay data family. Presentation-triggered gameplay effects remain required. |
| S01–S04 | Scenario parameters, events and editor operations already have owners and external representations. Editors may eventually expose the new settings; no generic scripted mechanic/effect system is implied. |
| N01–N05 | Wire formats, authority, barriers and timeouts remain protocol/session requirements. Do not externalize them as balance knobs. |
| X01, X02 | Compatibility, provenance and conformance contracts remain authoritative. A custom balance profile does not silently weaken the classic target. |

## 6. Loading and compatibility design

Use one resolved **built-in gameplay profile** alongside the existing text-backed
content. This is a proposed architectural concept, not a new `profile` identifier
accepted by today's schemas. Both native JSON and classic text input paths use
the same selected built-in defaults. Selecting a user's `RULES.TXT` must not
remove this layer or cause duplicate definitions of its parameters.

The implementation should ship a complete classic profile populated from the
specifications. Missing custom fields should either inherit explicitly from that
versioned profile or fail validation, following a documented component replacement
policy; do not scatter fallback copies across source files. Resolve and validate
the selected profile before a new session, and keep it fixed during that session
unless a particular reload operation is separately specified. Pin it with the
resolved content for replay and new-engine checkpoints.

Compatibility has two distinct cases:

- **Original values:** moving constants into JSON does not relax the gameplay
  target. Classic save/scenario interchange and mixed original-peer multiplayer
  remain required with equivalent selected text-backed content. Neither original
  files nor peers need to read these JSON files or negotiate their presence.
- **Changed built-in values:** this is an explicit custom gameplay variant.
  The unmodified original executable generally cannot apply the changes through
  its text files. A structurally valid classic save/scenario does not preserve
  the custom rules; loading it in the original reverts those future calculations
  to original constants. Do not claim equivalent continuation or mixed-peer
  compatibility, and do not enable original-peer sessions merely because the
  cleanroom host loaded the custom files. Original clients execute some mechanics.

New-engine custom multiplayer needs agreement on effective gameplay data before
play, plus save/resume and host-transfer checks. Record a versioned digest of the
resolved semantic values, separately from file-byte integrity hashes. Different
whitespace/provenance is not different gameplay. How new-engine peers establish
that agreement is later session/protocol design; no mandatory fields should be
added to the classic protocol or reserved save bytes by this extraction.

A changed setup-only parameter can sometimes be fully represented by the resulting
classic world. That is a per-field exception requiring evidence that the original
will not need the parameter later. It is not a blanket exception for map-related
data: resource predicates, calendar, difficulty and ongoing environment mechanics
continue to run after loading. A JSON profile name alone proves no compatibility.

The default compatibility gate should compare effective built-in values with the
classic baseline. A custom value needs an explicit compatibility classification;
unsupported original-interoperability requests should fail clearly, not silently
fall back to the classic value. Native metadata remains outside classic payloads;
imported original files use the classic baseline unless a custom variant is
explicitly selected. Missing custom-profile metadata must not be guessed from
state or hidden in unused bytes.

## 7. Extraction and validation backlog

1. Start with `calendar.json` and `spaceship.json`, then the remaining first-wave
   components. For each field, record semantic name/unit, default, owning spec
   section, exact evaluation point and all consumers. Distinguish copied tables
   from nominal values derived from a formula. Do not claim a complete extracted
   family until every field in its declared initial scope is accounted for.
2. Define file/version/manifest associations and JSON Schemas. Use semantic IDs,
   typed records and exact integer/rational inputs. Specify admissible custom
   bounds from the algorithm and interchange limits, not incidental C++ storage.
   The existing `classic-mge-5.4.0f` content profile cannot silently become a promise
   that arbitrary built-in changes interoperate with the original.
3. Populate complete, owned classic-default files from the specification values,
   with a provenance record and reviewable spec pins. Do not turn current partial
   text-backed examples into purported full stock data. There is no corresponding
   classic TXT import/export adapter for a genuinely built-in field.
4. Connect each field to its original consumer without changing formulas, AI
   heuristics or map generation. JSON member order is immaterial; meaningful
   array order, conditions, arithmetic, callbacks and partial effects remain with
   the algorithm. Do not author expressions, dispatch order or executable scripts.
5. Test default normalization and the owning worked cases, including boundaries:
   calendar period edges/zero year/first launch; ship raw versus supported counts;
   effective governments; food versus shields at turn 200; sequential combat
   bonuses; score thresholds and map scaling. Test admitted off-nominal fallback
   separately. These are specification-derived checks until independently observed.
6. Test at least one isolated custom value through each owning operation, plus
   validation failures, profile resolution, stable fingerprints and save/resume.
   Preserve original mixed-peer/file tests for the default profile and explicitly
   test rejection of incompatible custom runtime profiles.
7. Only after the first slice works, extract city, recovery, work, trade,
   environment, hut and espionage parameters in small groups. Keep AI/map and
   general effect-system expansion deferred; update this assessment as concrete
   schemas and consumer coverage replace proposals.

A semantic fraction alone does not determine evaluation. For example, U06's
`strength += strength >> 1` includes a specified signed rounding operation and
intermediate retention; replacing it with a generic floating multiplier can
change results. Likewise C07's two technology discounts truncate separately,
and W06's nominal outcome percentages do not erase its conditional draws and
redirects. The files supply operands to the specified operations; they do not
redefine those operations by implication.
