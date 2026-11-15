#ifdef _WIN32
#define NOMINMAX
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#endif

#include <catch2/catch_test_macros.hpp>

import std;
import openciv.tests.service_callbacks;
import openciv.tests.classic_stream_fixture;
import openciv.platform.classic_files;
import openciv.platform.native_files;
import openciv.session.classic_rules;
import openciv.session.classic_preferences;
import openciv.session.premade_world;
import openciv.formats.preference_io;
import openciv.session.runtime;
import openciv.formats.classic_save;
import openciv.tests.classic_rule_fixture;

// Native F03/F06 adapter cases use independently authored files and rules.

using namespace openciv;
using namespace openciv::formats;
using namespace openciv::session;
namespace classic = formats::classic;
namespace fs = std::filesystem;
namespace {
struct TemporaryDirectory {
    fs::path path;
    TemporaryDirectory() {
        const auto stamp = std::chrono::steady_clock::now().time_since_epoch().count();
        for (unsigned i = 0; i < 100; ++i) {
            path = fs::temp_directory_path() /
                   ("oc2-classic-" + std::to_string(stamp) + "-" + std::to_string(i));
            if (fs::create_directory(path))
                return;
        }
        throw std::runtime_error("Cannot reserve classic test directory");
    }
    ~TemporaryDirectory() {
        std::error_code ignored;
        fs::remove_all(path, ignored);
    }
    void text(const fs::path& relative, std::string_view value) {
        io::write_new_file(path / relative, Bytes(value.begin(), value.end()));
    }
};
StoredSave saved() {
    StoredSave s;
    s.globals.unit_end = s.globals.city_end = 1;
    s.globals.player_civ = s.globals.saved_current_civ = 1;
    s.globals.civs_alive = 2;
    s.map.header = {4, 2, 4, 0, 19, 1, 1};
    for (auto& memory : s.map.remembered)
        memory.resize(4);
    s.map.tiles.resize(4);
    s.map.coarse = {Bytes{3}, Bytes{4}};
    s.units.resize(1);
    s.units[0].token = state::UnitToken{1};
    s.units[0].owner = state::CivilizationSlot{1};
    s.cities.resize(1);
    s.cities[0].token = state::CityToken{1};
    s.cities[0].owner = state::CivilizationSlot{1};
    s.leaders.resize(1);
    return s;
}
} // namespace
#ifdef _WIN32
TEST_CASE("Native classic files accept Windows short directory aliases", "[io][classic-files]") {
    TemporaryDirectory dir;
    dir.text("input.txt", "payload\n");
    const auto required = GetShortPathNameW(dir.path.c_str(), nullptr, 0);
    REQUIRE(required != 0);
    std::wstring buffer(required, L'\0');
    const auto length = GetShortPathNameW(dir.path.c_str(), buffer.data(), required);
    REQUIRE(length != 0);
    REQUIRE(length < required);
    buffer.resize(length);
    const fs::path short_path(buffer);
    if (short_path == dir.path)
        SKIP("DOS short directory names unavailable on this volume");
    io::ClassicFiles files(short_path);
    auto input = files.open_text(".", "input.txt");
    REQUIRE(input.stream);
    CHECK(input.stream->read().characters == "payload\n");
    input.stream->close();
    auto output = files.open_output("new.dat");
    REQUIRE(output);
    CHECK(output->close());
    CHECK(fs::exists(dir.path / "new.dat"));
}
#endif
TEST_CASE("Native F03 preferences replace reload and delete failed output through classic services",
          "[io][classic-files][preferences]") {
    TemporaryDirectory dir;
    io::ClassicFiles files(dir.path);
    state::ProcessContext process;
    PreferenceFileCallbacks services{
        [&](std::string_view path) { return files.open_input(path); },
        [&](std::string_view path) { return files.open_output(path); },
        [&](std::string_view path) { return files.remove_file(path); }};
    load_classic_preferences(process, {1000, true}, services);
    CHECK(process.ordinary_preferences.game_options == 0x3f3258U);
    process.ordinary_preferences.text = std::string(32, static_cast<char>(0xff));
    process.multiplayer_preferences.game_name = std::string(32, static_cast<char>(0xfe));
    io::write_new_file(dir.path / "cIv2.dAt", Bytes(999, 0xaa));
    REQUIRE(save_classic_preferences(process, services) == PreferenceSaveStatus::success);
    const auto bytes = io::read_file(dir.path / "cIv2.dAt");
    REQUIRE(bytes.size() == 516);
    const auto ordinary = process.ordinary_preferences;
    const auto multiplayer = process.multiplayer_preferences;
    process.ordinary_preferences = {};
    process.multiplayer_preferences = {};
    load_classic_preferences(process, {1, false}, services);
    CHECK(process.ordinary_preferences == ordinary);
    CHECK(process.multiplayer_preferences == multiplayer);
    CHECK_FALSE(files.remove_file("missing"));
    CHECK_FALSE(files.remove_file("."));
    CHECK(fs::is_directory(dir.path));
    services.open_output = [&](std::string_view path) {
        auto output = std::shared_ptr<CoreOutput>(files.open_output(path));
        REQUIRE(output);
        return test::output_file({[output](std::span<const Byte> value) {
                                      CHECK(output->item(value.first(1)));
                                      return false;
                                  },
                                  [output] { return output->close(); }});
    };
    CHECK(save_classic_preferences(process, services) == PreferenceSaveStatus::output);
    CHECK_FALSE(fs::exists(dir.path / "cIv2.dAt"));
    CHECK(fs::is_directory(dir.path));
}

TEST_CASE("Native F05 premade loading uses binary files without rewriting their retained payload",
          "[io][classic-files][premade-world]") {
    TemporaryDirectory dir;
    io::ClassicFiles files(dir.path);
    RawMapFile source;
    source.header = {4, 2, 4, 1, 7, 1, 1};
    source.tiles.resize(4, decode_tile(Bytes{0xc2, 0xff, 0xe7, 61, 0xff, 0x39}));
    source.starts.x[20] = 300;
    source.starts.y[20] = -7;
    source.trailing = {0, 0x1a, 0xff, 13, 10};
    const auto bytes = encode_raw_map(source);
    io::write_new_file(dir.path / "MiXeD.Mp", bytes);
    state::ProcessContext process;
    SessionRuntime runtime{process, base::RuntimeId{94}};
    StartingPositions starts;
    PremadeWorldCallbacks services;
    unsigned failures = 0;
    services.open = [&](std::string_view path) { return files.open_input(path); };
    services.load_failure = [&] { ++failures; };
    services.choices.use_seed = [] { return -1; };
    test::fail_service(services.choices.ticks);
    services.choices.use_start_locations = [] { return 0; };
    REQUIRE(load_premade_world(runtime, starts, "mixed.mp", services) == WorldSource::premade_flat);
    CHECK(starts == StartingPositions{});
    CHECK(runtime.world().world_setup.width == 4);
    CHECK(runtime.world().globals.rules == 0x8000);
    for (const auto& tile : runtime.world().map.tiles())
        CHECK(encode_tile(tile) == Bytes{0xc2, 0, 0, 0, 0, 0xf0});
    CHECK(io::read_file(dir.path / "MiXeD.Mp") == bytes);
    io::write_new_file(dir.path / "short.mp", std::span{bytes}.first(100));
    CHECK_FALSE(load_premade_world(runtime, starts, "short.mp", services));
    CHECK(failures == 1);
    CHECK(runtime.world().map.tile({0, 0}).features == 0);
    CHECK_FALSE(runtime.world().map.tile({0, 0}).owner);
    CHECK(io::read_file(dir.path / "short.mp") == Bytes(bytes.begin(), bytes.begin() + 100));
}
TEST_CASE("Native classic input preserves short prefixes binary controls and independent cursors",
          "[io][classic-files]") {
    TemporaryDirectory dir;
    io::write_new_file(dir.path / "MiXeD.sAv", Bytes{0, 0x1a, 0xff, 13, 10});
    io::ClassicFiles files(dir.path);
    auto a = files.open_input("MIXED.SAV"), b = files.open_input("mixed.sav");
    REQUIRE(a);
    REQUIRE(b);
    REQUIRE(a->byte() == 0);
    const auto tail = a->item(9);
    REQUIRE_FALSE(tail.complete);
    REQUIRE(tail.prefix == Bytes{0x1a, 0xff, 13, 10});
    REQUIRE_FALSE(a->byte());
    REQUIRE(a->close());
    REQUIRE_THROWS_AS(a->byte(), std::logic_error);
    REQUIRE(b->item(5).complete);
    REQUIRE(b->close());
    REQUIRE_FALSE(files.open_input("missing"));
    REQUIRE_FALSE(files.open_input("."));
    REQUIRE_FALSE(files.open_output("missing\\output.sav"));
}
TEST_CASE("Native classic replacement truncates existing case alias and retains partial output",
          "[io][classic-files]") {
    TemporaryDirectory dir;
    io::ClassicFiles files(dir.path);
    io::write_new_file(dir.path / "Save.sav", Bytes(300, 0xaa));
    auto output = files.open_output("SAVE.SAV");
    REQUIRE(output);
    REQUIRE(output->item(Bytes{0x1a}));
    REQUIRE(output->item(Bytes{0, 0xff}));
    REQUIRE(output->item(Bytes{13, 10}));
    REQUIRE(output->close());
    REQUIRE(io::read_file(dir.path / "Save.sav") == Bytes{0x1a, 0, 0xff, 13, 10});
    REQUIRE(std::distance(fs::directory_iterator(dir.path), fs::directory_iterator{}) == 1);
    auto partial = files.open_output("SAVE.SAV");
    REQUIRE(partial->item(Bytes{1, 2, 3}));
    partial.reset(); // Resource cleanup also leaves the partial file.
    REQUIRE(io::read_file(dir.path / "Save.sav") == Bytes{1, 2, 3});
}
TEST_CASE("Native classic directories are explicit and text seeks use physical CRLF offsets",
          "[io][classic-files]") {
    TemporaryDirectory dir;
    fs::create_directory(dir.path / "Scenario");
    dir.text("Scenario/rules.txt", "@ONE\r\nfirst\r\n@TWO\r\nsecond\n");
    const auto cwd = fs::current_path();
    io::ClassicFiles files(dir.path);
    files.change_directory("sCeNaRiO");
    REQUIRE(fs::equivalent(files.working(), dir.path / "Scenario"));
    REQUIRE(fs::current_path() == cwd);
    REQUIRE(files.exists(".", "RULES.TXT"));
    auto opened = files.open_text(".", "RULES.TXT");
    REQUIRE(opened.stream);
    REQUIRE(opened.stream->read().characters == "@ONE\n");
    REQUIRE(opened.stream->read().characters == "first\n");
    REQUIRE(opened.stream->seek(13));
    REQUIRE(opened.stream->read().characters == "@TWO\n");
    REQUIRE(opened.stream->seek(0));
    REQUIRE(opened.stream->read().characters == "@ONE\n");
    opened.stream->close();
    REQUIRE_THROWS_AS(opened.stream->read(), std::logic_error);
    files.change_directory("..");
    REQUIRE(fs::equivalent(files.working(), dir.path));
    REQUIRE(fs::current_path() == cwd);
}
TEST_CASE("Native classic text keeps read windows bare carriage returns and explicit control stops",
          "[io][classic-files]") {
    TemporaryDirectory dir;
    dir.text("long.txt", std::string(253, 'x') + "\r\nY\rZ\n");
    io::ClassicFiles files(dir.path);
    auto opened = files.open_text(".", "long.txt");
    REQUIRE(opened.stream);
    REQUIRE(opened.stream->read().characters == std::string(253, 'x') + "\n");
    REQUIRE(opened.stream->read().characters == "Y\rZ\n");
    REQUIRE(opened.stream->read().status == classic::TextReadStatus::end);
    REQUIRE(opened.stream->seek(0));
    REQUIRE(opened.stream->read().status == classic::TextReadStatus::line);
    for (const Byte control : {Byte{0}, Byte{0x1a}}) {
        const auto name = "control" + std::to_string(control) + ".txt";
        io::write_new_file(dir.path / name, Bytes{'A', control, 'B'});
        auto bad = files.open_text(".", name);
        REQUIRE_THROWS_AS(bad.stream->read(), classic::TextError);
    }
}
TEST_CASE("Native F03 section fallback reopens home files without merging scenario text",
          "[io][classic-files]") {
    TemporaryDirectory dir;
    fs::create_directory(dir.path / "Scenario");
    dir.text("Scenario/rules.txt", "@FIRST\r\nscenario\r\n");
    dir.text("RuLeS.TxT", "@FIRST\nhome\n@SECOND\nfallback\n");
    io::ClassicFiles files(dir.path);
    classic::TextWorkspace workspace;
    classic::TextDirectories dirs{io::classic_path(dir.path),
                                  io::classic_path(dir.path / "Scenario"),
                                  io::classic_path(dir.path)};
    classic::TextReader reader(workspace, dirs,
                               {[&](auto d, auto n) { return files.open_text(d, n); }, {}},
                               classic::ascii_text_case());
    REQUIRE(reader.open("rules", "FIRST"));
    REQUIRE(reader.next_line());
    REQUIRE(reader.line() == "scenario");
    REQUIRE(reader.open("rules", "SECOND"));
    REQUIRE(reader.next_line());
    REQUIRE(reader.line() == "fallback");
    REQUIRE(dirs.search == io::classic_path(dir.path / "Scenario"));
}
TEST_CASE("Native core load edit and save bind real labels rules directories and binary streams",
          "[io][classic-files][native-core]") {
    TemporaryDirectory dir;
    fs::create_directory(dir.path / "Scenario");
    dir.text("labels.txt", "@STRINGHEAP\n1000\n@LABELS\n1\nLabel\n@POPUPS\nA\nB\nC\n");
    std::string rules;
    for (const auto& section : classic_rule_fixture::complete_sections())
        rules += section;
    dir.text("rules.txt", rules);
    dir.text("Scenario/game.txt", "@CUSTOM\ntext\n");
    const auto original = encode_stored_save(saved());
    io::write_new_file(dir.path / "Scenario/Start.sav", original);
    io::ClassicFiles native(dir.path);
    ClassicFileState files;
    files.text = {io::classic_path(dir.path), io::classic_path(dir.path),
                  io::classic_path(dir.path)};
    files.default_game_stem = io::classic_path(dir.path / "GAME");
    classic::TextWorkspace workspace;
    state::ProcessContext process;
    SessionRuntime runtime(process, base::RuntimeId{73});
    runtime.begin_client_session({10, 20});
    process.tokens = {100, 200};
    std::size_t sprites = 0, flushes = 0;
    RuleContextCallbacks hooks;
    hooks.exists = [&](auto d, auto n) { return native.exists(d, n); };
    hooks.comparison = classic::ascii_text_case();
    ClassicRuleContext context(files, workspace,
                               {[&](auto d, auto n) { return native.open_text(d, n); }, {}}, hooks);
    CoreLoadCallbacks loading;
    loading.open = [&](auto path) { return native.open_input(path); };
    loading.flush = [&] { ++flushes; };
    loading.content_installed = [&] { ++sprites; };
    loading.change_directory = [&](auto directory) { native.change_directory(directory); };
    loading.exists = hooks.exists;
    loading.comparison = hooks.comparison;
    loading.load_content = [&] { return context.load(); };
    const auto cwd = fs::current_path();
    REQUIRE(load_classic_core(runtime, files, io::classic_path(dir.path / "scenario/start.sav"),
                              loading, "disk/load") == CoreLoadStatus::success);
    REQUIRE(sprites == 1);
    REQUIRE(flushes == 1);
    REQUIRE(files.game_stem == io::classic_path(dir.path / "scenario/GAME"));
    REQUIRE(fs::equivalent(native.working(), dir.path));
    REQUIRE(fs::current_path() == cwd);
    REQUIRE(process.rules->definitions.cosmic.values[0] == 3);
    REQUIRE(runtime.world().entities.units.at(state::UnitSlot{0}).token == state::UnitToken{100});
    runtime.world().globals.turn = 17;
    runtime.world().entities.cities.at(state::CitySlot{0}).food_box = 41;
    const auto random = runtime.gameplay_random().checkpoint();
    CoreSaveCallbacks saving{[&](auto path) { return native.open_output(path); }};
    REQUIRE(save_classic_core(runtime, files, io::classic_path(dir.path / "Edited.SAV"), false,
                              saving) == CoreSaveStatus::success);
    const auto edited = decode_stored_save(io::read_file(dir.path / "Edited.SAV"));
    REQUIRE(edited.globals.turn == 17);
    REQUIRE(edited.cities[0].food_box == 41);
    REQUIRE(edited.units[0].token == state::UnitToken{100});
    REQUIRE(runtime.gameplay_random().checkpoint() == random);
    REQUIRE(io::read_file(dir.path / "Scenario/Start.sav") == original);
    REQUIRE(load_classic_core(runtime, files, io::classic_path(dir.path / "edited.sav"), loading,
                              "disk/reload") == CoreLoadStatus::success);
    REQUIRE(runtime.world().globals.turn == 17);
    REQUIRE(runtime.world().entities.cities.at(state::CitySlot{0}).food_box == 41);
    REQUIRE(runtime.world().entities.units.at(state::UnitSlot{0}).token == state::UnitToken{101});
    REQUIRE(fs::current_path() == cwd);
}
TEST_CASE("Native failed core save reports partial output without live hotseat substitution",
          "[io][classic-files][native-core]") {
    TemporaryDirectory dir;
    const auto target = dir.path / "Partial.HOT";
    io::write_new_file(target, Bytes(500, 0xaa));
    io::ClassicFiles native(dir.path);
    state::ProcessContext process;
    SessionRuntime runtime(process, base::RuntimeId{74});
    runtime.begin_client_session({10, 20});
    auto& world = runtime.world();
    world.globals.multiplayer = state::GameMode::hotseat;
    world.globals.human_players = 2;
    world.multiplayer.hotseat_participants = 6;
    world.multiplayer.local_human = 3;
    world.map.allocate(world::Geometry{4, 2});
    world.map.coarse_grid(0)[0] = 0;
    world.map.coarse_grid(1)[0] = 0;
    ClassicFileState files;
    std::size_t closes = 0, items = 0;
    CoreSaveCallbacks saving;
    saving.open = [&](std::string_view path) {
        auto output = std::shared_ptr<CoreOutput>(native.open_output(path));
        REQUIRE(output);
        return test::output_file({[&, output](std::span<const Byte> bytes) {
                                      ++items;
                                      REQUIRE(bytes.size() > 12);
                                      REQUIRE(output->item(bytes.first(11)));
                                      return false;
                                  },
                                  [&, output] {
                                      ++closes;
                                      REQUIRE(world.globals.human_players == 2);
                                      return output->close();
                                  }});
    };
    REQUIRE(save_classic_core(runtime, files, io::classic_path(target), false, saving) ==
            CoreSaveStatus::output);
    REQUIRE(items == 1);
    REQUIRE(closes == 1);
    REQUIRE(world.globals.saved_current_civ == 3);
    REQUIRE(io::read_file(target) == Bytes{'C', 'I', 'V', 'I', 'L', 'I', 'Z', 'E', 0, 0x1a, 0x2c});
}

TEST_CASE("Native F06 input finalization reports read errors rather than clean EOF",
          "[io][classic-files]") {
    TemporaryDirectory dir;
    io::ClassicFiles files(dir.path);
    auto input = files.open_input(".");
    // Some hosts reject directories at open; POSIX stdio can admit the handle
    // and fail its first read. Neither path is a successfully finalized input.
    if (!input) {
        SUCCEED("Host rejected the directory at open");
        return;
    }
    CHECK_FALSE(input->item(32).complete);
    CHECK_FALSE(input->close());
    CHECK_FALSE(input->close());
}
