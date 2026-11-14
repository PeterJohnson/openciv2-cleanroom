"""F03/data-format rule admission through real mounted files; authored expectations."""
import copy
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

DRIVER = Path(sys.argv.pop(1)).resolve()
ROOT = Path(__file__).resolve().parents[1]


def write(path, value):
    raw = json.dumps(value, ensure_ascii=False).encode("utf-8")
    path.write_bytes(raw)
    return {"path": path.name, "sha256": hashlib.sha256(raw).hexdigest()}


def authored_rules():
    """Complete input identities, not stock data or generated engine expectations."""
    cosmic = dict(zip((
        "roadMovementMultiplier", "triremeLossDenominator", "foodPerCitizen", "foodBoxRows",
        "shieldBoxRows", "settlerFoodLowGovernment", "settlerFoodHighGovernment",
        "unhappinessCitySize", "unhappinessEmpireSize", "aqueductSize", "sewerSize",
        "technologyCostParadigm", "transformationWork", "monarchyFreeSupport",
        "communismFreeSupport", "fundamentalismFreeSupport", "communismPalaceDistance",
        "fundamentalismScienceLossPercent", "productionSwitchLossPercent", "paradropRange",
        "spaceshipMassThrustParadigm", "fundamentalismScienceCapTenths"),
        (3, 2, 2, 5, 5, 1, 2, 7, 14, 8, 12, 10, 20, 3, 3, 8, 1, 50, 50, 10, 75, 5)))
    rules = {"format": "openciv2-rules/1", "globalRules": cosmic, "wonderObsolescence": []}
    compatibility = {"format": "openciv2-classic-compatibility/1",
                     "profile": "classic-mge-5.4.0f", "encoding": "windows-1252",
                     "ruleSlots": {}, "textLookups": {}, "scenarioFiles": [],
                     "scenarioResources": [], "retainedUnitFlags": []}
    none = {"special": "none"}
    prototypes = (
        ("advances", 100, dict(aiValue=1, civilizedAiModifier=0, prerequisites=[none, none],
                              epoch="ancient", category="academic")),
        ("improvements", 67, dict(costRows=10, upkeep=1, prerequisite=none)),
        ("units", 62, dict(obsoleteBy={"special": "never"}, domain="land", movementPoints=2,
                          range=0, attack=1, defense=1, hitPoints=30, firepower=1, costRows=10,
                          capacity=0, role="attack", prerequisite=none, abilities=[])),
        ("terrain", 33, dict(movementCost=1, defenseMultiplier={"numerator": 3, "denominator": 2},
                            food=1, shields=1, trade=0)),
        ("governments", 7, dict(maleTitle="Leader", femaleTitle="Leader")),
        ("tribes", 21, dict(adjective="Example", leaderNames={"male": "Alex", "female": "Pat"},
                           preferredLeaderGender="female", preferredColor=1, cityStyle=0,
                           personality={"aggression": 0, "expansion": 0,
                                        "civilizationPreference": 0}, governmentTitles=[])),
        ("commodities", 16, {}), ("orders", 12, dict(shortcutHint="x")),
        ("difficulties", 6, {}), ("diplomaticAttitudes", 9, {}))
    for collection, count, prototype in prototypes:
        rules[collection] = []
        compatibility["ruleSlots"][collection] = []
        for slot in range(count):
            identity = f"{collection.lower()}.authored.{slot}"
            row = copy.deepcopy(prototype)
            row["id"] = identity
            row["pluralName" if collection == "tribes" else "name"] = f"Café {slot}"
            if collection == "terrain" and slot < 11:
                row.update(irrigation={"result": {"special": "improve-in-place"}, "yieldBonus": 1,
                                       "workTurns": 3, "aiGovernmentThreshold": 2},
                           mining={"result": {"special": "no-result"}, "yieldBonus": 0,
                                   "workTurns": 4, "aiGovernmentThreshold": 1},
                           transformation={"terrain": "terrain.authored.0"})
            rules[collection].append(row)
            compatibility["ruleSlots"][collection].append(
                {"id": identity, "slot": slot + (collection == "orders")})
    rules["wonderObsolescence"] = [{"wonder": f"improvements.authored.{slot}", "advance": none}
                                    for slot in range(39, 67)]
    return rules, compatibility


class RulesCliTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "pack café"
        shutil.copytree(ROOT / "docs/content/examples", self.root)
        self.path = self.root / "content.json"
        self.manifest = json.loads(self.path.read_bytes())
        self.rules, self.compatibility = authored_rules()

    def publish(self):
        self.manifest["files"]["rules"] = write(self.root / "rules.json", self.rules)
        self.manifest["compatibility"] = write(self.root / "compatibility.json", self.compatibility)
        write(self.path, self.manifest)

    def invoke(self, path=None, *options, code=0, operation="validate-rules"):
        result = subprocess.run([str(DRIVER), "content", operation, str(path or self.path),
                                 "--json", *map(str, options)], cwd=self.root.parent,
                                capture_output=True, text=True, encoding="utf-8", timeout=10)
        self.assertEqual(result.returncode, code, result.stdout + result.stderr)
        return json.loads(result.stdout)

    def test_preferences_preserve_order_and_report_declaring_source(self):
        preferences = {"format": "openciv2-city-preferences/1",
                       "humanCityDefensePriority": "unchanged",
                       "buildPreferences": ["improvements.authored.2", "improvements.authored.1",
                                            "improvements.authored.2"]}
        receipt = write(self.root / "preferences.json", preferences)
        self.manifest["files"]["cityPreferences"] = receipt
        self.publish()
        before = {p: p.read_bytes() for p in self.root.iterdir() if p.is_file()}
        report = self.invoke(operation="validate-preferences")
        self.assertEqual(report["format"], "openciv2-city-preferences-validation/1")
        self.assertEqual(report["scope"], "rules-gameplay-and-city-preferences")
        self.assertEqual(report["cityPreferences"]["buildSlots"], [2, 1, 2])
        self.assertEqual(report["cityPreferences"]["humanCityDefensePriority"], "unchanged")
        source = next(f for f in report["files"] if f["role"] == "cityPreferences")
        self.assertEqual(source["sha256"], receipt["sha256"])
        self.assertEqual(source["owner"]["id"], self.manifest["id"])
        self.assertEqual(before, {p: p.read_bytes() for p in self.root.iterdir() if p.is_file()})

    def test_preferences_reject_collisions_and_unestablished_comparisons(self):
        preferences = {"format": "openciv2-city-preferences/1",
                       "humanCityDefensePriority": "deprioritize",
                       "buildPreferences": ["improvements.authored.2"]}
        self.manifest["files"]["cityPreferences"] = write(self.root / "preferences.json", preferences)
        self.rules["improvements"][2]["name"] = "CAFé 1"
        self.publish()
        report = self.invoke(code=1, operation="validate-preferences")
        self.assertEqual(report["code"], "invalid-content")
        self.assertNotIn("cityPreferences", report)
        self.rules["improvements"][2]["name"] = "CafÉ 1"
        self.publish()
        report = self.invoke(code=1, operation="validate-preferences")
        self.assertEqual(report["code"], "comparison-unavailable")
        self.assertIn("buildPreferences", report["location"])

    def test_complete_authored_pack_normalizes_without_modifying_sources(self):
        self.rules["units"].reverse()
        self.publish()
        before = {p: p.read_bytes() for p in self.root.iterdir() if p.is_file()}
        report = self.invoke(None, "--require-classic")
        self.assertEqual(report["format"], "openciv2-rules-validation/1")
        self.assertEqual(report["status"], "validated")
        self.assertEqual(report["scope"], "rules-and-gameplay-references")
        self.assertTrue(report["limitations"])
        self.assertEqual(report["rules"]["enabledAdvances"], 100)
        self.assertEqual(report["rules"]["foodBoxRows"], 6)
        self.assertEqual(report["rules"]["shieldBoxRows"], 5)
        self.assertEqual(report["rules"]["governmentAdvances"], [None, None, 54, 15, 31, 71, 21])
        self.assertEqual(len(report["rules"]["units"]), 62)
        self.assertEqual(report["rules"]["units"][0],
                         {"id": "units.authored.0", "movement": 6, "hitPoints": 30, "costRows": 10})
        self.assertEqual(before, {p: p.read_bytes() for p in self.root.iterdir() if p.is_file()})

    def test_format_example_is_rejected_as_incomplete_rules(self):
        report = self.invoke(code=1)
        self.assertEqual(report["status"], "rejected")
        self.assertEqual(report["code"], "invalid-content")
        self.assertNotIn("rules", report)

    def test_invalid_identities_references_encoding_and_hp_fail_before_publication(self):
        for kind in ("identity", "reference", "encoding", "hp"):
            with self.subTest(kind=kind):
                self.rules, self.compatibility = authored_rules()
                if kind == "identity":
                    self.compatibility["ruleSlots"]["units"].pop()
                elif kind == "reference":
                    self.rules["units"][0]["prerequisite"] = {"advance": "missing"}
                elif kind == "encoding":
                    self.rules["units"][0]["name"] = "unrepresentable 😀"
                else:
                    self.rules["units"][0]["hitPoints"] = 31
                self.publish()
                report = self.invoke(code=1)
                self.assertEqual(report["code"],
                                 "text-encoding" if kind == "encoding" else "invalid-content")
                self.assertIn("location", report)
                self.assertNotIn("rules", report)

    def test_inherited_rules_use_declaring_pack_and_selected_compatibility(self):
        self.publish()
        overlay = self.root.parent / "overlay"
        overlay.mkdir()
        child = {"format": "openciv2-content-pack/1", "id": "authored.overlay", "revision": 1,
                 "profile": "classic-mge-5.4.0f", "defaultLocale": "en", "files": {},
                 "base": {"id": self.manifest["id"], "revision": self.manifest["revision"],
                          "sha256": hashlib.sha256(self.path.read_bytes()).hexdigest()},
                 "provenance": {"authors": ["Contract tests"], "license": "MIT", "notes": []}}
        child_path = overlay / "content.json"
        write(child_path, child)
        report = self.invoke(child_path, "--parent", self.path)
        receipt = next(f for f in report["files"] if f["role"] == "rules")
        self.assertEqual(receipt["owner"]["id"], self.manifest["id"])
        child["compatibility"] = None
        write(child_path, child)
        self.assertEqual(self.invoke(child_path, "--parent", self.path, code=1)["code"],
                         "invalid-content")


if __name__ == "__main__":
    unittest.main()
