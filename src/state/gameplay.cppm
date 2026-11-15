export module openciv.state.gameplay;

import std;

export namespace openciv::state {
// Owned semantic operands from docs/content/gameplay-format.md. No JSON, host
// I/O or gameplay formulas belong here. Arrays use the named profile identities.
inline constexpr std::array<std::string_view, 6> gameplay_components{
    "calendar", "spaceship", "governments", "difficulty", "combat", "scoring"};
inline constexpr std::array<std::string_view, 4> calendar_profiles{"chieftain-warlord", "prince",
                                                                   "king", "emperor-deity"};
inline constexpr std::array<std::string_view, 7> government_names{
    "anarchy", "despotism", "monarchy", "communism", "fundamentalism", "republic", "democracy"};
inline constexpr std::array<std::string_view, 6> difficulty_names{"chieftain", "warlord", "prince",
                                                                  "king",      "emperor", "deity"};
inline constexpr std::array<std::string_view, 6> spaceship_part_names{
    "structural", "propulsion", "fuel", "habitation", "life-support", "solar-energy"};
inline constexpr std::array<std::string_view, 5> scenario_outcomes{
    "decisive-victory", "marginal-victory", "draw", "marginal-defeat", "decisive-defeat"};
/// An exact numerator/denominator pair for authored gameplay ratios.
struct Rational {
    std::int32_t numerator = 0, denominator = 1;
    bool operator==(const Rational&) const = default;
};
/// One authored calendar interval and its year-step behavior.
struct CalendarPeriod {
    std::int32_t start_year = 0, turn_count = 0, year_step = 0;
    bool operator==(const CalendarPeriod&) const = default;
};
/// The complete authored turn-to-year calendar inputs.
struct CalendarValues {
    std::array<std::array<CalendarPeriod, 6>, 4> profiles{};
    std::int32_t warning_year = 0, end_year = 0, scenario_warning_lead_turns = 0;
    bool operator==(const CalendarValues&) const = default;
};
/// Classify a spaceship part as structural, component, or module.
enum class SpaceshipGroup { structural, component, module };
/// Authored values for a spaceship part category.
struct SpaceshipPartValues {
    SpaceshipGroup group = SpaceshipGroup::structural;
    std::int32_t maximum_count = 0, mass_hundreds_of_tons = 0;
    bool operator==(const SpaceshipPartValues&) const = default;
};
/// Authored spaceship part limits, masses, and population per habitation module.
struct SpaceshipValues {
    std::array<SpaceshipPartValues, 6> parts{};
    std::int32_t people_per_habitation = 0;
    bool operator==(const SpaceshipValues&) const = default;
};
/// Authored gameplay modifiers indexed by government.
struct GovernmentValues {
    std::int32_t maximum_rate_tenths = 0, corruption_divisor = 0;
    // Resolved against selected rules before session admission; not a slot guess.
    std::optional<std::string> required_advance;
    bool operator==(const GovernmentValues&) const = default;
};
/// Authored gameplay modifiers indexed by difficulty.
struct DifficultyValues {
    std::int32_t ai_box_factor = 0, human_research_multiplier = 0, ai_research_multiplier = 0;
    bool operator==(const DifficultyValues&) const = default;
};
/// Authored constants used by combat calculations.
struct CombatValues {
    Rational veteran_bonus, paradrop_bonus;
    // Exact doubled factors consumed by U06; reduced rationals remain in the digest.
    std::int32_t fortification_factor = 0, fortress_factor = 0, city_walls_factor = 0;
    std::int32_t ordinary_aircraft_multiplier = 0, destroyed_aircraft_multiplier = 0,
                 partisans_against_zero_attack_multiplier = 0;
    bool operator==(const CombatValues&) const = default;
};
/// Authored scoring parameters grouped by contribution.
struct ScoringValues {
    std::int32_t per_citizen = 0, per_happy_citizen = 0, per_unhappy_citizen = 0;
    std::int32_t wonder_points = 0, spaceship_points_per_population_success = 0,
                 pollution_points_per_tile = 0, future_technology_points = 0;
    /// Scoring inputs for peace-related contributions.
    struct Peace {
        std::int32_t first_eligible_turn = 0, points_per_turn = 0, maximum_points = 0;
        bool operator==(const Peace&) const = default;
    } peace;
    /// Scoring inputs for barbarian activity.
    struct Barbarians {
        std::int32_t points_per_level = 0, offset_points = 0;
        bool operator==(const Barbarians&) const = default;
    } barbarians;
    /// Scoring inputs for conquest-related contributions.
    struct Conquest {
        std::int32_t points_per_enemy = 0, base_points = 0, turn_reference = 0,
                     points_per_remaining_turn = 0, no_restart_minimum_turn = 0;
        Rational large_map_multiplier, small_map_multiplier;
        bool operator==(const Conquest&) const = default;
    } conquest;
    std::int32_t objective_city = 0, objective_wonder = 0;
    std::array<std::int32_t, 5> outcome_points{};
    std::int32_t points_per_objective = 0, scenario_conquest_multiplier = 0;
    std::array<std::int32_t, 6> rating_difficulty_factors{};
    bool operator==(const ScoringValues&) const = default;
};
/// Validated native gameplay parameters shared by owning mechanics.
struct GameplayValues {
    CalendarValues calendar;
    SpaceshipValues spaceship;
    std::array<GovernmentValues, 7> governments{};
    std::array<DifficultyValues, 6> difficulties{};
    CombatValues combat;
    ScoringValues scoring;
    bool operator==(const GameplayValues&) const = default;
};
} // namespace openciv::state
