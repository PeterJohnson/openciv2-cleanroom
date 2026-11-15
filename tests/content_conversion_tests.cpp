#include <catch2/catch_test_macros.hpp>

import std;
import openciv.tests.rule_fixture;
import openciv.tests.classic_rule_fixture;
import openciv.formats.content_conversion;
import openciv.formats.city_preferences;
import openciv.formats.classic_city_preferences;
import openciv.formats.city_names;
import openciv.formats.classic_city_names;
import openciv.rules.rules_install;
import openciv.formats.classic_rules;
import openciv.rules.city_preferences;
import openciv.base.hash;

// M1-02/F03 section 8: authored classic input, native admission and retained effects.

using namespace openciv;
namespace content = formats::content;
namespace classic = formats::classic;
namespace {
content::Source source(std::string text) {
    return {"authored/CITYPREF.TXT", {text.begin(), text.end()}};
}
auto load(const content::ContentConversion& converted, content::ResolvedPack selected) {
    selected.components["cityPreferences"] = {
        nullptr, content::parse_json(converted.component, "converted")};
    return content::load_city_preferences(
        selected, content::load_rules(selected)->input.definitions, base::ascii_name_equal);
}
state::CityPreferences classic_result(const content::Source& input,
                                      const state::RuleDefinitions& rules, std::int32_t retained) {
    classic::TextWorkspace workspace;
    classic::TextDirectories directories;
    classic::TextReader reader(
        workspace, directories,
        {[&](auto, auto) -> classic::OpenedText {
             return {input.identity, std::make_unique<classic::MemoryTextStream>(
                                         std::string(input.bytes.begin(), input.bytes.end()))};
         },
         {}},
        classic::ascii_text_case());
    state::CityPreferences out;
    out.no_defend = retained;
    out.build_order.fill(97);
    rules::apply_city_preferences(
        classic::load_city_preferences(reader, rules, classic::ascii_text_case()), out);
    return out;
}
} // namespace
TEST_CASE("RULES conversion emits complete validated components with explicit slot identities",
          "[content][conversion]") {
    std::string text;
    for (const auto& section : classic_rule_fixture::complete_sections())
        text += section;
    auto input = source(text);
    input.identity = "authored/RULES.TXT";
    const auto converted = content::convert_rules(input, classic::ascii_text_case());
    content::ResolvedPack pack;
    pack.gameplay.bundle = content::builtin_gameplay();
    pack.components["rules"] = {nullptr, content::parse_json(converted.rules, "converted")};
    pack.compatibility =
        content::ResolvedDocument{nullptr, content::parse_json(converted.compatibility, "mapping")};
    const auto loaded = content::load_rules(pack);
    const auto authored = rule_fixture::Input{}.load();
    auto expected_projection = rule_fixture::installed(authored->input);
    auto& expected = *expected_projection;
    const auto identities = [](auto& to, const auto& from) {
        for (std::size_t i = 0; i < to.size(); ++i) {
            if constexpr (requires { to[i].identity.id; })
                to[i].identity.id = from[i].identity.id;
            else
                to[i].id = from[i].id;
        }
    };
    const auto actual_projection = rule_fixture::installed(loaded->input);
    const auto& actual = *actual_projection;
    identities(expected.advances, actual.advances);
    identities(expected.improvements, actual.improvements);
    identities(expected.units, actual.units);
    identities(expected.terrain, actual.terrain);
    identities(expected.governments, actual.governments);
    identities(expected.tribes, actual.tribes);
    identities(expected.commodities, actual.commodities);
    identities(expected.orders, actual.orders);
    identities(expected.difficulties, actual.difficulties);
    identities(expected.diplomatic_attitudes, actual.diplomatic_attitudes);
    CHECK(actual == expected);
    CHECK(loaded->input.definitions.cosmic == authored->input.definitions.cosmic);
    CHECK(rule_fixture::installed(loaded->input)->units[0].movement == 6);
    CHECK(loaded->input.movement_points[0] == 2);
    CHECK(loaded->input.definitions.units[0].hit_points == 30);
    CHECK(loaded->input.definitions.tribes[0].plural ==
          authored->input.definitions.tribes[0].plural);
    CHECK(loaded->input.definitions.advances[0].identity.id == "classic.advances.0");
    CHECK(loaded->input.definitions.orders[0].identity.id == "classic.orders.0");
    CHECK(converted.receipt["conversion"].getString() == "classic-rules/1");
    CHECK(converted.receipt["source"]["sha256"].getString() == base::sha256(input.bytes));
}

TEST_CASE("RULES conversion preserves reload operands and retained names",
          "[content][conversion]") {
    auto sections = classic_rule_fixture::complete_sections();
    sections[1] = "@CIVILIZE\nDisabled,3,1,no,nil,0,3\nDependent,257,1,AFl,nil,0,3\n; stop\n";
    sections[2] =
        "@IMPROVE\nCaf\xe9,266,257,AFl\n" + classic_rule_fixture::repeat("Building,10,1,nil", 66);
    sections[4] = "@UNITS\nWrapped,no,0,259,0,1,1,3,1,10,0,0,nil,1000000000000001\n; stop\n";
    sections[7] = "@LEADERS\n" +
                  classic_rule_fixture::repeat(
                      "Leader,Leader,1,1,0,Plural,Example,0,0,0,1,First,Lady,1,Last,Last Lady", 21);
    std::string text;
    for (const auto& s : sections)
        text += s;
    const auto converted = content::convert_rules(source(text), classic::ascii_text_case());
    content::ResolvedPack pack;
    pack.gameplay.bundle = content::builtin_gameplay();
    pack.components["rules"] = {nullptr, content::parse_json(converted.rules, "converted")};
    pack.compatibility =
        content::ResolvedDocument{nullptr, content::parse_json(converted.compatibility, "mapping")};
    const auto loaded = content::load_rules(pack);
    CHECK(loaded->input.definitions.improvements[0].identity.name.classic ==
          std::string("Caf\xe9"));
    CHECK(loaded->input.definitions.improvements[0].cost_rows == 10);
    CHECK(loaded->input.definitions.units[0].abilities == 0x8001);
    CHECK(loaded->input.definitions.units[1].identity.name.classic == "Unit 2");
    CHECK(loaded->input.movement_points[0] == 259);
    REQUIRE(loaded->input.title_overrides[0].size() == 2);
    CHECK(loaded->input.title_overrides[0][0].titles.male.classic == "First");
    auto original = std::make_unique<state::RuleState>(),
         native = std::make_unique<state::RuleState>();
    for (auto* target : {original.get(), native.get()}) {
        target->text.initialize();
        target->definitions = loaded->input.definitions;
        target->definitions.units[0].identity.name = {"Kept unit", "Kept unit"};
        target->definitions.improvements[0].identity.name = {"Kept building", "Kept building"};
        target->definitions.advances[1].identity.name = {"Kept advance", "Kept advance"};
    }
    classic::TextWorkspace workspace;
    classic::TextDirectories directories;
    classic::TextReader reader(workspace, directories,
                               {[&](auto, auto) -> classic::OpenedText {
                                    return {"authored",
                                            std::make_unique<classic::MemoryTextStream>(text)};
                                },
                                {}},
                               classic::ascii_text_case());
    classic_rule_fixture::InstalledRules classic_loader(reader, *original,
                                                        classic::ascii_text_case());
    rules::RuleInstaller native_loader(loaded->input, *native);
    REQUIRE(reader.open("RULES", "CIVILIZE"));
    classic_loader.advances(true);
    native_loader.advances(true);
    CHECK(native->definitions == original->definitions);
    for (auto* target : {original.get(), native.get()}) {
        target->definitions.advances[0].enabled = true;
        target->definitions.cosmic.values[0] = 5;
        target->definitions.governments[2].titles.male = {"Current", "Current"};
    }
    REQUIRE(reader.open("RULES", "IMPROVE"));
    classic_loader.improvements(true);
    native_loader.improvements(true);
    REQUIRE(reader.open("RULES", "UNITS"));
    classic_loader.units(true);
    native_loader.units(true);
    REQUIRE(reader.open("RULES", "LEADERS"));
    classic_loader.leaders(true);
    native_loader.leaders(true);
    CHECK(native->definitions == original->definitions);
    CHECK(native->definitions.units[0].movement == 15);
    CHECK(native->definitions.units[0].hit_points == 30);
    CHECK(native->definitions.improvements[0].prerequisite ==
          static_cast<state::AdvanceReference>(0));
    CHECK(native->definitions.tribes[0].titles[2].male.classic == "Current");
}

TEST_CASE("RULES conversion rejects unsupported codes and malformed references",
          "[content][conversion]") {
    for (const auto* row :
         {"Unit,no,3,2,0,1,1,3,1,10,0,0,nil,0", "Unit,no,0,2,0,1,1,26,1,10,0,0,nil,0",
          "Unit,unknown,0,2,0,1,1,3,1,10,0,0,nil,0"}) {
        auto sections = classic_rule_fixture::complete_sections();
        sections[4] = "@UNITS\n" + std::string(row) + "\n; stop\n";
        std::string text;
        for (const auto& s : sections)
            text += s;
        CHECK_THROWS_AS(content::convert_rules(source(text), classic::ascii_text_case()),
                        content::Error);
    }
    CHECK_THROWS_AS(content::convert_rules(source("@COSMIC\n"), classic::ascii_text_case()),
                    content::Error);
}

TEST_CASE("CITY conversion preserves classic name sequences across every cursor",
          "[content][conversion]") {
    rule_fixture::Input fixture;
    const auto selected = fixture.pack();
    for (int fallback = 0; fallback < 5; ++fallback) {
        const bool populated = fallback == 2 || fallback == 3;
        std::string text;
        for (int i = 0; i < 21; ++i)
            text += "@tribes " + std::to_string(i) + "\r\nFirst\r\nFirst\r\nCaf\xe9\r\n@STOP\r\n";
        if (fallback) {
            text += "@EXTRA\n";
            if (populated)
                text += "Extra One\nExtra Two\n";
            if (fallback < 3)
                text += "@STOP\n";
        }
        auto input = source(text);
        input.identity = "authored/CITY.TXT";
        const auto converted =
            content::convert_city_names(input, selected, classic::ascii_text_case());
        auto verification = selected;
        verification.components["cityNames"] = {
            nullptr, content::parse_json(converted.component, "converted")};
        const auto admitted = content::load_city_names(
            verification, fixture.load()->input.definitions, base::ascii_name_equal);
        CHECK(admitted->catalog.layers().front().fallback.has_value() == (fallback != 0));
        auto native = admitted->catalog;
        classic::TextWorkspace workspace;
        classic::TextDirectories directories;
        classic::TextReader reader(workspace, directories,
                                   {[&](auto, auto) -> classic::OpenedText {
                                        return {input.identity,
                                                std::make_unique<classic::MemoryTextStream>(text)};
                                    },
                                    {}},
                                   classic::ascii_text_case());
        auto original = classic::load_city_names(reader, directories, classic::ascii_text_case());
        for (int tribe = 0; tribe < 21; ++tribe) {
            const auto plural = "tribes " + std::to_string(tribe);
            for (unsigned cursor = 0; cursor < 256; ++cursor) {
                const auto a = original.select(plural, static_cast<std::uint8_t>(cursor));
                const auto b = native.select(plural, static_cast<std::uint8_t>(cursor));
                REQUIRE(a.outcome == b.outcome);
                REQUIRE(a.bytes == b.bytes);
                const auto expected = cursor == 0                ? "@" + plural
                                      : cursor <= 2              ? std::string("First")
                                      : cursor == 3              ? std::string("Caf\xe9")
                                      : populated && cursor == 4 ? std::string("Extra One")
                                      : populated && cursor == 5 ? std::string("Extra Two")
                                                                 : std::string{};
                REQUIRE(b.bytes == expected);
            }
        }
        CHECK(native.select("Missing", 1).outcome == city::NameOutcome::unavailable);
        for (const auto& list : admitted->catalog.layers().front().civilizations) {
            if (list.plural.classic != "STOP" && list.plural.classic != "EXTRA")
                continue;
            for (unsigned cursor = 0; cursor < 256; ++cursor) {
                const auto a =
                    original.select(list.plural.classic, static_cast<std::uint8_t>(cursor));
                const auto b =
                    native.select(list.plural.classic, static_cast<std::uint8_t>(cursor));
                REQUIRE(a.outcome == b.outcome);
                REQUIRE(a.bytes == b.bytes);
            }
        }
        CHECK(converted.receipt["source"]["sha256"].getString() == base::sha256(input.bytes));
        CHECK(converted.receipt["conversion"].getString() == "classic-city-names/1");
    }
}

TEST_CASE("CITY conversion diagnoses ambiguous selectors and unsupported input",
          "[content][conversion]") {
    rule_fixture::Input fixture;
    auto selected = fixture.pack();
    selected.manifests.resize(2);
    for (const auto* text :
         {"@Custom\nOne\n@STOP\n@CUSTOM\nTwo\n@STOP\n", "@Custom\nOne\n", "@Custom\n\x81\n@STOP\n",
          "@Custom\nOne\n@STOP\n@EXTRA\nOne\n@STOP\n@extra\nTwo\n@STOP\n"})
        CHECK_THROWS(
            content::convert_city_names(source(text), selected, classic::ascii_text_case()));
    CHECK_THROWS(content::convert_city_names(source("@Custom\nOne\n@STOP\n"), fixture.pack(),
                                             classic::ascii_text_case()));
    const auto input = source("@Custom\nOne\n@Other\nTwo\n@STOP\n");
    const auto converted = content::convert_city_names(input, selected, classic::ascii_text_case());
    const auto json = content::parse_json(converted.component, "converted").value;
    CHECK(json["civilizations"][0]["cityNames"].getArray().size() == 3);
    CHECK(json["civilizations"][0]["cityNames"][1].getString() == "@Other");
}

TEST_CASE("CITY conversion preserves reversible selectors and classic read boundaries",
          "[content][conversion]") {
    rule_fixture::Input fixture;
    auto selected = fixture.pack();
    selected.manifests.resize(2);
    const auto text = std::string("@Caf\xe9\r\n") + std::string(255, 'x') + "\r\n@STOP\r\n";
    const auto converted =
        content::convert_city_names(source(text), selected, classic::ascii_text_case());
    auto verification = selected;
    verification.components["cityNames"] = {nullptr,
                                            content::parse_json(converted.component, "converted")};
    const auto loaded = content::load_city_names(verification, fixture.load()->input.definitions,
                                                 base::ascii_name_equal);
    REQUIRE(loaded->catalog.layers().front().civilizations.size() == 1);
    CHECK(loaded->catalog.layers().front().civilizations[0].plural.classic ==
          std::string("Caf\xe9"));
    REQUIRE(loaded->catalog.layers().front().civilizations[0].names.size() == 2);
    CHECK(loaded->catalog.layers().front().civilizations[0].names[0].classic ==
          std::string(254, 'x'));
    CHECK(loaded->catalog.layers().front().civilizations[0].names[1].classic == "x");
    try {
        content::convert_city_names(source(text + "@CAF\xc9\nTwo\n@STOP\n"), selected,
                                    classic::ascii_text_case());
        FAIL("unknown non-ASCII equivalence must reject");
    } catch (const content::Error& error) {
        CHECK(error.code == "comparison-unavailable");
    }
}

TEST_CASE("CITYPREF conversion validates native output and preserves retained semantics",
          "[content][conversion]") {
    rule_fixture::Input fixture;
    const auto selected = fixture.pack();
    for (bool flag : {false, true}) {
        const auto input =
            source((flag ? std::string("@NODEFEND\r\n") : std::string{}) +
                   "@AUTOBUILD\r\nImPrOvEmEnTs 2,ignored\r\nimprovements 1\r\nimprovements 2\r\n");
        const auto before = input.bytes;
        const auto converted =
            content::convert_city_preferences(input, selected, classic::ascii_text_case());
        const auto native = load(converted, selected);
        CHECK(native->update.build_order == std::vector<std::uint8_t>{2, 1, 2});
        for (std::int32_t retained : {0, 7}) {
            state::CityPreferences actual;
            actual.no_defend = retained;
            actual.build_order.fill(97);
            rules::apply_city_preferences(native->update, actual);
            const auto expected =
                classic_result(input, fixture.load()->input.definitions, retained);
            CHECK(actual.count == expected.count);
            CHECK(actual.no_defend == expected.no_defend);
            CHECK(actual.build_order == expected.build_order);
        }
        CHECK(converted.receipt["source"]["sha256"].getString() == base::sha256(input.bytes));
        CHECK(converted.receipt["output"]["sha256"].getString() ==
              base::sha256({reinterpret_cast<const std::uint8_t*>(converted.component.data()),
                            converted.component.size()}));
        CHECK(converted.receipt["unsupportedFields"].getArray().empty());
        CHECK(converted.receipt["lossyFields"].getArray().empty());
        CHECK(input.bytes == before);
    }
}
TEST_CASE("CITYPREF conversion keeps first-match duplicates and nominal limits",
          "[content][conversion]") {
    rule_fixture::Input fixture;
    fixture.rules["improvements"][2]["name"] = fixture.rules["improvements"][1]["name"];
    const auto selected = fixture.pack();
    const auto converted =
        content::convert_city_preferences(source("@AUTOBUILD\nimprovements 1\nimprovements 1\n"),
                                          selected, classic::ascii_text_case());
    CHECK(load(converted, selected)->update.build_order == std::vector<std::uint8_t>{1, 1});
    std::string maximum = "@AUTOBUILD\n";
    for (int i = 0; i < 32; ++i)
        maximum += "improvements 1\n";
    maximum += "ignored after the classic limit\n";
    CHECK(load(content::convert_city_preferences(source(maximum), selected,
                                                 classic::ascii_text_case()),
               selected)
              ->update.build_order.size() == 32);
    const auto absent =
        load(content::convert_city_preferences(source("; empty preferences\n"), selected,
                                               classic::ascii_text_case()),
             selected);
    CHECK(absent->update.build_order.empty());
    CHECK_FALSE(absent->update.deprioritize_defense);
    for (const auto* ending : {"\n", "; comment\n", "@NEXT\n"}) {
        const auto terminated =
            source(std::string("@AUTOBUILD\nimprovements 1\n") + ending + "improvements 1\n");
        const auto result = load(
            content::convert_city_preferences(terminated, selected, classic::ascii_text_case()),
            selected);
        CHECK(result->update.build_order == std::vector<std::uint8_t>{1});
        CHECK(classic_result(terminated, fixture.load()->input.definitions, 0).count == 1);
    }
}
TEST_CASE("CITYPREF conversion distinguishes reversible text from unknown comparison rules",
          "[content][conversion]") {
    rule_fixture::Input fixture;
    fixture.rules["improvements"][1]["name"] = "Caf\xc3\xa9";
    const auto selected = fixture.pack();
    const auto converted = content::convert_city_preferences(source("@AUTOBUILD\nCaf\xe9\n"),
                                                             selected, classic::ascii_text_case());
    CHECK(load(converted, selected)->update.build_order == std::vector<std::uint8_t>{1});
    try {
        content::convert_city_preferences(source("@AUTOBUILD\nCAF\xc9\n"), selected,
                                          classic::ascii_text_case());
        FAIL("unestablished case equivalence must reject");
    } catch (const content::Error& e) {
        CHECK(e.code == "comparison-unavailable");
    }
}
TEST_CASE(
    "CITYPREF conversion diagnoses references and malformed input without narrowing classic load",
    "[content][conversion]") {
    rule_fixture::Input fixture;
    const auto selected = fixture.pack();
    const auto bad = source("@AUTOBUILD\nimprovements 1\nmisspelled reference\n");
    CHECK(classic_result(bad, fixture.load()->input.definitions, 7).count == 1);
    try {
        content::convert_city_preferences(bad, selected, classic::ascii_text_case());
        FAIL("unresolved reference must reject conversion");
    } catch (const content::Error& e) {
        CHECK(e.code == "unresolved-classic-reference");
    }
    CHECK_THROWS(content::convert_city_preferences(source(std::string("@AUTOBUILD\n\0bad", 16)),
                                                   selected, classic::ascii_text_case()));
    CHECK_THROWS(content::convert_city_preferences(source("@AUTOBUILD\n\x81\n"), selected,
                                                   classic::ascii_text_case()));
    fixture.rules["improvements"][1]["id"] = "unmapped.identity";
    CHECK_THROWS(content::convert_city_preferences(source("@AUTOBUILD\nimprovements 1\n"),
                                                   fixture.pack(), classic::ascii_text_case()));
}
