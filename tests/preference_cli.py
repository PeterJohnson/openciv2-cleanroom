"""F03/F06 preference driver integration with independently authored data."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

DRIVER = Path(sys.argv.pop(1)).resolve()


class PreferenceDriverTests(unittest.TestCase):
    def run_driver(self, *paths):
        return subprocess.run([str(DRIVER), *(str(p) for p in paths)],
                              capture_output=True, text=True, encoding="utf-8")

    def test_complete_items_and_ignored_trailing_bytes(self):
        with tempfile.TemporaryDirectory(prefix="oc2-preferences-") as temporary:
            root = Path(temporary)
            source, output = root / "MiXeD.DaT", root / "out.dat"
            data = bytes((i * 53 + 17) % 256 for i in range(516))
            source.write_bytes(data + b"ignored trailing bytes")
            run = self.run_driver(source, output)
            self.assertEqual(run.returncode, 0, run.stderr)
            report = json.loads(run.stdout)
            self.assertEqual(report, {
                "ordinaryBytes": 72, "multiplayerBytes": 444, "trailingBytes": 22,
                "options": int.from_bytes(data[14:18], "little"),
            })
            self.assertEqual(source.read_bytes(), data + b"ignored trailing bytes")
            self.assertEqual(output.read_bytes(), data)
            again = self.run_driver(output)
            self.assertEqual(again.returncode, 0, again.stderr)
            self.assertEqual(json.loads(again.stdout)["trailingBytes"], 0)

    def test_short_input_is_not_misreported_as_an_original_roundtrip(self):
        with tempfile.TemporaryDirectory(prefix="oc2-preferences-") as temporary:
            root = Path(temporary)
            source, output = root / "short.dat", root / "out.dat"
            for length in (0, 71, 72, 515):
                with self.subTest(length=length):
                    source.write_bytes(bytes(length))
                    run = self.run_driver(source, output)
                    self.assertNotEqual(run.returncode, 0)
                    self.assertFalse(output.exists())
                    self.assertEqual(source.read_bytes(), bytes(length))

    def test_existing_output_is_preserved(self):
        with tempfile.TemporaryDirectory(prefix="oc2-preferences-") as temporary:
            root = Path(temporary)
            source, output = root / "source.dat", root / "existing.dat"
            source.write_bytes(bytes(516))
            output.write_bytes(b"retained output")
            run = self.run_driver(source, output)
            self.assertNotEqual(run.returncode, 0)
            self.assertEqual(output.read_bytes(), b"retained output")
            self.assertEqual(source.read_bytes(), bytes(516))


if __name__ == "__main__":
    unittest.main()
