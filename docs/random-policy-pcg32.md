# Random policy: oc2-pcg32, version 1

Status: generated and controlled managed contexts, bounded semantic traces,
in-memory checkpoint admission and the portable RNG checkpoint codec are implemented.
The bounded local session checkpoint and managed X02 adapter, with explicit
versioned replay fixture replacements, are implemented. This policy implements
F01 §§2–4,6 reproducibility; it is not
an original-game sequence. The historical [MT policy](random-policy.md) and its
fixtures remain unchanged; managed replay uses separate versioned replacements.

## Engine and initialization

Use PCG-XSH-RR 64/32 (LCG), explicitly selected through **RandN.Pcg32 0.5.0**
and its locked RandN.Core dependency. The package implements the
[standard PCG32 algorithm](https://www.pcg-random.org/using-pcg-c-basic.html).
Only `Create(stateSeed, stream)` and `NextUInt32()` enter the game adapter;
package distribution helpers and changeable default generators are not used.
The standard stream selector has 63 significant bits; its high bit is discarded.

The caller supplies unsigned 64-bit seed material (including zero), a domain and
a stable context name. Domain labels are `gameplay`, `presentation-content` and
`cosmetic`. Context/site names are nonempty ASCII letters, digits or `.-_/`.
No address, locale, worker identity or scheduling order is a seed/context input.
Host seed acquisition is separate and must record the chosen seed.

FNV-1a-64 starts at 14695981039346656037 and, for each byte, xors that byte and
multiplies by 1099511628211 modulo 2^64. Hash the following ASCII byte sequences,
where `NUL` is one zero byte:

```text
identity  = domainLabel + NUL + contextName
stateSeed = FNV64("oc2-pcg32/state" + NUL + identity) XOR suppliedSeed
stream    = FNV64("oc2-pcg32/stream" + NUL + identity) AND 0x7fffffffffffffff
```

Initialize through PCG's reference seeding convention, with `stateSeed` as
`initstate` and `stream` as `initseq`. Seeding's internal warm-up operations are
not logical draws. The counter starts at zero. Domain derivation provides stable
ownership separation; it is not a cryptographic or collision-free guarantee.

## Sampling and ownership

Each nonconstant decision advances its owning engine exactly once. Primary codes
use the word's upper 15 bits (`word >> 17`); secondary codes use all 32 bits.
F01's modulo bias, inclusive unit endpoints, binary64 rounding points, wrapping
range arithmetic and retained decisions remain unchanged. In particular, do not
replace the secondary range with an unbiased integer distribution.

`PrimaryModulo(n=1)` and equal secondary bounds return without a draw or site
validation. Invalid divisors and nonconstant site names fail before advancing.
A draw at counter 2^64−1 fails before mutation. `GameplayRandom`,
`PresentationRandom` and `CosmeticRandom` are distinct sealed types; one context
never consumes another's state. The session lane owns mutation. Contexts are not
thread-safe and draw assignment must not depend on worker scheduling.

The first slice assumes the normal .NET nearest-even binary64 environment and
checks exact output bits on native and WASM. Alternate native floating-point
environment handling remains an integration gate before introducing native host
callbacks that could change that environment.

## Capture and restore

The immutable logical record contains policy ID/version, domain/context and
`{derivedStateSeed, canonicalStream, draws}`. It owns no mutable engine object.
Controlled checkpoints also own the supplied decisions and the chronological
consumed prefix; generated contexts optionally capture a bounded trace. Choices
retain exact binary64 result bits, including signed zero. Keyed requests match
semantic identity and law, never tape position. Missing, duplicate, mismatched and
unused decisions are explicit failures; controlled execution never draws from the
engine. Constant decisions consume neither a choice nor a trace slot. Site-only
calls cannot bypass active controlled input or tracing.

RandN 0.5.0 does not expose a public current-state/clone API; the adapter uses
bounded seed/position reconstruction. Engine capture is constant work; capturing
an active trace copies its events. Restore validates logical choice records and
consumption before O(draws) reconstruction, with an explicit caller replay limit.
Reconstruct separately and
replace the destination only after success. There is no private-field reflection.

Reject policy/version/domain/context mismatches, noncanonical streams and excess
replay work before mutation. Two restores produce independent engines. This is
an initial bounded representation, not a scalable whole-game checkpoint format;
Choose an efficient documented state adapter if future checkpoint positions
outgrow the budget. Never serialize package object memory or silently reinterpret
an MT checkpoint under PCG. Old generated sequences need not be preserved.

Classic files and wire messages acquire no RNG fields.

### Portable RNG checkpoint representation

`RandomCheckpointCodec` implements `openciv2-random-checkpoint/2`. Version 1
belongs to the historical MT policy and is explicitly unsupported. Version 2
contains exactly `format`, `policy`, `domain`, `context`, `engineSeed`, `stream`,
`draws`, `controlled` and `trace`. Policy is `{id: "oc2-pcg32", version: "1"}`;
the engine fields are the already-derived logical PCG position, not original
host seed material. All integers are canonical decimal strings, including full
u64 seeds/counts and the canonical 63-bit stream. Restore never derives them again.

Controlled input is null or a supplied choice array; lookup still
uses semantic keys rather than array position. Trace is null or `{limit, events}`.
Each choice has `siteUtf16`, `callUtf16`, `attempt`, `law`, `code` and `result`.
Structured choice keys preserve arbitrary managed strings: four lower-case hex
digits per UTF-16 code unit, including NUL, byte-valued characters and lone
surrogates. This is an explicit logical string encoding, not CLR object storage.
The ASCII name restriction above applies to context names and site-only calls.
Sampling laws remain `primary-code`, `primary-modulo` with `n`, `secondary-unit`,
and `secondary-range` with `lo`/`hi`. Results are exactly `{i32: decimal-string}`
or `{f64: sixteen-lower-case-hex-digits}` representing the binary64 bits.

The codec caps input at 16 MiB, combined supplied/trace choices at 16,384, context
names and key components at 4,096 characters/code units, and trace capacity at
u32. Declaring a large trace capacity does not allocate that capacity. A conservative
encoding budget bounds DOM construction before encoding. The default replay limit
is 1,000,000 draws and callers can lower it. Encoding and decoding validate policy,
sampling consistency, duplicates, consumed records and resource limits; metadata
validation precedes bounded engine reconstruction. Strict parsing rejects unknown
or duplicate fields, malformed Unicode JSON, noncanonical numbers and unsupported
versions. Returned checkpoint values own their data. This component is not the
local session profile or a whole-game checkpoint, and does not authenticate input.

## Portable fixture replay contexts

Managed X02 replay uses the same logical `F01.randomContexts` closure: one
`domain/fixture` entry containing typed domain/name, the original unsigned 64-bit
seed material and unsigned 64-bit logical draw count. The policy identity is
explicitly `oc2-pcg32`, version `1`, with this document's SHA-256. Reconstruction
derives the seed/stream exactly once, advances to the supplied count, and records
one subsequent nonconstant decision. Singleton/equal-bound decisions neither
advance nor emit a sample event. The test adapter admits at most 1,000,000 replay
draws; it rejects excess work before generating observations.

Seven versioned fixtures under `docs/algorithms/conformance-pcg32/` replace the
seven historical MT replay cases for managed runs. Their expected words are
independently calculated from the FNV/PCG formulas by the developer-only
`tools/dotnet/pcg_fixtures.py`, first checked against the published engine vector
and the derived-context vector below. They are implementation-policy regression
expectations, not original-game observations. The other 50 controlled-choice
fixtures retain their exact inputs and expectations. Selection and source hashes
are recorded in `docs/conformance/selection.json`; the historical fixtures and
registrations remain unchanged.

## Validation and provenance

The PCG author's `initstate=42`, `initseq=54` example emits these first six words:
`a15c02b7 7b47f409 ba1d3330 83d2f293 bfa4784b cbed606e`.
This independently checks the engine, seeding and raw-word order.

For seed material zero, gameplay domain and context `session/main`, the policy
derives state seed `cefe7b236446324f` and stream `773610f86f42849e`. Its first words
are `1f35ca66 7064c4ec 652d0716 5dd5c3d6 b9e0258e 5dd19c44`. These are policy
regression vectors, independently calculated from the declared FNV/PCG formulas
with the PCG calculation first checked against the published vector. They do not
establish classic-game trajectories.

The shared managed tests additionally cover primary projections, F01 mappings,
context isolation, constant cases, invalid input, independent restoration and
failure non-mutation. SDK/package upgrades must preserve these policy outputs or
introduce an explicit new policy version. The
[validation guide](validation/README.md) records executed tests and remaining
platform/whole-game evidence.
