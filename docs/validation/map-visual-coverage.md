# Map visual feature coverage

The required capabilities are [P02 MV01–MV30](../algorithms/P02-map-rendering-and-minimap.md#minimum-map-visual-feature-set).
The [M2 acceptance matrix](../testing/map-rendering.md) specifies each row's
authored cases, pass criteria, backend evidence and completion rules; the
[map performance contract](../performance/map-rendering.md) supplies the separate
interactive workloads and budgets.
The stock input contract is [map-assets.md](../content/map-assets.md), with
368 exact, verified atlas bindings in the [catalog](../content/map-asset-bindings.json).
Art availability, renderer implementation, a correct observer projection and
executed visual acceptance are separate claims.

This initial assessment on **2026-11-17 Pacific** uses source inspection of the
in-progress, uncommitted Riverbend slice and its local execution results.
The slice and its detailed execution report are separate implementation work.
It is a work list, not a new renderer execution report. “Partial” means an
existing path exercises some of the feature; no row claims full acceptance.
“Open” means the full named capability has no retained acceptance evidence here.
Update rows with concrete scenes/options/backend evidence as M2 progresses.

**Acceptance baseline:** every row below is pending for complete native and
browser acceptance. Existing partial Linux evidence does not pass an entire row.
The expanded P02/matrix requirements have not been executed by this document
update. Earlier source-bound reports remain historical after affected contract
changes; they must not be relabeled or rehashed to claim acceptance of this scope.

| ID | Initial coverage | Work required for acceptance |
| --- | --- | --- |
| MV01 | Partial: eleven base terrain bindings | Individual terrain identity/custom-art and overlaid scenes. |
| MV02 | Open | All coastline contacts, channels/islands, flat edges and wrap seam. |
| MV03 | Open | Edge blends and all Forest/Hills/Mountains connection variants. |
| MV04 | Partial: connected river art | All junctions and river mouths, knowledge boundaries and distant zoom. |
| MV05 | Partial: resource and Grassland-shield bindings | Every terrain/special selector; independent predicates and simultaneous layers. |
| MV06 | Partial: improvement marks | Exact agricultural/mine art, distinct combinations and city underlays. |
| MV07 | Partial: road/rail arms | Mixed junctions, isolated marks, city connectors and remembered changes. |
| MV08 | Partial: generic feature marks | Exact huts/pollution/fortress/airbase art and occupied-aircraft owner colors. |
| MV09 | Partial: unknown versus known tiles | Directional feathered edges, remembered/current distinctions and hidden-state noninterference. |
| MV10 | Partial: six demonstration unit images | All 63 classic bindings and the separate leader selection, custom units. |
| MV11 | Partial: unit marks/stack count | Actual color-role banners, primary/secondary stack treatment and dark shadows. |
| MV12 | Open | Health inputs, three severity bands and nonordinary imported values. |
| MV13 | Open | Order, waiting, automation and veteran projection/visual combinations. |
| MV14 | Open | Both sleeping styles, fortified overlay and aircraft-at-base cases. |
| MV15 | Partial: one city appearance | All 48 variants, appearance policy and per-variant registration. |
| MV16 | Partial: own-city name/size and anonymous remembered site | Foreign memory, owner flag, garrison, disorder and population anchors. |
| MV17 | Open | All eight color roles; live color reassignment across every dependent layer. |
| MV18 | Partial: selected tile/object | Distinct active versus inspected state, focus treatment and reduced-motion choice. |
| MV19 | Open | Grid, city radius, ownership and bare/full modes with observer-safe toggles. |
| MV20 | Open | Path/trade overlay drawing and separate safe U03/C07 producer integration. |
| MV21 | Open | Work counters and richer inspection; safe W04/knowledge projections. |
| MV22 | Open | Viewport-fixed notifications with required semantic completion. |
| MV23 | Open | Overview disclosure, owner marks, selection and actual camera footprint. |
| MV24 | Open | Movement, combat and all explosion/nuclear frames; interrupted/skipped cleanup. |
| MV25 | Partial: drag, wheel, bounds and picking | Presets/steps, active-unit tracking and all flat/wrapped/fractional cases. |
| MV26 | Partial: detail cutoffs | Declared bands with coast/river/fog/health continuity and every full-detail feature. |
| MV27 | Partial: pinned Sans labels | Physical-resolution status glyphs, annotation clipping, long/custom names and dense views. |
| MV28 | Partial: RGBA textures and batched layers | Tint/silhouette/greyscale, thin details, overhangs and overlap-sensitive ordering. |
| MV29 | Partial: retained image and dirty selection | Every layer's old/new bounds, observer/color/content/options changes, foreground clipping and stable margins. |
| MV30 | Open | Color correction, backend/failure transitions and equivalent feature coverage. |

The partial observation model currently supplies terrain/features, unit type and
owner, and a limited city projection. Rendering work must be accompanied by
explicit permitted inputs for the missing visual states. Do not infer garrisons,
health, foreign population, work or route hints from unrestricted world objects.
If a provider is not ready, an authored observation can prove the renderer's
capability but leaves that live integration open.

M2 closes the rendering/interaction side with authored scenes for every row.
Gameplay milestones close actual producer/command joins. Exact art-key validation
is a data check; a nonblank frame, all-atlas decode, one city's rendering, cache
equality or Linux interaction performance is not evidence that all rows render.
Preserve the finite scope of existing reports and record additional evidence
instead of relabeling it as full feature coverage.

## Recording acceptance

As a row progresses, retain its initial assessment and append/update an evidence
record with these independently assessed fields:

| Field | Required value |
| --- | --- |
| Feature | MV ID and matrix case-family/variant IDs, including combined and failure cases |
| Implementation | `open`, `partial` or `implemented`; link production renderer/projection/binding sources |
| Native acceptance | `pending`, `passed` or `failed`, with executed OS/backend and source-bound case, screenshot and visual-review evidence |
| Browser acceptance | `pending`, `passed` or `failed`, with actual published WASM/browser execution evidence |
| Live integration | `authored-only`, `partial` or `integrated`; identify which cases consume real scoped game observations and which use authored values |
| Provider follow-up | Missing operation/projection, owning specification and milestone; no generic “later gameplay” omission |
| Remaining failures | Failed/skipped cases and unavailable hardware/platform evidence; a skipped required case leaves its gate pending |

Full feature acceptance requires all matrix cases and cross-cutting checks for
the named backend. Keep hardware performance in separate records keyed to the
same source/content identity. Neither `implemented` nor `authored-only` means
the live mechanic or all-platform milestone is complete.

## Producer and milestone boundaries

| Renderer rows | Owning inputs and current follow-up boundary |
| --- | --- |
| MV01–MV09 | W01 terrain/features and W03 knowledge, plus F03/P03 selected content. Extend the existing scoped projection and safe adjacency in M2, including huts and compound features. Later terrain-work/revelation producers do not block authored visual cases. |
| MV10–MV18 | P02 appearance policy, F03 rules/colors, W03 admission and U01/unit/city state. Supply permitted body/status/city/color inputs in M2. Live changes from orders, combat, city development and civilization rules join through their M3–M4 owners. |
| MV19, MV23 | W01 geometry and W03/P02 disclosure. Map modes, safe overview inputs and navigation are M2 work, not prerequisites for a later playable turn. |
| MV20 | U03 routes and C07 trade-bonus paths under W03 privacy. M2 draws explicit authored paths and integrates any available safe producer; missing live path/trade computations remain named M3–M4 operations. |
| MV21 | W04 work counters and W03-permitted details. M2 renders/inspects supplied values; later work progression is a distinct gameplay join. |
| MV22, MV24 | P04/P10 notice/effect lifetime and completion. M2 implements presentation, skip/cancel and authored event sequences; U02/U06 and other semantic event producers join in M3–M4. |
| MV25–MV30 | P01/P02 camera/composition/lifetime and P03/P04 art/fonts. These renderer capabilities and their native/browser tests are M2 work; no game-rule dependency justifies omitting them. |

These assignments do not create a parallel engine or authorize UI reads of
mutable state. Replace authored input providers with scoped production providers
when ready, preserving the same renderer and explicit information contract.
