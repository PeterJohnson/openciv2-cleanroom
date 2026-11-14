#pragma once

#include <array>
#include <cstdint>

namespace openciv::state {
// F06 section 3: logical local layout, not native window handles. Coordinates
// remain full working integers until the specified save packing boundary.
struct Rectangle {
    std::int32_t left = 0, top = 0, right = 0, bottom = 0;
    bool operator==(const Rectangle&) const = default;
};
struct Layout {
    std::int16_t valid = 0;
    // Saved screen, main frame, eight maps, minimap, status, city window.
    std::array<Rectangle, 13> rectangles;
    bool operator==(const Layout&) const = default;
};
struct MapView {
    std::int16_t open = 0, track_moves = 0, x = 0, y = 0, zoom = 0;
    bool operator==(const MapView&) const = default;
};
} // namespace openciv::state
