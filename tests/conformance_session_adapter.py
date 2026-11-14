"""X02 fresh-game projection/adapter checks against the compiled runtime."""
from copy import deepcopy
import json
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import conformance_session as session
import run_conformance as runner

COMMAND = sys.argv[1:]
sys.argv[1:] = []


def path(name):
    return ROOT / f"docs/algorithms/conformance/X02-F02-fresh-game-{name}.json"


class SessionAdapterTests(unittest.TestCase):
    def invoke(self, words, arguments=()):
        return subprocess.run([*COMMAND, session.OPERATION, *arguments],
                              input="\n".join(words) + "\n", text=True, encoding="utf-8",
                              capture_output=True, timeout=5)

    def test_registered_vectors_use_real_runtime_and_exact_projection(self):
        for name in ("cold", "used", "empty-scans"):
            with self.subTest(name=name):
                case = runner.load_fixture(path(name))
                result = self.invoke(session.input_words(case))
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertIsNone(runner.compare(case, json.loads(result.stdout)))

    def test_every_observed_field_including_last_free_slot_affects_comparison(self):
        case = runner.load_fixture(path("used"))
        actual = json.loads(self.invoke(session.input_words(case)).stdout)
        for key, kind in session.STATES.items():
            with self.subTest(key=key):
                changed = deepcopy(actual)
                value = changed["state"][key]
                if kind == "bytes":
                    value["hex"] = value["hex"][:-2] + ("00" if value["hex"][-2:] != "00" else "01")
                elif kind == "i16[]":
                    value[-1]["value"] = "1"
                elif kind == "bool":
                    value["value"] = not value["value"]
                elif kind == "text":
                    value["value"] = "unexpected retained text"
                else:
                    value["value"] = "1" if value["value"] != "1" else "0"
                changed["trace"][-1]["payload"]["state"] = changed["state"]
                self.assertIsNotNone(runner.compare(case, changed))

    def test_projection_cannot_claim_complete_state_or_hide_other_fields(self):
        original_read = runner.inventory.read_json
        case = runner.load_fixture(path("cold"))
        changes = [lambda c: c["comparison"].update(stateScope="complete-boundary"),
                   lambda c: c["comparison"].update(exclusions=[]),
                   lambda c: c["comparison"]["exclusions"][0].update(selector="anything"),
                   lambda c: c["entry"]["state"].update(extra={"$type":"bool", "value":True})]
        for change in changes:
            modified = deepcopy(case)
            change(modified)
            with patch.object(runner.inventory, "read_json", side_effect=lambda p:
                              modified if Path(p) == path("cold") else original_read(p)):
                with self.assertRaises(ValueError):
                    runner.load_fixture(path("cold"))

    def test_transport_rejects_missing_extra_and_noncanonical_inputs(self):
        case = runner.load_fixture(path("used"))
        words = session.input_words(case)
        bad = [words[:-1], words + ["extra"], ["-1", *words[1:]],
               [*words[:2], "2049", *words[3:]],
               [*words[:3], "257", *words[4:]],
               [*words[:4], "00", *words[5:]],
               [*words[:4], "GG" * 2048, *words[5:]],
               [*words[:-1], "2"]]
        for values in bad:
            with self.subTest(prefix=values[:4], length=len(values)):
                result = self.invoke(values)
                self.assertEqual(result.returncode, 2)
                self.assertFalse(result.stdout)
        self.assertEqual(self.invoke(words, ["extra"]).returncode, 2)


if __name__ == "__main__":
    unittest.main()
