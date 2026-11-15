export module openciv.formats.classic_io;
import openciv.formats.state_conversion;

import std;
export import openciv.formats.record_compatibility;
export import openciv.formats.classic_records;

export namespace openciv::formats {
// F06 item service: a failed item may still return a transferred prefix. No
// rollback is implied. External I/O owns its cursor and byte-level side effects.
struct ReadResult {
    Bytes prefix;
    bool complete = false;
};
using ReadItem = std::function<ReadResult(std::size_t)>;
using WriteItem = std::function<bool(std::span<const Byte>)>;
class MemoryItems {
public:
    explicit MemoryItems(std::span<const Byte> bytes) : reader_(bytes) {}
    ReadResult read(std::size_t count);
    std::size_t position() const noexcept { return reader_.position(); }

private:
    ByteReader reader_;
};

struct StartingPositions {
    std::array<std::int16_t, 21> x, y;
    StartingPositions() {
        x.fill(-1);
        y.fill(-1);
    }
    bool operator==(const StartingPositions&) const = default;
};
struct RawMapFile {
    MapHeader header;
    StartingPositions starts;
    std::vector<world::Tile> tiles;
    Bytes trailing; // Original format requires no exact-EOF check.
    bool operator==(const RawMapFile&) const = default;
};
// F06 §4 stored view. Header sizes are retained, but dimension-derived sizes
// determine payload lengths, matching the reader. No live map is created here.
RawMapFile decode_raw_map(std::span<const Byte> bytes);
Bytes encode_raw_map(const RawMapFile& map);

enum class MapStream { raw, embedded };
bool write_map_items(const WriteItem& write, MapStream kind, const world::MapStorage& map,
                     const StartingPositions& starts);

bool write_entity_items(const WriteItem& write, const state::EntityPools& pools,
                        const RecordCompatibility& compatibility = {});

enum class VersionStatus { supported, unsupported_old, newer };
VersionStatus game_version_status(std::uint16_t version) noexcept;
struct GameHeader {
    std::uint8_t separator = 0x1a;
    std::uint16_t version = 0x2c;
    bool operator==(const GameHeader&) const = default;
};
GameHeader decode_game_header(std::span<const Byte> bytes);
Bytes encode_game_header(); // Current writer emits only 0x2c and the 0x1a separator.
} // namespace openciv::formats

module :private;
namespace openciv::formats {
namespace {
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
bool write_entity_items(const WriteItem& write, const state::EntityPools& pools,
                        const RecordCompatibility& compatibility) {
    if (pools.units.end() != 0 &&
        !write(encode_records(pools.units.scanned(), [&](const auto& value) {
            auto record = to_stored(value);
            const auto found = compatibility.units.find(value.token);
            if (found != compatibility.units.end())
                preserve_opaque(record, found->second);
            return encode_unit(record);
        })))
        return false;
    return pools.cities.end() == 0 ||
           write(encode_records(pools.cities.scanned(), [&](const auto& value) {
               auto record = to_stored(value);
               const auto found = compatibility.cities.find(value.token);
               if (found != compatibility.cities.end())
                   preserve_opaque(record, found->second);
               return encode_city(record);
           }));
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
