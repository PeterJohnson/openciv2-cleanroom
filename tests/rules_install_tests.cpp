#include <catch2/catch_test_macros.hpp>

import std;
import openciv.rules.rules_install;
import openciv.formats.leader_io;
import openciv.session.game_install;
import openciv.tests.rule_fixture;

// F02/F03 logical text and F06 storage separation.

using namespace openciv;
using namespace openciv::formats::content;
using openciv::rules::RuleInstaller;
TEST_CASE("Native names survive unrelated text allocations and preserve requested state",
          "[rules-install]") {
    const auto source = rule_fixture::Input{}.load();
    for (unsigned prefix : {0U, 70000U}) {
        auto live = std::make_unique<state::RuleState>();
        live->text.initialize();
        for (unsigned i = 0; i < prefix; ++i)
            live->text.store("Earlier");
        RuleInstaller install(source->input, *live);
        install.load(true);
        REQUIRE(live->definitions == *rule_fixture::installed(source->input));
        auto& leader = live->leaders[0];
        REQUIRE(live->text.text(leader.names[0].id) ==
                source->input.definitions.tribes[0].leaders[0].classic);
        REQUIRE(live->text.text(leader.plural.id) ==
                source->input.definitions.tribes[0].plural.classic);
        REQUIRE(leader.leader_name == leader.names[leader.female]);
        REQUIRE_FALSE(leader.leader_name.custom);
        leader.plural.custom = true;
        leader.times_used = 71;
        leader.city_name_index = 93;
        const auto before = live->leaders;
        const auto count = live->text.size();
        install.leaders(false);
        REQUIRE(live->leaders == before);
        REQUIRE(live->text.size() == count);
        const auto old = live->text.store("Old name");
        live->names.units[0] = old;
        live->definitions.units[0].identity.name = {"Old name", "Old name"};
        install.units(true);
        REQUIRE(live->text.text(live->names.units[0]) == "Old name");
        REQUIRE(live->definitions.units[0].movement == 6);
        REQUIRE(live->definitions.units[0].hit_points == 30);
        live->definitions.terrain[11].work = state::BaseTerrainWork{};
        live->definitions.terrain[11].work->irrigation.work_turns = 97;
        install.terrain();
        REQUIRE(live->definitions.terrain[11].work->irrigation.work_turns == 97);
    }
}
TEST_CASE("Repeated title overrides select final names without storage constraints",
          "[rules-install]") {
    rule_fixture::Input input;
    input.rules["tribes"][0]["governmentTitles"] = rule_fixture::json(
        R"([{"government":"governments.authored.1","male":"First","female":"Old"},
            {"government":"governments.authored.1","male":"Last","female":""}])");
    auto live = std::make_unique<state::RuleState>();
    live->text.initialize();
    RuleInstaller(input.load()->input, *live).load(true);
    REQUIRE(live->text.text(live->leaders[0].titles[1][0].id) == "Last");
    REQUIRE(live->text.text(live->leaders[0].titles[1][1].id) == "<nil>");
}
TEST_CASE("Stored markers preserve custom status independently of text identities",
          "[leader-names]") {
    auto live = std::make_unique<state::RuleState>();
    auto& leader = live->leaders[1];
    leader.names = {state::NameReference{90000, false}, state::NameReference{80000, false}};
    leader.plural = {70000, true};
    leader.adjective = {0, true};
    leader.titles[3][1] = {100000, true};
    auto stored = formats::stored_leader(leader);
    REQUIRE(stored.plural == -1);
    REQUIRE(stored.adjective == -1);
    REQUIRE(stored.titles[3][1] == -1);
    const auto bytes = formats::encode_leader(stored);
    leader.plural.id = 10;
    REQUIRE(formats::encode_leader(formats::stored_leader(leader)) == bytes);
    stored.plural = -32768;
    stored.names[0] = 12345;
    REQUIRE(formats::decode_leader(formats::encode_leader(stored)) == stored);
    auto input = std::make_unique<state::GameInput>();
    input->leaders[1].emplace();
    input->leaders[1]->plural = input->leaders[1]->adjective = true;
    input->leaders[1]->titles[3][1] = true;
    state::ProcessContext process;
    state::GameState world(process);
    world.globals.player_civ = 1;
    world.civilizations[1].tribe = 1;
    session::restore_leader_names(*input, world, *live);
    REQUIRE(leader.plural == state::NameReference{10, true});
    REQUIRE(leader.adjective.custom);
    REQUIRE(leader.titles[3][1].custom);
    REQUIRE(leader.leader_name == state::NameReference{90000, true});
}
TEST_CASE("Invalid native rules leave current content unchanged", "[rules-install]") {
    auto live = std::make_unique<state::RuleState>();
    live->text.initialize();
    live->definitions.units[0].attack = 72;
    rule_fixture::Input input;
    input.rules["units"][0]["hitPoints"] = jt::Json(31);
    REQUIRE_THROWS_AS(RuleInstaller(input.load()->input, *live).load(true), Error);
    REQUIRE(live->definitions.units[0].attack == 72);
    const auto admitted = rule_fixture::Input{}.load();
    live->text = {};
    REQUIRE_THROWS_AS(RuleInstaller(admitted->input, *live), std::invalid_argument);
}
