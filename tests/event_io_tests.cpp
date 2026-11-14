// Independently authored F02 event ownership, F06 section 8 and S02 section 2 cases.
#include "formats/event_io.hpp"
#include "session/runtime.hpp"
#include <catch2/catch_test_macros.hpp>
#include <algorithm>
#include <limits>

using namespace openciv;
using namespace openciv::formats;
namespace {
struct Input {
    Bytes bytes;
    explicit Input(Bytes supplied = {}) : bytes(std::move(supplied)) {}
    std::size_t position = 0;
    std::vector<std::size_t> items;
    std::size_t chars = 0;
    ReadResult item(std::size_t size) {
        items.push_back(size);
        const auto length = std::min(size, bytes.size() - position);
        const auto start = bytes.begin() + static_cast<std::ptrdiff_t>(position);
        position += length;
        return {{start, start + static_cast<std::ptrdiff_t>(length)}, length == size};
    }
    std::optional<Byte> byte() {
        ++chars;
        if (position == bytes.size())
            return {};
        return bytes[position++];
    }
    bool read(state::EventState& state, const state::EventServices& services = {}) {
        return read_event_items([&](std::size_t size) { return item(size); },
                                [&] { return byte(); }, state, services);
    }
};
Bytes header(std::int32_t count) {
    ByteWriter bytes;
    bytes.append(Bytes{'E', 'V', 'N', 'T'});
    bytes.i32(count);
    return std::move(bytes.bytes);
}
Input wire(std::span<const EventRecord> records, std::span<const Byte> strings = {}) {
    Input result;
    ByteWriter bytes;
    bytes.append(header(static_cast<std::int32_t>(records.size())));
    for (const auto& record : records)
        bytes.append(encode_event_record(record));
    bytes.append(strings);
    result.bytes = std::move(bytes.bytes);
    return result;
}
std::shared_ptr<state::Event> add(state::EventCollection& list, std::string_view text = "Old") {
    auto event = list.append();
    REQUIRE(event);
    event->values.actions = 0x80000021; // Includes retained state, not recomputed by copying.
    event->values.who_civ = -2;
    event->values.money_amount = std::numeric_limits<std::int32_t>::min();
    event->set_string(0, state::EventString(text));
    return event;
}
} // namespace

TEST_CASE("F06 absent or failed event tags retain old events while clearing the loaded flag",
          "[formats][event-io]") {
    for (const auto& bytes :
         {Bytes{}, Bytes{'E'}, Bytes{'E', 'V'}, Bytes{'E', 'V', 'N'}, Bytes{'E', 'V', 'N', 'X'}}) {
        state::EventState events;
        const auto old = add(events.collection);
        events.loaded = true;
        Input input{bytes};
        REQUIRE(input.read(events));
        REQUIRE_FALSE(events.loaded);
        REQUIRE(events.collection.size() == 1);
        REQUIRE(events.collection.at(0) == old);
        REQUIRE(input.position == bytes.size());
        REQUIRE(input.chars == 0);
    }
    state::EventState events;
    const auto old = add(events.collection);
    REQUIRE(read_event_items([](std::size_t) { return ReadResult{{'E', 'V', 'N', 'T'}, false}; },
                             {}, events));
    REQUIRE(events.collection.at(0) == old);
    for (std::size_t length = 4; length < 8; ++length) {
        Input input{header(1)};
        input.bytes.resize(length);
        events.loaded = true;
        REQUIRE_FALSE(input.read(events));
        REQUIRE_FALSE(events.loaded);
        REQUIRE(events.collection.at(0) == old);
    }
}
TEST_CASE("F06 zero and negative event counts replace the old collection with loaded emptiness",
          "[formats][event-io]") {
    for (const auto count : {0, -1, std::numeric_limits<std::int32_t>::min()}) {
        state::EventState events;
        add(events.collection);
        Input input{header(count)};
        REQUIRE(input.read(events));
        REQUIRE(events.loaded);
        REQUIRE(events.collection.size() == 0);
        REQUIRE(input.position == 8);
        REQUIRE(input.chars == 0);
    }
}
TEST_CASE("F06 event records precede all strings and resolve every field in specified order",
          "[formats][event-io]") {
    std::array<EventRecord, 2> records;
    records[0].actions = 0x80000101;
    records[0].who_civ = -2;
    records[1].money_amount = std::numeric_limits<std::int32_t>::min();
    ByteWriter strings;
    for (std::size_t e = 0; e < records.size(); ++e) {
        const auto markers = state::event_markers(records[e]);
        for (std::size_t field = 0; field < markers.size(); ++field) {
            *markers[field] = static_cast<std::uint32_t>(0x80000000 + field);
            strings.u8(static_cast<Byte>('A' + e));
            strings.u8(static_cast<Byte>(field + 1));
            strings.u8(0);
        }
    }
    auto input = wire(records, strings.bytes);
    state::EventState events;
    const auto old = add(events.collection);
    std::size_t string_requests = 0;
    const state::EventServices services{
        [&](state::EventAllocation request) {
            REQUIRE_FALSE(events.loaded);
            if (request.kind == state::EventAllocationKind::string) {
                ++string_requests;
                REQUIRE(input.items == std::vector<std::size_t>{4, 4, 444, 444});
            }
            return true;
        },
        {}};
    REQUIRE(input.read(events, services));
    REQUIRE(events.loaded);
    REQUIRE(events.collection.size() == 2);
    REQUIRE_FALSE(events.collection.contains(old));
    REQUIRE(input.position == input.bytes.size());
    REQUIRE(string_requests == 2 * 35 * 3); // Initial restore, then two S02 copy passes.
    REQUIRE(events.collection.at(0)->values.actions == 0x80000101);
    REQUIRE(events.collection.at(0)->values.who_civ == -2);
    REQUIRE(events.collection.at(1)->values.money_amount ==
            std::numeric_limits<std::int32_t>::min());
    for (std::size_t e = 0; e < 2; ++e)
        for (std::size_t field = 0; field < 35; ++field) {
            const auto& text = events.collection.at(e)->strings[field];
            REQUIRE(text.backed());
            REQUIRE(text.value() ==
                    std::string{static_cast<char>('A' + e), static_cast<char>(field + 1)});
        }
}
TEST_CASE("F06 every partial fixed event record retains its bytes and unresolved references",
          "[formats][event-io]") {
    EventRecord record;
    record.actions = 0x80000021;
    record.unit_name = 0x44332211;
    record.wave_file = 0x12345678;
    record.give_tech_civ = -7;
    const auto bytes = encode_event_record(record);
    for (std::size_t length = 0; length < bytes.size(); ++length) {
        auto input = wire(std::span{&record, 1});
        input.bytes.resize(8 + length);
        state::EventState events;
        const auto old = add(events.collection);
        REQUIRE_FALSE(input.read(events));
        REQUIRE_FALSE(events.loaded);
        REQUIRE(events.collection.size() == 1);
        const auto current = events.collection.at(0);
        REQUIRE(current != old);
        Bytes expected(bytes.size(), 0);
        std::copy_n(bytes.begin(), length, expected.begin());
        REQUIRE(encode_event_record(current->values) == expected);
        REQUIRE(current->strings[0].present() == (length > 8));
        REQUIRE_FALSE(current->strings[0].backed());
        REQUIRE(input.chars == 0);
    }
}
TEST_CASE("F06 event strings distinguish empty absence EOF and the consumed overlong byte",
          "[formats][event-io]") {
    EventRecord record;
    record.unit_name = 1;
    record.city_name = 2;
    for (const std::size_t length : {0U, 1U, 254U, 255U, 256U}) {
        Bytes bytes(length, 'x');
        bytes.insert(bytes.end(), {0, 'q', 0});
        auto input = wire(std::span{&record, 1}, bytes);
        state::EventState events;
        REQUIRE(input.read(events));
        const auto event = events.collection.at(0);
        REQUIRE(event->strings[0].value() == std::string(std::min<std::size_t>(255, length), 'x'));
        REQUIRE(event->strings[1].value() == (length == 256 ? "" : "q"));
        REQUIRE_FALSE(event->strings[2].present());
        REQUIRE(input.position == (length == 256 ? input.bytes.size() - 2 : input.bytes.size()));
    }
    for (const std::size_t length : {0U, 1U, 255U}) {
        const Bytes no_terminator(length, 'x');
        auto input = wire(std::span{&record, 1}, no_terminator);
        state::EventState events;
        std::size_t strings = 0;
        const state::EventServices services{
            [&](state::EventAllocation request) {
                strings += request.kind == state::EventAllocationKind::string ? 1 : 0;
                return true;
            },
            {}};
        REQUIRE_FALSE(input.read(events, services));
        REQUIRE(strings == 0);
        REQUIRE(events.collection.at(0)->strings[0].present());
        REQUIRE_FALSE(events.collection.at(0)->strings[0].backed());
        REQUIRE(input.chars == length + 1);
    }
}
TEST_CASE("F06 text holes retain later unresolved markers through compaction without extra reads",
          "[formats][event-io]") {
    EventRecord record;
    record.text[0] = 1;
    record.text[2] = 99;
    record.wave_file = 2;
    const Bytes strings{'A', 0, 'B', 0};
    auto input = wire(std::span{&record, 1}, strings);
    state::EventState events;
    REQUIRE(input.read(events));
    REQUIRE(events.loaded);
    const auto event = events.collection.at(0);
    REQUIRE(event->strings[4].value() == "A");
    REQUIRE_FALSE(event->strings[5].present());
    REQUIRE(event->strings[6].present());
    REQUIRE_FALSE(event->strings[6].backed());
    REQUIRE_THROWS_AS(event->strings[6].value(), std::out_of_range);
    REQUIRE(event->values.text[2] == 99);
    REQUIRE(event->strings[34].value() == "B");
    REQUIRE(input.position == input.bytes.size());
}
TEST_CASE("F06 event resource failures retain prior nodes and strings and report string failure",
          "[formats][event-io]") {
    std::array<EventRecord, 2> records;
    records[0].unit_name = records[0].city_name = 1;
    records[1].turn = 17;
    const Bytes strings{'A', 0, 'B', 0};
    for (const bool fail_event : {false, true}) {
        auto input = wire(records, strings);
        state::EventState events;
        unsigned node_requests = 0, string_requests = 0;
        std::vector<std::pair<std::int32_t, std::int32_t>> reports;
        const state::EventServices services{[&](state::EventAllocation request) {
                                                if (request.kind ==
                                                    state::EventAllocationKind::event)
                                                    return !fail_event || ++node_requests < 2;
                                                return ++string_requests < 2;
                                            },
                                            [&](std::int32_t code, std::int32_t category) {
                                                reports.emplace_back(code, category);
                                            }};
        REQUIRE_FALSE(input.read(events, services));
        REQUIRE_FALSE(events.loaded);
        REQUIRE(events.collection.size() == (fail_event ? 1 : 2));
        if (fail_event) {
            REQUIRE(input.chars == 0);
            REQUIRE(reports.empty());
        } else {
            REQUIRE(events.collection.at(0)->strings[0].value() == "A");
            REQUIRE_FALSE(events.collection.at(0)->strings[1].backed());
            REQUIRE(reports == std::vector<std::pair<std::int32_t, std::int32_t>>{{-9, 3}});
        }
    }
}
TEST_CASE("S02 copy separates duplicated aliases while text after a hole retains source identity",
          "[state][event-copy]") {
    state::EventCollection source, destination;
    const auto a = add(source, "Shared");
    a->set_string(1, a->strings[0]);
    a->set_string(4, a->strings[0]);
    a->set_string(6, a->strings[0]);
    a->set_string(34, state::EventString(std::string(300, 'W')));
    REQUIRE(state::copy_events(source, destination));
    const auto b = destination.at(0);
    REQUIRE(b->values.actions == a->values.actions);
    REQUIRE(b->values.who_civ == -2);
    REQUIRE(b->strings[0].value() == "Shared");
    REQUIRE(b->strings[0] != a->strings[0]);
    REQUIRE(b->strings[1] != b->strings[0]);
    REQUIRE(b->strings[4] != b->strings[0]);
    REQUIRE(b->strings[6] == a->strings[0]);
    REQUIRE(b->strings[34].value().size() == 300);
    a->strings[0].value()[0] = '!';
    REQUIRE(b->strings[0].value() == "Shared");
    REQUIRE(b->strings[6].value() == "!hared");
    a->strings[0].value() = std::string("A\0tail", 6);
    state::EventCollection terminated_copy;
    REQUIRE(state::copy_events(source, terminated_copy));
    REQUIRE(terminated_copy.at(0)->strings[0].value() == "A");
    REQUIRE_THROWS_AS(state::copy_events(source, source), std::invalid_argument);
    REQUIRE(source.size() == 1);
}
TEST_CASE("S02 copy into a nonempty destination rewrites strings starting at its old head",
          "[state][event-copy]") {
    state::EventCollection source, destination;
    const auto original = add(source, "Source");
    const auto old = add(destination, "Destination");
    old->values.turn = 99;
    old->set_string(1, state::EventString("Retained absent source field"));
    REQUIRE(state::copy_events(source, destination));
    REQUIRE(destination.size() == 2);
    REQUIRE(destination.at(0) == old);
    REQUIRE(old->values.turn == 99);
    REQUIRE(old->strings[0].value() == "Source");
    REQUIRE(old->strings[0] != original->strings[0]);
    REQUIRE(old->strings[1].value() == "Retained absent source field");
    REQUIRE(destination.at(1)->strings[0] == original->strings[0]);
}
TEST_CASE(
    "S02 compaction preserves event identities and payload and changes copied string identities",
    "[state][event-copy]") {
    state::EventCollection events;
    const auto first = add(events, "First"), second = add(events, "Second");
    first->set_string(6, first->strings[0]); // Hole at text slot zero: no text duplicates.
    const auto id = first->identity;
    const auto first_values = first->values;
    REQUIRE(state::compact_events(events));
    REQUIRE(events.size() == 2);
    const auto compacted = events.find(id);
    REQUIRE(compacted);
    REQUIRE(compacted == events.at(0));
    REQUIRE(compacted != first);
    REQUIRE(compacted->values == first_values);
    REQUIRE(compacted->strings[0] != first->strings[0]);
    REQUIRE(compacted->strings[6] == first->strings[0]);
    REQUIRE(events.at(1)->identity == second->identity);
    REQUIRE(events.at(1)->strings[0].value() == "Second");
    REQUIRE_FALSE(events.contains(first));
}
TEST_CASE("S02 compaction failure preserves the correct side of its replacement boundary",
          "[state][event-copy]") {
    for (const unsigned fail : {1U, 2U, 3U}) {
        state::EventCollection events;
        const auto original = add(events, "Text");
        unsigned strings = 0;
        std::vector<std::size_t> requests;
        const state::EventServices services{[&](state::EventAllocation request) {
                                                if (request.kind ==
                                                    state::EventAllocationKind::string) {
                                                    requests.push_back(request.bytes);
                                                    return ++strings != fail;
                                                }
                                                return true;
                                            },
                                            {}};
        REQUIRE(state::compact_events(events, services) == (fail == 3));
        REQUIRE(events.size() == 1);
        REQUIRE(events.at(0)->identity == original->identity);
        REQUIRE(events.at(0)->strings[0].value() == "Text");
        REQUIRE((events.at(0) == original) == (fail == 1));
        REQUIRE(
            std::all_of(requests.begin(), requests.end(), [](std::size_t n) { return n == 15; }));
    }
    EventRecord record;
    record.unit_name = 1;
    const Bytes text{'A', 0};
    auto input = wire(std::span{&record, 1}, text);
    state::EventState events;
    unsigned allocations = 0;
    REQUIRE_FALSE(input.read(events, {[&](state::EventAllocation request) {
                                          return request.kind !=
                                                     state::EventAllocationKind::string ||
                                                 ++allocations != 2;
                                      },
                                      {}}));
    REQUIRE_FALSE(events.loaded);
    REQUIRE(events.collection.at(0)->strings[0].value() == "A");
}
TEST_CASE("S02 copy observes live source text and stops at invalidated nodes or grown backing",
          "[state][event-copy]") {
    state::EventCollection source, destination;
    const auto original = add(source, "Before");
    REQUIRE(state::copy_events(source, destination,
                               {[&](state::EventAllocation request) {
                                    if (request.kind == state::EventAllocationKind::string)
                                        original->strings[0].value() = "After";
                                    return true;
                                },
                                {}}));
    REQUIRE(destination.at(0)->strings[0].value() == "After");
    destination.reset();
    REQUIRE_THROWS_AS(
        state::copy_events(source, destination,
                           {[&](state::EventAllocation request) {
                                if (request.kind == state::EventAllocationKind::string)
                                    original->strings[0].value() = std::string(100, 'X');
                                return true;
                            },
                            {}}),
        std::out_of_range);
    REQUIRE(destination.size() == 1);
    REQUIRE(destination.at(0)->strings[0] == original->strings[0]);
    destination.reset();
    REQUIRE_THROWS_AS(state::copy_events(source, destination,
                                         {[&](state::EventAllocation request) {
                                              if (request.kind ==
                                                  state::EventAllocationKind::string)
                                                  source.reset();
                                              return true;
                                          },
                                          {}}),
                      std::out_of_range);
    REQUIRE(source.size() == 0);
    REQUIRE(destination.size() == 1);
    REQUIRE_THROWS_AS(source.append({[&](state::EventAllocation) {
                                         REQUIRE(source.append());
                                         return true;
                                     },
                                     {}}),
                      std::out_of_range);
    REQUIRE(source.size() == 1); // The nested append survives the outer safe stop.
    REQUIRE(source.at(0)->values == state::EventValues{});
    REQUIRE_FALSE(source.at(0)->strings[0].present());
}
TEST_CASE("F06 event writes preserve order long text and live callbacks and stop at each service",
          "[formats][event-io]") {
    state::EventCollection events;
    const auto event = add(events, std::string(300, 'X'));
    event->set_string(1, state::EventString(""));
    event->set_string(4, state::EventString("Line"));
    event->set_string(6, state::EventString("Skipped"));
    event->set_string(34, state::EventString("Wave"));
    // tag, count, record, then four pairs of text/NUL calls.
    for (std::size_t fail = 0; fail <= 11; ++fail) {
        std::vector<std::size_t> sizes;
        std::size_t call = 0;
        const auto accept = [&] { return call++ != fail; };
        const bool complete = write_event_items(
            [&](std::span<const Byte> bytes) {
                sizes.push_back(bytes.size());
                return accept();
            },
            [&](std::span<const Byte> bytes) {
                sizes.push_back(bytes.size());
                return accept();
            },
            [&](Byte byte) {
                REQUIRE(byte == 0);
                sizes.push_back(1);
                return accept();
            },
            events);
        REQUIRE(complete == (fail == 11));
        const std::vector<std::size_t> expected{4, 4, 444, 300, 1, 0, 1, 4, 1, 4, 1};
        REQUIRE(sizes == std::vector<std::size_t>(
                             expected.begin(),
                             expected.begin() +
                                 static_cast<std::ptrdiff_t>(std::min(fail + 1, expected.size()))));
    }
    ByteWriter written;
    unsigned blocks = 0;
    REQUIRE(write_event_items(
        [&](std::span<const Byte> bytes) {
            written.append(bytes);
            if (++blocks == 2)
                event->values.turn = 1234;
            return true;
        },
        [&](std::span<const Byte> bytes) {
            written.append(bytes);
            event->set_string(34, state::EventString("Later"));
            return true;
        },
        [&](Byte byte) {
            written.u8(byte);
            return true;
        },
        events));
    REQUIRE(decode_event_record(std::span{written.bytes}.subspan(8, 444)).turn == 1234);
    REQUIRE(std::equal(written.bytes.end() - 6, written.bytes.end(),
                       Bytes{'L', 'a', 't', 'e', 'r', 0}.begin()));
    event->strings[0].value() = std::string("A\0tail", 6);
    std::vector<Bytes> texts;
    REQUIRE(write_event_items([](std::span<const Byte>) { return true; },
                              [&](std::span<const Byte> bytes) {
                                  texts.emplace_back(bytes.begin(), bytes.end());
                                  return true;
                              },
                              [](Byte) { return true; }, events));
    REQUIRE(texts.front() == Bytes{'A'});
}
TEST_CASE(
    "F06 event input preserves callback fields and stops on invalid services or detached nodes",
    "[formats][event-io]") {
    state::EventState events;
    Input input{header(1)};
    REQUIRE_FALSE(read_event_items(
        [&](std::size_t size) {
            if (size != 444)
                return input.item(size);
            const auto event = events.collection.at(0);
            event->values.give_tech_civ = 5;
            event->set_string(34, state::EventString("Retained"));
            return ReadResult{{0x7f}, false};
        },
        {}, events));
    REQUIRE(events.collection.at(0)->values.triggers == 0x7f);
    REQUIRE(events.collection.at(0)->values.give_tech_civ == 5);
    REQUIRE(events.collection.at(0)->strings[34].value() == "Retained");
    REQUIRE_THROWS_AS(
        read_event_items([](std::size_t) { return ReadResult{{}, true}; }, {}, events),
        std::invalid_argument);
    REQUIRE_FALSE(events.loaded);
    input = Input{header(1)};
    REQUIRE_THROWS_AS(read_event_items(
                          [&](std::size_t size) {
                              if (size != 444)
                                  return input.item(size);
                              events.collection.reset();
                              return ReadResult{Bytes(444), true};
                          },
                          {}, events),
                      std::out_of_range);
    REQUIRE(events.collection.size() == 0);
}
TEST_CASE("F06 loaded event storage is runtime owned and survives client lifetime replacement",
          "[session][event-io]") {
    state::ProcessContext process;
    session::SessionRuntime runtime(process, session::RuntimeId{51});
    runtime.begin_client_session({10, 20});
    EventRecord record;
    record.unit_name = 1;
    const Bytes text{'U', 0};
    auto input = wire(std::span{&record, 1}, text);
    const auto gameplay = runtime.gameplay_random().checkpoint();
    const auto presentation = runtime.presentation_random().checkpoint();
    REQUIRE(input.read(runtime.world().events));
    REQUIRE(runtime.gameplay_random().checkpoint() == gameplay);
    REQUIRE(runtime.presentation_random().checkpoint() == presentation);
    const auto id = runtime.world().events.collection.at(0)->identity;
    runtime.end_client_session();
    runtime.begin_client_session({30, 40});
    REQUIRE(runtime.world().events.loaded);
    REQUIRE(runtime.world().events.collection.find(id)->strings[0].value() == "U");
    runtime.world().events.collection.reset();
    REQUIRE(runtime.world().events.loaded); // Reset and loaded are separate operations.
}
