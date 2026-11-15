#include <catch2/catch_test_macros.hpp>

import std;
import openciv.formats.event_io;
import openciv.tests.classic_load_fixture;

using namespace openciv;
using namespace openciv::formats;
namespace {
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
TEST_CASE("S02 copy separates duplicated aliases while text after a hole retains source identity",
          "[state][event-copy]") {
    state::EventCollection source, destination;
    const auto a = add(source, "Shared");
    a->set_string(1, a->strings[0]);
    a->set_string(4, a->strings[0]);
    a->set_string(6, a->strings[0]);
    a->set_string(34, state::EventString(std::string(300, 'W')));
    CHECK_NOTHROW(state::copy_events(source, destination));
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
    CHECK_NOTHROW(state::copy_events(source, terminated_copy));
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
    CHECK_NOTHROW(state::copy_events(source, destination));
    REQUIRE(destination.size() == 2);
    REQUIRE(destination.at(0) == old);
    REQUIRE(old->values.turn == 99);
    REQUIRE(old->strings[0].value() == "Source");
    REQUIRE(old->strings[0] != original->strings[0]);
    REQUIRE(old->strings[1].value() == "Retained absent source field");
    REQUIRE(destination.at(1)->strings[0] == original->strings[0]);
}

TEST_CASE("S02 compaction preserves stable event IDs and required aliases in one replacement",
          "[events]") {
    state::EventCollection events;
    auto original = add(events);
    original->set_string(6, original->strings[0]);
    const auto id = original->identity();
    const auto original_string = original->strings[0];
    CHECK_NOTHROW(state::compact_events(events));
    const auto replacement = events.find(id);
    REQUIRE(replacement);
    CHECK(replacement->values == original->values);
    CHECK(replacement == original);
    CHECK(replacement->strings[0] != original_string);
    CHECK(replacement->strings[6] == original_string);
    CHECK(events.append()->identity() != id);
    static_assert(
        std::is_same_v<decltype(std::as_const(events).at(0)), std::shared_ptr<const state::Event>>);
    static_assert(!std::is_copy_assignable_v<state::Event>);
}
TEST_CASE("F06 stored event bits round trip while live exports derive presence from strings",
          "[formats][event-io]") {
    auto saved = load_fixture::saved();
    saved.events.emplace();
    StoredEvent input;
    input.record.unit_name = 0x44332211;
    input.record.retained.fill(0x7f);
    input.record.actions = 0x800001;
    input.strings.names[0] = Bytes{'U'};
    saved.events->entries.push_back(input);
    const auto bytes = encode_stored_save(saved);
    CHECK(encode_stored_save(decode_stored_save(bytes)) == bytes);
    load_fixture::Fixture f;
    f.bytes = bytes;
    REQUIRE(f.load() == session::CoreLoadStatus::success);
    const auto& events = f.runtime.world().events.collection;
    CHECK(events.at(0)->values.actions == input.record.actions);
    Bytes exported;
    const WriteItem append = [&](std::span<const Byte> block) {
        exported.insert(exported.end(), block.begin(), block.end());
        return true;
    };
    REQUIRE(write_event_items(
        append, append,
        [&](Byte byte) {
            exported.push_back(byte);
            return true;
        },
        events, f.files.event_bytes));
    const auto record =
        decode_event_record(std::span<const Byte>(exported).subspan(8, event_record_size));
    CHECK(record.unit_name == 1);
    CHECK(record.retained == input.record.retained);
    CHECK_NOTHROW(state::compact_events(f.runtime.world().events.collection));
    CHECK(f.files.event_bytes.at(events.at(0)->identity()).event.lock() == events.at(0));
}

TEST_CASE("F06 opaque imported event bytes cannot attach to a different event with the same ID",
          "[formats][event-io]") {
    state::EventCollection imported, fresh;
    const auto original = imported.append();
    const auto current = fresh.append();
    REQUIRE(original->identity() == current->identity());
    EventCompatibility compatibility;
    ImportedEventBytes saved;
    saved.event = original;
    saved.retained.fill(0x7e);
    compatibility.emplace(original->identity(), saved);
    Bytes output;
    const WriteItem append = [&](std::span<const Byte> bytes) {
        output.insert(output.end(), bytes.begin(), bytes.end());
        return true;
    };
    REQUIRE(write_event_items(
        append, append,
        [&](Byte byte) {
            output.push_back(byte);
            return true;
        },
        fresh, compatibility));
    CHECK(
        decode_event_record(std::span<const Byte>(output).subspan(8, event_record_size)).retained ==
        std::array<Byte, 40>{});
}
TEST_CASE("Owned event installation installs independent strings and fresh event identities",
          "[events]") {
    state::EventCollection collection;
    const auto original = collection.append();
    original->values.turn = 19;
    state::EventCatalog input(1);
    input[0].values.turn = 27;
    input[0].strings[0] = "owned source";
    state::install_events(input, collection);
    CHECK(collection.at(0)->identity() != original->identity());
    input[0].strings[0] = "edited input";
    CHECK(collection.at(0)->strings[0].value() == "owned source");
}
