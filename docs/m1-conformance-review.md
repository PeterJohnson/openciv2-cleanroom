# M1-03 conformance inventory and review

Status: **open**. The fixed inventory and integration-family mapping are recorded;
independent per-fixture review is pending. This document does not claim that the
implementation's successful execution is independent expectation review.

## Fixed review packet

[m1-review-inventory.json](conformance/m1-review-inventory.json) records exactly
the 57 fixture IDs and 15 operation IDs from baseline `b125df5`. Its paths and
SHA-256 values were checked against the current registry and files at `d65e659`.
Each fixture has pending fields for domain, expected values, provenance, excluded
state and disposition. Reviewer identity and reviewed revision are deliberately
unset. The reviewer must not be the author of the reviewed expectations. Record
findings against these hashes and update the packet after any correction.

The repository search found no completed independent review of this fixed set.
The user has been asked whether the earlier external review contains it. Until
that evidence arrives or another independent review is available, M1-03 stays
open. Do not treat this inventory as an approval or invent review dispositions.

The native/WASM reports refreshed with the `597dec1` implementation inputs execute
all 57 vectors. Observations and consumed prefixes match between platforms and
the preceding reports. The `d65e659` CLI change leaves those adapter inputs
unchanged. Final candidate reporting remains part of M1-06/M1-07.

`F02.prepareNewGame` is the declared foundation projection, with its fixture
exclusions retained; it is not a full playable game or arbitrary pending-world
checkpoint. `F02.cityCalculationEntry` uses supplied C03 children and does not
establish the implementation of those children. No additional operation families
are needed to close M1-03.

## Exactly eight integration evidence families

Paths below are relative to the repository. Native Catch2 cases are registered
individually in CTest; the portable cases execute together in WASM's `viewmodel`
target. CLI tests execute only where native tools are built. Optional SQLite
checks require the enabled configuration in the final M1-06 matrix.

| Family | Test files and checks |
| --- | --- |
| Fresh-game isolation | `tests/fresh_game_tests.cpp`, `tests/world_entry_tests.cpp`, `tests/session_tests.cpp`: cold/reused/cancelled/failed-entry equivalence, mutable state replacement, retained process preferences/tokens, pending-command and required-output guards, stale-client revocation |
| Commands/authority/queries and M1-01 | `tests/command_tests.cpp`, `tests/session_tests.cpp`, `tests/query_tests.cpp`, `tests/session_checkpoint_tests.cpp`: command/interaction identity, controller authority, fake-peer correlation and no reroll, owned query results and RNG isolation; the eight checkpoint scenario groups in `docs/m1-checkpoint.md` |
| Content/parsing and M1-02 | `tests/content_tests.cpp`, `tests/content_pack_tests.cpp`, `tests/rules_tests.cpp`, `tests/rules_install_tests.cpp`, `tests/classic_rules_tests.cpp`, `tests/classic_text_tests.cpp`, `tests/classic_labels_tests.cpp`, `tests/city_names_tests.cpp`, `tests/city_preferences_tests.cpp`, `tests/content_conversion_tests.cpp`; `content_cli`, `content_pack_cli`, `rules_cli`: typed admission, mounts/receipts, grammar, reload policy and the three-row conversion acceptance table |
| Save/preferences/semantic file choice | `tests/save_tests.cpp`, `tests/save_aux_io_tests.cpp`, `tests/classic_load_tests.cpp`, `tests/classic_save_tests.cpp`, `tests/classic_preferences_tests.cpp`, `tests/classic_save_menu_tests.cpp`, `tests/classic_load_dialog_tests.cpp`, `tests/native_classic_tests.cpp`; `live_save_cli`, `preference_cli`: bounded current/legacy loading, coherent export values, honest write/close failures, accepted preference effects, semantic file selection and supplied gameplay-child limitations |
| Bounded stack repair | `tests/unit_stack_tests.cpp`, `tests/classic_loaded_units_tests.cpp`: passive diagnostics, bounded traversal/cycles, valid finite stacks, repair and routed post-load effects, explicit stops without inventing gameplay children |
| Stored inspection and SQLite export | `tests/query_tests.cpp`, `tests/sqlite_export_tests.cpp`, `tests/native_io_tests.cpp`; `inspection_cli`, `sqlite_export_cli`: stored projections, source fidelity and diagnostic scope, exact SQL rows/BLOBs, cancellation, staging/no-overwrite and failure cleanup; supported raw-map/current-save boundaries remain explicit |
| N05 framing/envelopes | `tests/network_stream_tests.cpp`, the N05 composition case in `tests/network_message_tests.cpp`: little-endian headers, fragmented input, partial-send FIFO, routing/envelopes and bounded rejection; no full original-peer session claim |
| N02 sections/checksums/compression | `tests/network_message_tests.cpp`, `tests/network_compression_tests.cpp`: section descriptors, checksum/mirror behavior, retained block order, byte/word thresholds and tails, bounded expansion and failure behavior; synthetic producers do not establish complete live-world section application |

The current local regression evidence is 570 native CTests at `d65e659`, and
seven WASM targets with 546 portable cases at the unchanged `597dec1` core.
These establish execution for applicable configurations; they do not close
independent review, original-file execution or the remaining platform matrix.
