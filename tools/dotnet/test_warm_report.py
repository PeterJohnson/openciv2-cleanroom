#!/usr/bin/env python3
"""Reject incomplete, stale or corrupted production performance evidence."""
import argparse
import copy
import json
from pathlib import Path
import unittest
from measure_warm import validate


class EvidenceTests(unittest.TestCase):
    report = None

    def check(self, report):
        browser = any(b['Assembly'] == 'OpenCiv2.PerformanceBrowser' for b in self.report['Build'])
        repetitions = len(self.report['Results']['decode-large']) - 3
        return validate(report, self.report['InputMetadata'], repetitions, browser)

    def rejects(self, mutate):
        report = copy.deepcopy(self.report); mutate(report)
        with self.assertRaises(ValueError): self.check(report)

    def test_current_bound_report(self):
        self.assertEqual(set(self.check(self.report)), set(self.report['Results']))

    def test_missing_workload_or_sample(self):
        self.rejects(lambda r: r['Results'].pop('session-command'))
        self.rejects(lambda r: r['Results']['decode-large'].pop())
        self.rejects(lambda r: r['Results']['decode-large'][3].update(Warmup=True))

    def test_stale_inputs_or_compilation(self):
        self.rejects(lambda r: r['Inputs'].update({'large.sav': '0' * 64}))
        self.rejects(lambda r: r['Build'][0]['Sources'].update({'missing-source': '0' * 64}))
        self.rejects(lambda r: r['Build'].pop())

    def test_invalid_samples_or_inconsistent_output(self):
        self.rejects(lambda r: r['Results']['inspect-large'][0].update(WallMs=float('nan')))
        self.rejects(lambda r: r['Results']['inspect-large'][0].update(AllocatedBytes=-1))
        self.rejects(lambda r: r['Results']['inspect-large'][0].update(OutputSha256='0' * 64))

    def test_slow_samples_remain_failures(self):
        report = copy.deepcopy(self.report)
        for row in report['Results']['session-command']: row['WallMs'] = 10000
        self.assertFalse(self.check(report)['session-command']['withinBudgets'])

    def test_observed_wasm_memory_bound(self):
        if 'WasmMemoryAfter' not in self.report: self.skipTest('Historical/native report has no capacity observation')
        self.rejects(lambda r: r['WasmMemoryAfter'].update(memories=0))
        report = copy.deepcopy(self.report); report['WasmMemoryAfter']['bytes'] = 1024 * 1048576
        self.assertTrue(all(not result['withinBudgets'] for result in self.check(report).values()))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--report', required=True, type=Path)
    args = parser.parse_args(); EvidenceTests.report = json.loads(args.report.read_text())
    unittest.main(argv=[__file__])
