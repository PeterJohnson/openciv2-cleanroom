#!/usr/bin/env python3
"""Build/verify pinned miniaudio output; no system audio headers or SDL required.

Windows: run in a native Developer Command Prompt. --archive supports offline
builds. --test enables offline render exports and must never be distributed.
"""
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tarfile
import tempfile
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
SHIM = ROOT / 'native/audio/oc2_audio.c'
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path)
    parser.add_argument('--output', type=Path, default=ROOT / 'build/audio/native')
    parser.add_argument('--verify', action='store_true')
    parser.add_argument('--test', action='store_true')
    parser.add_argument('--cc', default=os.environ.get('CC', 'cl' if os.name == 'nt' else 'cc'))
    args = parser.parse_args()
    pin = json.loads((ROOT / 'dependencies.json').read_text())['libraries']['miniaudio']
    system = platform.system()
    filename = {'Windows': 'oc2_audio.dll', 'Darwin': 'liboc2_audio.dylib', 'Linux': 'liboc2_audio.so'}[system]
    output = args.output.resolve(); output.mkdir(parents=True, exist_ok=True)
    library = output / filename
    expected = dict(format='openciv2-audio-build/1', version=pin['version'], archiveSha256=pin['sha256'],
                    shimSha256=sha(SHIM), builderSha256=sha(Path(__file__)), library=filename,
                    platform=system, machine=platform.machine(), testOnly=args.test)
    if args.verify:
        receipt = json.loads((output / 'oc2-audio-build.json').read_text())
        if any(receipt.get(k) != v for k, v in expected.items()) or receipt['librarySha256'] != sha(library):
            raise ValueError('Audio build receipt/source/library mismatch')
        if receipt['noticeSha256'] != sha(output / 'MINIAUDIO-LICENSE.txt'): raise ValueError('Audio license mismatch')
    else:
        archive = args.archive or output / 'miniaudio.tar.gz'
        if not archive.exists():
            if args.archive: raise ValueError('Offline archive does not exist')
            with urllib.request.urlopen(pin['url'], timeout=60) as response: archive.write_bytes(response.read())
        if sha(archive) != pin['sha256']: raise ValueError('miniaudio archive hash differs from dependency pin')
        compiler = shutil.which(args.cc)
        if not compiler: raise ValueError('Native compiler not found: ' + args.cc)
        with tempfile.TemporaryDirectory(prefix='audio-build-', dir=output) as directory:
            work = Path(directory)
            with tarfile.open(archive) as bundle:
                for leaf in ('miniaudio.h', 'LICENSE'):
                    member = bundle.getmember('miniaudio-' + pin['version'] + '/' + leaf)
                    (work / leaf).write_bytes(bundle.extractfile(member).read())
            source = work / 'oc2_audio.c'; shutil.copyfile(SHIM, source)
            target = work / filename
            if system == 'Windows':
                command = [compiler, '/nologo', '/O2', '/LD', '/std:c11', '/D_CRT_SECURE_NO_WARNINGS', str(source)]
                if args.test: command += ['/DOC2_AUDIO_TEST']
                command += ['/link', '/OUT:' + str(target)]
                version = subprocess.run([compiler], capture_output=True, text=True)
            else:
                command = [compiler, '-O2', '-fPIC', '-fvisibility=hidden', '-dynamiclib' if system == 'Darwin' else '-shared',
                           str(source), '-o', str(target), '-lm', '-pthread']
                if system == 'Linux': command += ['-ldl']
                if system == 'Darwin': command += ['-Wl,-install_name,@rpath/' + filename]
                if args.test: command += ['-DOC2_AUDIO_TEST']
                version = subprocess.run([compiler, '--version'], capture_output=True, text=True, check=True)
            subprocess.run(command, cwd=work, check=True)
            shutil.copyfile(target, library)
            shutil.copyfile(work / 'LICENSE', output / 'MINIAUDIO-LICENSE.txt')
            receipt = expected | dict(librarySha256=sha(library), headerSha256=sha(work / 'miniaudio.h'),
                noticeSha256=sha(output / 'MINIAUDIO-LICENSE.txt'), compiler=str(compiler),
                compilerVersion=version.stdout + version.stderr, command=command)
        (output / 'oc2-audio-build.json').write_text(json.dumps(receipt, indent=2) + '\n')
    loaded = ctypes.CDLL(str(library)); loaded.oc2_audio_version.restype = ctypes.c_int
    if loaded.oc2_audio_version() != 11026: raise ValueError('Incorrect native audio version')
    if not args.test and hasattr(loaded, 'oc2_audio_open_offline'): raise ValueError('Test exports in production audio')
    print('Verified pinned miniaudio: ' + str(library))
if __name__ == '__main__': main()
