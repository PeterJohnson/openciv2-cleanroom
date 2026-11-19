#!/usr/bin/env python3
"""Keep independent renderer runs separate; summarize without pooling frames."""
import argparse
from collections import Counter, defaultdict
import hashlib
import itertools
import json
import math
from pathlib import Path
import statistics


def distribution(values):
    values = sorted(values)
    return {**{f'p{p}': values[math.ceil(len(values) * p / 100) - 1] for p in (50, 95, 99)},
            'mean': statistics.mean(values), 'max': values[-1]}


def metrics(samples):
    # Counters and cache estimates remain separate from process/DRM memory.
    names = set.intersection(*(set(sample) for sample in samples))
    names = {name for name in names if name.endswith(('Ms', 'Bytes', 'Calls', 'Hits', 'Builds', 'Evictions', 'Admissions', 'Tiles', 'Pixels')) or
             name in ('ms', 'foregroundTraversals', 'foregroundCommands', 'textureCreates', 'textureDeletes',
                      'textureBinds', 'gpuResources', 'atlasBatches', 'damageRegions', 'preparedChunks', 'demandRasterLoads')}
    return {name: distribution([sample[name] for sample in samples]) for name in sorted(names)
            if all(isinstance(sample[name], (int, float)) and not isinstance(sample[name], bool) for sample in samples)}


def summarize(root):
    report = {'scope': 'Independent runs; no pooled percentiles. Native draw is CPU work inside the Skia lease, not full frame submission or presentation. '
              'Profile fence/stream timing is separate. Memory layers may overlap and are not summed.',
              'evidence': {}, 'native': [], 'cold': [], 'profile': [], 'pipeline': [], 'latency': [], 'replay': []}
    for stage in ('native', 'cold'):
        for path in sorted((root / stage).glob('*/*/run-*/*/native-performance.json')):
            document = json.loads(path.read_text())
            report['evidence'][str(path.relative_to(root))] = hashlib.sha256(path.read_bytes()).hexdigest()
            for row in document['results']:
                samples = row['samples']
                expected = 300 if stage == 'native' else 20
                if len(samples) != expected or (stage == 'native' and len(row['warmupSamples']) != 30):
                    raise ValueError(f'Incomplete acceptance series: {path}')
                entry = {key: row[key] for key in ('width', 'height', 'density', 'mode', 'run', 'warmup')}
                entry.update(variant=path.parent.name, samples=len(samples), metrics=metrics(samples),
                             source=str(path.relative_to(root)),
                             gpuCacheLimitBytes=sorted({sample['gpuCacheLimitBytes'] for sample in samples}))
                for name in ('processMemoryKiB', 'processGpuMemoryAfterSeries', 'processGraphicsEngineIncreaseNs'):
                    if name in row:
                        entry[name] = row[name]
                report[stage].append(entry)
    for stage in ('profile', 'pipeline'):
        for path in sorted((root / stage).glob('*/*/profile.json')):
            document = json.loads(path.read_text())
            if not document.get('recordedNavigationPulse'):
                raise ValueError(f'Profile did not restore recorded navigation pulse: {path}')
            report['evidence'][str(path.relative_to(root))] = hashlib.sha256(path.read_bytes()).hexdigest()
            for row in document['results']:
                if row['frames'] != 300 or row['warmup'] != 30 or len(row['samples']) != 300:
                    raise ValueError(f'Incomplete acceptance profile: {path}')
                entry = {key: row[key] for key in ('width', 'height', 'density', 'mode', 'run', 'variant', 'features', 'depth',
                                                 'warmup', 'frames', 'elapsedMs', 'throughputFps', 'graphicsEngineMsPerFrame',
                                                 'clockUncertaintyMs', 'cacheLimitBytes')}
                entry.update(metrics=metrics(row['samples']), source=str(path.relative_to(root)))
                report[stage].append(entry)
    for path in sorted((root / 'latency').glob('*/*/*/*/latency.json')):
        document = json.loads(path.read_text())
        report['evidence'][str(path.relative_to(root))] = hashlib.sha256(path.read_bytes()).hexdigest()
        for row in document['results']:
            if row['sent'] != 300 or row['observed'] != len(row['samples']) or not row['memorySamples']:
                raise ValueError(f'Incomplete latency series: {path}')
            entry = {key: value for key, value in row.items() if key not in ('observations', 'inputs', 'memorySamples', 'reads', 'samples')}
            entry.update(source=str(path.relative_to(root)), size=document['size'], variant=path.parent.parent.name,
                         density=int(path.relative_to(root / 'latency').parts[0][:-1]),
                         capture=document['capture'], memoryPeaks={key: max(s[key] for s in row['memorySamples'])
                         for key in ('rssBytes', 'residentBytes', 'requestedBytes', 'sharedBytes')
                         if all(key in s for s in row['memorySamples'])})
            entry['memoryPeaks']['drmBytes'] = {key: max(s['drmBytes'].get(key, 0) for s in row['memorySamples'])
                for key in sorted(set().union(*(s['drmBytes'] for s in row['memorySamples'])))}
            report['latency'].append(entry)
    for path in sorted(root.glob('replay-*/*/*/*/replay.json')):
        document = json.loads(path.read_text())
        if not document.get('recordedNavigationPulse'):
            raise ValueError(f'Replay did not restore recorded navigation pulse: {path}')
        report['evidence'][str(path.relative_to(root))] = hashlib.sha256(path.read_bytes()).hexdigest()
        for row in document['results']:
            comparisons = row['gpuComparisons']
            if not document['everyGpuFrame'] or len(comparisons) != row['verified'] or not all(c['freshGpuExact'] for c in comparisons):
                raise ValueError(f'Incomplete or failed strict replay: {path}')
            cross = [c['diff'] for c in comparisons if c['diff'] is not None]
            report['replay'].append({**{key: value for key, value in row.items() if key != 'gpuComparisons'},
                                     'source': str(path.relative_to(root)), 'exactFrames': len(comparisons),
                                     'cpuComparedFrames': len(cross),
                                     'cpuToleranceFailedFrames': sum(d['MeanChannelError'] > 2 or d['PixelsOver8'] > d['Pixels'] * .10 for d in cross),
                                     'cpuMaxMeanChannelError': max(d['MeanChannelError'] for d in cross),
                                     'cpuMaxPixelsOver8Fraction': max(d['PixelsOver8'] / d['Pixels'] for d in cross)})
    return report


def coverage(report):
    """Require the full default cohort; reject duplicates as well as missing cells."""
    sizes = ((1920, 1080), (3840, 2160), (5120, 2880))
    modes = ('pan', 'continuous-zoom', 'dirty-world')
    variants = ('cpu', 'gpu', 'candidate')
    expected = {}
    for stage, workloads in (('native', modes), ('cold', ('cold-first-frame', 'resource-recovery'))):
        expected[stage] = {(d, w, h, run, mode, variant)
            for d, (w, h), run, mode, variant in itertools.product((1, 2), sizes, (1, 2, 3), workloads, variants)}
    for stage, cells, features, depths in (
            ('profile', sizes, ('cpu', 'streamed', 'commands', 'mask-batch', 'sparse', 'candidate'), (1,)),
            ('pipeline', ((5120, 2880),), ('streamed', 'candidate'), (1, 2, 3))):
        expected[stage] = {(d, w, h, run, mode, variant, depth)
            for d, (w, h), run, mode, variant, depth in itertools.product(
                (1, 2), cells, (1, 2, 3), (*modes, 'local-dirty'), features, depths)}
    expected['latency'] = {(d, f'{w}x{h}', run, rate, variant, capture)
        for d, (w, h), run, rate, variant, capture in itertools.product(
            (1, 2), sizes, (1, 2, 3), (60, 120), variants, ('present', 'receipt'))}
    expected['replay'] = {(d, w, h, run, mode)
        for d, (w, h), run, mode in itertools.product((1, 2), sizes, (1, 2, 3),
            (*modes, 'cold-first-frame', 'resource-recovery'))}
    result = {}
    for stage, wanted in expected.items():
        keys = ('density', 'size', 'run', 'rate', 'variant', 'capture') if stage == 'latency' else (
            'density', 'width', 'height', 'run', 'mode')
        if stage not in ('latency', 'replay'):
            keys += ('variant',)
        if stage in ('profile', 'pipeline'):
            keys += ('depth',)
        found = [tuple(row[key] for key in keys) for row in report[stage]]
        result[stage] = {'expected': len(wanted), 'actual': len(found), 'duplicates': len(found) - len(set(found)),
                         'missing': sorted(wanted - set(found)), 'unexpected': sorted(set(found) - wanted)}
    for stage in ('native', 'cold'):
        if any(row['gpuCacheLimitBytes'] != [512 * 2**20] for row in report[stage]):
            raise ValueError(f'{stage}: unmatched native GPU cache ceilings')
    for stage in ('profile', 'pipeline'):
        if any(row['cacheLimitBytes'] != 512 * 2**20 for row in report[stage]):
            raise ValueError(f'{stage}: unmatched profile GPU cache ceilings')
    for row in report['replay']:
        expected_frames = 330 if row['mode'] in modes else 20
        if row['exactFrames'] != expected_frames or row['savedWarmupFrames'] != (30 if expected_frames == 330 else 0):
            raise ValueError(f'Wrong strict replay frame count: {row["source"]}')
    if any(row['gpuCacheLimitBytes'] != 512 * 2**20 for row in report['latency']):
        raise ValueError('Unmatched latency GPU cache ceilings')
    return result


def audit_native_inputs(root):
    fields = ('x', 'y', 'zoom', 'scaling', 'width', 'height', 'revision', 'tiles', 'objects', 'labels')
    differences = Counter()
    paired = 0
    for baseline in sorted((root / 'native').glob('*/*/run-*/gpu/native-performance.json')):
        original = json.loads(baseline.read_text())
        for variant in ('cpu', 'candidate'):
            path = baseline.parent.parent / variant / 'native-performance.json'
            if not path.exists():
                continue  # The coverage check reports missing cells separately.
            candidate = json.loads(path.read_text())
            if candidate['identity'] != original['identity'] or candidate['content'] != original['content']:
                raise ValueError(f'Unmatched source/content identities: {path}')
            rows = {row['mode']: row for row in candidate['results']}
            for source in original['results']:
                if source['mode'] not in rows:
                    continue
                target = rows[source['mode']]
                for index, (a, b) in enumerate(zip(source['warmupSamples'] + source['samples'],
                                                   target['warmupSamples'] + target['samples'], strict=True)):
                    paired += 1
                    for field in fields:
                        if a[field] != b[field]:
                            raise ValueError(f'Unmatched {field} in {path}: {source["mode"]} frame {index}')
                    if source['mode'] == 'dirty-world' and (a['updateX'], a['updateY']) != (b['updateX'], b['updateY']):
                        raise ValueError(f'Unmatched dirty-world focus in {path}')
                    for field in set(a['presentation']) | set(b['presentation']):
                        if a['presentation'].get(field) != b['presentation'].get(field):
                            differences[(variant, source['mode'], field)] += 1
    return {'pairedFramesIncludingWarmup': paired, 'matchingFields': fields,
            'differences': [{'variant': v, 'mode': m, 'field': f, 'frames': n}
                            for (v, m, f), n in sorted(differences.items())],
            'scope': 'Source/content, cameras, revisions and base populations match. Native real-time pulse and '
                     'explosion overlap/progress can differ. Controlled per-change attribution uses the same '
                     'saved presentation in every separate profile variant.'}


def tables(report):
    lines = ['# GPU optimization measurements', '',
             'Native timings below are the worst of three independent run p95s, in milliseconds. '
             'Dirty-world includes authored update, scene binding and draw. Other rows measure the map draw call inside the Skia lease. '
             'The real-time host can vary pulse/explosion phase; see `native-input-audit.json`. '
             'Full per-run p50/p95/p99, means, maxima and evidence hashes are in `summary.json`.', '',
             '| Physical size | Density | Workload | CPU | Original GPU | Candidate | Candidate change |',
             '|---|---:|---|---:|---:|---:|---:|']
    grouped = defaultdict(dict)
    for row in report['native']:
        key = (row['density'], row['width'], row['height'], row['mode'])
        grouped[key].setdefault(row['variant'], []).append(row)
    for (density, width, height, mode), variants in sorted(grouped.items()):
        if not all(len(variants.get(name, [])) == 3 for name in ('cpu', 'gpu', 'candidate')):
            continue
        metric = 'updateAndDrawMs' if mode == 'dirty-world' else 'ms'
        values = [max(row['metrics'][metric]['p95'] for row in variants[name]) for name in ('cpu', 'gpu', 'candidate')]
        lines.append(f'| {width}×{height} | {density}× | {mode} | {values[0]:.3f} | {values[1]:.3f} | '
                     f'{values[2]:.3f} | {100 * (values[2] / values[1] - 1):+.1f}% |')
    lines += ['', 'Profile results are separately instrumented and must not be mixed with the native timing table.', '',
              '| Physical size | Density | Workload | Variant | Submit p95 | GPU completion p95 | '
              'Observed fence p95 | GPU stream p95 | Draw allocation mean KiB | Upload mean KiB | Cache peak MiB |',
              '|---|---:|---|---|---:|---:|---:|---:|---:|---:|---:|']
    grouped = defaultdict(list)
    for row in report['profile']:
        grouped[(row['density'], row['width'], row['height'], row['mode'], row['variant'])].append(row)
    for (density, width, height, mode, variant), rows in sorted(grouped.items()):
        if len(rows) != 3:
            continue
        def maximum(name, stat='p95'):
            return max(row['metrics'][name][stat] for row in rows)
        lines.append(f'| {width}×{height} | {density}× | {mode} | {variant} | {maximum("submitMs"):.3f} | '
                     f'{maximum("gpuCompletedMs"):.3f} | {maximum("completedMs"):.3f} | {maximum("gpuStreamMs"):.3f} | '
                     f'{maximum("drawAllocatedBytes", "mean") / 1024:.1f} | {maximum("uploadBytes", "mean") / 1024:.1f} | '
                     f'{maximum("gpuCacheBytes", "max") / 2**20:.1f} |')
    lines += ['', 'Cold/recovery values are the worst run p95 (20 events per independent run). '
              'After-admission time ends at the first draw callback, not physical presentation.', '',
              '| Physical size | Density | Workload | Variant | Draw p95 | After admission p95 | Recovery input-to-receipt p95 |',
              '|---|---:|---|---|---:|---:|---:|']
    grouped = defaultdict(list)
    for row in report['cold']:
        grouped[(row['density'], row['width'], row['height'], row['mode'], row['variant'])].append(row)
    for (density, width, height, mode, variant), rows in sorted(grouped.items()):
        draw = max(row['metrics']['ms']['p95'] for row in rows)
        admission = f'{max(row["metrics"]["afterAdmissionMs"]["p95"] for row in rows):.3f}' if mode == 'cold-first-frame' else '—'
        recovery = f'{max(row["metrics"]["inputToReceiptMs"]["p95"] for row in rows):.3f}' if mode == 'resource-recovery' else '—'
        lines.append(f'| {width}×{height} | {density}× | {mode} | {variant} | {draw:.3f} | {admission} | {recovery} |')
    lines += ['', 'Pipelining is an offscreen throughput/queue experiment. FPS shows the range of '
              'the three runs; other values are worst run p95 or peak. It is not an application input queue.', '',
              '| Density (5K) | Workload | Variant | Depth | FPS range | Submit p95 | GPU completion p95 | Cache peak MiB |',
              '|---:|---|---|---:|---:|---:|---:|---:|']
    grouped = defaultdict(list)
    for row in report['pipeline']:
        grouped[(row['density'], row['mode'], row['variant'], row['depth'])].append(row)
    for (density, mode, variant, depth), rows in sorted(grouped.items()):
        fps = [row['throughputFps'] for row in rows]
        submit = max(row['metrics']['submitMs']['p95'] for row in rows)
        complete = max(row['metrics']['gpuCompletedMs']['p95'] for row in rows)
        cache = max(row['metrics']['gpuCacheBytes']['max'] for row in rows) / 2**20
        lines.append(f'| {density}× | {mode} | {variant} | {depth} | {min(fps):.1f}–{max(fps):.1f} | '
                     f'{submit:.3f} | {complete:.3f} | {cache:.1f} |')
    lines += ['', 'Input capture uses independent 300-event streams at 60/120 Hz. Values are worst run '
              'p95 and ranges of observed-event fractions. `present` includes image readback; `receipt` reads '
              'draw metadata without waiting for GPU completion, through the host\'s 25 ms snapshot publisher. '
              'Its observed fraction is not presented FPS. Neither is physical scanout latency. '
              'Memory values are sampled owned-process peaks, not cache budgets; RSS and DRM overlap.', '',
              '| Physical size | Density | Variant | Capture | Input Hz | Observed fraction | Latency p95 | '
              'RSS peak MiB | DRM resident peak MiB |',
              '|---|---:|---|---|---:|---:|---:|---:|---:|']
    grouped = defaultdict(list)
    for row in report['latency']:
        grouped[(row['density'], row['size'], row['variant'], row['capture'], row['rate'])].append(row)
    for (density, size, variant, capture, rate), rows in sorted(grouped.items()):
        metric = 'inputToImageMs' if capture == 'present' else 'inputToReceiptMs'
        latency = max(row[metric]['p95'] for row in rows)
        observed = [row['observed'] / row['sent'] for row in rows]
        rss = max(row['memoryPeaks']['rssBytes'] for row in rows) / 2**20
        resident = max(row['memoryPeaks']['residentBytes'] for row in rows) / 2**20
        lines.append(f'| {size.replace("x", "×")} | {density}× | {variant} | {capture} | {rate} | '
                     f'{min(observed):.1%}–{max(observed):.1%} | {latency:.3f} | {rss:.1f} | {resident:.1f} |')
    return '\n'.join(lines) + '\n'


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root', type=Path)
    parser.add_argument('--allow-incomplete', action='store_true', help='Emit explicit missing-cell lists for interim analysis')
    args = parser.parse_args()
    result = summarize(args.root)
    result['coverage'] = coverage(result)
    if not args.allow_incomplete and any(c['missing'] or c['unexpected'] or c['duplicates'] for c in result['coverage'].values()):
        raise ValueError(f'Incomplete or mismatched acceptance cohort: {result["coverage"]}')
    result['nativeInputAudit'] = audit_native_inputs(args.root)
    (args.root / 'native-input-audit.json').write_text(json.dumps(result['nativeInputAudit'], indent=2) + '\n')
    (args.root / 'summary.json').write_text(json.dumps(result, indent=2) + '\n')
    (args.root / 'measurements.md').write_text(tables(result))
    print({stage: len(result[stage]) for stage in ('native', 'cold', 'profile', 'pipeline', 'latency', 'replay')})
