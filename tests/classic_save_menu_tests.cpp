// Independently authored F06 section 11/Appendix A and P04 section 2 cases.
#include "session/classic_save_menu.hpp"
#include "session/runtime.hpp"
#include "formats/classic_save.hpp"
#include <catch2/catch_test_macros.hpp>

using namespace openciv;
using namespace openciv::session;
namespace {
std::string filter_entry(std::string_view description, std::string_view suffix) {
    std::string result = std::string(description) + " (*." + std::string(suffix) + ")";
    result.push_back('\0');
    result += "*." + std::string(suffix);
    result.push_back('\0');
    return result;
}
std::string label(std::size_t index) {
    return "Label" + std::to_string(index);
}
std::string lower(std::string_view source) {
    std::string result(source);
    for (char& c : result)
        if (c >= 'A' && c <= 'Z')
            c = static_cast<char>(c + 32);
    return result;
}
struct Fixture {
    state::ProcessContext process;
    SessionRuntime runtime{process, base::RuntimeId{76}};
    ClassicFileState files;
    ClassicSaveMenuState menu;
    SaveMenuServices services;
    std::vector<std::string> calls, offered, opened, notices;
    std::vector<FileChoiceRequest> requests;
    formats::Bytes bytes;
    std::string name = "Henry VIII", error = "before write";
    std::function<std::uint32_t(std::string&)> choose;
    std::function<void()> after_close;
    unsigned times = 0, closes = 0;
    bool fail_item = false;
    Fixture() {
        runtime.begin_client_session({10, 20});
        runtime.world().map.allocate(world::Geometry{4, 2});
        runtime.world().map.coarse_grid(0)[0] = 0;
        runtime.world().map.coarse_grid(1)[0] = 0;
        runtime.world().multiplayer.local_human = 6;
        runtime.world().globals.year = -3980;
        process.rules->text.initialize();
        for (std::size_t i = 0; i < 6; ++i)
            process.rules->names.difficulties[i] =
                process.rules->text.store("Difficulty" + std::to_string(i));
        const auto text_id = state::NameReference{process.rules->text.store("Default"), false};
        for (auto& leader : process.rules->leaders)
            leader.adjective = leader.plural = leader.leader_name = text_id;
        files.text = {"old-working", "search", "home"};
        menu.session_active = true;
        menu.main_window = 9;
        process.text_parameters.line = "prior line";
        process.text_parameters.strings.fill("retained");
        services.stop_countdown = [&](bool notify) {
            REQUIRE(notify);
            calls.push_back("stop");
        };
        services.start_countdown = [&](bool notify) {
            REQUIRE(notify);
            calls.push_back("start");
        };
        services.local_time = [&] {
            ++times;
            calls.push_back("time");
            return LocalCalendar{11, 14, 2026, 1, 2, 3};
        };
        services.lowercase = [&](std::string_view value) {
            calls.push_back("lower");
            return lower(value);
        };
        services.change_directory = [&](std::string_view directory) {
            calls.push_back("cd:" + std::string(directory));
        };
        services.label = [&](std::size_t index) {
            calls.push_back("label:" + std::to_string(index));
            return label(index);
        };
        services.ruler_name = [&](std::int32_t civ) {
            calls.push_back("name:" + std::to_string(civ));
            return name;
        };
        services.ruler_title = [&](std::int32_t civ) {
            calls.push_back("title:" + std::to_string(civ));
            return "Emperor";
        };
        services.civilization_plural = [&](std::int32_t civ) {
            calls.push_back("plural:" + std::to_string(civ));
            return "English";
        };
        services.format_year = [&](std::int32_t year) {
            calls.push_back("year:" + std::to_string(year));
            return "Formatted year";
        };
        services.error_text = [&] {
            calls.push_back("error-text");
            return error;
        };
        services.notice = [&](std::string_view notice) {
            calls.push_back(std::string(notice));
            notices.emplace_back(notice);
        };
        services.chooser.choose = [&](const FileChoiceRequest& request, std::string& filename) {
            calls.push_back("choose");
            requests.push_back(request);
            offered.push_back(filename);
            return choose ? choose(filename) : 1U;
        };
        services.chooser.extended_error = [&] {
            calls.push_back("extended-error");
            return 99U;
        };
        services.output.open = [&](std::string_view path) {
            calls.push_back("open");
            opened.emplace_back(path);
            return std::make_unique<CoreOutput>(
                CoreOutput{[&](std::span<const formats::Byte> item) {
                               calls.push_back("item");
                               if (fail_item)
                                   return false;
                               bytes.insert(bytes.end(), item.begin(), item.end());
                               return true;
                           },
                           [&](std::span<const formats::Byte> value) {
                               bytes.insert(bytes.end(), value.begin(), value.end());
                               return true;
                           },
                           [&](formats::Byte value) {
                               bytes.push_back(value);
                               return true;
                           },
                           [&] {
                               ++closes;
                               calls.push_back("close");
                               if (after_close)
                                   after_close();
                               return false;
                           }});
        };
    }
    SaveMenuResult save(bool scenario = false) {
        return save_classic_menu(runtime, files, menu, scenario, services);
    }
};
} // namespace

TEST_CASE("F06 filters preserve mode order scenario precedence and omitted email alternatives",
          "[session][file-choice]") {
    state::GlobalValues globals;
    std::string filter = "stale";
    for (std::uint8_t mode = 0; mode <= 6; ++mode) {
        globals.multiplayer = mode;
        const auto first = mode == 0   ? filter_entry("Label238", "sav")
                           : mode == 1 ? filter_entry("Label855", "hot")
                           : mode == 2 ? filter_entry("Label858", "eml")
                                       : filter_entry("Label861", "net");
        REQUIRE(build_classic_filter(filter, globals, false, false, true, label) == first.size());
        REQUIRE(filter == first + '\0');
        auto expected = first;
        if (mode != 0)
            expected += filter_entry("Label238", "sav");
        if (mode < 3)
            expected += filter_entry("Label861", "net");
        if (mode != 1)
            expected += filter_entry("Label855", "hot");
        REQUIRE(build_classic_filter(filter, globals, false, false, false, label) ==
                expected.size());
        REQUIRE(filter == expected + '\0');
    }
    globals.multiplayer = 255;
    REQUIRE(build_classic_filter(filter, globals, true, true, false, label));
    REQUIRE(filter == filter_entry("Label415", "scn") + '\0');
    REQUIRE(build_classic_filter(filter, globals, false, true, false, label));
    REQUIRE(filter == filter_entry("Label261", "mp") + '\0');
    REQUIRE_FALSE(build_classic_filter(filter, globals, false, false, true, label));
    REQUIRE(filter == std::string(1, '\0'));
    REQUIRE_THROWS_AS(build_classic_filter(filter, globals, false, false, false, label),
                      std::out_of_range);
    REQUIRE(filter == std::string(1, '\0'));
}
TEST_CASE("F06 filter alternatives reread live mode after label callbacks",
          "[session][file-choice]") {
    state::GlobalValues globals;
    std::string filter;
    std::vector<std::size_t> indexes;
    const FileLabel labels = [&](std::size_t index) {
        indexes.push_back(index);
        globals.multiplayer = indexes.size() == 1 ? 2 : 3;
        return label(index);
    };
    REQUIRE(build_classic_filter(filter, globals, false, false, false, labels));
    REQUIRE(indexes == std::vector<std::size_t>{238, 238, 855});
    REQUIRE(filter == filter_entry("Label238", "sav") + filter_entry("Label238", "sav") +
                          filter_entry("Label855", "hot") + '\0');
}
TEST_CASE("F06 native chooser accepts only low byte one and retains cancellation writes",
          "[session][file-choice]") {
    for (const auto value : {0U, 1U, 2U, 0x100U, 0x101U, 0xffffffffU}) {
        unsigned errors = 0;
        FileChoiceRequest request;
        request.save = (value & 1) != 0;
        request.require_existing = !request.save;
        request.owner = value == 0 ? std::nullopt : std::optional<std::uint64_t>{33};
        request.title = "Title";
        request.filter = filter_entry("Files", "sav") + '\0';
        request.default_extension = "SAV";
        request.flags = 99;
        request.filename_capacity = 99;
        request.filter_index = 99;
        std::string filename = "before";
        FileChoiceServices services{[&](const FileChoiceRequest& actual, std::string& name) {
                                        REQUIRE(actual.save == request.save);
                                        REQUIRE(actual.owner == request.owner);
                                        REQUIRE(actual.title == "Title");
                                        REQUIRE(actual.filter == request.filter);
                                        REQUIRE(actual.default_extension == "SAV");
                                        REQUIRE(actual.filter_index == 1);
                                        REQUIRE(actual.filename_capacity == 256);
                                        REQUIRE(actual.flags ==
                                                (request.require_existing ? 0x180eU : 0x80eU));
                                        name = std::string("written\0tail", 12);
                                        return value;
                                    },
                                    [&] {
                                        ++errors;
                                        REQUIRE(filename == "written");
                                        return 17U;
                                    }};
        const bool accepted = (value & 255U) == 1;
        REQUIRE(choose_classic_file(request, filename, services) == accepted);
        REQUIRE(filename == "written");
        REQUIRE(errors == (accepted ? 0U : 1U));
    }
}
TEST_CASE("F06 inactive save menu has no timer path text or binding effects",
          "[session][save-menu]") {
    Fixture f;
    f.menu.session_active = false;
    f.menu.filename = "keep";
    f.files.extension = ".OLD";
    REQUIRE(save_classic_menu(f.runtime, f.files, f.menu, false, {}) == SaveMenuResult::inactive);
    REQUIRE(f.calls.empty());
    REQUIRE(f.menu.filename == "keep");
    REQUIRE(f.files.extension == ".OLD");
    REQUIRE(f.process.text_parameters.line == "prior line");
    REQUIRE(f.files.text.working == "old-working");
}
TEST_CASE("F06 save menu composes real core output and ordered success parameters",
          "[session][save-menu]") {
    Fixture f;
    const auto gameplay = f.runtime.gameplay_random().checkpoint();
    const auto presentation = f.runtime.presentation_random().checkpoint();
    REQUIRE(f.save() == SaveMenuResult::saved);
    REQUIRE(f.offered == std::vector<std::string>{"he_b3980.sav"});
    REQUIRE(f.opened == f.offered);
    REQUIRE(f.closes == 1);
    REQUIRE(f.menu.private_filter == filter_entry("Label238", "SAV") + '\0');
    REQUIRE(f.menu.shared_filter == filter_entry("Label238", "sav") + '\0');
    REQUIRE(f.requests[0].title == "Label239");
    REQUIRE(f.requests[0].default_extension == "SAV");
    REQUIRE(f.requests[0].owner == 9);
    REQUIRE_FALSE(f.requests[0].require_existing);
    REQUIRE(f.times == 0);
    REQUIRE(f.files.text.working == "home");
    const auto decoded = formats::decode_stored_save(f.bytes);
    REQUIRE(decoded.globals.year == -3980);
    REQUIRE(decoded.globals.saved_current_civ == 6);
    const auto& strings = f.process.text_parameters.strings;
    REQUIRE(strings[0] == "Emperor");
    REQUIRE(strings[1] == "Henry VIII");
    REQUIRE(strings[2] == "English");
    REQUIRE(strings[3] == "Formatted year");
    REQUIRE(strings[4] == "Difficulty0");
    REQUIRE(strings[5] == "retained");
    REQUIRE(f.calls.front() == "stop");
    REQUIRE(f.calls.back() == "start");
    const auto chosen = std::find(f.calls.begin(), f.calls.end(), "choose");
    REQUIRE(*(chosen + 1) == "cd:home");
    REQUIRE(*(chosen + 2) == "open");
    const auto closed = std::find(f.calls.begin(), f.calls.end(), "close");
    REQUIRE(std::vector<std::string>(closed, f.calls.end()) ==
            std::vector<std::string>{"close", "title:6", "name:6", "plural:6", "year:-3980",
                                     "SAVEOK", "start"});
    REQUIRE(f.runtime.gameplay_random().checkpoint() == gameplay);
    REQUIRE(f.runtime.presentation_random().checkpoint() == presentation);
}
TEST_CASE(
    "F06 save filename preserves short high and control bytes while replacing ASCII punctuation",
    "[session][save-menu]") {
    const std::vector<std::pair<std::string, std::string>> names{
        {"", "_a0.sav"},
        {"A", "a_a0.sav"},
        {"H@long", "h__a0.sav"},
        {std::string("\xc9\x01Z", 3), std::string("\xc9\x01_a0.sav", 9)}};
    for (const auto& [name, expected] : names) {
        Fixture f;
        f.name = name;
        f.runtime.world().globals.year = 0;
        f.choose = [](std::string&) { return 0U; };
        REQUIRE(f.save() == SaveMenuResult::cancelled);
        REQUIRE(f.offered[0] == expected);
        REQUIRE(f.opened.empty());
        REQUIRE(f.calls.back() == "start");
    }
    Fixture minimum;
    minimum.runtime.world().globals.year = -32768;
    minimum.choose = [](std::string&) { return 0U; };
    REQUIRE(minimum.save() == SaveMenuResult::cancelled);
    REQUIRE(minimum.offered[0] == "he_b32768.sav");
}
TEST_CASE("F06 scenario month filenames use live year and zero month branch",
          "[session][save-menu]") {
    for (const auto& [year, expected] :
         std::vector<std::pair<std::int16_t, std::string>>{{-13, "1label430.scn"},
                                                           {-12, "1label431.scn"},
                                                           {-1, "0label430.scn"},
                                                           {0, "0label420.scn"},
                                                           {13, "1label421.scn"},
                                                           {-32768, "2730label423.scn"}}) {
        Fixture f;
        f.runtime.world().globals.year = year;
        f.runtime.world().scenario.year_increment = -1;
        const std::string scenario_name = "My scenario";
        std::copy(scenario_name.begin(), scenario_name.end(),
                  f.runtime.world().scenario.name.bytes.begin());
        REQUIRE(f.save(true) == SaveMenuResult::saved);
        REQUIRE(f.offered[0] == expected);
        REQUIRE(f.requests[0].title == "Label416");
        REQUIRE(f.requests[0].filter == filter_entry("Label415", "scn") + '\0');
        REQUIRE(f.notices == std::vector<std::string>{"SCENOK"});
        REQUIRE(f.process.text_parameters.strings[0] == scenario_name);
        REQUIRE(f.process.text_parameters.strings[1] == "retained");
    }
    Fixture live;
    live.runtime.world().globals.game_flags = 0x80;
    live.runtime.world().scenario.year_increment = -1;
    unsigned lower_calls = 0;
    live.services.lowercase = [&](std::string_view name) {
        if (++lower_calls == 1)
            live.runtime.world().globals.year = 25;
        return lower(name);
    };
    live.services.label = [&](std::size_t index) {
        if (index == 421)
            live.files.extension = ".NEXT";
        return label(index);
    };
    live.choose = [](std::string&) { return 0U; };
    REQUIRE(live.save() == SaveMenuResult::cancelled);
    REQUIRE(live.offered[0] == "2label421.next");
    REQUIRE(lower_calls == 2);
}
TEST_CASE("F06 mode two cancellation rebuilds filename filter and calendar before retry",
          "[session][save-menu]") {
    Fixture f;
    f.runtime.world().globals.multiplayer = 2;
    f.choose = [&](std::string& name) {
        if (f.offered.size() == 1) {
            name = "cancelled edit";
            f.name = "Alex";
            f.runtime.world().globals.year = 13;
            return 0U;
        }
        name = "mail.eml";
        return 1U;
    };
    REQUIRE(f.save() == SaveMenuResult::saved);
    REQUIRE(f.offered == std::vector<std::string>{"he_b3980 11-14-2026 1-2-3.eml",
                                                  "al_a13 11-14-2026 1-2-3.eml"});
    REQUIRE(f.times == 2);
    REQUIRE(f.opened == std::vector<std::string>{"mail.eml"});
    REQUIRE(f.notices == std::vector<std::string>{"EMAILSAVED"});
    REQUIRE(f.process.text_parameters.strings[0] == "mail.eml");
    REQUIRE(std::count(f.calls.begin(), f.calls.end(), "stop") == 1);
    REQUIRE(std::count(f.calls.begin(), f.calls.end(), "start") == 1);
    REQUIRE(std::count(f.calls.begin(), f.calls.end(), "cd:home") == 2);
}
TEST_CASE("F06 cancellation uses post chooser home and live mode including scenario retries",
          "[session][save-menu]") {
    Fixture f;
    f.runtime.world().globals.multiplayer = 2;
    f.choose = [&](std::string& name) {
        name = "cancelled";
        f.files.text.home = "new-home";
        if (f.offered.size() == 2)
            f.runtime.world().globals.multiplayer = 0;
        return 0U;
    };
    REQUIRE(f.save(true) == SaveMenuResult::cancelled);
    REQUIRE(f.offered.size() == 2);
    REQUIRE(f.times == 0);
    REQUIRE(f.opened.empty());
    REQUIRE(f.files.text.working == "new-home");
    REQUIRE(f.menu.filename == "cancelled");
    REQUIRE(f.calls.back() == "start");
    REQUIRE(std::count(f.calls.begin(), f.calls.end(), "cd:new-home") == 2);
}
TEST_CASE("F06 accepted write failure reports post close error and starts timer without retry",
          "[session][save-menu]") {
    Fixture f;
    f.runtime.world().globals.multiplayer = 2;
    f.fail_item = true;
    f.after_close = [&] { f.error = "post-close error"; };
    REQUIRE(f.save() == SaveMenuResult::failed);
    REQUIRE(f.offered.size() == 1);
    REQUIRE(f.closes == 1);
    REQUIRE(f.bytes == formats::Bytes{'C', 'I', 'V', 'I', 'L', 'I', 'Z', 'E', 0, 0x1a});
    REQUIRE(f.process.text_parameters.strings[0] == "post-close error");
    REQUIRE(f.process.text_parameters.strings[1] == "retained");
    const auto closed = std::find(f.calls.begin(), f.calls.end(), "close");
    REQUIRE(std::vector<std::string>(closed, f.calls.end()) ==
            std::vector<std::string>{"close", "error-text", "SAVEERROR", "start"});
}
TEST_CASE("F06 ordinary save notice rereads local civ year and difficulty at each formatting step",
          "[session][save-menu]") {
    Fixture f;
    f.after_close = [&] { f.runtime.world().multiplayer.local_human = 2; };
    f.services.ruler_title = [&](std::int32_t civ) {
        REQUIRE(civ == 2);
        f.runtime.world().multiplayer.local_human = 3;
        return "Title2";
    };
    f.services.ruler_name = [&](std::int32_t civ) {
        if (f.closes == 0)
            return std::string("Henry");
        REQUIRE(civ == 3);
        f.runtime.world().multiplayer.local_human = 4;
        return std::string("Name3");
    };
    f.services.civilization_plural = [&](std::int32_t civ) {
        REQUIRE(civ == 4);
        f.runtime.world().globals.year = 123;
        return "Plural4";
    };
    f.services.format_year = [&](std::int32_t year) {
        REQUIRE(year == 123);
        f.runtime.world().globals.difficulty = 5;
        return "Year123";
    };
    REQUIRE(f.save() == SaveMenuResult::saved);
    const auto& p = f.process.text_parameters.strings;
    REQUIRE(p[0] == "Title2");
    REQUIRE(p[1] == "Name3");
    REQUIRE(p[2] == "Plural4");
    REQUIRE(p[3] == "Year123");
    REQUIRE(p[4] == "Difficulty5");
}
TEST_CASE("F06 post writer mode selects email notice and unresolved service stops retain effects",
          "[session][save-menu]") {
    Fixture mode;
    mode.after_close = [&] { mode.runtime.world().globals.multiplayer = 2; };
    REQUIRE(mode.save() == SaveMenuResult::saved);
    REQUIRE(mode.notices == std::vector<std::string>{"EMAILSAVED"});
    REQUIRE(mode.process.text_parameters.strings[0] == "he_b3980.sav");
    Fixture stopped;
    stopped.choose = [](std::string& name) -> std::uint32_t {
        name = "written before stop";
        throw std::runtime_error("external stop");
    };
    REQUIRE_THROWS_AS(stopped.save(), std::runtime_error);
    REQUIRE(stopped.menu.filename == "written before stop");
    REQUIRE(stopped.files.text.working == "search");
    REQUIRE(stopped.calls.back() == "choose");
    REQUIRE(stopped.opened.empty());
    REQUIRE(std::count(stopped.calls.begin(), stopped.calls.end(), "start") == 0);
}
