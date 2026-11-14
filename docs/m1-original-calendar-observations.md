# Original acceleration-year observations

On 2026-11-14 the supplied original MGE executable ran through its normal Windows
UI in the isolated `build/original-setup-runtime` directory. The
[report](conformance/reports/original-calendar-windows.json) records executable
and data hashes, the exact observed strings and nine retained screenshot
identities. No reconstruction source, injected calls, process-memory inspection
or historical harness was used. The original was closed after both comparisons.

Through Start a New Game, select Large, Prince, seven civilizations, Roving
Bands, Customize Rules, and Accelerated Startup. Leave the previously checked
Bloodlust option and other unchecked options as displayed. The Select Starting
Year dialog shows **4000, 3600 and 3200 B.C.** Canceling returns to Rules with
Customize Rules selected; canceling Rules returns to the main menu.

Repeat through Start a New Game with Small and Prince, retaining the same other
choices. Select Starting Year now shows **4000, 3000 and 2000 B.C.** Its Cancel
again returns to Rules. Both runs were canceled before gender/tribe selection,
world generation or a game save.

These displayed magnitudes match F04's specified profile selection and F05's
calendar labels at turns 0, 21 and 41. The private turn arguments, first-launch
write and retained cancellation locals were not inspected. This is not evidence
for scenario calendars, spaceship launch effects, later period boundaries,
signed wrap or custom JSON operands. Those implementation tests retain
specification provenance. The runtime rule/text files inherit the earlier
declared name probes; their exact identities are in the report.
