export module openciv.session.player_setup;

import std;
export import openciv.session.premade_world;
export import openciv.state.gameplay;

export extern "C++" {
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
    class PlayerSetupInteraction {
    public:
        PlayerSetupInteraction() = default;
        virtual ~PlayerSetupInteraction() = default;
        PlayerSetupInteraction(const PlayerSetupInteraction&) = delete;
        PlayerSetupInteraction& operator=(const PlayerSetupInteraction&) = delete;
        // Ordinary rows or a negative cancellation. Nominal positive domains:
        // difficulty 0..5, opponents 0..4 (6 - row opponents), barbarians 0..3,
        // rules 0=standard/1=custom, gender 0..1, acceleration 0..2, style 0..3.
        virtual std::int32_t choose_difficulty(state::Difficulty remembered) const = 0;
        virtual std::int32_t choose_rules(state::RuleSelection remembered) const = 0;
        virtual std::int32_t choose_gender(state::Gender remembered) const = 0;
        virtual SetupNumericChoice choose_opponents(std::int16_t remembered) const = 0;
        virtual SetupNumericChoice choose_barbarians(std::int16_t remembered) const = 0;
        virtual SetupAdvancedResponse choose_advanced(SetupAdvancedChoices defaults) const = 0;
        virtual std::int32_t choose_acceleration(std::array<std::uint32_t, 3> years,
                                                 std::int16_t remembered) const = 0;
        virtual SetupTribeChoice choose_tribe(std::span<const SetupTribeOption> options,
                                              std::int16_t remembered) const = 0;
        virtual SetupNameResponse choose_names(bool custom,
                                               std::array<std::string, 3> current) const = 0;
        virtual SetupTitleResponse choose_titles(std::array<std::string, 7> current) const = 0;
        // Required P03/P04 child, reached only by the corresponding custom-name
        // action. Its accepted portrait effects survive returning to the name loop.
        virtual void choose_portrait(state::CivilizationSlot civilization) const = 0;
        virtual std::int32_t choose_city_style(std::int16_t tribe_style,
                                               std::int16_t current_override) const = 0;
        // Random is a distinct choice; an ordinary nonnegative result is one of
        // the offered tribe slots. Slots are visited ascending, after roster draws.
        virtual SetupNumericChoice
        choose_opponent_tribe(state::CivilizationSlot civilization,
                              std::span<const SetupTribeOption> options) const = 0;
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
                                     const PlayerSetupInteraction& services);
    } // namespace openciv::session
}
