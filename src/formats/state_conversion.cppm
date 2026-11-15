export module openciv.formats.state_conversion;

import std;
export import openciv.formats.save_records;
export import openciv.state.preferences;

export namespace openciv::formats {
/// Translate stored fields into semantic live values without installing game state.
/// Explicit adapters: stored bytes and live domain values are separate contracts.
state::Unit to_live(const StoredUnit& value);
/// Translate semantic live values into the corresponding classic storage representation.
StoredUnit to_stored(const state::Unit& value);
/// Translate stored fields into semantic live values without installing game state.
state::City to_live(const StoredCity& value);
/// Translate semantic live values into the corresponding classic storage representation.
StoredCity to_stored(const state::City& value);
/// Translate stored fields into semantic live values without installing game state.
state::GlobalValues to_live(const StoredGlobals& value);
/// Translate semantic live values into the corresponding classic storage representation.
StoredGlobals to_stored(const state::GlobalValues& value);
/// Translate stored fields into semantic live values without installing game state.
state::Civilization to_live(const StoredCivilization& value);
/// Translate semantic live values into the corresponding classic storage representation.
StoredCivilization to_stored(const state::Civilization& value);
/// Translate stored fields into semantic live values without installing game state.
state::CivilizationNames to_live(const StoredCivilizationNames& value);
/// Translate semantic live values into the corresponding classic storage representation.
StoredCivilizationNames to_stored(const state::CivilizationNames& value);
/// Translate stored fields into semantic live values without installing game state.
state::ScenarioParameters to_live(const ScenarioParameters& value);
/// Translate semantic live values into the corresponding classic storage representation.
ScenarioParameters to_stored(const state::ScenarioParameters& value);
/// Translate stored fields into semantic live values without installing game state.
state::EliminationHistory to_live(const EliminationHistory& value);
/// Translate semantic live values into the corresponding classic storage representation.
EliminationHistory to_stored(const state::EliminationHistory& value);
/// Translate stored fields into semantic live values without installing game state.
state::MultiplayerValues to_live(const MultiplayerRecord& value);
/// Translate semantic live values into the corresponding classic storage representation.
MultiplayerRecord to_stored(const state::MultiplayerValues& value);
/// Translate stored fields into semantic live values without installing game state.
state::MapView to_live(const SavedMapView& value);
/// Translate semantic live values into the corresponding classic storage representation.
SavedMapView to_stored(const state::MapView& value);
/// Translate stored fields into semantic live values without installing game state.
state::OrdinaryPreferences to_live(const StoredOrdinaryPreferences& value);
/// Translate semantic live values into the corresponding classic storage representation.
StoredOrdinaryPreferences to_stored(const state::OrdinaryPreferences& value);
/// Translate stored fields into semantic live values without installing game state.
state::MultiplayerPreferences to_live(const StoredMultiplayerPreferences& value);
/// Translate semantic live values into the corresponding classic storage representation.
StoredMultiplayerPreferences to_stored(const state::MultiplayerPreferences& value);
/// Translate stored fields into semantic live values without installing game state.
state::EventValues to_live(const StoredEventValues& value);
/// Translate semantic live values into the corresponding classic storage representation.
StoredEventValues to_stored(const state::EventValues& value);
} // namespace openciv::formats
