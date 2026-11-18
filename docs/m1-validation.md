# Foundation validation

The bounded C# foundations are implemented and tested on native .NET and Chromium
WebAssembly. [Current validation](validation/README.md) records the executed
revision and platform limitations. [Test instructions](testing/README.md) give
reproducible commands; [coverage](testing/coverage.md) maps APIs to cases.

## Current closeout status

The implemented contract includes 432 portable groups, twenty native I/O tests,
two API boundary tests, fifteen registered X02 operations with 57 selected fixtures,
eight independent CLI/helper suites, and optional Lua/SQLite suites. The bounded
Avalonia host exercises production session commands. These denominators describe
different surfaces and are not a whole-game percentage.

## Deferred issues and current completion decision

M2 rendering, menus, storage, DPI/IME/accessibility and complete native desktop
acceptance remain open. M3–M7 own playable gameplay, full AI/event/turn processing,
expanded automation, original-peer networking and release packaging. Continue
from the [implementation plan](implementation-plan.md), preserving the
[finite foundation contract](m1-closeout.md).

## Compatibility evidence

Authored vectors, runtime regression cases, hosted CI and independent observations
of the original game are distinct evidence. Rebuild and rerun after source or
contract changes. Historical original-game observations do not establish that a
new binary interoperates; repeat the owning file/peer procedure for such a claim.
