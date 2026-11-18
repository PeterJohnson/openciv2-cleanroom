# Stock HD map asset bindings

This is the production art-input contract for P02's
[minimum map visual feature set](../algorithms/P02-map-rendering-and-minimap.md#minimum-map-visual-feature-set).
The [semantic binding catalog](map-asset-bindings.json) lists exact atlas keys,
all selector variants, logical registration, faction colors and per-image hashes
for runtime release **2026.11.07.1**. Its pack identity is pinned by
[assets.json](assets.json); [asset-format.md](asset-format.md) defines C2ATLAS1.
No renderer source, original image sheet or source-derived harness is needed.

These mappings are exact for the stock HD theme. A future theme can replace
them through a separately declared complete binding set. Supported scenario art
retains F03/P03 override precedence; do not silently replace supplied scenario
art with these stock images. Theme choice does not change terrain/unit identities,
game rules or observer permissions.

## Image lookup and dimensions

Resolve the exact case-sensitive `atlasKey` strings in the catalog. Keys that
contain `crop/file` or `crop/resource` are **opaque keys in this pack**. Never
open that apparent file, load a DLL, or crop a reconstructed sheet at runtime.
The RGBA PNG payload is the art; indexed proxy planes are not the draw source.

`images` records the logical size, physical PNG size and SHA-256 of each encoded
PNG payload. Logical sizes define art registration units; physical sizes define
sampling resolution. Scale a logical point by the chosen map transform and
display density exactly once. Preserve the entire logical image box and its
transparent margins; automatically trimming each PNG would invalidate anchors.
Do not assume that semantic keys and legacy crop aliases have equal physical
resolution or pixels. The catalog deliberately chooses an exact key for each
role, even where aliases or differently fitted versions also exist.

All tile-relative positions below use a 64×32 logical tile, top-left `(0,0)`.
Tall city/unit/fortress/airbase art has a 64×48 box whose top-left is `(0,-16)`
relative to that tile. These are stock-art registration units, not required
screen pixels or window dimensions. Alternative projections must transform
the art, annotations, selection and picking consistently.

## Terrain, connections and features

| Meaning | Exact selection |
| --- | --- |
| Base terrain | `terrain/{t}`, classic terrain index `t=0..10` in the catalog's named terrain order. |
| Special resource | `resource/{2*t+s-1}`, resolved W01 special `s=1,2`; no image for `s=0`. Grassland production shield is independently `resource/shield`. |
| Forest/Hills/Mountains | `feature/forest/{m}`, `feature/hills/{m}`, `feature/mountains/{m}`. Four same-terrain contacts NE, SE, SW, NW contribute 1, 2, 4, 8 to index `m`. |
| River | `river/{m}` with the same four contact weights; a contact joins a permitted River or Ocean neighbor. Center River presence is separate from its connection index. |
| River mouth | `mouth/{q}`, `q=0,1,2,3` for a river neighbor NE, SE, SW, NW of an Ocean center. Place the full 64×32 mouth image over that tile. |
| Ocean shoreline | `coast/{q}/{m}`. Quadrants `q=0..3` are top, right, bottom, left. Contacts and placement are specified below. |
| Terrain edge blend | `dither/{family}/{q}`. Families 0..9 select the corresponding neighboring land terrain; family 12 selects an Ocean neighbor. `q=0..3` means NE, SE, SW, NW. No blend when center and neighbor terrain match. |
| Unknown and unexplored edge | `fog/full`; `fog/{q}` for NE, SE, SW, NW edges. `dither/11/{q}` is an available alias, not an extra layer to draw on top. |
| Road/rail | `road/0` / `rail/0` for an isolated non-city endpoint. Segment indices 1..8 follow NE, E, SE, S, SW, W, NW, N. |
| Irrigation / Farmland / Mine | `improvement/irrigation`, `improvement/farmland`, `improvement/mine`. Resolve the logical combination before selecting art; Farmland is not an additional Mine overlay. |
| Pollution / hut | `improvement/pollution`, `improvement/village`. |
| Fortress / fortified unit / empty airbase | `improvement/fortress`, `improvement/fortified`, `improvement/airbase`. |
| Occupied airbase | The permitted occupant's `factions[].occupiedAirbase` key. Its aircraft is already selectively colored; leave the runway unchanged. |

Connection indices are **asset selectors**, not a required bit-field layout for
game state. The catalog expands each selector to its named contact set.

| Coast quadrant | Contacts contributing 1, 2, 4 | Logical top-left |
| --- | --- | --- |
| top (0) | NW, N, NE | (16,0) |
| right (1) | NE, E, SE | (32,8) |
| bottom (2) | SE, S, SW | (16,16) |
| left (3) | SW, W, NW | (0,8) |

Each coast contact means land rather than Ocean in the permitted adjacency
observation. Each quadrant image is 32×16. Blend/fog quadrants are also 32×16,
placed at NE `(32,0)`, SE `(32,16)`, SW `(0,16)`, NW `(0,0)`; their ordering
differs from coast quadrants. Apply W01 wrapping before evaluating contacts.
No contact may be inferred from an undisclosed live neighbor. Edge/unknown
treatment follows P02's knowledge contract, not the atlas's ability to draw it.

For routes, a disclosed city site is a road/rail connector; an airbase encoded
with overlapping original bits is not a city site. Draw a rail arm where both
ends admit rail/city connection; draw the ordinary road arm for a road connection
that lacks that rail connection. A city does not need an isolated-route dot.
Direction sets include diagonals and axial neighbors in W01's doubled coordinates.

Under normal detail, preserve the visual dependencies: base/coast below edge
blends and connected relief, agriculture/river/resource/network overlays above
their base, tall improvements/cities/units above their supporting tile, and
banners/status above their object. A suitable depth/compositing policy can
replace legacy call order, but overlapping translucent images must remain
correct. Fog clips every affected world layer and never samples hidden art.

## Unit, city and owner selection

Ordinary supported unit rule slots 0..62 select `unit/{slot}`. The stock
barbarian-Diplomat/leader display role selects `unit/62`; this alternate art
does not create a new playable unit rule. The catalog supplies all 63 classic unit entries; the extra atlas `unit/63`
is not a classic rule or the barbarian-leader mapping.
Do not map only the six unit kinds used by an authored demonstration.

`units.shieldMask` is `shield`, a 12×20 alpha silhouette. Use its alpha for the
owner-colored shield/border treatment, dark shadow and offset secondary stack
shield; do not multiply its near-black RGB by the owner color. Unit body art
keeps its own colors. Health bands, order/veteran/automation markers and stack
counts are composed separately. The unit shield anchor is `(48,4)` within the
64×48 body box, except rule slots 32..43 use `(2,0)`. These registrations are
included per variant in the catalog, including the default for the leader appearance.

City art selects `city/{8*style+2*sizeClass+walled}`, with style 0..5,
sizeClass 0..3, and walled 0 or 1. The catalog enumerates all 48 combinations
and their population-box anchors. The flag anchor is `(48,4)` in the body box.

The stock appearance policy starts from the permitted configured city style.
Where disclosed, Industrialization selects style 4; Automobile together with
Electronics selects style 5. Class is the number of thresholds reached by the
**disclosed** size: `(4,6,8)` for styles 0..3, `(5,8,11)` for style 4, and
`(5,11,19)` for style 5. A disclosed Palace advances a class below 3 when the
city is the observer's own, or its owner is nonhuman and difficulty is below
King. City Walls or an active Great Wall supplies walled appearance where that
fact is admitted. The projection may publish the resolved appearance instead
of its source inputs. It must not disclose hidden advances or current foreign
population to let the frontend calculate it. Appearance never changes city size.

Use the catalog's `factions` record for the resolved **color role** 0..7, not
the raw player/tribe slot. Its RGB is the pinned palette value at `208+3*color`;
the following two palette entries have the same RGB in this pack. The exact
stock choices are:

| Role | Key pattern (`p=208+3*color`) | Logical size |
| --- | --- | --- |
| Bright city flag | `tint/faction/flag/{p}` | 14×22 |
| Dark city flag | `tint/crop/file/cities/1,448,14,22/{p}` | 14×22 |
| Occupied airbase | `tint/crop/file/cities/338,423,64,48/{p}` | 64×48 |
| Disorder marker | `crop/file/icons/166,223,30,30`, used as an alpha marker colored for the owner | 30×30 |

Dark flags preserve darker cloth, with unchanged pole/trim/alpha. The source-slot
0 dark-flag key is the canonical choice; other source-slot variants are redundant
for a renderer that already resolved the live color role. Do not substitute the
unrelated 8×16 `flag/{color}` image or an uncolored base airbase for these roles.
Do not tint the whole city, runway, flagpole or gold trim. Population backgrounds,
label/overview accents, shields and disorder use the same owner-color record.
Scenario-defined nonstock colors require a separately declared selective-tint
binding or equivalent owned tint data; do not clamp them to a stock slot silently.

Sentry silhouette replaces body RGB while preserving alpha; detailed greyscale
preserves body luminance detail and alpha. They are effects applied to the chosen
unit image, not extra unit identities. Fortified, stack, health and order layers
remain separately selectable. Full and dirty redraws must include their overhangs.

## Selection, effects and procedural content

| Image-backed role | Exact selection |
| --- | --- |
| Tile selection | `crop/file/icons/199,256,64,32` |
| Ordinary grid / city-radius grid | `grid`, `grid/radius` |
| Ordinary explosion frame `i=0..7` | `crop/file/icons/{1+33*i},356,32,32` |
| Nuclear frame `i=0..10` | `crop/resource/tiles.dll/gifs/85/{1+92*(i mod 6)},{1+73*floor(i/6)},91,72` |

The catalog records these frames in sequence. P10 owns their semantic event,
position, completion and cancellation; frame cadence may change. Moving units
reuse their chosen body and annotations under the same camera transform.

The atlas does **not** need separate sprites for runtime city names, population,
orders, work counters, stack counts, health bars, path/trade lines, ownership
fills, minimap marks/viewport, hover information, notifications, clipping,
color correction or focus inversion. These are explicitly **procedural/text
roles**, listed in `proceduralRoles`, using the pinned TrueType font contract.
They remain required visual capabilities. Stock health green/yellow/red and
neutral/shadow colors are recorded in `displayColors`; they are not gameplay
thresholds. No missing-art fallback may impersonate a successful image lookup.

## Validation and data provenance

`python3 tools/check_map_assets.py` checks the catalog's internal keys, families,
registrations, selector coverage and the pack/source pins. With an explicitly
selected pack, it additionally checks all referenced keys, logical/physical
sizes and encoded PNG hashes against that pack:

```sh
python3 tools/check_map_assets.py --pack /path/to/runtime/data/atlas-hd.pack
```

The catalog was transcribed from approved replacement **data** at asset revision
`c4161c1843e8e06a5cf864e1633b6f6221abb797`: the selected compatibility bindings,
unit/city anchor JSON, and the published C2ATLAS1 index, palette and PNG payloads.
The standalone asset producer was used to identify crop/tint keys and their
data meanings. It was not imported as renderer code. The catalog retains source
hashes. No original-game renderer or reconstruction code was consulted for these
bindings. Exact binding coverage proves that the art exists, not that the new
renderer selects, composes or discloses it correctly; P02's visual acceptance
and the [M2 coverage ledger](../validation/map-visual-coverage.md) cover that work.
