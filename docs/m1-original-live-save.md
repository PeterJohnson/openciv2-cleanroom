# M1 original live-save exchange

These dated observations describe the original executable and its UI. They are
not a fresh interoperability run of the current C# implementation. Use the
current C# helper commands below and capture new input/output/executable hashes
before making that claim; see [validation](validation/README.md).

## Driver and reproduction

Build with native tools and tests enabled. `OpenCiv2.LiveSave` uses the real native
binary/text/directory services, the F03 reload wrapper and F06 ordered core
load/save. Its JSON report uses the System.Text.Json serializer. Invoke it
with an original game data directory and a new output filename:

```powershell
tests/OpenCiv2.LiveSave/bin/Release/net10.0/OpenCiv2.LiveSave.exe build/original-text-runtime build/original-text-runtime/nameprobe_resumed.sav build/original-text-runtime/live_cpp_gold777.sav --gold 6 777
```

The driver admits current `0x2c`, mode-zero stored input. The optional `--gold`
argument directly edits one loaded civilization's signed 32-bit gold field;
this is a declared test-fixture edit, not a gameplay command. The report separates
the loaded values, edit and saved values. Input and selected text hashes must
remain unchanged. Saving must leave both random contexts unchanged.

The output must end in `.sav` or `.SAV` and must not exist. After a successful
load, the driver atomically claims that exact destination before passing it to
the ordinary F06 replacement writer. A subsequent failure retains the owned
partial output. This driver policy does not alter the core save operation's
replacement/partial-write behavior. Four CLI integration cases cover an authored
load/edit/save/reload, token renewal, source preservation, existing destinations,
malformed input, invalid edits and output-path admission.

Headless caller inputs are explicit: runtime ID 75, independent gameplay and
presentation seeds 10/20, process token counters 100/200, screen rectangle
`0,0,1280,800`, ASCII comparison and uncached text search. Sprite and flush
callbacks count invocations. They do not load graphical resources. Each observed
load consumed 255 gameplay draws, invoked the sprite callback once and flushed
eight times. The driver does not perform F06's post-load wrapper/admission.

## Original-program observations

Use the isolated original executable and the existing data setup described in
[the text/save observation](m1-original-text-observations.md). The executable
SHA-256 is
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Launch it with `build/original-text-runtime` as its working directory. The
modified tribe plural `ProbeÉ` and authored CITY prefix remain in place; this
run makes no new case-comparison claim. The starting save is the previously
captured `nameprobe_resumed.sav` at turn 2, 3980 B.C., player/local civilization 6.

1. Export that save with civilization 6's gold changed from 1 to 777.
2. Load `live_cpp_gold777.sav` in the original. The load notice shows Warlord,
   Henry VIII of the ProbeÉ, 3980 B.C.; the map shows `AsciiOnly`, 10,000 people
   and 777 gold. Save as `live_original_777.sav`.
3. Dismiss the save notice and press Enter at the map's End of Turn prompt.
   The original advances to 3960 B.C. and 778 gold. Save as
   `live_original_turn3.sav`.
4. Run the driver on that resave without `--gold`, writing `live_cpp_turn3.sav`.
   The recorded loaded summary reports turn 3, year -3960 and civilization 6 gold 778.
5. Load `live_cpp_turn3.sav` in the original. The load notice and map show
   3960 B.C., `AsciiOnly`, 10,000 people and 778 gold.

Both exported saves differ from their original inputs, as expected from live
loading/saving and the declared first edit. All inputs and selected text files
remain unchanged by the driver. The first original resave also loads in the recorded tool
with gold 777; re-exporting it produces the same bytes as the first edited
export. After adding the explicit-extension guard, a repeat export of the
starting save with the final driver reproduces the first edited output exactly.
The final driver produced the turn-3 export actually reopened in step 5.

## Evidence limits

This run covers one current single-player world and one direct treasury edit.
The original executes turn processing; the cleanroom driver does not. Two AI
cities in the starting world have empty names from the earlier CITY setup, so
this is not a nominal stock-city-creation reference. No executable patching,
injection, process-memory inspection or reconstruction harness is used.