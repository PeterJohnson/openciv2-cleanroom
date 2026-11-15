export module openciv.formats.record_compatibility;

import std;
export import openciv.state.world_lifetime;
export import openciv.formats.stored_records;

export namespace openciv::formats {
// Imported opaque bytes never enter live domain state. Entity entries are keyed
// by renewed tokens; replacing a world expires the complete metadata snapshot.
struct RecordCompatibility {
    std::weak_ptr<const state::WorldLifetime> lifetime;
    StoredGlobals globals;
    std::array<StoredCivilization, 8> civilizations;
    std::optional<MultiplayerRecord> multiplayer;
    std::map<state::UnitToken, StoredUnit> units;
    std::map<state::CityToken, StoredCity> cities;
};
void preserve_opaque(StoredUnit& output, const StoredUnit& imported);
void preserve_opaque(StoredCity& output, const StoredCity& imported);
void preserve_opaque(StoredGlobals& output, const StoredGlobals& imported);
void preserve_opaque(StoredCivilization& output, const StoredCivilization& imported);
void preserve_opaque(MultiplayerRecord& output, const MultiplayerRecord& imported);
} // namespace openciv::formats

module :private;
namespace openciv::formats {
void preserve_opaque(StoredUnit& output, const StoredUnit& imported) {
    output.retained_byte = imported.retained_byte;
    output.retained_word = imported.retained_word;
}
void preserve_opaque(StoredCity& output, const StoredCity& imported) {
    output.retained_byte = imported.retained_byte;
}
void preserve_opaque(StoredGlobals& output, const StoredGlobals& imported) {
    output.retained_0e = imported.retained_0e;
    output.retained_34 = imported.retained_34;
}
void preserve_opaque(StoredCivilization& output, const StoredCivilization& imported) {
    output.retained_1b = imported.retained_1b;
    output.retained_65 = imported.retained_65;
    output.retained_3fd = imported.retained_3fd;
    output.retained_401 = imported.retained_401;
}
void preserve_opaque(MultiplayerRecord& output, const MultiplayerRecord& imported) {
    output.retained_142 = imported.retained_142;
    output.retained_465 = imported.retained_465;
    output.retained_472 = imported.retained_472;
    output.player_setup = imported.player_setup;
}
} // namespace openciv::formats
