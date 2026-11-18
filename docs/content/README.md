# Content inventory and acquisition

This M0 inventory separates approved replacement data, cleanroom-authored tests,
user-provided classic content and an external comparison installation. None is
required to build or run headless shell tests.

## Approved graphics input

[assets.json](assets.json) records the inspected asset checkout
`c4161c1843e8e06a5cf864e1633b6f6221abb797`, its explicit selection and metadata
hashes. The 840 selected inputs total 277,941,567 bytes: 829 PNGs and 11 JSON data
files. The selection fingerprint is SHA-256 of sorted UTF-8 lines
`<file-sha256>  <decimal-byte-count>  <relative-selection-name>\n`.
Only paths in `runtime-inputs.json` contribute; neighboring art is not selected.

The independently inspected published
[runtime release 2026.11.07.1](https://github.com/PeterJohnson/openciv2-assets/releases/tag/v2026.11.07.1)
is pinned by URL and archive SHA-256 in [dependencies.json](../../dependencies.json).
Its archive hash is `13d370f9e94371112992ef6e40a3731eac3cb0f302dc9a3143c95ad88704a253`,
matching the publisher's checksum. The eight payload hashes are retained in
`assets.json`; the ZIP has those files plus `package.json`. The source selection
and release identities are separate observations, not a claim that we reproduced
the publisher's build. No downloaded artwork is committed here.

| Runtime file | Contract/use |
| --- | --- |
| `data/atlas-hd.pack` | C2ATLAS1 index, PNG payloads and optional indexed proxies; 2,105 entries, 249,792,878 bytes |
| `data/fonts.bin` | Legacy C2FONTS2 cache; inventoried upstream, excluded from new-engine runtime/distribution |
| `data/fonts_aa.bin` | Legacy C2FONTAA cache; inventoried upstream, excluded from new-engine runtime/distribution |
| `data/cityview-rects.json` | 62 ordered `[x,y,width,height]` integer rectangles; zero rectangles remain significant |
| `include/hd_layouts.h` | Generated layout tables; treat as versioned data, never include executable code from an untrusted package |
| `data/ATLAS-PROVENANCE.md` | Replacement artwork provenance and limits |
| `LICENSE`, `THIRD-PARTY.txt` | Asset and font notices to retain in distributions |

The asset source's MIT license and runtime notice describe the replacement art
and historical DejaVu bitmap fonts. `assets.json` schema 2 separates all upstream
`packageFiles` from `selectedRuntimeFiles` and `ignoredRuntimeFiles`; only the six
selected graphics/metadata/notice files are eligible for new-engine packaging. The exact historical DejaVu/Wine versions were not
recorded. Noto fonts with OFL notices are authoring inputs, excluded from this
runtime release. Review screenshots, original GIF/DLL/EXE resources, authoring
masters, and original text are not approved runtime distribution inputs merely
because they exist in an asset repository. The complete notice remains with the
package; software dependency notices are separate.

## Stock TrueType fonts

The new UI uses the DejaVu Sans, Serif and Sans Mono families identified by the
asset notice, rasterized at runtime. Their exact historical cache-source versions
are unknown. We separately pin the upstream DejaVu **2.37** TTF archive, all twelve
regular/bold/italic/bold-italic faces and their license in [fonts.json](fonts.json).
The Avalonia host currently uses packaged Inter with full font notices. The
DejaVu selection remains the graphics-content input for M2 integration; frontend
rasterization and caches use Avalonia/Skia. Headless tools require no fonts.

## Standalone content and classic compatibility

[The JSON format specification](data-format.md) defines version 1 packs for
rules, city names, production preferences, semantic messages/help/facts/advice, menus,
briefings/credits, conditional council content, encyclopedia topics and events.
The [per-file text inventory](text-resources.md) maps all 31 observed TXT files
and specification-established optional/variant inputs to runtime formats or an
explicit documentation/ancillary disposition. Its [machine-readable ledger](text-resources.json)
records observed filenames and hashes without distributing original payloads.
Native catalogs describe game concepts; the separate classic compatibility
descriptor carries source addresses and slot mappings.
The [owned examples](examples/README.md) illustrate fields and references but deliberately lack complete playable tables
and interaction coverage. [JSON Schemas and a developer checker](schemas/README.md)
validate document structure. Runtime pack resolution, typed gameplay and native
rule admission are implemented in the [content runtime](../m1-content-runtime.md).
[City-production preferences](../m1-classic-text.md) now have native validation
and application plus an incremental classic reader.
The three M1 classic-to-native converters are enumerated by
[M1-02](../m1-closeout.md#m1-02-content-and-conversion-matrix). Complete semantic
pack validation, reverse/full-resource conversion and a distributable base pack
remain M2–M5 work. The provenance inventory below does not expand M1's closed scope.

Standalone play must work without original text files. Users may select original
or scenario text through compatibility adapters that retain the owning algorithm's
parsing, fallback, conversions and meaningful partial effects. Both paths feed
shared gameplay models; JSON does not change AI/map generation or classic
save/wire formats. Track user-import provenance after conversion as well.

## Built-in gameplay data

The [first-wave formats](gameplay-format.md) and [shipped defaults](../../data/gameplay/classic-mge-5.4.0f/README.md)
cover calendar, spaceship, government, difficulty, combat and scoring values
previously fixed in the executable. Schemas, source pins and an offline developer
loader validate these complete component scopes. Portable C# loading now
produces immutable typed values with classic/custom profile gates; owning
gameplay consumers and session integration remain open. The [assessment](built-in-gameplay-data.md)
tracks later candidates; further component extraction is deferred until
implementation reaches the owning mechanics. Changed operands require explicit
custom mode and do not inherit the classic-default original-file/peer compatibility
claim.

## Stock rules, text and other inputs

| Input | Current inventory and distribution policy | Follow-up |
| --- | --- | --- |
| Default rules, leader/tribe and city names | Native/classic rule readers and city-name providers are implemented with fixed identity mappings and paired authored checks. No complete stock rule/name set is bundled. F03/C01 own `RULES`/`CITY` compatibility and logical effects. | M1-02: the named component conversions; M3–M5: complete comparison profiles and distributable standalone defaults with provenance. |
| UI messages, labels, help, tutorial and editor text | JSON messages/inputs/defaults, named help/facts and production advice are specified; indexed/source bindings live in the compatibility descriptor. The shell has its own short labels; no full game catalog is bundled. The per-file inventory includes GAME/LABELS/ADVICE/HELP/DEBUG/HERALD/TUTORIAL and PEDIA templates/facts. | M2–M5: register every reachable interaction/lookup and supply native catalogs; test classic scenario/language overrides and partial publication. |
| Menus, council, briefings and credits | JSON action trees, named council opinions/dialogue and scenario-ID document associations cover the established runtime families. Original text/art are not bundled. MAPMENU companion actions and unconfirmed LEADERS/SC consumers are explicitly distinguished. | M2/M5: implement action mappings, same-line council selection, final-world briefing lookup and all four credit modes; preserve caller effects. |
| Civilopedia and descriptive text | Native JSON topics are specified; no complete prose set is bundled. P08 owns classic DESCRIBE/PEDIA compatibility and semantic consumers. | M5: author replacements and implement both content paths with correct topic/rule identities. |
| Scenarios, events, rules overrides, city preferences | Native preference validation/application and classic CITYPREF loading have paired authored checks; event bodies and EVENTS conversion remain open. No playable scenario corpus is bundled. | M1-02: CITYPREF conversion only, plus existing regressions. M5: scenario/event admission, conversion and owning effects. |
| Sound, music and movies | No runtime sound/media set is present in the approved graphics package. | M2/M5: extend the asset data contract and notices when authored audio arrives; preserve P10 completion/skip semantics with media disabled. |
| Classic saves/maps/scenarios and imported graphics/text | User inputs, not a source of automatically redistributable stock content. Never scan unrelated directories or silently replace custom content. | M1-04: fixed file corpus and current-export checks; M2–M6: wider content, formats and mode interoperability. |
| X02 foundation vectors | 57 authored F01/F02/W01 fixtures with hashes/provenance in the registry. No original execution claimed. | M1-03: fixed review/execution denominator; later operation families join their owning milestones. |
| Shell and metadata test data | Authored shared cases and temporary synthetic metadata/pack containers in `tests/`. No original assets or full font payloads. | Extend as contracts arrive; synthetic container acceptance is not glyph/PNG decoding evidence. |
| Original executable and installation resources | External comparison target only; absent from this checkout and release packages. | `original-access` below. |

These gaps are tracked as `stock-content`, `runtime-font-platforms`, and
`asset-runtime-release` in the [coverage ledger](../conformance/coverage.json).
They do not prevent M0's content-independent test/build skeleton.

## External original-game comparisons

Provision a separately licensed Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3** installation on an external Windows machine or a documented
compatibility environment. Do not acquire an executable from reconstruction
repositories, package it with this project, or link it into any target. Access is
now available through the user-supplied Windows installation at
`C:\Projects\civ2\Civ2` (2026-11-13). Its `civ2.exe` matches the target hash below.
Seven original raw maps and 21 current saves were used in recorded decode/encode observations with source
preservation. Later isolated Windows runs captured original LAN discovery/ID
responses and [actual acceptance of two byte-preserving save exports](../m1-original-text-observations.md),
including original city founding and resave between them. These are bounded
checks: full live-session interchange and original-peer sessions remain open.
The [Windows handoff](../m1-windows-handoff.md) preserves the earlier Linux
stopping point; current scope is in the [M1 audit](../m1-validation.md).

An independent driver should receive an explicit installation path and a separate
scratch/output directory. Before running a case, hash the executable, record its
version/platform and hash every selected input. The existing vectors name image
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`;
verify it rather than assuming the installed edition matches. A different image
requires a separately labeled comparison profile and reviewed evidence.

Start M1 with bounded F06 save/load exchanges and N05 discovery/handshake
observations, then cover the X02 G03/G13–G18 variants in both host roles. Use
ordinary supported inputs and externally visible files, packets, screens or
interaction outcomes; do not instrument or port executable internals. For random
work, follow X02's controlled-choice/distribution policy and per-operation network
authority instead of expecting independent original sessions to share a seed.

Record setup, actions, stop boundary, observed outputs, target/input hashes and
capture provenance. Keep proprietary inputs/captures external unless separately
approved for distribution; publish small independently authored fixtures and
bounded reports where allowed. A cleanroom regression snapshot is never an
original golden. Preserve failed, partial and inconclusive runs and the smallest
remaining evidence gap. Stock CI must continue to pass without this installation.
