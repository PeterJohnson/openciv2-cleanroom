"""Actual adapter rejection tests; invoked by CTest for native and WASM builds."""
from copy import deepcopy
import json
from pathlib import Path
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import conformance_random as random_inputs
import conformance as runner

COMMAND = sys.argv[1:]
sys.argv[1:] = []


class SamplerAdapterTests(unittest.TestCase):
    def setUp(self):
        self.case = runner.load_fixture(ROOT / "docs/algorithms/conformance/X02-F01-sample-gameplay-modulo.json")

    def invoke(self, case, *, extra=(), timeout=5):
        operation = case["boundary"]["operation"]
        args = [case["boundary"]["arguments"][key]["value"] for key in runner.OPERATIONS[operation]]
        words = random_inputs.input_words(case) + list(extra)
        return subprocess.run([*COMMAND, operation, *args], input="\n".join(words) + "\n",
                              capture_output=True, text=True, encoding="utf-8", timeout=timeout)

    def test_missing_duplicate_unused_and_mismatched_inputs_never_fall_back(self):
        for name, change, message in [
            ("missing", lambda c: c["randomness"].update(choices=[]), "missing"),
            ("duplicate", lambda c: c["randomness"]["choices"].append(deepcopy(c["randomness"]["choices"][0])), "duplicate_key"),
            ("arguments", lambda c: c["randomness"]["choices"][0]["arguments"]["n"].update(value="4"), "law_mismatch"),
            ("call", lambda c: c["randomness"]["choices"][0].update(call="other"), "missing"),
            ("attempt", lambda c: c["randomness"]["choices"][0].update(attempt=1), "missing"),
            ("unused", lambda c: c["randomness"]["choices"].append({**deepcopy(c["randomness"]["choices"][0]), "call": "unused"}), "unused"),
            ("code", lambda c: c["randomness"]["choices"][0]["code"].update(value="32768"), "invalid_record"),
        ]:
            with self.subTest(name=name):
                case = deepcopy(self.case)
                change(case)
                result = self.invoke(case)
                self.assertEqual(result.returncode, 2, result.stderr)
                self.assertIn(message, result.stderr)
                self.assertEqual(result.stdout, "")

    def test_constant_inputs_are_unused_and_trailing_input_is_rejected(self):
        case = deepcopy(self.case)
        case["boundary"]["arguments"]["n"]["value"] = "1"
        result = self.invoke(case)
        self.assertIn("unused", result.stderr)
        self.assertEqual(result.returncode, 2)
        result = self.invoke(self.case, extra=["unexpected"])
        self.assertIn("Trailing", result.stderr)
        self.assertEqual(result.returncode, 2)

    def test_replay_uses_only_origin_and_position_and_is_bounded_by_host_watchdog(self):
        case = runner.load_fixture(ROOT / "docs/algorithms/conformance/X02-F01-sample-replay-gameplay.json")
        result = self.invoke(case)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIsNone(runner.compare(case, json.loads(result.stdout)))
        case["randomness"]["contexts"]["gameplay/fixture"]["draws"]["value"] = "1000000000000"
        with self.assertRaises(subprocess.TimeoutExpired):
            self.invoke(case, timeout=1)


if __name__ == "__main__":
    if not COMMAND:
        raise SystemExit("Pass the compiled adapter command")
    unittest.main()
