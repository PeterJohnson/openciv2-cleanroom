#pragma once

#include "session/premade_world.hpp"
#include "state/gameplay.hpp"
#include <variant>

namespace openciv::session {
struct RandomSetupChoice {};
using SetupNumericChoice = std::variant<std::int32_t, RandomSetupChoice>;
struct SetupTribeOption {
    std::int32_t tribe = 0;
    std::string plural;
    std::int16_t preferred_color = 0;
};
struct SetupTribeChoice {
    std::int32_t tribe = -1; // Negative cancels; otherwise a table slot, not a color.
    bool custom = false;
};
// F05 section 4: Simplified Combat, Flat World, Pick Opponents, Accelerated
// Startup, Bloodlust, No Restart, in the specified read order.
using SetupAdvancedChoices = std::array<bool, 6>;
struct SetupAdvancedResponse {
    std::int32_t modal_result = 0; // Deliberately ignored, including cancellation.
    SetupAdvancedChoices checks{};
};
enum class SetupNameAction { accept, cancel, unavailable, titles, portrait };
struct SetupNameResponse {
    SetupNameAction action = SetupNameAction::cancel;
    // Classic byte strings: leader, plural, adjective. Only leader is read by
    // ordinary Name. Accepted entries have at most 23 bytes and no embedded NUL.
    std::array<std::string, 3> names;
};
struct SetupTitleResponse {
    enum class Status { accept, cancel, unavailable } status = Status::cancel;
    std::array<std::string, 7> titles;
};
struct PlayerSetupServices {
    // Ordinary rows or a negative cancellation. Nominal positive domains:
    // difficulty 0..5, opponents 0..4 (6 - row opponents), barbarians 0..3,
    // rules 0=standard/1=custom, gender 0..1, acceleration 0..2, style 0..3.
    std::function<std::int32_t(std::int16_t remembered)> difficulty, rules, gender;
    std::function<SetupNumericChoice(std::int16_t remembered)> opponents, barbarians;
    std::function<SetupAdvancedResponse(SetupAdvancedChoices defaults)> advanced;
    std::function<std::int32_t(std::array<std::uint32_t, 3> years, std::int16_t remembered)>
        acceleration;
    std::function<SetupTribeChoice(std::span<const SetupTribeOption>, std::int16_t remembered)>
        tribe;
    std::function<SetupNameResponse(bool custom, std::array<std::string, 3> current)> names;
    std::function<SetupTitleResponse(std::array<std::string, 7> current)> titles;
    // Required P03/P04 child, reached only by the corresponding custom-name
    // action. Its accepted portrait effects survive returning to the name loop.
    std::function<void(state::CivilizationSlot)> portrait;
    std::function<std::int32_t(std::int16_t tribe_style, std::int16_t current_override)> city_style;
    // Random is a distinct choice; an ordinary nonnegative result is one of
    // the offered tribe slots. Slots are visited ascending, after roster draws.
    std::function<SetupNumericChoice(state::CivilizationSlot, std::span<const SetupTribeOption>)>
        opponent_tribe;
    void validate() const;
};
struct PlayerSetupResult {
    bool accepted = false, pick_opponents = false;
    std::int32_t acceleration = 0;
};
// F05 section 4, ending after accepted city style and opponent overrides.
// Ordered, nontransactional writes; entry locals survive retries. Does not
// save files, generate a world, create civilizations or apply acceleration.
// Required services execute at explicit published boundaries. The caller owns
// scheduling/modal presentation and supplies the selected validated calendar.
// Sampling keys use three F05.setup.* sites, call/<per-site draw ordinal>, and
// attempt zero; ordinals persist across all retries in this invocation.
PlayerSetupResult select_players(SessionRuntime& runtime, base::GameplayRandom& random,
                                 std::string_view call, WorldSource source,
                                 const state::CalendarValues& calendar,
                                 const PlayerSetupServices& services);
} // namespace openciv::session
