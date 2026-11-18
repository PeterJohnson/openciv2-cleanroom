# Tests and evidence

Run from the repository root after the [build prerequisites](../BUILDING.md).
Shared behavior lives in `tests/OpenCiv2.FoundationCases`, executed by native xUnit
and the browser host. Native I/O/API boundary tests are in `OpenCiv2.Base.Tests`.
Optional Lua/SQLite and the X02 adapter have separate test hosts. See
[implementation coverage](coverage.md) for admitted APIs and exclusions.

## Independent CLI/helper drivers

After the root Release build, run the following. Append `.exe` to executable
paths on Windows; use `python` in place of `python3`. Set `DOTNET_ROOT` if apphosts
need a nonstandard SDK installation. SQLite commands require its optional build.

```sh
python3 tests/inspection_cli.py src/OpenCiv2.Cli/bin/Release/net11.0/oc2 tests/OpenCiv2.SaveRoundTrip/bin/Release/net11.0/OpenCiv2.SaveRoundTrip
python3 tests/content_cli.py src/OpenCiv2.Cli/bin/Release/net11.0/oc2
python3 tests/content_pack_cli.py src/OpenCiv2.Cli/bin/Release/net11.0/oc2
python3 tests/rules_cli.py src/OpenCiv2.Cli/bin/Release/net11.0/oc2
python3 tests/preference_cli.py tests/OpenCiv2.PreferenceRoundTrip/bin/Release/net11.0/OpenCiv2.PreferenceRoundTrip
python3 tests/roundtrip_helpers_cli.py tests/OpenCiv2.MapRoundTrip/bin/Release/net11.0/OpenCiv2.MapRoundTrip tests/OpenCiv2.WireRoundTrip/bin/Release/net11.0/OpenCiv2.WireRoundTrip
python3 tests/live_save_cli.py tests/OpenCiv2.LiveSave/bin/Release/net11.0/OpenCiv2.LiveSave
python3 tests/sqlite_cli.py src/OpenCiv2.Cli.Sqlite/bin/Release/net11.0/oc2-sqlite
```

These independently exercise complete JSON/status results, exact byte round trips,
publication, paths, malformed input and preserved sources. The bounded live-save
helper does not implement a full game load/play flow. See [helper contracts](helpers.md)
and [configuration coverage](configuration.md).

## X02 execution

```sh
python3 tools/dotnet/pcg_fixtures.py
python3 tools/dotnet/run_conformance.py --report build/test-results/conformance-native.json --adapter dotnet tests/OpenCiv2.ConformanceAdapter/bin/Release/net11.0/OpenCiv2.ConformanceAdapter.dll
python3 tools/dotnet/test_conformance.py --adapter dotnet tests/OpenCiv2.ConformanceAdapter/bin/Release/net11.0/OpenCiv2.ConformanceAdapter.dll
python3 tests/conformance_script_adapter.py dotnet tests/OpenCiv2.ConformanceAdapter/bin/Release/net11.0/OpenCiv2.ConformanceAdapter.dll
python3 tests/conformance_session_adapter.py dotnet tests/OpenCiv2.ConformanceAdapter/bin/Release/net11.0/OpenCiv2.ConformanceAdapter.dll
```

After installing the WASM workload and Node/Playwright as documented in BUILDING:

```sh
dotnet restore tests/OpenCiv2.ConformanceBrowser --locked-mode
dotnet publish tests/OpenCiv2.ConformanceBrowser -c Release --no-restore
python3 tools/dotnet/run_conformance.py --report build/test-results/conformance-browser.json --browser-bundle tests/OpenCiv2.ConformanceBrowser/bin/Release/net11.0-browser/publish/wwwroot
python3 tools/dotnet/test_conformance.py --browser-bundle tests/OpenCiv2.ConformanceBrowser/bin/Release/net11.0-browser/publish/wwwroot
python3 tools/dotnet/compare_conformance.py build/test-results/conformance-native.json build/test-results/conformance-browser.json
```

The selected 57 cases cover fifteen operations. The runner verifies compiled
source identities, fixture policy, invocation artifacts, result/trace comparisons
and consumed prefixes. Expected observations never enter the adapter. Follow
[conformance registration](../conformance/README.md) to extend the surface.

## Foundation reports and infrastructure

```sh
dotnet tests/OpenCiv2.FoundationProbe/bin/Release/net11.0/OpenCiv2.FoundationProbe.dll > build/test-results/native.json
python3 tools/dotnet/write_identity.py build/test-results/build-identity.json --report build/test-results/native.json --report build/test-results/browser.json
python3 tools/dotnet/test_identity.py
python3 tools/dotnet/test_coverage.py
python3 tools/dotnet/test_publish_notices.py
```

Create `build/test-results` before redirecting output. Generate the browser report
with the build guide's browser test command. Source changes invalidate prior
reports; rebuild/rerun rather than updating their hashes. Native dependency and
publication inventories also reject missing or modified inputs.

The [CI matrix](../../.github/workflows/ci.yml) runs the integrated commands.
[Platform acceptance](platforms.md), [packaging](../packaging.md) and
[performance](../performance/README.md) supply their own procedures. Keep authored
contract tests separate from external original-game observations and claims.
