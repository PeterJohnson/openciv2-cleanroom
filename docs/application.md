# Avalonia application

The Riverbend map is the first integrated M2 graphics and interaction slice.
The managed desktop host accepts the pinned graphics package and DejaVu faces.
Riverbend supports map inspection and reordering a coherent own unit stack.
See the [M2 validation record](validation/m2-map-slice.md) for the tested Linux
profile, measured CPU workloads and open platform gates.

`LocalUnitCommands.StackBottom()` optionally registers version 1 of
`unit.stack-bottom`. Its only argument is an owned unit slot/lifetime token. It
uses the existing [U01 move-to-bottom](algorithms/U01-unit-lifecycle-and-stacks.md#move-to-bottom)
mechanic through `UnitOperation`, the same command dispatcher and scoped clients
as other session operations. The command changes stack order, never movement,
orders, coordinates, rules or RNG. A supplied confirmation is a presentation
choice, not a new game-rule decision.

The admitted profile is single-player, a canonical placed target, and a coherent
chain of 1–2047 live units all owned by the participant. Imported mixed-owner,
cyclic, broken or full-2048 repair-boundary chains are unavailable through this
command; the lower-level U01 contracts remain intact. Admission reads raw links
without repairing them. Rejection messages never disclose another owner's data.
Remote requests and multiplayer modes require their later client adapters.

Submission validates the typed descriptor, mode, lifetime, ownership and complete
chain before queueing. Execution validates again before prompting and before
mutation, so an asynchronous reply cannot revive a replaced target. Choice 0,
explicit cancellation and timeout retire without effects; choice 1 calls the
production primitive once. Results carry the target and a `changed` boolean.
Actual reorder publishes a `unit.stack-reordered` effect; an already-last unit
completes without that effect. Client authority and reply-recipient checks remain
in the session runtime. This command has no checkpoint continuation registration;
trying to save its pending interaction remains explicitly unsupported.

The hosts use only `IPlayerClient` observations and descriptors outside
the composition root. The integration world is authored and clearly labeled;
it is not a second engine. Direct API, console, Avalonia and optional Lua clients
must use this same registration and prove confirmation/cancellation, stale-target
failure, observer privacy and the same resulting production stack order. The
inspection CLI has no live-command mode, so this command adds no `oc2` switch.

## Alder Valley local game

Start the desktop with `--game alder-valley`. Select a Pioneer or Outrider
on the map or in the unit list and use the direction buttons. Found a city
with the Pioneer, then set the city's worked tiles and production in its
city window. Land Surveys enables Temple construction; Clay Casting enables
Library construction. Completed buildings remain the manual production order
until another item is selected. The center resets automatic worker assignment. The window shows
food, shields, support, research and tax from the same session as the map.

Choose research in the sidebar or in the required turn decision. End-turn
confirmation can be cancelled before upkeep. A research choice during upkeep
must be settled before the player can issue another order or save. The console
lists descriptors with `help`, reports owned targets with `units` and `cities`,
and uses Tab to complete commands and disclosed target identities. Use
`save` and `reload` for the same classic slot as the File menu. Decision dialogs accept `reply <choice>` or `cancel` in their console field.

**Save game** writes the classic save in the application file library.
**Reload save** resumes that slot and renews the session and its target identities.
The file library can export the classic `.SAV` bytes. See the
[Linux playable profile](validation/m3-linux-playable.md) for scope and test evidence.

## Client and host evidence

`OpenCiv2.ViewModels.SessionPanel` receives only a scoped `IPlayerClient`. Its
owned observations, descriptors and current command result are cleared on binding
replacement or revocation. It permits one active presentation command and retains
no unbounded history. `OpenCiv2.Console.CommandText` parses the closed descriptor
value alternatives with invariant canonical integers and bounded input; it has no
GUI or scripting dependency. Five shared native/WASM groups cover direct/text
command parity, cancellation, observer changes, revocation, typed quoting and
malformed input without submissions.

`OpenCiv2.App` composes an authored 56×32 doubled-coordinate world with known
terrain, remembered improvements, a named own city, a remembered city site, an
own stack and independently filtered foreign units. The map observation includes
its controller binding, revision and topology. The model rebuilds on publication;
idle ticks, painting, panning, zoom and inspection submit no commands.

Drag or use the arrow keys to pan. Scroll to zoom around the pointer. Click a
city, unit or tile to inspect it. The own-unit list selects a specific stack
member. Enter on the map or **Send to bottom of stack** opens the same correlated
confirmation as the command console. Escape dismisses a pending confirmation.
An accepted reorder updates the visible representative without moving a unit.
Hit tests resolve diamonds to canonical W01 coordinates and repeat the horizontal
world only when it wraps. With the pinned graphics, Riverbend shows a tributary
joining the main river, a road crossing and the river reaching the gulf across
the wrapped seam. Visible river contacts and remembered routes select the
package's sprites. Special resources and grassland shields use the W01
predicates and their own pinned sprites.

The browser host now admits the pinned atlas and twelve DejaVu faces through
bounded asynchronous HTTP streams and exposes the same authored gallery as desktop.
Native/classic map replacements use the same selection reader and precedence.
The browser acceptance host links the exhaustive map matrix and adds WebGL frame,
input and lifecycle checks at 1× and 2×. Its execution evidence is separate from
native macOS/Windows UI, accessibility, physical display and performance acceptance.
The headless solution and CLI have no Avalonia dependencies.

The optional Lua native/browser hosts now run 13 matching production-session
case groups. Direct API, console and Lua compare actual stack links, command
results/effects and unchanged RNG; the Avalonia input checks above exercise the
same production registration. Lua cancellation/errors preserve completed effects,
and malformed UTF-8 produces a stable error on both platforms. Artifact-bound
reports and the [bridge contract](automation.md) record the evidence. No automation
editor, subscription system or VM checkpoint support is implied by this application boundary.


## Desktop map renderer selection

The desktop host offers **CPU** (default) and **GPU (experimental)**. In Riverbend,
open **Map options → Renderer after restart**; the gallery has a **Renderer**
button. Saving a choice applies on the next launch. The same panel shows the
active renderer and a reason if GPU rendering fell back to CPU. **Reload map
textures** retries the requested renderer. Selection does not add a frame queue
or change input, visibility, hit testing or gameplay.

Preferences live in the platform application-data directory at
`OpenCiv2/renderer.json`. `--renderer-settings PATH` redirects that file.
`--map-renderer cpu|gpu` and `--map-gpu-cache-mib 128..1024` override saved values
for that launch without writing them. The default GPU cache ceiling is 512 MiB.
Unreadable or malformed preferences produce a diagnostic and use defaults;
invalid command-line values reject the launch. The settings file is replaced
atomically only when the user changes the preference.

GPU uses CPU terrain preparation plus GPU sprite composition, atlas batching and
deferred sprite resizing/tinting. Other research feature combinations require
`OPENCIV2_MAP_EXPERIMENT=1`; ordinary launches ignore those environment switches.
The GPU option remains experimental. Linux validation and explicit backend pixel
tolerances are recorded in [renderer integration](validation/m2-renderer-options.md).
Browser, Windows and macOS testing is deferred for this integration.
