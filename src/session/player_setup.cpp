#include "session/player_setup.hpp"
#include "session/setup_calendar.hpp"
#include <algorithm>
#include <bit>
#include <utility>

namespace openciv::session {
void PlayerSetupServices::validate() const {
    if (!difficulty || !rules || !gender || !opponents || !barbarians || !advanced ||
        !acceleration || !tribe || !names || !titles || !portrait || !city_style || !opponent_tribe)
        throw std::invalid_argument("Player setup requires complete choice services");
}
namespace {
void publish(SessionRuntime& runtime) {
    if (runtime.active())
        runtime.publish();
}
void restore_names(state::RuleState& rules) {
    for (auto& leader : rules.leaders) {
        leader.leader_name = leader.names[leader.female];
        leader.plural.custom = false;
        leader.adjective.custom = false;
        for (auto& pair : leader.titles)
            for (auto& id : pair)
                id.custom = false;
    }
}
void copy(std::string& target, std::string_view text) {
    if (text.size() > 23 || text.find('\0') != std::string_view::npos)
        throw std::out_of_range("Setup name exceeds its admitted domain");
    target = text;
}
void customize(std::string& target, state::NameReference& label, const state::TextPool& pool) {
    copy(target, pool.text(label.id));
    label.custom = true;
}
std::vector<SetupTribeOption> tribes(const state::RuleState& rules,
                                     std::optional<std::int16_t> color = {}) {
    std::vector<SetupTribeOption> result;
    for (std::size_t i = 0; i < rules.leaders.size(); ++i) {
        const auto& leader = rules.leaders[i];
        if (!color || leader.color == *color)
            result.push_back({static_cast<std::int32_t>(i),
                              std::string(rules.text.text(leader.plural.id)), leader.color});
    }
    return result;
}
void row(std::int32_t value, std::int32_t maximum) {
    if (value > maximum)
        throw std::out_of_range("Setup response is outside the specified choices");
}
enum class Step { difficulty, opponents, barbarians, rules, gender, tribe, names, city_style };
} // namespace

PlayerSetupResult select_players(SessionRuntime& runtime, base::GameplayRandom& random,
                                 std::string_view call, WorldSource source,
                                 const state::CalendarValues& calendar,
                                 const PlayerSetupServices& supplied) {
    const auto services = supplied;
    services.validate();
    if (call.empty())
        throw std::invalid_argument("Player setup requires a semantic call identity");
    auto pin = runtime.pin_world();
    auto& world = runtime.world();
    auto& globals = world.globals;
    auto& rules = *runtime.process().rules;
    auto& preferences = runtime.process().ordinary_preferences;
    PlayerSetupResult result;
    std::array<std::uint64_t, 3> draws{};
    auto sample = [&](std::size_t site, std::int32_t modulus) {
        constexpr std::array<std::string_view, 3> sites{
            "F05.setup.opponent-count", "F05.setup.barbarians", "F05.setup.opponent-slot"};
        return random.primary_modulo(
            {std::string(sites[site]), std::string(call) + "/" + std::to_string(draws[site]++), 0},
            modulus);
    };
    auto invoke = [&]<typename F, typename... Args>(const F& service, Args&&... args) {
        publish(runtime);
        return service(std::forward<Args>(args)...);
    };
    auto run = [&] {
        restore_names(rules);
        auto step = Step::difficulty;
        std::int32_t gender = 0, selected_tribe = 0, human = 0;
        bool custom = false;
        while (true) {
            switch (step) {
            case Step::difficulty: {
                const auto choice = invoke(services.difficulty, preferences.difficulty);
                row(choice, 5);
                if (choice < 0)
                    return;
                globals.difficulty = static_cast<state::Difficulty>(choice);
                preferences.difficulty = static_cast<state::Difficulty>(choice);
                step = Step::opponents;
                break;
            }
            case Step::opponents: {
                const auto choice = invoke(services.opponents, preferences.opponents);
                if (const auto* ordinary = std::get_if<std::int32_t>(&choice)) {
                    row(*ordinary, 4);
                    if (*ordinary < 0) {
                        step = Step::difficulty;
                        break;
                    }
                    globals.num_enemies = static_cast<std::uint8_t>(6 - *ordinary);
                    preferences.opponents = static_cast<std::int16_t>(*ordinary);
                } else {
                    globals.num_enemies = static_cast<std::uint8_t>(sample(0, 5) + 2);
                }
                step = Step::barbarians;
                break;
            }
            case Step::barbarians: {
                const auto choice = invoke(services.barbarians, preferences.barbarians);
                if (const auto* ordinary = std::get_if<std::int32_t>(&choice)) {
                    row(*ordinary, 3);
                    if (*ordinary < 0) {
                        step = Step::difficulty;
                        break;
                    }
                    globals.barbarian_activity = static_cast<std::uint8_t>(*ordinary);
                    preferences.barbarians = static_cast<std::int16_t>(*ordinary);
                } else {
                    const auto level = static_cast<int>(globals.difficulty);
                    row(level, 5);
                    const auto activity = level < 2   ? sample(1, 2) + level
                                          : level < 5 ? sample(1, 3) + 1
                                                      : sample(1, 2) + 2;
                    globals.barbarian_activity = static_cast<std::uint8_t>(activity);
                }
                step = Step::rules;
                break;
            }
            case Step::rules: {
                const auto choice = invoke(services.rules, preferences.rules_choice);
                row(choice, 1);
                if (choice < 0)
                    return;
                preferences.rules_choice = static_cast<state::RuleSelection>(choice);
                if (choice == 0) {
                    globals.rules = 0x003f;
                    step = Step::gender;
                    break;
                }
                const auto remembered_rules = preferences.rules;
                const bool flat = source == WorldSource::generated
                                      ? (remembered_rules & 0x8000U) != 0
                                      : source == WorldSource::premade_flat;
                const SetupAdvancedChoices defaults{(remembered_rules & 0x10U) == 0,
                                                    flat,
                                                    false,
                                                    preferences.accelerated_start != 0,
                                                    (remembered_rules & 0x80U) != 0,
                                                    (remembered_rules & 0x100U) != 0};
                const auto checks = invoke(services.advanced, defaults).checks;
                globals.rules = 0x002f;
                if (!checks[0])
                    globals.rules |= 0x0010U;
                if (checks[1])
                    globals.rules |= 0x8000U;
                result.pick_opponents = checks[2];
                result.acceleration = checks[3] ? 1 : 0;
                if (checks[4])
                    globals.rules |= 0x0080U;
                if (checks[5])
                    globals.rules |= 0x0100U;
                preferences.rules = source == WorldSource::generated
                                        ? globals.rules
                                        : static_cast<std::uint16_t>((globals.rules & 0x7fffU) |
                                                                     (remembered_rules & 0x8000U));
                if (result.acceleration != 0) {
                    const auto years = prepare_acceleration_years(runtime, calendar);
                    result.acceleration =
                        invoke(services.acceleration, years, preferences.accelerated_start);
                    row(result.acceleration, 2);
                    if (result.acceleration < 0)
                        break; // Repeat Rules without replacing its entry-time locals.
                }
                preferences.accelerated_start = static_cast<std::int16_t>(result.acceleration);
                step = Step::gender;
                break;
            }
            case Step::gender:
                restore_names(rules);
                gender = invoke(services.gender, preferences.gender);
                row(gender, 1);
                if (gender < 0) {
                    step = Step::difficulty;
                    break;
                }
                preferences.gender = static_cast<state::Gender>(gender);
                step = Step::tribe;
                break;
            case Step::tribe: {
                const auto choices = tribes(rules);
                const auto choice = invoke(services.tribe, choices, preferences.tribe);
                row(choice.tribe, 20);
                if (choice.tribe < 0) {
                    step = Step::gender;
                    break;
                }
                selected_tribe = choice.tribe;
                custom = choice.custom;
                preferences.tribe = static_cast<std::int16_t>(selected_tribe);
                auto& leader = rules.leaders[static_cast<std::size_t>(selected_tribe)];
                human = leader.color;
                if (human < 1 || human > 7)
                    throw std::out_of_range("Setup human color has no civilization backing");
                globals.player_civ = static_cast<std::int8_t>(human);
                world.multiplayer.local_human = human;
                globals.human_players = static_cast<std::uint8_t>(1U << human);
                auto& civ = world.civilizations[static_cast<std::size_t>(human)];
                civ.tribe = static_cast<std::int16_t>(selected_tribe);
                civ.leader_portrait = static_cast<std::uint8_t>(selected_tribe + 21 * gender);
                leader.female = gender != 0;
                leader.leader_name = leader.names[static_cast<std::size_t>(gender)];
                auto& names = world.names[static_cast<std::size_t>(human)];
                customize(names.leader, leader.leader_name, rules.text);
                if (custom) {
                    customize(names.tribe, leader.plural, rules.text);
                    customize(names.adjective, leader.adjective, rules.text);
                    for (std::size_t g = 0; g < names.titles.size(); ++g)
                        customize(names.titles[g],
                                  leader.titles[g][static_cast<std::size_t>(gender)], rules.text);
                }
                step = Step::names;
                break;
            }
            case Step::names: {
                auto& names = world.names[static_cast<std::size_t>(human)];
                std::array<std::string, 3> current{names.leader, {}, {}};
                if (custom) {
                    current[1] = names.tribe;
                    current[2] = names.adjective;
                }
                const auto response = invoke(services.names, custom, current);
                if (response.action == SetupNameAction::unavailable)
                    return;
                if (response.action == SetupNameAction::cancel) {
                    step = Step::gender;
                    break;
                }
                if (!custom && response.action != SetupNameAction::accept)
                    throw std::invalid_argument("Ordinary Name has no custom editor action");
                copy(names.leader, response.names[0]);
                if (custom) {
                    copy(names.tribe, response.names[1]);
                    copy(names.adjective, response.names[2]);
                }
                if (response.action == SetupNameAction::titles) {
                    std::array<std::string, 7> titles;
                    for (std::size_t g = 0; g < titles.size(); ++g)
                        titles[g] = names.titles[g];
                    const auto edited = invoke(services.titles, titles);
                    if (edited.status == SetupTitleResponse::Status::unavailable)
                        return;
                    if (edited.status == SetupTitleResponse::Status::accept)
                        for (std::size_t g = 0; g < titles.size(); ++g)
                            copy(names.titles[g], edited.titles[g]);
                } else if (response.action == SetupNameAction::portrait) {
                    invoke(services.portrait,
                           state::CivilizationSlot{static_cast<std::uint8_t>(human)});
                } else {
                    step = Step::city_style;
                }
                break;
            }
            case Step::city_style: {
                auto& names = world.names[static_cast<std::size_t>(human)];
                const auto style =
                    invoke(services.city_style,
                           rules.leaders[static_cast<std::size_t>(selected_tribe)].city_style,
                           names.city_style);
                row(style, 3);
                if (style < 0) {
                    step = Step::gender;
                    break;
                }
                names.city_style = static_cast<std::int16_t>(style);
                globals.civs_alive = static_cast<std::uint8_t>(globals.human_players | 1U);
                for (unsigned admitted = 0; admitted < globals.num_enemies;) {
                    const auto slot = sample(2, 8);
                    const auto bit = static_cast<std::uint8_t>(1U << slot);
                    if ((globals.civs_alive & bit) == 0) {
                        globals.civs_alive |= bit;
                        ++admitted;
                    }
                }
                bool cancelled = false;
                if (result.pick_opponents)
                    for (std::uint8_t slot = 1; slot < 8; ++slot) {
                        const auto bit = static_cast<std::uint8_t>(1U << slot);
                        if ((globals.civs_alive & bit) == 0 || (globals.human_players & bit) != 0)
                            continue;
                        const auto choices = tribes(rules, slot);
                        const auto choice =
                            invoke(services.opponent_tribe, state::CivilizationSlot{slot}, choices);
                        if (const auto* selected = std::get_if<std::int32_t>(&choice)) {
                            if (*selected < 0) {
                                cancelled = true;
                                break;
                            }
                            if (std::none_of(
                                    choices.begin(), choices.end(),
                                    [&](const auto& option) { return option.tribe == *selected; }))
                                throw std::out_of_range(
                                    "Setup opponent is outside its color's offered tribes");
                            world.pending_leader_overrides[slot] =
                                static_cast<std::int8_t>(*selected);
                        }
                    }
                if (cancelled)
                    break; // Repeat City Style and redraw slots; retain overrides.
                result.accepted = true;
                return;
            }
            }
        }
    };
    try {
        run();
        publish(runtime);
        return result;
    } catch (...) {
        publish(runtime);
        throw;
    }
}
} // namespace openciv::session
