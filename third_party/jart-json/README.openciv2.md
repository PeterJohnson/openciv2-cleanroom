# JSON dependency

Runtime sources are unmodified files from
[jart/json.cpp at ccb9195e6abe9eb060e338a04f43e8d2e67b45ea](https://github.com/jart/json.cpp/tree/ccb9195e6abe9eb060e338a04f43e8d2e67b45ea),
including that commit's bundled double-conversion and jtckdint.h. The archive and
each retained upstream file are SHA-256 pinned in `dependencies.json`;
`cmake/Json.cmake` verifies the files on configuration. Only runtime files and
licenses are included. No upstream tests or unrelated JSON implementations ship.

`APACHE-2.0.txt` supplements the upstream copyright notice with the complete
[Apache license text](https://www.apache.org/licenses/LICENSE-2.0.txt), SHA-256
`cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30`.
The repository's `THIRD_PARTY_NOTICES.txt` includes Apache, ISC and BSD notices
for binary distributions. Do not format these dependency sources.

MSVC builds use `/Zc:__cplusplus` so jtckdint's C++ version detection selects
64-bit arithmetic on LLP64. No source patch is applied. The owned content wrapper
rejects duplicate keys, nonstandard escapes and unpaired surrogates; it preserves
numeric lexemes separately for exact authoring-domain admission. The native
content contract is stricter than the library's general-purpose parse policy.
