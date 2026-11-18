# Foundation build matrix

The active matrix is [.NET CI](../.github/workflows/ci.yml), with independent
[repository checks](../.github/workflows/repository.yml). See the executed
[validation receipt](validation/README.md) and [build instructions](BUILDING.md).

| Target | Required checks |
| --- | --- |
| Linux x64, macOS ARM64, Windows x64 | Locked restores; Release core/CLI tests; independent CLI/helper drivers; 57 X02 cases; optional Lua/SQLite; desktop build/publish; formatting and source identities; extracted self-contained CLI packages |
| Chromium WebAssembly | 432 portable groups, 57 X02 cases, thirteen Lua groups, actual application pointer/keyboard input and native/browser result comparison |
| Linux X11 | Actual application pointer input and normal window close; Lua sanitizer harness; OS signal/output handling |
| Windows/macOS desktop | Actual input and shutdown, OS interruption/output behavior and target-hardware performance; pending manual/native evidence |

A desktop build does not establish native UI behavior. Additional RIDs,
release signing/installers and full M2 window/menu/accessibility checks require
their own evidence. Commands and remaining gates are in [platform acceptance](testing/platforms.md).
