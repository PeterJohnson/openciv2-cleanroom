export module openciv.formats.event_io;
import openciv.formats.state_conversion;

import std;
export import openciv.base.callback_ref;
export import openciv.formats.classic_io;
export import openciv.formats.save_records;

export namespace openciv::formats {
// Opaque imported bytes belong to the classic adapter, keyed to actual live
// event lifetime. Expired/replaced events never transfer metadata to reused IDs.
struct ImportedEventBytes {
    std::weak_ptr<const state::Event> event;
    std::array<Byte, 40> retained{};
};
using EventCompatibility = std::map<state::EventId, ImportedEventBytes>;
using WriteEventText = base::CallbackRef<bool(std::span<const Byte>)>;
using WriteEventByte = base::CallbackRef<bool(Byte)>;
// Parent owns the events-loaded/scenario-export gate. Writes tag, count, all
// fixed records, then each present text and its NUL as separate service calls.
// No string truncation. A text hole ends only that event's text-line traversal.
bool write_event_items(const WriteItem& write, const WriteEventText& text,
                       const WriteEventByte& byte, const state::EventCollection& events,
                       const EventCompatibility& compatibility = {});
} // namespace openciv::formats

module :private;
namespace openciv::formats {
namespace {
constexpr std::array<Byte, 4> tag{'E', 'V', 'N', 'T'};
EventRecord wire_record(const state::Event& event) {
    EventRecord record;
    static_cast<StoredEventValues&>(record) = to_stored(event.values);
    const auto markers = event_markers(record);
    for (std::size_t i = 0; i < markers.size(); ++i)
        *markers[i] = event.strings[i].present() ? 1 : 0;
    return record;
}
} // namespace
bool write_event_items(const WriteItem& write, const WriteEventText& text,
                       const WriteEventByte& byte, const state::EventCollection& events,
                       const EventCompatibility& compatibility) {
    if (!write(tag))
        return false;
    if (events.size() > static_cast<std::size_t>(std::numeric_limits<std::int32_t>::max()))
        throw std::out_of_range("Event count exceeds the supported signed count backing");
    ByteWriter count;
    count.i32(static_cast<std::int32_t>(events.size()));
    if (!write(count.bytes))
        return false;
    for (std::size_t i = 0; i < events.size(); ++i) {
        const auto event = events.at(i);
        auto record = wire_record(*event);
        const auto imported = compatibility.find(event->identity());
        if (imported != compatibility.end() && imported->second.event.lock() == event)
            record.retained = imported->second.retained;
        if (!write(encode_event_record(record)))
            return false;
    }
    for (std::size_t i = 0; i < events.size(); ++i) {
        const auto event = events.at(i);
        for (std::size_t field = 0; field < state::event_string_count; ++field) {
            if (field >= state::event_text_begin && field < state::event_other_begin &&
                !event->strings[field].present()) {
                field = state::event_other_begin - 1;
                continue;
            }
            if (!event->strings[field].present())
                continue;
            const auto& source = event->strings[field].value();
            const auto end = std::find(source.begin(), source.end(), '\0');
            const Bytes bytes(source.begin(), end);
            if (!text(bytes) || !byte(0))
                return false;
        }
    }
    return true;
}
} // namespace openciv::formats
