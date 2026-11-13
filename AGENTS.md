# Development workflow

Follow the cleanroom boundary in [docs/architecture.md](docs/architecture.md).
Implement gameplay from the repository's specifications; do not consult or
derive code from OpenCiv2 or Civ2 reconstruction/decompilation sources.
External source links, filenames and source-derived harnesses retained in
validation/provenance sections are historical research records, not implementation
or test inputs. Do not follow them to fill a missing algorithm detail; record a
specification gap and resolve it through the behavioral contract or independent
original-game observations. Required file/wire/data definitions in the specs
remain implementation inputs even when documented in an appendix.

Before committing C/C++/Objective-C changes:

1. Run `python3 tools/format.py` with the clang-format version pinned in
   `dependencies.json` (use `python` on Windows).
2. Review the formatted diff and stage only the intended changes. The formatter
   never stages files; preserve any intentional partial staging.
3. Run `python3 tools/format.py --staged` and the checks appropriate to the change.

Enable the repository hook with `git config --local core.hooksPath .githooks`
when no other hook configuration needs preserving. The hook checks staged
contents and fails on formatting errors; fix them before committing.
See [docs/BUILDING.md](docs/BUILDING.md#formatting-and-commits) for setup, commands
and the CI check. Do not format dependencies or generated build files.
