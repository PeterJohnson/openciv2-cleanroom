// F03 section 8 and data-format.md section 5 / Appendix B; authored inputs.
#include "rule_fixture.hpp"
#include "formats/city_preferences.hpp"
#include "rules/city_preferences.hpp"
#include <catch2/catch_test_macros.hpp>
#include <algorithm>

using namespace openciv;
namespace classic = formats::classic;
namespace content = formats::content;
namespace {
struct PreferencesFixture {
    rule_fixture::Input input;
    jt::Json preferences = rule_fixture::json(
        R"({"format":"openciv2-city-preferences/1","humanCityDefensePriority":"unchanged","buildPreferences":["improvements.authored.2","improvements.authored.1","improvements.authored.2"]})");
    content::ResolvedPack pack() const {
        auto selected = input.pack();
        selected.components["cityPreferences"] = {
            nullptr, content::parse_json(preferences.toString(), "preferences.json")};
        return selected;
    }
    auto load() const { return content::load_city_preferences(pack(), classic::ascii_text_case()); }
};
state::CityPreferences retained() {
    state::CityPreferences state;
    state.count = 9;
    state.no_defend = 7;
    state.build_order.fill(1234);
    return state;
}
class ObservedStream final : public classic::TextStream {
public:
    ObservedStream(std::string bytes, std::function<void(unsigned)> callback,
                   std::shared_ptr<unsigned> reads)
        : memory_(std::move(bytes)), callback_(std::move(callback)), reads_(std::move(reads)) {}
    classic::TextRead read() override {
        ++*reads_;
        if (callback_)
            callback_(*reads_);
        return memory_.read();
    }
    bool seek(std::size_t position) override { return memory_.seek(position); }
    void close() override { memory_.close(); }

private:
    classic::MemoryTextStream memory_;
    std::function<void(unsigned)> callback_;
    std::shared_ptr<unsigned> reads_;
};
struct TextInput {
    classic::TextWorkspace workspace;
    classic::TextDirectories directories{"caller", "scenario", "home"};
    std::optional<std::string> bytes;
    std::function<void(unsigned)> callback;
    std::shared_ptr<unsigned> last_reads;
    classic::TextReader reader() {
        return {workspace,
                directories,
                {[&](std::string_view, std::string_view name) -> classic::OpenedText {
                     REQUIRE(name == "CITYPREF.TXT");
                     if (!bytes)
                         return {};
                     last_reads = std::make_shared<unsigned>(0);
                     return {"authored/CITYPREF.TXT",
                             std::make_unique<ObservedStream>(*bytes, callback, last_reads)};
                 },
                 {}},
                classic::ascii_text_case()};
    }
};
} // namespace

TEST_CASE("Native and classic city preferences share order duplicates and retained state",
          "[content][classic][preferences]") {
    for (const bool deprioritize : {false, true}) {
        PreferencesFixture f;
        f.preferences["humanCityDefensePriority"] =
            jt::Json(deprioritize ? "deprioritize" : "unchanged");
        const auto native = f.load();
        REQUIRE(native->update.build_order == std::vector<std::uint8_t>{2, 1, 2});
        auto native_state = retained();
        rules::apply_city_preferences(native->update, native_state);
        TextInput input;
        input.bytes = (deprioritize ? std::string("@NODEFEND\r\n") : std::string{}) +
                      "@AUTOBUILD\r\nImPrOvEmEnTs 2,ignored\r\nimprovements 1\r\nimprovements 2";
        auto reader = input.reader();
        auto classic_state = retained();
        classic::load_city_preferences(reader, native->rules->definitions, classic_state,
                                       classic::ascii_text_case());
        REQUIRE(classic_state == native_state);
        REQUIRE(classic_state.count == 3);
        REQUIRE(classic_state.no_defend == (deprioritize ? 1 : 7));
        REQUIRE(classic_state.build_order[3] == 1234);
        REQUIRE_FALSE(reader.is_open());
        REQUIRE(input.directories.working == "caller");
    }
}
TEST_CASE("Native city preferences reject unresolved slots and first name collisions",
          "[content][preferences]") {
    PreferencesFixture f;
    for (const auto id : {"missing", "improvements.authored.0", "improvements.authored.39"}) {
        f.preferences["buildPreferences"] = rule_fixture::json("[\"" + std::string(id) + "\"]");
        REQUIRE_THROWS_AS(f.load(), content::Error);
    }
    f.preferences["buildPreferences"] = rule_fixture::json(R"(["improvements.authored.2"])");
    f.input.rules["improvements"][2]["name"] = jt::Json("IMPROVEMENTS 1");
    try {
        f.load();
        FAIL("Expected a classic first-match collision");
    } catch (const content::Error& error) {
        REQUIRE(std::string(error.what()).find("earlier improvement") != std::string::npos);
    }
    // The first matching identity remains representable; duplicate rows persist.
    f.preferences["buildPreferences"] =
        rule_fixture::json(R"(["improvements.authored.1","improvements.authored.1"])");
    REQUIRE(f.load()->update.build_order == std::vector<std::uint8_t>{1, 1});
    f.preferences["buildPreferences"] = rule_fixture::json(R"(["improvements.authored.2"])");
    f.input.rules["improvements"][2]["name"] = jt::Json("");
    REQUIRE_THROWS_AS(f.load(), content::Error);
    f.input.rules["improvements"][1]["name"] = jt::Json("Caf\xc3\xa9");
    f.input.rules["improvements"][2]["name"] = jt::Json("Caf\xc3\x89");
    try {
        f.load();
        FAIL("Unestablished folding must not silently select a slot");
    } catch (const content::Error& error) {
        REQUIRE(error.code == "comparison-unavailable");
    }
}
TEST_CASE("City preference updates distinguish empty and unchanged and validate before applying",
          "[content][preferences]") {
    PreferencesFixture f;
    f.preferences["buildPreferences"].setArray();
    auto state = retained();
    rules::apply_city_preferences(f.load()->update, state);
    REQUIRE(state.count == 0);
    REQUIRE(state.no_defend == 7);
    REQUIRE(state.build_order == retained().build_order);
    for (int i = 0; i < 33; ++i)
        f.preferences["buildPreferences"].getArray().emplace_back("improvements.authored.1");
    REQUIRE_THROWS_AS(f.load(), content::Error);
    const auto before = state;
    REQUIRE_THROWS_AS(rules::apply_city_preferences({true, {1, 39}}, state), std::invalid_argument);
    REQUIRE(state == before);
    REQUIRE_THROWS_AS(
        rules::apply_city_preferences({true, std::vector<std::uint8_t>(33, 1)}, state),
        std::invalid_argument);
    REQUIRE(state == before);
}
TEST_CASE("Classic city preferences retain NoDefend and stop at the first unmatched name",
          "[classic][preferences]") {
    PreferencesFixture f;
    const auto rules = f.input.load();
    for (const auto& bytes :
         {std::optional<std::string>{},
          std::optional<std::string>{"@AUTOBUILD\n;not a comment to skip\nimprovements 1"},
          std::optional<std::string>{"@AUTOBUILD\nimprovements 39\nimprovements 1"}}) {
        TextInput input;
        input.bytes = bytes;
        auto reader = input.reader();
        auto state = retained();
        classic::load_city_preferences(reader, rules->definitions, state,
                                       classic::ascii_text_case());
        REQUIRE(state.count == 0);
        REQUIRE(state.no_defend == 7);
        REQUIRE(state.build_order == retained().build_order);
        REQUIRE_FALSE(reader.is_open());
    }
    TextInput marker;
    marker.bytes = "@NODEFEND";
    auto reader = marker.reader();
    auto state = retained();
    classic::load_city_preferences(reader, rules->definitions, state, classic::ascii_text_case());
    REQUIRE(state.count == 0);
    REQUIRE(state.no_defend == 1);
    REQUIRE_FALSE(reader.is_open());
}
TEST_CASE("Classic city preferences stop at capacity and preserve the earliest matching identity",
          "[classic][preferences]") {
    PreferencesFixture f;
    auto rules = f.input.load()->definitions;
    rules.improvements[2].identity.name = rules.improvements[1].identity.name;
    TextInput input;
    input.bytes = "@NODEFEND\n@AUTOBUILD\n";
    for (int i = 0; i < 40; ++i)
        *input.bytes += "improvements 1\n";
    auto reader = input.reader();
    auto state = retained();
    classic::load_city_preferences(reader, rules, state, classic::ascii_text_case());
    REQUIRE(state.count == 32);
    REQUIRE(state.no_defend == 1);
    REQUIRE(std::all_of(state.build_order.begin(), state.build_order.end(),
                        [](int slot) { return slot == 1; }));
    REQUIRE(*input.last_reads == 34); // Two headers and exactly 32 preference lines.
    REQUIRE_FALSE(reader.is_open());
}
TEST_CASE("Classic preference lookup reads live names and guards callback corrupted count",
          "[classic][preferences]") {
    PreferencesFixture f;
    auto rules = f.input.load()->definitions;
    TextInput input;
    input.bytes = "@NODEFEND\n@AUTOBUILD\nNew Name\nimprovements 1\n";
    auto state = retained();
    input.callback = [&](unsigned reads) {
        if (reads == 3)
            rules.improvements[2].identity.name.classic = "New Name";
        if (reads == 4)
            state.count = 32;
    };
    auto reader = input.reader();
    REQUIRE_THROWS_AS(
        classic::load_city_preferences(reader, rules, state, classic::ascii_text_case()),
        std::out_of_range);
    REQUIRE(state.no_defend == 1);
    REQUIRE(state.count == 32);
    REQUIRE(state.build_order[0] == 2);
    REQUIRE(state.build_order[1] == 1234);
    REQUIRE(state.build_order.back() == 1234);
    REQUIRE(input.workspace.line() == "improvements 1");
    REQUIRE(reader.is_open()); // Safe stop retains the preceding parsing effects.
}
