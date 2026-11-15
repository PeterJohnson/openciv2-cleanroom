export module openciv.world.geometry;
import openciv.base.arithmetic;

import std;

export namespace openciv::world {
/// A map position in doubled isometric coordinates.
struct Position {
    std::int32_t x = 0;
    std::int32_t y = 0;
    bool operator==(const Position&) const = default;
};

/// W01 §3: ordered inputs, not sets. Callers may supply altered tables without
/// sorting, normalizing, deduplicating, or replacing them with these defaults.
struct NeighborhoodTables {
    std::array<Position, 9> directions{
        {{1, -1}, {2, 0}, {1, 1}, {0, 2}, {-1, 1}, {-2, 0}, {-1, -1}, {0, -2}, {0, 0}}};
    std::array<Position, 5> diagonals{{{1, -1}, {1, 1}, {-1, 1}, {-1, -1}, {0, 0}}};
    std::array<Position, 8> coarse{
        {{0, -1}, {1, -1}, {1, 0}, {1, 1}, {0, 1}, {-1, 1}, {-1, 0}, {-1, -1}}};
    std::array<Position, 45> radius{
        {{1, -1},  {2, 0},   {1, 1},   {0, 2},  {-1, 1},  {-2, 0},  {-1, -1}, {0, -2},  {2, -2},
         {2, 2},   {-2, 2},  {-2, -2}, {1, -3}, {3, -1},  {3, 1},   {1, 3},   {-1, 3},  {-3, 1},
         {-3, -1}, {-1, -3}, {0, 0},   {4, 0},  {-4, 0},  {0, 4},   {0, -4},  {1, -5},  {2, -4},
         {3, -3},  {4, -2},  {5, -1},  {5, 1},  {4, 2},   {3, 3},   {2, 4},   {1, 5},   {-1, 5},
         {-2, 4},  {-3, 3},  {-4, 2},  {-5, 1}, {-5, -1}, {-4, -2}, {-3, -3}, {-2, -4}, {-1, -5}}};
};

/// W01 §§1–4: doubled coordinates and admitted classic dimensions. Topology is
/// supplied from the current game rule on each call, never cached from a file.
class Geometry {
public:
    /// Validate classic dimensions and compute backing area.
    /// @throws std::invalid_argument For invalid dimensions or area outside 1..32767.
    Geometry(std::int32_t width, std::int32_t height);
    /// Return the width in doubled-coordinate units.
    std::int32_t width() const noexcept { return width_; }
    /// Return the number of map rows.
    std::int32_t height() const noexcept { return height_; }
    /// Return the number of stored tiles, width / 2 times height.
    std::size_t area() const noexcept { return area_; }
    /// Return the number of coarse-grid columns.
    std::int32_t coarse_width() const noexcept { return (width_ + 3) / 4; }
    /// Return the number of coarse-grid rows.
    std::int32_t coarse_height() const noexcept { return (height_ + 3) / 4; }
    /// Check rectangular bounds without imposing coordinate parity.
    bool in_bounds(Position position) const noexcept;
    /// Check bounds and matching x/y parity for a canonical tile coordinate.
    bool canonical(Position position) const noexcept;
    /// Return a backing index for in-bounds coordinates, including parity aliases.
    std::optional<std::size_t> tile_index(Position position) const noexcept;
    /// Return the canonical coordinates of a checked backing index.
    Position coordinates(std::size_t index) const;
    /// Apply at most one horizontal wrap; flat maps retain the supplied x.
    std::int32_t wrap_x(std::int32_t x, bool flat) const noexcept;
    /// Apply at most one horizontal wrap using the coarse-grid width.
    std::int32_t coarse_wrap_x(std::int32_t x, bool flat) const noexcept;
    /// Compute horizontal separation with the current flat/cylindrical topology.
    std::int32_t wrapped_dx(std::int32_t x1, std::int32_t x2, bool flat) const noexcept;
    /// Compute W01 step distance using wrapping signed arithmetic.
    std::int32_t step_distance(Position first, Position second, bool flat) const;
    /// Compute W01 weighted distance using the current topology.
    std::int32_t weighted_distance(Position first, Position second, bool flat) const;
    /// Test a horizontal interval with the W01 wrapping and endpoint rules.
    bool contains_x(std::int32_t x, std::int32_t left, std::int32_t length,
                    bool flat) const noexcept;
    /// Test a map rectangle with horizontal topology and ordinary vertical bounds.
    bool contains_rectangle(Position position, Position top_left, Position extent,
                            bool flat) const noexcept;
    /// Apply ordered offsets and retain in-bounds neighbors without deduplication.
    std::vector<Position> neighbors(Position center, std::span<const Position> offsets,
                                    bool flat) const;

private:
    std::int32_t width_;
    std::int32_t height_;
    std::size_t area_;
};

/// Scalar queries also honor W01 Appendix A's accepted i32 arithmetic domain.
std::int32_t weighted_from_deltas(Position delta);
/// Return the last matching direction by delta signs, or 8 when none matches.
std::uint8_t direction_of(Position delta, const NeighborhoodTables& tables);
/// Test the coordinate pattern that supplies a grassland shield.
bool grassland_shield(Position position);
} // namespace openciv::world

module :private;
namespace openciv::world {
namespace {
using base::WrappingI32;
WrappingI32 abs32(WrappingI32 value) noexcept {
    return value <= WrappingI32{0} ? -value : value;
}
std::int32_t single_wrap(std::int32_t x, std::int32_t width, bool flat) noexcept {
    if (!flat) {
        if (x < 0)
            return static_cast<std::int32_t>(WrappingI32{x} + WrappingI32{width});
        if (x >= width)
            return static_cast<std::int32_t>(WrappingI32{x} - WrappingI32{width});
    }
    return x;
}
int sign(std::int32_t value) noexcept {
    return (value > 0) - (value < 0);
}
} // namespace

Geometry::Geometry(std::int32_t width, std::int32_t height)
    : width_(width), height_(height), area_(0) {
    // W01 §1 and the external signed-16 header domain. Reject unusable backing
    // before allocation; this does not validate scalar query coordinates.
    if (width <= 0 || width > 32767 || (width & 1) != 0 || height <= 0 || height > 32767)
        throw std::invalid_argument(
            "Map dimensions require positive even i16 width and positive i16 height");
    const auto area = static_cast<std::int64_t>(width / 2) * height;
    if (area >= 32768)
        throw std::invalid_argument("Map area must be in 1..32767");
    area_ = static_cast<std::size_t>(area);
}
bool Geometry::in_bounds(Position position) const noexcept {
    return position.x >= 0 && position.x < width_ && position.y >= 0 && position.y < height_;
}
bool Geometry::canonical(Position position) const noexcept {
    return in_bounds(position) && (position.x & 1) == (position.y & 1);
}
std::optional<std::size_t> Geometry::tile_index(Position position) const noexcept {
    if (!in_bounds(position))
        return std::nullopt;
    return static_cast<std::size_t>(position.y) * static_cast<std::size_t>(width_ / 2) +
           static_cast<std::size_t>(position.x / 2);
}
Position Geometry::coordinates(std::size_t index) const {
    if (index >= area_)
        throw std::out_of_range("Tile index outside map backing");
    const auto row_width = static_cast<std::size_t>(width_ / 2);
    const auto y = static_cast<std::int32_t>(index / row_width);
    return {static_cast<std::int32_t>(index % row_width) * 2 + (y & 1), y};
}
std::int32_t Geometry::wrap_x(std::int32_t x, bool flat) const noexcept {
    return single_wrap(x, width_, flat);
}
std::int32_t Geometry::coarse_wrap_x(std::int32_t x, bool flat) const noexcept {
    return single_wrap(x, coarse_width(), flat);
}
std::int32_t Geometry::wrapped_dx(std::int32_t x1, std::int32_t x2, bool flat) const noexcept {
    auto delta = abs32(WrappingI32{x1} - WrappingI32{x2});
    if (!flat && delta > WrappingI32{width_ / 2})
        delta = WrappingI32{width_} - delta;
    return static_cast<std::int32_t>(delta);
}
std::int32_t Geometry::step_distance(Position first, Position second, bool flat) const {
    const auto dx = WrappingI32{wrapped_dx(first.x, second.x, flat)};
    const auto dy = abs32(WrappingI32{first.y} - WrappingI32{second.y});
    return static_cast<std::int32_t>(base::shift_signed(dx + dy, WrappingI32{-1}));
}
std::int32_t weighted_from_deltas(Position delta) {
    const auto dx = abs32(WrappingI32{delta.x});
    const auto dy = abs32(WrappingI32{delta.y});
    const auto half_manhattan = base::shift_signed(dx + dy, WrappingI32{-1});
    const auto correction =
        base::shift_signed(half_manhattan - std::min(dx, dy) + WrappingI32{1}, WrappingI32{-1});
    return static_cast<std::int32_t>(std::max(dx, dy) - correction);
}
std::int32_t Geometry::weighted_distance(Position first, Position second, bool flat) const {
    return weighted_from_deltas(
        {wrapped_dx(first.x, second.x, flat),
         static_cast<std::int32_t>(abs32(WrappingI32{first.y} - WrappingI32{second.y}))});
}
bool Geometry::contains_x(std::int32_t x, std::int32_t left, std::int32_t length,
                          bool flat) const noexcept {
    auto adjusted = WrappingI32{x};
    const auto start = WrappingI32{left};
    const auto end = start + WrappingI32{length};
    if (!flat) {
        if (adjusted < start)
            adjusted += WrappingI32{width_};
        if (adjusted >= end)
            adjusted -= WrappingI32{width_};
    }
    return adjusted >= start && end - adjusted >= WrappingI32{1};
}
bool Geometry::contains_rectangle(Position position, Position origin, Position extent,
                                  bool flat) const noexcept {
    return contains_x(position.x, origin.x, extent.x, flat) && position.y >= origin.y &&
           WrappingI32{position.y} < WrappingI32{origin.y} + WrappingI32{extent.y};
}
std::vector<Position> Geometry::neighbors(Position center, std::span<const Position> offsets,
                                          bool flat) const {
    std::vector<Position> result;
    result.reserve(offsets.size());
    for (const auto delta : offsets) {
        const auto x = static_cast<std::int32_t>(WrappingI32{center.x} + WrappingI32{delta.x});
        const auto y = static_cast<std::int32_t>(WrappingI32{center.y} + WrappingI32{delta.y});
        const Position neighbor{wrap_x(x, flat), y};
        if (in_bounds(neighbor))
            result.push_back(neighbor);
    }
    return result;
}
std::uint8_t direction_of(Position delta, const NeighborhoodTables& tables) {
    std::uint8_t result = 8;
    for (std::uint8_t i = 0; i < 8; ++i)
        if (sign(delta.x) == sign(tables.directions[i].x) &&
            sign(delta.y) == sign(tables.directions[i].y))
            result = i;
    return result;
}
bool grassland_shield(Position position) {
    const auto half =
        base::shift_signed(WrappingI32{position.x} + WrappingI32{position.y}, WrappingI32{-1});
    return (static_cast<std::uint32_t>(-half - WrappingI32{position.x}) & 3U) < 2;
}
} // namespace openciv::world
