#pragma once

#include "state/civilization.hpp"
#include "state/multiplayer.hpp"
#include "state/rule_state.hpp"
#include "world/map.hpp"
#include <memory>

namespace openciv::state {
// F02 §2 ownership of the implemented process fields. Further preferences join
// this context as their owning operations arrive. Defaults
// are construction values, not a substitute for classic setup/load procedures.
struct ProcessContext {
    ProcessTokens tokens;
    // Keep the large rule tables off native/WASM caller stacks. Each process
    // has distinct storage, retained across client-session lifetime changes.
    std::unique_ptr<RuleState> rules = std::make_unique<RuleState>();
    MultiplayerPreferences multiplayer_preferences;
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
    MultiplayerState multiplayer;
    CityMoodWorkspace mood;
    world::MapStorage map;
};
} // namespace openciv::state
