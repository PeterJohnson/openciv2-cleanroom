#!/usr/bin/env python3
"""Reject weak startup evidence instead of accepting a fast empty/noninteractive UI."""
import argparse
import copy
import json
from pathlib import Path
import unittest
from measure_application import summarize


class EvidenceTests(unittest.TestCase):
    report = None

    def rejects(self, mutate):
        report = copy.deepcopy(self.report); mutate(report)
        with self.assertRaises(ValueError): summarize(report, 15)

    def test_current_report(self):
        self.assertEqual(summarize(self.report, 15), self.report['summaries'])

    def test_missing_sample_or_stale_identity(self):
        self.rejects(lambda r: r['samples'].pop())
        self.rejects(lambda r: r['identity']['build'][0]['Sources'].update({'stale': '0' * 64}))
        self.rejects(lambda r: r.update(host='unknown'))

    def test_unready_or_nonfunctional_ui(self):
        self.rejects(lambda r: r['samples'][0]['snapshot']['units'].pop())
        self.rejects(lambda r: r['samples'][0]['snapshot']['controls']['send'].update(width=0))
        self.rejects(lambda r: r['samples'][0]['completed'].update(effects=0))
        self.rejects(lambda r: r['samples'][0]['snapshot'].update(contentLoaded=False))

    def test_invalid_or_excessive_timing(self):
        self.rejects(lambda r: r['samples'][0].update(readyMs=float('nan')))
        report = copy.deepcopy(self.report)
        for sample in report['samples']: sample['readyMs'] = 10000
        self.assertTrue(all(not result['withinBudgets'] for result in summarize(report, 15).values()))

    def test_resource_evidence(self):
        if self.report['host'] == 'browser':
            self.rejects(lambda r: r['samples'][0]['memory'].update(memories=0))
            self.rejects(lambda r: r['samples'][0].update(networkEncodedBytes=0))
            self.rejects(lambda r: r['samples'][1].update(networkEncodedBytes=float('nan')))
            self.rejects(lambda r: r['samples'][1].update(serverRequests=r['samples'][0]['serverRequests']))
        else:
            self.rejects(lambda r: r['samples'][0]['idleRssBytes'].pop())
            self.rejects(lambda r: r['samples'][0].update(idleRssBytes=[0] * 10))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args(); EvidenceTests.report = json.loads(args.report.read_text())
    unittest.main(argv=[__file__])
