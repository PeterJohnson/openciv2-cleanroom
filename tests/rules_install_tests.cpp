// Authored data-format sections 1/4/Appendix B and F03 sections 3-7 expectations.
#include "formats/rules_install.hpp"
#include "formats/save_records.hpp"
#include "rule_fixture.hpp"
#include <catch2/catch_test_macros.hpp>
#include <new>

using namespace openciv;
using namespace openciv::formats::content;
namespace {
auto target() {
    auto out = std::make_unique<state::RuleState>();
    out->text.initialize();
    return out;
}
} // namespace
TEST_CASE("Native rules install identities names and live leaders without rescaling operands",
          "[content][rules-install]") {
    const auto source = rule_fixture::Input{}.load();
    auto live = target();
    live->names.orders[0] = 900;
    live->leaders[0].times_used = 71;
    live->leaders[0].city_name_index = 93;
    live->labels.popups[1] = 0;
    live->double_movement_applied = true;
    RuleInstaller(source, *live).load(true);
    REQUIRE(live->definitions == source->definitions);
    REQUIRE(live->text.size() == 411);
    REQUIRE(live->names.advances[0] == 1);
    REQUIRE(live->names.units[0] == 168);
    REQUIRE(live->names.interface_units[61] == live->names.units[61]);
    REQUIRE(live->names.orders[0] == 900);
    REQUIRE(live->leaders[0].names == std::array<std::int16_t, 2>{284, 285});
    REQUIRE(live->leaders[0].leader_name == 285);
    REQUIRE(live->leaders[0].plural == 286);
    REQUIRE(live->leaders[0].titles[0] == std::array<std::int16_t, 2>{264, 265});
    REQUIRE(live->leaders[0].times_used == 71);
    REQUIRE(live->leaders[0].city_name_index == 93);
    REQUIRE(live->definitions.units[0].movement == 6);
    REQUIRE(live->definitions.units[0].hit_points == 30);
    REQUIRE(live->labels.popups[1] == 0);
    REQUIRE_FALSE(live->double_movement_applied);
    REQUIRE(formats::decode_leader(formats::encode_leader(live->leaders[0])) == live->leaders[0]);
}
TEST_CASE("Native rule sections keep selected names and retain special terrain work",
          "[content][rules-install]") {
    const auto source = rule_fixture::Input{}.load();
    auto live = target();
    const auto label = live->text.store("Old name");
    live->definitions.advances[0].identity = {"old.advance", {"Old name", "Old name"}};
    live->definitions.improvements[0].identity.name = {"Old name", "Old name"};
    live->definitions.units[0].identity.name = {"Old name", "Old name"};
    live->names.advances[0] = live->names.improvements[0] = live->names.units[0] = label;
    live->names.interface_units[0] = 999;
    live->definitions.terrain[11].work = state::BaseTerrainWork{};
    live->definitions.terrain[11].work->irrigation.work_turns = 97;
    RuleInstaller install(source, *live, [](std::string_view, std::size_t) {
        FAIL("Keeping names must not allocate");
    });
    install.cosmic();
    install.advances(true);
    install.improvements(true);
    install.units(true);
    REQUIRE(live->definitions.advances[0].identity.id ==
            source->definitions.advances[0].identity.id);
    REQUIRE(live->definitions.advances[0].identity.name.classic == "Old name");
    REQUIRE(live->definitions.improvements[0].identity.name.classic == "Old name");
    REQUIRE(live->definitions.units[0].identity.name.classic == "Old name");
    REQUIRE(live->names.advances[0] == label);
    REQUIRE(live->names.improvements[0] == label);
    REQUIRE(live->names.units[0] == label);
    REQUIRE(live->names.interface_units[0] == 999);
    REQUIRE(live->definitions.units[0].hit_points == 30);
    REQUIRE(live->text.size() == 2);
    RuleInstaller(source, *live).terrain();
    REQUIRE(live->definitions.terrain[0].work == source->definitions.terrain[0].work);
    REQUIRE(live->definitions.terrain[11].work->irrigation.work_turns == 97);
}
TEST_CASE(
    "Native rule installation owns admitted input across callbacks and retains failure prefix",
    "[content][rules-install]") {
    auto source = rule_fixture::Input{}.load();
    auto live = target();
    live->definitions.improvements[1].cost_rows = 99;
    RuleInstaller install(source, *live, [&](std::string_view text, std::size_t) {
        source.reset();
        if (text == "improvements 1")
            throw std::bad_alloc();
    });
    REQUIRE_THROWS_AS(install.load(true), std::bad_alloc);
    REQUIRE_FALSE(source);
    REQUIRE(live->definitions.enabled_advance_count == 100);
    REQUIRE(live->definitions.improvements[0].cost_rows == 10);
    REQUIRE(live->definitions.improvements[1].cost_rows == 99);
    REQUIRE(live->definitions.improvements[1].identity.name.classic.empty());
    REQUIRE(live->text.size() == 102);
    REQUIRE(live->names.improvements[0] == 101);
    REQUIRE(live->names.improvements[1] == 0);
    REQUIRE(live->definitions.units[0].identity.name.classic.empty());
}
TEST_CASE("Native leader reload retains ordered duplicate override allocations and preservation",
          "[content][rules-install]") {
    rule_fixture::Input input;
    input.rules["tribes"][0]["governmentTitles"] = rule_fixture::json(
        R"([{"government":"governments.authored.1","male":"First","female":"Old"},
            {"government":"governments.authored.1","male":"Last","female":""}])");
    const auto source = input.load();
    REQUIRE(source->title_overrides[0].size() == 2);
    REQUIRE(source->title_overrides[0][0].titles.male.classic == "First");
    REQUIRE(source->definitions.tribes[0].titles[1].male.classic == "Last");
    auto live = target();
    RuleInstaller(source, *live).load(true);
    REQUIRE(live->text.size() == 414);
    REQUIRE(live->leaders[0].titles[1] == std::array<std::int16_t, 2>{290, 0});
    REQUIRE(live->text.text(288) == "First");
    REQUIRE(live->text.text(289) == "Old");
    REQUIRE(live->text.text(290) == "Last");
    REQUIRE(live->text.text(0) == "<nil>");
    const auto before = live->leaders;
    const auto before_model = live->definitions.tribes;
    const auto count = live->text.size();
    bool callback = false;
    RuleInstaller(source, *live, [&](std::string_view, std::size_t) {
        if (!callback) {
            callback = true;
            live->leaders[0].names[0] = -77;
            live->definitions.tribes[0].leaders[0] = {"Callback", "Callback"};
        }
    }).leaders(false);
    REQUIRE(live->leaders == before);
    REQUIRE(live->definitions.tribes == before_model);
    REQUIRE(live->text.size() == count + 87);
}
TEST_CASE("Native leader labels narrow at the same signed boundaries without changing input text",
          "[content][rules-install]") {
    const auto source = rule_fixture::Input{}.load();
    auto live = target();
    while (live->text.size() < 32768)
        live->text.store("Earlier");
    RuleInstaller(source, *live).leaders(true);
    REQUIRE(live->leaders[0].names[0] == -32768);
    REQUIRE(live->leaders[0].names[1] == -32767);
    REQUIRE(live->leaders[0].leader_name == -32767);
    REQUIRE(live->text.text(state::label_magnitude(live->leaders[0].leader_name)) == "Earlier");
    REQUIRE(live->definitions.tribes[0].leaders[1].classic == "Leader");
}
TEST_CASE("Invalid native content cannot reach installation or change existing rules",
          "[content][rules-install]") {
    auto live = target();
    live->definitions.units[0].attack = 72;
    rule_fixture::Input input;
    input.rules["units"][0]["hitPoints"] = jt::Json(31);
    REQUIRE_THROWS_AS(RuleInstaller(input.load(), *live).load(true), Error);
    REQUIRE(live->definitions.units[0].attack == 72);
    REQUIRE(live->text.size() == 1);
    REQUIRE_THROWS_AS(RuleInstaller(nullptr, *live), std::invalid_argument);
}
