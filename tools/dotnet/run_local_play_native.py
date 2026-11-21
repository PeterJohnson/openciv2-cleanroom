#!/usr/bin/env python3
"""Play Alder Valley through production Avalonia controls on an isolated Linux X11 display."""
import argparse
import hashlib
import json
from pathlib import Path
import sqlite3
import subprocess
import tempfile
import time

from run_application import validate
from run_application_native import X11


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', required=True, type=Path)
    parser.add_argument('--desktop', required=True, type=Path)
    parser.add_argument('--cli', required=True, type=Path)
    parser.add_argument('--probe', required=True, type=Path)
    parser.add_argument('--sqlite-cli', type=Path)
    parser.add_argument('--assets', type=Path)
    parser.add_argument('--fonts', type=Path)
    args = parser.parse_args()

    with tempfile.TemporaryDirectory(prefix='oc2-m3-play-') as directory:
        root = Path(directory)
        observation = root / 'observation.json'
        storage = root / 'storage'
        command = ['dotnet', str(args.desktop), '--game', 'alder-valley', '--observe', str(observation),
                   '--storage-directory', str(storage), '--renderer-settings', str(root / 'renderer.json')]
        if args.assets and args.fonts:
            command += ['--assets', str(args.assets), '--fonts', str(args.fonts)]
        log = root / 'desktop.log'
        with log.open('w') as output:
            app = subprocess.Popen(command, stdout=output, stderr=subprocess.STDOUT)
            try:
                result = drive(args, app, observation, storage)
            except Exception:
                print(log.read_text(), flush=True)
                if observation.exists():
                    state = json.loads(observation.read_text())
                    print(json.dumps(state, indent=2), flush=True)
                raise
            finally:
                if app.poll() is None:
                    app.terminate()
                    app.wait(timeout=10)
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(result, indent=2) + '\n')


def drive(args, app, observation, storage):
    driver = X11()
    checks = []

    def wait(predicate):
        deadline = time.monotonic() + 10
        while time.monotonic() < deadline:
            if app.poll() is not None:
                raise RuntimeError(f'Desktop exited with {app.returncode}')
            try:
                state = json.loads(observation.read_text())
            except (FileNotFoundError, json.JSONDecodeError):
                time.sleep(.04)
                continue
            if state.get('play') is not None and predicate(state):
                return state
            time.sleep(.04)
        raise AssertionError('Timed out waiting for the expected application state')

    def click_box(box, state):
        scale = state['scaling']
        assert box['enabled'] and box['width'] > 0 and box['height'] > 0, box
        assert 0 <= box['y'] + box['height'] / 2 <= state['height'], box
        scaled = {**box, **{key: box[key] * scale for key in ('x', 'y', 'width', 'height')}}
        driver.click(window, scaled)

    def click(name):
        state = wait(lambda s: (box := s['play']['controls'].get(name)) is not None
                     and box['enabled'] and box['width'] > 0 and box['height'] > 0)
        click_box(state['play']['controls'][name], state)

    def command(action, expected='Completed'):
        before = wait(lambda s: True)['play']['command']
        action()
        return wait(lambda s: s['status'] == expected and s['play']['command'] != before)

    def type_line(text, dialog=False):
        def text_box(state):
            return state['dialog']['console'] if dialog else state['controls']['input']

        state = wait(lambda s: s['dialog']['visible'] == dialog and text_box(s)['width'] > 0)
        click_box(text_box(state), state)
        driver.chord('Control_L', 'a')
        for char in text:
            if char == ':':
                driver.chord('Shift_L', 'semicolon')
            else:
                driver.key({' ': 'space', '.': 'period', '=': 'equal', '-': 'minus'}.get(char, char))
        driver.key('Return')

    def select(slot):
        def unit_index(state):
            return next(i for i, unit in enumerate(state['play']['units']) if unit['slot'] == slot)

        def row_ready(state):
            index = unit_index(state)
            box = state['play']['unitRows'][index]
            return state['selected'] == index or box['enabled'] and box['width'] > 0 and box['height'] > 0

        state = wait(row_ready)
        index = unit_index(state)
        if state['selected'] != index:
            command(lambda: click_box(state['play']['unitRows'][index], state))

    def choice(value):
        state = wait(lambda s: s['dialog']['visible']
                     and (box := s['dialog']['regions'].get(f'choice.{value}')) is not None and box['width'] > 0)
        click_box(state['dialog']['regions'][f'choice.{value}'], state)

    def end_turn(turn, console_reply=False):
        click('end')
        confirmation = wait(lambda s: s['status'] == 'AwaitingInteraction')
        assert not confirmation['play']['controls']['save']['enabled']
        if console_reply:
            type_line('reply 1', dialog=True)
        else:
            choice(1)
        state = wait(lambda s: s['play']['turn'] == turn and s['status'] in ('Completed', 'AwaitingInteraction'))
        if state['status'] == 'AwaitingInteraction':
            assert not state['play']['phase'] and 'turn.advanced' in state['play']['effects']
            assert not state['dialog']['regions']['cancel']['enabled']
            offered = sorted(int(key.split('.')[1]) for key in state['dialog']['regions'] if key.startswith('choice.'))
            choice(offered[0])
            state = wait(lambda s: s['status'] == 'Completed' and s['play']['turn'] == turn)
        assert state['play']['phase']
        return state['play']

    def semantic(state):
        city = state['city']
        units = state['units']
        return {key: state[key] for key in ('turn', 'year', 'gold', 'research', 'progress', 'known', 'explored')} | {
            'city': {key: city[key] for key in ('slot', 'name', 'x', 'y', 'size', 'production', 'foodBox',
                                                'shieldBox', 'specialists', 'worked')},
            'units': [{key: unit[key] for key in ('slot', 'x', 'y', 'type', 'moves', 'orders')} for unit in units],
        }

    initial = wait(lambda s: s['map']['composition'] == 'cpu' and s['play']['turn'] == 1)
    window = driver.window_for(app.pid, visible=True)
    assert window
    driver.wait_placement(window)
    select(1)
    moved = command(lambda: click('move.1'))['play']
    rider = next(u for u in moved['units'] if u['slot'] == 1)
    assert (rider['x'], rider['y'], rider['moves']) == (12, 8, 1)
    assert initial['play']['explored'] < moved['explored'] < 192
    checks.append('map movement and fog expansion')

    select(0)
    founded = command(lambda: click('found'))['play']
    assert founded['cityOpen'] and founded['city']['size'] == 1
    assert all(unit['type'] != 0 for unit in founded['units'])
    worker = next(tile for tile in founded['city']['worked'] if tile != 20)
    specialist = command(lambda: click(f'city.tile.{worker}'))['play']
    assert specialist['city']['specialists'] == 1
    returned = command(lambda: click('city.tile.1'))['play']
    assert returned['city']['specialists'] == 0 and returned['city']['worked'] == [1, 20]
    command(lambda: click('city.production.4'))
    click('city.close')
    checks.append('founding, workers and production via city controls')

    click('end')
    decision = wait(lambda s: s['status'] == 'AwaitingInteraction' and s['dialog']['regions']['cancel']['width'] > 0)
    click_box(decision['dialog']['regions']['cancel'], decision)
    wait(lambda s: s['status'] == 'Cancelled' and s['play']['turn'] == 1)
    seen_effects = set()
    for turn in range(2, 8):
        play = end_turn(turn, console_reply=turn == 2)
        seen_effects.update(play['effects'])
    assert {'unit.produced', 'research.completed'} <= seen_effects
    assert 0 in play['known'] and len(play['units']) >= 2
    select(1)
    command(lambda: click('fortify'))
    checks.append('multi-turn upkeep, cancellation and pending research with console and GUI replies')

    click('save')
    saved = wait(lambda s: s['message'] == 'Classic save written.')['play']
    encoded = (storage / 'file-alder-valley.oc2data').read_bytes()
    assert encoded.startswith(b'OC2DOC01')
    offset = 12 + int.from_bytes(encoded[8:12], 'little')
    classic = observation.with_name('alder-valley.sav')
    classic.write_bytes(encoded[offset:])
    def inspect(save_path):
        loaded = json.loads(subprocess.check_output([
            'dotnet', str(args.cli), 'inspect', str(save_path), '--view', 'loaded',
            '--rules', 'alder-valley', '--json'
        ]))
        return loaded | {'city': loaded['cities'][0], 'explored': len(loaded['tiles'])}

    loaded = inspect(classic)
    assert semantic(loaded) == semantic(saved)
    checks.append('classic file and isolated loaded CLI agree with native state')

    if args.sqlite_cli:
        database = observation.with_name('loaded.sqlite')
        subprocess.run(['dotnet', str(args.sqlite_cli), 'export', str(classic), '--view', 'loaded',
                        '--rules', 'alder-valley', '--sqlite', str(database)], check=True)
        with sqlite3.connect(f'file:{database}?mode=ro', uri=True) as connection:
            assert connection.execute('select count(*) from tiles').fetchone()[0] == saved['explored']
            assert connection.execute('select slot, token, x, y from units order by slot').fetchall() == [
                (unit['slot'], unit['token'], unit['x'], unit['y']) for unit in loaded['units']]
            assert connection.execute('select name, size from cities').fetchone() == (
                saved['city']['name'], saved['city']['size'])
        checks.append('loaded SQLite rows agree and omit unseen tiles')

    end_turn(8)
    click('load')
    restored = wait(lambda s: s['message'] == 'Classic save loaded.')['play']
    assert semantic(saved) == semantic(restored)
    assert restored['runtime'] != saved['runtime'] and not restored['cityOpen']
    assert restored['city']['token'] != saved['city']['token']
    stale = saved['city']
    rejected = command(lambda: type_line(f"city.worker city={stale['slot']}:{stale['token']} tile=20"), 'Rejected')
    assert 'city_not_available' in rejected['message'] and semantic(rejected['play']) == semantic(restored)
    resumed = end_turn(8)
    assert next(u for u in resumed['units'] if u['slot'] == 1)['orders'] == 2
    checks.append('stale city rejected after session replacement and resumed turn completed')

    reference_save = observation.with_name('direct-save.sav')
    reference_end = observation.with_name('direct-continued.sav')
    subprocess.run(['dotnet', str(args.probe), '--local-play-reference', str(reference_save), str(reference_end)],
                   check=True)
    assert semantic(inspect(reference_save)) == semantic(saved)
    assert semantic(inspect(reference_end)) == semantic(resumed)
    checks.append('direct client and native UI agree before reload and after continuation')
    driver.close_window(window)
    assert app.wait(timeout=10) == 0
    checks.append('normal desktop shutdown')

    identity = json.loads(Path(str(observation) + '.identity.json').read_text())
    validate(identity)
    artifacts = [args.desktop, args.cli, args.probe, args.sqlite_cli, Path(__file__)]
    hashes = {path.name: hashlib.sha256(path.read_bytes()).hexdigest() for path in artifacts if path}
    driver.close()
    return {'format': 'openciv2-linux-local-play/1', 'input': 'XTest pointer and keyboard', 'composition': 'cpu',
            'identity': identity, 'sha256': hashes, 'checks': checks, 'saved': semantic(saved), 'resumed': semantic(resumed)}


if __name__ == '__main__':
    main()
