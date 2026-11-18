#!/usr/bin/env python3
"""Preserve validated startup samples and actual transfer inventories compactly."""
import argparse
import json
from pathlib import Path
from measure_application import summarize
from measure_cli import BUDGET_PATH, digest
from performance_inputs import ROOT, sha


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True); parser.add_argument('reports', type=Path, nargs='+')
    args = parser.parse_args(); runs = []; requests = {}
    for path in args.reports:
        report = json.loads(path.read_text()); summaries = summarize(report, 15)
        if report['smokeOnly'] or report['budgetSha256'] != sha(BUDGET_PATH): raise ValueError('Smoke or different budgets')
        samples = []
        for sample in report['samples']:
            row = {k: sample[k] for k in ('iteration', 'warmup', 'readyMs')}
            if report['host'] == 'browser':
                inventory = sorted(sample['serverRequests'], key=lambda q: (q['path'], q['encoding'] or ''))
                key = digest(inventory); requests[key] = inventory
                row.update(cache=sample['cache'], networkEncodedBytes=sample['networkEncodedBytes'], requestInventory=key,
                           memory=sample['memory'], memoryAfterCommand=sample['memoryAfterCommand'])
            else:
                row.update(idleRssBytes=sample['idleRssBytes'], startupHighWaterBytes=sample['startupHighWaterBytes'])
            row['verifiedInputOutcome'] = {'status': sample['completed']['status'], 'changed': sample['completed']['changed'], 'effects': sample['completed']['effects']}
            samples.append(row)
        runs.append({'report': path.resolve().relative_to(ROOT).as_posix(), 'reportSha256': sha(path), 'host': report['host'],
                     'createdUtc': report['createdUtc'], 'revision': report['revision'], 'machine': report['machine'],
                     'browser': report.get('browser'), 'playwright': report.get('playwright'),
                     'sources': report['sources'], 'artifactManifestSha256': digest(report['artifacts']),
                     'compiledIdentitySha256': digest(report['identity']), 'logicalPackageBytes': report['logicalPackageBytes'],
                     'summaries': summaries, 'samples': samples, 'allStartupBudgetsPass': all(s['withinBudgets'] for s in summaries.values()),
                     'limitations': report['limitations']})
    args.output.write_text(json.dumps({'format': 'openciv2-application-performance-summary/1', 'budgetSha256': sha(BUDGET_PATH),
                                     'scope': 'Linux production desktop/Xvfb and Chromium browser startup, transfer and memory; not display FPS or cross-platform acceptance.',
                                     'requestInventories': requests, 'runs': runs}, indent=2) + '\n')
    print('Preserved ' + str(sum(len(r['samples']) for r in runs)) + ' validated production startup samples.')


if __name__ == '__main__': main()
