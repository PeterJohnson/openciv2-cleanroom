#!/usr/bin/env python3
"""Independent PCM output checks against the test-only miniaudio bridge (no device)."""
import argparse
import ctypes as c
import hashlib
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('library', type=Path); parser.add_argument('report', type=Path)
    args = parser.parse_args(); library = c.CDLL(str(args.library.resolve()))
    library.oc2_audio_open_offline.argtypes = [c.POINTER(c.c_float), c.c_int, c.c_int, c.c_int, c.c_int, c.POINTER(c.c_void_p)]
    library.oc2_audio_render.argtypes = [c.c_void_p, c.POINTER(c.c_float), c.c_uint]
    library.oc2_audio_state.argtypes = [c.c_void_p]; library.oc2_audio_close.argtypes = [c.c_void_p]
    checks = []
    for channels in [1, 2]:
        for loop in [0, 1]:
            values = [((i % 17) - 8) / 16 for i in range(256 * channels)]
            for cycle in range(100):
                samples = (c.c_float * len(values))(*values); handle = c.c_void_p()
                assert library.oc2_audio_open_offline(samples, 256, 22050, channels, loop, c.byref(handle)) == 0
                for i in range(len(values)): samples[i] = 0  # Buffer must own submitted data.
                try:
                    output = (c.c_float * (768 * channels))()
                    assert library.oc2_audio_render(handle, output, 768) == 0
                    expected = values * 3 if loop else values + [0.] * (512 * channels)
                    assert list(output) == expected, (channels, loop, cycle, list(output)[:16])
                    assert library.oc2_audio_state(handle) == (0 if loop else 1)
                finally: library.oc2_audio_close(handle)
            checks.append(dict(channels=channels, loop=bool(loop), replacements=100, exactSamples=True))
    args.report.write_text(json.dumps(dict(checks=checks, librarySha256=hashlib.sha256(args.library.read_bytes()).hexdigest(),
        driverSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), scope='Offline PCM graph, not physical output'), indent=2) + '\n')
    print('Passed 400 exact PCM buffer/loop/replacement checks')
if __name__ == '__main__': main()
