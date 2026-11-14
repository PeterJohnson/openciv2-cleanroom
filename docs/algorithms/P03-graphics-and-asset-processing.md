# P03: Graphics and asset processing

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in P03 |
| --- | --- |
| Gameplay | Meaningful custom asset identity and safe content availability; rendering adds no game effects. |
| Original files/content | Explicitly adopted palette/image headers, compression interpretation, sheet rectangles, transparency, overrides and exports. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Decoder implementation, surface locks, native handles, allocators and screen drawing. |

The explicit presentation contract below selects the required definitions;
its detailed original procedures remain reference. The table above adds no
requirement to reproduce an original UI or private runtime representation.

## Required presentation contract

The product target is the same game, with a broadly familiar Civilization II
feel and support for original configuration and scenario content. It is **not
an exact recreation of the original interface**. Apply [X01's presentation
policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy).

The requirements are the semantic obligations and external-data definitions
explicitly adopted below. All numbered sections and appendices under
[Original behavior reference](#original-behavior-reference) describe the old
interface. Their imperative wording, including historical “required” and
“conformance” lists, does not make their UI procedures mandatory. An adopted
formula or file definition does not adopt the surrounding prompt, callback,
storage, drawing or navigation algorithm.

Design screens, navigation, controls, prompts, formatting and rendering freely.
No equivalence proof against the original UI state machine is needed. Preserve
game rules, meaningful information, privacy, available game decisions, target
identity, accepted game/file/network effects and actual protocol deadlines.
Local draft edits are not committed game commands. Cancellation may discard
them; it cannot undo an already accepted game operation unless its owner permits
that. Keep source option identities separate from their displayed order.

Original allocation sizes, pool ordinals, shared scratch buffers, control IDs,
modal flags, retained UI state and native event traces are comparison details.
Production code must not emulate them merely to satisfy a historical snapshot.
Optional original-comparison tools/tests remain outside production dependencies.
File/wire/resource encodings are translated by production adapters, with safe
storage and documented diagnostics for unsupported or invalid input.

## Asset obligations

Load supported original graphics and scenario overrides into an owned asset
model. Preserve source colors, transparency, dimensions and semantic identities
where they encode authored content. Use any suitable independent codecs, pixel
formats, atlases, resource caches and rendering libraries. Scaling, filtering,
screen color matching, clipping primitives and palette effects are free choices.

The following are required **data definitions only**; the linked procedures are
reference implementations, not mandated decoder or provider algorithms:

| Input or output | Adopted definition | Excluded runtime machinery |
| --- | --- | --- |
| Resource lookup | §2 resource names/type identities, name sanitization and primary-before-registered resource precedence; F03 scenario/install content precedence | Drive-letter discovery, CD retry dialogs, process current directory, handle registry and live buffer rereads |
| Color files / CTAB | §3 RGB values, first/count fields, byte widths/order and payload meaning; Appendix B external asset bytes | Palette handles, generation tokens, aliased-buffer effects and ignored write failures |
| Original sheets | §5 filenames, scenario BMP then scenario GIF then installation GIF fallback, palette mapping, source rectangles, transparency/key and replacement-color meaning | Destructive loading, extraction allocation order, stale working images after failed loads |
| GIF and compact pictures | §8 admitted framing, compression and decoded pixels; compact-picture headers/palette dimensions | Dictionary allocation, native status codes and original incremental write timing |
| BMP input/export | §9 admitted BMP headers, orientation, palette/pixel encoding, sheet dimensions and exported bytes | Surface locks, row-buffer capacities and provider call order |
| Other supported images | §10 admitted Targa, PCX and LBM/PBM structures and pixel/color meaning | Native resource ownership and conversion traces |

Sheet coordinates are input-file definitions, never mandatory screen geometry.
Correct decoded content is the contract; there is no requirement to reimplement
the original LZW loop or pixel copier. Preserve original editor export formats
and test interoperability for the advertised domain. Safely diagnose failed or
unsupported loads; do not extract a previous image as if it were the new asset.
The native pack has its own [asset-format contract](../content/asset-format.md).

## Presentation freedom

New stock art, themes, fonts, resolution, animation and drawing are permitted.
Retain a recognizable strategy-map/city/unit vocabulary and meaningful scenario
customization. User-selected content locations replace original installation/CD
discovery. No missing file requires the original dialog or native failure trace.
Do not silently replace successfully supplied scenario content with stock art.

## Required verification

Test the adopted content definitions and semantic operations, including custom
content, observer restrictions, changed/deleted targets, accept/cancel, real
timeouts and remote responses where applicable. Verify that different layouts,
navigation paths, pagination, repaint counts and skipped cosmetics leave the
same accepted commands and gameplay outcomes. Compare resolved values and
identities, not UI buffers or callback traces. Qualitative play review checks
the familiar map/city/turn/advisor feel; pixel or original-navigation matching
is not an acceptance gate. Historical checks below are reference evidence,
not evidence that the new UI or a documented bug repair has been validated.

## Original behavior reference

The following record retains stable section anchors for research and existing
citations. Only the specific semantic rules and data definitions adopted above
are normative. Everything else is optional reference, including exact
user-facing sequences. Historical source links and harnesses remain provenance,
not implementation or test inputs. A missing UI design detail is a design
choice, not a specification gap; a missing game rule or external format is a gap.

### 1. Domains, state and observations

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Nominal inputs have valid initialized graphics objects, sufficient image and
pixel backing, positive supported dimensions, valid rectangles and palette
ranges, and complete supported assets. The principal sheets are indexed
640×480 images. The intended GIF profile has a global palette, one ordinary
noninterlaced image descriptor, no preceding extension and no local palette.
The dedicated sheet BMP loader accepts 640×480, 8-bit images. Other image
entry points have their own narrower rules below.

Required observations are resource identity/search precedence, decoded source
pixels and palettes, source-sheet slots/markers/transparency, file bytes and
partial effects at those boundaries. Asset loading is not transactional: failure
does not undo preceding source-palette imports or extracted assets.

Logical source state comprises dimensions, bounds/clip, indexed or RGB pixels,
sprite origin/transparency metadata, source RGB tables, resource prefixes and
ordered modules. Native surfaces, palette generations, display fades and screen
sampling caches are optional adapter state, documented in Appendix D.

Rectangles are half-open unless a primitive explicitly uses inclusive line
endpoints. Coordinates, differences, products, counters and address-independent
scalar calculations follow F01 reference results, with signed division
truncating toward zero. Equivalent sufficient-range evaluation is permitted. Stored pixel indices and RGB components use
their low 8 bits. A change to this convention is stated where needed.

[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
excludes memory overwrites, including surface, palette, dictionary, cache and
private-buffer overruns. Do not implement them as mutations of neighboring
logical objects. Stop, omit the unsafe operation or reject the input under a
declared safe profile, retaining preceding valid effects. Unknown reads need
established backing, supplied retained values or a stop at their first use.
A stricter decoder may reject unsupported files, but must identify the narrower
input profile rather than claim every accepted off-nominal result.

Native device, filesystem, resource, allocation and message services are
explicit inputs. An adapter's failure check or reconstruction exception is not
automatically an original branch. The native raster limitations in §11 remain
separate from the complete ordinary indexed algorithms.

### 2. Resource names, paths and modules

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

#### Search paths and drive discovery

Initialization requests the executable module's filename into the primary
search-prefix buffer. On a nominal complete path, remove characters after its
last backslash, retaining that backslash. The original ignores failure or
truncation of the filename query; reverse scanning unproduced or unbounded
bytes requires retained input or a safe stop. Rejecting those results is an
explicit restrictive behavior.

Drive discovery:

1. Set process error mode to suppress critical-error dialogs, capturing the
   previous mode. Request the drive bitmask, then the drive-root string list.
2. Scan drive letters A through Z, consuming one NUL-terminated list entry for
   each set mask bit. Obtain the type of that root.
3. On the first **fixed disk**, clear the shared line buffer, append the live
   root and supplied relative path, and perform an existence open. In this
   exact executable the open result is ignored. Clear the alternate prefix,
   publish the root's live first character plus colon/backslash, and stop.
4. Restore the previous error mode. Return the alternate prefix when nonempty,
   otherwise no prefix. A previous nonempty prefix can survive an exhausted
   scan.

Do not silently substitute a CD-ROM-type test or require the existence open to
succeed. Failure/oversize of the drive-list provider does not establish bytes
for subsequent reads. The independent reconstruction's early rejection of such
lists is a repair, not evidence of a different original search algorithm.

Initialization tries discovery twice. If neither succeeds, show the
CDROMNOTFOUND2 message with retry/cancel behavior. Read the shared dialog result:
zero returns failure; nonzero repeats both discovery attempts. Current prefixes
and dialog/provider callbacks remain live between attempts.

#### Resolving a file

Given a mutable input path, obtain its length twice before scanning backward
for its last backslash; capture the basename. Save error mode and suppress
critical errors. Try these candidates in order, stopping on an open result
different from both zero and the failure sentinel:

1. Primary prefix plus captured basename. On success rebuild the output from
   the **live** primary prefix plus that captured basename.
2. Primary prefix plus the original path's suffix after its **first**
   backslash, if that suffix is nonempty; otherwise plus the whole path.
   On success copy the captured candidate.
3. If the alternate prefix is nonempty, alternate prefix plus current input.
   On success rebuild from the **live** alternate prefix and live input.

Restore error mode and return success/failure. A failed search leaves the input
unchanged except for provider effects. Only backslash is the separator in these
algorithms. The first and last separator operations are intentionally different.

Filename-extension replacement first copies source unless it is the same
buffer. Find the last dot anywhere, including a directory component, truncate
there, append dot and the supplied extension, then uppercase the whole result
using the active CRT rules. Preserve source/destination alias effects; an
overflowing destination is an excluded bug.

#### Resource modules

Registration loads a module as data. Failure reports the missing file and
returns no module; success appends it to the ordered registry, increments the
count and returns its handle. A full registry must not overwrite adjacent state.

Lookup captures the four-byte resource type and adds its terminator. Search
the primary module first, then registered modules in ascending order, testing
the live count at each iteration. On success reread the selected module before
loading the found resource, so a lookup callback can change that receiver.
Bitmap-resource lookup uses the native bitmap type instead of a four-character
type. Missing lookup returns no resource.

A name-sanitizing wrapper changes spaces, dots and apostrophes to underscores
in place, checking the live string length on each iteration. It captures the
four type bytes before changing the potentially overlapping name.

Unregistration reads a registry slot before testing the count. If found within
the count, shift following entries left, including the read of the first
unused slot, decrement count and release the module. That last read requires
backed spare state; it does not authorize writing outside the registry.

### 3. Palette state, publication and matching

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Decode and retain source palettes so custom images and exported assets have the
specified colors/transparency. The operations below remain required for source-
image editing and file conversion. Native palette handles, display generations,
nearest-color cache representation and screen fades are reference procedures.

#### RGB writes, imports and commits

Writing one source RGB entry stores red, green and blue low bytes. Source import
and range operations apply only this upper clamp:

    if wrap32(first + count) > 256:
        count = wrap32(256 − first)

Reject or stop before an invalid destination write. Native reserved-color flags,
commit calls and generation tokens do not affect source RGB values and are in
Appendix D. Here, committing makes the changed logical palette available to its
consumer; it does not require a native handle or a random token.

Import reads RGB triples in order, loading blue, green and red before the
entry's stores, performs the same upper clamp, then commits the range.
Single-entry setRGB writes then commits one entry. Extraction writes red,
then loads/writes green, then loads/writes blue for each entry; overlapping
output can change later reads. It does not commit or draw randomness.

#### Color files

The operations below define input/output values. Native assignment, palette
rotation, repetition and fade procedures are retained in Appendix D. They are
optional screen effects, not additional requirements on newly rendered pixels.

Color files store little-endian signed first and count, followed by count RGB
triples. The default writer uses first=0,count=255. Capture RGB before creating
the file, write both header values and triples, ignore write results and close.
A creation failure does not produce a file. The reader performs both header
reads before preparing count×3 logical payload bytes; missing header bytes need
supplied input or a stop. Initialize payload to zero, so a short read retains
zeros in its unread suffix. Close before importing and committing. A CTAB
resource's default loader reads little-endian 32-bit first/count from its first
eight bytes, then imports RGB triples. The explicit-range loader skips those
bytes and uses the caller's first/count. Native storage ownership, leak and
unlock/free traces are in the implementation appendix.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 4. Sprite scaling and sampling

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Choose any screen scaling/sampling implementation that preserves interpreted
asset content, object identity and transparency. Screen resampling does not
change authored source images or S04 export pixels; file conversion continues
to use the required source/format contracts in §§5 and 8–10.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 5. Sheet loading and extraction catalog

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

The game-sprite parent constructs its temporary window/surface context, then
loads **Cities, general interface assets, Terrain, Units**, and finally destroys
that context. This order establishes civilization colors before portraits and
retains palette publication order.

#### Common sheet operation

For CITIES, ICONS, PEOPLE, TERRAIN1, TERRAIN2 and UNITS:

1. Change to the current scenario/rule search directory.
2. Try the dedicated sheet BMP loader on NAME.BMP, palette first=10,count=192.
3. On failure try NAME.GIF in that directory.
4. If that also fails, change to installation directory and retry NAME.GIF.
   Otherwise change to installation directory after the successful first/second
   attempt.
5. Continue extraction even if the final GIF attempt failed.

All loaders share the working sheet surface. A safe implementation must account
for its partial/retained state after failure. It must not pretend that the
parent has an all-assets-loaded success gate.

Unless specified otherwise, reset a target sprite before extraction, extract
with key color 7, and replace stored color 9 by 7 afterward. That replacement
does not recompute content bounds. All coordinates below refer to the decoded
sheet, with palette offset already applied. These are external asset-layout
contracts, not requirements on internal object placement.

#### Terrain sheets

TERRAIN1 extractions, in order:

| Family | Indices | Origin | Size |
| --- | --- | --- | --- |
| Base and two special resources | terrain t=0..10 | base (1,1+33t), specials (131,1+33t) and (196,1+33t) | 64×32 |
| Irrigation, Farmland, Mine | i=0..2 | (456,100+33i) | 64×32 |
| Pollution, Grassland shield, hut | in that order | (456,199), (456,232), (456,265) | 64×32 |
| Roads and railroads | i=0..8, road then rail per i | (1+65i,364) and (1+65i,397) | 64×32 |
| Blend mask, pick mask, hidden tile, direction mask, Ocean blend | in that order | (1,447), (66,447), (131,447), (261,447), (196,447) | 64×32 |

For each base/special triple reset all three before extracting. Likewise reset
both road/rail targets before extracting the pair.

Build blend families 0..12 next. Fill the working surface's top-left 64×32 area
with 7; draw the base terrain for families 0..10, hidden tile for 11 and Ocean
blend for 12 at (0,0). Draw the blend mask there using key **10**. Extract
four 32×16 quadrants in order from (32,0),(32,16),(0,16),(0,0). These sprite
draws use the current scale; the producer does not silently select identity.
Release the working surface before loading TERRAIN2.

TERRAIN2:

| Family | Indices and traversal | Origin | Size |
| --- | --- | --- | --- |
| River, Forest, Hills, Mountains | i=0..15; reset all four, then extract in that order | x=1+65*(i mod 8), y=base+33*floor(i/8), bases 67,133,199,265 | 64×32 |
| Coast quarters | variant i=0..7, quadrant q=0..3 | Let x=1+66i,y=429; q origins (x,y), (x+33,y+34), (x,y+17), (x,y+34) | 32×16 |
| River mouths | i=0..3 | (1+65i,331) | 64×32 |

Release the surface, then destroy the temporary Ocean-blend and mask sprites
in that order. P02 owns which connections select these extracted sprites.

#### Cities, shields and units

For each city style 0..5, and size class 0..3 within it, y=39+49*style.
Reset ordinary and walled sprites, then extract ordinary at
(1+65*size,y) and walled at (334+65*size,y), each 64×48. Immediately after
each extraction scan marker color 4, then marker color 3.

Each marker scan begins with x=y=0. For a cell origin (x,y), scan its 64
top-border pixels (x+column,y−1) in ascending column, then 48 left-border
pixels (x−1,y+row) in ascending row. Every match replaces the result; the
**last** match wins. Marker 4 gives offsets (column−1,row−15); marker 3 gives
(column,row). Missing markers retain zero.

For civilizations 0..7, let x=1+15*civ. Reset two shields, extract 14×22 at
(x,425) then (x,448), and sample primary RGB-index color at (x+3,429),
secondary at (x+6,429) and label/overview at (x,423). The unused fourth
record component remains retained. Next reset/extract 64×48 Fortified and
Fortress at (143,423) and (208,423), then unoccupied/occupied Airbases at
(273,423) and (338,423). Release the working surface.

UNITS loads 63 sprites, ascending index i, 64×48 at
(1+65*(i mod 9),1+49*floor(i/9)). After each extraction scan top and left
borders for marker 4, starting offsets at zero and using the last matching
column/row directly, without the city marker's subtraction. Finally extract
the 12×20 unit shield at (586,1) and release the surface.

#### Interface, editor, people and diplomacy sheets

Construct a local palette first. Change to the search directory and load GIF
resource 85 with first=10,count=192 into that palette. On failure retry after
changing to installation directory. Always finish in installation directory.
Extract 11 nuclear frames of size 91×72: first six at (1+92i,1), next five at
(1+92*(i−6),74). Release the working surface.

Load ICONS through the common operation. Perform these groups in table order;
reset each group's stated targets before its extractions. Anonymous art names
below identify distinct assets without assigning new gameplay meanings.

| Group | Sheet origins and dimensions |
| --- | --- |
| Map cursor, grid, city-radius grid | 64×32 at (199,256), (199,289), (248,430) |
| Alternate fortification | 64×48 at (32,187) |
| Small art A | 32×32 at (34,223) |
| Pollution art | 32×32 at (67,223) |
| Small art B/C, city disorder | 32×32 at (100,223), (133,223); 30×30 at (166,223) |
| Four map/interface icons | 32×32 at (199,256), (232,256), (265,256), (298,256) |
| Three tall pieces | 31×96 at (311,1), (311,98); 56×131 at (265,289) |
| Resource shortage/ordinary pairs, i=0..2 | 14×14 at (1+15i,290) and (1+15i,305); repeat the same pair reset/extraction **twice** before advancing i |
| Rate icons, i=0..2 | 14×14 at (1+15i,320) |
| Small resources, i=0..2 | 10×10 at (49+11i,334) |
| Luxury/tax/science icons, i=0..2 | 10×10 at (49+11i,345) |
| Unhappy/suppressed icons | 10×10 at (82,334), (82,345) |
| Research/warming pairs, i=0..3 | 14×14 at (49+15i,290) and (49+15i,305) |
| Explosions, i=0..7 | 32×32 at (1+33i,356) |
| Frame tile surface | Allocate 64×32, copy from (199,322) to (0,0); publish dialog frame then window border reference |
| Panel tile surface | Allocate 32×32, copy from (298,190); publish dialog client tile |
| Improvements i=1..38 | 36×20 at (343+37*((i−1) mod 8),1+21*floor((i−1)/8)); slot 0 is untouched |
| Wonders i=0..27 | 36×20 at (343+37*(i mod 7),106+21*floor(i/7)) |
| Advance icons | For epoch=0..3 outside category=0..4, 36×20 at (343+37*category,211+21*epoch) |
| Two additional icons | 64×32 at (350,255), (350,288) |
| Four caption controls | 16×16 at (1+17i,389), i=0..3 |
| Up/down arrows | 18×24 at (227,389), (246,389) |

Release the surface. In installation directory load EDITORPT.GIF. Only if it
succeeds, reset/extract 15 tools of 32×32 at (1+33i,447). Regardless of that
result, reset/extract editor unit backdrop 64×48 at (575,1) and tile backdrop
64×32 at (575,50), both with key 7 **without** replacing 9. Release the surface.

Load PEOPLE through the common operation. For era 0..3, citizen 0..10, extract
27×30 at (2+28*citizen,6+31*era). Then extract eleven small citizens, 15×26
at (2+16i,137), and release the surface.

Load GIF resource 86 into the local palette with the same search/install retry
as resource 85. Extract 34×34 government art in this order:
Anarchy (2,68), Despotism (2,68), Communism (68,68), Fundamentalism
(134,68), Monarchy (200,68), Democracy (266,68), Republic (332,68).
Then extract 64×64 Ceasefire, Peace, War, Allied and portrait art at
(2+66i,200), followed by information (398,134) and tutorial (134,398).
Reset Anarchy and Despotism together before either extraction; likewise reset
information and tutorial together. Other government/treaty targets reset
immediately before their individual extraction.
For each civilization 0..7 extract its 64×64 portrait from (2,398) and replace
color 8 by its live primary shield color. Release the surface and destroy the
local palette. P04/P07/P08/P09/P10 own selection of this art in their interfaces.

### 6. Sprite extraction, lifetime and drawing

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Sprite storage, allocation and on-screen drawing are implementation choices.
Preserve the source-content extraction below for classic asset interpretation;
its logical result can be stored without the original row/arena representation.
S04 export transforms remain exact source-image operations.

#### Extraction

Intersect the requested rectangle with the surface clip, ignoring the
intersection return value, and capture width/height. Read the established source
pixels; an unavailable mapping fails without inventing pixel contents.

The low-level key has three cases: −1 samples the first pixel as key; −2 treats
every sampled pixel as opaque; otherwise use its low byte. The ordinary rectangle
wrapper first narrows to a byte and cannot request either negative sentinel.
The transparent wrapper supplies −1 directly.

Scan rows from the top, left to right, for the first opaque pixel. Separately
scan from the bottom, left to right, for the last nonempty row. If all pixels
are transparent, publish empty content, full bounds (0,0,width,height), zero
offsets and the selected key; −2 publishes key 0. The completed wrapper leaves
no active sprite content.

For every row from first opaque row through last:

1. Begin with left=0,count=0. Find the first opaque pixel from the left.
2. Find the last opaque pixel from the right. Include all pixels between them,
   including transparent interior pixels; an empty intervening row has count 0.
3. Update global minimum left and maximum exclusive right from nonempty rows.
   Retain the source position, left and count for copying.

After gathering those row spans, retire the old sprite content before creating
its replacement. A failed replacement does not publish new geometry or restore
retired content. On success publish content bounds
(minLeft,firstRow,maxRight,lastRow+1), full bounds, zero offsets and selected key,
and copy rows in ascending order. The wrapper installs that result. Preserve
source pixels and the partial-publication boundary when this operation changes
an edited asset; a display-only allocation failure follows X01's UI policy.
Scratch tables, native locks, owned handles and their cleanup are reference
procedures in [Appendix E](#appendix-e-original-implementation-reference); Appendix B retains row packing for validation.

The same source-pixel trimming/transparency rules apply regardless of storage
backend. Native DirectDraw acquisition/release and draw failures are reference
procedures; use X01's safe cleanup policy.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 7. Surfaces, clipping and raster primitives

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Native surface allocation/orientation is optional. The following image-operation
semantics remain required where S04 uses them to produce classic files or edited
source assets; they do not require the on-screen renderer to use the same
primitives. Preserve clipping, transparency and output pixels at that file/asset
boundary.

#### Clips and pixel operations

Setting a clip captures input bottom, right, top, left before writing the clip,
then intersects it with full bounds. Reset restores full bounds. Copying a clip
loads/stores left, top, right, bottom successively; aliasing can affect later
loads. Indexed operations address the logical source pixel at (x,y).

An indexed pixel read outside the half-open clip warns and returns 0; a write
outside warns and omits the store. The validity decision is not repeated after
the warning callback. In-range operations use the established active storage.
Replacing a color clips the rectangle and scans rows/columns, replacing equal
bytes; extents remain live during the scan. Native access acquisition and
empty-region row-table reads are reference details.

#### Fill and copy

Indexed fill establishes writable pixels, intersects with the current clip
and returns on zero width or height. Ordinary
byte colors produce a solid fill. For accepted wider scalar colors, form

    pattern = color OR (color<<8) OR (color<<16) OR (color<<24)

and write little-endian groups of four bytes, then the low two bytes when needed,
then the low byte when needed. Thus high input bytes can affect multi-byte
groups. Do not narrow the argument before forming this pattern.

Surface copy establishes source then destination pixel access. Intersect source
and destination rectangles **independently** with their clips;
the first clip does not shift the opposite origin. Copy min(widths) by
min(heights). The source's pixel-mode size multiplies both horizontal coordinates
and copy width. Capture destination orientation then source orientation,
destination pixels then source pixels. For each row, copy forward in four-byte
read/write groups, then one byte at a time for the remainder. Proceed in positive
logical row order regardless of overlap. This is neither a whole-source
snapshot nor a direction-changing overlapping copy. Native access release
order is recorded in the implementation appendix.

The corrected-position wrapper adjusts negative source/destination offsets by
first subtracting each side's supplied coordinate origin, then moving the
opposite position and shrinking the requested extent. It restores those origins
when invoking
the basic copy only for positive remaining width and height. The basic copy's
independent clip rule still applies.

Indexed palette remapping returns immediately when no active storage exists.
Otherwise scan physical backing rows using live width, height and stride. For
each pixel whose signed (pixel−first)>=0, reread its index and replace it with
table[pixel−first]. The extra count argument is ignored; there is no upper-range
test. Table/output aliasing can change later reads. Padding is skipped, and no
new palette generation is published by remapping itself.

Background tiling captures tile dimensions. Start each axis with
abs(position−origin) remainder tileDimension, using F01 wrapping absolute value
and remainder. A position before the origin does not use mathematical modulo.
Copy successive edge-cropped pieces, columns inside rows, restarting the
horizontal offset for each row. Do not reload tile dimensions after callbacks.
Zero tile dimensions reach division failure unless a restrictive profile rejects
them first.

#### Lines and RGB primitives

The line parent first performs four coarse tests: at least one endpoint is at
or beyond clip left/top and at least one is below clip right/bottom, plus a
backend-present test. Horizontal lines clamp both x endpoints independently to
[left,right−1]; vertical lines similarly clamp y. If the resulting endpoints
coincide, return **without drawing the point**. Otherwise write both endpoints
and every intervening pixel using the current indexed text color.

Horizontal raw fill normally writes groups of four, then two/one remaining
bytes. Its off-nominal count rule is in Appendix B. Vertical raw fill writes one
byte then advances signed stride. Neither line parent acquires port access.
The diagonal path resolves the current indexed color to RGB and uses the
backend's line raster from first endpoint to second. Failed color lookup needs
supplied RGB or a stop; it does not establish black. Original native pen/object
management is in [Appendix E](#appendix-e-original-implementation-reference).

Outline calls top, right, bottom, left edges using the supplied right and bottom
coordinates themselves. The line clip handles those edges; do not subtract 1
in a replacement outline. RGB lines use the same coarse gate and backend line
raster for every direction, with low-byte RGB components. Their raster and
endpoint rules belong to the native-service contract, not the indexed H/V loop.

RGB fill establishes writable pixels. In RGB555 mode, each pixel is
((red AND 248)<<7)+((green AND 248)<<2)+((blue AND 255)>>3). Other modes fill
the clipped rectangle with the supplied solid RGB color through the backend.

RGB copies independently clip both rectangles and use the minimum dimensions
with a native source-copy operation. Stretch-to-port captures destination
height/width/y/x/backend, then source height/width/y/x and uses native stretch
source-copy. Stretch-to-window first updates the source palette from the target;
the backend realizes an attached window palette only in paletteMode 1.
Transfers require a usable destination and successful backend operation; a
failed native service does not produce invented pixels. These operations do
not implement §4's sprite lookup sampling. Native context ownership is in the
implementation appendix.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 8. GIF framing and LZW decoding

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

#### File and resource entry points

The file loader opens and reads the file, then requires its first three bytes
to be GIF. The resource loader looks up type GIFS but rejects only a
NUL-terminated string **equal to GIF**; other strings pass that check.
GIF87a/GIF89a pass. The resource comparison requires a backed terminator or
earlier differing byte. A short read needs established retained bytes or a
bounded stop; never fabricate its missing suffix.

Both require the global-color-map flag and derive colorCount from the packed
header's low three bits. If a palette is supplied, upper-clamp the requested
range as in §3 and import the unsigned minimum of requested count and
colorCount from the global RGB triples. Palette effects precede descriptor errors.

Skip zeros after the complete global table. Accept comma **or exclamation
mark**, advancing past it and interpreting the following bytes as an image
descriptor; extensions are not parsed as extensions. Capture width/height,
ignore offsets and logical-screen dimensions. A local-palette flag warns but
**does not skip the local table**. Ignore interlace. Replace the surface with
the captured dimensions: file loading aborts if this fails, resource loading
ignores that result but cannot write beyond available output. Read minimum code
size after surface creation, capture destination/stride and invoke decoding.

After decoding, assign the palette unconditionally and normalize bottom-up
backing to the specified logical row order. Return success even when the decoder
reported failure; retain its produced pixels. Bad signature/global table/
descriptor returns 0 after format-error reporting. Missing file returns 0;
missing resource reports failure first. Safe cleanup of native resources does
not roll back preceding palette or pixel effects. Original lock/allocation,
message-pump and double-close sequences are retained in [Appendix E](#appendix-e-original-implementation-reference).

#### Dictionary, stream and output algorithm

Create a logical dictionary of 4096 entries. Failure returns 0. Initialize
every entry's prefix to absent, character to
low8(paletteStart+index), and length to 1. The kernel uses code indices doubled
for storage; the algorithm below keeps those doubled indices to specify its
off-nominal masks exactly. They are scalar identifiers, not required addresses.

Let u16 narrow to unsigned 16-bit, s16 interpret that value as signed, and let
all other unsigned scalar operations wrap at 32 bits. Shift counts use F01.
For supplied code argument m:

    clear = u16(2 << (m mod 32)); end = u16(clear+2)
    initialWidth = u16(m+1)
    initialMask = u16((2 << (initialWidth mod 32))−2)
    next = u16(clear+4); previous = clear
    codeWidth = initialWidth; mask = initialMask
    bitBuffer = 0; bufferedBits = 0; rowCount = 0

The tables at doubled code c use entry c/2; invalid odd/out-of-range access
requires actual backed bytes or a safe stop, never permission to overwrite.
Set rowWidth=u16(width), padding=u16(stride−width), and logical output extent
to u16(height)×u16(stride). Track the next output position relative to the start
of that extent. For nominal backed surfaces, the three limit checks below test
whether that position **exceeds** the extent; equality is permitted because
phrase expansion writes backward from the next position. Native base addresses,
wrapping address addition and signed address comparisons are reference details
in the implementation appendix. An explicitly admitted address-sensitive
validation case uses that appendix's mapping; otherwise reject such off-nominal
inputs before an unsafe operation. It never permits writes beyond the logical
surface.

Repeat:

1. Before reading each code, if s16(rowCount)>=s16(rowWidth), subtract rowWidth
   once. If padding is nonzero, move the rowCount trailing output bytes backward
   from high address to low into positions padding bytes later, and advance
   output by padding. There is **at most one row transition per code**, even
   for a phrase spanning several rows.
2. While bufferedBits<codeWidth, obtain another byte. When the source cursor
   equals blockEnd, consume a block-length byte and set blockEnd to the cursor
   plus that length. Then consume one payload byte, OR it into bitBuffer shifted
   by bufferedBits modulo 32, and add 8 to bufferedBits with u16 narrowing.
   A zero-length block is not a terminator and still leads to a payload read.
3. Set code=u16(u16(bitBuffer<<1) AND mask). If unsigned code>next, return
   status 1 before consuming buffered bits. Otherwise shift bitBuffer right by
   codeWidth modulo 32 and subtract codeWidth from bufferedBits with u16 wrap.
4. If code==end, return status 2. If code==clear, restore initial width/mask,
   previous=clear and next=u16(end+2), leaving dictionary contents intact.
5. If previous==clear, read this code's length, advance output and rowCount,
   and return status 3 when the output position exceeds the logical extent.
   Otherwise set previous=code and expand that phrase backward into the output.
6. Otherwise store prefix[next]=previous and length[next]=u16(length[previous]+1)
   **before** testing output bounds. If code<next, advance by length[code],
   update rowCount, and return status 4 on the same output-limit comparison.
   Then set previous=code and expand backward.
7. For code==next, advance by length[previous]+1 and update rowCount; return
   status 5 on that comparison. Expand the previous phrase ending one byte
   before the new output position, set previous=code, and copy its first byte
   to the final position. This is the repeated-prefix special case.
8. Store the expanded phrase's first byte as character[next]. Advance next by
   2 with u16 wrap. If unsigned next>mask and codeWidth!=12, increment width
   and replace mask by u16((mask<<1)+2). Continue.

Backward expansion decrements its output cursor, writes the current entry's
character, then reads its prefix; repeat until the absent-prefix sentinel.
This write-before-prefix-read order matters for aliasing. A malformed chain can
read unproduced entries or overflow output; those are input/bug boundaries.
The original has no safe dictionary-full guard. Stop before a write beyond
4096 entries; do not wrap into another dictionary table.

The wrapper discards statuses 1..5, retires its dictionary and returns 1. File and
resource loaders discard that result too. Output bytes and palette effects
therefore survive a decoder exit. Neither a blanket standard-GIF decoder nor
an all-or-nothing decode transaction matches this contract.

#### Compact picture resources

A compact picture has a six-byte header: big-endian width/height, one-byte
minimum code size, one-byte color-count-minus-one; then RGB triples and the
same GIF block stream. Allocate the surface first. Optional palette import uses
the caller's requested count, **without** clamping it to declared colors.
Decode after the declared table, assign palette unconditionally, flip bottom-up
backing, release the resource and return success. The RGB555 path builds its
conversion table only from declared colors; other indices need supplied backing
or a stop. Appendix B records the code-argument upper-word retention in these
entry points.

### 9. BMP sheet input, output and alternate loaders

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

#### Dedicated 640×480 sheet input

Open in binary read mode. Open failure returns 0 without the later error notice.
Read the 14-byte file header and require BM. Read the information-header size,
then size−4 bytes, then **1024 palette bytes regardless of the declared count**.
The pixel-offset field is ignored: decoding continues from the current stream.
Nominal information-header size is 40. A larger or underflowed read overwrites
the local header and is excluded; a shorter successful read leaves later fields
as retained inputs. Do not invent values for those fields.

Require width=640,height=480,depth=8, then set success=1. If a palette is supplied,
reduce requested count to a nonzero declared color count when its signed value
is smaller, then upper-clamp first+count as in §3. Read BGR palette entries in
ascending order into consecutive requested palette slots and commit **once**.
Allocate the captured dimensions, ignoring its return. Read compression after
these effects:

- Uncompressed: for row 479 down to 0, read 640 bytes in ascending column and
  write low8(byte+first). EOF reports failure, retaining the valid pixel prefix.
- RLE8: start output at (0,479). The separate logical horizontal cursor has
  **no original producer**. Supply its retained value or stop when it first
  affects addressing. Runs before that use the explicit output pointer.
- RLE4: report unsupported compression and return failure after allocation.
- Other nonzero compression: return success without decoding pixels.

RLE8 repeatedly reads length. EOF at this read leaves success=1. Nonzero length
reads one value, emits length copies of low8(value+first), and adds length to the
logical cursor. With length 0, read a control:

| Control | Ordered effects |
| --- | --- |
| 0 | Decrement row, reset output pointer to (0,row), **retain** logical cursor. |
| 1 | Finish successfully. |
| 2 | Read vertical delta first and subtract from row; read horizontal delta second and add to logical cursor; set output to (cursor,row). |
| Other | Copy control literal bytes with palette offset; consume a padding byte when control is odd; add control to logical cursor. |

EOF in a value/control/literal position supplies the stream's −1 result to the
subsequent scalar logic; it is not the same as EOF at the length read. Any
resulting write outside the surface must stop. EOL's stale cursor and the
vertical-before-horizontal delta order are preserved within backed surfaces.
The independent loader's initial-cursor=0 repair is optional, not evidence that
the original initialized it.

Close before presenting any final error message. Close failure is ignored.
Neither error handling nor a restrictive rejection rolls back earlier palette
generation draws, allocation or valid pixels.

#### Sheet output

The writer always emits a 640×480, 8-bit uncompressed BMP. Build the fixed
headers in Appendix B. Initialize all palette RGB triples to black, then set
indices 236..249 to cyan, 250 to blue, 251 to (127,0,0), 252 to red, 253 to
magenta, 254 to green and 255 to (135,84,135). If a palette is supplied, copy
its requested first/count range into output palette entries beginning at **0**.
There is no new upper clamp in that copy. Each reserved palette byte is
unproduced in the original; exact file comparison requires supplied bytes.
Writing zero reserved bytes is an explicit deterministic repair.

Prepare the palette before opening in binary write mode. Open failure returns
0. Write the file header, information header and palette as successive blocks.
Only after all succeed, set success=1 and write rows 479 down to 0, each 640
pixels, as low8(pixel−first). First failed block/pixel write stops subsequent
writes. Close before error presentation, ignoring close failure. Retain a
partially written file. S04 owns sheet composition, destination selection and
removal of an earlier file.

#### Generic indexed BMP

These are distinct entry points from the sheet loader. A file maps the DIB at
offset 14; a bitmap resource begins directly with it. Capture width/height and
allocate the surface **before** checking depth and compression. Require depth 8
and compression 0. The file signature, pixel offset, declared header size and
palette count do not redirect these reads. A format failure returns failure
after preceding surface effects; native resource leaks and file-destructor
cleanup are reference details, not required ownership rules.

If a palette is present, iterate the caller's unsigned start-to-start+count
range, reading BGR quads immediately after the fixed 40-byte DIB header. Call
setRGB for **each** entry, including its per-entry generation draws; this loop
has no range clamp. Start pixels after 256 quads regardless of imported count.
Align width with §7's signed aligned-width operation. For row=1..capturedHeight,
copy alignedWidth bytes into (0,height−row), adding first to **padding as well
as image pixels**. Resource input advances by alignedWidth; file input advances
by the live port stride. Assign the palette unconditionally, release/unmap input
and return success. Unsafely large counts/dimensions do not authorize overwrites.

#### RGB555 and RGB24 BMP

Allocate the requested true-color mode from captured dimensions first. Round
the source **pixel count** up to a multiple of four. For RGB555 with 8-bit input,
read 256 BGR quads into a conversion table and convert indices. With 24-bit input,
both modes read BGR triples; RGB555 applies §7's formula, RGB24 stores red,
green, blue in that order. Traverse the rounded pixel count, bottom row first,
then move through the destination's live previous-row accessor. These paths
do not validate compression. Other depths still allocate and return success
without producing pixels.

Rounding pixels rather than source bytes for 24-bit input can overrun the
destination row/frame. That write is an excluded bug; choose a supported
dimension profile, omit the unsafe suffix or stop before it. A corrected
standard BMP stride is a declared repair, not the recovered rule.

### 10. Other image inputs and conversion

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

#### Indexed Targa

Capture width and height from the 18-byte header, then invoke the receiver's
allocation service. Other header fields remain live after that callback.
Advance past the header plus the **signed-byte** ID length. A nominal ID length
is 0..127; high-bit values seek backward under the original scalar rule.

If color-map type is nonzero and its depth is 24, import the caller's requested
count of BGR triples through per-entry setRGB. This branch does not first test
the palette reference for null. Then advance input by declared color-map-count×3,
ignoring color-map-first. Type 1 with depth 8 copies width×height indices,
adding first. Descriptor top-origin selects top-down logical traversal; otherwise
start at the bottom and reverse row stride. Other depths/type values produce no
pixels. Type 10 warns that compression is unimplemented. Right-origin warns
but does not reverse columns. Assign palette unconditionally and return success.

Missing/unsupported input reporting and all logical effects before failure
remain observable; a restrictive profile can reject unsupported types before
decoding. Native mapping, unlocking and destruction are reference mechanisms.

#### True-color Targa and GIF

True-color Targa similarly caches dimensions before allocation and reads other
fields afterward. Type 2 is the uncompressed input:

- RGB555 with depth 16 copies each width×2-byte row with overlap-safe copying.
- RGB555 with depth 24/32 converts successive BGR triples to RGB555, advancing
  input by 3/4 bytes per pixel respectively.
- RGB24 with depth 32 stores RGB and advances input by 4. With depth **24** it
  does **not advance input**, repeating the first input pixel across the frame.

Row origin, signed ID length and unsupported-compression/right-origin warnings
follow the indexed Targa rule. Resource RGB555 depth 24 falls through to an
additional depth-32 conversion pass after the first full frame; its writes
beyond the frame are excluded. The file path does not take that second pass.

True-color GIF/compact conversion first captures declared palette triples as
RGB555, creates an intermediate indexed backend, decodes at palette offset 0,
and flips that intermediate if bottom-up. Allocate final RGB555 backing, ignoring
its result, then walk intermediate rows in ascending physical order, converting
width indices per row. Destination row stride is reread after each row; source
stride comes from the intermediate. Dispose the intermediate before closing
input. A pixel index beyond the produced palette prefix needs supplied entries
or a stop.

Both file and resource RGB555 GIF use the resource-style inverted GIF-string
test from §8, require a global palette and use the same descriptor/local-table/
interlace quirks. The original also writes the unchanged width and height back
into the descriptor. A read-only file mapping makes those identity writes fault
on valid files. Removing them is an explicitly identified repair; a nominal
compatibility profile must state whether it includes that repaired file path.

#### PCX

Copy the entire 128-byte header before allocation. Reject when manufacturer!=10
**and** encoding==0; separately reject when depth!=8 **and** planes!=1. These
are conjunctions, so files outside the intended one-plane, 8-bit PCX profile
can pass. Width=signed16(xMax)+1,height=signed16(yMax)+1; minimum coordinates
are ignored. Allocate; take rowBytes from signed16(bytesPerLine), then treat
it as an unsigned loop bound.

Begin compressed input after the header. Keep a remaining-run count in 0…255
across rows. For each of rowBytes output positions per row:

1. If remaining is zero, read a byte. Top bits 11 introduce a run: remaining
   becomes the low six bits, value is the next byte, input advances two.
   Otherwise remaining=1,value=that byte,input advances one.
2. Write low8(value+first), then decrement remaining modulo256.

Thus a run header with low count 0 still writes once and wraps remaining to
255. Runs span row boundaries. Writes include declared row padding; rowBytes
larger than backed stride must stop before an overwrite.

File palette lookup uses fileEnd−769. Resource palette lookup uses the final
compressed cursor−(rowBytes−width), which is not generally the standard PCX
palette position for compressed padding. If that byte is marker 12, upper-clamp
the caller range and import RGB triples after it; then assign the palette even
if the marker was absent. Resource allocation failure leaves it locked;
ordinary success releases it. File failures retain the file object's cleanup.

#### LBM/PBM resources and row helpers

Require FORM and then PBM-space or ILBM; ignore FORM's declared total size.
Repeatedly read an eight-byte chunk header and round its big-endian length up
to an even byte count. Unknown chunks advance by that rounded count.

- BMHD copies the rounded payload into its 20-byte header. Oversized copies are
  excluded writes. Width/height are big-endian. At least the fields through
  compression must be produced; short/BODY-before-BMHD inputs need retained
  fields or a stop. More than eight planes reports and returns while retaining
  the locked resource.
- CMAP with a palette imports the upper-clamped requested range and advances
  by rounded chunk length. With no palette, it **does not advance** the input
  cursor over the payload.
- BODY changes 1×1 to 0×0. Packed row bytes round width even; planar row bytes
  are 2×ceil(width/16). Allocate even-rounded width when planes<5, otherwise
  original width. Ignore allocation failure. Save active pixels, then for
  each output row decode only the packed case; the ILBM case advances rows
  without decoding planes. Advance active pixels by live stride after each row.
  Assign palette, flip bottom-up backing, restore saved active pixels, release
  the resource and return 1.

The byte-run row decoder with compression 0 copies count literals. Compression
1 repeats packets until at least count output bytes have been emitted:
signed control 0..127 copies control+1 literals; −127..−1 reads and repeats
one byte 1−control times; −128 does nothing. A packet is not shortened to the
remaining row count. Unsupported compression emits nothing. Packet overshoot
that exceeds the output object is an excluded write.

The separate planar-row helper clears width output pixels, then for each plane
decodes a temporary row from the **same initial input cursor**. Test each bit
most-significant first and OR 1<<plane into the corresponding pixel. A requested
mask plane is decoded from that same cursor and discarded. The helper does not
return an advanced input position, and the resource loader above does not call
it for ILBM. Unsupported compression can leave temporary bytes unproduced;
Appendix B records its fixed capacity. Do not substitute a standard multi-plane
decoder while claiming this unusual entry-point behavior.

### 11. Integration boundaries and conformance

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Validate nominal custom asset search/override precedence, decoded source content,
sheet slots/markers, transparency and source palettes. Exercise supported file
formats and S04 exports through the original reader. Test safe rejection of
unsupported input and required file failures. Native surfaces, display sampling,
palette generations and raster traces are optional; export bytes are not.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### Appendix A. Evidence and source identity

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

The target executable is classic MGE 5.4.0f patch 3, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
The primary reconstruction is pinned at
`a71e8aefffe04e6f719e0e213b027b128a747711`:

- [graphics.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/graphics.cpp)
  supplies sheet loading/extraction, editor output callers and image/palette
  consumers.
- [mapview.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/mapview.cpp)
  supplies selected-scale and terrain/city sprite consumers.
- [graphics state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/graphics.h)
  and [initial data](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/gamedata.cpp)
  distinguish asset families, colors and initial shared values.

Independent evidence is `civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. Its relevant modules are:

| Area | Files under src/ unless marked otherwise |
| --- | --- |
| Names, paths and resources | resource_paths.cpp, resource_files.cpp, resource_modules.cpp, resource_file_helpers.cpp |
| Sheet extraction | terrain_sprites.cpp, city_sprites.cpp, sprite_initializer.cpp |
| Sprite algorithms | sprite.cpp, sprite_extract.cpp, sprite_surfaces.cpp, sprite_scale.cpp, sprite_paired_scale.cpp, segmented_raster_kernels.cpp |
| Surfaces | graphics_allocation.cpp, graphics_port.cpp, graphics_copy.cpp, graphics_port_operations.cpp, graphics_truecolor_transfers.cpp, graphics_screen.cpp, window_palette.cpp |
| Images | bitmap_file.cpp, gif_file_loader.cpp, gif_resource_loader.cpp, gif_decode.cpp, graphics_indexed_images.cpp, graphics_image_modes.cpp, graphics_image_io.cpp |
| Palettes | palette_import.cpp, palette_operations.cpp, palette_fade.cpp |
| External formats/contracts | include/civ2/graphics_image_formats.hpp, graphics_image_modes.hpp, graphics_image_io.hpp, graphics_indexed_images.hpp; palette_operations.hpp, graphics_port.hpp, graphics_screen.hpp, sprite_scale.hpp, segmented_raster_kernels.hpp in the same include directory |
| Existing probe recipes | tests/sprite_scale_probe.cpp, tests/gif_decode_probe.cpp, tests/gif_decode.py |

Original discriminator locations include palette generation `005c6a42`, range
commit `005c6480`, single/pair scale selection `005cd775`/`005cda2a`, scale-record
generation `005cdcdb`, extraction `005cf64c`, ordinary sprite draw `005d056c`,
row draw `005e518e`, sheet BMP input/output `0046f460`/`0046fbf3`, GIF file/resource
input `005bf071`/`005bf5e1`, decompression wrapper `005e4d60`, and palette-tree
search `005c7e06`. These addresses are evidence locators, not cleanroom APIs;
the checker records exact instruction probes separately. Paired call sites
`005d0d8c` and `005d1092` both target `0061a759`.

### Appendix B. Representation and excluded bugs

#### External asset bytes

These layouts constrain file/resource compatibility rather than internal
classes. Multi-byte values are little-endian except the stated big-endian
compact/LBM fields; offsets below are decimal bytes from the relevant header.

| Format | Consumed fields/layout |
| --- | --- |
| GIF | 13-byte header: signature/version 0..5, logical width/height at 6/8, palette flags 10. Global RGB count=2^((flags AND 7)+1), triples begin at 13. After the comma/exclamation byte, descriptor left/top/width/height are unsigned words at 0/2/4/6, flags at 8, code size at 9, subblocks at 10. Global/local table flag=128, interlace=64. |
| Compact | Type CvPc; six-byte header: big-endian unsigned width/height at 0/2, code size at 4, colorCount−1 at 5; RGB triples then GIF blocks. |
| BMP | File prefix 14 bytes; DIB 40 bytes. DIB size at 0, signed width/height at 4/8, planes/depth unsigned words at 12/14, compression at 16, image bytes 20, resolution 24/28, color count 32, important count 36. Palette entries B,G,R,reserved. Dedicated input consumes size−4 after the size word; generic input always uses the 40-byte shape. |
| Targa | 18-byte header: ID length/type/image type at 0/1/2, color-map-first/count words at 3/5, map depth 7, origin words 8/10, width/height words 12/14, pixel depth 16, descriptor 17. Top-origin=32, right-origin=16. |
| PCX | 128 bytes; manufacturer/encoding/depth at 0/2/3, signed max x/y words at 8/10, planes byte 65, signed rowBytes word 66. Pixels start at 128; palette marker 12 and 768 RGB bytes. |
| LBM | FORM plus big-endian length and four-byte form type; chunk tag plus big-endian length. BMHD: big-endian width/height words 0/2; planes/masking/compression bytes 8/9/10. Its nominal full size is 20. |
| Color file/CTAB | Signed 32-bit first,count, then RGB triples. Explicit-range CTAB ignores the two stored values. |

The sheet writer emits file size 308278, pixel offset 1078, reserved header
words 0; DIB size 40,width 640,height 480,planes 1,depth 8,compression 0,
image bytes 307200,resolution/count/important fields 0. Its 256 reserved palette
bytes are the retained inputs described in §9.

#### Internal evidence adapter

- Original sprite rows are little-endian DWORD left,count followed immediately
  by count bytes; headers can be unaligned. Extraction allocates eight extra
  trailer bytes, writes only the first four as zero and leaves the remaining
  four retained. Row positions are relative to full sprite origin, while the
  stream begins at the first content row. A replacement may store ordinary
  row objects if it preserves reads, pixels and lifetime effects.
- The cache has 16 records, each three DWORD metadata fields plus 1024 DWORD
  samples. Ticks compare as signed 32-bit values; ratios are unreduced. If no
  timestamp qualifies, candidate remains −1 and the original writes preceding
  graphics state. That is an **excluded overwrite**, even though the independent
  adapter allocates a 17th physical span containing scrollbar state. It is not
  a legitimate cache slot. Retain prior ratio/tick effects, then stop or use an
  explicitly repaired replacement policy.
- Extraction's three scratch arrays each hold 640 rows. More rows must not
  overwrite the arrays. A tree traversal has 512 scalar stack entries (two per
  saved continuation); excess depth must not overwrite palette/tree state.
  The planar LBM helper has an 80-byte private row buffer.
- Palette flags Ordinary=0, Animated/ReservedMatch=1, NoCollapse=4; native
  descriptor version is 768 with 256 entries. The mode-0 fallback RGB triples
  for entries 0..9 are (0,0,0), (128,0,0), (0,128,0), (128,128,0), (0,0,128),
  (128,0,128), (0,128,128), (192,192,192), (192,220,192), (164,200,240).
  Entries 246..255 are (255,251,240), (160,160,164), (128,128,128), (255,0,0),
  (0,255,0), (255,255,0), (0,0,255), (255,0,255), (0,255,255), (255,255,255).
- Surface modes 1/2/3 correspond to indexed/RGB555/RGB24 bytes per pixel in
  copy paths. Indexed fill still counts bytes without a mode multiplier.
- Horizontal raw line fill shifts only the low 16 bits of count to compute
  four-byte-group count. If that shifted low word is nonzero, its upper word
  remains from the original count; if zero, it omits that loop. Two/one-byte
  remainders use original low bits. This scalar quirk is distinct from any
  resulting unsafe write. Raw zero-row copy/fill and zero-count vertical fill
  execute once before an unsigned decrement wraps; stop before an overrun.
- GIF dictionaries use three 4096-word tables; absent prefix is 65535. Only the
  character's low byte is emitted. Width/height/stride narrowing, doubled
  identifiers and signed-address comparisons in §8 are original behavior.
  GIF file/resource code arguments retain stride's upper 16 bits; compact
  code arguments retain the header pointer's upper 16 bits. Low code size is
  zero-extended into the low word. Ordinary ranges make those upper bits
  irrelevant to the narrowed widths and masked shifts; do not assert that
  arbitrary off-nominal inputs were normalized.

All palette/span/surface/dictionary/buffer/object writes beyond their logical
objects are excluded bugs, including invalid sizes after arithmetic wrap,
overlarge BMP headers, resource Targa's extra frame and cache candidate −1.
Do not reproduce an overwrite by naming its victim as legitimate state.
Merge's omitted gaps and short header/provider outputs are read-definition
boundaries: supplied established bytes or a stop, never invented black pixels.
Read-only GIF descriptor writes, explicit cursor initialization and deterministic
BMP reserved bytes are separately identified repairs, not hidden original facts.

### Appendix C. Validation record

The historical research bundle included pinned source hashes, extracted bodies,
fixture adapters, original-byte probes and documentation checks. That scaffolding
is not included in this repository or required for implementation.

The harness compiles **45 unchanged whole bodies including helpers** from six
modules: sprite scaling, GIF decoding, palette-tree matching, raw copy/fill,
ordinary/colored sprite rows, and palette generation/commit. Static bounded
allocations, low-address tokens, clock/RNG providers and native palette calls
are explicit adapters. No original executable code or production GUI is run.

Optimized and ASan/UBSan builds both pass **45 assertions**: 13 scale checks,
12 GIF stream/status checks, ten palette checks, eight raster checks and two
original-asset checks. Cases include shrink/enlarge/distance rounding, clamping,
paired cache selection and tie behavior; block boundaries, clear/end, repeated
prefix, padding and all five kernel statuses; nearest-tree traversal/ties and
zero-generation retry; overlap groups, orientation, wider fill colors and keyed
or colored scaled pixels. The actual original 640×480 TERRAIN1.GIF compressed
stream is decoded through the extracted wrapper/kernel, and all **307200 pixels**
match an independent decoder after the classic palette offset. Asset SHA-256:
`1562bfaf6b0a2982c00f7dc59dac7c5e990a0cca44d2d8f071982195b2fac248`.

These checks do not execute complete sheet extraction, file/resource loaders,
native palette/display services, merge, fades, alternate formats or the paired
native child. Their descriptions rely on the pinned source evidence, with
selected original instruction checks for discriminating behavior. §11 lists
the remaining integrations. Source accounting, bounded fixtures and full runtime
compatibility are separate claims.

Source checks confirm 45 pinned independent files, all 45 extracted fixture
bodies, four primary files, ten original instruction probes and the original
GIF-comparison literal and asset identity. Repository checks validate document
links/anchors and specification IDs.

### Appendix D. Original presentation reference

This appendix retains the original presentation procedures removed from the main
specification. Geometry, pixel/sample output, native controls and cosmetic timing
are optional reference behavior under X01. Required semantic fragments were kept
in the numbered main sections and are linked where omitted here; this appendix
does not relax their simulation, information, file or protocol obligations.
Original section numbers and local § references identify the recovered procedure
family, whether its retained fragment is in the main section or here. Historical
native/pixel tests are reference validation, not baseline acceptance criteria.

#### Original §4. Sprite scaling and sampling

##### Ratio selection and table generation

A single scale has numerator and denominator, each independently clamped to at
least 1. Map scale selection supplies (wrap32(zoom+8),8); reset selects (1,1).
This is distinct from P01's coordinate arithmetic.

Generate an ordered source-index lookup L for destination indices using:

    source = 0; i = 0
    accumulator = 0 if numerator < denominator else numerator
    while more entries are needed:
        while i is within the required table
              and (accumulator >= 0 if shrinking else accumulator >= denominator):
            L[i++] = source
            accumulator -= denominator
        accumulator += numerator
        source += 1

All scalar operations follow F01. Nominal 3/8 produces
0,3,6,8,11,14,16,…; 2/1 produces 0,0,1,1,2,2,…. Do not replace shrinking
with floor(destination×denominator/numerator).

The original selects among cached tables using signed timestamps. Publish
the clamped ratio, capture current tick as oldest, then scan slots in ascending
order. An exact, unreduced numerator/denominator match gets a fresh tick and
returns immediately. Otherwise a timestamp<=oldest becomes the replacement
candidate, so the last equal timestamp wins. Publish that candidate's ratio
and fresh timestamp, generate its table and select it. Reads after tick
callbacks are live. In the single-scale path the generation loop reads the
shared ratio after the timestamp callback, so that callback can change the
generated table even though its cache key was already stored. The paired-table
generation helper instead captures its ratio arguments before its timestamp
callback. Keep that distinction in a reentrant adapter.

Paired selection repeats this for the horizontal scale, then the vertical
scale. The second may reuse an exact first-table match, but excludes the first
slot from replacement candidates. Initialization clears ratio/timestamp metadata,
selects identity once for the ordinary scale, then identity for both paired
scales. Cache capacity and the no-candidate overwrite are in Appendix B.

##### Distance through a table

For distance(value,base,roundUp):

1. Zero value returns zero without lookup reads. Capture its sign and take
   wrapping absolute value.
2. Set target=wrap32(L[base]+magnitude). Starting at base, find the first
   index whose signed lookup value is at least target.
3. When shrinking and roundUp is false, decrement the index if its lookup
   value is not exactly target.
4. Return index−base, negating for a negative input.

Content-origin positions use roundUp; sprite offsets and content extents use
the other form. At 3/8, distance(1,0,false)=0 and distance(1,0,true)=1.
A missing lookup entry is a read boundary. Do not silently extrapolate
arbitrary corrupted tables.

#### Original §6. Sprite extraction, lifetime and drawing

[Required behavior is retained in the main section.](#6-sprite-extraction-lifetime-and-drawing)

##### Initialization, replacement and copying

Initialization makes both rectangles empty, offsets zero, key zero, storage and
active access empty, and the two retained scale attributes 1. Destruction, when
storage exists, unlocks active rows then frees the live storage handle without
clearing every field. Reset independently unlocks active rows, then frees
existing storage and initializes, even if incoming handle/access disagree.
Access acquisition locks
only when no active rows exist; release stores the unlock result as active state.

Replacing one stored color acquires access if needed, then warns; after those
callbacks it captures content height and active rows. For each row, skip its
left field, read its count and replace matching payload bytes in order. The key,
geometry and row trimming do not change. Restore inactive access only when this
operation acquired it. Creating a solid transparent sprite builds/fills a
temporary surface, extracts using its top-left color, clears content once more
and destroys the temporary surface.

Copy/merge takes source, destination and displacement (x,y); its extra flags
argument is ignored. If source has no storage, return. If destination has none:

- Copy source bounds, adding x only to right and y only to bottom.
- Copy content and offset it by (x,y); add displacement to source offsets.
- Copy the two retained scale attributes and key. Duplicate source storage into
  the destination, then acquire it, including after duplication failure with
  the retained handle.

For a destination with storage, let sTop=source.content.top+y−destination.content.top,
sHeight and dHeight be content heights, and sRight/dRight the respective content
right edges. The temporary merge has left=top=0,
width=max(sRight+x,dRight), height=max(sTop+sHeight,dHeight). Acquire source then
destination, then allocate output backing. Walk those output rows in order:

- A row in neither input is empty.
- A row in just one input copies that row's payload and left; add x to a
  source-only left.
- A row in both inputs declares the union of their spans. Walk columns from
  0 through mergeWidth−1. Where old destination exists, copy its next byte.
  Where source exists, compare its **signed-byte** value to the source's
  zero-extended key. If unequal, replace output with source; if equal and no
  destination byte was written, write the destination key. Advance output only
  at columns covered by either span.

That signed comparison makes source indices 128..255 unequal to every
zero-extended key. Disjoint spans omit gap bytes even though the declared span
includes them; Appendix B treats later unproduced reads explicitly. Do not
silently fill those gaps while claiming original compatibility. Finally offset
the temporary rectangle by the destination's current content origin, publish
it as both content and full bounds, release/free old destination storage,
unlock output, install it and acquire it. Destination offsets/key remain live.

##### Ordinary and colored drawing

Choose the sprite's stored key only when the draw argument is −1; otherwise
use the argument's low byte. Capture source origin relative to full bounds:
sx=content.left−bounds.left, sy=content.top−bounds.top. If either full dimension
times the live scale numerator, wrapped then divided by denominator, exceeds
1024, report and return an empty rectangle. Negative results do not pass that
upper-limit test.

Otherwise use §4's distance operation D:

    scaledX = D(sx,0,true); scaledY = D(sy,0,true)
    left = scaledX + D(offsetX,0,false) + requestedX
    top  = scaledY + D(offsetY,0,false) + requestedY
    height = D(contentHeight,scaledY,false)
    width  = D(contentWidth,scaledX,false)
    rectangle = (left,top,left+width,top+height)

Intersect with the destination clip. Only a successful intersection draws.
Capture clipped width, then height, and the displacement of clipped left/top
from the original position. Read destination height, orientation, pitch,
current lookup, pixel storage and sprite rows in that order. The ordinary
port draw treats orientation==1 as positive pitch; the colored port draw uses
orientation!=0. The nominal backend returns a Boolean. Both paths require
already active sprite and port backing; they do not acquire it themselves.

For each clipped destination row, its selected source row is
L[scaledY+clipY+row]. Starting at sy, advance through row records until reaching
that selected row. Read its left and count. For each destination column use
L[scaledX+clipX+column]; skip indices before left, stop once index−left>=count,
and sample the corresponding payload byte. Ordinary drawing writes that byte
when it differs from key. Colored drawing writes the supplied color's low byte
instead, under the same transparency test. Source origin sx is not separately
added to the column sample. Empty payload rows write nothing.

For top-down storage the physical first destination row is y; otherwise it is
height−1−y. Advance by signed pitch after each row. The raw kernel performs one
iteration even for incoming height 0; ordinary intersection prevents that call
for empty geometry. Its initial skip of columns before the span has no
remaining-width guard, so malformed lookup/span reads still need backing.

After drawing or the no-draw branch, store output left, top, right, bottom in
that order. Output may alias other state: these stores happen after all pixel
effects. DirectDraw destinations use the same geometry, then lock pixels only
after successful intersection; a failed lock omits drawing. On success read
height/pitch/lookup/rows, draw and release the live surface before writing the
returned rectangle. Paired-axis drawing has the limitation in §11.

#### Original §7. Surfaces, clipping and raster primitives

##### Allocation and orientation

Replacing a surface releases its active access and old backend before testing
new geometry. Null or zero-width/height rectangles initialize an empty surface
and return success. Initialization clears active/backend/palette generation and
stride state, selects indexed mode, sets dimensions, full bounds and clip; the
old row-table token is retained. Negative or otherwise unsupported dimensions
can fail backend allocation, report and return failure after the old surface
has already been released.

Nominal indexed backing uses positive dimensions and row stride
4×trunc(wrap32(width+3)/4). A shared orientation value equal to 1 requests
top-down storage; otherwise bottom-up. Native creation supplies the actual
pixel backing and failure results; no blanket initialized-pixel assumption is
valid. Palette metadata initially describes black. The first backend allocation
also performs its shared one-time capability/reporting work.

Acquiring port access publishes active pixels before allocating its row-index
table. For top-down storage row y has offset y×stride; for bottom-up it has
(height−1−y)×stride. Signed next-row stride follows that orientation. Releasing
active access clears it through the backend release, then disposes the row
table. It does not erase the retained row-table token. Vertical flip exchanges
entire physical rows, including padding, through three copies per pair; height
and stride reads remain live around those copies.

[Required behavior is retained in the main section.](#7-surfaces-clipping-and-raster-primitives)

[Required behavior is retained in the main section.](#7-surfaces-clipping-and-raster-primitives)

[Required behavior is retained in the main section.](#7-surfaces-clipping-and-raster-primitives)

#### Original §3. Palette state, publication and matching

##### System initialization and generation

A palette contains 256 entries. Query the device's palette capability. If it
is absent, set reservedCount=0 and shared paletteMode=0. Otherwise set
reservedCount to the signed number of reserved device entries divided by two
and request all 256 system entries. A capable device does **not** restore
paletteMode to 1. A partial provider result leaves unproduced entry bytes as
retained inputs.

Initialize low reserved entries' flags to Ordinary; initialize the middle
entries to black with NoCollapse flags; initialize high reserved flags to
Ordinary. In mode 0, overwrite the first ten and last ten RGB triples with
Appendix B's fallback colors. Release the device context before creating the
native palette, which happens only in mode 1.

Then renew the generation:

    generation = 0
    while generation == 0:
        generation = primaryNext() & 32767

Store zero target RGB and clear owned fade/saved-color handles. Other retained
fade fields are not initialized by this constructor. Native-handle failure does
not remove the subsequent generation selection.

[Required behavior is retained in the main section.](#3-palette-state-publication-and-matching)

##### Nearest-color tree

Palette conversion uses this particular tree/search, not a global scan for
minimum RGB distance.

To build a tree from count RGB triples and output offset first:

1. Choose the first color with smallest squared distance to (128,128,128).
2. Insert colors in order: chosen index down through 0, then chosen+1 through
   count−1. Each node retains its original input index.
3. At the root compare Green; subsequent depths cycle Red, Blue, Green.
   Strictly smaller components go left; equality goes right.
4. Mark the tree ready after all insertions.

Searching unready state returns −1. Otherwise set bestDistance=196608 and no
candidate. Construct fixed lower/upper bounds for each component by subtracting
and adding the supplied radius with signed wrapping. Traverse depth first,
starting at the root with axis Green:

- If lower[axis] is at least the node's component, visit only its right child.
- Otherwise, if component<=upper[axis], evaluate the full squared RGB distance
  and schedule the right child after the left subtree. Replace the best only
  for strictly smaller distance.
- In that latter lower<component branch, visit the left child regardless of
  whether the upper comparison admitted the current node.
- Stop immediately on distance zero. Ties retain the first visited candidate.

The test is local to the current split axis; do not introduce a new all-three
components box test. Return 0 if no candidate, otherwise original input index
plus first. The ordinary lookup and remap builders use radius **32**.
The strict lower bound means radius 0 does not admit an exact split value.

A same-range map builds from the destination palette range, rereads the source
palette's corresponding RGB range, then writes low-byte search results in
source order. The separate-range form uses independent source and destination
ranges. Single-color lookup builds from all 256 entries. Tree construction,
traversal, allocation and release have no RNG draws. Excess traversal storage
is an excluded overwrite, not a permission to return an invented candidate.

[Required behavior is retained in the main section.](#3-palette-state-publication-and-matching)

#### Original §11. Integration boundaries and conformance

##### Paired-axis native raster boundary

Paired drawing selects independent §4 ratios/tables and computes x/y origins,
offsets and content extents with their respective distance operations. It clips
to the destination and prepares orientation, pitch, pixels, row records and both
lookups. The port path treats orientation>=1 as top-down; the DirectDraw path
locks pixels. Up to that point its inputs and effects are established.

The original calls a separate raster entry whose recovered standalone form uses
16-bit segmented row records and lookups. The Windows caller supplies the
ordinary 32-bit records/lookups; the required selector/format connection has
not been established. The independent reconstruction deliberately stops at
that child call. This specification **does not invent an anisotropic pixel
result** from the ordinary kernel. A cleanroom implementation claiming that
path needs a supplied native binding or further evidence for the conversion
and call convention; until then stop at the child after the valid geometry
prefix. The standalone segmented kernel alone is not proof of this join.

##### Finite checks and remaining joins

An implementation should compare pixels and ordered effects separately. Useful
discriminating cases include:

| Case | Expected distinction |
| --- | --- |
| Scale 3/8 versus 2/1 | Ceiling-like shrinking samples versus repeated enlargement samples; distance origins and roundUp matter. |
| Equal cache timestamps | Last replacement tie wins; an exact unreduced ratio match wins immediately. |
| Empty palette commit, disabled native mode | Select a nonzero generation token, even without a native palette call; raw RNG advances are unspecified. |
| Palette tree radius 0 or a boundary color | Strict lower split bound; no replacement by global nearest-color search. |
| Transparent interior, clipped/colored sprite | Trimmed spans retain holes; key filtering happens after lookup sampling. |
| Overlapping copy by one byte | Forward four-byte groups propagate differently from byte-only copying and a snapshot. |
| GIF end/error after partial output | Dictionary/palette/pixel prefix survives; outer loaders can return success. |
| GIF local table/interlace/extension | Accepted header bits do not implement those standard features. |
| BMP RLE EOL then delta | Stale horizontal cursor, vertical-before-horizontal delta; supplied initial cursor. |
| Generic BMP palette versus sheet BMP | Per-entry commits versus one range commit, hence different generation publications. |
| Targa RGB24 depth 24 | Repeated first pixel, while depth 32 advances. |
| Unknown read or out-of-bounds write | Supplied backed input or stop for reads; every overwrite remains an excluded bug. |

Remaining integration work is finite: run the complete sheet parent against
actual BMP/GIF fallback files, compare extracted pixels/markers and palette generation
trace; join P01/P02 renderers at their zoom thresholds with actual scale/clips;
join S04 imports/exports including failure prefixes; compare native device
palette realization, diagonal lines, stretch and DirectDraw access; establish
the paired-axis child connection; and exercise P04/P05/P10 reentry around
allocation, palette, message and presentation services. These are distinct from
the bounded kernel validation below. Platform raster services and unavailable
incoming bytes remain explicit inputs, not guessed implementation details.

#### Original graphics object state and observations

Observe resource-search order, current-directory changes, published palettes
and sprites, raster pixels, clipping, returned rectangles, active storage,
palette generations, controlled choices or presentation random context, message
pumps, reports and partial
effects. Asset loading is not transactional. An unsuccessful fallback does not
undo earlier palette imports, allocation or extraction.

Logical state consists of:

- A surface's dimensions, full bounds, current clip, pixel mode, orientation,
  pixel backing, active access and cached palette generation.
- A sprite's full bounds, trimmed content bounds, origin offsets, transparent
  color, row payload and storage/access lifetime.
- Palettes of 256 RGB entries with entry flags, native handle and generation,
  plus retained fade state and shared system-palette mode/reserved count.
- Shared resource search prefixes, primary resource module, ordered additional
  modules, current directory, working sheet surface and asset families.
- Single-axis and paired sprite scale ratios and lookup tables.

#### Original palette flags and native commits

Writing one RGB entry stores red, green and blue low bytes, then NoCollapse.
Scan the low reserved entries and then high reserved entries; exact RGB equality
to any reserved color changes the written entry's flag to ReservedMatch.
The comparisons read live entries after the stores, including an entry that
is itself reserved.

A commit of (first,count) first applies only the upper clamp:

    if wrap32(first + count) > 256:
        count = wrap32(256 − first)

In paletteMode 1, submit that range to the native palette; otherwise omit the
native call. **Always renew generation afterward**, including empty ranges,
negative accepted counts and disabled native palette mode. Invalid entry writes
or oversized native spans must stop before an excluded overwrite.

#### Original palette assignment and effects

Assigning a palette to a surface unconditionally reads the palette generation,
including when a null palette was supplied. If different from the surface cache,
publish all 256 entries to the backend and reread generation after that callback
before caching it. Equal generations skip entry reads. The separate
update-if-present helper first rejects null. Do not merge those contracts.

Window palette assignment publishes the palette reference before reading its
native handle. Even mode 0 performs that handle read. Mode 1 then stores the
handle in the backend and selects it into the window device context. P04 owns
window activation and realization around those operations.

##### Rotation, repetition, scaling and color files

Repeat-color clamps the range as above but takes its source from entry
**count**, not entry first. Each destination receives a fresh read of that
entry, so earlier writes can affect later colors. Commit afterward.

Rotation performs the requested number of individual one-position shifts.
Negative distance shifts toward smaller indices; positive shifts toward larger
indices. Save the displaced endpoint RGB, move entries in direction-safe order,
then install the saved endpoint. Animate the native range only in paletteMode 1.
Rotation does not renew generation. Keep repeated shifts and live accesses
when callbacks/aliasing make them observable. The negative-loop bound uses
wrapping negation; distance=−2147483648 performs no shifts, then animates.

Color scaling captures an RGB range and stores first/count/steps, optionally
first storing a target RGB. Mark the range animated. For a step in [0,steps],
each channel is:

    low8(trunc(originalChannel / steps) * step
         + trunc(targetChannel / steps) * (steps − step))

Division precedes multiplication. An out-of-range step reports and returns;
steps=0 at step 0 reaches division failure. Scaling/rotation write RGB while
retaining existing entry flags. Marking animated entries sets their flags to
Animated and commits once, including its generation draw. Animate after scale
step writes, then release temporary access. Completion imports the saved
original RGB, unlocks, **then runs repeat-color**, then frees the saved backing.
This final repetition is not equivalent to merely restoring the original colors.
Replacing saved scale RGB copies the requested count of triples into that
backing without changing the saved count or committing.

Palette-to-palette fade stores first/count/steps, allocates original then target
RGB buffers, captures/unlocks the original range then the target range, and marks
the destination range animated. Its step uses the same division-before-product
formula with each target entry's channel instead of one target color. Lock
original then target. Per entry evaluate target/original red, original/target
green, target/original blue; store RGB while retaining flags. After all entries,
submit the live range through the native set-entries service in mode 1,
**without a new upper clamp or generation renewal**, then unlock original and
target. Out-of-range steps report/return; zero-step division has the same failure
rule. Finish imports the captured original range, unlocks/frees original then
frees target. Unlike scale completion it does not repeat a color. P10 owns the
timing and window presentation surrounding these primitives.

#### Original DirectDraw sprite ownership

The DirectDraw-surface variant first locks pixels, returning immediately on
failure. It uses the same trimming rules with the surface's row accessor and
releases pixels on its corresponding normal/error paths. Its scratch-allocation
failure retains the acquired lock. Drawing to that kind of surface likewise
uses an explicit lock/release pair, described below.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### Appendix E. Original implementation reference

This appendix preserves original storage, allocation, native API and adapter
procedures for reference validation. The main text defines required behavior.
A different internal representation or platform is permitted; semantic identities,
ordered game effects, accepted input interpretation, file/wire values and required
failure/completion results remain governed by the main algorithms and X01.
Mixed excerpts retain context: their required effects are not made optional by
appearing here. Original memory/handle traces are not a second normative algorithm.
Optional providers and harmless UI failures retain X01's existing scope limits;
this reorganization introduces no new gameplay or interoperability relaxation.

#### Original sprite extraction storage procedure

Intersect the requested rectangle with the surface clip, ignoring the
intersection return value. Capture its width and height. If the surface has no
active access, warn and acquire it; remember that this call acquired access.
Obtain the first pixel address. A zero result reports invalid mapping, releases
access acquired here, and returns no storage.

The low-level key argument has three cases: −1 samples the first pixel as key;
−2 treats every sampled pixel as opaque; all other values use their low byte.
The ordinary rectangle wrapper first narrows its key to a byte, so it cannot
request either negative sentinel. The transparent wrapper supplies −1 directly.

Scan rows from the top, left to right, until finding an opaque pixel. Separately
scan from the bottom, left to right, to find the last nonempty row. If all pixels
are transparent, publish empty content, full bounds (0,0,width,height), zero
offsets and the selected key; −2 publishes key 0. Release access acquired here,
clear active sprite rows and return no new storage. The low-level operation
retains the old owned handle in this branch; its wrapper subsequently replaces
that handle with the returned empty value.

Otherwise allocate three scan-line scratch tables in order. Failure of any
allocation reports and returns immediately, retaining earlier table allocations
and access acquired here. For each row from first opaque row through last:

1. Begin with left=0,count=0. Find the first opaque pixel from the left.
2. Find the last opaque pixel from the right. The payload includes every pixel
   between them, including transparent interior pixels. An empty intervening
   row has count 0.
3. Update the global minimum left and maximum exclusive right from nonempty
   rows. Retain the source position, left and count for the later copy.

Release old sprite access and free old storage before allocating the replacement.
On failure, report, release source access acquired here and dispose all three
tables in allocation order. Previous geometry has not yet been replaced.
On success publish content (minLeft,firstRow,maxRight,lastRow+1), full bounds,
zero offsets and selected key. Copy rows in ascending order, then unlock the
new storage, release source access acquired here and dispose the scratch tables.
The low-level operation returns the new storage; only its wrapper installs and
acquires it. Appendix B records row packing and capacity limits for validation.

#### Original indexed pixel addressing

loads. Pixel addressing uses row-index[y]+activePixels+x in indexed mode.

#### Original color-replacement access management

Replacing a color in a rectangle acquires access if needed, clips and scans
rows/columns, replacing equal bytes; it restores access afterward if acquired
here. Its row-table access occurs even on an empty region, and extents/stride
remain live during the scan.

#### Original fill access acquisition

Indexed fill warns/acquires when inactive and **keeps** that access afterward.
Intersect with the current clip and return on zero width or height.

#### Original surface-copy access acquisition

Surface copy acquires source then destination when necessary, with warnings.
Intersect source and destination rectangles

#### Original surface-copy access release

snapshot nor a direction-changing overlapping copy. Release source before
destination when access was acquired here.

#### Original diagonal native pen procedure

The diagonal path asks the backend palette for the current indexed color,
creates/selects a one-pixel solid pen, moves to the first endpoint, draws to the
second using native line semantics, restores the prior object and deletes the
pen. Failed color lookup requires supplied RGB bytes or a stop before their
use. It does not establish black.

#### Original RGB line mechanism

coordinates themselves. The line clip handles those edges; do not subtract 1
in a replacement outline. RGB lines use the same coarse gate and native pen
sequence for every direction, with low-byte RGB components.

#### Original RGB brush procedure

RGB fill acquires and keeps access just like indexed fill. In RGB555 mode, each
pixel is ((red AND 248)<<7)+((green AND 248)<<2)+((blue AND 255)>>3).
Other modes select a solid RGB brush, fill the clipped rectangle through the
backend, restore the prior object and delete the brush.

#### Original stretch context ownership

These stretch parents acquire/restore source access when needed. Transfers to
DirectDraw obtain its device context and copy/stretch/release only on exact
native success. They do not implement §4's sprite lookup sampling.

#### Original GIF file and resource plumbing

The file loader opens, allocates file-size backing, pumps messages, then checks
allocation success. It locks, reads the live file size ignoring read success
and count, unlocks and pumps again; the allocated handle is then locked for
parsing. Require its first three bytes to be GIF. The resource loader instead
looks up type GIFS and locks it. Its original check rejects only a NUL-terminated
string **equal to GIF**; other strings pass this check. GIF87a/GIF89a pass.
This resource comparison needs a backed terminator or earlier differing byte;
do not fabricate unproduced string bytes.

Both require the global-color-map flag and derive colorCount from the packed
header's low three bits. If a palette is supplied, upper-clamp the requested
range as in §3, then import the unsigned minimum of requested count and
colorCount from the global RGB triples. File loading pumps once after the
header, additionally before and after palette import; resource loading pumps
once after the optional import. These palette effects precede descriptor errors.

Skip zero bytes after the complete global table. Accept comma **or exclamation
mark**, advancing over it and interpreting what follows as an image descriptor.
Extensions are not parsed as extensions. Capture its width/height; ignore image
offsets and logical-screen dimensions. A local-palette flag reports a warning
but does **not** skip the local table. Interlace is ignored. Replace the surface
with the captured dimensions; file loading aborts on allocation failure, resource
loading ignores that return. Read minimum code size only after allocation
callbacks, then pump, capture destination and stride, and invoke decoding.

After decoding, pump, assign the palette **unconditionally**, flip complete
physical rows if the backend is bottom-up, unlock/free input and return success.
File success explicitly closes before the file destructor closes again.
The caller ignores decompression failure. Bad signature/global table/descriptor
paths unlock/free input, report the corresponding format error and return 0;
missing file simply returns 0, while missing resource logs/reports first.
Short-read bytes retain the allocation's established backing, not invented data.

#### Original GIF dictionary allocation

Allocate three dictionary tables, each with 4096 entries. Failure logs and
returns 0. Initialize every entry's prefix to absent, character to

#### Original GIF dictionary teardown

The wrapper discards statuses 1..5, unlocks/frees tables and returns 1.

#### Original generic BMP resource cleanup

palette count do not redirect these reads. Resource format failure returns
without unlocking/freeing the resource; allocation failure frees without
unlocking. File error exits retain their destructor cleanup.

#### Original Targa mapping and teardown

Resource allocation failure frees without unlocking. File loading maps read-only
input, unmaps/closes after the pixel helper, then destroys the file object.
Missing/unsupported input reporting and all effects before failure remain
observable; a restrictive profile can reject unsupported types before decoding.

#### Original color-file resource ownership

Color files store little-endian signed first and count, followed by count RGB
triples. The default writer uses first=0,count=255. It captures RGB before
creating the file, writes both header values and triples, ignores write results,
closes, and frees captured storage. Failed creation leaves the earlier
allocation owned. The reader performs both header reads before allocating
count×3 bytes; missing header bytes need supplied input or a stop. Payload
short reads retain the allocator's established zero backing. Close before
importing and committing. A CTAB resource's default loader reads little-endian
32-bit first/count from its first eight bytes, then imports the RGB triples.
The separate explicit-range loader skips those bytes and uses the caller's
first/count instead. Both unlock/free the resource after import/commit effects.

#### Original GIF output-address limit

Set rowWidth=u16(width), padding=u16(stride−width), and the output limit to
destination+u16(height)×u16(stride)+1 under wrapping address arithmetic.
The original comparisons interpret output and limit as signed addresses;
Appendix B distinguishes that validation adapter from a logical surface bound.

#### Original GIF status checks against native addresses

5. If previous==clear, read this code's length, advance output and rowCount,
   and return status 3 if signed output>=signed limit. Otherwise set
   previous=code and expand that phrase backward into the output.
6. Otherwise store prefix[next]=previous and length[next]=u16(length[previous]+1)
   **before** testing output bounds. If code<next, advance by length[code],
   update rowCount, and return status 4 on the same output-limit comparison.
   Then set previous=code and expand backward.
7. For code==next, advance by length[previous]+1 and update rowCount; return
   status 5 on that comparison. Expand the previous phrase ending one byte
   before the new output position, set previous=code, and copy its first byte
   to the final position.
