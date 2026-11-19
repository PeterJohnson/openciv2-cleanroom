#!/usr/bin/env python3
"""Interleave frozen renderer candidates at matched physical dimensions and densities.

Native timing uses three independent 30+300 changed-frame runs. Profiling is a
separate invocation; it consumes the saved warmup as well as measured inputs.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
BASELINE = 'Atlas,Textures,Encoded,Direct'
VARIANTS = {
    'cpu': ('cpu', 'None'),
    'gpu': ('gpu', BASELINE),
    'commands': ('gpu', BASELINE + ',Commands'),
    'masks': ('gpu', BASELINE + ',Commands,Masks'),
    'mask-batch': ('gpu', BASELINE + ',Commands,Masks,MaskBatch'),
    'stable': ('gpu', BASELINE + ',Commands,StableAtlas'),
    'stable-masks': ('gpu', BASELINE + ',Commands,Masks,StableAtlas'),
    'pan-phase': ('gpu', BASELINE + ',Commands,PanCache,PanPhase,Sparse'),
    'tier-cache': ('gpu', BASELINE + ',Commands,PanCache,TierCache,Sparse'),
    'ground-plans': ('gpu', BASELINE + ',Commands,Terrain,GroundPlans,Sparse'),
    'candidate': ('gpu', BASELINE + ',Commands,Masks,MaskBatch,Sparse'),
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--stage', choices=('native', 'cold', 'profile', 'pipeline', 'latency', 'replay'), required=True)
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--fonts', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--binary', type=Path, required=True)
    parser.add_argument('--source-root', type=Path)
    parser.add_argument('--variant', action='append', choices=tuple(VARIANTS))
    parser.add_argument('--size', action='append', choices=('1920x1080', '3840x2160', '5120x2880'))
    parser.add_argument('--density', action='append', type=int, choices=(1, 2))
    parser.add_argument('--input-root', type=Path, help='Completed native output to replay under GL instrumentation')
    parser.add_argument('--probe', type=Path, help='Compiled MapProbe DLL for profiling')
    parser.add_argument('--profile-variants', default='cpu,streamed,commands,mask-batch,sparse,candidate')
    parser.add_argument('--resume', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    binary = args.binary.resolve()
    binary_hash = hashlib.sha256(binary.read_bytes()).hexdigest()
    sizes = args.size or ('1920x1080', '3840x2160', '5120x2880')
    densities = args.density or (1, 2)

    def execute(folder, command, environment):
        folder.mkdir(parents=True, exist_ok=True)
        invocation = [sys.executable, str(ROOT / 'tools/dotnet/run_xwayland_virtual.py'),
                      '--compositor-log', str(folder / 'compositor.log'), *command]
        (folder / 'invocation.json').write_text(json.dumps({'command': invocation, 'environment': environment,
            'binarySha256': binary_hash, 'driverSha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}, indent=2) + '\n')
        print(f'Running {folder}', flush=True)
        subprocess.run(invocation, cwd=ROOT, env={**os.environ, **environment}, check=True)
        if hashlib.sha256(binary.read_bytes()).hexdigest() != binary_hash:
            raise RuntimeError('Frozen desktop binary changed during measurement')

    if args.stage in ('profile', 'pipeline'):
        if not args.input_root or not args.probe:
            parser.error('profile requires --input-root and --probe')
        for density in densities:
            for size in sizes:
                results = []
                for run in (1, 2, 3):
                    receipt = args.input_root / f'{density}x/{size}/run-{run}/gpu/native-performance.json'
                    report = json.loads(receipt.read_text())
                    results.extend(row for row in report['results'] if row['mode'] in ('pan', 'continuous-zoom', 'dirty-world'))
                results += [{**row, 'mode': 'local-dirty'} for row in results if row['mode'] == 'dirty-world']
                if any(len(row['samples']) != 300 or len(row.get('warmupSamples', ())) != 30 for row in results):
                    raise RuntimeError('Profiling needs complete 30+300 native histories')
                folder = args.output / f'{density}x/{size}'
                folder.mkdir(parents=True, exist_ok=True)
                source = folder / 'input.json'
                source.write_text(json.dumps({'results': results}) + '\n')
                execute(folder, ['dotnet', str(args.probe.resolve()), str(args.assets.resolve()), str(args.fonts.resolve()),
                    str(folder / 'profile.json'), '--gpu-experiments', str(source)],
                    {'OPENCIV2_PROFILE_VARIANTS': args.profile_variants, 'OPENCIV2_PROFILE_FRAMES': '300',
                     'OPENCIV2_PROFILE_DEPTHS': '1,2,3' if args.stage == 'pipeline' else '1',
                     'DOTNET_TieredCompilation': '0', 'OPENCIV2_MAP_GPU_CACHE_MIB': '512'})
        return

    variants = args.variant or ['cpu', 'gpu', 'candidate']
    if args.stage == 'replay':
        if not args.input_root or not args.probe:
            parser.error('replay requires --input-root and --probe')
        for density in densities:
            for size in sizes:
                for variant in variants:
                    composition, features = VARIANTS[variant]
                    results = []
                    for run in (1, 2, 3):
                        receipt = args.input_root / f'{density}x/{size}/run-{run}/{variant}/native-performance.json'
                        report = json.loads(receipt.read_text())
                        results.extend(report['results'])
                    folder = args.output / f'{density}x/{size}/{variant}'
                    folder.mkdir(parents=True, exist_ok=True)
                    source = folder / 'input.json'
                    source.write_text(json.dumps({**report, 'results': results}) + '\n')
                    execute(folder, ['dotnet', str(args.probe.resolve()), str(args.assets.resolve()), str(args.fonts.resolve()),
                        str(folder / 'replay.json'), '--replay-gpu-experiment' if composition == 'gpu' else '--replay-native-navigation', str(source)],
                        {'OPENCIV2_MAP_GPU_FEATURES': features, 'OPENCIV2_REPLAY_ALL': '1',
                         'DOTNET_TieredCompilation': '0', 'OPENCIV2_MAP_GPU_CACHE_MIB': '512'})
        return

    if args.stage == 'latency':
        for density in densities:
            for size in sizes:
                for variant in variants:
                    composition, features = VARIANTS[variant]
                    for capture in ('present', 'receipt'):
                        folder = args.output / f'{density}x/{size}/{variant}/{capture}'
                        command = [sys.executable, str(ROOT / 'tools/dotnet/measure_map_latency.py'),
                            '--assets', str(args.assets.resolve()), '--fonts', str(args.fonts.resolve()), '--output', str(folder),
                            '--size', size, '--expected-density', str(density), '--native-aot', '--frames', '300', '--runs', '3',
                            '--expected-cache-mib', '512',
                            '--capture', capture, '--direct-input']
                        if args.source_root:
                            command += ['--source-root', str(args.source_root.resolve())]
                        command += ['--adapter', str(binary), '--map-renderer', composition, '--map-gpu-cache-mib', '512']
                        execute(folder, command, {'OPENCIV2_MAP_EXPERIMENT': '1', 'OPENCIV2_MAP_GPU_FEATURES': features,
                            'OPENCIV2_MAP_COMPOSITION': composition, 'OPENCIV2_MAP_LATENCY': '1',
                            'OPENCIV2_MAP_GPU_CACHE_MIB': '512', 'OPENCIV2_MAP_GPU_SYNC': '0',
                            'AVALONIA_GLOBAL_SCALE_FACTOR': str(density), 'DOTNET_TieredCompilation': '0'})
        return

    for run in (1, 2, 3):
        order = variants[run % len(variants):] + variants[:run % len(variants)]
        for density in densities:
            for size in sizes:
                for variant in order:
                    composition, features = VARIANTS[variant]
                    folder = args.output / f'{density}x/{size}/run-{run}/{variant}'
                    modes = ('pan', 'continuous-zoom', 'dirty-world') if args.stage == 'native' else ('cold-first-frame', 'resource-recovery')
                    saved = folder / 'native-performance.json'
                    if args.resume and saved.exists():
                        prior = json.loads(saved.read_text())
                        rows = prior['results']
                        count = 300 if args.stage == 'native' else 20
                        invocation = json.loads((folder / 'invocation.json').read_text())
                        if (invocation['binarySha256'] == binary_hash and
                            invocation['environment']['OPENCIV2_MAP_GPU_FEATURES'] == features and len(rows) == len(modes) and
                            {row['mode'] for row in rows} == set(modes) and all(len(row['samples']) == count and
                            row['run'] == run and row['density'] == density and
                            (args.stage != 'native' or len(row.get('warmupSamples', [])) == 30) and
                            all(sample['gpuCacheLimitBytes'] == 512 * 2**20 for sample in row['samples']) for row in rows)):
                            print(f'Keeping complete {folder}', flush=True)
                            continue
                        raise RuntimeError(f'Incomplete or mismatched resume receipt: {folder}')
                    command = [sys.executable, str(ROOT / 'tools/dotnet/measure_map_native.py'),
                        '--assets', str(args.assets.resolve()), '--fonts', str(args.fonts.resolve()), '--output', str(folder),
                        '--size', size, '--expected-density', str(density), '--native-aot', '--virtual-gpu',
                        '--composition', composition, '--run-index', str(run), '--expected-cache-mib', '512']
                    if args.source_root:
                        command += ['--source-root', str(args.source_root.resolve())]
                    for mode in modes:
                        command += ['--mode', mode]
                    if args.stage == 'cold':
                        command += ['--cold-detail', 'half']
                    command += ['--adapter', str(binary), '--map-renderer', composition, '--map-gpu-cache-mib', '512']
                    execute(folder, command, {'OPENCIV2_MAP_EXPERIMENT': '1', 'OPENCIV2_MAP_GPU_FEATURES': features,
                        'OPENCIV2_MAP_GPU_CACHE_MIB': '512', 'OPENCIV2_MAP_GPU_SYNC': '0',
                        'AVALONIA_GLOBAL_SCALE_FACTOR': str(density), 'DOTNET_TieredCompilation': '0'})


if __name__ == '__main__':
    main()
