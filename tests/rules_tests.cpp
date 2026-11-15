// F03 sections 3-7 and native data-format.md section 4 / Appendix B.
#include "rule_fixture.hpp"
#include "formats/classic_text.hpp"
#include "rules/normalization.hpp"
#include <catch2/catch_test_macros.hpp>
#include <algorithm>
#include <limits>

using namespace openciv;
using namespace formats::content;
using formats::decode_windows1252;
using formats::encode_windows1252;
using rule_fixture::Input;
using rule_fixture::json;

TEST_CASE("Windows1252 text conversion is exact and rejects undefined or malformed input",
          "[content][rules][encoding]") {
    const std::string utf8 = "Caf\xc3\xa9 \xe2\x82\xac \xc5\x92 \xe2\x80\x94";
    const std::string encoded = "Caf\xe9 \x80 \x8c \x97";
    REQUIRE(encode_windows1252(utf8, "text") == encoded);
    REQUIRE(decode_windows1252(encoded, "text") == utf8);
    for (int byte = 0; byte <= 255; ++byte) {
        const auto raw = std::string(1, static_cast<char>(byte));
        if (byte == 0x81 || byte == 0x8d || byte == 0x8f || byte == 0x90 || byte == 0x9d)
            REQUIRE_THROWS_AS(decode_windows1252(raw, "text"), Error);
        else
            REQUIRE(encode_windows1252(decode_windows1252(raw, "text"), "text") == raw);
    }
    for (const auto bad : {"\xc0\x80", "\xed\xa0\x80", "\xf4\x90\x80\x80", "\xe2\x82", "\x80",
                           "\xf0\x9f\x98\x80", "\xc2\x81"})
        REQUIRE_THROWS_AS(encode_windows1252(bad, "text"), Error);
}
TEST_CASE("Native complete rules bind explicit identities independently of array order",
          "[content][rules]") {
    Input input;
    const auto baseline = input.load();
    const auto projection = rule_fixture::installed(baseline->input);
    const auto& r = *projection;
    REQUIRE(r.advances.size() == 100);
    REQUIRE(r.enabled_advance_count == 100);
    REQUIRE(r.units[0].movement == 6);
    REQUIRE(r.units[0].hit_points == 30);
    REQUIRE(r.terrain[0].defense_factor == 3);
    REQUIRE(r.terrain[0].work->transformation == state::TerrainResult{0});
    REQUIRE_FALSE(r.terrain[11].work);
    REQUIRE(r.cosmic[state::CosmicParameter::food_box_rows] == 6);
    REQUIRE(r.cosmic[state::CosmicParameter::shield_box_rows] == 5);
    REQUIRE(baseline->input.government_advances[2] == state::AdvanceReference{54});
    REQUIRE(baseline->input.government_advances[3] == state::AdvanceReference{15});
    REQUIRE(baseline->input.government_advances[4] == state::AdvanceReference{31});
    REQUIRE(baseline->input.government_advances[5] == state::AdvanceReference{71});
    REQUIRE(baseline->input.government_advances[6] == state::AdvanceReference{21});
    REQUIRE_FALSE(baseline->input.government_advances[0]);
    for (auto& [key, value] : input.rules.getObject()) {
        (void)key;
        if (value.isArray())
            std::reverse(value.getArray().begin(), value.getArray().end());
    }
    REQUIRE(*rule_fixture::installed(input.load()->input) == r);
    input.rules["units"][0]["name"] = jt::Json("Renamed");
    const auto renamed = input.load();
    REQUIRE(renamed->input.definitions.units[61].identity.name.utf8 == "Renamed");
    REQUIRE(renamed->input.definitions.units[61].identity.id == "units.authored.61");
    REQUIRE(baseline->input.definitions.units[61].identity.name.utf8 == "units 61");
}
TEST_CASE("F03 normalization retains wrapping signed views and distinct scaling",
          "[content][rules]") {
    // Every F03 section 4 clamp, including signed-token extremes.
    std::array<std::int32_t, 22> extremes;
    extremes.fill(std::numeric_limits<std::int32_t>::min());
    const std::array<std::uint8_t, 22> minimum{1, 1, 0, 4, 4, 0, 0, 4, 10, 4,  4,
                                               3, 5, 0, 0, 0, 1, 0, 0, 4,  25, 0};
    REQUIRE(rules::normalize_cosmic(extremes).values == minimum);
    extremes.fill(std::numeric_limits<std::int32_t>::max());
    const std::array<std::uint8_t, 22> maximum{10, 100, 10, 20, 20, 10, 10,  12,  100, 50,  50,
                                               10, 100, 8,  8,  8,  20, 100, 100, 100, 200, 10};
    REQUIRE(rules::normalize_cosmic(extremes).values == maximum);
    REQUIRE(rules::normalize_unit_cost(-1) == 1);
    REQUIRE(rules::normalize_unit_cost(201) == 200);
    REQUIRE(rules::normalize_leader_color(32768) == 1);
    REQUIRE(rules::normalize_leader_style(32767) == 3);
    Input input;
    input.rules["globalRules"]["roadMovementMultiplier"] = jt::Json(30);
    input.rules["globalRules"]["foodBoxRows"] = jt::Json(19);
    input.rules["globalRules"]["shieldBoxRows"] = jt::Json(19);
    input.rules["advances"][0]["aiValue"] = jt::Json(128);
    input.rules["advances"][0]["civilizedAiModifier"] = jt::Json(255);
    input.rules["improvements"][0]["costRows"] = jt::Json(256);
    input.rules["improvements"][0]["upkeep"] = jt::Json(-1);
    input.rules["units"][0]["movementPoints"] = jt::Json(30);
    input.rules["units"][0]["hitPoints"] = jt::Json(250);
    input.rules["units"][0]["costRows"] = jt::Json(200);
    input.rules["units"][0]["attack"] = jt::Json(300);
    input.rules["units"][0]["defense"] = jt::Json(128);
    input.rules["units"][0]["abilities"] = json(R"(["detect-submarines","see-two"])");
    input.compatibility["retainedUnitFlags"] =
        json(R"([{"unit":"units.authored.0","positions":[15]}])");
    input.rules["terrain"][0]["food"] = jt::Json(-1);
    input.rules["tribes"][0]["preferredColor"] = jt::Json(65539);
    input.rules["tribes"][0]["cityStyle"] = jt::Json(65538);
    input.rules["tribes"][0]["personality"]["aggression"] = jt::Json(-129);
    const auto loaded = input.load();
    const auto projection = rule_fixture::installed(loaded->input);
    const auto& r = *projection;
    REQUIRE(r.cosmic[state::CosmicParameter::road_movement_multiplier] == 10);
    REQUIRE(r.cosmic[state::CosmicParameter::food_box_rows] == 20);
    REQUIRE(r.cosmic[state::CosmicParameter::shield_box_rows] == 19);
    REQUIRE(r.advances[0].ai_value == -128);
    REQUIRE(r.advances[0].civilized_ai_modifier == -1);
    REQUIRE(r.improvements[0].cost_rows == 0);
    REQUIRE(r.improvements[0].upkeep == 255);
    REQUIRE(r.units[0].movement == 44);
    REQUIRE(r.units[0].hit_points == 250);
    REQUIRE(r.units[0].cost_rows == 200);
    REQUIRE(rules::signed8(r.units[0].cost_rows) == -56);
    REQUIRE(r.units[0].attack == 44);
    REQUIRE(r.units[0].defense == 128);
    REQUIRE(r.units[0].abilities == 0xc001);
    REQUIRE(r.terrain[0].food == 255);
    REQUIRE(r.tribes[0].preferred_color == 3);
    REQUIRE(r.tribes[0].city_style == 2);
    REQUIRE(r.tribes[0].personality[0] == 127);
    REQUIRE(rules::normalize_classic_hp(3) == 30);
    REQUIRE(rules::normalize_classic_hp(26) == 4);
    REQUIRE(rules::normalize_movement(-1, 3) == 253);
}
TEST_CASE("Native rule integer tokens are admitted without floating point rounding",
          "[content][rules]") {
    Input input;
    input.rules["units"][0]["movementPoints"] = jt::Json("operand");
    const auto raw = input.rules.toString();
    const auto offset = raw.find("\"operand\"");
    REQUIRE(offset != std::string::npos);
    const auto load_token = [&](std::string_view token) {
        auto changed = raw;
        changed.replace(offset, 9, token);
        auto pack = input.pack();
        pack.components.at("rules").json = parse_json(changed, "rules.json");
        return load_rules(pack);
    };
    REQUIRE(rule_fixture::installed(load_token("2.000e0")->input)->units[0].movement == 6);
    REQUIRE(rule_fixture::installed(load_token("-2147483648")->input)->units[0].movement == 0);
    REQUIRE_THROWS_AS(load_token("2.000000000000000000001"), Error);
    REQUIRE_THROWS_AS(load_token("2147483648"), Error);
}
TEST_CASE(
    "F03 enabled advance fixup preserves original flags and unmodified expiry and unit references",
    "[content][rules]") {
    Input input;
    input.rules["advances"][1]["prerequisites"][0] = json(R"({"special":"never"})");
    input.rules["advances"][0]["prerequisites"] =
        json(R"([{"advance":"advances.authored.1"},{"special":"never"}])");
    input.rules["advances"][2]["prerequisites"][0] = json(R"({"advance":"advances.authored.3"})");
    input.rules["advances"][3]["prerequisites"][0] = json(R"({"advance":"advances.authored.2"})");
    input.rules["improvements"][1]["prerequisite"] = json(R"({"advance":"advances.authored.1"})");
    input.rules["units"][0]["prerequisite"] = json(R"({"advance":"advances.authored.1"})");
    input.rules["wonderObsolescence"][0]["advance"] = json(R"({"advance":"advances.authored.1"})");
    const auto loaded = input.load();
    const auto projection = rule_fixture::installed(loaded->input);
    const auto& r = *projection;
    REQUIRE(r.enabled_advance_count == 99);
    REQUIRE(r.advances[0].enabled);
    REQUIRE(r.advances[0].prerequisites[0] == state::AdvanceReference::never);
    REQUIRE_FALSE(r.advances[1].enabled);
    REQUIRE(r.advances[2].prerequisites[0] == state::AdvanceReference{3});
    REQUIRE(r.advances[3].prerequisites[0] == state::AdvanceReference{2});
    REQUIRE(r.improvements[1].prerequisite == state::AdvanceReference::never);
    REQUIRE(r.units[0].prerequisite == state::AdvanceReference{1});
    REQUIRE(r.wonder_obsolescence[0] == state::AdvanceReference{1});
}
TEST_CASE("Native rule titles text and presentation table identities preserve their distinctions",
          "[content][rules]") {
    Input input;
    input.rules["units"][0]["name"] = jt::Json("");
    input.rules["improvements"][1]["name"] = jt::Json("Caf\xc3\xa9");
    input.rules["orders"][0]["name"] = jt::Json("");
    input.rules["orders"][0]["shortcutHint"] = jt::Json("\xc5\x92");
    input.rules["difficulties"][0]["name"] = jt::Json("First, second; kept");
    input.rules["tribes"][0]["governmentTitles"] = json(
        R"([{"government":"governments.authored.1","male":"First","female":"First"},{"government":"governments.authored.1","male":"Last","female":""}])");
    const auto loaded = input.load();
    const auto projection = rule_fixture::installed(loaded->input);
    const auto& r = *projection;
    REQUIRE(r.units[0].identity.name.utf8 == " ");
    REQUIRE(r.improvements[1].identity.name.classic == "Caf\xe9");
    REQUIRE(r.orders[0].identity.name.utf8.empty());
    REQUIRE(r.orders[0].shortcut == 0x8c);
    REQUIRE(r.difficulties[0].name.utf8 == "First, second; kept");
    REQUIRE(r.tribes[0].titles[0] == r.governments[0].titles);
    REQUIRE(r.tribes[0].titles[1].male.utf8 == "Last");
    REQUIRE(r.tribes[0].titles[1].female.utf8.empty());
}
TEST_CASE(
    "Native rules reject incomplete identities unresolved references and unrepresentable operands",
    "[content][rules]") {
    const std::vector<std::function<void(Input&)>> changes{
        [](auto& i) { i.rules["advances"].getArray().pop_back(); },
        [](auto& i) { i.rules["units"][1]["id"] = i.rules["units"][0]["id"]; },
        [](auto& i) { i.compatibility["ruleSlots"]["units"].getArray().pop_back(); },
        [](auto& i) { i.compatibility["ruleSlots"]["units"][1]["slot"] = jt::Json(0); },
        [](auto& i) { i.compatibility["ruleSlots"]["orders"][0]["slot"] = jt::Json(0); },
        [](auto& i) { i.rules["units"][0]["hitPoints"] = jt::Json(31); },
        [](auto& i) { i.rules["units"][0]["hitPoints"] = jt::Json(260); },
        [](auto& i) { i.rules["units"][0]["domain"] = jt::Json("unknown"); },
        [](auto& i) { i.rules["units"][0]["movementPoints"] = jt::Json(false); },
        [](auto& i) { i.rules["units"][0]["abilities"] = json(R"(["see-two","see-two"])"); },
        [](auto& i) { i.rules["units"][0]["prerequisite"] = json(R"({"advance":"missing"})"); },
        [](auto& i) {
            i.rules["wonderObsolescence"][0]["wonder"] = jt::Json("improvements.authored.1");
        },
        [](auto& i) { i.rules["terrain"][0]["defenseMultiplier"]["denominator"] = jt::Json(5); },
        [](auto& i) {
            i.rules["terrain"][0]["defenseMultiplier"] =
                json(R"({"numerator":128,"denominator":1})");
        },
        [](auto& i) {
            i.rules["terrain"][0]["transformation"] = json(R"({"terrain":"terrain.authored.11"})");
        },
        [](auto& i) {
            i.rules["terrain"][11]["transformation"] = json(R"({"special":"no-result"})");
        },
        [](auto& i) {
            i.rules["tribes"][0]["governmentTitles"] =
                json(R"([{"government":"governments.authored.0","male":"X","female":"Y"}])");
        },
        [](auto& i) { i.rules["units"][0]["name"] = jt::Json(" leading"); },
        [](auto& i) { i.rules["units"][0]["name"] = jt::Json("with,comma"); },
        [](auto& i) { i.rules["advances"][0]["name"] = jt::Json(";termination"); },
        [](auto& i) { i.rules["tribes"][0]["pluralName"] = jt::Json("\xf0\x9f\x98\x80"); },
        [](auto& i) { i.rules["orders"][0]["shortcutHint"] = jt::Json("ab"); },
        [](auto& i) { i.rules["units"][0]["name"] = jt::Json(std::string(255, 'x')); },
        [](auto& i) { i.rules["units"][0]["extra"] = jt::Json(1); },
        [](auto& i) {
            i.compatibility["retainedUnitFlags"] =
                json(R"([{"unit":"units.authored.0","positions":[14]}])");
        }};
    for (std::size_t n = 0; n < changes.size(); ++n) {
        INFO(n);
        Input input;
        changes[n](input);
        REQUIRE_THROWS_AS(input.load(), Error);
    }
    Input input;
    auto altered = std::make_shared<GameplayBundle>(*input.gameplay);
    altered->values.governments[2].required_advance = "missing.advance";
    input.gameplay = altered;
    REQUIRE_THROWS_AS(input.load(), Error);
}
TEST_CASE("Registered gameplay advance identities cannot move but bind custom selected rule IDs",
          "[content][rules]") {
    Input input;
    input.rules["advances"][54]["id"] = jt::Json("advance.monarchy");
    auto& mappings = input.compatibility["ruleSlots"]["advances"].getArray();
    mappings.erase(mappings.begin() + 54);
    REQUIRE(input.load()->input.definitions.advances[54].identity.id == "advance.monarchy");
    auto mapping = json(R"({"id":"advance.monarchy","slot":53})");
    mappings.push_back(mapping);
    REQUIRE_THROWS_AS(input.load(), Error);
    input = Input{};
    auto altered = std::make_shared<GameplayBundle>(*input.gameplay);
    altered->values.governments[2].required_advance = "advances.authored.7";
    input.gameplay = altered;
    REQUIRE(input.load()->input.government_advances[2] == state::AdvanceReference{7});
}
