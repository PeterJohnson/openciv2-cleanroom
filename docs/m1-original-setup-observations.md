# Original world-setup observations

The original MGE executable was exercised through its normal Windows UI in an
isolated `build/original-setup-runtime` copy. The
[dated capture record](https://github.com/PeterJohnson/openciv2-cleanroom/blob/6e58a59e841760e7d3a5706e727d4bb8090bb224/docs/conformance/reports/original-setup-windows.json) records the
executable/data hashes, three exact preference-file identities, actions and
22 local screenshot identities. The original window was closed after the
comparison. No source, process-memory access or reconstruction harness was used.

## Observed behavior

1. Customize World initially selected the remembered Large preset. Custom
   displayed 50×80. Cancel returned to the preset menu; reentering Custom again
   displayed 50×80.
2. Submitting 19×80 through the OK button repeated Custom with 50×80. Submitting
   65586×80 was accepted and advanced to Land Mass, consistent with F05's signed
   16-bit reduction before validation. No world was generated to inspect its
   resulting dimensions independently.
3. Parameter dialogs appeared as Land Mass, Land Form, Climate, Temperature,
   Age. Starting preferences in F06's L,F,T,C,A order were `[0,0,0,-1,1]`;
   displayed defaults were Normal, Varied, Arid, Temperate, 5 billion.
4. Accepting Large, Archipelago, Wet, Cool, 4 billion produced saved values
   `[1,-1,-1,1,0]` upon reaching Difficulty. The remembered map size remained 2
   (Large) after the custom-size acceptance.
5. Canceling Difficulty and reentering Customize displayed those same remembered
   choices. Selecting Small and then Random at all five parameter dialogs left
   the five preferences unchanged. The entire 516-byte file differed from the
   previous snapshot only at byte 22: map-size preference 2 became 0.

These observations resolve the normalized preference representation and repeated
custom-input defaults in F05 section 3. The parameter tests cite these explicit
outcomes; they do not read original payloads or screenshots as test inputs.

## Reproduction and limits

Launch the supplied executable with the isolated installation as its working
directory. Use Customize World and the sequences above. At each Difficulty
screen, read CIV2.DAT using F06's signed little-endian map-size field at `0x16`
and five signed little-endian parameter fields at `0x1a`. Read the file before
canceling Difficulty, since menu reentry reloads preferences.

The runtime copy inherits the earlier authored name probes in RULES.TXT/CITY.TXT;
their exact identities are in the report. These observations do not exercise
tribe selection, city creation or generation. No original random outcomes or
draw counts, private partial writes, canceled-size dimensions or write failures
were measured. Those implementation tests retain specification provenance.

The local capture named `04-custom-invalid1950-retry.png` has a misleading
historical name: 1950 was edited but never submitted. It is not rejection/retry
evidence. Capture 08 follows the actual OK submission of 19×80. Return/Escape
while the width field had focus did not submit/cancel in this run; the dialog's
visible buttons were used for the successful actions.

The linked capture metadata is preserved at its recorded revision. It does not
validate the current C# exporter; repeat the procedure with current binaries and
record fresh source, executable and file identities for that claim.
