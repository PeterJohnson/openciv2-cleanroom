#include <catch2/catch_test_macros.hpp>

import std;
import openciv.tests.classic_load_fixture;
import openciv.formats.game_input;
import openciv.session.game_install;

using namespace openciv;
using namespace openciv::formats;
TEST_CASE("F06 leader selection preserves repeated-tribe customization toggles", "[leader-names]") {
    auto saved = load_fixture::saved(false, true);
    saved.globals.human_players = 6;
    saved.leaders.resize(2);
    saved.leaders[1].plural = -1;
    auto input = game_input(std::move(saved));
    state::ProcessContext process;
    state::GameState world(process);
    world.globals = input.globals;
    std::copy(input.civilizations.begin(), input.civilizations.end(), world.civilizations.begin());
    session::restore_leader_names(input, world, *process.rules);
    CHECK_FALSE(process.rules->leaders[0].leader_name.custom);
    CHECK_FALSE(process.rules->leaders[0].plural.custom);
    world.globals.human_players = 2;
    session::restore_leader_names(input, world, *process.rules);
    CHECK(process.rules->leaders[0].leader_name.custom);
    CHECK(process.rules->leaders[0].plural.custom);
}
TEST_CASE("F06 leader export never substitutes the live player selector", "[leader-names]") {
    state::ProcessContext process;
    state::GameState world(process);
    world.globals.multiplayer = state::GameMode::tcp;
    world.globals.human_players = 6;
    world.globals.player_civ = 7;
    std::size_t calls = 0;
    auto observe_write = [&](std::span<const Byte>) {
        ++calls;
        CHECK(world.globals.player_civ == 7);
        return true;
    };
    REQUIRE(write_leader_items(observe_write, world.globals, world.civilizations,
                               process.rules->leaders, {}));
    CHECK(calls == 67);
}
