#pragma once

#include <array>
#include <cstddef>
#include <cstdint>
#include <optional>
#include <span>
#include <vector>

namespace openciv::world {
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
