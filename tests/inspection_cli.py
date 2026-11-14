"""Native CLI contract tests using independently authored F06 byte fixtures."""
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import unittest

if __name__ == '__main__':
    DRIVER = Path(sys.argv.pop(1)).resolve()
    ROUNDTRIP = Path(sys.argv.pop(1)).resolve()
MAP = 12 + 330 + 8 * 242 + 8 * 1428
UNITS = MAP + 14 + 7 * 4 + 6 * 4 + 2 + 1024
CITY = UNITS + 32
END = CITY + 88 + 63 + 4 + 48 + 1200 + 106 + 80 + 338


def save_bytes():
    data = bytearray(END)
    data[:12] = b'CIVILIZE\0\x1a\x2c\0'
    struct.pack_into('<hh', data, 12 + 0x2e, 1, 1)
    struct.pack_into('<hhhHHhh', data, MAP, 4, 2, 4, 0, 24, 1, 1)
    data[UNITS + 0x10] = 255
    struct.pack_into('<hhI', data, UNITS + 0x16, -1, -1, 0x80000001)
    data[CITY + 0x20:CITY + 0x24] = b'X\0\xff\x80'
    struct.pack_into('<I', data, CITY + 0x54, 17)
    return data


class InspectionCli(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix='oc2-inspection-')
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.source = self.root / 'cité with spaces.SAV'
        self.source.write_bytes(save_bytes())
        self.initial = self.source.read_bytes()

    def tearDown(self):
        self.assertEqual(self.source.read_bytes(), self.initial)

    def run_cli(self, *args, status=0):
        result = subprocess.run([str(DRIVER), *map(str, args)], capture_output=True,
                                encoding='utf-8', timeout=30)
        self.assertEqual(result.returncode, status, result.stderr or result.stdout[:1000])
        return result

    def test_roundtrip_output_preserves_input_and_refuses_existing_paths(self):
        # F06 sections 2-4/8: stored-view encoding; no live load transformations.
        def run(*args, status=0):
            result = subprocess.run([str(ROUNDTRIP), *map(str, args)],
                                    capture_output=True, encoding='utf-8', timeout=30)
            self.assertEqual(result.returncode, status, result.stderr)
            return result

        self.assertFalse(json.loads(run(self.source).stdout)['outputWritten'])
        output = self.root / 'export with spaces.sav'
        report = json.loads(run(self.source, output).stdout)
        self.assertTrue(report['outputWritten'])
        self.assertTrue(report['byteEqual'])
        self.assertTrue(report['orderedAuxiliaryEqual'])
        self.assertEqual(output.read_bytes(), self.initial)
        for destination in (self.source, output):
            self.assertIn('already exists', run(self.source, destination, status=1).stderr)
            self.assertEqual(destination.read_bytes(), self.initial)
        invalid = self.root / 'truncated.sav'
        invalid.write_bytes(self.initial[:20])
        rejected = self.root / 'rejected.sav'
        run(invalid, rejected, status=1)
        self.assertFalse(rejected.exists())
        self.assertEqual(set(self.root.iterdir()), {self.source, output, invalid})

    def test_json_is_lossless_ordered_and_has_explicit_provenance(self):
        result = self.run_cli('inspect', self.source, '--json')
        doc = json.loads(result.stdout)
        self.assertEqual(doc['schema'], 'openciv2-inspection/1')
        self.assertEqual(doc['view'], 'stored')
        self.assertEqual(doc['access'], 'offline-omniscient')
        self.assertEqual(doc['source']['sha256'], hashlib.sha256(self.initial).hexdigest())
        self.assertEqual(doc['source']['byteCount'], str(len(self.initial)))
        self.assertFalse(doc['validation']['complete'])
        restored = bytearray()
        for record in doc['records']:
            self.assertEqual(record['provenance'], 'stored')
            self.assertEqual(int(record['offset']), len(restored))
            restored.extend(bytes.fromhex(record['bytes']))
        self.assertEqual(restored, self.initial)
        unit = next(r for r in doc['records'] if r['section'] == 'units')
        token = next(f for f in unit['fields'] if f['name'] == 'token')
        self.assertEqual(token, {'name': 'token', 'type': 'u32', 'value': '2147483649'})
        self.assertEqual(self.run_cli('inspect', self.source, '--json').stdout, result.stdout)

    def test_selection_and_human_output_report_omitted_sections(self):
        doc = json.loads(self.run_cli('inspect', self.source, '--cities', '--json').stdout)
        self.assertEqual([r['section'] for r in doc['records']], ['cities'])
        self.assertIn('units', doc['coverage']['omittedSections'])
        self.assertIn('cities[0]', self.run_cli('inspect', self.source, '--cities').stdout)
        self.assertIn('rules-unavailable', self.run_cli('validate', self.source).stdout)
        checked = json.loads(self.run_cli('validate', self.source, '--json').stdout)
        self.assertEqual(checked['records'], [])
        self.assertEqual(checked['validation']['status'], 'passed-available-checks')

    def test_diff_uses_slots_not_shifted_offsets_or_renewed_tokens(self):
        changed = bytearray(self.initial)
        changed[CITY:CITY] = bytes(32)
        struct.pack_into('<h', changed, 12 + 0x2e, 2)
        struct.pack_into('<I', changed, UNITS + 0x1a, 0xffffffff)
        target = self.root / 'after.sav'
        target.write_bytes(changed)
        doc = json.loads(self.run_cli('diff', self.source, target, '--json', status=1).stdout)
        self.assertTrue(doc['different'])
        self.assertTrue(doc['fieldsCompared'])
        self.assertTrue(doc['byteChanges'])
        self.assertFalse(any(c['section'] == 'cities' for c in doc['fieldChanges']))
        token = next(c for c in doc['fieldChanges'] if c['section'] == 'units'
                     and c['slot'] == '0' and c['field'] == 'token')
        self.assertEqual(token['after']['value'], '4294967295')
        self.assertEqual(target.read_bytes(), changed)
        same = json.loads(self.run_cli('diff', self.source, self.source, '--json').stdout)
        self.assertFalse(same['different'])
        self.assertEqual(same['fieldChanges'], [])

    def test_semantic_error_has_field_offset_without_repair(self):
        changed = bytearray(self.initial)
        changed[UNITS + 7] = 255
        target = self.root / 'invalid.sav'
        target.write_bytes(changed)
        doc = json.loads(self.run_cli('validate', target, '--json', status=1).stdout)
        error = next(d for d in doc['diagnostics'] if d['code'] == 'owner-range')
        self.assertEqual(error['category'], 'semantic')
        self.assertEqual(error['field'], 'units[0].owner')
        self.assertEqual(error['offset'], str(UNITS + 7))
        self.assertEqual(target.read_bytes(), changed)

    def test_closed_output_pipe_is_an_io_failure(self):
        with subprocess.Popen([str(DRIVER), 'inspect', str(self.source), '--json'],
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE) as process:
            process.stdout.close()
            process.stdout = None
            _, errors = process.communicate(timeout=10)
            self.assertEqual(process.returncode, 2, errors)
            self.assertIn(b'Output write failed', errors)

    def test_unsupported_truncated_and_usage_errors_are_distinct(self):
        target = self.root / 'legacy.sav'
        changed = bytearray(self.initial)
        changed[10] = 0x28
        target.write_bytes(changed)
        doc = json.loads(self.run_cli('inspect', target, '--json', status=3).stdout)
        self.assertFalse(doc['decoded'])
        self.assertEqual(doc['validation']['status'], 'unsupported')
        self.assertEqual(doc['diagnostics'][0]['offset'], '10')
        target.write_bytes(self.initial[:20])
        doc = json.loads(self.run_cli('inspect', target, '--json', status=4).stdout)
        self.assertEqual(doc['diagnostics'][0]['category'], 'structural')
        self.assertEqual(doc['records'], [])
        self.run_cli('inspect', self.source, '--view', 'loaded', status=3)
        self.run_cli('inspect', self.source, '--unknown', status=2)
        self.run_cli('validate', self.source, '--cities', status=2)
        self.run_cli('inspect', self.root / 'missing.sav', status=2)
        self.run_cli('diff', self.source, status=2)
        self.run_cli('--help')

    def test_raw_map_decoding_and_explicit_format_selection(self):
        data = struct.pack('<hhhHHhh', 4, 2, 4, 0, 24, 1, 1)
        data += b'\xff' * 84 + bytes([10, 0, 0, 0, 0, 0]) * 4
        target = self.root / 'map.MP'
        target.write_bytes(data)
        doc = json.loads(self.run_cli('inspect', target, '--json').stdout)
        self.assertEqual(doc['source']['format'], 'classic-raw-map')
        self.assertIsNone(doc['source']['version'])
        self.assertEqual(b''.join(bytes.fromhex(r['bytes']) for r in doc['records']), data)
        other = self.root / 'map.bin'
        other.write_bytes(data)
        self.run_cli('validate', other, '--format', 'map')
        self.assertEqual(other.read_bytes(), data)


if __name__ == '__main__':
    unittest.main()
