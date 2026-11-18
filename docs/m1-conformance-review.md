# Foundation conformance review

The active selection has fifteen operation IDs and 57 fixtures. Fifty controlled
specification vectors and seven versioned PCG32 replay vectors have distinct
provenance. Review expected values independently of the executing adapter; record
the reviewer, revision, scope and unresolved differences. Passing against captured
implementation output is not independent expectation review.

Use [conformance registration](conformance/README.md) and the
[current test guide](testing/README.md). Full G01–G23 gameplay integration remains
later milestone work. No extra adapter for each implementation helper is required.

## Exactly eight integration evidence families

| Family | Test files and checks |
| --- | --- |
| Fresh-game isolation | `tests/OpenCiv2.FoundationCases/FreshGameCases.cs`, `tests/OpenCiv2.FoundationCases/WorldEntryCases.cs`, `tests/OpenCiv2.FoundationCases/SessionCases.cs`: cold/reused/cancelled/failed-entry equivalence, mutable state replacement, retained process preferences/tokens, pending-command and required-output guards, stale-client revocation |
| Commands/authority/queries and M1-01 | `tests/OpenCiv2.FoundationCases/CommandCases.cs`, `tests/OpenCiv2.FoundationCases/SessionCases.cs`, `tests/OpenCiv2.FoundationCases/QueryCases.cs`, `tests/OpenCiv2.FoundationCases/SessionCheckpointCases.cs`: command/interaction identity, controller authority, fake-peer correlation and no reroll, owned query results and RNG isolation; the eight checkpoint scenario groups in `docs/m1-checkpoint.md` |
| Content/parsing and M1-02 | `tests/OpenCiv2.FoundationCases/ContentCases.cs`, `tests/OpenCiv2.FoundationCases/PackCases.cs`, `tests/OpenCiv2.FoundationCases/RuleCases.cs`, `tests/OpenCiv2.FoundationCases/ClassicRuleCases.cs`, `tests/OpenCiv2.FoundationCases/ClassicTextCases.cs`, `tests/OpenCiv2.FoundationCases/LabelCases.cs`, `tests/OpenCiv2.FoundationCases/CityNamingCases.cs`, `tests/OpenCiv2.FoundationCases/CityContentCases.cs`, `tests/OpenCiv2.FoundationCases/ConversionCases.cs`; `content_cli`, `content_pack_cli`, `rules_cli`: typed admission, mounts/receipts, grammar, reload policy and the three-row conversion acceptance table |
| Save/preferences/semantic file choice | `tests/OpenCiv2.FoundationCases/SaveCases.cs`, `tests/OpenCiv2.FoundationCases/AuxStateCases.cs`, `tests/OpenCiv2.FoundationCases/GameFileCases.cs`, `tests/OpenCiv2.FoundationCases/CalendarCases.cs`, `tests/OpenCiv2.FoundationCases/BinaryPreferenceCases.cs`, `tests/OpenCiv2.FoundationCases/SaveMenuCases.cs`, `tests/OpenCiv2.FoundationCases/LoadDialogCases.cs`, `tests/OpenCiv2.Base.Tests/NativeClassicTests.cs`; `live_save_cli`, `preference_cli`: bounded current/legacy loading, coherent export values, honest write/close failures, accepted preference effects, semantic file selection and supplied gameplay-child limitations |
| Bounded stack repair | `tests/OpenCiv2.FoundationCases/StackCases.cs`, `tests/OpenCiv2.FoundationCases/DeletionCases.cs`: passive diagnostics, bounded traversal/cycles, valid finite stacks, repair and routed post-load effects, explicit stops without inventing gameplay children |
| Stored inspection and SQLite export | `tests/OpenCiv2.FoundationCases/QueryCases.cs`, `tests/OpenCiv2.Sqlite.Tests/ExportTests.cs`, `tests/OpenCiv2.Base.Tests/NativeClassicTests.cs`; `inspection_cli`, `sqlite_cli`: stored projections, source fidelity and diagnostic scope, exact SQL rows/BLOBs, cancellation, staging/no-overwrite and failure cleanup; supported raw-map/current-save boundaries remain explicit |
| N05 framing/envelopes | `tests/OpenCiv2.FoundationCases/NetworkCases.cs`, the N05 composition case in `tests/OpenCiv2.FoundationCases/NetworkCases.cs`: little-endian headers, fragmented input, partial-send FIFO, routing/envelopes and bounded rejection; no full original-peer session claim |
| N02 sections/checksums/compression | `tests/OpenCiv2.FoundationCases/NetworkCases.cs`, `tests/OpenCiv2.FoundationCases/CompressionCases.cs`: section descriptors, checksum/mirror behavior, retained block order, byte/word thresholds and tails, bounded expansion and failure behavior; synthetic producers do not establish complete live-world section application |

The final regression evidence is recorded in the [hosted matrix](m1-build-matrix.md).
Independent review and [original-file execution](m1-original-closeout.md) retain
their separate provenance and bounded claims.


Current C# sources and tests are mapped in [implementation coverage](testing/coverage.md).
Run the [native/browser and CLI checks](testing/README.md);
[validation status](validation/README.md) records executed evidence and remaining gates.
