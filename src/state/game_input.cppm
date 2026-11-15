export module openciv.state.game_input;

import std;
export import openciv.state.game;
export import openciv.state.label_catalog;
export import openciv.state.rule_input;
export import openciv.world.map_input;

export namespace openciv::state {
/// Owned rule and label inputs prepared for a game installation.
struct LoadedContent {
    std::unique_ptr<RuleInput> rules;
    std::optional<LabelCatalog> labels;
};
/// Saved tribe-use inputs used to restore leader cursors and choices.
struct TribeInput {
    std::uint8_t times_used = 0, city_name_index = 0;
    bool female = false;
};
/// Saved leader customization markers consumed during live-name restoration.
struct LeaderCustomization {
    bool plural = false, adjective = false;
    std::array<std::array<bool, 2>, 7> titles{};
};
/// Format-independent, owned restoration operands. No I/O, live IDs or random draws.
struct GameInput {
    GlobalValues globals;
    std::array<CivilizationNames, 8> names;
    std::vector<Civilization> civilizations;
    world::MapInput map;
    std::vector<Unit> units;
    std::vector<City> cities;
    std::array<TribeInput, 21> tribes;
    std::array<std::optional<LeaderCustomization>, 21> leaders;
    std::int16_t cursor_x = 0, cursor_y = 0;
    PowerHistory power_history{};
    Layout layout;
    std::array<MapView, 8> views;
    std::optional<ScenarioParameters> scenario;
    EliminationHistory eliminations;
    std::optional<MultiplayerValues> multiplayer;
    std::optional<EventCatalog> events;
};
} // namespace openciv::state
