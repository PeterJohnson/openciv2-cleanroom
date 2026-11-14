# M1 local checkpoint implementation

Status: RNG component implemented; M1-01 remains open. The exact session profile
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

## Remaining M1-01 work

Compose this component with explicit owner continuation/state codecs at the session
boundary. Preserve the declared treasury, preferences/tokens, identities/effects,
interaction and counters, admit the matching immutable-content hash, reject all
unsupported state, and replace only after complete admission. Exercise the eight
session scenario groups on native/WASM before marking the gate passed. The authored
operation and its codec belong in tests; no fake command is installed in production.
