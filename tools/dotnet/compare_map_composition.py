#!/usr/bin/env python3
"""Compare CPU and experimental GPU composition using the existing native input workloads."""
import argparse
import json
import os
from pathlib import Path
import statistics
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[2]


def summarize(root):
    reports = {}
    for path in root.glob('*/*/native-performance.json'):
        report = json.loads(path.read_text())
        reports[(path.parent.parent.name, path.parent.name)] = report
    rows = []
    for size in ('1920x1080', '3840x2160', '5120x2880'):
        if any((size, mode) not in reports for mode in ('cpu', 'gpu')):
            continue
        for workload in ('pan', 'continuous-zoom', 'dirty-world'):
            row = {'size': size, 'workload': workload}
            metric = 'updateAndDrawMs' if workload == 'dirty-world' else 'drawMs'
            for mode in ('cpu', 'gpu'):
                series = [r for r in reports[size, mode]['results'] if r['mode'] == workload]
                row[mode] = [{'run': r['run'], 'samples': len(r['samples']), **r[metric],
                              'gpuWaitP95': r['gpuWaitMs']['p95']} for r in series]
            if not row['cpu'] or not row['gpu']:
                continue
            row['speedup'] = statistics.median(r['p95'] for r in row['cpu']) / statistics.median(r['p95'] for r in row['gpu'])
            rows.append(row)
    result = {'format': 'openciv2-map-composition-comparison/1',
              'scope': 'Matched Native AOT host input workloads. Draw work includes synchronous GPU completion; dirty work also includes authored update and scene binding. Physical presentation is unmeasured.',
              'results': rows}
    (root / 'comparison.json').write_text(json.dumps(result, indent=2) + '\n')
    for row in rows:
        cpu, gpu = (statistics.median(r['p95'] for r in row[mode]) for mode in ('cpu', 'gpu'))
        print(f'{row["size"]} {row["workload"]}: CPU {cpu:.2f}, GPU {gpu:.2f} ms p95; {row["speedup"]:.2f}x', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--assets', type=Path)
    parser.add_argument('--fonts', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--summarize-only', action='store_true')
    parser.add_argument('--diagnostic', action='store_true', help='One short 30-warmup/54-frame series, not acceptance')
    parser.add_argument('--gpu-features', default='None', help='Comma-separated experimental GPU features')
    parser.add_argument('--adapter', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    if args.summarize_only:
        summarize(args.output)
        return
    if not args.assets or not args.fonts or not args.adapter:
        parser.error('--assets, --fonts and --adapter are required to run')
    for size, density, order in [('1920x1080', 1, ('cpu', 'gpu')), ('3840x2160', 2, ('gpu', 'cpu')),
                                 ('5120x2880', 2, ('cpu', 'gpu'))]:
        for mode in order:
            output = args.output / size / mode
            env = os.environ.copy()
            env.update(OPENCIV2_MAP_EXPERIMENT='1', OPENCIV2_MAP_COMPOSITION=mode, OPENCIV2_MAP_GPU_SYNC='1', OPENCIV2_MAP_GPU_FEATURES=args.gpu_features,
                       AVALONIA_GLOBAL_SCALE_FACTOR=str(density))
            command = [sys.executable, str(ROOT / 'tools/dotnet/run_xwayland_virtual.py'),
                       '--compositor-log', str(output / 'compositor.log'),
                       sys.executable, str(ROOT / 'tools/dotnet/measure_map_native.py'),
                       '--assets', str(args.assets), '--fonts', str(args.fonts), '--output', str(output),
                       '--size', size, '--expected-density', str(density), '--native-aot', '--virtual-gpu',
                       '--composition', mode, '--gpu-sync', '--mode', 'pan', '--mode', 'continuous-zoom', '--mode', 'dirty-world']
            if args.diagnostic:
                command += ['--diagnostic', '--diagnostic-warmup', '30', '--diagnostic-frames', '54']
            command += ['--adapter', *args.adapter]
            print(f'Running {size} {mode}', flush=True)
            subprocess.run(command, check=True, env=env, cwd=ROOT)
            summarize(args.output)


if __name__ == '__main__':
    main()
