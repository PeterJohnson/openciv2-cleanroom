#include "formats/event_io.hpp"
#include <algorithm>
#include <bit>
#include <limits>

namespace openciv::formats {
namespace {
constexpr std::array<Byte, 4> tag{'E', 'V', 'N', 'T'};
constexpr std::array<std::size_t, state::event_string_count> offsets{
    0x008, 0x010, 0x014, 0x020, 0x038, 0x03c, 0x040, 0x044, 0x048, 0x04c, 0x050, 0x054,
    0x058, 0x05c, 0x060, 0x064, 0x068, 0x06c, 0x070, 0x074, 0x078, 0x07c, 0x080, 0x084,
    0x088, 0x090, 0x0c4, 0x0cc, 0x0d4, 0x0dc, 0x13c, 0x140, 0x148, 0x174, 0x184};
ReadResult item(const ReadItem& read, std::size_t size) {
    auto result = read(size);
    if (result.prefix.size() > size || (result.complete && result.prefix.size() != size))
        throw std::invalid_argument("Item service returned an inconsistent transferred prefix");
    return result;
}
void check_member(const state::EventCollection& events,
                  const std::shared_ptr<state::Event>& event) {
    if (!events.contains(event))
        throw std::out_of_range("Event I/O callback invalidated the selected event");
}
bool read_string(const ReadEventByte& read, state::EventString& result,
                 const state::EventServices& services) {
    std::string text;
    for (;;) {
        const auto byte = read();
        if (!byte)
            return false;
        if (text.size() == 255 || *byte == 0)
            break;
        text.push_back(std::bit_cast<char>(*byte));
    }
    return state::create_event_string(result, text, services);
}
EventRecord wire_record(const state::Event& event) {
    auto record = event.values;
    const auto markers = state::event_markers(record);
    for (std::size_t field = 0; field < markers.size(); ++field) {
        if (!event.strings[field].present())
            *markers[field] = 0;
        else if (*markers[field] == 0)
            *markers[field] = 1;
    }
    return record;
}
} // namespace
bool read_event_items(const ReadItem& read, const ReadEventByte& byte, state::EventState& events,
                      const state::EventServices& services) {
    events.loaded = false;
    const auto header = item(read, tag.size());
    if (!header.complete || !std::equal(header.prefix.begin(), header.prefix.end(), tag.begin()))
        return true;
    const auto count_bytes = item(read, 4);
    if (!count_bytes.complete)
        return false;
    const auto count = ByteReader{count_bytes.prefix}.i32();
    auto& collection = events.collection;
    collection.reset();
    for (std::int32_t i = 0; i < count; ++i) {
        const auto event = collection.append(services);
        if (!event)
            return false;
        const auto incoming = item(read, event_record_size);
        check_member(collection, event);
        auto bytes = encode_event_record(event->values);
        std::copy(incoming.prefix.begin(), incoming.prefix.end(), bytes.begin());
        event->values = decode_event_record(bytes);
        const auto markers = state::event_markers(event->values);
        for (std::size_t field = 0; field < offsets.size(); ++field)
            if (incoming.prefix.size() > offsets[field])
                event->strings[field] =
                    *markers[field] == 0 ? state::EventString{} : state::EventString::unresolved();
        if (!incoming.complete)
            return false;
    }
    for (std::size_t i = 0; i < collection.size(); ++i) {
        const auto event = collection.at(i);
        for (std::size_t field = 0; field < state::event_string_count; ++field) {
            if (field >= state::event_text_begin && field < state::event_other_begin &&
                !event->strings[field].present()) {
                field = state::event_other_begin - 1;
                continue;
            }
            if (!event->strings[field].present())
                continue;
            state::EventString text;
            if (!read_string(byte, text, services))
                return false;
            check_member(collection, event);
            event->set_string(field, std::move(text));
        }
    }
    if (!state::compact_events(collection, services))
        return false;
    events.loaded = true;
    return true;
}
bool write_event_items(const WriteItem& write, const WriteEventText& text,
                       const WriteEventByte& byte, const state::EventCollection& events) {
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
        if (!write(encode_event_record(wire_record(*event))))
            return false;
        check_member(events, event);
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
            check_member(events, event);
        }
    }
    return true;
}
} // namespace openciv::formats
