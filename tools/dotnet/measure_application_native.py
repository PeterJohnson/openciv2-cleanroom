#!/usr/bin/env python3
"""Measure real Linux X11 app startup/idle RSS and verify readiness through input."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile
import time
from run_application_native import X11


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', required=True, type=Path)
    parser.add_argument('--repetitions', type=int, default=15)
    parser.add_argument('--map-assets', type=Path, required=True)
    parser.add_argument('--map-fonts', type=Path, required=True)
    parser.add_argument('--adapter', nargs=argparse.REMAINDER, required=True)
    args = parser.parse_args()
    if not 1 <= args.repetitions <= 30: raise ValueError('Invalid population size')
    samples = []; identity = None; driver = X11()
    try:
        with tempfile.TemporaryDirectory(prefix='oc2-startup-', dir=args.report.parent) as directory:
            for iteration in range(args.repetitions + 3):
                state_path = Path(directory) / f'state-{iteration}.json'
                with args.report.with_suffix(f'.{iteration}.log').open('wb') as log:
                    start = time.perf_counter_ns()
                    process = subprocess.Popen([*args.adapter, '--observe', str(state_path), '--assets', str(args.map_assets),
                        '--fonts', str(args.map_fonts), '--storage-directory', str(Path(directory) / 'storage'),
                        '--renderer-settings', str(Path(directory) / 'renderer.json')], stdout=log, stderr=subprocess.STDOUT)
                    try:
                        def read(): return json.loads(state_path.read_text()) if state_path.exists() else {}
                        def wait(test):
                            deadline = time.monotonic() + 30
                            while time.monotonic() < deadline:
                                if process.poll() is not None: raise RuntimeError('Application exited before ready')
                                state = read()
                                if test(state): return state
                                time.sleep(.005)
                            raise RuntimeError('Application readiness/input timeout')
                        state = wait(lambda s: s.get('contentLoaded') and s.get('map', {}).get('composition') and s.get('controls', {}).get('send', {}).get('width', 0) > 0 and s['controls']['send']['enabled'] and
                                     s.get('observer') == '1' and [u['slot'] for u in s.get('units', [])] == ['0', '1', '2'])
                        ready_ms = (time.perf_counter_ns() - start) / 1e6
                        window = driver.window_for(process.pid)
                        if window is None: raise RuntimeError('No real application window')
                        current = json.loads(Path(str(state_path) + '.identity.json').read_text())
                        if identity is not None and current != identity: raise ValueError('Application identity changed')
                        identity = current
                        # Allow initial painting/JIT to settle, then retain ten RSS
                        # observations. The read-only observation timer remains active.
                        time.sleep(1)
                        rss = []; high_water = []
                        for _ in range(10):
                            if process.poll() is not None: raise RuntimeError('Application exited during idle sampling')
                            fields = dict(line.split(':', 1) for line in Path(f'/proc/{process.pid}/status').read_text().splitlines() if ':' in line)
                            rss.append(int(fields['VmRSS'].split()[0]) * 1024)
                            high_water.append(int(fields['VmHWM'].split()[0]) * 1024)
                            time.sleep(.1)
                        def click(name):
                            state = wait(lambda s: (box := s.get('controls', {}).get(name, {})).get('enabled') and box.get('width', 0) > 0 and box.get('height', 0) > 0)
                            box = dict(state['controls'][name])
                            for key in ('x', 'y', 'width', 'height'): box[key] *= state['scaling']
                            driver.click(window, box)
                        click('send'); wait(lambda s: s.get('status') == 'AwaitingInteraction')
                        click('confirm'); completed = wait(lambda s: s.get('status') == 'Completed')
                        if completed['changed'] is not True or completed['effects'] != 1: raise ValueError('Ready application failed command')
                        driver.close_window(window)
                        if process.wait(timeout=10) != 0: raise RuntimeError('Application failed to close cleanly')
                        samples.append({'iteration': iteration, 'warmup': iteration < 3, 'readyMs': ready_ms, 'snapshot': state,
                                        'idleRssBytes': rss, 'startupHighWaterBytes': max(high_water), 'completed': completed})
                        print(f'Completed desktop sample {iteration + 1}/{args.repetitions + 3}: {ready_ms:.1f} ms', flush=True)
                    finally:
                        if process.poll() is None:
                            process.terminate()
                            try: process.wait(timeout=10)
                            except subprocess.TimeoutExpired: process.kill(); process.wait()
        args.report.write_text(json.dumps({'format': 'openciv2-application-startup/1', 'host': 'desktop',
                                          'platform': 'Linux X11 under isolated Xvfb', 'identity': identity, 'samples': samples, 'errors': []}, indent=2) + '\n')
    finally: driver.close()


if __name__ == '__main__': main()
