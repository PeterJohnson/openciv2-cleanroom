#!/usr/bin/env python3
"""Reject weakened/stale real Lua reports and modified prepared browser sources."""
import argparse
import copy
import json
from pathlib import Path
import shutil
import tempfile
import unittest
import prepare_lua_wasm
import run_automation


class EvidenceTests(unittest.TestCase):
    report = None

    def test_current_compiled_report(self):
        self.assertTrue(run_automation.validate(self.report))

    def rejects(self, mutate):
        report = copy.deepcopy(self.report)
        mutate(report)
        with self.assertRaises(ValueError):
            run_automation.validate(report)

    def test_missing_or_duplicate_case(self):
        self.rejects(lambda r: r['Cases'].pop())
        self.rejects(lambda r: r['Cases'].append(r['Cases'][0]))

    def test_failed_case_or_changed_semantic_result(self):
        self.rejects(lambda r: r['Cases'][0].update(Passed=False))
        self.rejects(lambda r: r['Outcome'].update(Effects=0))
        self.rejects(lambda r: r['Outcome'].update(Stack='different'))

    def test_stale_or_missing_assembly(self):
        self.rejects(lambda r: r['Build'].pop())
        self.rejects(lambda r: r['Build'][0].update(Sdk='unverified'))
        self.rejects(lambda r: r['Build'][0]['Sources'].update({'src/OpenCiv2.Base/Arithmetic.cs': '0' * 64}))

    def test_prepared_sources_reject_tampering(self):
        original = run_automation.ROOT / 'build/lua/wasm'
        pin = json.loads((run_automation.ROOT / 'dependencies.json').read_text())['libraries']['Lua']
        with tempfile.TemporaryDirectory(prefix='oc2-lua-input-test-') as directory:
            output = Path(directory) / 'wasm'
            shutil.copytree(original, output)
            prepare_lua_wasm.prepare(output, None, True)
            extra = output / 'cpp/extra.cpp'; extra.write_text('/* unexpected compilation input */')
            with self.assertRaises(ValueError): prepare_lua_wasm.prepare(output, None, True)
            extra.unlink()
            source = output / 'source/oc2_lua.c'; saved = source.read_bytes(); source.write_bytes(saved + b'\n/* modified */\n')
            with self.assertRaises(ValueError): prepare_lua_wasm.prepare(output, None, True)
            source.write_bytes(saved)
            archive = output / Path(pin['url']).name; archive.write_bytes(b'wrong archive')
            with self.assertRaises(ValueError): prepare_lua_wasm.prepare(output, None, True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', required=True, type=Path)
    args = parser.parse_args()
    EvidenceTests.report = json.loads(args.report.read_text())
    unittest.main(argv=[__file__])
