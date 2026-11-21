# M2 development closeout

## Accepted disposition

**M2 development is closed with tracked deferrals; M3 is the main workstream.**
The graphical and platform implementation has
dated Linux, macOS ARM64, Windows x64 and Chromium evidence. **The original full
M2 acceptance gate is not met.** It requires measured performance budgets and
platform checks that still fail or remain unexecuted.

The user explicitly accepted closing M2 development with the eight follow-ups
below and retaining every failed/pending acceptance gate. This supersedes the
original rule that all those gates block the development milestone. It does not
change technical budgets, platform support claims or the eventual full-acceptance
criteria. Fix defects in the delivered surface and preserve its regressions;
do not restart the whole milestone for later gameplay/UI integrations.
CPU composition remains the default; experimental GPU promotion is separate.

## Delivered surface and reviewed evidence

| Surface | Delivered and executed scope |
| --- | --- |
| Map rendering and content | MV01–MV30 plus Mixed; pinned graphics and twelve TrueType faces; stock/native/classic map art; camera, picking, layers, labels, overlays, effects, overview, clipping and resource recovery. Shared CPU matrices run natively and in WASM. Windows has offscreen CPU and bounded real-window input evidence; it does not have a full interactive hardware-GPU gallery. |
| Renderer ownership | Shared renderer library, observer-filtered scenes, retained/fresh comparisons, bounded caches, context replacement and controlled GPU fallback. The reproducible SwiftShader sampling defect is repaired. |
| Shell and custom dialogs | One semantic action model, native macOS menus and in-window menus elsewhere; correlated decisions, focus containment, cancellation, repeat suppression, stale targets and text-entry isolation. Real Mac and Windows input/shutdown are now exercised. |
| Storage and services | Native/IndexedDB file libraries, owned-byte import/export, explicit persistence results, cancellation and preferences; explicit clock/media boundaries. Windows publication now preserves held readers across replacement. |
| Media | Authored PCM WAV playback through miniaudio and Web Audio, with stop/cancellation, malformed/unsupported input and explicit unavailable video. Physical output qualification remains separate. |
| Headless boundary | Core/session/contracts remain independent of graphical libraries. The same admitted command uses scoped clients, headless execution, console, menu and custom-dialog replies. |

The shell still exposes a bounded command over an authored world. This is not a
playable game, a complete gameplay UI, or original-game visual compatibility.
Later live-provider joins are assigned below; they are not counted as delivered
because a renderer can display an authored observation.

### Resolved findings

| Finding | Resolution and evidence |
| --- | --- |
| Windows concurrent storage replacement and platform-sensitive exception test | `906f285`: exclusive creation followed by `File.Replace` when needed; held-reader, creation-race and denied-sharing regressions. Nine storage tests and 488 native tests pass, with the existing device-audio skip. [Windows correction](validation/m2-windows-storage-fix-2026-11-20.md). |
| Mac keyboard-opened dialogs lack initial focus | `c2b8e13`: defer focus until layout, retaining interaction identity and existing focus guards. CPU/GPU shell and map input and the 29-check AOT sequence pass without Tab assistance. [Mac follow-up](validation/m2-macos-fixes-2026-11-20.md). |
| Stale P02 registry hash | `c2b8e13`: review the existing P02 change and refresh its hash. Conformance inventory and 99 repository tests pass; P02 remains unregistered for gameplay conformance. [Mac follow-up](validation/m2-macos-fixes-2026-11-20.md#conformance). |
| SwiftShader nearest-sampled terrain | `d3547d9`: explicit source texel centers and pixel-aligned coverage. All five former CPU-bound failures pass unchanged limits. Both browser backends pass 204 history cases and 100 warmed replacements; three hardware batches provide 114 exact presentation captures. [GPU investigation](validation/m2-gpu-sampling.md). |
| Excess damage scanning and repeated browser atlas packing | `c2b8e13`: viewport damage candidates with global route-eligibility checks and bounded terrain-atlas reuse. Native/WASM regressions pass. Mac dirty-world p95 improves to 54.6–55.2 ms, but **still fails** the 16.7 ms target. [Mac timing](validation/m2-macos-fixes-2026-11-20.md#timing-and-acceptance). |

The initial [Mac](validation/m2-macos-2026-11-20.md) and
[Windows](validation/m2-windows-2026-11-20.md) failure records remain historical
evidence. Their follow-ups close named defects, not every gate in the original
reports. Builds, offline rasters, native input, hardware WebGL, physical devices
and Native AOT each retain their own evidence boundaries.

## Explicit follow-up work

The accepted development closeout carries these items forward with unchanged
technical acceptance criteria. None is silently marked passed or removed from
release/platform claims.

| ID | Remaining work and current status | Owner and completion condition |
| --- | --- | --- |
| M2-F01 | Changing-map performance: measured failures remain, including Mac 1080p dirty-world p95 54.6–55.2 ms versus 16.7 ms; larger Linux/browser workloads also miss targets. | Renderer/session performance work alongside M3; required before M7 performance acceptance. Pass the existing [workloads and budgets](performance/map-rendering.md), including mutation/projection/binding, with source-bound samples and correctness checks. |
| M2-F02 | Startup/download/resource budgets: full-content browser startup and transfer, and measured desktop idle memory, exceed limits. Monolithic atlas transfer remains about 211 MiB even on cached navigation. The combined-checkout browser matrix also has an unresolved allocation failure, recorded below. | Content delivery and host startup follow-up before browser/release acceptance. Resolve the matrix allocation failure and meet [application budgets](performance/application.md); smaller initial admission or partitioned content needs its own design and validation. |
| M2-F03 | GPU promotion: intermittent hardware MV28 missing pixels and resource-replacement failure have not been explained; experimental stable-mask alpha differences persist. | Optional GPU renderer work. Retain CPU defaults and disabled experiments; promotion requires resolving relevant failures and validating the selected profile on its claimed backends. Passing reruns do not erase the original failures. |
| M2-F04 | Remaining platform rendering and browser engines: complete interactive Windows/macOS art/density/recovery coverage and Firefox/Safari execution are incomplete. | Platform qualification through M3–M5 and before M7 platform claims; execute the outstanding [platform checklist](testing/platforms.md) on actual target hosts. |
| M2-F05 | Native loops and input: Mac dispatcher progress is observed during menus/file panels, but deadline/peer delivery under native tracking is not fully established. Windows had two unexplained nonactivating clicks. Windows AOT retains two Avalonia DirectComposition callback diagnostics. | Host integration follow-up during M3; resolve or explicitly constrain affected paths before claiming native-loop/AOT acceptance. Preserve normal shutdown and one accepted command effect. |
| M2-F06 | Accessibility and displays: IME, screen readers, physical mixed-DPI transitions and full fullscreen/file-panel combinations remain unexecuted. Windows maximize/restore is not fullscreen evidence. | UI/platform qualification as screens grow in M3–M5; complete before claiming the corresponding M7 platform/accessibility support. |
| M2-F07 | Media devices: real audibility/channel order, device interruption/removal and remaining native/browser playback paths are not established by offline or virtual-sink checks. | Media/host work through M5 and platform qualification before M7. Verify the [media profile](content/media-profile.md) on real outputs; retain explicit skips and unavailable outcomes. |
| M2-F08 | Storage failure matrix: remaining OS permissions/disk failures, browser eviction/quota paths, power-loss behavior and full preference/removal flows are incomplete across targets. | I/O/host work with M3 file integration; complete applicable [platform checks](testing/platforms.md) before M7 durability/platform claims. Do not broaden the bounded per-instance writer contract into a multi-process writer guarantee. |

OS console interruption/output delivery, extra runtime identifiers, installers
and signing retain their existing platform/M7 ownership. A single architecture's
run is not evidence for the other architectures.

## M3 and later integration boundary

- M3 owns the playable session and live world/city/unit providers, real commands,
  turn progression and save/reload through the existing file-library boundary.
  W02 generation/world entry is implemented, but does not itself complete the
  playable slice.
- M3–M5 connect later visual facts through their owning W/U/C/P/G contracts;
  renderer fixtures must not become synthetic gameplay providers.
- M5 owns full scenario audio/video decoding and resolution, editors and the
  remaining screens. The agreed M2 media scope is authored WAV adapters and
  explicit unavailable video, not a full scenario codec stack.
- M6 owns original-peer multiplayer. Simulated deadline/peer-work tests do not
  establish original networking compatibility.

Continue with M3's smallest automated headless/graphical playable loop. Preserve
M2's functional regressions as those callers grow, and keep the follow-up IDs
visible in the implementation, platform and performance backlogs.

## Review provenance

This review starts from `778cc59d0b7f451e0f714d40b90b7acc68c8c09d`, containing
the Windows, GPU and Mac changes above plus W02 work. Platform evidence was
collected on each record's stated revision or preserved source cohort; it is not
relabeled as a full cross-platform rerun of this combined checkout. Mac/Windows
raw artifacts remain on their originating machines and were not independently
rehash-audited on this Linux host. The committed records retain their identities,
commands, failures, skips and available manifest hashes.

Local combined-checkout checks use Linux x64, .NET 11.0.100/runtime 11.0.0,
the pinned graphics/fonts and Chromium 151.0.7922.34. They supplement the platform
records rather than replacing their real-window/device evidence. This closeout
changes documentation only; no renderer, storage, media or acceptance budget is
changed here.

| Local review check | Result |
| --- | --- |
| Core and optional App Release builds | Pass, zero warnings/errors |
| Native suite | 504 total: 502 pass, zero fail, two expected skips (device audio and Windows-only handle sharing) |
| Repository metadata/schema tests | All 99 pass using the pinned schema dependencies; conformance inventory passes |
| Identity/project/coverage infrastructure | Seven identity tests pass; 42 projects, 57 families and 471 shared groups inventoried |
| Native CPU renderer | 31 boards, 317 asserted sprite identities and 18 companion groups pass, including the Mac damage/atlas regressions |
| Supported native GPU history | 204 exact retained/fresh cases satisfy CPU bounds; 100 warmed replacements pass exactness and cache limits |
| Combined-checkout WASM matrix | Incomplete: all 31 boards render, then a companion check throws `Out of memory`; no final matrix receipt. Retained under M2-F02/F04, not counted as a pass. |

The browser allocation failure is new evidence from this review. Reproduce with
the published `OpenCiv2.MapBrowser` host and
`node tools/dotnet/run_map_browser.cjs BUNDLE OUTPUT --matrix-only` using the
[pinned content setup](BUILDING.md). The failing CPU-composition/SwiftShader run
preserved partial artifacts through the viewport-scroll checks. A second run
adding only phase names and `GC.GetTotalMemory(false)` logging passed all 31
boards, 317 identities and 18 companion groups, saving 291 evidence files.
Its compiled identity was verified against that diagnostic source, which is
preserved with the patch under `browser-matrix-diagnostic/`. The logging was
removed afterward; no assertion, workload or runtime memory limit changed.

The runtime bundle has a 2 GiB linear-memory ceiling, but this review has not
isolated the failing allocation or established whether harness lifetimes or
production memory behavior cause it. The diagnostic pass does not erase the
original failure or qualify the uninstrumented combined build. M2-F02 must
resolve that distinction and repeat the unchanged matrix; M2-F04 retains the
browser qualification gap. Earlier Mac/browser passes remain evidence for their
own source cohorts.

The initial Python invocation lacked the optional JSON-schema dependencies;
rerunning in the existing environment with the versions pinned in
`tools/requirements-schemas.txt` passes. Both logs remain in
`build/m2-closeout-review/`. No product/test assertion was weakened to obtain a pass.

The native CPU/GPU and original browser receipts match all thirteen compiled
assembly identities in the reviewed checkout. All 62 native board image/fixture
hashes were checked. Local artifacts remain under `build/m2-closeout-review/`;
these hashes bind the review and retain the failed browser result:

| Receipt | SHA-256 |
| --- | --- |
| `source-audit.json` | `571c2dc6348bb5bc0d7f39728ac172e933e1e793e0748979ee43bd9406ef11e5` |
| `native-matrix/matrix.json` | `45298e2e610b874ee1b400663aef3431589fc5bb6d75f135ea4fec3cb99505ef` |
| `native-gpu-history/optimization-checks.json` | `d61f5ee555d7d61d62136034eacaa639e0ba5500edde31b30dc81a3a5c4594ae` |
| `browser-matrix/failure.json` | `9e57453dca7d47a05ee1105959b38621a40b041f98553e2235e480563f2cef09` |
| `browser-matrix-diagnostic/matrix.json` | `cfd3530760b28a112ec3371e8e629d1a8ae9b8446d876a25d7212c53d7d80aa8` |
