#pragma once

#include "state/records.hpp"

namespace openciv::state {
// F02 §4, F06 §3: retained logical values, not packed file records. Construction
// defaults are conveniences, not original process/new-game initializers. No
// census, diplomacy symmetry, lifetime renewal or other repair is implicit.
struct CivilizationGoal {
    std::int16_t x = 0, y = 0;
    std::int8_t role = 0, priority = 0;
    bool operator==(const CivilizationGoal&) const = default;
};
struct CivilizationNames {
    std::int16_t city_style = 0;
    FixedText<24> leader, tribe, adjective;
    std::array<FixedText<24>, 7> titles;
    bool operator==(const CivilizationNames&) const = default;
};
struct Leader {
    std::int8_t attack = 0, expand = 0, civilize = 0;
    std::uint8_t times_used = 0, female = 0, city_name_index = 0;
    std::int16_t color = 0, city_style = 0, leader_name = 0, plural = 0, adjective = 0;
    std::array<std::int16_t, 2> names{};
    std::array<std::array<std::int16_t, 2>, 7> titles{};
    bool operator==(const Leader&) const = default;
};
struct Civilization {
    std::uint16_t flags = 0;
    std::int32_t gold = 0;
    std::int16_t tribe = 0, research_progress = 0, researching = 0;
    std::int16_t capital_x = 0, last_city_turn = 0;
    std::uint8_t num_advances = 0, future_techs = 0, research_cost_advances = 0;
    std::uint8_t science_rate = 0, tax_rate = 0, government = 0, diplo_random = 0;
    std::array<std::uint8_t, 4> wonders_by_group{};
    std::uint8_t retained_1b = 0;
    std::int16_t subversion_count = 0;
    std::uint8_t reputation = 0;
    std::int8_t diplo_request_count = 0;
    std::array<std::uint32_t, 8> treaties{};
    std::array<std::uint8_t, 8> attitude{};
    std::array<std::int8_t, 8> reputation_credit{};
    std::array<std::uint8_t, 8> aggression_counter{};
    std::array<std::uint8_t, 13> advances{};
    std::uint8_t retained_65 = 0;
    std::int16_t military_count = 0, city_count = 0, ship_count = 0, total_city_size = 0;
    std::uint16_t military_strength = 0;
    std::int16_t total_attack = 0, population_milestone = 0;
    std::array<std::uint8_t, 100> advance_source{};
    std::array<std::uint8_t, 62> units_active{}, units_lost{}, units_in_production{};
    std::array<std::uint16_t, 64> continent_military{}, continent_attack{};
    std::array<std::uint8_t, 64> continent_cities{}, continent_city_size{}, continent_trade_units{};
    std::array<std::uint8_t, 64> continent_flags{}, continent_policy{};
    std::int16_t best_shield_surplus = 0;
    std::array<std::int16_t, 7> government_score{};
    std::array<std::int16_t, 8> last_contact_turn{};
    std::uint8_t leader_portrait = 0;
    std::array<std::uint8_t, 10> throne_room{};
    std::uint8_t retained_3fd = 0;
    std::int16_t throne_room_upgrades = 0;
    std::uint8_t spaceship_flags = 0, retained_401 = 0;
    std::int16_t spaceship_arrival_year = 0, spaceship_launch_year = 0, spaceship_population = 0;
    std::array<std::int16_t, 6> spaceship_parts{};
    std::array<CivilizationGoal, 48> goals;
    std::array<CivilizationGoal, 16> standing_goals;
    bool operator==(const Civilization&) const = default;
};
// These values span world/configuration/session ownership. A stored view keeps
// the serialized scan bounds and selectors; it is not a running SessionRuntime.
struct GlobalValues {
    std::uint16_t rules = 0;
    std::uint32_t game_options = 0;
    std::uint16_t transient_flags = 0, game_flags = 0, message_options = 0, shown_event_flags = 0;
    std::uint16_t retained_0e = 0;
    std::int16_t turn = 0, year = 0, first_launch_turn = 0;
    SlotLink<UnitSlot> active_unit, moving_unit;
    std::uint8_t multiplayer = 0;
    std::int8_t player_civ = 0, saved_current_civ = 0, turn_civ = 0, view_civ = 0;
    std::uint8_t reveal_map = 0, difficulty = 0, barbarian_activity = 0;
    std::uint8_t civs_alive = 0, human_players = 0, civs_at_start = 0, num_enemies = 0;
    std::int8_t warming_counter = 0, global_warmings = 0;
    std::int16_t warming_pollution = 0, polluted_tiles = 0, peace_turns = 0;
    std::int16_t unit_end = 0, city_end = 0, num_advances = 0, retained_34 = 0;
    std::array<std::uint8_t, 100> first_discoverer{}, advance_known_by{};
    // -1 not built, -2 destroyed, otherwise a city slot (F02 contextual absence).
    std::array<std::int16_t, 28> wonder_city{};
    std::int16_t next_history_turn = 0;
    std::uint8_t best_human_rank = 0, worst_human_rank = 0;
    std::array<std::uint8_t, 8> power_rank{}, civ_by_rank{};
    bool operator==(const GlobalValues&) const = default;
};
// F06 section 6 stage 17, after names restore and before rule reloads.
// Rebuilds only active/in-production counts, modulo 256; retained losses and
// strategic summaries survive. Unsupported type access stops after prior effects.
void rebuild_loaded_unit_census(std::span<Civilization, 8> civilizations, const EntityPools& pools);
} // namespace openciv::state
