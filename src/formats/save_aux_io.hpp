#pragma once
#include "formats/classic_io.hpp"
#include "formats/save_records.hpp"

namespace openciv::formats {
bool write_map_summary_items(const WriteItem& write, const world::MapStorage& map);
void pack_layout(const state::Layout& layout, SavedLayout& packed);
void unpack_layout(const SavedLayout& packed, state::Layout& layout);
bool write_history_layout_items(const WriteItem& write, const state::PowerHistory& history,
                                const state::Rectangle& current_screen, SavedLayout& packed,
                                const state::Layout& layout,
                                std::span<const state::MapView, 8> views);
bool write_scenario_history_items(const WriteItem& write, bool scenario_export,
                                  const state::GlobalValues& globals,
                                  const state::ScenarioParameters& scenario,
                                  const state::EliminationHistory& eliminations);
} // namespace openciv::formats
