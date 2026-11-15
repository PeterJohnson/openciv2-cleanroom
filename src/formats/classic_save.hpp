#pragma once

#include "formats/classic_io.hpp"
#include "formats/save_records.hpp"

namespace openciv::formats {
struct StoredMap {
    MapHeader header;
    std::array<Bytes, 7> remembered;
    std::vector<world::Tile> tiles;
    std::array<Bytes, 2> coarse;
    std::array<world::RegionSummary, 64> regions;
    bool operator==(const StoredMap&) const = default;
};
struct TribeUsage {
    Byte times_used = 0, city_name_index = 0, female = 0;
    bool operator==(const TribeUsage&) const = default;
};
struct EventStrings {
    // F06 §8 order: unit/city/who/whom, text prefix, then the eleven other
    // fields. Bytes exclude the terminator; nullopt and empty are distinct.
    std::array<std::optional<Bytes>, 4> names;
    std::array<std::optional<Bytes>, 20> text;
    std::array<std::optional<Bytes>, 11> other;
    bool operator==(const EventStrings&) const = default;
};
struct StoredEvent {
    EventRecord record;
    EventStrings strings;
    bool operator==(const StoredEvent&) const = default;
};
struct StoredEvents {
    std::vector<StoredEvent> entries;
    bool operator==(const StoredEvents&) const = default;
};
// F06 §§2–4,8 stored-file view, supported classic input versions; current output is 0x2c. It owns
// its values; no rules, process state, tokens, live map, RNG or presentation are accessed. The full
// F06 §6 session-load operation is a separate integration boundary.
struct StoredSave {
    GameHeader header;
    StoredGlobals globals;
    std::array<StoredCivilizationNames, 8> names;
    // Keep the large fixed population off the small WASM stack. Copies own
    // independent records; encoding enforces exactly eight civilization slots.
    std::vector<StoredCivilization> civilizations = std::vector<StoredCivilization>(8);
    StoredMap map;
    std::vector<StoredUnit> units;
    std::vector<StoredCity> cities;
    std::array<TribeUsage, 21> tribes;
    std::int16_t cursor_x = 0, cursor_y = 0;
    // File order, including repeated tribes. No last-tribe-wins load transform.
    std::vector<StoredLeader> leaders;
    std::array<std::array<Byte, 8>, 150> power_history{};
    SavedLayout layout;
    std::array<SavedMapView, 8> views;
    std::optional<ScenarioParameters> scenario;
    EliminationHistory eliminations;
    std::optional<MultiplayerRecord> multiplayer;
    std::optional<StoredEvents> events;
    Bytes trailing;
    bool operator==(const StoredSave&) const = default;
};
// Supported backing: W01 geometry, 0..2048 units, 0..256 city slots, nonnegative
// event counts, nominal contiguous text markers and strings of at most 255 bytes.
// Other scalar/retained values are preserved without claiming semantic validity.
// Rejection never mutates input or live state. DecodeError offsets are file-wide.
StoredSave decode_stored_save(std::span<const Byte> bytes);
// Lossless stored-view re-encoding preserves the admitted header separator and
// presence words. This is not the F06 §5 live save/scenario-export operation.
// Inconsistent container sizes/flags/presence reject before any bytes escape.
Bytes encode_stored_save(const StoredSave& save);
} // namespace openciv::formats
