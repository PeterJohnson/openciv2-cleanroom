#!/usr/bin/env python3
"""Summarize diagnostic runs without pooling their tails or hiding failed checks."""
import argparse
import hashlib
import json
import math
from pathlib import Path


def quantiles(values):
    values = sorted(values)
    return {key: values[math.ceil(len(values) * q) - 1]
            for key, q in [('p50', .5), ('p95', .95), ('p99', .99), ('max', 1)]} if values else None


def summarize(root):
    rows = []
    for path in sorted(root.glob('*/performance.json')):
        report = json.loads(path.read_text())
        checks_path = path.with_name('pixel-comparisons.json')
        checks = json.loads(checks_path.read_text()) if checks_path.exists() else None
        for result in report['results']:
            if not result.get('summary'):
                continue
            samples = result['samples']
            metrics = dict(result['summary'])
            for key in ['ms', 'callbackMs', 'terrainMs', 'foregroundMs', 'foregroundPlanMs', 'foregroundSubmitMs',
                        'groundBuildMs', 'groundSelectorMs', 'texturePrepareMs', 'drawAllocatedBytes']:
                metrics[key] = quantiles(s['draw'][key] for s in samples if s['draw'].get(key) is not None)
            metrics['rafCallbackCpuMs'] = quantiles(max((r['cpuMs'] for r in s['raf']), default=0) for s in samples)
            # A frame can have multiple callbacks. Maximum query interval is an
            # observed interval, never total GPU utilization or physical latency.
            metrics['gpuQueryMs'] = quantiles(max(values) for s in samples
                                             if (values := [r['gpuMs'] for r in s['raf'] if r.get('gpuMs') is not None]))
            rows.append(dict(cohort=path.parent.name, reportSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                             completed=report['completed'], diagnostic=report['diagnostic'],
                             backend=report['backend'], renderer=report['renderer'], gpuFeatures=report.get('gpuFeatures'),
                             size=report['physicalViewport'], density=report['density'], run=result['run'],
                             detail=result['detail'], mode=result['mode'], count=len(samples), warmup=report['warmup'],
                             coverage=result.get('coverage'), metrics=metrics,
                             memory=result.get('memory'), exactEndpoints=checks and checks['passed'],
                             measuredDrawTargetMet=result['warmDrawTargetMet'], acceptancePassed=False))
    result = dict(format='openciv2-browser-map-performance-summary/1',
                  scope='Diagnostic; sampled endpoint correctness and abbreviated workloads do not establish full performance acceptance.', rows=rows)
    (root / 'summary.json').write_text(json.dumps(result, indent=2) + '\n')
    for row in rows:
        m = row['metrics']['frameWorkMs']
        print(f"{row['cohort']} run {row['run']} {row['detail']}/{row['mode']}: p50 {m['p50']:.2f}, p95 {m['p95']:.2f}, p99 {m['p99']:.2f} ms")


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root', type=Path)
    summarize(parser.parse_args().root)
