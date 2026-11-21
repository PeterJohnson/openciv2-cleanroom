#!/usr/bin/env python3
"""Native WM fullscreen transitions while the production custom dialog is pending."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile
import time
from run_application_native import X11
from run_application import validate
from write_identity import sha


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--assets', type=Path, required=True); parser.add_argument('--fonts', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True); parser.add_argument('--renderer', choices=['cpu', 'gpu'], default='cpu')
    parser.add_argument('--adapter', nargs=argparse.REMAINDER, required=True); args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    artifacts = {str(p): sha(p) for p in sorted(Path(args.adapter[-1]).resolve().parent.rglob('*')) if p.is_file()}
    with tempfile.TemporaryDirectory(prefix='oc2-fullscreen-') as directory, open(str(args.output) + '.log', 'w') as log:
        root = Path(directory); snapshot = root / 'state.json'; driver = X11(); checks = []
        process = subprocess.Popen([*args.adapter, '--assets', str(args.assets), '--fonts', str(args.fonts),
            '--observe', str(snapshot), '--map-renderer', args.renderer, '--storage-directory', str(root / 'store'),
            '--renderer-settings', str(root / 'renderer.json')], stdout=log, stderr=log)
        def read():
            try: return json.loads(snapshot.read_text())
            except (FileNotFoundError, json.JSONDecodeError): return {}
        def wait(predicate):
            deadline = time.monotonic() + 30
            while time.monotonic() < deadline:
                if process.poll() is not None: raise RuntimeError('Host exited')
                value = read()
                if predicate(value): return value
                time.sleep(.03)
            raise RuntimeError('Fullscreen timeout: ' + json.dumps(read()))
        try:
            first = wait(lambda s: s.get('contentLoaded') and s.get('map', {}).get('composition') == args.renderer)
            identity = json.loads(Path(str(snapshot) + '.identity.json').read_text()); sources = validate(identity)
            window = driver.window_for(process.pid); driver.wait_placement(window)
            def click(name):
                state = read(); box = dict(state['controls'][name])
                for key in ('x', 'y', 'width', 'height'): box[key] *= state['scaling']
                driver.click(window, box)
            click('send'); pending = wait(lambda s: s.get('status') == 'AwaitingInteraction')
            for cycle in range(3):
                driver.fullscreen(window, True)
                full = wait(lambda s: s.get('width', 0) > first['width'] + 50 and s.get('serviceTicks', 0) > pending['serviceTicks'] + 3)
                assert full['status'] == 'AwaitingInteraction' and full['effects'] == 0 and full['map']['composition'] == args.renderer
                for name in ('confirm', 'cancel'):
                    box = full['controls'][name]
                    assert box['enabled'] and box['width'] > 0 and 0 <= box['x'] < full['width'] and 0 <= box['y'] < full['height']
                if cycle == 0: subprocess.run(['import', '-window', str(window), str(args.output) + '.fullscreen.png'], timeout=10, check=True)
                driver.fullscreen(window, False)
                normal = wait(lambda s: abs(s.get('width', 0) - first['width']) < 2)
                assert normal['status'] == 'AwaitingInteraction' and normal['units'] == first['units']
                checks.append(dict(cycle=cycle, fullscreen=full, restored=normal))
            driver.wait_placement(window)
            driver.x.XSetInputFocus(driver.display, window, 1, 0); driver.x.XFlush(driver.display)
            driver.key('Escape'); end = wait(lambda s: s.get('status') == 'Cancelled')
            assert end['effects'] == 0
            driver.close_window(window); assert process.wait(timeout=10) == 0
            assert all(sha(Path(p)) == digest for p, digest in artifacts.items())
            assert all(sha(Path(p)) == digest for p, digest in sources.items())
            args.output.write_text(json.dumps(dict(identity=identity, sources=sources, artifacts=artifacts, checks=checks,
                driverSha256=sha(Path(__file__)), inputDriverSha256=sha(Path(__file__).with_name('run_application_native.py')),
                final=end, scope='Virtual Xwayland fullscreen; physical display and mixed-DPI scanout remain separate'), indent=2) + '\n')
            print('Passed three native fullscreen/restore cycles with pending dialog: ' + args.renderer)
        finally:
            if process.poll() is None: process.terminate(); process.wait(timeout=10)
            driver.close()
if __name__ == '__main__': main()
