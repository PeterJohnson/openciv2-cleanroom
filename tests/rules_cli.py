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


def authored_classic_rules():
    text = "@COSMIC\n" + "\n".join(map(str, (3, 2, 2, 5, 5, 1, 2, 7, 14, 8, 12,
                                                 10, 20, 3, 3, 8, 1, 50, 50, 10, 75, 5))) + "\n"
    text += "@CIVILIZE\n" + "".join(f"Advance {i},3,1,nil,nil,0,3\n" for i in range(100))
    text += "@IMPROVE\n" + "".join(f"Café {i},10,1,nil\n" for i in range(67))
    text += "@ENDWONDER\n" + "nil\n" * 28
    text += "@UNITS\n" + "".join(f"Unit {i},no,0,2,0,1,1,3,1,10,0,0,nil,0\n" for i in range(62))
    text += "@TERRAIN\n"
    for i in range(33):
        text += f"Terrain {i},1,3,1,1,0" + (",yes,1,3,2,no,0,4,1,Drt" if i < 11 else "") + "\n"
    text += "@GOVERNMENTS\n" + "".join(f"Government {i},Leader,Leader\n" for i in range(7))
    text += "@LEADERS\n" + "".join(f"Leader,Leader,1,1,0,Tribe {i},Example,0,0,0\n" for i in range(21))
    for section, count in (("CARAVAN", 16), ("ORDERS", 12), ("DIFFICULTY", 6), ("ATTITUDES", 9)):
        text += f"@{section}\n" + "".join(f"Name {i}" + (",x" if section == "ORDERS" else "") + "\n" for i in range(count))
    return text.encode("cp1252")


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

    def city_names(self):
        return {"format": "openciv2-city-names/1",
                "civilizations": [{"pluralName": row["pluralName"], "cityNames": ["First", "First"]}
                                  for row in self.rules["tribes"]], "fallbackNames": None}

    def convert_rules(self, source, rules, mapping, code=0):
        result = subprocess.run([str(DRIVER), "content", "convert-rules", str(source),
                                 str(rules), str(mapping)], capture_output=True, text=True,
                                encoding="utf-8", timeout=10)
        self.assertEqual(result.returncode, code, result.stdout + result.stderr)
        return json.loads(result.stdout)

    def test_convert_rules_publishes_both_components_with_valid_receipts(self):
        source = self.root / "RULES.TXT"
        source.write_bytes(authored_classic_rules())
        original = source.read_bytes()
        rules, mapping = self.root / "converted-rules.json", self.root / "converted-mapping.json"
        report = self.convert_rules(source, rules, mapping)
        self.assertEqual(report["status"], "converted")
        self.assertEqual(report["conversion"], "classic-rules/1")
        self.assertEqual(report["source"]["sha256"], hashlib.sha256(original).hexdigest())
        for role, path in (("rules", rules), ("compatibility", mapping)):
            self.assertTrue(report["outputs"][role]["published"])
            self.assertEqual(report["outputs"][role]["sha256"], hashlib.sha256(path.read_bytes()).hexdigest())
        self.assertEqual(source.read_bytes(), original)
        self.manifest["files"]["rules"] = {"path": rules.name, "sha256": report["outputs"]["rules"]["sha256"]}
        self.manifest["compatibility"] = {"path": mapping.name, "sha256": report["outputs"]["compatibility"]["sha256"]}
        write(self.path, self.manifest)
        self.invoke()
        self.assertEqual(json.loads(rules.read_bytes())["improvements"][0]["name"], "Café 0")
        self.assertEqual(report["unsupportedFields"], [])
        self.assertEqual(report["lossyFields"], [])

    def test_convert_rules_refuses_aliases_existing_outputs_and_malformed_source(self):
        source = self.root / "RULES.TXT"
        source.write_bytes(authored_classic_rules())
        original = source.read_bytes()
        output = self.root / "new.json"
        existing = self.root / "existing.json"
        existing.write_bytes(b"keep")
        for a, b in ((source, output), (output, source), (output, output), (existing, output), (output, existing)):
            self.assertEqual(self.convert_rules(source, a, b, code=2)["status"], "rejected")
            self.assertFalse(output.exists())
            self.assertEqual(source.read_bytes(), original)
            self.assertEqual(existing.read_bytes(), b"keep")
        source.write_bytes(b"@COSMIC\n")
        self.assertEqual(self.convert_rules(source, output, self.root / "mapping.json", code=1)["status"], "rejected")
        self.assertFalse(output.exists())

    def test_convert_rules_reports_output_failure_and_partial_publication(self):
        source = self.root / "RULES.TXT"
        source.write_bytes(authored_classic_rules())
        missing = self.root / "missing" / "new.json"
        output = self.root / "new.json"
        report = self.convert_rules(source, missing, output, code=2)
        self.assertEqual(report["status"], "rejected")
        self.assertFalse(output.exists())
        report = self.convert_rules(source, output, missing, code=2)
        self.assertEqual(report["status"], "partial")
        self.assertTrue(report["outputs"]["rules"]["published"])
        self.assertFalse(report["outputs"]["compatibility"]["published"])
        self.assertEqual(hashlib.sha256(output.read_bytes()).hexdigest(), report["outputs"]["rules"]["sha256"])
        self.assertFalse(missing.exists())
        self.assertEqual(source.read_bytes(), authored_classic_rules())

    def convert_preferences(self, source, output, code=0, operation="convert-preferences"):
        result = subprocess.run([str(DRIVER), "content", operation,
                                 str(source), str(self.path), str(output)],
                                capture_output=True, text=True, encoding="utf-8", timeout=10)
        self.assertEqual(result.returncode, code, result.stdout + result.stderr)
        return json.loads(result.stdout)

    def test_convert_names_publishes_valid_component_and_preserves_sources(self):
        for slot, row in enumerate(self.rules["tribes"]):
            row["pluralName"] = f"Tribe {slot}"
        self.publish()
        source = self.root / "CITY.TXT"
        source.write_bytes(("".join(f"@Tribe {slot}\nFirst\nFirst\n@STOP\n" for slot in range(21)) +
                            "@EXTRA\nCafé\n@STOP\n").encode("cp1252"))
        before = {p: p.read_bytes() for p in self.root.iterdir() if p.is_file()}
        output = self.root / "converted-names.json"
        receipt = self.convert_preferences(source, output, operation="convert-names")
        self.assertEqual(receipt["conversion"], "classic-city-names/1")
        self.assertEqual(receipt["status"], "converted")
        self.assertEqual(receipt["source"]["sha256"], hashlib.sha256(source.read_bytes()).hexdigest())
        self.assertEqual(receipt["output"]["sha256"], hashlib.sha256(output.read_bytes()).hexdigest())
        self.assertEqual(before, {p: p.read_bytes() for p in before})
        self.assertEqual(json.loads(output.read_bytes())["fallbackNames"], ["Café"])
        self.manifest["files"]["cityNames"] = {
            "path": output.name, "sha256": receipt["output"]["sha256"]}
        self.publish()
        self.invoke(operation="validate-names")
        for destination in (source, output, self.root / "missing" / "new.json"):
            self.assertEqual(self.convert_preferences(source, destination, code=2,
                             operation="convert-names")["status"], "rejected")
        self.assertEqual(source.read_bytes(), before[source])

    def test_convert_names_rejects_ambiguous_input_without_publication(self):
        self.publish()
        source = self.root / "CITY.TXT"
        source.write_bytes(b"@Custom\nFirst\n@STOP\n@CUSTOM\nSecond\n@STOP\n")
        output = self.root / "converted-names.json"
        self.assertEqual(self.convert_preferences(source, output, code=1,
                         operation="convert-names")["code"], "ambiguous-classic-selector")
        self.assertFalse(output.exists())

    def test_convert_preferences_publishes_valid_component_and_receipts(self):
        for slot, row in enumerate(self.rules["improvements"]):
            row["name"] = f"Improvement {slot}"
        self.rules["improvements"][1]["name"] = "Café"
        self.publish()
        source = self.root / "CITYPREF.TXT"
        source.write_bytes("@NODEFEND\r\n@AUTOBUILD\r\nCafé\r\nImprovement 2\r\nCafé\r\n".encode("cp1252"))
        before = {p: p.read_bytes() for p in self.root.iterdir() if p.is_file()}
        output = self.root / "converted.json"
        receipt = self.convert_preferences(source, output)
        self.assertEqual(receipt["status"], "converted")
        self.assertEqual(receipt["source"]["sha256"], hashlib.sha256(source.read_bytes()).hexdigest())
        self.assertEqual(receipt["output"]["sha256"], hashlib.sha256(output.read_bytes()).hexdigest())
        self.assertEqual(receipt["conversion"], "classic-city-preferences/1")
        self.assertEqual({r["role"] for r in receipt["dependencies"]}, {"rules", "compatibility"})
        self.assertEqual(receipt["unsupportedFields"], [])
        self.assertEqual(receipt["lossyFields"], [])
        self.assertEqual(before, {p: p.read_bytes() for p in before})
        component = json.loads(output.read_bytes())
        self.assertEqual(component["buildPreferences"],
                         ["improvements.authored.1", "improvements.authored.2", "improvements.authored.1"])
        self.assertEqual(component["humanCityDefensePriority"], "deprioritize")
        self.manifest["files"]["cityPreferences"] = {
            "path": output.name, "sha256": receipt["output"]["sha256"]}
        self.publish()
        self.invoke(operation="validate-preferences")

    def test_convert_preferences_refuses_overwrite_alias_and_output_failure(self):
        self.publish()
        source = self.root / "CITYPREF.TXT"
        source.write_bytes(b"@NODEFEND\n")
        output = self.root / "existing.json"
        output.write_bytes(b"keep me")
        for destination in (source, output, self.root / "missing" / "new.json"):
            with self.subTest(destination=destination):
                report = self.convert_preferences(source, destination, code=2)
                self.assertEqual(report["status"], "rejected")
        self.assertEqual(source.read_bytes(), b"@NODEFEND\n")
        self.assertEqual(output.read_bytes(), b"keep me")
        self.assertFalse((self.root / "missing").exists())

    def test_convert_preferences_rejects_bad_source_and_selected_identity(self):
        self.publish()
        source = self.root / "CITYPREF.TXT"
        output = self.root / "new.json"
        source.write_bytes(b"@AUTOBUILD\nunknown improvement\n")
        self.assertEqual(self.convert_preferences(source, output, code=1)["code"],
                         "unresolved-classic-reference")
        self.assertFalse(output.exists())
        self.rules["improvements"][1]["id"] = "unmapped.identity"
        self.publish()
        source.write_bytes(b"@NODEFEND\n")
        self.assertEqual(self.convert_preferences(source, output, code=1)["status"], "rejected")
        self.assertFalse(output.exists())

    def test_city_names_validate_complete_lists_with_receipts_and_no_source_changes(self):
        names = self.city_names()
        receipt = write(self.root / "names.json", names)
        self.manifest["files"]["cityNames"] = receipt
        self.publish()
        before = {p: p.read_bytes() for p in self.root.iterdir() if p.is_file()}
        report = self.invoke(operation="validate-names")
        self.assertEqual(report["format"], "openciv2-city-names-validation/1")
        self.assertEqual(report["scope"], "rules-gameplay-and-city-names")
        self.assertTrue(report["cityNames"]["startingListsComplete"])
        self.assertFalse(report["cityNames"]["fallbackAvailable"])
        self.assertEqual(report["cityNames"]["fallbackCount"], 0)
        self.assertEqual(report["cityNames"]["civilizations"][0],
                         {"pluralName": "Café 0", "nameCount": 2})
        source = next(f for f in report["files"] if f["role"] == "cityNames")
        self.assertEqual(source["sha256"], receipt["sha256"])
        self.assertEqual(before, {p: p.read_bytes() for p in self.root.iterdir() if p.is_file()})

    def test_city_names_reject_missing_lists_ambiguity_and_unestablished_comparisons(self):
        for fault in ("missing", "ambiguous", "comparison", "terminator"):
            with self.subTest(fault=fault):
                names = self.city_names()
                if fault == "missing":
                    names["civilizations"].pop()
                elif fault == "ambiguous":
                    names["civilizations"][1]["pluralName"] = "CAFé 0"
                elif fault == "comparison":
                    names["civilizations"][1]["pluralName"] = "CafÉ 0"
                else:
                    names["fallbackNames"] = ["@stop now"]
                self.manifest["files"]["cityNames"] = write(self.root / "names.json", names)
                self.publish()
                report = self.invoke(code=1, operation="validate-names")
                self.assertEqual(report["code"],
                                 "comparison-unavailable" if fault == "comparison" else "invalid-content")
                self.assertNotIn("cityNames", report)

    def test_overlay_city_names_keep_missing_lists_and_empty_fallback_without_base_merging(self):
        self.manifest["files"]["cityNames"] = write(self.root / "names.json", self.city_names())
        self.publish()
        overlay = self.root.parent / "name-overlay"
        overlay.mkdir()
        names = {"format": "openciv2-city-names/1", "civilizations": [], "fallbackNames": []}
        receipt = write(overlay / "names.json", names)
        child = {"format": "openciv2-content-pack/1", "id": "authored.name-overlay", "revision": 1,
                 "profile": "classic-mge-5.4.0f", "defaultLocale": "en",
                 "files": {"cityNames": receipt},
                 "base": {"id": self.manifest["id"], "revision": self.manifest["revision"],
                          "sha256": hashlib.sha256(self.path.read_bytes()).hexdigest()},
                 "provenance": {"authors": ["Contract tests"], "license": "MIT", "notes": []}}
        path = overlay / "content.json"
        write(path, child)
        report = self.invoke(path, "--parent", self.path, operation="validate-names")
        self.assertFalse(report["cityNames"]["startingListsComplete"])
        self.assertTrue(report["cityNames"]["fallbackAvailable"])
        self.assertEqual(report["cityNames"]["civilizations"], [])
        source = next(f for f in report["files"] if f["role"] == "cityNames")
        self.assertEqual(source["owner"]["id"], "authored.name-overlay")

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
