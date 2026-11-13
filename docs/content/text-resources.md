# Classic text-resource inventory

This inventory covers the **31 top-level `.TXT` files actually inspected** in
one supplied MGE resource collection, and the additional optional/dynamic inputs
established by the repository specifications. It classifies content; it is not
proof that every shipped edition, language or third-party scenario has been
examined. The observed collection may include edited files. Exact relative
names, sizes and hashes are in [text-resources.json](text-resources.json); no
original text payloads or machine-local paths are distributed here.

The [JSON content contract](data-format.md) now provides a native representation
for every **established required game-runtime text resource family** below.
Formats and authored examples are specified; runtime adapters, the complete
interaction/action registry, full native catalogs and original-game validation
remain implementation work. No file is marked implemented by this inventory.

## Dispositions

- **Runtime core:** rules, naming and normal game UI. The native base supplies
  their semantic equivalents; original files remain optional user-selected inputs.
- **Runtime conditional:** needed when its owning feature/resource is selected
  (tutorial, help, editor, Civilopedia, council, credits or scenario). It cannot
  be discarded merely because ordinary turns do not read it. Full game support
  includes these operations; a scenario briefing may be absent with S01's fallback.
- **Runtime optional:** configuration whose absence has an owner-defined result.
- **Documentation only:** explanatory text, never implicitly parsed as game data.
- **Companion tool:** a distinct executable's resource, not evidence of a missing
  in-game rules file. Its text can be represented; tool commands need their own
  implementation/registration before claiming that tool's compatibility.
- **Legacy unconfirmed:** content is identified, but this profile has no established
  consumer. Keep the uncertainty explicit. Do not promote it to a dependency or
  claim it is unused in all historical products.

Native component names below are manifest keys. A `text` entry uses semantic
labels/messages/named help/facts/advice; classic source addresses are recorded
in the separate compatibility descriptor. The native format does not copy the
original section structure. `documents + text` means an explicit briefing
association plus a registered message. The same content can have replacement
prose in a standalone pack, while selected imported resources retain their text.

## Observed files

| File | Disposition | Native component | Behavior / evidence owner |
| --- | --- | --- | --- |
| `ADVICE.TXT` | runtime conditional | `text` | Advisor prompt fragments and live name-keyed recommendations; preserve header/body composition, @DEFAULT and @WONDER. Owner: P06; A02; A08. |
| `ALEXANDR.TXT` | runtime conditional | `documents + text` | Associated SCENARIO briefing; the text is present, but a matching world is not established by this inventory. Owner: S01. |
| `CITY.TXT` | runtime core | `cityNames` | Ordered tribe-name lists and EXTRA overflow; live plural-name lookup and naming cursors. Owner: C01. |
| `CIV2FANW.TXT` | documentation only | `None required` | Fantastic Worlds installation/player notes, not event definitions or rules. |
| `CIV2FAQ.TXT` | documentation only | `None required` | Troubleshooting FAQ. |
| `COUNCIL0.TXT` | runtime conditional | `council` | Conditional chapters/captions for variant 0; map all opinion entries, including same-line continuations. Owner: P09. |
| `COUNCIL1.TXT` | runtime conditional | `council` | Conditional chapters/captions for variant 1. Owner: P09. |
| `COUNCIL2.TXT` | runtime conditional | `council` | Conditional chapters/captions for variant 2. Owner: P09. |
| `CREDITS.TXT` | runtime conditional | `documents` | CREDITS section, About mode 0 / native base. Owner: P10; P05. |
| `DEBUG.TXT` | runtime conditional | `text` | Cheat/editor/diagnostic prompts and raw ordered tables such as EDITCOSMIC. The filename does not make it development-only. Owner: P04; S03; S04. |
| `DESCRIBE.TXT` | runtime conditional | `encyclopedia` | Rule-to-description indices and concept entries; preserve absent descriptions, identities and owner sorting. Owner: P08. |
| `FCREDITS.TXT` | runtime conditional | `documents` | CREDITS section, About mode 2 / native fantastic-worlds. Owner: P10; P05. |
| `GAME.TXT` | runtime core | `text` | Named game/network/dialog resources and live substitutions, choices, controls and ordered fragments. Owner: F03; P04 and callers. |
| `HELP.TXT` | runtime conditional | `text` | Editor/rule/event help topics; help is runtime information, not a README. Owner: P04; S03; S04. |
| `HERALD.TXT` | runtime conditional | `text` | HERALDWARNING prompt and enable/disable choices. New UI may replace obsolete hardware wording under X01; selected authored content still has a mapping. Owner: P04; P10. |
| `LABELS.TXT` | runtime core | `text` | Indexed LABELS and POPUPS; native bindings retain identities. STRINGHEAP is classic allocation metadata, not required native prose. Owner: P04. |
| `LEADERS.TXT` | legacy unconfirmed | `None required` | Separate unsectioned leader table. No runtime consumer established here; F03 reads RULES/LEADERS. Do not treat this as an automatic override or assume identical data. Owner: F03. |
| `MAPMENU.TXT` | companion tool | `menus` | Menu groups for the separate map-editor executable. Native tree covers labels/grouping; its distinct command registry is additional tool work. Owner: P05 format boundary; companion actions unregistered. |
| `MENU.TXT` | runtime core | `menus` | Main menu titles/item labels; fixed row-to-action map, no resource-defined shortcut handlers. Owner: P05. |
| `MISC.TXT` | documentation only | `None required` | Prose notes about changes from the first game; not a rules table. |
| `MPCREDITS.TXT` | runtime conditional | `documents` | CREDITS section, About mode 1 / native multiplayer. Owner: P10; P05. |
| `PATCH.TXT` | documentation only | `None required` | Patch notes, including sample CITYPREF syntax. Samples do not become active preferences. |
| `PEDIA.TXT` | runtime conditional | `text` | Civilopedia prompt/choice templates and ordered fact lists. DESCRIBE supplies topic descriptions; the two files are not interchangeable. Owner: P08; P04. |
| `README.TXT` | documentation only | `None required` | Installation/release instructions, including example configuration sections. |
| `READMEV13.TXT` | documentation only | `None required` | Multiplayer patch history and release instructions. |
| `ROME.TXT` | runtime conditional | `documents + text` | SCENARIO briefing associated with the final selected ROME world. Owner: S01. |
| `RULES.TXT` | runtime core | `rules` | All rule tables, including units, advances and leaders. No separate UNITS.TXT or ADVANCES.TXT input is established for this profile. Owner: F03. |
| `SC.TXT` | legacy unconfirmed | `None required` | Unsectioned credits-like text. No reader established here; P10 mode 3 explicitly selects SCREDITS/CREDITS, not SC. Retain as ancillary material, not silently relabel as runtime credits. Owner: P10. |
| `SCREDITS.TXT` | runtime conditional | `documents` | CREDITS section, About mode 3 / native scenarios. Owner: P10; P05. |
| `TUTORIAL.TXT` | runtime conditional | `text` | Tutorial messages and live parameters; activation/one-time state belongs to the caller, not to the catalog. Owner: P05; P04 and tutorial callers. |
| `WWII.TXT` | runtime conditional | `documents + text` | SCENARIO briefing associated with the final selected WWII world. Owner: S01. |

`LEADERS.TXT` may be useful for an explicit authoring import into `rules.tribes`,
and `SC.TXT` for an explicit ancillary credits import, but neither is automatically
loaded or used to replace the documented resource. They are **not** classified
as documentation-only merely because no consumer has been established.

## Optional inputs, variants and scenario folders

| Input family | Selection and native destination |
| --- | --- |
| `CITYPREF.<language>` | F03's optional NODEFEND/AUTOBUILD input → `cityPreferences`. Missing input retains/clears only the fields F03 specifies. It is absent from the observed 31 files. |
| `EVENTS.<language>` / caller-supplied event filename | S02 BEGINEVENTS resource → `events`. Loaded only through the specified scenario/event operation; event definitions are distinct from consumed/bound runtime state in a save. |
| `<final-world-stem>.<language>` | S01 SCENARIO section → `documents.briefings` and `text.messages`. Includes other scenario names and F06-selected alternatives; no hard-coded ROME/WWII-only list. |
| `.TXT`, `.FRE`, `.GER`, `.ITA`, `.SPA` | P04 languages 1…5; omitted-extension resolution is F03's. Native locale keys select complete localized catalogs. Non-English files were not present in the inspected collection; their encodings/acceptance need separate evidence. |
| Scenario-local copies of any selected runtime resource | Same destination as its base resource, retaining the owner's search, fallback, append and failure rules. A directory-wide merge is not implied. |
| Explicitly selected staged resources (`Events1.txt`, `Rules2.txt`, etc.) | Treat as inputs only when the caller/user actually selects or installs them at the expected resource path. Numbered files and batch scripts do not themselves define automatic runtime switching. |

Scenario packages may also contain README, player notes, design notes and unused
alternative resources. Classify each selected package by **consumer and content**,
not extension alone: a README containing sample `@AUTOBUILD` is documentation,
whereas a same-stem SCENARIO section is a runtime briefing. Do not discard an
unknown file on import; report it as unclassified/ancillary until its role is
established. Retaining an ancillary file does not execute it. This inventory is
not a promise to run arbitrary scenario installation scripts.

P09's `anarchy ` stem is a media selection; its normal chapter lookup is skipped
in anarchy. No separate required ANARCHY text file is established. The native
council's anarchy presentation can have authored captions without fabricating a
classic text lookup. Earlier `.PDE`/localized description fragments mentioned in
DESCRIBE's comments are historical source material, not additional required MGE
files under P08's documented loader.

## Other text-shaped and binary inputs

`CIV.INI` is platform/startup configuration, not a content catalog. F03 explicitly
places it outside the game-preference reader; new engine platform settings can
use a different store. Relevant language/content choices must select the same
logical resources, but standalone play does not require an original INI. This
work does not prescribe a new platform-settings schema or claim universal INI
key import. Logs, installer files, batch scripts and registry patches are also
outside the content-pack inputs.

`CIV2.DAT`, `HALLFAME.DAT`, saves/scenarios/maps and media are binary inputs with
separate F06/P03/P10 contracts. They are not missing JSON text-resource families.
User settings can start from defaults, and a new high-score file can start empty.
A scenario can still require its selected world, rules, event and media data.

## Required mapping and validation work

The native formats cover these distinct obligations:

| Obligation | Native representation | Acceptance evidence still required |
| --- | --- | --- |
| Game/dialog/help/tutorial text and numeric/string substitutions | `text.messages`, typed parameters, input fields/defaults; adapter source bindings | Register actual operation inputs/results; preserve Append/PreserveBlank, edits on cancellation, dynamic choices and caller effects. |
| Indexed labels and raw ordered text | `text.labels`, `text.helpTopics`, `text.facts`; adapter index mappings | LABELS/POPUPS identity and classic tail fallback; DEBUG/EDITCOSMIC's 22 descriptions; all PEDIA fact tables in consumer order. |
| Name-based advisor content | `text.productionAdvice` headings, name-based recommendations and fallback messages | Live name resolution, `@WONDER` and `@DEFAULT`, header/body composition; unchanged advisor decisions. |
| Main menus | `menus` with registered actions and catalog labels | Fixed MENU row/action mapping, live label/state updates and command dispatch; native placement/shortcuts are free. |
| Civilopedia descriptions and concepts | `encyclopedia.topics`; named messages/facts in `text` | Rule identity, description absence, concept links, sorting/ties, knowledge restrictions and explicit scenario DESCRIBE/PEDIA presence gates. |
| Council content | `council` named era/opinion dialogue and chapter captions | All variants/opinions, accepted-state retention, same-line branches, seen categories and response limit; optional media does not erase captions. |
| Scenario briefing | `documents.briefings` scenario ID → registered message | Final alternative-file association, localized override/fallback, start/end values, sprite gate and reselect result. |
| Credits | Four fixed `documents.credits` identities | Ordered authored lines/emphasis, mode selection, completion/cancel and caller polling/close. |

For a standalone release, enumerate reachable resource lookups from registered
operations and require each claimed locale to satisfy them. A filename inventory
or a catalog with a single example per type does not prove completeness. Mappings
with no executable consumer remain metadata; missing required semantic content
fails native validation. Exercise equivalent independently authored classic/JSON resources,
then original files/scenarios and mixed-peer sessions. Record diagnostics for
unsupported data and keep failures distinct from absent-resource fallbacks.

The `stock-content` gap in the [coverage ledger](../conformance/coverage.json)
tracks this implementation and content work. Separate companion-tool support and
legacy-consumer uncertainty remain explicit; they must not block classifying the
established core resources, or be quietly claimed as completed compatibility.
