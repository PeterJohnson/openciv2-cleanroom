# Native test helpers

The root solution includes the following native test executables under `tests/`.
They call production APIs and return status 0 on success, 1 on failure. They do not
expand the product CLI's domain or establish original-game interoperability.

| Executable | Contract and evidence |
| --- | --- |
| `OpenCiv2.SaveRoundTrip` | Stored save decode/encode, auxiliary values and no-overwrite output; `inspection_cli.py` and shared save-codec groups. |
| `OpenCiv2.PreferenceRoundTrip` | Two complete native item reads (72/444 bytes), successful close and live admission compared with decoded records. Raw stored records preserve opaque bytes on output; report ignored trailing bytes. `preference_cli.py`: three checks, including short input and existing output. |
| `OpenCiv2.MapRoundTrip` | F06 header, separate start arrays, exact tile bytes and retained tail. Seven combined map/wire CLI groups include malformed geometry/truncation, Unicode paths, preserved inputs and output races. |
| `OpenCiv2.WireRoundTrip` | N05 one-byte/coalesced frame reconstruction with retained padding; N02 messages recognized only by their magic and checked for exact declared extent. Datagram mode preserves header/body and the helper's 512-byte payload ceiling. Empty, incomplete and malformed captures fail. |
| `OpenCiv2.LiveSave` | Actual load/edit/save workflow and stored comparison; `live_save_cli.py`. |

`tests/roundtrip_helpers_cli.py` authors map and wire bytes from the F06/N02/N05
record definitions. It does not consume original files, source-derived fixtures,
or external reference harnesses. Its output race uses two real processes; the
native writer suite additionally synchronizes eight contenders after staging and
before publication. The helper suites have executed in CI on all three desktop operating systems.
Portable codecs also run in the shared browser suite; process-style CLI execution
is a native feature. See [commands](README.md#independent-clihelper-drivers) and
[validation status](../validation/README.md).

## Atomic output publication

`NativeFiles.WriteNewFile` stages, flushes and closes output, then calls
POSIX `link` on Linux/macOS. That operation atomically refuses any existing target.
Only the staging name is removed during cleanup; completed publication is never
rolled back. Filesystems without hard-link support fail explicitly, without a
partial-copy fallback. Windows retains its no-overwrite move path. Cancellation
before publication and cleanup failure precedence are unchanged. The same writer
protects conversion outputs, SQLite exports and round-trip outputs.
