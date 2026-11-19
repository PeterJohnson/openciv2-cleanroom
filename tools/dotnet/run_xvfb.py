#!/usr/bin/env python3
"""Run an input test on an isolated Xvfb display; never target the user's desktop."""
import argparse
import os
import selectors
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--xvfb', default='Xvfb'); parser.add_argument('--dpi', type=int, default=96)
    parser.add_argument('--screen', default='2400x1800x24')
    parser.add_argument('--mixed-dpi', action='store_true', help='Configure separate 1× and 2× virtual monitors')
    parser.add_argument('command', nargs=argparse.REMAINDER); args = parser.parse_args()
    if not args.command: parser.error('A test command is required')
    read, write = os.pipe()
    try:
        screen = '3960x1680x24' if args.mixed_dpi else args.screen
        options = [args.xvfb, '-displayfd', str(write), '-screen', '0', screen, '-dpi', str(args.dpi), '-nolisten', 'tcp']
        if args.mixed_dpi:
            options.append('-noreset')
        server = subprocess.Popen(options, pass_fds=(write,), stdout=subprocess.DEVNULL)
    finally: os.close(write)
    try:
        with selectors.DefaultSelector() as selector:
            selector.register(read, selectors.EVENT_READ)
            if not selector.select(timeout=15): raise RuntimeError('Xvfb did not publish a display')
            display = os.read(read, 32).decode().strip()
        if not display.isdecimal() or server.poll() is not None: raise RuntimeError('Xvfb startup failed')
        environment = {**os.environ, 'DISPLAY': ':' + display}
        if args.mixed_dpi:
            for name, geometry, output in [('Left', '1840/487x1680/444+0+0', 'screen'),
                                           ('Right', '2120/280x1680/222+1840+0', 'none')]:
                subprocess.run(['xrandr', '--setmonitor', name, geometry, output], env=environment, check=True)
            environment.pop('AVALONIA_GLOBAL_SCALE_FACTOR', None)
            environment['AVALONIA_SCREEN_SCALE_FACTORS'] = 'Left=1;Right=2'
        return subprocess.run(args.command, env=environment).returncode
    finally:
        os.close(read); server.terminate(); server.wait(timeout=10)


if __name__ == '__main__': raise SystemExit(main())
