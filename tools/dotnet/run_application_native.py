#!/usr/bin/env python3
"""Linux X11 pointer-input smoke for the actual production Avalonia desktop window."""
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


class X11:
    def __init__(self):
        self.x = C.CDLL(ctypes.util.find_library('X11'))
        signatures = {
            'XOpenDisplay': ([C.c_char_p], C.c_void_p), 'XDefaultRootWindow': ([C.c_void_p], C.c_ulong),
            'XInternAtom': ([C.c_void_p, C.c_char_p, C.c_int], C.c_ulong),
            'XQueryTree': ([C.c_void_p, C.c_ulong, C.POINTER(C.c_ulong), C.POINTER(C.c_ulong), C.POINTER(C.POINTER(C.c_ulong)), C.POINTER(C.c_uint)], C.c_int),
            'XGetWindowProperty': ([C.c_void_p, C.c_ulong, C.c_ulong, C.c_long, C.c_long, C.c_int, C.c_ulong, C.POINTER(C.c_ulong), C.POINTER(C.c_int), C.POINTER(C.c_ulong), C.POINTER(C.c_ulong), C.POINTER(C.POINTER(C.c_ubyte))], C.c_int),
            'XGetGeometry': ([C.c_void_p, C.c_ulong, C.POINTER(C.c_ulong), C.POINTER(C.c_int), C.POINTER(C.c_int), C.POINTER(C.c_uint), C.POINTER(C.c_uint), C.POINTER(C.c_uint), C.POINTER(C.c_uint)], C.c_int),
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
        self.pid_atom = self.x.XInternAtom(self.display, b'_NET_WM_PID', 0)

    def window_for(self, pid):
        pending = [self.root]
        while pending:
            window = pending.pop(); kind, length, after = C.c_ulong(), C.c_ulong(), C.c_ulong(); fmt = C.c_int(); data = C.POINTER(C.c_ubyte)()
            status = self.x.XGetWindowProperty(self.display, window, self.pid_atom, 0, 1, 0, 6, C.byref(kind), C.byref(fmt), C.byref(length), C.byref(after), C.byref(data))
            try:
                if status == 0 and fmt.value == 32 and length.value == 1 and C.cast(data, C.POINTER(C.c_ulong))[0] == pid:
                    root, x, y, width, height, border, depth = C.c_ulong(), C.c_int(), C.c_int(), C.c_uint(), C.c_uint(), C.c_uint(), C.c_uint()
                    self.x.XGetGeometry(self.display, window, C.byref(root), C.byref(x), C.byref(y), C.byref(width), C.byref(height), C.byref(border), C.byref(depth))
                    if width.value > 100 and height.value > 100:
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

    def click(self, window, box):
        if not box['enabled']: raise RuntimeError('Control is disabled')
        x, y, child = C.c_int(), C.c_int(), C.c_ulong()
        if not self.x.XTranslateCoordinates(self.display, window, self.root,
            int(box['x'] + box['width'] / 2), int(box['y'] + box['height'] / 2), C.byref(x), C.byref(y), C.byref(child)):
            raise RuntimeError('Cannot translate application pointer coordinates')
        self.x.XRaiseWindow(self.display, window); self.x.XSetInputFocus(self.display, window, 1, 0)
        self.input.XTestFakeMotionEvent(self.display, -1, x.value, y.value, 0)
        self.x.XFlush(self.display); time.sleep(.05)
        self.input.XTestFakeButtonEvent(self.display, 1, 1, 0)
        self.x.XFlush(self.display); time.sleep(.05)
        self.input.XTestFakeButtonEvent(self.display, 1, 0, 0)
        self.x.XFlush(self.display)

    def close_window(self, window):
        event = Event(); event.message = ClientMessage(33, 0, 1, self.display, window, self.x.XInternAtom(self.display, b'WM_PROTOCOLS', 0), 32,
            (C.c_long * 5)(self.x.XInternAtom(self.display, b'WM_DELETE_WINDOW', 0), 0, 0, 0, 0))
        self.x.XSendEvent(self.display, window, 0, 0, C.byref(event)); self.x.XFlush(self.display)

    def close(self): self.x.XCloseDisplay(self.display)


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--output', type=Path, required=True); parser.add_argument('--adapter', nargs=argparse.REMAINDER, required=True); args = parser.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='oc2-application-') as directory, open(str(args.output) + '.log', 'w') as log:
        snapshot = Path(directory) / 'state.json'; process = subprocess.Popen([*args.adapter, '--observe', str(snapshot)], stdout=log, stderr=subprocess.STDOUT)
        driver = None
        try:
            driver = X11(); checks = []
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
            def check(name, test):
                state = read()
                if not test(state): raise RuntimeError(name + ': ' + json.dumps(state))
                checks.append({'name': name, 'state': state})
            def click(name):
                state = read(); box = dict(state['controls'][name]); factor = state['scaling']
                for key in ('x', 'y', 'width', 'height'): box[key] *= factor
                driver.click(window, box)
            def status(value): return wait(lambda s: s.get('status') == value)
            check('observer-filtered-units', lambda s: s['observer'] == '1' and [u['slot'] for u in s['units']] == ['0', '1', '2'])
            click('send'); status('AwaitingInteraction'); check('asynchronous-decision', lambda s: s['effects'] == 0)
            click('cancel'); status('Cancelled'); check('cancel-preserves-state', lambda s: s['effects'] == 0)
            click('send'); status('AwaitingInteraction'); click('confirm'); status('Completed'); check('confirmed-reorder', lambda s: s['changed'] is True and s['effects'] == 1)
            click('send'); status('AwaitingInteraction'); click('confirm'); status('Completed'); check('repeat-is-no-op', lambda s: s['changed'] is False and s['effects'] == 0)
            click('send'); status('AwaitingInteraction'); click('decline'); status('Cancelled'); check('decline', lambda s: s['effects'] == 0)
            driver.close_window(window)
            if process.wait(timeout=10) != 0: raise RuntimeError('Desktop did not exit successfully')
            args.output.write_text(json.dumps({'format': 'openciv2-application-input/1', 'platform': 'X11', 'input': 'XTest pointer events at owned process window', 'identity': json.loads(Path(str(snapshot) + '.identity.json').read_text()), 'checks': checks}, indent=2) + '\n')
            print(f'{len(checks)} Avalonia native input checks passed.')
        finally:
            if driver: driver.close()
            if process.poll() is None: process.terminate(); process.wait(timeout=10)


if __name__ == '__main__': main()
