# Windows development and acceptance

Use the root [build guide](BUILDING.md) for .NET builds, tests and the Avalonia
application. Optional Lua/SQLite need a Visual Studio C compiler environment.
The [platform checklist](testing/platforms.md) describes outstanding real-desktop,
OS interruption and performance checks. Hosted Windows x64 CI evidence is recorded
in [validation](validation/README.md).

Continue implementation in the C# projects listed in the [source guide](code-guide.md)
and follow the next [milestone](implementation-plan.md). Classic file/wire values
remain explicitly encoded; runtime record layout is not a compatibility surface.
