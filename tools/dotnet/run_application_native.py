#!/usr/bin/env python3
"""Linux X11 pointer and keyboard input for the production Avalonia desktop window."""
import argparse
import ctypes as C
import ctypes.util
import json
from pathlib import Path
import subprocess
import tempfile
import time


class ClientMessage(C.Structure):
    _fields_ = [("type", C.c_int), ("serial", C.c_ulong), ("send_event", C.c_int), ("display", C.c_void_p),
                ("window", C.c_ulong), ("message_type", C.c_ulong), ("format", C.c_int), ("data", C.c_long * 5)]


class Event(C.Union):
    _fields_ = [("message", ClientMessage), ("padding", C.c_long * 24)]


class WindowAttributes(C.Structure):
    _fields_ = [(name, C.c_int) for name in ('x', 'y', 'width', 'height', 'border_width', 'depth')] + [
        ('visual', C.c_void_p), ('root', C.c_ulong), ('window_class', C.c_int), ('bit_gravity', C.c_int),
        ('win_gravity', C.c_int), ('backing_store', C.c_int), ('backing_planes', C.c_ulong),
        ('backing_pixel', C.c_ulong), ('save_under', C.c_int), ('colormap', C.c_ulong),
        ('map_installed', C.c_int), ('map_state', C.c_int), ('all_event_masks', C.c_long),
        ('your_event_mask', C.c_long), ('do_not_propagate_mask', C.c_long), ('override_redirect', C.c_int),
        ('screen', C.c_void_p)]


class X11:
    def __init__(self):
        self.x = C.CDLL(ctypes.util.find_library('X11'))
        signatures = {
            'XOpenDisplay': ([C.c_char_p], C.c_void_p), 'XDefaultRootWindow': ([C.c_void_p], C.c_ulong),
            'XInternAtom': ([C.c_void_p, C.c_char_p, C.c_int], C.c_ulong),
            'XQueryTree': ([C.c_void_p, C.c_ulong, C.POINTER(C.c_ulong), C.POINTER(C.c_ulong), C.POINTER(C.POINTER(C.c_ulong)), C.POINTER(C.c_uint)], C.c_int),
            'XGetWindowProperty': ([C.c_void_p, C.c_ulong, C.c_ulong, C.c_long, C.c_long, C.c_int, C.c_ulong, C.POINTER(C.c_ulong), C.POINTER(C.c_int), C.POINTER(C.c_ulong), C.POINTER(C.c_ulong), C.POINTER(C.POINTER(C.c_ubyte))], C.c_int),
            'XGetGeometry': ([C.c_void_p, C.c_ulong, C.POINTER(C.c_ulong), C.POINTER(C.c_int), C.POINTER(C.c_int), C.POINTER(C.c_uint), C.POINTER(C.c_uint), C.POINTER(C.c_uint), C.POINTER(C.c_uint)], C.c_int),
            'XGetWindowAttributes': ([C.c_void_p, C.c_ulong, C.POINTER(WindowAttributes)], C.c_int),
            'XStringToKeysym': ([C.c_char_p], C.c_ulong),
            'XKeysymToKeycode': ([C.c_void_p, C.c_ulong], C.c_uint),
            'XGetImage': ([C.c_void_p, C.c_ulong, C.c_int, C.c_int, C.c_uint, C.c_uint, C.c_ulong, C.c_int], C.c_void_p),
            'XGetPixel': ([C.c_void_p, C.c_int, C.c_int], C.c_ulong),
            'XDestroyImage': ([C.c_void_p], C.c_int),
            'XRaiseWindow': ([C.c_void_p, C.c_ulong], C.c_int),
            'XSetInputFocus': ([C.c_void_p, C.c_ulong, C.c_int, C.c_ulong], C.c_int),
            'XTranslateCoordinates': ([C.c_void_p, C.c_ulong, C.c_ulong, C.c_int, C.c_int, C.POINTER(C.c_int), C.POINTER(C.c_int), C.POINTER(C.c_ulong)], C.c_int),
            'XSendEvent': ([C.c_void_p, C.c_ulong, C.c_int, C.c_long, C.POINTER(Event)], C.c_int),
            'XFlush': ([C.c_void_p], C.c_int), 'XFree': ([C.c_void_p], C.c_int), 'XCloseDisplay': ([C.c_void_p], C.c_int),
        }
        for name, (args, result) in signatures.items():
            fn = getattr(self.x, name); fn.argtypes = args; fn.restype = result
        self.display = self.x.XOpenDisplay(None)
        if not self.display:
            raise RuntimeError('An X11 display is required (use xvfb-run in Linux CI).')
        self.root = self.x.XDefaultRootWindow(self.display)
        self.input = C.CDLL(ctypes.util.find_library('Xtst'))
        self.input.XTestFakeMotionEvent.argtypes = [C.c_void_p, C.c_int, C.c_int, C.c_int, C.c_ulong]
        self.input.XTestFakeButtonEvent.argtypes = [C.c_void_p, C.c_uint, C.c_int, C.c_ulong]
        self.input.XTestFakeKeyEvent.argtypes = [C.c_void_p, C.c_uint, C.c_int, C.c_ulong]
        self.pid_atom = self.x.XInternAtom(self.display, b'_NET_WM_PID', 0)

    def window_for(self, pid, excluding=None):
        pending = [self.root]
        while pending:
            window = pending.pop(); kind, length, after = C.c_ulong(), C.c_ulong(), C.c_ulong(); fmt = C.c_int(); data = C.POINTER(C.c_ubyte)()
            status = self.x.XGetWindowProperty(self.display, window, self.pid_atom, 0, 1, 0, 6, C.byref(kind), C.byref(fmt), C.byref(length), C.byref(after), C.byref(data))
            try:
                if status == 0 and fmt.value == 32 and length.value == 1 and C.cast(data, C.POINTER(C.c_ulong))[0] == pid:
                    root, x, y, width, height, border, depth = C.c_ulong(), C.c_int(), C.c_int(), C.c_uint(), C.c_uint(), C.c_uint(), C.c_uint()
                    self.x.XGetGeometry(self.display, window, C.byref(root), C.byref(x), C.byref(y), C.byref(width), C.byref(height), C.byref(border), C.byref(depth))
                    if window != excluding and width.value > 100 and height.value > 100:
                        print(f'Owned native window: {window} ({width.value}x{height.value})', flush=True)
                        return window
            finally:
                if data: self.x.XFree(data)
            root, parent, count = C.c_ulong(), C.c_ulong(), C.c_uint(); children = C.POINTER(C.c_ulong)()
            if self.x.XQueryTree(self.display, window, C.byref(root), C.byref(parent), C.byref(children), C.byref(count)):
                try: pending.extend(children[i] for i in range(count.value))
                finally:
                    if children: self.x.XFree(children)
        return None

    def move(self, window, relative_x, relative_y):
        x, y, child = C.c_int(), C.c_int(), C.c_ulong()
        if not self.x.XTranslateCoordinates(self.display, window, self.root,
            int(relative_x), int(relative_y), C.byref(x), C.byref(y), C.byref(child)):
            raise RuntimeError('Cannot translate application pointer coordinates')
        self.input.XTestFakeMotionEvent(self.display, -1, x.value, y.value, 0)
        self.x.XFlush(self.display)
        time.sleep(.05)

    def wait_placement(self, window):
        # Layout observations can precede the window manager's initial mapping
        # and placement. Do not translate the first click while it is moving.
        deadline, stable, previous = time.monotonic() + 10, time.monotonic(), None
        while time.monotonic() < deadline:
            attrs = WindowAttributes()
            x, y, child = C.c_int(), C.c_int(), C.c_ulong()
            mapped = self.x.XGetWindowAttributes(self.display, window, C.byref(attrs)) and attrs.map_state == 2
            translated = self.x.XTranslateCoordinates(self.display, window, self.root, 0, 0, C.byref(x), C.byref(y), C.byref(child))
            current = (x.value, y.value, attrs.width, attrs.height) if mapped and translated else None
            if current is None or current != previous:
                stable, previous = time.monotonic(), current
            elif time.monotonic() - stable >= .25:
                return
            time.sleep(.025)
        raise RuntimeError('Owned native window did not finish initial mapping/placement')

    def button(self, number, pressed):
        self.input.XTestFakeButtonEvent(self.display, number, pressed, 0)
        self.x.XFlush(self.display)
        time.sleep(.05)

    def key(self, name):
        code = self.x.XKeysymToKeycode(self.display, self.x.XStringToKeysym(name.encode()))
        if code == 0: raise RuntimeError(f'No X11 keycode for {name}')
        for pressed in (1, 0):
            self.input.XTestFakeKeyEvent(self.display, code, pressed, 0)
            self.x.XFlush(self.display)
            time.sleep(.05)

    def pixel(self, window, x, y):
        image = self.x.XGetImage(self.display, window, int(x), int(y), 1, 1, C.c_ulong(-1).value, 2)
        if not image: raise RuntimeError('Could not read the owned window')
        try: return self.x.XGetPixel(image, 0, 0)
        finally: self.x.XDestroyImage(image)

    def click(self, window, box):
        if not box['enabled']: raise RuntimeError('Control is disabled')
        self.x.XRaiseWindow(self.display, window)
        self.x.XSetInputFocus(self.display, window, 1, 0)
        self.move(window, box['x'] + box['width'] / 2, box['y'] + box['height'] / 2)
        self.button(1, 1)
        self.button(1, 0)

    def close_window(self, window):
        event = Event(); event.message = ClientMessage(33, 0, 1, self.display, window, self.x.XInternAtom(self.display, b'WM_PROTOCOLS', 0), 32,
            (C.c_long * 5)(self.x.XInternAtom(self.display, b'WM_DELETE_WINDOW', 0), 0, 0, 0, 0))
        self.x.XSendEvent(self.display, window, 0, 0, C.byref(event)); self.x.XFlush(self.display)

    def close(self): self.x.XCloseDisplay(self.display)


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--output', type=Path, required=True); parser.add_argument('--map', action='store_true'); parser.add_argument('--assets', type=Path); parser.add_argument('--fonts', type=Path); parser.add_argument('--adapter', nargs=argparse.REMAINDER, required=True); args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='oc2-application-') as directory, open(str(args.output) + '.log', 'w') as log:
        checks = []
        options = ['--assets', str(args.assets), '--fonts', str(args.fonts)] if args.map else []
        if args.map:
            bad = Path(directory) / 'bad-assets'
            bad.mkdir()
            (bad / 'package.json').write_bytes((args.assets / 'package.json').read_bytes())
            original = args.assets / 'LICENSE'
            for name, diagnostic in [('pin-hash-rejection', 'SHA-256 mismatch'), ('linked-file-containment', 'leaves its mounted directory')]:
                candidate = bad / 'LICENSE'
                if name == 'pin-hash-rejection':
                    data = original.read_bytes()
                    candidate.write_bytes(bytes([data[0] ^ 1]) + data[1:])
                else:
                    candidate.symlink_to(original.resolve())
                result = subprocess.run([*args.adapter, '--assets', str(bad), '--fonts', str(args.fonts)],
                                        capture_output=True, text=True, timeout=10)
                if result.returncode != 2 or diagnostic not in result.stderr:
                    raise RuntimeError(f'{name}: {result.returncode}, {result.stderr}')
                checks.append({'name': name, 'exitCode': result.returncode, 'diagnostic': result.stderr.strip()})
                candidate.unlink()
        snapshot = Path(directory) / 'state.json'
        process = subprocess.Popen([*args.adapter, *options, '--observe', str(snapshot)], stdout=log, stderr=subprocess.STDOUT)
        driver = None
        try:
            driver = X11()
            def read(): return json.loads(snapshot.read_text()) if snapshot.exists() else {}
            def wait(test):
                deadline = time.monotonic() + 30
                while time.monotonic() < deadline:
                    if process.poll() is not None: raise RuntimeError('Desktop host exited early; inspect log')
                    state = read()
                    if test(state): return state
                    time.sleep(.03)
                raise RuntimeError('Desktop observation timeout: ' + json.dumps(read()))
            wait(lambda s: s.get('controls', {}).get('send', {}).get('width', 0) > 0)
            window = driver.window_for(process.pid)
            if window is None: raise RuntimeError('Cannot find the launched process window')
            driver.wait_placement(window)
            def check(name, test):
                state = read()
                if not test(state): raise RuntimeError(name + ': ' + json.dumps(state))
                checks.append({'name': name, 'state': state})
            def click(name):
                state = wait(lambda s: (box := s.get('controls', {}).get(name, {})).get('enabled') and box.get('width', 0) > 0)
                box = dict(state['controls'][name]); factor = state['scaling']
                for key in ('x', 'y', 'width', 'height'): box[key] *= factor
                driver.click(window, box)
            def status(value): return wait(lambda s: s.get('status') == value)
            check('observer-filtered-units', lambda s: s['observer'] == '1' and [u['slot'] for u in s['units']] == ['0', '1', '2'])
            if args.map:
                initial_map = read()['map']
                click('mapOptions')
                options = wait(lambda s: s.get('controls', {}).get('motionToggle') is not None)
                factor = options['scaling']

                def toggle_box(box):
                    driver.move(window, (box['x'] + box['width'] / 2) * factor, (box['y'] + box['height'] / 2) * factor)
                    driver.button(1, 1)
                    driver.button(1, 0)

                modes = ('grid', 'cityRadius', 'ownership', 'bare')
                for enabled in (True, False):
                    for mode in modes:
                        box = wait(lambda s: s['controls']['modeToggles'][mode] is not None)['controls']['modeToggles'][mode]
                        before = read()['map']
                        toggle_box(box)
                        after = wait(lambda s: s['map'][mode] == enabled)['map']
                        if any(after[key] != before[key] for key in modes if key != mode):
                            raise RuntimeError(f'{mode} changed another map display mode')
                        if any(after[key] != initial_map[key] for key in ('revision', 'x', 'y', 'zoom', 'selection', 'tileX', 'tileY', 'stackTop')):
                            raise RuntimeError(f'{mode} changed the selected world or camera')
                    check('native-map-modes-enabled' if enabled else 'native-map-modes-restored',
                          lambda s: all(s['map'][mode] == enabled for mode in modes))

                toggle_box(wait(lambda s: s['controls']['motionToggle'] is not None)['controls']['motionToggle'])
                wait(lambda s: s['map']['reducedMotion'])
                driver.key('Escape')
                wait(lambda s: s['controls']['motionToggle'] is None)
            click('send'); status('AwaitingInteraction'); check('asynchronous-decision', lambda s: s['effects'] == 0)
            click('cancel'); status('Cancelled'); check('cancel-preserves-state', lambda s: s['effects'] == 0)
            click('send'); status('AwaitingInteraction'); click('confirm'); status('Completed'); check('confirmed-reorder', lambda s: s['changed'] is True and s['effects'] == 1)
            if args.map:
                wait(lambda s: s['map']['notices'] == 1)
                check('decisions-and-completion-notice-under-reduced-motion', lambda s: s['map']['reducedMotion'] and
                      s['effects'] == 1 and s['changed'] is True and s['map']['notices'] == 1)
            click('send'); status('AwaitingInteraction'); click('confirm'); status('Completed'); check('repeat-is-no-op', lambda s: s['changed'] is False and s['effects'] == 0)
            click('send'); status('AwaitingInteraction'); click('decline'); status('Cancelled'); check('decline', lambda s: s['effects'] == 0)
            if args.map:
                def map_pixels(x, y):
                    state = read()
                    box = state['controls']['map']
                    return ((box['x'] + x) * state['scaling'], (box['y'] + y) * state['scaling'])

                def map_click(x, y):
                    px, py = map_pixels(x, y)
                    driver.click(window, dict(x=px, y=py, width=0, height=0, enabled=True))

                def center(x, y):
                    camera = read()['map']
                    return camera['x'] + x * 32 * camera['zoom'], camera['y'] + y * 16 * camera['zoom']

                wait(lambda s: s.get('map', {}).get('stackTop') == 1)
                land = driver.pixel(window, *map_pixels(*center(4, 2)))
                sky = driver.pixel(window, *map_pixels(93, 10))
                check('native-map-contrast', lambda s: land != sky and s['map']['stackVisible'] == 3)
                map_click(*center(12, 4))
                wait(lambda s: s['map']['tileX'] == 12 and s['map']['tileY'] == 4)
                check('native-river-road-crossing', lambda s: s['map']['selection'] == 'Tile' and
                      'River' in s['map']['detail'] and 'Remembered features Road' in s['map']['detail'])
                map_click(*center(14, 6))
                wait(lambda s: s['map']['tileX'] == 14 and s['map']['tileY'] == 6)
                check('native-forest-resource-pick', lambda s: s['map']['selection'] == 'Tile' and
                      'Forest · Special resource 1' in s['map']['detail'])
                map_click(*center(-1, 1))
                wait(lambda s: s['map']['tileX'] == 55 and s['map']['tileY'] == 1)
                check('wrapped-map-pick', lambda s: s['map']['selection'] == 'Tile')
                map_click(93, 10)
                wait(lambda s: s['map']['selection'] is None)
                check('vertical-edge-miss', lambda s: s['map']['tileY'] is None)

                x, y = center(6, 4)
                map_click(x - 8, y - 8)
                wait(lambda s: s['map']['selection'] == 'City')
                check('city-pick', lambda s: 'Riverbend' in s['map']['detail'])
                driver.move(window, *map_pixels(300, 350))
                driver.button(1, 1)
                driver.move(window, *map_pixels(378, 390))
                driver.button(1, 0)
                wait(lambda s: s['map']['x'] > 145)
                check('pointer-drag-pans-without-reselection', lambda s: s['map']['selection'] == 'City')
                driver.key('Left')
                wait(lambda s: s['map']['x'] > 190)
                driver.move(window, *map_pixels(350, 240))
                driver.button(4, 1)
                driver.button(4, 0)
                wait(lambda s: s['map']['zoom'] > 1.25)
                check('native-pan-and-wheel-zoom', lambda s: s['map']['zoom'] > 1.25)

                x, y = center(2, 2)
                map_click(x, y - 13)
                wait(lambda s: s['map']['selection'] == 'Unit' and s['selected'] == 1)
                driver.key('Return')
                status('AwaitingInteraction')
                driver.key('Escape')
                status('Cancelled')
                check('keyboard-map-order-and-escape', lambda s: s['effects'] == 0 and s['map']['stackTop'] == 1)
                state = read()
                x_before = state['map']['x']
                # Leave the map first so its hover text no longer reflows
                # the sidebar between resolving and clicking the console.
                driver.move(window, (state['width'] - 9) * state['scaling'], 8 * state['scaling'])
                click('input')
                wait(lambda s: s['inputFocused'])
                driver.key('Left')
                check('console-focus-isolates-map', lambda s: s['inputFocused'] and s['map']['x'] == x_before)
                driver.key('Return')
                status('AwaitingInteraction')
                click('confirm')
                status('Completed')
                check('console-and-map-share-command', lambda s: s['effects'] == 0 and s['changed'] is False)
            driver.close_window(window)
            if process.wait(timeout=10) != 0: raise RuntimeError('Desktop did not exit successfully')
            args.output.write_text(json.dumps({'format': 'openciv2-application-input/1', 'platform': 'X11', 'input': 'XTest pointer and keyboard events at owned process window' if args.map else 'XTest pointer events at owned process window', 'identity': json.loads(Path(str(snapshot) + '.identity.json').read_text()), 'checks': checks}, indent=2) + '\n')
            print(f'{len(checks)} Avalonia native input checks passed.')
        finally:
            if driver: driver.close()
            if process.poll() is None: process.terminate(); process.wait(timeout=10)


if __name__ == '__main__': main()
