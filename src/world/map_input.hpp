#pragma once
#include "world/map.hpp"

namespace openciv::world {
struct MapInput {
    std::int32_t width = 0, height = 0;
    std::uint16_t topology = 0, seed = 0;
    std::vector<Tile> tiles;
    std::array<std::vector<std::uint8_t>, 7> remembered;
    std::array<std::vector<std::uint8_t>, 2> coarse;
    std::array<RegionSummary, 64> regions{};
};
void validate_map_input(const MapInput& input, bool embedded);
void install_map(const MapInput& input, MapStorage& map, bool embedded,
                 const AllocationCheck& allocation = {});
} // namespace openciv::world
