#!/usr/bin/env python3
"""Linux CLI signal boundary: cancel only after the native exporter is loaded."""
import os
from pathlib import Path
import signal
import sqlite3
import struct
import subprocess
import sys
import tempfile
import time


def main():
    if sys.platform != "linux":
        raise SystemExit("This integration check requires Linux /proc; portable token checks run separately")
    driver = Path(sys.argv[1]).resolve()
    data = struct.pack("<hhhHHhh", 200, 200, 20000, 0, 24, 1, 1) + b"\xff" * 84 + bytes([10, 0, 0, 0, 0, 0]) * 20000
    with tempfile.TemporaryDirectory(prefix="oc2-export-signals-") as temporary:
        root = Path(temporary)
        source = root / "map.mp"
        source.write_bytes(data)
        for requested in (signal.SIGINT, signal.SIGTERM):
            destination = root / (str(requested) + ".sqlite")
            process = subprocess.Popen([str(driver), "export", str(source), "--sqlite", str(destination), "--json"], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            try:
                deadline = time.monotonic() + 15
                while True:
                    if process.poll() is not None:
                        raise RuntimeError("Export ended before signal checkpoint: " + repr(process.communicate()))
                    if "liboc2_sqlite.so" in Path(f"/proc/{process.pid}/maps").read_text():
                        break
                    if time.monotonic() >= deadline:
                        raise RuntimeError("Exporter load checkpoint timed out")
                    time.sleep(0.005)
                process.send_signal(requested)
                output, error = process.communicate(timeout=15)
                if process.returncode != 130 or output or b"cancelled" not in error:
                    raise RuntimeError(f"Wrong cancellation result: {process.returncode} {output!r} {error!r}")
                if destination.exists() or list(root.glob("*.oc2-tmp-*")) or source.read_bytes() != data:
                    raise RuntimeError("Cancellation changed inputs or left output/staging")
            finally:
                if process.poll() is None:
                    process.kill()
                    process.communicate()
        # Failure writing the receipt is after publication, so the completed
        # database remains available and the command returns I/O status 2.
        destination = root / "closed-stdout.sqlite"
        reader, writer = os.pipe()
        os.close(reader)
        try:
            result = subprocess.run([str(driver), "export", str(source), "--sqlite", str(destination), "--json"], stdout=writer, stderr=subprocess.PIPE, timeout=30)
        finally:
            os.close(writer)
        if result.returncode != 2 or b"Output write failed" not in result.stderr or not destination.exists():
            raise RuntimeError("Receipt output failure did not preserve published database")
        with sqlite3.connect(destination) as db:
            if db.execute("PRAGMA integrity_check").fetchall() != [("ok",)]:
                raise RuntimeError("Published database failed integrity check")
        if list(root.glob("*.oc2-tmp-*")) or source.read_bytes() != data:
            raise RuntimeError("Receipt failure changed input or left staging")
    print("SIGINT/SIGTERM cancel before publication; closed stdout returns 2 with a completed database.")


if __name__ == "__main__":
    main()
