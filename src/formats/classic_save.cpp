#include "formats/classic_save.hpp"
#include <limits>

namespace openciv::formats {
namespace {
constexpr std::array<Byte, 4> event_tag{'E', 'V', 'N', 'T'};
bool has_event_tag(std::span<const Byte> bytes) {
    return bytes.size() >= event_tag.size() &&
           std::equal(event_tag.begin(), event_tag.end(), bytes.begin());
}
std::size_t leader_count(const state::GlobalValues& globals) {
    return globals.multiplayer == 0
               ? 1U
               : static_cast<std::size_t>(std::popcount(globals.human_players));
}
bool has_scenario(const state::GlobalValues& globals) {
    return (globals.game_flags & 0xc0) != 0;
}
std::size_t count(std::int16_t value, std::size_t limit, const char* label) {
    if (value < 0 || static_cast<std::size_t>(value) > limit)
        throw std::invalid_argument(std::string{label} + " exceeds supported stored-save backing");
    return static_cast<std::size_t>(value);
}
template <class Decode> auto read_record(ByteReader& reader, std::size_t size, Decode decode) {
    const auto offset = reader.position();
    const auto bytes = reader.take(size);
    try {
        return decode(bytes);
    } catch (const DecodeError& error) {
        throw DecodeError(offset + error.offset(), error.what());
    }
}
Bytes read_bytes(ByteReader& reader, std::size_t count) {
    const auto bytes = reader.take(count);
    return {bytes.begin(), bytes.end()};
}
// Visit strings in F06 §8 order. No reinterpretation of presence as an address.
void event_strings(const EventRecord& r, auto& strings, auto visit) {
    const std::array names{r.unit_name, r.city_name, r.who_name, r.whom_name};
    for (std::size_t i = 0; i < names.size(); ++i)
        visit(names[i], strings.names[i]);
    bool ended = false;
    for (std::size_t i = 0; i < r.text.size(); ++i) {
        if (ended && r.text[i] != 0)
            throw std::invalid_argument("Unsupported noncontiguous event text markers");
        ended = ended || r.text[i] == 0;
        visit(r.text[i], strings.text[i]);
    }
    const std::array other{r.move_owner_name,     r.move_unit_name,     r.aggression_whom_name,
                           r.aggression_who_name, r.create_owner_name,  r.create_unit_name,
                           r.create_homecity,     r.auxiliary_civ_name, r.auxiliary_unit_name,
                           r.money_receiver_name, r.wave_file};
    for (std::size_t i = 0; i < other.size(); ++i)
        visit(other[i], strings.other[i]);
}
void check(bool condition, const char* message) {
    if (!condition)
        throw std::invalid_argument(message);
}
} // namespace

StoredSave decode_stored_save(std::span<const Byte> bytes) {
    ByteReader r{bytes};
    StoredSave save;
    save.header = read_record(r, 12, decode_game_header);
    const auto version = save.header.version;
    if (game_version_status(version) != VersionStatus::supported)
        throw DecodeError(10, "Unsupported classic save version");
    const auto fragments = [&](std::size_t size,
                               std::initializer_list<std::pair<std::size_t, std::size_t>> parts) {
        Bytes record(size);
        for (const auto [offset, length] : parts) {
            const auto bytes = r.take(length);
            std::copy(bytes.begin(), bytes.end(),
                      record.begin() + static_cast<std::ptrdiff_t>(offset));
        }
        return record;
    };
    if (version < 0x28)
        save.globals = decode_globals(
            fragments(global_record_size, {{0, 0x36}, {0x36, 0x5d}, {0x9a, 0x5d}, {0xfe, 0x4c}}));
    else
        save.globals = read_record(r, global_record_size, decode_globals);
    std::size_t units = 0, cities = 0;
    try {
        units = count(save.globals.unit_end, state::unit_capacity, "Unit scan bound");
        cities = count(save.globals.city_end, state::city_backing_capacity, "City scan bound");
    } catch (const std::invalid_argument& error) {
        throw DecodeError(
            12 + (save.globals.unit_end < 0 || save.globals.unit_end > 2048 ? 0x2e : 0x30),
            error.what());
    }
    for (auto& v : save.names)
        v = read_record(r, civilization_names_size, decode_civilization_names);
    for (auto& v : save.civilizations) {
        if (version >= 0x28) {
            v = read_record(r, civilization_record_size, decode_civilization);
        } else {
            auto record_bytes = fragments(civilization_record_size, {{0, 0x58},
                                                                     {0x58, 0xc},
                                                                     {0x66, 0xe},
                                                                     {0x74, 0x5d},
                                                                     {0xd8, 0x36},
                                                                     {0x116, 0x36},
                                                                     {0x154, 0x36}});
            r.take(1);
            const auto tail = r.take(0x402);
            std::copy(tail.begin(), tail.end(), record_bytes.begin() + 0x192);
            v = decode_civilization(record_bytes);
        }
    }
    const auto map_offset = r.position();
    auto& map = save.map;
    map.header = read_record(r, map_header_size, decode_map_header);
    const auto geometry = [&] {
        try {
            return world::Geometry{map.header.width, map.header.height};
        } catch (const std::invalid_argument& error) {
            throw DecodeError(map_offset, error.what());
        }
    }();
    for (auto& layer : map.remembered)
        layer = read_bytes(r, geometry.area());
    for (std::size_t i = 0; i < geometry.area(); ++i)
        map.tiles.push_back(read_record(r, tile_record_size, decode_tile));
    const auto coarse_size = static_cast<std::size_t>(geometry.coarse_width()) *
                             static_cast<std::size_t>(geometry.coarse_height());
    for (auto& grid : map.coarse)
        grid = read_bytes(r, coarse_size);
    for (auto& region : map.regions)
        region = read_record(r, region_record_size, decode_region);
    for (std::size_t i = 0; i < units; ++i)
        save.units.push_back(version < 0x29 ? decode_unit(fragments(unit_record_size, {{0, 26}}))
                                            : read_record(r, unit_record_size, decode_unit));
    for (std::size_t i = 0; i < cities; ++i)
        save.cities.push_back(version < 0x29 ? decode_city(fragments(city_record_size, {{0, 84}}))
                                             : read_record(r, city_record_size, decode_city));
    for (auto& tribe : save.tribes) {
        tribe.times_used = r.u8();
        tribe.city_name_index = r.u8();
        tribe.female = r.u8();
    }
    save.cursor_x = r.i16();
    save.cursor_y = r.i16();
    for (std::size_t i = 0; i < leader_count(save.globals); ++i)
        save.leaders.push_back(read_record(r, leader_record_size, decode_leader));
    for (auto& sample : save.power_history)
        for (auto& value : sample)
            value = r.u8();
    save.layout = read_record(r, layout_record_size, decode_layout);
    for (auto& view : save.views)
        view = read_record(r, 10, decode_map_view);
    if (has_scenario(save.globals))
        save.scenario = read_record(r, scenario_parameters_size, decode_scenario_parameters);
    save.eliminations = read_record(r, elimination_record_size, decode_elimination_history);
    if (version >= 0x29 && save.globals.multiplayer != 0)
        save.multiplayer = read_record(r, multiplayer_record_size, decode_multiplayer);
    if (has_event_tag(r.remaining())) {
        r.take(4);
        const auto count_offset = r.position();
        const auto event_count = r.i32();
        if (event_count < 0)
            throw DecodeError(count_offset, "Unsupported negative stored event count");
        // Bound allocation by available fixed records, not an untrusted count.
        if (static_cast<std::size_t>(event_count) > r.remaining().size() / event_record_size)
            throw DecodeError(r.position(), "Truncated event record block");
        save.events.emplace();
        for (std::int32_t i = 0; i < event_count; ++i) {
            StoredEvent event;
            event.record = read_record(r, event_record_size, decode_event_record);
            save.events->entries.push_back(std::move(event));
        }
        for (auto& event : save.events->entries) {
            const auto string_offset = r.position();
            try {
                event_strings(
                    event.record, event.strings, [&](std::uint32_t present, auto& target) {
                        if (present == 0)
                            return;
                        Bytes text;
                        for (;;) {
                            const auto byte = r.u8();
                            if (byte == 0)
                                break;
                            if (text.size() == 255)
                                throw DecodeError(r.position() - 1,
                                                  "Unsupported event string over 255 bytes");
                            text.push_back(byte);
                        }
                        target = std::move(text);
                    });
            } catch (const std::invalid_argument& error) {
                throw DecodeError(string_offset, error.what());
            }
        }
    }
    save.trailing = read_bytes(r, r.remaining().size());
    return save;
}

Bytes encode_stored_save(const StoredSave& save) {
    check(save.header.version == 0x2c, "Stored-save encoding supports only version 0x2c");
    check(save.civilizations.size() == 8, "Incorrect civilization record count");
    check(save.units.size() ==
              count(save.globals.unit_end, state::unit_capacity, "Unit scan bound"),
          "Unit vector does not match serialized scan bound");
    check(save.cities.size() ==
              count(save.globals.city_end, state::city_backing_capacity, "City scan bound"),
          "City vector does not match serialized scan bound");
    check(save.leaders.size() == leader_count(save.globals), "Incorrect saved leader count");
    check(save.scenario.has_value() == has_scenario(save.globals), "Scenario block/flags disagree");
    check(save.multiplayer.has_value() == (save.globals.multiplayer != 0),
          "Multiplayer block/mode disagree");
    check(save.events.has_value() || !has_event_tag(save.trailing),
          "Trailing EVNT tag needs a decoded event block");
    const auto& map = save.map;
    const world::Geometry geometry{map.header.width, map.header.height};
    check(map.tiles.size() == geometry.area(), "Incorrect stored tile count");
    for (const auto& layer : map.remembered)
        check(layer.size() == geometry.area(), "Incorrect stored memory layer size");
    const auto coarse_size = static_cast<std::size_t>(geometry.coarse_width()) *
                             static_cast<std::size_t>(geometry.coarse_height());
    for (const auto& grid : map.coarse)
        check(grid.size() == coarse_size, "Incorrect stored coarse grid size");
    ByteWriter w;
    auto header = encode_game_header();
    header[9] = save.header.separator;
    w.append(header);
    w.append(encode_globals(save.globals));
    for (const auto& v : save.names)
        w.append(encode_civilization_names(v));
    for (const auto& v : save.civilizations)
        w.append(encode_civilization(v));
    w.append(encode_map_header(map.header));
    for (const auto& layer : map.remembered)
        w.append(layer);
    for (const auto& tile : map.tiles)
        w.append(encode_tile(tile));
    for (const auto& grid : map.coarse)
        w.append(grid);
    for (const auto& region : map.regions)
        w.append(encode_region(region));
    for (const auto& unit : save.units)
        w.append(encode_unit(unit));
    for (const auto& city : save.cities)
        w.append(encode_city(city));
    for (const auto& tribe : save.tribes) {
        w.u8(tribe.times_used);
        w.u8(tribe.city_name_index);
        w.u8(tribe.female);
    }
    w.i16(save.cursor_x);
    w.i16(save.cursor_y);
    for (const auto& leader : save.leaders)
        w.append(encode_leader(leader));
    for (const auto& sample : save.power_history)
        w.append(sample);
    w.append(encode_layout(save.layout));
    for (const auto& view : save.views)
        w.append(encode_map_view(view));
    if (save.scenario)
        w.append(encode_scenario_parameters(*save.scenario));
    w.append(encode_elimination_history(save.eliminations));
    if (save.multiplayer)
        w.append(encode_multiplayer(*save.multiplayer));
    if (save.events) {
        check(save.events->entries.size() <=
                  static_cast<std::size_t>(std::numeric_limits<std::int32_t>::max()),
              "Too many stored events");
        w.append(event_tag);
        w.i32(static_cast<std::int32_t>(save.events->entries.size()));
        for (const auto& event : save.events->entries)
            w.append(encode_event_record(event.record));
        for (const auto& event : save.events->entries)
            event_strings(event.record, event.strings,
                          [&](std::uint32_t present, const auto& text) {
                              check((present != 0) == text.has_value(),
                                    "Event presence marker/string disagree");
                              if (!text)
                                  return;
                              check(text->size() <= 255, "Unsupported event string over 255 bytes");
                              check(std::find(text->begin(), text->end(), Byte{0}) == text->end(),
                                    "Embedded event string NUL");
                              w.append(*text);
                              w.u8(0);
                          });
    }
    w.append(save.trailing);
    return std::move(w.bytes);
}
} // namespace openciv::formats
