# M2 browser content and map acceptance

Execution date: **2026-11-20**. This record covers the pinned browser content
adapter and the [MV01–MV30 acceptance matrix](../testing/map-rendering.md) in
WASM, with the production Avalonia map presented through WebGL2. Hardware
performance, other browser engines, native platform services and live gameplay
producer joins retain their separate gates.

**Result: MV01–MV30 browser functional acceptance passed for this executed
Chromium/WASM/WebGL2 backend.** The shared exhaustive matrix passed, and six
completed stock/native/classic × 1×/2× gallery batches passed **367 checks**.
All **229** presented map frames matched fresh WebGL references at zero tolerance:
**186** gallery frames plus **43** navigation, display and recovery frames.

## Implementation and reproduction

`OpenCiv2.Browser` now admits same-origin HTTP streams through the asynchronous
`PinnedGraphics` readers before creating the application. It verifies the compiled
release manifest, exact sizes and SHA-256 hashes for every runtime file, all twelve
DejaVu faces and the license. The atlas has one owned buffer; ancillary release
files are hashed incrementally. Browser source streams need no synchronous reads.
The bounded optional art index preserves case-independent native/classic lookup,
the existing BMP/GIF fallback and F03/P03 precedence.

The [build guide](../BUILDING.md#browser) gives publish, content staging and
acceptance commands. `stage_browser_content.py` validates source pins before
copying data. `?showcase=MV01&observe=1` opens the shared authored gallery;
`&art=native` or `&art=classic` selects the generated replacement fixtures.
Ordinary startup opens the scoped Riverbend application with pinned content.

`OpenCiv2.MapBrowser` links the production entry/content adapter and shared
`NativeMapMatrix` cases. Its only additional UI instrumentation is a read-only
fresh-frame capture using a temporary lease of the production WebGL context.
The fresh painter does not reuse the displayed map's cache. Reflection metadata
is retained for anonymous test evidence; the production host is separately
published with trimming and exercised with real pointer/keyboard commands.

The matrix's native worker race cannot execute in the single-thread WASM host.
That one check is explicitly reported as inapplicable; sequential stale-generation,
observer replacement, privacy, cache and resource cases still execute. Native
execution retains the worker race. Exact source-mask tint checks run before the
silhouette check. The latter requires the exact neutral color to be present,
rather than a native-specific count of fully opaque pixels after downsampling.
No pixel-comparison tolerance was introduced.

## Executed environment and receipts

- .NET SDK **11.0.100**, runtime **11.0.0**, browser-wasm Release.
- Node **24.19.0**, Playwright **1.58.2**, Chromium **151.0.7922.34** on Linux.
- WebGL2 through ANGLE/SwiftShader. This is software-backed functional evidence,
  not a physical GPU or interactive-performance pass.
- Runtime assets **2026.11.08.1**, archive SHA-256
  `093e791370a3a3724e0acc5b2c08dfbeff22e7efb9b5e9ac77b0f1da839539d4`;
  DejaVu **2.37** from the compiled font inventory.
- Browser CSS viewport **2250 × 1650**, backing density **1× and 2×**. Chromium's
  process scale and context scale agree, so Avalonia sees the intended logical
  extent. Resizing and a simultaneous independent map exercise smaller extents.

Local raw receipts are under `build/m2-browser/`:

| Receipt | Scope |
| --- | --- |
| `accepted-matrix/matrix.json`, `accepted-matrix/wasm/` | Shared exhaustive WASM semantics, selectors, authored observations and raster comparisons |
| `accepted-gallery/browser.json` | Actual gallery input, startup requests, densities, themes and compiled assembly identities |
| `accepted-gallery/pixel-comparisons.json` | Exact presented/fresh map comparisons and independently computed overview outlines |
| `accepted-gallery/visual-review.json`, `review-*.png` | Source-bound visual review and contact/detail sheets |
| `accepted-gallery/THEME-Nx/THEME-Nx-MVnn.{png,json}` | Actual WebGL screenshot and its observed state for every row |
| `accepted-gallery/THEME-Nx/*-fresh.png` | Fresh reference on the same WebGL context |
| `accepted-transitions/browser.json`, `pixel-comparisons.json` | Additional exact WebGL comparisons after pan, zoom, texture reload, resize and display-color changes at both densities |
| `accepted-gallery/stock-1x/context-loss.png`, `admission-*.png` | Visible graphics/content failure states |
| `accepted-production-input.json` | Trimmed production application command/decision checks at both densities |
| `accepted-foundation.json` | Portable foundation cases, including asynchronous admission failures |
| `accepted-bundles.json` | Size and SHA-256 inventory of both executed publications, including staged content |

Matrix records bind P02, the acceptance/performance contracts, the exact catalog,
authored fixture generator and all compiled source inputs. Reports do not acquire
new source identities when source files change. CI repeats the publication,
staging, matrix, gallery and pixel checks and retains test results as artifacts.

The long initial gallery process was terminated before writing its final receipt.
It remains diagnostic evidence under `diagnostic-gallery-interrupted`; the final
result reruns every batch with atomic incomplete checkpoints and separate final
receipts. The merger rejects incomplete runs, missing rows and differing build,
browser or driver identities. The pixel checker also rejects an incomplete run.
The final gallery receipt SHA-256 is
`141c7f3070791cd1860b8b7562046e13e76a7539088c3b491bff2da8805ee820`;
the combined pixel receipt is
`340a9dd815b8eda31df1fed61d3f8388d8429c7863b84b76272e0412f2d52de2`.

The focused transition run passed **65 browser checks** and **15 exact WebGL
comparisons**, including an independent zero-chroma check of greyscale map pixels.

The shared matrix passed **31 boards**, **317 explicitly asserted sprite
identities**, and its detailed semantic/pixel families, retaining **291 evidence
files**. It also passed **108 presentation frames** with exact retained/fresh
equality. `accepted-matrix/matrix.json` SHA-256 is
`988f40cc95a8f90137bf7cdb6352d5f03be0cb062b5b7706be5d3055a54a1065`.
The trimmed production host passed **16** actual input checks. The final native
core suite passed **469** tests; the current source-validated WASM foundation
report passed **447** portable groups. Formatting, graph/coverage checks, seven
identity-validator tests, ten notice tests and workflow validation also passed.

The instrumented matrix host requested **296,844,614 bytes** of pinned runtime
content and **5,358,176 bytes** of fonts before optional art/acceptance data. Its
loopback startup observation was **10.30 seconds** while other validation work
was running. These values describe this untrimmed functional test environment;
they are not production network, memory or hardware performance budgets.

## Per-row WASM execution

Each row passed its authored cases and applicable shared matrix checks. The
six WebGL gallery captures per row add production presentation evidence;
their theme, density, camera, display and observer state are retained in JSON.
The `Mixed` world is additional combined coverage. Live-provider status and
owning milestones remain in the existing visual ledger.

| Row | Shared case family | WASM result |
| --- | --- | --- |
| MV01 | Eleven terrain types | Passed |
| MV02 | Shoreline contact combinations | Passed |
| MV03 | Relief and terrain transitions | Passed |
| MV04 | River masks, mouths and crossings | Passed |
| MV05 | Both resources per terrain and the grassland shield | Passed |
| MV06 | Irrigation, farmland and mines | Passed |
| MV07 | Road, rail, mixed and city endpoints | Passed |
| MV08 | Villages, pollution, fortresses and airbases | Passed |
| MV09 | Unknown edges and intersections | Passed |
| MV10 | Unit bodies and leader | Passed |
| MV11 | Faction shields and stacks | Passed |
| MV12 | Health and withheld inputs | Passed |
| MV13 | Orders, waiting, automation and veteran | Passed |
| MV14 | Sleep, sentry and fortified units | Passed |
| MV15 | All city appearances | Passed |
| MV16 | City names, flags, garrisons and disorder | Passed |
| MV17 | Civilization colors and overlays | Passed |
| MV18 | Selection and active focus | Passed |
| MV19 | Grid, radius, ownership and bare terrain | Passed |
| MV20 | Authored route and trade lines | Passed |
| MV21 | Worker counts and inspection | Passed |
| MV22 | Authored viewport notifications | Passed |
| MV23 | Overview and wrapped footprint | Passed |
| MV24 | Authored movement and combat effects | Passed |
| MV25 | Camera, presets and picks | Passed |
| MV26 | Detail bands and density | Passed |
| MV27 | Crowded names and long labels | Passed |
| MV28 | Overlapping tall alpha layers | Passed |
| MV29 | Dirty updates and replacement | Passed |
| MV30 | Display correction and texture recovery | Passed |

## Comparison and failure semantics

Map captures compare RGB pixels at **zero tolerance**. Only the inset overview
control and its three-pixel border are excluded from the map comparison; each
overview's visible outline is checked independently from camera/world geometry.
The shared matrix supplies the separate disclosure, owner-color, selection,
wrapped/flat geometry and detailed feature assertions. Static gallery screenshots
use reduced motion; browser input and shared matrix cases separately run effects,
notices, cancellation and reduced-motion transitions.

Visual review covered all stock/native/classic 1× contact boards, the classic
2× boards, and enlarged stock/native 2× unit, crowded-label, occlusion and mixed
scenes. The final 93 baseline 1× frames exactly reproduce the reviewed contact
images. City population/flags, accented names, ellipsis, status marks, selected
replacements and failure UI were inspected. No unexpected halo, stale foreground,
erased terrain or clipped map panel was observed. A diagnostic color-capture
label offset was corrected: the MV30 board starts in high contrast. Final captures
assert their actual mode, and greyscale map pixels independently require equal
RGB channels. Earlier mislabeled receipts remain diagnostic rather than final
acceptance evidence.

Invalid release identity, a corrupted pinned license and a missing font must
present a visible startup error without a successful map. Lost WebGL context
must present a DOM recovery screen over the unavailable canvas. **Reload preview**
recreates the host and re-admits content. This is explicit recovery by reopening
the preview; it does not promise in-place context restoration or session persistence.

## Remaining scope

Content download/startup and draw/cache observations are retained for diagnosis.
They are not the performance contract's target-hardware workload measurements.
The full pinned release is substantial; download caching/installation, persistence,
import/export, menus, IME/accessibility, physical mixed-DPI/fullscreen and the
other platform-service gates remain M2 work. U03/C07 routes and trade, W03 foreign
city memory, U02/U06 combat and other live producers keep the named M3–M5 joins
in the [visual ledger](map-visual-coverage.md). Authored scenes do not implement
those gameplay operations.
