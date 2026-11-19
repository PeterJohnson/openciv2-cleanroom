#!/usr/bin/env python3
"""Test desktop renderer preferences through actual owned-window input (Linux X11)."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile
import time

from run_map_showcase_native import Driver, click, coords, launch, rendered, wait


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--fonts', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--gpu-available', action='store_true')
    parser.add_argument('--native-aot', action='store_true', help='Require the signed Native AOT build identity')
    parser.add_argument('--adapter', nargs=argparse.REMAINDER, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    checks = []
    driver = Driver()
    def capture_menu(window, name):
        x, y = driver.root_point(window, 0, 0)
        # Xwayland does not expose a capturable composited root pixmap.
        options = ['-window', str(window)] if args.gpu_available else ['-window', 'root', '-crop', f'1150x780+{x}+{y}']
        subprocess.run(['import', *options, str(args.output / name)], check=True, timeout=20)

    try:
        with tempfile.TemporaryDirectory(prefix='oc2-renderer-options-') as directory:
            settings = Path(directory) / 'renderer.json'
            isolation = ['--renderer-settings', str(settings)]

            def require(value, name):
                if not value:
                    raise RuntimeError(name)
                checks.append(name)

            with launch(args, driver, 'MV22', isolation) as (process, window, snapshot, first, identity):
                require(first['draw']['composition'] == 'cpu' and first['draw']['requestedRenderer'] == 'cpu', 'CPU default')
                require(not settings.exists(), 'startup does not write preferences')
                time.sleep(.5)  # Allow compositor placement to settle before pointer input.
                click(driver, window, first, 'rendererButton')
                opened = wait(process, snapshot, lambda s: s['rendererChoice'] is not None)
                driver.move(window, *coords(opened, 'rendererChoice'))
                driver.button(1, 1)
                driver.button(1, 0)
                time.sleep(.15)
                driver.key('End')
                driver.key('Return')
                saved = wait(process, snapshot, lambda s: s['savedRenderer'] == 'gpu')
                require(json.loads(settings.read_text())['renderer'] == 'gpu', 'UI saves GPU preference')
                require(saved['draw']['composition'] == 'cpu', 'selection applies only after restart')
                capture_menu(window, 'saved-preference.png')
            saved_bytes = settings.read_bytes()
            with launch(args, driver, 'MV22', [*isolation, '--map-gpu-cache-mib', '256']) as (process, window, snapshot, first, _):
                expected = 'gpu' if args.gpu_available else 'cpu'
                require(first['draw']['requestedRenderer'] == 'gpu' and first['draw']['composition'] == expected, 'saved GPU choice after restart')
                require((first['draw']['rendererFallback'] is None) == args.gpu_available, 'visible capability fallback reason')
                if args.gpu_available:
                    require(first['draw']['gpuCacheLimitBytes'] == 256 * 1024 * 1024, 'CLI GPU cache ceiling')
                time.sleep(.5)
                click(driver, window, first, 'recover')
                recovered = wait(process, snapshot, lambda s: rendered(s) and s['draw']['sequence'] > first['draw']['sequence'])
                require(recovered['draw']['composition'] == expected, 'reload retries requested renderer')
                click(driver, window, recovered, 'rendererButton')
                wait(process, snapshot, lambda s: s['rendererChoice'] is not None)
                capture_menu(window, 'active-renderer.png')
            with launch(args, driver, 'MV22', [*isolation, '--map-renderer', 'cpu']) as (_, _, _, first, _):
                require(first['draw']['requestedRenderer'] == 'cpu' and settings.read_bytes() == saved_bytes, 'CLI overrides without rewriting saved preference')
            settings.write_text('{"format":1}')
            with launch(args, driver, 'MV22', isolation) as (_, _, _, first, _):
                require(first['draw']['requestedRenderer'] == 'cpu' and 'Could not load' in first['rendererMessage'], 'malformed preference defaults with diagnostic')
            with launch(args, driver, 'MV22', ['--renderer-settings', directory]) as (process, window, snapshot, first, _):
                time.sleep(.5)
                click(driver, window, first, 'rendererButton')
                opened = wait(process, snapshot, lambda s: s['rendererChoice'] is not None)
                driver.move(window, *coords(opened, 'rendererChoice'))
                driver.button(1, 1)
                driver.button(1, 0)
                time.sleep(.15)
                driver.key('End')
                driver.key('Return')
                failed = wait(process, snapshot, lambda s: 'Could not save' in s['rendererMessage'])
                require(failed['savedRenderer'] == 'cpu', 'failed filesystem save is visible and preserves preference')
            for flags in (['--map-renderer', 'auto'], ['--map-gpu-cache-mib', '0'], ['--map-gpu-cache-mib', 'text']):
                result = subprocess.run([*args.adapter, *isolation, *flags], capture_output=True, text=True, timeout=15)
                require(result.returncode == 2, 'invalid CLI rejected: ' + ' '.join(flags))
    finally:
        driver.close()
    (args.output / 'renderer-options-native.json').write_text(json.dumps({'checks': checks, 'gpuAvailable': args.gpu_available, 'identity': identity}, indent=2) + '\n')
    print(f'Renderer preferences: {len(checks)} checks passed', flush=True)


if __name__ == '__main__':
    main()
