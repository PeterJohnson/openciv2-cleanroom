#!/usr/bin/env python3
"""Extract and exercise the actual CLI archive with installed-runtime lookup disabled."""
import argparse
import importlib
import json
import os
from pathlib import Path
import platform
import subprocess
import sys
import tarfile
import tempfile
import unittest
import zipfile

from package_cli import host_rid, verify_package
from write_identity import ROOT, sha


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('archive', type=Path)
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    archive = args.archive.resolve()
    expected = archive.with_name(archive.name + '.sha256').read_text().split()[0]
    if sha(archive) != expected:
        raise ValueError('Archive checksum differs')
    with tempfile.TemporaryDirectory(prefix='oc2 packaged café ') as temporary:
        root = Path(temporary)
        if archive.suffix == '.zip':
            with zipfile.ZipFile(archive) as bundle:
                for name in bundle.namelist():
                    if '\\' in name or Path(name).is_absolute() or '..' in Path(name).parts:
                        raise ValueError('Unsafe archive member')
                bundle.extractall(root)
        else:
            with tarfile.open(archive) as bundle:
                bundle.extractall(root, filter='data')
        directories = list(root.iterdir())
        if len(directories) != 1 or not directories[0].is_dir():
            raise ValueError('Expected one package root')
        directory = directories[0]
        manifest = verify_package(directory)
        if manifest['rid'] != host_rid():
            raise ValueError('Package is for another host')
        executable = directory / (manifest['command'] + ('.exe' if os.name == 'nt' else ''))
        # The .NET 10 host trace must show this extracted coreclr, rather than an
        # installed runtime. Empty roots disable environment-based discovery too.
        empty = root / 'no-installed-dotnet'
        empty.mkdir()
        for key in ('DOTNET_ROOT', 'DOTNET_ROOT_X64', 'DOTNET_ROOT_ARM64', 'DOTNET_ROOT_X86', 'DOTNET_ROOT(x86)'):
            os.environ[key] = str(empty)
        os.environ['DOTNET_MULTILEVEL_LOOKUP'] = '0'
        os.environ['DOTNET_ROLL_FORWARD'] = 'Disable'
        os.environ.pop('DOTNET_ADDITIONAL_DEPS', None)
        os.environ.pop('DOTNET_SHARED_STORE', None)
        trace = root / 'host.trace'
        environment = dict(os.environ, DOTNET_HOST_TRACE='1', DOTNET_HOST_TRACEFILE=str(trace), DOTNET_HOST_TRACE_VERBOSITY='4')
        subprocess.run([str(executable), '--help'], env=environment, check=True, capture_output=True, timeout=30)
        coreclr = {'win': 'coreclr.dll', 'osx': 'libcoreclr.dylib', 'linux': 'libcoreclr.so'}[manifest['rid'].split('-')[0]]
        text = trace.read_text(encoding='utf-8')
        if str(directory / coreclr).lower() not in text.lower():
            raise ValueError('Host trace does not identify the packaged runtime')
        # Exercise full existing authored CLI suites. The roundtrip helper is a
        # test-only executable, absent from this product package; omit that case.
        sys.path.insert(0, str(ROOT / 'tests'))
        sys.argv = [sys.argv[0]]
        inspection = importlib.import_module('inspection_cli')
        inspection.DRIVER = executable
        suite = unittest.TestSuite(test for test in unittest.defaultTestLoader.loadTestsFromTestCase(inspection.InspectionCli)
                                   if test._testMethodName != 'test_roundtrip_output_preserves_input_and_refuses_existing_paths')
        modules = ['content_cli', 'content_pack_cli', 'rules_cli']
        if manifest['command'] == 'oc2-sqlite':
            modules.append('sqlite_cli')
        for name in modules:
            sys.argv = [sys.argv[0], str(executable)]
            module = importlib.import_module(name)
            suite.addTests(unittest.defaultTestLoader.loadTestsFromModule(module))
        sys.argv = [sys.argv[0]]
        result = unittest.TextTestRunner(verbosity=2).run(suite)
        # Verify rejection of missing or modified shipped content, then restore.
        notice = directory / 'notices/RandN-LICENSE.txt'
        original = notice.read_bytes()
        rejection_checks = 0
        for corrupt in (True, False):
            if corrupt:
                notice.write_bytes(original + b'changed')
            else:
                notice.unlink()
            try:
                verify_package(directory)
            except ValueError:
                rejection_checks += 1
            else:
                raise ValueError('Manifest accepted changed/missing content')
            finally:
                notice.write_bytes(original)
        verify_package(directory)
        report = {'format': 'openciv2-cli-package-test/1', 'archive': archive.name,
                  'sha256': sha(archive), 'bytes': archive.stat().st_size, 'rid': manifest['rid'],
                  'command': manifest['command'], 'sdk': manifest['sdk'], 'runtime': manifest['runtime'],
                  'testsRun': result.testsRun, 'passed': result.wasSuccessful(),
                  'skipped': [{'test': test.id(), 'reason': reason} for test, reason in result.skipped],
                  'host': {'platform': platform.platform(), 'machine': platform.machine(), 'python': platform.python_version()},
                  'manifestRejectionChecks': rejection_checks, 'packagedCoreclrObserved': True,
                  'installedRuntimeRoots': 'empty', 'packageManifestSha256': sha(directory / 'PACKAGE.json'),
                  'testInputs': {p.relative_to(ROOT).as_posix(): sha(p) for p in
                                [Path(__file__).resolve(), ROOT / 'tools/dotnet/package_cli.py', ROOT / 'tools/dotnet/publish_notices.py',
                                 *[ROOT / 'tests' / (name + '.py') for name in ['inspection_cli', *modules]]]}}
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
        if not result.wasSuccessful():
            raise SystemExit(1)
        print('Passed extracted package: ' + str(archive))


if __name__ == '__main__':
    main()
