# M1-06 final build matrix

Status: **passed**, source revision `56be096c474314ef1dc40b602ce2a5251f8b7923`,
2026-11-14. The required jobs in [run 43648904399](https://github.com/PeterJohnson/openciv2-cleanroom/actions/runs/43648904399)
passed. All 12 workflow jobs also passed; desktop GUI jobs are outside this
finite gate.

The [retained matrix report](conformance/reports/m1-hosted-matrix.json) records
job links, artifact IDs/digests, build identities, JUnit summaries, evidence-file
hashes and relevant boundary test names. Downloaded archive SHA-256 values were
checked against GitHub's artifact digests before inspecting their contents.

| Required configuration | Observed result |
| --- | --- |
| Windows pinned Clang, SQLite on | 574 CTests pass; inspection and SQLite CLI included |
| Linux pinned Clang, SQLite on | 573 CTests pass; inspection and SQLite CLI included |
| macOS pinned AppleClang, SQLite on | 573 CTests pass; inspection and SQLite CLI included |
| Windows pinned Clang, SQLite off | 572 CTests pass; build identity disables SQLite |
| Pinned Emscripten/Node portable tests | Seven CTests pass; 547 Catch2 cases, 290567 assertions; metadata and X02 execution/rejection checks included |
| Linux ASan/UBSan | 571 applicable CTests pass, including checkpoint/conversion tests, without sanitizer findings |
| Linux tools-only | GUI/tests/SQLite/fetching disabled, Python discovery disabled; no runtime assets acquired; eight authored-file inspection and six content CLI tests pass |
| Developer checks on Windows/Linux/macOS | Pinned formatter checks 282 files; metadata, assets, content schemas and gameplay data pass; 115 Python tests pass on each platform with no skips |

All six conformance reports identify the exact clean source revision above and
verified pinned toolchains. Each executes the same 57 fixture IDs, and every
observation and consumed prefix matches across native, sanitizer and WASM builds.
The retained [native](conformance/reports/f01-native.json) and
[WASM](conformance/reports/f01-wasm.json) reports are from these hosted artifacts.

The earlier run at `47999e7` exposed two defects, now fixed: the pinned macOS
library lacks `std::stop_token`, and Windows runner paths use native DOS aliases
that the classic file resolver failed to recognize. Poll-only owned cancellation
tokens preserve cancellation behavior; native alias fallback preserves explicit
file access. Regression tests cover token ownership/isolation and Windows short
directory names. The old binary failed a real short-path probe; the fixed binary
succeeded. All 574 local SQLite-enabled CTests also passed before the fixes were
pushed. Earlier local MSVC results remain supplemental evidence, not substitutes
for the pinned hosted matrix.

Tools-only compilation does not discover Python. Python is used afterward solely
as the test harness for the resulting standalone executable. That distinction
preserves the production configuration's no-Python requirement.
