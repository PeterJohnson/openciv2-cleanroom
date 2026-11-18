#!/usr/bin/env python3
"""Prepare verified stock Lua C++ wrappers for the pinned .NET browser linker.

Lua supports C++ exceptions for protected calls. This avoids the pinned LLVM /
Emscripten setjmp helper mismatch; only the interpreter and bridge use C++.
"""
import argparse
import hashlib
import json
from pathlib import Path
import tarfile
import urllib.request
from build_lua import ROOT, BRIDGE, inputs, sha


def prepare(output, archive, verify):
    pin = json.loads((ROOT / 'dependencies.json').read_text())['libraries']['Lua']
    output.mkdir(parents=True, exist_ok=True)
    cached = output / Path(pin['url']).name
    archive = archive or cached
    if not archive.exists():
        if verify or archive != cached:
            raise ValueError('Pinned Lua archive is missing')
        with urllib.request.urlopen(pin['url'], timeout=60) as response:
            archive.write_bytes(response.read())
    if sha(archive) != pin['sha256']:
        raise ValueError('Lua archive does not match the pinned SHA-256')
    expected = {}
    prefix = 'lua-' + pin['version'] + '/src/'
    with tarfile.open(archive) as bundle:
        for member in bundle.getmembers():
            if member.isfile() and member.name.startswith(prefix):
                name = member.name[len(prefix):]
                if '/' not in name and name.endswith(('.c', '.h')) and name not in {'lua.c', 'luac.c', 'onelua.c'}:
                    expected['source/' + name] = bundle.extractfile(member).read()
    if 'source/lua.h' not in expected:
        raise ValueError('Pinned source archive is incomplete')
    expected['source/oc2_lua.c'] = (BRIDGE / 'bridge.c').read_bytes()
    expected['source/bridge.h'] = (BRIDGE / 'bridge.h').read_bytes()
    # A header-only change must invalidate native compilation too, even when the
    # SDK's incremental dependency scan only tracks the wrapper translation units.
    source_set = hashlib.sha256(b''.join(name.encode() + b'\0' + hashlib.sha256(data).digest() for name, data in sorted(expected.items()))).hexdigest()
    for name, data in list(expected.items()):
        if name.endswith('.c'):
            expected['cpp/' + Path(name).stem + '.cpp'] = (
                '// Source set SHA-256: ' + source_set + '\n// SHA-256: ' + hashlib.sha256(data).hexdigest() + '\n#include "../' + name + '"\n').encode()
    expected['LUA-NOTICE.txt'] = expected['source/lua.h']
    report = {'format': 'openciv2-lua-wasm-sources/1', 'version': pin['version'], 'abi': 1,
              'archiveSha256': pin['sha256'], 'mode': 'stock-lua-cpp-exceptions',
              'inputs': {**inputs(), str(Path(__file__).resolve().relative_to(ROOT)): sha(Path(__file__))},
              'files': {name: hashlib.sha256(data).hexdigest() for name, data in sorted(expected.items())}}
    actual = {p.relative_to(output).as_posix() for folder in ('source', 'cpp') for p in (output / folder).rglob('*') if p.is_file()}
    if actual - expected.keys():
        raise ValueError('Unexpected Lua build inputs; use a clean output directory')
    receipt = output / 'oc2-lua-wasm.json'
    if verify:
        if json.loads(receipt.read_text()) != report or any(not (output / name).is_file() or sha(output / name) != digest for name, digest in report['files'].items()):
            raise ValueError('Prepared Lua inputs/receipt are stale or modified')
    else:
        for name, data in expected.items():
            path = output / name
            path.parent.mkdir(parents=True, exist_ok=True)
            if not path.exists() or path.read_bytes() != data:
                path.write_bytes(data)
        if archive != cached:
            cached.write_bytes(archive.read_bytes())
        receipt.write_text(json.dumps(report, indent=2) + '\n')
    print(('Verified' if verify else 'Prepared') + ' pinned Lua WASM sources: ' + str(output))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path)
    parser.add_argument('--output', type=Path, default=ROOT / 'build/lua/wasm')
    parser.add_argument('--verify', action='store_true')
    args = parser.parse_args()
    prepare(args.output.resolve(), args.archive.resolve() if args.archive else None, args.verify)


if __name__ == '__main__':
    main()
