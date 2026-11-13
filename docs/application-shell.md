# Application shell implementation

This describes the implemented M0/M2 subset of the [architecture](architecture.md)
and [implementation plan](implementation-plan.md). The initial implementation
is a small application shell, with game rules left
to the existing algorithm specifications. It provides ownership, lifecycle,
rendering, command routing, menus and custom dialog input. It introduces no
original game assets or reconstructed implementation code.

## Boundaries

| Module | Responsibility |
| --- | --- |
| `src/viewmodel/` | SDL-free screen/dialog state, commands, menu descriptions, availability and checked state. |
| `src/application.*` | SDL lifetime, window/renderer ownership, owned input queue, command-event bridge and frame presentation. |
| `src/ui/` | Dear ImGui context/backends, title screen, drawn menus and custom modal content; SDL game-view rendering. |
| `src/platform/` | AppKit, Win32 or drawn-menu selection behind a small menu interface. |
| `src/main.cpp` | SDL callback entry points and exception-to-exit-status boundary. |
| `web/shell.html` | Browser canvas, loading/error feedback and restart after application exit. |

All rendering and native UI calls run on the main thread. SDL may invoke an
event callback from an event producer's thread, so that callback only copies
recognized events into a mutex-protected queue. UTF-8 text-input payloads are
copied into owned strings before the callback returns. Each iteration drains the
queue, forwards input to ImGui, applies native commands, builds the UI and
synchronizes native menu state again after ImGui actions. Key releases, wheel,
mouse enter/leave and focus changes are forwarded alongside key/button presses.
Unrecognized borrowed payloads such as file drops are not retained.
Workers must be stopped/joined before application teardown.

Native callbacks push an SDL user event containing only a command ID. The shell
checks availability again when dispatching, preventing stale queued menu actions
from changing state behind a modal. Shell IDs are not classic P05 command IDs.
The menu description is shared by all presenters; macOS relocates About/Quit
into its application menu. Failed required initialization reports an error and
unwinds owned resources. Native menus detach before the SDL window is destroyed.

The callback returns every frame, including while a custom dialog is open.
There is no nested game event loop or blocking message-box call. The desktop
callback rate is capped at 60; vsync is requested when supported. This is a
presentation policy, not a simulation tick. Future turn processing and F01
determinism must not derive game state from rendering time.

SDL logical presentation maps the **game view** to a 960 x 640 letterboxed
surface. ImGui renders afterward in window coordinates, with its SDL backend's
framebuffer scale for HiDPI output. UI events retain their original window
coordinates; do not apply the game's logical transform before forwarding them.
Fonts and style sizes follow display content scale, rebuilding from the base
style when it changes. The UI can resize without stretching the game view.

## Dear ImGui presentation

Dear ImGui **1.93.0** is pinned by archive URL and SHA-256 in
`dependencies.json`; downloading requires `OC2_FETCH_DEPENDENCIES=ON`. The runtime compiles
its core and official `imgui_impl_sdl3` / `imgui_impl_sdlrenderer3` backends.
There are no ImGui dependencies in `oc2_viewmodel`. The UI owns one context,
destroys both backends before the SDL renderer/window, and disables `imgui.ini`
and log-file persistence. Layout remains application-owned on all platforms.

The build applies one local fix to 1.93.0 in a generated copy of `imgui.cpp`:
the modal/window-switcher dimming rectangle uses `AddRectFilledMultiColor` with
four identical colors, guaranteeing the six indices required by that code.
Upstream `AddRectFilled` can emit extra anti-aliasing geometry at fractional
framebuffer scales and trigger an assertion. The runtime regression covers
1x, 1.25x, 1.5x, 1.65x and 2x. Re-evaluate this patch on a dependency upgrade;
the fetched source and any offline source override remain untouched.

`BeginMainMenuBar`, `BeginMenu` and `MenuItem` present the shared menu model on
Linux/web. `BeginPopupModal` hosts custom dialog content on every platform;
`ImDrawList` artwork can be combined with text, images and ordinary widgets.
The title screen uses the same controls. ImGui owns live focus, hit testing,
text layout, clipping and scrolling. The shell supplies semantic dialog state,
initial button selection and accepted/cancelled results.

Tab/Shift+Tab and arrows navigate, Enter/Space activate, and Escape cancels a
dialog. Quit initially selects Cancel with a visible focus outline. F10 focuses
the drawn menu bar; Down opens the selected menu. The isolated F10 bridge uses
ImGui internal navigation functions because the public focus API does not
select a standalone menu's navigation layer. Keep its integration test passing
when updating the pinned library. All other UI rendering uses public APIs.

The SDL backend always receives input. Future game input must check
`Application::ui_captures_mouse()` / `ui_captures_keyboard()` before handling it,
then convert only uncaptured world input to game coordinates. ImGui capture
flags and the shell modal guard prevent input from reaching underlying gameplay.
Primary-modifier shortcuts are explicitly registered through ImGui's shortcut
routing; `ImGuiMod_Ctrl` maps to physical Command when `ConfigMacOSXBehaviors` is
enabled, and physical Control otherwise. Menu shortcut labels alone do not bind
actions. AppKit retains ownership of native Command shortcuts. Native command
availability is checked independently of ImGui, including while a modal is open:
ImGui cannot disable an OS menu itself.

See the upstream [backends](https://github.com/ocornut/imgui/blob/v1.93.0/docs/BACKENDS.md)
and [input guidance](https://github.com/ocornut/imgui/blob/v1.93.0/docs/FAQ.md).

## GUI automation

`oc2_ui_tests` compiles the actual `ui::Interface` with an instrumented copy of
ImGui and Dear ImGui Test Engine, and links the same SDL-free view-model library as the game.
The test-engine hooks and configuration apply consistently to this executable
and its ImGui library; the production `openciv2`/`oc2_frontend` targets use
the ordinary library. Headless, non-testing and WASM builds do not fetch or
link the engine.

Tests use widget paths, menu actions and simulated keyboard input rather than
fixed pixel clicks. They observe shell screen/dialog state, focus, disabled
widgets and checkmarks. The harness renders through the actual SDL backend,
keeps ImGui assertions active even in Release, and validates draw-data integrity.
Each test resets its shell state and closes popups during teardown. A tested
Quit command sets shell state; the harness continues to report the result.

Headless runs use SDL's dummy video and software renderer. A test-only
`NewFramePre` hook sets the logical size and framebuffer density while the
physical SDL surface is resized to match. This exercises fractional-density
ImGui geometry; it does not emulate an operating system's DPI notifications.
The visible runner uses the desktop's actual density. SDL window/renderer calls
remain on the main thread; the engine's test coroutine cooperatively yields
to it. Failure captures resample the last completed framebuffer into window
points and are diagnostic images, not pixel-perfect golden-image assertions.

`oc2_runtime_tests` still checks SDL event delivery, focus loss, borrowed UTF-8
ownership, native command routing and backend teardown. SDL shortcut injection
exercises both macOS and non-macOS modifier mappings on every host, including
rejection of the other modifier. Browser tests cover the WASM/DOM boundary.
Native AppKit/Win32 interactions require platform testing;
the engine drives the drawn menu implementation.

## Native menus

**macOS:** Objective-C++ with ARC builds an `NSMenu`/`NSMenuItem` tree and assigns
[`NSApplication.mainMenu`](https://developer.apple.com/documentation/appkit/nsapplication/mainmenu).
AppKit owns Command-key equivalents. About, Services, Hide and Quit use the
application menu; game actions share the common model. Custom actions have an
explicit retained target and explicit enabled/checked state. The prior menu and
Services menu are restored on teardown.

**Windows:** obtain the HWND through
[`SDL_GetWindowProperties`](https://wiki.libsdl.org/SDL3/SDL_GetWindowProperties),
attach an `HMENU` using [`SetMenu`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setmenu),
and receive `WM_COMMAND` with
[`SetWindowSubclass`](https://learn.microsoft.com/en-us/windows/win32/api/commctrl/nf-commctrl-setwindowsubclass).
Unrelated messages continue through `DefSubclassProc`. This handles menu messages
sent synchronously to the window, which a pre-TranslateMessage hook alone does
not cover. ImGui shortcut routing implements Ctrl shortcuts. The model controls checkmarks
and enabled states. Menu labels are converted from UTF-8 to UTF-16.

## Integration with the top-level plan

The implementation delta comes from the four application commits on
`codex/sdl3-core`: `0c9fa3c`, `60ff4e4`, `deab69f`, and `c01bcef`. That branch
and the current specification snapshot have unrelated histories. Integration
retains the current `docs/algorithms/` tree verbatim and imports only that
four-commit implementation delta, with the build/test/documentation corrections
recorded here. No reconstruction source or historical harness was consulted.

`oc2_viewmodel` owns presentation-only shell state; `oc2_frontend` owns the SDL
application, ImGui interface, and menu adapters. The future `oc2_game`,
`oc2_session`, codecs, content adapter, and network targets do not exist yet.
Native menu implementations can become separate targets when their interfaces
need independent consumers; they are currently selected frontend sources.

The shell's whole-frame input drain and single-dialog guard are appropriate
only for this prototype. They are not implementations of P05/N02 reentry,
per-operation command authority, retained menu updates, or network deadlines.
Gameplay must use the planned semantic coordinator, preserving publication and
live-read boundaries. In particular, blocking native menu tracking still needs
a service strategy and platform tests before network progress can be promised.
Linux and browser menus remain Dear ImGui as specified; alternate GUI toolkits
are not part of this architecture. Desktop file dialogs will also use ImGui.

## Extension points and current limits

Add shell actions to `Command`/`main_menu()` and implement state transitions in
`Shell`; presenters use the same availability query. Game-facing dispatch will
need a separate adapter for the exact P05 IDs, ordering and dynamic menu rules.
The startup menu is intentionally small, not the full classic in-game menu.

Dialog state is data; drawing and input live in `ui::Interface`. Add custom
content and controls there while retaining modal-first routing. There is
currently one active application dialog, with no application dialog stack or
native child windows. Drawn text now uses pinned DejaVu Sans TrueType data and
ImGui's runtime rasterizer. DejaVu Serif/Mono and bold/italic faces are also
registered; [font metadata](content/fonts.json) pins all twelve face hashes.
Native menus retain OS typography. The bitmap fonts in the graphics release are
not consumed; no exact historical font metric match is claimed. UTF-8 input is
owned and forwarded, but no text-editing dialog is included yet.

Dear ImGui does not provide screen-reader accessibility or full complex-script
shaping/bidirectional layout. Localization, specialized game skins and the full
P04 control set remain future work. Keyboard navigation is not equivalent to
accessibility support. See the upstream [limitations](https://github.com/ocornut/imgui/blob/v1.93.0/docs/README.md#the-pitch).

Audio, graphics-pack loading, configuration persistence, ImGui file dialogs, browser
persistent storage, save/load, networking and simulation are not implemented by
this shell. Add them as owned services with explicit failure and asynchronous
completion paths. Build/documentation tests here do not replace X02 behavioral
fixtures or platform-native manual testing.
