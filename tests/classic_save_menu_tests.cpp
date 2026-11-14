// F06 semantic file choice and P04 presentation independence.
#include "classic_save_fixture.hpp"
#include "session/classic_save_menu.hpp"
#include <catch2/catch_test_macros.hpp>
using namespace openciv;
using namespace openciv::session;
namespace {
std::string label(std::size_t index) {
    return "Label" + std::to_string(index);
}
struct Fixture : test::ClassicSaveFixture {
    ClassicSaveMenuState menu{true, "previous.sav"};
    SaveMenuServices ui;
    FileChoiceResult choice{FileChoiceStatus::accepted, "chosen.sav", {}};
    std::vector<FileChoiceRequest> requests;
    std::vector<SaveNotice> notices;
    unsigned stops = 0, starts = 0;
    Fixture() {
        files.text.search = "saves";
        for (std::size_t i = 0; i < 6; ++i)
            process.rules->names.difficulties[i] =
                process.rules->text.store("Difficulty" + std::to_string(i));
        runtime.world().globals.year = -3980;
        runtime.world().globals.difficulty = 3;
        process.text_parameters.line = "unrelated shared line";
        process.text_parameters.strings.fill("unrelated shared string");
        ui.output = services;
        ui.stop_countdown = [&](bool notify) {
            CHECK(notify);
            ++stops;
        };
        ui.start_countdown = [&](bool notify) {
            CHECK(notify);
            ++starts;
        };
        ui.local_time = [] { return LocalCalendar{11, 14, 2026, 1, 2, 3}; };
        ui.lowercase = [](std::string_view text) {
            std::string result(text);
            for (auto& c : result)
                if (c >= 'A' && c <= 'Z')
                    c = static_cast<char>(c + 32);
            return result;
        };
        ui.label = label;
        ui.ruler_name = [](std::int32_t civ) { return "Ruler" + std::to_string(civ); };
        ui.ruler_title = [](std::int32_t civ) { return "Title" + std::to_string(civ); };
        ui.civilization_plural = [](std::int32_t civ) { return "Tribe" + std::to_string(civ); };
        ui.format_year = [](std::int32_t year) { return std::to_string(year); };
        ui.error_text = [] { return "Output failed"; };
        ui.notice = [&](const SaveNotice& notice) { notices.push_back(notice); };
        ui.chooser.choose = [&](const FileChoiceRequest& request) {
            requests.push_back(request);
            return choice;
        };
    }
    SaveMenuResult menu_save(bool scenario = false) {
        return save_classic_menu(runtime, files, menu, scenario, ui);
    }
};
} // namespace
TEST_CASE("F06 file types describe supported formats without native buffers",
          "[session][file-choice]") {
    for (std::uint8_t mode = 0; mode < 7; ++mode) {
        const auto types = classic_file_types(mode, false, false, true, label);
        const auto pattern = mode == 0   ? "*.sav"
                             : mode == 1 ? "*.hot"
                             : mode == 2 ? "*.eml"
                                         : "*.net";
        REQUIRE(types.size() == 1);
        CHECK(types[0].pattern == pattern);
        const auto alternatives = classic_file_types(mode, false, false, false, label);
        CHECK(alternatives.front() == types.front());
        CHECK(std::any_of(alternatives.begin(), alternatives.end(),
                          [](const auto& t) { return t.pattern == "*.sav"; }));
    }
    CHECK(classic_file_types(255, true, false, false, label) ==
          std::vector<FileType>{{"Label415", "*.scn"}});
    CHECK(classic_file_types(255, false, true, false, label) ==
          std::vector<FileType>{{"Label261", "*.mp"}});
    CHECK_THROWS_AS(classic_file_types(255, false, false, true, label), std::invalid_argument);
}
TEST_CASE("F06 chooser publishes only accepted valid paths without a native length limit",
          "[session][file-choice]") {
    FileChoiceRequest request;
    request.initial_path = "suggested.sav";
    for (const auto status :
         {FileChoiceStatus::accepted, FileChoiceStatus::cancelled, FileChoiceStatus::failed}) {
        const std::string path(700, 'a');
        const auto result =
            choose_classic_file(request, {[&](const auto& actual) {
                                    CHECK(actual.initial_path == "suggested.sav");
                                    return FileChoiceResult{status, path, "diagnostic"};
                                }});
        CHECK(result.status == status);
        CHECK(result.path == (status == FileChoiceStatus::accepted ? path : ""));
    }
    for (const auto& path : {std::string{}, std::string("bad\0name", 8)})
        CHECK(choose_classic_file(request, {[&](const auto&) {
                                      return FileChoiceResult{FileChoiceStatus::accepted, path, {}};
                                  }})
                  .status == FileChoiceStatus::failed);
}
TEST_CASE("F06 save UI supplies owned semantic notice arguments", "[session][save-menu]") {
    Fixture f;
    const auto random = f.runtime.gameplay_random().checkpoint();
    REQUIRE(f.menu_save() == SaveMenuResult::saved);
    REQUIRE(f.notices.size() == 1);
    CHECK(f.notices[0].key == "SAVEOK");
    CHECK(f.notices[0].arguments ==
          std::array<std::string, 5>{"Title2", "Ruler2", "Tribe2", "-3980", "Difficulty3"});
    CHECK(f.menu.filename == "chosen.sav");
    CHECK(f.path == "chosen.sav");
    CHECK(f.stops == 1);
    CHECK(f.starts == 1);
    CHECK(f.runtime.gameplay_random().checkpoint() == random);
    CHECK(f.process.text_parameters.line == "unrelated shared line");
    CHECK(f.process.text_parameters.strings[0] == "unrelated shared string");
    CHECK(formats::decode_stored_save(f.bytes).globals.year == -3980);
}
TEST_CASE("F06 cancellation and chooser failure never save or publish draft paths",
          "[session][save-menu]") {
    for (auto mode : {0, 1, 2, 3})
        for (bool failure : {false, true}) {
            Fixture f;
            f.runtime.world().globals.multiplayer = static_cast<std::uint8_t>(mode);
            const auto before = f.runtime.world().globals;
            f.choice = {failure ? FileChoiceStatus::failed : FileChoiceStatus::cancelled,
                        "draft.sav", "Chooser failed"};
            CHECK(f.menu_save() == (failure ? SaveMenuResult::failed : SaveMenuResult::cancelled));
            CHECK(f.menu.filename == "previous.sav");
            CHECK(f.opens == 0);
            CHECK(f.stops == 1);
            CHECK(f.starts == 1);
            CHECK(f.runtime.world().globals == before);
            if (failure) {
                REQUIRE(f.notices.size() == 1);
                CHECK(f.notices[0].key == "SAVEERROR");
                CHECK(f.notices[0].arguments[0] == "Chooser failed");
            } else
                CHECK(f.notices.empty());
        }
}
TEST_CASE("F06 save notices distinguish scenario email and output failure",
          "[session][save-menu]") {
    SECTION("scenario") {
        Fixture f;
        f.runtime.world().scenario.name.bytes[0] = 'X';
        CHECK(f.menu_save(true) == SaveMenuResult::saved);
        CHECK(f.notices[0].key == "SCENOK");
        CHECK(f.notices[0].arguments[0] == "X");
    }
    SECTION("email") {
        Fixture f;
        f.runtime.world().globals.multiplayer = 2;
        CHECK(f.menu_save() == SaveMenuResult::saved);
        CHECK(f.notices[0].key == "EMAILSAVED");
        CHECK(f.notices[0].arguments[0] == "chosen.sav");
    }
    SECTION("write or close failure") {
        for (bool close : {false, true}) {
            Fixture f;
            f.fail_close = close;
            f.fail_write = !close;
            CHECK(f.menu_save() == SaveMenuResult::failed);
            CHECK(f.notices[0].key == "SAVEERROR");
            CHECK(f.closes == 1);
            CHECK(f.starts == 1);
        }
    }
}
TEST_CASE("F06 presentation formatting does not change the saved game", "[session][save-menu]") {
    Fixture first, second;
    second.ui.ruler_title = [](auto) { return "Different wording"; };
    second.ui.format_year = [](auto) { return "3980 BC"; };
    CHECK(first.menu_save() == SaveMenuResult::saved);
    CHECK(second.menu_save() == SaveMenuResult::saved);
    CHECK(first.bytes == second.bytes);
    CHECK(first.runtime.gameplay_random().checkpoint() ==
          second.runtime.gameplay_random().checkpoint());
}
TEST_CASE("F06 save UI restarts countdown after exceptional external work",
          "[session][save-menu]") {
    Fixture f;
    f.ui.chooser.choose = [](const auto&) -> FileChoiceResult {
        throw std::runtime_error("Chooser failed");
    };
    CHECK_THROWS_AS(f.menu_save(), std::runtime_error);
    CHECK(f.starts == 1);
    CHECK(f.opens == 0);
    CHECK(f.menu.filename == "previous.sav");
}
TEST_CASE("F06 inactive save UI requires no presentation services", "[session][save-menu]") {
    Fixture f;
    f.menu.session_active = false;
    CHECK(save_classic_menu(f.runtime, f.files, f.menu, false, {}) == SaveMenuResult::inactive);
    CHECK(f.stops == 0);
    CHECK(f.opens == 0);
}
