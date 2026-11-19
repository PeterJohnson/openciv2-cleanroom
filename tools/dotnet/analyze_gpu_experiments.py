#!/usr/bin/env python3
"""Summarize independent runs without pooling frame distributions across runs."""
import argparse
from collections import defaultdict
import json
import math
from pathlib import Path
import statistics as stats


def summarize(root):
    result = {}
    for stage in ('profile', 'pipeline', 'cache', 'offload-followup', 'foreground-control'):
        grouped = defaultdict(list)
        for path in (root / stage).glob('*/*/profile.json'):
            for row in json.loads(path.read_text())['results']:
                grouped[(row['width'], row['height'], row['mode'], row['variant'], row['depth'], row['cacheLimitBytes'])].append(row)
        rows = []
        for (width, height, mode, variant, depth, budget), runs in sorted(grouped.items()):
            entry = dict(size=f'{width}x{height}', mode=mode, variant=variant, depth=depth, budgetMiB=budget / 2**20,
                         runs=len(runs), frames=sum(r['frames'] for r in runs),
                         fps=stats.median(r['throughputFps'] for r in runs),
                         observedP95Ms=stats.median(r['completedMs']['p95'] for r in runs),
                         gpuP50Ms=stats.median(r['gpuCompletedMs']['p50'] for r in runs),
                         gpuP95Ms=stats.median(r['gpuCompletedMs']['p95'] for r in runs),
                         gpuP95Range=[min(r['gpuCompletedMs']['p95'] for r in runs), max(r['gpuCompletedMs']['p95'] for r in runs)],
                         submitP95Ms=stats.median(r['submitMs']['p95'] for r in runs),
                         streamP95Ms=stats.median(r['gpuStreamMs']['p95'] for r in runs),
                         engineMs=stats.median(r['graphicsEngineMsPerFrame'] for r in runs),
                         clockUncertaintyMs=max(r['clockUncertaintyMs'] for r in runs),
                         cachePeakMiB=max(s['gpuCacheBytes'] for r in runs for s in r['samples']) / 2**20)
            for field in ('drawCalls', 'uploadCalls', 'uploadBytes', 'textureBinds', 'textureCreates', 'textureDeletes', 'demandRasterLoads'):
                entry[field] = stats.median(stats.mean(s[field] for s in r['samples']) for r in runs)
            for field in ('atlasBytes', 'encodedSourceBytes', 'groundCacheBytes', 'textureCacheBytes'):
                entry[field] = max(s[field] for r in runs for s in r['samples'])
            for field in ('terrainMs', 'foregroundMs', 'texturePrepareMs'):
                entry[field + 'P95'] = stats.median(sorted(s[field] for s in r['samples'])[math.ceil(len(r['samples']) * .95) - 1]
                                                   for r in runs)
            rows.append(entry)
        result[stage] = rows

    native = []
    for size in ('1920x1080', '3840x2160', '5120x2880'):
        paths = {mode: root / 'native' / size / mode / 'native-performance.json' for mode in ('cpu', 'gpu')}
        if not all(path.exists() for path in paths.values()):
            continue
        reports = {mode: json.loads(path.read_text()) for mode, path in paths.items()}
        for workload in ('pan', 'continuous-zoom', 'dirty-world'):
            entry = {'size': size, 'mode': workload}
            for mode, report in reports.items():
                runs = [r for r in report['results'] if r['mode'] == workload]
                if not runs:
                    break
                field = 'updateAndDrawMs' if workload == 'dirty-world' else 'drawMs'
                entry[mode] = {'runs': len(runs), 'frames': sum(len(r['samples']) for r in runs),
                               'p50': stats.median(r[field]['p50'] for r in runs),
                               'p95': stats.median(r[field]['p95'] for r in runs),
                               'p95Range': [min(r[field]['p95'] for r in runs), max(r[field]['p95'] for r in runs)],
                               'p99': stats.median(r[field]['p99'] for r in runs),
                               'cachePeakMiB': max(s['gpuCacheBytes'] for r in runs for s in r['samples']) / 2**20,
                               'features': sorted({s['gpuFeatures'] for r in runs for s in r['samples']})}
            if 'cpu' in entry and 'gpu' in entry:
                entry['speedup'] = entry['cpu']['p95'] / entry['gpu']['p95']
                native.append(entry)
    result['native'] = native

    latency = []
    for path in (root / 'latency').glob('*/*/*/latency.json'):
        report = json.loads(path.read_text())
        for rate in (60, 120):
            runs = [r for r in report['results'] if r['rate'] == rate]
            if not runs:
                continue
            latency.append({'size': report['size'], 'variant': path.parent.parent.name,
                            'capture': report.get('capture', 'poll'), 'directInput': report.get('directInput', False),
                            'synchronized': report['synchronized'], 'rate': rate, 'runs': len(runs),
                            'p50': stats.median(r['inputToImageMs']['p50'] for r in runs),
                            'p95': stats.median(r['inputToImageMs']['p95'] for r in runs),
                            'p95Range': [min(r['inputToImageMs']['p95'] for r in runs), max(r['inputToImageMs']['p95'] for r in runs)],
                            'observed': sum(r['observed'] for r in runs), 'sent': sum(r['sent'] for r in runs),
                            'duplicateStampTransitions': sum(r['duplicateStampTransitions'] for r in runs),
                            'outOfOrderStampTransitions': sum(r['outOfOrderStampTransitions'] for r in runs),
                            'injectionLatenessP95': max(r['injectionLatenessMs']['p95'] for r in runs),
                            'readbackP95': max(r['readbackMs']['p95'] for r in runs),
                            'residentPeakMiB': max(s['residentBytes'] for r in runs for s in r['memorySamples']) / 2**20,
                            'requestedPeakMiB': max(s['requestedBytes'] for r in runs for s in r['memorySamples']) / 2**20,
                            'rssPeakMiB': max(s['rssBytes'] for r in runs for s in r['memorySamples']) / 2**20,
                            'memorySampleMaxMs': max(s['sampleMs'] for r in runs for s in r['memorySamples'])})
    result['latency'] = sorted(latency, key=lambda r: (r['size'], r['rate'], r['variant'], r['synchronized']))

    controls = []
    for stage in ('latency-controls', 'latency-stride4', 'latency-receipts'):
        for path in (root / stage).glob('*/latency.json'):
            report = json.loads(path.read_text())
            for rate in sorted({r['rate'] for r in report['results']}):
                runs = [r for r in report['results'] if r['rate'] == rate]
                metric = 'inputToReceiptMs' if report['capture'] == 'receipt' else 'inputToImageMs'
                cost = 'metadataReadMs' if report['capture'] == 'receipt' else 'readbackMs'
                entry = {'stage': stage, 'variant': path.parent.name, 'size': report['size'],
                         'capture': report['capture'], 'presentStride': report.get('presentStride', 1),
                         'directInput': report['directInput'], 'rate': rate, 'runs': len(runs), 'metric': metric,
                         'p95': stats.median(r[metric]['p95'] for r in runs),
                         'p95Range': [min(r[metric]['p95'] for r in runs), max(r[metric]['p95'] for r in runs)],
                         'observed': sum(r['observed'] for r in runs), 'sent': sum(r['sent'] for r in runs),
                         'sampleCostP95Max': max(r[cost]['p95'] for r in runs)}
                if metric == 'inputToReceiptMs':
                    entry.update(uiInputToDrawP95=stats.median(r['uiInputToDrawMs']['p95'] for r in runs),
                                 mapDrawP95=stats.median(r['mapDrawMs']['p95'] for r in runs),
                                 presentHz=stats.median(r['presentNotificationsPerSecond'] for r in runs))
                controls.append(entry)
    result['latencyControls'] = controls

    result['nativeCacheFollowup'] = []
    path = root / 'native-cache-768/native-performance.json'
    if path.exists():
        runs = json.loads(path.read_text())['results']
        result['nativeCacheFollowup'].append({'size': '5120x2880', 'mode': 'continuous-zoom', 'runs': len(runs),
            'frames': sum(len(r['samples']) for r in runs), 'budgetMiB': 768,
            'p50': stats.median(r['drawMs']['p50'] for r in runs), 'p95': stats.median(r['drawMs']['p95'] for r in runs),
            'p95Range': [min(r['drawMs']['p95'] for r in runs), max(r['drawMs']['p95'] for r in runs)],
            'cachePeakMiB': max(r['gpuCachePeakBytes'] for r in runs) / 2**20})

    matrices = []
    for path in (root / 'matrices').glob('*/gpu-matrix.json'):
        rows = json.loads(path.read_text())['results']
        matrices.append({'variant': path.parent.name, 'cases': len(rows), 'exact': sum(r['freshGpuExact'] for r in rows),
                         'maxFreshChannelError': max(r['freshGpuDiff']['MaxChannelError'] for r in rows),
                         'maxFreshMeanError': max(r['freshGpuDiff']['MeanChannelError'] for r in rows),
                         'maxCpuMeanError': max(r['diff']['MeanChannelError'] for r in rows)})
    result['matrices'] = matrices
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = summarize(args.root)
    path = args.output or args.root / 'summary.json'
    path.write_text(json.dumps(report, indent=2) + '\n')
    print({stage: len(rows) for stage, rows in report.items()})
