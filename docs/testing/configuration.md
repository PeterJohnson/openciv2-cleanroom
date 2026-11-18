# Accepted configuration contracts

`Configuration`, `MultiplayerOptions` and `DoubleMovement` implement accepted
settings, persistence, rule transformation and bounded multiplayer proposals.
Authoritative contracts are F03 §8, F06 CIV2.DAT and N03 §11. These are current C#
APIs with explicit owned inputs and cancellation/failure boundaries.

## Public operation inventory

| Operation | C# API | Boundary |
| --- | --- | --- |
| Game options | `Configuration.Game` | Nullable accepted choices; sound/music before remaining settings, remembered full word, real preference writer. |
| Graphics options | `Configuration.Graphics` | Six settings; advisory herald warning does not veto acceptance. |
| Message options | `Configuration.Messages` | Rebuild disabled-message set, including clearing unknown entries. |
| Double movement | `DoubleMovement.Apply` | Current land movement bytes, signed removal, retained applied flag and supplied redraw child. |
| Multiplayer proposal lifecycle | `MultiplayerOptions` constructor, `Poll`, `Cancel`, `Proposal`/`Result`, `Dispose` | Owned bounded channel and world activity; explicit disposal, no finalizer or modal event loop. |
| Configuration dependency records and accepted choices | `GameOptionServices`, `GraphicsOptionServices`, `MultiplayerOptionServices`, existing Contracts configuration records/interfaces | Providers retain their identities across calls and suspended polls. Contracts have no Core/IO/GUI dependency; Session supplies composition. |

Production files are `src/OpenCiv2.Session/Configuration.cs`,
`MultiplayerOptions.cs`, `src/OpenCiv2.Core/Rules/DoubleMovement.cs`,
and the existing `src/OpenCiv2.Contracts/Configuration.cs`.

## Meaningful test mapping

Nine shared groups in `tests/OpenCiv2.FoundationCases/ConfigurationCases.cs`
cover the admitted behaviors:

| Behavior | Shared C# groups |
| --- | --- |
| Cancelled groups; missing required providers before effects | `F03.configuration-cancellation-admission` |
| Eleven individual game bits, unrelated-bit preservation, exact preference output | `F03.configuration-game-bits` |
| Four old/new music transitions, child ordering and publication; same provider across calls | `F03.configuration-music-transitions` |
| Failed open/item/close, retained accepted values, child exceptions and released world pin | `F03.configuration-failure-prefixes` |
| All six graphics bits, already-enabled herald bypass, advisory warning order and throwing warning prefix | `F03.configuration-graphics-warning` |
| Each disabled message, clear unknown bits, all enabled/disabled and failed persistence | `F03.configuration-message-replacement` |
| Publication and world replacement guards before/after client-session admission | `F03.configuration-world-pin-publication` |
| All 256 movement bytes, land/air/sea/unknown domains, current signed-byte removal | `F03.double-movement-byte-laws` |
| Single-player and repeated-request no-ops; complete table before missing/throwing redraw | `F03.double-movement-gates-failure` |

Seven shared groups in `MultiplayerOptionCases.cs` cover multiplayer options:

| Behavior | Shared C# groups |
| --- | --- |
| Nonzero dialog result, missing wiring, no persistence on admission failure | `N03.options-admission` |
| Begin yields before poll, pending state, owned proposal, exact pre-proposal save, publication/effect order, unchanged RNG and once-only cleanup | `N03.options-yield-apply` |
| Client-only chat save, unchanged values, local modes/lone host, save failure does not veto | `N03.options-local-gates` |
| Rejection restores current live values, negative pending and non-rejection status values, 2400/2401 threshold, signed/wrapping ticks | `N03.options-rejection-deadlines` |
| Handoff skips first clock, fresh completion tick, label/count effects, current publication gates and selected tail | `N03.options-handoff-live-effects` |
| Cancel before work, during persistence/begin and after yield, poll reentry guard, deferred cancellation and disposal | `N03.options-cancellation` |
| Begin/poll/clock/label/save/publication/rejection failures, retained effects, cleanup and same broadcaster across votes | `N03.options-child-failures` |

Tests use actual production preference encoding through authored in-memory
storage and bounded channel providers. They do not claim native audio, GUI
settings dialogs, real peer messaging, or the other N03 turn workflows.

## Deliberate managed boundaries

- Accepted choices and proposal strings are owned immutable values. Proposal
  construction does not overwrite the process's old byte-oriented presentation
  scratch slots. NUL termination of supplied labels/names retains the specified
  visible prefix; managed text otherwise remains full-width logical text.
- Multiplayer production/movement/simultaneous options use the existing semantic
  Boolean model and Contracts broadcaster. This slice does not introduce original
  private signed backing words into the game model or a wire transport adapter.
- A supplied vote channel is transferred even on constructor rejection. Finish
  cancels and disposes it once, then releases the world activity. Channel cleanup
  is a nonthrowing contract; violations cannot replace an earlier failure.
  Explicit operation cancellation/disposal reports a failing notification while
  still completing cleanup. Abandoning an undisposed operation is not a supported
  world-lifetime mechanism.
- Settings save failures retain accepted values. Child exceptions retain their
  completed prefixes. A settings operation has no inferred transaction rollback.
- Double movement transforms only rule values. It neither edits existing unit
  movement nor calculates production. The missing-redraw failure occurs after
  the completed transformation, matching the admitted child boundary.

N03 is now included in every compiled-input identity and in the verifier's
expected manifest, so a changed vote contract invalidates stale test evidence.
Linux native and Chromium WASM results are recorded in the validation guide;
the [hosted CI receipt](../validation/README.md) additionally records passing native groups
on macOS ARM64 and Windows x64.
