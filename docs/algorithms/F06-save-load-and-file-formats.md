# F06: Save, load and file formats

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. Evidence: OpenCiv2
`a71e8aefffe04e6f719e0e213b027b128a747711` and the independent reconstruction
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`.

This specification defines the external game, scenario, raw-map, preference and Hall of Fame
formats, their ordered reads/writes, version conversion and restoration effects.
It includes strings, event persistence, saved window state and multiplayer
payloads. The main text contains exact external encodings because interoperable
files require them; an implementation may use entirely different internal data.
Bidirectional interchange with the original target game is required within the
specified formats and input domains: original files must load here, and classic
exports must load in the original. This includes saved games, hotseat saves,
scenarios and raw maps. The accepted versions and current export version below
remain unchanged by the RNG relaxation.

Required shared contracts are [F01 arithmetic and sampling laws](F01-determinism-and-arithmetic.md),
[F02 state, identity and field meanings](F02-game-state-and-invariants.md),
[F03 rules and preferences](F03-rules-and-configuration.md), and
[W01 map allocation, geometry and spatial state](W01-map-geometry-and-spatial-state.md).
Scenario execution, session establishment and actual window restoration remain
S01–S02, N01/N04 and P01/P05 contracts. Their finite integration obligations are
listed below. A successful core load is not yet a complete resumed session.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

The file's section/record order and load transformations remain exact. Reordering
private encoding calculations does not change serialized bytes, lifetime renewal,
accepted partial-load state or observable I/O failure prefixes.

## Compatibility policy

X01's [input-admission policy](X01-classic-compatibility-contract.md#input-admission-and-rejection-policy)
requires the nominal supported formats/versions below, including custom scenarios
and normal retained fields. Malformed/truncated/unsupported content may be
rejected before publication with a diagnostic and safe cleanup; reproducing its
original partial-load trace is optional. Distinguish known invalid content from
I/O failure while processing admitted content. The latter, save-write failures,
and successful load transformations retain their specified effects. An optional
off-nominal importer must document its additional domain and results.

The [presentation policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy)
permits new file choosers and local window arrangements. Preserve selected file
identity, cancellation/acceptance effects and the external encodings. Saved view,
rectangle and preference fields must survive unchanged when not deliberately
edited, or use a documented valid translation for an intentional local change;
new UI metadata stays outside classic payloads. Different native windows do not
justify missing, resized or repurposed fields. Harmless chooser failures may
use clear diagnostics and safe cleanup without inventing a successful save/load.

Legacy transports are optional under X01. Keep file decoding and documented
mode adaptation for their saved state; an unavailable provider must be reported
before connection attempts, not silently substituted for another session mode.
Required TCP/IP save/resume and hotseat interoperability remain unchanged.

## Implementation boundary

The main sections use logical values, identities and operations. Original memory,
allocation and platform mechanics are recorded in [Appendix C](#appendix-c-original-implementation-reference). Preserve required
state changes and externally visible results without reproducing those mechanisms.
Exact numeric domains and file/wire encodings remain required wherever specified.

## 1. Inputs, outputs and domains

Inputs include the filename, requested save kind, file-service outcomes, incoming
world and retained fields, current game mode, both next-lifetime counters,
implementation random context or controlled choices, current rules/labels, preferences, screen rectangle, home
and scenario directories, language extension, and event list. Callback effects
at presentation/rule-loading boundaries are ordered inputs. Unless a fixture
says otherwise, callbacks do not mutate unrelated state or file bytes.

Saving returns **0 success, 1 failure**. Core game loading returns:

| Result | Meaning |
| --- | --- |
| 0 | All required reads and restoration stages completed. |
| 1 | Open failed, a safely terminated header did not match, or the version item could not be read. |
| 2 | Unsupported old/intermediate version. |
| 3 | Required data failed after rule loading began. |
| 4 | Version is newer than the supported maximum. |

Raw map read/write returns 0 success, 1 failure. Preference load has no success
return; it installs defaults for failed blocks. Preference save returns 0/1.
Fatal service failures and original out-of-backing accesses are separate from
these orderly returns. None of the readers provides transactional rollback.

### Nominal inputs

A nominal file is a complete file produced at a coherent supported save boundary:

- Counts, indices, sentinels, lifetimes, ownership, stacks and rule references
  satisfy F02. Unit/city counts are scan bounds including holes, not live counts.
  Ordinarily generated bounds are at most 2048 unit slots and 255 city slots;
  these are creation-domain limits, not proven original file-rejection rules.
- Map width is positive and even, height positive, and the tile count fits the
  classic allocation domain. W01 defines doubled coordinates, layer order and
  topology. Serialized area and coarse dimensions agree with the dimensions.
- Fixed text fields contain a NUL within their declared capacity. Bytes are
  classic byte strings in the selected language/code page, not UTF-8 code points.
  A fixed field retains all bytes after its first NUL. Variable event strings
  have at most 255 bytes before their terminator; the nominal event-text array
  is a contiguous prefix of present entries followed by absent entries.
- Civilization/tribe selectors, leader gender, production types, event references,
  map-view zoom and scenario parameters are in their owning algorithms' domains.
  Nominal saved zoom is −7..8, leader gender 0/1, and elimination count 0..12.
- Paths fit their destination buffers and core game-load paths contain a Windows
  backslash separating directory and filename. Required allocation and rule
  services succeed; a caller supplying a partial-I/O fixture must specify its
  returned count, bytes transferred and any callbacks.

The original generally trusts its own files. These conditions do **not** imply
that the original checks them. Section 10 separates deterministic off-nominal
behavior from memory-dependent failures. A new implementation may reject more
inputs, but must declare that restriction rather than attribute it to classic.

## 2. Encoding and framing

Widths in this section define the **external format**, not an internal game
record. Decode to the specified logical value/sign and encode it back at this
boundary; wider internal integers, enums and references are permitted. Preserve
reserved fields and supported round trips. A conversion needed during simulation
belongs at its producer's publication boundary, not only when a file is written.
Thus encoding alone does not excuse different live population or stock values.

All integers are little-endian; `i8/i16/i32` are two's-complement signed values,
`u8/u16/u32` unsigned. Arrays are contiguous in increasing index order, with the
last dimension varying fastest. There is no implicit alignment or padding.
`text[n]` means exactly n bytes, including terminator and retained tail.
`presence32` is a four-byte zero/nonnull marker; its nonzero value is not a file
offset or an identifier. Reserved fields must survive an admitted round trip
unless an operation explicitly overwrites them.

Bitsets use low bit first: civilization c is bit c; advance a is bit a within
its advance array; improvement k is bit k within its five-byte array. Retain
unused bits. F02 supplies unit/city/civilization/relationship/order/specialist
encodings and contextual sentinels in its [validation mapping](F02-game-state-and-invariants.md#appendix-a-original-representation-for-validation).
Those encodings apply at this file boundary without imposing an internal layout.
W01 supplies [tile field encodings](W01-map-geometry-and-spatial-state.md#appendix-a-original-representation-and-arithmetic-adapters).

### Game header and version

A current writer emits these twelve bytes before game state:

```text
43 49 56 49 4c 49 5a 45 00 1a 2c 00
 C  I  V  I  L  I  Z  E  NUL ^Z version
```

The reader collects a NUL-terminated header, consumes one additional byte, then
compares the string with `CIVILIZE`. It does not verify that the additional byte
is `1a`. The bounded-but-not-terminated malformed-header behavior is in section
10. The next two bytes are an unsigned version. Test in this order:

```text
if version > 0x2c: return 4
if version < 0x26 or version in {0x29, 0x2a, 0x2b}: return 2
otherwise accept                 # 0x26, 0x27, 0x28, 0x2c
```

Only version `0x2c` is written. A record helper's ability to read version `0x29`
is not acceptance by the core loader. File suffix does not select the binary
version or scenario fields; state and call arguments do.

### Current game stream

Let `U,C` be the serialized scan bounds, `W,H` map dimensions,
`A=(W/2)*H`, `CW=ceil(W/4)`, `CH=ceil(H/4)`. Let `L=1` for mode 0,
otherwise the population count of the eight-bit human-player mask. The writer
uses its current stored map sizes; the reader recomputes sizes during allocation.
The following table is also the order of required file operations. Each fixed
block is one item except where the table explicitly lists individual fields.
Zero unit/city blocks are skipped, avoiding a zero-size item failure.

| Order | Bytes | Content |
| --- | ---: | --- |
| 1 | 10 + 2 | Header and version. |
| 2 | `0x14a` | GameState record. |
| 3 | `8*0xf2` | CivNames records, civilizations 0..7. |
| 4 | `8*0x594` | Civ records, civilizations 0..7. |
| 5 | 14 | MapHeader. |
| 6 | `7*A` | Remembered feature layers, one A-byte item for each civilization 1..7. |
| 7 | `6*A` | Tile records in row-major W01 tile-index order. |
| 8 | `2*CW*CH` | Coarse grids 0 then 1, one item each. |
| 9 | `0x400` | 64 Landmass records in region-ID order. |
| 10 | `32*U` | Unit records, including free slots. |
| 11 | `88*C` | City records, including free slots. |
| 12 | 63 | For each tribe 0..20: times-used, city-name cursor, gender, one byte per read/write. |
| 13 | 4 | Cursor x then y, each an i16 item. |
| 14 | `48*L` | Leader records as selected below, one record per item. |
| 15 | `0x4b0` | Power history, 150 consecutive samples of eight civilization bytes. |
| 16 | `0x6a` | Saved layout block. |
| 17 | 80 | Eight map views: open, track moves, center x, center y, zoom; each field is an i16 item. |
| 18 | 100 if scenario | Scenario parameters. |
| 19 | `0x152` | Elimination history, always present. |
| 20 | `0x494` if mode nonzero | Multiplayer state. |
| 21 | optional | EVNT block, with records and then strings. |

For one-player saves the Leader record is the tribe of `playerCiv`. In any
nonzero mode, visit human-player bits 0..7 in ascending order and write/read
one record for that civilization's tribe. No human bits means no records.
Repeated tribes still occupy repeated records. The loader stores multiplayer
leader scratch records **by tribe**, so the last repeated tribe record wins.
These records supply saved sign markers; they do not replace rules' complete
leader definitions. The separate 21 triplets update their three runtime fields.

Scenario parameters are written if the explicit save-as-scenario argument is
true **or** the live game is already a scenario. The loader decides from the
loaded saved-as-scenario/scenario flags. A `.SCN` still uses the embedded map
format above, with remembered layers, not the raw `.MP` starting-position form.

An EVNT block is written only when events are marked loaded and the explicit
save-as-scenario argument is false. Thus an ordinary save of an ongoing scenario
can contain events, while the scenario-export operation omits them. The format
has no required checksum, closing marker or exact-EOF check.

### Game flags and options at the external boundary

Game flags: AI coordination `0001`, won `0002`, large map `0004`, small map
`0008`, cheated `0010`, scored/continued `0020`, saved-as-scenario `0040`,
scenario `0080`. Preserve other bits.

Game-option bits are music `000008`, sound `000010`, grid `000020`,
Enter-closes-city `000040`, mouse movement `000080`, tutorial `000100`,
instant advice `000200`, fast slide `000400`, no enemy pause `000800`,
show enemy moves `001000`, autosave `002000`, always wait `004000`,
cheat mode `008000`, wonder movies `010000`, throne room `020000`, diplomacy
screens `040000`, advance Civilopedia `080000`, council `100000`, animated
heralds `200000`. Message bits 0..10 mean **disabled**, in the F03 message-dialog
order. Rule bits and accepted-option effects are specified by F03.

## 3. Fixed game records

Offsets are hexadecimal and local to a record. Names describe logical state;
counts, masks, flags and ID tests use the contracts above, not the host language's
inferred signedness. For example, Unit's home byte `ff` means no home; `80..fe`
are unsigned city slots, despite its signed storage view. City supply/demand
bytes also have signed interpretations. ID zero denotes a free slot regardless
of the other bytes. Catalog fields without an assigned gameplay meaning are
retained payload, not a request to invent one.

### CivGoal record

Size: `0x6` bytes. Offsets below are hexadecimal.

| Offset | Encoding | Logical field |
| --- | --- | --- |
| `000` | `i16` | x |
| `002` | `i16` | y |
| `004` | `i8` | role |
| `005` | `i8` | priority |

### MapHeader record

Size: `0xe` bytes. Offsets below are hexadecimal.

| Offset | Encoding | Logical field |
| --- | --- | --- |
| `000` | `i16` | width |
| `002` | `i16` | height |
| `004` | `i16` | area |
| `006` | `u16` | flat |
| `008` | `u16` | seed |
| `00a` | `i16` | coarse-grid width |
| `00c` | `i16` | coarse-grid height |

### Tile record

Size: `0x6` bytes. Offsets below are hexadecimal.

| Offset | Encoding | Logical field |
| --- | --- | --- |
| `000` | `u8` | base terrain and terrain flags |
| `001` | `u8` | tile feature bits |
| `002` | `u8` | city-radius claimant and retained low bits |
| `003` | `u8` | region identifier |
| `004` | `u8` | explored-civilizations mask |
| `005` | `u8` | occupant owner and fertility |

### Unit record

Size: `0x20` bytes. Offsets below are hexadecimal.

| Offset | Encoding | Logical field |
| --- | --- | --- |
| `000` | `i16` | x |
| `002` | `i16` | y |
| `004` | `u16` | flags |
| `006` | `u8` | type |
| `007` | `i8` | owner |
| `008` | `u8` | moves used |
| `009` | `u8` | visibility |
| `00a` | `u8` | damage |
| `00b` | `u8` | last dir |
| `00c` | `u8` | ai task |
| `00d` | `u8` | counter |
| `00e` | `u8` | aux timer |
| `00f` | `i8` | orders |
| `010` | `i8` | home city |
| `011` | `u8` | retained byte |
| `012` | `i16` | goto x |
| `014` | `i16` | goto y |
| `016` | `i16` | prev in stack |
| `018` | `i16` | next in stack |
| `01a` | `i32` | id |
| `01e` | `u16` | retained word |

### City record

Size: `0x58` bytes. Offsets below are hexadecimal.

| Offset | Encoding | Logical field |
| --- | --- | --- |
| `000` | `i16` | x |
| `002` | `i16` | y |
| `004` | `u32` | flags |
| `008` | `i8` | owner |
| `009` | `i8` | size |
| `00a` | `i8` | founder |
| `00b` | `u8` | turns captured |
| `00c` | `u8` | known to |
| `00d` | `u8[8]` | revealed size |
| `015` | `u8` | retained city byte |
| `016` | `u32` | specialists |
| `01a` | `i16` | food box |
| `01c` | `i16` | shield box |
| `01e` | `i16` | base trade |
| `020` | `text[16]` | name |
| `030` | `u32` | worked tiles |
| `034` | `u8[5]` | improvements |
| `039` | `i8` | building |
| `03a` | `u8` | trade routes |
| `03b` | `u8[3]` | supplies |
| `03e` | `u8[3]` | demands |
| `041` | `u8[3]` | route goods |
| `044` | `i16[3]` | route partner |
| `04a` | `i16` | science |
| `04c` | `i16` | tax |
| `04e` | `i16` | trade |
| `050` | `u8` | total food |
| `051` | `u8` | total shields |
| `052` | `u8` | happy |
| `053` | `u8` | unhappy |
| `054` | `i32` | id |

### Leader record

Size: `0x30` bytes. Offsets below are hexadecimal.

| Offset | Encoding | Logical field |
| --- | --- | --- |
| `000` | `i8` | attack |
| `001` | `i8` | expand |
| `002` | `i8` | civilize |
| `003` | `u8` | times used |
| `004` | `u8` | female |
| `005` | `u8` | city name index |
| `006` | `i16` | color |
| `008` | `i16` | city style |
| `00a` | `i16` | leader name |
| `00c` | `i16` | plural |
| `00e` | `i16` | adjective |
| `010` | `i16[2]` | names |
| `014` | `i16[7][2]` | titles |

### CivNames record

Size: `0xf2` bytes. Offsets below are hexadecimal.

| Offset | Encoding | Logical field |
| --- | --- | --- |
| `000` | `i16` | city style |
| `002` | `text[24]` | leader name |
| `01a` | `text[24]` | tribe name |
| `032` | `text[24]` | adjective |
| `04a` | `text[7][24]` | titles |

### Civ record

Size: `0x594` bytes. Offsets below are hexadecimal.

| Offset | Encoding | Logical field |
| --- | --- | --- |
| `000` | `u16` | flags |
| `002` | `i32` | gold |
| `006` | `i16` | tribe |
| `008` | `i16` | research progress |
| `00a` | `i16` | researching |
| `00c` | `i16` | capital x |
| `00e` | `i16` | last city turn |
| `010` | `u8` | num advances |
| `011` | `u8` | future techs |
| `012` | `u8` | research cost advances |
| `013` | `u8` | science rate |
| `014` | `u8` | tax rate |
| `015` | `u8` | government |
| `016` | `u8` | diplo random |
| `017` | `u8[4]` | wonders by group |
| `01b` | `u8` | retained byte |
| `01c` | `i16` | subversion count |
| `01e` | `u8` | reputation |
| `01f` | `i8` | diplo request count |
| `020` | `u32[8]` | treaties |
| `040` | `u8[8]` | attitude |
| `048` | `i8[8]` | reputation credit |
| `050` | `u8[8]` | aggression counter |
| `058` | `u8[13]` | advances |
| `065` | `u8` | retained byte |
| `066` | `i16` | num military units |
| `068` | `i16` | num cities |
| `06a` | `i16` | num ships |
| `06c` | `i16` | total city size |
| `06e` | `u16` | military strength |
| `070` | `i16` | total attack |
| `072` | `i16` | population milestone |
| `074` | `u8[100]` | advance source |
| `0d8` | `u8[62]` | units active |
| `116` | `u8[62]` | units lost |
| `154` | `u8[62]` | units in production |
| `192` | `u16[64]` | continent military |
| `212` | `u16[64]` | continent attack |
| `292` | `u8[64]` | continent cities |
| `2d2` | `u8[64]` | continent city size |
| `312` | `u8[64]` | continent trade units |
| `352` | `u8[64]` | continent flags |
| `392` | `u8[64]` | continent policy |
| `3d2` | `i16` | best shield surplus |
| `3d4` | `i16[7]` | government score |
| `3e2` | `i16[8]` | last contact turn |
| `3f2` | `u8` | leader portrait |
| `3f3` | `u8[10]` | throne room |
| `3fd` | `u8` | retained byte |
| `3fe` | `i16` | throne room upgrades |
| `400` | `u8` | spaceship flags |
| `401` | `u8` | retained byte |
| `402` | `i16` | spaceship arrival year |
| `404` | `i16` | spaceship launch year |
| `406` | `i16` | spaceship population |
| `408` | `i16[6]` | spaceship parts |
| `414` | `CivGoal[48]` | ai goals |
| `534` | `CivGoal[16]` | ai standing goals |

### GameState record

Size: `0x14a` bytes. Offsets below are hexadecimal.

| Offset | Encoding | Logical field |
| --- | --- | --- |
| `000` | `u16` | rules |
| `002` | `u32` | game options |
| `006` | `u16` | transient flags |
| `008` | `u16` | game flags |
| `00a` | `u16` | message options |
| `00c` | `u16` | shown event flags |
| `00e` | `u16` | unused 0e |
| `010` | `i16` | turn |
| `012` | `i16` | year |
| `014` | `i16` | first launch turn |
| `016` | `i16` | active unit |
| `018` | `i16` | moving unit |
| `01a` | `u8` | multiplayer |
| `01b` | `i8` | player civ |
| `01c` | `i8` | saved current civ |
| `01d` | `i8` | turn civ |
| `01e` | `i8` | view civ |
| `01f` | `u8` | reveal map |
| `020` | `u8` | difficulty |
| `021` | `u8` | barbarian activity |
| `022` | `u8` | civs alive |
| `023` | `u8` | human players |
| `024` | `u8` | civs at start |
| `025` | `u8` | num enemies |
| `026` | `i8` | warming counter |
| `027` | `i8` | global warmings |
| `028` | `i16` | warming pollution |
| `02a` | `i16` | polluted tiles |
| `02c` | `i16` | peace turns |
| `02e` | `i16` | num units |
| `030` | `i16` | num cities |
| `032` | `i16` | num advances |
| `034` | `i16` | unused 34 |
| `036` | `u8[100]` | first discoverer |
| `09a` | `u8[100]` | advance known by |
| `0fe` | `i16[28]` | wonder city |
| `136` | `i16` | next history turn |
| `138` | `u8` | best human rank |
| `139` | `u8` | worst human rank |
| `13a` | `u8[8]` | power rank |
| `142` | `u8[8]` | civ by rank |

### Landmass record

Size: `0x10` bytes. Offsets below are hexadecimal.

| Offset | Encoding | Logical field |
| --- | --- | --- |
| `000` | `i16` | tiles |
| `002` | `i16` | fertile tiles |
| `004` | `i16` | water body size |
| `006` | `i8` | retained region byte |
| `007` | `i8[8]` | border bits |
| `00f` | `i8` | retained region byte |

### ScenarioEvent record

Size: `0x1bc` bytes. Offsets below are hexadecimal.

| Offset | Encoding | Logical field |
| --- | --- | --- |
| `000` | `u8[4]` | trigger bytes |
| `004` | `u8[4]` | actions bytes |
| `008` | `presence32` | unit name |
| `00c` | `i32` | unit type |
| `010` | `presence32` | city name |
| `014` | `presence32` | who name |
| `018` | `i32` | who civ |
| `01c` | `i32` | talker type |
| `020` | `presence32` | whom name |
| `024` | `i32` | whom civ |
| `028` | `i32` | listener type |
| `02c` | `i32` | turn |
| `030` | `i32` | denominator |
| `034` | `i32` | technology |
| `038` | `presence32[20]` | text |
| `088` | `presence32` | move owner name |
| `08c` | `i32` | move owner civ |
| `090` | `presence32` | move unit name |
| `094` | `i32` | move unit type |
| `098` | `i32` | move count |
| `09c` | `i32[8]` | move rect |
| `0bc` | `i32` | move to x |
| `0c0` | `i32` | move to y |
| `0c4` | `presence32` | aggression whom name |
| `0c8` | `i32` | aggression whom civ |
| `0cc` | `presence32` | aggression who name |
| `0d0` | `i32` | aggression who civ |
| `0d4` | `presence32` | create owner name |
| `0d8` | `i32` | create owner civ |
| `0dc` | `presence32` | create unit name |
| `0e0` | `i32` | create unit type |
| `0e4` | `i32[20]` | create locations |
| `134` | `i32` | create location count |
| `138` | `i32` | create veteran |
| `13c` | `presence32` | create homecity |
| `140` | `presence32` | auxiliary civilization-name string |
| `144` | `i32` | retained auxiliary scalar |
| `148` | `presence32` | auxiliary unit-name string |
| `14c` | `u8[40]` | retained event payload |
| `174` | `presence32` | money receiver name |
| `178` | `i32` | money receiver civ |
| `17c` | `i32` | money amount |
| `180` | `i32` | negotiation flag |
| `184` | `presence32` | wave file |
| `188` | `i32` | cd track |
| `18c` | `i32` | terrain type |
| `190` | `i32[8]` | terrain rect |
| `1b0` | `i32` | destroy civ |
| `1b4` | `i32` | give tech |
| `1b8` | `i32` | give tech civ |

For the ScenarioEvent record, the first two four-byte arrays encode little-endian
trigger and action words. The serialized prefix ends at `1bb`; list links are
not in the file. S02 owns the interpretation of trigger/action payloads, matching,
execution and one-shot state. The file adapter preserves all scalar payloads,
including action-used markers, whether or not an action currently consumes them.

## 4. Trailing blocks, raw maps and preferences

### Layout and map views

The 106-byte layout is an i16 valid marker followed by 13 short rectangles:
screen, main frame, eight map windows, minimap, status, city window. Each rectangle
is `(left, top, right, bottom)`, four i16 values. Saving reduces each incoming
coordinate modulo 65536 and interprets it as signed; loading sign-extends it.
Packing/unpacking visits rectangles in that order, and fields in the operation
order left, right, top, bottom. This access order matters only to alias/callback
fixtures; the file field order remains left, top, right, bottom.

The independent eight map-view records hold five i16 fields each as listed in
section 2. Loading them updates stored views, not native window objects. The
core load later replaces the layout-valid marker based on screen dimensions
and scenario status; it does not simply honor the saved marker.

### Scenario parameters

| Offset | Encoding | Field |
| --- | --- | --- |
| `00` | `u16` | Scenario flags. |
| `02` | `text[80]` | Scenario name. |
| `52` | `i16` | Technology paradigm. |
| `54` | `i16` | Year increment. |
| `56` | `i16` | Start year. |
| `58` | `i16` | Maximum turns. |
| `5a` | `i16` | Objective civilization. |
| `5c` | `i16[4]` | Victory thresholds. |

If no block is required, reset flags to 0, only the first name byte to NUL,
technology paradigm to 10, and all eight following numeric fields to 0. Retain
the remaining name bytes. [S01](S01-scenario-loading-and-parameters.md#2-parameters-and-their-consumers)
owns parameter meanings/reset and identifies their effect owners; F04 defines
calendar conversion and V08 objective evaluation.

### Elimination history

The 338-byte block has count i16 at `000`, twelve i16 turns at `002`, twelve
killer bytes at `01a`, twelve portrait bytes at `026`, and twelve 24-byte names
at `032`. All twelve rows are serialized regardless of count. Only these bytes
are part of the file, not adjacent reference padding.

### Multiplayer payload

Offsets are local to its 1172-byte block. This table specifies persistent state,
not session negotiation or authority.

| Offset | Encoding | Field |
| --- | --- | --- |
| `000` | `i16[8]` | Per-civilization saved center x. |
| `010` | `i16[8]` | Saved center y. |
| `020` | `i16[8]` | Per-civilization introduction/center-initialized state. |
| `030` | `i32` | Turn time limit in milliseconds. |
| `034` | `u8[256]` | Encoded password bytes. |
| `134` | `i16` | Open game. |
| `136` | `i16` | Kill civilization on retirement. |
| `138` | `i16` | Show human starts. |
| `13a` | `i16` | Chat from start. |
| `13c` | `i16` | Saved simultaneous setting, distinct from the menu preference. |
| `13e` | `i32` | Saved cheat-mode turn time. |
| `142` | `u8[290]` | Retained payload. |
| `264` | `u8[256]` | Civilization email rows, eight 32-byte slots. |
| `364` | `u8[256]` | Player setup table, retained session payload. |
| `464` | `u8` | Saved civilization to resume. |
| `465` | `u8` | Retained byte. |
| `466` | `i16` | Saved new-game/resume-event state. |
| `468` | `i16` | Autoplay active. |
| `46a` | `i16` | Autoplay turns left. |
| `46c` | `i16` | Double production. |
| `46e` | `i16` | Double movement. |
| `470` | `i16` | Claimed human-civilization mask. |
| `472` | `u8[34]` | Retained payload. |

Modes are 0 single player, 1 hotseat, 2 the e-mail code path, 3..6 network
choices. As X01 explains, having a mode-2 file encoding does not establish a
playable PBEM mode in this target. N01 owns the installed setup routes;
[N04](N04-hotseat-and-player-handoff.md#9-dormant-e-mail-branches) records the
dormant startup and second-human save/exit continuation. Preserve retained
session bytes at this boundary rather than reinterpret them as zero.

### Raw map stream

A raw `.MP` has no game magic or version:

```text
MapHeader (14)
startX[21] (42), then startY[21] (42), each i16
Tile[A] (6*A)
```

X and Y arrays are separate, not interleaved pairs. There are no remembered
layers, coarse grids or region records in this format. A missing start is −1;
F05 owns how a loaded start is selected and checked against a tribe.

Map reading is ordered as follows:

1. Read the 14-byte header into temporary storage. If incomplete, return 1
   without publishing the partial header.
2. For an embedded game map, set all 21 start pairs to `(−1,−1)`. For a raw
   map, read all X values into live starts, then all Y values; return 1 on a
   short item. The old map remains at this point, but starts may be changed.
3. Release the previous map, publish the header, and allocate using W01.
   Allocation recomputes area and coarse dimensions from width/height, replacing
   their serialized values. It clears new buffers as specified by W01; it does
   not rebuild the region table or the shared off-map tile.
4. For an embedded map, read remembered layers 1..7, each A bytes.
5. Read the tile block. Return 0 only on a complete item. On failure retain the
   allocated map and bytes already transferred. The outer game reader then
   reads coarse grids and regions; the raw-map reader does not.

Writing uses the same mode-specific ordering and returns 1 at the first failed
item. It uses stored map dimensions/area without rebuilding them. Original
reading adds no general header/tile-domain validator. The premade-world dialog's
subsequent clearing of features, ownership, regions and knowledge, pattern-seed
choice and rebuilding are the separate [F05 setup operation](F05-game-setup-and-starting-positions.md).

### CIV2.DAT

This file has no magic/version: one 72-byte ordinary-preference item followed
by one 444-byte multiplayer-preference item. Total nominal size is 516 bytes.
F03 owns defaults, load fallback, accepted option changes and save/delete order;
this section owns their exact byte encoding. Trailing input is ignored.

Ordinary preferences, offsets relative to file start:

| Offset | Encoding | Field |
| --- | --- | --- |
| `00` | `u16` | Game rules. |
| `02` | `i16` | Legacy default level. |
| `04` | `i16` | Difficulty. |
| `06` | `i16` | Tribe choice. |
| `08` | `i16` | Gender choice. |
| `0a` | `i16` | Science rate. |
| `0c` | `i16` | Tax rate. |
| `0e` | `u32` | Game options. |
| `12` | `u16` | Disabled-message bits. |
| `14` | `i16` | Opponent choice. |
| `16` | `i16` | Map-size choice. |
| `18` | `i16` | Barbarian-activity choice. |
| `1a` | `i16[5]` | Land mass, land form, temperature, climate, age choices. |
| `24` | `text[32]` | Preference text. |
| `44` | `i16` | Rules choice. |
| `46` | `i16` | Accelerated-start choice. |

Multiplayer preferences, offsets relative to file offset `48`:

| Offset | Encoding | Field |
| --- | --- | --- |
| `000` | `i16` | Main-menu choice. |
| `002` | `i16` | Multiplayer type. |
| `004` | `i16` | Hotseat menu choice. |
| `006` | `i16` | LAN menu choice. |
| `008` | `i16` | Legacy setting 1. |
| `00a` | `i16` | E-mail menu choice. |
| `00c` | `u16` | Network protocol. |
| `00e` | `i16` | Open game. |
| `010` | `i16` | Kill on retirement. |
| `012` | `i16` | Show human starts. |
| `014` | `i16` | Chat from start. |
| `016` | `i16` | Simultaneous moves. |
| `018` | `text[32]` | Game name. |
| `038` | `text[32]` | Player name. |
| `058` | `text[32]` | Email address. |
| `078` | `text[32]` | IP address. |
| `098` | `i16` | Timer choice. |
| `09a` | `text[6]` | Custom timer. |
| `0a0` | `i16` | Hotseat player-count choice. |
| `0a2` | `i32[4]` | Parley rectangle, left/top/right/bottom. |
| `0b2` | `i32[4]` | Chat rectangle, same order. |
| `0c2` | `i16` | Clear chat on new game. |
| `0c4` | `i16` | Clear chat on boot. |
| `0c6` | `i16` | Double production. |
| `0c8` | `i16` | Double movement. |
| `0ca` | `u8[24]` | Legacy setting 2 and retained tail. |
| `0e2` | `u8[24]` | Legacy setting 3 and retained tail. |
| `0fa` | `u8[24]` | Legacy setting 4 and retained tail. |
| `112` | `u8[7][24]` | Remembered settings table. |
| `1ba` | `i16` | Legacy setting 5. |

Defaults clear only the first byte of each legacy 24-byte setting and the
specified text strings, not their full tails. Failed-item bytes can therefore
remain in unassigned tails after fallback. A failed first read does not rewind
before the second read. Original preference saving ignores close failure, as
does game saving; a successfully reported block write is not proof of durable
storage. These details qualify F03's read/write-success terminology.

### HALLFAME.DAT

The classic Hall of Fame file has no header/version: six 72-byte records in order,
432 bytes nominally. Values below are little-endian; offsets are hexadecimal
within each record. [V08](V08-scoring-victory-and-retirement.md#4-hall-of-fame-records-insertion-and-display)
owns preparation, ranking, sparse reset, read/write failure order and display.
This section specifies the byte format, not a required internal struct.

| Offset | Encoding | Field |
| --- | --- | --- |
| `00` | `i16` | Score; sparse reset stores -1. Classic display skips all negative values. |
| `02` | `u16` | Difficulty 0…5 with optional Cheated mask `0x80`. |
| `04` | `i16` | Turn. |
| `06` | `i16` | Calendar year. |
| `08` | `i16` | V02 population quantity. |
| `0a` | `i16` | Owned objective value; -1 when not objective scoring. |
| `0c` | `i16` | Scenario year increment. |
| `0e` | `i16` | Scenario start year/month parameter. |
| `10` | `i16` | Scenario marker; writer stores game Scenario mask `0x80` or 0. |
| `12` | `i16` | Civilization rating percentage. |
| `14` | `i16` | Leader gender; zero selects male fame text. |
| `16` | `i16` | Fame title index 0…23. |
| `18` | `text[24]` | Leader name. |
| `30` | `text[24]` | Civilization plural name. |

Nominal name fields terminate within their 24 bytes. Bytes after their terminators
are retained, not automatically zero-filled. Clear sets only score/rating to -1;
read failures can therefore leave updated name/metadata bytes beneath an empty
row. A successful read consumes only the first six records and ignores trailing
bytes. Classic file loading lacks semantic validation; V08 distinguishes the
reconstructions' BF-198 guards from the original behavior and describes read
backing/stop requirements for accepted malformed rows.

Original name writes outside an intended field are excluded bugs, including
leader-name spill into the civilization field within this same record. Such
corruption is not a required serialization adapter. Choose safe name bounds or
a declared rejection/truncation policy; do not require adjacent-field overwrites.
V08 records the independent source's different repair and the validation limits.

The layout was checked against both source declarations and exercised in V08's
source-derived record insertion/save/load and short-file checks. Those are host
file/source checks, not a new native original-program round trip.

## 5. Ordered save operation

The writer is an operation on live state. It does not first snapshot the whole
world. Each block observes the state at its own write boundary.

1. Set result to 1. If entry mode is hotseat, remember the current human mask
   and replace it with the hotseat participant mask.
2. Select `.SCN` for scenario export; otherwise `.SAV` for mode 0, `.HOT` for
   1, `.EML` for 2, `.NET` for 3..6. An unrecognized mode leaves the previously
   selected extension unchanged. Copy the requested path; append the selected
   extension only if **no period anywhere in the path** exists. Do not replace
   an existing suffix. Open for binary replacement, truncating an existing file.
3. If open succeeded, write the header string including its NUL, then `1a`.
   These individual character-write results are ignored. Copy the current local
   human civilization into GameState's saved-current field, reducing to its
   signed byte. Attempt the two-byte version write. Stop on a failed item.
4. If exporting a scenario, set saved-as-scenario. For each advance 0..99,
   clear its known-by mask, then query civilizations 1..7 in order and add each
   possessor. With one possessor, set first discoverer to that civilization;
   with multiple, set it to 8; with none, retain the old first discoverer.
   Clear revolution, government-changed, pending-free-advance and the unused
   `0040` civilization flag for civilizations 1..7. An ordinary save instead
   clears saved-as-scenario and performs none of that rebuilding.
5. For scenario export or an already active scenario, visit civilizations 1..7.
   For each, replace its stored adjective, plural name and leader name, in that
   order, with the rules label if that tribe's corresponding label ID is
   nonnegative. Negative IDs keep the custom stored text. Text length/backing
   failure is not an orderly save failure invented by this specification.
6. Write the ordered blocks in section 2. Stop at the first failed item without
   attempting later blocks. For multiplayer Leader records, temporarily set
   `playerCiv` to the selected human and restore its prior value after the loop,
   including failure while writing a record.
7. After power history succeeds, copy the current screen rectangle to the saved
   screen rectangle and pack all layout fields before writing the layout item.
   These saved-layout mutations survive failure.
8. Immediately before the multiplayer block, remember its saved-resume civ and
   replace that byte with the network turn civ. Write the whole block. Restore
   the remembered byte **only if that write succeeds**; a short write leaves
   the replacement in live state.
9. If required, write `EVNT`, its event count, all fixed records, then all
   strings as specified in section 8. Set result to 0 only after every required
   operation reports success.
10. At common cleanup, if the **live** mode is hotseat, restore the saved human
    mask; then close the opened file once. Original close failure does not
    change the result. Return result.

If a callback changes mode into hotseat when entry was not hotseat, the cleanup
mask was not initialized by this operation. A compatibility fixture must supply
that original incoming value, or stop at the unresolved boundary; substituting
zero is not recovered behavior. Ordinary callbacks leave mode stable.

There is no direct random draw, map/region rebuild or lifetime renewal in this
writer. Game flags, discovery metadata, selected names, saved local civ and
layout changes are not rolled back. Failed saves leave partial files; they do
not use the preference writer's delete-on-failed-write rule. Saving can report
success even if a buffered close fails in the original.

## 6. Ordered core game load

The following stages describe the ordinary core entry. Do not reorder them
into a transactional deserialize-and-publish operation when reproducing failure
traces. Each item read may transfer some bytes before reporting failure.

### Entry and rule context

1. Remember the entry game mode; set status 1. Select the ordinary extension
   from that mode, apply the any-period filename rule, and open for binary read.
   Read/compare the header and read/check version as in section 2. Early
   unsupported-version returns perform no world-data loading.
2. Flush queued input/presentation work. From the **original requested path**,
   take the substring before its final backslash as the scenario directory and
   change into it. Probe `GAME` plus the language extension. If present, select
   that directory's `GAME` resource stem; otherwise select the default stem.
3. Remember the requested basename as the loaded filename. If the path begins
   with the home directory under a case-insensitive prefix comparison and has
   a relative subdirectory, zero the remembered subdirectory buffer and copy
   that relative directory without its final separator. Otherwise retain the
   prior subdirectory. Return the working directory to home.
4. Flush, perform F03's full rules load with initialization argument 1, set
   status 3, then flush again. These rule/path effects remain after later failure.

### Persistent world and version conversion

5. Read game state, name overrides and civilizations using section 7. Immediately
   after the game block, recompute enemy count as
   `popcount(alive)-1-(1 if barbarian bit is set else 0)`, stored modulo 256.
6. If loaded mode is nonzero, clear cheat mode. Capture `isScenario` as the OR
   of saved-as-scenario and scenario flags; if true, set scenario. If
   saved-as-scenario was set, clear cheat mode, reveal-map and cheated history.
   Always clear saved-as-scenario after these tests.
7. Read an embedded map, then coarse grid 0 and grid 1. Flush. Read the 64-region
   table. Read units and cities and renew lifetimes using section 7. Failure of
   any required item returns status 3 after earlier effects.
8. Read the 21 tribe triplets, cursor x/y and selected Leader records into the
   scratch arrangement from section 2. Restore temporarily changed `playerCiv`
   even on a failed multiplayer Leader read.
9. Read power history and the saved layout block; unpack layout and sign-extend
   coordinates, initially publishing the file's valid marker. Flush. Read all
   eight map-view records, each field separately.
10. If captured `isScenario` or the live scenario flag is true, read the 100-byte
    scenario block; otherwise perform its field-specific reset. Read elimination
    history. The ordinary entry always reads it; an internal unused skip branch
    is described in Appendix A.

### Local and multiplayer state

11. Set the runtime local human civilization from the loaded saved-current byte,
    sign-extending it. If version is below `29` or loaded mode is zero:
    - Reset passwords using section 9, consuming 255 primary draws.
    - Set open-game to 1; copy kill-on-retire, show-human-starts and chat-from-start
      from menu preferences; clear production/movement doubling and the menu's
      simultaneous-moves preference.
    - Set both saved-resume civ and runtime resume civ to the local human's low
      byte. Other multiplayer fields retain their incoming values unless the
      password/default operations explicitly assign them.
12. Otherwise, clear the entire multiplayer block to zero before reading its
    1172-byte item. On a short read, return 3 with the transferred prefix and
    zero remainder. If the saved-resume byte is zero, replace it with loaded
    turn civ; if still zero, choose the first alive civilization in 1..7, or
    retain zero if none. Copy it to runtime resume civ. Decode passwords,
    refresh each player's two password flags, then re-encode. No random draws
    occur in this branch.
13. Attempt optional events as in section 8. An absent/mismatched tag is not a
    load error. Flush after this stage succeeds.

### Names, derived state and completion

14. Normalize all 21 rules leaders: select the current leader-name label from
    that row's male/female name array; for plural, adjective and every one of
    seven governments' two title labels, negate when the value is nonpositive.
    This uses signed-16 reduction; −32768 remains −32768.
15. For an ordinary game, negate the player tribe's leader-name label. If the
    saved Leader record's plural/adjective/title field is negative, also negate
    the corresponding current label. In multiplayer repeat for human bits
    0..7 using the scratch record indexed by tribe, restoring `playerCiv`.
    This is **negation**, not assignment to an always-negative value. Shared
    tribes can be toggled more than once.
16. For a scenario instead, visit civilizations 1..7. Set that tribe's gender
    from the civilization female-leader flag, and portrait to tribe plus 21
    when female. Compare the stored plural name, case-sensitively, with the
    label selected by the absolute current plural-label ID. **On equality**,
    replace all government/sex title labels with the default government titles.
    Then negate leader-name, plural and adjective IDs. Title normalization
    from step 14 still applies; scenario processing does not apply ordinary
    saved-title sign markers.
17. Flush. For each civilization 0..7, zero its 62 active-unit and 62
    in-production counters. Scan unit slots ascending and increment the owner's
    counter for each nonzero-ID unit of that civilization. Scan city slots
    ascending and increment the type counter for every nonzero-ID owned city
    with nonnegative production. Each increment wraps modulo 256. Do not
    recompute units-lost or every other census field here.
18. Invalidate the calculated-city cache identity. Reload advances, improvements
    and units with initialization argument 1, in that order, then close the
    rules text stream. F03 owns those reload operations.
19. Set layout-valid to 1. Compare saved and current screen widths; if equal,
    compare heights. If the compared dimensions differ or captured `isScenario`
    is true, clear layout-valid. Equal dimensions with a different screen origin
    remain eligible. This overrides even a zero file valid marker.
20. Set status 0 and close the input. Ignore close result. If the loaded/live
    game mode differs from the remembered entry mode, perform mode adaptation
    below. Return status.

Mode adaptation sets open-game to 1; when entry mode was zero, also clears both
doubling options. Always clear the menu's simultaneous-moves preference. If
human mask is zero, set its bit for nonzero/alive runtime resume civ, otherwise
for the first alive civilization in 1..7; leave zero if none. If entry mode was
nonzero, assign it to game mode. If entry mode was zero, **retain the file's
mode**. The adaptation therefore does not simply force the entry mode in both
directions. It occurs only after a successful load and file close.

No random-generator seed, next-lifetime counter, shared off-map tile or complete
rules table is restored from these streams. Rules are loaded externally; next
IDs retain and advance their incoming counters. Password reset uses F01's primary
sampling law; the original advanced its retained primary stream up to 255 times.
Matching that stream is optional evidence. Rule/UI callbacks retain their own
effects. Stack repair, off-map unit deletion, window opening, visibility rebuilds
and event execution are not implicit parts of the core reader.

An original save import must explicitly initialize or retain the new
implementation's random contexts according to its documented policy. The file
does not contain them. A missing new-implementation RNG checkpoint must not
prevent loading an otherwise supported classic file, including one exported
here and subsequently saved by the original game.

Repeatable new-implementation continuation requires separately versioned
replay/checkpoint metadata recording its contexts and policy. Keep that metadata
outside the classic payload, and associate it with the corresponding saved state;
do not apply stale metadata to a file changed by the original game. For an
ordinary classic-file load, missing or inapplicable metadata falls back to the
declared classic-import random policy. An explicit exact-replay request
must instead report that its checkpoint cannot be reproduced.
Do not repurpose classic fields, padding or version markers for new RNG state.
The original must be able to read the classic export without that metadata.

Interchange preserves serialized fields and the load transformations specified
here, including retained sampled fields such as the diplomacy threshold and the
map-pattern seed. Loading does not reroll those fields merely because the PRNG
changed. Future random outcomes may diverge after import in either direction;
that does not waive file compatibility or authorize regenerating the loaded map.

## 7. Legacy records and lifetime renewal

Versions `26` and `27` have compact game/civilization records. The following
mappings consume bytes consecutively from the file into the current logical
records. All offsets and lengths here are hexadecimal.

Before the first old-version read, zero all civilizations' 62 active, lost and
in-production counts, all 100 game first-discoverer/known-by entries, all
civilizations' 100 advance-source bytes and all thirteen advance-bit bytes.
Other fields retain incoming contents until their reads overwrite them.

| Destination | Destination offset | Input length |
| --- | --- | --- |
| GameState prefix | `000` | `36` |
| First discoverers | `036` | `5d` (93 of 100) |
| Advance known-by masks | `09a` | `5d` (93 of 100) |
| GameState suffix | `0fe` | `4c` |
| CivNames array | `000` | `790` |

The game consumes `13c` (316) bytes. Recompute enemy count after its four items,
before the name array. For each of eight civilizations, read:

| Destination | Destination offset | Input length |
| --- | --- | --- |
| Civ prefix | `000` | `58` |
| Advance bits | `058` | `0c` |
| Census prefix | `066` | `0e` |
| Advance sources | `074` | `5d` |
| Active counts | `0d8` | `36` (54 of 62) |
| Lost counts | `116` | `36` |
| In-production counts | `154` | `36` |
| Discarded input byte | — | `01` |
| Civ suffix | `192` | `402` |

Each old Civ consumes `574` (1396) bytes. Its byte at `065` stays incoming;
the thirteenth advance byte and omitted array tails remain zero. Versions `28`
and `2c` read the entire current game/name/civilization blocks instead, with
no preliminary clearing. Either path stops at the first failed item, preserving
preclears, complete earlier reads and any partial item prefix.

The unit/city helper takes a version independently:

| Helper version | Unit input per slot | City input per slot | Renewal |
| --- | --- | --- | --- |
| Below `29` | First 26 bytes, separate item for every slot. | First 84 bytes, separate item for every slot. | Every unit and city slot receives a fresh lifetime. |
| `29` | First 30 bytes, separate item for every unit. | One 88*C-byte block. | Only nonzero serialized lifetimes are replaced. |
| At least `2a` | One 32*U-byte block. | One 88*C-byte block. | Only nonzero serialized lifetimes are replaced. |

Renew **only after all unit and city input items succeed**. Visit units ascending,
then cities ascending, using their separate incoming F02 counters. Assign the
current counter, then increment modulo 2^32. Do not skip zero when the counter
wraps. Old records do not contain lifetime IDs, so even a slot whose position
looks unused receives an ID. Unread unit suffix bytes retain their prior values;
renewal overwrites the ID field, but not the final retained word. Main game
loading never reaches the helper's `29/2a/2b` cases because of its version gate.

Modern zero-ID holes remain holes. Serialized nonzero ID values do not establish
new counter values or survive a successful renewal. References based on stable
unit/city slot indices are not remapped to different slots. A cleanroom loader
must distinguish slot identity from renewed lifetime identity.

## 8. Event records and strings

At the optional event position, first set events-loaded false, then try one
four-byte item. EOF, a partial tag, or four bytes other than `EVNT` means
successful absence; it does not clear the old event list at this core boundary.
On `EVNT`, require a signed-32 count; failure returns load status 3. Reset the
logical event collection, then create events in list order and read all
444-byte fixed records before reading any strings. Establish logical event order
independently of serialized link bytes, which never supply live links. The original
pool-size/allocation procedure is in [Appendix C](#appendix-c-original-implementation-reference).

For each event in order, restore strings indicated by nonzero presence fields,
using this **exact field order** (offsets hexadecimal):

```text
008, 010, 014, 020
038, 03c, ..., 084       # at most 20 text lines; stop at first zero marker
088, 090, 0c4, 0cc, 0d4, 0dc, 13c, 140, 148, 174, 184
```

Each ordinary field with zero marker consumes no string and remains absent.
A present empty string consumes one NUL and remains present, distinct from an
absent pointer. Text-line markers after the first zero do not consume strings.
Nominal input makes them zero; an off-nominal later nonzero marker is not silently
repaired into a safe initialized string reference.

String reading is:

```text
length = 0
repeat:
    ch = readByte()
    if EOF: fail without publishing a new string
    if length > 254: break        # this byte has already been consumed
    buffer[length++] = ch
    if ch == 0: break
buffer[length] = 0
create a string containing bytes through the first NUL
if creation fails: report failure; fail without fabricating an empty string
publish the new string
```

The buffer above is logical bounded decoding state, not a required stack or heap
object. Original allocations and native error numbers are in the reference appendix.

Thus lengths 0..255 followed by NUL work. For 256 or more non-NUL bytes, the
reader retains the first 255 and consumes/discards the next byte; it does not
scan to the eventual terminator. Remaining bytes become subsequent fields.
A writer emits the entire string through its first NUL with no length prefix
or truncation: failure of string output or its terminating-NUL write fails the
save. The writer therefore can emit off-nominal strings the reader will split.

After all strings succeed, finish the S02 compaction operation, preserving nominal
values/order and its supported logical string relationships. Set events-loaded
true only after that succeeds. A failure is not a successful empty event list.
Temporary containers, double copying and minimum per-string allocations are in
[Appendix C](#appendix-c-original-implementation-reference); they do not constrain internal storage or truncate strings.

Saving emits tag, signed-32 event count, all fixed records, then strings in the
same order. Serialized presence words originally contain process pointers.
A new writer may encode any nonzero token for present strings: the reader tests
only presence and replaces tokens. Byte-for-byte original-file comparison must
normalize these tokens or supply the reference pointer values. Event payload,
string order, absence versus empty and execution-state markers must still match.

The original signed count loop admits a negative count as zero iterations after
reset, then marks an empty list loaded. Reconstructions' negative/oversize count
rejections are stricter policies. This is a deterministic compatibility case,
not nominal event input.

## 9. Password state encoding

[N01 §9](N01-multiplayer-session-and-lobby.md#9-password-interaction) owns
password prompts, comparisons, pending-flag use and timer interactions; this
section owns their shared encoding/reset/refresh operations.

Passwords occupy eight consecutive 32-byte rows in a 256-byte logical array.
The transformation rotates the whole circular byte stream by three bits and
XORs each encoded byte with its index. For plaintext bytes p and encoded e,
with indices reduced modulo 256:

```text
e[i] = ((p[i] >> 3) | ((p[i-1] & 7) << 5)) XOR i
p[i] = (((e[i] XOR i) << 3) | ((e[i+1] XOR ((i+1) mod 256)) >> 5)) mod 256
```

Encoding walks forward retaining the preceding **original** byte; decoding walks
backward retaining the following decoded-XOR intermediate. Either formula may
use separate arrays. Do not rotate each 32-byte row separately.

Reset draws from F01's primary sampling law exactly once for each byte 0..254 and
stores the low eight bits. Byte 255 stays incoming. Then, for civilizations
0..7, clear the first byte of that civilization's row and both password flags
(prompt pending and has password). Encode the resulting 256 bytes. Draws whose
bytes are later cleared still count.

Refreshing flags decodes, sets both flags for each civilization to whether its
row's first byte is nonzero, then re-encodes. It consumes no random draw and
preserves encoded contents for all complete byte arrays. No string-length or
password-strength check is part of this operation.

## 10. Failure boundaries and off-nominal compatibility

### Defined behavior to preserve

| Input or failure | Classic effect |
| --- | --- |
| Version `25`, `29`, `2a`, `2b` | Return 2 after header/version; no rule/world load. |
| Version `2d` or `ffff` | Return 4, using unsigned version comparison. |
| Correct header with a non-`1a` separator | Consume and ignore it, then read version normally. |
| Serialized enemy count disagrees with alive bits | Replace it from the alive set; no general validity rejection. Empty/barbarian-only alive sets produce 255. |
| Stored map area/coarse sizes disagree with dimensions | Allocation recomputes them before payload reads, changing the lengths/positions the reader consumes. |
| Required block is short | Return 3 in the core reader, preserving prior changes and the transferred prefix. A partially read object is not implicitly zeroed. |
| Short map header | Retain old map; header scratch is not published. |
| Short raw start-array read | Retain old map, with partial start changes. |
| Short map layer or tile read | Retain the newly allocated map and earlier/partial payload. |
| Unit/city item fails | No lifetime renewal at all, including records read successfully before that failure. |
| Modern ID counter wraps through zero | A renewed record can become free; recount uses the resulting zero/nonzero test. |
| Missing or mismatched EVNT tag | Core succeeds with events-loaded false, leaving the old list until a caller resets it. |
| Long variable string | Truncate retained content at 255 bytes after consuming byte 256; do not discard through NUL. |
| Negative event count | Reset, process no records, mark an empty list loaded. |
| Failed multiplayer write | Keep the replacement saved-resume civ. |
| Failed buffered close after successful writes | Original writer can still return success. |
| Layout coordinate 32768 | Save bytes `00 80`; load as −32768. |
| Negative label ID −32768 | Normalization/negation wraps to the same value. |
| More than 255 active units of one type | Recount wraps its byte counter. |
| Period in a parent directory | Suppress automatic filename extension. |
| Preference block fails | Apply only that block's defaults over its partial/retained bytes; continue at the current position. |

### Original reads that require backing or a stop

Header-string reading performs at most 79 character reads, stopping at NUL,
then discards one more character unconditionally. EOF is stored as byte `ff`
inside the loop rather than causing an orderly failure. It returns the buffer
address even on EOF. Without a NUL among those bytes, later string comparison
can inspect incoming stack bytes. A wrong safely terminated header gives status
1; a generic clean "bad magic" error for every unterminated header is an added
policy, not a recovered original result.

Invalid counts/indices, unbounded fixed strings, corrupt list links, invalid
map dimensions, allocation exhaustion, a missing path backslash, and overlong
paths may reach memory outside declared objects. Pointer-like event tokens
remaining after a text gap may be used by later operations. An invalid leader
gender can select beyond its two-name array. These cases need explicitly supplied
original backing and ordered service outcomes, or a stop after established
prior effects. Ordinary arbitrary-precision arithmetic does not recover those
memory effects; neither does assuming an immediate error code. This backing
requirement concerns read evidence and original diagnostics. Out-of-bounds
writes are excluded bugs under X01: prevent the overwrite, using safe storage
or a documented rejection/stop where a valid continuation is not specified.
Do not require corrupting neighboring records or strings as a load result.

The documented deterministic reductions and partial effects should remain even
in a compatibility implementation whose safe-memory adapter stops at a later
unrecovered access. A restrictive importer may validate before publication and
reject all off-nominal inputs, but must label both its acceptance domain and
its different failure/rollback behavior. Preventing an excluded overwrite itself
is an accepted exception, not a failure of compatibility. Do not call the source reconstructions'
added validators original format rules.

## 11. Caller integration boundaries

### Save-menu wrapper and native file choice

[P05](P05-input-and-command-dispatch.md#7-main-menu-command-execution) routes
ordinary Save here with scenario=false; S03 scenario export uses true. If the
session-active latch is zero, return without timer, extension or path effects.
Otherwise stop N03 countdown(notify=1), then enter a retry loop:

1. Select the extension by section 5. Construct the private description/pattern
   pair: label 238 for ordinary save or 415 for scenario, space, `(*.EXT)`, NUL,
   `*.EXT`, NUL, NUL. `EXT` is the live extension excluding its leading period.
   This private filter is still constructed even though the chooser later uses
   the shared filter described below.
2. Copy the live local ruler name into the filename; write NUL at character
   index 2, retaining an earlier terminator for a shorter name; append `_`.
   Replace every signed byte in the range space through `@` with `_`; leave
   control characters and high bytes unchanged.
3. Clear shared text line and append `b` for a negative year, otherwise `a`.
   Append a space to the filename, then replace that last space with the first
   shared-line byte. Append decimal absolute year, rereading year for the number.
4. Only mode 2 and ordinary save: sample local calendar time once and append
   ` month-day-year hour-minute-second` with unpadded decimal fields. This is a
   dormant e-mail continuation, not proof of a playable e-mail entry.
5. Append period plus extension without its leading period; lowercase with the
   active classic CRT. If scenario export or an active scenario, and scenario
   time increment is negative, replace the whole filename: absolute truncating
   `year/12`, then month label, then full extension, then lowercase. For a
   nonnegative year month is absolute `year%12`; for a negative year it is
   `11−abs(year%12)`. Month labels start at 420. These later year tests/reads are
   live, not one cached calendar conversion.
6. Change directory to the search directory; build the shared current-mode-only
   filter with this scenario flag. Show Save on the main window with title label
   239 ordinary or 416 scenario, the filename, filter and extension excluding
   its period. Do not require an existing file. Restore the **live installation
   directory** after either acceptance or cancellation.
7. On acceptance, invoke section 5's writer. Failure copies the **post-writer**
   CRT error text into string parameter 0 and shows SAVEERROR. Success shows
   SCENOK with scenario name for scenario export, EMAILSAVED with filename for
   live mode 2, otherwise SAVEOK with local ruler title/name, civilization plural,
   P04-formatted current year, and difficulty label in parameters 0..4. The
   ordinary notice reads those inputs at their respective formatting steps.
   All accepted outcomes, including write failure, leave the retry loop.
8. Cancellation retries only if **live** mode is 2; even scenario export retries
   under that mode. Extension, filter and filename are rebuilt each time.

On normal exit start countdown(notify=1), using N03's start operation rather
than its separate resume operation. It runs after accepted errors as well as
success and ordinary cancellation. There is no automatic file retry after an
accepted failed write, or rollback of the writer's earlier mutations.

The shared filter builder first clears its original buffer. Scenario takes
precedence over raw-map: append label 415 plus ` (*.scn)` / pattern `*.scn`, or
label 261 plus ` (*.mp)` / pattern `*.mp`. Ordinary filtering appends current
mode first: 0→label238/`sav`, 1→label855/`hot`, 2→label858/`eml`, 3..6→label861/`net`.
Each entry is description plus space-parenthesized pattern, NUL, pattern, NUL;
zero-filled trailing storage supplies the final NUL. With current-only=false,
append ordinary save if live mode!=0, network save if live mode<3, then hotseat
if live mode!=1. Reread mode for each test. An unsupported mode returns no next
write position; a subsequent append needs valid backing or a stop. No e-mail
filter is added merely as an alternative to another mode.

Native file choice is a supplied service: owner may be absent; filter index is 1;
filename capacity advertised to the platform is 256; title, filter and default
extension are caller inputs. Select open versus save from the supplied save
flag. Success requires the returned **low byte exactly 1**, not arbitrary
nonzero. Otherwise query the platform's extended error and return cancellation/
failure 0, retaining any filename writes. Appendix A gives the original request
flags and storage capacities. P05 owns surrounding event dispatch.

### Accepted-file load dialog

The accepted-file dialog is distinct from the core loader:

1. Cancellation returns failure without invoking the reader. It restores the
   home working directory. An accepted choice first closes auxiliary map
   windows 1..7, then may select a scenario variant.
2. If the uppercased selected path contains `.SCN` anywhere, enumerate the
   directory's `*.ALT` matches in service-provided order. With n>0 matches,
   take one primary draw and compute `floor(draw*(n+1)/32768)`; zero keeps the
   selected scenario, k>0 selects the kth enumerated alternative. Enumeration
   order and a consistent second enumeration are external inputs. With no
   matches, consume no draw. [S01](S01-scenario-loading-and-parameters.md#3-loaded-world-and-resource-boundary)
   invokes this wrapper and retains the selected path for its briefing.
3. After a successful core read, flush. If events are not loaded and the game
   is a scenario, check for the scenario's localized EVENTS resource, reset the
   event container and invoke the parser if it exists. Successful parsing
   compacts and sets events-loaded. If still false, reset the old event list.
   [S02](S02-event-language-and-execution.md#11-caller-hooks-and-persistence) owns
   parser/compaction and scenario-loaded execution. Parsing alone appends; this
   caller's reset gives replacement semantics. Ordinary persisted Consumed flags
   and bound wildcard selectors survive unless this replacement path runs.
4. Replace live game options with preference game options. Configure sound;
   if music is enabled and events are loaded, invoke the scenario-loaded event
   hook before music synchronization. Handle optional title presentation and
   return to the home directory. These effects occur after core loading's
   option clearing; do not assume the core's saved options survive the dialog.
5. After notifications, set the network host flag. Visit unit slots ascending:
   for free slots clear both stack links to −1; for live slots invoke
   [U01 stack validation](U01-unit-lifecycle-and-stacks.md#3-ordered-stack-validation-and-repair)
   and, if either coordinate is negative, invoke U01 basic deletion with
   notifications false. Its counter changes, carrier wakeups and possible
   civilization-elimination callback still occur.
   Retain the final chosen path for the caller. On failure display the
   result-specific load notice and clear loaded-file-kind.

This sequence identifies observable mutation and draw boundaries, not every
presentation resource or dialog widget. P01/P05 own detailed presentation,
[S01](S01-scenario-loading-and-parameters.md) owns scenario entry and selection;
[N01](N01-multiplayer-session-and-lobby.md) owns network setup and shared password
use; [N04](N04-hotseat-and-player-handoff.md#8-save-load-and-shared-consumers)
owns local handoff, participant/active-mask transitions and their save joins.
S01 applies another preference override
after this wrapper, and its retry destinations determine whether loading/events
repeat. Keep core reading distinct from wrapper and session behavior, including
option overrides, mode adaptation, stack repair and negative-position deletion.

[F04](F04-turn-sequencing.md#9-autosave-boundary) owns autosave naming/rotation,
its pre-movement invocation and ignored writer return. F06 preserves that
snapshot timing: saved-current civ is assigned at writer step 3; the writer
does not run upkeep, renew IDs, advance the calendar or force turn-resume fields
beyond its explicit multiplayer substitution. Full resume tests must supply
the caller's existing resume fields and validate F04's skip-upkeep branch.
F05 owns premade-map preparation and new-game resets. F03 owns preference/rules
initialization. None should add a global "repair everything" stage to this
core contract without identifying an actual caller operation.

## 12. Conformance cases

A fixture supplies all unmentioned retained bytes, counters, streams and service
outcomes; it compares file positions, result, changed state and remaining random
state, not just the visible board. The following are independent expectations
for focused cases. Complete original-executable comparisons remain X02 work.

### Bidirectional interoperability cases

Require original-to-cleanroom and cleanroom-to-original load/play/save cycles,
then load each resulting file back in the other engine. Cover current saves,
supported legacy imports with current-version exports, hotseat, scenarios and
raw maps, using their respective callers. Compare serialized logical state and
specified load effects, including mode adaptation, event persistence, visibility,
passwords and map-pattern features. Do not expect an identity transformation
where this specification deliberately renews identities or changes runtime mode.

Test classic files without any new replay metadata, and files changed by the
original after a cleanroom export. Changed future random outcomes are allowed;
malformed exports, lost persistent fields and metadata-dependent loading are
failures. X02 owns actual original-program runs and the combined scenario/network
cases; the examples below do not establish that integration.

### Nominal cases

| Complete distinguishing inputs | Expected result |
| --- | --- |
| Current single-player file, no scenario/events, U=C=0, W=4,H=2,A=4,CW=CH=1, one Leader record, all other fields nominal. | 16,633-byte file; map payload has seven four-byte remembered layers and four tiles. Core load draws 255 times, renews no IDs, ignores EOF at optional tag and returns 0. |
| Raw map W=100,H=80, canonical header/start arrays and 4000 tiles. | 24,098 bytes: 14+42+42+24,000. No remembered layers or game header. |
| Mode 1 with humans 1 and 3; both use tribe 4, but differing saved Leader records. | Two Leader records on disk; second overwrites tribe-4 scratch. Ordinary-name fixup visits that tribe twice, so sign negations can cancel. |
| A present empty event unit-name field followed by an absent city-name field. | One NUL byte consumed for unit name, none for city name; empty remains present. |
| Non-scenario layout marker 0, identical saved/current screen dimensions, complete valid file. | Final layout-valid is 1. For the same file marked scenario, it is 0. |
| All password plaintext bytes zero. | Encoded byte i equals i. Refresh returns the same encoding and clears all password flags without a draw. |
| Password reset with incoming byte 255 equal to `5a`. | 255 draws; plaintext row starts zero; plaintext byte 255 remains `5a`; its low three bits affect encoded byte 0. |
| Ordinary preferences plus multiplayer preferences, exact nominal blocks. | 516 bytes, no header. Fixed text tails and legacy settings survive successful read/write. |

### Off-nominal and partial cases

| Complete distinguishing inputs | Expected result |
| --- | --- |
| Old version `27`; initialized retained bytes `a5`; complete records with 93 advances and 54 unit counters. | Omitted advance/counter tails zero; Civ byte `065` remains `a5`; every declared unit/city slot receives a new ID. |
| Modern units' IDs `[7,0,9,12]`, next-unit counter `fffffffe`, complete cities. | Unit IDs become `[fffffffe,0,ffffffff,0]`; next counter is 1. The final unit now fails the occupancy test. |
| Same units, but the final city item is one byte short. | No unit or city renewal; next counters unchanged; imported prefixes remain. |
| Modern incoming active-unit count 256 of one type/owner and distinct nonzero renewed IDs. | That owner's active counter for the type becomes 0 after recount. |
| Header bytes `CIVILIZE`, NUL, `?`, version `2c`. | Accepted header/version; separator is not validated. |
| No prior failures; optional tag supplies only `EV`. | Events-loaded false, prior list retained by core, successful load; two bytes consumed. |
| EVNT with signed count −1 and no records. | Reset list, mark empty events loaded, no string reads. |
| String is 256 `x` bytes, NUL, `q`. | First string becomes 255 `x`; cursor stops before NUL, after consuming 256 bytes. Next string read sees an empty string. |
| EOF after 255 non-NUL string bytes. | String read fails on its next character attempt; no string allocation. |
| Full map header but one byte missing from raw start X array. | Old map remains, partial X values published, Y unchanged. |
| Full multiplayer writes up to its block, then a short block write; old resume 2, network turn 5. | Save returns 1 and live saved-resume civ remains 5. |
| All writer item results successful; close returns EOF. | Classic result 0; repaired implementations may return 1. |
| Load mode 3 file with entry mode 0 and nonzero human mask. | Clear doubling, set open-game, clear menu simultaneous, retain loaded mode 3. |
| Load mode 0 file with entry mode 1 and zero human mask, resume 3 alive. | Password reset draws occur first; final human mask is bit 3 and game mode 1. |
| Valid header/version, but required data fails after rules initialization. | Result 3; selected directories/resources, rule changes, and preceding data changes remain. |

## Appendix A. Reference adapters and unreachable paths

The save-menu private filter and filename originally each hold 260 bytes; the
shared filter is 260 bytes. Longer admitted labels/month names can overflow these
objects; those overwrites are excluded bugs. Larger replacement storage is a
safe policy, not an original adjacent-state mutation. The native file-choice
request uses a 76-byte initialized prefix, flags `0x80e` plus `0x1000` when
existence is required, and advertised filename capacity 256. Its service result
uses low-byte-equals-one acceptance. Original source locators are save-menu
`0047758c`, filter builder `00473e55` and native chooser `004731d2`; independent
`src/local_turn.cpp` and `src/application_session.cpp` at revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752` supply these caller contracts. The P05
validation record identifies its bounded checks; full native Save/reload remains
a separate integration task.

Reference records are packed directly from the original x86 state. A compatible
implementation needs the external tables above and F02's reductions, not those
addresses, allocator handles or C declarations. The full in-memory event has
452 bytes, of which 444 are serialized; the final eight are list links. Event
presence words can differ across runs due to allocation addresses. Reserved
file bytes and semantic state are separate from those address tokens.

The event-string temporary has 260 bytes; the header temporary also has 260,
but the header loop writes at most 79. The event pool's 50000 argument and copy
minimum capacities establish allocation/error boundaries, not a requirement to
use a Win32 movable memory block. Port compaction may keep an allocated block
to preserve pointers; the original frees/reallocates. Valid logical event values
are unchanged, but arbitrary editor-held addresses need S02/S04 backing evidence.

The core reader's internal skip-elimination-history flag is initialized to zero.
Its alternate clear path is not a file-version rule: it would clear count, twelve
turns, twelve killers and each name's first byte, retaining portraits/name tails.
Do not skip the serialized history block for old files based on that unused path.

The separate saved-map import helper at `0047543c` has no production callers in
the inspected independent reconstruction. It accepts a signed version in
`26..2c`, including `29..2b`, loads settings/map and applies the caller's flat
rule. It is not the ordinary game/raw-map loader and does not expand their
supported versions. Revive it only with a specified caller and its separate
failure contract, not by changing the core version gate.

Original cleanup reloads live mode before restoring the hotseat mask, preceding
file close. OpenCiv2's scoped wrappers and explicit successful close can order
those operations differently. The source-informed independent writer identifies
the original cleanup order; callback/fault fixtures must follow that order.

## Appendix B. Evidence and validation limits

All linked sources are external reference material pinned to the revision in
the header. The cleanroom implementation does not need those files to implement
the contracts above.

| Contract | Evidence and original entry |
| --- | --- |
| Save ordering, file extension, records, strings | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): writer `004741be`, extension `00473d5e`, units/cities `004732a6`, game/civs `00473660`, string writer `00473c12`, string reader `00473c68`. |
| Core restoration and optional events | [retvals.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/retvals.cpp): `00475666` and its extracted restoration helpers. |
| Map serialization | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp): writer `005b8635`, reader `005b8783`, premade dialog `0041dd0e`. |
| Record field encodings | [civ2types.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h), [game state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/game.h), [report state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/reports.h), [enum names](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h). |
| Layout | `game.cpp` `00472f7b/00473064`; [graphics.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/graphics.cpp) `0047314e/00473190`. |
| Names, scenario defaults | [reports.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/reports.cpp) `00419ed3`; `game.cpp` `00484cc0`. |
| Magic/passwords/preferences | `game.cpp` `00497da0/00497e0f`, `00498784/0049882b/004988b8/00498943`, `004a733d/004a73d9`. |
| Event compaction and dialog integration | `game.cpp` copy `004ce98e`, compact `004cef35`, string allocation `004ce903`, `game_load_dialog`. |

The independent reconstruction's `src/game_file_reader.cpp`,
`src/game_file_writer.cpp`, `src/saved_map_import.cpp`, `include/civ2/state.hpp`
and `include/civ2/state_registry.hpp` corroborate field lengths, legacy mapping,
original cleanup order, retained incoming values and the unreachable helper.
Its repository and revision are identified in
[source provenance](README.md#source-provenance).

### Reconstruction differences

Both reconstructions contain stricter input-domain checks, including BF-091
count/record checks, BF-094 map dimensions, later tile-field checks, game/civ
selector and fixed-string validation, elimination count, scenario name/objective,
resume civilization, map-view zoom and event trigger/count checks. Validation
can also occur at different stages in the two reconstructions. These are
intentional repairs, not original rejection behavior. A source extraction must
identify which checks it retains, bypasses or stays within.

Original close failure is ignored. OpenCiv2 B28 selectively changes this when
UI fixes are enabled; independent BF-070 reports failure after a failed close.
OpenCiv2's finite record backing and independent original-backing adapters also
differ for corrupt indices. No specification here treats either host's object
bounds, exceptions or allocator as the original's complete malformed-file domain.

### Checks performed for this document

A temporary native harness extracted nine OpenCiv2 routines: game/civilization
record loading, unit/city loading, header read/write, event-string read, and four
password operations. It passed **178,465 assertions**. Tests cover current and
legacy mapping, all byte truncation positions of selected synthetic record
streams, old unread tails, zero blocks, nonzero/zero lifetimes, counter wrap,
enemy-count wrap, 256 complete password byte patterns, reset draw count and
retained byte 255, empty/boundary/oversize strings, and ignored header separator.

The harness replaced modern record-validation callbacks with success, kept
synthetic counts inside the host backing, used temporary host files, adapted
the event string's returned pointer to the native pointer width and supplied a
successful string allocator. It compiled with wrapping signed arithmetic.
These are checks of selected extracted operations against separate byte/formula
expectations, not an original-executable run or a full save/load integration test.

The fixed-record catalog was checked for complete byte coverage and exact sizes:
11 records, 242 fields, plus exact coverage of scenario, multiplayer and both
preference blocks. A separate byte-walking parser accounted for all bytes
in **45 local reference files**: 35 version-`2c` saves, three version-`27` files
(`TUTORIAL.SAV`, `ROME.SCN`, `WWII.SCN`) and seven raw maps. It used the stated
order, dimensions, version lengths and conditional blocks; no game assets were
copied into this repository. Those files contain no embedded event payload and
no multiplayer block. Their successful parse does not validate event/session
restoration or gameplay after loading.

Not exercised here: complete writer failure injection, allocation/reporter
failures, core callback interleavings, arbitrary corrupt backing, preference
fallback execution, real event-pool compaction, multiplayer/session negotiation,
UI/media restoration, and full-game save/resume replay. The corresponding
source contracts and finite boundaries remain explicit above. X02 must provide
portable fixtures and original-program comparisons; S01/S02, N01/N04, U01 and
P01/P05 must complete the stated caller integrations before collection-wide
conformance can be claimed.

## Appendix C. Original implementation reference

This appendix preserves original storage, allocation, native API and adapter
procedures for reference validation. The main text defines required behavior.
A different internal representation or platform is permitted; semantic identities,
ordered game effects, accepted input interpretation, file/wire values and required
failure/completion results remain governed by the main algorithms and X01.
Mixed excerpts retain context: their required effects are not made optional by
appearing here. Original memory/handle traces are not a second normative algorithm.
Optional providers and harmless UI failures retain X01's existing scope limits;
this reorganization introduces no new gameplay or interoperability relaxation.

### Original event-load pool construction

event container with pool-size argument 50000, allocate nodes in list order,
and read all 444-byte fixed records before reading any strings. Links are
constructed by allocation and never taken from serialized bytes.

### Original event-string allocation and reporting

String reading is:

```text
length = 0
repeat:
    ch = readByte()
    if EOF: fail without allocation
    if length > 254: break        # this byte has already been consumed
    buffer[length++] = ch
    if ch == 0: break
buffer[length] = 0
allocate strlen(buffer)+1 bytes
if allocation failed: report error -9/category 3; fail if reporter returns
copy through first NUL and return the new string
```

### Original post-load event compaction

After all strings succeed, compact by copying all fixed records in order and
then deep-copying their strings into a temporary container, reset the main
container, and copy back. For valid nonaliased strings this preserves logical
values/order. Each copy allocates at least 15 bytes for unit-name fields, 24
for city/civilization/home-city names, and 1 for text/wave strings, or
`strlen+1` if larger. No string is truncated by this minimum-capacity rule.
Set events-loaded true only after compaction. Allocation/reporting behavior
beyond supplied backing is a failure boundary, not a fabricated empty event.
