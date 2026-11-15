#include <catch2/catch_test_macros.hpp>

import std;
import openciv.formats.builtin_gameplay_data;
import openciv.formats.gameplay;
import openciv.base.hash;

// data-format.md §2; gameplay-format.md §§1–7. Authored contract expectations.

namespace {
constexpr auto& gameplay_test_files = builtin_gameplay_files;
using namespace openciv;
using namespace formats::content;
formats::Bytes bytes(std::string_view s) {
    return {s.begin(), s.end()};
}
struct BundleInput {
    jt::Json manifest;
    std::map<std::string, formats::Bytes> files;
    std::vector<std::string> requests;
    BundleInput() {
        for (const auto& [path, data] : gameplay_test_files)
            files[std::string(path)] = bytes(data);
        manifest = parse_json(gameplay_test_files[0].second, "manifest").value;
    }
    void raw(std::string name, std::string_view text) {
        const auto path = name + ".json";
        files[path] = bytes(text);
        manifest["components"][name]["sha256"] = jt::Json(base::sha256(files[path]));
    }
    void edit(std::string name, const std::function<void(jt::Json&)>& change) {
        const auto& data = files.at(name + ".json");
        auto document = parse_json(
            std::string_view(reinterpret_cast<const char*>(data.data()), data.size()), name);
        change(document.value);
        raw(name, document.value.toString());
    }
    std::shared_ptr<const GameplayBundle> load(bool classic = false) {
        return load_gameplay(
            bytes(manifest.toString()),
            [&](auto path) {
                requests.emplace_back(path);
                return Source{std::string(path), files.at(std::string(path))};
            },
            classic);
    }
    void custom() {
        manifest["mode"] = jt::Json("custom");
        manifest["id"] = jt::Json("authored.variant");
        manifest["revision"] = jt::Json(2);
    }
};
} // namespace

TEST_CASE("Native JSON rejects duplicate keys invalid Unicode and permissive escapes",
          "[content][json]") {
    for (const auto raw :
         {R"({"a":1,"a":2})", R"({"a":1,"\u0061":2})", R"({"nested":{"a":1,"a":1}})",
          R"({"value":"\x41"})", R"({"value":"\ud800"})", R"({"value":"\udc00"})",
          R"({"value":"\ud800\u0041"})", R"({"\udc00":1})", R"({"a":NaN})", R"({"a":Infinity})",
          R"({"a":01})", "[1,]", "{} {}", "{\"a\":\"\xc0\x80\"}", "{\"a\":\"\xed\xa0\x80\"}"}) {
        INFO(raw);
        REQUIRE_THROWS_AS(parse_json(raw, "authored"), Error);
    }
    auto parsed = parse_json(
        R"({"a":"\ud83d\ude00","b":"\u0000","meta":{"same":1},"other":{"same":2}})", "authored");
    REQUIRE(parsed.value["a"].getString() == "\xf0\x9f\x98\x80");
    REQUIRE(parsed.value["b"].getString() == std::string(1, '\0'));
    REQUIRE(parsed.numbers["meta"]["same"].getString() == "1");
    REQUIRE(parsed.numbers["a"].isNull());
    try {
        parse_json(std::string(25, '[') + "0" + std::string(25, ']'), "deep metadata");
        FAIL("expected parser depth limit");
    } catch (const Error& error) {
        REQUIRE(error.code == "resource-limit");
    }
}
TEST_CASE("Native JSON exact integer admission does not follow binary64 rounding",
          "[content][json]") {
    for (const auto token :
         {"-10", "-2147483648", "4294967295", "9007199254740993", "-9223372036854775808"}) {
        auto [status, value] = jt::Json::parse(token);
        REQUIRE(status == jt::Json::success);
        REQUIRE(value.isLong());
        REQUIRE(value.toString() == token);
    }
    REQUIRE(integer32("2147483647.000e0", "value") == 2147483647);
    REQUIRE(integer32("-2147483648e0", "value") == (-2147483647 - 1));
    REQUIRE(integer32("1000e-3", "value") == 1);
    REQUIRE(integer32("-0e9999999999999999999999", "value") == 0);
    REQUIRE(integer_text("9007199254740993", "revision") == "9007199254740993");
    REQUIRE(integer_text("1e100", "revision") == "1" + std::string(100, '0'));
    for (const auto token :
         {"1.00000000000000000000000001", "0.99999999999999999999999999", "1e-9999", "2147483648",
          "-2147483649", "01", "1.", "1e", "true", "NaN"}) {
        INFO(token);
        REQUIRE_THROWS_AS(integer32(token, "value"), Error);
    }
    auto parsed = parse_json(
        R"({"revision":9007199254740993,"value":1.00000000000000000000000001})", "exact");
    REQUIRE(integer_text(parsed.numbers["revision"].getString(), "revision") == "9007199254740993");
    REQUIRE_THROWS_AS(integer32(parsed.numbers["value"].getString(), "value"), Error);
}
TEST_CASE("Native gameplay loads all shipped operands with the trusted semantic digest",
          "[content][gameplay]") {
    BundleInput input;
    const auto original = input.files;
    const auto bundle = input.load(true);
    REQUIRE(bundle->classic_values);
    REQUIRE_FALSE(bundle->custom_mode);
    REQUIRE(bundle->semantic_sha256 == classic_gameplay_sha256);
    REQUIRE(bundle->id == "classic-mge-5.4.0f");
    REQUIRE(bundle->revision == "1");
    REQUIRE(input.requests.size() == 6);
    REQUIRE(input.files == original);
    const auto& v = bundle->values;
    REQUIRE(v.calendar.profiles[0][0].start_year == -4000);
    REQUIRE(v.calendar.warning_year == 2000);
    REQUIRE(v.calendar.end_year == 2020);
    REQUIRE(v.spaceship.parts[0].mass_hundreds_of_tons == 1);
    REQUIRE(v.spaceship.parts[1].mass_hundreds_of_tons == 4);
    REQUIRE(v.spaceship.parts[3].mass_hundreds_of_tons == 16);
    REQUIRE(v.spaceship.parts[0].maximum_count == 39);
    REQUIRE(v.governments[2].required_advance == "advance.monarchy");
    REQUIRE_FALSE(v.governments[0].required_advance);
    REQUIRE(v.governments[6].maximum_rate_tenths == 10);
    REQUIRE(v.difficulties[0].ai_box_factor == 15);
    REQUIRE(v.combat.veteran_bonus == state::Rational{1, 2});
    REQUIRE(v.combat.fortification_factor == 3);
    REQUIRE(v.combat.city_walls_factor == 6);
    REQUIRE(v.scoring.per_unhappy_citizen == -1);
    REQUIRE(v.scoring.conquest.large_map_multiplier == state::Rational{5, 4});
    REQUIRE(v.scoring.rating_difficulty_factors[5] == 13);
}
TEST_CASE("Native gameplay normalizes rational and numeric spellings before classic comparison",
          "[content][gameplay]") {
    BundleInput input;
    const auto baseline = input.load();
    input.custom();
    input.edit("scoring", [](auto& doc) {
        doc["conquest"]["largeMapMultiplier"]["numerator"] = jt::Json(10);
        doc["conquest"]["largeMapMultiplier"]["denominator"] = jt::Json(8);
        doc["extensions"]["org.example.note"] = jt::Json("metadata only");
    });
    INFO(std::string(input.files["scoring.json"].begin(), input.files["scoring.json"].end()));
    auto combat = std::string(gameplay_test_files[5].second);
    const auto position = combat.find("\"partisansAgainstZeroAttackMultiplier\": 8");
    REQUIRE(position != std::string::npos);
    combat.replace(position, std::string("\"partisansAgainstZeroAttackMultiplier\": 8").size(),
                   "\"partisansAgainstZeroAttackMultiplier\": 8.000e0");
    input.raw("combat", combat);
    const auto equivalent = input.load(true);
    REQUIRE(equivalent->custom_mode);
    REQUIRE(equivalent->classic_values);
    REQUIRE(equivalent->semantic_sha256 == baseline->semantic_sha256);
    REQUIRE(equivalent->values == baseline->values);
    REQUIRE(equivalent->files[5].sha256 != baseline->files[5].sha256);
}
TEST_CASE("Native gameplay changed operands require explicit custom mode and fail classic gate",
          "[content][gameplay]") {
    BundleInput input;
    const auto original = input.load();
    input.edit("spaceship",
               [](auto& doc) { doc["parts"]["structural"]["massTons"] = jt::Json(500); });
    REQUIRE_THROWS_AS(input.load(), Error);
    input.custom();
    const auto custom = input.load();
    REQUIRE_FALSE(custom->classic_values);
    REQUIRE(custom->values.spaceship.parts[0].mass_hundreds_of_tons == 5);
    REQUIRE(custom->semantic_sha256 != original->semantic_sha256);
    REQUIRE_THROWS_AS(input.load(true), Error);
    REQUIRE(original->values.spaceship.parts[0].mass_hundreds_of_tons == 1);
}
TEST_CASE(
    "Native gameplay validates descriptors before reading and rejects stale bytes and aliases",
    "[content][gameplay]") {
    BundleInput input;
    input.manifest["components"]["scoring"]["path"] = jt::Json("../escape.json");
    REQUIRE_THROWS_AS(input.load(), Error);
    REQUIRE(input.requests.empty());
    input = BundleInput{};
    input.files["calendar.json"].push_back(' ');
    REQUIRE_THROWS_AS(input.load(), Error);
    REQUIRE(input.requests.size() == 1);
    input = BundleInput{};
    REQUIRE_THROWS_AS(load_gameplay(bytes(input.manifest.toString()),
                                    [&](auto path) {
                                        return Source{"same-backing",
                                                      input.files.at(std::string(path))};
                                    }),
                      Error);
    for (const auto path : {"", "/absolute", "C:/drive", "https://server/file", "a\\b", "a//b",
                            "./file", "a/../b", "a/", "a\nfile"}) {
        INFO(path);
        REQUIRE_THROWS_AS(validate_relative_path(path), Error);
    }
}
TEST_CASE("Native gameplay rejects invalid complete documents without producing a partial bundle",
          "[content][gameplay]") {
    const std::vector<std::pair<std::string, std::function<void(jt::Json&)>>> changes{
        {"calendar",
         [](auto& d) { d["profiles"]["prince"]["periods"][1]["startYear"] = jt::Json(123); }},
        {"calendar",
         [](auto& d) {
             d["profiles"]["prince"]["periods"][0]["turnCount"] = jt::Json(2147483647);
         }},
        {"calendar", [](auto& d) { d["retirement"]["warningYear"] = d["retirement"]["endYear"]; }},
        {"spaceship", [](auto& d) { d["parts"]["fuel"]["massTons"] = jt::Json(401); }},
        {"spaceship", [](auto& d) { d["parts"]["fuel"]["group"] = jt::Json("module"); }},
        {"governments",
         [](auto& d) {
             d["governments"]["anarchy"]["requiredAdvance"] = jt::Json("advance.monarchy");
         }},
        {"difficulty", [](auto& d) { d["difficulties"]["king"]["aiBoxFactor"] = jt::Json(false); }},
        {"combat",
         [](auto& d) { d["defenseMultipliers"]["fortress"]["denominator"] = jt::Json(3); }},
        {"combat", [](auto& d) { d["strengthBonuses"]["veteran"]["denominator"] = jt::Json(0); }},
        {"scoring", [](auto& d) { d["objectiveValue"]["city"] = jt::Json(-1); }},
        {"scoring", [](auto& d) { d["extra"] = jt::Json(1); }},
        {"scoring", [](auto& d) { d["extensions"]["invalid"] = jt::Json(1); }},
        {"scoring", [](auto& d) { d.getObject().erase("peace"); }}};
    for (const auto& [name, change] : changes) {
        INFO(name);
        BundleInput input;
        input.custom();
        input.edit(name, change);
        REQUIRE_THROWS_AS(input.load(), Error);
    }
}
TEST_CASE("Native gameplay retains exact revision metadata and rejects rounded fractional operands",
          "[content][gameplay]") {
    BundleInput input;
    auto manifest = input.manifest.toString();
    const auto position = manifest.find("\"revision\":1");
    REQUIRE(position != std::string::npos);
    manifest.replace(position, 12, "\"revision\":9007199254740993");
    const auto result = load_gameplay(bytes(manifest), [&](auto path) {
        return Source{std::string(path), input.files.at(std::string(path))};
    });
    REQUIRE(result->revision == "9007199254740993");
    for (const auto number : {"1.00000000000000000000001", "1e-400", "2147483648"}) {
        auto combat = std::string(gameplay_test_files[5].second);
        const auto where = combat.find("\"partisansAgainstZeroAttackMultiplier\": 8");
        REQUIRE(where != std::string::npos);
        combat.replace(where, std::string("\"partisansAgainstZeroAttackMultiplier\": 8").size(),
                       std::string("\"partisansAgainstZeroAttackMultiplier\": ") + number);
        input.raw("combat", combat);
        input.custom();
        REQUIRE_THROWS_AS(input.load(), Error);
    }
}
