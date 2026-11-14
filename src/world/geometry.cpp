#include "world/geometry.hpp"
#include "base/arithmetic.hpp"
#include <algorithm>
#include <stdexcept>

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
