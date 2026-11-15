export module openciv.world.map_input;

import std;
export import openciv.world.map;

export namespace openciv::world {
struct MapInput {
    std::int32_t width = 0, height = 0;
    std::uint16_t topology = 0, seed = 0;
    std::vector<Tile> tiles;
    std::array<std::vector<std::uint8_t>, 7> remembered;
    std::array<std::vector<std::uint8_t>, 2> coarse;
    std::array<RegionSummary, 64> regions{};
};
void validate_map_input(const MapInput& input, bool embedded);
void install_map(const MapInput& input, MapStorage& map, bool embedded);
} // namespace openciv::world

module :private;
namespace openciv::world {
void validate_map_input(const MapInput& input, bool embedded) {
    const Geometry shape(input.width, input.height);
    if (input.tiles.size() != shape.area())
        throw std::invalid_argument("Map tile count does not match its dimensions");
    if (!embedded)
        return;
    for (const auto& layer : input.remembered)
        if (layer.size() != shape.area())
            throw std::invalid_argument("Map memory does not match its dimensions");
    const auto count = static_cast<std::size_t>(shape.coarse_width() * shape.coarse_height());
    for (const auto& grid : input.coarse)
        if (grid.size() != count)
            throw std::invalid_argument("Coarse map does not match its dimensions");
}
void install_map(const MapInput& input, MapStorage& map, bool embedded) {
    validate_map_input(input, embedded);
    Tile fallback;
    MapStorage staged(fallback);
    staged.stored_topology = input.topology;
    staged.pattern_seed = input.seed;
    staged.regions = map.regions;
    staged.remembered_barbarian = map.remembered_barbarian;
    staged.allocate(Geometry(input.width, input.height));
    std::copy(input.tiles.begin(), input.tiles.end(), staged.tiles().begin());
    if (embedded) {
        for (unsigned i = 0; i < 7; ++i)
            std::copy(input.remembered[i].begin(), input.remembered[i].end(),
                      staged.remembered_layer(i + 1).begin());
        for (unsigned i = 0; i < 2; ++i)
            std::copy(input.coarse[i].begin(), input.coarse[i].end(),
                      staged.coarse_grid(i).begin());
        staged.regions = input.regions;
    }
    map.swap_storage(staged);
}
} // namespace openciv::world
