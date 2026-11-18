#!/usr/bin/env python3
"""Preserve compact raw warm samples only after checking their bound executions."""
import argparse
import json
from pathlib import Path
import re

from measure_cli import digest
from measure_warm import BUDGET_PATH, validate
from performance_inputs import ROOT, sha

COLUMNS = ['Warmup', 'WallMs', 'AllocatedBytes', 'Gen0', 'Gen1', 'Gen2', 'GcPauseMs',
           'ManagedHeapBytes', 'ManagedHeapPlusAllocatedBytes']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('reports', nargs='+', type=Path)
    args = parser.parse_args(); runs = []; outputs = {}; inputs = None
    for path in args.reports:
        report = json.loads(path.read_text()); browser = 'Browser' in report
        repetitions = 15 if browser else 40
        summaries = validate(report, report['InputMetadata'], repetitions, browser)
        if report['SmokeOnly'] or report['BudgetSha256'] != sha(BUDGET_PATH): raise ValueError('Smoke or different budgets')
        if inputs is not None and inputs != report['Inputs']: raise ValueError('Different comparison inputs')
        inputs = report['Inputs']; results = {}
        for name, rows in report['Results'].items():
            output = rows[0]['OutputSha256']
            if name in outputs and outputs[name] != output: raise ValueError('Different verified native/browser output: ' + name)
            outputs[name] = output
            results[name] = {'summary': summaries[name], 'samples': [[row[key] for key in COLUMNS] for row in rows]}
        runs.append({'report': path.resolve().relative_to(ROOT).as_posix(), 'reportSha256': sha(path),
                     'createdUtc': report['CreatedUtc'], 'revision': report['Revision'], 'host': report['Host'],
                     'browser': report.get('Browser'), 'playwright': report.get('Playwright'),
                     'wasmMemoryAfter': report.get('WasmMemoryAfter'),
                     'sources': report['Sources'], 'artifactManifestSha256': digest(report['Artifacts']),
                     'compiledManifestSha256': digest(report['Build']), 'results': results,
                     'allOperationBudgetsPass': all(s['withinBudgets'] for s in summaries.values()),
                     'limitations': report['Limitations']})
    receipt = {'format': 'openciv2-warm-performance-summary/1', 'budgetSha256': sha(BUDGET_PATH),
               'scope': 'Production operation latency/allocation measurements; runs with wasmMemoryAfter also conservatively bound managed peak memory. Application startup and final performance acceptance are separate.',
               'inputs': inputs, 'verifiedOutputs': outputs, 'sampleColumns': COLUMNS, 'runs': runs}
    text = json.dumps(receipt, indent=2)
    # Keep each numeric sample on one line while preserving readable manifests.
    text = re.sub(r'\[\n\s+(?:true|false),\n(?:\s+(?:null|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?),?\n)+\s*\]',
                  lambda match: json.dumps(json.loads(match.group())), text)
    if json.loads(text) != receipt: raise ValueError('Compact formatting changed receipt')
    args.output.write_text(text + '\n')
    print(f'Preserved {sum(len(r["samples"]) for run in runs for r in run["results"].values())} validated warm/native/browser samples.')


if __name__ == '__main__': main()
