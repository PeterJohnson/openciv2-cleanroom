# Authored audio service profile

M2 supplies playback services and a real caller through **File → Files and
preferences → Play selected WAV**. The file library preserves imported bytes;
preview never changes a session, awards an effect, or answers a pending command.
Full scenario media resolution and video decoding remain M5, as distinguished in
[P10](../algorithms/P10-audio-video-and-animation.md). A `MediaKind.Video` request
returns `Unavailable`; the silent backend can explicitly skip any presentation.
The caller retains meaningful captions and semantic completion in either case.

`WaveAudio` admits RIFF/WAVE integer PCM, mono or stereo, 8/16/24/32 bits per
sample, at 8–192 kHz. It supports ancillary chunks with word padding and data
before format, validates sizes/alignment/rate, and rejects duplicate format/data
chunks, truncation and inconsistent metadata. Compressed, extensible and float
WAV formats are currently rejected with a visible diagnostic. Encoded files are
at most 32 MiB; decoded audio is at most 16,777,216 interleaved samples and ten
minutes. These are explicit admission limits, not claims about all classic or
scenario media. Unsigned 8-bit and signed wider samples become owned linear
float PCM without changing channel order or the selected resource.
The container definitions come from Microsoft's
[RIFF documentation](https://learn.microsoft.com/en-us/windows/win32/xaudio2/resource-interchange-file-format--riff-)
and [WAVEFORMATEX](https://learn.microsoft.com/en-us/windows/win32/api/mmeapi/ns-mmeapi-waveformatex).

The desktop adapter uses **miniaudio 0.11.26**, pinned by source archive hash in
`dependencies.json`. A small opaque C bridge owns the device, playback graph and
copied samples. No managed callback runs on the audio thread. Playback work runs
off the UI dispatcher. Production backend selection excludes miniaudio's null
backend, so missing libraries/devices cannot claim successful output. Decoder and
resource-manager features are disabled in this library; WAV admission is shared
C# code. The browser uses the standard Web Audio API with the same decoded PCM.
It bounds a suspended `resume()` request, reports permission/device failures,
closes contexts after completion/cancellation, and cancels active output on
`pagehide`. Neither host uses SDL.

`Completed` means the playback provider reached the end; it is not a measured
speaker timestamp. Native completion includes a short backend drain interval.
`Cancelled`, `Skipped` and `Unavailable` are distinct. Looping stays pending
until cancelled or the device fails. One playback is admitted per adapter;
overlapping requests return unavailable rather than queueing unbounded sounds.
Late cancellation is scoped to its own browser request. Missing stock cosmetic
media remains skipped; no unrelated sound is substituted.

Build native audio with a host C compiler before building the desktop:

```sh
python tools/dotnet/build_audio.py
# Offline: add --archive /path/to/miniaudio-0.11.26.tar.gz
python tools/dotnet/build_audio.py --verify
```

On Windows use a Developer Command Prompt. The helper verifies the source hash,
builds the host library, checks its runtime version, and writes a receipt and
license. Desktop builds verify that receipt and copy all three into the output
and publication. `-p:Oc2AudioDirectory=PATH` selects another verified build.
Native AOT uses the same small ABI. Root/headless builds do not require this
native library, an audio device, or graphical packages.

Local validation commands (Linux display/audio helpers need KWin, Xwayland,
PipeWire, WirePlumber, `pactl` and XTest):

```sh
python tools/dotnet/build_audio.py --test --output build/audio/test
python tools/dotnet/test_audio_native.py build/audio/test/liboc2_audio.so build/audio/offline.json
OC2_TEST_NATIVE_AUDIO=1 LD_LIBRARY_PATH="$PWD/build/audio/native" \
  python tools/dotnet/run_audio_isolated.py --log build/audio/server.log \
  dotnet test --project tests/OpenCiv2.Base.Tests -c Release --no-restore
node tools/dotnet/test_audio_browser.cjs BROWSER_BUNDLE build/audio/browser-service.json
node tools/dotnet/run_storage_browser.cjs BROWSER_BUNDLE build/audio/browser-ui.json --media
python tools/dotnet/run_audio_isolated.py --log build/audio/ui-server.log \
  python tools/dotnet/run_xwayland_virtual.py --compositor-log build/audio/compositor.log \
  python tools/dotnet/run_storage_native.py --media --output build/audio/native-ui.json \
  --adapter dotnet src/OpenCiv2.Desktop/bin/Release/net11.0/OpenCiv2.Desktop.dll
```

The native service test is explicitly skipped without `OC2_TEST_NATIVE_AUDIO=1`.
The isolated audio helper creates private servers, private configuration/state
and a virtual sink; it neither chooses the user's default device nor sends tones
to speakers. Offline PCM and virtual-sink tests do not establish audible output
or physical device removal behavior. `--test` native libraries expose offline
entry points and are rejected by the production build verifier.
