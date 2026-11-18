"""X02 rejection/comparison contracts; identity stubs never execute gameplay."""
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
import conformance as runner


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

    def test_existing_vectors_validate_without_external_game_or_assets(self):
        for path in self.path.parent.glob("*.json"):
            case = runner.load_fixture(path, self.root)
            self.assertIsNone(runner.compare(case, case["expected"]))

    def use_state_case(self, suffix):
        self.path = self.root / f"docs/algorithms/conformance/X02-F02-{suffix}.json"
        self.case = runner.load_fixture(self.path, self.root)

    def use_sampler(self, suffix):
        self.path = self.root / f"docs/algorithms/conformance/X02-F01-sample-{suffix}.json"
        self.case = runner.load_fixture(self.path, self.root)

    def test_controlled_choice_records_are_typed_unique_and_law_checked(self):
        self.use_sampler("gameplay-modulo")
        changes = [
            lambda c: c["randomness"]["choices"].append(deepcopy(c["randomness"]["choices"][0])),
            lambda c: c["randomness"]["choices"][0].update(attempt=True),
            lambda c: c["randomness"]["choices"][0].update(attempt=-1),
            lambda c: c["randomness"]["choices"][0].update(call=""),
            lambda c: c["randomness"]["choices"][0].update(law="unknown"),
            lambda c: c["randomness"]["choices"][0]["code"].update(value="32768"),
            lambda c: c["randomness"]["choices"][0]["code"].update({"$type": "u32"}),
            lambda c: c["randomness"]["choices"][0]["result"].update(value="2"),
            lambda c: c["randomness"].update(contexts={"invented": {}}),
            lambda c: c["randomness"].update(policy={"id": "invented"}),
        ]
        for change in changes:
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.load(change)
        self.use_sampler("zero-unit")
        with self.assertRaisesRegex(ValueError, "mapping"):
            self.load(lambda c: c["randomness"]["choices"][0]["result"].update(bits="8000000000000000"))

    def test_replay_requires_exact_policy_domain_and_complete_matching_position(self):
        self.use_sampler("replay-gameplay")
        context = "gameplay/fixture"
        changes = [
            lambda c: c["randomness"]["policy"].update(version="1"),
            lambda c: c["randomness"]["policy"].update(sha256="0" * 64),
            lambda c: c["randomness"]["contexts"][context].pop("draws"),
            lambda c: c["randomness"]["contexts"][context]["seed"].update(value="18446744073709551616"),
            lambda c: c["randomness"]["contexts"][context]["draws"].update(value="-1"),
            lambda c: c["randomness"]["contexts"][context]["domain"].update(value="cosmetic"),
            lambda c: c["randomness"]["contexts"][context]["name"].update(value="other"),
            lambda c: c["randomness"]["contexts"].update(extra={}),
            lambda c: c["entry"]["state"]["F01.randomContexts"][context]["draws"].update(value="0"),
            lambda c: c["randomness"].update(choices=[{}]),
        ]
        for change in changes:
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.load(change)

    def test_sampler_event_identity_dependencies_context_and_law_are_required(self):
        self.use_sampler("replay-unit")
        changes = [
            lambda c: c["expected"]["trace"][1].update(call="other"),
            lambda c: c["expected"]["trace"][1]["payload"].update(site="other"),
            lambda c: c["expected"]["trace"][1]["payload"].update(attempt=1),
            lambda c: c["expected"]["trace"][1]["payload"]["context"].update(value="gameplay/fixture"),
            lambda c: c["expected"]["trace"][1]["payload"]["result"].update(bits="3ff0000000000000"),
            lambda c: c["comparison"]["order"].update(before=[["root.enter", "root.return"], ["root.return", "root.exit"]]),
            lambda c: c["comparison"].update(traceKinds=["enter", "return", "snapshot"]),
        ]
        for change in changes:
            with self.subTest(change=change), self.assertRaises(ValueError):
                self.load(change)
        self.use_sampler("singleton")
        self.assertEqual(len(runner.selected_events(self.case)), 3)
        self.assertEqual(runner.consumed_inputs(self.case)["choices"], 0)

    def test_sampler_expected_values_never_enter_the_adapter(self):
        self.use_sampler("gameplay-modulo")
        actual = deepcopy(self.case["expected"])
        def wrong_expectation(c):
            c["expected"]["trace"][1]["payload"]["code"]["value"] = "32765"
            c["expected"]["trace"][1]["payload"]["result"]["value"] = "2"
            c["expected"]["trace"][2]["payload"]["result"]["value"] = "2"
            c["expected"]["result"]["value"] = "2"
        self.load(wrong_expectation)
        with patch.object(runner.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, json.dumps(actual), "")) as invoke:
            report = runner.execute(self.path, ["adapter"], self.root)
        self.assertEqual(report["status"], "failed")
        words = invoke.call_args.kwargs["input"].split()
        self.assertEqual(words[-1], "32767")
        self.assertNotIn("32765", words)
        self.assertEqual(report["consumed"], {"providers": 0, "choices": 1, "events": 4})
        self.assertEqual(report["randomness"], self.case["randomness"])

    def test_sample_budget_and_replay_watchdog_fail_without_a_success_claim(self):
        self.use_sampler("gameplay-code")
        self.load(lambda c: c["limits"].update(events=3))
        with patch.object(runner.subprocess, "run") as invoke:
            self.assertEqual(runner.execute(self.path, ["adapter"], self.root)["status"], "budget-exhausted")
            invoke.assert_not_called()
        self.use_sampler("replay-gameplay")
        with patch.object(runner.subprocess, "run", side_effect=subprocess.TimeoutExpired(["adapter"], 5)):
            self.assertEqual(runner.execute(self.path, ["adapter"], self.root)["status"], "budget-exhausted")

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
