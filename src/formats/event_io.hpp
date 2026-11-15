#pragma once

#include "formats/classic_io.hpp"
#include "formats/save_records.hpp"
#include <optional>
#include <map>

namespace openciv::formats {
// Opaque imported bytes belong to the classic adapter, keyed to actual live
// event lifetime. Expired/replaced events never transfer metadata to reused IDs.
struct ImportedEventBytes {
    std::weak_ptr<const state::Event> event;
    std::array<Byte, 40> retained{};
};
using EventCompatibility = std::map<state::EventId, ImportedEventBytes>;
using WriteEventText = std::function<bool(std::span<const Byte>)>;
using WriteEventByte = std::function<bool(Byte)>;
// Parent owns the events-loaded/scenario-export gate. Writes tag, count, all
// fixed records, then each present text and its NUL as separate service calls.
// No string truncation. A text hole ends only that event's text-line traversal.
bool write_event_items(const WriteItem& write, const WriteEventText& text,
                       const WriteEventByte& byte, const state::EventCollection& events,
                       const EventCompatibility& compatibility = {});
} // namespace openciv::formats
