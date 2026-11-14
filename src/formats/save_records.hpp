#pragma once

#include "formats/classic_records.hpp"
#include "state/civilization.hpp"
#include "state/multiplayer.hpp"

namespace openciv::formats {
// F06 §§3–4 fixed encodings. These stored values have no native window, password
// decoding, scenario execution or session negotiation effects.
inline constexpr std::size_t global_record_size = 0x14a;
inline constexpr std::size_t civilization_record_size = 0x594;
inline constexpr std::size_t civilization_names_size = 0xf2;
inline constexpr std::size_t leader_record_size = 0x30;
inline constexpr std::size_t layout_record_size = 106;
inline constexpr std::size_t scenario_parameters_size = 100;
inline constexpr std::size_t elimination_record_size = 338;
inline constexpr std::size_t multiplayer_record_size = 1172;
inline constexpr std::size_t event_record_size = 444;

struct SavedRectangle {
    std::int16_t left = 0, top = 0, right = 0, bottom = 0;
    bool operator==(const SavedRectangle&) const = default;
};
struct SavedLayout {
    std::int16_t valid = 0;
    std::array<SavedRectangle, 13> rectangles;
    bool operator==(const SavedLayout&) const = default;
};
struct SavedMapView {
    std::int16_t open = 0, track_moves = 0, x = 0, y = 0, zoom = 0;
    bool operator==(const SavedMapView&) const = default;
};
struct ScenarioParameters {
    std::uint16_t flags = 0;
    state::FixedText<80> name;
    std::int16_t technology_paradigm = 0, year_increment = 0, start_year = 0;
    std::int16_t maximum_turns = 0, objective_civ = 0;
    std::array<std::int16_t, 4> victory_thresholds{};
    bool operator==(const ScenarioParameters&) const = default;
};
struct EliminationHistory {
    std::int16_t count = 0;
    std::array<std::int16_t, 12> turns{};
    std::array<Byte, 12> killers{}, portraits{};
    std::array<state::FixedText<24>, 12> names;
    bool operator==(const EliminationHistory&) const = default;
};
using MultiplayerRecord = state::MultiplayerValues;
// Serialized presence words are retained bits, never pointers/IDs/offsets.
// Strings live separately in StoredEvent; this record alone executes nothing.
struct EventRecord {
    std::uint32_t triggers = 0, actions = 0, unit_name = 0;
    std::int32_t unit_type = 0;
    std::uint32_t city_name = 0, who_name = 0;
    std::int32_t who_civ = 0, talker_type = 0;
    std::uint32_t whom_name = 0;
    std::int32_t whom_civ = 0, listener_type = 0, turn = 0, denominator = 0, technology = 0;
    std::array<std::uint32_t, 20> text{};
    std::uint32_t move_owner_name = 0;
    std::int32_t move_owner_civ = 0;
    std::uint32_t move_unit_name = 0;
    std::int32_t move_unit_type = 0, move_count = 0;
    std::array<std::int32_t, 8> move_rectangle{};
    std::int32_t move_x = 0, move_y = 0;
    std::uint32_t aggression_whom_name = 0;
    std::int32_t aggression_whom_civ = 0;
    std::uint32_t aggression_who_name = 0;
    std::int32_t aggression_who_civ = 0;
    std::uint32_t create_owner_name = 0;
    std::int32_t create_owner_civ = 0;
    std::uint32_t create_unit_name = 0;
    std::int32_t create_unit_type = 0;
    std::array<std::int32_t, 20> create_locations{};
    std::int32_t create_location_count = 0, create_veteran = 0;
    std::uint32_t create_homecity = 0, auxiliary_civ_name = 0;
    std::int32_t auxiliary_scalar = 0;
    std::uint32_t auxiliary_unit_name = 0;
    std::array<Byte, 40> retained{};
    std::uint32_t money_receiver_name = 0;
    std::int32_t money_receiver_civ = 0, money_amount = 0, negotiation_flag = 0;
    std::uint32_t wave_file = 0;
    std::int32_t cd_track = 0, terrain_type = 0;
    std::array<std::int32_t, 8> terrain_rectangle{};
    std::int32_t destroy_civ = 0, give_tech = 0, give_tech_civ = 0;
    bool operator==(const EventRecord&) const = default;
};
state::GlobalValues decode_globals(std::span<const Byte> bytes);
state::Civilization decode_civilization(std::span<const Byte> bytes);
state::CivilizationNames decode_civilization_names(std::span<const Byte> bytes);
state::Leader decode_leader(std::span<const Byte> bytes);
SavedLayout decode_layout(std::span<const Byte> bytes);
SavedMapView decode_map_view(std::span<const Byte> bytes);
ScenarioParameters decode_scenario_parameters(std::span<const Byte> bytes);
EliminationHistory decode_elimination_history(std::span<const Byte> bytes);
MultiplayerRecord decode_multiplayer(std::span<const Byte> bytes);
EventRecord decode_event_record(std::span<const Byte> bytes);
Bytes encode_globals(const state::GlobalValues& value);
Bytes encode_civilization(const state::Civilization& value);
Bytes encode_civilization_names(const state::CivilizationNames& value);
Bytes encode_leader(const state::Leader& value);
Bytes encode_layout(const SavedLayout& value);
Bytes encode_map_view(const SavedMapView& value);
Bytes encode_scenario_parameters(const ScenarioParameters& value);
Bytes encode_elimination_history(const EliminationHistory& value);
Bytes encode_multiplayer(const MultiplayerRecord& value);
Bytes encode_event_record(const EventRecord& value);
} // namespace openciv::formats
