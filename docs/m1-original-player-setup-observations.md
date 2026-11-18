# Original player-setup observations

On 2026-11-14 the supplied original MGE executable ran through its normal Windows
UI in `build/original-setup-runtime`. The
[dated capture record](https://github.com/PeterJohnson/openciv2-cleanroom/blob/6e58a59e841760e7d3a5706e727d4bb8090bb224/docs/conformance/reports/original-player-setup-windows.json) records executable,
data, screenshot and preference-file identities. The runtime inherits the earlier
declared rule/text name probes; it is not a pristine installation. No
reconstruction source, process-memory inspection, injected calls or historical
harness was used. Three independent process runs used Small, King, three
civilizations, Raging Hordes, Female and Egyptians. Each created an unsaved game
and was closed normally after the observations.

## Advanced Cancel and custom names

Choose Customize Rules, check Accelerated Startup and retain Bloodlust checked.
Click Cancel on the advanced dialog. Select Starting Year still opens, establishing
that the resulting checks are processed despite advanced-modal cancellation.
Choose Normal Start, then Female, Egyptians and Custom. The defaults are
Cleopatra, Egyptians and Egyptian. Append `-Probe` to the leader name, then
open Titles. The seven defaults are Ms., Pharaoh, Great Pharaoh, Comrade,
High Priestess, Consul and President.

Append `Discard` to the first title and cancel. The name dialog retains
`Cleopatra-Probe`. Reopening Titles shows `Ms.` again, so the canceled title edit
was discarded. Cancel Titles, accept the names and Far East Pavilion city style.
The introduction uses `Cleopatra-Probe`. A copy of the 516-byte CIV2.DAT taken
at the introduction records difficulty 3, tribe 3, gender 1, opponents row 4,
barbarians 3, custom-rules choice 1 and acceleration 0. The resulting map starts
at 4000 B.C. No game file was saved.

## Canceled acceleration followed by Standard

With the original closed, author only CIV2.DAT's signed-16 acceleration field
at offset 70 to value 2. Launch again and retain the common choices and Customize
Rules. Accelerated Startup is checked, and Select Starting Year initially selects
the third row, 2000 B.C. Cancel that dialog, then select Standard Rules and finish
with ordinary Cleopatra and Bronze Age Monolith.

The introduction lists many technologies. CIV2.DAT at that point retains advanced
rule preference `0x00bf` and acceleration preference 2, while rules choice is now
0. After accepting the introduction, status shows 4950 B.C., 280,000 People and
-18 Gold alongside a research dialog. This observable whole-entry result supports
F05's retained acceleration-local contract; it does not directly measure the
private local or establish the full acceleration algorithm. The default research,
upkeep and population notices were dismissed before quitting; no turn was
manually ended and no game was saved.

## Explicitly clearing acceleration

With the second process closed, author only rules-choice offset 68 to value 1,
leaving acceleration 2. Relaunch, retain the common choices, open Customize Rules
and uncheck Accelerated Startup. Accepting advanced rules goes directly to Gender.
Finish with ordinary Cleopatra and Bronze Age Monolith. CIV2.DAT at the introduction
now has acceleration 0; status after the introduction shows 4000 B.C., 10,000 People
and 0 Gold. Thus explicit clearing differs from canceling the year dialog and
then selecting Standard Rules.

These runs establish selected visible choices, dialog transitions and persisted
preferences. They do not measure random sampling, rejected roster draws, opponent
overrides, private publication timing, scenario calendars, invalid backing or all
name-edit variants. Those tests retain specification provenance. The report lists
both deliberately authored preference seeds and all before/after files, separate
from original-produced outputs.

The linked capture metadata is preserved at its recorded revision. It does not
validate the current C# exporter; repeat the procedure with current binaries and
record fresh source, executable and file identities for that claim.
