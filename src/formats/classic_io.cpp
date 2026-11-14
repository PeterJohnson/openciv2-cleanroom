#include "formats/classic_io.hpp"

namespace openciv::formats {
namespace {
bool transfer(const ReadItem& read, std::span<Byte> destination) {
    const auto result = read(destination.size());
    if (result.prefix.size() > destination.size() ||
        (result.complete && result.prefix.size() != destination.size()))
        throw std::invalid_argument("Item service returned an inconsistent transferred prefix");
    std::copy(result.prefix.begin(), result.prefix.end(), destination.begin());
    return result.complete;
}
template <class Range, class Encode> Bytes encode_records(const Range& records, Encode encode) {
    ByteWriter writer;
    for (const auto& record : records)
        writer.append(encode(record));
    return std::move(writer.bytes);
}
template <class Range, class Decode>
void publish_records(std::span<const Byte> bytes, Range records, std::size_t size, Decode decode) {
    ByteReader reader{bytes};
    for (auto& record : records)
        record = decode(reader.take(size));
}
template <class Range, class Encode, class Decode>
bool read_records(const ReadItem& read, Range records, std::size_t size, Encode encode,
                  Decode decode) {
    if (records.empty())
        return true; // F06 zero-size entity blocks are skipped.
    auto bytes = encode_records(records, encode);
    const bool complete = transfer(read, bytes);
    publish_records(bytes, records, size, decode);
    return complete;
}
Bytes encode_starts(const std::array<std::int16_t, 21>& values) {
    ByteWriter writer;
    for (const auto value : values)
        writer.i16(value);
    return std::move(writer.bytes);
}
void decode_starts(std::span<const Byte> bytes, std::array<std::int16_t, 21>& values) {
    ByteReader reader{bytes};
    for (auto& value : values)
        value = reader.i16();
}
world::Geometry admitted_geometry(const MapHeader& header) {
    try {
        return {header.width, header.height};
    } catch (const std::invalid_argument& error) {
        throw DecodeError(0, error.what());
    }
}
MapHeader header_for(const world::MapStorage& map) {
    const auto& shape = map.geometry();
    return {static_cast<std::int16_t>(shape.width()),
            static_cast<std::int16_t>(shape.height()),
            static_cast<std::int16_t>(shape.area()),
            map.stored_topology,
            map.pattern_seed,
            static_cast<std::int16_t>(shape.coarse_width()),
            static_cast<std::int16_t>(shape.coarse_height())};
}
} // namespace
ReadResult MemoryItems::read(std::size_t count) {
    const auto available = reader_.take(std::min(count, reader_.remaining().size()));
    return {{available.begin(), available.end()}, available.size() == count};
}
RawMapFile decode_raw_map(std::span<const Byte> bytes) {
    ByteReader reader{bytes};
    RawMapFile map;
    map.header = decode_map_header(reader.take(map_header_size));
    const auto shape = admitted_geometry(map.header);
    decode_starts(reader.take(42), map.starts.x);
    decode_starts(reader.take(42), map.starts.y);
    const auto tiles = reader.take(shape.area() * tile_record_size);
    map.tiles.resize(shape.area());
    publish_records(tiles, std::span{map.tiles}, tile_record_size, decode_tile);
    map.trailing.assign(reader.remaining().begin(), reader.remaining().end());
    return map;
}
Bytes encode_raw_map(const RawMapFile& map) {
    const auto shape = admitted_geometry(map.header);
    if (map.tiles.size() != shape.area())
        throw std::invalid_argument("Raw map tile count does not match dimension-derived area");
    ByteWriter writer;
    writer.append(encode_map_header(map.header));
    writer.append(encode_starts(map.starts.x));
    writer.append(encode_starts(map.starts.y));
    writer.append(encode_records(map.tiles, encode_tile));
    writer.append(map.trailing);
    return std::move(writer.bytes);
}
bool read_map_items(const ReadItem& read, MapStream kind, world::MapStorage& map,
                    StartingPositions& starts, const world::AllocationCheck& allocation) {
    Bytes header_bytes(map_header_size);
    if (!transfer(read, header_bytes))
        return false;
    const auto header = decode_map_header(header_bytes);
    const auto shape =
        admitted_geometry(header); // Malformed dimensions may reject before publication.
    if (kind == MapStream::raw) {
        for (auto* values : {&starts.x, &starts.y}) {
            auto bytes = encode_starts(*values);
            const bool complete = transfer(read, bytes);
            decode_starts(bytes, *values);
            if (!complete)
                return false;
        }
    } else {
        starts = {};
    }
    map.release();
    map.stored_topology = header.topology;
    map.pattern_seed = header.seed;
    map.allocate(shape, allocation);
    if (kind == MapStream::embedded) {
        for (unsigned civ = 1; civ < 8; ++civ)
            if (!transfer(read, map.remembered_layer(civ)))
                return false;
    }
    return read_records(read, map.tiles(), tile_record_size, encode_tile, decode_tile);
}
bool write_map_items(const WriteItem& write, MapStream kind, const world::MapStorage& map,
                     const StartingPositions& starts) {
    if (!map.allocated() || map.tiles().size() != map.geometry().area())
        throw std::invalid_argument("Map write requires complete allocated tile backing");
    if (!write(encode_map_header(header_for(map))))
        return false;
    if (kind == MapStream::raw) {
        if (!write(encode_starts(starts.x)) || !write(encode_starts(starts.y)))
            return false;
    } else {
        for (unsigned civ = 1; civ < 8; ++civ) {
            const auto layer = map.remembered_layer(civ);
            if (layer.size() != map.geometry().area())
                throw std::invalid_argument("Map memory layer does not match allocated geometry");
            if (!write(layer))
                return false;
        }
    }
    return write(encode_records(map.tiles(), encode_tile));
}
bool read_entity_items(const ReadItem& read, std::uint16_t version, state::EntityPools& pools,
                       state::ProcessTokens& tokens) {
    if (version < 0x26 || version > 0x2c)
        throw std::invalid_argument("Unsupported entity helper version");
    if (version < 0x2a) {
        const std::size_t length = version < 0x29 ? 26 : 30;
        for (auto& unit : pools.units.scanned()) {
            auto bytes = encode_unit(unit);
            const bool complete = transfer(read, std::span{bytes}.first(length));
            unit = decode_unit(bytes);
            if (!complete)
                return false;
        }
    } else if (!read_records(read, pools.units.scanned(), unit_record_size, encode_unit,
                             decode_unit)) {
        return false;
    }
    if (version < 0x29) {
        for (auto& city : pools.cities.scanned()) {
            auto bytes = encode_city(city);
            const bool complete = transfer(read, std::span{bytes}.first(84));
            city = decode_city(bytes);
            if (!complete)
                return false;
        }
    } else if (!read_records(read, pools.cities.scanned(), city_record_size, encode_city,
                             decode_city)) {
        return false;
    }
    state::renew_loaded_lifetimes(pools, tokens, version < 0x29);
    return true;
}
bool write_entity_items(const WriteItem& write, const state::EntityPools& pools) {
    if (pools.units.end() != 0 && !write(encode_records(pools.units.scanned(), encode_unit)))
        return false;
    return pools.cities.end() == 0 || write(encode_records(pools.cities.scanned(), encode_city));
}
VersionStatus game_version_status(std::uint16_t version) noexcept {
    if (version > 0x2c)
        return VersionStatus::newer;
    if (version < 0x26 || (version >= 0x29 && version <= 0x2b))
        return VersionStatus::unsupported_old;
    return VersionStatus::supported;
}
GameHeader decode_game_header(std::span<const Byte> bytes) {
    ByteReader reader{bytes};
    constexpr std::array<Byte, 9> magic{'C', 'I', 'V', 'I', 'L', 'I', 'Z', 'E', 0};
    const auto actual = reader.take(magic.size());
    if (!std::equal(actual.begin(), actual.end(), magic.begin()))
        throw DecodeError(0, "Unrecognized game header");
    const auto separator = reader.u8();
    return {separator, reader.u16()};
}
Bytes encode_game_header() {
    return {'C', 'I', 'V', 'I', 'L', 'I', 'Z', 'E', 0, 0x1a, 0x2c, 0};
}
} // namespace openciv::formats
