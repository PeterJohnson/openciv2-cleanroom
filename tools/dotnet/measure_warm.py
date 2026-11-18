#!/usr/bin/env python3
"""Collect source-bound native/WASM production operation samples, excluding loading."""
import argparse
import base64
from datetime import datetime, timezone
import json
import math
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

from performance_inputs import ROOT, prepare, sha
from measure_cli import BUDGET_PATH, quantile
from write_identity import expected_inputs, unique_object

LIBRARIES = {'OpenCiv2.Base', 'OpenCiv2.IO', 'OpenCiv2.Contracts', 'OpenCiv2.Core',
             'OpenCiv2.Formats', 'OpenCiv2.Query', 'OpenCiv2.Network', 'OpenCiv2.Session', 'OpenCiv2.PerformanceCases'}


def validate(report, inputs, repetitions, browser):
    budget = json.loads(BUDGET_PATH.read_text())
    if report.get('Format') != 'openciv2-warm-performance/1' or report.get('PageErrors'):
        raise ValueError('Invalid production workload report')
    if report.get('Inputs') != {k: v['sha256'] for k, v in inputs['files'].items()}:
        raise ValueError('Measured input identity mismatch')
    if set(report.get('Results', {})) != set(budget['warmNative']):
        raise ValueError('Missing or extra production workloads')
    host = 'OpenCiv2.PerformanceBrowser' if browser else 'OpenCiv2.PerformanceProbe'
    names = [b['Assembly'] for b in report['Build']]
    if len(names) != len(set(names)) or set(names) != LIBRARIES | {host}:
        raise ValueError('Incomplete or duplicate compiled assembly identities')
    sdk = json.loads((ROOT / 'global.json').read_text())['sdk']['version']
    runtime = ET.parse(ROOT / 'Directory.Build.props').findtext('PropertyGroup/RuntimeFrameworkVersion')
    for entry in report['Build']:
        name = entry['Assembly']
        target = 'net11.0-browser' if name == 'OpenCiv2.PerformanceBrowser' else 'net11.0'
        if entry['Sources'] != expected_inputs(ROOT, name) or (entry['Sdk'], entry['Runtime'], entry['Target'], entry['Configuration']) != (sdk, runtime, target, 'Release'):
            raise ValueError('Stale, incomplete or unpinned workload assembly: ' + name)
    summaries = {}
    for name, rows in report['Results'].items():
        if len(rows) != repetitions + 3 or [r['Warmup'] for r in rows] != [True] * 3 + [False] * repetitions:
            raise ValueError('Wrong sample/warmup population: ' + name)
        hashes = {r['OutputSha256'] for r in rows}
        if len(hashes) != 1 or any(len(h) != 64 or any(c not in '0123456789abcdef' for c in h) for h in hashes):
            raise ValueError('Inconsistent or absent verified output: ' + name)
        for row in rows:
            for key in ('WallMs', 'AllocatedBytes', 'Gen0', 'Gen1', 'Gen2', 'ManagedHeapBytes', 'ManagedHeapPlusAllocatedBytes'):
                if type(row[key]) not in (int, float) or not math.isfinite(row[key]) or row[key] < 0:
                    raise ValueError('Invalid numeric sample: ' + name + '.' + key)
            if browser:
                if row['GcPauseMs'] is not None: raise ValueError('Browser GC pause duration is not measured')
            elif type(row['GcPauseMs']) not in (int, float) or not math.isfinite(row['GcPauseMs']) or row['GcPauseMs'] < 0:
                raise ValueError('Invalid native GC pause duration')
        measured = rows[3:]; limits = budget['warmNative'][name]
        multiplier = budget['browser']['warmOperationLatencyMultiplier'] if browser else 1
        summary = {'p50Ms': quantile([r['WallMs'] for r in measured], .5),
                   'p95Ms': quantile([r['WallMs'] for r in measured], .95),
                   'maxMs': max(r['WallMs'] for r in measured),
                   'maxAllocatedMiB': max(r['AllocatedBytes'] for r in measured) / 1048576,
                   'maxObservedManagedHeapMiB': max(r['ManagedHeapBytes'] for r in measured) / 1048576,
                   'maxHeapPlusAllocatedMiB': max(r['ManagedHeapPlusAllocatedBytes'] for r in measured) / 1048576,
                   'totalGcPauseMs': None if browser else sum(r['GcPauseMs'] for r in measured),
                   'gcCollections': [sum(r[key] for r in measured) for key in ('Gen0', 'Gen1', 'Gen2')]}
        summary['withinBudgets'] = (summary['p50Ms'] <= limits['p50Ms'] * multiplier and
                                    summary['p95Ms'] <= limits['p95Ms'] * multiplier and
                                    summary['maxAllocatedMiB'] <= limits['allocatedMiBPerOperation'])
        if browser:
            summary['withinBudgets'] &= summary['maxHeapPlusAllocatedMiB'] <= budget['browser']['peakManagedHeapMiB']
            if 'WasmMemoryAfter' in report:
                memory = report['WasmMemoryAfter']
                if memory['memories'] < 1 or memory['bytes'] <= 0: raise ValueError('Unobserved runtime WASM capacity')
                summary['runtimeWasmCapacityMiB'] = memory['bytes'] / 1048576
                summary['withinBudgets'] &= summary['runtimeWasmCapacityMiB'] <= budget['browser']['peakManagedHeapMiB']
        summaries[name] = summary
    return summaries


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--dotnet', default='dotnet')
    parser.add_argument('--browser-bundle', type=Path)
    parser.add_argument('--node', default='node')
    parser.add_argument('--smoke', action='store_true')
    args = parser.parse_args(); browser = args.browser_bundle is not None
    args.report = args.report.resolve(); args.report.parent.mkdir(parents=True, exist_ok=True)
    budget = json.loads(BUDGET_PATH.read_text()); dotnet = Path(shutil.which(args.dotnet) or args.dotnet).resolve()
    repetitions = 1 if args.smoke else budget['sampling']['browserRepetitions' if browser else 'coreRepetitions']
    host = 'OpenCiv2.PerformanceBrowser' if browser else 'OpenCiv2.PerformanceProbe'
    sources = {}
    for name in LIBRARIES | {host}: sources.update(expected_inputs(ROOT, name))
    sources.update({p.relative_to(ROOT).as_posix(): sha(p) for p in (ROOT / 'tools/dotnet').glob('*') if p.is_file()})
    for name in ('tests/inspection_cli.py', 'tests/rules_cli.py', 'solutions/OpenCiv2.Performance.slnx'):
        sources[name] = sha(ROOT / name)
    sources['docs/performance/budgets.json'] = sha(BUDGET_PATH)
    environment = dict(os.environ, DOTNET_ROOT=str(dotnet.parent))
    if browser:
        bundle = args.browser_bundle.resolve()
        executable = Path(shutil.which(args.node) or args.node).resolve()
        artifact_paths = [p for p in bundle.rglob('*') if p.is_file()] + [executable]
        chromium = Path(environment['OC2_CHROMIUM_EXECUTABLE']).resolve()
        artifact_paths.append(chromium)
    else:
        project = ROOT / 'tests' / host / (host + '.csproj')
        log = args.report.with_suffix('.build.log')
        with log.open('wb') as output:
            subprocess.run([str(dotnet), 'build', str(project), '-c', 'Release', '--no-restore', '-m:1', '-nr:false'], cwd=ROOT,
                           env=environment, stdout=output, stderr=subprocess.STDOUT, check=True, timeout=180)
        executable = project.parent / 'bin/Release/net11.0' / host
        artifact_paths = [p for p in executable.parent.iterdir() if p.is_file()] + [dotnet]
    artifacts = {p.as_posix(): sha(p) for p in artifact_paths}
    load = os.getloadavg() if hasattr(os, 'getloadavg') else None
    with tempfile.TemporaryDirectory(prefix='oc2-warm-', dir=args.report.parent) as temporary:
        folder = Path(temporary); inputs = folder / 'inputs'; metadata = prepare(inputs)
        payload = folder / 'inputs.json'
        payload.write_text(json.dumps({p.relative_to(inputs).as_posix(): base64.b64encode(p.read_bytes()).decode() for p in sorted(inputs.rglob('*')) if p.is_file()}))
        raw = folder / 'report.json'
        if browser:
            command = [str(executable), str(ROOT / 'tools/dotnet/run_performance_browser.cjs'), str(bundle), str(payload), str(raw), str(repetitions)]
            subprocess.run(command, env=environment, check=True, timeout=600)
        else:
            command = [str(executable), str(payload), str(repetitions)]
            with raw.open('wb') as output: subprocess.run(command, env=environment, stdout=output, check=True, timeout=600)
        report = json.loads(raw.read_text(), object_pairs_hook=unique_object)
        summaries = validate(report, metadata, repetitions, browser)
    if any(sha(ROOT / p) != value for p, value in sources.items()) or any(sha(Path(p)) != value for p, value in artifacts.items()):
        raise ValueError('Workload source/artifact changed during measurement')
    report.update(CreatedUtc=datetime.now(timezone.utc).isoformat(), Revision=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
                  BudgetSha256=sha(BUDGET_PATH), Sources=sources, Artifacts=artifacts, InputMetadata=metadata,
                  Host={'platform': platform.platform(), 'affinity': sorted(os.sched_getaffinity(0)) if hasattr(os, 'sched_getaffinity') else None,
                        'loadBefore': load, 'loadAfter': os.getloadavg() if hasattr(os, 'getloadavg') else None,
                        'cpu': subprocess.check_output(['lscpu'], text=True) if platform.system() == 'Linux' else platform.processor(),
                        'dotnetInfo': subprocess.check_output([str(dotnet), '--info'], env=environment, text=True),
                        'runtimeEnvironment': {k: v for k, v in environment.items() if k.startswith(('DOTNET_', 'COMPlus_'))}},
                  Summaries=summaries, SmokeOnly=args.smoke,
                  AllAbsoluteBudgetsPass=not args.smoke and all(s['withinBudgets'] for s in summaries.values()),
                  Limitations=['Warmed operation latency and fresh-process CLI timing are separate populations.',
                               'Reset/input loading/result validation excluded; verification allocations can influence later GC.',
                               'No forced GC between operations. Heap snapshots and pre-call heap plus allocated bytes are estimates, not continuous resident/committed memory peaks.',
                               'Browser GC pause API reported zero despite collections; durations are unavailable and serialized as null.',
                               'Browser probe startup is not production Avalonia readiness or transfer acceptance.'])
    args.report.write_text(json.dumps(report, indent=2) + '\n')
    for name, result in summaries.items(): print(name + ': ' + json.dumps(result), flush=True)


if __name__ == '__main__': main()
