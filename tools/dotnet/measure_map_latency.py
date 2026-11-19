#!/usr/bin/env python3
"""Measure native input to a stamped X11 window image; this is not photon/display latency."""
import argparse
import ctypes as C
import json
import os
from pathlib import Path
import subprocess
import sys
import time

from measure_map_native import machine, place_at_half_world, quantiles, viewport_size
from map_gpu_memory import MemorySampler
from run_map_showcase_native import Driver, launch, rendered, sha, wait


class GenericEventCookie(C.Structure):
    _fields_ = [('type', C.c_int), ('serial', C.c_ulong), ('send_event', C.c_int), ('display', C.c_void_p),
                ('extension', C.c_int), ('evtype', C.c_int), ('cookie', C.c_uint), ('data', C.c_void_p)]


class XEvent(C.Union):
    _fields_ = [('cookie', GenericEventCookie), ('pad', C.c_long * 24)]


class PresentWatcher:
    """Read a stamp only after an owned window's X Present completion event."""
    def __init__(self, driver, window):
        self.driver, self.events, self.times = driver, 0, []
        self.lib = C.CDLL('libXpresent.so.1')
        self.lib.XPresentSelectInput.argtypes = [C.c_void_p, C.c_ulong, C.c_uint]
        self.lib.XPresentSelectInput.restype = C.c_ulong
        self.lib.XPresentFreeInput.argtypes = [C.c_void_p, C.c_ulong, C.c_ulong]
        self.lib.XPresentFreeInput.restype = None
        driver.x.XQueryExtension.argtypes = [C.c_void_p, C.c_char_p, C.POINTER(C.c_int), C.POINTER(C.c_int), C.POINTER(C.c_int)]
        driver.x.XQueryExtension.restype = C.c_int
        driver.x.XPending.argtypes, driver.x.XPending.restype = [C.c_void_p], C.c_int
        driver.x.XNextEvent.argtypes, driver.x.XNextEvent.restype = [C.c_void_p, C.POINTER(XEvent)], C.c_int
        driver.x.XGetEventData.argtypes, driver.x.XGetEventData.restype = [C.c_void_p, C.POINTER(GenericEventCookie)], C.c_int
        driver.x.XFreeEventData.argtypes, driver.x.XFreeEventData.restype = [C.c_void_p, C.POINTER(GenericEventCookie)], None
        opcode, event, error = C.c_int(), C.c_int(), C.c_int()
        if not driver.x.XQueryExtension(driver.display, b'Present', C.byref(opcode), C.byref(event), C.byref(error)):
            raise RuntimeError('X Present extension is required for completion-driven capture')
        self.opcode, self.selected = opcode.value, []
        pending = [window]
        while pending:
            owned = pending.pop()
            self.selected.append((owned, self.lib.XPresentSelectInput(driver.display, owned, 2)))
            root, parent, children, count = C.c_ulong(), C.c_ulong(), C.POINTER(C.c_ulong)(), C.c_uint()
            if driver.x.XQueryTree(driver.display, owned, C.byref(root), C.byref(parent), C.byref(children), C.byref(count)):
                pending.extend(children[i] for i in range(count.value))
                if children:
                    driver.x.XFree(children)
        driver.x.XFlush(driver.display)

    def poll(self):
        ready = False
        while self.driver.x.XPending(self.driver.display):
            event = XEvent()
            self.driver.x.XNextEvent(self.driver.display, C.byref(event))
            if event.cookie.type == 35 and self.driver.x.XGetEventData(self.driver.display, C.byref(event.cookie)):
                if event.cookie.extension == self.opcode and event.cookie.evtype == 1:
                    self.events += 1
                    self.times.append(time.monotonic_ns())
                    ready = True
                self.driver.x.XFreeEventData(self.driver.display, C.byref(event.cookie))
        return ready

    def close(self):
        for window, event in self.selected:
            self.lib.XPresentFreeInput(self.driver.display, window, event)
        self.driver.x.XFlush(self.driver.display)


def inject(arguments, direct=False):
    window, x, y, count, hz, path = arguments
    window, x, y, count, hz = map(int, (window, x, y, count, hz))
    driver = Driver()
    root_x, root_y = driver.root_point(window, x, y) if direct else (0, 0)
    start = time.monotonic_ns() + 100_000_000
    events = []
    for i in range(count):
        due = start + round(i * 1e9 / hz)
        remaining = (due - time.monotonic_ns()) / 1e9
        if remaining > 0:
            time.sleep(remaining)
        before = time.monotonic_ns()
        if direct:
            # Cache the fixed owned window's coordinates before timing. A
            # per-input XTranslateCoordinates round trip couples injection to readback.
            driver.input.XTestFakeMotionEvent(driver.display, -1, root_x + 2 * (i + 1), root_y, 0)
            driver.x.XFlush(driver.display)
        else:
            driver.mouse(window, x + 2 * (i + 1), y)
        events.append({'x': x + 2 * (i + 1), 'sentNs': before,
                       'sendMs': (time.monotonic_ns() - before) / 1e6, 'lateMs': (before - due) / 1e6})
    Path(path).write_text(json.dumps(events))


def stamp(driver, window, x, y):
    before = time.monotonic_ns()
    image = driver.x.XGetImage(driver.display, window, x, y, 64, 1, C.c_ulong(-1).value, 2)
    if not image:
        raise RuntimeError('Cannot read the owned X11 window stamp')
    try:
        bits = sum((1 << bit) for bit in range(32) if driver.x.XGetPixel(image, bit * 2, 0) & 0xffffff > 0x808080)
    finally:
        driver.x.XDestroyImage(image)
    after = time.monotonic_ns()
    return (bits & 0xffff if (bits >> 16) == ((~bits) & 0xffff) else None), after, (after - before) / 1e6


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--fonts', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--size', required=True)
    parser.add_argument('--expected-density', type=float, required=True)
    parser.add_argument('--expected-cache-mib', type=int, help='Require the actual host GPU cache ceiling before and after each stream')
    parser.add_argument('--native-aot', action='store_true')
    parser.add_argument('--source-root', type=Path, help='Validate a frozen executable against its archived sources')
    parser.add_argument('--frames', type=int, default=300)
    parser.add_argument('--runs', type=int, default=3)
    parser.add_argument('--poll-ms', type=float, default=1, help='Delay between window-image reads; use 4 ms as an observer-overhead control')
    parser.add_argument('--capture', choices=('poll', 'present', 'receipt'), default='poll',
                        help='receipt samples existing host metadata without pixel readback; it does not measure presentation')
    parser.add_argument('--present-stride', type=int, default=1, help='Read after this many Present events; use 4 to test capture overhead')
    parser.add_argument('--direct-input', action='store_true', help='Cache root coordinates before fixed-rate XTest injection')
    parser.add_argument('--rate', type=int, action='append')
    parser.add_argument('--adapter', nargs=argparse.REMAINDER, required=True)
    args = parser.parse_args()
    if os.environ.get('OPENCIV2_MAP_LATENCY') != '1':
        parser.error('OPENCIV2_MAP_LATENCY=1 is required for the test-only window stamp')
    if args.poll_ms <= 0:
        parser.error('--poll-ms must be positive')
    if args.present_stride < 1 or args.present_stride != 1 and args.capture != 'present':
        parser.error('--present-stride must be positive and needs --capture present')
    args.output.mkdir(parents=True, exist_ok=True)
    width, height = map(int, args.size.split('x'))
    driver = Driver()
    report = {'format': 'openciv2-native-map-latency/1', 'machine': machine(True), 'size': args.size,
              'scope': 'XTest injection to first sampled X11 window image carrying that input. Includes readback/poll overhead. '
                       'Unobserved inputs may be coalesced or missed by sampling. No physical scanout/photon claim.',
              'composition': os.environ.get('OPENCIV2_MAP_COMPOSITION', 'cpu'),
              'features': os.environ.get('OPENCIV2_MAP_GPU_FEATURES', 'None'),
              'synchronized': os.environ.get('OPENCIV2_MAP_GPU_SYNC') == '1', 'pollMs': args.poll_ms, 'warmup': 30,
              'capture': args.capture, 'directInput': args.direct_input or args.capture != 'poll',
              'presentStride': args.present_stride,
              'expectedCacheMiB': args.expected_cache_mib,
              'driverSha256': sha(__file__), 'results': []}
    if args.capture == 'receipt':
        report['scope'] = ('XTest injection to sampled host draw metadata, including observation scheduling/poll overhead. '
                           'UI-input-to-draw is recorded inside the application. Asynchronous draws do not wait for GPU completion. '
                           'No window pixel reads, presentation-latency or photon claim.')
    for run in range(1, args.runs + 1):
        for rate in args.rate or (60, 120):
            logical = f'{round(width / args.expected_density)}x{round(height / args.expected_density) + 67}'
            with launch(args, driver, 'Mixed', ('--window', logical)) as (process, window, snapshot, state, identity):
                report['identity'] = identity
                state = viewport_size(driver, process, window, snapshot, state, width, height)
                state = place_at_half_world(driver, process, window, snapshot, state)
                cache_limit = state['draw']['gpuCacheLimitBytes']
                if args.expected_cache_mib is not None and cache_limit != args.expected_cache_mib * 2**20:
                    raise RuntimeError(f'Unexpected latency-start GPU cache ceiling: {cache_limit}')
                scale, box = state['scaling'], state['viewport']
                left, top = round(box['x'] * scale), round(box['y'] * scale)
                x, y = left + round(width * .20), top + round(height * .46)
                driver.mouse(window, x, y)
                driver.press(1, True)
                producer = None
                watcher = None
                try:
                    driver.mouse(window, x + 20, y)
                    state = wait(process, snapshot, lambda s: rendered(s) and s['draw'].get('inputCode') == x + 20 - left)
                    # Warm the same pan path before independent fixed-rate injection.
                    for step in range(report['warmup']):
                        driver.mouse(window, x + 22 + step * 2, y)
                        time.sleep(.04)
                    x += 22 + 2 * report['warmup']
                    watcher = PresentWatcher(driver, window) if args.capture != 'poll' else None
                    memory = MemorySampler(process.pid)
                    memory_samples = [memory.sample()]
                    next_memory = time.monotonic() + .1
                    sent_path = args.output / f'input-{run}-{rate}.json'
                    producer = subprocess.Popen([sys.executable, __file__, '--inject-direct' if report['directInput'] else '--inject', str(window), str(x), str(y),
                                                 str(args.frames), str(rate), str(sent_path)])
                    observations, reads = [], []
                    last_capture_event, final_check = 0, False
                    last = None
                    deadline = time.monotonic() + args.frames / rate + 15
                    final = (x + 2 * args.frames - left) & 0xffff
                    while time.monotonic() < deadline:
                        ready = watcher is None or watcher.poll()
                        finished = producer.poll() is not None
                        capture = ready and (watcher is None or watcher.events - last_capture_event >= args.present_stride or finished)
                        # A stride must not miss a final presentation that falls
                        # between capture slots and produces no subsequent event.
                        if args.present_stride > 1 and finished and not final_check:
                            capture, final_check = True, True
                        if capture or args.capture == 'receipt':
                            details = {}
                            if args.capture == 'receipt':
                                before = time.monotonic_ns()
                                draw = json.loads(snapshot.read_text()).get('draw')
                                code = draw.get('inputCode') if draw else None
                                observed = time.monotonic_ns()
                                cost = (observed - before) / 1e6
                                if draw:
                                    details = {'uiInputToDrawMs': draw['inputToDrawMs'], 'mapDrawMs': draw['ms']}
                            else:
                                code, observed, cost = stamp(driver, window, left, top + 1)
                            if watcher is not None:
                                last_capture_event = watcher.events
                            reads.append({'ms': cost})
                            if code is not None and code != last:
                                observations.append({'code': code, 'observedNs': observed, **details})
                                last = code
                        if producer.poll() is not None and last == final:
                            break
                        if process.poll() is not None:
                            raise RuntimeError('Desktop exited during latency measurement')
                        if time.monotonic() >= next_memory:
                            memory_samples.append(memory.sample())
                            next_memory = time.monotonic() + .1
                        time.sleep(args.poll_ms / 1000)
                    else:
                        raise RuntimeError('Final input stamp was not observed')
                    if producer.wait(timeout=5):
                        raise RuntimeError('Input injector failed')
                    events = json.loads(sent_path.read_text())
                    sent = {((event['x'] - left) & 0xffff): event for event in events}
                    matched = [{**item, **sent[item['code']], 'ms': (item['observedNs'] - sent[item['code']]['sentNs']) / 1e6}
                               for item in observations if item['code'] in sent and item['observedNs'] >= sent[item['code']]['sentNs']]
                    first = {}
                    for item in matched:
                        first.setdefault(item['code'], item)
                    rows = list(first.values())
                    if not rows or rows[-1]['code'] != final:
                        raise RuntimeError('No matched completed input stream')
                    final_cache_limit = json.loads(snapshot.read_text())['draw']['gpuCacheLimitBytes']
                    if final_cache_limit != cache_limit:
                        raise RuntimeError(f'GPU cache ceiling changed during latency stream: {cache_limit} -> {final_cache_limit}')
                    metric = 'inputToReceiptMs' if args.capture == 'receipt' else 'inputToImageMs'
                    result = {'run': run, 'rate': rate, 'sent': len(events), 'observed': len(rows),
                              'presentEvents': watcher.events if watcher else None, 'imageReads': len(reads),
                              'unobserved': len(events) - len(rows), metric: quantiles(rows, 'ms'),
                              'duplicateStampTransitions': len(matched) - len(rows),
                              'gpuCacheLimitBytes': cache_limit,
                              'outOfOrderStampTransitions': sum(b['sentNs'] < a['sentNs'] for a, b in zip(matched, matched[1:])),
                              'injectionLatenessMs': quantiles(events, 'lateMs'), 'readbackMs': quantiles(reads, 'ms'),
                              'samples': rows, 'inputs': events, 'memorySamples': memory_samples,
                              'memoryScope': 'DRM resident/requested regions and process RSS sampled every 100 ms. '
                                             'Includes app/UI/driver resources and shared buffers, excludes compositor clients. '
                                             'RSS and DRM accounting overlap; do not add them. Sampled peaks are lower bounds.'}
                    if args.capture == 'receipt':
                        result['metadataReadMs'] = result.pop('readbackMs')
                        result['uiInputToDrawMs'] = quantiles(rows, 'uiInputToDrawMs')
                        result['mapDrawMs'] = quantiles(rows, 'mapDrawMs')
                        result['imageReads'] = 0
                        end = events[-1]['sentNs'] + round(1e9 / rate)
                        result['presentNotificationsPerSecond'] = sum(events[0]['sentNs'] <= t < end for t in watcher.times) * 1e9 / (end - events[0]['sentNs'])
                    report['results'].append(result)
                    (args.output / 'latency.json').write_text(json.dumps(report, indent=2) + '\n')
                    print(f'{args.size} run {run} {rate}Hz: {len(rows)}/{len(events)} observed; '
                          f'{result[metric]["p50"]:.2f}/{result[metric]["p95"]:.2f} ms p50/p95 ({metric})', flush=True)
                finally:
                    if producer is not None and producer.poll() is None:
                        producer.terminate()
                        producer.wait(timeout=5)
                    driver.press(1, False)
                    if watcher is not None:
                        watcher.close()


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] in ('--inject', '--inject-direct'):
        inject(sys.argv[2:], direct=sys.argv[1] == '--inject-direct')
    else:
        main()
