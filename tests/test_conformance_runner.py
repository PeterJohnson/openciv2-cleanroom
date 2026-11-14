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

    def use_state_case(self, suffix):
        self.path = self.root / f"docs/algorithms/conformance/X02-F02-{suffix}.json"
        self.case = runner.load_fixture(self.path, self.root)

    def test_state_closure_and_array_domains_are_required_before_execution(self):
        self.use_state_case("unit-reserve-hole")
        changes = [
            lambda c: c["entry"]["state"].pop("F02.effectiveHumanSet"),
            lambda c: c["entry"]["state"]["F02.unitEnd"].update(value="2049"),
            lambda c: c["entry"]["state"]["F02.unitTokenScan"].pop(),
            lambda c: c["entry"]["state"]["F02.unitTokenScan"].append({"$type": "u32", "value": "0"}),
            lambda c: c["entry"]["state"]["F02.unitTokenScan"][0].update({"$type": "i32"}),
            lambda c: c["entry"]["state"]["F02.unitTokenScan"][0].update(value="-1"),
            lambda c: c["entry"]["state"]["F02.militaryCounts"].pop(),
            lambda c: c["entry"]["state"]["F02.militaryCounts"][1].update(value="32768"),
            lambda c: c["entry"]["state"]["F02.militaryCounts"][1].update(value="-32769"),
            lambda c: c["entry"]["state"]["F02.effectiveHumanSet"].update(value="256"),
            lambda c: c["boundary"]["arguments"]["owner"].update(value="8"),
            lambda c: c["expected"]["state"]["F02.unitTokenScan"].pop(),
        ]
        for change in changes:
            case = deepcopy(self.case)
            change(case)
            self.path.write_text(json.dumps(case), encoding="utf-8")
            with self.subTest(change=change), patch.object(runner.subprocess, "run") as invoke:
                self.assertEqual(runner.execute(self.path, ["adapter"], self.root)["status"], "invalid-fixture")
                invoke.assert_not_called()

    def test_state_comparison_preserves_slots_and_unchanged_fields(self):
        self.use_state_case("unit-reserve-hole")
        for key in ("F02.unitTokenScan", "F02.militaryCounts"):
            actual = deepcopy(self.case["expected"])
            actual["state"][key][0], actual["state"][key][1] = actual["state"][key][1], actual["state"][key][0]
            self.assertEqual(runner.compare(self.case, actual)["surface"], "state")
        actual = deepcopy(self.case["expected"])
        actual["trace"][-1]["payload"]["state"]["F02.effectiveHumanSet"]["value"] = "0"
        self.assertEqual(runner.compare(self.case, actual)["surface"], "trace.root.exit.payload")
        actual = deepcopy(self.case["expected"])
        actual["state"].pop("F02.militaryCounts")
        with self.assertRaisesRegex(ValueError, "missing registered state"):
            runner.compare(self.case, actual)

    def test_state_adapter_receives_entry_values_and_never_expected_outputs(self):
        self.use_state_case("unit-token-initial")
        actual = deepcopy(self.case["expected"])
        self.load(lambda c: (c["expected"]["state"]["F02.nextUnitToken"].update(value="999"),
                            c["expected"]["trace"][-1]["payload"]["state"]["F02.nextUnitToken"].update(value="999")))
        process = subprocess.CompletedProcess([], 0, json.dumps(actual), "")
        with patch.object(runner.subprocess, "run", return_value=process) as invoke:
            report = runner.execute(self.path, ["adapter"], self.root)
            self.assertEqual(report["status"], "failed")
            self.assertEqual(report["firstMismatch"]["surface"], "state")
            self.assertEqual(invoke.call_args.args[0], ["adapter", "F02.assignUnitToken"])
            self.assertEqual(invoke.call_args.kwargs["input"], "1\n200\n")

    def test_absence_is_distinct_from_slot_zero_and_snapshot_must_match(self):
        self.use_state_case("unit-reserve-military-limit")
        actual = deepcopy(self.case["expected"])
        actual["result"] = {"$type": "u16", "value": "0"}
        self.assertEqual(runner.compare(self.case, actual)["surface"], "result")
        for result in ({"$type": "null", "value": "0"}, {"$type": "i32", "value": "-1"},
                       {"$type": "u16", "value": "2048"}):
            with self.subTest(result=result), self.assertRaises(ValueError):
                self.load(lambda c: c["expected"].update(result=result))
        with self.assertRaisesRegex(ValueError, "exit trace/state disagreement"):
            self.load(lambda c: c["expected"]["trace"][-1]["payload"]["state"]["F02.unitEnd"].update(value="0"))

    def test_registration_cannot_omit_preserved_process_counter(self):
        self.use_state_case("unit-token-wrap")
        path = self.root / "docs/conformance/registry.json"
        registry = json.loads(path.read_text())
        op = next(row for row in registry["operations"] if row["id"] == "F02.assignUnitToken")
        op["reads"].remove("F02.nextCityToken")
        path.write_text(json.dumps(registry), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "state closure mismatch"):
            runner.load_fixture(self.path, self.root)

    def use_geometry_case(self, suffix):
        self.path = self.root / f"docs/algorithms/conformance/X02-W01-{suffix}.json"
        self.case = runner.load_fixture(self.path, self.root)

    def test_geometry_boolean_is_typed_and_dimensions_are_admitted_before_execution(self):
        self.use_geometry_case("wrap-flat")
        changes = [lambda c: c["entry"]["state"].pop("W01.flat")]
        for value in (0, 1, "true", "1", None):
            changes.append(lambda c, v=value: c["entry"]["state"]["W01.flat"].update(value=v))
        for key, value in (("W01.width", "0"), ("W01.width", "99"),
                           ("W01.width", "32768"), ("W01.height", "0"),
                           ("W01.height", "656"), ("W01.height", "-1")):
            changes.append(lambda c, k=key, v=value: c["entry"]["state"][k].update(value=v))
        for change in changes:
            case = deepcopy(self.case)
            change(case)
            self.path.write_text(json.dumps(case), encoding="utf-8")
            with self.subTest(case=case["entry"]), patch.object(runner.subprocess, "run") as invoke:
                self.assertEqual(runner.execute(self.path, ["adapter"], self.root)["status"], "invalid-fixture")
                invoke.assert_not_called()

    def test_geometry_adapter_receives_boolean_codes_and_original_coordinates(self):
        for suffix, args, stdin in (
                ("wrap-flat", ["W01.wrapX", "201"], "100\n80\n1\n"),
                ("step-off-map", ["W01.stepDistance", "0", "0", "250", "0"], "100\n80\n0\n"),
                ("lookup-alias", ["W01.tileIndex", "8", "5"], "100\n80\n")):
            self.use_geometry_case(suffix)
            process = subprocess.CompletedProcess([], 0, json.dumps(self.case["expected"]), "")
            with self.subTest(suffix=suffix), patch.object(runner.subprocess, "run", return_value=process) as invoke:
                self.assertEqual(runner.execute(self.path, ["adapter"], self.root)["status"], "passed")
                self.assertEqual(invoke.call_args.args[0], ["adapter", *args])
                self.assertEqual(invoke.call_args.kwargs["input"], stdin)

    def test_geometry_state_and_tile_absence_are_compared_without_coercion(self):
        self.use_geometry_case("wrap-flat")
        actual = deepcopy(self.case["expected"])
        actual["state"]["W01.flat"]["value"] = False
        self.assertEqual(runner.compare(self.case, actual)["surface"], "state")
        actual["state"]["W01.flat"]["value"] = 1
        with self.assertRaisesRegex(ValueError, "boolean type mismatch"):
            runner.compare(self.case, actual)
        self.use_geometry_case("lookup-miss")
        actual = deepcopy(self.case["expected"])
        actual["result"] = {"$type": "u32", "value": "0"}
        self.assertEqual(runner.compare(self.case, actual)["surface"], "result")
        for result in ({"$type": "null", "value": "0"}, {"$type": "i32", "value": "-1"},
                       {"$type": "u32", "value": "32768"}):
            with self.subTest(result=result), self.assertRaises(ValueError):
                self.load(lambda c: c["expected"].update(result=result))

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
