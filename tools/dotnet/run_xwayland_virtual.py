#!/usr/bin/env python3
"""Run a command on a separate GPU-capable KWin virtual Xwayland display."""
import argparse
import os
from pathlib import Path
import shlex
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--screen', default='5600x3300')
    parser.add_argument('--compositor-log', type=Path, required=True)
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if not args.command:
        parser.error('a display command is required')
    try:
        width, height = (int(value) for value in args.screen.split('x'))
    except ValueError:
        parser.error('--screen requires WIDTHxHEIGHT')
    if width <= 0 or height <= 0:
        parser.error('--screen dimensions must be positive')

    args.compositor_log.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='oc2-virtual-xwayland-') as directory:
        root = Path(directory)
        environment = os.environ.copy()
        for inherited in ('DISPLAY', 'WAYLAND_DISPLAY', 'WAYLAND_SOCKET', 'DBUS_SESSION_BUS_ADDRESS',
                          'XAUTHORITY', 'LIBGL_ALWAYS_SOFTWARE'):
            environment.pop(inherited, None)
        for key in ('XDG_RUNTIME_DIR', 'XDG_CONFIG_HOME', 'XDG_CACHE_HOME', 'XDG_DATA_HOME', 'XDG_STATE_HOME'):
            isolated = root / key
            isolated.mkdir(mode=0o700)
            environment[key] = str(isolated)

        # This harness needs a session bus for KWin, without activating the
        # user's desktop services or mounting a document portal in the runtime.
        bus = root / 'session-bus.conf'
        bus.write_text('''<busconfig>
  <type>session</type>
  <listen>unix:tmpdir=/tmp</listen>
  <auth>EXTERNAL</auth>
  <policy context="default">
    <allow send_destination="*"/>
    <allow receive_sender="*"/>
    <allow own="*"/>
  </policy>
</busconfig>
''')

        session = root / 'session.sh'
        status = root / 'exit-code'
        session.write_text(f'#!/bin/sh\n{shlex.join(args.command)}\n'
                           f'result=$?\nprintf "%s\\n" "$result" > {shlex.quote(str(status))}\nexit "$result"\n')
        session.chmod(0o700)
        with args.compositor_log.open('w') as log:
            result = subprocess.run(['dbus-run-session', '--config-file', str(bus), '--', 'kwin_wayland', '--virtual', '--width', str(width),
                                     '--height', str(height), '--socket', 'oc2-virtual', '--xwayland', '--no-lockscreen',
                                     '--no-global-shortcuts', '--no-kactivities', '--exit-with-session', str(session)],
                                    env=environment, stdout=log, stderr=subprocess.STDOUT)
        if not status.exists():
            raise RuntimeError(f'Virtual compositor did not finish the display command. See {args.compositor_log}')
        return int(status.read_text()) or result.returncode


if __name__ == '__main__':
    raise SystemExit(main())
