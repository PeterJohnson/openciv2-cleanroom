#pragma once

#include "formats/classic_records.hpp"
#include "state/civilization.hpp"
#include "state/events.hpp"
#include "state/history.hpp"
#include "state/layout.hpp"
#include "state/multiplayer.hpp"
#include "state/scenario.hpp"

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

// F06 stored representation only. Never a live rule/name record.
struct StoredLeader {
    std::int8_t attack = 0, expand = 0, civilize = 0;
    std::uint8_t times_used = 0, female = 0, city_name_index = 0;
    std::int16_t color = 0, city_style = 0, leader_name = 0, plural = 0, adjective = 0;
    std::array<std::int16_t, 2> names{};
    std::array<std::array<std::int16_t, 2>, 7> titles{};
    bool operator==(const StoredLeader&) const = default;
};

struct SavedRectangle {
    std::int16_t left = 0, top = 0, right = 0, bottom = 0;
    bool operator==(const SavedRectangle&) const = default;
};
struct SavedLayout {
    std::int16_t valid = 0;
    std::array<SavedRectangle, 13> rectangles;
    bool operator==(const SavedLayout&) const = default;
};
using SavedMapView = state::MapView;
using ScenarioParameters = state::ScenarioParameters;
using EliminationHistory = state::EliminationHistory;
using MultiplayerRecord = state::MultiplayerValues;
// Serialized presence words are retained bits, never pointers/IDs/offsets.
// Strings live separately in StoredEvent; this record alone executes nothing.
struct EventRecord : state::EventValues {
    std::uint32_t unit_name = 0, city_name = 0, who_name = 0, whom_name = 0;
    std::array<std::uint32_t, 20> text{};
    std::uint32_t move_owner_name = 0, move_unit_name = 0;
    std::uint32_t aggression_whom_name = 0, aggression_who_name = 0;
    std::uint32_t create_owner_name = 0, create_unit_name = 0, create_homecity = 0;
    std::uint32_t auxiliary_civ_name = 0, auxiliary_unit_name = 0;
    std::array<std::uint8_t, 40> retained{};
    std::uint32_t money_receiver_name = 0, wave_file = 0;
    bool operator==(const EventRecord&) const = default;
};
std::array<std::uint32_t*, state::event_string_count> event_markers(EventRecord& values);
state::GlobalValues decode_globals(std::span<const Byte> bytes);
state::Civilization decode_civilization(std::span<const Byte> bytes);
state::CivilizationNames decode_civilization_names(std::span<const Byte> bytes);
StoredLeader decode_leader(std::span<const Byte> bytes);
SavedLayout decode_layout(std::span<const Byte> bytes);
SavedMapView decode_map_view(std::span<const Byte> bytes);
ScenarioParameters decode_scenario_parameters(std::span<const Byte> bytes);
EliminationHistory decode_elimination_history(std::span<const Byte> bytes);
MultiplayerRecord decode_multiplayer(std::span<const Byte> bytes);
EventRecord decode_event_record(std::span<const Byte> bytes);
Bytes encode_globals(const state::GlobalValues& value);
Bytes encode_civilization(const state::Civilization& value);
Bytes encode_civilization_names(const state::CivilizationNames& value);
Bytes encode_leader(const StoredLeader& value);
Bytes encode_layout(const SavedLayout& value);
Bytes encode_map_view(const SavedMapView& value);
Bytes encode_scenario_parameters(const ScenarioParameters& value);
Bytes encode_elimination_history(const EliminationHistory& value);
Bytes encode_multiplayer(const MultiplayerRecord& value);
Bytes encode_event_record(const EventRecord& value);
} // namespace openciv::formats
