#include "formats/world_record_io.hpp"
#include <algorithm>
#include <bit>

namespace openciv::formats {
namespace {
ReadResult read_item(const ReadItem& read, std::size_t length) {
    auto result = read(length);
    if (result.prefix.size() > length || (result.complete && result.prefix.size() != length))
        throw std::invalid_argument("Item service returned an inconsistent transferred prefix");
    return result;
}
template <class Record, class Encode, class Decode>
bool read_part(const ReadItem& read, Record& record, std::size_t offset, std::size_t length,
               Encode encode, Decode decode) {
    const auto result = read_item(read, length);
    auto bytes = encode(record); // Untransferred fields retain post-callback live values.
    std::copy(result.prefix.begin(), result.prefix.end(),
              bytes.begin() + static_cast<std::ptrdiff_t>(offset));
    record = decode(bytes);
    return result.complete;
}
template <class Range, class Encode> Bytes encode_array(Range records, Encode encode) {
    ByteWriter writer;
    for (const auto& record : records)
        writer.append(encode(record));
    return std::move(writer.bytes);
}
template <class Range, class Encode, class Decode>
bool read_array(const ReadItem& read, Range records, std::size_t size, Encode encode,
                Decode decode) {
    const auto result = read_item(read, records.size() * size);
    auto bytes = encode_array(records, encode);
    std::copy(result.prefix.begin(), result.prefix.end(), bytes.begin());
    ByteReader reader(bytes);
    for (auto& record : records)
        record = decode(reader.take(size));
    return result.complete;
}
void clear_legacy(state::GlobalValues& globals, std::span<state::Civilization, 8> civilizations) {
    globals.first_discoverer.fill(0);
    globals.advance_known_by.fill(0);
    for (auto& civ : civilizations) {
        civ.units_active.fill(0);
        civ.units_lost.fill(0);
        civ.units_in_production.fill(0);
        civ.advance_source.fill(0);
        civ.advances.fill(0);
    }
}
bool read_old_civilization(const ReadItem& read, state::Civilization& civ) {
    for (const auto [offset, length] : {std::pair{0x000U, 0x58U},
                                        {0x058U, 0x0cU},
                                        {0x066U, 0x0eU},
                                        {0x074U, 0x5dU},
                                        {0x0d8U, 0x36U},
                                        {0x116U, 0x36U},
                                        {0x154U, 0x36U}})
        if (!read_part(read, civ, offset, length, encode_civilization, decode_civilization))
            return false;
    if (!read_item(read, 1).complete) // Discarded byte is still a required item.
        return false;
    return read_part(read, civ, 0x192, 0x402, encode_civilization, decode_civilization);
}
} // namespace
bool read_persistent_items(const ReadItem& read, std::uint16_t version,
                           state::GlobalValues& globals,
                           std::span<state::CivilizationNames, 8> names,
                           std::span<state::Civilization, 8> civilizations, bool& scenario) {
    if (game_version_status(version) != VersionStatus::supported)
        throw std::invalid_argument("Unsupported core game record version");
    if (version < 0x28) {
        clear_legacy(globals, civilizations);
        for (const auto [offset, length] :
             {std::pair{0x000U, 0x36U}, {0x036U, 0x5dU}, {0x09aU, 0x5dU}, {0x0feU, 0x4cU}})
            if (!read_part(read, globals, offset, length, encode_globals, decode_globals))
                return false;
    } else if (!read_part(read, globals, 0, global_record_size, encode_globals, decode_globals)) {
        return false;
    }
    globals.num_enemies =
        static_cast<std::uint8_t>(std::popcount(globals.civs_alive) - 1 - (globals.civs_alive & 1));
    if (!read_array(read, names, civilization_names_size, encode_civilization_names,
                    decode_civilization_names))
        return false;
    if (version < 0x28) {
        for (auto& civ : civilizations)
            if (!read_old_civilization(read, civ))
                return false;
    } else if (!read_array(read, civilizations, civilization_record_size, encode_civilization,
                           decode_civilization)) {
        return false;
    }
    if (globals.multiplayer != 0)
        globals.game_options &= ~0x8000U;
    scenario = (globals.game_flags & 0xc0U) != 0;
    if (scenario)
        globals.game_flags |= 0x80U;
    if (globals.game_flags & 0x40U) {
        globals.game_options &= ~0x8000U;
        globals.reveal_map = 0;
        globals.game_flags &= static_cast<std::uint16_t>(~0x10U);
    }
    globals.game_flags &= static_cast<std::uint16_t>(~0x40U);
    return true;
}
bool write_persistent_items(const WriteItem& write, const state::GlobalValues& globals,
                            std::span<const state::CivilizationNames, 8> names,
                            std::span<const state::Civilization, 8> civilizations) {
    return write(encode_globals(globals)) &&
           write(encode_array(names, encode_civilization_names)) &&
           write(encode_array(civilizations, encode_civilization));
}
} // namespace openciv::formats
