export module openciv.world.map;

import std;
export import openciv.world.geometry;

export extern "C++" {
    namespace openciv::world {
    enum class Terrain : std::uint8_t {
        desert,
        plains,
        grassland,
        forest,
        hills,
        mountains,
        tundra,
        glacier,
        swamp,
        jungle,
        ocean
    };
    // F02 §7 / W01: logical runtime fields. Codecs must encode each field explicitly.
    struct Tile {
        Terrain terrain = Terrain::ocean;
        std::uint8_t terrain_flags = 0; // Upper nibble: suppression 0x40, river 0x80.
        std::uint8_t features = 0;
        std::uint8_t claimant = 0;
        std::uint8_t radius_other = 0;
        std::uint8_t region = 0;
        std::uint8_t revelation = 0;
        std::uint8_t fertility = 0;
        std::optional<std::uint8_t> owner = std::uint8_t{0};
        bool ocean() const noexcept { return terrain == Terrain::ocean; }
        bool city() const noexcept { return (features & 0x42) == 0x02; }
        bool airbase() const noexcept { return (features & 0x42) == 0x42; }
        bool unit() const noexcept { return (features & 1) != 0; }
        bool operator==(const Tile&) const = default;
    };
    struct RegionSummary {
        std::int16_t land = 0;
        std::int16_t fertile = 0;
        std::int16_t water = 0;
        std::uint64_t neighboring_water = 0;
        std::array<std::uint8_t, 2> retained{};
        bool operator==(const RegionSummary&) const = default;
    };

    enum class TileField { features, terrain, revelation, fertility, owner, region, claimant };
    struct TileMutation {
        TileField field;
        Position position;
        std::int32_t operand;
        std::int32_t set = 1;
        bool synchronize = false;
        bool operator==(const TileMutation&) const = default;
    };
    struct TileSyncControls {
        std::int32_t multiplayer_mode = 0;
        bool client = false;
        bool batching = false;
        bool enabled = false;
    };
    enum class TileSyncRoute { client_batch, request_host, broadcast };
    struct TileSyncEffect {
        TileSyncRoute route;
        TileMutation mutation;
        bool operator==(const TileSyncEffect&) const = default;
    };
    using TilePublisher = std::function<void(const TileSyncEffect&)>;

    class MapStorage {
    public:
        // The owner supplies the shared fallback explicitly; allocation and release
        // never initialize it. Separate process/peer contexts supply separate tiles.
        explicit MapStorage(Tile& fallback) noexcept : fallback_(fallback) {}
        MapStorage(const MapStorage&) = delete;
        MapStorage& operator=(const MapStorage&) = delete;

        void allocate(Geometry geometry);
        void release();
        // Exchange owned storage after successful preparation; fallback bindings stay local.
        void swap_storage(MapStorage& other) noexcept;
        bool allocated() const noexcept { return allocated_; }
        const Geometry& geometry() const;
        std::span<Tile> tiles();
        std::span<const Tile> tiles() const;
        Tile& tile(Position position);
        const Tile& tile(Position position) const;
        std::span<std::uint8_t> remembered_layer(unsigned civilization);
        std::span<const std::uint8_t> remembered_layer(unsigned civilization) const;
        std::uint8_t& remembered(unsigned civilization, Position position);
        std::uint8_t remembered(unsigned civilization, Position position) const;
        std::uint8_t coarse(unsigned grid, Position position) const;
        void set_coarse(unsigned grid, Position position, std::uint8_t value);
        // Direct F06 transfer backing; absent cells have not been produced. The
        // revision detects replacement/release across an external service call.
        std::span<std::optional<std::uint8_t>> coarse_grid(unsigned grid);
        std::span<const std::optional<std::uint8_t>> coarse_grid(unsigned grid) const;

        std::optional<std::uint8_t> owner(Position position) const;
        std::optional<std::uint8_t> city_owner(Position position) const;
        std::optional<std::uint8_t> unit_owner(Position position) const;
        std::optional<std::uint8_t> airbase_owner(Position position) const;
        std::optional<std::uint8_t> city_or_unit_owner(Position position) const;
        std::optional<std::uint8_t> land_region(Position position) const;
        std::uint8_t claimant_or_fertility(Position position) const;
        std::uint32_t revelation(Position position, std::int32_t civilization) const;
        bool select_terrain(Position base, bool ocean, bool flat, Position& selected) const;
        std::uint8_t special_resource(Position position) const;
        bool hut(Position position) const;
        void mutate(TileMutation mutation, TileSyncControls controls = {},
                    const TilePublisher& publish = {});

        // Retained values: allocation/release does not rebuild or reset these.
        std::uint16_t pattern_seed = 0;
        std::uint16_t stored_topology = 0;
        std::array<RegionSummary, 64> regions{};
        std::vector<std::uint8_t> remembered_barbarian;

    private:
        template <class T> using Buffer = std::shared_ptr<std::vector<T>>;
        template <class T> void replace(Buffer<T>& buffer, std::size_t count) {
            auto replacement = std::make_shared<std::vector<T>>(count);
            // W01: allocating a replacement does not release the previous storage.
            // Well-behaved setup/load callers explicitly release first. Retaining
            // replaced buffers also keeps any admitted old views backed.
            if (buffer)
                retired_.push_back(buffer);
            buffer = std::move(replacement);
        }
        std::size_t memory_index(Position position, std::size_t backing) const;
        std::size_t coarse_index(unsigned grid, Position position) const;
        Tile& fallback_;
        std::optional<Geometry> geometry_;
        bool allocated_ = false;
        Buffer<Tile> tiles_;
        std::array<Buffer<std::uint8_t>, 8> remembered_;
        std::array<Buffer<std::optional<std::uint8_t>>, 4> coarse_;
        std::vector<std::shared_ptr<void>> retired_;
    };
    } // namespace openciv::world
}
