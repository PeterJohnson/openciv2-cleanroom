"""Authored F06 raw-map and N02/N05 wire helper checks; no original-game inputs."""
import json
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import unittest

MAP = Path(sys.argv.pop(1)).resolve()
WIRE = Path(sys.argv.pop(1)).resolve()


def map_bytes():
    # F06 MapHeader, separate 21-element i16 start arrays, twelve six-byte tiles.
    header = struct.pack('<hhhHHhh', 6, 4, 12, 1, 0xbeef, 2, 1)
    starts = struct.pack('<42h', *range(-21, 21))
    return header + starts + bytes((i * 37 + 11) % 256 for i in range(72)) + b'\x00tail'


def message(body=b'\xff\x00abc'):
    # N02 section 2: magic, kind, total bytes and sequence, followed by opaque body.
    return struct.pack('<IIII', 0x66606660, 0x21, 16 + len(body), 0xffffffff) + body


def frame(payload, tag=0x12345678):
    # N05 section 4: three u32 header words and the retained tail to a 64-byte boundary.
    total = ((12 + len(payload) + 63) // 64) * 64
    return struct.pack('<III', total, tag, len(payload)) + payload + bytes([0xa7]) * (total - 12 - len(payload))


class RoundTripHelpers(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix='oc2-roundtrip-')
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.source = self.root / '源 MiXeD.bin'
        self.output = self.root / '輸出 out.mp'

    def run_helper(self, executable, *args):
        return subprocess.run([str(executable), *map(str, args)], capture_output=True, text=True, encoding="utf-8", timeout=20)

    def test_map_bytes_fields_and_tail(self):
        data = map_bytes(); self.source.write_bytes(data)
        run = self.run_helper(MAP, self.source, self.output)
        self.assertEqual(run.returncode, 0, run.stderr)
        self.assertEqual(json.loads(run.stdout), dict(width=6, height=4, tiles=12, trailingBytes=5, byteEqual=True))
        self.assertEqual(self.output.read_bytes(), data)
        self.assertEqual(self.source.read_bytes(), data)

    def test_map_rejects_truncation_geometry_and_existing_output(self):
        good = map_bytes()
        for data in [good[:size] for size in (0, 13, 14, 55, 97, 169)] + [b'\x03\x00' + good[2:]]:
            with self.subTest(length=len(data)):
                self.source.write_bytes(data)
                self.assertEqual(self.run_helper(MAP, self.source, self.output).returncode, 1)
                self.assertFalse(self.output.exists()); self.assertEqual(self.source.read_bytes(), data)
        self.source.write_bytes(good); self.output.write_bytes(b'retained')
        self.assertEqual(self.run_helper(MAP, self.source, self.output).returncode, 1)
        self.assertEqual(self.output.read_bytes(), b'retained')
        self.assertEqual(self.run_helper(MAP, self.source, self.source).returncode, 1)
        self.assertEqual(self.source.read_bytes(), good)

    def test_map_publication_race(self):
        data = map_bytes(); self.source.write_bytes(data)
        commands = [subprocess.Popen([str(MAP), str(self.source), str(self.output)], stdout=subprocess.PIPE, stderr=subprocess.PIPE) for _ in range(2)]
        for process in commands: process.communicate(timeout=20)
        self.assertEqual(sorted(p.returncode for p in commands), [0, 1])
        self.assertEqual(self.output.read_bytes(), data); self.assertEqual(self.source.read_bytes(), data)

    def test_stream_one_byte_and_coalesced_retained_padding(self):
        data = frame(b'not a message') + frame(message()) + frame(b'', 0xfc000000)
        self.source.write_bytes(data); run = self.run_helper(WIRE, self.source)
        self.assertEqual(run.returncode, 0, run.stderr)
        self.assertEqual(json.loads(run.stdout), dict(frames=3, applicationMessages=1, bytes=len(data), oneByteAndCoalesced=True, byteEqual=True))
        self.assertEqual(self.source.read_bytes(), data)

    def test_stream_rejects_empty_partial_and_malformed(self):
        good = frame(message())
        malformed_message = message()[:8] + struct.pack('<I', 16) + message()[12:]
        for data in (b'', good[:3], good[:-1], struct.pack('<I', 0), struct.pack('<I', 11), struct.pack('<I', 9000000), frame(malformed_message)):
            self.source.write_bytes(data)
            self.assertEqual(self.run_helper(WIRE, self.source).returncode, 1)
            self.assertEqual(self.source.read_bytes(), data)

    def test_datagram_exact_bytes_and_nominal_capacity(self):
        for payload in (message(), message(bytes(496))):
            data = struct.pack('<I', 0xfedcba98) + payload; self.source.write_bytes(data)
            run = self.run_helper(WIRE, '--datagram', self.source)
            self.assertEqual(run.returncode, 0, run.stderr)
            self.assertEqual(json.loads(run.stdout), dict(bytes=len(data), kind=0x21, byteEqual=True))
            self.assertEqual(self.source.read_bytes(), data)
        for data in (b'', b'\x00' * 3, struct.pack('<I', 0) + message(bytes(497)), struct.pack('<I', 0) + message()[:-1]):
            self.source.write_bytes(data)
            self.assertEqual(self.run_helper(WIRE, '--datagram', self.source).returncode, 1)
            self.assertEqual(self.source.read_bytes(), data)

    def test_usage_and_missing_inputs(self):
        self.assertEqual(self.run_helper(MAP).returncode, 1)
        self.assertEqual(self.run_helper(WIRE).returncode, 1)
        for executable, args in [(MAP, (self.source, self.output)), (WIRE, ('--datagram', self.source))]:
            run = self.run_helper(executable, *args)
            self.assertEqual(run.returncode, 1)
            self.assertIn(self.source.name, run.stderr)


if __name__ == '__main__': unittest.main()
