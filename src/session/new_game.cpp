#include "session/new_game.hpp"
#include <bit>

namespace openciv::session {
void NewGameDependencies::validate() const {
    if (!create_civilization || !balance_starts)
        throw std::invalid_argument("New game requires creation and balancing services");
}
namespace {
void publish(SessionRuntime& runtime) {
    if (runtime.active())
        runtime.publish();
}
} // namespace
void initialize_new_game(SessionRuntime& runtime, base::GameplayRandom& random,
                         std::string_view call, const NewGameDependencies& supplied) {
    const auto& services = supplied;
    if (call.empty())
        throw std::invalid_argument("New-game initialization requires a semantic call identity");
    services.validate();
    auto pin = runtime.pin_world();
    auto& world = runtime.world();
    auto& process = runtime.process();
    auto& globals = world.globals;
    try {
        state::reset_scenario_parameters(world.scenario);
        world.eliminations.count = 0;
        world.eliminations.turns.fill(0);
        world.eliminations.killers.fill(0);
        for (auto& name : world.eliminations.names)
            name.clear();
        world.civilization_color_flags.fill(0);
        for (auto& leader : process.rules->leaders) {
            leader.times_used = 0;
            leader.city_name_index = 0;
        }

        globals.turn = 0;
        globals.year = -4000;
        globals.first_launch_turn = -1;
        globals.game_options = process.ordinary_preferences.game_options & ~0x8000U;
        if (globals.difficulty == state::Difficulty::chieftain)
            globals.game_options |= 0x300U;
        else
            globals.game_options &= ~0x100U;
        globals.transient_flags = globals.shown_event_flags = 0;
        globals.message_options = process.ordinary_preferences.disabled_messages;
        world.movement_mode = state::MovementMode::move_units;
        world.entities.units.set_end(0);
        world.entities.cities.set_end(0);
        globals.unit_end = globals.city_end = 0;
        globals.active_unit.value = 0;
        globals.turn_civ = 0;
        globals.view_civ = -1;
        globals.reveal_map = 0;
        globals.warming_counter = globals.global_warmings = 0;
        globals.warming_pollution = globals.polluted_tiles = 0;
        globals.first_discoverer.fill(0);
        globals.advance_known_by.fill(0);
        globals.wonder_city.fill(-1);

        for (std::size_t slot = 0; slot < world.civilizations.size(); ++slot) {
            auto& civ = world.civilizations[slot];
            civ.spaceship_flags = 0;
            civ.spaceship_parts.fill(0);
            civ.spaceship_population = 0;
            civ.spaceship_launch_year = civ.spaceship_arrival_year = 0;
            civ.city_count = 0;
            world.unit_totals[slot] = 0;
            civ.military_count = 0;
            civ.military_strength = 0;
            civ.total_attack = 0;
            civ.total_city_size = 0;
            civ.ship_count = 0;
            civ.population_milestone = 0;
            civ.researching = 0;
            civ.continent_military.fill(0);
            civ.continent_attack.fill(0);
            civ.continent_cities.fill(0);
            civ.continent_city_size.fill(0);
        }

        globals.next_history_turn = static_cast<std::int16_t>(random.primary_modulo(
            base::ChoiceKey{"F05.new-game.history", std::string(call), 0}, 50));
        for (auto& row : world.power_history)
            row.fill(0);
        for (std::int32_t slot = 0; slot < 8; ++slot) {
            publish(runtime);
            (void)services.create_civilization(slot);
        }
        globals.civs_at_start = globals.civs_alive;
        publish(runtime);
        services.balance_starts();

        auto& preferences = process.ordinary_preferences;
        if (preferences.science + preferences.tax > 10 || preferences.science > 6 ||
            preferences.tax > 6) {
            preferences.science = 6;
            preferences.tax = 4;
        }
        if (std::popcount(globals.human_players) == 1) {
            if (globals.player_civ < 0 || globals.player_civ >= 8)
                throw std::out_of_range("New-game player civilization outside backing");
            auto& civ = world.civilizations[static_cast<std::size_t>(globals.player_civ)];
            civ.science_rate = static_cast<std::uint8_t>(preferences.science);
            civ.tax_rate = static_cast<std::uint8_t>(preferences.tax);
        } else {
            for (std::size_t slot = 1; slot < 8; ++slot)
                if (globals.human_players & (1U << slot)) {
                    world.civilizations[slot].science_rate = 6;
                    world.civilizations[slot].tax_rate = 4;
                }
        }
        if (globals.difficulty == state::Difficulty::chieftain)
            for (std::size_t slot = 1; slot < 8; ++slot)
                if (globals.human_players & (1U << slot))
                    world.civilizations[slot].gold = 50;
        publish(runtime);
    } catch (...) {
        publish(runtime);
        throw;
    }
}
} // namespace openciv::session
