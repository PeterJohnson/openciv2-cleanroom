// Independently authored F03/F06 interoperability driver. Real native services,
// explicit headless resource callbacks and a declared optional fixture edit.
// It does not perform post-load admission, gameplay or original execution.
#include "base/hash.hpp"
#include "formats/classic_save.hpp"
#include "platform/classic_files.hpp"
#include "platform/native_files.hpp"
#include "session/classic_rules.hpp"
#include "session/classic_save.hpp"
#include "session/runtime.hpp"
#include <algorithm>
#include <charconv>
#include <iostream>
#include <map>
#include <json.h>

namespace {
using namespace openciv;
namespace fs = std::filesystem;
std::int32_t integer(std::string_view text) {
    std::int32_t value = 0;
    const auto parsed = std::from_chars(text.data(), text.data() + text.size(), value);
    if (parsed.ec != std::errc{} || parsed.ptr != text.data() + text.size())
        throw std::invalid_argument("Fixture edit requires signed decimal integers");
    return value;
}
fs::path native(std::string_view classic) {
    std::u8string text(classic.begin(), classic.end());
    std::replace(text.begin(), text.end(), u8'\\', u8'/');
    return fs::path(text);
}
jt::Json identity(const fs::path& path) {
    const auto bytes = io::read_file(path);
    jt::Json result;
    result["path"] = jt::Json(io::classic_path(path));
    result["bytes"] = jt::Json(static_cast<std::uint64_t>(bytes.size()));
    result["sha256"] = jt::Json(base::sha256(bytes));
    return result;
}
jt::Json world_summary(const state::GameState& world) {
    jt::Json result;
    result["turn"] = jt::Json(world.globals.turn);
    result["year"] = jt::Json(world.globals.year);
    result["mode"] = jt::Json(world.globals.multiplayer);
    result["playerCiv"] = jt::Json(world.globals.player_civ);
    result["localHuman"] = jt::Json(world.multiplayer.local_human);
    result["gameFlags"] = jt::Json(world.globals.game_flags);
    result["unitSlots"] = jt::Json(world.globals.unit_end);
    result["citySlots"] = jt::Json(world.globals.city_end);
    result["gold"].setArray();
    for (const auto& civ : world.civilizations)
        result["gold"].getArray().emplace_back(civ.gold);
    return result;
}
} // namespace
int main(int argc, char** argv) {
    using namespace openciv;
    using namespace openciv::session;
    namespace classic = formats::classic;
    try {
        if (argc != 4 && argc != 7)
            throw std::invalid_argument(
                "Usage: oc2_live_save HOME INPUT.SAV NEW_OUTPUT.SAV [--gold CIV VALUE]");
        std::optional<std::pair<std::int32_t, std::int32_t>> edit;
        if (argc == 7) {
            if (std::string_view(argv[4]) != "--gold")
                throw std::invalid_argument("Only the declared --gold fixture edit is supported");
            edit = {integer(argv[5]), integer(argv[6])};
            if (edit->first < 0 || edit->first >= 8)
                throw std::invalid_argument("Fixture civilization must be 0..7");
        }
        const auto home = fs::absolute(argv[1]), input = fs::absolute(argv[2]),
                   output = fs::absolute(argv[3]);
        const auto extension = output.extension().string();
        if (extension != ".sav" && extension != ".SAV")
            throw std::invalid_argument("Output must have an explicit .sav or .SAV extension");
        if (fs::exists(output))
            throw std::invalid_argument(
                "Output already exists; this driver requires a new destination");
        // Current single-player files have an independent stored view for the
        // report. Other core versions/modes remain outside this driver's scope.
        const auto source = io::read_file(input);
        const auto stored = formats::decode_stored_save(source);
        if (stored.globals.multiplayer != 0)
            throw std::invalid_argument("This driver admits only mode-zero input");
        io::ClassicFiles disk(home);
        ClassicFileState files;
        files.text = {io::classic_path(home), io::classic_path(home), io::classic_path(home)};
        files.default_game_stem = io::classic_path(home / "GAME");
        files.current_screen = {0, 0, 1280, 800}; // Explicit headless caller input.
        state::ProcessContext process;
        process.tokens = {100, 200};
        SessionRuntime runtime(process, base::RuntimeId{75});
        runtime.begin_client_session({10, 20});
        classic::TextWorkspace workspace;
        std::map<std::string, std::string> text_sources;
        std::size_t sprites = 0, flushes = 0;
        RuleContextServices hooks;
        hooks.exists = [&](auto directory, auto name) { return disk.exists(directory, name); };
        hooks.comparison = classic::ascii_text_case();
        ClassicRuleContext rules(
            files, workspace,
            {[&](auto directory, auto name) {
                 auto opened = disk.open_text(directory, name);
                 if (opened.stream) {
                     const auto hash = base::sha256(io::read_file(native(opened.path)));
                     const auto [found, inserted] = text_sources.emplace(opened.path, hash);
                     if (!inserted && found->second != hash)
                         throw std::runtime_error("Text input changed during the operation");
                 }
                 return opened;
             },
             {}},
            hooks);
        CoreLoadServices loading;
        loading.open = [&](auto path) { return disk.open_input(path); };
        loading.flush = [&] { ++flushes; };
        loading.content_installed = [&] { ++sprites; };
        loading.change_directory = [&](auto directory) { disk.change_directory(directory); };
        loading.exists = hooks.exists;
        loading.comparison = hooks.comparison;
        loading.load_content = [&] { return rules.load(); };
        const auto loaded =
            load_classic_core(runtime, files, io::classic_path(input), loading, "live-driver/load");
        if (loaded != CoreLoadStatus::success)
            throw std::runtime_error("Core load failed with status " +
                                     std::to_string(static_cast<int>(loaded)));
        jt::Json report;
        report["format"] = jt::Json("openciv2-live-save-driver/1");
        report["scope"] =
            jt::Json("F03 wrapper and F06 core load/save; no post-load admission or gameplay");
        report["input"] = identity(input);
        report["loaded"] = world_summary(runtime.world());
        if (edit) {
            auto& civ = runtime.world().civilizations[static_cast<std::size_t>(edit->first)];
            report["fixtureEdit"]["field"] = jt::Json("gold");
            report["fixtureEdit"]["civilization"] = jt::Json(edit->first);
            report["fixtureEdit"]["before"] = jt::Json(civ.gold);
            report["fixtureEdit"]["after"] = jt::Json(edit->second);
            civ.gold = edit->second;
        }
        const auto gameplay = runtime.gameplay_random().checkpoint();
        const auto presentation = runtime.presentation_random().checkpoint();
        // Claim a new destination atomically, then F06 replaces that owned file.
        // After this boundary, a failed save deliberately retains its partial file.
        io::write_new_file(output, {});
        CoreSaveServices saving{[&](auto path) { return disk.open_output(path); }};
        if (save_classic_core(runtime, files, io::classic_path(output), false, saving) !=
            CoreSaveStatus::success)
            throw std::runtime_error("Core save failed; partial output retained");
        if (runtime.gameplay_random().checkpoint() != gameplay ||
            runtime.presentation_random().checkpoint() != presentation)
            throw std::runtime_error("Core save changed a random context");
        if (io::read_file(input) != source)
            throw std::runtime_error("Input changed during the operation");
        report["textSources"].setArray();
        for (const auto& [path, hash] : text_sources) {
            if (base::sha256(io::read_file(native(path))) != hash)
                throw std::runtime_error("Text input changed during the operation");
            jt::Json row;
            row["path"] = jt::Json(path);
            row["sha256"] = jt::Json(hash);
            report["textSources"].getArray().push_back(std::move(row));
        }
        report["saved"] = world_summary(runtime.world());
        report["output"] = identity(output);
        report["outputWritten"] = jt::Json(true);
        report["sourceUnchanged"] = jt::Json(true);
        report["saveRandomContextsUnchanged"] = jt::Json(true);
        report["loadGameplayDraws"] = jt::Json(gameplay.draws);
        report["headlessSpriteCallbacks"] = jt::Json(static_cast<std::uint64_t>(sprites));
        report["flushCallbacks"] = jt::Json(static_cast<std::uint64_t>(flushes));
        report["screen"] = jt::Json("0,0,1280,800");
        report["textSearch"] = jt::Json("uncached native files; ASCII comparison");
        std::cout << report.toString() << '\n';
        if (!std::cout)
            throw std::runtime_error("Output report write failed");
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
