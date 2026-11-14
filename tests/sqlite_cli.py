"""Cross-check exported rows with the public JSON contract using Python SQLite."""
import json
from pathlib import Path
import sqlite3
import struct
import subprocess
import sys
import tempfile
import unittest

# Shares the independently authored input bytes, not exporter expectations.
import inspection_cli as fixture

DRIVER = Path(sys.argv.pop(1)).resolve()


class SQLiteCli(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix='oc2-sqlite-')
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.source = self.root / 'source.SAV'
        self.data = fixture.save_bytes()
        # Full unsigned 64-bit summary, negative retained census and two slot holes.
        region = fixture.MAP + 14 + 7 * 4 + 6 * 4 + 2
        struct.pack_into('<Q', self.data, region + 7, 2**64-1)  # F06 section 3: offset 007.
        struct.pack_into('<i', self.data, 12 + 330 + 8 * 242 + 2, -2**31)
        struct.pack_into('<h', self.data, 12 + 330 + 8 * 242 + 0x66, -123)
        self.data[fixture.CITY:fixture.CITY] = bytes(64)
        struct.pack_into('<h', self.data, 12 + 0x2e, 3)
        self.source.write_bytes(self.data)

    def tearDown(self):
        self.assertEqual(self.source.read_bytes(), self.data)
        self.assertFalse(list(self.root.glob('*.oc2-tmp-*')))

    def run_cli(self, *args, status=0):
        result = subprocess.run([str(DRIVER), *map(str, args)], capture_output=True,
                                encoding='utf-8', timeout=30)
        self.assertEqual(result.returncode, status, result.stderr or result.stdout[:1000])
        return result

    def database(self, path):
        db = sqlite3.connect(path)
        self.addCleanup(db.close)
        return db

    def test_every_exported_field_matches_lossless_query_records(self):
        query = json.loads(self.run_cli('inspect', self.source, '--json').stdout)
        target = self.root / 'analyse cité.sqlite'
        receipt = json.loads(self.run_cli('export', self.source, '--sqlite', target, '--json').stdout)
        self.assertTrue(receipt['complete'])
        self.assertEqual(receipt['sourceSha256'], query['source']['sha256'])
        db = self.database(target)
        self.assertEqual(db.execute('PRAGMA application_id').fetchone()[0], 0x4f433253)
        self.assertEqual(db.execute('PRAGMA user_version').fetchone()[0], 1)
        self.assertEqual(db.execute('PRAGMA integrity_check').fetchall(), [('ok',)])
        self.assertEqual(db.execute('PRAGMA foreign_key_check').fetchall(), [])
        snapshot = db.execute('SELECT * FROM snapshots').fetchone()
        self.assertEqual(snapshot, (query['source']['sha256'], 'classic-save', 44,
                                   len(self.data), 'stored', 'offline-omniscient',
                                   'passed-available-checks', 0))
        rows = db.execute('SELECT section,slot,ordinal,source_offset,raw,provenance '
                          'FROM records ORDER BY ordinal').fetchall()
        self.assertEqual(len(rows), len(query['records']))
        self.assertEqual(b''.join(row[4] for row in rows), self.data)
        expected_fields = []
        for ordinal, (row, record) in enumerate(zip(rows, query['records'])):
            self.assertEqual(row, (record['section'], int(record['slot']), ordinal,
                                   int(record['offset']), bytes.fromhex(record['bytes']), 'stored'))
            for index, field in enumerate(record['fields']):
                value = field['value']
                kind, integer, blob = 'null', None, None
                if value is not None:
                    if field['type'] in ('bytes', 'classic-text'):
                        kind, blob = 'bytes', bytes.fromhex(value)
                    else:
                        kind = 'signed' if field['type'].startswith('i') else 'unsigned'
                        integer = value
                expected_fields.append((record['section'], int(record['slot']), index,
                                        field['name'], field['type'], kind, integer, blob))
        actual_fields = db.execute('SELECT f.section,f.slot,f.ordinal,f.name,f.type,'
                                   'f.value_kind,f.integer_decimal,f.value_blob FROM fields f '
                                   'JOIN records r USING(snapshot_id,section,slot) '
                                   'ORDER BY r.ordinal,f.ordinal').fetchall()
        self.assertEqual(actual_fields, expected_fields)
        self.assertEqual(db.execute('SELECT token FROM unit_slots ORDER BY slot').fetchall(),
                         [('2147483649',), ('0',), ('0',)])
        self.assertEqual(db.execute("SELECT integer_decimal FROM fields WHERE "
                                    "section='civilizations' AND slot=0 AND name='military_count'")
                         .fetchone(), ('-123',))
        self.assertEqual(db.execute("SELECT integer_decimal,typeof(integer_decimal) FROM fields "
                                    "WHERE section='regions' AND slot=0 AND name='neighboring_water'")
                         .fetchone(), ('18446744073709551615', 'text'))
        self.assertEqual(db.execute('SELECT name FROM validation_checks ORDER BY ordinal').fetchall(),
                         [(name,) for name in query['validation']['checks']])
        self.assertEqual(db.execute('SELECT category,severity,code,field,byte_offset,message '
                                    'FROM diagnostics ORDER BY ordinal').fetchall(),
                         [(d['category'], d['severity'], d['code'], d['field'],
                           None if d['offset'] is None else int(d['offset']), d['message'])
                          for d in query['diagnostics']])
        self.assertEqual(sorted(r[0] for r in db.execute('SELECT section FROM coverage')),
                         sorted(query['coverage']['supportedSections']))
        self.assertEqual(db.execute('SELECT SUM(record_count) FROM coverage').fetchone()[0], len(rows))

    def test_existing_source_destination_and_failed_paths_are_preserved(self):
        self.run_cli('export', self.source, '--sqlite', self.source, status=2)
        target = self.root / 'existing.sqlite'
        target.write_bytes(b'keep this existing output')
        self.run_cli('export', self.source, '--sqlite', target, status=2)
        self.assertEqual(target.read_bytes(), b'keep this existing output')
        self.run_cli('export', self.source, '--sqlite', self.root / 'missing' / 'db.sqlite', status=2)
        try:
            alias = self.root / 'source-alias.sqlite'
            alias.hardlink_to(self.source)
        except OSError:
            pass
        else:
            self.run_cli('export', self.source, '--sqlite', alias, status=2)

    def test_raw_map_has_null_version_and_exact_source_bytes(self):
        data = struct.pack('<hhhHHhh', 4, 2, 4, 0, 24, 1, 1)
        data += b'\xff' * 84 + bytes([10, 0, 0, 0, 0, 0]) * 4
        source = self.root / 'source.MP'
        source.write_bytes(data)
        target = self.root / 'map.sqlite'
        self.run_cli('export', source, '--sqlite', target)
        db = self.database(target)
        self.assertEqual(db.execute('SELECT source_format,source_version FROM snapshots')
                         .fetchone(), ('classic-raw-map', None))
        self.assertEqual(b''.join(r[0] for r in db.execute('SELECT raw FROM records ORDER BY ordinal')), data)
        self.assertEqual(db.execute("SELECT value_kind,length(value_blob) FROM fields "
                                    "WHERE section='trailing' AND name='payload'")
                         .fetchone(), ('bytes', 0))
        self.assertEqual(source.read_bytes(), data)

    def test_unsupported_or_malformed_input_produces_no_database(self):
        target = self.root / 'never.sqlite'
        invalid = self.root / 'invalid.sav'
        invalid.write_bytes(self.data[:10])
        self.run_cli('export', invalid, '--sqlite', target, status=4)
        self.assertFalse(target.exists())
        legacy = bytearray(self.data)
        legacy[10] = 0x28
        invalid.write_bytes(legacy)
        self.run_cli('export', invalid, '--sqlite', target, status=3)
        self.assertFalse(target.exists())

    def test_semantic_errors_remain_in_explicitly_incomplete_analysis(self):
        invalid = self.root / 'semantic.sav'
        data = bytearray(self.data)
        data[fixture.UNITS + 7] = 255
        invalid.write_bytes(data)
        target = self.root / 'diagnosed.sqlite'
        self.run_cli('export', invalid, '--sqlite', target, status=1)
        db = self.database(target)
        self.assertEqual(db.execute('SELECT validation_status,validation_complete FROM snapshots')
                         .fetchone(), ('errors', 0))
        self.assertEqual(db.execute("SELECT code FROM diagnostics WHERE severity='error'")
                         .fetchall(), [('owner-range',)])
        self.assertEqual(invalid.read_bytes(), data)


if __name__ == '__main__':
    unittest.main()
