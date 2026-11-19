#!/usr/bin/env python3
"""Drive production map input under X11 and record Avalonia draw receipts."""
import argparse
import hashlib
import io
import json
import math
from pathlib import Path
import platform
import subprocess
import time

from PIL import Image
from run_application import map_content_inputs
from map_gpu_memory import MemorySampler
from run_map_showcase_native import Driver, ROOT, click, coords, launch, rendered, sha, wait

MODES = ('pan', 'continuous-zoom', 'dirty-world', 'resource-recovery')
COLD_DETAILS = ('full', 'half', 'overview')
MIN_ZOOM = .25


def quantiles(samples, field):
    values = sorted(row[field] for row in samples)
    return {f'p{p}': values[math.ceil(len(values) * p / 100) - 1] for p in (50, 95, 99, 100)}


def dimensions(state):
    scale = state['scaling']
    box = state['viewport']
    return round(scale * box['width']), round(scale * box['height'])


def distinct_tiles_at_center(state):
    x, y, zoom = (state[k] for k in ('x', 'y', 'zoom'))
    width, height = (state['viewport'][k] for k in ('width', 'height'))
    world_width, world_height = state['worldWidth'], state['worldHeight']
    top, bottom = max(0, math.ceil(-y / (zoom * 16) - 2)), min(world_height - 1, math.floor((height - y) / (zoom * 16) + 3))
    left, right = math.ceil(-x / (zoom * 32) - 2), math.floor((width - x) / (zoom * 32) + 2)
    if state['flat']:
        left, right = max(left, 0), min(right, world_width - 1)
    return sum(len({column % world_width for column in range(left + ((left ^ row) & 1), right + 1, 2)})
               for row in range(top, bottom + 1))


def machine(virtual_gpu):
    def field(path, name):
        with Path(path).open() as stream:
            return next((line.partition(':')[2].strip() for line in stream if line.startswith(name)), None)

    facts = {'cpu': field('/proc/cpuinfo', 'model name'), 'memory': field('/proc/meminfo', 'MemTotal'),
             'loadAtStart': Path('/proc/loadavg').read_text().strip(), 'xDisplay': 'isolated X11 software renderer'}
    if virtual_gpu:
        def display_tool(*command):
            return subprocess.run(command, check=True, capture_output=True, text=True, timeout=15).stdout

        glx = display_tool('glxinfo', '-B')
        if 'Accelerated: yes' not in glx or 'direct rendering: Yes' not in glx:
            raise RuntimeError('The virtual X11 display needs direct hardware-accelerated GLX')
        facts.update(xDisplay='virtual Xwayland target', glx=glx, displayModes=display_tool('xrandr', '--current'))
    return facts


def graphics_time(pid):
    """Per-client DRM graphics-engine counters from the actual desktop process."""
    clients = {}
    for fd in Path(f'/proc/{pid}/fdinfo').iterdir():
        try:
            with fd.open() as stream:
                fields = dict(line.strip().split(':', 1) for line in stream if line.startswith('drm-') and ':' in line)
        except FileNotFoundError:
            continue  # An unrelated descriptor can close while enumerating.
        counter = fields.get('drm-engine-gfx', fields.get('drm-engine-render'))
        if 'drm-client-id' not in fields or counter is None:
            continue
        value, units = counter.split()
        if units != 'ns':
            raise RuntimeError(f'Unknown DRM graphics counter units {units}')
        client = (fields['drm-driver'].strip(), fields['drm-client-id'].strip())
        clients[client] = max(clients.get(client, 0), int(value))
    total = sum(clients.values())
    if total <= 0:
        raise RuntimeError('No hardware graphics work observed in the owned desktop process')
    return total


def viewport_size(driver, process, window, snapshot, state, width, height):
    old_width, old_height = dimensions(state)
    header_and_footer = (state['viewport']['y'] + 23) * state['scaling']
    window_height = round(height + header_and_footer)
    if old_width != width or old_height != height:
        driver.resize(window, width, window_height)
        state = wait(process, snapshot, lambda s: rendered(s) and dimensions(s) == (width, height) and
                     s['draw']['sequence'] > state['draw']['sequence'])
    return state


def half_world_scale(state):
    box = state['viewport']
    return math.sqrt(box['width'] * box['height'] / (state['worldWidth'] * state['worldHeight'] * 32 * 16 * .5))


def place_at_half_world(driver, process, window, snapshot, state):
    desired = max(MIN_ZOOM, half_world_scale(state))
    x, y = coords(state, 'viewport', .57, .22)
    delta = round(80 * state['scaling'] * math.log(state['zoom'] / desired, 1.25))
    driver.mouse(window, x, y)
    driver.key_edge('Shift_L', True)
    driver.press(1, True)
    try:
        driver.mouse(window, x, y + delta)
        state = wait(process, snapshot, lambda s: rendered(s) and abs(s['zoom'] / desired - 1) < .005)
    finally:
        driver.press(1, False)
        driver.key_edge('Shift_L', False)
    click(driver, window, state, 'overview', .24, .50)
    return wait(process, snapshot, lambda s: rendered(s) and s['draw']['sequence'] > state['draw']['sequence'] and
                (s['x'], s['y']) != (state['x'], state['y']))


def place_near_flat_pan_limit(driver, process, window, snapshot, state):
    scale = state['scaling']
    viewport = state['viewport']
    limit = viewport['width'] - 32 * state['zoom']
    distance = round((limit - state['x']) * scale) - 48
    if distance <= 6 * scale:
        return state
    x, y = coords(state, 'viewport', .08, .46)
    if distance >= .89 * viewport['width'] * scale:
        raise RuntimeError('Flat edge setup does not fit inside the input viewport')
    driver.mouse(window, x, y)
    driver.press(1, True)
    try:
        driver.mouse(window, x + distance, y)
        return wait(process, snapshot, lambda s: rendered(s) and s['x'] > state['x'])
    finally:
        driver.press(1, False)


def collect(driver, process, window, snapshot, state, mode, warm, count):
    frames = []
    warmup_frames = []
    gesture = mode in ('pan', 'continuous-zoom')
    shift = mode == 'continuous-zoom'
    recovery = mode == 'resource-recovery'
    control = 'viewport' if gesture else 'recover' if recovery else 'change'
    x, y = coords(state, control, .55, .46)
    displacement, direction = 0, 1
    if shift:
        pixels_per_step = 80 * state['scaling']
        closest = round(pixels_per_step * math.log(state['zoom'] / 1.1, 1.25))
        furthest = round(pixels_per_step * math.log(state['zoom'] / .28, 1.25))
        stride = max(6, round((furthest - closest) / 44))
    driver.mouse(window, x, y)
    if shift:
        driver.key_edge('Shift_L', True)
    if gesture:
        driver.press(1, True)
    try:
        last_pan = None
        if mode == 'pan':
            # Start the drag before measuring short physical displacements.
            # Avalonia compares the activation threshold in logical pixels.
            x += math.ceil(6 * state['scaling'])
            driver.mouse(window, x, y)
            state = wait(process, snapshot, lambda s: rendered(s) and s['x'] != state['x'], timeout=30)
            last_pan = (x, y)
        for i in range(warm + count):
            before = time.monotonic_ns()
            pointer_delta = None
            if mode == 'pan':
                step = 80 - abs((i + 1) % 160 - 80)
                if i == warm:
                    point = (last_pan[0], last_pan[1] + 1) if state['flat'] else (last_pan[0] + 1, last_pan[1])
                else:
                    point = (x + 5 * step, y + 2 * step)
                pointer_delta = [int(point[0]) - int(last_pan[0]), int(point[1]) - int(last_pan[1])]
                driver.mouse(window, *point)
                last_pan = point
            elif shift:
                # Vary actual pointer displacement around one anchor and cross
                # the displayed scale bands in both directions.
                displacement += direction * stride
                if displacement >= furthest or displacement <= closest:
                    displacement = min(furthest, max(closest, displacement))
                    direction = -direction
                driver.mouse(window, x, y + displacement)
            else:
                driver.press(1, True)
                driver.press(1, False)

            def changed(s):
                receipt = s.get('changedDraw')
                if not rendered(s) or not receipt or receipt['sequence'] <= state['changedDraw']['sequence'] or any(
                        s[key] != receipt[key] for key in ('x', 'y', 'zoom')):
                    return False
                if mode == 'pan':
                    return (s['x'], s['y']) != (state['x'], state['y'])
                if shift:
                    return not math.isclose(s['zoom'], state['zoom'])
                if recovery:
                    return (s['revision'] == state['revision'] and
                            all(s[key] == state[key] for key in ('x', 'y', 'zoom', 'selectionX', 'selectionY', 'selectionKind')) and
                            (receipt['redraw'] in ('Full', 'Direct') or receipt.get('regionBuilds', 0) > 0) and receipt['preparedChunks'] > 0)
                return s['revision'] > state['revision'] and receipt['epoch'] > state['draw']['epoch'] and receipt.get('effects', 0) > 0

            state = wait(process, snapshot, changed, timeout=30)
            row = {**state['changedDraw'], 'revision': state['revision'],
                   'inputToReceiptMs': (time.monotonic_ns() - before) / 1e6}
            if pointer_delta is not None:
                row['pointerDeltaPixels'] = pointer_delta
            if mode == 'dirty-world':
                work = state['updateWork']
                if not work or work['revision'] != row['revision'] or min(work['authoredMs'], work['bindMs']) <= 0:
                    raise RuntimeError('Dirty draw needs preparation measurements for the same observed revision')
                row.update(updateX=state['updateX'], updateY=state['updateY'], authoredUpdateMs=work['authoredMs'],
                           sceneBindMs=work['bindMs'], updateAndDrawMs=work['authoredMs'] + work['bindMs'] + row['ms'])
                for name in ('sceneIndexMs', 'sceneIndexAllocatedBytes', 'rendererWaitMs', 'groundInvalidateMs'):
                    row[name] = work[name]
                row['bindingDeferred'] = work['bindingDeferred']
            if recovery and state['selectionKind'] is not None:
                row['selection'] = {'x': state['selectionX'], 'y': state['selectionY'], 'kind': state['selectionKind']}
            (frames if i >= warm else warmup_frames).append(row)
    finally:
        if gesture:
            driver.press(1, False)
        if shift:
            driver.key_edge('Shift_L', False)
    return frames, warmup_frames


def collect_cold(args, driver, width, height, run, count, detail):
    density = args.expected_density
    logical = f'{round(width / density)}x{round(height / density)}'
    frames = []
    phases = ('frameworkSetupMs', 'rendererSetupMs', 'viewSetupMs', 'windowAndDrawQueueMs', 'drawCallbackMs')
    startup_times = ('admissionMs', 'afterAdmissionMs', 'admissionStartToDrawMs', *phases, 'boardMs', 'bindMs',
                     'startupPreparationMs', 'startupPreparationWaitMs', 'startupGroundPreparationMs', 'startupGroundJoinMs')
    prepared = ('startupPreparedTextures', 'startupPreparedTextureBytes')
    driver.move(driver.root, 0, 0)
    for i in range(count):
        with launch(args, driver, 'Mixed', ('--window', logical, '--cold-view', detail)) as (
                process, window, _, state, identity):
            startup = state['coldStartup']
            draw = startup['firstDraw']
            if args.expected_cache_mib is not None and draw['gpuCacheLimitBytes'] != args.expected_cache_mib * 2**20:
                raise RuntimeError('The cold renderer did not apply the experiment cache ceiling')
            requested_scale = half_world_scale(state) if detail == 'half' else MIN_ZOOM if detail == 'overview' else 1.25
            expected_scale = max(MIN_ZOOM, min(3, requested_scale))
            if (dimensions(state) != (width, height) or startup['detail'] != detail or
                    any(draw[k] != state['draw'][k] for k in ('epoch', 'width', 'height', 'x', 'y', 'zoom', 'scaling')) or
                    not math.isclose(draw['zoom'], expected_scale) or draw['redraw'] not in ('Full', 'Direct', 'Scrolled') or draw['error'] or
                    draw['effects'] != 0 or not draw['preparedChunks'] or not draw['textureCacheBytes'] or
                    state['revision'] != 0 or state['flat'] or not state['reducedMotion'] or state['selectionKind'] is not None or
                    not 0 < draw['ms'] <= startup['drawCallbackMs'] or not 0 < startup['admissionMs'] or
                    not all(startup[name] >= 0 for name in phases) or
                    not math.isclose(sum(startup[name] for name in phases), startup['afterAdmissionMs'], abs_tol=.001) or
                    not math.isclose(startup['admissionMs'] + startup['afterAdmissionMs'], startup['admissionStartToDrawMs'],
                                     abs_tol=.001) or
                    not 0 <= startup['startupPreparationWaitMs'] <= startup['rendererSetupMs'] or
                    not 0 <= startup['startupPreparationMs'] <= startup['afterAdmissionMs'] or
                    not 0 <= startup['startupGroundJoinMs'] <= startup['afterAdmissionMs'] or
                    not 0 <= startup['startupGroundPreparationMs'] <= startup['afterAdmissionMs'] or
                    not 0 <= startup['startupPreparedTextureBytes'] <= 8 * 1024 * 1024 or
                    (startup['startupPreparedTextures'] == 0) != (startup['startupPreparedTextureBytes'] == 0) or
                    not 0 < startup['boardMs'] + startup['bindMs'] <= startup['viewSetupMs']):
                raise RuntimeError(f'Unexpected first cold frame for {width}×{height} at {detail}: {startup}')
            frame = {**draw, 'revision': state['revision']}
            frame.update((name, startup[name]) for name in (*startup_times, *prepared))
            capture_path = args.output / f'cold-{width}x{height}-{detail}-run{run}.png' if i == 0 else None
            frame['windowPixels'] = capture_map_pixels(window, state, width, height, capture_path)
            if args.virtual_gpu:
                frame['processGraphicsEngineNs'] = graphics_time(process.pid)
                frame['processGpuMemory'] = MemorySampler(process.pid).sample()
            frames.append(frame)
            if (i + 1) % 10 == 0:
                print(f'{width}×{height} {detail} run {run}: {i + 1}/{count} process-cold frames', flush=True)
    return identity, {
        'width': width, 'height': height, 'density': density, 'detail': detail,
        'requestedScale': requested_scale, 'displayedScale': expected_scale,
        'distinctTiles': distinct_tiles_at_center(state), 'worldTiles': state['worldWidth'] * state['worldHeight'] // 2,
        'flat': state['flat'], 'initialRevision': 0, 'run': run, 'mode': 'cold-first-frame', 'warmup': 0, 'samples': frames,
        'drawMs': quantiles(frames, 'ms'),
        **{field: quantiles(frames, field) for field in startup_times},
    }


def capture_map_pixels(window, state, width, height, path):
    density = state['scaling']
    left, top = (round(state['viewport'][key] * density) for key in ('x', 'y'))
    cut_x, cut_y = (math.floor((state['overview'][key] - state['viewport'][key] - 2) * density) for key in ('x', 'y'))
    if not 0 < cut_x < width or not 0 < cut_y < height:
        raise RuntimeError('Overview did not leave a measurable map rectangle')
    screenshot = subprocess.run(['import', '-window', str(window), '-depth', '8', 'ppm:-'],
                                check=True, timeout=20, stdout=subprocess.PIPE).stdout
    with Image.open(io.BytesIO(screenshot)) as image:
        if left < 0 or top < 0 or left + width > image.width or top + height > image.height:
            raise RuntimeError(f'Map viewport {(left, top, width, height)} exceeds the native window {image.size}')
        digest = hashlib.sha256()
        for x1, y1, x2, y2 in ((0, 0, width, cut_y), (0, cut_y, cut_x, height)):
            rgb = image.crop((left + x1, top + y1, left + x2, top + y2)).convert('RGB')
            digest.update(rgb.tobytes())
        if path:
            image.crop((left, top, left + width, top + height)).save(path)
    return {'cutX': cut_x, 'cutY': cut_y, 'rgbSha256': digest.hexdigest(), **({'path': path.name} if path else {})}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--fonts', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--size', choices=['1920x1080', '3840x2160', '5120x2880'], action='append')
    parser.add_argument('--mode', choices=(*MODES, 'cold-first-frame'), action='append')
    parser.add_argument('--cold-detail', choices=COLD_DETAILS, action='append')
    parser.add_argument('--expected-density', type=float, choices=[1., 2.], default=1.)
    topology = parser.add_mutually_exclusive_group()
    topology.add_argument('--flat', action='store_true', help='Use the authored Mixed board with flat edges')
    topology.add_argument('--flat-edge', action='store_true', help='Stage a pan near the flat camera limit')
    parser.add_argument('--diagnostic', action='store_true')
    parser.add_argument('--run-index', type=int, choices=(1, 2, 3), help='Run one acceptance repetition for interleaved variant ordering')
    parser.add_argument('--diagnostic-warmup', type=int)
    parser.add_argument('--diagnostic-frames', type=int)
    parser.add_argument('--native-aot', action='store_true', help='Require the signed Native AOT build identity')
    parser.add_argument('--source-root', type=Path, help='Verify an archived binary against its preserved source snapshot')
    parser.add_argument('--virtual-gpu', action='store_true', help='Verify direct GLX and desktop DRM work on a virtual Xwayland display')
    parser.add_argument('--composition', choices=('cpu', 'gpu'), help='Require the selected experimental composition mode in every receipt')
    parser.add_argument('--gpu-sync', action='store_true', help='Require synchronous GPU completion inside every timed draw')
    parser.add_argument('--expected-cache-mib', type=int, help='Require this actual Skia cache ceiling in every receipt')
    parser.add_argument('--adapter', nargs=argparse.REMAINDER, required=True)
    args = parser.parse_args()
    if not args.diagnostic and (args.diagnostic_warmup is not None or args.diagnostic_frames is not None):
        parser.error('custom sample counts require --diagnostic')
    if args.diagnostic_warmup is not None and args.diagnostic_warmup < 0:
        parser.error('--diagnostic-warmup needs zero or more changes')
    if args.diagnostic_frames is not None and args.diagnostic_frames < 1:
        parser.error('--diagnostic-frames needs at least one change')
    if args.cold_detail and 'cold-first-frame' not in (args.mode or ()):
        parser.error('--cold-detail requires --mode cold-first-frame')
    if (args.flat or args.flat_edge) and 'cold-first-frame' in (args.mode or ()):
        parser.error('the cold first-frame workload uses the wrapped Mixed board')
    args.output.mkdir(exist_ok=True, parents=True)
    runs, warm, count = (1, 3, 10) if args.diagnostic else (3, 30, 300)
    if args.diagnostic_warmup is not None:
        warm = args.diagnostic_warmup
    if args.diagnostic_frames is not None:
        count = args.diagnostic_frames
    inputs = map_content_inputs(args.assets, args.fonts)
    backend = 'hardware-accelerated virtual Xwayland' if args.virtual_gpu else 'isolated X11 software rendering'
    helpers = ['tools/dotnet/run_map_showcase_native.py', 'tools/dotnet/run_application_native.py', 'tools/dotnet/map_gpu_memory.py',
               'tools/dotnet/run_xwayland_virtual.py' if args.virtual_gpu else 'tools/dotnet/run_xvfb.py']
    report = {
        'format': 'openciv2-native-map-input-timing/1',
        'scope': f'Linux host, {backend}. Input to polled Avalonia draw receipt includes polling overhead. '
                 'Cold timing ends at the host map draw callback after the Skia lease is released. X11 readback follows timing. '
                 'Physical presentation and per-frame GPU timing remain separate measurements.',
        'platform': platform.platform(), 'machine': machine(args.virtual_gpu), 'warmup': warm, 'measuredChanges': count,
        'diagnostic': args.diagnostic, 'nativeAot': args.native_aot, 'virtualGpu': args.virtual_gpu,
        'composition': args.composition, 'gpuSynchronized': args.gpu_sync,
        'expectedCacheMiB': args.expected_cache_mib,
        'contracts': {p: sha(ROOT / p) for p in ('docs/testing/map-rendering.md', 'docs/performance/map-rendering.md',
                     'docs/content/map-asset-bindings.json', 'docs/algorithms/P02-map-rendering-and-minimap.md')},
        'driverSha256': sha(__file__), 'command': args.adapter, 'results': [],
        'sourceRoot': str((args.source_root or ROOT).resolve()),
        'helperSha256': {path: sha(ROOT / path) for path in helpers},
        'content': {str(p.resolve()): sha(p) for p in inputs},
        'sizes': args.size or ['1920x1080', '3840x2160', '5120x2880'],
        'modes': args.mode or (('pan',) if args.flat_edge else MODES),
    }
    driver = Driver()
    try:
        def save(identity, row):
            if args.composition and any(s.get('composition') != args.composition for s in row['samples']):
                raise RuntimeError('Draw receipts do not match the requested composition mode')
            if args.gpu_sync and any(s.get('gpuSynchronized') is not True for s in row['samples']):
                raise RuntimeError('Draw receipts did not wait for GPU completion')
            if args.expected_cache_mib is not None and any(s.get('gpuCacheLimitBytes') != args.expected_cache_mib * 2**20 for s in row['samples']):
                raise RuntimeError('Draw receipts do not match the requested Skia cache ceiling')
            if all('gpuWaitMs' in s for s in row['samples']):
                row['gpuWaitMs'] = quantiles(row['samples'], 'gpuWaitMs')
            if all('gpuCacheBytes' in s for s in row['samples']):
                row['gpuCachePeakBytes'] = max(s['gpuCacheBytes'] for s in row['samples'])
                row['gpuCacheLimitBytes'] = sorted({s['gpuCacheLimitBytes'] for s in row['samples']})
            for field in ('foregroundPlanMs', 'atlasPrepareMs', 'foregroundSubmitMs', 'drawAllocatedBytes',
                          'atlasAdmissions', 'atlasEvictions', 'atlasHits', 'regionBuilds', 'regionHits',
                          'regionBytes', 'topologyHits', 'topologyBuilds', 'topologyBytes'):
                if all(field in sample for sample in row['samples']):
                    row[field] = quantiles(row['samples'], field)
            report['identity'] = identity
            report['results'].append(row)
            (args.output / 'native-performance.json').write_text(json.dumps(report, indent=2) + '\n')

        for size in report['sizes']:
            width, height = map(int, size.split('x'))
            for run in ([args.run_index] if args.run_index else range(1, runs + 1)):
                for mode in report['modes']:
                    if mode == 'cold-first-frame':
                        for detail in args.cold_detail or COLD_DETAILS:
                            identity, row = collect_cold(args, driver, width, height, run, count if args.diagnostic else 20, detail)
                            save(identity, row)
                            print(f'{size} run {run} cold {detail}: {len(row["samples"])} first frames', flush=True)
                        continue
                    scale = args.expected_density
                    logical = f'{round(width / scale)}x{round(height / scale) + 67}'
                    series_warm, series_count = (0, 20) if mode == 'resource-recovery' and not args.diagnostic else (warm, count)
                    with launch(args, driver, 'Mixed', ('--window', logical)) as (
                            process, window, snapshot, state, identity):
                        state = viewport_size(driver, process, window, snapshot, state, width, height)
                        if args.expected_cache_mib is not None and state['draw']['gpuCacheLimitBytes'] != args.expected_cache_mib * 2**20:
                            raise RuntimeError('The selected renderer did not apply the experiment cache ceiling')
                        if args.flat or args.flat_edge:
                            click(driver, window, state, 'flatToggle')
                            state = wait(process, snapshot, lambda s: rendered(s) and s['flat'] and
                                         s['revision'] > state['revision'] and s['draw']['epoch'] > state['draw']['epoch'])
                            state = viewport_size(driver, process, window, snapshot, state, width, height)
                        click(driver, window, state, 'viewport', .3, .3)
                        state = place_at_half_world(driver, process, window, snapshot, state)
                        half_world = state
                        from_flat_edge = args.flat_edge and mode == 'pan'
                        if from_flat_edge:
                            state = place_near_flat_pan_limit(driver, process, window, snapshot, state)
                        requested_scale = half_world_scale(half_world)
                        graphics = {}
                        if args.virtual_gpu:
                            start = graphics_time(process.pid)
                        frames, warmup_frames = collect(driver, process, window, snapshot, state, mode, series_warm, series_count)
                        if args.virtual_gpu:
                            elapsed = graphics_time(process.pid) - start
                            if elapsed <= 0:
                                raise RuntimeError('Desktop graphics counter did not advance during the workload')
                            graphics['processGraphicsEngineIncreaseNs'] = elapsed
                            status = Path(f'/proc/{process.pid}/status').read_text().splitlines()
                            graphics['processMemoryKiB'] = {line.split(':')[0]: int(line.split()[1]) for line in status
                                                           if line.startswith(('VmRSS:', 'VmHWM:'))}
                            graphics['processGpuMemoryAfterSeries'] = MemorySampler(process.pid).sample()
                        preparation = {}
                        if mode == 'dirty-world':
                            preparation = {name: quantiles(frames, name) for name in
                                           ('authoredUpdateMs', 'sceneBindMs', 'sceneIndexMs', 'sceneIndexAllocatedBytes', 'rendererWaitMs',
                                            'groundInvalidateMs', 'deferredGroundInvalidateMs', 'updateAndDrawMs')}
                            preparation['deferredBindings'] = sum(row['bindingDeferred'] for row in frames)
                        if from_flat_edge and not any(
                                math.isclose(frame['x'], state['viewport']['width'] - 32 * frame['zoom']) for frame in frames):
                            raise RuntimeError('Measured pan did not reach the flat camera limit')
                        save(identity, {'width': width, 'height': height, 'density': state['scaling'],
                                                  'halfWorldScale': half_world['zoom'], 'halfWorldReceipt': half_world['draw'],
                                                  'requestedHalfWorldScale': requested_scale,
                                                  'halfWorldZoomLimited': requested_scale < MIN_ZOOM,
                                                  'halfWorldDistinctTiles': distinct_tiles_at_center(half_world),
                                                  'worldTiles': state['worldWidth'] * state['worldHeight'] // 2,
                                                  'flat': state['flat'], 'initialRevision': state['revision'],
                                                  'initialView': state['draw'], 'initialDistinctTiles': distinct_tiles_at_center(state),
                                                  'fromFlatEdge': from_flat_edge,
                                                  'run': run, 'mode': mode, 'warmup': series_warm, 'samples': frames, 'warmupSamples': warmup_frames,
                                                  'drawMs': quantiles(frames, 'ms'),
                                                  'inputToReceiptMs': quantiles(frames, 'inputToReceiptMs'), **preparation, **graphics})
                        print(f'{size} run {run} {mode}: {len(frames)} changed frames', flush=True)
    finally:
        driver.close()


if __name__ == '__main__':
    main()
