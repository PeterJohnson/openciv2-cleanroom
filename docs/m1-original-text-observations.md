# M1 original text and save observations

These dated observations describe the original executable and its UI. They are
not a fresh interoperability run of the current C# implementation. Use the
current C# helper commands below and capture new input/output/executable hashes
before making that claim; see [validation](validation/README.md).

On 2026-11-13 Pacific, the original MGE executable accepted two byte-preserving
exported saves on Windows. It resumed the first, founded a city, processed a turn
and saved again. The codec reproduced that second save exactly, and the
original reopened it with the city present. The
[dated capture record](https://github.com/PeterJohnson/openciv2-cleanroom/blob/6e58a59e841760e7d3a5706e727d4bb8090bb224/docs/conformance/reports/original-text-and-save-windows.json)
records executable/input/output/source hashes, build identity, actions and local
capture identities. This is independent original-program evidence, outside X02.
This historical observation predates the [completed M1 audit](m1-validation.md).

## Setup and reproduction

The executable SHA-256 is
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
The user-supplied installation at `C:\Projects\civ2\Civ2` was copied through the
earlier isolated peer runtime into `build/original-text-runtime`. Launch that
copy with its own directory as the working directory. The initial UI launcher
attempt exposed no game window; an explicit executable launch with that working
directory succeeded. Subsequent input used the original's normal UI. No code
patching, injection, process-memory reads or reconstruction harness was used.

Create a large 150×120 world, Warlord difficulty, seven civilizations, Roving
Bands and Standard Rules. Select the English tribe (rule slot 12), retain Henry
VIII and the English adjective, and set the custom civilization display name
to `ProbeÉ`. Save before founding the first human city as `nameprobe_base.sav`.

Back up `CITY.TXT` and prepend the following independently authored text. Encode
it as Windows-1252 with CRLF, including a blank line before the original tail.
The report retains the exact prefix bytes. É is byte C9 and é is byte E9.

```text
@probeé
Folded
@STOP
@pRoBeÉ
AsciiOnly
@STOP
@ProbeÉ
ExactOnly
@STOP

```

In this run, the original CITY tail was appended only after new-game setup and
before the first human founding attempt. Two AI cities consequently retained
empty names. The save exchange is useful interoperability evidence, but this
world is not a nominal stock-city-creation reference.

With the rules plural still `English`, Build New City proposed `London`.
Cancel. Back up `RULES.TXT`, then change only field 5 (zero-based comma field)
of `@LEADERS` row 12 from `English` to `ProbeÉ`, retaining its leading space.
Reload the baseline save. Build New City now proposes `AsciiOnly`; cancel again.
F03 §7 defines this field and C01's **Default city naming** section specifies
the pooled plural lookup separately from the civilization's display name.

## Observations and comparison limit

The selected header was `@pRoBeÉ`, the second of the three authored headers.
The proposal repeated after loading the exported save. This is consistent with
ASCII case equivalence and a distinction between É/é in this lookup, but it
does **not** establish either a complete comparison table or an isolated
accented-pair result. F03 §2 distinguishes ordinary unique-header input from
duplicate equivalent headers and retained section-index behavior. Those can
affect selection in this ordered input.

The next comparison needs separate single-candidate files, positive/negative
controls and fresh lookup state, followed by other accented pairs, nonletters
and explicit locale observations. The F03/C01 non-ASCII comparison gap remains
open. Do not turn this result into a Unicode-folding or host-locale algorithm.
No new comparison algorithm is implemented by this change.

## Stored-save exchange

Run the native driver, supplying a new destination:

```powershell
tests/OpenCiv2.SaveRoundTrip/bin/Release/net10.0/OpenCiv2.SaveRoundTrip.exe build/original-text-runtime/nameprobe_base.sav build/original-text-runtime/nameprobe_cpp.sav
```

The original loaded `nameprobe_cpp.sav` and displayed Warlord/4000 B.C. Build
New City again proposed `AsciiOnly`; accepting it produced the founding notice.
After the city view/tutorials and acceptance of the offered Code of Laws
research, the original processed turns to 3980 B.C. Save as
`nameprobe_resumed.sav`, export it to `nameprobe_resumed_cpp.sav`, and reopen
that export in the original. The map retained `AsciiOnly` at 3980 B.C.

Both exports equal their respective original inputs byte for byte. The baseline
has eight unit slots and two city slots; the resumed save has seven of each.
Stored city slot 2 has owner 6, size 1, token 9 and name `AsciiOnly`. Stored
globals report turn 2, year −3980 and multiplayer mode 0. The first export and
the original input both rejected reuse as output paths and remained unchanged.
The linked capture metadata is preserved at its recorded revision. It does not
validate the current C# exporter; repeat the procedure with current binaries and
record fresh source, executable and file identities for that claim.
