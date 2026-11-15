export module openciv.state.rules;

import std;
export import openciv.state.choices;

export namespace openciv::state {
/// F03 semantic rule definitions. Text retains an explicit presentation and
/// windows-1252 comparison/serialization view; neither is an ambient host encoding.
struct RuleText {
    std::string utf8, classic;
    bool operator==(const RuleText&) const = default;
};
/// A semantic rule identity with presentation and classic name spellings.
struct RuleName {
    std::string id;
    RuleText name;
    bool operator==(const RuleName&) const = default;
};
/// An advance index or an explicit none, never, or invalid sentinel.
enum class AdvanceReference : std::int16_t { none = -1, never = -2, invalid = -3 };
/// A terrain index or an explicit in-place, absent, or invalid work result.
enum class TerrainResult : std::int16_t { no_result = -1, improve_in_place = -2, invalid = -3 };
/// The movement domain of a unit rule.
enum class RuleDomain : std::uint8_t { land, air, sea };
/// The AI role assigned by a unit rule.
enum class RuleRole : std::uint8_t {
    attack,
    defend,
    naval_superiority,
    air_superiority,
    sea_transport,
    settle,
    diplomacy,
    trade
};
/// The historical era assigned to an advance.
enum class AdvanceEpoch : std::uint8_t { ancient, renaissance, industrial, modern };
/// The research category assigned to an advance.
enum class AdvanceCategory : std::uint8_t { military, economic, social, academic, applied };
/// Named indices into the ordered classic cosmic-parameter table.
enum class CosmicParameter : std::size_t {
    road_movement_multiplier,
    trireme_loss_denominator,
    food_per_citizen,
    food_box_rows,
    shield_box_rows,
    settler_food_low_government,
    settler_food_high_government,
    unhappiness_city_size,
    unhappiness_empire_size,
    aqueduct_size,
    sewer_size,
    technology_cost_paradigm,
    transformation_work,
    monarchy_free_support,
    communism_free_support,
    fundamentalism_free_support,
    communism_palace_distance,
    fundamentalism_science_loss_percent,
    production_switch_loss_percent,
    paradrop_range,
    spaceship_mass_thrust_paradigm,
    fundamentalism_science_cap_tenths
};
/// The normalized byte values of the 22 cosmic parameters.
struct CosmicRules {
    std::array<std::uint8_t, 22> values{};
    /// Read a cosmic parameter by its checked semantic index.
    std::uint8_t operator[](CosmicParameter parameter) const {
        return values.at(static_cast<std::size_t>(parameter));
    }
    bool operator==(const CosmicRules&) const = default;
};
/// An advance definition with prerequisites and AI/research classifications.
struct AdvanceRule {
    RuleName identity;
    std::int8_t ai_value = 0, civilized_ai_modifier = 0;
    std::array<AdvanceReference, 2> prerequisites{AdvanceReference::never, AdvanceReference::never};
    AdvanceEpoch epoch{};
    AdvanceCategory category{};
    bool enabled = false;
    bool operator==(const AdvanceRule&) const = default;
};
/// An improvement definition with cost, upkeep, and prerequisite.
struct ImprovementRule {
    RuleName identity;
    std::uint8_t cost_rows = 0, upkeep = 0;
    AdvanceReference prerequisite = AdvanceReference::never;
    bool operator==(const ImprovementRule&) const = default;
};
/// A unit-type definition with combat, movement, production, and role inputs.
struct UnitRule {
    RuleName identity;
    AdvanceReference obsolete_by = AdvanceReference::never, prerequisite = AdvanceReference::never;
    RuleDomain domain{};
    RuleRole role{};
    std::uint8_t movement = 0, range = 0, attack = 0, defense = 0, hit_points = 0, firepower = 0,
                 cost_rows = 0, capacity = 0;
    // F03 Appendix A: retained flags are 32-bit; parsing clears positions 16..31.
    std::uint32_t abilities = 0;
    bool operator==(const UnitRule&) const = default;
};
/// A terrain-work operation and its authored yield/time/result inputs.
struct TerrainWork {
    TerrainResult result = TerrainResult::no_result;
    std::uint8_t yield_bonus = 0, work_turns = 0, ai_government_threshold = 0;
    bool operator==(const TerrainWork&) const = default;
};
/// The base inputs for work performed on a terrain type.
struct BaseTerrainWork {
    TerrainWork irrigation, mining;
    TerrainResult transformation = TerrainResult::no_result;
    bool operator==(const BaseTerrainWork&) const = default;
};
/// A terrain definition and the work operations available on it.
struct TerrainRule {
    RuleName identity;
    std::uint8_t movement_cost = 0, defense_factor = 0, food = 0, shields = 0, trade = 0;
    // No update for a special-resource definition. Installation retains any
    // corresponding old work fields, as required by F03 section 6.
    std::optional<BaseTerrainWork> work;
    bool operator==(const TerrainRule&) const = default;
};
/// The paired male/female titles for a government.
struct TitlePair {
    RuleText male, female;
    bool operator==(const TitlePair&) const = default;
};
/// A government name and its title definitions.
struct GovernmentRule {
    RuleName identity;
    TitlePair titles;
    bool operator==(const GovernmentRule&) const = default;
};
/// A tribe definition with names, preferred appearance, and AI traits.
struct TribeRule {
    std::string id;
    RuleText plural, adjective;
    std::array<RuleText, 2> leaders;
    Gender preferred_gender = Gender::male;
    std::uint8_t preferred_color = 1, city_style = 0;
    std::array<std::int8_t, 3> personality{};
    std::array<TitlePair, 7> titles;
    bool operator==(const TribeRule&) const = default;
};
/// A unit-order name and its presentation metadata.
struct OrderRule {
    RuleName identity;
    std::uint8_t shortcut = 0;
    bool operator==(const OrderRule&) const = default;
};
/// Semantic rule tables installed together as the selected ruleset.
struct RuleDefinitions {
    CosmicRules cosmic;
    std::array<AdvanceRule, 100> advances;
    std::uint16_t enabled_advance_count = 0;
    std::array<ImprovementRule, 67> improvements;
    std::array<AdvanceReference, 28> wonder_obsolescence{};
    std::array<UnitRule, 62> units;
    std::array<TerrainRule, 33> terrain;
    std::array<GovernmentRule, 7> governments;
    std::array<TribeRule, 21> tribes;
    std::array<RuleName, 16> commodities;
    std::array<OrderRule, 12> orders; // Logical external slots 1 through 12.
    std::array<RuleName, 6> difficulties;
    std::array<RuleName, 9> diplomatic_attitudes;
    bool operator==(const RuleDefinitions&) const = default;
};
} // namespace openciv::state
