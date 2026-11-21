# M2 storage and service adapters

The shell's **File → Files and preferences** flow imports a selected file into
application storage, lists stored copies, exports their unchanged bytes and
removes a selected stored copy. It saves local map display preferences separately
from the running game. This is an application storage boundary, not a playable
session loader or a new classic save format.

## Ownership and completion

The adapters use [Avalonia's storage provider](https://docs.avaloniaui.net/docs/services/storage/storage-provider)
and [IndexedDB transaction durability](https://developer.mozilla.org/en-US/docs/Web/API/IDBTransaction/durability).

`IByteStore` provides asynchronous application-owned values with opaque keys.
`NativeByteStore` stages each replacement in the destination directory, flushes
and closes it, then renames it. Publication failure removes staging and retains
an existing value. Native writes copy their input before dispatching blocking
flush/publication work away from the UI. Reads return owned bytes. The desktop
host defaults to the `OpenCiv2/storage` directory in the OS application-data
folder; `--storage-directory PATH` isolates tests and alternate profiles.

The browser adapter uses an origin-scoped IndexedDB database named
`openciv2-app-v1`. It requests strict transaction durability and resolves writes
only from `IDBTransaction.oncomplete`, never from a successful `put` request.
Abort, quota, permission and blocked-upgrade errors are failures; there is no
in-memory success fallback. Connections close after each operation and on
version changes. The browser exposes no invented filesystem paths. Persistence
survives browser restart; browser site-data deletion/eviction remains possible.
Export important files independently. This record does not claim power-loss
or storage-device failure testing.

`DocumentLibrary` admits one operation at a time. During selection, cancellation
revokes the pending reply; late files cannot be imported. Persistence begins
only after selection and bounded reading succeed. Once publication starts,
cancellation cannot relabel a committed write. Disposing the library revokes a
pending selection; an already admitted commit retains its result. Every imported
copy has its own ID, so duplicate filenames never overwrite another import.

Native file selection/export uses Avalonia's platform storage provider and owns
each selected handle/stream until completion. Native export truncates a chosen
existing stream, writes, flushes and closes before reporting completion. It is
not an atomic-replacement guarantee for arbitrary external file providers; an
external write failure can retain a partial destination. Browser import uses a
file input with cancellation and a 32 MiB limit. Browser export requests a Blob
download and explicitly says to check Downloads for completion. It does not
claim that requesting a download durably saved a file.

The private application envelope is `OC2DOC01` (eight ASCII bytes), followed by
a little-endian signed 32-bit UTF-8 filename byte count, the filename, then the
unaltered payload. Filenames are plain leaves, at most 255 UTF-16 code units;
the decoder additionally bounds the encoded name to 1–1024 bytes. Payloads are
bounded to 32 MiB. Storage keys contain only lowercase ASCII letters, digits and
hyphens, with a 100-character limit. These are application-owned definitions,
not F06 save-file fields. Arbitrary imported bytes are preserved; importing does
not validate a save, install scenario resources, or mutate the authored world.
Existing classic/native content lookup and its case-alias checks remain separate.

## Preferences and services

Display preferences persist grid, bare terrain, city radius, disclosed ownership,
reduced motion, following, sentry appearance and color correction. They never
enter the simulation or replace CIV2.DAT gameplay preferences. Renderer settings
retain their separate versioned codec; browser saves are now asynchronous.
A failed write does not change the saved model. Startup query overrides do not
rewrite the saved choice. The software browser backend selects CPU unless an
explicit unsupported GPU request is made, which is rejected as before.

`IPlatformClock` separates monotonic milliseconds from calendar time. The host
passes its clock into the session tick for interaction expiry and uses it for
cosmetic presentation. `ManualPlatformClock` advances only when instructed;
shared deadline tests use it while processing authored peer requests.
`IPresentationMedia` is presentation-only. The initial `SilentMedia` backend
reports skipped/cancelled explicitly. Stack-order effects and meaningful notices
complete independently of playback. Actual audio/video decoders and authored
P10 media playback remain separate work.

## Executed validation

The final local receipts and frozen sources are under `build/m2-services/`.
The environment is Linux x64, .NET SDK 11.0.100/runtime 11.0.0, Avalonia 12.1.6,
Chromium 151.0.7922.34 and Playwright 1.58.2. Native input uses isolated KWin/
Xwayland with the real GTK file chooser. Browser input runs the trimmed production
WASM app with pinned graphics/fonts and WebGL/SwiftShader, at both 1× and 2×.
These are functional checks, not physical display or performance measurements.

| Check | Result |
|---|---|
| Native suite | 482 tests pass |
| Shared native and WASM suites | All 454 groups pass on each host |
| Linux storage/services, 1× and 2× | 13 checks pass at each density |
| Linux existing application regression, 1× and 2× | 32 checks pass at each density |
| Trimmed browser storage/services, 1× and 2× | 40 checks pass |
| Trimmed browser existing application regression | 41 checks pass |
| Linked map-browser startup/gallery/input/recovery smoke | 15 checks pass; seven captured boards |
| Builds and formatting | Root/App/browser builds/publications and formatting verification pass; linked map-browser formatting passes |
| Repository checks | Seven identity tests, 42 project boundaries and all 55 coverage families pass |

The dedicated drivers exercise actual menu/button/key input, picker cancellation,
continued dispatcher ticks while selection is pending, byte-exact import/export,
process/browser restart, preferences, removal, focus and resizing. Browser faults
are injected at the IndexedDB API boundary: abort after a successful request and
quota failure. There are no production fault switches or engine mutation exports.
The browser storage driver is also registered in CI; that workflow change has
not been executed remotely in this record. Shared tests cover owned payloads, invalid envelopes/keys, cancellation, failure,
explicit clock progression and silent completion. Native tests additionally hold
selection/commit tasks pending, reject late replies and duplicate dispatch, and
check atomic replacement with concurrent readers and staging cleanup.

Native picker responsiveness and shared peer/deadline behavior are distinct
observations: this does not exercise a real network connection inside the GTK
picker or the macOS native menu loop. Imported Unicode names and bytes round-trip;
the pinned font set does not provide every Unicode glyph (including the CJK
filename used in the browser byte test).

## Evidence inventory

The audit admits nine final reports, 391 compiled source inputs, 1,302 production
application artifacts and 29 screenshots. It verifies every recorded compiled
source against the tested checkout, the production input drivers' before/after
artifact inventories and the final driver hashes. `source/` retains the admitted
compiled inputs and reporting tools. Earlier diagnostic runs are excluded;
`validate.sh`, `final-input.sh` and `audit.py` preserve local orchestration.
The receipts carry the parent revision plus exact source identities; they are
not inferred from a revision label alone.

| Receipt | SHA-256 |
|---|---|
| `evidence.json` | `3750eb66955994d402fa8e51e910ea4e33c7133cd0a468bf1628092baf9b1a39` |
| `storage-native-1.json` | `ef2066e513f4c941fb4fd3c35a8d1469b5d248e7c68d3af626c9a1a3a54c5dfc` |
| `storage-native-2.json` | `8d1762b928401c24ca5e422442452caf57337e22f80fe4fc2b7588558dabb2f4` |
| `storage-browser-final.json` | `257ba6ee52fd2791c8de6b676aea756a6c1a5aeb6a776573f1b3595c783607d5` |

## Remaining gates

Actual Windows/macOS file selection and native menu tracking, Firefox/Safari,
IME, screen readers, mixed-monitor/fullscreen behavior, large-library/startup
costs, filesystem power-loss behavior and browser eviction remain unmeasured.
Live-session load/save still needs the M3 game/rules setup path and the existing
F06 semantic save/load operations; the asynchronous application store must not
be substituted for synchronous classic partial-write contracts. Full media
playback remains P10 work. The renderer's functional/performance gates and GPU
default decision are unchanged.
