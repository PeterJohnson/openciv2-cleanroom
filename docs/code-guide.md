# Reading the source

Start with a module's `.cppm` file. It declares the types and operations available
to its consumers, with Doxygen comments describing ownership, mutation, failure,
and compatibility rules. Larger implementations live in the matching `.cpp`;
smaller ones follow `module :private;` in the interface file. Internal module
partitions, such as `session/runtime_state.cppm`, hold implementation details.

`docs/architecture.md` defines the cleanroom boundary and the algorithm
specifications indexed by `docs/algorithms/README.md` define gameplay behavior.
Comments citing F01, F06, N02, or other specification IDs point to those contracts.
They describe implemented operations, not a claim that the entire referenced
specification is complete. Historical provenance links are not implementation
inputs.

## Where to look

| Directory | Responsibility | Useful starting point |
| --- | --- | --- |
| `src/base` | Defined arithmetic, random domains, callbacks, cancellation, identities | `arithmetic.cppm`, `random.cppm` |
| `src/state` | Live world records, rules, preferences, and owned text | `game.cppm`, `records.cppm`, `rule_state.cppm` |
| `src/world` | Doubled map coordinates, topology, layers, and tile updates | `geometry.cppm`, `map.cppm` |
| `src/unit`, `src/city`, `src/civilization`, `src/ai`, `src/turn` | Specification-owned mechanics | `unit/stacks.cppm`, `ai/goals.cppm`, `turn/calendar.cppm` |
| `src/rules` | Normalize and install selected rule inputs | `rules_install.cppm` |
| `src/formats` | Classic byte/text codecs and native content decoding | `bytes.cppm`, `classic_save.cppm`, `content_pack.cppm` |
| `src/session` | Setup/load/save workflows, command admission, clients, and publication | `runtime.cppm`, `client.cppm`, `commands.cppm` |
| `src/network` | Classic messages, state images, compression, and stream framing | `messages.cppm`, `sections.cppm`, `stream.cppm` |
| `src/query`, `src/analysis` | Owned observations, file inspection, diffs, and optional SQLite export | `query/stored.cppm`, `query/live_records.cppm` |
| `src/io`, `src/platform` | Portable I/O contracts and native file/menu adapters | `io/stream.cppm`, `platform/classic_files.cppm` |
| `src/viewmodel`, `src/ui` | Shell decisions and their graphical presentation | `viewmodel/shell.cppm`, `ui/interface.cppm` |
| `src/application.cppm` | SDL application composition and event/frame lifecycle | `Application` |

## Contracts to keep in view

- **Stored and live values differ.** `formats::Stored*` types retain classic file
  fields and opaque bytes. Live state uses semantic values, owned strings, and
  lifetime tokens. Codecs serialize explicit widths and byte order; they do not
  dump native objects.
- **Slots do not identify lifetimes.** Unit and city targets carry both a slot and
  a token. Pool scan bounds can include holes and are distinct from live counts.
- **Borrowed services need stable owners.** References, spans, and `CallbackRef`
  do not own their backing. Read each operation's lifetime and reentry contract
  before retaining a view or invoking an injected service.
- **Randomness is explicit.** Gameplay, presentation-content, and cosmetic
  contexts are separate types. Semantic choice keys identify decisions; they are
  not draw indices. Equal-bound sampling may consume no decision.
- **Publication belongs to the operation.** `SessionRuntime::publish()` advances
  observations at specified boundaries. A failure can retain earlier effects;
  staged decoding and replacement are documented separately where supported.
- **Classic arithmetic is intentional.** Wrapping integers, signed-count shifts,
  clamp ordering, coordinate aliases, and one-step wrapping encode behavioral
  contracts. Do not replace them with conventional arithmetic based on names
  alone.

## Browsing the API

With Doxygen installed, run this from the repository root:

```sh
doxygen Doxyfile
```

Open `build/api-docs/html/index.html`. The configuration recognizes `.cppm` as
C++, includes source navigation, and writes diagnostics to
`build/api-docs/warnings.log`. Documentation generation is separate from the
normal build and does not fetch dependencies.

## Maintaining comments

Document a class's responsibility and ownership at its declaration. Document a
function's observable result and any nonobvious precondition, mutation, failure,
or lifetime rule beside the declaration callers read. Use `///` comments and
`@param`, `@return`, or `@throws` when they clarify a contract. Defaulted special
members and straightforward forwarding definitions generally inherit the type
or interface contract instead of repeating it.

Keep specification references and explain why unusual ordering or arithmetic is
required. Put implementation rationale near the relevant branch. Avoid comments
that merely restate the name, and update the contract whenever behavior changes.
