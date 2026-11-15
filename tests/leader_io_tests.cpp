#include "classic_load_fixture.hpp"
#include "formats/game_input.hpp"
#include "session/game_install.hpp"
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
    world.globals.multiplayer = 3;
    world.globals.human_players = 6;
    world.globals.player_civ = 7;
    std::size_t calls = 0;
    REQUIRE(write_leader_items(
        [&](std::span<const Byte>) {
            ++calls;
            CHECK(world.globals.player_civ == 7);
            return true;
        },
        world.globals, world.civilizations, process.rules->leaders, {}));
    CHECK(calls == 67);
}
