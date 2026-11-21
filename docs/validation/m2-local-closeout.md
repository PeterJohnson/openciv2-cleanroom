# M2 local media and platform follow-up

This slice finishes the local WAV service work, checks content admission and
fullscreen focus, and prepares the [cross-platform handoff](../testing/platforms.md#m2-cross-platform-handoff).
It does not declare M2 complete: fixed performance budgets, experimental browser
GPU pixel failures and actual remaining-platform acceptance retain their gates.
Full scenario video decoding/resolution remains M5; playable session loading
through the file library remains M3.

## Delivered behavior

**File → Files and preferences → Play selected WAV** previews an imported file
through the same `IPresentationMedia` boundary on desktop and browser. The
[declared profile](../content/media-profile.md) admits integer PCM WAV and rejects
unsupported/malformed data visibly. Native miniaudio 0.11.26 and browser Web Audio
preserve owned samples, channel order and loop requests. Playback completion,
cancellation, explicit skipping and unavailable output are distinct; none answers
a pending gameplay command or changes committed effects. Video requests report
unavailable. SDL is not a dependency.

The native library is pinned, built with a host C compiler and verified before
desktop build/publication. Managed and Native AOT outputs include its receipt and
license. Device work runs off the dispatcher; production backend selection
excludes the null backend. Browser playback bounds autoplay suspension, cancels
on page exit and releases contexts after completion/cancellation. Physical
speaker latency, audibility and device-removal behavior are not established by
the local virtual output sink.

Validation exposed and repaired three UI/error paths: disabling Play now moves
focus to Stop so Escape can cancel; malformed WAV and stored-document envelopes
produce recoverable errors; returning from native fullscreen/restore restores a
pending modal's keyboard focus. The latter preserves the pending interaction,
target and zero committed effects through three transitions on each renderer.

Both graphics loaders and browser staging now use `selectedRuntimeFiles` from
the pinned inventory. The package's historical bitmap font caches were previously
read despite being excluded from the runtime profile. Removing them saves
**47,004,986 decoded bytes (44.8 MiB)**. Runtime text continues to use the pinned
TrueType fonts. Browser staging removes stale compressed representations, and
the content compression helper verifies Brotli round trips without changing
decoded pinned bytes.

## Local validation

The environment is Linux x64, .NET SDK 11.0.100/runtime 11.0.0, Avalonia 12.1.6,
Chromium 151.0.7922.34 and Playwright 1.58.2. Native UI input uses isolated KWin/
Xwayland and the real GTK picker; audio uses private PipeWire/PulseAudio servers
and a virtual sink. Browser UI uses the trimmed production WASM application with
pinned graphics/fonts and WebGL/SwiftShader. These functional runs make no
physical display/speaker or Windows/macOS claim.

| Check | Result |
|---|---|
| Native suite, isolated audio enabled | 486 pass, no failures/skips |
| Shared native and WASM suites | 456 groups pass on each host |
| Native offline audio graph | 400 sample/loop/replacement checks pass |
| Native storage/audio UI, 1× and 2× | 17 checks pass at each density |
| Browser storage/audio UI, 1× and 2× | 50 checks pass |
| Browser audio service | Six checks pass, including actual autoplay denial without synthetic user activation |
| Existing production application input | 32 native checks at each density; 41 browser checks |
| Native fullscreen/restore | Three cycles per CPU/GPU renderer with a pending dialog and subsequent Escape cancellation |
| Linux Native AOT | Publication, 17 storage/audio UI checks and 32 full application checks pass |
| Native and WASM MV01–MV30 matrix | 31 boards and 317 asserted sprite identities pass on each host |
| Focused hardware GPU gallery, native art at 2× | 61 checks and 38 pixel-exact captures pass on AMD Radeon 890M/ANGLE OpenGL ES |
| Formatting and repository checks | Root/App/browser/map-browser verification; seven identity tests, 42 project boundaries and 56 coverage families pass |

Audio checks include actual API/sample delivery, completion, repeat/replacement,
loop cancellation, stale cancellation, continued dispatcher ticks, duplicate-play
prevention, malformed input and visible denial. The real-browser autoplay check
uses CDP evaluation with `userGesture:false`; ordinary Playwright evaluation can
supply activation and would not establish that failure path. Offline native
checks render the production graph, but their test-only exports are absent from
the published native library. The native device test is explicitly skipped unless
`OC2_TEST_NATIVE_AUDIO=1`; ordinary headless builds need no audio device.
CI now builds the pinned native audio library before the desktop jobs. That
workflow change has not been executed remotely as part of this local record.

## Startup and renderer gates

Current startup readiness requires the admitted pinned graphics/fonts, first map
draw, scoped model and usable command control. Every measured launch also accepts
real pointer input and completes exactly one command effect. The old .NET 10
empty-seam receipt remains historical; it does not measure loaded content.

The full-content measurements and unchanged budgets are recorded in
[application performance](../performance/application.md). The atlas alone is
249,808,905 decoded bytes (238.2 MiB). After exclusion of unused bitmap fonts,
selected graphics/fonts total 255,546,219 bytes; Brotli quality 5 transports
224,168,891 bytes before framework/application resources. A 25 MiB download
ceiling requires a smaller initial content set or partitioned/on-demand content;
compression alone does not close it.

The complete 54-launch/navigation receipt records desktop median/p95 readiness
of 1,228/1,436 ms with 565.4 MiB peak idle RSS. Browser cold median/p95 is
11,935/13,454 ms; cached is 11,449/14,250 ms. Cold/cached transfer reaches
218.70/211.24 MiB. WASM capacity is at most 703.75 MiB, below the conservative
768 MiB memory ceiling. Desktop memory and browser startup/download gates fail;
all post-readiness input checks pass. Every cached navigation still fetches the
211.24 MiB Brotli atlas body. These are localhost software-rendering populations,
not physical-compositor or internet-speed claims.

The [browser GPU default record](m2-browser-gpu-default.md) remains authoritative
for unresolved sampling/pixel differences. No renderer algorithm or default is
changed by this slice. The default CPU composition path's functional matrix and
experimental GPU checks are distinct from the changing-camera/world performance
gate. Fixed budgets are not relaxed, and a passing focused rerun cannot erase an
unresolved earlier intermittent failure. The fresh hardware GPU gallery passes
all 38 exact comparisons, including MV28; the earlier intermittent MV28 mismatch
and software-WebGL sampling failures remain unresolved.

## Evidence and handoff

Executed on 2026-11-20 Pacific / 2026-11-21 UTC. The final audit verifies 20
reports, 416 compiled inputs, 1,562 artifacts and 829 evidence files.

Raw reports, screenshots, build logs, isolated server logs and preserved sources
are under `build/m2-local/`; final runs are under `final/`. `audit.py` validates
compiled source identities, driver hashes and artifact inventories and writes
`evidence.json`. Receipts carry the parent revision plus the exact tested source
identities. Earlier diagnostic attempts are excluded from accepted populations.
The startup collectors were corrected afterward to wait for laid-out dialog
buttons during input verification. Functional reports inventory all tools,
including those unused collectors; their exact pre-fix bytes are preserved in
`pre-measurement-driver-fix/`, while `source/` contains the final collectors.
The compiled application and invoked functional drivers are unchanged by that
instrumentation correction.

| Receipt | SHA-256 |
|---|---|
| `build/m2-local/evidence.json` | `dea677d8c400d03dc3ee924143aafefa9ab03dfb8044dff3d665f552905000c3` |
| [Current startup receipt](../performance/application-content.json) | `d391ecfb389e33c01a5ac5a928cd87cb79cf10aa15249dae9bda558e49f5fdee` |

Remaining work is explicit in the [implementation plan](../implementation-plan.md):
Windows/macOS actual input and native menus/pickers; Firefox/Safari; physical
audio and device interruption; real mixed-DPI/fullscreen displays; IME and
assistive technology; browser eviction and filesystem power-loss behavior;
target-hardware resource/performance acceptance; GPU pixel fixes and the download
budget. Local virtual display/audio checks do not establish these results.
