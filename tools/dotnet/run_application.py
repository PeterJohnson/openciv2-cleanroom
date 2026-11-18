#!/usr/bin/env python3
"""Run production application input checks, binding compiled sources and invoked artifacts."""
import argparse
import json
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
import write_identity as identity

ROOT = identity.ROOT
LIBRARIES = identity.LIBRARIES - {'OpenCiv2.FoundationCases'} | {'OpenCiv2.App'}
HOSTS = {'OpenCiv2.Desktop', 'OpenCiv2.Browser'}


def validate(build):
    if build.get('format') != 'openciv2-application-seam/1': raise ValueError('Unknown application identity')
    names = [entry['Assembly'] for entry in build['build']]
    hosts = set(names) & HOSTS
    if len(hosts) != 1 or len(names) != len(set(names)) or set(names) != LIBRARIES | hosts:
        raise ValueError('Incomplete or duplicate application identity')
    sdk = json.loads((ROOT / 'global.json').read_text())['sdk']['version']
    runtime = ET.parse(ROOT / 'Directory.Build.props').findtext('PropertyGroup/RuntimeFrameworkVersion')
    sources = {}
    for entry in build['build']:
        name = entry['Assembly']; expected = identity.expected_inputs(ROOT, name)
        target = 'net10.0-browser' if name == 'OpenCiv2.Browser' else 'net10.0'
        if entry['Sources'] != expected or (entry['Sdk'], entry['Runtime'], entry['Target'], entry['Configuration']) != (sdk, runtime, target, 'Release'):
            raise ValueError('Stale or unpinned application assembly: ' + name)
        sources.update(expected)
    return sources


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', required=True, type=Path); parser.add_argument('--node', default='node')
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--browser-bundle', type=Path); group.add_argument('--adapter', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    host = 'OpenCiv2.Browser' if args.browser_bundle else 'OpenCiv2.Desktop'
    sources = {}
    for name in LIBRARIES | {host}: sources.update(identity.expected_inputs(ROOT, name))
    sources.update({p.relative_to(ROOT).as_posix(): identity.sha(p) for p in (ROOT / 'tools/dotnet').glob('*') if p.is_file()})
    if args.browser_bundle:
        bundle = args.browser_bundle.resolve(); artifacts = {str(p): identity.sha(p) for p in bundle.rglob('*') if p.is_file()}
        if not artifacts: raise ValueError('Missing published application bundle')
    else:
        if not args.adapter: raise ValueError('Missing desktop command')
        entry = Path(args.adapter[-1]).resolve()
        if not entry.is_file(): raise ValueError('Desktop command must end with its assembly/apphost')
        paths = {p for p in entry.parent.rglob('*') if p.is_file() and p.suffix != '.pdb'}
        host_exe = Path(shutil.which(args.adapter[0]) or args.adapter[0]).resolve()
        if host_exe.is_file(): paths.add(host_exe)
        artifacts = {str(p): identity.sha(p) for p in sorted(paths)}
    output = args.report.resolve()
    if str(output) in artifacts or output in {ROOT / p for p in sources}: raise ValueError('Output would overwrite an input')
    output.parent.mkdir(parents=True, exist_ok=True)
    if args.browser_bundle:
        command = [args.node, str(ROOT / 'tools/dotnet/run_application_browser.cjs'), str(bundle), str(output)]
    else:
        command = [sys.executable, str(ROOT / 'tools/dotnet/run_application_native.py'), '--output', str(output), '--adapter', *args.adapter]
    subprocess.run(command, check=True, timeout=240)
    report = json.loads(output.read_text(), object_pairs_hook=identity.unique_object)
    compiled = validate(report['identity'])
    expected_count = 16 if args.browser_bundle else 6
    if report.get('format') != 'openciv2-application-input/1' or len(report['checks']) != expected_count or report.get('errors'):
        raise ValueError('Incomplete application checks')
    if any(identity.sha(ROOT / p) != digest for p, digest in sources.items()) or any(identity.sha(Path(p)) != digest for p, digest in artifacts.items()):
        raise ValueError('Application sources/artifacts changed during input checks')
    report.update(compiledSources=compiled, reportingSources=sources, invokedArtifacts=artifacts, command=command, machine=platform.platform(),
                  revision=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip())
    output.write_text(json.dumps(report, indent=2) + '\n')
    print(f'Bound {len(compiled)} compiled inputs and {len(artifacts)} application artifacts.')


if __name__ == '__main__': main()
