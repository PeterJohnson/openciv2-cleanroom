#pragma once

#include "formats/bytes.hpp"
#include "formats/stored_records.hpp"
#include "world/map.hpp"

namespace openciv::formats {
// F06 §3. Decoding is read-only and does not renew tokens, repair records, refresh
// derived fields or interpret text as UTF-8. All fixed-record bytes are retained.
inline constexpr std::size_t unit_record_size = 32;
inline constexpr std::size_t city_record_size = 88;
inline constexpr std::size_t tile_record_size = 6;
inline constexpr std::size_t region_record_size = 16;
inline constexpr std::size_t map_header_size = 14;
struct MapHeader {
    std::int16_t width = 0, height = 0, area = 0;
    std::uint16_t topology = 0, seed = 0;
    std::int16_t coarse_width = 0, coarse_height = 0;
    bool operator==(const MapHeader&) const = default;
};
StoredUnit decode_unit(std::span<const Byte> bytes);
StoredCity decode_city(std::span<const Byte> bytes);
world::Tile decode_tile(std::span<const Byte> bytes);
world::RegionSummary decode_region(std::span<const Byte> bytes);
MapHeader decode_map_header(std::span<const Byte> bytes);
Bytes encode_unit(const StoredUnit& value);
Bytes encode_city(const StoredCity& value);
Bytes encode_tile(const world::Tile& value);
Bytes encode_region(const world::RegionSummary& value);
Bytes encode_map_header(const MapHeader& value);
Bytes encode_unit(const state::Unit& value);
Bytes encode_city(const state::City& value);
} // namespace openciv::formats
