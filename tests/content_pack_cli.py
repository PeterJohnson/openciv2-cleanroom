"""Explicit mount/overlay receipt checks; resolution does not validate component bodies."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

DRIVER = Path(sys.argv.pop(1)).resolve()
ROOT = Path(__file__).resolve().parents[1]
LOCALIZED = ("text", "encyclopedia", "menus", "documents", "council")


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def write(path, value):
    raw = json.dumps(value, ensure_ascii=False).encode("utf-8")
    path.write_bytes(raw)
    return {"path": path.name, "sha256": sha(raw)}


class PackCliTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.temp_path = Path(self.temp.name)
        self.root = self.temp_path / "parent café"
        shutil.copytree(ROOT / "docs/content/examples", self.root)
        self.path = self.root / "content.json"
        self.manifest = json.loads(self.path.read_bytes())
        self.overlay = self.temp_path / "overlay"
        self.overlay.mkdir()
        self.child = {"format": "openciv2-content-pack/1", "id": "authored.overlay", "revision": 1,
                      "profile": "classic-mge-5.4.0f", "defaultLocale": "en", "files": {},
                      "base": {"id": self.manifest["id"], "revision": self.manifest["revision"],
                               "sha256": sha(self.path.read_bytes())},
                      "provenance": {"authors": ["Contract tests"], "license": "MIT", "notes": []}}
        self.child_path = self.overlay / "content.json"
        write(self.child_path, self.child)

    def invoke(self, path=None, *options, code=0):
        result = subprocess.run([str(DRIVER), "content", "resolve", str(path or self.path),
                                 "--json", *map(str, options)], cwd=self.temp_path,
                                capture_output=True, text=True, encoding="utf-8", timeout=10)
        self.assertEqual(result.returncode, code, result.stdout + result.stderr)
        return json.loads(result.stdout)

    def overlay_run(self, *options, code=0):
        write(self.child_path, self.child)
        return self.invoke(self.child_path, "--parent", self.path, *options, code=code)

    def test_example_reports_bounded_scope_and_exact_receipts_without_writing_sources(self):
        before = {p.name: p.read_bytes() for p in self.root.iterdir() if p.is_file()}
        report = self.invoke(None, "--require-classic")
        self.assertEqual(report["status"], "resolved")
        self.assertEqual(report["scope"], "pack-selection-and-gameplay")
        self.assertTrue(report["limitations"])
        self.assertTrue(report["gameplay"]["builtin"])
        self.assertTrue(report["gameplay"]["classicValues"])
        self.assertEqual(report["manifests"][0]["sha256"], sha(self.path.read_bytes()))
        self.assertEqual(report["manifests"][0]["provenance"], self.manifest["provenance"])
        for file in report["files"]:
            if not file.get("externalWorld"):
                self.assertEqual(file["sha256"], sha((self.root / file["path"]).read_bytes()))
                self.assertFalse(Path(file["path"]).is_absolute())
        self.assertEqual(before, {p.name: p.read_bytes() for p in self.root.iterdir() if p.is_file()})

    def test_overlay_uses_separate_origin_and_never_opens_replaced_sources(self):
        self.child["files"]["rules"] = write(self.overlay / "rules.fragment.json",
                                            {"format": "openciv2-rules/1", "marker": "overlay-only"})
        self.child["compatibility"] = None
        self.child["scenarios"] = []
        (self.root / self.manifest["files"]["rules"]["path"]).unlink()
        (self.root / self.manifest["compatibility"]["path"]).unlink()
        report = self.overlay_run()
        rules = next(f for f in report["files"] if f["role"] == "rules")
        names = next(f for f in report["files"] if f["role"] == "cityNames")
        self.assertEqual(rules["owner"]["id"], "authored.overlay")
        self.assertEqual(names["owner"]["id"], self.manifest["id"])
        self.assertFalse(any(f["role"] == "compatibility" for f in report["files"]))
        unused = self.temp_path / "unused.json"
        unused.write_bytes(b"unrelated invalid JSON")
        self.assertEqual(report, self.overlay_run("--parent", unused))

    def test_locale_selection_falls_back_as_one_group_and_partial_translation_fails(self):
        self.child["defaultLocale"] = "fr"
        for kind in LOCALIZED:
            self.child["files"][kind] = {"fr": write(self.overlay / f"{kind}.fr.json",
                {"format": f"openciv2-{kind}/1", "locale": "fr"})}
        report = self.overlay_run("--locale", "fr")
        self.assertEqual(report["selectedLocale"], "fr")
        self.assertEqual(self.overlay_run("--locale", "unavailable")["selectedLocale"], "fr")
        self.assertEqual(self.overlay_run("--locale", "en")["selectedLocale"], "en")
        del self.child["files"]["council"]
        self.assertEqual(self.overlay_run(code=1)["code"], "incomplete-locale")

    def test_parent_pins_and_cycles_are_diagnosed_without_ambient_search(self):
        self.assertEqual(self.invoke(self.child_path, code=1)["code"], "missing-parent")
        self.assertEqual(self.overlay_run("--parent", self.path, code=1)["code"], "ambiguous-parent")
        self.child["base"]["revision"] = 999
        self.assertEqual(self.overlay_run(code=1)["code"], "parent-identity")
        self.child["base"].update(id=self.child["id"], revision=1)
        self.assertEqual(self.overlay_run(code=1)["code"], "dependency-cycle")

    def test_nested_custom_gameplay_and_explicit_builtin_replacement(self):
        bundle_root = self.overlay / "variant"
        shutil.copytree(ROOT / "data/gameplay/classic-mge-5.4.0f", bundle_root)
        bundle = json.loads((bundle_root / "gameplay.json").read_bytes())
        combat = json.loads((bundle_root / "combat.json").read_bytes())
        combat["antiAirMultipliers"]["ordinaryAircraft"] = 7
        bundle["components"]["combat"] = write(bundle_root / "combat.json", combat)
        bundle.update(id="authored.variant", revision=2, mode="custom")
        selection = write(bundle_root / "gameplay.json", bundle)
        selection["path"] = "variant/gameplay.json"
        self.child["gameplay"] = selection
        report = self.overlay_run()
        self.assertFalse(report["gameplay"]["builtin"])
        self.assertFalse(report["gameplay"]["classicValues"])
        self.assertEqual(report["gameplay"]["owner"]["id"], self.child["id"])
        self.assertEqual(self.overlay_run("--require-classic", code=1)["code"], "classic-values-required")
        self.child["gameplay"] = {"builtin": "classic-mge-5.4.0f", "revision": 1}
        (bundle_root / "gameplay.json").write_bytes(b"now unused")
        self.assertTrue(self.overlay_run("--require-classic")["gameplay"]["builtin"])

    def test_nested_gameplay_cannot_symlink_outside_its_directory_inside_pack(self):
        bundle_root = self.overlay / "variant"
        shutil.copytree(ROOT / "data/gameplay/classic-mge-5.4.0f", bundle_root)
        outside = self.overlay / "elsewhere.json"
        shutil.copyfile(bundle_root / "calendar.json", outside)
        (bundle_root / "calendar.json").unlink()
        try:
            (bundle_root / "calendar.json").symlink_to(outside)
        except OSError as error:
            self.skipTest(f"symlinks unavailable: {error}")
        self.child["gameplay"] = {"path": "variant/gameplay.json",
                                  "sha256": sha((bundle_root / "gameplay.json").read_bytes())}
        self.assertEqual(self.overlay_run(code=2)["code"], "source-error")


if __name__ == "__main__":
    unittest.main()
