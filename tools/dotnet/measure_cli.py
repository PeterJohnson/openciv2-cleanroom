#!/usr/bin/env python3
"""Measure C# CLI workloads on Linux with per-child GNU time resources.

Outputs are validated outside timing; all warmups and measured samples remain
in the report. This is performance/regression evidence, not an
independent original-game oracle. Default execution builds the pinned sources.
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import platform
import signal
import sqlite3
import shutil
import subprocess
import tempfile
import time

from performance_inputs import ROOT, inventory, prepare, sha
from write_identity import COMMON

BUDGET_PATH = ROOT / 'docs/performance/budgets.json'


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def source_inventory():
    paths = [ROOT / p for p in COMMON]
    paths += [p for folder in ('src', 'tests', 'native', 'packaging', 'solutions') for p in (ROOT / folder).rglob('*') if p.is_file()
              and not {'bin', 'obj'} & set(p.parts) and p.suffix in ('.cs', '.csproj', '.slnx', '.json', '.props', '.targets')]
    paths += list((ROOT / 'tools/dotnet').glob('*.py'))
    paths += [ROOT / 'tests/inspection_cli.py', ROOT / 'tests/rules_cli.py', BUDGET_PATH]
    return {p.relative_to(ROOT).as_posix(): sha(p) for p in sorted(set(paths))}


def artifacts(executable):
    return {p.as_posix(): sha(p) for p in sorted(executable.parent.iterdir()) if p.is_file()}


def invoke(command, directory, environment):
    stdout, stderr = directory / 'stdout', directory / 'stderr'
    resources = directory / 'resources'
    # GNU time forks the measured child after its own small native exec. Direct
    # Python fork/exec wait4 RSS includes inherited Python pages before exec.
    timed = ['/usr/bin/time', '--quiet', '--format=%M %U %S %x', '--output=' + str(resources), *map(str, command)]
    with stdout.open('wb') as out, stderr.open('wb') as err:
        start = time.perf_counter_ns()
        process = subprocess.Popen(timed, stdout=out, stderr=err, env=environment, start_new_session=True)
        try:
            while True:
                pid, status, _ = os.wait4(process.pid, os.WNOHANG)
                if pid:
                    process.returncode = os.waitstatus_to_exitcode(status)
                    break
                if time.perf_counter_ns() - start > 90_000_000_000:
                    os.killpg(process.pid, signal.SIGKILL)
                    _, status, _ = os.wait4(process.pid, 0)
                    process.returncode = os.waitstatus_to_exitcode(status)
                    raise TimeoutError('Workload exceeded 90 seconds: ' + str(command))
                time.sleep(.001)
        finally:
            if process.returncode is None:
                os.killpg(process.pid, signal.SIGKILL); process.wait()
        elapsed = (time.perf_counter_ns() - start) / 1e6
    rss_kib, user_s, system_s, child_status = resources.read_text().split()
    if int(child_status) != process.returncode: raise ValueError('Time wrapper and child exit status differ')
    return {'wallMs': elapsed, 'userMs': float(user_s) * 1000, 'systemMs': float(system_s) * 1000,
            'peakRssMiB': int(rss_kib) / 1024, 'exitCode': process.returncode,
            'stdoutBytes': stdout.stat().st_size, 'stderr': stderr.read_text()[:4000]}, stdout


def command_for(workload, tools, inputs, output):
    source = inputs / 'large.sav'
    if workload == 'startup-help': return [tools['cli'], '--help']
    if workload == 'save-roundtrip': return [tools['roundtrip'], source, output / 'copy.sav']
    if workload == 'inspect-large': return [tools['cli'], 'inspect', source, '--json']
    if workload == 'validate-large': return [tools['cli'], 'validate', source, '--json']
    if workload == 'diff-large': return [tools['cli'], 'diff', source, inputs / 'changed.sav', '--json']
    if workload == 'resolve-pack':
        return [tools['cli'], 'content', 'resolve', inputs / 'overlay-1/content.json', '--json', '--locale', 'en',
                '--parent', inputs / 'overlay-0/content.json', '--parent', inputs / 'pack/content.json']
    if workload == 'convert-rules': return [tools['cli'], 'content', 'convert-rules', inputs / 'RULES.TXT', output / 'rules.json', output / 'mapping.json']
    if workload == 'export-sqlite': return [tools['sqlite'], 'export', source, '--sqlite', output / 'export.sqlite', '--json']
    raise ValueError(workload)


def validate(workload, sample, stdout, inputs, output, metadata):
    wanted = 1 if workload == 'diff-large' else 0
    if sample['exitCode'] != wanted or sample['stderr']:
        raise ValueError(f'{workload}: unexpected status/error: {sample}')
    if workload == 'startup-help':
        text = stdout.read_text()
        if 'inspect' not in text or 'convert-rules' not in text: raise ValueError('Incomplete help output')
        return 'help-command-matrix'
    doc = json.loads(stdout.read_bytes())
    source_hash = metadata['files']['large.sav']['sha256']
    if workload == 'save-roundtrip':
        if not all(doc[k] for k in ('byteEqual', 'orderedAuxiliaryEqual', 'outputWritten')): raise ValueError('Failed roundtrip')
        for k, v in [('version', 44), ('width', 252), ('height', 252), ('unitSlots', 2048), ('citySlots', 256)]:
            if doc[k] != v: raise ValueError('Wrong roundtrip extent')
        if sha(output / 'copy.sav') != source_hash: raise ValueError('Wrong roundtrip bytes')
    elif workload in ('inspect-large', 'validate-large'):
        if not doc['decoded'] or doc['source']['sha256'] != source_hash or doc['validation']['status'] != 'passed-available-checks': raise ValueError('Input not admitted')
        if any(d['severity'] == 'error' for d in doc['diagnostics']): raise ValueError('Unexpected input diagnostic')
        if workload == 'inspect-large':
            counts = Counter(r['section'] for r in doc['records'])
            if any(counts[k] != v for k, v in [('units', 2048), ('cities', 256), ('tiles', 31752)]): raise ValueError('Incomplete inspection')
            recovered = bytearray()
            for record in doc['records']:
                if int(record['offset']) != len(recovered): raise ValueError('Disordered inspection')
                recovered.extend(bytes.fromhex(record['bytes']))
            if hashlib.sha256(recovered).hexdigest() != source_hash: raise ValueError('Inspection did not preserve complete bytes')
        elif doc['records']: raise ValueError('Validation unexpectedly emitted records')
    elif workload == 'diff-large':
        changes = doc['fieldChanges']
        if not doc['different'] or not doc['fieldsCompared'] or len(changes) != 1: raise ValueError('Wrong diff extent')
        change = changes[0]
        if (change['section'], change['slot'], change['field'], change['before']['value'], change['after']['value']) != ('units', '0', 'token', '1', '4294967295'): raise ValueError('Wrong diff values')
    elif workload == 'resolve-pack':
        if doc['status'] != 'resolved' or len(doc['manifests']) != 3 or not doc['files']: raise ValueError('Incomplete pack resolution')
    elif workload == 'convert-rules':
        if doc['status'] != 'converted' or doc['source']['sha256'] != metadata['files']['RULES.TXT']['sha256']: raise ValueError('Wrong conversion input')
        components = []
        for key, name in [('rules', 'rules.json'), ('compatibility', 'mapping.json')]:
            receipt = doc['outputs'][key]
            if not receipt['published'] or receipt['sha256'] != sha(output / name): raise ValueError('Unpublished conversion')
            components.append(json.loads((output / name).read_bytes()))
        if len(components[0]['units']) != 62 or len(components[0]['advances']) != 100: raise ValueError('Incomplete converted rules')
        return digest(components)  # Formatting and output-path receipts may differ.
    elif workload == 'export-sqlite':
        path = output / 'export.sqlite'
        if doc['sourceSha256'] != source_hash or int(doc['bytes']) != path.stat().st_size: raise ValueError('Invalid SQLite receipt')
        with sqlite3.connect(path) as db:
            if db.execute('PRAGMA integrity_check').fetchall() != [('ok',)] or db.execute('PRAGMA foreign_key_check').fetchall(): raise ValueError('Invalid database')
            counts = dict(db.execute('SELECT section, count(*) FROM records GROUP BY section'))
            if any(counts.get(k) != v for k, v in [('units', 2048), ('cities', 256), ('tiles', 31752)]): raise ValueError('Incomplete database')
            if db.execute('SELECT id FROM snapshots').fetchall() != [(source_hash,)]: raise ValueError('Wrong database identity')
            # Stream canonical rows outside timing, so full equality does not require
            # retaining two enormous Python row sets or trusting counts alone.
            schema = db.execute('SELECT type,name,tbl_name,sql FROM sqlite_schema ORDER BY name').fetchall()
            h = hashlib.sha256(json.dumps(schema).encode())
            for _, name, _, _ in (s for s in schema if s[0] == 'table'):
                quoted = '"' + name.replace('"', '""') + '"'
                columns = len(db.execute('PRAGMA table_info(' + quoted + ')').fetchall())
                for row in db.execute('SELECT * FROM ' + quoted + ' ORDER BY ' + ','.join(map(str, range(1, columns + 1)))):
                    h.update(json.dumps(row, default=lambda b: {'hex': b.hex()}, separators=(',', ':')).encode()); h.update(b'\n')
            doc['bytes'] = '<verified>'
            return digest([doc, h.hexdigest()])
    return digest(doc)


def quantile(values, percentile):
    return sorted(values)[max(0, math.ceil(len(values) * percentile) - 1)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--dotnet', default='dotnet')
    parser.add_argument('--smoke', action='store_true', help='One invocation per workload; no performance acceptance claim')
    args = parser.parse_args()
    if platform.system() != 'Linux':
        raise ValueError('This collector requires Linux GNU time RSS units; do not relabel another platform')
    args.report = args.report.resolve()
    args.report.parent.mkdir(parents=True, exist_ok=True)
    if args.report.exists():
        raise ValueError('Use a new report path; do not overwrite measured evidence')
    budget = json.loads(BUDGET_PATH.read_text())
    sources = source_inventory()
    dotnet = Path(shutil.which(args.dotnet) or args.dotnet).resolve()
    environment = dict(os.environ, DOTNET_ROOT=str(dotnet.parent))
    builds = []
    for solution in ('OpenCiv2.slnx', 'solutions/OpenCiv2.Sqlite.slnx'):
        log = args.report.with_name(args.report.stem + '-' + Path(solution).name + '.build.log')
        command = [str(dotnet), 'build', solution, '-c', 'Release', '--no-restore', '-m:1', '-nr:false']
        with log.open('wb') as stream:
            subprocess.run(command, cwd=ROOT, env=environment, stdout=stream, stderr=subprocess.STDOUT, check=True, timeout=180)
        builds.append({'command': command, 'logSha256': sha(log)})
    if source_inventory() != sources:
        raise ValueError('Build modified measured sources')
    executables = {
        'cli': ROOT / 'src/OpenCiv2.Cli/bin/Release/net11.0/oc2',
        'roundtrip': ROOT / 'tests/OpenCiv2.SaveRoundTrip/bin/Release/net11.0/OpenCiv2.SaveRoundTrip',
        'sqlite': ROOT / 'src/OpenCiv2.Cli.Sqlite/bin/Release/net11.0/oc2-sqlite',
    }
    before = {str(p): artifacts(p) for p in set(executables.values())}
    report = {'format': 'openciv2-cli-performance/1', 'createdUtc': datetime.now(timezone.utc).isoformat(),
              'completed': False, 'smoke': args.smoke, 'budgets': budget, 'sources': sources, 'builds': builds,
              'artifacts': before, 'host': {'platform': platform.platform(),
                'cpu': subprocess.check_output(['lscpu'], text=True), 'memory': Path('/proc/meminfo').read_text(),
                'affinity': sorted(os.sched_getaffinity(0)), 'loadBefore': os.getloadavg(),
                'resourceCollector': subprocess.check_output(['/usr/bin/time', '--version'], text=True),
                'resourceCollectorSha256': sha(Path('/usr/bin/time'))},
              'collection': 'New processes; uncontrolled OS cache; wall time includes GNU time launch and 1ms polling. Verification is outside timing.',
              'results': {}}
    warmups = 0 if args.smoke else budget['sampling']['warmup']
    repetitions = 1 if args.smoke else budget['sampling']['cliRepetitions']
    with tempfile.TemporaryDirectory(prefix='oc2-cli-performance-', dir=args.report.parent) as tmp:
        work = Path(tmp)
        inputs = work / 'inputs'
        metadata = prepare(inputs)
        report['inputs'] = metadata
        for workload, limits in budget['cli'].items():
            rows = []
            fingerprints = set()
            for iteration in range(warmups + repetitions):
                output = work / 'output'
                output.mkdir()
                command = command_for(workload, executables, inputs, output)
                sample, stdout = invoke(command, output, environment)
                fingerprint = validate(workload, sample, stdout, inputs, output, metadata)
                sample.update(iteration=iteration, warmup=iteration < warmups, validatedOutputSha256=fingerprint)
                rows.append(sample)
                fingerprints.add(fingerprint)
                shutil.rmtree(output)
            if len(fingerprints) != 1:
                raise ValueError('Repeated workload results disagree: ' + workload)
            measured = [r for r in rows if not r['warmup']]
            values = [r['wallMs'] for r in measured]
            summary = {'p50Ms': quantile(values, .5), 'p95Ms': quantile(values, .95),
                       'maxMs': max(values), 'peakRssMiB': max(r['peakRssMiB'] for r in measured)}
            summary['withinAbsoluteBudgets'] = all(summary[k] <= v for k, v in limits.items())
            report['results'][workload] = {'samples': rows, 'summary': summary}
            print(workload + ': ' + json.dumps(summary), flush=True)
            args.report.write_text(json.dumps(report, indent=2) + '\n')
        if inventory(inputs) != metadata['files']:
            raise ValueError('Workload modified its inputs')
    if sources != source_inventory() or before != {str(p): artifacts(p) for p in set(executables.values())}:
        raise ValueError('Measured inputs/artifacts changed')
    report['host']['loadAfter'] = os.getloadavg()
    report['completed'] = True
    report['allAbsoluteBudgetsPass'] = not args.smoke and all(r['summary']['withinAbsoluteBudgets'] for r in report['results'].values())
    args.report.write_text(json.dumps(report, indent=2) + '\n')
    print('Completed C# CLI measurement. Compare identical workloads and environments before making regression claims.', flush=True)


if __name__ == '__main__':
    main()
