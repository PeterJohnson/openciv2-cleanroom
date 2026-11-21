#!/usr/bin/env python3
"""Failure-path tests for managed evidence admission; no compiler or network needed."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import write_identity as identity
import run_application as application


class IdentityTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        (self.root / "global.json").write_text('{"sdk":{"version":"11.0.100"}}')
        (self.root / "Directory.Build.props").write_text(
            '<Project><PropertyGroup><RuntimeFrameworkVersion>11.0.0</RuntimeFrameworkVersion></PropertyGroup></Project>')
        self.expected = {"src/Example.cs": "a" * 64}
        self.report = {
            "Format": "openciv2-managed-foundations/2", "Passed": True,
            "Cases": [{"Passed": True}], "Build": [
                {"Assembly": name, "Sdk": "11.0.100", "Runtime": "11.0.0", "Target": "net11.0",
                 "Configuration": "Release", "Sources": dict(self.expected)}
                for name in sorted(identity.LIBRARIES | {"OpenCiv2.FoundationProbe"})]
        }

    def validate(self, report):
        with patch.object(identity, "expected_inputs", return_value=self.expected):
            return identity.validate_report(report, self.root)

    def test_accepts_matching_compiled_inputs(self):
        self.assertEqual(self.validate(self.report), self.expected)

    def test_stale_and_partial_evidence_rejected(self):
        mutations = [
            lambda r: r.update(Format="openciv2-managed-foundations/1"),
            lambda r: r.update(Passed=False),
            lambda r: r.update(Cases=[]),
            lambda r: r.update(PageErrors=["browser failure"]),
            lambda r: r["Cases"][0].update(Passed=False),
            lambda r: r["Build"].pop(),
            lambda r: r["Build"].append(copy.deepcopy(r["Build"][0])),
            lambda r: r["Build"][0].update(Sources={}),
            lambda r: r["Build"][0]["Sources"].update({"src/Example.cs": "b" * 64}),
            lambda r: r["Build"][0]["Sources"].update({"../outside.cs": "a" * 64}),
            lambda r: r["Build"][0].update(Sdk="latest"),
            lambda r: r["Build"][0].update(Runtime="10.0.0"),
            lambda r: r["Build"][0].update(Target="net9.0"),
            lambda r: r["Build"][0].update(Configuration="Debug"),
        ]
        for mutate in mutations:
            with self.subTest(mutate=mutate):
                report = copy.deepcopy(self.report)
                mutate(report)
                with self.assertRaises(ValueError):
                    self.validate(report)

    def test_duplicate_json_keys_rejected(self):
        with self.assertRaises(ValueError):
            json.loads('{"Passed":false,"Passed":true}', object_pairs_hook=identity.unique_object)

    def test_application_requires_current_renderer_identity(self):
        for host in sorted(application.HOSTS):
            with self.subTest(host=host):
                names = application.LIBRARIES | {host}
                if host == "OpenCiv2.Desktop":
                    names |= {"OpenCiv2.IO.Native"}
                report = {"format": "openciv2-application-seam/1", "build": [
                    {"Assembly": name, "Sdk": "11.0.100", "Runtime": "11.0.0",
                     "Target": "net11.0-browser" if name in {"OpenCiv2.Browser", "OpenCiv2.MapBrowser"} else "net11.0",
                     "Configuration": "Release", "Sources": dict(self.expected)} for name in sorted(names)]}
                with patch.object(identity, "expected_inputs", return_value=self.expected):
                    self.assertEqual(application.validate(report, source_root=self.root), self.expected)
                    renderer = next(entry for entry in report["build"] if entry["Assembly"] == "OpenCiv2.MapRendering")
                    renderer["Sources"] = {}
                    with self.assertRaisesRegex(ValueError, "Stale or unpinned application assembly"):
                        application.validate(report, source_root=self.root)
                    report["build"].remove(renderer)
                    with self.assertRaisesRegex(ValueError, "Incomplete or duplicate application identity"):
                        application.validate(report, source_root=self.root)

    def test_client_libraries_and_test_hosts_use_their_actual_directories(self):
        for kind, name in (
            ("src", "OpenCiv2.Console"), ("src", "OpenCiv2.ViewModels"),
            ("src", "OpenCiv2.App"), ("src", "OpenCiv2.Desktop"), ("src", "OpenCiv2.Browser"),
            ("src", "OpenCiv2.MapRendering"),
            ("tests", "OpenCiv2.FoundationCases"), ("tests", "OpenCiv2.FoundationProbe"),
            ("tests", "OpenCiv2.Browser.Tests"),
        ):
            with self.subTest(project=name):
                directory = self.root / kind / name
                directory.mkdir(parents=True)
                for filename in (f"{name}.csproj", "packages.lock.json", "Case.cs"):
                    (directory / filename).write_text(filename)
                with patch.object(identity, "COMMON", ()):
                    actual = identity.expected_inputs(self.root, name)
                self.assertEqual(set(actual), {
                    path.relative_to(self.root).as_posix() for path in directory.iterdir()
                })

    def test_map_browser_tracks_linked_production_and_matrix_inputs(self):
        names = ['tests/OpenCiv2.MapBrowser/OpenCiv2.MapBrowser.csproj',
                 'tests/OpenCiv2.MapBrowser/packages.lock.json',
                 'src/OpenCiv2.Browser/Program.cs', 'src/OpenCiv2.Browser/wwwroot/index.html',
                 'src/OpenCiv2.Browser/wwwroot/browser-lifecycle.js', 'src/OpenCiv2.Browser/wwwroot/storage.js',
                 'tests/OpenCiv2.MapProbe/NativeMapMatrix.Binding.cs',
                 'tests/OpenCiv2.MapProbe/RendererLibraryChecks.cs',
                 'tests/OpenCiv2.MapProbe/MapPresentationChecks.cs',
                 'tests/OpenCiv2.MapProbe/ScopedMapPerformanceWorld.cs',
                 'tests/OpenCiv2.MapProbe/GpuOptimizationChecks.cs',
                 'tests/OpenCiv2.MapProbe/GpuMaskChecks.cs',
                 'tests/OpenCiv2.MapProbe/GpuPixelChecks.cs']
        for name in names:
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(name)
        with patch.object(identity, 'COMMON', ()):
            before = identity.expected_inputs(self.root, 'OpenCiv2.MapBrowser')
            self.assertEqual(set(before), set(names))
            (self.root / 'src/OpenCiv2.Browser/Program.cs').write_text('changed host')
            self.assertNotEqual(before, identity.expected_inputs(self.root, 'OpenCiv2.MapBrowser'))

    def test_source_inventory_detects_added_deleted_and_changed_files(self):
        directory = self.root / "src/OpenCiv2.Base"
        directory.mkdir(parents=True)
        for relative in identity.COMMON:
            path = self.root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.touch(exist_ok=True)
        (directory / "OpenCiv2.Base.csproj").write_text("project")
        (directory / "packages.lock.json").write_text("lock")
        source = directory / "One.cs"
        source.write_text("first")
        first = identity.expected_inputs(self.root, "OpenCiv2.Base")
        (directory / "obj").mkdir()
        (directory / "obj/Generated.cs").write_text("not a source")
        self.assertEqual(first, identity.expected_inputs(self.root, "OpenCiv2.Base"))
        source.write_text("changed")
        self.assertNotEqual(first, identity.expected_inputs(self.root, "OpenCiv2.Base"))
        source.unlink()
        self.assertNotEqual(first.keys(), identity.expected_inputs(self.root, "OpenCiv2.Base").keys())
        (directory / "Two.cs").write_text("new")
        self.assertNotEqual(first.keys(), identity.expected_inputs(self.root, "OpenCiv2.Base").keys())
        (directory / "wwwroot").mkdir()
        asset = directory / "wwwroot/main.js"
        asset.write_text("first browser host")
        before = identity.expected_inputs(self.root, "OpenCiv2.Base")
        self.assertIn(asset.relative_to(self.root).as_posix(), before)
        asset.write_text("changed browser host")
        self.assertNotEqual(before, identity.expected_inputs(self.root, "OpenCiv2.Base"))


if __name__ == "__main__":
    unittest.main()
