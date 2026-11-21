#!/usr/bin/env python3
"""Run Linux playback against a private PipeWire/PulseAudio null sink, without using speakers."""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--log', type=Path, required=True)
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if not args.command: parser.error('command required')
    args.log.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='oc2-private-audio-') as directory, args.log.open('w') as log:
        root = Path(directory); env = os.environ.copy()
        env.update(XDG_RUNTIME_DIR=str(root), PIPEWIRE_RUNTIME_DIR=str(root), PIPEWIRE_REMOTE='pipewire-0',
                   PULSE_SERVER='unix:' + str(root / 'pulse/native'), PULSE_RUNTIME_PATH=str(root / 'pulse'))
        for key in ('XDG_CONFIG_HOME', 'XDG_CACHE_HOME', 'XDG_DATA_HOME', 'XDG_STATE_HOME'):
            (root / key).mkdir(); env[key] = str(root / key)
        env['DBUS_SESSION_BUS_ADDRESS'] = 'unix:path=' + str(root / 'no-session-bus')
        # Explicit server address keeps child display isolation from selecting the user's audio server.
        processes = []
        try:
            for command, socket in [(['pipewire'], root / 'pipewire-0'), (['pipewire-pulse'], root / 'pulse/native')]:
                process = subprocess.Popen(command, env=env, stdout=log, stderr=log); processes.append(process)
                deadline = time.monotonic() + 10
                while not socket.exists():
                    if process.poll() is not None or time.monotonic() > deadline: raise RuntimeError('Audio server failed; inspect log')
                    time.sleep(.05)
            policy = subprocess.Popen(['wireplumber', '--profile=policy'], env=env, stdout=log, stderr=log)
            processes.append(policy)
            time.sleep(1)
            subprocess.run(['pactl', 'load-module', 'module-null-sink', 'sink_name=oc2_test', 'rate=48000', 'channels=2'],
                           env=env, stdout=log, stderr=log, check=True)
            env['PULSE_SINK'] = 'oc2_test'
            deadline = time.monotonic() + 10
            while 'oc2_test' not in subprocess.check_output(['pactl', 'list', 'short', 'sinks'], env=env, text=True):
                if time.monotonic() > deadline: raise RuntimeError('Private null sink did not appear')
                time.sleep(.1)
            return subprocess.run(args.command, env=env).returncode
        finally:
            for process in reversed(processes):
                process.terminate()
                try: process.wait(timeout=5)
                except subprocess.TimeoutExpired: process.kill(); process.wait()
if __name__ == '__main__': raise SystemExit(main())
