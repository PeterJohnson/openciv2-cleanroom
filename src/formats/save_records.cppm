export module openciv.formats.save_records;

import std;
export import openciv.formats.classic_records;
export import openciv.state.civilization;
export import openciv.state.events;
export import openciv.state.history;
export import openciv.state.layout;
export import openciv.state.multiplayer;
export import openciv.state.scenario;

export namespace openciv::formats {
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

/// F06 stored representation only. Never a live rule/name record.
struct StoredLeader {
    std::int8_t attack = 0, expand = 0, civilize = 0;
    std::uint8_t times_used = 0, female = 0, city_name_index = 0;
    std::int16_t color = 0, city_style = 0, leader_name = 0, plural = 0, adjective = 0;
    std::array<std::int16_t, 2> names{};
    std::array<std::array<std::int16_t, 2>, 7> titles{};
    bool operator==(const StoredLeader&) const = default;
};

/// A rectangle stored in the classic save layout representation.
struct SavedRectangle {
    std::int16_t left = 0, top = 0, right = 0, bottom = 0;
    bool operator==(const SavedRectangle&) const = default;
};
/// Packed window layout values for classic save interchange.
struct SavedLayout {
    std::int16_t valid = 0;
    std::array<SavedRectangle, 13> rectangles;
    bool operator==(const SavedLayout&) const = default;
};

/// Serialized presence words are retained bits, never pointers/IDs/offsets.
/// Strings live separately in StoredEvent; this record alone executes nothing.
struct EventRecord : StoredEventValues {
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
/// Return mutable event string markers in their F06 serialized order.
std::array<std::uint32_t*, state::event_string_count> event_markers(EventRecord& values);
/// Decode the classic globals record from explicit little-endian fields.
StoredGlobals decode_globals(std::span<const Byte> bytes);
/// Decode the classic civilization record from explicit little-endian fields.
StoredCivilization decode_civilization(std::span<const Byte> bytes);
/// Decode the classic civilization names record from explicit little-endian fields.
StoredCivilizationNames decode_civilization_names(std::span<const Byte> bytes);
/// Decode the classic leader record from explicit little-endian fields.
StoredLeader decode_leader(std::span<const Byte> bytes);
/// Decode the classic layout record from explicit little-endian fields.
SavedLayout decode_layout(std::span<const Byte> bytes);
/// Decode the classic map view record from explicit little-endian fields.
SavedMapView decode_map_view(std::span<const Byte> bytes);
/// Decode the classic scenario parameters record from explicit little-endian fields.
ScenarioParameters decode_scenario_parameters(std::span<const Byte> bytes);
/// Decode the classic elimination history record from explicit little-endian fields.
EliminationHistory decode_elimination_history(std::span<const Byte> bytes);
/// Decode the classic multiplayer record from explicit little-endian fields.
MultiplayerRecord decode_multiplayer(std::span<const Byte> bytes);
/// Decode the classic event record record from explicit little-endian fields.
EventRecord decode_event_record(std::span<const Byte> bytes);
/// Serialize the globals record with explicit classic field widths and byte order.
Bytes encode_globals(const StoredGlobals& value);
/// Serialize the civilization record with explicit classic field widths and byte order.
Bytes encode_civilization(const StoredCivilization& value);
/// Serialize the civilization names record with explicit classic field widths and byte order.
Bytes encode_civilization_names(const StoredCivilizationNames& value);
/// Serialize the leader record with explicit classic field widths and byte order.
Bytes encode_leader(const StoredLeader& value);
/// Serialize the layout record with explicit classic field widths and byte order.
Bytes encode_layout(const SavedLayout& value);
/// Serialize the map view record with explicit classic field widths and byte order.
Bytes encode_map_view(const SavedMapView& value);
/// Serialize the scenario parameters record with explicit classic field widths and byte order.
Bytes encode_scenario_parameters(const ScenarioParameters& value);
/// Serialize the elimination history record with explicit classic field widths and byte order.
Bytes encode_elimination_history(const EliminationHistory& value);
/// Serialize the multiplayer record with explicit classic field widths and byte order.
Bytes encode_multiplayer(const MultiplayerRecord& value);
/// Serialize the event record record with explicit classic field widths and byte order.
Bytes encode_event_record(const EventRecord& value);
/// Serialize the globals record with explicit classic field widths and byte order.
Bytes encode_globals(const state::GlobalValues& value);
/// Serialize the civilization record with explicit classic field widths and byte order.
Bytes encode_civilization(const state::Civilization& value);
/// Serialize the civilization names record with explicit classic field widths and byte order.
Bytes encode_civilization_names(const state::CivilizationNames& value);
/// Serialize the map view record with explicit classic field widths and byte order.
Bytes encode_map_view(const state::MapView& value);
/// Serialize the scenario parameters record with explicit classic field widths and byte order.
Bytes encode_scenario_parameters(const state::ScenarioParameters& value);
/// Serialize the elimination history record with explicit classic field widths and byte order.
Bytes encode_elimination_history(const state::EliminationHistory& value);
/// Serialize the multiplayer record with explicit classic field widths and byte order.
Bytes encode_multiplayer(const state::MultiplayerValues& value);
} // namespace openciv::formats
