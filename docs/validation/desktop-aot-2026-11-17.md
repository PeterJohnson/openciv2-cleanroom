# Opt-in desktop Native AOT validation — 2026-11-17 Pacific

The uncommitted change based on `d365aa491ef4cb6e1abff817876b793c5b44e2bc`
passed on Ubuntu 26.04 x64 with .NET SDK **11.0.100**, runtime **11.0.0**,
Clang **22.1.6** and Python **3.14.4**. Managed desktop builds remain the default.
The tested AOT publication used `Oc2DesktopAot=true` and the `linux-x64` RID.

| Check | Result |
| --- | --- |
| Native AOT publication | Native code generation and dependency notices completed without warnings; the executable is an x86-64 ELF binary |
| AOT runtime identity | Dynamic code support is false; the publication contains no managed entry DLL, runtimeconfig or CoreCLR library; notices identify the pinned Native AOT runtime |
| AOT desktop input | Six XTest pointer checks and normal window shutdown passed on an isolated Xvfb display |
| Managed default | No AOT property or RID is selected; `SelfContained=false`; ordinary locked restore, build and publication passed after the AOT restores |
| Managed desktop input | The same six pointer checks and normal shutdown passed; dynamic code support is true |
| Mode rejection | The Native AOT verifier rejects the managed publication before launching it |
| Dependency locks | Linux x64, macOS ARM64 and Windows x64 AOT locked restores passed; ordinary development locks were unchanged |
| Regression checks | 454 core tests passed, zero failures/skips; nine notice tests, five identity tests and the 39-project dependency check passed |
| Formatting | Core and application solutions formatted and passed `--verify-no-changes`; both managed builds had zero warnings/errors |

The six input checks cover observer-filtered units, asynchronous decisions,
cancellation without effects, confirmed reordering, a repeated command with no
effect, and declining a decision. They exercise the existing bounded application,
not a playable game or full M2 platform acceptance.

Reports and logs are under ignored `build/desktop-aot/`. The reports bind the
executed binaries and compiled source manifests: 190 inputs for Native AOT
(including its separate lock) and 189 for managed. Local publication selected
`-p:CppCompilerAndLinker=clang-22`; the Xvfb runner used the provisioned executable
at `/tmp/oc2-xvfb/runtime/usr/bin/Xvfb`. NuGet packages were cached in the workspace.
The graphical and .NET test runners required local socket/pipe access outside the
restricted execution sandbox; this did not require source changes.

| Artifact | SHA-256 |
| --- | --- |
| `application-aot.json` | `daaf7f0d2797024e8d8b9e6ad021703df6ba7f188301026f684e9ee9704a4244` |
| `application-managed.json` | `640e7a14b46942258069001d49b4295fd84a17b0cd6a26718f6ccabb7db508d5` |
| Published Linux AOT executable | `ed9bd232e3abf2b0500f0609dc478eb327637f0f0c3c5470447a661d4974b808` |

The updated CI workflow publishes Native AOT on Linux x64, macOS ARM64 and
Windows x64, and exercises Linux input. At the time of this initial local run,
that workflow had not executed for this change; macOS/Windows AOT compilation
and desktop execution were unverified. No performance, full-desktop acceptance or
original-game compatibility claim follows from these checks. Reproduce them
with the [build guide](../BUILDING.md#opt-in-native-aot-desktop).

## Correction after hosted restore

[The first hosted run](https://github.com/PeterJohnson/openciv2-cleanroom/actions/runs/44233912263)
found that the initial local restore had selected Ubuntu's rebuilt, unsigned
`Microsoft.DotNet.ILCompiler` package. Microsoft's signed NuGet package has a
different content hash and adds a RID-specific compiler dependency; the initial
locks therefore failed on all three hosted platforms. The earlier report hashes
above remain historical evidence of that local build.

The correction regenerated all three locks using Microsoft's SDK 11.0.100 and a
fresh cache. SDK signature verification passed for the generic compiler and all
three platform compiler archives before their content/archive hashes were pinned.
The notice collector now includes the SDK license for the platform compiler too.
All three locked restores, ten notice tests and five identity tests passed.
Linux AOT publication completed without warnings, with 31 packages and 36 notice
files; the rebuilt executable passed the same six input checks and normal close.

| Corrected artifact | SHA-256 |
| --- | --- |
| `build/desktop-aot/application-aot-upstream.json` | `47071b1f094872474a48b59a61f12ee54acade95affefacee40cfa07722b22f4` |
| Published Linux AOT executable | `ecd07503a77b6e74d3b98f77f97fa29ca2b9f1bfe4006b91bee6904a9636750e` |
