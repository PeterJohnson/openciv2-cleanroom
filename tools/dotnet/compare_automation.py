#!/usr/bin/env python3
"""Require current artifact-bound native/browser Lua results and equal outcomes."""
import argparse
import json
from pathlib import Path
import run_automation as runner


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('native', type=Path); parser.add_argument('browser', type=Path)
    args = parser.parse_args()
    reports = [json.loads(p.read_text(), object_pairs_hook=runner.identity.unique_object) for p in (args.native, args.browser)]
    for report, host in zip(reports, ('OpenCiv2.AutomationProbe', 'OpenCiv2.AutomationBrowser')):
        sources = runner.validate(report)
        if report.get('compiledSources') != sources or host not in {entry['Assembly'] for entry in report['Build']}:
            raise ValueError('Missing source binding or incorrect host')
        artifacts = report.get('invokedArtifacts', {})
        reporting = report.get('reportingSources', {})
        if not artifacts or not reporting:
            raise ValueError('Missing artifact/runner binding')
        if any(runner.identity.sha(Path(p)) != digest for p, digest in artifacts.items()) or any(runner.identity.sha(runner.ROOT / p) != digest for p, digest in reporting.items()):
            raise ValueError('Artifacts or reporting tools changed since execution')
    if reports[0]['Cases'] != reports[1]['Cases'] or reports[0]['Outcome'] != reports[1]['Outcome']:
        raise ValueError('Native and browser Lua outcomes differ')
    print(f"All {len(runner.CASES)} Lua cases and the complete production stack/effect outcome match on native and browser.")


if __name__ == '__main__': main()
