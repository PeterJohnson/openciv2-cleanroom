#!/usr/bin/env python3
"""Managed X02 stale-build, policy separation and real adapter rejection checks."""
import argparse
from copy import deepcopy
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
import sys

import run_conformance as managed

runner = managed.runner
ROOT = managed.ROOT
ARGS = None


def fixture(name):
    return runner.load_fixture(ROOT / f"docs/algorithms/conformance/X02-{name}.json")


def request(case):
    arguments, data = runner.adapter_input(case)
    return {"arguments": arguments, "input": data, "hostMilliseconds": 5000}


def admission_cases():
    result = []
    base = fixture("F01-sample-gameplay-modulo")
    for label, change, message in [
        ("missing", lambda c: c["randomness"].update(choices=[]), "missing"),
        ("duplicate", lambda c: c["randomness"]["choices"].append(deepcopy(c["randomness"]["choices"][0])), "duplicate_key"),
        ("law", lambda c: c["randomness"]["choices"][0]["arguments"]["n"].update(value="4"), "law_mismatch"),
        ("call", lambda c: c["randomness"]["choices"][0].update(call="other"), "missing"),
        ("attempt", lambda c: c["randomness"]["choices"][0].update(attempt=1), "missing"),
        ("unused", lambda c: c["randomness"]["choices"].append({**deepcopy(c["randomness"]["choices"][0]), "call": "unused"}), "unused"),
        ("code", lambda c: c["randomness"]["choices"][0]["code"].update(value="32768"), "invalid_record"),
        ("constant", lambda c: c["boundary"]["arguments"]["n"].update(value="1"), "unused"),
    ]:
        case = deepcopy(base); change(case); result.append((label, request(case), 2, message, None))
    trailing = request(base); trailing["input"] += "extra\n"; result.append(("trailing", trailing, 2, "Trailing", None))
    for label, arguments, data, message in [
        ("unknown", ["unknown"], "", "Unknown"),
        ("arity", ["F01.primaryCode"], "", "argument count"),
        ("numeric", ["F01.primaryModuloFromCode", "01", "7"], "", "Noncanonical"),
        ("range", ["F01.primaryModuloFromCode", "2147483648", "7"], "", "out-of-range"),
        ("map", ["W01.wrapX", "0"], "3 4 0", "dimensions"),
        ("boolean", ["W01.wrapX", "0"], "8 8 2", "boolean"),
        ("capacity", ["F02.reserveUnitSlot", "1"], "2049", "bound"),
        ("domain", ["F01.primaryCode", "cosmetic"], "implementation-replay 0 0 0", "mode/domain"),
        ("replay-budget", ["F01.primaryCode", "gameplay"], "implementation-replay 0 0 1000000000000", "budget"),
    ]:
        result.append((label, {"arguments": arguments, "input": data, "hostMilliseconds": 5000}, 2, message, None))
    for label, change, outcome in [
        ("provider-missing", lambda c: c["environment"].update(inputs=[]), "unexpected-input"),
        ("provider-mismatch", lambda c: c["environment"]["inputs"][0].update(id="other"), "unexpected-input"),
        ("event-budget", lambda c: c["limits"].update(events=2), "budget-exhausted"),
    ]:
        case = fixture("F02-workspace-publication"); change(case); result.append((label, request(case), 0, "", outcome))
    return result


class ManagedConformanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cases = admission_cases()
        if ARGS.browser_bundle:
            with tempfile.TemporaryDirectory(prefix="oc2-conformance-admission-") as directory:
                inputs, output = Path(directory) / "inputs.json", Path(directory) / "output.json"
                inputs.write_text(json.dumps([case[1] for case in cls.cases]), encoding="utf-8")
                process = subprocess.run([ARGS.node, str(ROOT / "tools/dotnet/run_conformance_browser.cjs"), str(ARGS.browser_bundle), str(inputs), str(output)], capture_output=True, text=True, timeout=120)
                if process.returncode: raise RuntimeError(process.stderr)
                raw = managed.read_json(output)
                if raw["pageErrors"]: raise RuntimeError(str(raw["pageErrors"]))
                cls.identity, cls.observations = raw["identity"], raw["observations"]
        else:
            process = subprocess.run([*ARGS.adapter, "--identity"], capture_output=True, text=True, check=True, timeout=10)
            cls.identity = json.loads(process.stdout)
            cls.observations = []
            for _, item, *_ in cls.cases:
                process = subprocess.run([*ARGS.adapter, *item["arguments"]], input=item["input"], capture_output=True, text=True, timeout=5)
                cls.observations.append({"exitCode": process.returncode, "stdout": process.stdout, "stderr": process.stderr})

    def test_actual_rejections_and_failure_prefixes(self):
        self.assertEqual(len(self.cases), len(self.observations))
        for (label, _, code, message, outcome), observation in zip(self.cases, self.observations):
            with self.subTest(label=label):
                self.assertEqual(code, observation["exitCode"], observation)
                self.assertIn(message, observation["stderr"])
                if code:
                    self.assertEqual("", observation["stdout"])
                else:
                    actual = json.loads(observation["stdout"])
                    self.assertEqual(outcome, actual["outcome"])
                    self.assertEqual(actual["state"], actual["trace"][-2]["payload"]["state"])
                    self.assertEqual("stop", actual["trace"][-1]["kind"])
                    self.assertNotIn("root/exit", [e["id"] for e in actual["trace"]])

    def test_compiled_identity_rejects_stale_missing_duplicate_and_wrong_policy(self):
        self.assertTrue(managed.validate_identity(self.identity))
        for mutate in [lambda x: x.update(policy="oc2-mt19937-64"), lambda x: x.update(policyVersion=2), lambda x: x["Build"].pop(),
                       lambda x: x["Build"].append(deepcopy(x["Build"][0])), lambda x: x["Build"][0].update(Configuration="Debug"),
                       lambda x: x["Build"][0].update(Sdk="unknown"), lambda x: x["Build"][0]["Sources"].pop(next(iter(x["Build"][0]["Sources"]))),
                       lambda x: x["Build"][0]["Sources"].update({next(iter(x["Build"][0]["Sources"])): "0" * 64})]:
            damaged = deepcopy(self.identity); mutate(damaged)
            with self.assertRaises(ValueError): managed.validate_identity(damaged)

    def test_seven_policy_replacements_preserve_fifty_historical_fixtures(self):
        manifest, paths = managed.selection()
        self.assertEqual(57, len(paths)); self.assertEqual(15, len({c["operation"] for c in manifest["cases"]}))
        self.assertEqual(50, sum(c["path"] == c["historicalPath"] and c["sha256"] == c["historicalSha256"] for c in manifest["cases"]))
        old = ROOT / "docs/algorithms/conformance/X02-F01-sample-replay-gameplay.json"
        new = ROOT / "docs/algorithms/conformance-pcg32/X02-F01-sample-replay-gameplay.json"
        with self.assertRaises(ValueError): runner.load_fixture(old, random_policy=managed.POLICY)
        with self.assertRaises(ValueError): runner.load_fixture(new)

    def test_expectations_never_enter_adapter_inputs(self):
        for name in ["F01-sample-gameplay-modulo", "F02-fresh-game-used", "F02-workspace-reentry"]:
            case = fixture(name); before = runner.adapter_input(case); case["expected"] = {"sentinel": "never sent"}
            self.assertEqual(before, runner.adapter_input(case))

    def test_mutated_artifacts_are_detected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "adapter.dll"; path.write_bytes(b"first"); original = {str(path): managed.write_identity.sha(path)}
            managed.stable({}, original); path.write_bytes(b"second")
            with self.assertRaises(ValueError): managed.stable({}, original)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--adapter", nargs=argparse.REMAINDER)
    group.add_argument("--browser-bundle", type=Path)
    parser.add_argument("--node", default="node")
    ARGS = parser.parse_args()
    unittest.main(argv=[sys.argv[0]])
