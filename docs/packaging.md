# CLI packaging

The base `oc2` and optional `oc2-sqlite` are self-contained .NET 11 native
command-line packages. They implement the finite M1 inspection/content contract;
they are not a playable game. Game files and content packs are separate inputs.
The default CLI graph contains no Avalonia, Lua or SQLite dependencies.

## Build and check on the target host

Install the exact SDK in `global.json`, Python 3.12 or newer, and the
operating-system prerequisites for .NET 11. A self-contained package carries the
.NET runtime, but still requires its native OS libraries (including ICU on Linux).
For SQLite, also use a native C compiler: a Visual Studio Developer Command Prompt
on Windows, Xcode command-line tools on macOS, or `cc` on Linux. Packages are
unsigned; signing/notarization and installers are separate release work.

From the repository root:

```sh
python tools/dotnet/package_cli.py
python tools/dotnet/build_sqlite.py
python tools/dotnet/package_cli.py --sqlite
```

Use `python3` where appropriate. `--dotnet /absolute/path/to/dotnet` selects a
non-PATH SDK. `NUGET_PACKAGES` and `DOTNET_CLI_HOME` may select isolated writable
caches. `--output` selects an empty output location; existing archives/checksums
are never overwritten. Run each build sequentially because project restore
assets are shared. A subsequent ordinary development build should first run its
ordinary locked restore again.

The packager detects the native RID; `--rid` can assert it but cannot cross-build.
Checked-in restore graphs and runtime package hashes cover `linux-x64`,
`linux-arm64`, `osx-x64`, `osx-arm64`, `win-x64`, and `win-arm64`. This is a list of
packaging inputs, **not evidence of execution on all six targets**. Linux here
means glibc, not Alpine/musl. [Hosted CI](validation/README.md) has executed both packages
on `linux-x64`, `osx-arm64` and `win-x64`; `linux-arm64`, `osx-x64` and `win-arm64`
still require actual native execution.

Packages appear under `build/packages/`. Windows uses ZIP; Linux/macOS use
tar.gz. For example, on Linux x64:

```sh
python tools/dotnet/test_package.py build/packages/oc2-linux-x64.tar.gz --report build/test-results/package-base.json
python tools/dotnet/test_package.py build/packages/oc2-sqlite-linux-x64.tar.gz --report build/test-results/package-sqlite.json
```

Use the corresponding RID and `.zip` on Windows. Keep both JSON reports and the
archives/checksums as acceptance artifacts. The test extracts the actual archive
under a Unicode/spaced path, checks every manifest hash, points runtime discovery
at empty directories, and requires the .NET host trace to identify the extracted
CoreCLR. It runs the existing inspection/content/pack/rules suites, plus SQLite
export checks when enabled. The test-only stored round-trip executable is not
shipped; its standalone test remains in normal native acceptance. Missing and
modified package files must fail manifest validation. This proves bundled-runtime
execution on the test host, not operation on a machine lacking required OS libraries.

## Inputs, identity and notices

`packaging/locks/<rid>/` is separate from ordinary development locks.
Packaging restores in locked mode and includes the selected lock in compiled
assembly identities. Implicit framework downloads are not represented in NuGet's
project locks; `packaging/runtime-packs.json` pins their version and actual
NuGet archive SHA-512. The packager checks it and checks shipped runtime bytes
against that archive. SDK selection is exact, with roll-forward disabled.

Each archive contains:

- `PACKAGE.json`: toolchain, RID, optional native build receipt, pre/post-verified
  source inventory and SHA-256 of every other shipped file.
- `LICENSE.txt`: OpenCiv2's MIT terms.
- `notices/dotnet-LICENSE.txt` and `notices/dotnet-THIRD-PARTY-NOTICES.txt`:
  verbatim files from that exact runtime package, covering its managed/native
  runtime components.
- `notices/RandN-LICENSE.txt`: the MIT license from
  [RandN's v0.5.0 release](https://github.com/ociaw/RandN/blob/v0.5.0/LICENSE.txt).
  The two shipped packages are `RandN.Core` and `RandN.Pcg32` 0.5.0; their package
  content hashes are in the shipped `notices/packages.lock.json`.
- SQLite packages additionally contain the pinned native library,
  `oc2-sqlite-build.json` and `SQLITE-NOTICE.txt`. SQLite is public domain; its
  source archive hash, compiler/configuration and produced library hash are in
  the receipt. Lua, Avalonia and Skia are not part of these CLI packages.

The vendored RandN license SHA-256 is
`b0072c83082f674e9b33b272f5447e3882833f08a35ba45ca61b946401c00924`.
NuGet identifies its source through an Hg commit; the GitHub release tag is used
only as the license source, not asserted to have the same Git commit identity.

Archive ordering, ownership, modes and timestamps are normalized. This makes
archive metadata stable; byte-identical builds across native compilers/hosts are
not claimed. Native receipts retain exact compiler/path details. SHA-256 sidecars
identify the actual archive; they are integrity checks, not release signatures.

To intentionally update packaging locks, restore the SQLite CLI project (its
references also cover the base graph) for each RID with
`-p:Oc2PackageRid=<rid> -p:RestoreLockedMode=false --force-evaluate`. Review the
changed graphs, regenerate the runtime archive SHA-512 pins from the pinned
NuGet packages, and rerun packaging and native acceptance. Normal packaging never
updates either pin set.


## Notices for every published application

`packaging/Notices.targets` runs the Python notice collector after CLI,
Avalonia desktop/browser and executable browser test publications. Ordinary
restore/build/test does not invoke it. Publish requires Python 3 (`Oc2Python`
can override the executable). Every distribution includes `DEPENDENCIES.json`
and `DEPENDENCY-NOTICES/`; keep both with the application, including the whole
browser `wwwroot` directory when hosting. They do not load as startup assets.

The inventory covers the complete locked restored graph, including build-only
packages and unselected platform assets; it is deliberately not a claim about
which assemblies survived trimming. It records package versions, content and
archive hashes, source revisions, declared licenses, full notice file hashes,
SDK/runtime identity and optional native notices. The collector rejects missing
licenses, changed vendored attribution and lock mismatches. It reads package
notices directly from verified archives. Signed archives can have a raw SHA-512
different from NuGet's content hash, as described by the
[NuGet verification command](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-verify).
`packaging/package-archives.json` binds the exact reviewed archive bytes to the
locked content hash. Both hashes must match; a changed archive or lock is rejected.
This makes repeated publication independent of platform certificate APIs and
records the pin file's hash in `DEPENDENCIES.json`.

When adding or updating a signed archive pin, use the pinned SDK on Linux or
Windows to run `dotnet nuget verify PATH.nupkg --all --verbosity normal`. Require
successful signature/integrity verification and the exact content hash from the
project lock, then record the base64 SHA-512 of that same archive. Review both
values before committing. Never copy a cached `.sha512` sidecar as proof of the
archive bytes, and never change a pin just to accept a failed verification.
Unrecorded archives use the SDK verifier directly with offline revocation lookup;
macOS's verification limitations make reviewed pins necessary for reliable
publication there. Neither path asserts current online revocation status.

Avalonia, MicroCom, Tmds.DBus, BuildServices and RandN packages omit full license
text. Checked-in supplements under `packaging/notices/` record exact
upstream source URLs and hashes in `sources.json`; changed versions/commits need
an attribution review. Avalonia's own third-party notices are included. The
Inter font embedded in both net8.0 and net10.0 assemblies identifies version
3.019, git `0a5106e0b`, and SIL OFL 1.1; its separate font license is included
alongside the package wrapper's MIT license. Skia/HarfBuzz/ANGLE notices come
from their package archives. Browser Mono and self-contained native runtime
notices come from separately pinned runtime archives. Framework-dependent desktop
publications include SDK host notices and record that CoreCLR is not distributed.
Lua browser publications must contain the Lua notice or publication fails.

Opt-in desktop Native AOT publication uses a separate desktop lock and
`packaging/aot-runtime-packs.json`. Its runtime inventory is labeled `native-aot`
and its full runtime notices come from the pinned .NET Native AOT runtime archive,
rather than the CoreCLR distribution. The AOT compiler package is included in the
locked dependency inventory. To update a desktop AOT lock deliberately, restore
the desktop project with `-p:Oc2DesktopAot=true -p:Oc2DesktopAotRid=<rid>
-p:RestoreLockedMode=false --force-evaluate`. Update the runtime archive SHA-512
pins and reviewed signed-package pins with the same verification procedure above,
then republish and execute the application-input checks on the target host.

Local Release publication checks passed for Avalonia desktop (28 locked packages),
Avalonia browser (23) and Lua/WASM (4), including real signed-package hash checks.
Nine regression groups cover changed/missing notices, lock mismatches, license
expressions without full text, stale source attribution and failed/mismatched
signature verification, plus exact archive pins, tampered bytes and silent
verifier failures, plus Native AOT runtime pins and invalid publication modes.
The [executed CI receipt](validation/README.md) records
the tested CLI archives and platform gates.
