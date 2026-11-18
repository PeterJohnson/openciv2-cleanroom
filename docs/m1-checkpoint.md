# M1 local checkpoint implementation

`SessionRuntime.Checkpoint` and `RestoreCheckpoint` implement the bounded
[M1-01 profile](m1-closeout.md#m1-01-checkpoint-profile). They preserve owned
world values and suspended continuations without claiming whole-game persistence.
Native and browser cases exercise the same profile; see
[current validation](validation/README.md) for executed platforms.

## Random component

The RNG component uses the explicitly versioned
[`openciv2-random-checkpoint/2` representation](random-policy-pcg32.md#portable-rng-checkpoint-representation)
with PCG32 seed, stream and draw fields. Version-1 RNG and local envelopes are
unsupported.

## Local representation

`LocalCheckpointCodec` reads and writes `openciv2-local-checkpoint/2`. Its owned
`LocalCheckpoint` value contains a profile identity, content hash, origin/session/
participant IDs, counters, world bytes, both RNG checkpoints and an optional
`CheckpointCommand`. The command carries its request and descriptor version,
actor controller/access, pending update, interaction, committed effects and named,
versioned continuation bytes. It contains no mutable engine references.

The envelope has exactly `format`, `payload` and `sha256`. SHA-256 covers the exact
UTF-8 payload string, before JSON normalization. The payload uses fixed
field names and closed command-value alternatives. All integers are canonical
signed/unsigned decimal strings at their declared widths. Owner blobs remain
lowercase byte hex. In version 2, every encoded command/profile string uses four
lowercase hex digits per UTF-16 code unit, preserving managed text including NUL
and isolated surrogates. RNG strings embed the PCG32 version-2 component, and the
two session domains are checked explicitly. Neither version-1 envelope is accepted.

Admission checks exact fields, duplicate keys, integer bounds, nonzero identities,
counter ordering, command/session correlation, sole civilization-1 recipient,
unique nonempty named values, interaction choices/deadline/default, consecutive
committed-effect indices and nonempty versioned codec identities. Operation
registration, argument schemas, content/profile equality and the actual suspended
runtime boundary remain the session owner's responsibility.

Limits remain 16 MiB input, 4 MiB UTF-8 payload, 512 KiB per owner blob, 4096
UTF-16 code units per text, 128 per profile identity and 4096 elements per array.
Encoding applies a conservative aggregate budget before constructing large nested
arrays. RNG encoding/decoding retain their explicit replay limit. Five shared
`LocalCheckpointCodecCases.cs` groups test owned round trips and malformed/resource
admission in native and browser runs. Runtime coverage is recorded next.

## Runtime acceptance

`SessionRuntime.Checkpoint` and `RestoreCheckpoint` use the version-2 representation
with `CheckpointProfile` and `CommandDefinition.Checkpoint`. The owner contract is
synchronous, read-only during encode and limited to supplied staged operands during
decode. Session/client reentry is rejected. `BeginClientSession` accepts explicit
owner-supplied gameplay and presentation contexts as well as the seed convenience
overload, so distinct logical context names survive capture and restoration.

Restore prepares an isolated process/world, RNG contexts and continuation before
replacement. A staged map never shares the destination fallback. Process/token/
rule-provider identities remain stable on installation; fallback and text bindings
transfer with the new world. Old borrowed world/fallback/text references become
stale. Accepted effects populate both the retained update and cumulative log;
new effects continue the existing indices without repeating the execution factory.

Old clients are revoked and destination runtime identity remains unchanged. Saved
correlations retain their original runtime/session. All five identity generators
retain the maximum of saved and destination counters, including exhausted values;
the saved observation revision is restored. Queued inputs, owner pins and peer
obligations prevent replacement. Failed owner/continuation decoding leaves live
state, clients and RNG intact. Replaced continuation disposal is release-only and
runs once after installation; nonfatal cleanup errors cannot undo success. Fatal
allocation exhaustion propagates.

The test-only profile in `SessionCheckpointCases.cs` preserves treasury, token
counters and complete ordinary/multiplayer preferences and checks the remaining
world/process/content closure against constructor defaults. No actual gameplay
operation or arbitrary-world codec is registered by this feature.

## Eight-group acceptance evidence

| M1-01 group | Shared C# coverage |
| --- | --- |
| 1. Idle | Separate runtime/process, complete preferences and tokens, distinct context names, fresh empty observations, pin rejection |
| 2. Pending decision | Generated and controlled variants match uninterrupted command result, treasury, retained effects and suffix notifications; factory/prefix never rerun |
| 3. Cancellation | Accepted debit remains; no suffix draw or credit |
| 4. Timeout | No action at 49; retained deadline/default resumes at 50 |
| 5. RNG variants | Both domains, trace/controlled consumed prefixes, exact future samples and tape exhaustion; component cases cover all laws |
| 6. Bindings/replies | Stale/repeated restore clients fail; new binding answers; duplicate replies fail; runtime/correlation identities and all counter high-water marks retained |
| 7. Bad input | Corrupt envelopes/metadata, profile/content/codec mismatch, bad owner bytes/arguments/grants, failed staged decode and codec reentry leave destination unchanged |
| 8. Unsupported state | Nondefault world/process categories, extra/terminal commands, connected participants, queued inputs, owner activities, missing codecs and peer obligations reject without losing output |

Session, local-codec and RNG-codec cases execute in the native and browser
foundation suites. The [coverage inventory](testing/coverage.md) maps their current
registrations. Complete platform acceptance and whole-game persistence remain
separate milestones.
