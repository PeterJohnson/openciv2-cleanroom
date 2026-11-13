"""Authored document-shape cases; these are not pack/gameplay conformance tests."""

from contextlib import redirect_stderr, redirect_stdout
from copy import deepcopy
from decimal import Decimal
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
import check_content_schemas as content


OPINIONS = {
    "military": "unit-shortage land-capability-lag naval-capability-lag no-barracks undefended-capital ready peaceful-power".split(),
    "science": "science-lag low-science-rate research-building-shortage trade-shortage competitive-research reserved science-lead".split(),
    "economic": "treasury-deficit economic-technology-needed economic-building-shortage trade-technology-needed trade-capacity-shortage low-surplus healthy-surplus".split(),
    "foreign": "no-contact alliance-opportunity diplomatic-technology-needed poor-relations intelligence-needed seek-exchanges relations-established".split(),
    "domestic": "democratic-disorder luxury-spending-needed unhappiness balanced-without-luxuries content celebrating".split(),
}


def example(name):
    return content.read_json(ROOT / "docs/content/examples" / name)


def rules():
    """Synthetic table shapes; deliberately not a playable identity mapping."""
    result = example("rules.fragment.json")
    counts = {
        "advances": 100, "improvements": 67, "units": 62, "terrain": 33,
        "governments": 7, "tribes": 21, "commodities": 16, "orders": 12,
        "difficulties": 6, "diplomaticAttitudes": 9, "wonderObsolescence": 28,
    }
    for key, count in counts.items():
        source = result[key][0]
        result[key] = []
        for index in range(count):
            item = deepcopy(source)
            if "id" in item:
                item["id"] = f"test.{key.lower()}.{index}"
            if key == "terrain" and index >= 11:
                for field in ("irrigation", "mining", "transformation"):
                    item.pop(field)
            result[key].append(item)
    return result


def council():
    result = example("council.en.json")
    opinions = [
        {"advisor": advisor, "opinion": opinion, "opening": {"caption": "Opening"},
         "start": "finish", "dialogue": {"finish": {"kind": "end"}}}
        for advisor, names in OPINIONS.items() for opinion in names
    ]
    result["eras"] = [{"era": era, "opinions": deepcopy(opinions)} for era in ("early", "middle", "late")]
    return result


def event(trigger=None, actions=None):
    return {"format": "openciv2-events/1", "events": [{"id": "test.event", "once": False,
            "trigger": trigger or {"type": "scenarioLoaded"}, "actions": actions or {}}]}


class ContentSchemaTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.schemas = content.SchemaSet(ROOT / "docs/content/schemas")

    def valid(self, document):
        self.assertEqual([], self.schemas.errors(document))

    def invalid(self, document):
        self.assertTrue(self.schemas.errors(document))

    def test_repository_documents_and_exact_fragment_omissions(self):
        content.check_examples(self.schemas, ROOT)
        for name in ("rules.fragment.json", "council.en.json"):
            with self.subTest(name=name):
                self.invalid(example(name))

    def test_all_formats_reject_unknown_root_fields(self):
        documents = [example(path.name) for path in (ROOT / "docs/content/examples").glob("*.json")
                     if path.name not in ("rules.fragment.json", "council.en.json")]
        documents += [rules(), council(), content.read_json(ROOT / "docs/content/text-resources.json")]
        for document in documents:
            with self.subTest(format=document["format"]):
                self.valid(document)
                document["unregisteredField"] = True
                self.invalid(document)

    def test_unknown_versions_and_non_object_inputs(self):
        for document in ([], None, {"format": "openciv2-events/2"}, {"format": []}):
            with self.subTest(document=document), self.assertRaises(ValueError):
                self.schemas.errors(document)

    def test_root_manifest_requires_all_components_but_overlay_inherits(self):
        document = example("content.json")
        document["files"].pop("text")
        self.invalid(document)
        document["base"] = {"id": "base", "revision": 1, "sha256": "0" * 64}
        for key in ("compatibility", "scenarios", "resources"):
            document.pop(key)
        document["files"] = {}
        self.valid(document)
        document["revision"] = 0
        self.invalid(document)

    def test_manifest_descriptors_reject_path_escapes_and_malformed_hashes(self):
        for path in ("../rules.json", "a/../rules.json", "./rules.json", "/rules.json", "a//b",
                     "a/", "a\\b", "C:/rules.json", "https://host/a", "a\n.json", ""):
            with self.subTest(path=path):
                document = example("content.json")
                document["files"]["rules"]["path"] = path
                self.invalid(document)
        for digest in ("0" * 63, "A" * 64, "0" * 64 + "\n"):
            document = example("content.json")
            document["files"]["rules"]["sha256"] = digest
            self.invalid(document)
        document = example("content.json")
        document["files"]["rules"]["path"] = "data/règles.json"
        self.valid(document)

    def test_extensions_are_namespaced_metadata(self):
        document = event()
        document["extensions"] = {"org.example.notes": {"any": [None, 1, "text"]}}
        self.valid(document)
        document["extensions"] = {"unqualified": True}
        self.invalid(document)

    def test_complete_rules_and_required_table_coverage(self):
        document = rules()
        self.valid(document)
        for key in ("units", "advances", "wonderObsolescence"):
            for change in ("remove", "append"):
                with self.subTest(key=key, change=change):
                    invalid = deepcopy(document)
                    if change == "remove":
                        invalid[key].pop()
                    else:
                        invalid[key].append(deepcopy(invalid[key][0]))
                    self.invalid(invalid)
        document["globalRules"].pop("roadMovementMultiplier")
        self.invalid(document)

    def test_gameplay_integer_domain_and_significant_unit_conversion(self):
        document = rules()
        unit = document["units"][0]
        for value in (-(2**31), 2**31 - 1, Decimal("2.0")):
            unit["attack"] = value
            self.valid(document)
        for value in (-(2**31) - 1, 2**31, True, "2", Decimal("2.00000000000000000001")):
            unit["attack"] = value
            self.invalid(document)
        unit["attack"] = 1
        for value in (0, 30, 250):
            unit["hitPoints"] = value
            self.valid(document)
        for value in (-10, 3, 251, 260):
            unit["hitPoints"] = value
            self.invalid(document)

    def test_unit_enums_and_abilities_are_closed(self):
        for field, value in (("domain", "space"), ("role", "anything"),
                             ("abilities", ["alpine", "alpine"]), ("abilities", ["unknown"]),
                             ("classicIndex", 0)):
            with self.subTest(field=field):
                document = rules()
                document["units"][0][field] = value
                self.invalid(document)

    def test_reference_alternatives_and_base_terrain_fields(self):
        document = rules()
        document["advances"][0]["prerequisites"][0] = {"advance": "test.advance", "special": "none"}
        self.invalid(document)
        document = rules()
        document["terrain"][0].pop("mining")
        self.invalid(document)
        document = rules()
        document["terrain"][11] = deepcopy(document["terrain"][0])
        self.invalid(document)  # Twelve base-shaped records instead of eleven.

    def test_rational_denominator_positive_and_integer(self):
        for denominator in (0, -1, Decimal("1.5"), True):
            document = rules()
            document["terrain"][0]["defenseMultiplier"]["denominator"] = denominator
            self.invalid(document)

    def test_city_names_are_semantic_and_single_line(self):
        document = example("city-names.json")
        document["civilizations"][0]["cityNames"] = ["Same", "Same", "Unicode 城"]
        self.valid(document)
        for name in ("", "two\nlines", "two\rlines"):
            document["civilizations"][0]["cityNames"] = [name]
            self.invalid(document)
        document = example("city-names.json")
        document["sections"] = document.pop("civilizations")
        self.invalid(document)

    def test_city_preferences_preserve_duplicates_and_bound_count(self):
        document = example("city-preferences.json")
        document["buildPreferences"] = ["same"] * 32
        self.valid(document)
        document["buildPreferences"].append("same")
        self.invalid(document)

    def test_text_controls_have_typed_initials_and_exclusive_runs(self):
        document = example("text.en.json")
        message = document["messages"][0]
        message["inputs"] = [{"id": "name", "kind": "text", "label": [], "initial": {"literal": ""}, "maxLength": 10},
                             {"id": "enabled", "kind": "checkbox", "label": [], "initial": {"literal": True}}]
        self.valid(document)
        for index, value in ((0, False), (1, "yes")):
            invalid = deepcopy(document)
            invalid["messages"][0]["inputs"][index]["initial"]["literal"] = value
            self.invalid(invalid)
        message["inputs"][1]["maxLength"] = 10
        self.invalid(document)
        message["inputs"].pop()
        message["body"] = [{"text": "literal", "parameter": "name"}]
        self.invalid(document)

    def test_help_subjects_and_parameter_types(self):
        document = example("text.en.json")
        document["helpTopics"][0]["subject"] = {"globalRule": "notRegistered"}
        self.invalid(document)
        document = example("text.en.json")
        document["messages"][0]["parameters"] = {"value": "expression"}
        self.invalid(document)

    def test_recursive_menus_and_non_executable_entry_shape(self):
        document = {"format": "openciv2-menus/1", "locale": "en", "menus": [
            {"id": "menu", "label": "menu.label", "items": [
                {"id": "sub", "kind": "submenu", "label": "sub.label", "items": [
                    {"id": "action", "kind": "action", "action": "game.save", "label": "save.label"},
                    {"id": "separator", "kind": "separator"}]}]}]}
        self.valid(document)
        document["menus"][0]["items"][0]["items"][1]["action"] = "game.save"
        self.invalid(document)

    def test_document_block_alternatives(self):
        document = example("documents.en.json")
        document["credits"][0]["blocks"] = [{"kind": "heading", "text": ""}, {"kind": "text", "text": ""}, {"kind": "blank"}]
        self.valid(document)
        document["credits"][0]["blocks"][2]["text"] = "hidden"
        self.invalid(document)

    def test_complete_council_and_unique_era_opinion_coverage(self):
        document = council()
        self.valid(document)
        document["eras"][1]["era"] = "early"
        self.invalid(document)
        document = council()
        document["eras"][0]["opinions"][1] = deepcopy(document["eras"][0]["opinions"][0])
        self.invalid(document)

    def test_council_response_opinion_matches_its_advisor(self):
        document = council()
        dialogue = document["eras"][0]["opinions"][0]["dialogue"]
        dialogue["respond"] = {"kind": "response", "advisor": "science", "opinion": "science-lag",
                               "caption": None, "whenIncluded": "finish", "whenSkipped": "finish",
                               "clip": {"resource": "movie", "startFrame": 10, "endFrame": 0}}
        document["eras"][0]["opinions"][0]["start"] = "respond"
        self.valid(document)  # Reversed clip ranges have an explicit admission policy.
        dialogue["respond"]["opinion"] = "unit-shortage"
        self.invalid(document)
        dialogue["respond"]["opinion"] = None
        self.valid(document)
        dialogue["respond"]["accepted"] = True
        self.invalid(document)

    def test_encyclopedia_concept_and_rule_subjects(self):
        document = example("encyclopedia.en.json")
        concept = {"id": "concept", "kind": "concept", "subject": None, "title": "Concept", "body": "", "links": []}
        document["topics"] = [concept]
        self.valid(document)
        concept["body"] = None
        self.invalid(document)
        concept["kind"] = "unit"
        concept["subject"] = "unit.test"
        self.valid(document)
        concept["subject"] = None
        self.invalid(document)

    def test_all_event_trigger_forms_and_no_extra_payloads(self):
        civ = {"civilizationSlot": 1}
        triggers = [
            {"type": "unitKilled", "unit": "unit", "attacker": civ, "defender": civ},
            {"type": "cityTaken", "cityName": "City", "attacker": civ, "defender": civ},
            {"type": "turn", "turn": "every"}, {"type": "turnInterval", "interval": 0},
            {"type": "randomTurn", "denominator": 1000},
            {"type": "negotiation", "talker": civ, "talkerType": "human", "listener": civ, "listenerType": "either"},
            {"type": "scenarioLoaded"}, {"type": "noSchism", "defender": civ},
            {"type": "receivedTechnology", "receiver": civ, "technology": "advance"},
        ]
        for trigger in triggers:
            with self.subTest(type=trigger["type"]):
                self.valid(event(trigger))
                self.invalid(event({**trigger, "unregistered": True}))
        for denominator in (0, 1001):
            self.invalid(event({"type": "randomTurn", "denominator": denominator}))
        self.invalid(event({"type": "turn", "turn": -1}))

    def test_all_event_actions_and_structural_bounds(self):
        civ = {"civilizationSlot": 1}
        rectangle = [[0, 0], [4, 0], [4, 4], [0, 4]]
        actions = {
            "playSound": {"sound": "sound"}, "playMusic": {"track": 1},
            "createUnit": {"unit": "unit", "owner": civ, "veteran": False, "homeCityName": "", "locations": [[0, 0]]},
            "moveUnit": {"unit": {"special": "any-unit"}, "owner": civ, "rectangle": rectangle, "destination": [2, 2], "quantity": "all"},
            "changeTerrain": {"terrain": "terrain", "rectangle": rectangle},
            "makeAggression": {"who": civ, "whom": civ}, "changeTreasury": {"receiver": civ, "amount": -50},
            "destroyCivilization": {"whom": civ}, "grantTechnology": {"receiver": civ, "technology": "advance"},
            "showText": {"lines": [""] * 20}, "suppressWonderMovies": True,
        }
        self.valid(event(actions=actions))
        for key, field, value in (("playMusic", "track", 0), ("createUnit", "locations", []),
                                  ("createUnit", "locations", [[0, 0]] * 11), ("moveUnit", "rectangle", [[0, 0]] * 3),
                                  ("moveUnit", "quantity", -1), ("showText", "lines", [""] * 21)):
            with self.subTest(key=key, field=field):
                invalid = deepcopy(actions)
                invalid[key][field] = value
                self.invalid(event(actions=invalid))
        self.invalid(event(actions={"suppressWonderMovies": False}))

    def test_civilization_selectors_do_not_accept_tribe_ids(self):
        for selector in ({"civilizationSlot": 8}, {"civilizationSlot": -1}, {"tribe": "riverfolk"},
                         {"civilizationSlot": 1, "special": "anybody"}):
            self.invalid(event(actions={"changeTreasury": {"receiver": selector, "amount": 0}}))
        for special in ("anybody", "trigger-attacker", "trigger-defender"):
            self.valid(event(actions={"changeTreasury": {"receiver": {"special": special}, "amount": 0}}))

    def test_compatibility_metadata_has_explicit_slot_domains(self):
        document = example("classic-compatibility.json")
        document["ruleSlots"] = {"orders": [{"id": "order", "slot": 12}], "units": [{"id": "unit", "slot": 61}]}
        self.valid(document)
        for collection, slot in (("orders", 0), ("orders", 13), ("units", 62)):
            invalid = deepcopy(document)
            invalid["ruleSlots"][collection][0]["slot"] = slot
            self.invalid(invalid)
        document["retainedUnitFlags"] = [{"unit": "unit", "positions": [14]}]
        self.invalid(document)

    def test_inventory_dispositions_and_hashes(self):
        document = content.read_json(ROOT / "docs/content/text-resources.json")
        self.valid(document)
        record = document["files"][0]
        record["disposition"] = "documentation-only"
        record["nativeComponent"] = "runtime"
        self.invalid(document)
        record["nativeComponent"] = None
        self.valid(document)
        record["sha256"] = "wrong"
        self.invalid(document)

    def test_structural_validation_does_not_claim_reference_or_graph_validation(self):
        document = council()
        document["eras"][0]["opinions"][0]["start"] = "missing.step"
        self.valid(document)  # Loader must reject this dangling reference later.
        document = rules()
        document["terrain"][0]["defenseMultiplier"] = {"numerator": 1, "denominator": 3}
        self.valid(document)  # Ratio is not representable in classic half-units.

    def test_exact_json_numbers_and_invalid_lexical_inputs(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "input.json"
            path.write_text('{"a":2.0,"b":2e0,"c":2.00000000000000000001}', encoding="utf-8")
            values = content.read_json(path)
            self.assertTrue(content.is_integer(None, values["a"]))
            self.assertTrue(content.is_integer(None, values["b"]))
            self.assertFalse(content.is_integer(None, values["c"]))
            self.assertFalse(content.is_integer(None, True))
            for raw in (b'{"a":1,"a":2}', b'{"a":NaN}', b'{"a":Infinity}', b'{"a":"\xff"}',
                        b'{"a":"\\ud800"}', b'{"\\udfff":0}'):
                with self.subTest(raw=raw):
                    path.write_bytes(raw)
                    with self.assertRaises(ValueError):
                        content.read_json(path)
            path.write_bytes(b'{"a":"\\ud83d\\ude00"}')
            self.assertEqual(content.read_json(path)["a"], "😀")

    def test_all_references_are_preloaded_and_unused_broken_reference_fails(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary) / "schemas"
            shutil.copytree(ROOT / "docs/content/schemas", directory)
            with patch("socket.socket", side_effect=AssertionError("network forbidden")):
                schemas = content.SchemaSet(directory)
                self.assertEqual([], schemas.errors(event()))
                path = directory / "events.schema.json"
                schema = json.loads(path.read_text(encoding="utf-8"))
                schema["$defs"]["unused"] = {"$ref": "https://example.invalid/missing.schema.json"}
                path.write_text(json.dumps(schema), encoding="utf-8")
                with self.assertRaisesRegex(ValueError, "unresolved schema reference"):
                    content.SchemaSet(directory)

    def test_cli_examples_mode_and_requested_fragment_rejection(self):
        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            self.assertEqual(content.main(["--root", str(ROOT)]), 0)
            self.assertEqual(content.main([str(ROOT / "docs/content/examples/events.json")]), 0)
            self.assertEqual(content.main([str(ROOT / "docs/content/examples/rules.fragment.json")]), 1)
            self.assertEqual(content.main([str(ROOT / "missing.json")]), 1)


if __name__ == "__main__":
    unittest.main()
