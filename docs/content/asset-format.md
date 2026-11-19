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
without casting file memory to runtime records.

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

The M2 reader exposes owned binding IDs, logical and physical sizes, palette
values and read-only PNG/proxy streams. Atlas bytes are capped at 256 MiB, each
encoded image at 32 MiB and each logical or decoded image at 5 million pixels.
All entries in the pinned release use non-interlaced RGBA8 PNG. The managed
reader uses .NET's zlib and validates PNG framing, chunk CRCs, scanlines and
filters before handing straight-alpha pixels to the frontend. It inflates
into one bounded buffer with one filter byte per row, validates Adler-32,
and compacts the reconstructed rows in place. `Pixels` and `CopyPixels`
expose the contiguous RGBA data. Other PNG
profiles and the classic sheet codecs remain outside this slice.
The frontend creates bounded Skia images for the selected map bindings.
The game and query libraries have no Skia or Avalonia dependencies.

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

The configured map host checks the license and all twelve exact face pins before
registering the three families in Avalonia. The map labels use the same pinned
Sans regular source through Skia. Map measurement and drawing share a run
layout that tries the pinned Serif and Sans Mono faces for missing glyphs and
uses the pinned replacement character when none supplies the glyph. Avalonia
and Skia own their runtime glyph caches independently of the source bytes.
Asset-free shell launches retain packaged Inter under its SIL OFL license.

The asset release still contains `fonts.bin` (`C2FONTS2`) and `fonts_aa.bin`
(`C2FONTAA`). They are opaque legacy package members, **excluded from the new
engine's runtime selection and future distribution**. Package inspection checks
their inventory hashes when validating the original eight-file release, but
neither parses nor renders their contents. There is no bitmap-font-schema
implementation task. The source-selection check does not require these caches.

The stack-order confirmation uses the configured Avalonia text family. Broader
dialog roles, supported text coverage, fallback, accessibility and shaping remain
M2/P04 work.
Keep game decisions, classic UI admission inputs, file fields and network outcomes
independent of the new raster metrics. Do not promise original pixel/metric parity.

To inspect an extracted upstream TTF archive separately:

```sh
python3 tools/check_assets.py --fonts /path/to/dejavu-fonts-ttf-2.37
```

## Map image bindings

[map-assets.md](map-assets.md) and [map-asset-bindings.json](map-asset-bindings.json)
define the stock map theme independently of the container: exact keys and image
hashes, semantic selector families, logical registration, annotation anchors and
faction colors. All 368 referenced PNG bindings are present in this pinned pack.
`tools/check_assets.py` validates the catalog metadata;
`tools/check_map_assets.py --pack /path/to/data/atlas-hd.pack` additionally checks
the image records and registration metadata against explicitly selected bytes.
Catalog coverage is not renderer feature acceptance.

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

## Runtime integration

Validate an explicitly acquired asset package from the repository root:

```sh
python3 tools/check_assets.py /path/to/extracted/runtime
```

Without a directory, the tool validates repository metadata only. Verify the
pinned archive checksum before extraction. The desktop host accepts explicit
asset and font roots, checks the manifest against the compiled pins, validates
all eight sizes and hashes, and retains the atlas through CPU sprite decoding.
Native opens use the contained regular-file adapter. Browser packages still need
to stage or asynchronously fetch selected immutable data under the same manifest.
Headless builds require no graphics checkout, installed fonts, display or rasterizer.
See [desktop launch and validation](../BUILDING.md#avalonia-desktop).
