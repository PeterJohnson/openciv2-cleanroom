# Standalone JSON content and classic text compatibility

Status: version 1 **format specification**, not yet a released format. Remaining
semantic component loaders, classic converter and playable default content are
implementation-plan work.
The [C++ content loaders](../m1-content-runtime.md) implement the six built-in
gameplay components, common JSON admission and pack inheritance/selection with
hash-verified mounted inputs. The [native rule decoder](../m1-rule-content.md)
admits complete typed rule definitions and their references. The
[city-preference adapters](../m1-classic-text.md) validate native preference IDs
and apply both native and classic updates, with an explicit partial comparison
profile. City names and other text-backed component bodies still require
validation before session admission.
[JSON Schemas and developer validation](schemas/README.md) check document structure;
semantic pack validation remains open. The examples below are owned format
examples, not a stock data set or evidence of gameplay compatibility.

The game supports two content paths:

- **Standalone:** explicitly selected, versioned JSON content supplies rules,
  names and every required runtime text resource without an original installation
  or original text files. The [per-file inventory](text-resources.md) defines
  coverage and explicitly separates documentation and unconfirmed legacy files.
- **Classic compatibility:** user-selected classic text resources are read by
  compatibility adapters implementing F03, C01, P04, P08, P09 and S02. They remain
  supported inputs; installing them is optional for standalone play.

Both paths supply the same logical rule tables, naming services, semantic text
and event definitions. Mechanics, AI and map generation do not branch on the
source file format. Their owning [algorithm specifications](../algorithms/README.md)
remain authoritative. These formats describe content, not saved runtime state,
X02 fixtures, an executable script language or a new multiplayer protocol.

## 1. Ownership and compatibility boundary

`oc2_formats` owns both JSON decoding and the classic text adapters. It consumes
explicit byte sources supplied by the I/O layer and publishes logical data or
an owner-defined operation into `oc2_game`/`oc2_session`. Presentation consumes
semantic text through view models. No text parser requires SDL or native widgets.
Image/font/audio decoding remains in `oc2_content` under [asset-format.md](asset-format.md).

Keep classic parsing and its effects at the compatibility boundary: section
lookup/fallback, encodings, name resolution, conversions, defaults, retained
values and accepted game/peer effects remain as specified. Under X01/F03,
unpublished parsing may be staged and failures handled without original partial
memory writes. Preserve in-game `keepNames`/leader-replacement policy; staging
does not permit discarding meaningful retained names or accepted changes. Parser
globals, stream position objects and allocation callbacks are not live game state.

Native JSON has a separate authoring contract: validate the complete selected
pack before a new-game installation, and report invalid data without partially
installing it. During a running session, apply validated content only through
the owner's permitted reload/editor operation and publication boundaries. No
implicit hot reload, city recalculation, event reset or leader-cursor reset.

Support existing classic scenario overrides, local-language variants and
section-specific fallback. A present custom file is not a request to fill every
missing row from native defaults. Preserve P04's explicit label-tail fallback,
F03's explicit advance/unit tails and the other owners' particular rules.
Do not replace them with a general JSON/text deep merge.

### Native authoring is semantic

Native files describe civilizations, rule parameters, units, advice, interactions,
scenarios and dialogue. They are not JSON transcriptions of classic file sections,
row positions, parser cursors or temporary flags. Use named identities and units
that express the game concept. For example city-name lists belong to a named
civilization, a unit has `hitPoints`, and a council response explicitly chooses
its continuation when included or skipped.

Classic addresses, slot mappings and otherwise unnamed retained values belong in
the optional [classic compatibility descriptor](#appendix-a-classic-compatibility-descriptor)
or the adapter's registered profile. Native algorithms consume semantic requests;
they must not open virtual RULES/GAME files or scan virtual section headers.
Some classic quirks affect results and therefore remain required **behavior**,
but their legacy encoding need not be the native authoring representation.

The observed-file [inventory](text-resources.md) deliberately names physical files:
it is compatibility evidence, not a template for native content. Raw file/wire
fixtures and import receipts likewise keep the encodings they document.

## 2. Common JSON conventions

Every document is one UTF-8 JSON object. `format` is the exact identifier listed
below. This unreleased version 1 draft and its examples evolve together; after
publication, an incompatible structural/semantic change requires a new version.
Reject duplicate keys, unknown required-format versions, unknown fields,
non-JSON numbers, invalid UTF-8 and unpaired surrogate escapes. Optional
`extensions` is an object keyed by dot-separated namespaced identifiers such as
`org.example.notes`. Keys have at least two segments, each matching
`[a-z][a-z0-9_-]*`. Version 1 extensions are metadata only and cannot alter
gameplay or required text semantics.

Numbers in gameplay input fields are mathematical integers in the signed
32-bit token domain used by F03/S02, unless a narrower semantic domain is stated.
This is an external authoring limit, not a C++ storage requirement. Parse integers
exactly, reject fractional values and never route them through an imprecise
floating representation. Apply the significant conversions specified below.
Strings, booleans, null and integers are not implicitly interchangeable.

IDs are nonempty ASCII strings matching `[a-z][a-z0-9_.-]*`. They identify content
within a namespace; they are not localized display names. Reject duplicate IDs
and unresolved references. Renaming display text must not renumber a unit,
technology, building, tribe or classic wire/file identity.

Rule records use semantic IDs. The selected gameplay profile and any explicit
compatibility descriptor resolve those IDs to the fixed identities used by the
classic algorithms and external saves/wire messages. Validate a complete unique
identity set; array position is never an implicit slot assignment. Custom IDs
need explicit mappings if the registered profile does not already know them.
Identity ordering still governs a consumer's specified scan or tie.

City-name lists, build preferences, text runs, choices, menu entries, document
blocks, concept declarations and event lists retain meaningful array order.
Named help/fact entries and council dialogue steps are addressed by ID, not
position. JSON object member order never determines gameplay, event action
order or a dialogue branch.

Native UI prose is Unicode and is not silently trimmed, case-folded or normalized.
Gameplay names that participate in classic matching, truncation or serialization
also follow the selected profile's declared comparison/serialization encoding.
Version 1 of the classic compatibility descriptor supports the explicit
`windows-1252` profile; additional encodings need a versioned adapter and tests.
This is a selected content profile, not a claim that every historical installation
uses that encoding. Preserve encoded-byte comparisons and owner-defined length
operations at classic boundaries, rather than replacing them with Unicode case
folding or character counts. Validate gameplay names against their actual classic
codec, including token delimiters and byte limits. Diagnose an unrepresentable
export; do not substitute question marks or add new truncation. Owner-required
truncation, such as C01 city-name publication, still applies.

The six [built-in gameplay components](gameplay-format.md) separately supply
selected values formerly fixed in the executable. They have shipped classic
defaults and explicit custom-mode admission; they do not duplicate these
text-backed records. Both native and classic text paths select the same gameplay
bundle independently of text decoding.

## 3. Pack manifest

Conventional filename: `content.json`. Format: `openciv2-content-pack/1`.

| Field | Meaning |
| --- | --- |
| `id`, `revision` | Stable pack ID and positive integer revision. Change revision whenever content/dependencies change. |
| `profile` | Exactly `classic-mge-5.4.0f`, naming the algorithm and identity family. The separate gameplay bundle mode identifies any custom operands; it does not change identity counts or claim classic equivalence. |
| `gameplay` | `{builtin:"classic-mge-5.4.0f",revision:1}`, or `{path,sha256}` selecting a complete `openciv2-gameplay/1` bundle. See [gameplay-format.md](gameplay-format.md). Root packs require it; overlays inherit by omission or replace the whole selection. |
| `defaultLocale` | Nonempty locale tag, such as `en`; also a key in every selected localized component. |
| `compatibility` | Null, or a descriptor for `openciv2-classic-compatibility/1`. Adds mappings not already supplied by the registered profile; no original files are required. |
| `base` | Null for a standalone base pack; otherwise `{id,revision,sha256}` naming the exact parent manifest bytes. |
| `files` | Component descriptors described below. |
| `scenarios` | Array of `{id,world}` defining scenario identities. World is a descriptor for its F06 world file, or null for an externally supplied world. |
| `resources` | Array of `{id,kind,path,sha256}` for loose media referenced by this pack; kinds `audio`, `image`, `video`. Empty is valid. |
| `provenance` | `{authors,license,notes}`: arrays of author names and notes, plus a nonempty license identifier/statement. It records provenance, not automatic redistribution permission. |

A descriptor is `{path,sha256}`. `path` is a nonempty relative UTF-8 path using
`/`, with no absolute prefix, backslash, empty/`.`/`..` component, URI scheme,
ASCII control character (U+0000…U+001F or U+007F), or escape through a symlink
outside the explicitly mounted pack. Hashes are 64
lowercase hexadecimal SHA-256 digits over exact file bytes. A loader resolves
only declared inputs, validates hashes before decoding, and never fetches a URL
from content. Parent packs are resolved from explicitly selected/local registered
packs, with cycle and missing-parent diagnostics.

`files` has these keys:

| Key | Format |
| --- | --- |
| `rules` | `openciv2-rules/1` |
| `cityNames` | `openciv2-city-names/1` |
| `cityPreferences` | `openciv2-city-preferences/1` |
| `text` | Object mapping locale tags to descriptors for `openciv2-text/1`. |
| `encyclopedia` | Object mapping locale tags to descriptors for `openciv2-encyclopedia/1`. |
| `menus` | Locale-to-descriptor object for `openciv2-menus/1`. |
| `documents` | Locale-to-descriptor object for `openciv2-documents/1`. |
| `council` | Locale-to-descriptor object for `openciv2-council/1`. |
| `events` | `openciv2-events/1`; empty events are explicit. |

Loose resource paths obey the same descriptor rules and must be unique; resource
IDs must also be unique. An event's sound ID must name a declared audio resource.
Graphics/fonts selected through the separate asset-package contract do not need
to be duplicated here. An overlay replaces the entire `resources` list when
present and otherwise inherits it; references must resolve in the final selection.

A root pack supplies every manifest field and component, including an explicit
`gameplay` selection, `compatibility: null` and `scenarios: []` when unused. An overlay may
omit a component to inherit it. A present component replaces the entire file;
a present locale entry replaces that entire locale catalog. There is no row,
string or event-list merge. Resolve parents first, then replacements; validate
cross-references against the resulting complete set. Reordering parent discovery
must not select different content. Runtime locale selection chooses the explicitly
requested locale only when all five localized components (`text`, `encyclopedia`,
`menus`, `documents`, `council`) supply it; otherwise use the default locale for
all five. Resolve references within that selected locale. Do not combine missing
messages or dangling label references across languages. Every advertised locale
must provide all five components and the registry's required content for the
supported operations. An incomplete translation is not an advertised locale.

Scenario IDs are unique. A nonnull world descriptor follows the same path/hash
rules as components and is selected explicitly, never by a nearby filename search.
Null declares an external-world entry: its launcher/import operation must bind
validated F06 bytes to that ID before play; null does not supply a playable world.
Briefings reference these scenario IDs. A classic file without an embedded native
ID can use Appendix A's scoped filename association after F06 selects its final
alternative. This does not add a field to the classic world file. An overlay
replaces the whole scenarios array when present and otherwise inherits it.

Scenario information-display policy belongs in the semantic encyclopedia model
(§7). Actual source presence, where needed to import/export a classic scenario,
belongs in its compatibility descriptor. Neither sets the world's Scenario flag
or invents a new load path; the owning operation still establishes that context.

Record the resolved manifest/file hashes and adapter versions for replay and
bug reports. A native pack's source or license provenance does not change merely
because a user's original file was converted to JSON. A released standalone
base pack must have independently supplied distributable rules, city names and
prose; this specification does not supply those defaults.

## 4. Rules

Conventional filename: `rules.json`. Format: `openciv2-rules/1`.
Required top-level members are `globalRules`, `advances`, `improvements`,
`wonderObsolescence`, `units`, `terrain`, `governments`, `tribes`, `commodities`,
`orders`, `difficulties` and `diplomaticAttitudes`, plus `format` and optional `extensions`.

These are rule definitions, not runtime snapshots or a transcript of RULES
columns. Names are author-facing; the mechanic owners retain their clamps,
conversions, enabled-before-fixup rule and signed/unsigned consumer views. The
[unit-conversion boundary](#appendix-b-classic-rule-and-name-conversions) explains
how differently expressed fields normalize to the same logical results exactly
once. In particular, `hitPoints: 30` means thirty hit points, not three groups.

Every record in the named collections below has an `id`; all except tribes also
have a `name`. Tribes use the more specific `pluralName`. Wonder-obsolescence
associations are defined separately below. Complete native
files supply the full identity set, including disabled identities. The classic
adapter's short-table defaults can be materialized by a converter; native content
does not hide missing definitions behind file-reader defaults.

| Collection | Required identities in this profile | Additional fields |
| --- | --- | --- |
| `advances` | 100 | `aiValue`, `civilizedAiModifier`, `prerequisites` (exactly two advance references), `epoch`, `category`. |
| `improvements` | 67 | `costRows`, `upkeep`, `prerequisite`. Includes ordinary buildings, spaceship parts, Capitalization, wonders and the None identity. These identities retain their fixed meanings. |
| `units` | 62 | `obsoleteBy`, `domain`, `movementPoints`, `range`, `attack`, `defense`, `hitPoints`, `firepower`, `costRows`, `capacity`, `role`, `prerequisite`, `abilities`. |
| `terrain` | 33 | `movementCost`, `defenseMultiplier`, `food`, `shields`, `trade`; the 11 base terrain identities also require `irrigation`, `mining`, `transformation`. Special-resource identities omit those three fields. |
| `governments` | 7 | `maleTitle`, `femaleTitle`; each ID resolves to its fixed government meaning. |
| `tribes` | 21 | `pluralName`, `adjective`, `leaderNames`, `preferredLeaderGender`, `preferredColor`, `cityStyle`, `personality`, `governmentTitles`. These are reusable tribe definitions, not the eight civilization slots in a running world. |
| `commodities` | 16 | No additional fields. Names label fixed commodity identities. |
| `orders` | 12 | `shortcutHint`: empty or one character in the profile encoding. A label hint does not register a command or keyboard binding. |
| `difficulties` | 6 | No additional fields; names do not define difficulty formulas. |
| `diplomaticAttitudes` | 9 | No additional fields; names do not define diplomacy formulas. |

`leaderNames` is `{male,female}`. `preferredLeaderGender` is `male` or `female`.
`personality` is `{aggression,expansion,civilizationPreference}`, the three F03
traits with their existing AI effects; no heuristic is redefined by these names.
Preferred color and city style retain the profile's logical color/style identities.
`governmentTitles` is the ordered override array described below.

`hitPoints` is an actual HP quantity; the native authoring domain is 0…250 in
multiples of ten. `defenseMultiplier` is an exact nonnegative rational
`{numerator,denominator}` with positive denominator and an integral doubled value
in 0…255; e.g. `{numerator:3,denominator:2}` means ×1.5. Parse and compare rationals
exactly. These native restrictions are explicit; they do not narrow the classic
adapter's off-nominal accepted domain. Unsupported conversion is diagnosed.
`movementPoints` is in whole movement points. `costRows` deliberately remains a
row cost: the live shield-row rule determines its shield value, so replacing it
with a fixed shield total would change behavior when that rule changes.

An advance reference is `{"advance":"id"}` or `{"special":"none"}` or
`{"special":"never"}`. Never conflate None, Never, advance 0, Future Technology
(index 89) and an invalid/unresolved reference. Native unresolved references are
errors. Resolve IDs to indices before running F03's disabled-reference fixup;
do not add acyclicity checks to nominal classic input because a JSON reader
happens to use a graph representation. F03/V03 own graph traversal behavior.

`wonderObsolescence` has exactly 28 objects `{wonder,advance}`. `wonder` refers to
one of the fixed wonder improvement IDs, once each; `advance` uses the reference form above.
Expiry references do **not** pass through disabled-advance fixup.

The profile uses these named domains (classic numeric mapping in Appendix B):

- `domain`: `land`, `air`, `sea`.
- `role`: `attack`, `defend`, `naval-superiority`, `air-superiority`,
  `sea-transport`, `settle`, `diplomacy`, `trade`.
- `epoch`: `ancient`, `renaissance`, `industrial`, `modern`.
- `category`: `military`, `economic`, `social`, `academic`, `applied`.

Convert named domains to their specified logical codes before F03 processing.
Other numeric fields follow their F03 row conversion, including leader color/
style clamps and signed AI modifiers. A classic adapter may admit additional
codes under its declared input profile; native version 1 need not encode every
off-nominal source execution. A converter must reject or report an unsupported
representation instead of silently repairing such data.

`abilities` is a duplicate-free array drawn from `see-two`, `ignore-zoc`,
`amphibious`, `submarine`, `attack-air`, `coast-bound`, `ignore-walls`,
`carry-aircraft`, `paradrop`, `alpine`, `anti-horse-defense`,
`fundamentalism-free-support`, `destroyed-after-attack`, `anti-air-defense`,
`detect-submarines`. Array order is immaterial. Appendix B maps these capabilities
to classic positions. Otherwise unnamed retained flags go in the compatibility
descriptor instead of becoming invented gameplay abilities.

A terrain result is `{"terrain":"base-terrain-id"}`, `{"special":"no-result"}`
or `{"special":"improve-in-place"}`. Irrigation and mining objects contain
`result`, `yieldBonus`, `workTurns`, `aiGovernmentThreshold`; transformation is
just a result. Apply W04's actual admission/behavior to the decoded result rather
than assigning new meaning to an unusual result accepted by F03.

Tribe `governmentTitles` is an ordered array of `{government,male,female}` overrides for
non-anarchy government IDs. Initialize defaults from the government records, then apply overrides
in order; duplicate government overrides retain the last result. Anarchy cannot
be overridden. In-game preservation of old leader data/name cursors is controlled
by the caller, not by replacing the JSON definition or resetting process state.

`globalRules` is an object with exactly these named integer members. They expose
gameplay parameters; F03 supplies their clamps and food-box rounding. Their
listing here is not a JSON member-order requirement:

```text
roadMovementMultiplier, triremeLossDenominator, foodPerCitizen, foodBoxRows,
shieldBoxRows, settlerFoodLowGovernment, settlerFoodHighGovernment,
unhappinessCitySize, unhappinessEmpireSize, aqueductSize, sewerSize,
technologyCostParadigm, transformationWork, monarchyFreeSupport,
communismFreeSupport, fundamentalismFreeSupport, communismPalaceDistance,
fundamentalismScienceLossPercent, productionSwitchLossPercent, paradropRange,
spaceshipMassThrustParadigm, fundamentalismScienceCapTenths
```

These names expose parameters, not new tunables for hard-coded AI, combat,
difficulty or map-generation formulas. Those constants remain specification-owned
code/data, and are not inferred from an original executable at runtime.

## 5. City names and production preferences

`openciv2-city-names/1` contains `civilizations` and `fallbackNames`.
Each civilization is `{pluralName,cityNames}`: its current plural name and an
ordered list of proposed city names. This is a name-based selector because C01
uses the **live civilization plural name**, including custom renamed/loaded
civilizations. It is not a permanent tribe-ID association. Reject ambiguous
plural names under the profile's name comparison. A complete base supplies a
nonempty list for every tribe's starting plural name.

`fallbackNames` is the shared ordered fallback pool. Null means unavailable;
`[]` means present but exhausted. Keep that distinction and C01's cursor lifetime,
wrap, continuation into the fallback pool, two-pass retry, publication truncation
and failure result. A civilization with no matching entry does not acquire a
list through an invented tribe-ID fallback. City names can affect scenario event
matching. Names are nonempty single-line text subject to the profile's declared
admission rules; no uniqueness filter, random suffixes or implicit sorting.

No native entry is a text-file section, and authors do not supply `@STOP` or
header strings. The header-derived result in C01's counter-wrap case remains a
compatibility result, calculated at the boundary described in Appendix B; it
does not require constructing a virtual file or exposing a scanner cursor.

`openciv2-city-preferences/1` contains:

| Field | Meaning |
| --- | --- |
| `humanCityDefensePriority` | `deprioritize` or `unchanged`. The former applies A02's human-city defense preference; the latter retains the incoming preference. It does not globally prohibit defensive production. |
| `buildPreferences` | Ordered improvement IDs for buildable non-wonder projects, at most 32, preserving duplicates. Empty clears the active preference count. |

This is a preference application, not a full settings snapshot. Omission of a
classic NODEFEND section must not reset a retained preference. Appendix B records
the old flag and name-resolution mapping. IDs identify the same build preferences;
the priority list does not change AI heuristics or acquire a cost sort. Diagnose
an ID that cannot round-trip through a classic name collision rather than silently
choosing a different improvement.

## 6. Text and interaction catalogs

These five content forms cover messages/labels/help/facts, menus, documents,
council dialogue and (§7) encyclopedia topics. The [per-file inventory](text-resources.md)
identifies each classic input and its native destination. All require `format`
and `locale`, matching the manifest key; optional `extensions` follows §2.
Empty collections are explicit, and do not establish completeness for an operation
that needs their content. New prose and layout are permitted; selected authored
scenario text, information, choices and effects must survive conversion.

### 6.1 Text and interaction catalogs

Format `openciv2-text/1` requires `labels`, `messages`, `helpTopics`, `facts`
and `productionAdvice`.
IDs are unique within each collection. Cross-references identify their collection;
menus reference labels, and briefings reference messages.

Labels are `{id,text}` objects; text is a literal string. Messages have
`{id,parameters,body,choices}`, with optional `title`, `inputs` and `defaults`.

- `parameters` maps names to `text`, `integer` or `boolean`. They are values
  supplied by a registered semantic interaction, never expressions to execute.
- `body` and `title` are arrays of text runs. A run has exactly one member:
  `{"text":"literal"}` or `{"parameter":"declared-name"}`. Newlines are literal
  `\n`; integer parameters render in base ten with a minus sign when needed.
  Boolean parameters are control values and cannot appear in text runs. The
  owner can supply preformatted text parameters for dates, units, hexadecimal
  values or other P04 formatters. No implicit separators, recursive expansion
  or executable templates. Authored text is not evaluated a second time.
- `choices` is an ordered array of `{id,label}`; label is text runs. These are
  registered semantic choices, including ordinary options and custom completion
  buttons. A choice ID does not itself imply a numeric classic result. Code owns
  availability, target/actor identity, result mapping and mutations.
- `inputs` defaults to `[]`. Each item is `{id,kind,label}`, with optional
  `initial` and `maxLength`. Kind is `text` or `checkbox`; label is text runs.
  Initial is `{"literal":value}` or `{"parameter":"name"}`, with string/`text`
  for a text input and boolean/`boolean` for a checkbox. Text-only `maxLength`
  is a positive integer in the operation's declared length unit (classic encoded
  bytes when that is its domain), not an assumed Unicode character count.
- Optional `defaults` is `{choice}` where choice is a declared choice ID or null.
  An omitted input initial/default choice inherits the owner-supplied value;
  null means no resource-requested selection. The operation registry specifies
  when authored defaults may apply. P04's original-empty-input and zero-choice
  gates belong to decoding the classic resource, not a required frontend state
  machine. Defaults do not overwrite accepted game values or submit a command
  merely by receiving focus.

Input IDs are unique within a message, as are choice IDs. Validate parameter
references and types in every run/initial. The registry constrains semantic input
types, length domains, automatic defaults, accepted effects and any actual
cancel/protocol response. Resources cannot create a command, widen a mechanic's
input domain or change a network reply. The frontend may redesign navigation and
interaction, retaining choice/input identities at acceptance. The array order
is authored order, not mandatory screen order. Widget type, modal state and
draft publication on cancellation are not part of this file format. A cancelled
local draft can be discarded; an already accepted game operation keeps its
effects under its owner. This follows X01/P04's presentation policy.

A message may also be a fragment: callers can append an ADVICE body to its
MILITARY/DOMESTIC header, append dynamic eligible choices to a prompt, or build
a PEDIA detail page from facts. Keep authored fragment meaning and any order
needed to interpret its text, not the original sequence of buffer appends.
Bind current query values at the semantic interaction boundary; a converter
cannot freeze a game query result into its content file. UI appends, measurement
and drawing create no game callbacks or partial game publications. The registry
states meaningful parameter binding and accepted-operation boundaries.

A help topic is `{id,subject,parameters,body}`, where body uses the same text
runs and parameters as a message. Subject is exactly one of `{"globalRule":key}`,
`{"rule":id}` or `{"interaction":id}`. These identify a named global parameter,
a rule definition or a registered operation. No source row number is required.
A fact is `{id,parameters,body}`; its semantic ID identifies text selected by an
owning rule, e.g. an alpine unit's movement fact. Fact definitions do not supply
new predicates or replace computed rule information. Presentation order comes
from the operation's semantic field order, not the array order of these catalogs.

The registry binds semantic requests to IDs and declares the required parameter,
input, choice, help and fact coverage for supported operations. Named help topics
replace DEBUG's ordered parameter descriptions; named facts replace PEDIA's
capability/fixed-fact rows. Appendix A maps the indexed legacy input during
import/export. Authors need neither numeric label indices nor classic filenames
in these catalogs. Unknown unused content may be retained, but cannot create a
new executable operation. Missing required semantic content fails native pack
validation.

### 6.2 Production advice

`productionAdvice` is `{headings,recommendations,wonderMessage,defaultMessage}`.
Headings is `{military,domestic}`, each a message ID. Recommendations is an array
of `{productionName,message}` keyed by the proposed unit/building's live display
name; reject ambiguous duplicate keys under the profile's name comparison.
`wonderMessage` and `defaultMessage` are message IDs. All references resolve
within this text catalog and satisfy the P06 advice interaction's parameter types.

P06/A02 still choose the recommendation. P06 composes its heading and selected
body, selects the general wonder message where specified, and falls back to the
default message on a name miss. This name-based rule is observable and stays
explicit; replacing it with an unconditional unit-ID lookup would change behavior
on renaming. Science advice and other fixed interactions use their registered
message IDs. Appendix A owns ADVICE section spelling and fallback addresses.

Classic-selected resources retain F03/P04 lookup, Append/PreserveBlank, live
substitutions, DEFAULT/LENGTH meanings and fallback. Native parser-buffer
publication is reference only; X01 permits staged decoding. Their
adapters translate semantic requests to source addresses. Native consumers do not
construct `@` keys or execute a line parser. A converter must diagnose unsupported
used constructs, retain the classic selection or reject conversion explicitly;
it cannot substitute stock text or freeze a live value into a message.

### 6.3 Menus

Format `openciv2-menus/1` contains `menus`, an array of `{id,label,items}`.
Label references a text-catalog label ID. Items form an ordered tree:

| Kind | Required fields besides `kind` | Meaning |
| --- | --- | --- |
| `action` | `id`, `action`, `label` | Stable entry ID, registered semantic action ID, text label ID. |
| `submenu` | `id`, `label`, `items` | Nested group with ordered items. |
| `separator` | `id` | A grouping boundary, no command. |

Menu IDs and all entry IDs within a menu are unique. Action identity is independent
of entry position, localized label, shortcut spelling and platform command ID.
No predicates, command numbers or executable handlers are authored here. P05 and
the operation owner supply enabled/hidden/checked state, dynamic item insertion,
mutable labels, retained update fields, command routing and authority. Catalog
labels are initial text; a permitted live label change is not undone on repaint.
Validate the action set against the registered menu surface; missing required
actions or an unknown action is an error. Availability is never inferred from
presence in a file. Platforms may regroup entries while keeping every available
action reachable and preserving behaviorally significant option order/results.

Classic MENU mapping is specified in Appendix A and P05. The distinct companion
map-editor surface can use this tree format, but needs its own registered actions;
representing its labels is not a claim that its executable has been implemented.

### 6.4 Scenario briefings and credits

Format `openciv2-documents/1` requires `briefings` and `credits` arrays.
A briefing is `{id,scenario,message}`. Scenario references a scenario ID declared
in the manifest; message resolves in the same locale's text catalog. Its
registered S01 interaction supplies scenario name, technology paradigm and
formatted start/end values. Briefings do not infer identity from a filename.

Use the final selected world's associated scenario identity, including after
F06 alternative selection. Reject duplicate briefing associations for an identity
in one locale. Missing briefing takes S01's generic ScenarioLoaded branch; it
does not alias another scenario. Title, active-unit/objective sprite admission,
accept/reselect result and load/setup mutations remain S01. Appendix A describes
classic filename association, including text with no bundled matching world.

A credits record is `{id,blocks}`. Blocks are ordered `{"kind":"heading","text":s}`,
`{"kind":"text","text":s}` or `{"kind":"blank"}`. Text is literal; repeated/empty
lines are not dropped. Headings are semantic emphasis, not a specific color or
font. The registered About choices select credits by ID; the standard profile's
selectable identities are `base`, `multiplayer`, `fantastic-worlds`, `scenarios`.
Native content provides appropriately attributed, independently supplied credits
for those choices and can add `openciv2` or pack-specific records. It need not
repeat original names when crediting replacement work. Classic mode/file numbers
and heading markers belong in Appendix A. Selected imported credits retain their
authored lines; completion/cancellation/polling remain P10/P05 behavior. Scrolling,
line caches and decorative backgrounds are optional presentation. Documentation
files are not implicitly added to runtime credits.

### 6.5 Conditional council content

Format `openciv2-council/1` contains `eras` and `anarchy`. Eras is an array of
`{era,opinions}`, using unique era identities `early`, `middle`, `late`. P09's
existing era calculation selects them; these names introduce no calendar thresholds.
Each opinion has `{advisor,opinion,opening,start,dialogue}`. Advisor is `military`,
`science`, `economic`, `foreign` or `domestic`; opinion is the named result in
Appendix C. Complete content supplies all 34 opinions per era. The names identify
P09's results; they do not redefine its opinion priorities or AI decisions.
Opening is `{caption}` with an optional clip; caption is literal text.

Dialogue is an object mapping step IDs to the following records. Start names
its first step. All references must resolve, all steps must be reachable, and
the graph must be acyclic. Object order is immaterial. The graph describes the
conversation's possible continuations, not a classic token/parser program.

| Kind | Other fields | Meaning |
| --- | --- | --- |
| `response` | `advisor`, `opinion`, `caption`, `whenIncluded`, `whenSkipped` | Opinion is null (any) or a named result for that advisor. Caption is string or null (suppressed); optional clip. |
| `chance` | `probability`, `onSuccess`, `onFailure` | Exact rational probability `{numerator,denominator}`, between zero and one, representable in tenths for this profile. |
| `end` | None | Successful end of the conversation. |

Emit the opening, and initially mark no responding advisors as heard: the opening
speaker may respond once later. At a response, skip it if that advisor has already
responded or its nonnull opinion differs from the live P09 result. Follow
whenSkipped. Otherwise emit the response, mark that advisor heard and follow
whenIncluded. After three total chapters (opening plus two responses), finish
successfully without visiting another step. A null caption still counts as an
included response. End also finishes successfully, even with no responses.

A chance step selects success with its authored probability and follows the
corresponding edge. It does not change a hidden acceptance flag. F01 owns the
random policy; Appendix C specifies the exact ten-outcome selection and classic
conversion, including zero/one probabilities. Different response outcomes can
lead to distinct copies of a chance step when their later continuations differ.
A converter must preserve those continuations rather than introduce a global
`accepted` variable into the native format.

Optional clip is `{resource,startFrame,endFrame}`, referencing a declared video
resource and integer media coordinates. Preserve imported ranges, including a
reversed range; unsupported playback uses an explicit media admission policy.
Skipping video still presents meaningful captions and preserves P09/P10 effects.
Anarchy is `{caption}` with string or null and optional clip; P09's anarchy gate
selects it instead of ordinary council dialogue. These media choices do not
change opinion calculations, seen-advisor rules or the response limit.

## 7. Encyclopedia

`openciv2-encyclopedia/1` contains `locale`, `scenarioPolicies` and `topics`.
A topic has `{id,kind,subject,title,body,links}`. `kind` is `advance`, `improvement`, `wonder`,
`unit`, `terrain`, `government` or `concept`; `subject` is the corresponding rule
ID, or null for a concept. Title is a literal string; body is a literal string
or null for an unavailable rule description. Concept bodies must be strings.
A complete native catalog supplies each list-eligible rule identity exactly once,
including body=null where no description is available; absence of prose does
not remove a rule from its eligible list or suppress its computed facts. `links`
is an ordered array of topic IDs. Reject duplicate IDs or kind/subject pairs
(except null concept subjects) and dangling links, but allow link cycles.

`scenarioPolicies` is an array of `{scenario,descriptionsAvailable,showStandardAdvanceFacts}`.
Scenario references a manifest ID, unique within this catalog; the other fields
are booleans. Omitted scenario policies default to true/true. They describe the
information offered when a separate scenario resource context is active. The first follows P08's per-operation Scenario/directory gates;
false disables/skips description content at those gates. The second controls the
twelve fixed advance facts, but does not suppress expiration facts or query effects.
A base native catalog ordinarily has an empty policy array. These are not
permissions to reveal hidden state or bypass P08's eligibility. Appendix A maps the old DESCRIBE/
PEDIA presence tests and verifies policy agreement when converting a scenario.

P08 supplies topic eligibility, prerequisite relationships, knowledge visibility
and current rule facts. Display sorting, navigation/history and tree layout are
frontend choices; topic IDs remain stable through them. Prose cannot override those facts
or reveal hidden state. A topic's rule identity is separate from its name;
translations do not renumber rule entries. Native text can describe mechanics
without copying stock prose. Classic `DESCRIBE` indices/concepts map to these
topics. `PEDIA` prompt templates and fact text map to §6 messages and named
facts; their eligibility and assembly remain P08. Both inputs retain their
adapter and resource-fallback behavior. The native topic model preserves the
selected semantic content, including explicit links, without requiring classic
section numbers or a native control layout.

## 8. Scenario events

`openciv2-events/1` contains `events`, an ordered array of
`{id,trigger,once,actions}`. IDs are unique. `once` is boolean. Definitions
contain no Consumed state, wildcard bindings, RNG state or runtime cursor;
those belong to F02/F06 and must survive load/resume by the normal owner rules.
Installing fresh native definitions starts from S02's zeroed event defaults,
sets only the declared trigger/actions and JustOnce, and leaves Consumed clear.
Where S02 stores display-name payloads, populate them from the selected rule/world
names at installation; symbolic selectors use the corresponding S02 spelling.
Do not populate name fields the owning parser deliberately leaves absent, or
refresh stored names on every subsequent match. Unused text/location slots
remain absent/zero as in a fresh record. An ordinary base pack uses an empty array.

Each trigger has a `type` and the following fields. Unit/advance references here
are rule IDs resolved to their fixed indices; no filename/name search is added.

| Type | Other fields |
| --- | --- |
| `unitKilled` | `unit`, `attacker`, `defender` |
| `cityTaken` | `cityName` (literal matching name), `attacker`, `defender` |
| `turn` | `turn`: nonnegative integer or `every` |
| `turnInterval` | `interval`: nonnegative integer, including S02's zero behavior |
| `randomTurn` | `denominator`: integer 1…1000 |
| `negotiation` | `talker`, `talkerType`, `listener`, `listenerType`; classes `human`, `computer`, `either` |
| `scenarioLoaded` | None |
| `noSchism` | `defender` |
| `receivedTechnology` | `receiver`, `technology` |

Civilization selectors are `{"civilizationSlot":0…7}` or `{"special":"anybody"}`,
`{"special":"trigger-attacker"}`, `{"special":"trigger-defender"}`. These denote
world civilizations or trigger roles, not tribe-definition indices. Appendix B
records their classic sentinel mapping.
Negotiation definitions initialize S02's permission to Deny; NoSchism retains
its veto behavior. No new permission switch is implied by the JSON syntax.
Trigger-receiver uses the same Whom binding as trigger-defender. Only use a
selector where S02's resolver admits it; do not give Anybody or indirect values
new wildcard/recursive meaning in consumers. `unitKilled.unit` is a definite
unit ID. Action unit selectors additionally accept `{"special":"any-unit"}`
where S02 permits that code; their execution still follows its supported domain.

`actions` is an object. Omitted keys disable an action. Each present key occurs
once and has its complete payload; parsing it cannot depend on another action's
partial fields. The allowed keys and payloads are:

| Key | Payload |
| --- | --- |
| `playSound` | `{sound}`: a declared audio resource ID |
| `playMusic` | `{track}`: positive logical music-track identity; no CD device is required. Media service owns unavailable/skip behavior |
| `createUnit` | `{unit,owner,veteran,homeCityName,locations}`; veteran boolean, homeCityName literal name, 1…10 coordinate pairs in preference order |
| `moveUnit` | `{unit,owner,rectangle,destination,quantity}`; quantity nonnegative integer or `all` |
| `changeTerrain` | `{terrain,rectangle}`; terrain references a base terrain |
| `makeAggression` | `{who,whom}` |
| `changeTreasury` | `{receiver,amount}`; signed amount |
| `destroyCivilization` | `{whom}` |
| `grantTechnology` | `{receiver,technology}` |
| `showText` | `{lines}`: up to 20 literal strings, preserving empty strings |
| `suppressWonderMovies` | `true` |

Coordinates are integer `[x,y]` pairs in W01's doubled coordinates. Rectangle
is exactly four pairs in S02's original corner order; do not sort corners or
substitute screen coordinates. Home-city names and city-trigger names retain
S02 matching and special-name behavior. Decode definitions into its logical
records; classic event text still uses S02's original grammar/name resolver and
may have different accepted off-nominal parsing behavior.

Execute enabled actions in the table order above, as specified by S02 §8,
regardless of JSON key order. Retain live flags/parameters, failed-child behavior,
reentry and **late** Consumed publication. The `once` flag is not an early
reservation. Imported saves restore event runtime state; they do not blindly
reinstall these fresh definitions. No Lua runtime is required for either format.

## 9. Source selection, conversion and interoperability

The session explicitly selects a native pack or classic content roots. Optional
hybrid selection may use classic rules with native stock UI, for example, but
it must identify the source of each component in the resolved content record.
When a hybrid selection combines classic and native content, record selection
per semantic service (e.g. Civilopedia facts versus dialog messages). Its
compatibility resolver still tracks each underlying classic source and presence
separately. A catalog-wide override flag cannot model a scenario PEDIA override.
Reject an ambiguous selection; derive native scenario policy from the selected
sources when converting, and let classic-selected operations keep their original
presence/fallback behavior.
Do not detect a nearby original installation automatically. A classic scenario's
supplied rules/text/media retain their specified override precedence; selecting
new stock UI is not permission to discard that authored content. Missing or
invalid selected resources produce owner-appropriate diagnostics/fallbacks,
not silent substitution of a different ruleset.

`oc2 content resolve` inspects the selected pack chain, files and gameplay operands;
`oc2 content validate-rules` additionally admits the selected native rule definitions
and government advance bindings. Neither command installs a session;
complete pack validation and explicit import/export of supported classic resources
remain implementation work. Conversion reads user-selected input and writes
only a separate requested destination. Retain source hashes, selected language,
source kind, adapter version and unsupported/lossy fields in a receipt. The
receipt is local metadata outside classic saves/wire payloads. Do not include
absolute machine paths in distributable pack manifests. Retaining original
source bytes for lossless re-export is an optional local facility; a standalone
pack cannot depend on those bytes being present.

Classic saves/maps/scenarios, editor exports and N01–N05 messages retain their
existing formats and identities. Native content IDs/revisions/hashes do not go
into reserved bytes or become a required peer negotiation. An original peer
cannot apply changed built-in gameplay values; the [gameplay compatibility gate](gameplay-format.md#8-runtime-and-compatibility-work-still-required)
requires the classic baseline for original-peer play. An original peer
cannot read these JSON files: export applicable rules/scenario resources through
the classic codecs and provide equivalent content through the original workflow.
Validate representability and content selection, not merely that both sides use
the same nominal pack title. Unrepresentable content must fail an explicit
classic export/interoperability request rather than be silently approximated.

New-engine checkpoints may bind the selected content hashes outside classic
payloads. A supported original save still loads through explicit content selection
without requiring that sidecar. Do not change imported map seeds, scenario event
continuation or operation authority to accommodate the new content format.

## 10. Validation and delivery

The [format examples](examples/README.md) illustrate individual documents and
records, including every runtime text form. They are deliberately incomplete as
a playable pack. The [schemas and developer checker](schemas/README.md) validate
structure and documented field constraints, including the expected rejection of
the rules and council fragments. They do not resolve a selected pack, validate
semantic references or establish runtime acceptance. The schema guide lists the
additional checks that remain required.

M1 implements the manifest/resolver, native rules/names/preferences and classic
adapters, typed models, diagnostics and initial authored fixtures. M2/M5 adds the
registered semantic messages/help/facts/advice, menus, documents, conditional
council dialogue, encyclopedia and scenario authoring paths.
Before claiming a standalone playable release, supply the complete base pack,
required interaction registry and provenance, and pass these checks:

1. Start a fresh game with access only to native content; no original executable,
   installation, text files or hidden fallback directories.
2. Feed equivalent authored JSON/classic fixtures through their adapters and
   compare logical tables, city-name sequences, preferences and conditional
   gameplay effects under X02. Include disabled advances, scaled movement/HP,
   cost/AI conversions, duplicate names/preferences and retained reload fields.
3. Exercise classic missing-section/fallback, supported custom values, partial
   failure and live publication; do not use native prevalidation to erase those
   compatibility requirements. Native failures leave a pre-install session intact.
4. Verify every required runtime row of the [text inventory](text-resources.md):
   text/input parameters, choice identity/results, named help/facts, dynamic advice
   lookup, menu actions, five-component locale selection, scenario/alternative
   briefings, explicit scenario DESCRIBE/PEDIA presence gates, all credits modes
   and council branches. Include imported rejected-response/greater-R and `#`
   continuations, initial-speaker repetition, opinion mismatch and three-chapter
   termination; the native dialogue must express the same branches without a
   parser-acceptance variable.
   Cover classic overrides, city-name event matching, fixed event action order
   and save/resume state. Documentation-only files must never supply runtime rules.
5. Reject unknown versions/fields, duplicate IDs/keys, missing slots/references,
   malformed numeric data, path escapes, hash mismatches and unsupported classic
   conversions. A file-format error must identify component, record and field.
6. Run bidirectional original-file exchanges and mixed original-peer sessions
   with equivalent selected content; JSON-only self-round-trips do not establish
   original interoperability. Keep AI/map-generation acceptance unchanged.

## Appendix A. Classic compatibility descriptor

This appendix is the compatibility boundary, not the standalone content model.
Format `openciv2-classic-compatibility/1` contains `profile`, `encoding`,
`ruleSlots`, `textLookups`, `scenarioFiles`, `scenarioResources` and
`retainedUnitFlags`, plus `format` and optional metadata-only `extensions`.
Profile must match the manifest. Encoding is `windows-1252` for version 1.
When no descriptor is supplied, use the registered profile's mappings and codec;
if a required identity remains unresolved, fail validation. Never assign slots
from JSON order or localized names. Original file payloads are not dependencies
of this descriptor. The format/profile registries are implementation work.

- `ruleSlots` maps each native rule-collection name to an array of `{id,slot}`.
  Each ID must be defined in the selected rules; slots have the domains in
  Appendix B. These supplement profile mappings for custom IDs. After resolution,
  each required slot has exactly one definition and each definition one slot.
  An explicit mapping cannot relocate an already registered canonical identity.
  The descriptor is replaced as a whole by a manifest overlay, not deep-merged.
- `textLookups` maps locale to arrays of `{source,target}`. Source is
  `{resource,section}` with optional nonnegative `index`; resource is an uppercase
  extensionless stem matching `[A-Z][A-Z0-9_-]*`, and section is the exact F03
  lookup argument **before** it prepends `@`. Target is `{kind,id}`, with kind
  `label`, `message`, `help` or `fact`, resolving in that locale's native catalog.
  Index addresses an owner-defined logical item, not a byte offset: label indices
  exclude the leading count, POPUPS indices 0/1/2 are OK/Help/Cancel, and HELP/fact
  rows follow the consumer's specified sequence. Duplicate source addresses
  under the classic comparison are errors; absent index differs from index 0.
  Multiple addresses may share one target. These mappings register no commands.
- `scenarioFiles` is an array of `{file,scenario}`. File is a basename including
  its world-file extension; scenario references a manifest ID. Names are unique
  under classic filename comparison **within the selected scenario context**.
  Associate the final F06-selected alternative, not the initial request. This
  is an adapter for identity-less original worlds, not native briefing lookup.
- `scenarioResources` contains `{scenario,locale,presentResources}` records,
  unique by scenario/locale, recording which uppercase resource stems exist in
  that selected scenario scope, even when empty. This is conversion/export
  metadata; it does not set the world's Scenario flag or create files at runtime.
  Verify agreement with the native encyclopedia's policy: DESCRIBE presence maps
  to descriptionsAvailable, and PEDIA presence maps to the negation of
  showStandardAdvanceFacts. P08 still owns each operation's directory/Scenario
  gate and the fact reads/effects that remain when display is suppressed.
- `retainedUnitFlags` contains `{unit,positions}` records for otherwise unnamed
  classic data. Unit resolves to a rule ID; positions is a duplicate-free array
  containing only 15 in this version. Publish/serialize that retained value
  without inventing a new capability. Named abilities remain in native rules.

When converting indexed text, decode its consumer-specific markers first. For
example PEDIAUNITFACTS discards its first character; native fact bodies contain
the resulting authored prose, and a classic exporter restores a suitable marker.
Preserve P08's special expiration-prefix and ordered assembly behavior at the
corresponding semantic boundaries; never apply those source transforms twice.

The registered semantic request determines parameter types, composition, input
publication and classic result mapping. A native view asks for its semantic
message/help/fact; it never needs a source address. The classic adapter translates
that request to the selected text resource and executes F03/P04 at its required
boundaries. Conversion maps live STRING/NUMBER/HEX slots to typed semantic
parameters/formatters; it must not freeze their current values into strings.
Retained source bytes and unknown constructs belong in a separate import receipt
or an explicitly retained classic source, not an opaque required native blob.

Production advice has a direct conversion: MILITARY/DOMESTIC become headings;
name-keyed `@@<production name>` bodies become recommendations; `@@WONDER` and
`@@DEFAULT` become the wonder/default messages. Those are F03 lookup arguments
`@<name>`, `@WONDER`, `@DEFAULT`. The selected native productionName retains the
stored key spelling needed by live matching. Diagnose collisions with reserved
classic keys on export. The native author supplies no leading `@`.

MENU group titles and rows map to P05's fixed actions. Separators consume no row;
shortcut hints/mnemonics are optional presentation and do not register handlers.
MAPMENU has a separate companion-tool action registry. Credits mode 0/1/2/3 maps
to CREDITS/MPCREDITS/FCREDITS/SCREDITS, section CREDITS, and native credit IDs
base/multiplayer/fantastic-worlds/scenarios. A leading `^` selects a heading;
other line text, blank lines, completion and caller close remain intact.

S01 still derives a classic SCENARIO resource by changing the final world's
extension to the selected language extension. A converter associates its content
with a scenario ID via scenarioFiles; a missing associated resource uses S01's
generic branch. A native external-world declaration can describe a briefing
without asserting that a matching world has been bundled.

Classic file/existence selection and label-tail fallback remain input meanings.
Retained native streams and partial parser publication are reference machinery;
prevalidation may stage a complete decoded resource under X01. Where a classic resource cannot be expressed under
the native admission contract, report the specific gap and retain the original
selection or reject conversion; do not silently repair it.

## Appendix B. Classic rule and name conversions

This profile fixes the following external identities. Internal representation
is free; these mappings are needed for classic rules, file/wire interchange and
behaviorally significant fixed-identity rules, scans and ties.

| Native collection | Classic slots |
| --- | --- |
| advances | 0…99 |
| improvements | 0…66: None 0, buildings 1…34, spaceship parts 35…37, Capitalization 38, wonders 39…66 |
| units | 0…61 |
| terrain | 0…32, base terrain 0…10 |
| governments | 0…6, F03 order |
| tribes | 0…20 in RULES/LEADERS; distinct from world civilization slots |
| commodities | 0…15 |
| orders | 1…12 |
| difficulties | 0…5 |
| diplomaticAttitudes | 0…8 |

`globalRules` corresponds to F03's COSMIC parameters by **name**, using the
existing 22-value mapping; JSON member order is immaterial. Tribe leader names,
preferred gender/color/style, plural name, adjective, three personality traits
and government title overrides map to F03's fields without changing defaults,
clamps, retained-name policies or cursor lifetimes. Duplicate title overrides
keep their last-wins behavior. New field names do not change those AI parameters.

Normalize native units explicitly: whole movement points undergo the existing
road-multiplier conversion once. For hitPoints H, the nominal classic authoring
value is H/10; F03's HP conversion publishes H. Do not feed H itself to a reader
that multiplies by ten. Thus JSON HP 30 and classic HP token 3 agree. A classic
wrapped result not representable in the stated native domain cannot be silently
rounded to a multiple of ten; reject conversion or keep the classic source.
For defenseMultiplier n/d, the corresponding F03 input is exactly 2*n/d, not a
floating approximation. A unit's row cost remains a row cost; no tenfold shield
conversion is implicit in JSON. Other fields retain F03's documented conversions,
reference fixup and enabled-before-fixup behavior exactly once.

Domain, role, epoch and category names map to their zero-based codes in the
order listed in §4; preferred leader gender maps male/female to 0/1.
The fifteen named unit abilities in §4 map to F03 positions 0…14 in their listed
order; position 15 is retained separately as Appendix A specifies. Conditions
None/Never, signed consumer views and wonder obsolescence stay distinct.

C01 resolves civilizations.pluralName with the same comparison as a CITY section
name and cityNames in list order. The compatibility result for a cursor wrapping
255→0 is `@` followed by that **stored matching pluralName**, then the ordinary
publication truncation, because the original publishes its just-found header.
Calculate that result directly; no virtual stream is needed.
Missing versus exhausted lists, retries, cursor effects and failure outcomes
still follow C01. Classic @STOP and F03 line trimming/254-byte reads are input
syntax/admission details. Native city-name strings must be nonempty single lines
representable under this profile; preserve the existing 254-byte authoring limit
and reject values that would become empty or an @STOP terminator on classic
export. Never introduce uniqueness filtering or change CityTaken name matching.

`humanCityDefensePriority: deprioritize` sets F03's NoDefend state; unchanged
retains it, including when a classic NODEFEND section is absent. Build preferences
map to AUTOBUILD's ordered improvement identities 1…38, keeping duplicates,
first-name-match compatibility and the 32-entry limit. These are updates, not a
new snapshot format with implicit reset-to-false behavior.

Native event action names map to S02 in its fixed execution order:
playSound→PlayWaveFile, playMusic→PlayCDTrack, createUnit→CreateUnit,
moveUnit→MoveUnit, changeTerrain→ChangeTerrain, makeAggression→MakeAggression,
changeTreasury→ChangeMoney, destroyCivilization→DestroyACivilization,
grantTechnology→GiveTechnology, showText→Text,
suppressWonderMovies→DontPlayWonders. Sound resource IDs resolve to declared
paths at the boundary. A music track is a logical media selection; original
export uses its track number, but the new engine need not own a CD device.
Once maps to JustOnce; it does not move S02's late Consumed publication.
CivilizationSlot is the logical world identity, not a tribe index or byte offset.
Anybody/trigger-attacker/trigger-defender map to S02 codes −2/−3/−4; direct
identities remain 0…7 and retain each consumer's admission/resolution rules.

## Appendix C. Council result and control-flow mapping

Era IDs early/middle/late map to P09's existing variants 0/1/2. The following
opinion IDs name P09's existing results; their conditions and priority remain
entirely in P09. Science result 6 is reserved by the ordinary chooser but remains
addressable for supplied content/cheat preview. An ID is not a new predicate.

| Advisor | Opinion IDs in classic result order, starting at 1 |
| --- | --- |
| military | unit-shortage, land-capability-lag, naval-capability-lag, no-barracks, undefended-capital, ready, peaceful-power |
| science | science-lag, low-science-rate, research-building-shortage, trade-shortage, competitive-research, reserved, science-lead |
| economic | treasury-deficit, economic-technology-needed, economic-building-shortage, trade-technology-needed, trade-capacity-shortage, low-surplus, healthy-surplus |
| foreign | no-contact, alliance-opportunity, diplomatic-technology-needed, poor-relations, intelligence-needed, seek-exchanges, relations-established |
| domestic | democratic-disorder, luxury-spending-needed, unhappiness, balanced-without-luxuries, content, celebrating |

The native graph has no retained `accepted` flag. To convert P09's classic
chapter grammar, track parser continuation and acceptance **during conversion**.
Emit response edges for acceptance/rejection and resolve each later `#` against
that path's acceptance. Duplicate a continuation when its outcomes differ by
incoming acceptance. A greater R branch leaves acceptance unchanged; the other
branch clears it. A later accepted-guard can therefore terminate even after a
successful random gate. Do not turn R into unconditional acceptance.

For native probability n/d, require exact k=10*n/d in 0…10. Use the F01 inclusive
1…10 choice and select success when draw>10−k. A classic R threshold t converts
to k=clamp(10−t,0,10), evaluated mathematically without an incidental conversion
overflow. Preserve decision identity, dependencies and live opinion tests. Chance
steps explicitly select even when probability is 0 or 1; X01's equivalence rule
still governs any implementation optimization. Greater/equal boundary behavior
and conditional reuse must not change accidentally.

Classic response-category tests become the native advisor/opinion tests; its
seen-category set becomes the heard-response set, excluding the initial speaker.
A selected chapter and a skipped caption are different: later `*` captions become
null while still counting as a chapter. A leading `*` on the initial caption is
literal. `;` and end resolve to next-line continuation and end; same-line `#`/R
continuations become explicit edges. Reject unrepresentable malformed retained-
category cases under the declared safe profile rather than inventing a category.
Native graph object order is immaterial, and no parser cursor is runtime state.
P09 defines successful classic interpretation. EOF or failed decoding may reject
an unpublished resource safely without publishing the original partial parser.
