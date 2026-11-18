#!/usr/bin/env python3
"""Regression checks for CLI resource collection; these do not time gameplay."""
import os
from pathlib import Path
import platform
import sys
import tempfile
import unittest

from measure_cli import invoke


@unittest.skipUnless(platform.system() == 'Linux' and Path('/usr/bin/time').is_file(), 'Linux GNU time collector')
class ProcessResources(unittest.TestCase):
    def test_python_parent_memory_is_not_reported_as_child_peak(self):
        retained = bytearray(128 * 1024 * 1024)
        with tempfile.TemporaryDirectory() as directory:
            sample, output = invoke([sys.executable, '-c', 'data=bytearray(16*1024*1024); print(len(data))'], Path(directory), os.environ)
            self.assertEqual(sample['exitCode'], 0)
            self.assertEqual(output.read_text().strip(), str(16 * 1024 * 1024))
            self.assertGreaterEqual(sample['peakRssMiB'], 16)
            self.assertLess(sample['peakRssMiB'], 64)
            self.assertEqual(len(retained), 128 * 1024 * 1024)

    def test_nonzero_child_status_is_retained_without_time_diagnostics(self):
        with tempfile.TemporaryDirectory() as directory:
            sample, output = invoke([sys.executable, '-c', 'print("different"); raise SystemExit(1)'], Path(directory), os.environ)
            self.assertEqual(sample['exitCode'], 1)
            self.assertEqual(sample['stderr'], '')
            self.assertEqual(output.read_text().strip(), 'different')
            self.assertGreater(sample['wallMs'], 0)


if __name__ == '__main__': unittest.main()
