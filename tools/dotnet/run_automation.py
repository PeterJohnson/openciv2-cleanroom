#!/usr/bin/env python3
"""Validate production Lua native/browser executions and bind sources/artifacts."""
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
LIBRARIES = identity.LIBRARIES - {'OpenCiv2.FoundationCases', 'OpenCiv2.ViewModels'} | {'OpenCiv2.Automation', 'OpenCiv2.AutomationCases'}
HOSTS = {'OpenCiv2.AutomationProbe', 'OpenCiv2.AutomationBrowser'}
CASES = ['command-equivalence', 'observer-and-descriptor', 'instruction-budget', 'memory-budget',
         'host-output-command-budgets', 'script-errors', 'cancel-before-and-after-effects', 'disposal-and-gc',
         'revoked-authority', 'task-command-ownership', 'stale-target', 'sandbox-and-limits', 'error-preserves-effects']
OUTCOME = {'Stack': '2,-1,100,1;-1,2,101,1;1,0,102,1;-1,-1,103,2', 'Effects': 1, 'Changed': True, 'Output': 'completed\n'}


def expected_inputs(name):
    expected = identity.expected_inputs(ROOT, name)
    extra = []
    if name == 'OpenCiv2.Automation':
        extra = ['docs/automation.md', 'src/OpenCiv2.Automation/Prelude.lua',
                 'native/lua/bridge.c', 'native/lua/bridge.h']
    elif name == 'OpenCiv2.AutomationProbe':
        extra = ['native/lua/Native.targets', 'tools/dotnet/build_lua.py', 'build/lua/native/oc2-lua-build.json']
    elif name == 'OpenCiv2.AutomationBrowser':
        extra = ['native/lua/Browser.targets', 'tools/dotnet/prepare_lua_wasm.py', 'tools/dotnet/build_lua.py',
                 'build/lua/wasm/oc2-lua-wasm.json']
        extra += [p.relative_to(ROOT).as_posix() for folder in ('source', 'cpp') for p in (ROOT / 'build/lua/wasm' / folder).iterdir() if p.is_file()]
    expected.update({p: identity.sha(ROOT / p) for p in extra})
    return expected


def validate(report):
    if report.get('Format') != 'openciv2-production-lua/1' or report.get('Passed') is not True or report.get('PageErrors'):
        raise ValueError('Missing or failed production Lua report')
    if [c['Name'] for c in report['Cases']] != CASES or any(c.get('Passed') is not True or c.get('Error') is not None for c in report['Cases']):
        raise ValueError('Missing, duplicate, reordered or failed Lua cases')
    if report.get('Outcome') != OUTCOME:
        raise ValueError('Production Lua semantic outcome differs from the authored command contract')
    names = [entry['Assembly'] for entry in report['Build']]
    hosts = set(names) & HOSTS
    if len(hosts) != 1 or len(names) != len(set(names)) or set(names) != LIBRARIES | hosts:
        raise ValueError('Incomplete or duplicate Lua assembly identities')
    sdk = json.loads((ROOT / 'global.json').read_text())['sdk']['version']
    runtime = ET.parse(ROOT / 'Directory.Build.props').findtext('PropertyGroup/RuntimeFrameworkVersion')
    sources = {}
    for entry in report['Build']:
        name = entry['Assembly']; expected = expected_inputs(name)
        target = 'net11.0-browser' if name == 'OpenCiv2.AutomationBrowser' else 'net11.0'
        if entry['Sources'] != expected or (entry['Sdk'], entry['Runtime'], entry['Target'], entry['Configuration']) != (sdk, runtime, target, 'Release'):
            raise ValueError('Stale, incomplete or unpinned Lua assembly: ' + name)
        sources.update(expected)
    return sources


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path, required=True); parser.add_argument('--node', default='node')
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--browser-bundle', type=Path); group.add_argument('--adapter', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    browser = bool(args.browser_bundle)
    subprocess.run([sys.executable, str(ROOT / 'tools/dotnet' / ('prepare_lua_wasm.py' if browser else 'build_lua.py')), '--verify'], check=True)
    host = 'OpenCiv2.AutomationBrowser' if browser else 'OpenCiv2.AutomationProbe'
    sources = {}
    for name in LIBRARIES | {host}: sources.update(expected_inputs(name))
    reporting = {p.relative_to(ROOT).as_posix(): identity.sha(p) for p in (ROOT / 'tools/dotnet').glob('*') if p.is_file()}
    sources.update(reporting)
    if browser:
        bundle = args.browser_bundle.resolve()
        paths = {p for p in bundle.rglob('*') if p.is_file()}
        if not paths: raise ValueError('Missing published Lua browser bundle')
        executable = Path(shutil.which(args.node) or args.node).resolve()
    else:
        if not args.adapter: raise ValueError('Missing native probe command')
        entry = Path(args.adapter[-1]).resolve()
        if not entry.is_file(): raise ValueError('Native command must end in its assembly or apphost')
        paths = {p for p in entry.parent.rglob('*') if p.is_file() and p.suffix != '.pdb'}
        executable = Path(shutil.which(args.adapter[0]) or args.adapter[0]).resolve()
    if executable.is_file(): paths.add(executable)
    artifacts = {str(p): identity.sha(p) for p in sorted(paths)}
    output = args.report.resolve()
    if str(output) in artifacts or output in {ROOT / p for p in sources}: raise ValueError('Report would overwrite an input')
    output.parent.mkdir(parents=True, exist_ok=True)
    if browser:
        command = [str(executable), str(ROOT / 'tools/dotnet/run_automation_browser.cjs'), str(bundle), str(output)]
        subprocess.run(command, check=True, timeout=120)
        report = json.loads(output.read_text(), object_pairs_hook=identity.unique_object)
    else:
        command = args.adapter
        result = subprocess.run(command, check=True, capture_output=True, text=True, timeout=60)
        report = json.loads(result.stdout, object_pairs_hook=identity.unique_object)
    compiled = validate(report)
    if host not in {entry["Assembly"] for entry in report["Build"]}:
        raise ValueError("Adapter reported the wrong execution host")
    if any(identity.sha(ROOT / p) != digest for p, digest in sources.items()) or any(identity.sha(Path(p)) != digest for p, digest in artifacts.items()):
        raise ValueError('Lua sources/artifacts changed during execution')
    report.update(compiledSources=compiled, reportingSources=reporting, invokedArtifacts=artifacts,
                  command=command, machine=platform.platform(), revision=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip())
    output.write_text(json.dumps(report, indent=2) + '\n')
    print(f'Bound {len(compiled)} compiled inputs and {len(artifacts)} artifacts for {len(CASES)} Lua groups.')


if __name__ == '__main__': main()
