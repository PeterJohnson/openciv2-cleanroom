# Native mounted-file identity

The managed content adapter keeps native filesystem details outside Formats and
Core. It canonicalizes paths, checks parent-directory identities against the
explicit mount (and optional narrower directory), rejects nonregular sources,
and reads with the configured byte limit. File identity is obtained from the
opened handle so hardlinks cannot masquerade as distinct component sources.
The OS identity is internal: each mount reports the first canonical source path
seen for that backing file, matching the existing CLI receipt contract. Windows
extended path prefixes are removed and receipt separators are `/`.
This preserves the ordinary local-filesystem contract; it
is not a claim of an adversarial concurrent-directory sandbox.

The narrow `LibraryImport` boundary uses these documented OS interfaces:

- Linux: [statx](https://man7.org/linux/man-pages/man2/statx.2.html), including
  `AT_EMPTY_PATH` for descriptors. The architecture-independent UAPI structure
  has a 256-byte allocation; offsets for mode, inode and device numbers are
  checked against `linux/stat.h`. Missing requested type/inode data rejects.
- macOS: [stat64 and fstat64](https://developer.apple.com/library/archive/documentation/System/Conceptual/ManPages_iPhoneOS/man2/stat.2.html),
  with the 144-byte Darwin 64-bit structure, device/mode/inode prefix. x64 uses the explicit suffixed entry points; ARM64 uses `stat`/`fstat`
  because its ABI is exclusively 64-bit-inode. See Apple’s
  [public header](https://github.com/apple-oss-distributions/xnu/blob/main/bsd/sys/stat.h). Native execution
  still needs the platform CI check; Linux cannot establish that evidence.
- Windows: [FILE_ID_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_id_info)
  combines the volume serial number with the complete 128-bit file ID.
  `GetFileInformationByHandleEx(FileIdInfo)` and a metadata-only directory handle
  preserve hardlink and directory identity; `GetFinalPathNameByHandleW` resolves
  the actual path. Handles are owned by `SafeFileHandle` and closed deterministically.
  Mounted slash-separated paths are converted to native separators before opening:
  [extended Windows paths skip implicit normalization](https://learn.microsoft.com/en-us/dotnet/standard/io/file-path-formats#skip-normalization).
  The containment regression covers nested required directories and Unicode names.

No native gameplay model, native JSON parser or shell subprocess participates in
content loading. These platform imports never enter the browser dependency graph.
