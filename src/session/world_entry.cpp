#include "session/world_entry.hpp"
#include "civilization/knowledge.hpp"
#include <utility>

namespace openciv::session {
void WorldEntryServices::validate(NewWorldChoice choice) const {
    if (!load_ordinary_rules || !reload_table || !stop_menu_sound || !generate_world ||
        !accelerate || !introduce)
        throw std::invalid_argument("World entry requires complete owning services");
    if (choice == NewWorldChoice::premade) {
        if (!choose_map)
            throw std::invalid_argument("Premade world entry requires a file choice service");
        premade.validate();
    } else {
        size.validate();
    }
    if (choice == NewWorldChoice::customize)
        parameters.validate();
    else
        parameters.preferences.validate_output();
    players.validate();
    initialize.validate();
}
namespace {
void publish(SessionRuntime& runtime) {
    if (runtime.active())
        runtime.publish();
}
} // namespace
std::string starting_advance_list(const state::GameState& world, const state::RuleState& rules,
                                  std::int32_t civilization) {
    std::array<std::size_t, 12> selected{};
    std::size_t count = 0;
    for (std::size_t index = 99; index > 0 && count < selected.size(); --index) {
        const auto& prerequisites = rules.definitions.advances[index].prerequisites;
        if (prerequisites[0] == state::AdvanceReference::never &&
            prerequisites[1] == state::AdvanceReference::never)
            continue;
        if (civilization::knows_advance(world.civilizations, civilization,
                                        static_cast<std::int32_t>(index)))
            selected[count++] = index;
    }
    std::string result;
    while (count > 0) {
        result += rules.text.text(rules.names.advances[selected[--count]]);
        result += ", ";
    }
    return result;
}
bool enter_new_world(SessionRuntime& runtime, ClassicFileState& files, base::GameplayRandom& random,
                     std::string_view call, NewWorldChoice choice,
                     const state::CalendarValues& calendar, const WorldEntryServices& supplied) {
    const auto services = supplied;
    if (call.empty())
        throw std::invalid_argument("World entry requires a semantic call identity");
    if (choice != NewWorldChoice::random && choice != NewWorldChoice::premade &&
        choice != NewWorldChoice::customize)
        throw std::invalid_argument("Unknown new-world menu choice");
    services.validate(choice);
    runtime.prepare_new_game();
    // Preserve application directory/screen configuration, not loaded-game
    // descriptors, hints, leader scratch, cursor, layout or scenario paths.
    auto fresh_files = ClassicFileState{};
    fresh_files.text = files.text;
    fresh_files.text.working = fresh_files.text.search = fresh_files.text.home;
    fresh_files.current_screen = files.current_screen;
    fresh_files.default_game_stem = files.default_game_stem;
    fresh_files.game_stem = fresh_files.default_game_stem;
    files = std::move(fresh_files);
    auto invoke = [&]<typename F, typename... Args>(const F& service, Args&&... args) {
        publish(runtime);
        return service(std::forward<Args>(args)...);
    };
    auto run = [&] {
        invoke(services.load_ordinary_rules);
        auto source = WorldSource::generated;
        bool selected;
        if (choice == NewWorldChoice::premade) {
            const auto loaded = load_premade_world(runtime, files.starts,
                                                   invoke(services.choose_map), services.premade);
            selected = loaded.has_value();
            if (loaded)
                source = *loaded;
        } else {
            selected = select_world_size(runtime, services.size);
        }
        if (!select_world_parameters(runtime, random, std::string(call) + "/parameters",
                                     choice == NewWorldChoice::customize, selected,
                                     services.parameters))
            return false;
        const auto players = select_players(runtime, random, std::string(call) + "/players", source,
                                            calendar, services.players);
        if (!players.accepted)
            return false;
        publish(runtime);
        (void)save_classic_preferences(runtime.process(), services.parameters.preferences);
        for (const auto table :
             {LoadedRuleTable::advances, LoadedRuleTable::units, LoadedRuleTable::improvements})
            invoke(services.reload_table, table);
        invoke(services.stop_menu_sound);
        invoke(services.generate_world, source != WorldSource::generated);
        initialize_new_game(runtime, random, std::string(call) + "/initialize",
                            services.initialize);
        if (players.acceleration != 0)
            invoke(services.accelerate, players.acceleration);
        const auto player = runtime.world().globals.player_civ;
        if (player < 1 || player > 7)
            throw std::out_of_range("World introduction requires a human civilization slot");
        const auto advances =
            starting_advance_list(runtime.world(), *runtime.process().rules, player);
        invoke(services.introduce, state::CivilizationSlot{static_cast<std::uint8_t>(player)},
               advances);
        return true;
    };
    try {
        const auto result = run();
        publish(runtime);
        return result;
    } catch (...) {
        publish(runtime);
        throw;
    }
}
} // namespace openciv::session
