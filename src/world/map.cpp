#include "world/map.hpp"
#include "base/arithmetic.hpp"
#include <stdexcept>
#include <utility>

namespace openciv::world {
namespace {
using base::WrappingI32;
std::size_t checked_index(std::int32_t index, std::size_t size) {
    if (index < 0 || static_cast<std::size_t>(index) >= size)
        throw std::out_of_range("Direct grid index has no supplied backing");
    return static_cast<std::size_t>(index);
}
std::int32_t floor_shift(std::int32_t value, std::int32_t count) {
    return base::shift_signed(value, -count);
}
struct Pattern {
    std::int32_t u;
    std::int32_t v;
    std::uint32_t cell;
    std::int32_t pattern;
};
Pattern pattern_at(Position p, std::uint16_t seed) {
    // Only called after rectangular admission; W01 §1 bounds keep these sums
    // in i32. Negative v still requires floor division, not truncation.
    const auto u = (p.x + p.y) / 2;
    const auto v = p.x - u;
    return {u, v, (static_cast<std::uint32_t>(u) & 3U) + 4 * (static_cast<std::uint32_t>(v) & 3U),
            11 * ((p.x + p.y) / 8) + 13 * floor_shift(v, 2) + seed};
}
} // namespace

void MapStorage::allocate(Geometry shape, const AllocationCheck& check) {
    ++storage_revision_;
    geometry_ = shape;
    if (check)
        check({StorageKind::tiles, 0, shape.area()});
    replace(tiles_, shape.area()); // Tile defaults are exactly the W01 Ocean initialization.
    for (unsigned civ = 1; civ < 8; ++civ) {
        if (check)
            check({StorageKind::remembered, civ, shape.area()});
        replace(remembered_[civ], shape.area());
    }
    const auto coarse_area = static_cast<std::size_t>(shape.coarse_width()) *
                             static_cast<std::size_t>(shape.coarse_height());
    for (unsigned grid = 0; grid < 4; ++grid) {
        if (check)
            check({StorageKind::coarse, grid, coarse_area});
        replace(coarse_[grid], coarse_area); // nullopt: unknown, never implicit zero.
    }
    allocated_ = true;
}
void MapStorage::release(const ReleaseObserver& observer) {
    if (!allocated_)
        return;
    ++storage_revision_;
    for (unsigned i = 4; i > 0; --i) {
        auto& grid = coarse_[i - 1];
        if (grid) {
            const auto count = grid->size();
            grid.reset();
            if (observer)
                observer({StorageKind::coarse, i - 1, count});
        }
    }
    if (tiles_) {
        const auto count = tiles_->size();
        tiles_.reset();
        if (observer)
            observer({StorageKind::tiles, 0, count});
    }
    for (unsigned civ = 1; civ < 8; ++civ) {
        auto& layer = remembered_[civ];
        if (layer) {
            const auto count = layer->size();
            layer.reset();
            if (observer)
                observer({StorageKind::remembered, civ, count});
        }
    }
    allocated_ = false;
}
const Geometry& MapStorage::geometry() const {
    if (!geometry_)
        throw std::logic_error("Map dimensions have not been supplied");
    return *geometry_;
}
std::span<Tile> MapStorage::tiles() {
    if (!tiles_)
        throw std::logic_error("Tile storage is unavailable");
    return *tiles_;
}
std::span<const Tile> MapStorage::tiles() const {
    if (!tiles_)
        throw std::logic_error("Tile storage is unavailable");
    return *tiles_;
}
Tile& MapStorage::tile(Position p) {
    const auto index = geometry().tile_index(p);
    if (!index)
        return fallback_;
    const auto backing = tiles();
    return backing[checked_index(static_cast<std::int32_t>(*index), backing.size())];
}
const Tile& MapStorage::tile(Position p) const {
    const auto index = geometry().tile_index(p);
    if (!index)
        return fallback_;
    const auto backing = tiles();
    return backing[checked_index(static_cast<std::int32_t>(*index), backing.size())];
}
std::span<std::uint8_t> MapStorage::remembered_layer(unsigned civ) {
    if (civ >= 8)
        throw std::out_of_range("Unknown civilization memory layer");
    if (civ == 0)
        return remembered_barbarian;
    if (!remembered_[civ])
        throw std::logic_error("Remembered layer is unavailable");
    return *remembered_[civ];
}
std::span<const std::uint8_t> MapStorage::remembered_layer(unsigned civ) const {
    if (civ >= 8)
        throw std::out_of_range("Unknown civilization memory layer");
    if (civ == 0)
        return remembered_barbarian;
    if (!remembered_[civ])
        throw std::logic_error("Remembered layer is unavailable");
    return *remembered_[civ];
}
std::size_t MapStorage::memory_index(Position p, std::size_t backing) const {
    const auto index = WrappingI32{base::shift_signed(p.x, -1)} +
                       WrappingI32{geometry().width() / 2} * WrappingI32{p.y};
    return checked_index(static_cast<std::int32_t>(index), backing);
}
std::uint8_t& MapStorage::remembered(unsigned civ, Position p) {
    auto layer = remembered_layer(civ);
    return layer[memory_index(p, layer.size())];
}
std::uint8_t MapStorage::remembered(unsigned civ, Position p) const {
    const auto layer = remembered_layer(civ);
    return layer[memory_index(p, layer.size())];
}
std::size_t MapStorage::coarse_index(unsigned grid, Position p) const {
    if (grid >= 4 || !coarse_[grid])
        throw std::out_of_range("Coarse grid is unavailable");
    const auto index = WrappingI32{geometry().coarse_width()} * WrappingI32{p.y} + WrappingI32{p.x};
    return checked_index(static_cast<std::int32_t>(index), coarse_[grid]->size());
}
std::uint8_t MapStorage::coarse(unsigned grid, Position p) const {
    const auto index = coarse_index(grid, p);
    const auto cell = (*coarse_[grid])[index];
    if (!cell)
        throw std::logic_error("Coarse cell has not been initialized by a producer");
    return *cell;
}
void MapStorage::set_coarse(unsigned grid, Position p, std::uint8_t value) {
    const auto index = coarse_index(grid, p);
    (*coarse_[grid])[index] = value;
}
std::span<std::optional<std::uint8_t>> MapStorage::coarse_grid(unsigned grid) {
    if (grid >= coarse_.size() || !coarse_[grid])
        throw std::out_of_range("Coarse grid is unavailable");
    return *coarse_[grid];
}
std::span<const std::optional<std::uint8_t>> MapStorage::coarse_grid(unsigned grid) const {
    if (grid >= coarse_.size() || !coarse_[grid])
        throw std::out_of_range("Coarse grid is unavailable");
    return *coarse_[grid];
}
std::optional<std::uint8_t> MapStorage::owner(Position p) const {
    return geometry().in_bounds(p) ? tile(p).owner : std::nullopt;
}
std::optional<std::uint8_t> MapStorage::city_owner(Position p) const {
    return tile(p).city() ? owner(p) : std::nullopt;
}
std::optional<std::uint8_t> MapStorage::unit_owner(Position p) const {
    return tile(p).unit() ? owner(p) : std::nullopt;
}
std::optional<std::uint8_t> MapStorage::airbase_owner(Position p) const {
    return tile(p).airbase() ? owner(p) : std::nullopt;
}
std::optional<std::uint8_t> MapStorage::city_or_unit_owner(Position p) const {
    const auto city = city_owner(p);
    return city ? city : unit_owner(p);
}
std::optional<std::uint8_t> MapStorage::land_region(Position p) const {
    const auto& selected = tile(p);
    return selected.ocean() ? std::nullopt : std::optional{selected.region};
}
std::uint8_t MapStorage::claimant_or_fertility(Position p) const {
    const auto& selected = tile(p);
    if (selected.claimant != 0)
        return selected.claimant;
    return selected.fertility >= 1 && selected.fertility <= 8 ? 8 : selected.fertility;
}
std::uint32_t MapStorage::revelation(Position p, std::int32_t civ) const {
    if (civ < 0)
        return 1;
    return tile(p).revelation & (std::uint32_t{1} << (static_cast<std::uint32_t>(civ) & 31U));
}
bool MapStorage::select_terrain(Position base, bool ocean, bool flat, Position& selected) const {
    const std::array candidates{
        base, Position{static_cast<std::int32_t>(WrappingI32{base.x} + WrappingI32{1}),
                       static_cast<std::int32_t>(WrappingI32{base.y} + WrappingI32{1})}};
    for (auto p : candidates) {
        p.x = geometry().wrap_x(p.x, flat);
        if (geometry().in_bounds(p) && tile(p).ocean() == ocean) {
            selected = p;
            return true;
        }
    }
    return false;
}
std::uint8_t MapStorage::special_resource(Position p) const {
    if (!geometry().in_bounds(p))
        return 0;
    const auto& selected = tile(p);
    if ((selected.terrain_flags & 0x40) != 0 || selected.terrain == Terrain::grassland)
        return 0;
    const auto pattern = pattern_at(p, pattern_seed);
    if (pattern.cell != (static_cast<std::uint32_t>(pattern.pattern) & 15U))
        return 0;
    const auto scale_shift = static_cast<std::int32_t>((pattern_seed / 16) & 3);
    return (floor_shift(pattern.u, scale_shift) & 1) == (floor_shift(pattern.v, scale_shift) & 1)
               ? 2
               : 1;
}
bool MapStorage::hut(Position p) const {
    if (!geometry().in_bounds(p) || tile(p).ocean() || tile(p).owner)
        return false;
    const auto pattern = pattern_at(p, pattern_seed);
    return pattern.cell == (static_cast<std::uint32_t>(pattern.pattern + 8) & 31U);
}
void MapStorage::mutate(TileMutation mutation, TileSyncControls controls,
                        const TilePublisher& publish) {
    auto& selected = tile(mutation.position);
    const auto before = selected;
    const auto byte = static_cast<std::uint8_t>(mutation.operand);
    switch (mutation.field) {
    case TileField::features:
        selected.features = mutation.set != 0
                                ? static_cast<std::uint8_t>(selected.features | byte)
                                : static_cast<std::uint8_t>(selected.features & ~byte);
        break;
    case TileField::terrain:
        selected.terrain = static_cast<Terrain>(byte & 15);
        selected.terrain_flags |= byte & 0xf0;
        break;
    case TileField::revelation:
        selected.revelation = mutation.set != 0
                                  ? static_cast<std::uint8_t>(selected.revelation | byte)
                                  : static_cast<std::uint8_t>(selected.revelation & ~byte);
        break;
    case TileField::fertility:
        selected.fertility = byte & 15;
        break;
    case TileField::owner:
        selected.owner =
            mutation.operand >= 0 && mutation.operand <= 8 ? std::optional{byte} : std::nullopt;
        mutation.operand = selected.owner.value_or(15);
        break;
    case TileField::region:
        selected.region = byte;
        break;
    case TileField::claimant:
        selected.claimant = byte & 7;
        break;
    default:
        throw std::invalid_argument("Unknown tile field");
    }
    if (!mutation.synchronize || selected == before || controls.multiplayer_mode <= 2)
        return;
    std::optional<TileSyncRoute> route;
    if (controls.client && controls.batching)
        route = TileSyncRoute::client_batch;
    else if (controls.enabled)
        route = controls.client ? TileSyncRoute::request_host : TileSyncRoute::broadcast;
    if (route) {
        if (!publish)
            throw std::logic_error(
                "Tile synchronization requires a publisher after local mutation");
        publish({*route, mutation});
    }
}
} // namespace openciv::world
