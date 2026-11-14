// Authored P04 section 2 / Appendix B and F03 section 2 expectations.
#include "formats/classic_labels.hpp"
#include <catch2/catch_test_macros.hpp>
#include <limits>
#include <map>
#include <new>

using namespace openciv;
using namespace openciv::formats::classic;
namespace {
struct Fixture {
    TextWorkspace workspace;
    TextDirectories directories{"caller", "scenario", "home"};
    state::TextPool pool;
    state::Labels labels;
    std::map<std::string, std::string> files;
    std::vector<std::string> opens;
    std::function<void(std::string_view)> on_open;
    LabelServices hooks;
    TextReader reader;
    Fixture() : reader(workspace, directories, services(), ascii_text_case()) { pool.initialize(); }
    TextFiles services() {
        return {[this](std::string_view directory, std::string_view path) -> OpenedText {
                    const auto key = std::string(directory) + "/" + std::string(path);
                    opens.push_back(key);
                    if (on_open)
                        on_open(key);
                    const auto found = files.find(key);
                    if (found == files.end())
                        return {};
                    return {key, std::make_unique<MemoryTextStream>(found->second)};
                },
                {}};
    }
    LabelLoader loader() { return {reader, directories, pool, labels, hooks}; }
};
std::string table(std::int32_t count, std::string_view prefix = "Label") {
    std::string result = "@LABELS\n" + std::to_string(count) + "\n";
    for (std::int32_t i = 0; i < count; ++i)
        result += std::string(prefix) + std::to_string(i) + "\n";
    return result;
}
} // namespace

TEST_CASE("P04 full labels and popup strings establish the pre-rule ordinal sequence",
          "[classic][labels]") {
    Fixture f;
    f.files["scenario/LABELS.TXT"] =
        "@STRINGHEAP\n50000\n" + table(888) + "@POPUPS\nOK\nHelp\nCancel\nUnused\n";
    f.loader().rebuild();
    REQUIRE(f.labels.pool_capacity == 50000);
    REQUIRE(f.labels.declared_count == 888);
    REQUIRE(f.labels.entries.size() == 888);
    REQUIRE(f.labels.entries.front() == 1);
    REQUIRE(f.labels.entries.back() == 888);
    REQUIRE(f.labels.label(887, f.pool) == "Label887");
    REQUIRE(f.labels.popups == std::array<std::optional<state::TextId>, 3>{889, 890, 891});
    REQUIRE(f.labels.popup(1, f.pool) == "Help");
    REQUIRE(f.pool.size() == 892);
    REQUIRE(f.reader.is_open()); // Popup loading does not close the section file.
    REQUIRE(f.reader.next_line());
    REQUIRE(f.reader.line() == "Unused");
    REQUIRE(f.opens == std::vector<std::string>(3, "scenario/LABELS.TXT"));
    REQUIRE(f.directories.working == "caller");
}
TEST_CASE("P04 short labels append installation rows then share one error label",
          "[classic][labels]") {
    Fixture f;
    f.files["scenario/LABELS.TXT"] = "@LABELS\n2\nScenario,whole;label\n\n";
    f.files["home/LABELS.TXT"] = "@LABELS\n4\nSkipped0\nSkipped1\nHome2\nHome3\n";
    REQUIRE(f.loader().load_labels() == 888);
    REQUIRE(f.labels.declared_count == 2); // Fallback count is not published here.
    REQUIRE(f.labels.label(0, f.pool) == "Scenario,whole;label");
    REQUIRE(f.labels.entries[1] == 0); // Ordinary empty text did not allocate.
    REQUIRE(f.labels.label(1, f.pool) == "<nil>");
    REQUIRE(f.labels.label(2, f.pool) == "Home2");
    REQUIRE(f.labels.label(3, f.pool) == "Home3");
    REQUIRE(f.labels.entries[4] == 4);
    REQUIRE(f.labels.entries[887] == f.labels.entries[4]);
    REQUIRE(f.labels.label(887, f.pool) == "[ERROR:LABELS.TXT]");
    REQUIRE(f.pool.size() == 5);
    REQUIRE(f.directories.search == "scenario");
    REQUIRE_FALSE(f.reader.is_open());
}
TEST_CASE("P04 zero and negative label counts retain signed publication and fallback behavior",
          "[classic][labels]") {
    for (const auto count : {0, -3}) {
        Fixture f;
        f.files["scenario/LABELS.TXT"] = table(count);
        f.files["home/LABELS.TXT"] = table(2, "Home");
        REQUIRE(f.loader().load_labels() == 888);
        REQUIRE(f.labels.declared_count == count);
        REQUIRE(f.labels.label(0, f.pool) == "Home0");
        REQUIRE(f.labels.label(1, f.pool) == "Home1");
        REQUIRE(f.pool.size() == 4);
    }
}
TEST_CASE("P04 missing labels report failure clear the collection and retain popup bindings",
          "[classic][labels]") {
    Fixture f;
    REQUIRE(f.labels.popup(0, f.pool) == "OK");
    REQUIRE(f.labels.popup(1, f.pool) == "Help");
    REQUIRE(f.labels.popup(2, f.pool) == "Cancel");
    f.labels.declared_count = 7;
    f.labels.entries = {f.pool.store("Old label")};
    f.labels.popups[1] = f.pool.store("Old help");
    unsigned reports = 0;
    f.hooks.missing_labels = [&] {
        ++reports;
        REQUIRE(f.labels.declared_count == 7);
        REQUIRE(f.labels.entries.size() == 1);
    };
    auto loader = f.loader();
    REQUIRE(loader.load_labels() == 0);
    REQUIRE(reports == 1);
    REQUIRE(f.labels.declared_count == 0);
    REQUIRE(f.labels.entries.empty());
    REQUIRE_THROWS_AS(f.labels.label(0, f.pool), std::out_of_range);
    loader.load_popups();
    REQUIRE(f.labels.popup(1, f.pool) == "Old help");
    REQUIRE(f.pool.size() == 3);
    REQUIRE_FALSE(f.reader.is_open());
}
TEST_CASE("P04 initialization selects language before cache and raises retained capacity",
          "[classic][labels]") {
    for (const auto language :
         {0, 1, 2, 3, 4, 5, 6, -1, std::numeric_limits<std::int32_t>::min()}) {
        Fixture f;
        static constexpr std::array<std::string_view, 5> extensions{"TXT", "FRE", "GER", "ITA",
                                                                    "SPA"};
        const auto extension = language >= 1 && language <= 5
                                   ? extensions[static_cast<std::size_t>(language - 1)]
                                   : "TXT";
        f.files["scenario/LABELS." + std::string(extension)] = "@STRINGHEAP\n36000\n";
        f.labels.entries = {f.pool.store("Retained until rebuild")};
        unsigned caches = 0;
        f.loader().initialize(language, [&] {
            ++caches;
            REQUIRE(f.directories.extension == extension);
            REQUIRE(f.opens.empty());
            REQUIRE(f.pool.size() == 2);
        });
        REQUIRE(caches == 1);
        const auto expected = language == 0                    ? -1
                              : language == -1                 ? -2
                              : language >= 1 && language <= 3 ? language - 1
                                                               : 0;
        REQUIRE(f.labels.status_language == expected);
        REQUIRE(f.labels.pool_capacity == 36000);
        REQUIRE(f.pool.size() == 1);
        REQUIRE(f.labels.entries.size() == 1); // Only rebuild releases labels.
        REQUIRE_THROWS_AS(f.labels.label(0, f.pool), std::out_of_range);
        f.files["scenario/LABELS." + std::string(extension)] = "@STRINGHEAP\n35001\n";
        f.loader().initialize(language, [] {});
        REQUIRE(f.labels.pool_capacity == 36000);
    }
}
TEST_CASE("P04 rebuild preserves language and rule bindings while resetting the shared pool",
          "[classic][labels]") {
    Fixture f;
    f.directories.extension = "GER";
    f.labels.status_language = 2;
    f.labels.pool_capacity = 60000;
    const auto old_rule_binding = f.pool.store("Old rule");
    f.labels.popups[0] = old_rule_binding;
    f.files["scenario/LABELS.GER"] = "@STRINGHEAP\n40000\n" + table(888, "New");
    f.loader().rebuild();
    REQUIRE(f.labels.pool_capacity == 60000);
    REQUIRE(f.labels.status_language == 2);
    REQUIRE(f.directories.extension == "GER");
    REQUIRE(f.pool.text(old_rule_binding) == "New0");
    REQUIRE(f.labels.popup(0, f.pool) == "New0"); // Missing POPUPS retains numeric binding.
    REQUIRE(f.pool.size() == 889);
}
TEST_CASE("P04 live label count and current table are reread across allocation callbacks",
          "[classic][labels]") {
    Fixture f;
    f.files["scenario/LABELS.TXT"] = table(3, "Scenario");
    f.files["home/LABELS.TXT"] = table(4, "Home");
    f.hooks.allocate_text = [&](std::string_view text, std::size_t) {
        if (text == "Scenario0")
            f.labels.declared_count = 1;
        if (text == "Home1") {
            f.labels.declared_count = 100; // Captured fallback count still ends at four.
            f.labels.entries.assign(888, std::nullopt);
        }
    };
    REQUIRE(f.loader().load_labels() == 888);
    REQUIRE(f.labels.declared_count == 100);
    REQUIRE_FALSE(f.labels.entries[0]); // Callback replaced the live collection.
    REQUIRE(f.labels.label(1, f.pool) == "Home1");
    REQUIRE(f.labels.label(3, f.pool) == "Home3");
    REQUIRE(f.labels.label(4, f.pool) == "[ERROR:LABELS.TXT]");
    REQUIRE(f.pool.size() == 6);
}
TEST_CASE("P04 label safe stops retain allocation and table prefixes without clamping counts",
          "[classic][labels]") {
    for (const bool fallback : {false, true}) {
        Fixture f;
        f.files["scenario/LABELS.TXT"] = table(fallback ? 1 : 889);
        f.files["home/LABELS.TXT"] = table(889, "Home");
        REQUIRE_THROWS_AS(f.loader().load_labels(), std::out_of_range);
        REQUIRE(f.labels.declared_count == (fallback ? 1 : 889));
        REQUIRE(f.labels.entries.size() == 888);
        REQUIRE(f.labels.entries[887] == 888);
        REQUIRE(f.pool.size() == 890); // Last allocation precedes the excluded write.
        REQUIRE(f.directories.search == "scenario");
    }
    Fixture f;
    f.files["scenario/LABELS.TXT"] = table(2);
    f.hooks.allocate_text = [&](std::string_view text, std::size_t) {
        if (text == "Label1")
            f.labels.entries.resize(1);
    };
    REQUIRE_THROWS_AS(f.loader().load_labels(), std::out_of_range);
    REQUIRE(f.labels.entries[0] == 1);
    REQUIRE(f.pool.size() == 3);
}
TEST_CASE("P04 fallback skips the live declared count and restores search before error pooling",
          "[classic][labels]") {
    Fixture f;
    f.files["scenario/LABELS.TXT"] = table(1, "Scenario");
    f.files["home/LABELS.TXT"] = table(2, "Home");
    f.on_open = [&](std::string_view key) {
        if (key == "home/LABELS.TXT") {
            REQUIRE(f.directories.search == "home");
            f.labels.declared_count = 0;
        }
    };
    f.hooks.allocate_text = [&](std::string_view text, std::size_t) {
        if (text == "[ERROR:LABELS.TXT]") {
            REQUIRE(f.directories.search == "scenario");
            throw std::bad_alloc();
        }
    };
    REQUIRE_THROWS_AS(f.loader().load_labels(), std::bad_alloc);
    REQUIRE(f.labels.declared_count == 0);
    REQUIRE(f.labels.label(0, f.pool) == "Scenario0");
    REQUIRE(f.labels.label(1, f.pool) == "Home0");
    REQUIRE_FALSE(f.labels.entries[2]);
    REQUIRE(f.pool.size() == 3);
    REQUIRE(f.directories.search == "scenario");
}
TEST_CASE("P04 label allocation and popup read failures retain only their reached effects",
          "[classic][labels]") {
    Fixture f;
    f.labels.entries = {f.pool.store("Old")};
    f.files["scenario/LABELS.TXT"] = table(3);
    f.hooks.allocate_labels = [&](std::size_t count) {
        REQUIRE(count == 888);
        REQUIRE(f.labels.declared_count == 3);
        throw std::bad_alloc();
    };
    REQUIRE_THROWS_AS(f.loader().load_labels(), std::bad_alloc);
    REQUIRE(f.labels.declared_count == 3);
    REQUIRE(f.labels.label(0, f.pool) == "Old");
    f.files["scenario/LABELS.TXT"] = "@POPUPS\nYes\n";
    REQUIRE_THROWS_AS(f.loader().load_popups(), TextError);
    REQUIRE(f.labels.popup(0, f.pool) == "Yes");
    REQUIRE(f.labels.popups[1] == 0); // First EOF leaves an empty line; next read stops.
    REQUIRE(f.labels.popup(2, f.pool) == "Cancel");
    REQUIRE_FALSE(f.reader.is_open());

    Fixture reset;
    reset.labels.entries = {reset.pool.store("Previous")};
    reset.labels.declared_count = 1;
    reset.files["scenario/LABELS.TXT"] = "@STRINGHEAP\n70000\n";
    reset.hooks.allocate_text = [](std::string_view, std::size_t) { throw std::bad_alloc(); };
    REQUIRE_THROWS_AS(reset.loader().rebuild(), std::bad_alloc);
    REQUIRE(reset.labels.pool_capacity == 70000);
    REQUIRE(reset.pool.size() == 0);
    REQUIRE(reset.labels.entries[0] == 1); // Rebuild never reached table release.
    REQUIRE(reset.labels.declared_count == 1);
    REQUIRE_THROWS_AS(reset.labels.label(0, reset.pool), std::out_of_range);
    REQUIRE_THROWS_AS(reset.loader().load_labels(), std::invalid_argument);
    REQUIRE_THROWS_AS(reset.loader().load_popups(), std::invalid_argument);
}
