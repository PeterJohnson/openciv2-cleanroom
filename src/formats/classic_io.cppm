export module openciv.formats.classic_io;

import std;
export import openciv.formats.record_compatibility;
export import openciv.formats.classic_records;

export extern "C++" {
    namespace openciv::formats {
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
}
