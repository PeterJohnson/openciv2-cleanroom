# Determinism and arithmetic

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in F01 |
| --- | --- |
| Gameplay | Numeric results, significant rounding/wrap, sampling laws, retained choices and reproducibility under the selected RNG policy. |
| Original files/content | Numeric encodings belong to their file/content owners; a fixture numeric tag is not a runtime field type. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Original PRNG streams, raw draw counts, machine intermediates, CRT/ABI and per-helper clock traces. |

The algorithms below specify game values/decisions and their real dependencies,
plus explicitly identified external data definitions. They do not prescribe
function boundaries, object/global layout, allocation strategy or incidental
helper/store order. Preserve retained data where an admitted game/file/peer
consumer uses it; represent that data flow explicitly rather than emulating
original memory. Native or synthetic callbacks do not create new game boundaries.
Historical backing-byte/fault/trace cases are reference unless an actual external
format or supported semantic operation consumes the value. Missing gameplay or
format meaning is an owner gap, not permission to invent it or require arbitrary
original bytes. This scope controls stronger historical wording below.

**Required profile:** [X01 fresh-game isolation and bug repairs](X01-classic-compatibility-contract.md#fresh-game-isolation-and-supported-bug-repairs)
and [behaviorally significant ordering](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
override conflicting procedures and examples below. Retention applies within a
game or to explicit imported inputs, not across new games. Confirmed original-bug
stack/backing/stop cases are optional diagnostics; use documented safe repairs.
Only real effects and callbacks impose ordering; test adapters cannot invent them.
Unspecified safe continuations remain explicit gaps, not required arbitrary bytes.

**ID:** F01. **Target:** Civilization II Multiplayer Gold
Edition **5.4.0f, patch 3**, under the [X01 contract](X01-classic-compatibility-contract.md).

This specification defines numeric operations, random decision distributions,
clock-derived inputs and reproducibility within the new implementation. Classic
PRNG algorithms, seed-to-sequence mappings, shared streams and discarded-draw
counts are not requirements. Individual algorithms own their decision rules and
state-dependent ordering; Appendix D preserves the original generators as evidence.

Evidence is pinned to OpenCiv2
`ce18e5e52e19870cf1598651eee7c1a97d517519` and the independent reconstruction in
`civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. Original entry addresses and validation
limits appear at the end. These procedures do not require the original memory
layout or source-language types.

The main text specifies mathematical values, reductions and rounding steps.
An implementation may use larger integers, arbitrary-precision arithmetic or
different storage if it preserves the required game/file/protocol results and
semantic publications. Equivalent internal control flow may differ. Equivalent evaluation need not reproduce unobserved machine
intermediates or execute a cast whose conversion has no effect.
[Appendix A](#appendix-a-original-runtime-and-representation) describes the
original runtime and representation;
[Appendix B](#appendix-b-reference-harness-conventions) describes its replay
test environment.

## Compatibility policy

X01's [presentation policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy)
limits the required numeric/random observations here to simulation, information,
accepted content and file/protocol effects. Purely cosmetic scaling, animation,
city-scene decoration and media choices may use other arithmetic/sampling; their
original formulas are optional reference material. Gameplay probability laws,
AI heuristics, W02 map generation and saved-map predicates are unchanged. Required
turn/network deadlines remain clock inputs; native paint/frame timings need not
match. The [input policy](X01-classic-compatibility-contract.md#input-admission-and-rejection-policy)
allows validation before admitting unsupported values, not changing wrap or
rounding for accepted values or ordinary play. Own-engine reproducibility still
covers declared simulation choices and relevant external events; cosmetic-only
state is needed only for a separately claimed visual replay.

## 1. Numeric model and notation

The following names denote **mathematical domains and conversion operations**,
not required language or storage types. For reference evaluation, an unsuffixed
arithmetic scalar uses the `i32` domain unless its owner specifies otherwise.
Individually declared logical ranges and conversion boundaries take precedence.
This default defines otherwise unstated overflow results; it is not a demand
for a machine-width temporary or a reduction proven irrelevant under the
behavioral-equivalence rule below.

| Domain | Meaning |
| --- | --- |
| `u8`, `u16`, `u32`, `u64` | Integers in `0..2^N-1`, for `N` equal to 8, 16, 32 or 64. |
| `i8`, `i16`, `i32`, `i64` | Integers in `-2^(N-1)..2^(N-1)-1`, for the corresponding `N`. |
| `binary32`, `binary64` | Values and rounding of the IEEE binary formats with 24- and 53-bit significands respectively, including the implicit leading bit. |

For mathematical integer `x`, define:

```text
uN(x) = x modulo 2^N, in [0, 2^N - 1]
iN(x) = uN(x)                    if uN(x) < 2^(N-1)
        uN(x) - 2^N             otherwise
```

These conversions reduce values without saturation. For example,
`u8(260)=4`, `i8(255)=-1`, `i16(32768)=-32768` and
`i32(0xffffffff)=-1`. Widening into a domain containing the value preserves that
value. Signed interpretation still matters: `i8(255)` and `u8(255)` produce
different inputs to later arithmetic and comparisons.

For example, an arbitrary-precision integer can implement a wrapped counter
provided each observable update has the specified `u32` result. The modulo
effect is required when it changes behavior; allocating a four-byte object or
executing a redundant conversion is optional. Bitwise notation below
operates on the mathematical two's-complement representation of the stated
domain, independently of how the implementation stores it.

### Behavioral equivalence and logical ranges

Specify a quantity by its meaning, admitted values and sentinels first. A range
such as civilization identity 0…7, a bounded part count or a signed rule value
requires a representation capable of those values; it does not require the
smallest possible integer type. A range alone does not introduce saturation,
wrapping, input rejection or a new cap at its endpoint.

Separate three contracts:

| Contract | Requirement |
| --- | --- |
| Logical values | Represent all values and sentinels required by the admitted domain. Intermediate calculations may need more range than their inputs or outputs. |
| Behavioral conversions | Preserve modulo reduction, signed reinterpretation, rounding, truncation and exceptional results wherever they affect a required observation. `sN`, `iN`, `signedN` and `signed N-bit` arithmetic notation name the same signed conversion; `uN` and unsigned forms name its nonnegative counterpart. |
| External representation | Decode/encode the exact file, wire, checksum or asset representation specified by its owner. Original private storage widths and ABI layouts are reference details unless implementing that adapter. |

An implementation may use ordinary wider or arbitrary-precision integers,
eliminate redundant reductions, or combine operations when it establishes
**equivalent required behavior over the complete admitted execution domain**.
Sufficient cases include proving that every reached value fits the reference
range, or proving that an unobserved intermediate reduction cannot affect a
later result. For pure integer addition, for example,
`u8(u8(a+b)+c) = u8(a+b+c)`; this does not authorize delaying a publication
visible to a callback between the additions.

The equivalence must cover branches, comparisons, division/remainder, signed
shifts, rounding, failure results, ordered effects, return values, published
state and file/network output. A shared workspace read by a later operation is
observable even if no player sees it immediately. Do not add an internal
checkpoint solely to require an otherwise unobservable machine intermediate.
An arithmetic identity alone does not license moving callbacks, live reads or
random decisions. [X01's ordering convention](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
permits equivalent internal scheduling only when those dependencies, failure
prefixes and observations are preserved.

For example, a bounded identity lookup need not repeatedly narrow a known 0…7
value. A cyclic population update that takes 127 to −128 still needs that
conversion's effect before subsequent food calculations. Delaying conversion
until save/network serialization would change local play. Likewise, signed and
unsigned consumers of the same rule code can require different logical views.

A proof must cover admitted custom content, imported saves/scenarios, remote
updates and reachable subsequent states—not just stock initial values or common
play. Existing X01 rejection policies may exclude an explicitly unsupported
input profile; they do not permit rejecting nominal content merely because its
later play reaches overflow. If equivalence is not established, evaluate the
specified conversions at their original behavioral boundaries. This convention
changes representation freedom, not AI heuristics, world generation or the
classic arithmetic outcomes.

### Integer operations

- An add, subtract or multiply specified in the 32-bit arithmetic domain applies
  `u32` or `i32` to its result in reference evaluation. Preserve their effects;
  omit or delay a reduction only under the behavioral-equivalence rule above.
  In particular, a final reduction does not generally recover overflow before
  a comparison, division or signed shift.
- A conversion to a narrower domain applies its `uN` or `iN` operation at the
  stated boundary. A small stored field does not imply reduction after every
  arithmetic operation involving its value; the owning algorithm specifies that.
- Signed division truncates toward zero: `sdiv(-7,3)=-2`. Signed remainder is
  `a - sdiv(a,b)*b`, so `srem(-7,3)=-1`. This differs from floor division and
  from a language's always-nonnegative modulo operator.
- Unsigned division uses nonnegative values and rounds down. Unsigned remainder
  is nonnegative. Do not exchange signed and unsigned comparisons or divisions.
- For `v` in `i32` and an effective count `k` in `0..31`,
  `asr32(v,k) = floor(v/2^k)`; for example, `asr32(-7,1)=-4`.
  Unsigned right shift is `floor(u32(v)/2^k)`.
  Left shift multiplies by `2^k` and reduces to the stated result domain.
- A 32-bit variable shift reduces its count modulo 32, so a count of 32 means
  zero. This is the rule for the helper below; an owning algorithm must identify
  any operation with a different count rule or value domain.
- Do not reassociate expressions, move a cast across an operation, substitute
  shift for signed division on negative values, or replace integer arithmetic
  with floating arithmetic without proving equivalent required behavior,
  including rounding and failure boundaries.

Signed division by zero and `INT_MIN / -1`, where `INT_MIN=-2147483648`, are
failure cases with no ordinary `i32` result. A caller must exclude these inputs
or specify its failure behavior; zero, saturation and wrap are not substitute
results. Operations depending on carries, multiple numeric domains or exceptional
values need their own explicit contract. The original processor behavior and
source-language pitfalls are described in Appendix A.

### Shared clamp and shift helpers

All arguments here are in `i32`; comparisons are signed. The two clamp forms agree
for `lo <= hi`, but **do not swap reversed bounds**:

```text
clampAB(value, lo, hi):            # int_clamp and int_clamp_b
    return min(max(value, lo), hi)

clampC(value, lo, hi):             # int_clamp_c
    if value > hi: return hi
    return max(value, lo)

shiftSigned(value, shift):
    if shift == 0: return value
    if shift > 0:
        return i32(u32(value) << (u32(shift) & 31))
    count = u32(-mathematical_integer(shift)) & 31
    return asr32(value, count)
```

For reversed bounds `(lo,hi)=(10,0)`, `clampAB(-5,10,0)=0`, while
`clampC(-5,10,0)=10`; both return zero for `value=5`.
`shiftSigned(-7,-1)=-4`, `shiftSigned(0x40000000,1)=-2147483648`, and
`shiftSigned(-7,-32)=-7`. The count calculation also handles `shift=INT_MIN`
without a host-language signed negation overflow.

### Floating point

For the finite, ordinary-magnitude computations below, use a **53-bit binary
significand**, round-to-nearest, ties-to-even, and a separately rounded result
at each specified floating operation. Binary64 arithmetic supplies these results
in the range used here. Another arithmetic system is suitable if its required
results match this rounded reference evaluation. Higher intermediate precision,
fused operations or reassociation must not change those results. Under the
behavioral-equivalence rule, an implementation need not materialize a separately
rounded temporary when it proves the same required outcome.

An algorithm involving underflow, overflow, special values, library math or an
alternate arithmetic environment must specify those details separately. The
normal random-range contract here does not establish equivalence for all
floating expressions elsewhere in the game. Appendix A records the original
precision controls and their relevance to nondefault reference tests.

Floating-to-integer conversion in the range helper truncates toward zero, then
applies the specified `u32`/`i32` conversions. Its products fit the `i64` domain;
no particular intermediate storage type is required.

## 2. Random decision and state boundaries

A required **random decision** is a sampled value that can affect a required
outcome, branch, score, candidate, retry, or observable state. Preserve its distribution,
arguments, reuse of a sampled value, and the point at which it reads or changes
game state. Separate decisions receive fresh samples unless the algorithm
explicitly reuses a value or defines a repeatable key. No particular original
cross-call correlation or seed-to-output sequence is required.

`primaryNext()`, `R()` and “primary” name the sampling law in §3. `secondaryUnit()`,
`secondaryRange()`, `randomRange()` and “secondary” name §4's laws. These labels
in other specifications distinguish distributions; they do **not** require two
process-global generators. “Draw” means a logical sample, not a required count
of underlying PRNG calls. Implementations may partition simulation, map, AI and
presentation randomness, use more than two streams, or derive samples from
stable decision keys. Do not reseed every decision to the same value.

A sample is unused only if no specified observation depends on its value; a
required semantic checkpoint exposing an intermediate score can make it relevant
even if that score is later overwritten; an artificial internal checkpoint cannot
create such a requirement. Discarded samples, equal-bound samples and original
seed exchanges with no remaining non-RNG effect may be omitted. Preserve
significant computations, callbacks, queries, mutations and attempts surrounding
them: an unsuccessful candidate is still an attempted candidate, and an overwritten roll may have affected an
earlier branch. Preserve adaptive retries, live-read dependencies, deterministic
ties and reuse of sampled state. Pure work and independent sample generation
may be reordered under X01; this does not remove a stateful attempt or permit
fresh sampling where the algorithm reuses a choice.

The implementation must define and version its generator(s), seed handling,
stream/key assignment and ordering policy sufficiently to reproduce its own runs.
Capture the random context needed for continuation; this need not have the
original state sizes or process lifetime. Context identifiers must be stable
across replay; native addresses or nondeterministic task scheduling are not
implicit keys. Presentation may use independent randomness and must be included
in a replay only to the extent it affects the
chosen comparison surface or performs real simulation callbacks. Original
presentation-to-simulation RNG coupling is not required.

This freedom is local to random generation. It does not change which peer
executes an operation or how its result is published in multiplayer. Follow
N01–N05's original authority and synchronization contracts; original peers need
neither the new RNG policy nor its internal state. Received game fields and
resolved results remain exact inputs and must not be independently rerolled.
[X01's interoperability requirement](X01-classic-compatibility-contract.md#file-and-original-game-multiplayer-interoperability)
applies even when peers use different generators.

A keyed result included in a declared comparison must remain repeatable for the
same key, relevant inputs and implementation version. Original decorative layout
formulas are optional under X01; required game/map formulas remain exact. A keyed
stochastic mapping need not match the original generator. Define zero as a usable key rather than requiring the
original zero-seed no-op and inherited process stream. The consumer owns its
key expression and scene/state inputs.

`P` is separate logical map state: `0..65535` for a supplied map value and
`1..32767` when newly created by §5. It drives deterministic coordinate predicates,
not a PRNG stream. Preserve its stored value and the owning map formulas exactly.

## 3. Primary sampling law

A primary decision samples a code uniformly from the integers `0..32767`.
`primaryNext()` or `R()` denotes that code. The uniform-code model specifies the
single-decision probability law, not the original generator's correlations.

For positive integer `n`, callers commonly use:

```text
primaryModuloFromCode(code, n):
    return code % n              # code in 0..32767, n > 0
```

Preserve this **modulo bias**. Write `32768 = q*n + r`, with `0 <= r < n`.
Residues `0..r-1` each have probability `(q+1)/32768`; residues `r..n-1` each
have probability `q/32768`. This also defines unreachable residues when
`n > 32768`. An ideal uniform draw from `0..n-1` is a different rule unless
`n` divides 32768. Direct masks, comparisons, shifts and reuse of `R` retain
the formulas in their owning algorithms. A one-value result may be returned
without advancing a generator. Zero/negative divisors retain their separately
specified failure or off-nominal contracts.

A cleanroom implementation may implement the resulting law directly rather
than materializing a 15-bit code. For controlled-choice tests the code is an
explicit input, so threshold and remainder behavior can be checked exactly.
For example, codes `0,1,2,32767` modulo 3 produce `0,1,2,1`; the probabilities
are `10923/32768,10923/32768,10922/32768`.

## 4. Secondary sampling law

A secondary decision uses a uniform integer code `U` in `0..4294967295`.
Define `round53` as nearest-even rounding to a 53-bit binary significand and
`trunc` as mathematical truncation toward zero. The unit law is
`round53(U / 4294967295)`, including both zero and one. The inclusive range
law is the following deterministic mapping of that code:

```text
secondaryRangeFromCode(lo, hi, U):            # endpoints i32; U is u32
    if lo == hi: return lo                   # no sample needed
    if hi < lo: swap lo, hi                  # signed comparison
    difference = i32(u32(hi) - u32(lo))
    adjustment = 0x1.ffffde7210be9p-1         # nearest binary64 to 0.999999
    span = round53(integer_value(difference) + adjustment)
    fraction = round53(integer_value(U) / 4294967295)
    product = round53(fraction * span)
    integral = trunc(product)
    return i32(u32(integral) + u32(lo))
```

`secondaryRange(lo,hi)` samples according to this mapping; it need not use the
original recurrence or floating hardware. Equivalent integer interval weights
are permitted. The rounding points and exact adjustment define the probability
law, including rare endpoint cases; replacing it with an ideal uniform integer
range is an additional behavior change. There is no requirement to reproduce
the original sequence of codes or to consume a sample for equal bounds.

Nominal intervals have mathematical endpoint difference at most 2147483647;
results then lie between the endpoints. Accepted wider signed intervals wrap
the difference negative and can return outside the interval. Keep that numeric
behavior for accepted inputs, or declare a restricted input domain under X01.
This scalar wrap is distinct from excluded out-of-bounds writes.

| Supplied code U | lo | hi | Required result |
| --- | --- | --- | --- |
| 1015568748 | 0 | 2 | 0 |
| 1015568748 | 2 | 0 | 0 |
| Any valid code, unused | 5 | 5 | 5 |
| 1015568748 | -4 | 7 | -2 |
| 0 | -4 | 7 | -4 |
| 4294967295 | -4 | 7 | 7 |
| 1015568748 | -2147483648 | 2147483647 | -2147483648 |
| 1015568748 | -2147483648 | 1 | 1639699275 |

These are mapping vectors, not seed-to-sequence requirements. Generator state
and the number of raw entropy requests are absent from their expected results.

## 5. Clock inputs and the map pattern

Startup clock reads used solely to seed the original generators are historical
in Appendix D. New seed acquisition is an implementation policy and must be
captured for reproducibility. Clocks used for gameplay, callbacks, animation and
map-pattern conversion remain specified inputs; changing the generator does not
relax their arithmetic or ordering effects.

### Game ticks

Let `readMilliseconds()` supply one external millisecond-clock value in `u32`.
The game tick helper computes:

```text
gameTicks():
    milliseconds = external readMilliseconds()
    return u32(6 * milliseconds) / 100       # unsigned integer division
```

The multiplication wraps **before** division. This is not unbounded
`floor(milliseconds*0.06)`. Its output is in `0..42949672`, which also fits `i32`.
Do not replace existing busy-wait comparisons with wrap-aware elapsed-time
formulas unless their owner specifies that behavior; the original helper's
discontinuities are observable inputs.

Creating a new map-pattern seed is a separate operation:

```text
mapPatternSeedFromTicks(ticks):
    result = ticks & 32767
    if result == 0: result = 1
    return result

newMapPatternSeed():
    P = mapPatternSeedFromTicks(gameTicks())
```

`newMapPatternSeed` makes one tick query and no random draw. The input to
`mapPatternSeedFromTicks` is an **already converted game-tick value**, not raw
milliseconds. A supplied map seed remains unchanged unless a caller explicitly
invokes a seed-changing operation; these primitives do not decide when a map
should be reseeded.

| Raw milliseconds | Game ticks | New `P` |
| --- | --- | --- |
| 0 | 0 | 1 |
| 16 | 0 | 1 |
| 17 | 1 | 1 |
| 1000 | 60 | 60 |
| 715827882 | 42949672 | 23592 |
| 715827883 | 0 | 1 |
| 4294967295 | 42949672 | 23592 |

The first multiplication overflow occurs after about 8.28 days of the raw
millisecond count, before that counter itself wraps at 32 bits.

Given already converted game ticks `0,32768,32769,65535`,
`mapPatternSeedFromTicks` returns `1,1,1,32767`, respectively.

## 6. Deterministic execution and replay inputs

Use three distinct validation modes, as formalized by X02:

1. **Controlled choices:** supply codes or decisions at named semantic sites,
   with arguments and attempt identity. Compare formulas, branch results, state
   and ordered effects exactly. Validate injected outcomes against the declared
   law; a trace alone does not prove the implementation's sampler distribution.
2. **Implementation replay:** run the new implementation with its versioned
   random policy, initial contexts and ordered external inputs, then repeat or
   restore a checkpoint. Require identical results within that implementation.
   Include sampler-law tests separately. Original seeds need not match.
3. **Legacy reference:** optionally reproduce Appendix D's recurrences and all
   original discarded draws, reseeds and stream sharing for research. Passing
   this mode is not required for the cleanroom target and does not substitute
   for testing the new implementation's own replay policy.

A deterministic boundary also needs rules, caches, relevant prior-session state,
map pattern P, callback behavior and any backed reads required by X01. Neither
an original save file nor a single seed proves these inputs complete. F06 owns
original serialized fields; new replay metadata must identify the policy and
contexts explicitly, without inventing generator fields in the classic format.
That metadata is not a prerequisite for loading an original or exported classic
save. Across engines, preserve the saved game state and specified load effects;
only future random correspondence is waived. During a mixed multiplayer replay,
capture incoming original-peer messages/results as external inputs; do not
require access to that peer's internal RNG or reproduction of its random sequence.

Each consumer identifies the sampling law, arguments, conditions, retained versus
fresh samples, candidate/retry logic, and state-dependent order. Preserve original
operand evaluation order when it affects non-RNG state or callbacks. Expressions
whose only difference is assignment of samples from the old global sequence may
use the new implementation's declared policy. Do not add random tie breaking to
a deterministic first/last selection.

Trace semantic site, operation/attempt, law, arguments and value. In implementation
replay also capture context IDs, policy version and the state/checkpoint needed
for reproduction. Do not compare unrelated generators' internal states, raw draw
counts or same-seed trajectories. Pure queries must still have no random decision
or change to the implementation's random contexts.

External inputs require an ordered transcript or a fully specified environment
model, including clocks, callbacks, files and network delivery. Preserve real
callback/state effects of presentation even when its RNG is isolated. P05 and X02
own event-loop and fixture details. Appendix B describes the original harness;
its environment variables and virtual clock are evidence tools, not game rules.

## Appendix A: Original runtime and representation

This appendix explains the source-language and processor mechanisms behind the
main mathematical contract. They support source review, reference-state decoding
and validation; they do not prescribe a new implementation's internal types or
processor architecture.

### Scalar storage and instruction behavior

Original scalar `char`, `short`, `int`, `long` and pointers occupy 8, 16, 32, 32
and 32 bits respectively, with plain `char` signed in this build. Multi-byte
memory/file values are little-endian where the relevant record format exposes
their bytes. Specific record layouts and pointer replacements are outside this
numeric primitive contract.

The original RNG states occupy 32-bit words. Decode them as nonnegative `u32`
values for the recurrences, even where reconstructed source declares a signed
state variable. The map-pattern seed occupies an unsigned 16-bit field. Hex
state values in Appendix D and its vectors identify mathematical integers; when
comparing original memory snapshots, encode them in these widths and byte order.

Original narrow stores retain low field-width bits. Later reads sign-extend or
zero-extend according to the recovered type; byte arithmetic is often promoted
to a larger integer domain before the next store. A fixture decoder must preserve
that interpretation. It must not impose an 8-bit reduction after every operation
merely because one source field was a byte.

The x86 variable-shift instructions mask 32-bit shift counts to their low five
bits. Their signed right shift fills with the sign bit, implementing the floor
division specified in the main text. Signed integer division faults on a zero
divisor and on `INT_MIN / -1`. The owning algorithm must specify the corresponding
failure boundary; this does not require a new implementation to trigger a
hardware exception by executing the same instruction.

Decompiler expressions are not themselves portable arithmetic contracts. In
particular, the pinned OpenCiv2 C++ range helper uses `(hi-lo)` directly, which
does not explicitly define portable signed overflow for extreme endpoints. The
independent reconstruction preserves the original wrapping subtraction. Both
must be assessed against the explicit `i32`/`u32` operations in the main text,
rather than the host compiler's interpretation of an overflowing signed expression.

### Floating runtime mechanics

Original startup selects 53-bit x87 precision through
`__controlfp(0x10000,0x30000)`. A stored `double` is binary64, but an x87 register
retains the extended exponent range even with a 53-bit significand. Consequently,
ordinary binary64 operations reproduce the bounded random calculations here;
they do not establish equivalence for all possible floating computations.
Underflow, overflow, special values and library math require their owning
algorithm's contract.

The range helper stores its computed span as binary64 before drawing. The
quotient from the secondary generator remains on the x87 stack for the
multiplication. In the normal arithmetic environment these operations produce
the `round53` results specified in the main text.

For the final conversion, the original runtime saves the control word,
temporarily selects rounding toward zero, converts to a signed 64-bit integer,
then restores the control word. It uses the low 32 bits of that integer for the
endpoint addition. The abstract procedure expresses the same result through
`trunc`, `u32` and `i32`, without requiring a control-word change or an actual
64-bit temporary object.

If a reference test deliberately changes the incoming control word, capture it.
The original range operations use that incoming precision and rounding except
for the final truncating conversion. Such a test needs an explicit arithmetic
environment beyond the normal 53-bit, nearest-even contract; silently assuming
the normal environment is not an equivalent test.

### Clock interfaces

Appendix D's external wall-clock seconds result comes from `_time` in the
original system initializer. The later application initializer requests a 4 ms
timer resolution using `timeBeginPeriod(4)` before its two game-tick queries.
`readMilliseconds()` corresponds to one `GetTickCount` call, returning an
unsigned 32-bit millisecond count. The game-tick calculation then multiplies by
six with 32-bit wrap and divides by 100. Its signed return wrapper preserves
the result because that result fits `i32`.

A new implementation can provide these clock inputs through its own platform
adapter. Preserve values, query order and scheduling effects for in-scope clock
operations. Reads used only for original PRNG startup seeding belong to the
optional legacy-reference mode.

## Appendix B: Reference harness conventions

These conventions explain existing reference recordings. They are environment
inputs for a replay, not the game's native clock algorithm:

| Mechanism | Pinned harness behavior |
| --- | --- |
| `CIV2_SEED` | A nonempty value parsed with base autodetection replaces the seed on **every** primary `srand` call. It does not directly set `N` or `P`. Record the effective `u32` value; avoid relying on host parsing of out-of-range text. |
| Virtual clock | Starts at zero elapsed microseconds with a millisecond baseline of `1000000`. Game `GetTickCount`/`timeGetTime` queries advance elapsed time by 50 microseconds, then return `u32(1000000 + floor(elapsed_us/1000))`. These queries do not dispatch timers in virtual mode. |
| Idle and sleep | A virtual idle step adds 10000 microseconds, dispatches due timers, then runs due script actions. `Sleep(ms)` adds `ms*1000` microseconds and dispatches due timers. |
| Keyboard polling | During replay, `GetAsyncKeyState` adds 50 microseconds, dispatches due timers and runs due script actions before returning key state. It is a scheduling point, unlike a virtual tick query. |
| Wall-clock seconds | During replay, `plat_time` returns `922449600 + floor(elapsed_us/1000000)` without advancing time. This is an epoch-seconds input, not a promise that every calendar API implements an unrestricted calendar. |
| Real-time option | A nonempty `CIV2_REALTIME` disables the virtual tick/idle clock path. Do not claim deterministic scheduling from the script alone in this mode. |
| `CIV2_RAND_LOG` | Captures primary seeding and primary returned values with caller identity. It does **not** trace the secondary stream. |
| `CIV2_CALL_LOG` | Records selected sound and video requests during replay. It does not establish complete media decoding or completion equivalence. |

The full event-loop/timer/input ordering belongs to P05 and X02. Reimplementing
only the clock rows above is insufficient to reproduce arbitrary scripts; either
supply an ordered event/clock transcript or use the pinned harness with its
message queue, timers and script interpreter. Declare which model a fixture uses.

For example, with zero initial virtual elapsed time and no other activity, the
first tick query returns `1000000` milliseconds and `gameTicks()` returns
`60000`. After twenty 50-microsecond queries the millisecond return is `1000001`;
the game-tick result is still `60000`. These query counts are part of the input
history even though many consecutive returns are equal.

## Appendix C: Source traceability and validation

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Original addresses below identify routines/data in the target executable; an
implementer does not need to use those addresses.

| Contract | Evidence at the pinned revisions |
| --- | --- |
| Widths and floating precision | [Type definitions](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/tools/ghidra.h), [32-bit long compatibility](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/include/auto_common.h); `__setdefaultprecision` at `0x005fe240` in [decompilation](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/re/decomp.c), setting precision through `__controlfp(0x10000,0x30000)`. |
| Clamp/shift variants | [support.cpp](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/auto/support.cpp): `int_clamp_c` `0x00511350`, `int_clamp_b` `0x005a1a44`, `int_clamp` `0x005adfa0`, `int_shift_signed` `0x005ae3ec`. |
| Primary RNG | [crt.cpp](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/native/crt.cpp): original `srand` `0x005f2260`, `rand` `0x005f2280`; state at `0x00639e50`. [Determinism hook](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/hook/determinism.c) records these original calls. |
| Secondary RNG | [support.cpp](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/auto/support.cpp), [random.cpp](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/game/random.cpp): seed exchange `0x0059a6f0`, unit draw `0x0059a733`, range `0x0059a791`; state at `0x00635094`. |
| Initial states and adjustment | [Executable image layout](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/data/image/layout.json), [.data](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/data/image/data.bin), [.rdata](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/data/image/rdata.bin), [state declarations](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/data/gamedata.cpp); binary64 adjustment at `0x0061d700`. |
| Startup and ticks | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/auto/game.cpp): system initialization `0x005dbbd6`, application startup `0x0041b4c0`, tick wrapper `0x00421bb0`, tick calculation `0x005d41e0`. |
| Map-pattern seed | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/auto/map.cpp): `map_new_seed` `0x005b85fe`; `MapHeader.seed` in [civ2types.h](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/include/civ2types.h). |
| City-view reseed | [city.cpp](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/auto/city.cpp): `CityViewWindow::layout_buildings` `0x00455314`. Secondary gameplay callers also occur in [ai.cpp](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/auto/ai.cpp), [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/auto/civ.cpp) and [diplomacy.cpp](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/auto/diplomacy.cpp). |
| Replay environment | [replay.c](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/platform/replay.c), [message.c](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/src/platform/message.c), [original replay hook](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/hook/replay.c), [comparison script](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/harness/compare.py). |

Supplementary evidence under `civ2` is
`src/random_range.cpp` (explicit wrapped difference and x87 operations),
`src/runtime_startup.cpp` (53-bit precision), `src/x86_arithmetic.hpp` (signed
division instruction semantics), and `tests/random_range.py` with its probe and
original-code oracle. The latter defines 72 seed/endpoint edge combinations and
512 generated combinations, comparing returned integer and final secondary
state. It is a useful independent conformance source; it was **not rerun against
the original executable** for this documentation change.

Before the randomness-policy revision, 39 helper/vector checks compared the documented results with
extracted OpenCiv2 helpers compiled with explicit wrapping arithmetic. Primary
and secondary vector tables were also independently recomputed. The initial seed
words and adjustment bits were checked directly in the stored executable data sections.
That validates the tables and source transcription, not complete original-game
conformance. Existing [unit-case inventory](https://github.com/PeterJohnson/openciv2/blob/ce18e5e52e19870cf1598651eee7c1a97d517519/tests/unit/cases.tsv) includes
the secondary routines, but a zero primary-random-call count there does not mean
the secondary routine consumed no randomness.

Those historical checks do not establish the new implementation's sampling
policy or reproducibility. Required integration cases cover callback/decision
ordering, save/load, second-session state and mode-specific scheduling. Complete
capture of both original streams and city-view-to-AI RNG coupling is now only
an optional legacy-reference validation task.


## Appendix D: Optional original random-sequence reference

Everything in this appendix describes the original implementation for historical
validation only. Its imperative language applies only to an optional exact
legacy-reference replay. The recurrences, initial states, seed exchanges, discarded
draws and cross-subsystem coupling below are **not cleanroom requirements**. The
sampling laws in §§3–4 and ordinary arithmetic elsewhere remain required.

### Original states and shared consumers

| State | Value domain | Fresh-process value | Meaning |
| --- | --- | --- | --- |
| `S` | `u32` | `0x00000001` | Primary Microsoft CRT random state. |
| `N` | `u32` | `0x00bc6840` | Secondary Numerical Recipes linear-congruential state. |
| `P` | `0..65535` for a supplied map value; `1..32767` when newly generated | Supplied with map state | Resource/shield/hut pattern input; not a third random stream. |

Fresh-process values precede startup seeding. They are not defaults to insert
when loading a fixture or starting another game in an existing process. A
fixture entering after startup must supply the actual states at its boundary.
F06 owns whether and how particular save/load operations restore or retain them;
do not assume that loading a save restores all process-level determinism inputs.

Both generators are shared mutable state. A primitive is described as one
sequential call with no intervening game callback; synchronization and callback
placement around calls are the caller's responsibility. No primary operation
implicitly advances `N`, and no secondary operation implicitly advances `S`.
Setting `P` itself advances neither; later operations can use `P` to reseed `N`.

The secondary generator serves presentation as well as AI, city production and
diplomacy. Separate generators change the original sequence; the current contract permits this. [P03 palette construction and commits](P03-graphics-and-asset-processing.md#system-initialization-and-generation)
also consume the **primary** generator, retrying zero palette generations.
Presentation work therefore cannot generally be omitted from a shared RNG trace.

### Original primary generator

```text
seedPrimary(seed):                # seed is u32; no random draw
    S = seed

primaryNext():
    S = u32(214013 * S + 2531011)
    return (S >> 16) & 32767      # i32 in [0,32767], after updating S
```

`seedPrimary(0)` really sets zero. Seeding replaces state without advancing it
and does not reset the secondary generator. The observable result of a draw
includes its returned value in `0..32767` and the entire updated state in `u32`;
the returned value alone cannot be used to reconstruct the next state.

Many callers use `primaryNext() % n` for a positive `n`. Preserve the modulo
bias and the fact that this consumes a draw even for `n=1`. It is not rejection
sampling. Callers must specify their handling of zero or negative bounds.

### Primary vectors

Starting from `S=1`, successive calls return:

| Call | Return | State after call (hex) |
| --- | --- | --- |
| 1 | 41 | `0029e2c0` |
| 2 | 18467 | `c823f683` |
| 3 | 6334 | `18be873a` |
| 4 | 26500 | `e7847115` |
| 5 | 19169 | `cae1df84` |
| 6 | 15724 | `3d6c1037` |
| 7 | 11478 | `2cd60a1e` |
| 8 | 29358 | `72ae7869` |
| 9 | 26962 | `69521988` |
| 10 | 24464 | `df90722b` |

Starting from `S=1`, `primaryNext()%1` returns zero and leaves `S=0x0029e2c0`.
Returning zero without calling `primaryNext` would leave `S=1` instead.
Identical return values do not establish identical execution.

### Original secondary generator and inclusive range

Define `round53(x)` as nearest-even rounding to a 53-bit significand. All values
in this section are in binary64's normal range or exactly zero, so binary64
arithmetic at the stated boundaries implements it. `trunc(x)` is mathematical
truncation toward zero. Multiplication/addition in the state update are integer
operations, with the final result reduced modulo `2^32`.

```text
exchangeSecondarySeed(seed):     # seed is interpreted through u32
    if u32(seed) == 0:
        return 0                 # N is unchanged; no draw
    previous = N
    N = u32(seed)
    return i32(previous)

secondaryUnit():
    N = u32(1664525 * N + 1013904223)
    return round53(integer_value(N) / 4294967295)
```

The numerator is the **nonnegative value of N**, including when `N >= 2^31`.
The denominator is `2^32-1`, not `2^32`. The floating result is in the closed interval `[0,1]`;
both endpoints are possible. A zero seed request is a no-op, whereas an actual
zero state can occur through the recurrence or be explicitly supplied in a
boundary fixture. A fixture with `N=0` must not attempt to establish that state
by calling `exchangeSecondarySeed(0)`.

The inclusive integer helper takes endpoints in the `i32` domain, in either order:

```text
secondaryRange(lo, hi):
    if lo == hi:
        discard secondaryUnit()             # exactly one draw even here
        return lo
    if hi < lo: swap lo, hi                  # signed comparison

    difference = i32(u32(hi) - u32(lo))
    adjustment = binary64_bits(0x3feffffde7210be9)
    span = round53(integer_value(difference) + adjustment)
    # span has binary64 precision before the draw
    fraction = secondaryUnit()
    product = round53(fraction * span)
    integral = trunc(product)               # result fits the i64 domain
    return i32(u32(integral) + u32(lo))
```

`binary64_bits` identifies an exact numerical value by its standard binary64
encoding; it does not require an in-memory reinterpretation. Here `adjustment`
is the binary64 value nearest decimal `0.999999`, equivalently
`0x1.ffffde7210be9p-1` in hexadecimal floating notation. Preserve this exact
value and the addition/division/multiplication rounding points. The normal
result lies in `[min(lo,hi),max(lo,hi)]` when their mathematical difference is
at most `2147483647`. It is not a modulo reduction and is not an exactly uniform
integer sampler. The adjustment and rounded multiplication are part of the
behavior, including at the endpoints.

For a wider signed-endpoint interval, the difference wraps to a negative `i32`.
The result then need not lie between the requested endpoints. This is original
operation behavior, not an instruction to widen, clamp or reject the difference.
Use the explicit reductions above even when the implementation's native integers
can hold the wider difference. Appendix A explains the corresponding original
operations and a portability limitation in the reconstructed C++.

Every call consumes exactly one secondary draw, including equal bounds. Swapping
endpoints and seed exchange consume none. There is no loop, rejection or special
case that skips the draw for a one-value range.

### Secondary vectors

Each row is an independent call with the given **entry state**, under the normal
53-bit, nearest-even environment:

| Entry `N` (hex) | `lo` | `hi` | Return | Exit `N` (hex) |
| --- | --- | --- | --- | --- |
| `00000001` | 0 | 2 | 0 | `3c88596c` |
| `00000001` | 2 | 0 | 0 | `3c88596c` |
| `00000001` | 5 | 5 | 5 | `3c88596c` |
| `00000001` | -4 | 7 | -2 | `3c88596c` |
| `00bc6840` | 0 | 100 | 52 | `85c9be9f` |
| `00000000` | -4 | 7 | -2 | `3c6ef35f` |
| `ffffffff` | 0 | 100 | 23 | `3c558d52` |
| `25d60fe5` | -4 | 7 | -4 | `00000000` |
| `26f5b720` | -4 | 7 | 7 | `ffffffff` |
| `00000001` | -2147483648 | 2147483647 | -2147483648 | `3c88596c` |
| `00000001` | -2147483648 | 1 | 1639699275 | `3c88596c` |

The last row intentionally returns outside the supplied interval. The two rows
ending in zero and `ffffffff` also check `secondaryUnit()` results of exactly
zero and one. Separately, starting at `N=0x00bc6840`, exchanging seed zero
returns zero and leaves the state unchanged; exchanging seed one then returns
`0x00bc6840` and leaves `N=1`.

### Original seeding and process history

The recovered startup sequence includes these distinct actions:

1. System initialization seeds `S` with `u32` applied to the external wall-clock
   seconds result.
2. Later application startup requests a 4 ms timer period, calls the game's
   tick helper, and seeds `S` from that result.
3. It makes a **second** tick-helper call and passes that result to
   `exchangeSecondarySeed`, ignoring the returned previous state. A zero result
   leaves the existing `N` in place; it does not replace it with zero.

Do not merge the clock reads or assume the first seed was never observed. A
boundary after startup can capture the resulting states directly; a process
replay must preserve the earlier sequence and any intervening work.

City-view building layout subsequently calls:

```text
exchangeSecondarySeed(u32(city.x * 256 + city.y + P))
```

It does not save and restore the previous state. Its layout then consumes
secondary ranges, including deliberate `secondaryRange(0,0)` draws. Thus viewing
a city can affect the secondary state seen by later AI/diplomacy. An exact original-sequence replay must include that coupling. The current
P06 contract permits an isolated, repeatable visual random context.
This is also why a game seed plus a list of turn-ending commands is insufficient
to reproduce every decision.

## Appendix E. Original numeric annotations

These original annotations retain storage widths and reference-evaluation context.
They do not prescribe internal types or redundant casts. The main contract and
F01 determine logical ranges and behaviorally significant conversions; external
file/wire encodings remain normative in their owning mappings. Where an excerpt
includes an off-nominal conversion, it applies to that explicitly admitted profile,
not as a new requirement to accept unsupported inputs.

### Reference annotation 1

not required language or storage types. An unsuffixed scalar uses the `i32`
domain unless its owning algorithm specifies otherwise. Individually declared
domains and conversion boundaries take precedence over that default.

### Reference annotation 2

`u32` or `i32` to its result as appropriate. Preserve each intermediate reduction,
  rather than reducing only the final result of a longer expression.

### Reference annotation 3

with floating arithmetic without proving the same intermediate results.
