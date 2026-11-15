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
/// Adapt borrowed memory to the item-oriented classic file reader contract.
class MemoryItems {
public:
    /// Start an item reader over bytes that must outlive this adapter.
    explicit MemoryItems(std::span<const Byte> bytes) : reader_(bytes) {}
    /// Read one item, retaining the short-read result required by classic loading.
    io::ReadResult read(std::size_t count);
    /// Return the number of bytes consumed from the backing span.
    std::size_t position() const noexcept { return reader_.position(); }

private:
    ByteReader reader_;
};

/// A decoded standalone map, including starting positions and trailing bytes.
struct RawMapFile {
    MapHeader header;
    state::StartingPositions starts;
    std::vector<world::Tile> tiles;
    Bytes trailing; // Original format requires no exact-EOF check.
    bool operator==(const RawMapFile&) const = default;
};
/// F06 §4 stored view. Header sizes are retained, but dimension-derived sizes
/// determine payload lengths, matching the reader. No live map is created here.
RawMapFile decode_raw_map(std::span<const Byte> bytes);
/// Serialize a standalone map while retaining its stored header and trailing bytes.
Bytes encode_raw_map(const RawMapFile& map);

/// Select standalone-map or embedded-save map serialization.
enum class MapStream { raw, embedded };
/// Write the selected map representation in F06 item order; return false on write failure.
bool write_map_items(const WriteItem& write, MapStream kind, const world::MapStorage& map,
                     const state::StartingPositions& starts);

/// Write allocated unit/city records, preserving supplied opaque imported fields.
bool write_entity_items(const WriteItem& write, const state::EntityPools& pools,
                        const RecordCompatibility& compatibility = {});

/// Classify a stored save version relative to the supported classic format.
enum class VersionStatus { supported, unsupported_old, newer };
/// Classify the version without attempting to decode its payload.
VersionStatus game_version_status(std::uint16_t version) noexcept;
/// The separator and version from the classic game-file header.
struct GameHeader {
    std::uint8_t separator = 0x1a;
    std::uint16_t version = 0x2c;
    bool operator==(const GameHeader&) const = default;
};
/// Decode and validate the classic game-file header bytes.
GameHeader decode_game_header(std::span<const Byte> bytes);
/// Emit the current 0x2c game header with its 0x1a separator.
Bytes encode_game_header(); // Current writer emits only 0x2c and the 0x1a separator.
} // namespace openciv::formats
