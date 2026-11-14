// Independently authored F06 sections 2/5/6 and F02 leader-flag expectations.
#include "formats/leader_io.hpp"
#include <catch2/catch_test_macros.hpp>
#include <algorithm>
#include <bit>

using namespace openciv;
using namespace openciv::formats;
namespace {
struct World {
    state::GlobalValues globals;
    std::unique_ptr<std::array<state::Civilization, 8>> civs =
        std::make_unique<std::array<state::Civilization, 8>>();
    std::array<state::CivilizationNames, 8> names;
    std::unique_ptr<state::RuleState> rules = std::make_unique<state::RuleState>();
    SavedCursor cursor{0x7788, 0x5566};
    LeaderLoadScratch scratch;
    World() {
        globals.player_civ = 1;
        for (std::size_t i = 0; i < civs->size(); ++i)
            (*civs)[i].tribe = static_cast<std::int16_t>(i);
        rules->text.initialize();
        for (auto& leader : rules->leaders) {
            leader.names = {state::NameReference{12, false}, state::NameReference{13, true}};
            leader.leader_name = state::NameReference{444, false};
            leader.plural = state::NameReference{14, true};
            leader.adjective = state::NameReference{32768, true};
            leader.titles.fill({state::NameReference{16, true}, state::NameReference{0, false}});
            leader.female = 1;
            leader.times_used = 72;
            leader.city_name_index = 73;
        }
    }
    bool read(const ReadItem& service) {
        return read_leader_items(service, globals, *civs, rules->leaders, cursor, scratch);
    }
    void restore(bool scenario) {
        restore_leader_names(scenario, globals, *civs, names, *rules, scratch);
    }
};
void text(state::FixedText<24>& to, std::string_view from) {
    REQUIRE(from.size() < to.bytes.size());
    std::copy(from.begin(), from.end(), to.bytes.begin());
    to.bytes[from.size()] = 0;
}
void scenario_rules(World& w) {
    for (std::size_t i = 0; i < w.rules->leaders.size(); ++i) {
        auto& live = w.rules->leaders[i];
        live.female = 0;
        live.names[0] = state::NameReference{w.rules->text.store("Male"), false};
        live.names[1] = state::NameReference{w.rules->text.store("Female"), false};
        live.plural = state::NameReference{w.rules->text.store("People"), false};
        live.adjective = state::NameReference{w.rules->text.store("Adjective"), false};
    }
    for (std::size_t g = 0; g < 7; ++g)
        w.rules->names.government_titles[g] = {static_cast<state::TextId>(100 + g * 2),
                                               static_cast<state::TextId>(101 + g * 2)};
}
Bytes input_bytes(const StoredLeader& first, const StoredLeader& second) {
    ByteWriter bytes;
    for (unsigned i = 0; i < 21; ++i) {
        bytes.u8(static_cast<Byte>(i));
        bytes.u8(static_cast<Byte>(i + 21));
        bytes.u8(static_cast<Byte>(i % 2));
    }
    bytes.i16(-321);
    bytes.i16(456);
    bytes.append(encode_leader(first));
    bytes.append(encode_leader(second));
    return std::move(bytes.bytes);
}
void two_humans(World& w) {
    w.globals.multiplayer = 3;
    w.globals.human_players = 5; // Include bit zero, then civilization two.
    w.globals.player_civ = 6;
    (*w.civs)[0].tribe = (*w.civs)[2].tribe = 3;
}
} // namespace

TEST_CASE("F06 leader items publish triplets and keep selected records in tribe-indexed scratch",
          "[formats][leader-io]") {
    World w;
    two_humans(w);
    w.rules->leaders[3].attack = 17;
    StoredLeader first, second;
    first.plural = -12;
    second.plural = 15;
    second.attack = -22;
    const auto bytes = input_bytes(first, second);
    MemoryItems memory(bytes);
    std::vector<std::size_t> sizes;
    std::vector<std::int8_t> players;
    REQUIRE(w.read([&](std::size_t size) {
        sizes.push_back(size);
        if (size == 48)
            players.push_back(w.globals.player_civ);
        return memory.read(size);
    }));
    auto expected = std::vector<std::size_t>(63, 1);
    expected.insert(expected.end(), {2, 2, 48, 48});
    REQUIRE(sizes == expected);
    REQUIRE(players == std::vector<std::int8_t>{0, 2});
    REQUIRE(w.globals.player_civ == 6);
    REQUIRE(w.scratch.by_tribe[3].value() == second);
    REQUIRE(w.scratch.by_tribe[0].backed == 0);
    REQUIRE(w.rules->leaders[3].times_used == 3);
    REQUIRE(w.rules->leaders[3].city_name_index == 24);
    REQUIRE(w.rules->leaders[3].female == 1);
    REQUIRE(w.rules->leaders[3].attack == 17);
    REQUIRE(w.rules->leaders[3].plural == state::NameReference{14, true});
    REQUIRE(w.cursor.x == -321);
    REQUIRE(w.cursor.y == 456);
    REQUIRE(memory.position() == bytes.size());

    for (const auto mode : {0, 3}) {
        World selected;
        selected.globals.multiplayer = static_cast<std::uint8_t>(mode);
        selected.globals.human_players = 0;
        selected.globals.player_civ = 4;
        (*selected.civs)[4].tribe = 9;
        MemoryItems selected_memory(bytes);
        REQUIRE(selected.read([&](std::size_t size) { return selected_memory.read(size); }));
        REQUIRE(selected_memory.position() == (mode == 0 ? 115 : 67));
        REQUIRE(selected.scratch.by_tribe[9].backed == (mode == 0 ? 48 : 0));
        if (mode == 0)
            REQUIRE(selected.scratch.by_tribe[9].value() == first);
        REQUIRE(selected.globals.player_civ == 4);
    }
}
TEST_CASE("F06 every truncated leader-item prefix retains earlier fields and scratch backing",
          "[formats][leader-io]") {
    StoredLeader first, second;
    first.plural = -111;
    first.leader_name = 222;
    second.plural = -333;
    second.leader_name = -444;
    const auto bytes = input_bytes(first, second);
    for (std::size_t length = 0; length <= bytes.size(); ++length) {
        World w;
        two_humans(w);
        MemoryItems memory(std::span{bytes}.first(length));
        REQUIRE(w.read([&](std::size_t size) { return memory.read(size); }) ==
                (length == bytes.size()));
        REQUIRE(w.globals.player_civ == 6);
        for (std::size_t i = 0; i < 21; ++i) {
            const auto& live = w.rules->leaders[i];
            REQUIRE(live.times_used == (length > i * 3 ? bytes[i * 3] : 72));
            REQUIRE(live.city_name_index == (length > i * 3 + 1 ? bytes[i * 3 + 1] : 73));
            REQUIRE(live.female == (length > i * 3 + 2 ? bytes[i * 3 + 2] : 1));
            REQUIRE(live.plural ==
                    state::NameReference{14, true}); // Saved Leader never overwrites the rule row.
        }
        Bytes cursor{0x88, 0x77, 0x66, 0x55};
        const auto cursor_bytes = length > 63 ? std::min<std::size_t>(4, length - 63) : 0;
        std::copy_n(bytes.begin() + 63, cursor_bytes, cursor.begin());
        ByteReader cursor_reader(cursor);
        REQUIRE(w.cursor.x == cursor_reader.i16());
        REQUIRE(w.cursor.y == cursor_reader.i16());
        const auto backed = length > 67 ? std::min<std::size_t>(48, length - 67) : 0;
        const auto& record = w.scratch.by_tribe[3];
        REQUIRE(record.backed == backed);
        if (backed < 48) {
            REQUIRE_THROWS_AS(record.value(), std::out_of_range);
            REQUIRE(std::equal(record.bytes.begin(),
                               record.bytes.begin() + static_cast<std::ptrdiff_t>(backed),
                               bytes.begin() + 67));
        } else {
            auto expected = encode_leader(first);
            const auto tail = length - 115;
            std::copy_n(bytes.begin() + 115, tail, expected.begin());
            REQUIRE(encode_leader(record.value()) == expected);
        }
    }
}
TEST_CASE("F06 item callbacks retain live fields and masks but captured record destinations",
          "[formats][leader-io]") {
    World w;
    unsigned calls = 0;
    REQUIRE_FALSE(w.read([&](std::size_t size) {
        ++calls;
        if (calls == 64) {
            w.cursor.x = std::bit_cast<std::int16_t>(std::uint16_t{0xabcd});
            return ReadResult{{0x34}, false};
        }
        return ReadResult{Bytes(size, 0), true};
    }));
    REQUIRE(w.cursor.x == std::bit_cast<std::int16_t>(std::uint16_t{0xab34}));
    REQUIRE(w.cursor.y == 0x5566);
    REQUIRE(calls == 64);

    two_humans(w);
    w.globals.human_players = 1;
    (*w.civs)[0].tribe = 4;
    (*w.civs)[2].tribe = 5;
    std::vector<std::int8_t> players;
    REQUIRE(w.read([&](std::size_t size) {
        if (size == 48) {
            players.push_back(w.globals.player_civ);
            w.globals.human_players |= 4;
            (*w.civs)[0].tribe = 6;
        }
        return ReadResult{Bytes(size, 0), true};
    }));
    REQUIRE(players == std::vector<std::int8_t>{0, 2});
    REQUIRE(w.scratch.by_tribe[4].backed == 48);
    REQUIRE(w.scratch.by_tribe[5].backed == 48);
    REQUIRE(w.scratch.by_tribe[6].backed == 0);
    REQUIRE(w.globals.player_civ == 6);
    REQUIRE_THROWS_AS(w.read([](std::size_t size) { return ReadResult{Bytes(size + 1, 0), true}; }),
                      std::invalid_argument);
    two_humans(w);
    REQUIRE_THROWS_AS(w.read([](std::size_t size) {
        if (size == 48)
            throw std::runtime_error("Record service stopped");
        return ReadResult{Bytes(size, 0), true};
    }),
                      std::runtime_error);
    REQUIRE(w.globals.player_civ == 6);
    REQUIRE(w.scratch.by_tribe[3].backed == 0);
    unsigned records = 0;
    REQUIRE_FALSE(w.read([&](std::size_t size) {
        if (size == 48) {
            ++records;
            return ReadResult{Bytes(size, 1), false}; // Full transfer may still fail.
        }
        return ReadResult{Bytes(size, 0), true};
    }));
    REQUIRE(records == 1);
    REQUIRE(w.scratch.by_tribe[3].backed == 48);
    REQUIRE(w.scratch.by_tribe[3].value().attack == 1);
    REQUIRE(w.globals.player_civ == 6);
}
TEST_CASE("F06 leader writes observe live fields and restore the multiplayer selector on failure",
          "[formats][leader-io]") {
    World w;
    two_humans(w);
    std::vector<Bytes> writes;
    std::vector<std::int8_t> players;
    REQUIRE_FALSE(write_leader_items(
        [&](std::span<const Byte> bytes) {
            writes.emplace_back(bytes.begin(), bytes.end());
            if (writes.size() == 1)
                w.rules->leaders[0].city_name_index = 99;
            if (bytes.size() == 48) {
                players.push_back(w.globals.player_civ);
                w.rules->leaders[3].plural.custom = false;
                return players.size() < 2;
            }
            return true;
        },
        w.globals, *w.civs, w.rules->leaders, w.cursor));
    REQUIRE(writes.size() == 67);
    REQUIRE(writes[1] == Bytes{99});
    REQUIRE(players == std::vector<std::int8_t>{0, 2});
    REQUIRE(decode_leader(writes[65]).plural == -1);
    REQUIRE(decode_leader(writes[66]).plural == 1);
    REQUIRE(w.globals.player_civ == 6);
    w.globals.human_players = 0;
    unsigned count = 0;
    REQUIRE(write_leader_items(
        [&](std::span<const Byte>) {
            ++count;
            return true;
        },
        w.globals, *w.civs, w.rules->leaders, w.cursor));
    REQUIRE(count == 65);
    REQUIRE(w.globals.player_civ == 6);
}
TEST_CASE("F06 ordinary loaded names normalize all leaders and use saved signs without replacement",
          "[formats][leader-names]") {
    World w;
    (*w.civs)[1].tribe = 2;
    StoredLeader saved;
    saved.leader_name = 99; // The sign here does not control current-name negation.
    saved.plural = -100;
    saved.adjective = -200;
    saved.titles[0] = {-300, 1};
    saved.female = 0;
    saved.attack = 67;
    w.scratch.by_tribe[2].assign(saved);
    w.restore(false);
    const auto& live = w.rules->leaders[2];
    REQUIRE(live.leader_name ==
            state::NameReference{13, false}); // Negation, not forcing a negative sign.
    REQUIRE(live.names == std::array<state::NameReference, 2>{state::NameReference{12, false},
                                                              state::NameReference{13, true}});
    REQUIRE(live.plural == state::NameReference{14, true});
    REQUIRE(live.adjective == state::NameReference{32768, true});
    REQUIRE(live.titles[0] == std::array<state::NameReference, 2>{state::NameReference{16, true},
                                                                  state::NameReference{0, false}});
    REQUIRE(live.titles[1][0] == state::NameReference{16, false});
    REQUIRE(live.female == 1);
    REQUIRE(live.attack == 0);
    REQUIRE(live.times_used == 72);
    REQUIRE(live.city_name_index == 73);
    REQUIRE(w.rules->leaders[20].leader_name == state::NameReference{13, true});
    REQUIRE(w.rules->leaders[20].plural == state::NameReference{14, false});
    REQUIRE(w.rules->leaders[20].adjective == state::NameReference{32768, false});
    REQUIRE(w.rules->text.size() == 1);
}
TEST_CASE("F06 repeated human tribes toggle saved name signs once for every selected civilization",
          "[formats][leader-names]") {
    for (const auto humans : {std::uint8_t{0x81}, std::uint8_t{0x85}}) {
        World w;
        w.globals.multiplayer = 3;
        w.globals.human_players = humans;
        w.globals.player_civ = 5;
        (*w.civs)[0].tribe = (*w.civs)[2].tribe = (*w.civs)[7].tribe = 3;
        StoredLeader saved;
        saved.plural = saved.titles[0][0] = -1;
        w.scratch.by_tribe[3].assign(saved);
        w.restore(false);
        REQUIRE(w.rules->leaders[3].leader_name == state::NameReference{13, humans == 0x81});
        REQUIRE(w.rules->leaders[3].plural == state::NameReference{14, humans != 0x81});
        REQUIRE(w.rules->leaders[3].titles[0][0] == state::NameReference{16, humans != 0x81});
        REQUIRE(w.globals.player_civ == 5);
    }
}
TEST_CASE("F06 name restoration safe stops preserve normalization and selected-name effects",
          "[formats][leader-names]") {
    World w;
    w.rules->leaders[4].female = 2;
    REQUIRE_THROWS_AS(w.restore(false), std::out_of_range);
    REQUIRE(w.rules->leaders[3].leader_name == state::NameReference{13, true});
    REQUIRE(w.rules->leaders[4].leader_name == state::NameReference{444, false});
    REQUIRE(w.rules->leaders[5].plural == state::NameReference{14, true});
    w.rules->leaders[4].female = 1;
    w.globals.multiplayer = 3;
    w.globals.human_players = 2;
    w.globals.player_civ = 6;
    REQUIRE_THROWS_AS(w.restore(false), std::out_of_range); // No saved scratch backing.
    REQUIRE(w.rules->leaders[1].leader_name == state::NameReference{13, false});
    REQUIRE(w.rules->leaders[1].plural == state::NameReference{14, false});
    REQUIRE(w.globals.player_civ == 6);
    REQUIRE(w.rules->leaders[20].leader_name == state::NameReference{13, true});
}
TEST_CASE("F06 scenario names reset titles on exact plural equality and retain the prior gender "
          "selection",
          "[formats][leader-names]") {
    World w;
    scenario_rules(w);
    const auto male = w.rules->leaders[1].names[0];
    w.rules->names.government_titles[0][0] = 40000;
    (*w.civs)[1].flags = 0x200;
    text(w.names[1].tribe, "People");
    text(w.names[2].tribe, "people"); // No case folding in this operation.
    const auto count = w.rules->text.size();
    w.restore(true); // No saved scratch is required by this branch.
    REQUIRE(w.rules->leaders[1].female == 1);
    REQUIRE((*w.civs)[1].leader_portrait == 22);
    REQUIRE(
        w.rules->leaders[1].leader_name ==
        state::NameReference{male.id, !male.custom}); // Step 16 does not reselect the female name.
    REQUIRE(w.rules->leaders[1].titles[0][0] == state::NameReference{40000, false});
    REQUIRE(w.rules->leaders[1].titles[6] ==
            std::array<state::NameReference, 2>{state::NameReference{112, false},
                                                state::NameReference{113, false}});
    REQUIRE(w.rules->leaders[2].titles[0][0] == state::NameReference{16, false});
    REQUIRE(w.rules->leaders[2].plural.custom);
    REQUIRE(w.rules->text.size() == count);
    REQUIRE(w.globals.player_civ == 1);

    World shared;
    scenario_rules(shared);
    (*shared.civs)[2].tribe = 1;
    (*shared.civs)[1].flags = 0x200;
    text(shared.names[1].tribe, "People");
    text(shared.names[2].tribe, "People");
    const auto name = shared.rules->leaders[1].names[0];
    const auto plural = shared.rules->leaders[1].plural;
    shared.restore(true);
    REQUIRE(shared.rules->leaders[1].leader_name == name);
    REQUIRE(shared.rules->leaders[1].plural == plural);
    REQUIRE(shared.rules->leaders[1].female == 0);
    REQUIRE((*shared.civs)[1].leader_portrait == 22);
    REQUIRE((*shared.civs)[2].leader_portrait == 1);
}
TEST_CASE("F06 scenario name comparison stops on missing text backing after gender and portrait",
          "[formats][leader-names]") {
    for (const bool missing_pool : {false, true}) {
        World w;
        scenario_rules(w);
        (*w.civs)[1].flags = 0x200;
        if (missing_pool)
            w.rules->leaders[1].plural = state::NameReference{30000, false};
        else
            w.names[1].tribe.bytes.fill('A');
        REQUIRE_THROWS_AS(w.restore(true), std::out_of_range);
        REQUIRE(w.rules->leaders[1].female == 1);
        REQUIRE((*w.civs)[1].leader_portrait == 22);
        REQUIRE(w.rules->leaders[1].leader_name == w.rules->leaders[1].names[0]);
        REQUIRE(w.rules->leaders[1].titles[0][0] == state::NameReference{16, false});
        REQUIRE(w.rules->leaders[20].leader_name == w.rules->leaders[20].names[0]);
    }
}
TEST_CASE(
    "F06 scenario save names copy nonnegative labels in order and preserve tails and custom text",
    "[formats][leader-names]") {
    World w;
    for (auto& names : w.names) {
        names.adjective.bytes.fill(0xa5);
        names.leader.bytes.fill(0xa5);
        text(names.tribe, "Custom");
    }
    for (auto& live : w.rules->leaders)
        live.adjective = live.plural = live.leader_name = state::NameReference{1, true};
    auto& live = w.rules->leaders[1];
    live.adjective = state::NameReference{w.rules->text.store("Caf\xe9"), false};
    live.leader_name = state::NameReference{0, false};
    const auto before = w.names;
    prepare_scenario_names(false, w.globals, *w.civs, w.names, *w.rules);
    REQUIRE(w.names == before);
    w.globals.game_flags = 0x40;
    prepare_scenario_names(false, w.globals, *w.civs, w.names, *w.rules);
    REQUIRE(w.names == before);
    w.globals.game_flags = 0x80;
    prepare_scenario_names(false, w.globals, *w.civs, w.names, *w.rules);
    REQUIRE(w.names[0] == before[0]);
    REQUIRE(w.names[1].adjective.bytes[3] == 0xe9);
    REQUIRE(w.names[1].adjective.bytes[4] == 0);
    REQUIRE(w.names[1].adjective.bytes[5] == 0xa5);
    REQUIRE(w.names[1].tribe == before[1].tribe);
    REQUIRE(w.names[1].leader.bytes[0] == '<');
    REQUIRE(w.names[1].leader.bytes[5] == 0);
    REQUIRE(w.names[1].leader.bytes[6] == 0xa5);
    REQUIRE(w.names[1].titles == before[1].titles);
    w.globals.game_flags = 0;
    live.plural = state::NameReference{w.rules->text.store(std::string(24, 'X')), false};
    live.leader_name = state::NameReference{w.rules->text.store("Later"), false};
    w.names[1].adjective.bytes[0] = '!';
    const auto leader_before = w.names[1].leader;
    REQUIRE_THROWS_AS(prepare_scenario_names(true, w.globals, *w.civs, w.names, *w.rules),
                      std::out_of_range);
    REQUIRE(w.names[1].adjective.bytes[0] == 'C'); // Earlier field is already updated.
    REQUIRE(w.names[1].tribe == before[1].tribe);
    REQUIRE(w.names[1].leader == leader_before);
}
