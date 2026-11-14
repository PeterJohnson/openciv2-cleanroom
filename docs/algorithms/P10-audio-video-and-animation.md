# P10: Audio, video and animation

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in P10 |
| --- | --- |
| Gameplay | Meaningful notices and completion/skip behavior without duplicating/omitting game effects. |
| Original files/content | Supported authored media/resource identities, captions/substitutions and relevant file decoding. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Stock effects, device buffers, codec algorithms, frame clocks and animation sequencing. |

The explicit presentation contract below selects the required definitions;
its detailed original procedures remain reference. The table above adds no
requirement to reproduce an original UI or private runtime representation.

## Required presentation contract

The product target is the same game, with a broadly familiar Civilization II
feel and support for original configuration and scenario content. It is **not
an exact recreation of the original interface**. Apply [X01's presentation
policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy).

The requirements are the semantic obligations and external-data definitions
explicitly adopted below. All numbered sections and appendices under
[Original behavior reference](#original-behavior-reference) describe the old
interface. Their imperative wording, including historical “required” and
“conformance” lists, does not make their UI procedures mandatory. An adopted
formula or file definition does not adopt the surrounding prompt, callback,
storage, drawing or navigation algorithm.

Design screens, navigation, controls, prompts, formatting and rendering freely.
No equivalence proof against the original UI state machine is needed. Preserve
game rules, meaningful information, privacy, available game decisions, target
identity, accepted game/file/network effects and actual protocol deadlines.
Local draft edits are not committed game commands. Cancellation may discard
them; it cannot undo an already accepted game operation unless its owner permits
that. Keep source option identities separate from their displayed order.

Original allocation sizes, pool ordinals, shared scratch buffers, control IDs,
modal flags, retained UI state and native event traces are comparison details.
Production code must not emulate them merely to satisfy a historical snapshot.
Optional original-comparison tools/tests remain outside production dependencies.
File/wire/resource encodings are translated by production adapters, with safe
storage and documented diagnostics for unsupported or invalid input.

## Media and animation obligations

Media supports the game's recognizable feel and communicates events; it does
not own simulation. Choose new sound mixing, codecs/providers, frame clocks,
animations, transitions, captions, volume controls and playback UI. Original
WAV buffers, native device/MCI state, track chooser, fades, delays, credits
scrolling and frame-by-frame sequences are reference only. Stock effects and
cosmetic random choices may change or be disabled.

Movement/combat/nuclear effects consume U02/U06/U07/W05 results. Opening, wonder,
launch and ending flows consume F04/V07/V08 decisions. Execute required game
effects and completion once at their semantic boundary regardless of rendering,
skip, cancellation or device failure. A movie cannot award victory, change a
combat result or pause N03's actual deadline. Service N02 network events and
revalidate pending targets where required. UI-only playback callbacks are not
gameplay callbacks.

Preserve meaningful notices, participant/target identity, scenario text and
substitution values, including the ending/history information cataloged in
§17. Pages may be combined or presented without video. No invented advance,
conquered civilization or stale caption is required when information is absent.
Use a truthful omission/diagnostic for unavailable presentation content without
changing score or continuation. Original selection of stock flavor media is
optional; authored conditional content retains its declared interpretation.

## Original-file input contract

F03/P03/P04 and S02 own resource resolution, graphics, text and scenario media
requests. Preserve explicit authored filenames, effect/track identities,
overrides, caption text and required supported media decoding. Reference §§4,
7–11 describe admitted media/container inputs; independent correct decoders may
replace their provider algorithms. Original device buffers/timing are not data
formats. §18's effect-ID-to-filename catalog remains a classic resource mapping,
not a mandate for stock sampling, cancellation callbacks or internal table layout.

Reference §§14,16–17 contain authored opening/wonder/credits/ending section keys,
line/marker grammar and substitution meanings. Those file definitions remain
required; section reading does not require retained global streams or the old
playback flow. Native content has its own catalog. Diagnostics or no-video
presentation must not silently substitute unrelated stock content for a valid
scenario request, and must resolve the owning game interaction correctly.

## Required verification

Test the adopted content definitions and semantic operations, including custom
content, observer restrictions, changed/deleted targets, accept/cancel, real
timeouts and remote responses where applicable. Verify that different layouts,
navigation paths, pagination, repaint counts and skipped cosmetics leave the
same accepted commands and gameplay outcomes. Compare resolved values and
identities, not UI buffers or callback traces. Qualitative play review checks
the familiar map/city/turn/advisor feel; pixel or original-navigation matching
is not an acceptance gate. Historical checks below are reference evidence,
not evidence that the new UI or a documented bug repair has been validated.

## Original behavior reference

The following record retains stable section anchors for research and existing
citations. Only the specific semantic rules and data definitions adopted above
are normative. Everything else is optional reference, including exact
user-facing sequences. Historical source links and harnesses remain provenance,
not implementation or test inputs. A missing UI design detail is a design
choice, not a specification gap; a missing game rule or external format is a gap.

### 1. State, providers and input profiles

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Nominal effects are backed 22,050 Hz unsigned 8-bit PCM WAVs with complete format
metadata, valid block alignment, finite data and fitting filenames. Nominal
video providers supply complete AVI metadata, codec output and readable frame
buffers. Devices, clocks, errors and input delivery are explicit environmental
inputs. Preserve callback order and fresh reads after calls; a provider can
reenter the program. Timers are not assumed monotonic across unsigned wrap.

Required media state identifies the selected resource, requested loop/range,
pending completion/cancellation, owning game interaction and relevant user
preferences. Device, stream, buffer and native video records are implementation
choices; their original inventory is retained in the reference appendix.

The following rules apply throughout:

- Apply F01 wrapping arithmetic and each stated signed/unsigned comparison.
  Retain scalar narrowing when it changes return values or counters.
- Unknown incoming fields or failed-provider output require established backing,
  supplied retained input or a stop at consumption. No fabricated zero/default
  callback, sample, format field, cursor or device choice is implied.
- [X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
  excludes **every** out-of-bounds write. A mapped alias into another header,
  receiver, queue link or resource is still corruption, not a logical update.
- A restrictive profile can reject malformed resources/indices or stop unbounded
  loops. Preserve completed valid effects and distinguish that policy from the
  original failure path. Native handles and internal allocations may differ.

### 2. Effect selection and dispatch

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Preserve semantic effect identity, supported scenario overrides and requested
loop/cancel/completion behavior. Publish last-effect/last-mode when those values
are consumed by the caller. A negative effect cancels the matching narrowed
identity even when Sound Effects is disabled; nonnegative requests are suppressed
when that preference is disabled or effect≥133. Cancellation is not an invented
completion callback. Stock cosmetic selection may differ under X01, while an
explicit authored media request must retain its selected content.

The original filename/override mapping below is required when interpreting the
classic effect catalog and custom replacement files. Device buffer queues,
native return quirks and pump locations are reference implementation details.

Copy the current effect-name table entry. If it equals `CHEERS`, consume one F01
inclusive range(0,2), add 1 with wrap and append its decimal value. Append `.WAV`.
Build default `CIV2\SOUND\`+filename and scenario search-directory+
`\SOUND\`+filename. If the scenario path exists, use it. Otherwise obtain and
cache the alternate-resource-prefix reference, resolve the default path and
return if resolution failed. After resolution, if that reference is nonnull
and the resolved path's first character equals its **live** first character,
return. This is a first-character test, not a normalized directory comparison.
The CHEERS draw precedes all file-existence failures.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 3. Device and output-buffer lifetime

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Use any supported audio device/backend and buffer organization. Preserve requested
authored content, user sound preference and required completion/cancel effects.
Device initialization or allocation failure may use X01 diagnostics/fallback and
safe cleanup without reporting an accepted game command or losing a required
protocol reply.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 4. WAV stream construction

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Decode supported nominal WAV content, including requested looping, without
changing the authored signal or its semantic identity. A replacement audio library
need not reproduce the original stream records/refill buffers. Unsupported or
malformed media can be rejected under the declared profile; required
authored-content failures must remain explicit.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 5. Sample conversion and mixing

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Mix/resample for the chosen output device using an implementation-selected audio
pipeline. Original output sample equality, clipping arithmetic and mixer scratch
are optional. This freedom does not select a different authored sound or change
gameplay completion/cancellation.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 6. Filling, submission and completion

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Service playback completion and cancellation exactly once at their semantic
boundary. Native queue filling/submission and wave-device callbacks may differ.
Preserve callbacks that affect gameplay or protocol state, including required
ordering and live reads after external events.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 7. AVI audio and position clocks

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Decode and synchronize supported AVI audio/video with a replacement backend.
Original sample clocks, stride buffering and native stream position conversions
are optional. Preserve the requested authored media/range and its meaningful
completion/skip result; keep network service responsive.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 8. CD music and MCI transport

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Honor an explicit scenario CD-track request and user music controls within the
supported media profile. Cosmetic stock track selection, device discovery and
native MCI commands may differ. Missing media/device reports or falls back under
X01 and must not stall the game or silently substitute different scenario content.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 9. Video state, lifetime and media setup

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Open the selected supported video resource and maintain a pending playback
operation until completion, cancellation or declared failure. Native surfaces,
codecs, palette objects, allocation order and embedded player structure are
optional; semantic content and required caller effects remain.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 10. Seeking, decoding and playback scheduling

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

A replacement player may schedule, decode and render frames independently of the
original timers. Respect requested authored ranges and completion/skip semantics,
deliver relevant game/network events before subsequent reads, and do not duplicate
caller effects. Frame/palette caches and native update traces are reference
details.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 11. MCI movie services and track chooser

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Expose semantic movie selection, playback, stop and completion. Native MCI
wrappers, file chooser layout and original track controls may be replaced. Resolve
user/authored selection to the intended media identity and preserve required
caller results and network processing.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 12. Timing, palette transitions and block reveals

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Fades, palette transitions, random block reveals and their delays are cosmetic.
They may be replaced or skipped. Keep the surrounding game operation, committed
state, relevant external events and required network/turn deadline policy
unchanged.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 13. Map, combat and nuclear animation timelines

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Simulation callers decide movement/combat/damage; this document owns only their
presentation boundary. Keep active/selected/moving identities and required busy
or deferred-command gates coherent through completion. Cosmetic frame counts,
positions and waits may differ, but relevant external input/network events must
be delivered before later simulation reads.

#### Moving a unit

Set the animation gate and reset the two movement/network scratch identities to
−1 at entry; compute destination through W01's ordered direction0..8 offsets,
horizontal wrapping and signed y addition. Preserve source/destination refresh
semantics and reset the movement redraw identity to−1. View visibility governs
whether sound/visual output is appropriate. Clear the gate on normal completion.
Do not insert a second movement or change U02's already resolved destination.

#### Eight-frame effect

The original name identifies the operation, not a required frame count. Clear
received-interruption count and establish its busy gate. No participating visible
view returns0 after releasing the gate. Otherwise preserve the admitted sound
request and interruption/completion distinction; either ordinary completion or
received interruption returns1 and releases the gate. N02 owns the interruption
input. Original background restoration and trailing waits are cosmetic.

#### Nuclear effect

Invalid coordinates or a target invisible to the live human skip local visuals.
An admitted effect establishes/releases its busy gate and preserves required
remote presentation/completion handling and external event delivery before the
caller's destruction pass. U07/W05 still perform damage even when visuals are
suppressed, skipped or unavailable. Original sound wait, frame geometry and
saved backgrounds are optional.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 14. Opening, wonder and spaceship launch movies

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Opening and wonder movies are presentation operations. Preserve selected
supported authored content, meaningful notices and caller continuation; shortened
or skipped media does not undo completed gameplay. Wonder presentation retains
its caller-requested improvement help (wonder+39) when language!=0.

#### Spaceship launch

P07 owns the pending launch interaction and V07 owns the launch mutation.
Open the supported launch media when available; finish/skip resolves its pending
playback while required multiplayer idle work runs. A missing video or harmless
native-player failure may use X01 cleanup/fallback, without treating a failed
presentation as a second launch or omitting the V07 continuation. Native video
allocation leaks, palettes, geometry and buffer cleanup order are reference
behavior.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 15. Herald video and spaceship stars

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Diplomatic herald media conveys the V05-selected audience and P07 information;
keep supported authored content and semantic playback completion. Decorative
herald staging, moving spaceship stars and text reveal timers are optional.
They must not change science-cache publication, diplomacy decisions, V07 ship
state or the pending interaction's network/timeout obligations.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 16. Credits

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

[Native documents](../content/data-format.md#64-scenario-briefings-and-credits)
supply ordered credit blocks under four identities corresponding to the classic
About modes. Selected classic credit files retain their authored content and
caller completion/close behavior; native catalogs need no original text files.

Credits convey the selected authored credit text and allow semantic completion.
Layout, scrolling, fonts, decorative imagery and native timers are optional.
Required resource parsing and caller/network continuation remain; credits must not
mutate simulation state.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 17. Ending presentations

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

V08 decides whether an ending runs, winner/loser identity and subsequent score/
retirement. Presentation must not award victory independently. P04 owns text
substitution, F04 year formatting and F03 resource search/line reading. Preserve
supported authored ending content and the following information mapping.

| Ending | Required content/values |
| --- | --- |
| Defeat | Actor is the wrapped absolute supplied civilization when input<1, otherwise that civilization. String0 is the lowercased name of the first owned advance0–99; string1 is ruler. Missing advance0–99 has the specified retained-read boundary, not an invented substitute advance. Use ARCHAEOLOGISTS0..3. |
| Successful arrival | Same actor interpretation. Strings0=launch year,1=population counter followed by `0,000`,2=adjective,3=formatted arrival year,4=ruler. Use CENTAURI0..3. The existing no-video CENTAURI dialog instead puts the fresh numeric arrival year in number slot3. |
| Another civilization arrives first | Negative spaceship-ending input uses its wrapped absolute actor; arrival substitutions apply except string4 is the local human ruler. Use CENTAURI_BEATEN0..4. |
| Conquered civilizations | Visit matching conqueror history entries0..11; show stored defeated name and, when requested, formatted year of the signed elimination turn, plus supported portrait identity. Use SLAM concluding text and a Continue action. No matching history entry has its retained-tail-input boundary, not an invented empty entry. |

For ordinary caption pages, skip a missing section. Read mode1 lines until the
first character `@`, appending each live token-cursor string and a space; then
substitute the complete caption. EOF before that terminator is a declared input
failure, not an invented terminator. Close the file after ordinary pages;
conquered-ending SLAM retains the shared open stream as specified by its caller.
Defeat's final ARCHAEOLOGISTS3 caption is uppercased. CENTAURI3 frame-caption text
instead substitutes each line separately. New wrapping/fonts/transition timing
must not lose this authored content or change substitution values.

Resolve completion/skip under the owning caller, processing network idle for
network mode. Cosmetic waits, movie staging, palettes, native singleton objects,
portrait movement and text placement are optional reference details. Required
semantic failure effects follow X01 and the caller; no failed video changes the
winner, history records or score.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### 18. Initial effect-name catalog

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

The identity-to-stem mapping is behavioral input, not an internal string layout.
Each stem receives `.WAV`; every `CHEERS` entry invokes §2's sampling law.
Repeated names are separate effect identities for cancellation and callbacks.

| ID | Stem | ID | Stem | ID | Stem |
| --- | --- | --- | --- | --- | --- |
| 0 | `AIRCOMBT` | 45 | `CHEERS` | 90 | `FANFARE8` |
| 1 | `AIRPLANE` | 46 | `NAVBTTLE` | 91 | `FEEDBK01` |
| 2 | `CHEERS` | 47 | `NEWBANK` | 92 | `FEEDBK02` |
| 3 | `CRWDBUGL` | 48 | `NEWONDER` | 93 | `FEEDBK03` |
| 4 | `AQUEDUCT` | 49 | `CHEERS` | 94 | `FEEDBK04` |
| 5 | `BARRACKS` | 50 | `NUKEXPLO` | 95 | `FEEDBK05` |
| 6 | `BIGGUN` | 51 | `CHEERS` | 96 | `FEEDBK06` |
| 7 | `BLDCITY` | 52 | `CHEERS` | 97 | `FEEDBK07` |
| 8 | `BLDSPCSH` | 53 | `CRWDBUGL` | 98 | `FEEDBK08` |
| 9 | `BOATSINK` | 54 | `CHEERS` | 99 | `MOVPIECE` |
| 10 | `CATAPULT` | 55 | `CHEERS` | 100 | `ENDOTURN` |
| 11 | `CATHEDRL` | 56 | `CHEERS` | 101 | `CUSTOM1` |
| 12 | `CAVALRY` | 57 | `CHEERS` | 102 | `CUSTOM2` |
| 13 | `CHEERS` | 58 | `CHEERS` | 103 | `CUSTOM3` |
| 14 | `CIVDISOR` | 59 | `CHEERS` | 104 | `POS1` |
| 15 | `CHEERS` | 60 | `CHEERS` | 105 | `NEG1` |
| 16 | `CHEERS` | 61 | `NEWGOVT` | 106 | `MENUOK` |
| 17 | `NEWGOVT` | 62 | `REVOLT` | 107 | `MENULOOP` |
| 18 | `CHEERS` | 63 | `RIFLE` | 108 | `MENUEND` |
| 19 | `CRWDBUGL` | 64 | `CHEERS` | 109 | `BUY` |
| 20 | `NEWGOVT` | 65 | `CHEERS` | 110 | `SELL` |
| 21 | `NEWGOVT` | 66 | `CHEERS` | 111 | `GUILLOTN` |
| 22 | `DIESEL` | 67 | `SMALLEXP` | 112 | `DRUMAL` |
| 23 | `DIVCRASH` | 68 | `SPYSOUND` | 113 | `DRUMAY` |
| 24 | `DIVEBOMB` | 69 | `STKMARKT` | 114 | `DRUMA0` |
| 25 | `ELEPHANT` | 70 | `SUBMRINE` | 115 | `DRUMAN` |
| 26 | `ENGNSPUT` | 71 | `CHEERS` | 116 | `DRUMBL` |
| 27 | `CHEERS` | 72 | `CHEERS` | 117 | `DRUMBY` |
| 28 | `FIRE---` | 73 | `SWORDFGT` | 118 | `DRUMB0` |
| 29 | `NEWGOVT` | 74 | `SWRDHORS` | 119 | `DRUMBN` |
| 30 | `CHEERS` | 75 | `TANKMOTR` | 120 | `DRUMCL` |
| 31 | `CHEERS` | 76 | `CHEERS` | 121 | `DRUMCY` |
| 32 | `HELICPTR` | 77 | `TORPEDOS` | 122 | `DRUMC0` |
| 33 | `HELISHOT` | 78 | `JETSPUTR` | 123 | `DRUMCN` |
| 34 | `INFANTRY` | 79 | `JETCRASH` | 124 | `LETTER` |
| 35 | `LARGEXPL` | 80 | `JETBOMB` | 125 | `EXTRA1` |
| 36 | `CHEERS` | 81 | `JETPLANE` | 126 | `EXTRA2` |
| 37 | `CHEERS` | 82 | `JETCOMBT` | 127 | `EXTRA3` |
| 38 | `MCHNGUNS` | 83 | `FANFARE1` | 128 | `EXTRA4` |
| 39 | `MEDEXPL` | 84 | `FANFARE2` | 129 | `EXTRA5` |
| 40 | `MEDGUN` | 85 | `FANFARE3` | 130 | `EXTRA6` |
| 41 | `MISSILE` | 86 | `FANFARE4` | 131 | `EXTRA7` |
| 42 | `MRKTPLCE` | 87 | `FANFARE5` | 132 | `EXTRA8` |
| 43 | `NEWGOVT` | 88 | `FANFARE6` |  |  |
| 44 | `CHEERS` | 89 | `FANFARE7` |  |  |

### 19. Conformance cases

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Validate explicit scenario sound/video/track identity, override lookup,
preferences, cancellation versus completion and required callbacks. Exercise
movement/combat/nuclear presentation with real U02/U06/U07/W05 effects, network
interruptions and changed targets; skipping or changing duration must not repeat
or omit gameplay. Check ending text substitutions and V08 continuation. Original
sample/frame/codec/buffer/geometry traces are optional reference tests.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### Appendix A. Original adapters, repairs and excluded bugs

These details support original-program validation. They do not require the new
implementation to use packed records, memory addresses or native Win32 handles.

| Original representation | Behavioral interpretation |
| --- | --- |
| Ordinary integer operations | F01 32-bit wrap unless a narrower operation is identified; preserve signed/unsigned comparisons and division faults |
| Active-stream count, stream kind/effect, queue return | Unsigned16 count; kind/effect unsigned16; queue signed16 return, default wrapper zero-extends |
| Kernel count/frame bytes | Count low16 zero-extended into 32-bit do/while decrement; initial0 executes 2³² groups. Frame sizes low8; stream consumers advance by low16 of consumed count |
| Missing kernel sample | Incoming AL when no read has established a sample; copy/mix-stream caller supplies low8 of its destination token. Supply it explicitly or reject malformed zero-source-frame input |
| PCM format | 16-byte prefix: tag/channels at0/2, rate at4, average bytes at8, alignment at12, bits at14. AVI requests14 bytes; WAV requests declared chunk length |
| Format/default device state | PCM1/channels2/rate22050/average44100/align2/bits8; initialized follows the format. Optional format copy is four ordered DWORD groups |
| Output node/header | Node32 bytes, Win32 header32; data follows header. Ordinal bits7..14 store low8 index. Spare1, End2, SourceReady8, EndSubmitted16, Queued32, Mixed64; mask4 is unused here |
| Stream188 bytes | Format+116; kind/effect+136/+138; flags+112: Ended1, Inactive2, Looping4, NeedsRefill8, Started16; callback+180, next+184 |
| Embedded audio member | Low16 flags: Attached1, Prepared2, Playing4; stream reference+4. Constructor clears only stream reference |
| Original video receiver | Finished callback+276, frame callback+280, audio member+284, formats+1472/+1512, palette+1552, first/last/current+2576/+2580/+2584, cue+2592, looping+2596 |
| Codec protocol | AVI `vids`/`auds`; codec type `vidc`; handler from stream. IV41 sequence/frame control signatures, environment2/version65538, types4/2, frame flags2147483656, update rectangle output; preroll flag2147483648 |
| Notifications | Wave completion0x3bd; MCI notification0x3b9 and success reason1; reported MCI device low16; track low8 for TMSF playback |
| Graphics/menu domains | Fast movement flag0x400; wonder disable flags0x40/0x80; game palette flag4; credits string allocation sizes low16 |
| Clock rate | Binary64 `cd cc cc cc cc 0c 36 40` = the stored22.05; x87 extended operation and F01 truncation, not decimal-exact substitution |

Wave errors request provider text for the narrowed code with capacity 200, ignore
status and show the produced/retained terminated string as `Wave Out Error` with
information icon. Failed output is not assumed initialized by an arbitrary
provider. Audio error 10 shows `This Sound format is not supported` / `Sound Error`;
error 11 shows nothing; other codes show `Undefined Sound Error` / `Sound Error`.
Dialog returns are ignored and can pump/reenter the media machinery.

The primary reference's sample helpers initialize the sample to0 and expose a
short return. Original instructions instead retain AL until the first source
read and return the full consumed count; callers may then narrow. The primary
mix helper also omits the intermediate wrapped destination store. §§5–6 use the
original order. “Resample” in those function names does not mean interpolation.

Independent reconstruction repairs are evidence about differences, not implicit
classic defaults: BF-045/060/062/066 enlarge private paths or captions;
BF-111 rejects short PCM formats; BF-157 rejects high-bit device counts;
BF-160 validates AVI metadata and supplies the format tail; BF-188 supplies
ended-copy extent0; BF-191 restricts credits row height; BF-297 initializes the
opening owner's audio flags; BF-298 initializes the council owner's frame
callback; BF-305 safely consumes extended WAV formats. P09 owns the separate
council chapter-cursor repair. The shared constructors themselves do not supply
those original missing values.

Every original overflowing filename/caption/format copy, wrapped allocation,
sample/buffer overrun, coordinate-list overwrite or out-of-surface pixel write
is an excluded bug, even for nominal authored assets. Safe capacity growth,
rejection, clipped/omitted presentation or a stop can prevent it; identify the
chosen policy and preserve the valid prefix. Do not emulate corruption of next
fields or adjacent objects. Unknown reads, dangling publications and division
faults are separate boundaries; no invented empty string, zero callback, zero
pixel or successful playback continuation is implied.

### Appendix B. Evidence and ownership

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary reference revision: `a71e8aefffe04e6f719e0e213b027b128a747711`, especially
[sound.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/sound.cpp)
and the hand-written
[sample helpers](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/regargs.cpp).
The source names are evidence-navigation aids; the main sections above are the
implementation contract. The independent reconstruction at
`civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, supplies the following cross-checks:

| Family | Independent source / original entry examples | Owner |
| --- | --- | --- |
| Effect lookup/dispatch | `sound_effect.cpp`, `audio_effects.cpp`, `sound_filenames.inc`; `0046e020` | §2, §18 |
| Device/ring/queue/lifetime | `audio_device.cpp`, `audio_buffers.cpp`, `audio_queue.cpp`, `audio_cleanup.cpp`; `005d48f0`, `005d4a11`, `005d645e` | §§2–3 |
| WAV and conversion | `audio_stream.cpp`, `audio_memory.cpp`, `audio_samples.cpp`, `audio_fill.cpp`, `audio_submit.cpp`, `audio_completion.cpp`; `005d4c5f`, `005d4f6a`, `005edc6c`, `005edcac` | §§4–6 |
| AVI audio/clock | `audio_video.cpp`, `audio_sample_clock.cpp`, `movie_audio_attachment.cpp`; `005d52a2`, `005d5706` | §7 |
| CD/MCI | `cd_playback.cpp`, `mci_movie.cpp`, `menu_command.cpp`; `0046e2fa`, `005dd965` notification continuation | §§8,11 |
| Codec player | `video_media.cpp`, `video_decode.cpp`, `video_update.cpp`, `video_operations.cpp`, `video_window.cpp`, `video_cleanup.cpp`, `surface_window.cpp`; `005dd010`, `005e1c8e`, `005e2997`, `005e2cd1` | §§9–10 |
| Timelines/reveals | `message_pump.cpp`, `palette_fade.cpp`, `graphics_transition.cpp`, `move_animation.cpp`, `network_work.cpp`; `005683c5`, `0056c730`, `0057ed3f`, `0057f657` | §§12–13; P05 dispatch, N02 receipt |
| Movie callers | `opening_video.cpp`, `wonder_movie.cpp`, `civ_spaceship_presentation.cpp`, `herald_window.cpp` | §§14–15; P07 report/herald controller, P09 council controller |
| Credits/endings | `credits.cpp`, `conquered_ending.cpp`, `defeat_ending.cpp`, `centauri_ending.cpp`; `00470303` allocation, `00471116` allocation | §§16–17; V08 victory decisions |

The original image is MGE5.4.0f patch3, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Original initialized tables at `0062af70`, `0061d1e8`, `0061d200`, `0061d2a8`,
`0062ca60` and clock constant `0061d710` were compared directly. Full conversion
and mixing instructions at `005edc6c` and `005edcac` were inspected separately
from the C++ models. Source, binary tables and generated fixture hashes were
retained in the working evidence; this repository does not contain original
program code or copyrighted media.

### Appendix C. Validation performed and limits

Finite native64 fixtures exercise nine unchanged independent CD-selection,
request and notification bodies, plus both complete sample kernels with only
the x86 CLD instruction omitted. Typed byte accesses do not rely on that flag;
the fixture uses backed low-address buffers and explicit provider schedules.
Optimized and AddressSanitizer/UndefinedBehaviorSanitizer builds each pass
**65,821 assertions**. These include all 65,536 signed mixing input pairs,
all 256 unsigned-to-signed conversions, stereo duplication/discard, argument
narrowing, missing sample input, bounded overlapping reads/writes, track retry
and repeat draws, callback replacement and temporary inactive state. A supplied
playback adapter records the full track; it does not validate native TMSF output.
The enormous zero-count loop and any invalid memory write are not executed.

**220 direct original data/instruction checks cover2,462 bytes**: all 133 effect
names,21 herald codes/categories/positions/range rows, the sample-clock constant,
and both complete sample kernels. These establish those constants/instructions;
they do not establish every media branch or native platform result.

Remaining integration work includes real WAV/MMIO/device failure/completion
delivery; AVI/Indeo frames, audio timing and palette output; reentrant shared
contexts; all modal movie/report callers; network interruption; credits and
ending pixels/text; resource-search failures; and all safe-stop profiles. A
codec implementation may differ internally but must satisfy the declared frame,
palette, keyframe, error and callback contract for the chosen fixtures. Native
device timing, decoder behavior, fonts and resource content must be captured as
environment inputs. Third-party codec and MCIWnd implementation internals,
allocator internals and OS packaging are outside this game's algorithm scope.

The finite tests do not establish independent whole-program conformance or certify original playback, complete gameplay
integration, arbitrary malformed input or every legal callback interleaving.

### Appendix D. Original presentation reference

This appendix retains the original presentation procedures removed from the main
specification. Geometry, pixel/sample output, native controls and cosmetic timing
are optional reference behavior under X01. Required semantic fragments were kept
in the numbered main sections and are linked where omitted here; this appendix
does not relax their simulation, information, file or protocol obligations.
Original section numbers and local § references identify the recovered procedure
family, whether its retained fragment is in the main section or here. Historical
native/pixel tests are reference validation, not baseline acceptance criteria.

#### Original §2. Effect selection and dispatch

`playEffect(effect,mode,flags,extra)` first publishes last-effect and last-mode.
If signed effect<0, cancel all streams whose narrowed identity equals the wrapped
negation and return. This happens even when the sound preference is disabled.
Otherwise return when Sound Effects is disabled or effect≥133.

[Required behavior is retained in the main section.](#2-effect-selection-and-dispatch)

Mode 99 invokes asynchronous platform WAV playback with no-default-sound and,
when flags!=0, looping. Other modes queue the file with Looping requested only
when flags!=0, effect identity unchanged and `extra` as completion callback.
Return values are ignored. Pump messages once after either playback call; early
returns above do not pump. Mode 0 contributes a no-interrupt platform option in
the original flag construction, but only mode 99 reaches that platform call.

Startup effects call `playEffect(−107,0,1,0)` then `(108,0,0,0)`. The simpler
platform wrappers request asynchronous sound, asynchronous looping sound,
stop with a null filename, or the system's default simple beep; they do not
perform this effect-table lookup or preference gate.

Queue-file first requires a device; absence returns3. If not initialized,
initialize using the remembered arguments; any nonzero initialization result
returns0 here. If the unsigned active-stream count≥5, return 0. Looping chooses
the memory/loop loader; otherwise choose the streaming WAV loader. Narrow its
return to the original signed result domain; nonzero returns immediately.
On zero, increment the **live** active count with narrowing wrap. If the current
buffer-head is not Queued, walk up to the live buffer count, filling each header
until fill returns1; advance through the live next link after each call. Within
that same not-Queued branch, submit from the fresh shared buffer-head, ignoring
submission's return. Return 0.
The three-argument queue wrapper supplies extra0 and zero-extends the narrowed
result instead of sign-extending it.

Cancel(identity) traverses all streams, comparing narrowed identities. Remove
matching records without advancing predecessor; otherwise advance predecessor
and live next. Return 0. Mark-inactive traverses all matches and sets Inactive.
Presence returns1 at the first identity match, regardless of Inactive/Ended,
and0 at finite exhaustion. Neither operation invents completion delivery.

#### Original §3. Device and output-buffer lifetime

Default output is PCM, stereo, 22,050 samples/second, 44,100 bytes/second,
alignment2 and8 bits/sample; initialized is0. Open-device returns0 immediately
if already initialized. If a replacement format is supplied, copy its four
original field groups in order, preserving supported overlap reads.

Enumerate device indices from 0 while signed index<signed reported count. Query
capabilities into shared retained storage and ignore the query status. Select
the **first** device advertising 22,050 Hz stereo 8-bit output. Otherwise retain
the **last** device advertising the mono equivalent. At finite exhaustion with
neither, report audio error 11 and return 11. Mono fallback sets channels1,
bytes/second22,050 and alignment1; other format fields remain.

Open the selected device with the callback window. Narrow status to the original
result domain. On failure, stop platform WAV playback and retry once. On zero,
publish initialized 1 then copy the fresh device into the optional output.
Return status. An invalid signed device-count continuation cannot invent a
selected index; the reconstruction's early rejection is a separate policy.

Initialize publishes remembered callback/bytes/count and current buffer count,
opens the device, and returns a nonzero open error. Otherwise call buffer setup
and **ignore its result**, returning0. Close calls the provider only when
initialized, then clears initialized unconditionally even on provider failure;
it does not clear the shared device handle. Shutdown, when a device exists,
cleans buffers then closes the fresh shared device.

##### Buffer allocation and setup

Compute unsigned `perBuffer=bytes/count` **before** testing initialized. If not
initialized return 3. For indices0..count−1, allocate a node and its header+sample
storage. Allocation failure returns4; after a previous iteration it first runs
cleanup. The first node is published as first/current head before its fields
are cleared. Failure allocating its first sample block leaves that node retained.

Clear the header, point data at sample storage, set its length and node back
reference, retain allocation handles, start/end/size and narrowed ordinal.
Prepare the header through the provider. Failure reports the wave error, cleans
buffers and returns5. On success set header Done, re-reading the header after
prepare, then link from the previous node. After the final node, link to the
**live** first-node reference, publish buffers-ready1 and set MMIO buffer size to
incoming bytes; return 0. Wrapping allocation sizes never authorize an overwrite.

Resize returns without a device, or while the head is an active AVI stream.
Cap requested count unsigned to the remembered maximum, then apply signed
minimum2. Grow/shrink by the difference. Grow detaches that many spare nodes,
clearing Spare, updates spare head before walking active nodes, appends them and
closes the ring, then increases count. Shrink moves current head to first if its
ordinal is outside the remaining range, cuts after the remaining active nodes,
clears removed-node flags individually and sets Spare, prepends the removed
chain to spare head, then decreases count. Nominal counts describe backed finite
chains; zero/negative/wrapped requests are not permission to walk unknown memory.

Cleanup caches first-node **before** provider callbacks, clears cleanup counter,
resets device if initialized, and requests restoration to the remembered count
if needed. From the cached node, repeat up to the live remembered count: cache
next, unprepare any existing header, free its sample storage, free node and move
to cached next. Clear first-node. Remove head streams in a do/while, including
one null-head call when initially empty, until none; finally publish head0.
Other references are not all blanket-zeroed.

Block allocation publishes the allocation handle, then locks it. Failed lock
frees the **live output handle** and clears that output. Free-block, when its
argument is nonnull, unlocks then frees the same captured argument. These are
distinct from the fresh handle lookup used by video resource cleanup.

Remove-stream(null) returns0. Otherwise cache next, unlink through predecessor
or shared head, close primary stream, close secondary only if live Looping,
unlock allocation then re-read its handle for free, decrement live active count
with narrowing wrap and return cached next. Callback fields are not invoked by
this primitive. Remove-video-head first clears video-audio state; absent or
non-AVI head returns5. Otherwise remove it and explicitly restore the returned
cached next into shared head, returning0.

#### Original §4. WAV stream construction

Both loaders open the file, find RIFF type `WAVE`, then `fmt `, and request the
whole format chunk. File-open failure returns7. Missing chunks or a read count
whose unsigned value differs from chunk size close the file and return 8.
The original accepts the format when rate==22,050 and bits/sample==8; it does
not additionally validate PCM tag, channels or alignment here. Unsupported
format reports error 10, closes and returns10. Ascend from format, ignoring
status; find `data` or close/return 8.

Nominal format fields are fully defined. The original fixed local cannot safely
hold an oversized chunk, and a short chunk leaves consumed fields incoming.
A safe implementation can consume extra format bytes into adequate storage and
use the supported prefix, or reject; BF-305's safe consumption is such a repair.
BF-111's rejection of short format output is separate from original retained
input. Neither nominal authored WAV extensions nor deliberate malformed files
require reproducing a stack overwrite.

The streaming loader opens with the shared MMIO buffer size in cleared options.
After finding data, get buffer information then advance it, ignoring both return
statuses. Allocate/lock a cleared stream record. Allocation failure closes the
file and returns2; lock failure also frees the allocation. Publish file/chunk/
format/buffer metadata, kind WAV, narrowed effect and completion callback=extra.
Append to the live queue tail, or publish head if empty; return 0 without closing
or freeing the now-owned resources. Incoming flags are not read by this loader.

The looping loader opens the source with default options. After data lookup,
create cleared memory-stream options with the shared MMIO buffer size and open
a memory stream; failure returns7 **without closing the original file**. Get
memory info, ignoring status, then allocate/lock the record. Later allocation
failures return 2, freeing a failed-lock allocation but retaining the open
streams. Set kind WAV, primary memory stream, secondary source file, format,
chunk, memory metadata, Looping and narrowed effect. Refill it before appending
to the live queue; return 0. No extra callback parameter exists on this path.

##### Looping refill

Snapshot the complete buffer metadata. Let leftover=0 if cursor==base, otherwise
end−cursor; room=capacity−leftover. If leftover nonzero, move those bytes to base
with overlap-safe semantics and start writing at base+leftover; otherwise base.
Repeat: remaining=chunkSize−live secondary progress. If signed room<signed
remaining, request room bytes from the live secondary stream, ignore actual read
count, add requested room to fresh progress, set cursor to cached base and return.
Otherwise request remaining bytes, set progress0, advance destination by remaining,
set room=cached end−destination, seek to live data offset, and repeat. No read
failure branch or zero-data termination is invented. Enforce storage bounds and
an explicit safe stop for nonterminating off-nominal cases.

#### Original §5. Sample conversion and mixing

The conversion parameters often named source/destination “rate” in source are
**bytes per frame**, not sample rates. Let groups be the narrowed group count;
for each group, set reads to the narrowed source-frame size and writes to the
narrowed destination-frame size. While either remains:

1. If reads>0, read one source byte, XOR128 to convert unsigned PCM to signed-byte
   representation, retain it as sample, advance source/consumed and decrement reads.
2. If writes>0, write the retained sample to destination, advance and decrement
   writes. Extra destination bytes repeat the last sample; extra source bytes
   are read/discarded after writes are exhausted.

Mix uses the same sequence but adds the signed retained sample to each signed
destination byte, saturating to [−128,127]. The original first stores the wrapped
sum, then conditionally stores the saturation endpoint. Preserve that order for
established observable storage; all out-of-bounds destinations remain excluded.
Return total source bytes consumed with F01 wrap. Ordinary mono→stereo repeats
each byte; stereo→mono keeps the first byte of each pair. There is no averaging,
interpolation, pitch change or general temporal resampling.

The original outer loop is do/while with a narrowed initial count and a wider
wrapping decrement: a narrowed zero does not mean no work. Missing first sample
when source-frame size is zero requires supplied incoming sample or a stop.
The exact narrowing domains and enormous zero-count loop are in the validation
appendix; a safe adapter must stop before any unsupported read/write.

##### Copy one stream to an output buffer

First compute unsigned requested frames=`outputBytes/outputAlignment`, before
null/Ended checks. Start copied0, remaining=requested. For nonnull, non-Ended
stream, extent is Looping ? bufferEnd−cursor : chunkSize−progress. Compute
buffered frames=(end−cursor)/sourceAlignment and copied=extent/sourceAlignment.
Cap copied to requested; mark Ended if it is smaller. Set NeedsRefill exactly
when buffered<wrapped(requested×2), otherwise clear it. Remaining=requested−copied.

If copied>0, convert into output, using the original supplied incoming sample
only if no source read defines one. Advance cursor by the **narrowed** consumed
byte result. For AVI, if cursor==end reset it to base. Clear Started.

If remaining>0, fill the remaining output-frame bytes with signed silence0.
For a nonnull stream, consume the earlier extent; if nonzero, increment live
progress by sourceAlignment×copied. Return 1. An already-Ended stream did not
establish that extent; BF-188 supplies0 as a repair, not an original producer.
When remaining==0, set progress0 for Looping, otherwise add the same product;
return 0. This last branch still dereferences a null stream when requested==0
in the original, so a safe consumer stops instead of inventing success.

##### Mix another stream

Require a backed record. Compute extent, buffered frames, copied and requested,
then the same cap/Ended/NeedsRefill decisions. If copied>0, mix and advance cursor
by the narrowed consumed bytes. Do not add AVI cursor wrapping, clear Started
or fill the remaining output: those are copy-only operations. Set Looping
progress0 or add sourceAlignment×copied. Return 1 only for Ended and not Looping;
otherwise0. A later live stream returning0 can keep a composite buffer active
when an earlier stream ended.

#### Original §6. Filling, submission and completion

##### Initial fill

If not initialized return 0. Clear target node End and find the first non-Inactive
stream. If present and node not Mixed, copy it (AVI uses its ordinal×stride
segment and signed node-size/output-alignment division); retain whether ordinary
copy returned1, set Mixed and advance stream. Mix each remaining non-Inactive
stream; any mix return 0 clears the retained ended result. If node was already
Mixed, this mixing starts with the first non-Inactive stream. Clear header Done.
If no stream, clear node Queued instead.

Rescan the fresh shared stream head for cleanup/refill. Ended WAVs are removed;
Ended AVI marks node End and stream Inactive. For a non-Inactive NeedsRefill
stream, AVI refill runs first; **re-read kind afterward**. If now not AVI,
Looping invokes loop refill, otherwise advance its MMIO buffer. Follow live next
links after callbacks. Return 1 if head is empty or retained ended is true, else0.

##### Submission

Starting from the requested node, while Mixed: cache its sample cursor and XOR128
into each output byte, re-reading header/length as prescribed by the provider
boundary. Submit through the live device. A narrowed nonzero status reports a
wave error except status 33, clears Queued and leaves Mixed. Success sets Queued
and clears Mixed. Follow live next; stop at the first non-Mixed node and return 0.
A retry can therefore apply XOR a second time after a failed submission.

##### Wave-completion callback

Return 0 when not initialized. Cache node from completed header. Advance shared
buffer-head through up to the live buffer count, stopping at Queued. If completed
node is Spare, return 0. If head is AVI and node has both End and EndSubmitted,
clear Playing in the stream's control object and mark stream Inactive.

Initialize last callback absent and repeat for the current node:

1. Cache its header and data pointer; skip Inactive streams. If active stream
   exists and node not Mixed, compose first stream and mix remaining active
   streams. For AVI, when not SourceReady, attempt node refill and increment
   read-attempt count even on mismatch return 13. Convert its ordinal segment;
   if node End, set EndSubmitted. Set Mixed and clear live header Done.
2. When a stream existed, XOR cached output data through the cached header's
   live length, then submit with the callback's device and fresh node header.
   Every nonzero narrowed error, **including33**, is reported; clear Queued.
   On success clear Mixed, set Queued and clear SourceReady. With no active
   stream, fill the live header's data with unsigned silence128 and clear Queued.
3. Rescan fresh head. Remove Ended non-AVI streams. Before each removal, if its
   callback is nonnull, capture it and narrowed effect, clear that field, and
   replace any previously captured callback. Ended AVI remains. Count active
   non-Ended streams and refill their non-AVI NeedsRefill buffers.
4. Reload head and advance live node. Continue only if active count nonzero and
   this next node is not Queued.

After the loop invoke only the **last captured nonnull callback**, passing the
zero-extended effect identity. Return 0. Completion is not one callback per
removed stream, and removal alone does not deliver callbacks.

#### Original §7. AVI audio and position clocks

Attach requires a device (else3), nonnull AVI stream (else12), and returns0
immediately if head is already AVI. Query metadata; original ignored status
requires produced/supplied fields. Require audio type `auds`, else9. Perform a
one-sample sizing read at sample0 with no destination, ignoring output. Compute
sampleSize×length with wrap after that call. Read format at sample0, requesting
14 bytes into the original larger format record. Its retained tail and failed
output are explicit inputs; BF-160's rejection/tail initialization are repairs.

Choose mode by destination/source channels: mono/stereo→1, mono/other→2,
other/stereo→0, other/other→3. Memory capacity is shared MMIO size doubled for1,
halved unsigned for3, unchanged otherwise. Compute stride=capacity/remembered
buffer count unsigned **before** opening the memory stream. Open/get info,
allocate/lock a cleared record; return 7 on open failure or2 on allocation failure,
without invented cleanup of already retained handles. Set kind AVI, format,
metadata, chunk audio type/total bytes, sample stream, Inactive and cached stride.
Build a mask by repeatedly shifting live returned memory capacity right until≤1,
each time setting mask=2×mask+1. Prepend record and increment live active count.

Member attachment returning a status stores stream then sets Attached on success;
on failure it clears member flags. The alternate attachment constructor stores
stream/null and, only on success, sets Attached; failure leaves flags unchanged.
Neither success initializes unrelated incoming bits.

##### Position conversion

Request device position as sample count only when initialized. Recognized output
types are milliseconds, samples and bytes. Unrecognized type returns0 without
reading its union. Recognized but unavailable/partial output needs retained input.

The millisecond query returns milliseconds directly, or converts unsigned samples
by division by the exact binary64 constant22.05 using F01 extended arithmetic,
truncates to signed64 and retains the original low result. Byte output first
uses unsigned division by live output alignment. The sample-position query
instead multiplies millisecond output by that constant, returns sample output
directly, or divides byte output by alignment. Read byte-mode alignment before
consuming the position union. Neither query fabricates0 after an API failure.

##### Preparing and playing a range

Require device (3), then AVI head (5). Restore full remembered buffer count if
needed. Convert current time then end time to samples using the live stream for
each call; multiply end then first by live source alignment. Range bytes are
`(endBytes!=0 ? endBytes : liveChunkSize)−firstBytes`; signed negative returns12.
Reset video audio, then on fresh head store range size, progress/base=firstBytes,
general/partial progress0 and cursor=end. Refill AVI audio. Clear Inactive and
Ended, publish control reference and video-audio state0.

Walk up to remembered buffer count from first: individually clear Queued, Spare,
End, EndSubmitted, Queued again and Mixed; set start from header data, fill and
break on1, otherwise advance live next. Refill AVI head once more. Walk all
remembered nodes setting SourceReady; set buffer-head to first; return 0.
Member preparation sets Prepared on zero or clears it on error, preserving the
other live flags. Missing original flag input is not an implicit zero.

Play, when head is AVI, submits from first, marks fresh head Started and sets
Playing through any nonnull control reference. Then **unconditionally** set
video-audio state1 and publish first as head's next-AVI-buffer; a null head faults
only after the state store in the original. Member play returns the narrowed0.

Reset clears cleanup counter and caches head before its device test. With a
device, reset it, discard only queued wave-completion messages for the remembered
window and count discarded messages. Traverse the cached chain removing all
non-AVI records while retaining AVI records. Finally clear Queued and Mixed on
all active output buffers. Other node flags are not blanket-reset.

##### Refilling AVI data

The simple stream refill acts only when cursor==end or cursor==base. Its available
bound comes from **global head**, even if the passed record differs:
`head.chunkSize−(head.aviProgress−head.aviBase)`. Cap unsigned to the passed
record's capacity; set its cursor to base. Initialize actual bytes 0 and request
count/alignment samples starting at progress/alignment. Ignore API status; add
actual bytes to live progress and fill capacity−actual bytes with unsigned128.

Node refill first caches global-head buffer metadata, then returns13 if the
passed stream's next-AVI-buffer differs from node. Otherwise read at most the
minimum of stride−partial progress and range bytes remaining, using global-head
stream/progress/alignment. Initialize actual bytes 0. Update the **fresh global
head's** progress but the **cached metadata's** cursor; advance partial progress.
Wrap cursor only at equality with end. At stride completion mark SourceReady,
advance next-buffer and set partial progress0. At range end, pad the new/current
stride only while cursor<end, wrap at equality, clear its SourceReady, set End,
mark stream Ended, advance next-buffer and clear partial progress.

The scheduled fill operation differs:

1. Device absent→3; video-audio state0→14; no AVI head→5; Ended head→14;
   next-buffer already SourceReady→0.
2. Distance between next/current buffer ordinals is masked to eight positions.
   When distance<1, override requested samples with remaining stride/alignment
   and increment read-attempt count.
3. Cache head metadata and next node. Cap requested unsigned to signed division
   of contiguous buffer bytes/alignment. Publish this requested-sample count.
   Compute bytes=alignment×requested and cap to stride remainder then range
   remainder. Request samples/start by unsigned division, actual bytes initially0.
4. Update fresh head progress and partial progress, cached cursor and equality
   wrapping. At a full stride mark SourceReady and advance next-buffer.
5. At range end, pad stride remainder, then wrap at equality; mark node
   SourceReady **and** End, advance next-buffer, clear partial progress and set
   stream Ended. Unlike node refill, the original padding has no cursor<end
   guard; every resulting overflow is excluded.
6. Increment fill-count and return 0.

These two refill operations must not be merged into a single policy that loses
the different SourceReady and padding behavior.

#### Original §8. CD music and MCI transport

Shared CD state starts with no device/door device, current track−1, generic-disc
selection0, playback-active1 and track-count 0. The shared exit/playback-request
latch starts1. Primary and “other” MCI devices have separate completion slots.
Native command numbers, selected-field records and notification encodings are
validation adapters; the commands below name their semantics.

##### Opening and querying

Notification initialization registers a hidden media-window class, ignores
registration failure, creates the window, publishes its result even if null,
then opens CD playback. The callback/resource module is re-read after registration.

Open-playback returns if a primary device already exists. Otherwise open type
`cdaudio`; failure logs the error and returns. Publish returned device, set TMSF
time format, and on failure close the **live** device, clear shared device and
log the retained error after close. On success initialize status callback/return/
track 0 and query number of tracks; ignore status error and publish its return
field, including retained0 or partial provider output.

Scenario-status has a distinct failure policy. If necessary open `cdaudio`, cache
the returned device, publish it, and set TMSF. A set failure closes the cached
device, clears shared device and returns−1. Otherwise query track count using
the cached device. Status failure calls scenario-close and returns−1; success
publishes full count and returns its signed interpretation. Scenario-close acts
only if shared device nonzero: close it, then clear device and track-count.
Original API records supply only flag-selected input fields; unused holes are
not logical default values or additional provider inputs.

##### Track playback and automatic selection

Low-level play-track opens if no device; if still absent return 0. With an existing
device, cache it **before** stopping through the shared device. Narrow requested
track to its byte-domain identity. Seek cached device to that track. Failure
closes the fresh shared device, logs and returns0. Compare the narrowed track
to the **live full track count**: for the last track play from it with completion
notification and no explicit end; otherwise end at the narrowed next track.
The notification window is read after seek. Playback failure closes/logs/returns0;
success returns1. The track-only request wrapper directly forwards this result.

Automatic selection queries scenario-status at most ten times, stopping early
only for counts24,18,12 or10. Count1 or−1 clears request latch and returns.
Otherwise repeatedly choose base4 for those special counts, base2 for others.
Let difference=wrapped signed(count−base). If positive, consume one primary-law sample
and take signed remainder by wrapped(count−base+1); otherwise remainder0 with
no draw. Selected=base+remainder. Publish generic-disc flag 0 for special discs,
1 otherwise. If selected differs from retained previous track, stop; otherwise
set previous track−1 and retry, potentially consuming a second draw. Then clear
request latch, store selected track, set playback-active1 and play it, ignoring
result. Missing device/media can therefore still perform track selection;
matching original shared RNG consumption is not required.

Initialize-selection first installs the CD-completed callback, then queries
count once. Count−1/1 clears request latch and retains other flags. Otherwise
set playback-active1, request latch 1 and generic-disc classification.
CD-completed sets request latch 1 **before** reading playback-active; if active,
reconcile. Reconcile, when Music is disabled, stops primary MCI and sets request 1
only if request was clear. When Music enabled and request set, automatic-select.
This stop-and-request operation is the source's exit-named service used by
other presentations; it does not itself clear the game-active state.

The `(track,force)` request first applies Music preference. Disabled music
conditionally stops/requests and returns. With no pending request and force0,
return if the full requested track equals current track. Return on generic disc.
Otherwise perform the same bounded count queries and count−1/1 return. Set
playback-active0 before Stop, install CD-completed, play requested track, then
store the **full** original track, playback-active1 and request 0. Callbacks see
that temporary inactive state; the child still plays the narrowed identity.

##### Controls and notification

| Operation | Ordered behavior |
| --- | --- |
| Open door | If no primary device, open a separate device then request door-open; otherwise cache primary, stop it, request door-open through fresh primary. Publish cached device as door device even if Set failed |
| Close door | With neither device, open a device, request **door-open**, then close it. With a door device, cache and clear door slot, stop primary, request door-closed on cached door device then close it. Primary-only with no door slot does nothing |
| Play/pause | Query mode on live primary; failure returns. Stopped→Play; every other successful mode→Pause |
| Resume | Query mode; Stopped→Play, Paused→Resume, other modes do nothing |
| Next | Open/publish primary if absent; cache it. Query current track, then track count; each failure returns. Play current+1 unless unsigned count<wrapped(current+1), then1 |
| Previous | Same two queries, including the otherwise unused count query. Play1 when current==1, otherwise wrapped current−1; no wrap to last track |
| Position | Require primary; cache it, set TMSF, query position, return0 on either failure. Store optional minutes, seconds, track in that order from fresh packed return reads; return1 |

Only successful MCI notifications dispatch completion. Narrow the reported device
identity, compare “other” first then primary; equal device slots therefore route
to other. Non-notification messages use the default window procedure; all media
notifications return 0, including ignored reasons/devices. Each dispatcher calls
its live nonnull callback once. Primary Stop does not clear its handle. Other
Stop clears its handle after provider return. Platform shutdown stops WAV,
stops primary, scenario-closes primary, stops other, then destroys the retained
notification window without adding a null gate or post-destruction clear.

#### Original §9. Video state, lifetime and media setup

Video uses shared file, video/audio streams, codec, compressed buffer/capacity,
source dimensions, active context, cleanup-ready and running gates, frame-output
pending flag, codec rectangles, start tick and update count. They are **shared**,
not isolated per window. A video receiver contains parent surface, audio member,
palette/backing port, finished/frame callbacks, double-size, background-composite,
audio-enabled, frame-ready and audio-started flags, optional background parent
and saved origin, input/output formats/palette, first/last/current frames,
frame-callback threshold and looping.

Construct parent, audio member, palette and backing port. The audio member only
clears its attached stream, leaving flags retained. Set finished callback absent,
double-size/background-composite/background-parent0, audio-enabled1,
audio-started0, background-parent0 again, frame bounds/current0, private counter0,
frame threshold 0, frame-ready1 and looping0; publish global current window.
The frame callback itself is **not initialized**. Production wrapper repairs
must be declared independently from this constructor.

Create standalone/parented window at requested origin, initial320×240, local
palette; fill black. After those callbacks publish saved x then y, and for the
parented variant first publish the captured background parent. Destruction
stops, releases media (which stops again), clears published current window,
then destroys backing port, palette, audio member and parent. The member
conditionally removes AVI head if its attached-stream reference is nonnull,
without clearing that reference. Deleting destruction additionally frees storage
when requested. No fabricated cleanup runs at an unresolved read stop.

Open-media first releases shared video resources, then initializes:

1. Initialize AVI service if needed and set its latch. Publish this active
   context, initialize the documented codec-control records and clear shared
   updated-frame rectangle components.
2. Open file; obtain video stream index0. Either failure releases resources and
   returns that status. If receiver audio-enabled, obtain audio stream index0;
   failure clears audio-enabled. On success clear audio-started, attach audio
   and set audio-enabled exactly when attachment returned0.
3. Read video format at sample1, requesting its complete nominal header; query
   stream info. Original statuses are ignored, so unavailable fields need
   retained input. Overwrite input image size from suggested buffer size and
   compute unsigned frames/second=rate/scale before codec lookup.
4. Locate decompressor for the stream handler. If absent release and **return 0**.
   Set first from stream-start, then last=live first+live-stream length−1 and
   current=first. Copy input header to output; force uncompressed indexed8,
   image size 0 and256 colors. Publish source width/height. For top-down graphics,
   negate output height with wrap. Resize surface to source dimensions, doubled
   when the live receiver requests it.
5. Allocate/lock compressed buffer using suggested size when nonzero, otherwise
   signed(wrapped bitDepth×width×height)/8. Failure releases and **returns0**.
   Query decompression compatibility at source dimensions. Nonzero status
   releases and returns it.
6. Ask codec for output palette, then read colors10..245 into an RGB snapshot.
   Import those236 colors into receiver palette at 10. Failed or incomplete codec
   output needs backed palette input. Set codec palette and begin decompression
   at source dimensions. Begin failure releases and returns status.
7. Publish cleanup-ready1 and running0. Configure display for live compositing
   mode. **After** that operation, set compressed-read capacity to the wrapped
   frame-byte formula; reset update count and private update state; return 0.

A0 return therefore does not guarantee a usable codec/buffer. Configuration can
already seek/read with the *previous* compressed-read capacity before step 7
publishes the new capacity. Preserve this order and error behavior; do not infer
that allocated suggested capacity is also the read argument.

##### Palette and compositing configuration

Palette-map requires receiver and cleanup-ready. End decompression, extract
236 colors starting at 0 from the supplied palette, and overwrite output palette
entries0..235 in BGR form with reserved0. Set codec palette. Capture doubled flag;
if true, double output height then width. Begin decoding with source input
size and doubled destination dimensions when selected. After the callback,
restore dimensions by signed truncating division by2 of their **live** values.
Nonzero status clears cleanup-ready. Palette assignment then applies the supplied
palette to the surface and presentation regardless of the map helper's gate.

Display configuration also gates on receiver/cleanup-ready, ends decompression,
publishes codec mode records and begins with shared source dimensions. Doubled
output fields are restored with arithmetic right shift, unlike palette-map's
truncating divide. Begin failure clears cleanup-ready. Mode 0 still allocates an
empty backing port and returns. Nonzero mode still allocates source-sized backing,
constructs the current-surface rectangle, offsets source by saved origin and
copies any background parent. If cleanup-ready remains set, copy backing to
surface, seek current frame without update and present, without new gate tests
between these last calls.

Background-composite setter acts only on a state edge, publishing the flag before
configuration. Double-size ignores its argument: obtain height then width,
double both with wrap, resize, then set doubled1. It is not a toggle or a no-op
when passed0.

##### Stop and release

Stop requires receiver and cleanup-ready. If audio-enabled clear audio-started;
set frame-ready1. If running, clear media-pump gate, output-pending and running,
in that order. It does not itself remove audio streams.

Release(null) returns. Otherwise Stop, then conditionally close codec, release
audio stream, release video stream, release file and exit AVI service, clearing
each shared reference **after** its callback. For a compressed buffer, resolve
allocation handle/unlock, then resolve the handle again through the **live buffer
reference** and free; clear buffer. Finally clear compressed-read capacity,
source dimensions and active context. The cleanup-ready flag is not blanket
cleared here. Children/provider side effects survive their original ordering.

#### Original §10. Seeking, decoding and playback scheduling

##### Range and frame controls

Start-member clears looping then calls Start. Full-range reset requires receiver/
cleanup-ready, stops if running, queries first then length from fresh stream,
sets last=live first+length−1 and current=first, then seeks first without update.
Rewind performs that reset. Full looping resets, sets looping1 even if reset's
gate returned, then Start.

There are three distinct range entries:

| Entry | Order |
| --- | --- |
| Member nonlooping range | Signed first>last returns before receiver access. Clear looping, seek first without update, store last then first from captured arguments, Start |
| Member looping range | Store first, seek it, store last, set looping1, Start; no signed-order rejection |
| Procedural range | Gate on receiver/cleanup-ready; stop if running; seek first, store first then last, Start; does not change looping or reject reversed arguments |

Seek requires receiver/cleanup-ready and stops if running. Find the previous
keyframe for the **full incoming** frame argument. Store result as current.
While unsigned current<target, decode in preroll mode; target is0 for a negative
signed incoming frame, otherwise its value. Ignore each result. Then decode
normally and, if requested, update context. A failed decode can leave the loop
without progress; safe bounded stopping is allowed, fabricated frame advancement
is not. Member seek requests update1.

Copy-frame rejects negative x/y and insufficient signed wrapped remaining
width/height before decoding. Decode normally, ignore return, reload dimensions,
copy the full current frame to destination offset x/y. Export-frame instead
decodes first, enlarges destination to both source dimensions when either is
insufficient, reloads dimensions and copies. Background-copy constructs a full
video rectangle, offsets its source by saved y/x reads and copies the optional
background parent into backing port.

##### Decode one frame

Return 1 for null receiver or unavailable cleanup gate. For composited mode and
normal flags, restore the retained updated rectangle from backing before reading.
Initialize bytesRead0; request one current video sample into shared compressed
buffer with the shared read capacity. Nonzero read status returns immediately.
Publish bytesRead into input image size. Capture double-size and temporarily
double output dimensions if needed.

Capture destination height then width from shared source dimensions, doubling
each for doubled mode; obtain live
surface storage, then decompress using fresh codec/input/source dimensions and
the captured destination extents. The provider receives complete source and
destination rectangles; its codec algorithm is an external decoder contract.
Restore doubled output dimensions using arithmetic shift of **live** fields.
If decompressor result is neither0 nor1, show its formatted error message and
continue.

If threshold is nonzero and equals live current frame, clear it **before** calling
the live frame callback. Then, if unsigned last<current and shared running:
looping seeks first then Starts; otherwise Stop then invoke finished callback.
Return 0 on that completion branch. Otherwise increment live current and return 1
for nonzero flags, else full decompressor result. Thus the frame at exactly last
is still decoded/incremented; completion here uses strict greater-than after
callbacks. Read failure does not run frame callbacks or advance current.

Frame-callback setter publishes callback then threshold. Callback invocation
requires established input even though threshold defaults0. Finished callback
is independently initialized absent and invoked only when nonnull.

##### Start

Require receiver/cleanup-ready; stop if already running. If doubled, end
current decoding, publish doubled/compositing codec-control settings, temporarily
double output dimensions, begin decompression and ignore its return; restore
live dimensions by arithmetic shift. If audio-enabled, convert last frame then
current frame to time using the fresh video stream each time; prepare audio,
ignoring status.

Recheck cleanup-ready. If set and not compositing, update context before setting
running. Do not recheck cleanup after that update: publish media-pump gate1,
running1 and update-count 0. If live audio-enabled, play audio then set
audio-started1; otherwise capture start tick. These operations can reenter
another presentation before the parent's later live reads.

##### Update context

Require receiver/cleanup-ready. If running with audio-enabled, query member
Playing. If false, either Stop+finished callback or seek first+Start for looping,
then return. Otherwise query audio milliseconds, map to video samples and add
live first to get target. If frame-ready is false or unsigned current>target,
fill AVI audio with 64 requested samples, increment update-count and return.

If running without audio, get tick, convert wrapped(tick−startTick) to video
sample and add first. Unsigned current>target returns. Unsigned last<target
performs the same finish/restart branch and returns. Audio mode does not add
this time-based end test.

When running and output-pending, present the whole surface for noncomposited
mode. In composited mode form the codec's current changed rectangle, union it
with retained updated rectangle, present that region, then copy the **live
post-presentation local rectangle** into retained updated rectangle.

If current<target, find successive next keyframes starting after current, retaining
previous, until signed next≥signed target or next is absent. If previous==current,
choose next only when it exists and unsigned target−current>next−target.
Otherwise choose next when it exists and signed target−previous>next−target;
else use previous if present. Ties keep previous. While unsigned current<target
and current<wrapped(last−1), decode preroll, ignoring return. A stalled provider
requires a safe stop, not a synthetic current++.

Finally, if still running and live audio-enabled, fill audio64 and increment
update-count. Decode normally regardless of whether running was initially set;
set output-pending exactly when that decode returned0. P05's media pump drains
wave-completion messages before this update; its general idle path can invoke
an additional update after pumping. Keep those two opportunities distinct.

#### Original §11. MCI movie services and track chooser

These are separate from the AVI codec player above. An MCI movie window owns a
child-window token. Creation first destroys an existing child, captures the
application module, resolves the parent, creates the child with no menu/playbar
and notification enabled, and publishes the returned token even if null. Creation
with a filename then selects frame time format even if creation failed; empty
creation omits that call. Open sends the filename, then selects frames through
the live token. Play, Stop, Pause, Seek and Position forward their arguments;
Seek-end uses −1. Show shows then realizes the child; Hide only hides. Close
sends media-close then window-close through the live token, then clears it.
Destroy performs Stop then Close only when its entry token is nonnull.

Center queries the outer window rectangle and the live parent's client rectangle,
then moves to arithmetic-half of their wrapped width/height differences, keeping
size and z-order. Failed rectangle queries require retained output after both
calls. Resize keeps position/z-order; Move keeps size/z-order. Assign-palette
uses the captured child token.

The lower-level device API opens type `avivideo`, reports success only for zero
status, and closes the device obtained by that name; closing also deletes the
live global palette when present, then clears it. File-open supplies child style
and parent; failure returns null. Success allocates a device record, obtains its
identity **by name**, shows the media window, queries its window token while
ignoring status, and sets realization to background. It does not use the open
command's returned identity. Background=false selects normal realization;
true selects background. Play notifies the window's narrowed identity. Stop is
waited; Rewind seeks to start; Seek uses wait+to; Position requests current
position with wait. Source rectangle ignores query status. Double-size queries
source and puts a rectangle `(0,0,2×right,2×bottom)`, without subtracting its
origin. Release frees the record; it does not close the device. Unknown failed
query output and allocation failures are explicit input/stop boundaries.

The music chooser constructs a P04 prompt, then tries scenario-status up to five
times, stopping on 24/18/12/10 tracks. On −1 it closes the scenario device; every
unsuccessful iteration, including the fifth, sleeps 200 ms. Final −1 or any
other count shows `NOPICKMUSICNEW`, destroys the prompt and returns. Counts
24/18/12/10 load `PICKMUSICFANWORLDS`, `PICKMUSICSCENARIO`, `PICKMUSICGOLD` and
`PICKMUSIC`, respectively. If live current track>1, select current−2 initially.
Negative signed modal result cancels. Otherwise request wrapped(choice+2),
force=1; destroy the prompt on both normal paths.

#### Original §12. Timing, palette transitions and block reveals

Prepare-presentation discards queued input unless the network input-retention
gate is set, pumps messages, flushes queued paint, then pumps again.
`waitWithMessages(ticks)` computes signed-truncating `wrap(ticks×50)/3`, captures
the multimedia clock, and executes at least one iteration: pump, sample clock,
then network-poll(1,0) if live game mode≥3. Repeat while signed wrapped elapsed
is less than duration. The retained timestamp precedes the poll. The separate
platform-tick wait captures start and repeatedly compares **signed absolute**
current time to wrapped(start+ticks), pumping only while less. It is not an
elapsed-time comparison. P05 owns message dispatch and timers; N02 owns receipt.

Game-window presentation visits map views 0..7 (view0 always; others only when
enabled), overview, main status and city, then flushes paint. Update uses the
same order with Update instead of Present. Gray remapping locks the application
lookup; null returns. Remap map views, main status, overview and city, in that
order, over indices 10..245; unlock and present all. Restoring palette flags
sets all 256 entries to the reserved game flag and commits indices 10..245.

Fade-to-gray clears the shared presentation latch before testing palette mode.
When enabled: P03 prepare-fade(10,236,10,gray), present all, then for steps 10
down through 0 call fade-step and waitWithMessages(1). Restore flags, remap
windows, finish-fade, restore flags again. Fade-from-gray tests mode first;
prepare the same fade, apply step 0, update all, present all, then steps 0..10
with the same wait. Finish and restore flags. P03 owns color interpolation.

##### Random block reveal

Input is a surface, optional rectangle, block size and mode. Copy the rectangle,
or construct the full surface rectangle by querying height before width. Compute
columns=trunc(width/block), rows=trunc(height/block), remaining=wrapped(rows×columns).
Allocate a zeroed coordinate list of that many entries and lock it. Failure
returns 0; failed lock retains the allocation. Zero divisor faults before
allocation in the original. All allocation/count overflows are safe-stop cases.

Populate coordinates in the following order, pumping once for every entry:

| Mode | Enumeration | Coordinate within rectangle |
| --- | --- | --- |
| 1 | Column outer, row inner | `((columns−column−1)×block, row×block)` |
| 2, 5 | Column outer, row inner | `(column×block, row×block)` |
| 3 | Row outer, column inner | `(column×block, row×block)` |
| 4 | Row outer, column inner | `(column×block, (rows−row−1)×block)` |
| Other | No population loop | Retain the allocation's actual zeroed entries |

Cache rectangle left/top after the population pumps. While remaining!=0: pump;
maximum=remaining−1. For mode 5 minimum=0; otherwise minimum is signed
`wrap(remaining−1−unsigned(wrap(rows×20))/block)`, clamped to 0 when≤0.
Select with F01 inclusive-range(minimum,maximum). A one-entry range is
deterministic and requires no sample.
Add cached origin to the selected coordinate. End coordinates are the signed
minimum of coordinate+block and rectangle right−1/bottom−1. Present that
rectangle. Decrement remaining, then replace selected x with the last entry's
**post-presentation** x; replace y afterward from its then-live value. The list
is an unordered bag, not a stable removal. Finally unlock, resolve the live
allocation handle again for free, return 1.

Integer division omits residual partial rows/columns; the right−1/bottom−1 caps
also matter with half-open graphics rectangles. Do not silently extend coverage
to the last pixel. Visual sampling may use its own context and need not change
simulation randomness.

#### Original §14. Opening, wonder and spaceship launch movies

All these entries use §§9–10, including their shared resources and false-success
returns. Missing art/media, failed codecs, retained flags and callbacks are
separate from successful playback. A safe wrapper may initialize missing owned
state, but must identify that repair rather than claim an original producer.

##### Opening

Construct video, outer window, palette and module; query screen bottom then
right; publish the video. Resolve `civ2\civ2art.dll`, then `civ2\video\opening.avi`.
On success create the black outer window with screen width and bottom+5 height,
republish video, create it, install click/key/finished callbacks, enable double
size, then open media. Nonzero status clears the published pointer; class-not-
registered also shows `VFWNOTREGISTERED`. Missing either file exits without that
clear, so the original leaves a published reference to the subsequently
destroyed local. Do not turn that dangling reference into usable new state.

On zero status fill black, center, show outer/flush, show video/flush, start,
capture input, install the network idle callback when mode>2, run modal, clear
idle, release capture, stop, hide video/flush, hide outer, clear publication and
close module. Destroy module, palette, outer and video on the normal tail.
The finished latch is not reset at entry. Finish, when the published video is
nonnull, clears modal, sets finished 1 and requests effect 107 looping. Keys
208..210 request 107, set finished 1, then clear the freshly selected modal.
Left-click only clears modal if present: it neither sets finished nor plays107.

##### Wonder

Construct outer/map surface, video, background and palette, then publish active
wonder. Setup constructs temporary sprite then port, requests heap compaction,
and resolves `civ2\wonder.dll`. If absent allocate64×32 background filled33.
If present, open module and load resource `wonder+20000`, palette 10/count 236.
Load failure shows `Failed to load civ2art.gif`/`NOTICE` and continues without
the missing-module fallback. Success extracts `(1,1,64,32)` with transparent 2,
draws into64×32 background, resets temporary art, closes/destroys the module.

After those callbacks store wonder, read building name at wonder+39, create the
320×240 titled intro surface, assign palette, fill 0, draw status, update, center
and install click. Status drawing temporarily binds the background, invokes the
P01 intro renderer, then restores the canonical main port, not the entry binding.
Destroy temporary port then sprite. Run first resizes for margins/caption and
prepares/redraws/fills/updates/centers/flushes; only then test the two map-window
disable flags. Resolve `civ2\video\wonder` + (`0` when signed wonder<10) + decimal
wonder + `.avi`. Missing file returns. Bind outer click/key, create video child
at margin/caption, open. Nonzero returns, showing the codec notice only for
class-not-registered.

On success bind finish/child-click, assign both video and presentation palettes,
fade game to gray, assign the main palette, show outer then video, flush, start,
install network idle if mode≥3, run modal, clear idle, reset video audio, hide
video/flush, restore initializer palette, hide outer/flush, fade from gray.
Click/finish clear active modal; keys208..210 do likewise. These callbacks require
a backed active receiver. Wrapper order is reset audio, construct, clear shared
presentation latch, request music stop/reselection, setup, run, reset audio,
reconcile music, optionally P08 improvement help(wonder+39) when language!=0,
destroy. Destruction clears active publication before palette/background/video/
outer destruction. Unknown embedded flags/frame callbacks remain explicit inputs.

##### Spaceship launch

[P07](P07-advisors-reports-and-history.md#19-spaceship-presentation) owns the
surrounding launch controls and subsequent V07 launch mutation. Request music
stop/reselection, allocate/construct a video and publish it in the report. Resolve
`civ2\video\launch.avi`. If found, create a standalone doubled video and open it.
Every nonzero open result returns immediately, retaining the published allocation
and omitting deletion, token clear and music reconciliation; the codec-not-
registered result additionally shows its notice.

On success hide the report/flush, fill video black, bind the report's click/key
handlers and a finish callback, center, present, show/flush, start, discard queued
input, capture, install network idle for mode>2, run modal, clear idle, release,
hide/flush. Reload the published video for each child, as callbacks may replace
it. After success or missing file, cache the then-live video, delete if nonnull,
clear the report token and reconcile music. Finish selects the live active
report and clears its video modal only if its token is nonzero.

#### Original §15. Herald video and spaceship stars

P07 owns herald creation, art, input and sound-category selection. The table
below supplies this document's media inputs. Each row gives leader code, sound
category, child origin, and five inclusive ranges: intro, idle, responses2/3/4.
The response4 overlap for leader 18 is intentional. Tables are initial mutable
state, not a claim that callbacks can never alter them.

| Leader | Code | Category | x,y | Intro | Idle | Response2 | Response3 | Response4 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | ROM | 2 | 81,43 | 0..29 | 30..59 | 60..89 | 90..119 | 120..149 |
| 1 | BAB | 1 | 76,86 | 0..29 | 30..59 | 60..89 | 90..119 | 120..149 |
| 2 | GER | 2 | 94,58 | 0..29 | 30..59 | 60..89 | 90..119 | 120..149 |
| 3 | EGY | 0 | 100,105 | 0..29 | 30..69 | 70..99 | 100..129 | 130..154 |
| 4 | AME | 2 | 43,0 | 0..29 | 30..59 | 60..89 | 90..119 | 120..149 |
| 5 | GRE | 2 | 55,89 | 0..29 | 30..69 | 70..94 | 95..124 | 125..149 |
| 6 | IND | 0 | 93,86 | 0..29 | 30..59 | 60..89 | 90..119 | 120..149 |
| 7 | RUS | 2 | 92,64 | 0..29 | 30..59 | 60..89 | 90..119 | 120..149 |
| 8 | ZUL | 0 | 7,13 | 0..39 | 40..59 | 60..89 | 90..119 | 120..149 |
| 9 | FRE | 2 | 102,69 | 0..29 | 30..59 | 60..89 | 90..119 | 120..149 |
| 10 | AZT | 0 | 103,46 | 0..29 | 30..59 | 60..89 | 90..119 | 120..149 |
| 11 | CHI | 1 | 105,111 | 0..29 | 30..59 | 60..89 | 90..119 | 120..149 |
| 12 | ENG | 2 | 94,58 | 0..29 | 30..59 | 60..89 | 90..119 | 120..149 |
| 13 | MON | 1 | 66,43 | 0..29 | 30..59 | 60..89 | 90..119 | 120..149 |
| 14 | CEL | 2 | 11,104 | 0..39 | 40..59 | 60..89 | 90..119 | 120..149 |
| 15 | JAP | 1 | 53,28 | 0..29 | 30..59 | 60..89 | 90..119 | 120..149 |
| 16 | VIK | 2 | 11,72 | 0..29 | 30..43 | 44..66 | 67..85 | 86..104 |
| 17 | SPA | 2 | 98,59 | 0..29 | 30..59 | 60..89 | 90..119 | 120..149 |
| 18 | PER | 1 | 80,82 | 0..29 | 30..69 | 70..94 | 95..124 | 120..149 |
| 19 | CAR | 2 | 75,61 | 0..29 | 30..59 | 60..89 | 90..119 | 120..149 |
| 20 | SIO | 0 | 66,43 | 0..29 | 30..59 | 60..89 | 90..119 | 120..149 |

Herald initialization constructs a temporary palette, stores the other leader's
low identity, then re-reads the signed leader for its code. Filename is
`civ2\kings\` + (`hrld`+code truncated by writing a terminator at position7) +
`.avi`. Resolve it; failure destroys only the temporary palette and returns0.
Read table y before fresh x, create a child on herald art and open media.
Nonzero status shows the codec notice only for class-not-registered, destroys
temporary palette and returns0, retaining the embedded video. Success sets
finish=update-herald, enables background compositing, loads palette resource
leader+1000, copies its colors146..245 into herald palette, assigns video and
presentation palette, seeks frame 0, destroys temporary palette and returns1.

Intro reads range last before fresh first and starts nonlooping. Update captures
the current herald before testing active-animation. Inactive returns. Request0
reads idle last then fresh first and starts looping. Otherwise cache request,
clear it before table reads, read last then first for that request and start
nonlooping. Only after the range call read sound: modes2..4 use the three live
category variants; play(mode 1, no loop). The otherwise reachable mode 5 reads
the supplied adjacent prerequisite-count scalar as sound; unknown other sound
slots require established input or stop. No adjacent writes are authorized.

Request-response captures herald, gates on active/nonzero, reads current frame
before validating full mode, and accepts only 2/3/4. Require strict
`idle.first < frame < idle.last`, with a fresh leader lookup for each bound.
Publish the narrowed request then invoke Update, which re-selects global herald.
Boundary frames do not interrupt the idle animation.

##### Star field

Initialize 80 stars in order, consuming three F01 inclusive draws each: x from
−32000..32000, y from −24000..24000, depth from 1..100. Query surface height then
width and fill `(0,20,width,height)` with color 10. In a second pass project
`screenX=trunc(x/depth)+320`, `screenY=trunc(y/depth)+240`, using F01 wrapping
addition and signed division. If outside inclusive0..640 or0..480, draw x/y
again from their ranges, set depth 100 without a depth draw, and reproject once.
Write color 41 at the result. Exact right/bottom edge coordinates are not rejected
by this test; P03 clipping or a safe excluded-write stop must prevent corruption.

Then reset P07 typing cursor/phase to offset 0, x10, y115, phase 1 and run its text
step until false. Compose label 454, colon, space and the live owner's formatted
arrival year; define the success text. Draw it in rectangle `(0,449,639,479)`
with shadow10 offset(1,1), foreground 250, present it; draw the component sprite
at(175,99). If live presentation state3, set typing y140.

Each star timer captures active report **before** pumping. For each star, project
old x/y using separately read depth; add 250 to live x, add 300 to live y, subtract1
from depth. If depth becomes0, draw new x, y, depth 50..100 in that order. Project
again; outside inclusive bounds triggers a second x/y/depth draw and one more
projection. Erase old point only when strictly inside `(0,640)×(0,480)` and its
pixel is41; write 10. Draw new point only when strictly inside and its pixel is10;
write 41. Present the whole report after all 80. A star can consume six replacement
draws in one tick; collisions and existing art affect pixels. Zero depth or
signed-division overflow faults retain prior effects and may be safely rejected.

#### Original §16. Credits

Credits own a string sequence, scroll position, partial-redraw flag, last dirty
rectangle and 30 cached line sprites with line identities, widths and x positions.
Initial line count/scroll/partial flag are0; initial cache identities and pointers
are0. The About controller, rather than the static initializer, changes all cache
identities to −1 before use. Allocate-lines uses the narrowed requested capacity
and clears count. Append allocates narrowed(strlen+1), copies the current string,
increments the live count with wrap and returns the previous count. Traversal
walks NUL-terminated strings; unknown termination or escaping storage stops.

Read mode 0/1/2/3 selects file `CREDITS`/`MPCREDITS`/`FCREDITS`/`SCREDITS`,
section `CREDITS`. Invalid mode or section-open failure returns1. Read F03 lines
in mode 1 until EOF or a line starts `@`, appending each preceding shared line;
return 0 **without closing the shared stream**.

Draw(mode) forces full redraw when partial-ready is false. Full mode prepares
the auxiliary surface and copies its entire background; partial mode restores
the last dirty rectangle from background relative to the inner origin. Set
top=inner.y, bottom=top+374; obtain rowHeight from the font. Select auxiliary
target/font and style(37,18,2,1), publish text advance/clip metrics. Let
`line=trunc(scroll/(rowHeight/2))` and `y=top−2×remainder(scroll,rowHeight/2)`.
If signed line≥count, clear modal and return after the background effects.
Free cached nonnegative identities below line, clearing pointer and identity.

Set maximumWidth=2. While y<bottom and line<count:

1. Retrieve the line. Scan all 30 slots until an exact line identity is found,
   remembering the first negative identity. A miss with no free slot returns
   immediately, without final presentation or modal clear.
2. On a miss, copy text into the shared buffer. A leading `^` is stripped once
   and chooses foreground 186; other lines choose190. Both use background 18 and
   style offsets−1,−1. Measure text width. Construct a temporary port; if width
   nonzero allocate width×rowHeight, assign initializer palette, select target,
   fill 250 and draw at 0,0. Allocate/publish a sprite and, for nonzero width,
   extract using transparent 250. Delete the port. Publish cached line/width and
   `x=inner.x+asr(inner.width,1)−asr(width,1)`.
3. If width nonzero draw at cached x,y. Replace maximumWidth when≤width;
   increment line and add rowHeight to y with wrap.

Add2 to maximumWidth. Center the resulting dirty rectangle over inner width,
from top to bottom; publish it and partial-ready 1. If inner width≤maximumWidth,
force full presentation and clear partial-ready. Full presentation draws status
then presents all; otherwise present only the dirty rectangle. Redraw calls
Draw(1); timer Advance increments scroll with wrap then Draw(0). Bad row metrics
can divide by zero or run across wrapped positions; BF-191's metric rejection
is a restrictive repair, not the original font contract.

About starts timer identity−1, allocates4096 string capacity and resets caches.
On successful read, reset scroll/partial state, set auxiliary actor 0 and create
600×400 presentation using resource 10000 for mode 3, otherwise−95. Install Close
and Redraw, request CD track 3 with force0, update/show, prepare-presentation,
start repeating timer 50, and for network mode install the report's polling idle.
Record shared game ticks, run modal, clear idle and auxiliary selection.
On both normal paths delete occupied cached sprites, clear their entries, reset
timer if signed identity≥0 and release lines. This controller does not close the
credits stream; the owning P05 menu path closes it after About returns.

#### Original §13. Map, combat and nuclear animation timelines

The caller supplies unit/sound/frame identities and has already decided gameplay
effects. [P01](P01-map-projection-and-interaction.md) owns view containment and
projection; [P02](P02-map-rendering-and-minimap.md) owns the unit/sprite renderers.
No stereo pan or distance-volume calculation is added to the sound dispatcher.
View visibility controls whether these callers request sound at all.

##### Moving a unit

Construct eight saved-background ports, set shared animation-busy, and reset the
two movement/network scratch identities to −1. Use W01's ordered direction table
for a backed direction 0..8; destination x uses W01 horizontal wrapping and y
uses wrapped addition. Redraw source then destination using the live human for
each call, and set the shared movement redraw identity to −1. Visit views 0..7,
view0 always and others only if enabled; a view participates when it contains
source **or** destination. Cache its origin,
project both endpoints, subtract its live vertical pixel step, and form their
union using unit image width/height. Allocate/save that union background.

The first participating view sets step=scaled(2); values<2 become1 and value 3
becomes4. Set frameCount=trunc(tilePixelHeight/step)+1 and delay=256/frameCount.
For a human air unit double step and recompute frameCount **after** delay was
computed. Other views derive step=trunc(their tilePixelHeight/shared frameCount).
Set each current point to source; deltas are step×direction.x and
trunc(step×direction.y/2). With no participating view, skip the frame loop.

For each frame, visit participating views in order. Drop a view if its source is
no longer contained or cached origin changed; destination containment alone does
not keep it active now. If sound≥0, request it only once, before the first draw,
using supplied extra as its mode and publishing the sound latch before the
callback. Negative movement sounds are skipped, not cancellation requests. Draw unit in
animation mode 5 at current point with live scale. Present a rectangle extending
two steps around the unit, four steps plus live unit width/height in size,
clamped to the saved union. Restore the unit rectangle from saved background
using live drawing bias/dimensions, then advance its point.

Between frames, except the last, skip delay only when the fast-animation option
is set. Otherwise pump, sample clock, poll network(1,0) for mode≥3 until signed
wrapped elapsed≥delay; retain that sampled clock for the next interval. Clear
busy before destroying saved ports in reverse order. No final redraw is added.

##### Eight-frame effect

Construct eight saved ports; set busy and clear received-interruption count.
For each enabled participating view containing the tile, cache origin, scale
32×32, project tile and add the view's quarter-tile width to x. Save the effect
rectangle into a port sized to that view's tile width/height. If none participate,
clear busy, destroy ports in reverse and return 0. If supplied sound!=0, request
it with mode 1; negative values retain the cancellation behavior. Capture clock.

For frames0..7, visit active views: drop one no longer containing the tile;
if origin changed, recapture its background. Set its scale, draw this effect
frame and present its area. Then pump, sample clock and, for network mode,
poll(1,0); a received interruption exits **without** restoring backgrounds or
the final wait. Otherwise wait until signed elapsed≥64 ms, retain the sample,
and restore each active saved background. After all frames waitWithMessages(10).
Both completion paths reset sprite scale, clear busy, destroy ports in reverse
and return 1. N02 owns delivery and the interruption count.

##### Nuclear effect

Invalid map coordinate or invisibility to the live human returns immediately.
Set busy; allocate/construct the temporary port; ensure views show the tile,
prepare-presentation and request effect 50 in mode 0. If Sound Effects remains
enabled, pump, capture time, then pump/poll/sample until signed elapsed≥5500 ms.
The sound wait still occurs if file playback failed.

Use primary-view scaled size 91×72. Allocate the port, project the tile, and
place its top-left at tile point plus horizontal/vertical pixel steps minus
arithmetic-half of width/height. Save that full background. Clamp only rectangle
left/top to0; leave right/bottom unchanged. If resulting width/height are
positive, set scale and animate 11 frames at the unclamped origin, presenting
the clamped area; after each frame pump, poll when needed, then sample until
signed elapsed≥100 ms and restore saved background. Reset scale and present
the area again. Delete the port when nonnull, then clear busy. Unknown allocation
output stops at use; it does not suppress or reverse the U07 blast already made.

#### Original §17. Ending presentations

[V08](V08-scoring-victory-and-retirement.md) owns whether these presentations run,
winner/loser selection and subsequent scoring/retirement. These presentations
must not independently award victory. All text substitution is P04 and all
year formatting is F04. Unless stated otherwise, resources import colors10..245;
screen-covering outer windows are black, positioned at screen left/top, sized
right−left by bottom−top+5. Video windows are doubled, centered and filled black.
Open status class-not-registered shows `VFWNOTREGISTERED`; other errors remain
silent. Shared receiver selection stays live across callbacks.

##### Defeat and successful arrival

Both construct outer, palette, video, resource module, font and background, then
actor 0. Their wrappers allocate/publish the object and return on null. Destroy
background, font, module, video, palette and outer, then free when requested.
The wrapper re-selects the live singleton for deletion after all callbacks,
then clears its publication.

Defeat preparation queries screen, tests installation-directory+`\loser.avi`,
then installation-directory+`\civ2art.dll`; either absent returns0. Load module,
store actor=`civilization<1 ? wrap(−civilization) : civilization`, create outer,
load resource 4000 into video surface/palette, create doubled video and open it.
Error returns0. Success installs defeat-finish, sets text rectangle
`(40,100,600,480)`, allocates background to video height/width and copies frame,
sets font(face0,size 18,style2), closes module and returns1. Preparation failure
has no substitute ending dialog.

Arrival preparation stores the same absolute actor **before** querying screen,
initializes resource search with `civ2\civ2.exe`, resolves
`civ2\video\winwin.avi` then `civ2\civ2art.dll`, and returns0 on either miss.
Use resource 2000, finish=arrival-finish, frame-caption threshold 683, text rectangle
`(2,100,638,480)` and font(0,18,3); other successful setup is as above. The
nonnegative arrival wrapper first requests music stop/reselection. If preparation
fails, form dialog strings0=launch year,1=population-counter+`0,000`,2=adjective,
4=ruler; format arrival year but put its fresh numeric value in **number slot 3**.
Run P04 `CENTAURI` prompt with the spaceship sprite, then normal deletion.

Defeat Show first requests CD2 force1. Both Show outer/flush, assign outer palette,
show video/flush, capture, select color 255. Defeat scans advances0..99 for the
first owned advance, uses its name lowercased in string0 and actor ruler in
string1. No advance found reads the original index100 name field: supplied
backing or a stop is required; do not choose a substitute technology. Arrival
uses string0=launch year,1=population-counter+`0,000`,2=adjective,3=formatted
arrival year,4=ruler, re-reading actor/state after each callback.

For pages0..2, clear caption/shared line and open `ARCHAEOLOGISTS`+page for defeat
or `CENTAURI`+page for arrival. Missing section skips the page. Otherwise read
mode 1 lines until first character `@`, appending each cursor string then a space.
EOF before `@` needs a stop, not an implicit terminator. Substitute the complete
caption. Restore text rectangle from background, offset(1,1), draw shadow0,
offset(−1,−1), draw foreground 255 for defeat or241 for arrival. Reveal blocks
size 4/mode 5, close rule file, waitWithMessages(360). Long captions may use safe
growth or rejection; overwriting the following live members is excluded.

Start video, install network idle for mode>2, run modal, clear idle, release
capture, hide video/flush. Defeat restores the **main** initializer palette
before hiding outer; arrival hides outer before restoring the main palette.
Flush. Arrival-finish only clears the live singleton's modal.

Defeat-finish gathers `ARCHAEOLOGISTS3` the same way. If found, substitute, uppercase
the result, set rectangle by origin/size `(229,76,200,133)`, draw color 0, reveal
4/5 and close file. Missing section still waits360. Then re-select the live
singleton and clear modal. No key/click skip handler is installed by this ending.

Arrival frame-caption first clears frame-ready, even if its text section is
missing, then clears caption/line and opens `CENTAURI3`. On success select its
font, video target and style(255,0,1,1), y12. Read each line until `@`; copy and
substitute each line independently, center in live text rectangle at y, then add
live font height. Reveal rectangle with top12/bottom=y using4/5 and close file.
It does not wait or clear modal. Stopping drawing here does not mean stopping
audio/playback scheduling.

##### Another civilization arrives first

Negative spaceship-ending input uses this branch. Construct inner surface,
module, outer, palette, background and font; retain the wrapped absolute actor.
Set font(0,18,3), resolve/load `civ2\civ2art.dll`; if missing, destroy normally.
Create full-screen outer and a separate, **unparented** 640×240 inner surface,
load resource 30000, center it, save background over `(10,0,630,240)` and assign
palette. Show outer/flush, assign palette, show inner/flush, capture, color 255.
Use the arrival string substitutions above except string4 is the **local human's**
ruler. Set rectangle top to arithmetic-half of `240−2×cached font height`.

Visit `CENTAURI_BEATEN0`..`4`, skipping missing sections. Gather/substitute text,
restore background, draw offset shadow0 then foreground 255, reveal4/5, close
file and wait 360, as above. Release, hide inner/flush, restore outer initializer
palette, hide outer/flush, close module. Destroy font, background, palette,
outer, module and inner. There is no AVI movie or post-page modal loop here.

##### Conquered civilizations

Construct outer, palette, inner, port, module, moving figure, portrait and font;
actor 0, publish singleton, figureTop0, portrait rectangle `(208,57,433,332)`,
font(0,24,1). Prepare queries screen and resolves `civ2\mk.dll`; miss returns0.
Load module, set actor, create outer and centered parented640×480 inner.
Load10001, extract moving sprite with transparent 8 from `(0,0,297,173)`;
load10000, copy full background to inner. Set text rectangle `(193,371,448,461)`,
allocate the shared saved port to image dimensions, and save that text area.
Find first of12 history entries whose conqueror equals narrowed actor, clear
show-year and draw its name. A miss passes index12; established tail input or
stop is required. Set figureTop307, moving rectangle `(171,307,468,480)` and
move by0; return 1.

Draw-elimination captures show-year before calls, restores saved text and builds
defeated name. When captured true append colon, space, label 455, space, formatted
year of the stored signed eliminated turn. Draw shadow33 with rectangle offset
(1,1), foreground 16 after restoring origin. With year, copy/present text and
clear show-year. Always copy text once more, including the duplicate year copy.
History names require known terminators; unknown tail input is not an empty name.

Load-portrait constructs temporary image/palette, restores old portrait area,
loads resource `220+defeated leader variant`, copies palette colors42..105,
assigns palette, extracts `(1,1,225,275)` with transparent 9 and draws at(208,57).
Reset portrait rectangle and destroy palette then image. Ignored image failure
does not invent palette bytes.

Move-figure(shift) copies moving rectangle; negative shift increases exposed top
by −shift, otherwise increases bottom by shift. Restore that exposed region,
draw portrait at(208,57); for positive shift additionally restore portrait area
from figureTop to figureTop+shift. Draw moving figure at(171,figureTop+shift),
offset moving rectangle by shift, clamp exposed top to0 when≤0, present exposed,
then add shift to **live** figureTop. Raise repeats pump then move(−4) while
figureTop>−173, with no fixed delay. Lower restores portrait, resets audio,
plays111 mode 1, captures clock, then while figureTop<307 moves(+15) and pumps,
samples, polls until signed elapsed≥22 ms; retain the pre-poll timestamp.

Show constructs a caption font, shows outer/flush, assigns palette, shows inner/
flush and captures. For each matching history row0..11: draw name, set show-year 1,
load portrait, capture start, request `83+((leaderVariant+unsigned mapSeed)&7)`
without RNG, raise, wait 60, then pump/sample/poll until signed elapsed≥7000 ms
from start, draw name+year, lower. After all rows wait 60, fill inner0, reveal
full640×480 with block4/mode 4. Load10002; set main font/style(124,129,1,1).
Gather `SLAM` lines with spaces until `@`; if present, center at(0,72,width 640).
Leave this shared file open. Reveal full area4/3, draw label 456 using font(0,16,3)
centered at y=476−font height, present all. Bind click and keys208..210 to modal
clear, optionally network idle, run modal, clear idle, release, hide inner/flush,
restore outer initializer palette, hide outer/flush; destroy caption font.
Normal wrapper destroys even after failed preparation: close module, clear
singleton, destroy font, portrait, figure, module, port, inner, palette, outer.

#### Original §19. Conformance cases

Capture ordered file/device/codec calls, controlled media/animation choices,
clock reads, media flags/queues, callbacks, pixel/palette changes and cleanup.
Providers must expose failed/partial output and reentrant state changes explicitly.
Useful cases include:

- Disabled sound versus negative cancellation; CHEERS followed by missing file;
  five active streams; initialization failure swallowed by queue; loop/nonloop.
- Mono/stereo duplication or discard; positive/negative saturation; odd frame
  sizes; narrowed zero count; undefined first sample; ended-copy retained extent.
- Submit failure33 versus completion failure33; two ended callback streams;
  inactive prefix; reentrant head replacement; failed submission retried.
- AVI EOF on partial/full stride, both refill entries, mismatched buffer13,
  reset preserving AVI, prepared range negative after wrap and clock type changes.
- CD special/generic discs, retry limit, repeated track draw, previous track 1,
  narrowed playback versus full retained track, failed Set and notification collision.
- Video missing codec returning0; stale read capacity during display setup;
  seek callbacks, strict last-frame test, stalled preroll, keyframe distance tie,
  audio-ended looping, repeated double-size and shared-context replacement.
- Reveal modes1..5 plus zeroed unknown mode; singleton range draws, residual
  pixels and last-entry replacement after callback mutation.
- Movement destination-only entry dropped at frame 0, negative sound, aircraft
  delay/count difference, view-origin change; eight-frame network interruption;
  nuclear sound disabled versus missing file with preference enabled.
- Opening key versus click, missing-media dangling publication; launch open-error
  retained allocation; wonder module absent versus image-load failure; herald
  idle boundary and leader 18 response overlap.
- Stars at exact boundaries, collision colors and double replacement draws;
  credits cache exhaustion, partial/full redraw and rowHeight/2 truncation.
- All ending resource failures, missing pages, year-caption duplicate copy,
  arrival numeric fallback versus movie string slot 3, live singleton replacement,
  undefined advance 100/history12 and safe long-caption handling.

These are acceptance requirements for future implementation fixtures. Appendix C
separately records the finite checks actually performed for this specification.

#### Original audio/video state inventory

Logical audio state consists of a device, initialized flag, remembered window/
byte budget/buffer count, output format, an active buffer ring, spare buffers,
a singly linked stream queue and active-stream count. Each output buffer has
sample storage, a header and flags named Spare, End, SourceReady, EndSubmitted,
Queued and Mixed, plus its original ordinal. Stream state includes file/memory
handle, optional looping-file handle, format, chunk size/data offset, buffer
base/cursor/end/capacity, progress, effect identity, kind, next and completion
callback. Stream flags are Ended, Inactive, Looping, NeedsRefill and Started.
AVI adds sample stream, range base/progress, per-output-buffer stride, next
buffer, partial-stride progress and an optional audio-control reference.
Original encodings and widths belong in the appendix.
