#!/usr/bin/env python3
"""Exercise the production map showcase and scoped controls on an isolated Linux X11 display."""
import argparse
import ctypes as C
import hashlib
import json
import math
import os
from pathlib import Path
import platform
import subprocess
import sys
import tempfile
import time
from contextlib import contextmanager

from run_application_native import Event, X11
from run_application import map_content_inputs, validate


ROOT = Path(__file__).resolve().parents[2]
CASES = [f'MV{i:02}' for i in range(1, 31)] + ['Mixed']
# Fully show the selector grids at authored scale. Keep the mixed and input
# boards at the ordinary showcase size so navigation remains visible.
SELECTOR_BOARDS = set(CASES[:17]) | {'MV21', 'MV27', 'MV28'}
SELECTOR_WINDOW = ('--window', '2250x1650')


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


class Driver(X11):
    def click(self, window, box):
        # A managed Xwayland display needs a window-manager activation request;
        # raising an obscured client alone can leave the next click on its peer.
        atom = self.x.XInternAtom(self.display, b'_NET_ACTIVE_WINDOW', 0)

        def active():
            kind, count, after, fmt = C.c_ulong(), C.c_ulong(), C.c_ulong(), C.c_int()
            data = C.POINTER(C.c_ubyte)()
            self.x.XGetWindowProperty(self.display, self.root, atom, 0, 1, 0, 33,
                                     C.byref(kind), C.byref(fmt), C.byref(count), C.byref(after), C.byref(data))
            try:
                return C.cast(data, C.POINTER(C.c_ulong))[0] if count.value == 1 and fmt.value == 32 else None
            finally:
                if data:
                    self.x.XFree(data)

        current = active()
        if current is not None and current != window:
            event = Event()
            event.message.type = 33
            event.message.display = self.display
            event.message.window = window
            event.message.message_type = atom
            event.message.format = 32
            event.message.data[0] = 2  # Pager/tool activation of this owned client.
            self.x.XSendEvent(self.display, self.root, 0, (1 << 19) | (1 << 20), C.byref(event))
            self.x.XFlush(self.display)
            deadline = time.monotonic() + 3
            while active() != window:
                if time.monotonic() >= deadline:
                    raise RuntimeError('Window manager did not activate the owned test window')
                time.sleep(.005)
        super().click(window, box)

    def root_point(self, window, x, y):
        rx, ry, child = C.c_int(), C.c_int(), C.c_ulong()
        if not self.x.XTranslateCoordinates(self.display, window, self.root,
                                            int(x), int(y), C.byref(rx), C.byref(ry), C.byref(child)):
            raise RuntimeError('No root coordinates for owned window')
        return rx.value, ry.value

    def mouse(self, window, x, y):
        rx, ry = self.root_point(window, x, y)
        self.input.XTestFakeMotionEvent(self.display, -1, rx, ry, 0)
        self.x.XFlush(self.display)

    def press(self, number, down):
        self.input.XTestFakeButtonEvent(self.display, number, down, 0)
        self.x.XFlush(self.display)

    def key_edge(self, key, down):
        code = self.x.XKeysymToKeycode(self.display, self.x.XStringToKeysym(key.encode()))
        if not code:
            raise RuntimeError(f'Missing X11 key {key}')
        self.input.XTestFakeKeyEvent(self.display, code, down, 0)
        self.x.XFlush(self.display)

    def resize(self, window, width, height):
        self.x.XResizeWindow.argtypes = [C.c_void_p, C.c_ulong, C.c_uint, C.c_uint]
        self.x.XResizeWindow.restype = C.c_int
        self.x.XResizeWindow(self.display, window, width, height)
        self.x.XFlush(self.display)

    def position(self, window, x, y):
        move = self.x.XMoveWindow
        move.argtypes = [C.c_void_p, C.c_ulong, C.c_int, C.c_int]
        move.restype = C.c_int
        move(self.display, window, x, y)
        self.x.XFlush(self.display)


def wait(process, snapshot, predicate, timeout=20):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise RuntimeError(f'Showcase exited with {process.returncode}')
        if snapshot.exists():
            state = json.loads(snapshot.read_text())
            if predicate(state):
                return state
        time.sleep(.004)
    raise RuntimeError('Timed out waiting for owned showcase state ' + (snapshot.read_text() if snapshot.exists() else '(absent)'))


def rendered(state):
    draw = state.get('draw')
    return (draw and draw['tiles'] > 0 and all(state[key] == draw[key] for key in ('x', 'y', 'zoom', 'scaling')) and
            all(state['viewport'][dimension] == draw[dimension] for dimension in ('width', 'height')))


@contextmanager
def launch(args, driver, case, extras=()):
    with tempfile.TemporaryDirectory(prefix='oc2-native-gallery-') as work:
        snapshot = Path(work) / 'snapshot.json'
        command = [*args.adapter, '--assets', str(args.assets), '--fonts', str(args.fonts), '--showcase', case,
                   '--observe', str(snapshot), *extras]
        log = args.output / (case + '.log')
        with log.open('ab') as output:
            process = subprocess.Popen(command, stdout=output, stderr=subprocess.STDOUT)
            window = None
            try:
                first = wait(process, snapshot, rendered, 50)
                density = getattr(args, 'expected_density', None)
                if density is not None and first['scaling'] != density:
                    raise RuntimeError(f'Expected {density}× backing density, got {first["scaling"]}×')
                window = driver.window_for(process.pid)
                if window is None:
                    raise RuntimeError('No owned X11 showcase window')
                driver.wait_placement(window)
                identity = json.loads(Path(str(snapshot) + '.identity.json').read_text())
                validate(identity, native_aot=getattr(args, 'native_aot', False), source_root=getattr(args, 'source_root', None))
                yield process, window, snapshot, first, identity
            finally:
                if process.poll() is None and window is not None:
                    driver.close_window(window)
                    if process.wait(timeout=15):
                        raise RuntimeError(f'Failed to close native showcase {case}')
                elif process.poll() is None:
                    process.terminate()
                    process.wait(timeout=10)


def capture(driver, window, path):
    subprocess.run(['import', '-window', str(window), str(path)], check=True, timeout=20)
    return {'path': path.name, 'sha256': sha(path)}


def coords(state, control, u=.5, v=.5):
    box = state[control]
    return ((box['x'] + box['width'] * u) * state['scaling'],
            (box['y'] + box['height'] * v) * state['scaling'])


def click(driver, window, state, control, u=.5, v=.5):
    x, y = coords(state, control, u, v)
    driver.click(window, dict(x=x, y=y, width=0, height=0, enabled=True))


def interaction(args, driver):
    with launch(args, driver, 'MV25') as (process, window, snapshot, first, identity):
        checks = []
        click(driver, window, first, 'overview', .58, .45)
        overview = wait(process, snapshot, lambda s: rendered(s) and s['x'] != first['x'])
        checks.append(('pointer recenter', overview['draw']))
        driver.key('Right')
        cursor = wait(process, snapshot, lambda s: s['overviewFocused'] and s['overviewX'] != overview['overviewX'])
        driver.key('Return')
        key = wait(process, snapshot, lambda s: rendered(s) and s['x'] != overview['x'] and s['overviewX'] == cursor['overviewX'])
        checks.append(('keyboard overview recenter', key['draw']))

        x, y = coords(key, 'viewport', .35, .43)
        driver.mouse(window, x, y)
        driver.press(1, True)
        driver.mouse(window, x + 30, y + 24)
        time.sleep(.06)
        driver.press(1, False)
        pan = wait(process, snapshot, lambda s: rendered(s) and s['x'] != key['x'])
        checks.append(('native pointer drag', pan['draw']))

        driver.mouse(window, x, y)
        driver.key_edge('Shift_L', True)
        driver.press(1, True)
        driver.mouse(window, x, y + 34)
        time.sleep(.06)
        driver.press(1, False)
        driver.key_edge('Shift_L', False)
        zoom = wait(process, snapshot, lambda s: rendered(s) and s['zoom'] != pan['zoom'])
        if math.isclose(zoom['zoom'] / pan['zoom'], 1.25) or math.isclose(zoom['zoom'] / pan['zoom'], .8):
            raise RuntimeError('Smooth drag produced only a discrete wheel step')
        checks.append(('fractional zoom by Shift-drag', zoom['draw']))
        driver.key('Next')
        preset = wait(process, snapshot, lambda s: rendered(s) and math.isclose(s['zoom'], .35))
        checks.append(('keyboard zoom preset', preset['draw']))
        click(driver, window, preset, 'change')
        dirty = wait(process, snapshot, lambda s: rendered(s) and s['revision'] > preset['revision'] and s['draw']['epoch'] > preset['draw']['epoch'])
        checks.append(('changed world reaches active viewport', dirty['draw']))
        click(driver, window, dirty, 'recover')
        recovery = wait(process, snapshot, lambda s: rendered(s) and s['draw']['sequence'] > dirty['draw']['sequence'])
        checks.append(('texture recovery', recovery['draw']))
        return [dict(name=name, receipt=receipt) for name, receipt in checks], identity


def independent_windows(args, driver):
    fields = ('x', 'y', 'zoom', 'revision', 'viewport')

    def unchanged(before, after):
        return all(before[k] == after[k] for k in fields)

    with launch(args, driver, 'MV25', ('--window', '980x720')) as (first, first_window, first_path, left, _):
        with launch(args, driver, 'MV23', ('--window', '840x620')) as (other, other_window, other_path, right, _):
            click(driver, first_window, left, 'overview', .59, .48)
            recentered = wait(first, first_path, lambda s: rendered(s) and (s['x'], s['y']) != (left['x'], left['y']))
            if not unchanged(right, json.loads(other_path.read_text())):
                raise RuntimeError('Navigating the first window changed the other scene or camera')
            driver.resize(other_window, round(955 * right['scaling']), round(690 * right['scaling']))
            resized = wait(other, other_path, lambda s: rendered(s) and s['viewport']['width'] != right['viewport']['width'] and
                           s['draw']['sequence'] > right['draw']['sequence'])
            if not unchanged(recentered, json.loads(first_path.read_text())):
                raise RuntimeError('Resizing the other window changed the first scene or camera')
        click(driver, first_window, recentered, 'recover')
        recovered = wait(first, first_path, lambda s: rendered(s) and s['draw']['sequence'] > recentered['draw']['sequence'])
        return {'name': 'independent native windows, resize and remaining cache recovery',
                'first': recovered['draw'], 'other': resized['draw'], 'firstViewport': recovered['viewport'],
                'otherViewport': resized['viewport'], 'density': [recovered['scaling'], resized['scaling']]}


def mixed_density_windows(args, driver):
    def companion_matches(state, density):
        other = state.get('companion')
        return other and rendered(other) and other['scaling'] == density

    def view_state(state):
        return tuple(state[key] for key in ('case', 'x', 'y', 'zoom', 'viewport', 'revision', 'flat', 'scaling'))

    with launch(args, driver, 'MV29', ('--window', '980x720')) as (process, main, snapshot, first, identity):
        driver.position(main, 32, 42)
        click(driver, main, first, 'companionButton')
        opened = wait(process, snapshot, lambda s: companion_matches(s, 1))
        other = driver.window_for(process.pid, excluding=main)
        if other is None:
            raise RuntimeError('No second native window owned by the same process')
        driver.position(other, 1880, 48)
        split = wait(process, snapshot, lambda s: rendered(s) and s['scaling'] == 1 and companion_matches(s, 2))
        original = split['companion']
        if original['case'] != 'MV23' or opened['case'] != 'MV29':
            raise RuntimeError('Expected two distinct authored views in the same process')
        wrapped_image = capture(driver, other, args.output / 'overview-wrapped-2x.png')
        wrapped_footprint = inspect_footprint(original, args.output / wrapped_image['path'])
        if len(wrapped_footprint) != 2:
            raise RuntimeError('Expected both halves of the wrapped footprint')
        click(driver, other, original, 'overview', .62, .42)
        moved = wait(process, snapshot, lambda s: companion_matches(s, 2) and s['companion']['x'] != original['x'])
        if view_state(moved) != view_state(split):
            raise RuntimeError('Companion navigation altered the primary view')
        cursor = [moved['companion']['overviewX'], moved['companion']['overviewY']]
        click(driver, other, moved['companion'], 'viewport', .38, .42)
        selected = wait(process, snapshot, lambda s: companion_matches(s, 2) and
                        s['companion']['selectionKind'] is not None and not s['companion']['overviewFocused'] and
                        s['companion']['draw']['sequence'] > moved['companion']['draw']['sequence'])
        selected = selected['companion']
        pick = [selected['selectionX'], selected['selectionY']]
        selection_image = capture(driver, other, args.output / 'overview-map-selection-2x.png')
        path = args.output / selection_image['path']
        selection_ink = (228, 219, 160)
        if pick == cursor or overview_cell_pixel(selected, path, pick) != selection_ink or \
                overview_cell_pixel(selected, path, cursor) == selection_ink:
            raise RuntimeError('Overview retained its old cursor instead of showing the main-map selection')

        click(driver, other, selected, 'flatToggle')
        flattened = wait(process, snapshot, lambda s: companion_matches(s, 2) and s['companion']['flat'] and
                         s['companion']['draw']['epoch'] > moved['companion']['draw']['epoch'])
        flat_image = capture(driver, other, args.output / 'overview-flat-2x.png')
        flat_footprint = inspect_footprint(flattened['companion'], args.output / flat_image['path'])
        driver.resize(other, 1900, 1340)
        resized = wait(process, snapshot, lambda s: companion_matches(s, 2) and
                       s['companion']['viewport']['width'] != flattened['companion']['viewport']['width'])
        if view_state(resized) != view_state(split):
            raise RuntimeError('Companion scene replacement or resize altered the primary view')
        right_image = capture(driver, other, args.output / 'mixed-dpi-companion.png')
        resized_footprint = inspect_footprint(resized['companion'], args.output / right_image['path'])
        if len(flat_footprint) != 1 or len(resized_footprint) != 1 or resized_footprint == flat_footprint:
            raise RuntimeError('Overview footprint did not reflect the changed flat viewport dimensions')

        driver.mouse(other, *coords(resized['companion'], 'viewport', .38, .42))
        driver.press(4, True)
        driver.press(4, False)
        zoomed = wait(process, snapshot, lambda s: companion_matches(s, 2) and
                      s['companion']['zoom'] > resized['companion']['zoom'])
        zoomed_image = capture(driver, other, args.output / 'overview-zoomed-2x.png')
        zoomed_footprint = inspect_footprint(zoomed['companion'], args.output / zoomed_image['path'])
        if zoomed_footprint == resized_footprint:
            raise RuntimeError('Overview footprint did not respond to companion zoom')

        # Move the same live companion back onto the 1× monitor. Test the
        # actual draw receipt so a fresh layout alone cannot pass.
        driver.position(other, 72, 82)
        migrated = wait(process, snapshot, lambda s: companion_matches(s, 1) and
                         s['companion']['draw']['sequence'] > zoomed['companion']['draw']['sequence'])
        if view_state(migrated) != view_state(split):
            raise RuntimeError('Moving the companion across displays altered primary rendering')
        driver.close_window(other)
        closed = wait(process, snapshot, lambda s: s.get('companion') is None and rendered(s))
        if view_state(closed) != view_state(split):
            raise RuntimeError('Closing the companion altered primary rendering')
        click(driver, main, closed, 'recover')
        warmed = wait(process, snapshot, lambda s: rendered(s) and s['changedDraw']['sequence'] > closed['draw']['sequence'] and
                      s['changedDraw']['preparedChunks'] > 0)
        if view_state(warmed) != view_state(split):
            raise RuntimeError('Primary cache recovery changed its displayed map')
        left_image = capture(driver, main, args.output / 'mixed-dpi-primary.png')
        return [{'name': 'independent maps in one process switch native backing scale and release on close',
                 'sameProcess': True, 'primary': warmed['changedDraw'], 'companionAt2x': resized['companion']['draw'],
                 'companionAt1x': migrated['companion']['draw'], 'images': [left_image, right_image]},
                {'name': 'overview outline pixels follow wrap, flat resize, and zoom',
                 'images': [wrapped_image, flat_image, right_image, zoomed_image],
                 'outlines': [wrapped_footprint, flat_footprint, resized_footprint, zoomed_footprint]},
                {'name': 'overview reflects main-map selection after focus leaves its navigation cursor',
                 'image': selection_image, 'selection': pick, 'formerCursor': cursor}], identity


def overview_cell_coordinates(state, point):
    box = state['overview']
    density = state['scaling']
    width, height = state['worldWidth'], state['worldHeight']
    scale = min((box['width'] - 8) / (width * 2), (box['height'] - 8) / height)
    x = box['x'] + (box['width'] - 2 * width * scale) / 2 + (point[0] // 2 + .5) * 4 * scale
    y = box['y'] + (box['height'] - height * scale) / 2 + (point[1] + .5) * scale
    return round(x * density), round(y * density)


def overview_cell_pixel(state, path, point):
    from PIL import Image

    with Image.open(path) as image:
        return image.convert('RGB').getpixel(overview_cell_coordinates(state, point))


def inspect_footprint(state, path):
    from PIL import Image

    ov, view = state['overview'], state['viewport']
    density, zoom = state['scaling'], state['zoom']
    world_width, world_height = state['worldWidth'], state['worldHeight']
    scale = min((ov['width'] - 8) / (world_width * 2), (ov['height'] - 8) / world_height)
    margin_x = (ov['width'] - world_width * 2 * scale) / 2
    margin_y = (ov['height'] - world_height * scale) / 2
    left = -state['x'] / (32 * zoom)
    span = view['width'] / (32 * zoom)
    top = max(0, -state['y'] / (16 * zoom))
    bottom = min(world_height, (view['height'] - state['y']) / (16 * zoom))
    if state['flat']:
        segments = [(max(0, left), min(world_width, left + span))]
    elif span >= world_width:
        segments = [(0, world_width)]
    else:
        left %= world_width
        segments = [(left, min(world_width, left + span))]
        if left + span > world_width:
            segments.append((0, left + span - world_width))
    outlines = [[(margin_x + a * 2 * scale) * density, (margin_y + top * scale) * density,
                 (margin_x + b * 2 * scale) * density, (margin_y + bottom * scale) * density] for a, b in segments]
    with Image.open(path) as image:
        x = round(ov['x'] * density)
        y = round(ov['y'] * density)
        rgb = image.convert('RGB').crop((x, y, x + round(ov['width'] * density), y + round(ov['height'] * density)))
        pixels = rgb.tobytes()
        ink = bytes((255, 240, 209))
        painted = {((i // 3) % rgb.width, (i // 3) // rgb.width)
                   for i in range(0, len(pixels), 3) if pixels[i:i + 3] == ink}
    for x1, y1, x2, y2 in outlines:
        for x, y in [(x1, (y1 + y2) / 2), (x2, (y1 + y2) / 2),
                     ((x1 + x2) / 2, y1), ((x1 + x2) / 2, y2)]:
            if not any(abs(a + .5 - x) < 2.5 and abs(b + .5 - y) < 2.5 for a, b in painted):
                raise RuntimeError(f'Overview outline is missing at {(x, y)} in {path.name}')
    for x, y in painted:
        if not any(x1 - 2 <= x <= x2 + 2 and y1 - 2 <= y <= y2 + 2 and
                   min(abs(x - x1), abs(x - x2), abs(y - y1), abs(y - y2)) <= 2
                   for x1, y1, x2, y2 in outlines):
            raise RuntimeError(f'Unexpected or stale overview outline at {(x, y)} in {path.name}')
    return outlines


def camera_edge_input(args, driver):
    def point(state, x, y):
        box = state['viewport']
        scale = state['scaling']
        return (round((box['x'] + x) * scale), round((box['y'] + y) * scale))

    def select(window, xy):
        driver.click(window, dict(x=xy[0], y=xy[1], width=0, height=0, enabled=True))

    with launch(args, driver, 'MV25') as (process, window, snapshot, first, _):
        click(driver, window, first, 'bareToggle')
        click(driver, window, first, 'flatToggle')
        flat = wait(process, snapshot, lambda s: rendered(s) and s['flat'] and s['bare'])
        xy = point(flat, flat['x'] + 6 * 32 * flat['zoom'], flat['y'] + 4 * 16 * flat['zoom'])
        select(window, xy)
        picked = wait(process, snapshot, lambda s: (s['selectionX'], s['selectionY'], s['selectionKind']) == (6, 4, 'Tile'))
        driver.mouse(window, *xy)
        driver.press(4, True)
        driver.press(4, False)
        zoomed = wait(process, snapshot, lambda s: rendered(s) and s['zoom'] != picked['zoom'])
        anchor = (xy[0] / flat['scaling'] - flat['viewport']['x'], xy[1] / flat['scaling'] - flat['viewport']['y'])
        if not math.isclose(zoomed['zoom'] / picked['zoom'], 1.25) or any(not math.isclose(
                (p - picked[key]) / picked['zoom'], (p - zoomed[key]) / zoomed['zoom'], abs_tol=1e-7)
                for p, key in zip(anchor, ('x', 'y'))):
            raise RuntimeError('Wheel zoom moved the picked world position from its pointer anchor')
        driver.key('Escape')
        wait(process, snapshot, lambda s: s['selectionKind'] is None)
        select(window, xy)
        anchored = wait(process, snapshot, lambda s: (s['selectionX'], s['selectionY']) == (6, 4))
        driver.key('Home')
        state = wait(process, snapshot, lambda s: rendered(s) and math.isclose(s['zoom'], 1))

        for _ in range(3):
            start = coords(state, 'viewport', .07, .12)
            end = coords(state, 'viewport', .92, .9)
            driver.mouse(window, *start)
            driver.press(1, True)
            driver.mouse(window, *end)
            time.sleep(.07)
            driver.press(1, False)
            if (math.isclose(state['x'], state['viewport']['width'] - 32) and
                    math.isclose(state['y'], state['viewport']['height'] - 16)):
                break
            state = wait(process, snapshot, lambda s: rendered(s) and (s['x'], s['y']) != (state['x'], state['y']))
        if not (math.isclose(state['x'], state['viewport']['width'] - 32) and
                math.isclose(state['y'], state['viewport']['height'] - 16)):
            raise RuntimeError('Flat pan overshot or failed to reach its physical viewport clamp')
        clamped = state['draw']
        driver.mouse(window, *coords(state, 'viewport', .5, .63))
        driver.press(1, True)
        driver.mouse(window, *coords(state, 'viewport', .5, .32))
        time.sleep(.07)
        driver.press(1, False)
        state = wait(process, snapshot, lambda s: rendered(s) and s['y'] < state['y'] and s['x'] == state['x'])
        outside = point(state, state['x'] - 33, state['y'])
        select(window, outside)
        wait(process, snapshot, lambda s: s['selectionKind'] is None)
        for xy in (point(state, state['x'] - 32, state['y']), point(state, state['x'], state['y'] - 16)):
            select(window, xy)
            state = wait(process, snapshot, lambda s: (s['selectionX'], s['selectionY']) == (0, 0))
            driver.key('Escape')
            wait(process, snapshot, lambda s: s['selectionKind'] is None)

        click(driver, window, state, 'flatToggle')
        wrapped = wait(process, snapshot, lambda s: rendered(s) and not s['flat'])
        click(driver, window, wrapped, 'overview', .45, .44)
        recentered = wait(process, snapshot, lambda s: rendered(s) and s['x'] != wrapped['x'])
        select(window, point(recentered, 60, 12))
        driver.key('Next')
        distant = wait(process, snapshot, lambda s: rendered(s) and math.isclose(s['zoom'], .35))
        driver.mouse(window, *point(distant, distant['viewport']['width'] / 2, distant['viewport']['height'] / 2))
        driver.press(5, True)
        driver.press(5, False)
        distant = wait(process, snapshot, lambda s: rendered(s) and math.isclose(s['zoom'], .28))
        zoom = distant['zoom']
        period = distant['worldWidth'] * 32 * zoom
        y = distant['y'] + 4 * 16 * zoom
        if y < 2 or y >= distant['viewport']['height'] - 2:
            driver.key('Up')
            distant = wait(process, snapshot, lambda s: rendered(s) and s['y'] != distant['y'])
            y = distant['y'] + 4 * 16 * zoom
        origin = distant['x'] + 6 * 32 * zoom
        first_copy = (origin - 2) % period + 2
        copies = [point(distant, first_copy + i * period, y) for i in range(2)]
        for xy in copies:
            select(window, xy)
            wait(process, snapshot, lambda s: (s['selectionX'], s['selectionY']) == (6, 4))
            driver.key('Escape')
            wait(process, snapshot, lambda s: s['selectionKind'] is None)
        result = {'name': 'native wheel anchor, flat clamps and vertices, fractional wrap and tall-art picks',
                  'density': distant['scaling'], 'anchorDraw': anchored['draw'], 'flatClampDraw': clamped, 'edgeDraw': state['draw'],
                  'wrappedDraw': distant['draw'], 'wrappedPhysicalPicks': copies}

    with launch(args, driver, 'MV10') as (process, window, snapshot, first, _):
        select(window, point(first, 270, 135))
        driver.key('Next')
        distant = wait(process, snapshot, lambda s: rendered(s) and math.isclose(s['zoom'], .35))
        driver.key('Escape')
        wait(process, snapshot, lambda s: s['selectionKind'] is None)
        zoom = distant['zoom']
        # The first figure is at (4, 4). Its body extends above the
        # diamond, whose north vertex is only sixteen world pixels up.
        xy = point(distant, distant['x'] + 4 * 32 * zoom, distant['y'] + (4 * 16 - 24) * zoom)
        select(window, xy)
        body = wait(process, snapshot, lambda s: (s['selectionX'], s['selectionY'], s['selectionKind']) == (4, 4, 'Unit'))
        click(driver, window, body, 'bareToggle')
        bare = wait(process, snapshot, lambda s: rendered(s) and s['bare'])
        select(window, xy)
        terrain = wait(process, snapshot, lambda s: s['selectionKind'] == 'Tile')
        if (terrain['selectionX'], terrain['selectionY']) == (4, 4):
            raise RuntimeError('Tall-art hit point should be beyond the unit tile diamond')
        result.update(tallArtPhysicalPick=xy, bodyPick=[body['selectionX'], body['selectionY']],
                      barePick=[terrain['selectionX'], terrain['selectionY']])
    return result


def inspect_and_switch_modes(args, driver):
    with launch(args, driver, 'MV21') as (process, window, snapshot, first, _):
        def tile(x, y):
            box = first['viewport']
            return ((box['x'] + first['x'] + x * 32 * first['zoom']) * first['scaling'],
                    (box['y'] + first['y'] + y * 16 * first['zoom']) * first['scaling'])

        facts = [
            ('Unit 0', 'Accumulated work 0'), ('Unit 1', 'Accumulated work 9'),
            ('Unit 2', 'Accumulated work 231'), ('Unit 3', 'Work counter unavailable'),
            ('Seen unit', 'Work counter withheld'), ('Workshop', 'Population 4', 'No garrison'),
            ('Old Harbor', 'Remembered population 5'), ('Remembered city site',),
        ]
        descriptions = []
        for x, expected in zip(range(4, 20, 2), facts):
            driver.move(window, *tile(x, 4))
            pointed = wait(process, snapshot, lambda s: all(word in s.get('inspection', '') for word in expected)
                           and s.get('inspection', '').endswith(f'({x}, 4)'))
            description = pointed['inspection']
            if 'turns remaining' in description.casefold():
                raise RuntimeError('Native inspection interprets work as turns remaining')
            descriptions.append(description)

        x, y = tile(4, 4)
        driver.click(window, dict(x=x, y=y, width=0, height=0, enabled=True))
        wait(process, snapshot, lambda s: (s['selectionX'], s['selectionY']) == (4, 4) and
             s.get('inspection') == descriptions[0])
        driver.key_edge('Control_L', True)
        try:
            for description in descriptions[1:]:
                driver.key('Right')
                wait(process, snapshot, lambda s: s.get('inspection') == description)
        finally:
            driver.key_edge('Control_L', False)
        inspection = {'name': 'worker and city facts match native pointer and keyboard inspection',
                      'density': first['scaling'], 'inspection': descriptions}

    with launch(args, driver, 'MV19') as (process, window, snapshot, first, _):
        click(driver, window, first, 'bareToggle')
        bare = wait(process, snapshot, lambda s: rendered(s) and s.get('bare') and s['draw']['objects'] == 0)
        click(driver, window, bare, 'bareToggle')
        restored = wait(process, snapshot, lambda s: rendered(s) and not s.get('bare') and
                        s['draw']['objects'] == first['draw']['objects'])
        if any(first[k] != restored[k] for k in ('x', 'y', 'zoom', 'revision')):
            raise RuntimeError('Display modes changed the world or camera')
        display = {'name': 'bare and full display toggle preserves world and camera',
                   'bare': bare['draw'], 'restored': restored['draw']}
    return [inspection, display]


def inspect_knowledge_region(args, driver):
    with launch(args, driver, 'MV09') as (process, window, snapshot, first, _):
        def point(state, world_x):
            box = state['viewport']
            return dict(x=(box['x'] + state['x'] + world_x * 32 * state['zoom']) * state['scaling'],
                        y=(box['y'] + state['y'] + 4 * 16 * state['zoom']) * state['scaling'],
                        width=0, height=0, enabled=True)

        driver.click(window, point(first, 10))
        wait(process, snapshot, lambda s: s['selectionX'] == 10 and s['inspection'].endswith('(10, 4)'))
        click(driver, window, first, 'change')
        clipped = wait(process, snapshot, lambda s: rendered(s) and s['revision'] > first['revision'] and s['selectionX'] is None)
        driver.click(window, point(clipped, 4))
        inside = wait(process, snapshot, lambda s: s['selectionX'] == 4 and s['inspection'].endswith('(4, 4)'))
        driver.click(window, point(inside, 10))
        excluded = wait(process, snapshot, lambda s: s['selectionX'] is None and not s['inspection'].endswith('(10, 4)'))
        click(driver, window, excluded, 'change')
        restored = wait(process, snapshot, lambda s: rendered(s) and s['revision'] > clipped['revision'])
        driver.click(window, point(restored, 10))
        resumed = wait(process, snapshot, lambda s: s['selectionX'] == 10 and s['inspection'].endswith('(10, 4)'))
        return {'name': 'native region clears stale selection and clips pointer inspection',
                'density': first['scaling'], 'inside': inside['inspection'], 'restored': resumed['inspection']}


def reassign_owner_color(args, driver):
    with launch(args, driver, 'MV17') as (process, window, snapshot, first, _):
        box = first['viewport']
        density, zoom = first['scaling'], first['zoom']
        def world_point(point):
            return ((box['x'] + first['x'] + point[0] * 32 * zoom) * density,
                    (box['y'] + first['y'] + point[1] * 16 * zoom) * density)

        x, y = world_point((10, 10))
        territory = world_point((10, 28))
        other_territory = world_point((16, 28))
        driver.click(window, dict(x=x, y=y, width=0, height=0, enabled=True))
        picked = wait(process, snapshot, lambda s: s['selectionKind'] == 'City' and
                      s['selectionX'] == 10 and 'Civilization 1' in s['inspection'])
        before = capture(driver, window, args.output / 'MV17-before-change.png')
        old_fill = driver.pixel(window, *territory)
        other_fill = driver.pixel(window, *other_territory)
        click(driver, window, picked, 'change')
        changed = wait(process, snapshot, lambda s: rendered(s) and s['revision'] == first['revision'] + 1
                       and s['draw']['epoch'] != first['draw']['epoch'])
        driver.move(window, x, y)
        restored = wait(process, snapshot, lambda s: s['inspection'] == picked['inspection'])
        after = capture(driver, window, args.output / 'MV17-after-change.png')
        new_fill = driver.pixel(window, *territory)
        if old_fill == new_fill or driver.pixel(window, *other_territory) != other_fill:
            raise RuntimeError('Native main-map ownership fill did not recolor only the chosen civilization')
        owner = (10, 16)
        neighbor = (16, 16)
        old = overview_cell_pixel(picked, args.output / before['path'], owner)
        new = overview_cell_pixel(restored, args.output / after['path'], owner)
        if (old, new) != ((238, 228, 191), (118, 180, 112)):
            raise RuntimeError(f'Overview did not apply the new role color: {old} to {new}')
        if overview_cell_pixel(picked, args.output / before['path'], neighbor) != overview_cell_pixel(restored, args.output / after['path'], neighbor):
            raise RuntimeError('Native owner color change recolored another civilization')
        if any(picked[key] != restored[key] for key in ('x', 'y', 'zoom', 'selectionX', 'selectionY', 'selectionKind')):
            raise RuntimeError('Native owner color change moved the camera or selected city')
        return {'name': 'native owner role change preserves identity and recolors overview and main map',
                'density': density, 'before': before, 'after': after, 'colors': [old, new], 'inspection': restored['inspection'],
                'draw': changed['draw'], 'territoryPixels': [old_fill, new_fill]}


def inspect_object_facts(args, driver):
    cases = [
        ('MV12', 'Unit health facts', [((4, 4), ('Health 10/10',)),
                                      ((4, 10), ('Health unavailable',)),
                                      ((10, 10), ('Health withheld',))]),
        ('MV13', 'Unit order facts', [((10, 4), ('Order Fortifying',)),
                                     ((22, 10), ('Order GoTo', 'Automated'))]),
        ('MV16', 'Own and remembered city facts', [((10, 4), ('Harbor 2', 'Population 3')),
                                                  ((16, 4), ('Harbor 3', 'Remembered population 4'))]),
    ]
    reports = []
    for case, name, targets in cases:
        with launch(args, driver, case) as (process, window, snapshot, first, _):
            viewport = first['viewport']
            descriptions = []
            for (x, y), facts in targets:
                px = (viewport['x'] + first['x'] + x * 32 * first['zoom']) * first['scaling']
                py = (viewport['y'] + first['y'] + y * 16 * first['zoom']) * first['scaling']
                driver.move(window, px, py)
                found = wait(process, snapshot, lambda s: all(word in s.get('inspection', '') for word in facts) and
                             s.get('inspection', '').endswith(f'({x}, {y})'))
                descriptions.append(found['inspection'])
            reports.append({'name': name, 'density': first['scaling'], 'inspection': descriptions})
    return reports


def track_active_unit(args, driver):
    def position(state):
        zoom = state['zoom']
        width = state['viewport']['width']
        period = state['worldWidth'] * 32 * zoom
        x = state['x'] + state['activeX'] * 32 * zoom
        x += round((width / 2 - x) / period) * period
        return x, state['y'] + state['activeY'] * 16 * zoom

    with launch(args, driver, 'MV25') as (process, window, snapshot, first, _):
        x, y = position(first)
        if 0 < x < first['viewport']['width'] and 0 < y < first['viewport']['height']:
            raise RuntimeError('The authored active unit should begin outside the displayed window')
        click(driver, window, first, 'followToggle')
        followed = wait(process, snapshot, lambda s: rendered(s) and s.get('follow') and s['x'] != first['x'])
        x, y = position(followed)
        if not (math.isclose(x, followed['viewport']['width'] / 2) and math.isclose(y, followed['viewport']['height'] / 2)):
            raise RuntimeError('Follow mode did not center the nearest displayed copy')
        click(driver, window, followed, 'followToggle')
        disabled = wait(process, snapshot, lambda s: rendered(s) and not s.get('follow'))
        click(driver, window, disabled, 'followToggle')
        unchanged = wait(process, snapshot, lambda s: rendered(s) and s.get('follow'))
        if any(unchanged[k] != followed[k] for k in ('x', 'y', 'zoom', 'revision')):
            raise RuntimeError('Follow mode moved the camera for an already visible unit')
        return {'name': 'active unit follows the nearest copy and preserves a comfortable camera',
                'before': first['draw'], 'after': unchanged['draw'], 'density': unchanged['scaling']}


def change_visible_world(args, driver):
    with launch(args, driver, 'MV29') as (process, window, snapshot, first, _):
        click(driver, window, first, 'change')
        state = wait(process, snapshot, lambda s: rendered(s) and s['revision'] > first['revision'] and
                     s['changedDraw']['epoch'] > first['changedDraw']['epoch'])
        width, height = state['viewport']['width'], state['viewport']['height']
        x = state['x'] + state['updateX'] * 32 * state['zoom']
        y = state['y'] + state['updateY'] * 16 * state['zoom']
        if not (0 < x < width and 0 < y < height and state['changedDraw']['effects'] > 0):
            raise RuntimeError('World change did not reach the visible tile with an active effect')
        return {'name': 'visible dirty terrain and effect in the same native frame',
                'position': [state['updateX'], state['updateY']], 'draw': state['changedDraw']}


def presentation_input(args, driver):
    from PIL import Image

    def playing(state):
        return rendered(state) and state['draw']['moving'] == 1 and state['draw']['notices'] == 2 and state['draw']['effects'] >= 3

    def sample(state, stage):
        path = args.output / f'MV24-presentation-{stage}.png'
        receipt = capture(driver, window, path)
        box = state['viewport']
        density = state['scaling']
        with Image.open(path) as image:
            rgb = image.convert('RGB')
            masks = []
            ink = bytes((255, 240, 209))
            # Opaque glyph pixels keep their color over any world layer under the HUD.
            for top in (12, 41):
                x, y = round((box['x'] + 12) * density), round((box['y'] + top) * density)
                cropped = rgb.crop((x, y, x + round(330 * density), y + round(25 * density)))
                pixels = cropped.tobytes()
                masks.append(tuple(pixels[i:i + 3] == ink for i in range(0, len(pixels), 3)))
        receipt['glyphPixels'] = [sum(mask) for mask in masks]
        return receipt, masks

    with launch(args, driver, 'MV24') as (process, window, snapshot, first, _):
        click(driver, window, first, 'play')
        active = wait(process, snapshot, playing)
        before, masks = sample(active, 'before')
        if min(before['glyphPixels']) < 40:
            raise RuntimeError('Both authored notices must render readable glyph pixels')
        x, y = coords(active, 'viewport', .58, .48)
        driver.mouse(window, x, y)
        driver.press(1, True)
        driver.mouse(window, x + 24 * active['scaling'], y)
        driver.press(1, False)
        panned = wait(process, snapshot, lambda s: playing(s) and s['x'] != active['x'])
        after_pan, pan_masks = sample(panned, 'pan')

        driver.mouse(window, x, y)
        driver.press(4, True)
        driver.press(4, False)
        zoomed = wait(process, snapshot, lambda s: playing(s) and s['zoom'] > panned['zoom'])
        after_zoom, zoom_masks = sample(zoomed, 'zoom')
        if masks != pan_masks or masks != zoom_masks or any(s['revision'] != first['revision'] for s in (active, panned, zoomed)):
            raise RuntimeError('Map input moved the viewport notices or changed the authored world')
        anchored = {'name': 'two notice glyph masks remain fixed during input with moving and combat effects',
                    'before': before, 'pan': after_pan, 'zoom': after_zoom, 'draw': zoomed['draw']}

        click(driver, window, zoomed, 'clear')
        cleared = wait(process, snapshot, lambda s: rendered(s) and all(s['draw'][k] == 0 for k in ('moving', 'effects', 'notices')))
        cancelled = {'name': 'native cancellation clears notices, movement and combat effects', 'draw': cleared['draw']}

        click(driver, window, cleared, 'play')
        active = wait(process, snapshot, playing)
        click(driver, window, active, 'motionToggle')
        quiet = wait(process, snapshot, lambda s: rendered(s) and s['reducedMotion'] and
                     s['draw']['moving'] == 0 and s['draw']['effects'] == 0 and s['draw']['notices'] == 2)
        if quiet['draw']['objects'] <= active['draw']['objects'] or quiet['revision'] != first['revision']:
            raise RuntimeError('Reduced motion failed to restore the settled unit in the same world')
        reduced = {'name': 'native reduced motion skips effects, restores the unit and preserves notices', 'draw': quiet['draw']}
        click(driver, window, quiet, 'motionToggle')
        resumed = wait(process, snapshot, lambda s: rendered(s) and not s['reducedMotion'])
        click(driver, window, resumed, 'clear')
        ready = wait(process, snapshot, lambda s: rendered(s) and s['draw']['notices'] == 0)
        click(driver, window, ready, 'play')
        replacing = wait(process, snapshot, playing)
        click(driver, window, replacing, 'selector')
        driver.key('Home')
        driver.key('Return')
        replaced = wait(process, snapshot, lambda s: rendered(s) and s['case'] == 'MV01' and
                        s['draw']['epoch'] > replacing['draw']['epoch'] and
                        all(s['draw'][k] == 0 for k in ('moving', 'effects', 'notices')))
        replacement = {'name': 'replacing the binding interrupts active notices and effects', 'draw': replaced['draw']}
        return [anchored, cancelled, reduced, replacement]


def pinned_map_text(args, driver):
    from PIL import Image

    with launch(args, driver, 'MV27') as (process, window, snapshot, first, _):
        def playing(s):
            return rendered(s) and s['draw']['notices'] == 1 and s['draw']['labels'] > 0

        def sample(state, stage):
            path = args.output / f'MV27-pinned-text-{stage}.png'
            receipt = capture(driver, window, path)
            box = state['viewport']
            scale = state['scaling']
            left = round((box['x'] + 12) * scale)
            top = round((box['y'] + 12) * scale)
            with Image.open(path) as image:
                rgb = image.convert('RGB').crop((left, top, left + round(280 * scale), top + round(25 * scale))).tobytes()
            mask = [i for i in range(0, len(rgb), 3) if rgb[i:i + 3] == b'\xff\xf0\xd1']
            return receipt, mask

        click(driver, window, first, 'play')
        ready = wait(process, snapshot, playing)
        before, mask = sample(ready, 'before')
        x, y = coords(ready, 'viewport', .55, .45)
        driver.mouse(window, x, y)
        driver.press(5, True)
        driver.press(5, False)
        after = wait(process, snapshot, lambda s: playing(s) and s['zoom'] < ready['zoom'])
        frame, zoomed = sample(after, 'zoom')
        if len(mask) < 65 or mask != zoomed:
            raise RuntimeError('Pinned map text changed size or placement with world zoom')
        return {'name': 'native pinned fallback text stays at viewport scale',
                'density': first['scaling'], 'glyphPixels': len(mask), 'before': before, 'zoom': frame}


def live_art_replacement(args, driver):
    from PIL import Image

    grade = ['--map-art', str(args.art_cases / 'native'), '--scenario-art', str(args.art_cases / 'scenario'),
             '--installation-art', str(args.art_cases / 'installation')]
    with launch(args, driver, 'MV30', grade) as (process, window, snapshot, first, _):
        click(driver, window, first, 'viewport', .36, .40)
        selected = wait(process, snapshot, lambda s: rendered(s) and s['selectionX'] is not None)
        key = ('x', 'y', 'zoom', 'revision', 'selectionX', 'selectionY', 'selectionKind')

        def sample(state, stage):
            path = args.output / f'MV29-theme-{stage}.png'
            image = capture(driver, window, path)
            box, scale = state['viewport'], state['scaling']
            x, y = round(box['x'] * scale), round(box['y'] * scale)
            with Image.open(path) as p:
                pixels = p.convert('RGB').crop((x, y, x + round(box['width'] * scale), y + round(box['height'] * scale)))
                # X11 capture can meet the pointer-exit frame. Compare the
                # art separately from the hover highlight at the selected tile.
                zoom = state['zoom']
                cx = (state['x'] + state['selectionX'] * 32 * zoom) * scale
                cy = (state['y'] + state['selectionY'] * 16 * zoom) * scale
                half_w, half_h = 45 * zoom * scale, 24 * zoom * scale
                exclusion = tuple(round(v) for v in (cx - half_w, cy - half_h, cx + half_w, cy + half_h))
                pixels.paste((0, 0, 0), exclusion)
            image['excludedHoverRect'] = exclusion
            return image, pixels.tobytes()

        def change(state, supplied):
            click(driver, window, state, 'artToggle')
            return wait(process, snapshot, lambda s: rendered(s) and s['suppliedArt'] == supplied and
                        s['draw']['epoch'] > state['draw']['epoch'] and s['changedDraw']['preparedChunks'] > 0)

        def selection_present(state):
            if state['selectionX'] is None or state['selectionY'] is None:
                return False
            xy = overview_cell_coordinates(state, (state['selectionX'], state['selectionY']))
            return (driver.pixel(window, *xy) & 0xffffff) == 0xe4dba0

        # Clear the hover marker before comparing map pixels from the three appearances.
        driver.move(window, *coords(selected, 'artToggle'))
        ready = wait(process, snapshot, lambda s: rendered(s) and s['inspection'].startswith('Authored map.') and
                     selection_present(s))
        probes = [coords(ready, 'viewport', u, v) for u, v in ((.2, .27), (.47, .38), (.64, .5))]
        def visible_pixels():
            return tuple(driver.pixel(window, *point) for point in probes)

        original_pixels = visible_pixels()
        supplied, initial = sample(ready, 'supplied')
        stock_state = change(ready, False)
        wait(process, snapshot, lambda s: not s['suppliedArt'] and selection_present(s) and visible_pixels() != original_pixels)
        stock, middle = sample(stock_state, 'stock')
        restored_state = change(stock_state, True)
        wait(process, snapshot, lambda s: s['suppliedArt'] and selection_present(s) and visible_pixels() == original_pixels)
        restored, returned = sample(restored_state, 'restored')
        if initial == middle or initial != returned:
            raise RuntimeError('Live theme replacement mixed old and new art inside the displayed viewport')
        if any(ready[k] != s[k] for s in (stock_state, restored_state) for k in key):
            raise RuntimeError('Live theme replacement altered the camera, observation or selection')
        return {'name': 'live theme replacement refreshes native world and overview',
                'density': first['scaling'], 'supplied': supplied, 'stock': stock, 'restored': restored,
                'draw': restored_state['changedDraw']}


def renderer_recovery_input(args, driver):
    from PIL import Image

    with launch(args, driver, 'MV30') as (process, window, snapshot, first, _):
        probes = [coords(first, 'viewport', u, v) for u, v in ((.18, .25), (.29, .32), (.46, .38))]
        def monochrome():
            for point in probes:
                raw = driver.pixel(window, *point)
                if len({raw >> shift & 255 for shift in (0, 8, 16)}) != 1:
                    return False
            return True

        click(driver, window, first, 'viewport', .35, .40)
        selected = wait(process, snapshot, lambda s: rendered(s) and s['selectionKind'] is not None)
        click(driver, window, selected, 'display')
        grey = wait(process, snapshot, lambda s: rendered(s) and s['colorMode'] == 'Greyscale' and
                    s['draw']['sequence'] > selected['draw']['sequence'] and monochrome())
        path = args.output / 'MV30-greyscale.png'
        greyscale = capture(driver, window, path)
        box, scale = grey['viewport'], grey['scaling']
        x, y = round((box['x'] + 40) * scale), round((box['y'] + 40) * scale)
        with Image.open(path) as p:
            rgb = p.convert('RGB').crop((x, y, x + round(220 * scale), y + round(120 * scale))).tobytes()
        if any(rgb[i] != rgb[i + 1] or rgb[i] != rgb[i + 2] for i in range(0, len(rgb), 3)):
            raise RuntimeError('Greyscale correction left colored main-map pixels')

        click(driver, window, grey, 'display')
        normal = wait(process, snapshot, lambda s: rendered(s) and s['colorMode'] == 'Normal' and
                      s['draw']['sequence'] > grey['draw']['sequence'] and not monochrome())
        point = coords(normal, 'viewport', .46, .38)
        expected = driver.pixel(window, *point)

        click(driver, window, normal, 'change')
        failed = wait(process, snapshot, lambda s: s.get('draw') and
                      s['draw']['error'] == 'Unsupported owner color role 17' and s['draw']['epoch'] > normal['draw']['epoch'] and
                      driver.pixel(window, *point) & 0xffffff == 0x172b33)
        error_frame = capture(driver, window, args.output / 'MV30-limited.png')
        if expected == driver.pixel(window, *point) or failed['draw']['objects'] != 0:
            raise RuntimeError('Limited native frame retained old map pixels')
        click(driver, window, failed, 'recover')
        retry = wait(process, snapshot, lambda s: s.get('draw') and s['draw']['error'] == failed['draw']['error']
                     and s['draw']['sequence'] > failed['draw']['sequence'])
        click(driver, window, retry, 'change')
        restored = wait(process, snapshot, lambda s: rendered(s) and not s['draw']['error']
                        and s['draw']['epoch'] > retry['draw']['epoch'] and driver.pixel(window, *point) == expected)
        recovered = capture(driver, window, args.output / 'MV30-recovered.png')
        key = ('x', 'y', 'zoom', 'selectionX', 'selectionY', 'selectionKind')
        if any(normal[k] != restored[k] for k in key) or expected != driver.pixel(window, *point):
            raise RuntimeError('Recovery changed the native selection, camera or normal pixels')
        return {'name': 'native color correction and explicit limited-frame recovery',
                'density': first['scaling'], 'greyscale': greyscale, 'errorFrame': error_frame,
                'recovered': recovered, 'draw': restored['draw']}


def retained_terrain_input(args, driver):
    with launch(args, driver, 'MV25') as (process, window, snapshot, first, _):
        click(driver, window, first, 'viewport', .38, .44)
        driver.key('Home')
        state = wait(process, snapshot, lambda s: rendered(s) and s['zoom'] == 1)
        for _ in range(2):
            before = state
            driver.key('Right')
            state = wait(process, snapshot, lambda s: rendered(s) and s['x'] != before['x'] and
                         s['changedDraw']['sequence'] > before['changedDraw']['sequence'])
        draw = state['changedDraw']
        if draw.get('composition', 'cpu') == 'cpu':
            if draw['redraw'] != 'Scrolled' or not 0 < draw['groundDamagePixels'] < draw['damagePixels'] / 4:
                raise RuntimeError(f'Native pan did not reuse the terrain viewport {draw}')
        elif draw['redraw'] != 'Direct' or draw['error'] is not None:
            raise RuntimeError(f'GPU pan did not draw the direct composition {draw}')
        if state['revision'] != first['revision'] or state['zoom'] != 1:
            raise RuntimeError('Retained native pan changed the world or zoom')
        return {'name': 'native keyboard pan preserves the world and uses the selected renderer', 'density': state['scaling'], 'draw': draw}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--fonts', type=Path, required=True)
    parser.add_argument('--art-cases', type=Path, required=True)
    selection = parser.add_mutually_exclusive_group()
    selection.add_argument('--interaction-only', action='store_true')
    selection.add_argument('--mixed-dpi-only', action='store_true')
    parser.add_argument('--expected-density', type=float)
    parser.add_argument('--virtual-gpu', action='store_true', help='Require hardware GLX on the isolated display')
    parser.add_argument('--native-aot', action='store_true', help='Require the signed Native AOT build identity')
    parser.add_argument('--adapter', nargs=argparse.REMAINDER, required=True)
    args = parser.parse_args()
    args.output.mkdir(exist_ok=True, parents=True)
    driver = Driver()
    data = []
    glx = subprocess.run(['glxinfo', '-B'], check=True, capture_output=True, text=True).stdout if args.virtual_gpu else None
    if glx is not None and ('Accelerated: yes' not in glx or 'direct rendering: Yes' not in glx):
        raise RuntimeError('Hardware GLX is required for this display run')
    identity = None
    try:
        for case in ([] if args.interaction_only or args.mixed_dpi_only else CASES):
            size = SELECTOR_WINDOW if case in SELECTOR_BOARDS else ()
            with launch(args, driver, case, size) as (_, window, _, first, identity):
                if not rendered(first):
                    raise RuntimeError(f'{case} has no native render receipt')
                image = capture(driver, window, args.output / (case + '.png'))
                data.append({'case': case, 'viewport': first['viewport'], 'density': first['scaling'],
                             'draw': first['draw'], 'image': image})
        alternatives = ['--map-art', str(args.art_cases / 'native'), '--scenario-art', str(args.art_cases / 'scenario'),
                        '--installation-art', str(args.art_cases / 'installation')]
        for case in ([] if args.interaction_only or args.mixed_dpi_only else
                     ['MV01', 'MV03', 'MV04', 'MV05', 'MV06', 'MV08', 'MV09', 'MV10', 'MV15', 'MV17', 'MV28']):
            with launch(args, driver, case, [*SELECTOR_WINDOW, *alternatives]) as (_, window, _, first, identity):
                image = capture(driver, window, args.output / (case + '-override.png'))
                if image['sha256'] == next(row['image']['sha256'] for row in data if row['case'] == case):
                    raise RuntimeError(f'{case} selected replacement did not change native capture')
                data.append({'case': case + '-override', 'viewport': first['viewport'], 'density': first['scaling'],
                             'draw': first['draw'], 'image': image})
        for case in ([] if args.interaction_only or args.mixed_dpi_only else ['MV01', 'MV10', 'MV15']):
            native_art = ['--map-art', str(args.art_cases / 'native')]
            with launch(args, driver, case, [*SELECTOR_WINDOW, *native_art]) as (_, window, _, first, identity):
                image = capture(driver, window, args.output / (case + '-native-art.png'))
                if image['sha256'] == next(row['image']['sha256'] for row in data if row['case'] == case):
                    raise RuntimeError(f'{case} native-only selection did not change the capture')
                data.append({'case': case + '-native-art', 'viewport': first['viewport'], 'density': first['scaling'],
                             'draw': first['draw'], 'image': image})
        if args.mixed_dpi_only:
            controls, identity = mixed_density_windows(args, driver)
        else:
            controls, identity = interaction(args, driver)
            controls.append(independent_windows(args, driver))
            controls.extend(inspect_and_switch_modes(args, driver))
            controls.append(inspect_knowledge_region(args, driver))
            controls.append(reassign_owner_color(args, driver))
            controls.extend(inspect_object_facts(args, driver))
            controls.append(track_active_unit(args, driver))
            controls.append(change_visible_world(args, driver))
            controls.extend(presentation_input(args, driver))
            controls.append(pinned_map_text(args, driver))
            controls.append(live_art_replacement(args, driver))
            controls.append(renderer_recovery_input(args, driver))
            controls.append(retained_terrain_input(args, driver))
            controls.append(camera_edge_input(args, driver))
    finally:
        driver.close()
    inputs = map_content_inputs(args.assets, args.fonts)
    inputs.extend(p for p in sorted(args.art_cases.glob('*/*')) if p.is_file())
    report = {'format': 'openciv2-native-map-gallery/1', 'platform': platform.platform(),
              'backend': 'hardware virtual Xwayland' if args.virtual_gpu else 'isolated Linux X11 software display',
              'glx': glx,
              'command': [sys.executable, *sys.argv],
              'helpers': {p: sha(ROOT / p) for p in ['tools/dotnet/run_map_showcase_native.py',
                          'tools/dotnet/run_application_native.py', 'tools/dotnet/run_application.py', 'tools/dotnet/run_xvfb.py']},
              'identity': identity, 'cases': data, 'interaction': controls,
              'content': {str(p.resolve()): sha(p) for p in inputs},
              'contracts': {p: sha(ROOT / p) for p in ['docs/testing/map-rendering.md', 'docs/performance/map-rendering.md',
                            'docs/content/map-asset-bindings.json', 'docs/algorithms/P02-map-rendering-and-minimap.md']}}
    if args.mixed_dpi_only:
        report['virtualMonitors'] = subprocess.check_output(['xrandr', '--listactivemonitors'], text=True).splitlines()
    (args.output / 'native-gallery.json').write_text(json.dumps(report, indent=2) + '\n')
    print(f'{len(data)} native case windows and {len(controls)} input/lifecycle checks passed')


if __name__ == '__main__':
    main()
