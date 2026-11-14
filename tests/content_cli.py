"""Native gameplay content I/O/CLI checks; stdlib-only, no original installation."""
from decimal import Decimal
import hashlib
import json
from math import gcd
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

DRIVER = Path(sys.argv.pop(1)).resolve()
ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "data/gameplay/classic-mge-5.4.0f"
LEDGER = json.loads((ROOT / "docs/content/gameplay-defaults.json").read_text())


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def normalized(value):
    if isinstance(value, dict):
        value = {k: normalized(v) for k, v in value.items()}
        if set(value) == {"numerator", "denominator"}:
            factor = gcd(value["numerator"], value["denominator"])
            return {k: v // factor for k, v in value.items()}
        return value
    if isinstance(value, list):
        return [normalized(v) for v in value]
    if isinstance(value, Decimal):
        assert value == value.to_integral_value()
        return int(value)
    return value


class ContentCliTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "content café"
        shutil.copytree(DATA, self.root)
        self.path = self.root / "gameplay.json"
        self.manifest = json.loads(self.path.read_text())

    def write_manifest(self):
        self.path.write_bytes(json.dumps(self.manifest, ensure_ascii=False).encode("utf-8"))

    def invoke(self, *options, code=0):
        result = subprocess.run([str(DRIVER), "content", "validate", str(self.path), "--json", *options],
                                capture_output=True, text=True, encoding="utf-8", timeout=10)
        self.assertEqual(result.returncode, code, result.stdout + result.stderr)
        return json.loads(result.stdout)

    def update(self, component, change=None, raw=None):
        path = self.root / self.manifest["components"][component]["path"]
        if raw is None:
            document = json.loads(path.read_text())
            change(document)
            raw = json.dumps(document, ensure_ascii=False).encode("utf-8")
        path.write_bytes(raw)
        self.manifest["components"][component]["sha256"] = sha(raw)
        self.write_manifest()

    def custom(self):
        self.manifest.update(id="authored.custom", revision=2, mode="custom")
        self.write_manifest()

    def test_shipped_bundle_matches_trusted_ledger_without_input_writes(self):
        before = {p.name: p.read_bytes() for p in self.root.iterdir() if p.is_file()}
        report = self.invoke("--require-classic")
        self.assertEqual(report["semanticSha256"], LEDGER["baseline"]["semanticSha256"])
        self.assertEqual(report["manifestSha256"], LEDGER["baseline"]["manifest"]["sha256"])
        self.assertEqual(report["revision"], "1")
        self.assertEqual(report["scope"], "gameplay-bundle-only")
        self.assertTrue(report["classicValues"])
        self.assertEqual({p.name: p.read_bytes() for p in self.root.iterdir() if p.is_file()}, before)

    def test_custom_normalization_matches_independent_decimal_fingerprint(self):
        self.custom()
        self.update("scoring", lambda d: d["conquest"]["largeMapMultiplier"].update(numerator=10, denominator=8))
        self.assertTrue(self.invoke("--require-classic")["classicValues"])
        self.update("combat", lambda d: d["strengthBonuses"]["veteran"].update(numerator=2, denominator=3))
        report = self.invoke()
        values = {}
        for name, descriptor in self.manifest["components"].items():
            doc = json.loads((self.root / descriptor["path"]).read_bytes(), parse_float=Decimal)
            doc.pop("extensions", None)
            values[name] = normalized(doc)
        envelope = {"format": "openciv2-gameplay-values/1", "target": self.manifest["target"], "components": values}
        digest = sha(json.dumps(envelope, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode("ascii"))
        self.assertEqual(report["semanticSha256"], digest)
        self.assertFalse(report["classicValues"])
        self.assertEqual(self.invoke("--require-classic", code=1)["code"], "classic-values-required")

    def test_hash_checks_precede_decoding_and_unknown_fields_are_rejected(self):
        path = self.root / "calendar.json"
        original = path.read_bytes()
        path.write_bytes(b"not JSON")
        self.assertEqual(self.invoke(code=1)["code"], "hash-mismatch")
        self.update("calendar", raw=b"not JSON")
        self.assertEqual(self.invoke(code=4)["code"], "invalid-json")
        self.update("calendar", raw=original)
        self.update("calendar", lambda d: d.update(unknown="not metadata"))
        report = self.invoke(code=1)
        self.assertEqual(report["code"], "invalid-content")
        self.assertIn("calendar.json", report["location"])

    def test_fraction_that_rounds_to_integer_is_rejected_and_large_revision_is_exact(self):
        raw = self.path.read_text().replace('"revision": 1', '"revision": 9007199254740993')
        self.path.write_bytes(raw.encode())
        self.assertEqual(self.invoke()["revision"], "9007199254740993")
        raw = (self.root / "combat.json").read_bytes().replace(b'"partisansAgainstZeroAttackMultiplier": 8',
                                                             b'"partisansAgainstZeroAttackMultiplier": 8.0000000000000000000001')
        self.update("combat", raw=raw)
        self.assertEqual(self.invoke(code=1)["code"], "invalid-integer")

    def test_paths_and_hardlink_aliases_cannot_select_unrelated_or_duplicate_sources(self):
        self.manifest["components"]["scoring"]["path"] = "../outside.json"
        self.write_manifest()
        self.assertEqual(self.invoke(code=1)["code"], "invalid-path")
        os.link(self.root / "calendar.json", self.root / "same-calendar.json")
        self.manifest["components"]["scoring"] = {"path": "same-calendar.json", "sha256": sha((self.root / "calendar.json").read_bytes())}
        self.write_manifest()
        self.assertEqual(self.invoke(code=1)["code"], "source-alias")

    def test_symlink_cannot_escape_mount_even_when_hash_is_correct(self):
        outside = Path(self.temp.name) / "outside.json"
        shutil.copyfile(self.root / "calendar.json", outside)
        try:
            (self.root / "escape.json").symlink_to(outside)
        except OSError as error:
            self.skipTest(f"symlinks unavailable: {error}")
        self.manifest["components"]["calendar"]["path"] = "escape.json"
        self.write_manifest()
        self.assertEqual(self.invoke(code=2)["code"], "source-error")


if __name__ == "__main__":
    unittest.main()
