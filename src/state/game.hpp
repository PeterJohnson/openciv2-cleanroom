#pragma once

#include "state/civilization.hpp"
#include "state/events.hpp"
#include "state/history.hpp"
#include "state/layout.hpp"
#include "state/multiplayer.hpp"
#include "state/rule_state.hpp"
#include "state/scenario.hpp"
#include "state/text_parameters.hpp"
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
    OrdinaryPreferences ordinary_preferences;
    TextParameters text_parameters;
    world::Tile fallback;
};
// Logical world and retained workspace ownership. File records are decoded
// separately; constructing this object performs no F05/F06 transformations.
// The supplying process context must outlive the world.
struct GameState {
    explicit GameState(ProcessContext& process) : map(process.fallback) {}
    EntityPools entities;
    UnitStackState unit_stacks;
    UnitDeletionState unit_deletion;
    // U01 / V01 retained census, distinct from military and per-type counts.
    std::array<std::int32_t, 8> unit_totals{};
    // Classic F04/N03 loop activity is distinct from SessionRuntime clients.
    bool turn_loop_active = false;
    GlobalValues globals;
    std::array<Civilization, 8> civilizations{};
    std::array<CivilizationNames, 8> names{};
    MultiplayerState multiplayer;
    EventState events;
    PowerHistory power_history{};
    EliminationHistory eliminations;
    ScenarioParameters scenario;
    Layout layout;
    std::array<MapView, 8> views;
    CityMoodWorkspace mood;
    world::MapStorage map;
};
} // namespace openciv::state
