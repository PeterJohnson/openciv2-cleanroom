# M2 Riverbend map validation

The slice covers the authored 56×32 doubled-coordinate map and the existing
`unit.stack-bottom` command. This fixture grants tile exploration, remembered
improvements, and unit detection independently. It displays the own city and
an anonymous remembered foreign city site. Richer foreign city memory and
the gameplay rules that publish knowledge remain W03/M3 work.

The pinned graphics release is 2026.11.07.1 and the font release is DejaVu 2.37.
Only the runtime package and upstream font bytes are inputs. See the
[asset contract](../content/asset-format.md) and
[desktop reproduction commands](../BUILDING.md#avalonia-desktop).

## Linux execution

Executed on November 17, 2026, on Ubuntu 26.04 x64 with .NET 11.0.100 / 11.0.0,
Avalonia 12.1.6 and SkiaSharp 4.156.1. The CPU is an AMD Ryzen AI 9 HX 370.
The tested working tree extends `9bc6e0176872d63504b2a206ecadbe3a3c9624b4`.
The runtime and font archive hashes matched the dependency pins before use.

- The headless and Avalonia Release builds and both solution format verifications
  passed. The native xUnit suite passed 459 tests, including the 437 shared
  groups and the new P03, P05 and W03 cases.
- The managed desktop and the Linux Native AOT publish each passed all 17 checks
  on an isolated 1020×720 X11 window at reported scale 1. XTest exercised native pointer and keyboard delivery,
  wrapped and vertical-edge picks, city and unit selection, drag, pan, wheel
  zoom, keyboard cancellation, console focus, confirmation, no-op repetition
  and decline. A pixel sample verified that the map painted. The host exited
  via the window-close protocol. Native startup rejected a same-length corrupt
  asset and a member symlink outside the mounted package. The report contains
  compiled source, package file and invocation hashes.
- The raster probe decoded 2,105 atlas bindings and admitted twelve font faces.
  It compared cached and dirty draws with a fresh full redraw at simulated 2×,
  checked texture release and viewport invalidation, suppressed an obsolete
  observer draw, and compared both RNG captures and map, unit and city records
  across rendering. The portable command tests independently cover cancellation,
  duplicate replies, target replacement while awaiting input and view revocation.

The browser host's C# compile target passed with its production settings. The
full browser bundle and runtime behavior were outside this execution scope.

## Software raster workloads

The probe draws on Skia CPU surfaces with the production renderer. The larger
world contains 23,808 tiles, 1,600 units and 180 cities before observer filtering.
Tile and object counts include prepared edge margins and horizontal copies.
Each workload warms once and records 16 iterations. Model projection and
frontend indexing are timed separately. A full draw rebuilds the viewport and
renders the first selection. A cached draw blits the retained image. A dirty draw
restores the previous selection rectangle, draws the new selection and blits the
result. The final viewport blit is included in all three timings.

Times are medians in milliseconds. The raw report includes all samples, p95
and texture-batch counts.

| Workload | Physical pixels | Zoom / scale | Tiles (distinct / prepared) | Objects | Observe / index | Full | Cached | Dirty |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| authored | 1020×720 | 1.25 / 1× | 480 / 480 | 6 | 0.037 / 0.002 | 16.31 | 0.067 | 0.081 |
| half-world-1080p | 1920×1080 | 0.31 / 1× | 12,214 / 12,214 | 485 | 0.676 / 0.171 | 43.53 | 0.346 | 0.328 |
| 4k-standard | 3840×2160 | 1 / 1× | 7,688 / 7,688 | 307 | 0.222 / 0.132 | 198.18 | 1.68 | 1.72 |
| 4k-zoomed-out | 3840×2160 | 0.25 / 1× | 23,808 / 30,008 | 1,185 | 0.231 / 0.256 | 62.41 | 1.61 | 1.59 |
| 5k-zoomed-out-2x | 5120×2880 | 0.25 / 2× | 20,088 / 20,088 | 793 | 0.213 / 0.081 | 166.19 | 3.65 | 3.57 |
| 5k-half-world | 5120×2880 | 0.83 / 1× | 12,214 / 12,214 | 484 | 0.169 / 0.049 | 273.13 | 3.54 | 3.65 |

The full-frame rebuild is the costliest step in the large views.
The 5K half-world full redraw reached 360 ms at p95.
The cache held 19 images using 10.76 MiB of pixel data.
Package admission took 283 ms, font pin checking took 6.2 ms,
and preparing the slice's textures and Skia typeface took 93 ms.
The exhaustive decoder pass processed 453,621,833 pixels in 9.98 s.
Peak probe RSS was 1,467,544 KiB across exhaustive decoding and all workloads.

These are offscreen physical pixel counts. They establish software raster costs
on this CPU. Physical monitors, calibrated DPI, GPU upload, compositor latency,
frame pacing and target-hardware budget acceptance remain open.

Local raw evidence under the ignored `build/m2/` directory:

- `linux-map-input.json`, SHA-256 `495ad4cf3f2593a2c3944de97dd0181db588a785affe6b4f07d792cfa41b3f39`
- `linux-map-aot-input.json`, SHA-256 `23cd5f488cbf612c0764f78cac8c8b285c9c65a7a7e388b85fdf8aae102c60a9`
- `map-raster.json`, SHA-256 `7116be6638ae73f9b661185be7f47ee38f7506eabc78484b854c4ab92752a241`

## Pinned feature sprites

The follow-up replaces the tick marks for rivers, roads, railroads, grassland
shields and special resources with pinned atlas art. W01 directions select
connections from explored terrain and remembered route flags. The additional
probe cases cover flat edges, wrapped joins, hidden neighbors, resource variants
and actual sprite pixels. All 17 managed Linux X11 checks passed on the updated
renderer. The capture below was taken from that managed desktop on isolated X11.

The cache now holds 76 images using 30.63 MiB of pixel data. In the same software
probe, the authored view used 27 batches and took
19.16 ms for a median full redraw. The 5K half-world full redraw
took 322.61 ms. That workload's cached and dirty draws took
3.41 and 3.62 ms, including the final blit.

Local follow-up evidence under `build/m2/`:

- `feature-linux-input.json`, SHA-256 `9be3f9a763582638485dc0fc6e8bfae82342d47022ed9d1f34e56ab945a45c88`
- `feature-raster.json`, SHA-256 `819b6cc76e10014280caa2af6cf9e8c8a9a0b453657094c29d09ad9367b49159`
- `riverbend-feature-art.png`, SHA-256 `b08773443d84571644949851fe76dc0efd5941b26d909df670e36d5028750a6a`

### Riverbend feature layout

The authored river now follows a continuous course from the northeast, crosses
the road, meets a tributary, and reaches the western gulf across the horizontal
seam. The Linux matrix verified the exact pinned art bindings at the crossing,
the confluence, the wrapped mouth and the first forest resource. The 1020×720
native capture shows the new course. The X11 application suite passed 22 checks
each at 1× and 2×, including pointer inspection of the crossing and resource.

Local evidence under `build/m2-native/`:

- `riverbend-course-input.json`, SHA-256 `0e5056e57b935d170053468662cdbb0b72bb53d5584462e0ef723f000faffc83`
- `riverbend-course-input-2x.json`, SHA-256 `40160d911dd89bfadafac2eabf399f7ccd230a2242f28fdbee1b73bf92c538ea`
- `riverbend-course.png`, SHA-256 `6f61410c0f67dc30bd322a3a8501bdca8fbd9755d65f2c260f316eb45f607ade`
- `riverbend-course-matrix-p03/matrix.json`, SHA-256 `d53f49b49e19d2691110690e4961573077fd7a00831918be3006f148b56151c9`

The rebuilt Release desktop was launched with and without the pinned package
and fonts on isolated Linux X11. The stock capture shows the river, road and
special resources. The pale icons on grassland are the production-shield art.
The launch without content displayed the limited preview. The pinned launch
passed 22 input checks at each density, including crossing and resource picking.
Follow the [desktop launch instructions](../BUILDING.md#avalonia-desktop) to
select the stock art for a manual run.

Local evidence under `build/m2-native/pinned-manual/`:

- `riverbend-stock-art.png`, SHA-256 `c81edcf5327508b502e4dd1b66438a8ab03e4b8d33cc6b2ec97fc407334e10ec`
- `riverbend-limited-preview.png`, SHA-256 `471ba418fed1f387fe33cbff1702f0f9c5001db6b72b906e4a5033e069c7f0ac`
- `input-1x.json`, SHA-256 `34fa99fe8645f995332ed0e536e8dea71583df44a82d7f8a8b696d61b43e4d61`
- `input-2x.json`, SHA-256 `37b38373c9018009bb20b5c8ad9b43049a2bdb1b816a39fa92fee392f6110915`

## Remaining M2 gates

- Browser pinned-content delivery, startup/memory and runtime input/rendering.
- Actual Windows and macOS interaction, mixed DPI and fullscreen on all target
  desktops, accessibility and IME, and physical 4K/5K GPU and frame-pacing evidence.
- Native menu tracking around deadlines, asynchronous file choice and persistence,
  fake network/deadline and silent media service acceptance, and platform
  interruption and shutdown checks beyond the isolated Linux window close.
- Overview navigation, more complete art and layout bindings, classic/scenario
  graphic precedence and decoding, and broader font roles, shared measurement,
  glyph fallback and text coverage.
