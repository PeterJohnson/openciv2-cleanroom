"""F03/F06 live driver contract with authored rules and stored bytes."""
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import unittest
from inspection_cli import save_bytes, UNITS, CITY

DRIVER = Path(sys.argv.pop(1)).resolve()


def rules():
    # Independent F03 rows; these are not an engine-produced expectation.
    parts = ['@COSMIC\n' + '\n'.join(map(str, (3, 2, 2, 5, 5, 1, 2, 7, 14, 8, 12,
                                             10, 20, 3, 3, 8, 1, 50, 50, 10, 75, 5))) + '\n']
    for name, count, row in (
        ('CIVILIZE', 100, 'Advance,3,1,nil,nil,0,3'),
        ('IMPROVE', 67, 'Improvement,10,1,nil'),
        ('ENDWONDER', 28, 'nil'),
        ('UNITS', 62, 'Unit,no,0,2,0,1,1,3,1,10,0,0,nil,0'),
        ('TERRAIN', 33, 'Terrain,1,3,1,1,0,yes,1,3,2,no,0,4,1,Drt'),
        ('GOVERNMENTS', 7, 'Government,Leader,Leader'),
        ('LEADERS', 21, 'Leader,Leader,1,1,0,Tribes,Example,0,0,0'),
        ('CARAVAN', 16, 'Commodity'), ('ORDERS', 12, 'Order,x'),
        ('DIFFICULTY', 6, 'Difficulty'), ('ATTITUDES', 9, 'Attitude')):
        parts.append('@' + name + '\n' + (row + '\n') * count)
    return ''.join(parts)


class LiveSaveCli(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix='oc2-live-save-')
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.source = self.root / 'Source.SAV'
        self.source.write_bytes(save_bytes())
        (self.root / 'RULES.TXT').write_text(rules(), encoding='ascii')
        (self.root / 'LABELS.TXT').write_text(
            '@STRINGHEAP\n1000\n@LABELS\n1\nLabel\n@POPUPS\nA\nB\nC\n', encoding='ascii')
        self.originals = {p: p.read_bytes() for p in self.root.iterdir()}

    def tearDown(self):
        for path, raw in self.originals.items():
            self.assertEqual(path.read_bytes(), raw)

    def run_driver(self, *args, status=0):
        result = subprocess.run([str(DRIVER), *map(str, args)], capture_output=True,
                                text=True, encoding='utf-8', timeout=30)
        self.assertEqual(result.returncode, status, result.stderr)
        return result

    def test_live_edit_changes_requested_value_and_renews_tokens(self):
        out = self.root / 'edited.sav'
        report = json.loads(self.run_driver(self.root, self.source, out, '--gold', 1, 777).stdout)
        self.assertEqual(report['format'], 'openciv2-live-save-driver/1')
        self.assertEqual(report['fixtureEdit'], {'field': 'gold', 'civilization': 1,
                                               'before': 0, 'after': 777})
        self.assertTrue(report['sourceUnchanged'])
        self.assertTrue(report['saveRandomContextsUnchanged'])
        self.assertEqual(report['loadGameplayDraws'], 255)
        raw = out.read_bytes()
        self.assertEqual(report['output']['sha256'], hashlib.sha256(raw).hexdigest())
        self.assertEqual(struct.unpack_from('<I', raw, UNITS + 0x1a)[0], 100)
        self.assertEqual(struct.unpack_from('<I', raw, CITY + 0x54)[0], 200)
        self.assertEqual(struct.unpack_from('<i', raw, 12 + 330 + 8 * 242 + 1428 + 2)[0], 777)
        self.assertNotEqual(raw, self.originals[self.source])
        self.assertEqual(len(report['textSources']), 2)
        second = self.root / 'again.sav'
        again = json.loads(self.run_driver(self.root, out, second).stdout)
        self.assertEqual(again['loaded']['gold'][1], 777)

    def test_existing_and_malformed_inputs_leave_no_new_output(self):
        self.run_driver(self.root, self.source, self.source, status=1)
        out = self.root / 'untouched.sav'
        out.write_bytes(b'unchanged')
        self.run_driver(self.root, self.source, out, status=1)
        self.assertEqual(out.read_bytes(), b'unchanged')
        truncated = self.root / 'truncated.sav'
        truncated.write_bytes(save_bytes()[:20])
        absent = self.root / 'absent.sav'
        self.run_driver(self.root, truncated, absent, status=1)
        self.assertFalse(absent.exists())

    def test_invalid_fixture_edit_is_rejected_before_output(self):
        out = self.root / 'absent.sav'
        for options in (('--gold', 8, 5), ('--gold', 1, '2147483648'), ('--unknown', 1, 2)):
            self.run_driver(self.root, self.source, out, *options, status=1)
            self.assertFalse(out.exists())

    def test_output_requires_explicit_save_extension(self):
        # Claim exactly the path passed to F06, even below a dotted directory.
        directory = self.root / 'dotted.directory'
        directory.mkdir()
        for name in ('no_extension', 'wrong.txt'):
            out = directory / name
            self.run_driver(self.root, self.source, out, status=1)
        self.assertEqual(list(directory.iterdir()), [])


if __name__ == '__main__':
    unittest.main()
