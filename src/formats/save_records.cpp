#include "formats/save_records.hpp"
#include <concepts>

namespace openciv::formats {
namespace {
// The ordered field lists below are the F06 §§3–4 file schema. Both directions
// visit that schema, never host struct storage or padding. Arrays retain order.
struct Input {
    ByteReader reader;
    template <class T> void one(T& v) {
        if constexpr (std::integral<T>) {
            if constexpr (sizeof(T) == 1)
                v = std::bit_cast<T>(reader.u8());
            else if constexpr (sizeof(T) == 2)
                v = std::bit_cast<T>(reader.u16());
            else
                v = std::bit_cast<T>(reader.u32());
        } else if constexpr (requires { v.bytes; }) {
            one(v.bytes);
        } else if constexpr (requires { v.value; }) {
            one(v.value);
        } else if constexpr (std::same_as<T, state::CivilizationGoal>) {
            (*this)(v.x, v.y, v.role, v.priority);
        } else if constexpr (std::same_as<T, SavedRectangle>) {
            (*this)(v.left, v.top, v.right, v.bottom);
        } else {
            for (auto& entry : v)
                one(entry);
        }
    }
    template <class... T> void operator()(T&... v) { (one(v), ...); }
};
struct Output {
    ByteWriter writer;
    template <class T> void one(const T& v) {
        if constexpr (std::integral<T>) {
            if constexpr (sizeof(T) == 1)
                writer.u8(static_cast<std::uint8_t>(v));
            else if constexpr (sizeof(T) == 2)
                writer.u16(static_cast<std::uint16_t>(v));
            else
                writer.u32(static_cast<std::uint32_t>(v));
        } else if constexpr (requires { v.bytes; }) {
            one(v.bytes);
        } else if constexpr (requires { v.value; }) {
            one(v.value);
        } else if constexpr (std::same_as<T, state::CivilizationGoal>) {
            (*this)(v.x, v.y, v.role, v.priority);
        } else if constexpr (std::same_as<T, SavedRectangle>) {
            (*this)(v.left, v.top, v.right, v.bottom);
        } else {
            for (const auto& entry : v)
                one(entry);
        }
    }
    template <class... T> void operator()(const T&... v) { (one(v), ...); }
};
void globals_fields(auto& a, auto& v) {
    a(v.rules, v.game_options, v.transient_flags, v.game_flags, v.message_options,
      v.shown_event_flags, v.retained_0e, v.turn, v.year, v.first_launch_turn, v.active_unit,
      v.moving_unit, v.multiplayer, v.player_civ, v.saved_current_civ, v.turn_civ, v.view_civ,
      v.reveal_map, v.difficulty, v.barbarian_activity, v.civs_alive, v.human_players,
      v.civs_at_start, v.num_enemies, v.warming_counter, v.global_warmings, v.warming_pollution,
      v.polluted_tiles, v.peace_turns, v.unit_end, v.city_end, v.num_advances, v.retained_34,
      v.first_discoverer, v.advance_known_by, v.wonder_city, v.next_history_turn, v.best_human_rank,
      v.worst_human_rank, v.power_rank, v.civ_by_rank);
}
void civilization_fields(auto& a, auto& v) {
    a(v.flags, v.gold, v.tribe, v.research_progress, v.researching, v.capital_x, v.last_city_turn,
      v.num_advances, v.future_techs, v.research_cost_advances, v.science_rate, v.tax_rate,
      v.government, v.diplo_random, v.wonders_by_group, v.retained_1b, v.subversion_count,
      v.reputation, v.diplo_request_count, v.treaties, v.attitude, v.reputation_credit,
      v.aggression_counter, v.advances, v.retained_65, v.military_count, v.city_count, v.ship_count,
      v.total_city_size, v.military_strength, v.total_attack, v.population_milestone,
      v.advance_source, v.units_active, v.units_lost, v.units_in_production, v.continent_military,
      v.continent_attack, v.continent_cities, v.continent_city_size, v.continent_trade_units,
      v.continent_flags, v.continent_policy, v.best_shield_surplus, v.government_score,
      v.last_contact_turn, v.leader_portrait, v.throne_room, v.retained_3fd, v.throne_room_upgrades,
      v.spaceship_flags, v.retained_401, v.spaceship_arrival_year, v.spaceship_launch_year,
      v.spaceship_population, v.spaceship_parts, v.goals, v.standing_goals);
}
void names_fields(auto& a, auto& v) {
    a(v.city_style, v.leader, v.tribe, v.adjective, v.titles);
}
void leader_fields(auto& a, auto& v) {
    a(v.attack, v.expand, v.civilize, v.times_used, v.female, v.city_name_index, v.color,
      v.city_style, v.leader_name, v.plural, v.adjective, v.names, v.titles);
}
void layout_fields(auto& a, auto& v) {
    a(v.valid, v.rectangles);
}
void view_fields(auto& a, auto& v) {
    a(v.open, v.track_moves, v.x, v.y, v.zoom);
}
void scenario_fields(auto& a, auto& v) {
    a(v.flags, v.name, v.technology_paradigm, v.year_increment, v.start_year, v.maximum_turns,
      v.objective_civ, v.victory_thresholds);
}
void elimination_fields(auto& a, auto& v) {
    a(v.count, v.turns, v.killers, v.portraits, v.names);
}
void multiplayer_fields(auto& a, auto& v) {
    a(v.center_x, v.center_y, v.introduced, v.turn_time_limit, v.encoded_passwords, v.open_game,
      v.kill_on_retirement, v.show_human_starts, v.chat_from_start, v.simultaneous,
      v.cheat_turn_time, v.retained_142, v.emails, v.player_setup, v.resume_civ, v.retained_465,
      v.resume_event, v.autoplay_active, v.autoplay_turns, v.double_production, v.double_movement,
      v.claimed_humans, v.retained_472);
}
void event_fields(auto& a, auto& v) {
    a(v.triggers, v.actions, v.unit_name, v.unit_type, v.city_name, v.who_name, v.who_civ,
      v.talker_type, v.whom_name, v.whom_civ, v.listener_type, v.turn, v.denominator, v.technology,
      v.text, v.move_owner_name, v.move_owner_civ, v.move_unit_name, v.move_unit_type, v.move_count,
      v.move_rectangle, v.move_x, v.move_y, v.aggression_whom_name, v.aggression_whom_civ,
      v.aggression_who_name, v.aggression_who_civ, v.create_owner_name, v.create_owner_civ,
      v.create_unit_name, v.create_unit_type, v.create_locations, v.create_location_count,
      v.create_veteran, v.create_homecity, v.auxiliary_civ_name, v.auxiliary_scalar,
      v.auxiliary_unit_name, v.retained, v.money_receiver_name, v.money_receiver_civ,
      v.money_amount, v.negotiation_flag, v.wave_file, v.cd_track, v.terrain_type,
      v.terrain_rectangle, v.destroy_civ, v.give_tech, v.give_tech_civ);
}
template <class T, class Fields>
T decode(std::span<const Byte> bytes, std::size_t size, Fields fields) {
    if (bytes.size() != size)
        throw DecodeError(std::min(size, bytes.size()), "Incorrect fixed-record length");
    Input input{ByteReader{bytes}};
    T value;
    fields(input, value);
    if (!input.reader.remaining().empty())
        throw std::logic_error("Incomplete fixed-record schema");
    return value;
}
template <class T, class Fields> Bytes encode(const T& value, std::size_t size, Fields fields) {
    Output output;
    fields(output, value);
    if (output.writer.bytes.size() != size)
        throw std::logic_error("Incorrect fixed-record schema length");
    return std::move(output.writer.bytes);
}
} // namespace

state::GlobalValues decode_globals(std::span<const Byte> bytes) {
    return decode<state::GlobalValues>(bytes, global_record_size,
                                       globals_fields<Input, state::GlobalValues>);
}
Bytes encode_globals(const state::GlobalValues& v) {
    return encode(v, global_record_size, globals_fields<Output, const state::GlobalValues>);
}
state::Civilization decode_civilization(std::span<const Byte> bytes) {
    return decode<state::Civilization>(bytes, civilization_record_size,
                                       civilization_fields<Input, state::Civilization>);
}
Bytes encode_civilization(const state::Civilization& v) {
    return encode(v, civilization_record_size,
                  civilization_fields<Output, const state::Civilization>);
}
state::CivilizationNames decode_civilization_names(std::span<const Byte> bytes) {
    return decode<state::CivilizationNames>(bytes, civilization_names_size,
                                            names_fields<Input, state::CivilizationNames>);
}
Bytes encode_civilization_names(const state::CivilizationNames& v) {
    return encode(v, civilization_names_size, names_fields<Output, const state::CivilizationNames>);
}
state::Leader decode_leader(std::span<const Byte> bytes) {
    return decode<state::Leader>(bytes, leader_record_size, leader_fields<Input, state::Leader>);
}
Bytes encode_leader(const state::Leader& v) {
    return encode(v, leader_record_size, leader_fields<Output, const state::Leader>);
}
SavedLayout decode_layout(std::span<const Byte> bytes) {
    return decode<SavedLayout>(bytes, layout_record_size, layout_fields<Input, SavedLayout>);
}
Bytes encode_layout(const SavedLayout& v) {
    return encode(v, layout_record_size, layout_fields<Output, const SavedLayout>);
}
SavedMapView decode_map_view(std::span<const Byte> bytes) {
    return decode<SavedMapView>(bytes, 10, view_fields<Input, SavedMapView>);
}
Bytes encode_map_view(const SavedMapView& v) {
    return encode(v, 10, view_fields<Output, const SavedMapView>);
}
ScenarioParameters decode_scenario_parameters(std::span<const Byte> bytes) {
    return decode<ScenarioParameters>(bytes, scenario_parameters_size,
                                      scenario_fields<Input, ScenarioParameters>);
}
Bytes encode_scenario_parameters(const ScenarioParameters& v) {
    return encode(v, scenario_parameters_size, scenario_fields<Output, const ScenarioParameters>);
}
EliminationHistory decode_elimination_history(std::span<const Byte> bytes) {
    return decode<EliminationHistory>(bytes, elimination_record_size,
                                      elimination_fields<Input, EliminationHistory>);
}
Bytes encode_elimination_history(const EliminationHistory& v) {
    return encode(v, elimination_record_size, elimination_fields<Output, const EliminationHistory>);
}
MultiplayerRecord decode_multiplayer(std::span<const Byte> bytes) {
    return decode<MultiplayerRecord>(bytes, multiplayer_record_size,
                                     multiplayer_fields<Input, MultiplayerRecord>);
}
Bytes encode_multiplayer(const MultiplayerRecord& v) {
    return encode(v, multiplayer_record_size, multiplayer_fields<Output, const MultiplayerRecord>);
}
EventRecord decode_event_record(std::span<const Byte> bytes) {
    return decode<EventRecord>(bytes, event_record_size, event_fields<Input, EventRecord>);
}
Bytes encode_event_record(const EventRecord& v) {
    return encode(v, event_record_size, event_fields<Output, const EventRecord>);
}
} // namespace openciv::formats
