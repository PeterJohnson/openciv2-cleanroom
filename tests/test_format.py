"""Exercise formatting against real temporary Git indexes and commit hooks."""

import os
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
FORMATTED = b"int value() {\n    return 1;\n}\n"
UNFORMATTED = b"int  value(){return 1;}\n"


class FormattingTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix="oc2-format-test-")
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.env = {key: value for key, value in os.environ.items() if not key.startswith("GIT_")}
        self.env.update(GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull,
                        GIT_TERMINAL_PROMPT="0")
        for name in (".clang-format", ".gitattributes", "dependencies.json",
                     "tools/format.py", ".githooks/pre-commit"):
            target = self.root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(ROOT / name, target)
        self.write("src/probe.cpp", FORMATTED)
        self.git("init", "-q")
        self.git("config", "user.name", "Formatting Test")
        self.git("config", "user.email", "format-test@example.invalid")
        self.git("config", "core.autocrlf", "false")
        self.git("config", "core.hooksPath", ".githooks")
        self.git("add", ".")
        self.git("commit", "-qm", "Fixture")

    def write(self, name, data):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def run_command(self, *args):
        return subprocess.run(args, cwd=self.root, env=self.env, capture_output=True)

    def git(self, *args):
        result = self.run_command("git", *args)
        self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))
        return result.stdout

    def format(self, *args):
        return self.run_command(sys.executable, "tools/format.py", *args)

    def test_partial_staging_checks_index_without_changing_either_copy(self):
        self.write("src/probe.cpp", UNFORMATTED)
        self.git("add", "src/probe.cpp")
        self.write("src/probe.cpp", FORMATTED)
        self.assertEqual(self.format("--staged").returncode, 1)
        self.assertEqual(self.git("show", ":src/probe.cpp"), UNFORMATTED)
        self.assertEqual((self.root / "src/probe.cpp").read_bytes(), FORMATTED)

        # Stage a different formatted change, leaving a malformed working copy.
        changed = FORMATTED.replace(b"return 1", b"return 2")
        self.write("src/probe.cpp", changed)
        self.git("add", "src/probe.cpp")
        self.write("src/probe.cpp", UNFORMATTED)
        self.assertEqual(self.format("--staged").returncode, 0)
        self.assertEqual(self.format("--check").returncode, 1)
        self.assertEqual(self.git("show", ":src/probe.cpp"), changed)
        self.assertEqual((self.root / "src/probe.cpp").read_bytes(), UNFORMATTED)

    def test_staged_style_changes_check_unchanged_sources(self):
        style = (self.root / ".clang-format").read_bytes().replace(b"IndentWidth: 4", b"IndentWidth: 2")
        self.write(".clang-format", style)
        self.write("src/probe.cpp", FORMATTED.replace(b"return 1", b"return 2"))
        self.git("add", "src/probe.cpp")
        self.assertEqual(self.format("--staged").returncode, 0)
        self.git("restore", "--staged", "src/probe.cpp")
        self.git("add", ".clang-format")
        self.assertEqual(self.format("--staged").returncode, 1)
        self.assertEqual(self.format().returncode, 0)
        self.git("add", "src/probe.cpp")
        self.assertEqual(self.format("--staged").returncode, 0)

    def test_staged_version_pin_is_used(self):
        manifest = json.loads((self.root / "dependencies.json").read_text())
        manifest["tools"]["clang-format"] = "999.0.0"
        self.write("dependencies.json", json.dumps(manifest).encode())
        self.write("src/probe.cpp", FORMATTED.replace(b"return 1", b"return 2"))
        self.git("add", "src/probe.cpp")
        self.assertEqual(self.format("--staged").returncode, 0)
        self.git("add", "dependencies.json")
        result = self.format("--staged")
        self.assertEqual(result.returncode, 1)
        self.assertIn(b"999.0.0 is required", result.stderr)

    def test_untracked_owned_sources_are_formatted_but_other_roots_are_not(self):
        self.write("src/new file.mm", UNFORMATTED)
        self.write("src/module.cppm", UNFORMATTED)
        self.write("build/vendor.cpp", UNFORMATTED)
        self.assertEqual(self.format("--check").returncode, 1)
        self.assertEqual(self.format().returncode, 0)
        self.assertEqual((self.root / "src/new file.mm").read_bytes(), FORMATTED)
        self.assertEqual((self.root / "src/module.cppm").read_bytes(), FORMATTED)
        self.assertEqual((self.root / "build/vendor.cpp").read_bytes(), UNFORMATTED)
        self.assertEqual(self.format("--check").returncode, 0)

    def test_rename_with_spaces_and_deletion(self):
        self.git("mv", "src/probe.cpp", "src/renamed file.cpp")
        self.write("src/renamed file.cpp", UNFORMATTED)
        self.git("add", "src/renamed file.cpp")
        result = self.format("--staged")
        self.assertEqual(result.returncode, 1)
        self.assertIn(b"src/renamed file.cpp", result.stdout)
        self.git("rm", "-f", "src/renamed file.cpp")
        self.assertEqual(self.format("--staged", "--clang-format", "missing-formatter").returncode, 0)

    def test_wrong_or_missing_formatter_fails(self):
        for executable in (sys.executable, "missing-formatter"):
            with self.subTest(executable=executable):
                result = self.format("--check", "--clang-format", executable)
                self.assertEqual(result.returncode, 1)
                self.assertIn(b"is required", result.stderr)

    def test_commit_hook_rejects_then_accepts_formatted_index(self):
        self.write("src/probe.cpp", UNFORMATTED.replace(b"return 1", b"return 2"))
        self.git("add", "src/probe.cpp")
        self.assertNotEqual(self.run_command("git", "commit", "-qm", "Bad").returncode, 0)
        self.assertEqual(self.git("log", "-1", "--format=%s").strip(), b"Fixture")
        self.assertEqual(self.format().returncode, 0)
        # Formatting does not restage the file: the index is still rejected.
        self.assertEqual(self.format("--staged").returncode, 1)
        self.git("add", "src/probe.cpp")
        self.git("commit", "-qm", "Formatted")


if __name__ == "__main__":
    unittest.main()
