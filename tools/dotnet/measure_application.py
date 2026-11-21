#!/usr/bin/env python3
"""Bind real production application startup measurements to sources and budgets."""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import tempfile
from measure_cli import BUDGET_PATH, quantile
from performance_inputs import ROOT, sha
from run_application import validate, LIBRARIES, map_content_inputs
from write_identity import expected_inputs, unique_object


def summarize(report, repetitions):
    if report.get('format') != 'openciv2-application-startup/1' or report.get('errors'): raise ValueError('Failed application startup report')
    validate(report['identity'])
    if report.get('host') not in ('browser', 'desktop'): raise ValueError('Unknown startup host')
    budget = json.loads(BUDGET_PATH.read_text()); browser = report['host'] == 'browser'
    expected_host = 'OpenCiv2.Browser' if browser else 'OpenCiv2.Desktop'
    if expected_host not in {b['Assembly'] for b in report['identity']['build']}: raise ValueError('Startup host identity mismatch')
    samples = report['samples']; populations = ['cold', 'warm'] if browser else ['desktop']
    if len(samples) != (repetitions + 3) * len(populations): raise ValueError('Incomplete sample population')
    if browser:
        for iteration in range(repetitions + 3):
            pair = [[s for s in samples if s['iteration'] == iteration and s['cache'] == cache] for cache in ('cold', 'warm')]
            if any(len(rows) != 1 for rows in pair): raise ValueError('Missing or duplicate navigation pair')
            cold, warm = (rows[0] for rows in pair)
            if sum(q['bodyBytes'] for q in warm['serverRequests']) >= sum(q['bodyBytes'] for q in cold['serverRequests']):
                raise ValueError('Cached navigation did not reduce actual transferred body bytes')
    summaries = {}
    for population in populations:
        rows = [s for s in samples if not browser or s['cache'] == population]
        if [s['iteration'] for s in rows] != list(range(repetitions + 3)) or [s['warmup'] for s in rows] != [True] * 3 + [False] * repetitions:
            raise ValueError('Wrong sample order/warmups')
        for row in rows:
            if not 0 < row['readyMs'] < 90000: raise ValueError('Invalid startup timing')
            state, completed = row['snapshot'], row['completed']
            if state['observer'] != '1' or [u['slot'] for u in state['units']] != ['0', '1', '2'] or not 0 < state['controls']['send']['width'] < 10000 or state['controls']['send']['enabled'] is not True:
                raise ValueError('Unready model/layout')
            if completed['status'] != 'Completed' or completed['changed'] is not True or completed['effects'] != 1:
                raise ValueError('Readiness did not include functional input')
            if not state.get('contentLoaded') or not state.get('map', {}).get('composition'):
                raise ValueError('Readiness omitted pinned content or first map draw')
        measured = rows[3:]; values = [r['readyMs'] for r in measured]
        summary = {'p50Ms': quantile(values, .5), 'p95Ms': quantile(values, .95), 'maxMs': max(values)}
        if browser:
            for row in rows:
                counters = [row['networkEncodedBytes'], *[q[key] for q in row['serverRequests'] for key in ('bodyBytes', 'decodedBytes')]]
                if any(type(value) is not int or value < 0 for value in counters):
                    raise ValueError('Invalid transfer byte count')
                if any(type(row[stage][key]) is not int or row[stage][key] <= 0 for stage in ('memory', 'memoryAfterCommand') for key in ('memories', 'bytes')):
                    raise ValueError('Missing observed WASM memory')
                if row['networkEncodedBytes'] < sum(q['bodyBytes'] for q in row['serverRequests']):
                    raise ValueError('Network accounting omits transferred body bytes')
                if row['memoryAfterCommand']['bytes'] < row['memory']['bytes'] or row['memoryAfterCommand']['memories'] < row['memory']['memories']:
                    raise ValueError('Impossible shrinking WASM memory accounting')
            summary.update(maxNetworkEncodedMiB=max(r['networkEncodedBytes'] for r in measured) / 1048576,
                           maxTransferredBodyMiB=max(sum(q['bodyBytes'] for q in r['serverRequests']) for r in measured) / 1048576,
                           maxWasmCapacityMiB=max(max(r['memory']['bytes'], r['memoryAfterCommand']['bytes']) for r in measured) / 1048576)
            if any(r['memory']['memories'] < 1 or r['memory']['bytes'] <= 0 for r in rows): raise ValueError('Missing observed WASM memory')
            if population == 'cold' and any(not r['serverRequests'] or not any(q['encoding'] == 'br' for q in r['serverRequests']) for r in rows):
                raise ValueError('Cold navigation did not transfer compressed runtime resources')
            summary['withinBudgets'] = (summary['maxMs'] <= budget['browser'][population + 'ReadyMs'] and
                                        summary['maxNetworkEncodedMiB'] <= budget['browser']['compressedTransferMiB'] and
                                        summary['maxWasmCapacityMiB'] <= budget['browser']['peakManagedHeapMiB'])
        else:
            if any(len(r['idleRssBytes']) != 10 or any(type(v) is not int or v <= 0 for v in [*r['idleRssBytes'], r['startupHighWaterBytes']]) for r in rows): raise ValueError('Missing idle RSS samples')
            summary.update(maxIdleRssMiB=max(max(r['idleRssBytes']) for r in measured) / 1048576,
                           maxStartupHighWaterMiB=max(r['startupHighWaterBytes'] for r in measured) / 1048576)
            summary['withinBudgets'] = summary['maxMs'] <= budget['desktop']['readyMs'] and summary['maxIdleRssMiB'] <= budget['desktop']['idleRssMiB']
        summaries[population] = summary
    return summaries


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--report', required=True, type=Path)
    parser.add_argument('--smoke', action='store_true'); parser.add_argument('--node', default='node')
    parser.add_argument('--xvfb', default='Xvfb')
    parser.add_argument('--map-assets', type=Path)
    parser.add_argument('--map-fonts', type=Path)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--browser-bundle', type=Path); group.add_argument('--desktop', type=Path)
    args = parser.parse_args(); browser = args.browser_bundle is not None
    if not browser and (not args.map_assets or not args.map_fonts): parser.error('Native startup requires --map-assets and --map-fonts')
    if platform.system() != 'Linux': raise ValueError('This collection protocol currently requires Linux')
    args.report = args.report.resolve(); args.report.parent.mkdir(parents=True, exist_ok=True)
    repetitions = 1 if args.smoke else 15
    host = 'OpenCiv2.Browser' if browser else 'OpenCiv2.Desktop'
    sources = {}
    hosts = {host} if browser else {host, 'OpenCiv2.IO.Native', 'OpenCiv2.Platform.Native'}
    for name in LIBRARIES | hosts: sources.update(expected_inputs(ROOT, name))
    sources.update({p.relative_to(ROOT).as_posix(): sha(p) for p in (ROOT / 'tools/dotnet').glob('*') if p.is_file()})
    sources['docs/performance/budgets.json'] = sha(BUDGET_PATH)
    root = args.browser_bundle.resolve() if browser else args.desktop.resolve().parent
    paths = [p for p in root.rglob('*') if p.is_file()]
    if not paths: raise ValueError('Missing production artifacts')
    executable = Path(shutil.which(args.node if browser else args.xvfb) or (args.node if browser else args.xvfb)).resolve(); paths.append(executable)
    if browser: paths.append(Path(os.environ['OC2_CHROMIUM_EXECUTABLE']).resolve())
    artifacts = {str(p): sha(p) for p in paths}
    if not browser:
        artifacts.update({str(p.resolve()): sha(p) for p in map_content_inputs(args.map_assets, args.map_fonts)})
    load = os.getloadavg()
    with tempfile.TemporaryDirectory(prefix='oc2-app-performance-', dir=args.report.parent) as temp:
        raw = Path(temp) / 'raw.json'
        if browser:
            command = [str(executable), str(ROOT / 'tools/dotnet/measure_application_browser.cjs'), str(root), str(raw), str(repetitions)]
        else:
            command = [sys.executable, str(ROOT / 'tools/dotnet/run_xvfb.py'), '--xvfb', str(executable), sys.executable,
                       str(ROOT / 'tools/dotnet/measure_application_native.py'), '--report', str(raw), '--repetitions', str(repetitions),
                       '--map-assets', str(args.map_assets.resolve()), '--map-fonts', str(args.map_fonts.resolve()), '--adapter', str(args.desktop.resolve())]
        subprocess.run(command, check=True, timeout=1800)
        report = json.loads(raw.read_text(), object_pairs_hook=unique_object)
        # Preserve a complete raw population even if the fixed acceptance gates reject it.
        shutil.copy2(raw, args.report.with_suffix('.raw.json'))
        # Preserve diagnostics/screenshots after successful execution.
        for extra in Path(temp).glob('raw.*'):
            if extra != raw:
                suffix = extra.name[len(raw.name):] if extra.name.startswith(raw.name) else extra.name[3:]
                shutil.copy2(extra, args.report.with_name(args.report.name + suffix))
    summaries = summarize(report, repetitions)
    if any(sha(ROOT / p) != value for p, value in sources.items()) or any(sha(Path(p)) != value for p, value in artifacts.items()):
        raise ValueError('Application sources/artifacts changed during collection')
    report.update(createdUtc=datetime.now(timezone.utc).isoformat(), revision=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
                  budgetSha256=sha(BUDGET_PATH), sources=sources, artifacts=artifacts, summaries=summaries, smokeOnly=args.smoke,
                  allStartupBudgetsPass=not args.smoke and all(s['withinBudgets'] for s in summaries.values()),
                  machine={'platform': platform.platform(), 'cpu': subprocess.check_output(['lscpu'], text=True), 'affinity': sorted(os.sched_getaffinity(0)),
                           'loadBefore': load, 'loadAfter': os.getloadavg(), 'memory': Path('/proc/meminfo').read_text()},
                  logicalPackageBytes=sum(p.stat().st_size for p in root.rglob('*') if p.is_file() and p.suffix not in ('.br', '.gz')),
                  limitations=['Linux x64 Xvfb/Chromium software rendering; no display FPS, native compositor or Mac/Windows claims.',
                               'Browser cold means new isolated context with empty HTTP/IndexedDB cache; OS and browser-engine caches are uncontrolled.',
                               'Browser uses localhost without bandwidth/latency throttling; serving verified published Brotli assets.',
                               'Ready means owned model/control layout plus paint opportunity (browser) or observation-timer publication (desktop); actual input verifies functionality afterward.',
                               'Desktop observation timer remains enabled during idle RSS sampling; startup high-water comes from /proc, not inherited fork memory.',
                               'WASM memory capacity is monotonic and bounds managed heap plus native allocations; it is not browser process RSS.'])
    args.report.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(summaries, indent=2))


if __name__ == '__main__': main()
