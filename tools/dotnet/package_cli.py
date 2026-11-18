#!/usr/bin/env python3
"""Publish a pinned, host-native, self-contained CLI archive (no installed .NET needed).

Run under the SDK in global.json. SQLite is optional and must have been
built on this host with build_sqlite.py. The base package uses only .NET.
"""
import argparse
import base64
import gzip
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET
import zipfile

from write_identity import COMMON, ROOT, sha
from publish_notices import verify as verify_notices


def host_rid():
    system = {'Linux': 'linux', 'Darwin': 'osx', 'Windows': 'win'}.get(platform.system())
    machine = {'x86_64': 'x64', 'amd64': 'x64', 'aarch64': 'arm64', 'arm64': 'arm64'}.get(platform.machine().lower())
    if not system or not machine:
        raise ValueError('Packaging requires native Windows, macOS or glibc Linux on x64/ARM64')
    return system + '-' + machine


def inventory(directory):
    return {p.relative_to(directory).as_posix(): sha(p) for p in sorted(directory.rglob('*')) if p.is_file()}


def sources():
    paths = {ROOT / p for p in COMMON}
    paths.update(p for folder in ('src', 'tests', 'native', 'packaging', 'solutions') for p in (ROOT / folder).rglob('*') if p.is_file()
                 and not {'bin', 'obj', '__pycache__', 'TestResults'} & set(p.relative_to(ROOT).parts))
    paths.update([Path(__file__).resolve(), ROOT / 'tools/dotnet/build_sqlite.py', ROOT / 'tools/dotnet/publish_notices.py', ROOT / 'LICENSE'])
    return {p.relative_to(ROOT).as_posix(): sha(p) for p in sorted(paths)}


def archive_tree(directory, archive):
    """Stable ordering, timestamps, ownership and modes; native compiler bytes may vary."""
    if archive.suffix == '.zip':
        with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as bundle:
            for path in sorted(directory.rglob('*')):
                if path.is_file():
                    info = zipfile.ZipInfo(directory.name + '/' + path.relative_to(directory).as_posix(), (1980, 1, 1, 0, 0, 0))
                    info.compress_type = zipfile.ZIP_DEFLATED
                    info.create_system = 3
                    info.external_attr = (0o100755 if path.stat().st_mode & 0o111 else 0o100644) << 16
                    bundle.writestr(info, path.read_bytes())
    else:
        with archive.open('wb') as output, gzip.GzipFile(filename='', fileobj=output, mode='wb', mtime=0) as compressed:
            with tarfile.open(fileobj=compressed, mode='w') as bundle:
                for path in sorted(directory.rglob('*')):
                    if path.is_file():
                        info = bundle.gettarinfo(str(path), directory.name + '/' + path.relative_to(directory).as_posix())
                        info.uid = info.gid = info.mtime = 0
                        info.uname = info.gname = ''
                        info.mode = 0o755 if path.stat().st_mode & 0o111 else 0o644
                        with path.open('rb') as source:
                            bundle.addfile(info, source)


def verify_package(directory):
    manifest = json.loads((directory / 'PACKAGE.json').read_text())
    actual = inventory(directory)
    del actual['PACKAGE.json']
    if manifest['files'] != actual:
        raise ValueError('Packaged files differ from the manifest')
    config = json.loads((directory / (manifest['command'] + '.runtimeconfig.json')).read_text())['runtimeOptions']
    if 'framework' in config or 'frameworks' in config or not config.get('includedFrameworks'):
        raise ValueError('Package is not self-contained')
    verify_notices(directory)
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', default='dotnet')
    parser.add_argument('--rid', help='Defaults to the actual host; cross-native packaging is rejected')
    parser.add_argument('--sqlite', action='store_true')
    parser.add_argument('--output', type=Path, default=ROOT / 'build/packages')
    args = parser.parse_args()
    rid = args.rid or host_rid()
    if rid != host_rid():
        raise SystemExit('Package on the target OS/architecture; requested RID differs from this host')
    dotnet = shutil.which(args.dotnet)
    if not dotnet:
        raise SystemExit('Pinned dotnet SDK not found')
    sdk = json.loads((ROOT / 'global.json').read_text())['sdk']['version']
    version = subprocess.check_output([dotnet, '--version'], cwd=ROOT, text=True).strip()
    if version != sdk:
        raise SystemExit('Wrong SDK: ' + version)
    runtime = ET.parse(ROOT / 'Directory.Build.props').findtext('PropertyGroup/RuntimeFrameworkVersion')
    command = 'oc2-sqlite' if args.sqlite else 'oc2'
    project = 'OpenCiv2.Cli.Sqlite' if args.sqlite else 'OpenCiv2.Cli'
    name = command + '-' + rid
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    archive = output / (name + ('.zip' if rid.startswith('win-') else '.tar.gz'))
    if archive.exists() or archive.with_name(archive.name + '.sha256').exists():
        raise SystemExit('Refusing to overwrite an existing package or checksum: ' + str(archive))
    before = sources()
    project_file = ROOT / 'src' / project / (project + '.csproj')
    properties = ['-p:Oc2PackageRid=' + rid]
    subprocess.run([dotnet, 'restore', str(project_file), '--locked-mode', *properties], cwd=ROOT, check=True)
    # Framework packs are implicit downloads, absent from the project lock file.
    assets = json.loads((project_file.parent / 'obj/project.assets.json').read_text())
    folders = [Path(p) for p in assets['packageFolders']]
    package_id = 'microsoft.netcore.app.runtime.' + rid
    pack = next((p / package_id / runtime for p in folders if (p / package_id / runtime).is_dir()), None)
    if pack is None:
        raise ValueError('Restored runtime package missing')
    runtime_archive = pack / (package_id + '.' + runtime + '.nupkg')
    digest = base64.b64encode(hashlib.sha512(runtime_archive.read_bytes()).digest()).decode()
    pins = json.loads((ROOT / 'packaging/runtime-packs.json').read_text())
    if pins['version'] != runtime or pins['sha512'].get(rid) != digest:
        raise ValueError('Runtime package bytes differ from the checked-in pin')
    with tempfile.TemporaryDirectory(prefix='oc2-package-', dir=output) as temporary:
        directory = Path(temporary) / name
        subprocess.run([dotnet, 'publish', str(project_file), '-c', 'Release', '--no-restore',
                        '-m:1', '-nr:false', *properties, '-o', str(directory)], cwd=ROOT, check=True)
        # Check published runtime bytes against the pinned archive, including
        # native binaries. An altered extracted NuGet cache must not bypass the pin.
        with zipfile.ZipFile(runtime_archive) as runtime_bundle:
            for entry in runtime_bundle.namelist():
                if entry.startswith('runtimes/' + rid + '/') and not entry.endswith('/'):
                    shipped = directory / Path(entry).name
                    if shipped.is_file() and shipped.read_bytes() != runtime_bundle.read(entry):
                        raise ValueError('Published runtime differs from pinned archive: ' + entry)
        for pdb in directory.glob('*.pdb'):
            pdb.unlink()
        notices = directory / 'notices'
        notices.mkdir()
        shutil.copyfile(ROOT / 'LICENSE', directory / 'LICENSE.txt')
        shutil.copyfile(pack / 'LICENSE.TXT', notices / 'dotnet-LICENSE.txt')
        shutil.copyfile(pack / 'THIRD-PARTY-NOTICES.TXT', notices / 'dotnet-THIRD-PARTY-NOTICES.txt')
        shutil.copyfile(ROOT / 'packaging/notices/RandN-LICENSE.txt', notices / 'RandN-LICENSE.txt')
        dependencies = {'dotnetRuntime': {'version': runtime, 'package': package_id, 'sha512': digest},
                        'randN': {'version': '0.5.0', 'licenseSource': 'https://github.com/ociaw/RandN/blob/v0.5.0/LICENSE.txt'}}
        shutil.copyfile(ROOT / 'packaging/locks' / rid / (project + '.json'), notices / 'packages.lock.json')
        if args.sqlite:
            dependencies['sqlite'] = json.loads((directory / 'oc2-sqlite-build.json').read_text())
        (directory / 'README.txt').write_text(
            'OpenCiv2 finite M1 command-line tools\n\n'
            f'Run {command}{".exe" if rid.startswith("win-") else ""} --help\n'
            'Includes the .NET runtime; no .NET installation is required.\n'
            'Requires the operating system libraries supported by .NET 10 (including ICU).\n'
            'This is an unsigned development package, not a full playable game.\n'
            'Input game/content files are supplied separately.\n'
            'LICENSE.txt and notices/ contain license terms; SQLite packages also include SQLITE-NOTICE.txt.\n', encoding='utf-8')
        if sources() != before:
            raise ValueError('Build inputs changed during packaging; discard and retry')
        manifest = {'format': 'openciv2-cli-package/1', 'rid': rid, 'command': command,
                    'sdk': sdk, 'runtime': runtime, 'selfContained': True,
                    'sources': before, 'dependencies': dependencies, 'files': inventory(directory)}
        (directory / 'PACKAGE.json').write_text(json.dumps(manifest, indent=2, sort_keys=True) + '\n', encoding='utf-8')
        verify_package(directory)
        staged = Path(temporary) / archive.name
        archive_tree(directory, staged)
        # Atomic no-clobber publication, including against a competing packager.
        os.link(staged, archive)
    archive.with_name(archive.name + '.sha256').write_text(sha(archive) + '  ' + archive.name + '\n', encoding='ascii')
    print('Packaged: ' + str(archive))


if __name__ == '__main__':
    main()
