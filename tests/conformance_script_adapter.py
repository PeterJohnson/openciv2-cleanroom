"""X02 failure-prefix and bounded reentry tests against the compiled real entry."""
from copy import deepcopy
import json
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import conformance_script as script
import conformance as runner

COMMAND = sys.argv[1:]
sys.argv[1:] = []


def fixture(name):
    return runner.load_fixture(ROOT / f"docs/algorithms/conformance/X02-F02-workspace-{name}.json")


class ScriptAdapterTests(unittest.TestCase):
    def invoke(self, case, extra=()):
        city, full = script.arguments(case["boundary"]["arguments"])
        words = script.input_words(case) + list(extra)
        process = subprocess.run([*COMMAND, script.OPERATION, str(city), str(int(full))],
                                 input="\n".join(words) + "\n", capture_output=True, text=True,
                                 encoding="utf-8", timeout=5)
        self.assertEqual(process.returncode, 0, process.stderr)
        actual = json.loads(process.stdout)
        script.trace_events(actual["trace"])
        return actual

    def assert_stop(self, actual, outcome, completed=0):
        self.assertEqual(actual["outcome"], outcome)
        self.assertEqual(actual["result"], {"$type": "null"})
        self.assertEqual(actual["trace"][-1]["payload"]["completedInputs"], completed)
        self.assertEqual(actual["trace"][-2]["payload"]["state"], actual["state"])
        self.assertNotIn("root/exit", [e["id"] for e in actual["trace"]])

    def test_missing_mismatched_and_unused_inputs_retain_selection(self):
        for name, mutate in [
            ("missing", lambda c: c["environment"].update(inputs=[])),
            ("identity", lambda c: c["environment"]["inputs"][0].update(id="other/pipeline")),
            ("arguments", lambda c: c["environment"]["inputs"][0]["arguments"]["city"].update(value="1")),
            ("request", lambda c: c["environment"]["inputs"][0].update(request="other")),
        ]:
            with self.subTest(name=name):
                case = fixture("publication")
                mutate(case)
                actual = self.invoke(case)
                self.assert_stop(actual, "unexpected-input")
                self.assertEqual(actual["state"], {**case["entry"]["state"], "F02.workspaceCity": {"$type": "u16", "value": "0"}})
        case = fixture("publication")
        case["environment"]["inputs"].append({**deepcopy(case["environment"]["inputs"][0]), "id": "unused"})
        actual = self.invoke(case)
        self.assert_stop(actual, "unexpected-input", 1)
        self.assertEqual(actual["state"], case["expected"]["state"])

    def test_input_order_and_provider_budget_stop_before_nested_output(self):
        for wrong_order in (False, True):
            case = fixture("reentry")
            if wrong_order:
                case["environment"]["inputs"].reverse()
            else:
                case["limits"]["providerRequests"] = 1
            actual = self.invoke(case)
            self.assert_stop(actual, "unexpected-input" if wrong_order else "budget-exhausted")
            self.assertEqual(actual["state"]["F02.workspaceCity"]["value"], "3" if wrong_order else "0")
            self.assertEqual(actual["state"]["F02.happyCount"]["value"], "99" if wrong_order else "5")

    def test_every_event_budget_preserves_a_bounded_prefix(self):
        for limit in range(2, 12):
            case = fixture("publication")
            case["limits"]["events"] = limit
            actual = self.invoke(case)
            self.assertLessEqual(len(actual["trace"]), limit)
            if limit < len(case["expected"]["trace"]) + 2:
                self.assertEqual(actual["outcome"], "budget-exhausted")
                self.assertEqual(actual["state"], actual["trace"][-2]["payload"]["state"])
                effects = [e for e in actual["trace"] if e["kind"] == "effect"]
                self.assertEqual(actual["state"]["F02.happyCount"]["value"], "8" if effects else "5")
            else:
                self.assertIsNone(runner.compare(case, actual))

    def test_unresolved_outer_result_counts_only_completed_nested_provider(self):
        case = fixture("unresolved-after-reentry")
        actual = self.invoke(case)
        self.assert_stop(actual, "unresolved-input", 1)
        self.assertIsNone(runner.compare(case, actual))

    def test_failure_actions_and_unicode_id_do_not_cancel_later_outputs(self):
        case = fixture("failure-output")
        self.assertIsNone(runner.compare(case, self.invoke(case)))

    def test_nested_missing_result_prevents_later_parent_actions(self):
        case = fixture("reentry")
        case["environment"]["inputs"][1]["result"] = {"$type": "unavailable", "reason": "nested result unknown"}
        actual = self.invoke(case)
        self.assert_stop(actual, "unresolved-input")
        self.assertEqual(actual["trace"][-1]["call"], "root/deliver/full")
        self.assertEqual(actual["state"]["F02.unhappyCount"]["value"], "4")
        self.assertNotIn("root/action/later", [e["id"] for e in actual["trace"]])

    def test_callback_depth_is_bounded_without_rolling_back_parent_selection(self):
        case = fixture("publication")
        inputs, call = [], "root"
        for depth in range(65):
            selected = depth
            action = {"id": "next", "kind": "deliver", "handler": script.OPERATION,
                      "arguments": {"city": {"$type": "u16", "value": str(depth + 1)}, "full": {"$type": "bool", "value": True}}}
            inputs.append({"id": call + "/pipeline", "request": script.PROVIDER,
                           "arguments": {"city": {"$type": "u16", "value": str(selected)}},
                           "actions": [action], "result": {"$type": "i32", "value": "0"}})
            call += "/deliver/next"
        case["environment"]["inputs"] = inputs
        case["limits"].update(events=1000, providerRequests=100)
        actual = self.invoke(case)
        self.assert_stop(actual, "budget-exhausted")
        self.assertIn("depth", actual["trace"][-1]["payload"]["reason"])
        self.assertEqual(actual["state"]["F02.workspaceCity"]["value"], "63")

    def test_runner_does_not_report_budget_stop_as_gameplay_success(self):
        path = ROOT / "docs/algorithms/conformance/X02-F02-workspace-publication.json"
        case = fixture("publication")
        case["limits"]["events"] = 6
        with patch.object(runner, "load_fixture", return_value=case):
            report = runner.execute(path, COMMAND)
        self.assertEqual(report["status"], "budget-exhausted")
        self.assertEqual(report["consumed"], {"providers": 0, "choices": 0, "events": 6})
        self.assertEqual(report["actual"]["state"]["F02.happyCount"]["value"], "8")


if __name__ == "__main__":
    if not COMMAND:
        raise SystemExit("Pass the compiled adapter command")
    unittest.main()
