# M1 local checkpoint implementation

Status: **M1-01 passed at `3b135a6`** on Windows native and WASM. The exact session profile
and eight acceptance groups are defined by [the closeout contract](m1-closeout.md#m1-01-checkpoint-profile).
This document does not expand that profile or claim a whole-game save feature.

## Random component

`formats::encode_random_checkpoint` and `decode_random_checkpoint` exchange
owned UTF-8 JSON using the pinned jart parser and the existing strict duplicate-key,
Unicode and resource checks. Format `openciv2-random-checkpoint/1` contains exactly
`format`, `policy`, `domain`, `context`, `engineSeed`, `draws`, `controlled`, `trace`.
Policy is `{id: "oc2-mt19937-64", version: "2"}`. Domain names follow the random
policy. All integers are canonical decimal strings, including full unsigned u64
seeds/counts; no integer passes through a JSON double.

The standard source retains its derived engine seed and draw position. Export
checks that position against the context counter. Restore constructs the standard
library engine and advances it exactly that many times, in temporary state. It
does not apply context seed derivation a second time. The caller supplies a replay
limit (default 1,000,000 draws); encode/decode both reject excess work. This linear
representation is intentional for M1-01. No custom generator, vendor object bytes,
or standard-library stream format is used. Injected source implementations reject,
even if they claim the production source ID.

Controlled input is null or a choice array. Trace is null or an object containing
`limit` and chronological `events`. Each choice stores `siteHex`, `callHex`,
`attempt`, `law`, `code`, and `result`. Hex key strings preserve arbitrary existing
key bytes, including NUL and non-UTF-8 bytes. Law kinds are `primary-code`,
`primary-modulo` (with `n`), `secondary-unit`, and `secondary-range` (with `lo`,
`hi`). Code type follows its law. Result is exactly `{i32: <decimal string>}` or
`{f64: <16 lowercase hex digits of the binary64 bit pattern>}`. Existing F01
validation verifies computed results, key uniqueness, trace budgets and consumed
records. Controlled checkpoints require zero generated draws.

The codec caps input at 16 MiB, combined tape/trace records at 16,384, each context
or choice-key component at 4,096 bytes, and trace capacity at u32. A conservative
encoding-size budget also bounds DOM construction. Structural/semantic validation
precedes replay. Decode returns a new value; failed decode or context admission
cannot replace a destination or consume its RNG. This validates structure and
sampling consistency, not the authenticity of caller-supplied checkpoint bytes.

`tests/random_checkpoint_tests.cpp` checks all three domains across engine-block
boundaries, exact u64 seeds, generated traces, all four controlled laws, consumed
key rejection, byte-valued keys, malformed input and replay limits. Existing source
and sampler tests still check the standard reference vector and unchanged policy.

Validation of this component on Windows and WASM: five checkpoint cases with
12,708 assertions per platform; full suites passed 551 native CTests and seven
WASM targets (527 portable cases). The 115 Python tests passed with two Windows
skips. All 57 conformance observations and consumed prefixes matched native/WASM
and the preceding revision. These are component/regression results, not completion
of the eight session-level M1-01 groups or the final M1-06 platform matrix.

## Local session profile

`SessionRuntime::checkpoint` and `restore_checkpoint` implement the production
boundary. `CheckpointProfile` supplies the named state projection codec;
`CommandDefinition::checkpoint` supplies the versioned continuation codec. These
are trusted, non-reentrant owner functions: encoding is read-only; decoding writes
only supplied staged state, without services or access to the shared map fallback.
No native stack, closure or function pointer is serialized.

The concrete `test.local-foundation/1` profile and `test.checkpoint` operation live
only in `tests/session_checkpoint_tests.cpp`. Fresh defaults are constructor-default
world/process values. Treasury, complete ordinary/multiplayer preferences and token
counters are the mutable state closure; the codec checks all other world, content
and workspace state against those defaults. Immutable default test content has an
explicit versioned identity whose SHA-256 must be supplied again on restore.

The runtime requires one local civilization-1 participant and either no command or
one owned suspended decision. Additional commands, queued inputs, connected
participants and peer-routing work reject. It retains actor/participant identity,
arguments and codec versions, committed effects, interaction choices, recipient,
target, deadline/default, correlations, RNG components and identity/revision counters.
The operation codec retains its phase, debit argument and resolved random choice.
Restore invokes the codec, never the execution factory or committed prefix.

Strict JSON format `openciv2-local-checkpoint/1` contains an exact payload string
and SHA-256. The hash detects accidental damage, not an authenticated author. The
payload binds the profile/content hash and carries explicit owner bytes as hex.
Integers are decimal strings and command text is byte-preserving hex. Limits are
16 MiB input, 4 MiB payload, 512 KiB per owner blob, 4096 bytes per text and 4096
elements per array. RNG replay uses the component's one-million-draw default limit.

Decode/admission and owner codecs complete before no-throw process/runtime
replacement. Failure retains destination state, bindings and RNG. Success revokes
old leases; fresh binding reconstructs notifications from the retained update.
The destination retains its runtime identity and the accepted command retains its
original correlation. Counters retain both saved and destination high-water marks.
Replacing a destination with queued inputs or peer obligations rejects.

## Eight-group acceptance evidence

Test source: `tests/session_checkpoint_tests.cpp`, revision `3b135a6`.
Every row uses owned serialized bytes and the production session API.

| M1-01 group | Executed evidence |
| --- | --- |
| 1. Idle | Separate runtime/process; exact treasury, token counters, complete preferences and distinct saved RNG context names; empty queries/work queue |
| 2. Pending decision | Generated/controlled variants restore the same pending update and match uninterrupted treasury, effects, result and RNG suffix after the same reply; the prefix runs once |
| 3. Cancellation | Treasury 95 and one committed debit survive; matches uninterrupted cancellation |
| 4. Timeout | No action at 49; retained deadline/default acts at 50 and matches uninterrupted completion |
| 5. RNG variants | Gameplay/presentation contexts persist, prefix keys remain consumed and completed tapes are exhausted; component cases cover all laws and engine-block boundaries |
| 6. Bindings/replies | Old and repeated-restore leases reject, fresh binding answers, duplicate/stale replies reject, controller/command/interaction identities advance without reuse |
| 7. Bad input | Truncation, damaged payload, unknown format/policy/profile/operation/codec, content mismatch, bad owner bytes/counters/recipient reject; destination checkpoint and client remain unchanged |
| 8. Unsupported state | World/process/content deviations, participant/domain changes, extra commands, queued inputs and accepted routed peer requests reject; RNG/treasury and routed output survive; missing operation codecs reject |

Seven session cases passed with 241 assertions on each platform. Full verification
passed 558 native CTests and seven WASM targets (534 portable cases, 206183
assertions). All 57 X02 observations and consumed prefixes matched native/WASM and
the preceding revision. Session cases are implementation foundation proofs, not
original-game observations or extra X02 operation registrations.

M1-01 is complete for this profile. Arbitrary world/process state, actual gameplay
continuations, remote queues, F05 wizard persistence and efficient arbitrary-engine
persistence remain M3–M6 work. The final M1-06 matrix remains a separate gate.
