export module openciv.world.map;

import std;
export import openciv.base.callback_ref;
export import openciv.world.geometry;

export namespace openciv::world {
/// Classic terrain kinds in their serialized numeric order.
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
/// F02 §7 / W01: logical runtime fields. Codecs must encode each field explicitly.
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
    /// Report whether the terrain is ocean.
    bool ocean() const noexcept { return terrain == Terrain::ocean; }
    /// Test city presence while excluding the shared airbase bit pattern.
    bool city() const noexcept { return (features & 0x42) == 0x02; }
    /// Test the airbase feature-bit pattern.
    bool airbase() const noexcept { return (features & 0x42) == 0x42; }
    /// Test whether the tile has a unit-presence marker.
    bool unit() const noexcept { return (features & 1) != 0; }
    bool operator==(const Tile&) const = default;
};
/// Retained land, fertility, water, and adjacency summary for one region.
struct RegionSummary {
    std::int16_t land = 0;
    std::int16_t fertile = 0;
    std::int16_t water = 0;
    std::uint64_t neighboring_water = 0;
    std::array<std::uint8_t, 2> retained{};
    bool operator==(const RegionSummary&) const = default;
};

/// The semantic tile field targeted by a mutation.
enum class TileField { features, terrain, revelation, fertility, owner, region, claimant };
/// A tile update operand, bit-operation mode, and synchronization request.
struct TileMutation {
    TileField field;
    Position position;
    std::int32_t operand;
    std::int32_t set = 1;
    bool synchronize = false;
    bool operator==(const TileMutation&) const = default;
};
/// Current multiplayer routing controls read at the tile mutation boundary.
struct TileSyncControls {
    std::int32_t multiplayer_mode = 0;
    bool client = false;
    bool batching = false;
    bool enabled = false;
};
/// Select client batching, host request, or broadcast for a tile update.
enum class TileSyncRoute { client_batch, request_host, broadcast };
/// A semantic tile mutation paired with its selected synchronization route.
struct TileSyncEffect {
    TileSyncRoute route;
    TileMutation mutation;
    bool operator==(const TileSyncEffect&) const = default;
};
using TilePublisher = base::CallbackRef<void(const TileSyncEffect&)>;

/// Own map layers while borrowing the process fallback tile for off-map access.
class MapStorage {
public:
    /// The owner supplies the shared fallback explicitly; allocation and release
    /// never initialize it. Separate process/peer contexts supply separate tiles.
    explicit MapStorage(Tile& fallback) noexcept : fallback_(fallback) {}
    MapStorage(const MapStorage&) = delete;
    MapStorage& operator=(const MapStorage&) = delete;

    /// Allocate layers for the supplied geometry, retaining replaced backing until release.
    void allocate(Geometry geometry);
    /// Release owned layers and retired backing without resetting retained map metadata.
    void release();
    /// Exchange owned storage after successful preparation; fallback bindings stay local.
    void swap_storage(MapStorage& other) noexcept;
    /// Report whether map allocation completed.
    bool allocated() const noexcept { return allocated_; }
    /// Borrow the current geometry; reject access when it is unavailable.
    const Geometry& geometry() const;
    /// Borrow mutable tile backing for the current map.
    std::span<Tile> tiles();
    /// Borrow immutable tile backing for the current map.
    std::span<const Tile> tiles() const;
    /// Return a tile or the shared fallback for an out-of-bounds position.
    Tile& tile(Position position);
    /// Read a tile or the shared fallback for an out-of-bounds position.
    const Tile& tile(Position position) const;
    /// Borrow a civilization remembered-map layer after checking its backing.
    std::span<std::uint8_t> remembered_layer(unsigned civilization);
    /// Read a civilization remembered-map layer after checking its backing.
    std::span<const std::uint8_t> remembered_layer(unsigned civilization) const;
    /// Borrow one remembered cell after validating its layer and position.
    std::uint8_t& remembered(unsigned civilization, Position position);
    /// Read one remembered cell after validating its layer and position.
    std::uint8_t remembered(unsigned civilization, Position position) const;
    /// Read a produced coarse-grid cell; reject cells with no established value.
    std::uint8_t coarse(unsigned grid, Position position) const;
    /// Set a coarse-grid cell and mark its value established.
    void set_coarse(unsigned grid, Position position, std::uint8_t value);
    /// Direct F06 transfer backing; absent cells have not been produced. The
    /// revision detects replacement/release across an external service call.
    std::span<std::optional<std::uint8_t>> coarse_grid(unsigned grid);
    /// Borrow coarse-grid values, preserving absent cells as nullopt.
    std::span<const std::optional<std::uint8_t>> coarse_grid(unsigned grid) const;

    /// Return an in-bounds tile owner, or nullopt for an absent/off-map owner.
    std::optional<std::uint8_t> owner(Position position) const;
    /// Return the tile owner only when the city feature is present.
    std::optional<std::uint8_t> city_owner(Position position) const;
    /// Return the tile owner only when the unit feature is present.
    std::optional<std::uint8_t> unit_owner(Position position) const;
    /// Return the tile owner only when the airbase feature is present.
    std::optional<std::uint8_t> airbase_owner(Position position) const;
    /// Prefer the city owner, then fall back to the unit owner.
    std::optional<std::uint8_t> city_or_unit_owner(Position position) const;
    /// Return the region for non-ocean terrain, or nullopt for ocean.
    std::optional<std::uint8_t> land_region(Position position) const;
    /// Return the claimant, or the W01 fertility-derived fallback when unclaimed.
    std::uint8_t claimant_or_fertility(Position position) const;
    /// Return the civilization revelation bit; negative civilization selectors return 1.
    std::uint32_t revelation(Position position, std::int32_t civilization) const;
    /// Try the base then its diagonal alternative for the requested ocean/land kind.
    bool select_terrain(Position base, bool ocean, bool flat, Position& selected) const;
    /// Return the seeded special-resource class, honoring terrain suppression.
    std::uint8_t special_resource(Position position) const;
    /// Test the seeded hut pattern on eligible unowned land.
    bool hut(Position position) const;
    /// Apply a local tile update, then publish any required synchronization effect.
    /// A missing or failing publisher does not roll back the local mutation.
    void mutate(TileMutation mutation, TileSyncControls controls = {},
                const TilePublisher& publish = {});

    // Retained values: allocation/release does not rebuild or reset these.
    std::uint16_t pattern_seed = 0;
    std::uint16_t stored_topology = 0;
    std::array<RegionSummary, 64> regions{};
    std::vector<std::uint8_t> remembered_barbarian;

private:
    template <class T> using Buffer = std::shared_ptr<std::vector<T>>;
    /// Allocate replacement backing and retain the previous buffer for admitted old views.
    template <class T> void replace(Buffer<T>& buffer, std::size_t count) {
        auto replacement = std::make_shared<std::vector<T>>(count);
        // W01: allocating a replacement does not release the previous storage.
        // Well-behaved setup/load callers explicitly release first. Retaining
        // replaced buffers also keeps any admitted old views backed.
        if (buffer)
            retired_.push_back(buffer);
        buffer = std::move(replacement);
    }
    /// Resolve remembered-layer coordinates against the supplied backing extent.
    std::size_t memory_index(Position position, std::size_t backing) const;
    /// Resolve a coarse-grid index after validating the grid and coordinates.
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
