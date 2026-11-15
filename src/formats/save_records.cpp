#include "formats/record_fields.hpp"
#include <concepts>
#include <string_view>

namespace openciv::formats {
namespace {
// The ordered field lists in record_fields.hpp are the F06 §§3–4 file schema. Both directions
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
        } else if constexpr (requires { visit_fields(*this, v); }) {
            visit_fields(*this, v);
        } else {
            for (auto& entry : v)
                one(entry);
        }
    }
    template <class T> void operator()(std::string_view, T& v) { one(v); }
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
        } else if constexpr (requires { visit_fields(*this, v); }) {
            visit_fields(*this, v);
        } else {
            for (const auto& entry : v)
                one(entry);
        }
    }
    template <class T> void operator()(std::string_view, const T& v) { one(v); }
};

template <class T> T decode(std::span<const Byte> bytes, std::size_t size) {
    if (bytes.size() != size)
        throw DecodeError(std::min(size, bytes.size()), "Incorrect fixed-record length");
    Input input{ByteReader{bytes}};
    T value;
    visit_fields(input, value);
    if (!input.reader.remaining().empty())
        throw std::logic_error("Incomplete fixed-record schema");
    return value;
}
template <class T> Bytes encode(const T& value, std::size_t size) {
    Output output;
    visit_fields(output, value);
    if (output.writer.bytes.size() != size)
        throw std::logic_error("Incorrect fixed-record schema length");
    return std::move(output.writer.bytes);
}
} // namespace

state::GlobalValues decode_globals(std::span<const Byte> bytes) {
    return decode<state::GlobalValues>(bytes, global_record_size);
}
Bytes encode_globals(const state::GlobalValues& v) {
    return encode(v, global_record_size);
}
state::Civilization decode_civilization(std::span<const Byte> bytes) {
    return decode<state::Civilization>(bytes, civilization_record_size);
}
Bytes encode_civilization(const state::Civilization& v) {
    return encode(v, civilization_record_size);
}
state::CivilizationNames decode_civilization_names(std::span<const Byte> bytes) {
    return decode<state::CivilizationNames>(bytes, civilization_names_size);
}
Bytes encode_civilization_names(const state::CivilizationNames& v) {
    return encode(v, civilization_names_size);
}
StoredLeader decode_leader(std::span<const Byte> bytes) {
    return decode<StoredLeader>(bytes, leader_record_size);
}
Bytes encode_leader(const StoredLeader& v) {
    return encode(v, leader_record_size);
}
SavedLayout decode_layout(std::span<const Byte> bytes) {
    return decode<SavedLayout>(bytes, layout_record_size);
}
Bytes encode_layout(const SavedLayout& v) {
    return encode(v, layout_record_size);
}
SavedMapView decode_map_view(std::span<const Byte> bytes) {
    return decode<SavedMapView>(bytes, 10);
}
Bytes encode_map_view(const SavedMapView& v) {
    return encode(v, 10);
}
ScenarioParameters decode_scenario_parameters(std::span<const Byte> bytes) {
    return decode<ScenarioParameters>(bytes, scenario_parameters_size);
}
Bytes encode_scenario_parameters(const ScenarioParameters& v) {
    return encode(v, scenario_parameters_size);
}
EliminationHistory decode_elimination_history(std::span<const Byte> bytes) {
    return decode<EliminationHistory>(bytes, elimination_record_size);
}
Bytes encode_elimination_history(const EliminationHistory& v) {
    return encode(v, elimination_record_size);
}
MultiplayerRecord decode_multiplayer(std::span<const Byte> bytes) {
    return decode<MultiplayerRecord>(bytes, multiplayer_record_size);
}
Bytes encode_multiplayer(const MultiplayerRecord& v) {
    return encode(v, multiplayer_record_size);
}
EventRecord decode_event_record(std::span<const Byte> bytes) {
    return decode<EventRecord>(bytes, event_record_size);
}
Bytes encode_event_record(const EventRecord& v) {
    return encode(v, event_record_size);
}
std::array<std::uint32_t*, state::event_string_count> event_markers(EventRecord& v) {
    std::array<std::uint32_t*, state::event_string_count> result{};
    result[0] = &v.unit_name;
    result[1] = &v.city_name;
    result[2] = &v.who_name;
    result[3] = &v.whom_name;
    for (std::size_t i = 0; i < 20; ++i)
        result[4 + i] = &v.text[i];
    const std::array other{&v.move_owner_name,     &v.move_unit_name,     &v.aggression_whom_name,
                           &v.aggression_who_name, &v.create_owner_name,  &v.create_unit_name,
                           &v.create_homecity,     &v.auxiliary_civ_name, &v.auxiliary_unit_name,
                           &v.money_receiver_name, &v.wave_file};
    std::copy(other.begin(), other.end(), result.begin() + state::event_other_begin);
    return result;
}
} // namespace openciv::formats
