#include <catch2/catch_test_macros.hpp>

import std;
import openciv.tests.service_callbacks;
import openciv.formats.classic_labels;
import openciv.session.text_configuration;

// Authored P04 section 2 / Appendix B and F03 section 2 expectations.

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
    LabelCallbacks hooks;
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
    state::LabelCatalog read() { return load_labels(reader, directories, hooks); }
    void rebuild() { state::install_labels(read(), pool, labels); }
    std::int32_t load() {
        rebuild();
        return static_cast<std::int32_t>(labels.entries.size());
    }
};
std::string table(std::int32_t count, std::string_view prefix = "Label") {
    std::string result = "@LABELS\n" + std::to_string(count) + "\n";
    for (std::int32_t i = 0; i < count; ++i)
        result += std::string(prefix) + std::to_string(i) + "\n";
    return result;
}
} // namespace

TEST_CASE("P04 classic label indices and popup text resolve authored content",
          "[classic][labels]") {
    Fixture f;
    f.files["scenario/LABELS.TXT"] =
        "@STRINGHEAP\n50000\n" + table(888) + "@POPUPS\nOK\nHelp\nCancel\nUnused\n";
    f.rebuild();
    REQUIRE(f.labels.declared_count == 888);
    REQUIRE(f.labels.entries.size() == 888);
    REQUIRE(f.labels.label(887, f.pool) == "Label887");
    REQUIRE(f.labels.popup(1, f.pool) == "Help");
    REQUIRE_FALSE(f.reader.is_open());
    REQUIRE(f.opens == std::vector<std::string>(2, "scenario/LABELS.TXT"));
    REQUIRE(f.directories.working == "caller");
}
TEST_CASE("P04 short labels append installation rows then share one error label",
          "[classic][labels]") {
    Fixture f;
    f.files["scenario/LABELS.TXT"] = "@LABELS\n2\nScenario,whole;label\n\n";
    f.files["home/LABELS.TXT"] = "@LABELS\n4\nSkipped0\nSkipped1\nHome2\nHome3\n";
    REQUIRE(f.load() == 888);
    REQUIRE(f.labels.declared_count == 2); // Fallback count is not published here.
    REQUIRE(f.labels.label(0, f.pool) == "Scenario,whole;label");
    REQUIRE(f.labels.entries[1] == 0); // Ordinary empty text did not allocate.
    REQUIRE(f.labels.label(1, f.pool) == "<nil>");
    REQUIRE(f.labels.label(2, f.pool) == "Home2");
    REQUIRE(f.labels.label(3, f.pool) == "Home3");
    REQUIRE(f.labels.entries[887] == f.labels.entries[4]);
    REQUIRE(f.labels.label(887, f.pool) == "[ERROR:LABELS.TXT]");
    REQUIRE(f.directories.search == "scenario");
    REQUIRE_FALSE(f.reader.is_open());
}
TEST_CASE("P04 zero and negative label counts retain signed publication and fallback behavior",
          "[classic][labels]") {
    for (const auto count : {0, -3}) {
        Fixture f;
        f.files["scenario/LABELS.TXT"] = table(count);
        f.files["home/LABELS.TXT"] = table(2, "Home");
        REQUIRE(f.load() == 888);
        REQUIRE(f.labels.declared_count == count);
        REQUIRE(f.labels.label(0, f.pool) == "Home0");
        REQUIRE(f.labels.label(1, f.pool) == "Home1");
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
    REQUIRE(f.load() == 0);
    REQUIRE(reports == 1);
    REQUIRE(f.labels.declared_count == 0);
    REQUIRE(f.labels.entries.empty());
    REQUIRE_THROWS_AS(f.labels.label(0, f.pool), std::out_of_range);
    REQUIRE(f.labels.popup(1, f.pool) == "Old help");
    REQUIRE(f.pool.size() == 3);
    REQUIRE_FALSE(f.reader.is_open());
}
TEST_CASE("P04 initialization selects language and ignores original heap-size hints",
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
        auto observe_caches = [&] {
            ++caches;
            REQUIRE(f.directories.extension == extension);
            REQUIRE(f.opens.empty());
            REQUIRE(f.pool.size() == 2);
        };
        session::initialize_text(language, f.directories, f.pool, f.labels, observe_caches);
        REQUIRE(caches == 1);
        const auto expected = language == 0                    ? -1
                              : language == -1                 ? -2
                              : language >= 1 && language <= 3 ? language - 1
                                                               : 0;
        REQUIRE(f.labels.status_language == expected);
        REQUIRE(f.pool.size() == 1);
        REQUIRE(f.labels.entries.size() == 1); // Only rebuild releases labels.
        REQUIRE_THROWS_AS(f.labels.label(0, f.pool), std::out_of_range);
        f.files["scenario/LABELS." + std::string(extension)] = "@STRINGHEAP\n35001\n";
        auto ignore_caches = [] {};
        session::initialize_text(language, f.directories, f.pool, f.labels, ignore_caches);
    }
}
TEST_CASE("P04 label rebuild preserves unrelated rule references", "[classic][labels]") {
    Fixture f;
    f.directories.extension = "GER";
    f.labels.status_language = 2;
    const auto old_rule_binding = f.pool.store("Old rule");
    f.labels.popups[0] = old_rule_binding;
    f.files["scenario/LABELS.GER"] = "@STRINGHEAP\n40000\n" + table(888, "New");
    f.rebuild();
    REQUIRE(f.labels.status_language == 2);
    REQUIRE(f.directories.extension == "GER");
    REQUIRE(f.pool.text(old_rule_binding) == "Old rule");
    REQUIRE(f.labels.popup(0, f.pool) == "Old rule"); // Missing POPUPS retains its value.
}
TEST_CASE("P04 invalid label counts cannot write outside the resource table", "[classic][labels]") {
    for (const bool fallback : {false, true}) {
        Fixture f;
        f.files["scenario/LABELS.TXT"] = table(fallback ? 1 : 889);
        f.files["home/LABELS.TXT"] = table(889, "Home");
        REQUIRE_THROWS_AS(f.load(), std::out_of_range);
        REQUIRE(f.labels.entries.empty());
        REQUIRE(f.pool.size() == 1);
        REQUIRE_FALSE(f.reader.is_open());
        REQUIRE(f.directories.search == "scenario");
    }
}

TEST_CASE("P04 parsed catalogs own their text independently of reader and runtime pools",
          "[classic][labels]") {
    Fixture f;
    f.files["scenario/LABELS.TXT"] = table(2, "Owned");
    auto catalog = f.read();
    CHECK(f.labels.entries.empty());
    CHECK(f.pool.size() == 1);
    f.files.clear();
    f.reader.close();
    state::install_labels(catalog, f.pool, f.labels);
    CHECK(f.labels.label(0, f.pool) == "Owned0");
}
