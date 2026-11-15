export module openciv.session.player_setup_types;

import std;
export import openciv.state.choices;
export import openciv.state.identities;
export namespace openciv::session {
/// Explicit request for a random setup decision, distinct from an ordinary row.
struct RandomSetupChoice {};
using SetupNumericChoice = std::variant<std::int32_t, RandomSetupChoice>;
/// A selectable tribe identity with its display name and preferred color.
struct SetupTribeOption {
    std::int32_t tribe = 0;
    std::string plural;
    std::int16_t preferred_color = 0;
};
/// A chosen tribe slot and whether custom naming was requested.
struct SetupTribeChoice {
    std::int32_t tribe = -1; // Negative cancels; otherwise a table slot, not a color.
    bool custom = false;
};
// F05 section 4: Simplified Combat, Flat World, Pick Opponents, Accelerated
// Startup, Bloodlust, No Restart, in the specified read order.
using SetupAdvancedChoices = std::array<bool, 6>;
/// Advanced-rule checkbox values and their intentionally ignored modal result.
struct SetupAdvancedResponse {
    std::int32_t modal_result = 0; // Deliberately ignored, including cancellation.
    SetupAdvancedChoices checks{};
};
/// Actions available from the player-name setup interaction.
enum class SetupNameAction { accept, cancel, unavailable, titles, portrait };
/// A name-dialog action with owned classic leader, plural, and adjective bytes.
struct SetupNameResponse {
    SetupNameAction action = SetupNameAction::cancel;
    // Classic byte strings: leader, plural, adjective. Only leader is read by
    // ordinary Name. Accepted entries have at most 23 bytes and no embedded NUL.
    std::array<std::string, 3> names;
};
/// The accepted, cancelled, or unavailable result of editing government titles.
struct SetupTitleResponse {
    /// Distinguish title acceptance from cancellation and an unavailable interaction.
    enum class Status { accept, cancel, unavailable } status = Status::cancel;
    std::array<std::string, 7> titles;
};
/// Owner-supplied player-setup interactions returning semantic choices.
class PlayerSetupInteraction {
public:
    PlayerSetupInteraction() = default;
    virtual ~PlayerSetupInteraction() = default;
    PlayerSetupInteraction(const PlayerSetupInteraction&) = delete;
    PlayerSetupInteraction& operator=(const PlayerSetupInteraction&) = delete;
    /// Ordinary rows or a negative cancellation. Nominal positive domains:
    /// difficulty 0..5, opponents 0..4 (6 - row opponents), barbarians 0..3,
    /// rules 0=standard/1=custom, gender 0..1, acceleration 0..2, style 0..3.
    virtual std::int32_t choose_difficulty(state::Difficulty remembered) const = 0;
    /// Choose standard/custom rules from the remembered selection, or cancel.
    virtual std::int32_t choose_rules(state::RuleSelection remembered) const = 0;
    /// Choose leader gender from the remembered selection, or cancel.
    virtual std::int32_t choose_gender(state::Gender remembered) const = 0;
    /// Choose an opponent-count row or an explicit random decision.
    virtual SetupNumericChoice choose_opponents(std::int16_t remembered) const = 0;
    /// Choose barbarian activity or an explicit random decision.
    virtual SetupNumericChoice choose_barbarians(std::int16_t remembered) const = 0;
    /// Return the advanced-rule checkbox values shown to the player.
    virtual SetupAdvancedResponse choose_advanced(SetupAdvancedChoices defaults) const = 0;
    /// Choose an accelerated-start period from the offered years, or cancel.
    virtual std::int32_t choose_acceleration(std::array<std::uint32_t, 3> years,
                                             std::int16_t remembered) const = 0;
    /// Choose one offered tribe and whether to customize its names.
    virtual SetupTribeChoice choose_tribe(std::span<const SetupTribeOption> options,
                                          std::int16_t remembered) const = 0;
    /// Edit leader/tribe names or request a title/portrait child interaction.
    virtual SetupNameResponse choose_names(bool custom,
                                           std::array<std::string, 3> current) const = 0;
    /// Edit the seven government titles or return a cancellation/unavailable result.
    virtual SetupTitleResponse choose_titles(std::array<std::string, 7> current) const = 0;
    /// Required P03/P04 child, reached only by the corresponding custom-name
    /// action. Its accepted portrait effects survive returning to the name loop.
    virtual void choose_portrait(state::CivilizationSlot civilization) const = 0;
    /// Choose a city style using the tribe default and current override.
    virtual std::int32_t choose_city_style(std::int16_t tribe_style,
                                           std::int16_t current_override) const = 0;
    /// Random is a distinct choice; an ordinary nonnegative result is one of
    /// the offered tribe slots. Slots are visited ascending, after roster draws.
    virtual SetupNumericChoice
    choose_opponent_tribe(state::CivilizationSlot civilization,
                          std::span<const SetupTribeOption> options) const = 0;
};

/// Accepted setup outcome, opponent-selection policy, and accelerated-start period.
struct PlayerSetupResult {
    bool accepted = false, pick_opponents = false;
    std::int32_t acceleration = 0;
};

} // namespace openciv::session
