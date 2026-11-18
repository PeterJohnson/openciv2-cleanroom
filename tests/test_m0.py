"""Specification/content metadata integrity checks; no gameplay is executed."""

import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import check_assets as assets
import check_conformance as conformance


def write_json(path, value):
    path.write_text(json.dumps(value), encoding="utf-8")


class RegistryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        shutil.copytree(conformance.ROOT / "docs", self.root / "docs")
        shutil.copyfile(conformance.ROOT / "dependencies.json", self.root / "dependencies.json")
        registry = conformance.read_json(conformance.ROOT / "docs/conformance/registry.json")
        for name in {row["implementation"] for kind in ("operations", "states") for row in registry[kind]} - {None}:
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(conformance.ROOT / name, path)
        shutil.copytree(conformance.ROOT / "tools", self.root / "tools", ignore=shutil.ignore_patterns("__pycache__"))

    def edit(self, file, change):
        path = self.root / file
        data = conformance.read_json(path)
        change(data)
        write_json(path, data)

    def test_checked_in_inventory_without_external_inputs(self):
        self.assertIn("no gameplay executed", conformance.validate(self.root))
        self.assertIn("no external files", assets.check_inventory(self.root))

    def test_windows_style_checkout_preserves_inventory_bytes(self):
        shutil.copyfile(conformance.ROOT / ".gitattributes", self.root / ".gitattributes")
        binary = b"\x00\x01\n\x02\r\n\xff"
        (self.root / "binary.dat").write_bytes(binary)
        checkout = self.root / "checkout"
        # Exercise Git's Windows newline setting without inheriting user hooks,
        # attributes, or index/worktree overrides, and without changing this repo.
        env = {key: value for key, value in os.environ.items() if not key.startswith("GIT_")}
        env.update(GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull, GIT_TERMINAL_PROMPT="0")
        for args in (
            ["init"],
            ["config", "core.autocrlf", "true"],
            ["config", "core.attributesFile", os.devnull],
            ["add", "docs", "dependencies.json", ".gitattributes", "binary.dat", "src", "tools"],
            ["checkout-index", "--all", f"--prefix={checkout.as_posix()}/"],
        ):
            result = subprocess.run(["git", *args], cwd=self.root, env=env, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("no gameplay executed", conformance.validate(checkout))
        self.assertIn("no external files", assets.check_inventory(checkout))
        self.assertEqual((checkout / "binary.dat").read_bytes(), binary)

    def test_changed_specification_requires_review(self):
        path = next((self.root / "docs/algorithms").glob("F01-*.md"))
        path.write_bytes(path.read_bytes() + b"\nChanged contract.\n")
        with self.assertRaisesRegex(ValueError, "specification hash changed"):
            conformance.validate(self.root)

    def test_added_fixture_cannot_escape_inventory(self):
        write_json(self.root / "docs/algorithms/conformance/new.json", {})
        with self.assertRaisesRegex(ValueError, "fixture inventory mismatch"):
            conformance.validate(self.root)

    def test_false_execution_claim_is_rejected(self):
        def remove_implementation(registry):
            operation = registry["fixtures"][0]["operation"]
            next(row for row in registry["operations"] if row["id"] == operation)["implementation"] = None
        self.edit("docs/conformance/registry.json", remove_implementation)
        self.edit("docs/conformance/registry.json", lambda r: r["fixtures"][0].update(executionStatus="passed"))
        with self.assertRaisesRegex(ValueError, "Execution claims require"):
            conformance.validate(self.root)

    def test_missing_state_closure_is_rejected(self):
        self.edit("docs/conformance/registry.json", lambda r: r["operations"][0]["reads"].append("F02.missing"))
        with self.assertRaisesRegex(ValueError, "unknown state closure"):
            conformance.validate(self.root)

    def test_cyclic_boundary_order_is_rejected(self):
        self.edit("docs/conformance/registry.json", lambda r: r["operations"][0]["dependencies"].append(["root.exit", "root.enter"]))
        with self.assertRaisesRegex(ValueError, "cyclic event"):
            conformance.validate(self.root)

    def test_unknown_gap_and_stale_asset_pin(self):
        self.edit("docs/conformance/coverage.json", lambda r: r["rows"][0]["gaps"].append("unknown"))
        with self.assertRaisesRegex(ValueError, "unknown gap"):
            conformance.validate(self.root)
        self.edit("dependencies.json", lambda r: r["assets"].update(sha256="0" * 64))
        with self.assertRaisesRegex(ValueError, "asset release pin mismatch"):
            assets.check_inventory(self.root)

    def test_bitmap_caches_cannot_reenter_runtime_selection(self):
        self.edit("docs/content/assets.json", lambda r: r["selectedRuntimeFiles"].append("data/fonts.bin"))
        with self.assertRaisesRegex(ValueError, "runtime selection mismatch"):
            assets.check_inventory(self.root)

    def test_font_source_version_and_face_identity(self):
        self.edit("docs/content/fonts.json", lambda r: r.update(version="unknown"))
        with self.assertRaisesRegex(ValueError, "font dependency pin mismatch"):
            assets.check_inventory(self.root)
        self.edit("docs/content/fonts.json", lambda r: r.update(version="2.37"))
        self.edit("docs/content/fonts.json", lambda r: r["faces"][0].update(id="mono.regular"))
        with self.assertRaisesRegex(ValueError, "font face inventory mismatch"):
            assets.check_inventory(self.root)

    def test_duplicate_json_key_and_nonfinite_rejected(self):
        path = self.root / "data.json"
        for text in ('{"value":1,"value":2}', '{"value":NaN}'):
            path.write_text(text, encoding="utf-8")
            with self.assertRaises(ValueError):
                conformance.read_json(path)


class AssetTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def atlas(self, count=1, offset=None, proxy=0):
        # Deliberately a header-only PNG: M0 checks containers, M2 will decode pixels.
        png = b"\x89PNG\r\n\x1a\n" + struct.pack(">I", 13) + b"IHDR" + struct.pack(">II", 1, 1)
        start = 780 + 180 * count
        index = b""
        for i in range(count):
            key = f"image/{i}".encode().ljust(160, b"\0")
            index += key + struct.pack("<5I", 1, 1, start if offset is None else offset, len(png), proxy)
        path = self.root / "atlas.pack"
        path.write_bytes(b"C2ATLAS1" + struct.pack("<I", count) + bytes(768) + index + png)
        return path

    def test_atlas_deduplicated_payload_is_allowed(self):
        self.assertEqual(assets.check_atlas(self.atlas(count=2)), 2)

    def test_atlas_bounds_and_truncation(self):
        for kwargs in ({"offset": 1}, {"offset": 0xFFFFFFFF}, {"proxy": 0xFFFFFFFF}):
            with self.subTest(kwargs=kwargs), self.assertRaisesRegex(ValueError, "outside payload"):
                assets.check_atlas(self.atlas(**kwargs))
        path = self.atlas()
        path.write_bytes(path.read_bytes()[:800])
        with self.assertRaisesRegex(ValueError, "truncated atlas index"):
            assets.check_atlas(path)

    def test_path_escape_rejected(self):
        for name in ("../secret", "/etc/passwd", "C:/secret", "data/../../secret", "data\\secret"):
            with self.subTest(name=name), self.assertRaisesRegex(ValueError, "unsafe asset"):
                assets.data_file(self.root, name)

    def test_source_selection_does_not_pick_up_neighbors(self):
        art = self.root / "assets/atlas-hd"
        art.mkdir(parents=True)
        (art / "selected.png").write_bytes(b"selected")
        (art / "neighbor.png").write_bytes(b"unselected")
        write_json(self.root / "runtime-inputs.json", ["selected.png"])
        baseline = assets.selection_identity(self.root)
        (art / "neighbor.png").write_bytes(b"changed")
        self.assertEqual(assets.selection_identity(self.root), baseline)
        (art / "selected.png").write_bytes(b"changed")
        self.assertNotEqual(assets.selection_identity(self.root), baseline)
        write_json(self.root / "runtime-inputs.json", ["review/comparison.png"])
        with self.assertRaisesRegex(ValueError, "non-runtime"):
            assets.selection_identity(self.root)

    def test_symlink_cannot_escape_selected_root(self):
        with tempfile.TemporaryDirectory() as outside:
            target = Path(outside) / "image.png"
            target.write_bytes(b"external")
            (self.root / "assets").mkdir()
            try:
                (self.root / "assets/atlas-hd").symlink_to(outside, target_is_directory=True)
            except OSError:
                self.skipTest("symlinks unavailable on this host")
            write_json(self.root / "runtime-inputs.json", ["image.png"])
            with self.assertRaisesRegex(ValueError, "outside asset"):
                assets.selection_identity(self.root)

    def test_corrupt_package_and_manifest_paths(self):
        for name in assets.PACKAGE_FILES:
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b"test")
        shutil.copyfile(self.atlas(), self.root / "data/atlas-hd.pack")
        # These are still verified as opaque upstream payloads, never parsed as fonts.
        (self.root / "data/fonts.bin").write_bytes(b"unused legacy data")
        (self.root / "data/fonts_aa.bin").write_bytes(b"unused legacy data")
        write_json(self.root / "data/cityview-rects.json", [[0, 0, 0, 0]] * 62)
        manifest = {"schema": 1, "kind": "openciv2-assets", "version": "synthetic-test",
                    "files": {name: assets.identity(self.root / name) for name in assets.PACKAGE_FILES}}
        write_json(self.root / "package.json", manifest)
        self.assertIn("no release authenticity claim", assets.check_package(self.root))
        (self.root / "LICENSE").write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "package file changed"):
            assets.check_package(self.root)
        manifest["files"]["../escape"] = manifest["files"].pop("LICENSE")
        write_json(self.root / "package.json", manifest)
        with self.assertRaisesRegex(ValueError, "file set mismatch"):
            assets.check_package(self.root)


if __name__ == "__main__":
    unittest.main()
