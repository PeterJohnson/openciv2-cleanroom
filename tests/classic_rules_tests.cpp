// Independently authored F03 sections 2-7,9/10 and Appendix A expectations.
// These are specification tests, not captures from the original executable.
#include "formats/classic_rules.hpp"
#include "formats/city_names.hpp"
#include "formats/city_preferences.hpp"
#include "formats/save_records.hpp"
#include "formats/rules_install.hpp"
#include "rule_fixture.hpp"
#include <catch2/catch_test_macros.hpp>
#include <algorithm>
#include <tuple>
#include <map>

using namespace openciv;
using namespace openciv::formats::classic;
namespace {
struct Fixture {
    std::string bytes;
    std::map<std::string, std::string> files;
    std::vector<std::string> opens;
    TextWorkspace workspace;
    TextDirectories directories{"caller", "scenario", "home"};
    std::unique_ptr<state::RuleState> runtime = std::make_unique<state::RuleState>();
    state::RuleDefinitions* values = &runtime->definitions;
    state::RuleState& retained = *runtime;
    RuleLoadServices hooks;
    TextReader reader;
    explicit Fixture(std::string text)
        : bytes(std::move(text)), reader(workspace, directories, services(), ascii_text_case()) {
        retained.text.initialize();
    }
    TextFiles services() {
        return {[this](std::string_view directory, std::string_view path) -> OpenedText {
                    auto key = std::string(directory) + "/" + std::string(path);
                    opens.push_back(key);
                    if (files.empty())
                        return {key, std::make_unique<MemoryTextStream>(bytes)};
                    const auto it = files.find(key);
                    if (it == files.end())
                        return {};
                    return {key, std::make_unique<MemoryTextStream>(it->second)};
                },
                {}};
    }
    RuleLoader loader() { return {reader, retained, ascii_text_case(), hooks}; }
    void select(std::string_view section) { REQUIRE(reader.open("RULES", section)); }
};
std::string repeat(std::string_view row, std::size_t count) {
    std::string out;
    while (count--)
        out += std::string(row) + "\n";
    return out;
}
// Authored independently of the native fixture's serialization and the loader.
std::array<std::string, 12> complete_sections() {
    std::array<std::string, 12> out{
        "@COSMIC\n3\n2\n2\n5\n5\n1\n2\n7\n14\n8\n12\n10\n20\n3\n3\n8\n1\n50\n50\n10\n75\n5\n",
        "@CIVILIZE\n",
        "@IMPROVE\n",
        "@ENDWONDER\n",
        "@UNITS\n",
        "@TERRAIN\n",
        "@GOVERNMENTS\n",
        "@LEADERS\n",
        "@CARAVAN\n",
        "@ORDERS\n",
        "@DIFFICULTY\n",
        "@ATTITUDES\n"};
    for (int i = 0; i < 100; ++i)
        out[1] += "advances " + std::to_string(i) + ",3,1,nil,nil,0,3\n";
    for (int i = 0; i < 67; ++i)
        out[2] += "improvements " + std::to_string(i) + ",10,1,nil\n";
    out[3] += repeat("nil", 28);
    for (int i = 0; i < 62; ++i)
        out[4] += "units " + std::to_string(i) + ",no,0,2,0,1,1,3,1,10,0,0,nil,0\n";
    for (int i = 0; i < 33; ++i) {
        out[5] += "terrain " + std::to_string(i) + ",1,3,1,1,0";
        if (i < 11)
            out[5] += ",yes,1,3,2,no,0,4,1,Drt";
        out[5] += '\n';
    }
    for (int i = 0; i < 7; ++i)
        out[6] += "governments " + std::to_string(i) + ",Leader,Leader\n";
    for (int i = 0; i < 21; ++i)
        out[7] += "Leader,Leader,1,1,0,tribes " + std::to_string(i) + ",Example,0,0,0\n";
    for (int i = 0; i < 16; ++i)
        out[8] += "commodities " + std::to_string(i) + "\n";
    for (int i = 0; i < 12; ++i)
        out[9] += "orders " + std::to_string(i) + ",x\n";
    for (int i = 0; i < 6; ++i)
        out[10] += "difficulties " + std::to_string(i) + "\n";
    for (int i = 0; i < 9; ++i)
        out[11] += "diplomaticAttitudes " + std::to_string(i) + "\n";
    return out;
}
template <class Action>
void reference_error(Action action, int code, std::size_t row, int position,
                     std::string_view line) {
    try {
        action();
        FAIL("Expected fatal rule reference boundary");
    } catch (const RuleLoadError& error) {
        REQUIRE(error.fault == RuleLoadFault::reference);
        REQUIRE(error.module == 3);
        REQUIRE(error.code == code);
        REQUIRE(error.row == row);
        REQUIRE(error.position == position);
        REQUIRE(error.line == line);
    }
}
} // namespace

TEST_CASE("F03 complete classic tables match independently authored native definitions",
          "[classic][rules]") {
    const auto native = rule_fixture::Input{}.load();
    auto sections = complete_sections();
    // Physical order deliberately differs from full-load request order.
    std::reverse(sections.begin(), sections.end());
    std::string bytes;
    for (const auto& section : sections)
        bytes += section;
    Fixture f(bytes);
    auto identities = [](auto& into, const auto& from) {
        for (std::size_t i = 0; i < into.size(); ++i)
            into[i].identity.id = from[i].identity.id;
    };
    auto& actual = *f.values;
    const auto& expected = native->definitions;
    identities(actual.advances, expected.advances);
    identities(actual.improvements, expected.improvements);
    identities(actual.units, expected.units);
    identities(actual.terrain, expected.terrain);
    identities(actual.governments, expected.governments);
    identities(actual.orders, expected.orders);
    for (std::size_t i = 0; i < actual.tribes.size(); ++i)
        actual.tribes[i].id = expected.tribes[i].id;
    for (std::size_t i = 0; i < actual.commodities.size(); ++i)
        actual.commodities[i].id = expected.commodities[i].id;
    for (std::size_t i = 0; i < actual.difficulties.size(); ++i)
        actual.difficulties[i].id = expected.difficulties[i].id;
    for (std::size_t i = 0; i < actual.diplomatic_attitudes.size(); ++i)
        actual.diplomatic_attitudes[i].id = expected.diplomatic_attitudes[i].id;
    f.retained.double_movement_applied = true;
    f.retained.names.orders[0] = 777;
    f.retained.leaders[0].times_used = 19;
    f.retained.leaders[0].city_name_index = 23;
    f.loader().load("rules", true);
    REQUIRE(actual == expected);
    REQUIRE(f.opens == std::vector<std::string>(3, "scenario/RULES.TXT"));
    REQUIRE_FALSE(f.reader.is_open());
    REQUIRE(f.directories.working == "caller");
    REQUIRE(f.retained.text.size() == 411);
    REQUIRE(f.retained.names.advances[0] == 1);
    REQUIRE(f.retained.names.improvements[0] == 101);
    REQUIRE(f.retained.names.terrain[0] == 230);
    REQUIRE(f.retained.names.governments[0] == 263);
    REQUIRE(f.retained.names.government_titles[0] == std::array<state::TextId, 2>{264, 265});
    REQUIRE(f.retained.names.orders[0] == 777);
    REQUIRE(f.retained.leaders[0].names == std::array<std::int16_t, 2>{284, 285});
    REQUIRE(f.retained.leaders[0].leader_name == 285);
    REQUIRE(f.retained.leaders[0].plural == 286);
    REQUIRE(f.retained.leaders[0].titles[0] == std::array<std::int16_t, 2>{264, 265});
    REQUIRE(f.retained.leaders[0].times_used == 19);
    REQUIRE(f.retained.leaders[0].city_name_index == 23);
    REQUIRE(formats::decode_leader(formats::encode_leader(f.retained.leaders[0])) ==
            f.retained.leaders[0]);
    REQUIRE_FALSE(f.retained.double_movement_applied);
    for (std::size_t i = 0; i < 62; ++i)
        REQUIRE(f.retained.names.interface_units[i] == 168 + i);
    for (const auto& leader : f.retained.leaders)
        REQUIRE(f.retained.text.text(state::label_magnitude(leader.leader_name)) == "Leader");
}
TEST_CASE("F03 classic reference vocabulary is independent of names and case", "[classic][rules]") {
    REQUIRE(advance_reference("nil") == state::AdvanceReference::none);
    REQUIRE(advance_reference("no") == state::AdvanceReference::never);
    REQUIRE(advance_reference("NIL") == state::AdvanceReference::invalid);
    REQUIRE(advance_reference("afl") == state::AdvanceReference::invalid);
    REQUIRE(advance_reference("nil ; note") == state::AdvanceReference::invalid);
    REQUIRE(advance_reference("AFl") == static_cast<state::AdvanceReference>(0));
    REQUIRE(advance_reference("FP") == static_cast<state::AdvanceReference>(32));
    REQUIRE(advance_reference("X7") == static_cast<state::AdvanceReference>(99));
    REQUIRE(terrain_result("YES", ascii_text_case()) == state::TerrainResult::improve_in_place);
    REQUIRE(terrain_result("No", ascii_text_case()) == state::TerrainResult::no_result);
    REQUIRE(terrain_result("Drt", ascii_text_case()) == static_cast<state::TerrainResult>(0));
    REQUIRE(terrain_result("Oce", ascii_text_case()) == static_cast<state::TerrainResult>(10));
    REQUIRE(terrain_result("drt", ascii_text_case()) == state::TerrainResult::invalid);
}
TEST_CASE("F03 advance defaults and disabled fixups preserve initial enablement",
          "[classic][rules]") {
    Fixture f("@CIVILIZE\nA,3,0,Alp,nil,0,0\nB,3,0,no,nil,0,0\nC,3,0,nil,no,0,0\n; end\n"
              "@IMPROVE\n" +
              repeat("Building,-1,258,Alp", 67) + "@ENDWONDER\n" + repeat("Alp,ignored", 28));
    auto loader = f.loader();
    f.select("CIVILIZE");
    loader.advances();
    REQUIRE(f.values->enabled_advance_count == 2);
    REQUIRE(f.values->advances[0].enabled);
    REQUIRE(f.values->advances[0].prerequisites[0] == state::AdvanceReference::never);
    REQUIRE(f.values->advances[2].enabled);
    REQUIRE(f.values->advances[3].identity.name.classic == "Advance 4");
    REQUIRE(f.values->advances[99].identity.name.classic == "Advance 100");
    REQUIRE(f.values->advances[3].ai_value == 3);
    f.select("IMPROVE");
    loader.improvements();
    REQUIRE(f.values->improvements[0].cost_rows == 255);
    REQUIRE(f.values->improvements[0].upkeep == 2);
    REQUIRE(f.values->improvements[0].prerequisite == state::AdvanceReference::never);
    f.select("ENDWONDER");
    loader.wonder_expiry();
    REQUIRE(f.values->wonder_obsolescence[0] == static_cast<state::AdvanceReference>(1));
    Fixture cycles("@CIVILIZE\nA,3,0,Alp,AFl,0,0\nB,3,0,AFl,nil,0,0\n\n");
    cycles.select("CIVILIZE");
    cycles.loader().advances();
    REQUIRE(cycles.values->enabled_advance_count == 2);
    REQUIRE(cycles.values->advances[0].prerequisites[0] == static_cast<state::AdvanceReference>(1));
}
TEST_CASE("F03 keep names consumes tokens without allocations including default tails",
          "[classic][rules]") {
    Fixture f("@CIVILIZE\nNew,-129,258,nil,nil,258,259\n\n@IMPROVE\n" + repeat("New,10,1,nil", 67) +
              "@UNITS\nNew,no,0,43,258,-1,256,13,2,0,257,7,nil,101x11\n\n");
    for (auto& row : f.values->advances)
        row.identity = {"stable.advance", {"Old", "Old"}};
    for (auto& row : f.values->improvements)
        row.identity.name = {"Building", "Building"};
    for (auto& row : f.values->units) {
        row.identity.name = {"Unit", "Unit"};
        row.abilities = 0xffffffffU;
    }
    f.values->cosmic.values[0] = 3;
    f.retained.names.interface_units.fill(42);
    f.retained.names.advances.fill(77);
    f.retained.names.improvements.fill(88);
    f.retained.names.units.fill(99);
    auto loader = f.loader();
    f.select("CIVILIZE");
    loader.advances(true);
    REQUIRE(f.values->advances[0].ai_value == 127);
    REQUIRE(f.values->advances[0].civilized_ai_modifier == 2);
    REQUIRE(f.values->advances[0].epoch == static_cast<state::AdvanceEpoch>(2));
    REQUIRE(f.values->advances[0].category == static_cast<state::AdvanceCategory>(3));
    REQUIRE(f.values->advances[99].identity.name.classic == "Old");
    f.select("IMPROVE");
    loader.improvements(true);
    REQUIRE(f.values->improvements[66].identity.name.classic == "Building");
    f.select("UNITS");
    loader.units(true);
    const auto& unit = f.values->units[0];
    REQUIRE(unit.movement == 129);
    REQUIRE(unit.range == 2);
    REQUIRE(unit.attack == 255);
    REQUIRE(unit.defense == 0);
    REQUIRE(unit.hit_points == 130);
    REQUIRE(unit.firepower == 2);
    REQUIRE(unit.cost_rows == 1);
    REQUIRE(unit.capacity == 1);
    REQUIRE(unit.role == state::RuleRole::trade);
    REQUIRE(unit.abilities == 5);
    REQUIRE(f.values->units[61].identity.name.classic == "Unit");
    REQUIRE(f.values->units[61].attack == 1);
    REQUIRE(f.values->units[61].cost_rows == 1);
    REQUIRE(f.values->units[61].abilities == 0);
    REQUIRE(f.retained.names.interface_units[0] == 42);
    REQUIRE(f.retained.names.interface_units[61] == 42);
    REQUIRE(f.retained.names.advances[99] == 77);
    REQUIRE(f.retained.names.improvements[66] == 88);
    REQUIRE(f.retained.names.units[61] == 99);
    REQUIRE(f.retained.text.size() == 1);
}
TEST_CASE("F03 padded storage appends without truncation and retains unit tail interface names",
          "[classic][rules]") {
    Fixture f("@UNITS\n,no,0,1,0,1,1,1,1,1,0,0,nil,0\n\n");
    f.retained.names.interface_units.fill(123);
    f.select("UNITS");
    f.loader().units();
    REQUIRE(f.values->units[0].identity.name.classic == " ");
    REQUIRE(f.retained.text.allocation(1) == " " + std::string(14, '\0'));
    REQUIRE(f.retained.names.interface_units[0] == 1);
    REQUIRE(f.retained.names.interface_units[1] == 123);
    REQUIRE(f.retained.names.units[1] == 2);
    REQUIRE(f.retained.text.text(f.retained.names.units[1]) == "Unit 2");
    REQUIRE(f.values->units[61].identity.name.classic == "Unit 62");
    f.bytes = "@CARAVAN\n" + repeat("A commodity name longer than ten", 16);
    f.select("CARAVAN");
    f.loader().commodities();
    REQUIRE(f.retained.text.size() == 79);
    REQUIRE(f.retained.text.allocation(*f.retained.text.last()).size() == 33);
    REQUIRE(f.values->commodities[15].name.classic == "A commodity name longer than ten");
}
TEST_CASE("F03 reference errors retain their ordered prefix and cannot return to parsing",
          "[classic][rules]") {
    for (int p = 0; p < 2; ++p) {
        const auto row = p == 0 ? "Name,-1,2,NIL,nil,3,4" : "Name,-1,2,nil,NIL,3,4";
        Fixture f("@CIVILIZE\n" + std::string(row) + "\n");
        f.values->advances[0].epoch = state::AdvanceEpoch::industrial;
        f.values->advances[99].enabled = true;
        unsigned reports = 0;
        f.hooks.fatal = [&](const RuleLoadError&) { ++reports; };
        f.select("CIVILIZE");
        reference_error([&] { f.loader().advances(); }, -4, 0, p, row);
        REQUIRE(reports == 1);
        REQUIRE(f.values->advances[0].ai_value == -1);
        REQUIRE(f.values->advances[0].epoch == state::AdvanceEpoch::industrial);
        REQUIRE(f.values->advances[0].prerequisites[static_cast<std::size_t>(p)] ==
                state::AdvanceReference::invalid);
        REQUIRE_FALSE(f.values->advances[99].enabled);
        REQUIRE(f.retained.text.size() == 2);
    }
    Fixture f("@IMPROVE\nGood,7,8,nil\nBad,-1,2,NIL\n@ENDWONDER\nNIL,ignored\n"
              "@UNITS\nBad,NIL,2,3,4,5,6,7,8,999,9,1,NIL,101\n");
    auto loader = f.loader();
    f.select("IMPROVE");
    reference_error([&] { loader.improvements(); }, -5, 1, 0, "Bad,-1,2,NIL");
    REQUIRE(f.values->improvements[0].cost_rows == 7);
    REQUIRE(f.values->improvements[1].cost_rows == 255);
    REQUIRE(f.values->improvements[1].prerequisite == state::AdvanceReference::invalid);
    f.select("ENDWONDER");
    reference_error([&] { loader.wonder_expiry(); }, -4, 0, 0, "NIL,ignored");
    REQUIRE(f.values->wonder_obsolescence[0] == state::AdvanceReference::invalid);
    f.values->units[61].prerequisite = state::AdvanceReference::none;
    f.retained.double_movement_applied = true;
    f.select("UNITS");
    reference_error([&] { loader.units(); }, -6, 0, 0, "Bad,NIL,2,3,4,5,6,7,8,999,9,1,NIL,101");
    REQUIRE(f.values->units[0].cost_rows == 200);
    REQUIRE(f.values->units[0].hit_points == 70);
    REQUIRE(f.values->units[0].abilities == 5);
    REQUIRE(f.values->units[61].prerequisite == state::AdvanceReference::never);
    REQUIRE_FALSE(f.retained.double_movement_applied);
}
TEST_CASE("F03 terrain errors publish complete work groups and special rows retain work",
          "[classic][rules]") {
    for (const int position : {0, 1, 99}) {
        auto row = std::string("Land,1,2,3,4,5,") + (position == 0 ? "bad" : "YES") + ",6,7,8," +
                   (position == 1 ? "bad" : "NO") + ",9,10,11," + (position == 99 ? "bad" : "Drt");
        Fixture f("@TERRAIN\n" + row + "\n");
        f.values->terrain[0].work = state::BaseTerrainWork{};
        f.values->terrain[0].work->transformation = state::TerrainResult::improve_in_place;
        f.select("TERRAIN");
        reference_error([&] { f.loader().terrain(); }, -7, 0, position, row);
        const auto& work = *f.values->terrain[0].work;
        REQUIRE(work.irrigation.ai_government_threshold == 8);
        REQUIRE(work.mining.ai_government_threshold == (position == 0 ? 0 : 11));
        REQUIRE(work.transformation == (position == 99 ? state::TerrainResult::invalid
                                                       : state::TerrainResult::improve_in_place));
    }
    Fixture f(complete_sections()[5]);
    state::BaseTerrainWork retained;
    retained.irrigation.work_turns = 123;
    retained.transformation = state::TerrainResult::improve_in_place;
    f.values->terrain[11].work = retained;
    f.values->terrain[32].work = retained;
    f.select("TERRAIN");
    f.loader().terrain();
    REQUIRE(f.values->terrain[11].work == retained);
    REQUIRE(f.values->terrain[32].work == retained);
    REQUIRE(f.values->terrain[0].work->irrigation.work_turns == 3);
}
TEST_CASE("F03 leaders replace fields and repeated titles while preservation still allocates",
          "[classic][rules]") {
    const auto row = "Male,Female,2,65538,65538,People,Adjective,-129,258,-1,1,A,B,1,C,D,6,,E";
    Fixture f("@GOVERNMENTS\n" + repeat("Government,Default M,Default F", 7) + "@LEADERS\n" +
              repeat(row, 21));
    auto loader = f.loader();
    f.select("GOVERNMENTS");
    loader.governments();
    f.select("LEADERS");
    loader.leaders(true);
    const auto& leader = f.values->tribes[0];
    REQUIRE(leader.preferred_gender == 1);
    REQUIRE(leader.preferred_color == 2);
    REQUIRE(leader.city_style == 2);
    REQUIRE(leader.personality == std::array<std::int8_t, 3>{127, 2, -1});
    REQUIRE(leader.titles[0].male.classic == "Default M");
    REQUIRE(leader.titles[1].male.classic == "C");
    REQUIRE(leader.titles[1].female.classic == "D");
    REQUIRE(leader.titles[6].male.classic.empty());
    REQUIRE(f.retained.leaders[0].titles[6][0] == 0);
    REQUIRE(f.retained.text.text(0) == "<nil>");
    REQUIRE(leader.titles[6].female.classic == "E");
    REQUIRE(f.retained.text.text(state::label_magnitude(f.retained.leaders[0].leader_name)) ==
            "Female");
    REQUIRE(f.retained.text.size() == 22 + 21 * 9);
    const auto before = std::make_unique<state::RuleDefinitions>(*f.values);
    const auto selected = f.retained.leaders;
    f.bytes = "@LEADERS\n" + repeat("New,Other,0,7,3,New People,New Adj,1,2,3,2,King,Queen", 21);
    f.select("LEADERS");
    loader.leaders(false);
    REQUIRE(*f.values == *before);
    REQUIRE(f.retained.leaders == selected);
    REQUIRE(f.retained.text.size() == 22 + 21 * 15);
}
TEST_CASE("F03 leader preservation captures each field before its allocation callback",
          "[classic][rules]") {
    Fixture f("@LEADERS\n" + repeat("Male,Female,0,1,0,People,Adj,0,0,0,1,King,Queen", 21));
    f.values->tribes[0].leaders[0] = {"Before", "Before"};
    f.values->tribes[0].titles[1].male = {"Old title", "Old title"};
    f.retained.leaders[0].names[0] = -123;
    f.retained.leaders[0].titles[1][0] = -456;
    unsigned calls = 0;
    f.hooks.allocate = [&](std::string_view text, std::size_t) {
        if (!calls++) {
            f.values->tribes[0].leaders[0] = {"Callback", "Callback"};
            f.retained.leaders[0].names[0] = 987;
        }
        if (text == "King")
            f.values->tribes[0].titles[1].male = {"Changed", "Changed"};
        if (text == "Queen" && calls == 6)
            REQUIRE(f.values->tribes[0].titles[1].male.classic == "Old title");
    };
    f.select("LEADERS");
    f.loader().leaders(false);
    REQUIRE(f.values->tribes[0].leaders[0].classic == "Before");
    REQUIRE(f.retained.leaders[0].names[0] == -123);
    REQUIRE(f.retained.leaders[0].titles[1][0] == -456);
    // Later callbacks are live and may change an already restored field.
    REQUIRE(f.values->tribes[0].titles[1].male.classic == "Changed");
    REQUIRE(calls == 126);
    Fixture invalid("@LEADERS\nM,F,0,1,0,P,A,0,0,0,7,Outside,Array\n");
    invalid.select("LEADERS");
    try {
        invalid.loader().leaders(true);
        FAIL("Title index must be bounded before writing outside the array");
    } catch (const RuleLoadError& error) {
        REQUIRE(error.fault == RuleLoadFault::invalid_title_index);
        REQUIRE(error.position == 7);
    }
    REQUIRE(invalid.retained.text.size() == 5);
}
TEST_CASE("F03 ordinary text preserves whole labels and empty pooling has no allocation",
          "[classic][rules]") {
    Fixture f("@ORDERS\n,\n" + repeat("Order, x extra", 11) + "@DIFFICULTY\n" +
              repeat("  A,B;label\t", 6) + "@ATTITUDES\n" + repeat(";friendly, really", 9));
    auto loader = f.loader();
    f.select("ORDERS");
    loader.orders();
    REQUIRE(f.values->orders[0].identity.name.classic.empty());
    REQUIRE(f.values->orders[0].shortcut == 0);
    REQUIRE(f.values->orders[11].shortcut == 'x');
    REQUIRE(f.retained.text.size() == 12);
    f.select("DIFFICULTY");
    loader.difficulties();
    REQUIRE(f.values->difficulties[5].name.classic == "A,B;label");
    f.select("ATTITUDES");
    loader.attitudes();
    REQUIRE(f.values->diplomatic_attitudes[8].name.classic == ";friendly, really");
    REQUIRE(f.retained.text.size() == 27);
    REQUIRE(f.retained.text.allocation(1) == std::string("Order\0", 6));
}
TEST_CASE("F03 allocation failure and text EOF retain only the reached publication prefix",
          "[classic][rules]") {
    Fixture f("@IMPROVE\n" + repeat("Building,7,8,nil", 67));
    unsigned allocations = 0;
    f.hooks.allocate = [&](std::string_view, std::size_t size) {
        REQUIRE(size == 25);
        if (++allocations == 2)
            throw std::bad_alloc();
    };
    f.values->improvements[1].cost_rows = 99;
    f.select("IMPROVE");
    REQUIRE_THROWS_AS(f.loader().improvements(), std::bad_alloc);
    REQUIRE(f.values->improvements[0].cost_rows == 7);
    REQUIRE(f.values->improvements[1].cost_rows == 99);
    REQUIRE(f.retained.text.size() == 2);
    Fixture eof("@COSMIC\n9\n");
    eof.values->cosmic.values.fill(77);
    eof.select("COSMIC");
    try {
        eof.loader().cosmic();
        FAIL("A fixed-count table cannot continue reading a closed stream");
    } catch (const TextError& error) {
        REQUIRE(error.fault == TextFault::unavailable_stream);
    }
    REQUIRE(eof.values->cosmic.values[0] == 9);
    REQUIRE(eof.values->cosmic.values[1] == 1); // Retained end cursor reads empty token.
    REQUIRE(eof.values->cosmic.values[2] == 77);
}
TEST_CASE("F03 full load uses explicit fallback then keeps the selected improvement stream",
          "[classic][rules]") {
    const auto sections = complete_sections();
    Fixture f("");
    f.files["scenario/RULES.TXT"] = sections[0];
    for (std::size_t i = 1; i < sections.size(); ++i)
        f.files["home/RULES.TXT"] += sections[i];
    f.values->advances[0].identity.name.classic = "RULES";
    f.loader().load(f.values->advances[0].identity.name.classic, false);
    REQUIRE(f.opens == std::vector<std::string>{"scenario/RULES.TXT", "scenario/RULES.TXT",
                                                "home/RULES.TXT", "scenario/RULES.TXT",
                                                "home/RULES.TXT"});
    REQUIRE(f.values->cosmic.values[0] == 3);
    REQUIRE(f.values->units[0].movement == 6);
    REQUIRE(f.values->governments[0].titles.male.classic == "Leader");
    REQUIRE(f.values->tribes[0].leaders[0].classic.empty());
    REQUIRE(f.values->diplomatic_attitudes[8].name.classic == "diplomaticAttitudes 8");
}
TEST_CASE("F03 missing reused sections stop after prior tables without home merging",
          "[classic][rules]") {
    const auto sections = complete_sections();
    Fixture f("");
    f.files["scenario/RULES.TXT"] = sections[0] + sections[1] + sections[2];
    for (const auto& section : sections)
        f.files["home/RULES.TXT"] += section;
    f.values->units[0].attack = 99;
    try {
        f.loader().load("RULES", true);
        FAIL("A missing reused section cannot reopen the home file");
    } catch (const TextError& error) {
        REQUIRE(error.fault == TextFault::unavailable_stream);
    }
    REQUIRE(f.opens == std::vector<std::string>(3, "scenario/RULES.TXT"));
    REQUIRE(f.values->enabled_advance_count == 100);
    REQUIRE(f.values->improvements[66].cost_rows == 10);
    REQUIRE(f.values->units[0].attack == 99);
    REQUIRE_FALSE(f.reader.is_open());
    REQUIRE(f.directories.search == "scenario");
    Fixture missing("");
    missing.values->cosmic.values[0] = 77;
    try {
        missing.loader().load("RULES", true);
        FAIL("A missing initial section is an explicit safe stop");
    } catch (const RuleLoadError& error) {
        REQUIRE(error.fault == RuleLoadFault::missing_section);
        REQUIRE(error.section == "COSMIC");
        REQUIRE(error.code == 0); // Not an invented original reference-error code.
    }
    REQUIRE(missing.values->cosmic.values[0] == 77);
}
TEST_CASE("F03 fixed rows consume blank and comment data and decode explicit classic bytes",
          "[classic][rules]") {
    Fixture f("@CARAVAN\n\n; commodity\nCaf\xe9\n" + repeat("Another", 13));
    f.select("CARAVAN");
    f.loader().commodities();
    REQUIRE(f.values->commodities[0].name.classic == " ");
    REQUIRE(f.values->commodities[1].name.classic == "; commodity");
    REQUIRE(f.values->commodities[2].name.utf8 == "Caf\xc3\xa9");
    REQUIRE(f.values->commodities[2].name.classic == "Caf\xe9");
    REQUIRE(f.retained.text.size() == 17);
    f.bytes = "@IMPROVE\n\nIgnored,1,2,nil\n";
    f.select("IMPROVE");
    reference_error([&] { f.loader().improvements(); }, -5, 0, 0, "");
    REQUIRE(f.values->improvements[0].identity.name.classic == " ");
    REQUIRE(f.values->improvements[0].cost_rows == 0);
    REQUIRE(f.values->improvements[1].identity.name.classic.empty());
}
TEST_CASE("F03 narrowed leader labels bind real pool ordinals through C01 and the save codec",
          "[classic][rules][text-pool]") {
    for (const state::TextId first : {32768U, 65536U}) {
        Fixture f("@LEADERS\n" + repeat("Male,Female,1,7,3,People,Adj,-1,1,2", 21));
        const auto plural_id = first == 32768 ? 32766U : 2U;
        while (f.retained.text.size() < first)
            f.retained.text.store(f.retained.text.size() == plural_id ? "Alias civ" : "Seed");
        f.retained.leaders[0].times_used = 77;
        f.select("LEADERS");
        f.loader().leaders(true);
        const auto& live = f.retained.leaders[0];
        REQUIRE(live.names[0] == (first == 32768 ? -32768 : 0));
        REQUIRE(live.names[1] == (first == 32768 ? -32767 : 1));
        REQUIRE(live.plural == (first == 32768 ? -32766 : 2));
        REQUIRE(live.leader_name == live.names[1]);
        REQUIRE(live.times_used == 77);
        REQUIRE(live.color == 7);
        REQUIRE(live.city_style == 3);
        REQUIRE(live.attack == -1);
        REQUIRE(live.expand == 1);
        REQUIRE(live.civilize == 2);
        REQUIRE(f.retained.text.text(state::label_magnitude(live.leader_name)) == "Seed");
        REQUIRE(f.retained.text.text(state::label_magnitude(live.plural)) == "Alias civ");
        REQUIRE(f.values->tribes[0].plural.classic == "People"); // Decoded input, not live binding.
        REQUIRE(formats::decode_leader(formats::encode_leader(live)) == live);
        f.bytes = "@Alias civ\nPool selected\n@STOP\n";
        auto provider = city_name_provider(f.reader, f.directories, ascii_text_case());
        state::City city;
        city.owner = state::CivilizationSlot{1};
        auto civilizations = std::make_unique<std::array<state::Civilization, 8>>();
        REQUIRE(city::default_name(city, *civilizations, f.retained, provider));
        REQUIRE(std::string(city.name.text().begin(), city.name.text().end()) == "Pool selected");
        REQUIRE(live.city_name_index == 1);
        REQUIRE(live.times_used == 77);
    }
}
TEST_CASE("Process preference lookup resolves retained pool bindings instead of decoded names",
          "[classic][rules][text-pool]") {
    Fixture f("@AUTOBUILD\nNew binding\n");
    f.retained.names.improvements[1] = f.retained.text.store("Original binding");
    f.values->improvements[1].identity.name = {"Cached input", "Cached input"};
    f.retained.text.initialize();
    REQUIRE(f.retained.text.store("New binding") == 1);
    state::CityPreferences preferences;
    load_city_preferences(f.reader, f.retained, preferences, ascii_text_case());
    REQUIRE(preferences.count == 1);
    REQUIRE(preferences.build_order[0] == 1);
    REQUIRE(f.values->improvements[1].identity.name.classic == "Cached input");
}
TEST_CASE("Native and classic installation agree at allocation boundaries with live dependencies",
          "[classic][rules][rules-install]") {
    rule_fixture::Input input;
    input.rules["advances"][0]["prerequisites"][0] =
        rule_fixture::json(R"({"advance":"advances.authored.1"})");
    input.rules["advances"][1]["prerequisites"][0] = rule_fixture::json(R"({"special":"never"})");
    input.rules["improvements"][0]["prerequisite"] =
        rule_fixture::json(R"({"advance":"advances.authored.1"})");
    input.rules["tribes"][0]["governmentTitles"] = rule_fixture::json(
        R"([{"government":"governments.authored.1","male":"First","female":"FirstFemale"},
            {"government":"governments.authored.1","male":"Last","female":"LastFemale"},
            {"government":"governments.authored.6","male":"","female":""}])");
    const auto source = input.load();
    REQUIRE(source->advance_prerequisites[0][0] == static_cast<state::AdvanceReference>(1));
    REQUIRE(source->definitions.advances[0].prerequisites[0] == state::AdvanceReference::never);
    REQUIRE(source->improvement_prerequisites[0] == static_cast<state::AdvanceReference>(1));
    REQUIRE(source->definitions.improvements[0].prerequisite == state::AdvanceReference::never);
    auto sections = complete_sections();
    auto replace_once = [](std::string& bytes, std::string_view before, std::string_view after) {
        const auto at = bytes.find(before);
        REQUIRE(at != std::string::npos);
        bytes.replace(at, before.size(), after);
    };
    replace_once(sections[1], "advances 0,3,1,nil,nil", "advances 0,3,1,Alp,nil");
    replace_once(sections[1], "advances 1,3,1,nil,nil", "advances 1,3,1,no,nil");
    replace_once(sections[2], "improvements 0,10,1,nil", "improvements 0,10,1,Alp");
    replace_once(sections[7], "tribes 0,Example,0,0,0\n",
                 "tribes 0,Example,0,0,0,1,First,FirstFemale,1,Last,LastFemale,6,,\n");
    std::string bytes;
    for (const auto& section : sections)
        bytes += section;
    using Observation =
        std::tuple<std::string, std::size_t, std::size_t, state::AdvanceRule, std::uint16_t,
                   state::ImprovementRule, state::UnitRule, state::Leader, state::TitlePair, bool>;
    for (const bool replace : {false, true}) {
        for (const bool fail : {false, true}) {
            Fixture f(bytes);
            *f.values = source->definitions; // Same identity namespace and initial values.
            f.retained.double_movement_applied = true;
            f.retained.names.orders[0] = 923;
            f.retained.leaders[0].names = {-53, -54};
            f.retained.leaders[0].leader_name = -54;
            f.retained.leaders[0].times_used = 78;
            f.retained.leaders[0].city_name_index = 79;
            f.values->terrain[11].work = state::BaseTerrainWork{};
            f.values->terrain[11].work->irrigation.work_turns = 87;
            auto native = std::make_unique<state::RuleState>(f.retained);
            std::vector<Observation> classic_trace, native_trace;
            auto observe = [&](state::RuleState& live, std::vector<Observation>& trace) {
                return [&live, &trace, fail](std::string_view text, std::size_t minimum) {
                    auto& values = live.definitions;
                    trace.emplace_back(text, minimum, live.text.size(), values.advances[0],
                                       values.enabled_advance_count, values.improvements[0],
                                       values.units[0], live.leaders[0],
                                       values.governments[2].titles, live.double_movement_applied);
                    if (text == "improvements 0") {
                        values.advances[1].enabled = true;
                        values.advances[1].prerequisites.fill(state::AdvanceReference::none);
                    }
                    if (text == "units 0")
                        values.cosmic.values[0] = 7;
                    if (text == "First") {
                        values.governments[2].titles.male = {"Changed", "Changed"};
                        live.names.government_titles[2][0] = live.names.government_titles[0][0];
                    }
                    if (fail && text == "LastFemale")
                        throw std::bad_alloc();
                };
            };
            f.hooks.allocate = observe(f.retained, classic_trace);
            formats::content::RuleInstaller install(source, *native,
                                                    observe(*native, native_trace));
            if (fail) {
                REQUIRE_THROWS_AS(f.loader().load("RULES", replace), std::bad_alloc);
                REQUIRE_THROWS_AS(install.load(replace), std::bad_alloc);
            } else {
                f.loader().load("RULES", replace);
                install.load(replace);
            }
            REQUIRE(native_trace == classic_trace);
            REQUIRE(native->definitions == f.retained.definitions);
            REQUIRE(native->names == f.retained.names);
            REQUIRE(native->leaders == f.retained.leaders);
            REQUIRE(native->text.size() == f.retained.text.size());
            for (state::TextId id = 0; id < native->text.size(); ++id)
                REQUIRE(native->text.allocation(id) == f.retained.text.allocation(id));
            REQUIRE(native->definitions.units[0].movement == 14);
            REQUIRE(native->definitions.units[0].hit_points == 30);
            REQUIRE(native->definitions.improvements[0].prerequisite ==
                    static_cast<state::AdvanceReference>(1));
            REQUIRE(native->definitions.terrain[11].work->irrigation.work_turns == 87);
            REQUIRE(native->leaders[0].times_used == 78);
            REQUIRE(native->leaders[0].city_name_index == 79);
        }
    }
}
