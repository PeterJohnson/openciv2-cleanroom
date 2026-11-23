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
        def ready(state):
            box = state['play']['controls'].get(name)
            return box is not None and box['enabled'] and box['width'] > 0 and box['height'] > 0

        state = wait(ready)
        # A command can expose a previously measured control before Avalonia arranges it.
        ticks = state['serviceTicks']
        state = wait(lambda s: s['serviceTicks'] >= ticks + 2 and ready(s))
        click_box(state['play']['controls'][name], state)

    def command(action, expected='Completed'):
        before = wait(lambda s: True)['play']['command']
        action()
        return wait(lambda s: s['status'] == expected and s['play']['command'] != before)

    def type_text(text):
        for char in text:
            if char == ':':
                driver.chord('Shift_L', 'semicolon')
            elif char == '_':
                driver.chord('Shift_L', 'minus')
            else:
                driver.key({' ': 'space', '.': 'period', '=': 'equal', '-': 'minus', '/': 'slash'}.get(char, char))

    def enter_line(text, dialog=False):
        def text_box(state):
            return state['dialog']['console'] if dialog else state['controls']['input']

        state = wait(lambda s: s['dialog']['visible'] == dialog and text_box(s)['width'] > 0)
        click_box(text_box(state), state)
        driver.chord('Control_L', 'a')
        type_text(text)

    def type_line(text, dialog=False):
        enter_line(text, dialog)
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
        assert not confirmation['menu']['actions']['file.library']['enabled']
        if console_reply:
            type_line('reply 1', dialog=True)
        else:
            choice(1)
        state = wait(lambda s: s['play']['turn'] == turn and s['status'] in ('Completed', 'AwaitingInteraction'))
        if state['status'] == 'AwaitingInteraction':
            assert not state['play']['phase'] and 'turn.advanced' not in state['play']['effects']
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
    type_line('help')
    wait(lambda s: 'Enter tile x y' in s['message'] and 'unit.move' in s['message'])
    enter_line('tile 0 0')
    driver.key('Left')
    driver.chord('Control_L', 's')
    driver.key('Return')
    hidden = wait(lambda s: s['message'] == 'Tile is unexplored.')
    assert hidden['play']['command'] == initial['play']['command']
    assert all(hidden['map'][key] == initial['map'][key] for key in ('x', 'y'))
    assert not (storage / 'file-alder-valley.oc2data').exists()
    type_line('tile 8 8')
    wait(lambda s: s['message'].startswith('(8,8) · '))
    driver.key('Up')
    driver.key('Up')
    driver.key('Return')
    wait(lambda s: s['message'] == 'Tile is unexplored.')
    pioneer = initial['play']['units'][0]
    enter_line(f"unit.select unit={pioneer['slot']}:")
    driver.key('Tab')
    completed = command(lambda: driver.key('Return'))
    assert f"unit={pioneer['slot']}:{pioneer['token']}" in completed['message']
    assert 'unit.selected' in completed['message']
    checks.append('console help, focus, history, live completion, scoped tile queries and structured results')

    select(1)
    moved = command(lambda: click('move.1'))['play']
    rider = next(u for u in moved['units'] if u['slot'] == 1)
    assert (rider['x'], rider['y'], rider['moves']) == (12, 8, 1)
    assert initial['play']['explored'] < moved['explored'] < 192
    checks.append('map movement and fog expansion')

    south = command(lambda: click('move.3'))
    assert south['selected'] == 1
    rider = next(u for u in south['play']['units'] if u['slot'] == 1)
    assert (rider['x'], rider['y'], rider['moves']) == (12, 10, 4)
    spent = command(lambda: click('move.3'), 'Failed')
    assert 'movement_spent' in spent['message'] and spent['play']['effects'] == ['unit.movement_spent']
    rider = next(u for u in spent['play']['units'] if u['slot'] == 1)
    assert (rider['x'], rider['y'], rider['moves']) == (12, 10, 6)
    assert spent['selected'] == 0 and (spent['map']['tileX'], spent['map']['tileY']) == (8, 8)
    checks.append('selection follows a moving unit and advances after a failed partial step')

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
    assert loaded == inspect(classic)
    checks.append('repeatable isolated loaded CLI agrees with native state')

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
    # Fortification executes on activation; the nearer manual unit is selected first.
    assert next(u for u in resumed['units'] if u['slot'] == 1)['orders'] == 1
    checks.append('stale city rejected after session replacement and resumed turn completed')

    reference_save = observation.with_name('direct-save.sav')
    reference_end = observation.with_name('direct-continued.sav')
    subprocess.run(['dotnet', str(args.probe), '--local-play-reference', str(reference_save), str(reference_end)],
                   check=True)
    assert semantic(inspect(reference_save)) == semantic(saved)
    assert semantic(inspect(reference_end)) == semantic(resumed)
    checks.append('direct client and native UI agree before reload and after continuation')

    def menu_click(state, bounds):
        assert bounds['enabled']
        scale = state['scaling']
        driver.move(window, (bounds['x'] + bounds['width'] / 2) * scale,
                    (bounds['y'] + bounds['height'] / 2) * scale)
        driver.button(1, 1)
        driver.button(1, 0)

    def stored_control(name):
        state = wait(lambda s: s['files']['visible'] and not s['files']['busy']
                     and s['files']['controls'][name]['enabled'])
        click_box(state['files']['controls'][name], state)

    def import_save(path):
        stored_control('import')
        wait(lambda s: s['files']['canCancel'])
        deadline = time.monotonic() + 10
        while not (picker := driver.window_for(app.pid, excluding=window, visible=True)):
            if time.monotonic() >= deadline:
                raise AssertionError('Native file picker did not open')
            time.sleep(.05)
        driver.wait_placement(picker)
        driver.x.XRaiseWindow(driver.display, picker)
        driver.x.XSetInputFocus(driver.display, picker, 1, 0)
        driver.chord('Control_L', 'l')
        driver.chord('Control_L', 'a')
        type_text(str(path))
        driver.key('Return')
        state = wait(lambda s: not s['files']['busy'] and s['files']['message'] == f'Stored {path.name}.')
        index = next(i for i, entry in enumerate(state['files']['entries']) if entry['name'] == path.name)
        state = wait(lambda s: s['files']['rows'][index]['width'] > 0)
        click_box(state['files']['rows'][index], state)

    state = wait(lambda s: s['menu']['groups']['File']['bounds'] is not None)
    menu_click(state, state['menu']['groups']['File']['bounds'])
    state = wait(lambda s: s['menu']['actions']['file.library']['bounds'] is not None)
    menu_click(state, state['menu']['actions']['file.library']['bounds'])
    invalid = observation.with_name('invalid.sav')
    invalid.write_bytes(classic.read_bytes()[:20])
    import_save(invalid)
    stored_control('open')
    rejected = wait(lambda s: s['files']['message'] == 'The classic save could not be read.')
    assert rejected['files']['visible'] and rejected['play']['runtime'] == resumed['runtime']
    assert semantic(rejected['play']) == semantic(resumed)
    import_save(classic)
    stored_control('open')
    imported = wait(lambda s: not s['files']['visible'] and s['message'] == 'Classic save loaded.')['play']
    assert imported['runtime'] != resumed['runtime']
    assert semantic(imported) == semantic(saved)
    assert semantic(end_turn(8)) == semantic(resumed)
    checks.append('native file picker import, invalid load preserves live session, valid load resumes identically')

    click('city')
    command(lambda: click('city.production.-4'))
    click('city.close')
    building = None
    for turn in range(9, 17):
        candidate = end_turn(turn)
        if 'city.improvement_built' in candidate['effects']:
            building = candidate
            break
    assert building is not None and building['city']['production'] == -4 and building['city']['shieldBox'] == 0
    click('city')
    wait(lambda s: s['play']['cityOpen'] and 'city.production.-4' not in s['play']['controls'])
    click('city.close')
    click('save')
    building_saved = wait(lambda s: s['message'] == 'Classic save written.')['play']
    click('load')
    building_loaded = wait(lambda s: s['message'] == 'Classic save loaded.')['play']
    assert semantic(building_saved) == semantic(building_loaded)
    click('city')
    wait(lambda s: s['play']['cityOpen'] and 'city.production.-4' not in s['play']['controls'])
    click('city.close')
    duplicate = end_turn(building['turn'] + 1)
    assert 'city.invalid_build' in duplicate['effects'] and 'city.improvement_built' not in duplicate['effects']
    checks.append('building chosen through city controls, completed, saved/reloaded and duplicate build rejected without spending')

    click('city')
    command(lambda: click('city.sell.4'), 'AwaitingInteraction')
    choice(0)
    declined = wait(lambda s: s['status'] == 'Cancelled')['play']
    assert declined['gold'] == duplicate['gold'] and 'city.sell.4' in declined['controls']
    command(lambda: click('city.sell.4'), 'AwaitingInteraction')
    choice(1)
    sold = wait(lambda s: s['status'] == 'Completed' and 'city.improvement_sold' in s['play']['effects'])['play']
    assert sold['gold'] == duplicate['gold'] + 8 and 'city.sell.4' not in sold['controls']
    assert not sold['controls']['city.sell.1']['enabled']
    command(lambda: click('city.production.4'))
    command(lambda: click('city.buy'), 'AwaitingInteraction')
    type_line('cancel', dialog=True)
    cancelled = wait(lambda s: s['status'] == 'Cancelled')['play']
    assert cancelled['gold'] == sold['gold'] and cancelled['city']['shieldBox'] == sold['city']['shieldBox']
    command(lambda: click('city.buy'), 'AwaitingInteraction')
    choice(1)
    bought = wait(lambda s: s['status'] == 'Completed' and 'city.production_bought' in s['play']['effects'])['play']
    remaining = max(0, min(999, 8 - cancelled['city']['shieldBox']))
    price = (remaining * remaining // 20 + 2 * remaining) * (2 if cancelled['city']['shieldBox'] == 0 else 1)
    assert bought['gold'] == cancelled['gold'] - price and bought['city']['shieldBox'] == 8
    assert bought['units'] == cancelled['units']
    click('city.close')
    click('save')
    paid_save = wait(lambda s: s['message'] == 'Classic save written.')['play']
    click('load')
    paid_reload = wait(lambda s: s['message'] == 'Classic save loaded.')['play']
    assert semantic(paid_save) == semantic(paid_reload)
    finished_purchase = end_turn(bought['turn'] + 1)
    assert 'unit.produced' in finished_purchase['effects'] and len(finished_purchase['units']) == len(bought['units']) + 1
    checks.append('native sale and purchase confirmations, cancellation, prices, save/reload and next-turn completion')

    # A fresh process matches the reference's identity-counter and load history.
    driver.close_window(window)
    assert app.wait(timeout=10) == 0
    launch = list(app.args)
    observation = observation.with_name('work-observation.json')
    storage = storage.with_name('work-storage')
    launch[launch.index('--observe') + 1] = str(observation)
    launch[launch.index('--storage-directory') + 1] = str(storage)
    app = subprocess.Popen(launch)
    try:
        wait(lambda s: s['map']['composition'] == 'cpu' and s['play']['turn'] == 1)
        window = driver.window_for(app.pid, visible=True)
        assert window
        driver.wait_placement(window)
        work_initial = observation.with_name('terrain-initial.sav')
        work_started = observation.with_name('terrain-working.sav')
        work_done = observation.with_name('terrain-completed.sav')
        subprocess.run(['dotnet', str(args.probe), '--local-work-reference', str(work_initial),
                        str(work_started), str(work_done)], check=True)
        state = wait(lambda s: s['menu']['groups']['File']['bounds'] is not None)
        menu_click(state, state['menu']['groups']['File']['bounds'])
        state = wait(lambda s: s['menu']['actions']['file.library']['bounds'] is not None)
        menu_click(state, state['menu']['actions']['file.library']['bounds'])
        import_save(work_initial)
        stored_control('open')
        wait(lambda s: not s['files']['visible'] and s['message'] == 'Classic save loaded.' and s['play']['turn'] == 1)
        select(0)
        started_work = command(lambda: click('work'))['play']
        assert next(u for u in started_work['units'] if u['slot'] == 0)['orders'] == 6
        click('save')
        wait(lambda s: s['message'] == 'Classic save written.')

        def saved_classic():
            document = (storage / 'file-alder-valley.oc2data').read_bytes()
            return document[12 + int.from_bytes(document[8:12], 'little'):]

        def compare_work_save(expected):
            actual, reference = saved_classic(), expected.read_bytes()
            if actual != reference:
                args.report.parent.mkdir(parents=True, exist_ok=True)
                prefix = args.report.with_name(args.report.stem + '-' + expected.stem)
                prefix.with_suffix('.actual.sav').write_bytes(actual)
                prefix.with_suffix('.expected.sav').write_bytes(reference)
                differences = [i for i, (a, b) in enumerate(zip(actual, reference)) if a != b]
                raise AssertionError(f'{expected.name}: save lengths {len(actual)}/{len(reference)}, '
                                     f'{len(differences)} differing bytes; first offsets {differences[:16]}')

        compare_work_save(work_started)
        click('load')
        wait(lambda s: s['message'] == 'Classic save loaded.')
        queued = end_turn(2)
        assert next(u for u in queued['units'] if u['slot'] == 0)['orders'] == 6
        completed_work = end_turn(3)
        assert next(u for u in completed_work['units'] if u['slot'] == 0)['orders'] == -1
        assert 'unit.work_completed' in completed_work['effects']
        click('save')
        wait(lambda s: s['message'] == 'Classic save written.')
        compare_work_save(work_done)
        checks.append('native terrain work selector, queued progress, save/reload and exact completion match the direct client')

        driver.close_window(window)
        assert app.wait(timeout=10) == 0
    finally:
        if app.poll() is None:
            app.terminate()
            app.wait(timeout=10)
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
