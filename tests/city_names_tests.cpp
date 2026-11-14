// C01 default naming and data-format.md section 5 / Appendix B.
// Authored provider inputs and expected outcomes, not original-game captures.
#include "formats/city_names.hpp"
#include "formats/rules_install.hpp"
#include "rule_fixture.hpp"
#include <catch2/catch_test_macros.hpp>
#include <map>

using namespace openciv;
namespace classic = formats::classic;
namespace content = formats::content;
namespace {
struct Input {
    rule_fixture::Input rules;
    jt::Json names = rule_fixture::json(
        R"({"format":"openciv2-city-names/1","civilizations":[],"fallbackNames":["Fallback One","Fallback Two"]})");
    Input() {
        for (int i = 0; i < 21; ++i) {
            auto entry = rule_fixture::json(R"({"cityNames":["First","Second"]})");
            entry["pluralName"] = jt::Json("tribes " + std::to_string(i));
            names["civilizations"].getArray().push_back(std::move(entry));
        }
    }
    content::ResolvedPack pack(bool overlay = false) const {
        auto out = rules.pack();
        out.components["cityNames"] = {nullptr,
                                       content::parse_json(names.toString(), "city-names.json")};
        if (overlay)
            out.manifests.resize(2);
        return out;
    }
    auto load(bool overlay = false) const {
        return content::load_city_names(pack(overlay), classic::ascii_text_case());
    }
};
struct World {
    state::City city;
    std::array<state::Civilization, 8> civilizations;
    std::array<state::Leader, 21> leaders;
    std::vector<std::int32_t> labels;
    std::string plural = "tribes 0";
    World() {
        city.owner = state::CivilizationSlot{1};
        city.name.bytes.fill(0xa5);
        city.name.bytes[0] = 0;
        leaders[0].plural = state::NameReference{123, true};
    }
    bool run(const city::NameProvider& provider) {
        return city::default_name(
            city, civilizations, leaders,
            [&](std::int32_t id) {
                labels.push_back(id);
                return plural;
            },
            provider);
    }
    std::string name() const {
        const auto bytes = city.name.text();
        return {bytes.begin(), bytes.end()};
    }
};
struct TextInput {
    classic::TextWorkspace workspace;
    classic::TextDirectories directories{"caller", "scenario", "home"};
    std::map<std::string, std::string> files;
    std::vector<std::string> opened;
    std::function<void()> opening;
    classic::TextReader reader() {
        return {workspace,
                directories,
                {[&](std::string_view directory, std::string_view path) -> classic::OpenedText {
                     REQUIRE(directory == directories.working);
                     REQUIRE(path == "CITY.TXT");
                     const auto key = std::string(directory) + "/" + std::string(path);
                     opened.push_back(key);
                     if (opening)
                         opening();
                     const auto found = files.find(key);
                     if (found == files.end())
                         return {};
                     return {key, std::make_unique<classic::MemoryTextStream>(found->second)};
                 },
                 {}},
                classic::ascii_text_case()};
    }
};
void rejected(const Input& input, std::string_view code = "invalid-content") {
    try {
        input.load();
        FAIL("Expected city-name admission failure");
    } catch (const content::Error& error) {
        REQUIRE(error.code == code);
    }
}
} // namespace

TEST_CASE("Native installed leaders supply C01 names while rule reload retains the live cursor",
          "[city][names][content][rules-install]") {
    const auto admitted = Input{}.load();
    auto rules = std::make_unique<state::RuleState>();
    rules->text.initialize();
    content::RuleInstaller install(admitted->rules, *rules);
    install.load(true);
    World world;
    const auto provider = content::city_name_provider(admitted, classic::ascii_text_case());
    REQUIRE(city::default_name(world.city, world.civilizations, *rules, provider));
    REQUIRE(world.name() == "First");
    REQUIRE(rules->leaders[0].city_name_index == 1);
    const auto plural = rules->leaders[0].plural;
    install.load(false);
    REQUIRE(rules->leaders[0].plural == plural);
    REQUIRE(rules->leaders[0].city_name_index == 1);
    REQUIRE(city::default_name(world.city, world.civilizations, *rules, provider));
    REQUIRE(world.name() == "Second");
    install.load(true);
    REQUIRE(rules->leaders[0].plural != plural);
    REQUIRE(rules->leaders[0].city_name_index == 2);
    REQUIRE(city::default_name(world.city, world.civilizations, *rules, provider));
    REQUIRE(world.name() == "Fallback One");
    REQUIRE(rules->leaders[0].city_name_index == 3);
}

TEST_CASE("C01 native and classic providers preserve ordered names residual fallback and wrap",
          "[city][names][content][classic]") {
    Input input;
    input.names["civilizations"][0]["pluralName"] = jt::Json("TrIbEs 0");
    const auto admitted = input.load();
    REQUIRE(admitted->starting_lists_complete);
    auto native_provider = content::city_name_provider(admitted, classic::ascii_text_case());
    for (const unsigned count : {0U, 1U, 2U, 3U, 4U, 254U, 255U}) {
        CAPTURE(count);
        TextInput text;
        text.files["scenario/CITY.TXT"] =
            "@TrIbEs 0\r\nFirst\r\nSecond\r\n@stop ends\r\n@EXTRA\r\nFallback One\r\nFallback "
            "Two\r\n@STOP\r\n";
        auto reader = text.reader();
        auto classic_provider =
            classic::city_name_provider(reader, text.directories, classic::ascii_text_case());
        World native, original;
        native.leaders[0].city_name_index = static_cast<std::uint8_t>(count);
        original.leaders[0].city_name_index = static_cast<std::uint8_t>(count);
        REQUIRE(native.run(native_provider));
        REQUIRE(original.run(classic_provider));
        REQUIRE(native.city.name == original.city.name);
        REQUIRE(native.leaders == original.leaders);
        REQUIRE(native.labels == std::vector<std::int32_t>{123});
        REQUIRE_FALSE(reader.is_open());
        REQUIRE(text.directories.working == "home");
        const std::string expected = count == 255 ? "@TrIbEs 0"
                                     : count == 1 ? "Second"
                                     : count == 2 ? "Fallback One"
                                     : count == 3 ? "Fallback Two"
                                                  : "First";
        REQUIRE(native.name() == expected);
        const auto final_count = count == 255 ? 0U : count >= 4 ? 1U : count + 1;
        REQUIRE(native.leaders[0].city_name_index == final_count);
    }
}
TEST_CASE("C01 successful publication truncates classic bytes clears tail and keeps duplicates",
          "[city][names][content][classic]") {
    Input input;
    input.names["civilizations"][0]["cityNames"] =
        rule_fixture::json(R"(["12345678901234é tail","12345678901234é tail","X"])");
    auto provider = content::city_name_provider(input.load(), classic::ascii_text_case());
    World world;
    REQUIRE(world.run(provider));
    REQUIRE(world.name() == std::string("12345678901234\xe9"));
    REQUIRE(world.city.name.bytes[15] == 0);
    const auto first = world.city.name;
    REQUIRE(world.run(provider));
    REQUIRE(world.city.name == first);
    REQUIRE(world.run(provider));
    REQUIRE(world.name() == "X");
    for (std::size_t i = 1; i < 16; ++i)
        REQUIRE(world.city.name.bytes[i] == 0);
    TextInput text;
    text.files["scenario/CITY.TXT"] = "@tribes 0\n12345678901234\xe9 tail\n@STOP\n";
    auto reader = text.reader();
    auto classic_provider =
        classic::city_name_provider(reader, text.directories, classic::ascii_text_case());
    World classic_world;
    REQUIRE(classic_world.run(classic_provider));
    REQUIRE(classic_world.city.name == first);
}
TEST_CASE("C01 unavailable plural lists preserve the increment and old name payload",
          "[city][names][content][classic]") {
    Input input;
    auto provider = content::city_name_provider(input.load(), classic::ascii_text_case());
    World world;
    world.plural = "Renamed civilization";
    world.leaders[0].city_name_index = 9;
    const auto before = world.city.name;
    REQUIRE_FALSE(world.run(provider));
    REQUIRE(world.city.name == before);
    REQUIRE(world.leaders[0].city_name_index == 10);
    TextInput text;
    text.files["scenario/CITY.TXT"] = "@Other\nUnused\n@EXTRA\nUnused fallback\n";
    auto reader = text.reader();
    auto classic_provider =
        classic::city_name_provider(reader, text.directories, classic::ascii_text_case());
    World classic_world;
    classic_world.plural = world.plural;
    classic_world.leaders[0].city_name_index = 9;
    REQUIRE_FALSE(classic_world.run(classic_provider));
    REQUIRE(classic_world.city.name == before);
    REQUIRE(classic_world.leaders[0].city_name_index == 10);
    REQUIRE_FALSE(reader.is_open());
    REQUIRE(text.directories.working == "home");
}
TEST_CASE("C01 exhausted lists reset and retry twice while absent fallback stays distinct",
          "[city][names][content][classic]") {
    for (const bool absent : {false, true}) {
        Input input;
        input.names["civilizations"][0]["cityNames"].setArray();
        input.names["fallbackNames"] = absent ? jt::Json() : rule_fixture::json("[]");
        const auto admitted = input.load(true);
        REQUIRE(admitted->lists.fallback.has_value() == !absent);
        REQUIRE_FALSE(admitted->starting_lists_complete);
        auto native = content::city_name_provider(admitted, classic::ascii_text_case());
        unsigned attempts = 0, closes = 0;
        const city::NameProvider observed{[&](auto name, auto count) {
                                              ++attempts;
                                              return native.attempt(name, count);
                                          },
                                          [&] {
                                              ++closes;
                                              native.close();
                                          }};
        World world;
        world.leaders[0].city_name_index = 11;
        const auto before = world.city.name;
        REQUIRE_FALSE(world.run(observed));
        REQUIRE(attempts == 2);
        REQUIRE(closes == 1);
        REQUIRE(world.leaders[0].city_name_index == 0);
        REQUIRE(world.city.name == before);
        TextInput text;
        text.files["scenario/CITY.TXT"] =
            "@tribes 0\n@STOP\n" + (absent ? std::string{} : std::string("@EXTRA\n@STOP\n"));
        auto reader = text.reader();
        auto classic_provider =
            classic::city_name_provider(reader, text.directories, classic::ascii_text_case());
        World classic_world;
        classic_world.leaders[0].city_name_index = 11;
        REQUIRE_FALSE(classic_world.run(classic_provider));
        REQUIRE(classic_world.leaders == world.leaders);
        REQUIRE(classic_world.city.name == before);
        REQUIRE(text.opened.size() == 2);
        REQUIRE_FALSE(reader.is_open());
    }
}
TEST_CASE("C01 owner and plural are captured but counters follow live tribe changes",
          "[city][names]") {
    World world;
    world.leaders[0].city_name_index = 7;
    world.leaders[2].city_name_index = 19;
    world.leaders[2].plural = state::NameReference{456, true};
    world.civilizations[3].tribe = 4;
    world.leaders[4].city_name_index = 99;
    unsigned calls = 0, closes = 0;
    city::NameProvider provider{[&](std::string_view plural, std::uint8_t count) {
                                    REQUIRE(plural == "tribes 0");
                                    if (!calls++) {
                                        REQUIRE(count == 8);
                                        REQUIRE(world.leaders[0].city_name_index == 8);
                                        world.city.owner = state::CivilizationSlot{3};
                                        world.civilizations[1].tribe = 2;
                                        world.plural = "Changed";
                                        return city::NameAttempt{city::NameOutcome::exhausted, {}};
                                    }
                                    REQUIRE(count == 1);
                                    REQUIRE(world.leaders[2].city_name_index == 1);
                                    return city::NameAttempt{city::NameOutcome::selected,
                                                             "Published"};
                                },
                                [&] { ++closes; }};
    REQUIRE(world.run(provider));
    REQUIRE(world.name() == "Published");
    REQUIRE(world.leaders[0].city_name_index == 8);
    REQUIRE(world.leaders[2].city_name_index == 1);
    REQUIRE(world.leaders[4].city_name_index == 99);
    REQUIRE(world.labels == std::vector<std::int32_t>{123});
    REQUIRE(closes == 1);
}
TEST_CASE("C01 opening callbacks precede resets and missing EXTRA reopens only on retry",
          "[city][names][classic]") {
    TextInput text;
    text.files["scenario/CITY.TXT"] = "@tribes 0\nFirst\n@STOP\n";
    World world;
    world.leaders[0].city_name_index = 1;
    world.leaders[2].city_name_index = 77;
    unsigned opened = 0;
    text.opening = [&] {
        if (!opened++) {
            REQUIRE(world.leaders[0].city_name_index == 2);
            world.civilizations[1].tribe = 2;
        } else
            REQUIRE(world.leaders[2].city_name_index == 1);
    };
    auto reader = text.reader();
    const auto provider =
        classic::city_name_provider(reader, text.directories, classic::ascii_text_case());
    REQUIRE(world.run(provider));
    REQUIRE(world.name() == "First");
    REQUIRE(world.leaders[0].city_name_index == 2);
    REQUIRE(world.leaders[2].city_name_index == 1);
    REQUIRE(opened == 2);
    REQUIRE(text.opened == std::vector<std::string>(2, "scenario/CITY.TXT"));
}
TEST_CASE("C01 classic fallback selection retains the chosen file and leaves home directory",
          "[city][names][classic]") {
    TextInput text;
    text.files["scenario/CITY.TXT"] = "@Other\nScenario unused\n@EXTRA\nWrong\n@STOP\n";
    text.files["home/CITY.TXT"] = "@TrIbEs 0\nHome\n@STOP\n@EXTRA\nHome extra\n@STOP\n";
    auto reader = text.reader();
    const auto provider =
        classic::city_name_provider(reader, text.directories, classic::ascii_text_case());
    World world;
    world.leaders[0].city_name_index = 1;
    REQUIRE(world.run(provider));
    REQUIRE(world.name() == "Home extra");
    REQUIRE(text.opened == std::vector<std::string>{"scenario/CITY.TXT", "home/CITY.TXT"});
    REQUIRE(text.directories.working == "home");
    REQUIRE(text.directories.search == "scenario");
    REQUIRE_FALSE(reader.is_open());
}
TEST_CASE("C01 provider failures preserve reached effects without invented normal cleanup",
          "[city][names][classic]") {
    World world;
    world.leaders[0].plural = state::NameReference{32768, true};
    unsigned closed = 0;
    city::NameProvider provider{[&](auto, auto) -> city::NameAttempt {
                                    world.city.name.bytes[4] = 77;
                                    throw std::runtime_error("supplied provider failure");
                                },
                                [&] { ++closed; }};
    REQUIRE_THROWS_AS(world.run(provider), std::runtime_error);
    REQUIRE(world.labels == std::vector<std::int32_t>{32768});
    REQUIRE(world.leaders[0].city_name_index == 1);
    REQUIRE(world.city.name.bytes[4] == 77);
    REQUIRE(closed == 0);
    TextInput text;
    text.files["scenario/CITY.TXT"] = "@tribes 0\nOnly\n"; // No explicit terminator.
    auto reader = text.reader();
    auto classic_provider =
        classic::city_name_provider(reader, text.directories, classic::ascii_text_case());
    World eof;
    eof.leaders[0].city_name_index = 1;
    const auto before = eof.city.name;
    try {
        eof.run(classic_provider);
        FAIL("EXTRA cannot reuse a stream already closed by EOF");
    } catch (const classic::TextError& error) {
        REQUIRE(error.fault == classic::TextFault::unavailable_stream);
    }
    REQUIRE(eof.leaders[0].city_name_index == 2);
    REQUIRE(eof.city.name == before);
}
TEST_CASE("C01 live reference backing failures stop after the applicable prefix", "[city][names]") {
    World world;
    world.civilizations[1].tribe = -1;
    unsigned attempts = 0, closes = 0;
    city::NameProvider provider{[&](auto, auto) {
                                    ++attempts;
                                    world.civilizations[1].tribe = 21;
                                    return city::NameAttempt{city::NameOutcome::exhausted, {}};
                                },
                                [&] { ++closes; }};
    REQUIRE_THROWS_AS(world.run(provider), std::out_of_range);
    REQUIRE(world.labels.empty());
    REQUIRE(attempts == 0);
    world.civilizations[1].tribe = 0;
    REQUIRE_THROWS_AS(world.run(provider), std::out_of_range);
    REQUIRE(world.leaders[0].city_name_index == 1);
    REQUIRE(attempts == 1);
    REQUIRE(closes == 0);
}
TEST_CASE("Native city-name admission rejects ambiguous selectors missing base lists and bad text",
          "[city][names][content]") {
    Input input;
    const auto original = input.names;
    input.names["civilizations"][1]["pluralName"] = jt::Json("TRIBES 0");
    rejected(input);
    input.names = original;
    input.names["civilizations"][0]["cityNames"].setArray();
    rejected(input);
    input.names = original;
    input.names["civilizations"].getArray().erase(input.names["civilizations"].getArray().begin());
    rejected(input);
    for (const auto& invalid :
         {std::string{}, std::string("@sToP and more"), std::string(" padded"),
          std::string("padded\t"), std::string("Two\nLines"), std::string("Null\0Byte", 9),
          std::string("EOF\x1a"), std::string(255, 'x'), std::string("Unrepresentable ☃")}) {
        input.names = original;
        input.names["fallbackNames"][0] = jt::Json(invalid);
        rejected(input, invalid == "Unrepresentable ☃" ? "text-encoding" : "invalid-content");
    }
    input.names = original;
    input.names["civilizations"][0]["pluralName"] = jt::Json("Café");
    input.names["civilizations"][1]["pluralName"] = jt::Json("CAFÉ");
    rejected(input, "comparison-unavailable");
    input.names = original;
    input.names["fallbackNames"][0] = jt::Json(std::string(254, 'x'));
    REQUIRE(input.load()->lists.fallback->front().classic.size() == 254);
}
