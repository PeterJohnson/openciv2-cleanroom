#pragma once

#include "formats/classic_io.hpp"
#include "formats/save_records.hpp"

namespace openciv::formats {
using FlushLoadWork = std::function<void()>;
// F06 section 6 stage 7, after embedded map input: two coarse-grid items,
// flush, then one region-table item. Unknown/untransferred coarse cells remain
// unknown. Replacement of selected map backing by an I/O callback stops safely.
bool read_map_summary_items(const ReadItem& read, world::MapStorage& map,
                            const FlushLoadWork& flush = {});
bool write_map_summary_items(const WriteItem& write, const world::MapStorage& map);

// F06 sections 3/5/6. A separate packed buffer retains a failed layout transfer;
// only a complete layout item unpacks into live coordinates. Neither operation
// creates windows. Parent owns the later screen/scenario validity decision.
void pack_layout(const state::Layout& layout, SavedLayout& packed);
void unpack_layout(const SavedLayout& packed, state::Layout& layout);
bool read_history_layout_items(const ReadItem& read, state::PowerHistory& history,
                               SavedLayout& packed, state::Layout& layout,
                               std::span<state::MapView, 8> views, const FlushLoadWork& flush = {});
// After power output succeeds, copy the live current screen into saved screen,
// pack and write layout. These mutations persist if layout/view output fails.
bool write_history_layout_items(const WriteItem& write, const state::PowerHistory& history,
                                const state::Rectangle& current_screen, SavedLayout& packed,
                                state::Layout& layout, std::span<const state::MapView, 8> views);
// F06 section 6 stage 19, after rule reload. Compare dimensions, not origins;
// overwrite the file's valid marker, using the captured scenario result.
void finish_loaded_layout(state::Layout& layout, const state::Rectangle& current_screen,
                          bool captured_scenario);

// F06 section 6 stage 10 / section 5 stream order. The read gate combines the
// captured scenario result with the live Scenario flag; ordinary input resets
// only S01's specified parameter fields. Elimination input is always required.
bool read_scenario_history_items(const ReadItem& read, bool captured_scenario,
                                 const state::GlobalValues& globals,
                                 state::ScenarioParameters& scenario,
                                 state::EliminationHistory& eliminations);
bool write_scenario_history_items(const WriteItem& write, bool scenario_export,
                                  const state::GlobalValues& globals,
                                  const state::ScenarioParameters& scenario,
                                  const state::EliminationHistory& eliminations);
} // namespace openciv::formats
