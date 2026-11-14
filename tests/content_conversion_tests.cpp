// M1-02/F03 section 8: authored classic input, native admission and retained effects.
#include "rule_fixture.hpp"
#include "formats/content_conversion.hpp"
#include "formats/city_preferences.hpp"
#include "rules/city_preferences.hpp"
#include "base/hash.hpp"
#include <catch2/catch_test_macros.hpp>

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
    return content::load_city_preferences(selected, classic::ascii_text_case());
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
    classic::load_city_preferences(reader, rules, out, classic::ascii_text_case());
    return out;
}
} // namespace
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
            const auto expected = classic_result(input, native->rules->definitions, retained);
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
        CHECK(classic_result(terminated, fixture.load()->definitions, 0).count == 1);
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
    CHECK(classic_result(bad, fixture.load()->definitions, 7).count == 1);
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
