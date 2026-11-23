#!/usr/bin/env python3
"""Play Alder Valley through production Avalonia controls on an isolated Linux X11 display."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import signal
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
    parser.add_argument('--automation-desktop', type=Path)
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
        state = None
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
        play = (state or {}).get('play') or {}
        if play.get('automation') is not None:
            controls = {key: value for key, value in play['controls'].items() if key.startswith('automation.')}
            raise AssertionError(f"Timed out waiting for the automation window: {play['automation']}; "
                                 f"controls {controls}; message {state['message']!r}")
        failure = args.report.with_suffix('.failure.json')
        failure.parent.mkdir(parents=True, exist_ok=True)
        failure.write_text(json.dumps(state, indent=2) + '\n')
        raise AssertionError(f'Timed out waiting for the expected application state; current window saved to {failure}')

    def click_box(box, state):
        scale = state['scaling']
        assert box['enabled'] and box['width'] > 0 and box['height'] > 0, box
        assert 0 <= box['y'] + box['height'] / 2 <= state['height'], box
        scaled = {**box, **{key: box[key] * scale for key in ('x', 'y', 'width', 'height')}}
        driver.click(window, scaled)

    def sidebar_visible(select_box):
        state = wait(lambda s: select_box(s)['width'] > 0)
        for _ in range(24):
            box = select_box(state)
            top, bottom = state['controls']['map']['y'], state['height'] - 16
            if box['y'] >= top and box['y'] + box['height'] <= bottom:
                return state
            # Use the sidebar's scrollbar edge so scrolling cannot change a combo value.
            scale = state['scaling']
            driver.move(window, (state['width'] - 22) * scale, (top + 40) * scale)
            wheel = 4 if box['y'] < top else 5
            driver.button(wheel, 1)
            driver.button(wheel, 0)
            ticks = state['serviceTicks']
            state = wait(lambda s: s['serviceTicks'] >= ticks + 2)
        raise AssertionError('Sidebar control did not scroll into view')

    def click(name):
        def ready(state):
            box = state['play']['controls'].get(name)
            return box is not None and box['enabled'] and box['width'] > 0 and box['height'] > 0

        state = wait(ready)
        # A command can expose a previously measured control before Avalonia arranges it.
        ticks = state['serviceTicks']
        state = wait(lambda s: s['serviceTicks'] >= ticks + 2 and ready(s))
        state = sidebar_visible(lambda s: s['play']['controls'][name])
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
        if not dialog:
            state = sidebar_visible(text_box)
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
        while state['status'] == 'AwaitingInteraction':
            assert state['prompt'] == 'Choose the next advance.' or state['prompt'].startswith('Your civilization has reached')
            assert not state['play']['phase'] and 'turn.advanced' not in state['play']['effects']
            assert not state['dialog']['regions']['cancel']['enabled']
            old_prompt = state['prompt']
            offered = sorted(int(key.split('.')[1]) for key in state['dialog']['regions'] if key.startswith('choice.'))
            choice(offered[0])
            state = wait(lambda s: s['play']['turn'] == turn and
                         (s['status'] == 'Completed' or (s['status'] == 'AwaitingInteraction' and s['prompt'] != old_prompt)))
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

        jobs_initial = observation.with_name('city-jobs-initial.sav')
        jobs_completed = observation.with_name('city-jobs-completed.sav')
        jobs_continued = observation.with_name('city-jobs-continued.sav')
        subprocess.run(['dotnet', str(args.probe), '--local-city-jobs-reference', str(jobs_initial),
                        str(jobs_completed), str(jobs_continued)], check=True)
        state = wait(lambda s: s['menu']['groups']['File']['bounds'] is not None)
        menu_click(state, state['menu']['groups']['File']['bounds'])
        state = wait(lambda s: s['menu']['actions']['file.library']['bounds'] is not None)
        menu_click(state, state['menu']['actions']['file.library']['bounds'])
        import_save(jobs_initial)
        stored_control('open')
        wait(lambda s: not s['files']['visible'] and s['message'] == 'Classic save loaded.' and s['play']['turn'] == 1)

        def civilization_records(path):
            stored = json.loads(subprocess.check_output([
                'dotnet', str(args.cli), 'inspect', str(path), '--civilizations', '--json']))
            return [(record['slot'], record['bytes']) for record in stored['records']]

        def compare_city_jobs(expected):
            click('save')
            wait(lambda s: s['message'] == 'Classic save written.')
            actual = observation.with_name('native-' + expected.name)
            actual.write_bytes(saved_classic())
            assert civilization_records(actual) == civilization_records(expected), expected.name

        end_turn(2)
        compare_city_jobs(jobs_completed)
        click('load')
        wait(lambda s: s['message'] == 'Classic save loaded.')
        end_turn(3)
        compare_city_jobs(jobs_continued)
        checks.append('pollution goals and government scores match all direct-client civilization records after desktop turns and reload')

        # Match the rate reference's fresh-process creation and load history,
        # including the entity lifetime tokens carried by classic saves.
        driver.close_window(window)
        assert app.wait(timeout=10) == 0
        observation = observation.with_name('rates-observation.json')
        storage = storage.with_name('rates-storage')
        launch[launch.index('--observe') + 1] = str(observation)
        launch[launch.index('--storage-directory') + 1] = str(storage)
        app = subprocess.Popen(launch)
        wait(lambda s: s['map']['composition'] == 'cpu' and s['play']['turn'] == 1)
        window = driver.window_for(app.pid, visible=True)
        assert window
        driver.wait_placement(window)
        select(0)
        click('name')
        driver.chord('Control_L', 'a')
        type_text('Rates')
        command(lambda: click('found'))
        click('city.close')
        wait(lambda s: not s['play']['cityOpen'])

        rates_initial = observation.with_name('rates-initial.sav')
        rates_saved = observation.with_name('rates-saved.sav')
        rates_continued = observation.with_name('rates-continued.sav')
        rates_preferences = observation.with_name('rates-preferences.dat')
        subprocess.run(['dotnet', str(args.probe), '--local-rates-reference', str(rates_initial),
                        str(rates_saved), str(rates_continued), str(rates_preferences)], check=True)
        state = wait(lambda s: s['menu']['groups']['File']['bounds'] is not None)
        menu_click(state, state['menu']['groups']['File']['bounds'])
        state = wait(lambda s: s['menu']['actions']['file.library']['bounds'] is not None)
        menu_click(state, state['menu']['actions']['file.library']['bounds'])
        import_save(rates_initial)
        stored_control('open')
        wait(lambda s: not s['files']['visible'] and s['message'] == 'Classic save loaded.')
        click('rates')
        wait(lambda s: s['dialog']['visible'] and 'Maximum 60%' in s['prompt'])
        choice(5)
        wait(lambda s: 'Choose a rate to hold' in s['prompt'])
        choice(3)
        wait(lambda s: 'Hold: Luxury' in s['prompt'])
        choice(2)
        wait(lambda s: 'Choose Tax;' in s['prompt'])
        choice(6)
        draft = wait(lambda s: s['prompt'].startswith('Tax 60%') and 'Choose Tax;' not in s['prompt']
                     and s['dialog']['regions']['cancel']['width'] > 0)
        assert draft['play']['rates'] == {'tax': 4, 'science': 6, 'luxury': 0, 'maximum': 6}
        click_box(draft['dialog']['regions']['cancel'], draft)
        wait(lambda s: s['status'] == 'Cancelled' and not s['dialog']['visible'])
        click('rates')
        wait(lambda s: s['dialog']['visible'] and 'Hold: Luxury' in s['prompt'])
        choice(2)
        wait(lambda s: 'Choose Tax;' in s['prompt'])
        choice(6)
        wait(lambda s: s['prompt'].startswith('Tax 60%') and 'Choose Tax;' not in s['prompt'])
        choice(1)
        wait(lambda s: s['status'] == 'Completed' and not s['dialog']['visible'])
        checks.append('native rate editor balances with retained holds, cancels drafts and applies accepted rates')
        changed_rates = command(lambda: type_line('civilization.rates tax=6 science=4'))['play']
        assert changed_rates['rates'] == {'tax': 6, 'science': 4, 'luxury': 0, 'maximum': 6}
        assert (storage / 'CIV2.DAT').read_bytes() == rates_preferences.read_bytes()
        denied_rates = command(lambda: type_line('civilization.rates tax=6 science=6'), 'Failed')
        assert 'rates_not_available' in denied_rates['message']
        assert denied_rates['play']['rates'] == changed_rates['rates']
        click('save')
        wait(lambda s: s['message'] == 'Classic save written.')
        compare_work_save(rates_saved)
        click('load')
        wait(lambda s: s['message'] == 'Classic save loaded.')
        assert end_turn(2)['rates'] == changed_rates['rates']
        click('save')
        wait(lambda s: s['message'] == 'Classic save written.')
        compare_work_save(rates_continued)
        checks.append('native rate command, cap rejection, CIV2.DAT persistence and save/reload match the direct client')

        preference_file = storage / 'CIV2.DAT'
        preference_file.unlink()
        preference_file.mkdir()  # A directory cannot be replaced by the classic preference writer.
        failed_output = command(lambda: type_line('civilization.rates tax=4 science=6'), 'Failed')
        assert 'preferences_output_failed' in failed_output['message']
        assert failed_output['play']['rates'] == {'tax': 4, 'science': 6, 'luxury': 0, 'maximum': 6}
        assert 'civilization.rates' in failed_output['play']['effects']
        preference_file.rmdir()
        assert end_turn(3)['rates'] == failed_output['play']['rates']
        checks.append('native preference-output failure reports committed rates and the game continues')

        driver.close_window(window)
        assert app.wait(timeout=10) == 0
        observation = observation.with_name('government-observation.json')
        storage = storage.with_name('government-storage')
        launch[launch.index('--observe') + 1] = str(observation)
        launch[launch.index('--storage-directory') + 1] = str(storage)
        app = subprocess.Popen(launch)
        wait(lambda s: s['map']['composition'] == 'cpu' and s['play']['turn'] == 1)
        window = driver.window_for(app.pid, visible=True)
        assert window
        driver.wait_placement(window)
        select(0)
        click('name')
        driver.chord('Control_L', 'a')
        type_text('Council')
        command(lambda: click('found'))
        click('city.close')
        wait(lambda s: not s['play']['cityOpen'])
        click('revolution')
        wait(lambda s: s['dialog']['visible'] and s['prompt'].startswith('Start a revolution'))
        choice(1)
        declined = wait(lambda s: s['status'] == 'Completed' and not s['dialog']['visible'])
        assert declined['play']['government'] == 1
        click('revolution')
        wait(lambda s: s['dialog']['visible'] and s['prompt'].startswith('Start a revolution'))
        choice(0)
        anarchy = wait(lambda s: s['status'] == 'Completed' and s['play']['government'] == 0)
        assert not anarchy['play']['controls']['revolution']['enabled']

        government_initial = observation.with_name('government-initial.sav')
        government_selected = observation.with_name('government-selected.sav')
        government_continued = observation.with_name('government-continued.sav')
        subprocess.run(['dotnet', str(args.probe), '--local-government-reference', str(government_initial),
                        str(government_selected), str(government_continued)], check=True)
        state = wait(lambda s: s['menu']['groups']['File']['bounds'] is not None)
        menu_click(state, state['menu']['groups']['File']['bounds'])
        state = wait(lambda s: s['menu']['actions']['file.library']['bounds'] is not None)
        menu_click(state, state['menu']['actions']['file.library']['bounds'])
        import_save(government_initial)
        stored_control('open')
        wait(lambda s: not s['files']['visible'] and s['message'] == 'Classic save loaded.')
        assert end_turn(2)['government'] == 0
        assert end_turn(3)['government'] == 0
        click('end')
        wait(lambda s: s['status'] == 'AwaitingInteraction')
        choice(1)
        selection = wait(lambda s: s['prompt'] == 'Choose a new government.')
        assert selection['play']['turn'] == 4 and not selection['play']['phase']
        assert sorted(k for k in selection['dialog']['regions'] if k.startswith('choice.')) == ['choice.1']
        choice(1)
        rates = wait(lambda s: s['prompt'].startswith('Tax ') and s['dialog']['visible'])
        assert rates['play']['government'] == 1 and not rates['play']['phase']
        choice(1)
        wait(lambda s: s['status'] == 'Completed' and s['play']['phase'])
        click('save')
        wait(lambda s: s['message'] == 'Classic save written.')
        compare_work_save(government_selected)
        click('load')
        wait(lambda s: s['message'] == 'Classic save loaded.')
        assert end_turn(5)['government'] == 1
        click('save')
        wait(lambda s: s['message'] == 'Classic save written.')
        compare_work_save(government_continued)
        checks.append('native revolution, Anarchy reload, turn-four government/rate decisions and continuation match the direct client')
        driver.close_window(window)
        assert app.wait(timeout=10) == 0
        observation = observation.with_name('population-observation.json')
        storage = storage.with_name('population-storage')
        launch[launch.index('--observe') + 1] = str(observation)
        launch[launch.index('--storage-directory') + 1] = str(storage)
        app = subprocess.Popen(launch)
        wait(lambda s: s['map']['composition'] == 'cpu' and s['play']['turn'] == 1)
        window = driver.window_for(app.pid, visible=True)
        assert window
        driver.wait_placement(window)
        select(0)
        click('name')
        driver.chord('Control_L', 'a')
        type_text('Council')
        command(lambda: click('found'))
        click('city.close')
        wait(lambda s: not s['play']['cityOpen'])
        population_initial = observation.with_name('population-initial.sav')
        population_saved = observation.with_name('population-saved.sav')
        population_continued = observation.with_name('population-continued.sav')
        subprocess.run(['dotnet', str(args.probe), '--local-population-reference', str(population_initial),
                        str(population_saved), str(population_continued)], check=True)
        state = wait(lambda s: s['menu']['groups']['File']['bounds'] is not None)
        menu_click(state, state['menu']['groups']['File']['bounds'])
        state = wait(lambda s: s['menu']['actions']['file.library']['bounds'] is not None)
        menu_click(state, state['menu']['actions']['file.library']['bounds'])
        import_save(population_initial)
        stored_control('open')
        wait(lambda s: not s['files']['visible'] and s['message'] == 'Classic save loaded.')
        click('end')
        wait(lambda s: s['status'] == 'AwaitingInteraction')
        choice(1)
        population = wait(lambda s: s['prompt'].startswith('Your civilization has reached') and s['dialog']['visible'])
        assert '100,000' in population['prompt']
        assert population['play']['turn'] == 2 and not population['play']['phase']
        assert population['play']['gold'] >= 30000
        assert 'turn.advanced' not in population['play']['effects']
        assert not population['dialog']['regions']['cancel']['enabled']
        choice(0)
        wait(lambda s: s['status'] == 'Completed' and s['play']['phase'])
        click('save')
        wait(lambda s: s['message'] == 'Classic save written.')
        compare_work_save(population_saved)
        click('load')
        wait(lambda s: s['message'] == 'Classic save loaded.')
        assert end_turn(3)['phase']
        click('save')
        wait(lambda s: s['message'] == 'Classic save written.')
        compare_work_save(population_continued)
        checks.append('native treasury normalization and population notice suspend the turn, persist the milestone and match direct save/reload continuation')
        score_saved = observation.with_name('score-saved.sav')
        score_continued = observation.with_name('score-continued.sav')
        subprocess.run(['dotnet', str(args.probe), '--local-score-reference', str(score_saved), str(score_continued)], check=True)
        state = wait(lambda s: s['menu']['groups']['Advisors']['bounds'] is not None)
        menu_click(state, state['menu']['groups']['Advisors']['bounds'])
        state = wait(lambda s: s['menu']['actions']['civilization.score']['bounds'] is not None)
        menu_click(state, state['menu']['actions']['civilization.score']['bounds'])
        score = wait(lambda s: s['dialog']['visible'] and s['prompt'].startswith('Civilization score'))
        assert 'Civilization rating:' in score['prompt'] and 'pending' in score['prompt']
        assert score['play']['effects'].count('civilization.score.prepared') == 1
        assert not score['play']['controls']['save']['enabled']
        settled = wait(lambda s: s['serviceTicks'] >= score['serviceTicks'] + 4)
        assert settled['prompt'] == score['prompt']
        assert settled['play']['effects'].count('civilization.score.prepared') == 1
        choice(0)
        wait(lambda s: s['status'] == 'Completed' and not s['dialog']['visible'])
        click('save')
        wait(lambda s: s['message'] == 'Classic save written.')
        compare_work_save(score_saved)
        click('load')
        wait(lambda s: s['message'] == 'Classic save loaded.')
        end_turn(4)
        click('save')
        wait(lambda s: s['message'] == 'Classic save written.')
        compare_work_save(score_continued)
        checks.append('native score report prepares once, displays rating and pending ship points, and matches direct saved/resumed state')
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
    automation_identity = None
    if args.automation_desktop:
        script = Path(__file__).resolve().parents[2] / 'examples/automation/next-production.lua'
        observation = observation.with_name('automation-observation.json')
        storage = storage.with_name('automation-storage')
        script_copy = observation.with_name('native-production.lua')
        script_copy.write_bytes(script.read_bytes())
        trace = observation.with_name('production-trace.json')
        launch = ['dotnet', str(args.automation_desktop), '--script', str(script_copy), '--record', str(trace),
                  '--observe', str(observation), '--storage-directory', str(storage),
                  '--renderer-settings', str(observation.with_name('automation-renderer.json'))]
        if args.assets and args.fonts:
            launch += ['--assets', str(args.assets), '--fonts', str(args.fonts)]
        app = subprocess.Popen(launch)
        try:
            wait(lambda s: s['play']['automation']['status'] == 'Ready')
            window = driver.window_for(app.pid, visible=True)
            assert window
            driver.wait_placement(window)
            initial = observation.with_name('automation-initial.sav')
            expected = observation.with_name('automation-expected.sav')
            subprocess.run(['dotnet', str(args.probe), '--desktop-production-policy-reference',
                            str(initial), str(expected)], check=True)
            state = wait(lambda s: s['menu']['groups']['File']['bounds'] is not None)
            menu_click(state, state['menu']['groups']['File']['bounds'])
            state = wait(lambda s: s['menu']['actions']['file.library']['bounds'] is not None)
            menu_click(state, state['menu']['actions']['file.library']['bounds'])
            import_save(initial)
            covered_header = driver.pixel(window, 15, 45)
            stored_control('open')
            wait(lambda s: not s['files']['visible'] and s['message'] == 'Classic save loaded.' and s['play']['city'])
            # The observation can precede the compositor's first frame without the file overlay.
            wait(lambda s: driver.pixel(window, 15, 45) != covered_header)
            click('automation.start')
            wait(lambda s: s['play']['automation']['status'] == 'Running')
            click('automation.cancel')
            wait(lambda s: s['play']['automation']['status'] == 'Cancelled')
            click('automation.start')
            wait(lambda s: s['play']['automation']['status'] == 'Running')
            click('automation.pause')
            wait(lambda s: s['play']['automation']['status'] == 'Paused')
            assert end_turn(2)['city']['production'] == 2
            click('automation.pause')
            final = wait(lambda s: s['play']['automation']['status'] in ('Completed', 'Failed'))['play']
            assert final['automation'] == {'status': 'Completed', 'detail': 'Selected Outrider'}, final['automation']
            assert final['city']['production'] == 4
            click('save')
            wait(lambda s: s['message'] == 'Classic save written.')
            actual, reference = saved_classic(), expected.read_bytes()
            if actual != reference:
                args.report.parent.mkdir(parents=True, exist_ok=True)
                args.report.with_suffix('.automation-actual.sav').write_bytes(actual)
                args.report.with_suffix('.automation-expected.sav').write_bytes(reference)
                raise AssertionError('Desktop production policy save differs from the direct reference')
            click('automation.start')
            wait(lambda s: s['play']['automation']['status'] == 'Running')
            click('load')
            loaded = wait(lambda s: s['play']['automation']['status'] == 'Stopped')['play']
            assert loaded['automation']['detail'] == 'Game reloaded' and loaded['city']['production'] == 4
            click('save')
            wait(lambda s: s['message'] == 'Classic save written.')
            replay_expected = saved_classic()
            checks.append('optional desktop Lua starts, cancels, pauses and resumes, matches manual save bytes, and stops on reload')
            driver.close_window(window)
            assert app.wait(timeout=10) == 0
        finally:
            if app.poll() is None:
                app.terminate()
                app.wait(timeout=10)
        automation_identity = json.loads(Path(str(observation) + '.identity.json').read_text())
        validate(automation_identity, desktop_automation=True)
        artifacts.extend([args.automation_desktop, args.automation_desktop.with_name('OpenCiv2.Automation.dll'), script])
        script_copy.unlink()
        payload = json.loads(json.loads(trace.read_bytes())['payload'])
        assert payload['scriptSha256'] == hashlib.sha256(script.read_bytes()).hexdigest()
        assert any(step['kind'] == 'submit' and step['controller'] == 'Automation' for step in payload['steps'])

        def replay_save(recording, expected):
            output = recording.with_suffix('.sav')
            subprocess.run(['dotnet', str(args.automation_desktop), '--replay', str(recording),
                            '--output', str(output)], check=True)
            assert output.read_bytes() == expected

        replay_save(trace, replay_expected)
        checks.append('desktop production input journal replays without the script source and matches the reloaded save')

        interaction_script = observation.with_name('automation-interaction.lua')
        interaction_script.write_text("""
local command = game.command('turn.end')
while command.status == 'Pending' or command.status == 'AwaitingInteraction' do
    game.wait()
    command = game.result(command.id)
end
assert(command.status == 'Cancelled', command.status)
game.log('Turn cancelled')
""")
        launch[launch.index('--script') + 1] = str(interaction_script)
        interaction_trace = observation.with_name('interaction-trace.json')
        launch[launch.index('--record') + 1] = str(interaction_trace)
        observation = observation.with_name('automation-interaction.json')
        launch[launch.index('--observe') + 1] = str(observation)
        app = subprocess.Popen(launch)
        try:
            wait(lambda s: s['play']['automation']['status'] == 'Ready')
            window = driver.window_for(app.pid, visible=True)
            assert window
            driver.wait_placement(window)
            click('automation.start')
            decision = wait(lambda s: s['status'] == 'AwaitingInteraction' and s['dialog']['visible']
                            and s['dialog']['regions']['cancel']['width'] > 0)
            click_box(decision['dialog']['regions']['cancel'], decision)
            cancelled = wait(lambda s: s['play']['automation']['status'] in ('Completed', 'Failed'))['play']
            assert cancelled['automation'] == {'status': 'Completed', 'detail': 'Turn cancelled'}
            assert cancelled['turn'] == 1
            click('save')
            wait(lambda s: s['message'] == 'Classic save written.')
            interaction_expected = saved_classic()
            checks.append('desktop follows a script-submitted interaction and returns the human cancellation to Lua')
            driver.close_window(window)
            assert app.wait(timeout=10) == 0
        finally:
            if app.poll() is None:
                app.terminate()
                app.wait(timeout=10)
        interaction_script.unlink()
        replay_save(interaction_trace, interaction_expected)
        checks.append('desktop journal replays cancellation of a Lua command through the normal player dialog')

        memory_script = observation.with_name('automation-memory.lua')
        memory_script.write_text("""
local command = game.command('turn.end')
while command.status == 'Pending' or command.status == 'AwaitingInteraction' do
    game.wait()
    command = game.result(command.id)
end
assert(command.status == 'Completed', command.status)

local value = 'allocation'
while true do value = value .. value end
""")
        launch[launch.index('--script') + 1] = str(memory_script)
        record_option = launch.index('--record')
        del launch[record_option:record_option + 2]
        observation = observation.with_name('automation-memory.json')
        launch[launch.index('--observe') + 1] = str(observation)
        app = subprocess.Popen(launch)
        try:
            wait(lambda s: s['play']['automation']['status'] == 'Ready')
            window = driver.window_for(app.pid, visible=True)
            assert window
            driver.wait_placement(window)
            click('automation.start')
            wait(lambda s: s['status'] == 'AwaitingInteraction' and s['dialog']['visible'])
            choice(1)
            failed = wait(lambda s: s['play']['automation']['status'] == 'Failed')['play']
            assert failed['automation']['detail'] == 'memory_budget', failed['automation']
            assert failed['turn'] == 2 and failed['phase']
            assert end_turn(3)['turn'] == 3
            checks.append('desktop Lua heap exhaustion preserves the committed turn and the game keeps accepting commands')

            click('automation.start')
            wait(lambda s: s['status'] == 'AwaitingInteraction' and s['dialog']['visible'])
            children = Path(f'/proc/{app.pid}/task/{app.pid}/children')
            worker = next(int(pid) for pid in children.read_text().split()
                          if Path(f'/proc/{pid}/cmdline').read_bytes().endswith(b'--lua-worker\x00'))
            os.kill(worker, signal.SIGSTOP)
            try:
                choice(1)
                settled = wait(lambda s: s['play']['turn'] == 4 and s['play']['phase'])
                assert settled['play']['automation']['status'] == 'Running'
                timed_out = wait(lambda s: s['play']['automation']['status'] == 'Failed')['play']
                assert timed_out['automation']['detail'] == 'script_worker_timeout'
                assert timed_out['turn'] == 4 and timed_out['phase']
                assert end_turn(5)['turn'] == 5
                checks.append('stalled Lua worker leaves the turn dialog responsive and the watchdog ends the task')
            finally:
                try:
                    if Path(f'/proc/{worker}/cmdline').read_bytes().endswith(b'--lua-worker\x00'):
                        os.kill(worker, signal.SIGKILL)
                except (FileNotFoundError, ProcessLookupError):
                    pass
            driver.close_window(window)
            assert app.wait(timeout=10) == 0
        finally:
            if app.poll() is None:
                app.terminate()
                app.wait(timeout=10)
    hashes = {path.name: hashlib.sha256(path.read_bytes()).hexdigest() for path in artifacts if path}
    driver.close()
    return {'format': 'openciv2-linux-local-play/1', 'input': 'XTest pointer and keyboard', 'composition': 'cpu',
            'identity': identity, 'automationIdentity': automation_identity, 'sha256': hashes,
            'checks': checks, 'saved': semantic(saved), 'resumed': semantic(resumed)}


if __name__ == '__main__':
    main()
