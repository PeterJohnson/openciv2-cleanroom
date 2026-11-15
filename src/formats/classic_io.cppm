export module openciv.formats.classic_io;

import std;
export import openciv.base.callback_ref;
export import openciv.state.starting_positions;
export import openciv.io.stream;
export import openciv.formats.record_compatibility;
export import openciv.formats.classic_records;

export namespace openciv::formats {
using ReadItem = base::CallbackRef<io::ReadResult(std::size_t)>;
using WriteItem = base::CallbackRef<bool(std::span<const Byte>)>;
class MemoryItems {
public:
    explicit MemoryItems(std::span<const Byte> bytes) : reader_(bytes) {}
    io::ReadResult read(std::size_t count);
    std::size_t position() const noexcept { return reader_.position(); }

private:
    ByteReader reader_;
};

struct RawMapFile {
    MapHeader header;
    state::StartingPositions starts;
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
                     const state::StartingPositions& starts);

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
