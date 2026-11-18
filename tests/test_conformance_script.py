"""X02 supplied-child schema and trace validation, independent of engine output."""
from copy import deepcopy
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import conformance_script as script
import conformance as runner


def fixture(name):
    return runner.load_fixture(ROOT / f"docs/algorithms/conformance/X02-F02-workspace-{name}.json")


class ScriptFixtureTests(unittest.TestCase):
    def test_all_authored_schedules_and_complete_closures_validate(self):
        for path in (ROOT / "docs/algorithms/conformance").glob("X02-F02-workspace-*.json"):
            with self.subTest(path=path.name):
                case = runner.load_fixture(path)
                self.assertIsNone(runner.compare(case, case["expected"]))

    def test_unregistered_actions_handlers_and_outputs_rejected(self):
        case = fixture("publication")
        mutations = [
            lambda e: e["inputs"][0].update(request="unknown"),
            lambda e: e["inputs"][0]["actions"][0].update(kind="enqueue"),
            lambda e: e["inputs"][0]["actions"][0].update(extra="silent"),
            lambda e: e["inputs"][0]["actions"][1]["target"].update(stateKey="F02.nextCityToken"),
            lambda e: e["inputs"][0]["actions"][1]["value"].update(value="2147483648"),
            lambda e: e["inputs"].append(deepcopy(e["inputs"][0])),
            lambda e: e["inputs"][0]["actions"].append(deepcopy(e["inputs"][0]["actions"][0])),
        ]
        for mutate in mutations:
            value = deepcopy(case["environment"])
            mutate(value)
            with self.assertRaises(ValueError):
                script.environment(value)
        value = fixture("reentry")["environment"]
        value["inputs"][0]["actions"][2]["handler"] = "host.code"
        with self.assertRaisesRegex(ValueError, "callback"):
            script.environment(value)

    def test_missing_reordered_or_relabelled_actions_cannot_bless_their_own_order(self):
        for mutation in ("missing", "reorder", "label", "output"):
            case = fixture("publication")
            trace = case["expected"]["trace"]
            if mutation == "missing":
                trace.pop(2)
            elif mutation == "reorder":
                trace[2], trace[3] = trace[3], trace[2]
            elif mutation == "label":
                trace[2]["payload"]["label"] = "other"
            else:
                trace[3]["payload"]["values"]["value"]["value"] = "9"
            for i, event in enumerate(trace):
                event["seq"] = i
            case["comparison"]["order"]["before"] = [[a["id"], b["id"]] for a, b in zip(trace, trace[1:])]
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                script.validate_expected(case)

    def test_reentry_must_keep_parent_provider_open(self):
        case = fixture("reentry")
        for field, value in (("parent", None), ("operation", "unknown")):
            trace = deepcopy(case["expected"]["trace"])
            next(e for e in trace if e["id"] == "root/deliver/full/enter")["payload"][field] = value
            with self.assertRaises(ValueError):
                script.trace_events(trace)

    def test_completed_inputs_count_returns_not_accepted_requests(self):
        case = fixture("unresolved-after-reentry")
        self.assertEqual(script.consumed(case["expected"])["providers"], 1)
        case["expected"]["trace"][-1]["payload"]["completedInputs"] = 2
        with self.assertRaisesRegex(ValueError, "completed input"):
            script.validate_expected(case)

    def test_stops_require_adjacent_snapshot_and_no_fabricated_cleanup(self):
        for mutation in ("snapshot", "return", "reason", "state"):
            case = fixture("unresolved")
            trace = case["expected"]["trace"]
            if mutation == "snapshot":
                trace[-1]["payload"]["snapshot"] = "earlier"
            elif mutation == "return":
                trace.append({"seq": len(trace), "id": "root/return", "kind": "return", "call": "root",
                              "payload": {"result": {"$type": "i32", "value": "0"}}})
            elif mutation == "reason":
                trace[-1]["payload"]["reason"] = "invented"
            else:
                case["expected"]["state"]["F02.workspaceCity"]["value"] = "4"
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                script.validate_expected(case)

    def test_city_absence_is_not_slot_zero_and_backing_is_bounded(self):
        self.assertEqual(script.city({"$type": "null"}), -1)
        self.assertEqual(script.city({"$type": "u16", "value": "0"}), 0)
        for value in ({"$type": "i32", "value": "-1"}, {"$type": "u16", "value": "256"},
                      {"$type": "null", "value": "0"}):
            with self.assertRaises(ValueError):
                script.city(value)

    def test_script_encoding_does_not_consume_expectations(self):
        case = fixture("reentry")
        words = script.input_words(case)
        case["expected"] = {"invented": "not an execution input"}
        case["comparison"] = {}
        self.assertEqual(script.input_words(case), words)


if __name__ == "__main__":
    unittest.main()
