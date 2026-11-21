#!/usr/bin/env python3
"""Attach verified dependency notices to a native/browser publication, without downloads.

Includes the complete restored package graph (also build-only and unused platform
assets), not a claim that every package survives trimming. Runtime packs and native
SQLite receipts are recorded separately. Unknown missing licenses fail closed.
"""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[2]
NOTICES = ROOT / 'packaging/notices'
ARCHIVE_PINS = ROOT / 'packaging/package-archives.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def sha512(raw):
    return base64.b64encode(hashlib.sha512(raw).digest()).decode()


def verify_archive(archive, expected, dotnet_root):
    raw_hash = sha512(archive.read_bytes())
    if raw_hash != expected:
        pins = json.loads(ARCHIVE_PINS.read_text())
        if pins.get('format') != 'openciv2-package-archives/1':
            raise ValueError('Unknown NuGet archive pin format')
        pinned = pins['packages'].get(archive.name.lower())
        if pinned is not None:
            # These exact bytes were verified against this lock content hash with
            # the pinned SDK. Recheck both here without platform certificate APIs.
            if pinned != {'contentHash': expected, 'archiveSha512': raw_hash}:
                raise ValueError('NuGet archive differs from reviewed lock/archive pins: ' + str(archive))
            return raw_hash
        # Signed NuGet content hashes exclude signature bookkeeping. Use the
        # SDK to verify unrecorded archives; review their exact hashes before
        # adding them to the portable pins. Never trust cached .sha512 sidecars.
        env = {**os.environ, 'DOTNET_CLI_UI_LANGUAGE': 'en-US',
               'NUGET_CERT_REVOCATION_MODE': 'offline'}
        executable = dotnet_root / ('dotnet.exe' if os.name == 'nt' else 'dotnet')
        result = subprocess.run([str(executable), 'nuget', 'verify', str(archive),
                                 '--all', '--verbosity', 'normal'], env=env,
                                text=True, capture_output=True, timeout=120)
        hashes = re.findall(r'^Content hash: (\S+)\s*$', result.stdout, re.M)
        if result.returncode or hashes != [expected]:
            raise ValueError('NuGet archive does not match lock or failed signature verification: '
                             + str(archive) + f' (verifier exit {result.returncode})\n'
                             + result.stdout + result.stderr)
    return raw_hash


def metadata(bundle):
    names = [n for n in bundle.namelist() if n.endswith('.nuspec')]
    if len(names) != 1:
        raise ValueError('Expected one NuGet manifest')
    root = ET.fromstring(bundle.read(names[0]))
    meta = next(x for x in root if x.tag.split('}')[-1] == 'metadata')
    return {x.tag.split('}')[-1]: {'text': x.text or '', **x.attrib} for x in meta}


def supplements(name, version, meta):
    commit = meta.get('repository', {}).get('commit')
    rules = {
        **{name: ('0.5.8', 'c735770a21407226cfd1204bbe11a25de27cddf9', ['LuaCSharp'])
           for name in ('LuaCSharp', 'LuaCSharp.Annotations', 'LuaCSharp.SourceGenerator')},
        'Avalonia.BuildServices': ('11.3.2', '777f975b0a0cecf0311273711d56697212c558c0', ['BuildServices']),
        'MicroCom.Runtime': ('0.11.6', '76785efcafd91b5902fd19dd11145f6dd655b7b4', ['MicroCom']),
        'Tmds.DBus.Protocol': ('0.94.1', 'b4a7fed0b878f74cb54f7cca84d2889af4e596ba', ['Tmds']),
        'RandN.Core': ('0.5.0', '7f10c5554c974e4d96147c0a08abfa01cd04f3fc', ['RandN']),
        'RandN.Pcg32': ('0.5.0', '7f10c5554c974e4d96147c0a08abfa01cd04f3fc', ['RandN']),
    }
    if (name == 'Avalonia' or name.startswith('Avalonia.')) and name not in ('Avalonia.BuildServices', 'Avalonia.Angle.Windows.Natives'):
        rule = ('12.1.6', 'cf8e564674700348947e93ab19105f27afe859a9', ['Avalonia', 'AvaloniaNotice', 'Apache'])
        if name == 'Avalonia.Fonts.Inter':
            rule = (*rule[:2], [*rule[2], 'Inter'])
    else:
        rule = rules.get(name)
    if rule is None:
        return []
    if (version, commit) != rule[:2]:
        raise ValueError('Review upstream notice attribution for changed dependency: ' + name)
    return rule[2]


def generate(project, assets_path, lock_path, output, dotnet_root, sdk, runtime, rid, browser, self_contained=False, native_aot=False):
    if sdk != json.loads((ROOT / 'global.json').read_text())['sdk']['version']:
        raise ValueError('Unpinned publication SDK')
    if native_aot and (browser or not self_contained or not rid):
        raise ValueError('Native AOT publication requires a self-contained desktop RID')
    pins_path = ROOT / ('packaging/aot-runtime-packs.json' if native_aot else 'packaging/runtime-packs.json')
    pins = json.loads(pins_path.read_text())
    if runtime != pins['version']:
        raise ValueError('Unpinned publication runtime')
    assets = json.loads(assets_path.read_text())
    lock = json.loads(lock_path.read_text())
    locked = {(name.lower(), row['resolved']): row['contentHash']
              for target in lock['dependencies'].values() for name, row in target.items() if 'contentHash' in row}
    sources_path = NOTICES / 'sources.json'
    sources = json.loads(sources_path.read_text())
    payload = {}
    def add(name, raw):
        if name in payload and payload[name] != raw:
            raise ValueError('Notice filename collision: ' + name)
        payload[name] = raw
    def vendor(key):
        row = sources[key]; raw = (NOTICES / row['file']).read_bytes()
        if sha(raw) != row['sha256']:
            raise ValueError('Vendored notice changed: ' + key)
        name = 'upstream/' + row['file']; add(name, raw)
        return name
    packages = []
    for key, library in sorted(assets['libraries'].items()):
        if library['type'] != 'package':
            continue
        name, version = key.rsplit('/', 1)
        expected = locked.get((name.lower(), version))
        if not expected or expected != library['sha512']:
            raise ValueError('Restored dependency does not match lock: ' + key)
        archive = next((Path(folder) / library['path'] / (name.lower() + '.' + version + '.nupkg')
                        for folder in assets['packageFolders']
                        if (Path(folder) / library['path'] / (name.lower() + '.' + version + '.nupkg')).is_file()), None)
        if archive is None:
            raise ValueError('NuGet archive is missing: ' + key)
        archive_hash = verify_archive(archive, expected, dotnet_root)
        with zipfile.ZipFile(archive) as bundle:
            meta = metadata(bundle)
            files = []
            for entry in bundle.namelist():
                if re.search(r'(license|licence|third.party.notices|copying|ofl)', Path(entry).name, re.I) and not entry.endswith('/'):
                    destination = name + '/' + entry.replace('/', '_')
                    add(destination, bundle.read(entry)); files.append(destination)
            for supplement in supplements(name, version, meta):
                files.append(vendor(supplement))
            if name in ('Microsoft.NET.ILLink.Tasks', 'Microsoft.NET.Sdk.WebAssembly.Pack', 'Microsoft.DotNet.ILCompiler') or re.fullmatch(r'runtime\.[a-z0-9-]+\.Microsoft\.DotNet\.ILCompiler', name):
                if version != runtime:
                    raise ValueError('Unpinned SDK dependency')
                add('dotnet/SDK-LICENSE.txt', (dotnet_root / 'LICENSE.txt').read_bytes())
                files.append('dotnet/SDK-LICENSE.txt')
            if not any(re.search(r'(license|licence|copying|ofl)', Path(f).name, re.I) for f in files):
                raise ValueError('Missing full license; review package: ' + key)
            roles = sorted({role for target in assets['targets'].values() for role in target.get(key, {})
                            if role not in ('type', 'dependencies')})
            packages.append({'id': name, 'version': version, 'contentHash': expected, 'archiveSha512': archive_hash,
                             'declaredLicense': meta.get('license', {}), 'repository': meta.get('repository', {}),
                             'restoredAssetKinds': roles, 'notices': sorted(set(files))})
    runtime_info = {'version': runtime, 'redistributed': bool(browser or self_contained),
                    'kind': 'native-aot' if native_aot else ('mono' if browser else 'coreclr')}
    if browser:
        pack = dotnet_root / 'packs/Microsoft.NETCore.App.Runtime.Mono.browser-wasm' / runtime
        archive = pack / 'Microsoft.NETCore.App.Runtime.Mono.browser-wasm.nupkg'
        runtime_key = 'browser-wasm'
    elif self_contained:
        if not rid:
            raise ValueError('Self-contained publication requires a RID')
        package = ('microsoft.netcore.app.runtime.nativeaot.' if native_aot else 'microsoft.netcore.app.runtime.') + rid
        pack = next((Path(folder) / package / runtime for folder in assets['packageFolders']
                     if (Path(folder) / package / runtime).is_dir()), None)
        if pack is None:
            raise ValueError('Native runtime pack missing')
        archive = pack / (package + '.' + runtime + '.nupkg')
        runtime_key = rid
    else:
        pack = None
    if pack is not None:
        digest = sha512(archive.read_bytes())
        if digest != pins['sha512'].get(runtime_key):
            raise ValueError('Runtime archive differs from pin')
        # Read notices from verified archive bytes, not an unverified extracted cache.
        with zipfile.ZipFile(archive) as bundle:
            for name in ('LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT'):
                add('dotnet/' + name, bundle.read(name))
        runtime_info.update(pack=runtime_key, sha512=digest)
    else:
        # Framework-dependent apphosts contain SDK host code, but do not ship CoreCLR.
        for name in ('LICENSE.txt', 'ThirdPartyNotices.txt'):
            add('dotnet/SDK-' + name, (dotnet_root / name).read_bytes())
        runtime_info['note'] = 'Framework-dependent host; runtime installed separately. SDK host notices included.'
    native = {}
    for kind, notice, receipt in [('sqlite', 'SQLITE-NOTICE.txt', 'oc2-sqlite-build.json'),
                                 ('miniaudio', 'MINIAUDIO-LICENSE.txt', 'oc2-audio-build.json')]:
        if (output / notice).is_file():
            add(kind + '/' + notice, (output / notice).read_bytes())
            native[kind] = {'noticeSha256': sha((output / notice).read_bytes())}
            if (output / receipt).is_file():
                native[kind]['build'] = json.loads((output / receipt).read_text())
    add('OpenCiv2-LICENSE.txt', (ROOT / 'LICENSE').read_bytes())
    report = {'format': 'openciv2-published-dependencies/1', 'project': project.stem, 'sdk': sdk,
              'scope': 'Complete locked restored graph; may include build-only and unselected platform assets, not a trimming reachability claim.',
              'projectSha256': sha(project.read_bytes()), 'lockSha256': sha(lock_path.read_bytes()),
              'collectorSha256': sha(Path(__file__).read_bytes()), 'noticeSourcesSha256': sha(sources_path.read_bytes()),
              'packageArchivePinsSha256': sha(ARCHIVE_PINS.read_bytes()),
              'runtimePinsSha256': sha(pins_path.read_bytes()), 'upstreamNoticeSources': sources,
              'packages': packages, 'runtime': runtime_info, 'native': native,
              'files': {name: sha(raw) for name, raw in sorted(payload.items())}}
    output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='oc2-notices-', dir=output) as temporary:
        staging = Path(temporary)
        for name, raw in payload.items():
            path = staging / name; path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(raw)
        destination = output / 'DEPENDENCY-NOTICES'
        if destination.exists():
            shutil.rmtree(destination)
        staging.rename(destination)
    (output / 'DEPENDENCIES.json').write_text(json.dumps(report, indent=2, sort_keys=True) + '\n', encoding='utf-8')
    verify(output)
    print(f'Published notices for {len(packages)} locked packages and {len(payload)} files: {output}')


def verify(output):
    report = json.loads((output / 'DEPENDENCIES.json').read_text())
    actual = {p.relative_to(output / 'DEPENDENCY-NOTICES').as_posix(): sha(p.read_bytes())
              for p in (output / 'DEPENDENCY-NOTICES').rglob('*') if p.is_file()}
    if report['files'] != actual:
        raise ValueError('Published notices are missing or changed')
    for package in report['packages']:
        if not package['notices'] or not set(package['notices']) <= actual.keys():
            raise ValueError('Package notice inventory is incomplete')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--assets', type=Path, required=True)
    parser.add_argument('--lock', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--dotnet-root', type=Path, required=True)
    parser.add_argument('--sdk', required=True); parser.add_argument('--runtime', required=True)
    parser.add_argument('--self-contained', action='store_true')
    parser.add_argument('--native-aot', action='store_true')
    parser.add_argument('--rid', default=''); parser.add_argument('--browser', action='store_true')
    args = parser.parse_args()
    generate(args.project, args.assets, args.lock, args.output, args.dotnet_root, args.sdk, args.runtime, args.rid, args.browser, args.self_contained, args.native_aot)
