#pragma once

#include "formats/classic_io.hpp"
#include "formats/save_records.hpp"
#include <optional>

namespace openciv::formats {
using ReadEventByte = std::function<std::optional<Byte>()>;
using WriteEventText = std::function<bool(std::span<const Byte>)>;
using WriteEventByte = std::function<bool(Byte)>;
// F06 section 8: optional tag absence succeeds and retains the old collection;
// recognized input replaces it, reads all fixed records, then strings, then
// runs S02 compaction. loaded is true only after complete success. A false
// result preserves partial effects; unresolved saved references remain explicit.
bool read_event_items(const ReadItem& read, const ReadEventByte& byte, state::EventState& events,
                      const state::EventServices& services = {});
// Parent owns the events-loaded/scenario-export gate. Writes tag, count, all
// fixed records, then each present text and its NUL as separate service calls.
// No string truncation. A text hole ends only that event's text-line traversal.
bool write_event_items(const WriteItem& write, const WriteEventText& text,
                       const WriteEventByte& byte, const state::EventCollection& events);
} // namespace openciv::formats
