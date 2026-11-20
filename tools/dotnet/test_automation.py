#!/usr/bin/env python3
"""Reject weakened/stale Lua-CSharp execution reports."""
import argparse
import copy
import json
from pathlib import Path
import unittest
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

    def test_native_bridge_report_rejected(self):
        self.rejects(lambda r: r.update(Format='openciv2-production-lua/1'))

    def test_changed_dependency_lock(self):
        def mutate(report):
            entry = next(e for e in report['Build'] if e['Assembly'] == 'OpenCiv2.Automation')
            entry['Sources']['src/OpenCiv2.Automation/packages.lock.json'] = '0' * 64
        self.rejects(mutate)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', required=True, type=Path)
    args = parser.parse_args()
    EvidenceTests.report = json.loads(args.report.read_text())
    unittest.main(argv=[__file__])
