# Development workflow

Follow the cleanroom boundary in [docs/architecture.md](docs/architecture.md).
Implement gameplay from the repository's specifications; do not consult or
derive game code from OpenCiv2 or Civ2 reconstruction/decompilation sources.
Original game executables and reconstructed game source must not be copied into
this repository or its build. Other material in those repositories, including
tools, capture drivers, build helpers, tests and documentation, is reusable;
exclude any embedded original or reconstructed game implementation. Review reused
tools' assumptions and preserve provenance. Historical results retain their
recorded scope; reconstruction-generated snapshots are not independent original-game
observations. Do not use reconstructed game code to fill a missing algorithm
detail; record a specification gap and resolve it through the behavioral contract
or independent original-game observations. Required file/wire/data definitions
in the specs remain implementation inputs even when documented in an appendix.

Before committing C# changes:

1. Run `dotnet format OpenCiv2.slnx --no-restore` from the repository root,
   plus the affected optional solution under `solutions/` when applicable.
2. Review the diff and stage only intended changes; preserve partial staging.
3. Run formatting verification and the checks appropriate to the affected
   projects. See [docs/BUILDING.md](docs/BUILDING.md) and
   [docs/testing/README.md](docs/testing/README.md).

For Lua-CSharp automation changes, run the shared native/browser task and resource
tests. Do not format dependencies or generated build files.

Current project ownership is in [docs/code-guide.md](docs/code-guide.md).
Continue work from [docs/implementation-plan.md](docs/implementation-plan.md);
implemented foundation contracts do not imply the full specification is complete.
