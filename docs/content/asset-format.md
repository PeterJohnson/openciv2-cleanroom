# Runtime asset data contract

This records the observed **2026.11.07.1** runtime release and the selected data
at source revision `c4161c1843e8e06a5cf864e1633b6f6221abb797`. Evidence is the
asset repository's runtime guide, selected JSON data, the standalone asset
producer's container-writing boundary, and inspection of the published bytes.
No old-game renderer, resolver, font probe or reconstruction was consulted.

## Package boundary

The extracted root contains UTF-8 `package.json` with `schema: 1`,
`kind: openciv2-assets`, a string `version`, and a `files` object keyed by relative
POSIX path. Each file has integer `size` and lowercase hexadecimal `sha256`.
The exact eight-file inventory and byte identities are in [assets.json](assets.json).
The archive's independent SHA-256 in the dependency lock authenticates which
release we selected; a manifest's self-reported hashes establish only integrity.

Reject absolute paths, drive names, backslashes, empty/dot/parent components,
paths/symlinks escaping the selected root, duplicate entries, missing files,
and size/hash mismatches. A future ZIP importer must additionally reject duplicate
archive names, symlinks, oversized expansion and partial extraction; publish a
cache directory only after validation. M0 takes an already extracted directory
and contains no ZIP importer, downloader, auto-discovery or asset-code execution.

## C2ATLAS1

All index integers are unsigned little-endian 32-bit values, decoded explicitly
without casting file memory to C++ structs.

| Offset | Bytes | Meaning |
| --- | --- | --- |
| 0 | 8 | ASCII `C2ATLAS1` |
| 8 | 4 | Entry count `N` |
| 12 | 768 | 256 RGB palette triples used by the indexed proxy data |
| 780 | `180*N` | Ordered index records |
| `780+180*N` | remaining | PNG and optional proxy payloads; identical blobs may share offsets |

Each 180-byte record contains a UTF-8 binding key terminated and padded by zero
within 160 bytes, then five integers: logical width, logical height, absolute PNG
offset, PNG byte length, and absolute indexed-proxy offset. Zero proxy offset
means absent. Present proxy data has `width*height` palette-index bytes in row
order. PNG pixels retain their own physical resolution and alpha; logical sizes
are drawing units and need not equal PNG dimensions. Keys are sorted and unique.
Keys containing names like `resource/cv.dll/...` identify replacement bindings;
they are not permission to open those original files or paths on disk.

Before allocation/read, validate count/index arithmetic, nonzero dimensions,
UTF-8/termination, uniqueness, PNG ranges/signatures and proxy ranges against the
file size using checked arithmetic. Allow intentional payload deduplication.
M0's inspector has a 1 GiB file and 100,000-entry inspection budget, reads only the
index and PNG headers, and verifies manifest hashes. These limits describe this
inspector, not the accepted game-content domain. It does not decode PNG streams,
check every PNG chunk/CRC, validate proxy semantics or allocate textures.

M2 should expose a portable read-only catalog of semantic IDs, logical/physical
size, palette and encoded image bytes. Select and pin an independent PNG decoder,
apply decoded-pixel budgets before allocation, then pass decoded pixels to SDL
texture ownership in the frontend. The game and queries receive no SDL objects.
Test the parser using small owned valid and malformed containers; validate the
real package separately from graphics-independent tests.

## TrueType fonts and runtime rasterization

The game uses **DejaVu 2.37 TrueType fonts**, acquired from the independent
[upstream release](https://github.com/dejavu-fonts/dejavu-fonts/releases/tag/version_2_37)
and pinned in `dependencies.json`. [fonts.json](fonts.json) records all twelve
regular/bold/italic/bold-italic faces and their byte hashes, plus the license hash.
The archive SHA-256 is
`fa9ca4d13871dd122f61258a80d01751d603b4d3ee14095d65453b4e846e17d7`.

| Requested classic face | Replacement family | Italic face name |
| --- | --- | --- |
| Times New Roman | DejaVu Serif | Italic |
| Arial / System | DejaVu Sans | Oblique |
| Courier | DejaVu Sans Mono | Oblique |

These are the families identified by the asset repository's `THIRD-PARTY.txt`.
Its historical cache-generation records do **not** identify exact source font
versions or hashes. Version 2.37 is our reproducible content selection, not a claim
that these exact bytes produced the historical caches. Noto fonts in the asset
checkout belong to authoring/review and are a different font source.

`cmake/Fonts.cmake` acquires the TTF archive only for GUI builds, under the existing
explicit-download/offline-override policy. It checks each face and license hash,
then embeds the unmodified TTF bytes in `oc2_font_data`. This is data packaging,
not build-time rasterization. The twelve faces total 5,349,360 bytes before
executable/linker or web transfer compression. No installed OS font, Wine, bitmap
font parser, Python generator or graphics checkout is required. The complete
upstream font notice is appended to the build's `THIRD_PARTY_NOTICES.txt`.

The frontend registers these process-lifetime bytes with Dear ImGui 1.93.0's
bundled `stb_truetype` rasterizer; ImGui does not own/free the embedded data.
DejaVu Sans regular is the drawn UI default. Size/density-specific glyph atlases
are generated on demand at runtime and uploaded through the SDL renderer backend.
Widgets and custom ImGui draw lists use the same fonts, measurements and caches.
Reuse glyphs across frames; DPI/zoom changes request the appropriate raster size,
and renderer/context teardown destroys only the derived caches/textures. Native
macOS/Windows menu bars retain platform typography.

The asset release still contains `fonts.bin` (`C2FONTS2`) and `fonts_aa.bin`
(`C2FONTAA`). They are opaque legacy package members, **excluded from the new
engine's runtime selection and future distribution**. Package inspection checks
their inventory hashes when validating the original eight-file release, but
neither parses nor renders their contents. There is no bitmap-font-schema
implementation task. The source-selection check does not require these caches.

Only the pinned stock fonts are loaded today. Font-role selection in future game
dialogs, supported text coverage, fallback, accessibility and shaping are separate
M2/P04 work; having a glyph does not establish complex-script/bidirectional layout.
Keep game decisions, classic UI admission inputs, file fields and network outcomes
independent of the new raster metrics. Do not promise original pixel/metric parity.

To inspect an extracted upstream TTF archive separately:

```sh
python3 tools/check_assets.py --fonts /path/to/dejavu-fonts-ttf-2.37
```

## Layout and identity data

`cityview-rects.json` contains exactly 62 ordered nonnegative integer rectangles
in the inspected release. Preserve entries and empty rectangles; do not sort or
compact them. The source selection's 11 JSON files include unit/city anchors,
interface/report layout data, panorama sizes/placement hints and spaceship slots.
Their exact hashes are inventoried; the source catalog/review coverage is not
proof that every gameplay resource has an implementation binding.

The package's generated `hd_layouts.h` holds three constant data tables: city art
(binding key, replacement art ID, relative width, priority, waterfront/wall flags),
city sites (land/water rectangles, gate point, perimeter points), and ship slots
(kind/index, rectangle, art ID). It is data output, not an ABI or implementation
dependency. The cleanroom build does not include or execute it. M2 should obtain
versioned JSON equivalents from the asset publisher or transcribe reviewed values
into owned typed tables with provenance, explicit dimensions/order and tests.
Do not pull layout behavior from the old renderer or treat embedded source-path
strings in asset metadata as implementation references.

P03/F03 still own resource lookup and scenario override precedence. P04/P06–P09
own semantic interaction and information. Replacement layout hints do not replace
those contracts. Future sound/media additions need new manifest/schema entries,
codec contracts and notices; they must preserve P10 completion/cancellation effects.

## CMake and runtime integration

`OC2_ASSETS_DIR` is now a CMake cache path for an **explicit** approved source
root or extracted package. For a GUI build, an invalid path fails configuration;
a recognized directory enables the explicit `asset-check` target, requiring
Python 3.9+ only for that opt-in development check. Example:

```sh
cmake --preset desktop -DOC2_ASSETS_DIR="$HOME/project/openciv2-assets"
cmake --build --preset desktop --target asset-check
python3 tools/check_assets.py /path/to/extracted/runtime
```

Supply the ordinary offline library overrides from the build guide as needed.
A source root is compared to the inspected selection/metadata baseline. An
extracted package is checked structurally and against its own inventory; use the
pinned archive checksum before extraction for release identity. `python3
tools/check_assets.py` without a directory validates repository metadata alone.

This setting does not yet load, copy, preload or install assets. M2 will consume
the validated data through a new reader, with immutable package receipts and a
separate explicit scenario-content path. WASM packaging will stage the selected
data into its virtual filesystem or fetch it asynchronously under the same
manifest. There is no baked-in developer home path. Headless builds ignore
`OC2_ASSETS_DIR` entirely, even when it names a missing path, and never require
an asset checkout, Python, SDL or ImGui.
