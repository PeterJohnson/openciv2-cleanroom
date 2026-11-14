# F04 calendar and F05 acceleration labels

`turn/calendar.hpp` implements F04 section 5 using the selected decoded
`CalendarValues`. It reads the four six-period profiles from the existing
gameplay JSON model; the game library has no JSON parser or implicit default
bundle. Both the shipped classic bundle and explicitly authored custom bundles
reach the same calculation. The caller owns content selection and admission.

The operation selects a profile from difficulty and retained Small/Large flags,
including both adjustments when both flags are set. It preserves turns 0/1,
strict period comparisons, one-unit progression after the last period, ordinary
year-zero substitution, scenario override and the first-launch supplement.
Significant arithmetic uses explicit signed-32 modular operations. The result
is not narrowed to a saved year. Turn/year, scenario values and random contexts
are not modified by this calculation. Map dimensions are not an input.

`session/setup_calendar.hpp` supplies F05's acceleration labels. It sets the
first-launch marker to Absent, invokes the real calendar at turns 0, 21 and 41,
and returns their magnitudes. Incoming scenario/calendar settings survive.
Magnitudes use an unsigned domain so custom calendars can display the magnitude
of the most negative signed-32 year. Active sessions publish at completion;
the helper also works before client admission. The outer wizard still owns
choice handling, retries and the entry-time acceleration local.

## Verification

Fourteen portable cases cover 162 assertions: all difficulty/classification
combinations, every ordinary period transition, negative and wrapped turns,
first-launch boundary/zero ordering, scenario gates and signed increments,
actual custom JSON decoding with fresh source hashes, large authored operands,
retained state, both acceleration label profiles and client publication without
random draws. These expectations come from the specifications. The JSON tests
use the pinned jart/json.cpp integration and complete admitted gameplay bundles.

Windows MSVC passes all 508 default-off and 510 SQLite-enabled CTests. The
pinned WebAssembly build passes all six CTests, including 485 portable cases
with 190,820 assertions. All 54 registered conformance observations match
between native and WebAssembly. Local validation logs are retained under
`build/calendar-*-final-build.log` and `build/calendar-*-final-ctest.log`.

The [original Windows observation](m1-original-calendar-observations.md)
independently confirms the displayed acceleration years for Prince on Large
and Small maps. Its limited UI evidence is separate from the specification
tests; original screenshots and private executable state are not test inputs.

This completes the F04 calendar consumer of the gameplay component and its F05
label helper. The [player/rules wizard](m1-player-setup.md) now composes the label helper.
V08 retirement consumers, outer new-game composition,
global-turn progression, display/date conventions, content/session/checkpoint
admission and original-peer profile gates remain required work. The calendar
helper does not implement those larger operations or complete M1.
