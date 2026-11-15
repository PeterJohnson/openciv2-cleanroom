#pragma once
#include "formats/stored_records.hpp"
#include <map>
#include <memory>
namespace openciv::state {
struct WorldLifetime;
}
namespace openciv::formats {
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
