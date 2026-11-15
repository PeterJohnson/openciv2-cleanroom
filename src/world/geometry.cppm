export module openciv.world.geometry;
import openciv.base.arithmetic;

import std;

export namespace openciv::world {
struct Position {
    std::int32_t x = 0;
    std::int32_t y = 0;
    bool operator==(const Position&) const = default;
};

// W01 §3: ordered inputs, not sets. Callers may supply altered tables without
// sorting, normalizing, deduplicating, or replacing them with these defaults.
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

// W01 §§1–4: doubled coordinates and admitted classic dimensions. Topology is
// supplied from the current game rule on each call, never cached from a file.
class Geometry {
public:
    Geometry(std::int32_t width, std::int32_t height);
    std::int32_t width() const noexcept { return width_; }
    std::int32_t height() const noexcept { return height_; }
    std::size_t area() const noexcept { return area_; }
    std::int32_t coarse_width() const noexcept { return (width_ + 3) / 4; }
    std::int32_t coarse_height() const noexcept { return (height_ + 3) / 4; }
    bool in_bounds(Position position) const noexcept;
    bool canonical(Position position) const noexcept;
    std::optional<std::size_t> tile_index(Position position) const noexcept;
    Position coordinates(std::size_t index) const;
    std::int32_t wrap_x(std::int32_t x, bool flat) const noexcept;
    std::int32_t coarse_wrap_x(std::int32_t x, bool flat) const noexcept;
    std::int32_t wrapped_dx(std::int32_t x1, std::int32_t x2, bool flat) const noexcept;
    std::int32_t step_distance(Position first, Position second, bool flat) const;
    std::int32_t weighted_distance(Position first, Position second, bool flat) const;
    bool contains_x(std::int32_t x, std::int32_t left, std::int32_t length,
                    bool flat) const noexcept;
    bool contains_rectangle(Position position, Position top_left, Position extent,
                            bool flat) const noexcept;
    std::vector<Position> neighbors(Position center, std::span<const Position> offsets,
                                    bool flat) const;

private:
    std::int32_t width_;
    std::int32_t height_;
    std::size_t area_;
};

// Scalar queries also honor W01 Appendix A's accepted i32 arithmetic domain.
std::int32_t weighted_from_deltas(Position delta);
std::uint8_t direction_of(Position delta, const NeighborhoodTables& tables);
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
bool Geometry::in_bounds(Position p) const noexcept {
    return p.x >= 0 && p.x < width_ && p.y >= 0 && p.y < height_;
}
bool Geometry::canonical(Position p) const noexcept {
    return in_bounds(p) && (p.x & 1) == (p.y & 1);
}
std::optional<std::size_t> Geometry::tile_index(Position p) const noexcept {
    if (!in_bounds(p))
        return std::nullopt;
    return static_cast<std::size_t>(p.y) * static_cast<std::size_t>(width_ / 2) +
           static_cast<std::size_t>(p.x / 2);
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
std::int32_t Geometry::step_distance(Position a, Position b, bool flat) const {
    const auto dx = WrappingI32{wrapped_dx(a.x, b.x, flat)};
    const auto dy = abs32(WrappingI32{a.y} - WrappingI32{b.y});
    return static_cast<std::int32_t>(base::shift_signed(dx + dy, WrappingI32{-1}));
}
std::int32_t weighted_from_deltas(Position delta) {
    const auto dx = abs32(WrappingI32{delta.x});
    const auto dy = abs32(WrappingI32{delta.y});
    const auto h = base::shift_signed(dx + dy, WrappingI32{-1});
    const auto correction =
        base::shift_signed(h - std::min(dx, dy) + WrappingI32{1}, WrappingI32{-1});
    return static_cast<std::int32_t>(std::max(dx, dy) - correction);
}
std::int32_t Geometry::weighted_distance(Position a, Position b, bool flat) const {
    return weighted_from_deltas(
        {wrapped_dx(a.x, b.x, flat),
         static_cast<std::int32_t>(abs32(WrappingI32{a.y} - WrappingI32{b.y}))});
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
bool Geometry::contains_rectangle(Position p, Position origin, Position extent,
                                  bool flat) const noexcept {
    return contains_x(p.x, origin.x, extent.x, flat) && p.y >= origin.y &&
           WrappingI32{p.y} < WrappingI32{origin.y} + WrappingI32{extent.y};
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
bool grassland_shield(Position p) {
    const auto half = base::shift_signed(WrappingI32{p.x} + WrappingI32{p.y}, WrappingI32{-1});
    return (static_cast<std::uint32_t>(-half - WrappingI32{p.x}) & 3U) < 2;
}
} // namespace openciv::world
