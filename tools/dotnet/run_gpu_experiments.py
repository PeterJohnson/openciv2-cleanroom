#!/usr/bin/env python3
"""Reproduce GPU profiling, cache-budget, pipeline and window-latency experiments."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
from run_application import validate

ROOT = Path(__file__).resolve().parents[2]
SIZES = ('1920x1080', '3840x2160', '5120x2880')


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--stage', choices=('profile', 'pipeline', 'cache', 'matrix', 'latency'), required=True)
    p.add_argument('--assets', type=Path, required=True)
    p.add_argument('--fonts', type=Path, required=True)
    p.add_argument('--fixtures', type=Path)
    p.add_argument('--baseline', type=Path, required=True, help='Existing native benchmark root containing SIZE/gpu/native-performance.json')
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--frames', type=int, default=120)
    p.add_argument('--variants', help='Override the profiling variant list')
    p.add_argument('--size', action='append', choices=SIZES)
    p.add_argument('--latency-capture', choices=('poll', 'present'), default='poll')
    p.add_argument('--resume', action='store_true', help='Skip complete, matching latency configurations; retain each receipt\'s driver identity')
    p.add_argument('--adapter', nargs=argparse.REMAINDER, help='Native desktop command for latency; otherwise dotnet and the MapProbe DLL')
    args = p.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    probe = args.adapter or ['dotnet', str(ROOT / 'tests/OpenCiv2.MapProbe/bin/Release/net11.0/OpenCiv2.MapProbe.dll')]

    def run(folder, command, **environment):
        folder.mkdir(parents=True, exist_ok=True)
        env = os.environ.copy()
        for name in ('OPENCIV2_MAP_GPU_FEATURES', 'OPENCIV2_MAP_GPU_CACHE_MIB', 'OPENCIV2_PROFILE_VARIANTS', 'OPENCIV2_PROFILE_DEPTHS'):
            env.pop(name, None)
        env.update(DOTNET_TieredCompilation='0', **{key: str(value) for key, value in environment.items()})
        invocation = [sys.executable, str(ROOT / 'tools/dotnet/run_xwayland_virtual.py'),
                      '--compositor-log', str(folder / 'compositor.log'), *command]
        (folder / 'invocation.json').write_text(json.dumps({'command': invocation, 'environment': environment}, indent=2) + '\n')
        print(f'Running {folder}', flush=True)
        subprocess.run(invocation, check=True, env=env, cwd=ROOT)

    if args.stage == 'matrix':
        if not args.fixtures:
            p.error('--fixtures is required for matrix checks')
        for name, features in [('baseline', 'None'), ('atlas-direct', 'Atlas,Direct'), ('offload', 'Offload'),
                               ('retain', 'Retain'), ('combined', 'Atlas,Offload,Retain,Direct'),
                               ('atlas-textures', 'Atlas,Textures,Direct'), ('atlas-terrain', 'Atlas,Terrain,Direct'),
                               ('streamed', 'Atlas,Textures,Encoded,Direct'), ('full-streamed', 'Atlas,Offload,Encoded,Direct'),
                               ('textures-direct', 'Textures,Encoded,Direct')]:
            if args.variants and name not in args.variants.split(','):
                continue
            folder = args.output / name
            run(folder, [*probe, str(args.assets), str(args.fonts), str(folder), '--gpu-experiment-matrix', str(args.fixtures)],
                OPENCIV2_MAP_GPU_FEATURES=features)
        return

    for size in args.size or SIZES:
        source = args.baseline / size / 'gpu/native-performance.json'
        report = json.loads(source.read_text())
        if args.stage == 'profile':
            # Preserve recorded global edits and add an explicitly distinct,
            # tile-only workload using the same camera and input cadence.
            report['results'] += [{**row, 'mode': 'local-dirty'} for row in report['results'] if row['mode'] == 'dirty-world']
        if args.stage == 'pipeline':
            report['results'] = [row for row in report['results'] if row['mode'] in ('pan', 'continuous-zoom')]
        if args.stage == 'cache':
            if size != '5120x2880':
                continue
            report['results'] = [row for row in report['results'] if row['mode'] == 'continuous-zoom']
        input_path = args.output / (size + '-input.json')
        input_path.write_text(json.dumps(report) + '\n')
        if args.stage in ('profile', 'pipeline', 'cache'):
            defaults = {'profile': 'cpu,gpu,atlas,offload,retain,direct,combined,atlas-direct,atlas-textures,atlas-terrain,streamed',
                        'pipeline': 'cpu,gpu,atlas-direct,streamed', 'cache': 'gpu,atlas-direct,offload,streamed'}
            for budget in ((256, 512, 768) if args.stage == 'cache' else (512,)):
                folder = args.output / size / (str(budget) if args.stage == 'cache' else 'results')
                run(folder, [*probe, str(args.assets), str(args.fonts), str(folder / 'profile.json'), '--gpu-experiments', str(input_path)],
                    OPENCIV2_PROFILE_FRAMES=args.frames, OPENCIV2_PROFILE_VARIANTS=args.variants or defaults[args.stage],
                    OPENCIV2_PROFILE_DEPTHS='1,2,3' if args.stage == 'pipeline' else '1', OPENCIV2_MAP_GPU_CACHE_MIB=budget)
        else:
            if not args.adapter:
                p.error('--adapter must identify the Native AOT desktop executable for latency')
            density = 1 if size == SIZES[0] else 2
            for name, composition, features in [('cpu', 'cpu', 'None'), ('gpu', 'gpu', 'None'), ('streamed', 'gpu', 'Atlas,Textures,Encoded,Direct')]:
                for sync in (1, 0):
                    folder = args.output / size / name / ('sync' if sync else 'pipeline')
                    saved = folder / 'latency.json'
                    if args.resume and saved.exists():
                        prior = json.loads(saved.read_text())
                        runs = prior['results']
                        if (prior.get('capture') == args.latency_capture and prior.get('presentStride', 1) == 1 and prior['size'] == size and
                                prior['composition'] == composition and prior['features'] == features and
                                prior['synchronized'] == bool(sync) and len(runs) == 6 and
                                {(r['run'], r['rate']) for r in runs} == {(n, hz) for n in (1, 2, 3) for hz in (60, 120)} and
                                all(r['sent'] == args.frames for r in runs)):
                            validate(prior['identity'], native_aot=True)
                            print(f'Keeping complete {folder}', flush=True)
                            continue
                    run(folder, [sys.executable, str(ROOT / 'tools/dotnet/measure_map_latency.py'),
                                 '--assets', str(args.assets), '--fonts', str(args.fonts), '--output', str(folder),
                                 '--size', size, '--expected-density', str(density), '--native-aot',
                                 '--frames', str(args.frames), '--capture', args.latency_capture, '--adapter', *args.adapter],
                        OPENCIV2_MAP_EXPERIMENT='1', OPENCIV2_MAP_COMPOSITION=composition, OPENCIV2_MAP_GPU_FEATURES=features,
                        OPENCIV2_MAP_GPU_SYNC=sync, OPENCIV2_MAP_LATENCY=1, AVALONIA_GLOBAL_SCALE_FACTOR=density)


if __name__ == '__main__':
    main()
