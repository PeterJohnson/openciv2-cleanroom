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
The shell embeds the verified TTF data and uses ImGui's runtime rasterizer, with
no graphics-pack dependency. See the [font contract](asset-format.md#truetype-fonts-and-runtime-rasterization).
The build appends the full upstream font notice to its software notices. Font
hashes are checked even for explicit local source overrides.

## Stock rules, text and other inputs

| Input | Current inventory and distribution policy | Follow-up |
| --- | --- | --- |
| Default rules and leader/tribe names | No stock rule file is bundled. F03 specifies `RULES.<language>` syntax and table semantics, not an approved redistribution package. | M1: author bounded parser fixtures; M3–M5: supply licensed playable defaults with provenance. |
| UI messages and labels | The shell's own short labels are authored in this repo. Classic `GAME` and `LABELS` resources and localized variants are absent. F03/P04 own lookup/substitution behavior. | M2–M5: author stock text and preserve explicit scenario/language overrides. |
| Civilopedia and descriptive text | No stock `DESCRIBE`/`PEDIA` content is bundled. P08 defines sections and consumers. | M5: author licensed replacements; support user-selected scenario text. |
| Scenarios, events, rules overrides, city preferences | No redistributable scenario corpus or `CITYPREF.TXT` is bundled. S01–S04/F03 specify contracts. | M1/M5: authored minimal fixtures, positive custom-content cases and explicit unsupported-input diagnostics. |
| Sound, music and movies | No runtime sound/media set is present in the approved graphics package. | M2/M5: extend the asset data contract and notices when authored audio arrives; preserve P10 completion/skip semantics with media disabled. |
| Classic saves/maps/scenarios and imported graphics/text | User inputs, not a source of automatically redistributable stock content. Never scan unrelated directories or silently replace custom content. | M1 onward: explicit import paths, bounded parsers, provenance/hash receipts and original interchange tests. |
| Three X02 F01 vectors | Authored specification-derived JSON in `docs/algorithms/conformance`; hashes/provenance in the registry. No original execution claimed. | M1: execute through the new runner. |
| Shell and metadata test data | Authored Catch2 cases and temporary synthetic metadata/pack containers in `tests/`. No original assets or full font payloads. | Extend as contracts arrive; synthetic container acceptance is not glyph/PNG decoding evidence. |
| Original executable and installation resources | External comparison target only; absent from this checkout and release packages. | `original-access` below. |

These gaps are tracked as `stock-content`, `runtime-font-platforms`, and
`asset-runtime-release` in the [coverage ledger](../conformance/coverage.json).
They do not prevent M0's content-independent test/build skeleton.

## External original-game comparisons

Provision a separately licensed Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3** installation on an external Windows machine or a documented
compatibility environment. Do not acquire an executable from reconstruction
repositories, package it with this project, or link it into any target. Access is
not available in this checkout; the ledger's `original-access` gap stays open.

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
