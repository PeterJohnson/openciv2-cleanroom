"""X02 §2/§6 rejection and comparison contracts; actual C++ cases run in CTest."""
from copy import deepcopy
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import run_conformance as runner


class ConformanceRunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        shutil.copytree(ROOT / "docs", self.root / "docs")
        self.path = self.root / "docs/algorithms/conformance/X02-F01-primary-modulo.json"
        self.case = runner.load_fixture(self.path, self.root)

    def load(self, change):
        case = deepcopy(self.case)
        change(case)
        self.path.write_text(json.dumps(case), encoding="utf-8")
        return runner.load_fixture(self.path, self.root)

    def identity_adapter(self):
        for source in runner.SOURCES:
            destination = self.root / source
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / source, destination)
        sources = {path: runner.inventory.digest(self.root / path) for path in runner.SOURCES}
        identity = {"format": "openciv2-conformance-adapter/1",
                    "sources": {path: sources[path] for path in runner.ADAPTER_SOURCES},
                    "buildIdentity": "test build identity\n"}
        # A frozen build-time identity, never recomputed from the live checkout.
        adapter = self.root / "identity_adapter.py"
        adapter.write_text("import sys\n"
                           "assert sys.argv[1:] == ['--identity']\n"
                           f"print({json.dumps(identity)!r})\n", encoding="utf-8")
        return [sys.executable, str(adapter)], sources, identity

    def test_stale_adapter_is_rejected_before_any_fixture_can_pass(self):
        command, _, _ = self.identity_adapter()
        report = self.root / "report.json"
        argv = ["run_conformance.py", "--root", str(self.root), "--report", str(report),
                "--adapter", *command]
        for source in runner.ADAPTER_SOURCES:
            with self.subTest(source=source):
                path = self.root / source
                before = path.read_bytes()
                path.write_bytes(before + b"\n")
                with patch.object(sys, "argv", argv), patch.object(runner, "execute") as execute:
                    with self.assertRaisesRegex(ValueError, "stale or incomplete adapter"):
                        runner.main()
                    execute.assert_not_called()
                self.assertFalse(report.exists())
                path.write_bytes(before)

    def test_report_retains_verified_embedded_identity(self):
        command, sources, identity = self.identity_adapter()
        report = self.root / "report.json"
        argv = ["run_conformance.py", "--root", str(self.root), "--report", str(report),
                "--adapter", *command]
        result = {"path": str(self.path), "status": "passed"}
        with patch.object(sys, "argv", argv), patch.object(runner, "execute", return_value=result):
            self.assertEqual(runner.main(), 0)
        recorded = json.loads(report.read_text())
        self.assertEqual(recorded["format"], "openciv2-conformance-run/2")
        self.assertEqual(recorded["adapterIdentity"], identity)
        self.assertEqual(recorded["implementationSources"], sources)
        self.assertEqual(recorded["buildIdentity"], identity["buildIdentity"])

    def test_external_build_identity_cannot_relabel_the_adapter(self):
        command, _, _ = self.identity_adapter()
        build_identity = self.root / "build-identity.txt"
        build_identity.write_text("a different configuration\n", encoding="utf-8")
        argv = ["run_conformance.py", "--root", str(self.root), "--build-identity", str(build_identity),
                "--adapter", *command]
        with patch.object(sys, "argv", argv), patch.object(runner, "execute") as execute:
            with self.assertRaisesRegex(ValueError, "build identity does not match"):
                runner.main()
            execute.assert_not_called()

    def test_missing_malformed_and_timed_out_identities_fail(self):
        command, sources, identity = self.identity_adapter()
        for output in ("not JSON", "{}", json.dumps({**identity, "format": "unknown"}),
                       json.dumps({**identity, "sources": {}})):
            with self.subTest(output=output[:50]):
                process = subprocess.CompletedProcess([], 0, output, "")
                with patch.object(runner.subprocess, "run", return_value=process):
                    with self.assertRaises(ValueError):
                        runner.read_adapter_identity(command, sources)
        with patch.object(runner.subprocess, "run", return_value=subprocess.CompletedProcess([], 2, "", "old adapter")):
            with self.assertRaisesRegex(ValueError, "identity query failed"):
                runner.read_adapter_identity(command, sources)
        with patch.object(runner.subprocess, "run", side_effect=subprocess.TimeoutExpired(command, 5)):
            with self.assertRaisesRegex(ValueError, "identity watchdog expired"):
                runner.read_adapter_identity(command, sources)

    def test_source_change_during_execution_cannot_publish_a_report(self):
        command, _, _ = self.identity_adapter()
        report = self.root / "report.json"
        def change_during_execution(*_):
            path = self.root / "src/base/arithmetic.cpp"
            path.write_bytes(path.read_bytes() + b"\n")
            return {"path": str(self.path), "status": "passed"}
        argv = ["run_conformance.py", "--root", str(self.root), "--report", str(report),
                "--adapter", *command]
        with patch.object(sys, "argv", argv), patch.object(runner, "execute", side_effect=change_during_execution):
            with self.assertRaisesRegex(ValueError, "sources changed during the run"):
                runner.main()
        self.assertFalse(report.exists())

    def test_existing_vectors_validate_without_external_game_or_assets(self):
        for path in self.path.parent.glob("*.json"):
            case = runner.load_fixture(path, self.root)
            self.assertIsNone(runner.compare(case, case["expected"]))

    def test_parser_rejects_duplicate_keys_nonfinite_and_trailing_data(self):
        for data in ('{"format":"a","format":"b"}', '{"x":NaN}', '{} garbage'):
            with self.subTest(data=data):
                self.path.write_text(data, encoding="utf-8")
                with self.assertRaises(ValueError):
                    runner.load_fixture(self.path, self.root)

    def test_integer_types_ranges_and_canonical_decimal_are_exact(self):
        for value in ("-0", "+1", "01", "2147483648", "-2147483649", 1, True, "1.0", " 1"):
            with self.subTest(value=value), self.assertRaises(ValueError):
                self.load(lambda c: c["boundary"]["arguments"]["code"].update(value=value))
        with self.assertRaises(ValueError):
            self.load(lambda c: c["boundary"]["arguments"]["code"].update({"$type": "u32"}))
        with self.assertRaises(ValueError):
            self.load(lambda c: c["boundary"]["arguments"]["n"].update(value="0"))

    def test_unknown_fields_metadata_target_and_hashes_fail(self):
        changes = [
            lambda c: c.update(unknown=True),
            lambda c: c.update(revision=True),
            lambda c: c.update(format="openciv2-conformance/2"),
            lambda c: c["boundary"].update(unknown=True),
            lambda c: c["target"].update(version="unverified"),
            lambda c: c["specifications"][0].update(sha256="0" * 64),
            lambda c: c["specifications"][0].update(file="../../../../outside.md"),
            lambda c: c["specifications"].append(deepcopy(c["specifications"][0])),
            lambda c: c["evidence"]["sources"][0].update(section="missing-heading"),
        ]
        for change in changes:
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.load(change)
        case = self.load(lambda c: c.update(extensions={"note": "metadata only"}))
        self.assertEqual(case["extensions"]["note"], "metadata only")

    def test_unimplemented_features_never_silently_reduce_a_claim(self):
        changes = [
            lambda c: c["boundary"].update(operation="F02.unknown"),
            lambda c: c["entry"].update(state={"F02.extra": {"$type": "i32", "value": "1"}}),
            lambda c: c["randomness"].update(mode="implementation-replay"),
            lambda c: c["randomness"].update(choices=[{}]),
            lambda c: c["environment"].update(inputs=[{}]),
            lambda c: c["comparison"].update(exclusions=[{}]),
            lambda c: c["comparison"].update(normalizations=[{}]),
            lambda c: c["expected"].update(outcome="safe-stop"),
        ]
        for change in changes:
            with self.subTest(change=change), self.assertRaises(runner.UnsupportedFixture):
                self.load(change)

    def test_missing_or_invented_dependencies_and_events_fail(self):
        for edges in ([], [["root.enter", "root.exit"]], [["root.return", "root.enter"]],
                      [["unknown", "root.exit"]], [["root.enter", "root.enter"]],
                      [["root.enter", "root.return"], ["root.enter", "root.return"]]):
            with self.subTest(edges=edges), self.assertRaises(ValueError):
                self.load(lambda c: c["comparison"]["order"].update(before=edges))
        with self.assertRaises(ValueError):
            self.load(lambda c: c["comparison"].update(traceKinds=["return", "snapshot"]))
        with self.assertRaises(ValueError):
            self.load(lambda c: c["expected"]["trace"].pop())

    def test_comparison_rejects_results_payloads_order_and_duplicate_events(self):
        actual = deepcopy(self.case["expected"])
        actual["result"]["value"] = "2"
        self.assertEqual(runner.compare(self.case, actual)["surface"], "result")
        actual = deepcopy(self.case["expected"])
        actual["trace"][0]["payload"]["arguments"]["n"]["value"] = "4"
        self.assertEqual(runner.compare(self.case, actual)["surface"], "trace.root.enter.payload")
        actual = deepcopy(self.case["expected"])
        actual["trace"][0], actual["trace"][1] = actual["trace"][1], actual["trace"][0]
        for seq, event in enumerate(actual["trace"]):
            event["seq"] = seq
        self.assertEqual(runner.compare(self.case, actual)["surface"], "trace.order")
        actual = deepcopy(self.case["expected"])
        actual["trace"].append(deepcopy(actual["trace"][0]))
        actual["trace"][-1]["seq"] = 3
        with self.assertRaisesRegex(ValueError, "duplicate"):
            runner.compare(self.case, actual)

    def test_adapter_receives_inputs_only_and_changed_expectation_is_a_failure(self):
        actual = deepcopy(self.case["expected"])
        self.load(lambda c: (c["expected"]["result"].update(value="2"),
                             c["expected"]["trace"][1]["payload"]["result"].update(value="2")))
        process = subprocess.CompletedProcess([], 0, json.dumps(actual), "")
        with patch.object(runner.subprocess, "run", return_value=process) as invoke:
            report = runner.execute(self.path, ["adapter"], self.root)
            self.assertEqual(invoke.call_args.args[0], ["adapter", "F01.primaryModuloFromCode", "32767", "3"])
        self.assertEqual(report["status"], "failed")
        self.assertEqual(report["firstMismatch"]["surface"], "result")

    def test_budgets_invalid_inputs_and_adapter_errors_cannot_pass(self):
        self.load(lambda c: c["limits"].update(events=2))
        with patch.object(runner.subprocess, "run") as invoke:
            self.assertEqual(runner.execute(self.path, ["adapter"], self.root)["status"], "budget-exhausted")
            invoke.assert_not_called()
        self.path.write_text('{"bad":true}', encoding="utf-8")
        with patch.object(runner.subprocess, "run") as invoke:
            self.assertEqual(runner.execute(self.path, ["adapter"], self.root)["status"], "invalid-fixture")
            invoke.assert_not_called()
        self.path.write_text(json.dumps(self.case), encoding="utf-8")
        with patch.object(runner.subprocess, "run", side_effect=subprocess.TimeoutExpired("adapter", 1)):
            self.assertEqual(runner.execute(self.path, ["adapter"], self.root)["status"], "budget-exhausted")
        for result in (subprocess.CompletedProcess([], 2, "", "failure"),
                       subprocess.CompletedProcess([], 0, "not JSON", "")):
            with patch.object(runner.subprocess, "run", return_value=result):
                self.assertEqual(runner.execute(self.path, ["adapter"], self.root)["status"], "adapter-failure")


if __name__ == "__main__":
    unittest.main()
