"""First-wave data validation and specification-derived transcription probes.

Small arithmetic probes check extracted operands against worked cases; they are
not gameplay engine implementations, callback tests or original-game captures.
"""

from contextlib import redirect_stderr, redirect_stdout
from copy import deepcopy
import io
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import check_gameplay as gameplay

DATA = ROOT / "data/gameplay/classic-mge-5.4.0f"


def calendar_probe(document, turn, profile="prince", first_launch=-1):
    """F04 §5 ordinary calendar, nominal arithmetic only."""
    extra = max(turn - first_launch, 0) if first_launch >= 0 else 0
    t = turn - extra
    if t != 0:
        t -= 1
    periods = document["profiles"][profile]["periods"]
    for period in periods:
        if t <= period["turnCount"]:
            year = period["startYear"] + period["yearStep"] * t
            return (1 if year == 0 else year) + extra
        t -= period["turnCount"]
    last = periods[-1]
    year = last["startYear"] + last["turnCount"] * last["yearStep"] + t
    return (1 if year == 0 else year) + extra


class GameplayDataTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.schemas = gameplay.SchemaSet(ROOT / "docs/content/schemas")
        cls.ledger, cls.baseline = gameplay.check_repository(ROOT, cls.schemas)
        cls.values = cls.baseline["components"]
        cls.fingerprint = cls.baseline["semanticSha256"]

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.directory = Path(temporary.name)
        self.bundle = self.directory / "bundle"
        shutil.copytree(DATA, self.bundle)

    def load(self, require_classic=False):
        return gameplay.load_gameplay(self.bundle / "gameplay.json", self.schemas,
                                      self.fingerprint, require_classic)

    def write_manifest(self, document):
        (self.bundle / "gameplay.json").write_text(json.dumps(document), encoding="utf-8")

    def set_mode(self, mode):
        document = gameplay.read_json(self.bundle / "gameplay.json")
        document["mode"] = mode
        self.write_manifest(document)

    def replace_component(self, name, document=None, raw=None):
        path = self.bundle / (name + ".json")
        raw = raw if raw is not None else json.dumps(document).encode("utf-8")
        path.write_bytes(raw)
        manifest = gameplay.read_json(self.bundle / "gameplay.json")
        manifest["components"][name]["sha256"] = gameplay.digest(raw)
        self.write_manifest(manifest)

    def test_complete_defaults_and_pinned_provenance(self):
        self.assertTrue(self.load()["classicValues"])
        self.assertEqual(set(gameplay.COMPONENTS), set(self.values))
        self.assertEqual(8, len(self.ledger["specifications"]))
        self.assertEqual(59, len(self.ledger["bindings"]))

    def test_calendar_boundary_vectors_from_f04(self):
        calendar = self.values["calendar"]
        self.assertEqual([calendar_probe(calendar, turn) for turn in (0, 1, 61, 62, 101, 102)],
                         [-4000, -4000, -1000, -975, 1, 10])
        self.assertEqual(calendar_probe(calendar, 110, first_launch=101), 10)
        self.assertEqual(calendar_probe(calendar, 551, "chieftain-warlord"), 2000)
        self.assertEqual(calendar_probe(calendar, 552, "chieftain-warlord"), 2001)
        retirement = calendar["retirement"]
        self.assertEqual((retirement["warningYear"], retirement["endYear"], retirement["scenarioWarningLeadTurns"]), (2000, 2020, 5))
        self.assertNotEqual(2021, retirement["endYear"])  # Equality, not reached-year.

    def test_spaceship_original_parts_and_mass(self):
        ship = self.values["spaceship"]
        expected = {"structural": (39, 100), "propulsion": (8, 400), "fuel": (8, 400),
                    "habitation": (4, 1600), "life-support": (4, 1600), "solar-energy": (4, 1600)}
        self.assertEqual({k: (v["maximumCount"], v["massTons"]) for k, v in ship["parts"].items()}, expected)
        mass = sum(p["maximumCount"] * (p["massTons"] // 100) for p in ship["parts"].values())
        self.assertEqual(mass, 295)  # V07 full human ship.
        self.assertEqual(ship["peoplePerHabitation"], 10000)
        self.assertEqual((mass * 75) // (10 * 14 + 1), 156)  # Unchanged stock paradigm/thrust.

    def test_government_original_rates_and_effective_divisors(self):
        names = ("anarchy", "despotism", "monarchy", "communism", "fundamentalism", "republic", "democracy")
        records = self.values["governments"]["governments"]
        self.assertEqual([records[k]["maximumRateTenths"] for k in names], [6, 6, 7, 8, 8, 8, 10])
        self.assertEqual([records[k]["corruptionDivisor"] for k in names], [4, 5, 6, 7, 7, 8, 8])
        self.assertIsNone(records["anarchy"]["requiredAdvance"])
        self.assertIsNone(records["despotism"]["requiredAdvance"])
        for name in names[2:]:
            self.assertEqual(records[name]["requiredAdvance"], "advance." + name)
        # C03 celebration changes Despotism's effective divisor to Monarchy's.
        self.assertEqual(10 * 60 * 3 // (records["monarchy"]["corruptionDivisor"] * 20), 15)

    def test_difficulty_initial_operands_and_turn_200_distinction(self):
        names = ("chieftain", "warlord", "prince", "king", "emperor", "deity")
        records = self.values["difficulty"]["difficulties"]
        self.assertEqual([records[k]["aiBoxFactor"] for k in names], [15, 13, 12, 10, 9, 8])
        self.assertEqual([records[k]["humanResearchMultiplier"] for k in names], [6, 8, 10, 12, 14, 14])
        self.assertEqual([records[k]["aiResearchMultiplier"] for k in names], [14, 13, 12, 11, 10, 10])
        # C05's specified King/turn200/highest-human/no-qualifying-tech example.
        base = records["king"]["aiBoxFactor"]
        self.assertEqual((base, base - 2), (10, 8))

    def test_combat_sequential_bonus_and_replacement_vectors(self):
        combat = self.values["combat"]
        strength = 4 * 8
        for name in ("veteran", "paradrop"):
            bonus = combat["strengthBonuses"][name]
            strength += strength * bonus["numerator"] // bonus["denominator"]
        self.assertEqual(strength, 72)  # U06 attack4, Veteran + Paradropped.
        for name, expected in (("fortification", 36), ("fortress", 48), ("cityWalls", 72)):
            multiplier = combat["defenseMultipliers"][name]
            self.assertEqual(24 * multiplier["numerator"] // multiplier["denominator"], expected)
        self.assertEqual(combat["antiAirMultipliers"], {"ordinaryAircraft": 3, "destroyedAfterAttack": 5})
        self.assertEqual(combat["partisansAgainstZeroAttackMultiplier"], 8)
        bonus = combat["strengthBonuses"]["veteran"]
        self.assertEqual(-3 + (-3 * bonus["numerator"] // bonus["denominator"]), -5)

    def test_scoring_original_coefficients_and_worked_cases(self):
        score = self.values["scoring"]
        self.assertEqual(score["citizens"], {"perCitizen": 1, "perHappyCitizen": 1, "perUnhappyCitizen": -1})
        self.assertEqual(score["wonderPoints"], 20)
        self.assertEqual(score["futureTechnologyPoints"], 5)
        self.assertEqual(score["spaceshipPointsPerPopulationSuccess"] * 2 * 75, 150)
        self.assertEqual((2 - 5) * score["pollutionPointsPerTile"], 30)
        self.assertEqual(score["peace"]["firstEligibleTurn"], 200)
        self.assertEqual(min(40 * score["peace"]["pointsPerTurn"], score["peace"]["maximumPoints"]), 100)
        self.assertEqual(score["barbarianActivity"], {"pointsPerLevel": 25, "offsetPoints": -50})
        conquest = score["conquest"]
        end_turn = max(1, conquest["noRestartMinimumTurn"])
        value = conquest["pointsPerEnemy"] * 6 + conquest["pointsPerRemainingTurn"] * (conquest["turnReference"] - end_turn) + conquest["basePoints"]
        self.assertEqual(value, 1640)
        for key in ("largeMapMultiplier", "smallMapMultiplier"):
            ratio = conquest[key]
            value = value * ratio["numerator"] // ratio["denominator"]
        self.assertEqual(value, 1640)
        self.assertEqual(score["objectiveValue"], {"city": 1, "wonder": 1})
        scenario = score["scenario"]
        self.assertEqual(scenario["outcomePoints"], {"decisive-victory": 1000, "marginal-victory": 750, "draw": 500, "marginal-defeat": 250, "decisive-defeat": 0})
        self.assertEqual((scenario["outcomePoints"]["draw"] + scenario["pointsPerObjective"] * 3) * scenario["conquestMultiplier"], 1060)
        self.assertEqual(score["rating"]["difficultyFactors"], {"chieftain": 4, "warlord": 5, "prince": 6, "king": 8, "emperor": 10, "deity": 13})

    def test_all_new_documents_reject_unknown_root_fields(self):
        documents = [*self.values.values(), self.baseline["manifest"], self.ledger]
        for document in documents:
            with self.subTest(format=document["format"]):
                invalid = deepcopy(document)
                invalid["unregistered"] = True
                self.assertTrue(self.schemas.errors(invalid))

    def test_missing_named_identity_unknown_nested_key_and_fixed_group(self):
        document = deepcopy(self.values["governments"])
        document["governments"].pop("monarchy")
        self.assertTrue(self.schemas.errors(document))
        document = deepcopy(self.values["difficulty"])
        document["difficulties"]["deity"]["classicIndex"] = 5
        self.assertTrue(self.schemas.errors(document))
        document = deepcopy(self.values["spaceship"])
        document["parts"]["fuel"]["group"] = "module"
        self.assertTrue(self.schemas.errors(document))

    def test_numeric_domains_and_exact_units(self):
        for component, path, value in (
            ("spaceship", ["parts", "structural", "massTons"], 101),
            ("spaceship", ["parts", "structural", "maximumCount"], 32768),
            ("governments", ["governments", "monarchy", "corruptionDivisor"], 0),
            ("governments", ["governments", "democracy", "maximumRateTenths"], 11),
            ("difficulty", ["difficulties", "king", "aiBoxFactor"], True),
            ("combat", ["strengthBonuses", "veteran", "denominator"], 0),
        ):
            with self.subTest(component=component, path=path):
                document = deepcopy(self.values[component])
                target = document
                for key in path[:-1]:
                    target = target[key]
                target[path[-1]] = value
                self.replace_component(component, document)
                with self.assertRaises(ValueError):
                    self.load()
                self.replace_component(component, self.values[component])

    def test_semantic_period_and_defense_constraints(self):
        for component, mutate in (
            ("calendar", lambda d: d["profiles"]["prince"]["periods"][1].update(startYear=-999)),
            ("calendar", lambda d: d["retirement"].update(warningYear=2021)),
            ("combat", lambda d: d["defenseMultipliers"].update(fortress={"numerator": 1, "denominator": 3})),
        ):
            document = deepcopy(self.values[component])
            mutate(document)
            self.assertFalse(self.schemas.errors(document))
            self.replace_component(component, document)
            with self.assertRaises(ValueError):
                self.load()
            self.replace_component(component, self.values[component])

    def test_custom_change_for_every_component_requires_explicit_mode_and_fails_classic_gate(self):
        changes = {
            "calendar": lambda d: d["retirement"].update(endYear=2030),
            "spaceship": lambda d: d["parts"]["structural"].update(massTons=200),
            "governments": lambda d: d["governments"]["despotism"].update(maximumRateTenths=7),
            "difficulty": lambda d: d["difficulties"]["deity"].update(aiBoxFactor=7),
            "combat": lambda d: d["antiAirMultipliers"].update(ordinaryAircraft=4),
            "scoring": lambda d: d.update(wonderPoints=21),
        }
        for name, mutate in changes.items():
            with self.subTest(component=name):
                document = deepcopy(self.values[name])
                mutate(document)
                self.replace_component(name, document)
                with self.assertRaisesRegex(ValueError, "require custom mode"):
                    self.load()
                self.set_mode("custom")
                result = self.load()
                self.assertFalse(result["classicValues"])
                self.assertNotEqual(result["semanticSha256"], self.fingerprint)
                with self.assertRaisesRegex(ValueError, "classic-values gate"):
                    self.load(True)
                self.replace_component(name, self.values[name])
                self.set_mode("classic")

    def test_semantic_digest_ignores_metadata_key_order_and_equivalent_number_spelling(self):
        document = deepcopy(self.values["combat"])
        document["strengthBonuses"]["veteran"] = {"numerator": 2, "denominator": 4}
        document["extensions"] = {"org.example.notes": {"anything": "metadata"}}
        raw = json.dumps(document, sort_keys=True, indent=4).replace('"numerator": 2,', '"numerator": 2.0,').encode()
        self.replace_component("combat", raw=raw)
        result = self.load(True)
        self.assertEqual(result["semanticSha256"], self.fingerprint)
        self.assertEqual(result["components"]["combat"]["strengthBonuses"]["veteran"], {"numerator": 1, "denominator": 2})

    def test_manifest_label_cannot_override_changed_values(self):
        document = deepcopy(self.values["scoring"])
        document["wonderPoints"] = 99
        self.replace_component("scoring", document)
        self.set_mode("custom")
        self.assertFalse(self.load()["classicValues"])  # ID/revision still claim the stock name.

    def test_file_integrity_and_wrong_component_kind(self):
        path = self.bundle / "scoring.json"
        path.write_bytes(path.read_bytes() + b" ")
        with self.assertRaisesRegex(ValueError, "hash mismatch"):
            self.load()
        self.replace_component("scoring", self.values["combat"])
        with self.assertRaisesRegex(ValueError, "expected document format"):
            self.load()

    def test_hash_checked_bytes_are_strictly_decoded(self):
        self.replace_component("scoring", raw=b'{"format":"openciv2-scoring/1","a":1,"a":2}')
        with self.assertRaisesRegex(ValueError, "duplicate"):
            self.load()

    def test_path_escape_and_symlink_escape(self):
        manifest = gameplay.read_json(self.bundle / "gameplay.json")
        manifest["components"]["scoring"]["path"] = "../scoring.json"
        self.write_manifest(manifest)
        with self.assertRaises(ValueError):
            self.load()
        manifest["components"]["scoring"]["path"] = "scoring.json"
        self.write_manifest(manifest)
        path = self.bundle / "scoring.json"
        outside = self.directory / "outside.json"
        path.rename(outside)
        try:
            path.symlink_to(outside)
        except OSError as exc:
            self.skipTest(f"symlink creation unavailable: {exc}")
        with self.assertRaisesRegex(ValueError, "leaves selected root"):
            self.load()

    def test_content_pack_selector_and_overlay_boundary(self):
        result = gameplay.load_from_content_pack(ROOT / "docs/content/examples/content.json", ROOT, self.schemas, self.ledger, True)
        self.assertTrue(result["classicValues"])
        pack = gameplay.read_json(ROOT / "docs/content/examples/content.json")
        raw = (self.bundle / "gameplay.json").read_bytes()
        pack["gameplay"] = {"path": "bundle/gameplay.json", "sha256": gameplay.digest(raw)}
        path = self.directory / "content.json"
        path.write_text(json.dumps(pack), encoding="utf-8")
        self.assertTrue(gameplay.load_from_content_pack(path, ROOT, self.schemas, self.ledger)["classicValues"])
        pack["base"] = {"id": "parent", "revision": 1, "sha256": "0" * 64}
        path.write_text(json.dumps(pack), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "inheritance"):
            gameplay.load_from_content_pack(path, ROOT, self.schemas, self.ledger)
        pack["base"] = None
        pack.pop("gameplay")
        self.assertTrue(self.schemas.errors(pack))

    def test_provenance_drift_and_missing_field_coverage(self):
        root = self.directory / "repo"
        for relative in ("data/gameplay/classic-mge-5.4.0f", "docs/content/schemas"):
            shutil.copytree(ROOT / relative, root / relative)
        ledger_path = root / gameplay.LEDGER
        shutil.copy2(ROOT / gameplay.LEDGER, ledger_path)
        for spec in self.ledger["specifications"]:
            path = root / spec["path"]
            path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(ROOT / spec["path"], path)
        gameplay.check_repository(root, self.schemas)
        ledger = deepcopy(self.ledger)
        ledger["bindings"] = [b for b in ledger["bindings"] if b["pointer"] != "/wonderPoints"]
        ledger_path.write_text(json.dumps(ledger), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "missing source bindings"):
            gameplay.check_repository(root, self.schemas)
        ledger_path.write_text(json.dumps(self.ledger), encoding="utf-8")
        source = root / self.ledger["specifications"][0]["path"]
        source.write_bytes(source.read_bytes() + b"\n")
        with self.assertRaisesRegex(ValueError, "source pin changed"):
            gameplay.check_repository(root, self.schemas)

    def test_offline_loading_and_cli_exit_codes(self):
        with patch("socket.socket", side_effect=AssertionError("network forbidden")), redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            self.assertTrue(self.load()["classicValues"])
            self.assertEqual(gameplay.main(["--root", str(ROOT)]), 0)
            self.assertEqual(gameplay.main(["--root", str(ROOT), "--manifest", str(self.directory / "missing.json")]), 1)


if __name__ == "__main__":
    unittest.main()
