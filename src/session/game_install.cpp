#include "session/game_install.hpp"
#include "rules/rules_install.hpp"
#include "base/arithmetic.hpp"
#include <algorithm>
#include <bit>

namespace openciv::session {
namespace {
std::size_t tribe(std::int32_t civ, std::span<const state::Civilization> civilizations,
                  std::size_t leader_count) {
    if (civ < 0 || static_cast<std::size_t>(civ) >= civilizations.size())
        throw std::out_of_range("Leader selection has no civilization backing");
    const auto selected = civilizations[static_cast<std::size_t>(civ)].tribe;
    if (selected < 0 || static_cast<std::size_t>(selected) >= leader_count || selected >= 21)
        throw std::out_of_range("Leader selection has no tribe backing");
    return static_cast<std::size_t>(selected);
}
void copy_label(std::string& target, state::NameReference name, const state::TextPool& pool) {
    if (!name.custom)
        target = pool.text(name.id);
}
} // namespace
void validate_game_input(const state::GameInput& input) {
    world::validate_map_input(input.map, true);
    if (input.civilizations.size() != 8 || input.globals.unit_end < 0 ||
        input.globals.city_end < 0 ||
        input.units.size() != static_cast<std::size_t>(input.globals.unit_end) ||
        input.units.size() > state::unit_capacity ||
        input.cities.size() != static_cast<std::size_t>(input.globals.city_end) ||
        input.cities.size() > state::city_backing_capacity)
        throw std::invalid_argument("Restoration input has inconsistent population bounds");
    for (std::size_t civ = 0; civ < 8; ++civ)
        (void)tribe(static_cast<std::int32_t>(civ), input.civilizations, 21);
    const bool scenario = (input.globals.game_flags & 0xc0U) != 0;
    if (scenario != input.scenario.has_value())
        throw std::invalid_argument("Scenario flags and restoration parameters disagree");
    if (!scenario) {
        const auto selected = [&](std::int32_t civ) {
            if (!input.leaders[tribe(civ, input.civilizations, 21)])
                throw std::invalid_argument("Missing selected leader customization");
        };
        if (input.globals.multiplayer == state::GameMode::single_player)
            selected(input.globals.player_civ);
        else
            for (unsigned civ = 0; civ < 8; ++civ)
                if (input.globals.human_players & (1U << civ))
                    selected(static_cast<std::int32_t>(civ));
    }
    if (scenario)
        for (const auto& names : input.names)
            if (names.tribe.find('\0') != std::string::npos)
                throw std::invalid_argument("Scenario tribe name contains NUL");
}
void restore_leader_names(const state::GameInput& input, state::GameState& world,
                          state::RuleState& rules) {
    for (auto& live : rules.leaders) {
        live.leader_name = live.names.at(live.female);
        live.plural.custom = live.adjective.custom = false;
        for (auto& pair : live.titles)
            for (auto& id : pair)
                id.custom = false;
    }
    if (!(input.globals.game_flags & 0xc0U)) {
        const auto restore = [&](std::int32_t civ) {
            const auto selected = tribe(civ, world.civilizations, rules.leaders.size());
            auto& live = rules.leaders[selected];
            const auto& saved = input.leaders[selected].value();
            live.leader_name.custom = !live.leader_name.custom;
            live.plural.custom ^= saved.plural;
            live.adjective.custom ^= saved.adjective;
            for (std::size_t g = 0; g < 7; ++g)
                for (std::size_t sex = 0; sex < 2; ++sex)
                    live.titles[g][sex].custom ^= saved.titles[g][sex];
        };
        if (world.globals.multiplayer == state::GameMode::single_player)
            restore(world.globals.player_civ);
        else
            for (unsigned civ = 0; civ < 8; ++civ)
                if (world.globals.human_players & (1U << civ))
                    restore(static_cast<std::int32_t>(civ));
        return;
    }
    for (std::size_t civ = 1; civ < 8; ++civ) {
        const auto selected =
            tribe(static_cast<std::int32_t>(civ), world.civilizations, rules.leaders.size());
        auto& live = rules.leaders[selected];
        live.female = (world.civilizations[civ].flags & 0x200U) != 0;
        world.civilizations[civ].leader_portrait =
            static_cast<std::uint8_t>(selected + 21 * live.female);
        if (world.names[civ].tribe == rules.text.text(live.plural.id))
            for (std::size_t g = 0; g < 7; ++g)
                for (std::size_t sex = 0; sex < 2; ++sex)
                    live.titles[g][sex] = {rules.names.government_titles[g][sex], false};
        live.leader_name.custom = !live.leader_name.custom;
        live.plural.custom = !live.plural.custom;
        live.adjective.custom = !live.adjective.custom;
    }
}
void install_game(const state::GameInput& input, SessionRuntime& runtime,
                  const state::RuleInput& rules_input, const state::Rectangle& screen,
                  std::string_view password_reset_call) {
    validate_game_input(input);
    auto& world = runtime.world();
    auto& process = runtime.process();
    const auto entry_mode = world.globals.multiplayer;
    world::install_map(input.map, world.map, true);
    auto& globals = world.globals;
    globals = input.globals;
    globals.num_enemies =
        static_cast<std::uint8_t>(std::popcount(globals.civs_alive) - 1 - (globals.civs_alive & 1));
    const bool scenario = (globals.game_flags & 0xc0U) != 0;
    if (globals.multiplayer != state::GameMode::single_player)
        globals.game_options &= ~0x8000U;
    if (scenario)
        globals.game_flags |= 0x80U;
    if (globals.game_flags & 0x40U) {
        globals.game_options &= ~0x8000U;
        globals.reveal_map = 0;
        globals.game_flags &= static_cast<std::uint16_t>(~0x10U);
    }
    globals.game_flags &= static_cast<std::uint16_t>(~0x40U);
    world.names = input.names;
    std::copy(input.civilizations.begin(), input.civilizations.end(), world.civilizations.begin());
    world.entities.units.set_end(input.units.size());
    world.entities.cities.set_end(input.cities.size());
    std::copy(input.units.begin(), input.units.end(), world.entities.units.scanned().begin());
    std::copy(input.cities.begin(), input.cities.end(), world.entities.cities.scanned().begin());
    state::renew_loaded_lifetimes(world.entities, process.tokens, false);
    for (std::size_t i = 0; i < input.tribes.size(); ++i) {
        auto& target = process.rules->leaders[i];
        target.times_used = input.tribes[i].times_used;
        target.city_name_index = input.tribes[i].city_name_index;
        target.female = input.tribes[i].female;
    }
    world.power_history = input.power_history;
    world.layout = input.layout;
    world.views = input.views;
    if (input.scenario)
        world.scenario = *input.scenario;
    else
        state::reset_scenario_parameters(world.scenario);
    world.eliminations = input.eliminations;
    auto& multiplayer = world.multiplayer;
    auto& saved = multiplayer.saved;
    auto& preferences = process.multiplayer_preferences;
    multiplayer.local_human = globals.saved_current_civ;
    if (!input.multiplayer) {
        state::reset_passwords(multiplayer, runtime.gameplay_random(), password_reset_call);
        saved.open_game = true;
        saved.kill_on_retirement = preferences.kill_on_retirement;
        saved.show_human_starts = preferences.show_human_starts;
        saved.chat_from_start = preferences.chat_from_start;
        saved.double_production = saved.double_movement = false;
        preferences.simultaneous = false;
        saved.resume_civ = static_cast<std::uint8_t>(multiplayer.local_human);
    } else {
        saved = *input.multiplayer;
        if (!saved.resume_civ) {
            saved.resume_civ = static_cast<std::uint8_t>(globals.turn_civ);
            if (!saved.resume_civ)
                for (unsigned civ = 1; civ < 8; ++civ)
                    if (globals.civs_alive & (1U << civ)) {
                        saved.resume_civ = static_cast<std::uint8_t>(civ);
                        break;
                    }
        }
        state::refresh_password_flags(multiplayer);
    }
    multiplayer.resume_civ = saved.resume_civ;
    if (input.events)
        state::install_events(*input.events, world.events.collection);
    else
        world.events.collection.reset();
    world.events.loaded = input.events.has_value();
    restore_leader_names(input, world, *process.rules);
    state::rebuild_loaded_unit_census(world.civilizations, world.entities);
    world.mood.city.reset();
    rules::RuleInstaller installer(rules_input, *process.rules);
    installer.advances(true);
    installer.improvements(true);
    installer.units(true);
    const auto& saved_screen = world.layout.rectangles[0];
    world.layout.valid = !scenario &&
                         base::subtract32(saved_screen.right, saved_screen.left) ==
                             base::subtract32(screen.right, screen.left) &&
                         base::subtract32(saved_screen.bottom, saved_screen.top) ==
                             base::subtract32(screen.bottom, screen.top);
    state::adapt_loaded_mode(entry_mode, globals, multiplayer, preferences);
}
void prepare_scenario_names(bool scenario_export, const state::GlobalValues& globals,
                            std::span<const state::Civilization> civilizations,
                            std::span<state::CivilizationNames> names,
                            const state::RuleState& rules) {
    if (!scenario_export && !(globals.game_flags & 0x80U))
        return;
    for (std::size_t civ = 1; civ < 8; ++civ) {
        const auto selected =
            tribe(static_cast<std::int32_t>(civ), civilizations, rules.leaders.size());
        if (civ >= names.size())
            throw std::out_of_range("Scenario civilization has no name backing");
        const auto& live = rules.leaders[selected];
        copy_label(names[civ].adjective, live.adjective, rules.text);
        copy_label(names[civ].tribe, live.plural, rules.text);
        copy_label(names[civ].leader, live.leader_name, rules.text);
    }
}
} // namespace openciv::session
