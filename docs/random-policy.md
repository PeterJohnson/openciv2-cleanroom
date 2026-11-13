# Random policy: oc2-mt19937-64, version 2

This is the new implementation's replay policy under F01 §§2–4,6 and the
[randomness ownership contract](architecture.md#randomness-ownership). It does
not implement an original-game sequence. Required sampling laws still come from
F01; this policy only supplies codes. It adds no classic save or wire fields.
Version 2 replaces the initial SplitMix64 policy with C++20's `std::mt19937_64`.
It changes seed-to-sequence results; version 1 checkpoints are rejected.

`GameplayRandom`, `PresentationRandom` and `CosmeticRandom` are different C++
types, each owning its state. No global generator exists. Initializing or drawing
from one context never accesses another. Owners supply explicit unsigned 64-bit
seed material (zero is valid) and a stable context name. Context/site names are
nonempty ASCII letters, digits or `.-_/`; addresses, native handles and thread
scheduling are not context identities. Seed acquisition is outside this library.

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

The code generator is the standard library's `std::mt19937_64`, whose
[transition, output and seeding algorithms](https://eel.is/c++draft/rand.eng.mers)
and [predefined parameters](https://eel.is/c++draft/rand.predef) are standardized.
There is no local implementation of the generator. The selected engine and its
integer seeding overload fix the raw output sequence across conforming platforms.
We do not use `std::default_random_engine` or standard distribution adapters.

Each nonconstant draw calls the engine once. Primary codes use the top 15 output
bits; secondary codes use the top 32. The existing F01 modulo/range mappings
preserve the specified biases, endpoints and floating-point evaluation.
This does not claim original trajectories or cryptographic quality.
`primaryModulo(n=1)` and equal secondary bounds return without consuming a code.
Invalid divisors, invalid sites for nonconstant draws and unsupported floating
rounding modes fail before advancing state. Constant results do not request a
decision site or code. Ordinary arithmetic uses the F01 mappings in `oc2_base`.

A checkpoint owns `{version, domain, context, engine, draws}`, including a copy
of the entire `std::mt19937_64` object with its state and current position. Restore
checks version/domain/name before changing the destination. The original seed is
unnecessary for continuation. The draw counter may reach 2^64−1; another draw
then fails before mutation. Checkpoints are logical C++ values at present, not a
new file format or full session/pending-interaction checkpoint. Never persist the
engine's object bytes; a future codec must define its portable representation.
Game operations must retain their own arguments, resolved choices and
continuations separately.

Controlled pure-mapping tests supply F01 codes directly and bypass generators.
The first X02 runner supports only those mappings; semantic choice tapes,
provider callbacks and implementation-replay fixture execution remain subsequent
work. Catch2 separately tests this policy's repeat/restore, boundary behavior and
presentation isolation, including restore across multiple engine state blocks.
The standard's 10,000th-output reference vector checks seeding, advancement and
both code projections on native and WASM. Policy and standard-library vectors
do not count as original-program observations. No OpenCiv2 or reconstruction
code is an implementation input.
