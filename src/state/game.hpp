#pragma once

#include "state/civilization.hpp"
#include "world/map.hpp"

namespace openciv::state {
// F02 §2 ownership of the implemented process fields. Rule tables/preferences
// will join this context as their owning F03 implementations arrive. Defaults
// are construction values, not a substitute for classic setup/load procedures.
struct ProcessContext {
    ProcessTokens tokens;
    std::array<Leader, 21> leaders{};
    world::Tile fallback;
};
// Logical world and retained workspace ownership. File records are decoded
// separately; constructing this object performs no F05/F06 transformations.
// The supplying process context must outlive the world.
struct GameState {
    explicit GameState(ProcessContext& process) : map(process.fallback) {}
    EntityPools entities;
    GlobalValues globals;
    std::array<Civilization, 8> civilizations{};
    std::array<CivilizationNames, 8> names{};
    CityMoodWorkspace mood;
    world::MapStorage map;
};
} // namespace openciv::state
