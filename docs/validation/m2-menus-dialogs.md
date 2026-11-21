# M2 menus and custom dialogs

The Riverbend shell now has one action model for menus, shortcuts and the drawn
order control. The available gameplay command remains `unit.stack-bottom`;
unsupported later-game commands are not presented as working actions.

## Implemented boundary

`ShellActions` supplies stable IDs, labels, shortcut hints, enabled/hidden states
and checked display preferences. Availability reads owned client observations.
The command service still revalidates mode, authority, target lifetime and stack
coherence at admission and execution. Showing a menu does not submit a command
or consume gameplay RNG.

Orders offers **Send to bottom of stack**. View offers **Map grid**, **Bare
terrain**, **Reduced motion**, **Map options** and **Command console**. The menu
and Map options share their checked values. Ctrl+Enter orders the selected unit;
Ctrl+G toggles the grid outside text entry. The macOS adapter uses the Command
modifier; the host's Avalonia configuration resolves the primary modifier,
including in WASM. Alt+O opens Orders for keyboard navigation on the in-window
menu. Central key handling suppresses repeats; menu gesture text does not add
another in-window key binding. Native accelerators are suppressed during text
entry as well.

Linux, Windows and WASM use Avalonia's in-window menu. macOS attaches a
`NativeMenu` to the window and delegates native gestures to that adapter, using
the same command objects and state. The adapter is built locally, but actual
macOS export/tracking is not an executed gate in this record.

The confirmation is a nonblocking overlay. Its custom painted choice regions
retain standard Button input and automation peers, with stable IDs
`dialog.choice.1`, `dialog.choice.0` and `dialog.cancel`. Text uses the shared
pinned DejaVu collection and Avalonia text layout. Tab stays within the dialog;
Escape respects cancellation policy. Input behind it is disabled. Focus returns
to the initiating control, or the map after menu activation, once the operation
finishes. Submitted choices remain visibly latched while the session consumes
them. A captured client/command/interaction identity cannot answer a later dialog.

## Validation

The four new portable groups cover observational availability, slot reuse,
invalid and duplicate choices, cancellation, old dialog callbacks, observer
handoff and revocation. Changed target tokens, ownership and stack links fail
without applying the requested reorder. A non-cancellable interaction remains
pending while eight authored peer requests are processed; an explicit fake-clock
deadline completes it with its specified default. This exercises the session
and dialog model, not a real network transport inside a platform menu loop.

Application input uses actual pointer and keyboard events. It exercises menu
popup selection and closure, every admitted View action, checked-state agreement
with Map options, central shortcuts, key repeat, text-entry isolation, custom
choice bounds, focus cycling, cancellation, command parity and resizing to
680×640. Browser screenshots include the custom dialog at both densities and
viewport sizes. Popups use logical view coordinates; separate native popup roots
are converted from physical pixels, while browser screen coordinates use CSS
pixels.

Executed on 2026-11-20 with .NET SDK 11.0.100/runtime 11.0.0, Avalonia 12.1.6,
Chromium 151.0.7922.34 and Playwright 1.58.2. Linux runs use isolated virtual
Xwayland windows; browser runs use WebGL/SwiftShader plus the explicit software
backend configuration checks. These are functional tests, not physical scanout
or performance measurements.

| Gate | Result |
|---|---|
| Native test suite | 473 pass |
| Shared native and WASM suites | All 451 groups pass on each host |
| Linux pinned-content input at 1× and 2× | 32 checks pass at each density |
| Linux limited preview | 16 checks pass |
| Trimmed browser application | 41 input/configuration checks pass across 1× and 2× |
| Build and formatting | Application build, browser publication and root/App/browser formatting verification pass |
| Repository validation | Identity tests, project boundaries/package locks and coverage inventory pass |

The final audit verifies six receipts against 355 compiled source inputs and
their invoked application artifacts, retaining seven browser screenshots.
Local evidence is under `build/m2-shell/`; `source/` preserves the admitted source
inputs and `evidence.json` records report/image hashes. Earlier diagnostic runs
are excluded from that admitted set. The input drivers retain source, tool,
artifact and content hashes; reports identify the parent revision and the exact
tested source contents rather than assuming the revision alone identifies them.

| Receipt | SHA-256 |
|---|---|
| `evidence.json` | `0b0f7e37df01098d20b8f27f2f1c520a88dee9531d46761a34ee164d97e2ad7d` |
| `native-1.json` | `b7b693d8810c969b6e7a7b2164df7b4159d93312fe1edf3880821124d2c0156a` |
| `native-2.json` | `93008a38e8d0ab307c51d4798d0acfd350abc5e4ca182456237ff53bbc75ef9d` |
| `browser-final.json` | `91d8370d35b8860cf3a42a73a2941733eede3a88d276a0b20ac31233b78392ac` |

## Limits and follow-up

This slice does not close full M2 acceptance. Actual macOS NativeMenu tracking
and protocol/deadline responsiveness in that native loop, Windows input, IME,
screen-reader behavior, mixed-DPI/fullscreen transitions, storage/import/export
and media/time services retain separate gates. Linux global-menu export is
optional and is not enabled. City/report dialogs and their gameplay commands
remain with their M3–M5 owners. The renderer performance and GPU-default blockers
are unchanged.

The subsequent [storage/service slice](m2-storage-services.md) records the
implemented file library, native/IndexedDB persistence, preferences and
clock/silent-media adapters; those results are separate from this earlier run.
