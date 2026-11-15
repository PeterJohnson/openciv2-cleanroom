#pragma once
#include "formats/save_records.hpp"
#include "state/preferences.hpp"

namespace openciv::formats {
// Explicit adapters: stored bytes and live domain values are separate contracts.
state::Unit to_live(const StoredUnit& value);
StoredUnit to_stored(const state::Unit& value);
state::City to_live(const StoredCity& value);
StoredCity to_stored(const state::City& value);
state::GlobalValues to_live(const StoredGlobals& value);
StoredGlobals to_stored(const state::GlobalValues& value);
state::Civilization to_live(const StoredCivilization& value);
StoredCivilization to_stored(const state::Civilization& value);
state::CivilizationNames to_live(const StoredCivilizationNames& value);
StoredCivilizationNames to_stored(const state::CivilizationNames& value);
state::ScenarioParameters to_live(const ScenarioParameters& value);
ScenarioParameters to_stored(const state::ScenarioParameters& value);
state::EliminationHistory to_live(const EliminationHistory& value);
EliminationHistory to_stored(const state::EliminationHistory& value);
state::MultiplayerValues to_live(const MultiplayerRecord& value);
MultiplayerRecord to_stored(const state::MultiplayerValues& value);
state::MapView to_live(const SavedMapView& value);
SavedMapView to_stored(const state::MapView& value);
state::OrdinaryPreferences to_live(const StoredOrdinaryPreferences& value);
StoredOrdinaryPreferences to_stored(const state::OrdinaryPreferences& value);
state::MultiplayerPreferences to_live(const StoredMultiplayerPreferences& value);
StoredMultiplayerPreferences to_stored(const state::MultiplayerPreferences& value);
state::EventValues to_live(const StoredEventValues& value);
StoredEventValues to_stored(const state::EventValues& value);
} // namespace openciv::formats
