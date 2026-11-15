export module openciv.state.game;

import std;
export import openciv.state.entity_allocation;
export import openciv.state.world_lifetime;
export import openciv.state.civilization;
export import openciv.state.events;
export import openciv.state.history;
export import openciv.state.layout;
export import openciv.state.multiplayer;
export import openciv.state.rule_state;
export import openciv.state.scenario;
export import openciv.state.text_parameters;
export import openciv.state.world_setup;
export import openciv.world.map;

export namespace openciv::state {
/// F02 §2 ownership of the implemented process fields. Further preferences join
/// this context as their owning operations arrive. Defaults
/// are construction values, not a substitute for classic setup/load procedures.
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
/// Logical world and retained workspace ownership. File records are decoded
/// separately; constructing this object performs no F05/F06 transformations.
/// The supplying process context must outlive the world.
struct GameState {
    /// Create a world whose map fallback is borrowed from the longer-lived process.
    explicit GameState(ProcessContext& process) : map(process.fallback) {}
    const std::shared_ptr<const WorldLifetime> lifetime = std::make_shared<WorldLifetime>();
    EntityPools entities;
    UnitStackState unit_stacks;
    UnitDeletionState unit_deletion;
    // U01 / V01 retained census, distinct from military and per-type counts.
    std::array<std::int32_t, 8> unit_totals{};
    // Classic F04/N03 loop activity is distinct from SessionRuntime clients.
    bool turn_loop_active = false;
    // F05 section 5 / U04: logical View=0, Move units=1; retain other values
    // until an owning operation writes them. Not a serialized game field.
    MovementMode movement_mode = MovementMode::view;
    // F05 clears the eight presentation color-record flag sets independently
    // of civilization flags. Native palette records are not game-state inputs.
    std::array<std::uint32_t, 8> civilization_color_flags{};
    GlobalValues globals;
    WorldSetup world_setup;
    std::array<Civilization, 8> civilizations{};
    std::array<CivilizationNames, 8> names{};
    // F05 section 4 / V01 section 4: retained until chosen or consumed. Wizard
    // retries and Random choices do not clear an earlier pending override.
    std::array<std::int8_t, 8> pending_leader_overrides{-1, -1, -1, -1, -1, -1, -1, -1};
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
