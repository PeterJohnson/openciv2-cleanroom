export module openciv.session.world_entry;
import openciv.civilization.knowledge;
import openciv.rules.rules_install;
export import openciv.state.game_input;
export import openciv.session.premade_world;

import std;
export import openciv.base.callback_ref;
export import openciv.session.new_game;
export import openciv.session.player_setup;
export import openciv.session.world_setup;

export namespace openciv::session {
/// Select generated-world or premade-map entry.
enum class NewWorldChoice { random, premade, customize };
/// Borrowed providers and callable owners must outlive the consuming operation.
struct WorldEntryDependencies {
    /// Selected source closes its readers and returns the same owned inputs used
    /// for load/resume. Installation and final name-preserving updates live here.
    base::CallbackRef<void()> reset_source;
    base::CallbackRef<state::LoadedContent()> load_content;
    const WorldSizeInteraction* size = nullptr;
    base::CallbackRef<std::optional<std::string>()> choose_map;
    PremadeWorldDependencies premade;
    // parameters.preferences serves both parameter-completion and final saves.
    WorldParameterDependencies parameters;
    const PlayerSetupInteraction* players = nullptr;
    base::CallbackRef<void()> stop_menu_sound;
    /// Required W02: false generates terrain; true retains/finalizes it. Reads
    /// current world_setup, map, rules and starts; no allocated-map substitute.
    base::CallbackRef<void(bool keep)> generate_world;
    NewGameDependencies initialize;
    /// Required F05 sections 9-10, including any retained negative selection.
    base::CallbackRef<void(std::int32_t selection)> accelerate;
    /// Required P03/P04 presentation resolves current leader/civilization names.
    /// Advance text is owned classic bytes with ", " after every listed name.
    base::CallbackRef<void(state::CivilizationSlot, std::string advances)> introduce;
    /// Require dependencies for the selected new-world source before entering setup.
    void validate(NewWorldChoice choice) const;
};
/// F05 section 2 / V03 section 2: highest at most twelve known advances,
/// displayed ascending, excluding zero and both-No definitions. The extra
/// identifier-100 probe cannot contribute under V03, so no unbacked read is made.
/// Does not grant advances, alter state, update the year or draw randomness.
std::string starting_advance_list(const state::GameState& world, const state::RuleState& rules,
                                  std::int32_t civilization);
/// F05 section 2, after a new-world menu choice and before application admission.
/// Validates selected-branch services before effects, then prepares an isolated
/// new game, revoking old clients and discarding all
/// previous world/rule/file scratch. Composes size/map, parameter, player and initialization
/// operations. False returns with current-attempt effects retained. A failed
/// size/map selection does not consume discarded parameter draws.
/// Required child exceptions publish and escape without rollback. The caller
/// supplies a unique call identity; child paths append /parameters, /players and
/// /initialize. Every retry starts fresh; within-attempt accepted effects remain
/// until the next attempt. Successful setup does not itself admit client leases.
bool enter_new_world(SessionRuntime& runtime, state::StartingPositions& starts,
                     base::GameplayRandom& random, std::string_view call, NewWorldChoice choice,
                     const state::CalendarValues& calendar, const WorldEntryDependencies& services);
} // namespace openciv::session

module :private;
namespace openciv::session {
void WorldEntryDependencies::validate(NewWorldChoice choice) const {
    if (!reset_source || !load_content || !stop_menu_sound || !generate_world || !accelerate ||
        !introduce)
        throw std::invalid_argument("World entry requires complete owning services");
    if (choice == NewWorldChoice::premade) {
        if (!choose_map)
            throw std::invalid_argument("Premade world entry requires a file choice service");
        premade.validate();
    } else {
        if (!size)
            throw std::invalid_argument("World size interaction is required");
    }
    if (choice == NewWorldChoice::customize)
        parameters.validate();
    else if (!parameters.preferences)
        throw std::invalid_argument("Preference storage is required");
    if (!players)
        throw std::invalid_argument("Player setup interaction is required");
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
bool enter_new_world(SessionRuntime& runtime, state::StartingPositions& starts,
                     base::GameplayRandom& random, std::string_view call, NewWorldChoice choice,
                     const state::CalendarValues& calendar,
                     const WorldEntryDependencies& supplied) {
    const auto& services = supplied;
    if (call.empty())
        throw std::invalid_argument("World entry requires a semantic call identity");
    if (choice != NewWorldChoice::random && choice != NewWorldChoice::premade &&
        choice != NewWorldChoice::customize)
        throw std::invalid_argument("Unknown new-world menu choice");
    services.validate(choice);
    runtime.prepare_new_game();
    auto pin = runtime.pin_world();
    starts = {};
    services.reset_source();
    auto invoke = [&]<typename F, typename... Args>(const F& service, Args&&... args) {
        publish(runtime);
        return service(std::forward<Args>(args)...);
    };
    auto run = [&] {
        auto content = invoke(services.load_content);
        if (!content.rules)
            throw std::invalid_argument("Content source returned no rules");
        auto& rules = *runtime.process().rules;
        if (content.labels)
            state::install_labels(*content.labels, rules.text, rules.labels);
        rules::RuleInstaller installer(*content.rules, rules);
        installer.load(true);
        auto source = WorldSource::generated;
        bool selected;
        if (choice == NewWorldChoice::premade) {
            const auto loaded =
                load_premade_world(runtime, starts, invoke(services.choose_map), services.premade);
            selected = loaded.has_value();
            if (loaded)
                source = *loaded;
        } else {
            selected = select_world_size(runtime, *services.size);
        }
        if (!select_world_parameters(runtime, random, std::string(call) + "/parameters",
                                     choice == NewWorldChoice::customize, selected,
                                     services.parameters))
            return false;
        const auto players = select_players(runtime, random, std::string(call) + "/players", source,
                                            calendar, *services.players);
        if (!players.accepted)
            return false;
        publish(runtime);
        (void)services.parameters.preferences->write(
            {runtime.process().ordinary_preferences, runtime.process().multiplayer_preferences});
        installer.advances(true);
        installer.units(true);
        installer.improvements(true);
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
