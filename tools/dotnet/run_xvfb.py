#!/usr/bin/env python3
"""Run an input test on an isolated Xvfb display; never target the user's desktop."""
import argparse
import os
import selectors
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--xvfb', default='Xvfb'); parser.add_argument('--dpi', type=int, default=96)
    parser.add_argument('command', nargs=argparse.REMAINDER); args = parser.parse_args()
    if not args.command: parser.error('A test command is required')
    read, write = os.pipe()
    try:
        server = subprocess.Popen([args.xvfb, '-displayfd', str(write), '-screen', '0', '2400x1800x24', '-dpi', str(args.dpi), '-nolisten', 'tcp'], pass_fds=(write,), stdout=subprocess.DEVNULL)
    finally: os.close(write)
    try:
        with selectors.DefaultSelector() as selector:
            selector.register(read, selectors.EVENT_READ)
            if not selector.select(timeout=15): raise RuntimeError('Xvfb did not publish a display')
            display = os.read(read, 32).decode().strip()
        if not display.isdecimal() or server.poll() is not None: raise RuntimeError('Xvfb startup failed')
        return subprocess.run(args.command, env={**os.environ, 'DISPLAY': ':' + display}).returncode
    finally:
        os.close(read); server.terminate(); server.wait(timeout=10)


if __name__ == '__main__': raise SystemExit(main())
