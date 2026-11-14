#pragma once
#include "classic_io.hpp"
#include "save_records.hpp"
#include "state/rule_state.hpp"

namespace openciv::formats {
// F06 sections 2/6: selected saved records supply sign markers, not replacement
// rule definitions. A first short read establishes only its transferred prefix.
struct LeaderScratchRecord {
    std::array<Byte, leader_record_size> bytes{};
    std::size_t backed = 0;
    StoredLeader value() const;
    void assign(const StoredLeader& leader);
};
// F06 live export: canonical signed markers, independent of runtime text IDs.
// Imported byte-for-byte records use encode_leader directly instead.
StoredLeader stored_leader(const state::Leader& leader);

struct LeaderLoadScratch {
    std::array<LeaderScratchRecord, 21> by_tribe;
};
struct SavedCursor {
    std::int16_t x = 0, y = 0;
};
// F06 section 6 stage 8 / section 5 block order. Globals/civilizations/rules are
// already installed by preceding stages. Each triplet byte and cursor word is
// a separate item. Failed reads retain transferred bytes and prior effects.
// Multiplayer player_civ substitution is restored, including failure/stops.
bool read_leader_items(const ReadItem& read, state::GlobalValues& globals,
                       std::span<const state::Civilization> civilizations,
                       std::span<state::Leader> leaders, SavedCursor& cursor,
                       LeaderLoadScratch& scratch);
bool write_leader_items(const WriteItem& write, state::GlobalValues& globals,
                        std::span<const state::Civilization> civilizations,
                        std::span<const state::Leader> leaders, const SavedCursor& cursor);

// F06 section 6 stages 14-16. The parent supplies its selected scenario branch.
// Normalizes every rule leader before applying saved signs or scenario names.
// Does not replace definitions, allocate strings or perform later load stages.
void restore_leader_names(bool scenario, state::GlobalValues& globals,
                          std::span<state::Civilization> civilizations,
                          std::span<const state::CivilizationNames> names, state::RuleState& rules,
                          const LeaderLoadScratch& scratch);
// F06 section 5 stage 5: copy noncustom rule names for scenario export or an
// active scenario, adjective/plural/leader in order. Preserve bytes after NUL.
// A field that cannot fit stops before its write; prior fields remain updated.
void prepare_scenario_names(bool scenario_export, const state::GlobalValues& globals,
                            std::span<const state::Civilization> civilizations,
                            std::span<state::CivilizationNames> names,
                            const state::RuleState& rules);
} // namespace openciv::formats
