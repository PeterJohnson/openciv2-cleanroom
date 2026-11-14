// F06 section 11 accepted-file dialog; S02 sections 2/11; U01 sections 3/6.
// Authored file/service inputs and specification expectations, not original
// captures. S02 parsing/execution and presentation are supplied child effects.
#include "classic_stream_fixture.hpp"
#include "session/classic_load_dialog.hpp"
#include "session/runtime.hpp"
#include "session/classic_preferences.hpp"
#include "formats/preference_io.hpp"
#include "formats/classic_save.hpp"
#include <catch2/catch_test_macros.hpp>
#include <algorithm>

using namespace openciv;
using namespace openciv::session;
namespace {
formats::StoredSave saved(bool scenario = false) {
    formats::StoredSave save;
    save.globals.civs_alive = 3;
    save.globals.player_civ = save.globals.saved_current_civ = save.globals.turn_civ = 1;
    save.globals.game_options = 0x8000;
    save.globals.game_flags = scenario ? 0x40 : 0;
    save.map.header = {4, 2, 4, 0, 19, 1, 1};
    for (auto& layer : save.map.remembered)
        layer = formats::Bytes(4, 0);
    save.map.tiles.resize(4);
    save.map.coarse = {formats::Bytes{0}, formats::Bytes{0}};
    save.leaders.resize(1);
    save.layout.rectangles[0] = {0, 0, 640, 480};
    if (scenario)
        save.scenario.emplace();
    return save;
}
struct Fixture {
    state::ProcessContext process;
    SessionRuntime runtime{process, base::RuntimeId{77}};
    ClassicFileState files;
    ClassicLoadDialogState dialog{"prior-path", 19};
    LoadDialogServices services;
    unit::StackBudget budget{100000, 64};
    formats::Bytes bytes = formats::encode_stored_save(saved());
    std::size_t position = 0, enumerations = 0, opens = 0, closes = 0;
    std::vector<std::string> trace, entries;
    std::shared_ptr<state::Event> parsed;
    bool resource_exists = true, parse_success = true, open_success = true;
    Fixture() {
        runtime.begin_client_session({10, 20});
        process.tokens = {100, 200};
        process.ordinary_preferences.game_options = 0x10018;
        process.rules->text.initialize();
        const auto label = state::NameReference{process.rules->text.store("Default"), false};
        for (auto& leader : process.rules->leaders) {
            leader.names.fill(label);
            leader.plural = leader.adjective = label;
            for (auto& titles : leader.titles)
                titles.fill(label);
        }
        files.text = {"C:\\caller", "C:\\home", "C:\\home", "TXT"};
        files.current_screen = {0, 0, 640, 480};
        services.close_map_window = [&](std::int32_t i) {
            trace.push_back("window:" + std::to_string(i));
        };
        services.enumerate = [&](std::string_view directory, std::string_view pattern) {
            ++enumerations;
            trace.push_back("enumerate:" + std::string(directory) + "\\" + std::string(pattern));
            return entries;
        };
        services.input.comparison = formats::classic::ascii_text_case();
        services.input.open = [&](std::string_view path) -> std::unique_ptr<CoreInput> {
            ++opens;
            trace.push_back("open:" + std::string(path));
            if (!open_success)
                return {};
            return test::input_file(
                {[&](std::size_t size) {
                     const auto length = std::min(size, bytes.size() - position);
                     const auto start = bytes.begin() + static_cast<std::ptrdiff_t>(position);
                     position += length;
                     return formats::ReadResult{
                         {start, start + static_cast<std::ptrdiff_t>(length)}, length == size};
                 },
                 [&]() -> std::optional<formats::Byte> {
                     if (position == bytes.size())
                         return {};
                     return bytes[position++];
                 },
                 [&] {
                     ++closes;
                     trace.push_back("close");
                     return true;
                 }});
        };
        services.input.flush = [&] { trace.push_back("flush"); };
        services.input.change_directory = [&](std::string_view path) {
            CHECK(files.text.working == path);
            trace.push_back("cd:" + std::string(path));
        };
        services.input.exists = [&](std::string_view directory, std::string_view name) {
            trace.push_back("probe:" + std::string(directory) + "\\" + std::string(name));
            return name.starts_with("EVENTS.") && resource_exists;
        };
        services.input.load_rules = [&](bool init) {
            CHECK(init);
            trace.push_back("rules");
        };
        services.input.reload_table = [&](LoadedRuleTable, bool init) {
            CHECK(init);
            trace.push_back("reload");
        };
        services.input.close_rules = [&] { trace.push_back("rules-close"); };
        services.parse_events = [&](std::string_view stem, std::string_view section) {
            CHECK(stem == "EVENTS");
            CHECK(section == "BEGINEVENTS");
            CHECK(runtime.world().events.collection.size() == 0);
            CHECK_FALSE(runtime.world().events.loaded);
            trace.push_back("parse");
            parsed = runtime.world().events.collection.append();
            REQUIRE(parsed);
            parsed->values.turn = 42;
            parsed->set_string(4, state::EventString{"Authored event"});
            return parse_success;
        };
        services.configure_sound = [&](bool enabled) {
            trace.push_back(enabled ? "sound:on" : "sound:off");
        };
        services.scenario_loaded = [&] { trace.push_back("scenario-loaded"); };
        services.synchronize_music = [&] { trace.push_back("music"); };
        services.present_title = [&] { trace.push_back("title"); };
        services.load_notice = [&](CoreLoadStatus status) {
            CHECK(dialog.loaded_file_kind == 19);
            trace.push_back("notice:" + std::to_string(static_cast<int>(status)));
        };
    }
    std::optional<CoreLoadStatus> load(std::optional<std::string> path = "C:\\home\\world.sav",
                                       bool title = true) {
        return load_classic_dialog(runtime, files, dialog, std::move(path), title, services, budget,
                                   "dialog/one");
    }
    void choice(std::optional<std::int32_t> code) {
        base::ControlledChoices choices;
        if (code)
            choices.records.push_back(base::make_choice(
                {"F06.scenario-alternative", "dialog/one", 0}, base::PrimaryCode{}, *code));
        base::GameplayRandom controlled(std::move(choices),
                                        runtime.gameplay_random().checkpoint().context);
        runtime.gameplay_random().restore(controlled.checkpoint());
    }
    std::vector<std::string> after_close() const {
        const auto found = std::find(trace.begin(), trace.end(), "close");
        REQUIRE(found != trace.end());
        return {found + 1, trace.end()};
    }
};
} // namespace

TEST_CASE("F06 cancelled load only restores the live home directory", "[session][load-dialog]") {
    Fixture f;
    f.services.close_map_window = {};
    f.services.input.open = {};
    f.choice({});
    const auto random = f.runtime.gameplay_random().checkpoint();
    f.files.text.home = "C:\\changed-home";
    CHECK_FALSE(f.load({}));
    CHECK(f.trace == std::vector<std::string>{"cd:C:\\changed-home"});
    CHECK(f.dialog.chosen_path == "prior-path");
    CHECK(f.dialog.loaded_file_kind == 19);
    CHECK(f.runtime.gameplay_random().checkpoint() == random);
    CHECK_FALSE(f.runtime.world().multiplayer.network_host);
}

TEST_CASE("F06 core failures notify by result and do not run the success tail",
          "[session][load-dialog]") {
    for (const auto expected : {CoreLoadStatus::input, CoreLoadStatus::unsupported,
                                CoreLoadStatus::data, CoreLoadStatus::newer}) {
        Fixture f;
        CAPTURE(expected);
        if (expected == CoreLoadStatus::input)
            f.open_success = false;
        else if (expected == CoreLoadStatus::unsupported)
            f.bytes[10] = 0x24;
        else if (expected == CoreLoadStatus::newer)
            f.bytes[10] = 0x2d;
        else
            f.bytes.resize(12);
        CHECK(f.load() == expected);
        CHECK(std::vector<std::string>(f.trace.begin(), f.trace.begin() + 8) ==
              std::vector<std::string>{"window:1", "window:2", "window:3", "window:4", "window:5",
                                       "window:6", "window:7", "open:C:\\home\\world.sav"});
        CHECK(f.trace.back() == "notice:" + std::to_string(static_cast<int>(expected)));
        CHECK(f.dialog.loaded_file_kind == 0);
        CHECK(f.dialog.chosen_path == "prior-path");
        CHECK_FALSE(f.runtime.world().multiplayer.network_host);
        CHECK(std::find(f.trace.begin(), f.trace.end(), "music") == f.trace.end());
        CHECK(f.closes == (expected == CoreLoadStatus::input ? 0 : 1));
    }
}

TEST_CASE("F06 ALT mapping uses scaled primary boundaries and service enumeration order",
          "[session][load-dialog][random]") {
    const std::array codes{0, 8191, 8192, 16383, 16384, 24575, 24576, 32767};
    const std::array selected{"chosen.bin", "chosen.bin", "z.ALT",      "z.ALT",
                              "a.ALT",      "a.ALT",      "middle.ALT", "middle.ALT"};
    for (std::size_t i = 0; i < codes.size(); ++i) {
        Fixture f;
        f.entries = {"z.ALT", "a.ALT", "middle.ALT"};
        f.open_success = false;
        f.choice(codes[i]);
        // Contains .SCN in the directory, not the selected filename extension.
        CHECK(f.load("C:\\home\\campaign.sCn.extra\\chosen.bin") == CoreLoadStatus::input);
        CHECK(f.files.opened_path == "C:\\home\\campaign.sCn.extra\\" + std::string(selected[i]));
        CHECK(f.enumerations == (i < 2 ? 1 : 2));
        CHECK_NOTHROW(f.runtime.gameplay_random().require_choices_consumed());
        CHECK(f.runtime.gameplay_random().checkpoint().trace->events.size() == 1);
        CHECK(f.dialog.chosen_path == "prior-path");
    }
}

TEST_CASE("F06 missing alternatives and ordinary choices consume no variant draw",
          "[session][load-dialog][random]") {
    for (const bool scenario : {false, true}) {
        Fixture f;
        f.open_success = false;
        f.choice({});
        const auto before = f.runtime.gameplay_random().checkpoint();
        CHECK(f.load(scenario ? "C:\\home\\choice.scn" : "C:\\home\\choice.sav") ==
              CoreLoadStatus::input);
        CHECK(f.enumerations == (scenario ? 1 : 0));
        CHECK(f.runtime.gameplay_random().checkpoint() == before);
    }
}

TEST_CASE("F06 inconsistent ALT enumeration stops after its consumed decision",
          "[session][load-dialog][random]") {
    Fixture f;
    f.choice(32767);
    f.services.enumerate = [&](std::string_view, std::string_view) {
        return ++f.enumerations == 1 ? std::vector<std::string>{"a.ALT", "b.ALT"}
                                     : std::vector<std::string>{"b.ALT", "a.ALT"};
    };
    CHECK_THROWS_AS(f.load("C:\\home\\choice.scn"), std::invalid_argument);
    CHECK(f.enumerations == 2);
    CHECK(f.opens == 0);
    CHECK(f.dialog.chosen_path == "prior-path");
    CHECK(f.dialog.loaded_file_kind == 19);
    CHECK_NOTHROW(f.runtime.gameplay_random().require_choices_consumed());
}

TEST_CASE("F06 ALT backing errors stop without fabricating a load result",
          "[session][load-dialog]") {
    SECTION("Selected name cannot escape the enumerated directory") {
        Fixture f;
        f.entries = {"..\\escape.ALT"};
        f.choice(32767);
        CHECK_THROWS_AS(f.load("C:\\home\\choice.scn"), std::invalid_argument);
        CHECK(f.opens == 0);
        CHECK_NOTHROW(f.runtime.gameplay_random().require_choices_consumed());
    }
    SECTION("Oversized enumeration stops before sampling") {
        Fixture f;
        f.entries.resize(65536, "entry.ALT");
        f.choice({});
        const auto before = f.runtime.gameplay_random().checkpoint();
        CHECK_THROWS_AS(f.load("C:\\home\\choice.scn"), std::length_error);
        CHECK(f.opens == 0);
        CHECK(f.runtime.gameplay_random().checkpoint() == before);
    }
}

TEST_CASE("F06 ordinary load replaces options and clears an unloaded retained event list",
          "[session][load-dialog]") {
    Fixture f;
    auto& world = f.runtime.world();
    world.events.loaded = true;
    REQUIRE(world.events.collection.append());
    f.services.parse_events = {};
    f.services.scenario_loaded = {};
    f.services.configure_sound = [&](bool enabled) {
        CHECK(enabled);
        CHECK(world.globals.game_options == 0x10018);
        CHECK_FALSE(world.events.loaded);
        CHECK(world.events.collection.size() == 0);
        CHECK_FALSE(world.multiplayer.network_host);
        f.trace.push_back("sound:on");
    };
    f.services.present_title = [&] {
        CHECK_FALSE(world.multiplayer.network_host);
        CHECK(f.dialog.chosen_path == "prior-path");
        f.trace.push_back("title");
    };
    CHECK(f.load() == CoreLoadStatus::success);
    CHECK(f.after_close() ==
          std::vector<std::string>{"flush", "sound:on", "music", "title", "cd:C:\\home"});
    CHECK(world.multiplayer.network_host);
    CHECK(f.dialog.chosen_path == "C:\\home\\world.sav");
    CHECK(f.dialog.loaded_file_kind == 19);
    CHECK(f.position == f.bytes.size());
}

TEST_CASE("F06 scenario replacement resets parses compacts and publishes before its hook",
          "[session][load-dialog][events]") {
    Fixture f;
    f.bytes = formats::encode_stored_save(saved(true));
    auto& events = f.runtime.world().events;
    const auto old = events.collection.append();
    REQUIRE(old);
    f.files.text.extension = "GER";
    std::size_t allocations = 0;
    f.services.input.events.allocate = [&](state::EventAllocation) {
        CHECK_FALSE(events.loaded);
        ++allocations;
        f.trace.push_back("allocate");
        return true;
    };
    f.services.scenario_loaded = [&] {
        CHECK(events.loaded);
        REQUIRE(events.collection.size() == 1);
        CHECK_FALSE(events.collection.contains(old));
        CHECK_FALSE(events.collection.contains(f.parsed));
        CHECK(events.collection.at(0)->identity == f.parsed->identity);
        CHECK(events.collection.at(0)->strings[4] != f.parsed->strings[4]);
        CHECK(events.collection.at(0)->strings[4].value() == "Authored event");
        CHECK(events.collection.at(0)->values.turn == 42);
        CHECK(f.runtime.world().globals.game_options == 0x10018);
        f.trace.push_back("scenario-loaded");
    };
    CHECK(f.load() == CoreLoadStatus::success);
    CHECK(f.after_close() == std::vector<std::string>{"flush", "probe:C:\\home\\EVENTS.GER",
                                                      "parse", "allocate", "allocate", "allocate",
                                                      "allocate", "sound:on", "scenario-loaded",
                                                      "music", "title", "cd:C:\\home"});
    CHECK(allocations == 4);
}

TEST_CASE("F06 unavailable or failed replacement events leave no stale list",
          "[session][load-dialog][events]") {
    for (const bool exists : {false, true}) {
        Fixture f;
        f.bytes = formats::encode_stored_save(saved(true));
        auto& events = f.runtime.world().events;
        REQUIRE(events.collection.append());
        events.loaded = true;
        f.resource_exists = exists;
        f.parse_success = false;
        f.services.scenario_loaded = {};
        CHECK(f.load() == CoreLoadStatus::success);
        CHECK_FALSE(events.loaded);
        CHECK(events.collection.size() == 0);
        CHECK(static_cast<bool>(f.parsed) == exists);
        CHECK(f.runtime.world().multiplayer.network_host);
    }
}

TEST_CASE("F06 persisted events survive the wrapper without replacement or a scenario gate",
          "[session][load-dialog][events]") {
    for (const bool scenario : {false, true}) {
        Fixture f;
        auto save = saved(scenario);
        save.events.emplace();
        formats::StoredEvent event;
        event.record.actions = 0x80000021U;
        event.record.who_civ = 6;
        event.record.whom_civ = 2;
        save.events->entries.push_back(event);
        f.bytes = formats::encode_stored_save(save);
        f.services.parse_events = {};
        CHECK(f.load("C:\\home\\continued.sav", false) == CoreLoadStatus::success);
        CHECK(f.after_close() == std::vector<std::string>{"flush", "sound:on", "scenario-loaded",
                                                          "music", "cd:C:\\home"});
        REQUIRE(f.runtime.world().events.collection.size() == 1);
        CHECK(f.runtime.world().events.collection.at(0)->values == event.record);
    }
}

TEST_CASE("F06 sound and event callbacks precede live music gates and home restoration",
          "[session][load-dialog][events]") {
    for (const bool enable : {false, true}) {
        Fixture f;
        f.bytes = formats::encode_stored_save(saved(true));
        f.process.ordinary_preferences.game_options = enable ? 0U : 8U;
        f.services.configure_sound = [&](bool enabled) {
            CHECK_FALSE(enabled);
            f.runtime.world().globals.game_options = enable ? 8U : 0U;
        };
        f.services.scenario_loaded = [&] {
            CHECK(enable);
            f.trace.push_back("scenario-loaded");
            f.runtime.world().globals.game_options = 0;
            f.runtime.world().events.loaded = false;
        };
        f.services.synchronize_music = [&] {
            CHECK(f.runtime.world().globals.game_options == 0);
            f.trace.push_back("music");
        };
        f.services.present_title = [&] { f.files.text.home = "C:\\new-home"; };
        CHECK(f.load() == CoreLoadStatus::success);
        CHECK(std::count(f.trace.begin(), f.trace.end(), "scenario-loaded") == (enable ? 1 : 0));
        CHECK(f.trace.back() == "cd:C:\\new-home");
        CHECK(f.runtime.world().events.collection.size() == 1); // No reset after event execution.
    }
}

TEST_CASE("F06 compaction resource failures retain the reached copy prefix and stop loading",
          "[session][load-dialog][events]") {
    for (const std::size_t fail : {1U, 3U, 4U}) {
        Fixture f;
        f.bytes = formats::encode_stored_save(saved(true));
        auto& events = f.runtime.world().events;
        std::size_t allocations = 0;
        f.services.input.events.allocate = [&](state::EventAllocation) {
            return ++allocations != fail;
        };
        CHECK_THROWS_AS(f.load(), std::bad_alloc);
        CHECK_FALSE(events.loaded);
        CHECK(events.collection.size() == (fail == 3 ? 0 : 1));
        CHECK(events.collection.contains(f.parsed) == (fail == 1));
        CHECK(f.runtime.world().globals.game_options == 0); // Core cleared saved cheat bit.
        CHECK_FALSE(f.runtime.world().multiplayer.network_host);
        CHECK(f.dialog.chosen_path == "prior-path");
        CHECK(f.dialog.loaded_file_kind == 19);
        CHECK(f.closes == 1);
        CHECK(std::find(f.trace.begin(), f.trace.end(), "music") == f.trace.end());
    }
}

TEST_CASE("F06 load wrapper runs real cleanup after presentation and publishes deletion to clients",
          "[session][load-dialog][unit]") {
    Fixture f;
    auto save = saved();
    save.globals.unit_end = 3;
    save.units.resize(3);
    save.units[0].previous.value = save.units[0].next.value = 99; // Free stale links.
    save.units[1].token = state::UnitToken{91};
    save.units[1].owner = state::CivilizationSlot{1};
    save.units[1].type = state::UnitType{2};
    save.units[1].x = -1;
    save.units[1].y = 0;
    save.units[2].token = state::UnitToken{92};
    save.units[2].owner = state::CivilizationSlot{1};
    save.units[2].type = state::UnitType{3};
    save.units[2].orders = state::UnitOrder::sleeping;
    save.units[2].destination_x = 1;
    f.bytes = formats::encode_stored_save(save);
    auto& world = f.runtime.world();
    f.process.rules->definitions.units[2].role = state::RuleRole::settle;
    world.unit_totals[1] = 2;
    const auto client = f.runtime.bind(f.runtime.add_participant(state::CivilizationSlot{1}),
                                       ControllerKind::frontend);
    f.services.present_title = [&] {
        CHECK_FALSE(world.multiplayer.network_host);
        CHECK(client.own_units().records.size() == 2);
        CHECK(world.entities.units.storage()[0].next.value == 99);
        f.trace.push_back("title");
    };
    f.services.deletion.check_destroyed = [&](std::int32_t owner, std::int32_t killer) {
        CHECK(owner == 1);
        CHECK(killer == -1);
        CHECK(world.multiplayer.network_host);
        CHECK(world.entities.units.storage()[0].next.value == -1);
        CHECK(world.entities.units.storage()[0].previous.value == -1);
        CHECK(world.civilizations[1].units_active[2] == 0);
        CHECK(world.unit_totals[1] == 1);
        REQUIRE(client.own_units().records.size() == 1);
        CHECK(client.own_units().records[0].slot == state::UnitSlot{2});
        CHECK(client.own_units().records[0].orders == state::UnitOrder::none);
        CHECK(f.files.text.working == "C:\\home");
        CHECK(f.dialog.chosen_path == "prior-path");
        f.trace.push_back("destroyed");
    };
    CHECK(f.load() == CoreLoadStatus::success);
    CHECK(f.after_close() == std::vector<std::string>{"flush", "sound:on", "music", "title",
                                                      "cd:C:\\home", "destroyed"});
    CHECK_FALSE(state::occupied(world.entities.units.storage()[1].token));
    CHECK(f.dialog.chosen_path == "C:\\home\\world.sav");
    CHECK(client.own_units().records.size() == 1);
}

TEST_CASE("F06 missing owning child stops at its invocation with earlier effects retained",
          "[session][load-dialog]") {
    Fixture f;
    f.services.synchronize_music = {};
    CHECK_THROWS_AS(f.load(), std::bad_function_call);
    CHECK(f.closes == 1);
    CHECK(f.after_close() == std::vector<std::string>{"flush", "sound:on"});
    CHECK(f.runtime.world().globals.game_options == f.process.ordinary_preferences.game_options);
    CHECK_FALSE(f.runtime.world().multiplayer.network_host);
    CHECK(f.dialog.chosen_path == "prior-path");
    CHECK(f.dialog.loaded_file_kind == 19);
}

TEST_CASE("F06 completed load retains the selected alternative without drawing again",
          "[session][load-dialog][random]") {
    Fixture f;
    auto save = saved();
    save.globals.multiplayer = 3;
    save.globals.human_players = 2;
    save.multiplayer.emplace();
    f.bytes = formats::encode_stored_save(save);
    f.runtime.world().globals.multiplayer = 3;
    f.entries = {"last.ALT", "first.ALT"};
    f.choice(32767);
    CHECK(f.load("C:\\home\\campaign\\start.scn") == CoreLoadStatus::success);
    CHECK(f.dialog.chosen_path == "C:\\home\\campaign\\first.ALT");
    CHECK(f.files.opened_path == f.dialog.chosen_path);
    CHECK(f.files.loaded_filename == "first.ALT");
    CHECK(f.files.scenario_directory == "C:\\home\\campaign");
    CHECK(f.enumerations == 2);
    CHECK(f.opens == 1);
    CHECK(f.closes == 1);
    CHECK_NOTHROW(f.runtime.gameplay_random().require_choices_consumed());
    CHECK(f.runtime.gameplay_random().checkpoint().trace->events.size() == 1);
}

TEST_CASE("F06 post-core flush changes are consumed by event and preference operations",
          "[session][load-dialog][events]") {
    Fixture f;
    f.services.input.flush = [&] {
        if (f.closes == 1) {
            f.runtime.world().globals.game_flags |= 0x80U;
            f.process.ordinary_preferences.game_options = 8;
            f.files.text.extension = "FRE";
        }
        f.trace.push_back("flush");
    };
    CHECK(f.load() == CoreLoadStatus::success);
    CHECK(f.after_close() == std::vector<std::string>{"flush", "probe:C:\\home\\EVENTS.FRE",
                                                      "parse", "sound:off", "scenario-loaded",
                                                      "music", "title", "cd:C:\\home"});
    CHECK(f.runtime.world().globals.game_options == 8);
    CHECK(f.runtime.world().events.loaded);
}

TEST_CASE("F06 cleanup stop retains presentation and host effects before path publication",
          "[session][load-dialog][unit]") {
    Fixture f;
    auto save = saved();
    save.globals.unit_end = 1;
    save.units.resize(1);
    save.units[0].token = state::UnitToken{19};
    save.units[0].owner = state::CivilizationSlot{1};
    f.bytes = formats::encode_stored_save(save);
    f.budget.steps = 0;
    CHECK_THROWS_AS(f.load(), unit::StackResourceStop);
    CHECK(f.after_close() ==
          std::vector<std::string>{"flush", "sound:on", "music", "title", "cd:C:\\home"});
    CHECK(f.runtime.world().multiplayer.network_host);
    CHECK(f.runtime.world().entities.units.storage()[0].token == state::UnitToken{100});
    CHECK(f.dialog.chosen_path == "prior-path");
    CHECK(f.dialog.loaded_file_kind == 19);
}

TEST_CASE("F06 failed load notice exception retains the uncleared file classification",
          "[session][load-dialog]") {
    Fixture f;
    f.open_success = false;
    f.services.load_notice = [&](CoreLoadStatus status) {
        CHECK(status == CoreLoadStatus::input);
        throw std::runtime_error("notice service stop");
    };
    CHECK_THROWS_AS(f.load(), std::runtime_error);
    CHECK(f.dialog.loaded_file_kind == 19);
    CHECK(f.dialog.chosen_path == "prior-path");
    CHECK(f.opens == 1);
}

TEST_CASE("F03 loaded preference word reaches F06 accepted-file option replacement",
          "[session][load-dialog][preferences]") {
    Fixture f;
    state::OrdinaryPreferences remembered;
    remembered.game_options = 0x8120; // Includes saved cheat and grid; sound/music off.
    auto bytes = formats::encode_ordinary_preferences(remembered);
    const auto multiplayer = formats::encode_multiplayer_preferences({});
    bytes.insert(bytes.end(), multiplayer.begin(), multiplayer.end());
    formats::MemoryItems input(bytes);
    PreferenceFileServices services;
    services.open_input = [&](std::string_view path) {
        CHECK(path == "CIV2.DAT");
        return test::input_file(
            {[&](std::size_t size) { return input.read(size); }, {}, [] { return true; }});
    };
    load_classic_preferences(f.process, {1000, true}, services);
    CHECK(f.load() == CoreLoadStatus::success);
    CHECK(f.runtime.world().globals.game_options == 0x8120);
    CHECK(f.after_close() ==
          std::vector<std::string>{"flush", "sound:off", "music", "title", "cd:C:\\home"});
}
