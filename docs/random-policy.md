# Random policy: oc2-mt19937-64, version 2

This document defines the MT19937-64 reference replay policy used by the
reference fixture corpus. **Production C# uses [PCG32 version 1](random-policy-pcg32.md).**
This reference does not specify production seeding or checkpoint representation,
and neither policy reproduces an original-game sequence. F01 owns the sampling
laws; replay policies only supply raw codes and add no classic save/wire fields.

The reference contexts have distinct gameplay, presentation-content and cosmetic
domains, each owning its state. Explicit unsigned 64-bit seeds and stable ASCII
context names determine initialization; addresses and scheduling are not identities.

Initialization applies FNV-1a-64, starting at decimal 14695981039346656037 and
multiplying by 1099511628211 after each byte xor, to the domain label followed by
one zero byte followed by the context name. Labels are `gameplay`,
`presentation-content` and `cosmetic`. The engine is initialized through its
single-integer `seed()` overload with that hash xor the supplied seed; draw count
starts at zero. Hash arithmetic reduces modulo 2^64. Labels isolate
ownership and reproducibility; they are not a claim of cryptographic separation
or statistically independent streams for every possible user-supplied seed.

Each nonconstant decision advances its owning context once, in semantic call
order. Independent future workers must receive separately named contexts or have
their decisions serialized; this version does not allow scheduling-dependent
assignment of draws. A site name identifies the caller's semantic decision for
instrumentation, not a separate keyed stream. Repeating a site draws again;
callers explicitly retain and reuse resolved results when their contract does.

The code generator is MT19937-64, whose
[transition, output and seeding algorithms](https://eel.is/c++draft/rand.eng.mers)
and [predefined parameters](https://eel.is/c++draft/rand.predef) are standardized.
There is no local implementation of the generator. The selected engine and its
integer seeding overload fix the raw output sequence across conforming platforms.
Reference calculations use this exact engine and the explicit F01 mappings.

The engine is behind the owned `RandomSource` interface. Its production
reference adapter uses MT19937-64; validation can supply a source
with authored raw words. Contexts still own F01 mappings, domain boundaries,
draw accounting and semantic traces. Injection transfers exclusive ownership of
an already initialized source and does not apply the seed/name derivation again.
Sources expose a stable implementation identity, clone their complete state and
compare logical positions. They must not share mutable state, invoke game services
or callbacks, or change the floating-point environment. A failed draw leaves the
source unchanged; the context then retains its counter and consumed-choice prefix.
Mock source identities are separate from the production policy identity and are
not admitted by the X02 production replay adapter. Controlled semantic tapes
remain the choice-level test interface; raw mocks do not replace their key/law checks.

Each nonconstant draw calls the engine once. Primary codes use the top 15 output
bits; secondary codes use the top 32. The existing F01 modulo/range mappings
preserve the specified biases, endpoints and floating-point evaluation.
This does not claim original trajectories or cryptographic quality.
`primaryModulo(n=1)` and equal secondary bounds return without consuming a code.
Invalid divisors, invalid sites for nonconstant draws and unsupported floating
rounding modes fail before advancing state. Constant results do not request a
decision site or code. Ordinary arithmetic uses the F01 mappings in `oc2_base`.

A checkpoint owns `{version, domain, context, engine, draws}`, including an
independent source clone (the entire MT19937-64 state and position for the
production source). Copying a checkpoint or context never shares mutable source
state. Restore checks version/domain/name/source identity before changing the
destination. The original seed is
unnecessary for continuation. The draw counter may reach 2^64−1; another draw
then fails before mutation. The [M1 RNG component codec](m1-checkpoint.md#random-component)
persists these values using a bounded seed/position replay representation plus
controlled records. It is not a full session/pending-interaction checkpoint.
Never persist the engine's object bytes.
Game operations must retain their own arguments, resolved choices and
continuations separately.

Checkpoints now also own optional controlled input and a bounded semantic trace,
as described below. This extends the logical in-memory record, without changing
the version 2 generator sequence. The separate component codec versions its format.
Restore prepares and validates all records and lookup state before replacement.
It checks unique keys, mapping results, trace budgets and consumed records against
their tape; a controlled checkpoint must have zero generated draws. A generated
trace cannot contain more events than generated draws. Validation does not prove
the provenance of a caller-supplied engine or its relation to a hand-edited trace.
The engine owner must capture trusted checkpoints at an admitted boundary.

## Controlled choices and semantic traces

Each typed random context accepts an owned `ControlledChoices` input in place of
a seed. Operations use the semantic overloads with `ChoiceKey{site, call, attempt}`.
Site/call are nonempty symbolic strings, and attempt is a nonnegative i32 semantic
candidate/round identity. A key is independent of tape position or a global draw
counter. The original site-only overloads remain available for generated contexts
without semantic recording. Nonconstant site-only calls fail when controlled
input or semantic recording is active; there is no fallback generator draw.

`PrimaryCode`, `PrimaryModulo{n}`, `SecondaryUnit` and `SecondaryRange{lo, hi}`
encode X02's four laws and exact arguments. Codes retain signed i32 versus
unsigned u32 types. Results retain i32 versus binary64, compared by exact bits.
Installation checks every supplied code/result against F01, including biases,
inclusive endpoints, signed wrap, and negative-zero disagreement. Duplicate
`(site, call, attempt)` keys are rejected. Each request looks up its key, matches
the exact law/arguments and consumes the record once. Independent sites may be
requested in a different order than the tape. Swapped range arguments still
constitute an input mismatch even though their F01 mappings agree.

Missing, reused, mismatched or malformed choices raise `ChoiceError` with a stable
category and, where applicable, the key. The already-consumed prefix remains.
At the registered stop boundary the harness calls `require_choices_consumed()`;
any unused mandatory input fails. Constant decisions return without a key, code
or trace event. Supplying a redundant constant choice therefore leaves an unused
input. The context neither synthesizes retries nor chooses deterministic ties;
the owning operation assigns attempts, retains reused decisions and samples at
the specified live-state boundary. No provider callback executes inside a sampler.

Controlled mode always retains an owned chronological trace, bounded by its input
size, and leaves the dormant engine/counter untouched. Generated contexts can opt
into `record_choices(limit)` for a bounded capture. Budget exhaustion rejects the
next decision before generator advancement. Tracing requires unique semantic
keys, records actual codes/results, and changes no policy v2 outputs. Ordinary
generated contexts keep no decision history. The owning typed context supplies
domain/name metadata; one context never requests input from another.

Checkpoint restore includes input, consumed prefix and trace budget, so a consumed
choice remains unavailable after restore and the same suffix can be reproduced.
A trusted session owner may install a matching domain/context checkpoint through
its engine-only random port. `SessionRuntime` exposes explicit
`random_choice_<category>` command failures while retaining published effects.
Restoring a random context while its existing continuation is suspended is tested;
serializing/reconstructing that continuation, world/process state, client inputs,
effects and routing queues remains separate pending-session checkpoint work.

These semantic records are independent of X02 JSON. The sampler fixture
adapter validates event IDs, fixture key uniqueness, typed choices and the
registered sampling boundary. Broader provider/state dependencies remain the
responsibility of their owning operation adapters.

## Portable fixture replay contexts

The X02 sampler adapters register `F01.randomContexts` for implementation replay.
Their single executing context is named `fixture` in the explicitly selected
domain. Its map key is `<domain>/fixture`; its value has typed `domain`/`name`
text and unsigned u64 `seed`/`draws`. Seed is the original seed material before
domain/name derivation; draws is the complete stream position, not a per-fixture
count. Together they reconstruct the entire policy v2 generator through its
single-integer initialization and exactly `draws` advances. A seed alone cannot
install a replay fixture. Entry state must equal the explicit random contexts;
exit state retains the origin and records the final position.

This is a portable logical representation of a reachable generated context,
independent of standard-library object layout or stream serialization. The
adapter verifies that reconstructing its emitted origin/position equals the
actual final engine before publishing the snapshot. Reconstruction is linear in
the position; the fixture's host watchdog bounds this work. This representation
is not an efficient general persistence codec, a controlled-tape checkpoint, or
a suspended-session checkpoint. Arbitrary hand-edited generator states and
preexisting harness traces are outside this registered replay boundary.

Policy identity is `{id: "oc2-mt19937-64", version: "2", sha256: <this file hash>}`.
Controlled fixtures omit generator state. Both modes record actual semantic
codes/results; only replay events identify the policy-local context. Constant
decisions record neither a random event nor an advance. The adapter receives
inputs only; the fixture's expected operation result/state/trace never enter it.

Replay regression vectors use the standard `mt19937_64` default seed 5489 and
[10,000th output](https://eel.is/c++draft/rand.predef) 9981545732273789042,
with seed material chosen to undo the declared domain/name xor. Their expected
codes are projections of that independent standard vector and their results
follow F01. They establish this implementation policy, not an original trajectory.

Reference vectors test controlled mappings in all three domains, invalid input/prefix
preservation, bounded generated traces, restore across engine state blocks, and
an authored command with retries, deterministic ties, retained choices across an
interaction, a live read after a reply, and presentation-consumer invariance.
The standard's 10,000th-output reference vector checks seeding, advancement and
both code projections on native and WASM. Policy and standard-library vectors
do not count as original-program observations. No OpenCiv2 or reconstruction
code is an implementation input.
