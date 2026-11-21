#!/usr/bin/env python3
"""Actual Linux file-picker input and durable application storage across host restarts."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import time
from run_application_native import X11


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--adapter', nargs=argparse.REMAINDER, required=True)
    args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    artifact_root = Path(args.adapter[-1]).resolve().parent
    def inventory():
        return {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(artifact_root.rglob('*')) if p.is_file() and p.suffix != '.pdb'}
    artifacts = inventory()
    drivers = {str(Path(name)): hashlib.sha256(Path(name).read_bytes()).hexdigest()
               for name in (__file__, str(Path(__file__).with_name('run_application_native.py')))}
    checks, identities = [], []
    with tempfile.TemporaryDirectory(prefix='oc2-storage-native-') as directory, open(str(args.output) + '.log', 'w') as log:
        root = Path(directory); snapshot = root / 'state.json'; store = root / 'store'
        source = root / 'sample.sav'; exported = root / 'exported.sav'
        payload = bytes(i % 256 for i in range(131077)); source.write_bytes(payload)
        driver = X11(); process = None; window = None
        def read():
            try: return json.loads(snapshot.read_text())
            except (FileNotFoundError, json.JSONDecodeError): return {}
        def wait(test, seconds=30):
            deadline = time.monotonic() + seconds
            while time.monotonic() < deadline:
                if process.poll() is not None: raise RuntimeError('Host exited; inspect log')
                state = read()
                if test(state): return state
                time.sleep(.03)
            raise RuntimeError('Timeout: ' + json.dumps(read()))
        def launch():
            nonlocal process, window
            snapshot.unlink(missing_ok=True)
            process = subprocess.Popen([*args.adapter, '--observe', str(snapshot), '--storage-directory', str(store),
                                        '--renderer-settings', str(root / 'renderer.json')], stdout=log, stderr=log)
            wait(lambda s: s.get('menu', {}).get('groups', {}).get('File', {}).get('bounds'))
            window = driver.window_for(process.pid); driver.wait_placement(window)
            identities.append(json.loads(Path(str(snapshot) + '.identity.json').read_text()))
        def stop():
            driver.close_window(window); process.wait(timeout=10)
            if process.returncode != 0: raise RuntimeError(f'Host shutdown failed: {process.returncode}')
        def click_box(box):
            factor = read()['scaling']; box = dict(box)
            for name in ('x', 'y', 'width', 'height'): box[name] *= factor
            driver.click(window, box)
        def click(name):
            time.sleep(.1); click_box(read()['files']['controls'][name])
        def action(group, name):
            click_box({**read()['menu']['groups'][group]['bounds'], 'enabled': True})
            wait(lambda s: s['menu']['groups'][group]['open']); time.sleep(.1)
            click_box(read()['menu']['actions'][name]['bounds'])
        def open_library():
            action('File', 'file.library')
            wait(lambda s: s['files']['visible'] and not s['files']['busy'] and s['files']['controls']['import']['width'] > 0 and s['files']['controls']['import']['focused'])
        def check(name, condition):
            state = read()
            if not condition(state): raise RuntimeError(name + ': ' + json.dumps(state))
            checks.append({'name': name, 'state': state})
        def modifier_key(key, down):
            code = driver.x.XKeysymToKeycode(driver.display, driver.x.XStringToKeysym(key.encode()))
            driver.input.XTestFakeKeyEvent(driver.display, code, int(down), 0); driver.x.XFlush(driver.display)
        def picker():
            time.sleep(.5)
            selected = None
            deadline = time.monotonic() + 10
            while selected is None and time.monotonic() < deadline:
                selected = driver.window_for(process.pid, excluding=window, visible=True)
                if selected is None: time.sleep(.05)
            if selected is None: raise RuntimeError('Native file chooser not found')
            driver.wait_placement(selected)
            driver.x.XRaiseWindow(driver.display, selected); driver.x.XSetInputFocus(driver.display, selected, 1, 0)
            return selected
        def enter_path(selected, path):
            # GTK's native location field; the dialog is real, not a mocked file service.
            driver.x.XSetInputFocus(driver.display, selected, 1, 0)
            modifier_key('Control_L', True); driver.key('l'); modifier_key('Control_L', False)
            modifier_key('Control_L', True); driver.key('a'); modifier_key('Control_L', False)
            for character in str(path):
                if character == '_': modifier_key('Shift_L', True)
                driver.key({'/': 'slash', '.': 'period', '-': 'minus', '_': 'minus'}.get(character, character))
                if character == '_': modifier_key('Shift_L', False)
            driver.key('Return')
        try:
            launch(); action('View', 'view.grid'); open_library()
            check('empty-library-and-focus', lambda s: not s['files']['entries'] and s['files']['controls']['import']['focused'])
            click('import'); dialog = picker()
            ticks = read()['serviceTicks']; wait(lambda s: s['serviceTicks'] >= ticks + 3)
            check('native-picker-dispatcher-progress', lambda s: s['files']['busy'] and s['files']['canCancel'])
            subprocess.run(['import', '-window', str(dialog), str(args.output) + '.picker.png'], check=True)
            driver.key('Escape'); wait(lambda s: not s['files']['busy'])
            check('native-picker-cancel', lambda s: not s['files']['entries'] and s['files']['message'] == 'Import cancelled.')
            click('import'); dialog = picker(); enter_path(dialog, source)
            wait(lambda s: not s['files']['busy'])
            check('native-import-commit', lambda s: len(s['files']['entries']) == 1 and s['files']['entries'][0]['size'] == len(payload) and s['files']['message'].startswith('Stored '))
            click('preferences'); wait(lambda s: s['files']['message'] == 'Display preferences saved.')
            subprocess.run(['import', '-window', str(window), str(args.output) + '.files.png'], check=True)
            stop(); launch()
            check('preferences-after-process-restart', lambda s: s['map']['grid'])
            open_library(); check('file-after-process-restart', lambda s: len(s['files']['entries']) == 1)
            click('export'); dialog = picker(); enter_path(dialog, exported)
            wait(lambda s: not s['files']['busy'])
            if exported.read_bytes() != payload: raise RuntimeError('Export changed bytes')
            check('native-export-finalized', lambda s: s['files']['message'] == 'Export completed.')
            # Repeat to a shorter existing output and exercise the OS overwrite confirmation.
            exported.write_bytes(payload + b'old trailing bytes')
            click('export'); dialog = picker(); enter_path(dialog, exported); time.sleep(.5); driver.key('Return')
            wait(lambda s: not s['files']['busy'])
            if exported.read_bytes() != payload: raise RuntimeError('Export failed to truncate old destination')
            check('native-overwrite-truncation', lambda s: s['files']['message'] == 'Export completed.')
            click('remove'); wait(lambda s: not s['files']['busy'])
            check('native-remove-complete', lambda s: not s['files']['entries'])
            click('close'); check('file-panel-focus-return', lambda s: not s['files']['visible'] and s['controls']['map']['focused'])
            # Silent presentation must not suppress or duplicate the admitted gameplay effect.
            click_box(read()['controls']['send']); wait(lambda s: s['status'] == 'AwaitingInteraction')
            time.sleep(.15); click_box(read()['controls']['confirm']); wait(lambda s: s['status'] == 'Completed')
            wait(lambda s: s['map']['notices'] > 0)
            check('silent-media-semantic-completion', lambda s: s['effects'] == 1 and s['media'] == 'Skipped')
            wait(lambda s: s['map']['notices'] == 0)
            check('presentation-clock-expiry', lambda s: s['effects'] == 1 and s['status'] == 'Completed')
            stop(); launch(); open_library()
            check('native-remove-after-restart', lambda s: not s['files']['entries'])
            stop(); process = None
            if inventory() != artifacts: raise RuntimeError('Native artifacts changed during validation')
            args.output.write_text(json.dumps({'format': 'openciv2-storage-native/1', 'checks': checks, 'identities': identities,
                                              'invokedArtifacts': artifacts, 'drivers': drivers,
                                              'payloadSha256': hashlib.sha256(payload).hexdigest()}, indent=2) + '\n')
            print(f'Passed {len(checks)} native storage checks.')
        except Exception:
            Path(str(args.output) + '.failure.json').write_text(json.dumps(read(), indent=2))
            if window: subprocess.run(['import', '-window', str(window), str(args.output) + '.failure.png'])
            raise
        finally:
            if process is not None and process.poll() is None: process.terminate(); process.wait(timeout=10)
            driver.close()

if __name__ == '__main__': main()
