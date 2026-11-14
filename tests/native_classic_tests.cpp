// Native F03/F06 adapter cases use independently authored files and rules.
#include "platform/classic_files.hpp"
#include "platform/native_files.hpp"
#include "session/classic_rules.hpp"
#include "session/runtime.hpp"
#include "formats/classic_save.hpp"
#include "classic_rule_fixture.hpp"
#include <catch2/catch_test_macros.hpp>
#include <chrono>

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
    REQUIRE(output->byte(0x1a));
    REQUIRE(output->item(Bytes{0, 0xff}));
    REQUIRE(output->text(Bytes{13, 10}));
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
    RuleContextServices hooks;
    hooks.reload_sprites = [&] { ++sprites; }; // Explicit headless resource consumer.
    hooks.flush = [&] { ++flushes; };
    hooks.change_directory = [&](auto d) { native.change_directory(d); };
    hooks.exists = [&](auto d, auto n) { return native.exists(d, n); };
    hooks.comparison = classic::ascii_text_case();
    ClassicRuleContext context(*process.rules, files, workspace,
                               {[&](auto d, auto n) { return native.open_text(d, n); }, {}}, hooks);
    CoreLoadServices loading;
    loading.open = [&](auto path) { return native.open_input(path); };
    loading.flush = hooks.flush;
    loading.change_directory = hooks.change_directory;
    loading.exists = hooks.exists;
    loading.comparison = hooks.comparison;
    loading.load_rules = [&](bool replace) { context.load(replace); };
    loading.reload_table = [&](auto table, bool keep) { context.reload(table, keep); };
    loading.close_rules = [&] { context.close(); };
    const auto cwd = fs::current_path();
    REQUIRE(load_classic_core(runtime, files, io::classic_path(dir.path / "scenario/start.sav"),
                              loading, "disk/load") == CoreLoadStatus::success);
    REQUIRE(sprites == 1);
    REQUIRE(flushes == 8);
    REQUIRE(files.game_stem == io::classic_path(dir.path / "scenario/GAME"));
    REQUIRE(fs::equivalent(native.working(), dir.path));
    REQUIRE(fs::current_path() == cwd);
    REQUIRE_FALSE(context.is_open());
    REQUIRE(process.rules->definitions.cosmic.values[0] == 3);
    REQUIRE(runtime.world().entities.units.at(state::UnitSlot{0}).token == state::UnitToken{100});
    runtime.world().globals.turn = 17;
    runtime.world().entities.cities.at(state::CitySlot{0}).food_box = 41;
    const auto random = runtime.gameplay_random().checkpoint();
    CoreSaveServices saving{[&](auto path) { return native.open_output(path); }};
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
TEST_CASE("Native failed core save leaves truncated prefix and restores hotseat before close",
          "[io][classic-files][native-core]") {
    TemporaryDirectory dir;
    const auto target = dir.path / "Partial.HOT";
    io::write_new_file(target, Bytes(500, 0xaa));
    io::ClassicFiles native(dir.path);
    state::ProcessContext process;
    SessionRuntime runtime(process, base::RuntimeId{74});
    runtime.begin_client_session({10, 20});
    auto& world = runtime.world();
    world.globals.multiplayer = 1;
    world.globals.human_players = 2;
    world.multiplayer.hotseat_participants = 6;
    world.multiplayer.local_human = 3;
    ClassicFileState files;
    std::size_t closes = 0, items = 0;
    CoreSaveServices saving;
    saving.open = [&](std::string_view path) {
        auto output = native.open_output(path);
        REQUIRE(output);
        const auto write = output->item;
        output->item = [&, write](std::span<const Byte> bytes) {
            ++items;
            REQUIRE(bytes.size() == 2); // Inject an actual one-byte version prefix.
            REQUIRE(write(bytes.first(1)));
            return false;
        };
        const auto close = output->close;
        output->close = [&, close] {
            ++closes;
            REQUIRE(world.globals.human_players == 2);
            return close();
        };
        return output;
    };
    REQUIRE(save_classic_core(runtime, files, io::classic_path(target), false, saving) ==
            CoreSaveStatus::output);
    REQUIRE(items == 1);
    REQUIRE(closes == 1);
    REQUIRE(world.globals.saved_current_civ == 3);
    REQUIRE(io::read_file(target) == Bytes{'C', 'I', 'V', 'I', 'L', 'I', 'Z', 'E', 0, 0x1a, 0x2c});
}
